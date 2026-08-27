// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022-25
// LICENSE   : GPL-3.0-or-later
// HOMEPAGE  : https://github.com/kuiperzone/AvantGarde
//
// Avant Garde is free software: you can redistribute it and/or modify it under
// the terms of the GNU General Public License as published by the Free Software
// Foundation, either version 3 of the License, or (at your option) any later version.
//
// Avant Garde is distributed in the hope that it will be useful, but WITHOUT
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
// FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License along
// with Avant Garde. If not, see <https://www.gnu.org/licenses/>.
// -----------------------------------------------------------------------------

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Remote.Protocol;
using Avalonia.Remote.Protocol.Designer;
using Avalonia.Remote.Protocol.Viewport;
using Avalonia.Threading;
using AvantGarde.Projects;

namespace AvantGarde.Loading;

/// <summary>
/// Loads a preview using the remote Avalonia remote preview host. The class is non-blocking, with updates
/// arriving by an event.
/// </summary>
public sealed class RemoteLoader : IDisposable
{
    private static readonly Vector Dpi = new(96, 96);
    private const string DotnetHostName = "Avalonia.Designer.HostApp.dll";

    // Milliseconds to wait for a killed host to actually go. Process.Kill only requests the
    // termination, and the shadow copy taken moments later overwrites files the dying process may
    // still have mapped. See StopNoSync.
    private const int ExitTimeout = 2000;

    // Milliseconds to wait for StartDesignerSessionMessage after the host connects. Measured, the
    // message arrives within a few milliseconds of the accept, so this is generous. It is separate
    // from Timeout because that governs process start, and a host which connects but never
    // announces should not stall a preview for the full process-start allowance.
    private const int SessionTimeout = 5000;

    // Prefixes output lines written by AvantGarde itself, so they can be told apart from the host's
    // own stdout and stderr in the same buffer.
    private const string OutputPrefix = "[AvantGarde] ";

    // Frames a second the designer host is allowed to deliver. See FrameRateLimiter: the host waits
    // for each frame to be acknowledged, so this throttles its rendering and not just ours. An
    // animated control renders at about 43 fps unpaced, and a full uncompressed bitmap crosses the
    // socket for every one of them.
    private const int DefaultFrameRate = 30;

    // Dips of slack allowed before a re-derived natural size is taken as a real change. See
    // DeriveNaturalSize - it absorbs the rounding of a frame divided by a fractional DPI, nothing
    // larger.
    private const double NaturalSizeTolerance = 1.5;

    // Ordered by preference. See FindDesignerHost.
    private static readonly string[] HostFrameworkPreference = { "net8.0", "net10.0", "netstandard2.0" };

    private static readonly object _nugetSync = new();
    private static string? _nugetRoot;
    private static bool _nugetRootResolved;

    private readonly object _startSync = new();
    private readonly object _outputSync = new();

    // Guards the scale and the latched natural size. Deliberately separate from _startSync, which
    // UpdateThread holds across the whole of StartHostNoSync - a 10s process wait plus a 5s session
    // wait. Scale used to live under _startSync, which was tolerable while only the scale dropdown
    // touched it; fit-to-window moves it onto the resize path, where a 15s block would be a visible
    // UI freeze. This lock is never held across blocking work, and is always the inner lock: code
    // may take it while holding _startSync, never the reverse.
    private readonly object _viewportSync = new();

    // Guards the pending frame acknowledgement and its pacing. Separate from the others because the
    // ack is written from the transport thread and from a timer callback, and neither has any
    // business waiting on the lifecycle or the viewport. Like _viewportSync it is an inner lock and
    // is never held across blocking work - the send is fired outside it.
    private readonly object _ackSync = new();

    private readonly List<string> _output = new();
    private readonly HashSet<string> _reported = new();

    private volatile bool v_disposed;
    private volatile int v_timeout = 10000;
    private volatile IDisposable? v_listener;
    private volatile Process? v_process;
    private volatile IAvaloniaRemoteTransportConnection? v_connection;
    private volatile PreviewFactory? v_factory = null;
    private volatile int v_maxOutputLines = 100;
    private volatile string? v_sessionId;
    private volatile bool v_sessionStarted;
    private volatile bool v_sessionMismatch;

    // Set when XAML has been sent and no result or frame has come back for it yet. Scale pushes are
    // withheld while it is set - see Scale.
    private volatile bool v_xamlPending;
    private volatile bool v_scalePending;

    private volatile int v_maxFrameRate = DefaultFrameRate;
    private volatile bool v_renderPaused;
    private volatile bool v_shadowCopy;

    // Both under _startSync, which is held across the whole of StartHostNoSync and SendXaml - the
    // only two places either is touched.
    private ShadowCopier? _copier;
    private ShadowPaths? _shadow;

    // All under _viewportSync.
    private double _scale = 1.0;
    private double _naturalWidth = double.NaN;
    private double _naturalHeight = double.NaN;
    private bool _naturalLatched;

    // All under _ackSync. The clock is monotonic and runs for the lifetime of the loader; the
    // connection doubles as the "an ack is pending" flag.
    private readonly Stopwatch _ackClock = Stopwatch.StartNew();
    private readonly System.Threading.Timer _ackTimer;
    private IAvaloniaRemoteTransportConnection? _ackConnection;
    private long _ackSequence;
    private long _ackLast = -1;


    /// <summary>
    /// Constructor.
    /// </summary>
    public RemoteLoader()
    {
        // One timer for the life of the loader, rescheduled rather than recreated. A frame is
        // acknowledged either as it arrives or by this, never both - see AckFrame.
        _ackTimer = new System.Threading.Timer(AckTimerHandler, null,
            System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

        // Deliberately not conditional on shadow copy being enabled, and deliberately not left to
        // the first mirror. A root stranded by a crash would otherwise sit in the temporary
        // directory until some later session happened to turn the option back on. It enumerates
        // and deletes directories, so it does not belong on the calling thread.
        ThreadPool.QueueUserWorkItem(SweepShadowRoots, null);
    }

    /// <summary>
    /// Occurs when preview has been generated. The event is invoked in the UI thread.
    /// </summary>
    public event Action<PreviewPayload>? PreviewReady;

    /// <summary>
    /// Occurs when stdout or stderr has been received. The event is invoked in the UI thread.
    /// </summary>
    public event Action<string>? OutputReceived;

    /// <summary>
    /// Gets or sets an internal process start timeout in milliseconds.
    /// </summary>
    public int Timeout
    {
        get { return v_timeout; }
        set { v_timeout = Math.Max(value, 0); }
    }

    /// <summary>
    /// Gets or sets the maximum number process output lines. A value of 0 or less disables.
    /// </summary>
    public int MaxOutputLines
    {
        get { return v_maxOutputLines; }
        set { v_maxOutputLines = value; }
    }

    /// <summary>
    /// Gets or sets the maximum number of frames a second the designer host may deliver. A value of
    /// 0 or less disables the limit. See <see cref="FrameRateLimiter"/>.
    /// </summary>
    public int MaxFrameRate
    {
        get { return v_maxFrameRate; }
        set { v_maxFrameRate = Math.Min(value, FrameRateLimiter.MaxRate); }
    }

    /// <summary>
    /// Gets or sets whether frame acknowledgement is withheld entirely, which stops the designer
    /// host rendering. Intended for when the preview cannot be seen at all, such as a minimized
    /// window. Clearing it releases the held frame immediately.
    /// </summary>
    /// <remarks>
    /// This is what actually bounds an idle guest. A rate limit cannot: a blinking caret renders
    /// about twice a second, which is under any frame rate worth setting, so the frames go on
    /// regardless. Nothing on this side can stop the guest animating - the only lever is refusing
    /// to take delivery.
    ///
    /// The last frame received is still displayed while paused, and any XAML update sent meanwhile
    /// is still compiled by the host and still reports its errors. Only the picture waits.
    /// </remarks>
    public bool IsRenderPaused
    {
        get { return v_renderPaused; }

        set
        {
            if (v_renderPaused != value)
            {
                v_renderPaused = value;
                Debug.WriteLine("Render paused: " + value);

                if (!value)
                {
                    FlushAck();
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the designer host is run against a temporary copy of the build output
    /// rather than the output itself. A change takes effect the next time the host is started.
    /// </summary>
    /// <remarks>
    /// The host holds every assembly it loads open for its lifetime, so without this a build of
    /// the project being previewed fails on a locked file unless the host is stopped first - which
    /// is why <see cref="Projects.BuildWatcher"/> exists and why the preview comes down on every
    /// build. With it, the only thing the build changes is that a restart is owed afterwards.
    ///
    /// A copy which cannot be taken is reported and the host is started from the output directory
    /// as before, so the failure costs the locking behaviour and not the preview.
    /// </remarks>
    public bool IsShadowCopyEnabled
    {
        get { return v_shadowCopy; }
        set { v_shadowCopy = value; }
    }

    /// <summary>
    /// Gets or sets the scale. Setting a change will cause a new preview to be delivered.
    /// </summary>
    public double Scale
    {
        get { lock (_viewportSync) { return _scale; } }

        set
        {
            bool changed = false;
            value = Math.Max(value, 0.01);

            lock (_viewportSync)
            {
                if (value != _scale)
                {
                    _scale = value;
                    changed = true;
                }
            }

            if (changed)
            {
                if (v_xamlPending)
                {
                    // A XAML update is in flight. Pushing DPI now makes the host render the old
                    // markup at the new scale and then the new markup immediately after, so the
                    // push is deferred to whichever of the result or the frame lands first.
                    Debug.WriteLine("Scale deferred - XAML update in flight");
                    v_scalePending = true;
                }
                else
                {
                    SendScale(v_connection, value);
                }

                var factory = v_factory;

                if (factory?.IsImmediate == true)
                {
                    // Let the thread handle it
                    Update(factory.Load);
                }
            }
        }
    }

    /// <summary>
    /// Gets the natural size of the previewed control in dips, i.e. the size the designer host
    /// renders it at when the scale is 1.0. The value is NaN until a frame has arrived. See
    /// <see cref="DeriveNaturalSize"/>.
    /// </summary>
    public Size NaturalSize
    {
        get { lock (_viewportSync) { return new Size(_naturalWidth, _naturalHeight); } }
    }

    /// <summary>
    /// Gets the the remote process is running.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            var p = v_process;
            return p != null && !p.HasExited;
        }
    }

    /// <summary>
    /// Gets a free TCP port.
    /// </summary>
    public static int GetFreePort()
    {
        Debug.WriteLine(nameof(GetFreePort));
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();

        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();

        Debug.WriteLine("Port: " + port);
        return port;
    }

    /// <summary>
    /// Returns true if the string looks like a version number.
    /// </summary>
    public static bool IsAvaloniaVersion([NotNullWhen(true)] string? version)
    {
        version = version?.Trim();
        return !string.IsNullOrEmpty(version) && char.IsAsciiDigit(version[0]);
    }

    /// <summary>
    /// Returns an array of installed avalonia version numbers, i.e. ["11.0.4", "11.0.5", "11.0.6"].
    /// The result is empty if none are detected.
    /// </summary>
    public static string[] GetInstalledAvaloniaVersions()
    {
        var src = GetAvaloniaPackagesDirectory();

        if (src != null)
        {
            var dir = new DirectoryInfo(src);

            if (dir.Exists)
            {
                var list = new List<string>();

                foreach (var item in new DirectoryInfo(src).EnumerateDirectories("*", new EnumerationOptions()))
                {
                    if (IsAvaloniaVersion(item.Name))
                    {
                        list.Add(item.Name);
                    }
                }

                list.Sort(CompareVersions);

                // Warnings disabled. See editor config
                return list.ToArray();
            }
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Static method which locates fully qualified path of the Avalonia remote preview host.
    /// </summary>
    /// <exception cref="ArgumentException">Version null or empty</exception>
    /// <exception cref="FileNotFoundException">Unable to locate remote preview host</exception>
    public static PathItem FindDesignerHost(string? version)
    {
        // Fallback only. MSBuild states the host path outright - see
        // MsBuildEvaluator.PreviewerToolPathProperty - and that is used in preference. This runs
        // where a project could not be evaluated, and cannot see a NuGet globalPackagesFolder set
        // in nuget.config.
        // ~/.nuget/packages/avalonia/<avalonia-version>/tools/<tfm>/designer/Avalonia.Designer.HostApp.dll
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var src = GetAvaloniaPackagesDirectory();

        if (src != null)
        {
            var tools = Path.Combine(Path.Combine(src, version), "tools");

            // Resolve the known layouts explicitly rather than taking whatever a recursive search
            // happens to reach first. Avalonia 12 ships two designer hosts, net8.0 and net10.0, and
            // net8.0 is preferred because net8 IL rolls forward onto a net10 runtime while the
            // reverse fails outright on System.Runtime. Avalonia's own props defaults to net8.0
            // for the same reason. 11.x ships netstandard2.0 alone.
            foreach (var tfm in HostFrameworkPreference)
            {
                var path = Path.Combine(Path.Combine(Path.Combine(tools, tfm), "designer"), DotnetHostName);

                if (File.Exists(path))
                {
                    return new PathItem(path, PathKind.Assembly);
                }
            }

            // Unknown layout - a version newer than anything anticipated here. Search rather than
            // fail, accepting that the choice is arbitrary where there is more than one candidate.
            var node = new NodeItem(tools, PathKind.Directory);
            node.Properties.ShowEmptyDirectories = false;
            node.Properties.FilePatterns = DotnetHostName;
            node.Refresh();

            var found = node.FindFile(DotnetHostName, StringComparison.OrdinalIgnoreCase);

            if (found != null)
            {
                return found;
            }
        }

        throw new FileNotFoundException($"Unable to locate {DotnetHostName} for version {version}");

    }

    /// <summary>
    /// Updates the XAML content without blocking. The caller is informed of the previews, or any error, via the
    /// <see cref="InvokePreviewReady"/> event.
    /// </summary>
    public void Update(LoadPayload payload)
    {
        Debug.WriteLine($"{nameof(RemoteLoader)}.{nameof(Update)}");
        AssertNotDisposed();
        ThreadPool.QueueUserWorkItem(UpdateThread, payload);
    }

    /// <summary>
    /// Sends pointer event information. It does nothing if events are disabled.
    /// </summary>
    public void SendPointerEvent(PointerEventMessage msg)
    {
        Debug.WriteLineIf(msg.IsPressOrReleased, $"{nameof(RemoteLoader)}.{nameof(SendPointerEvent)}");

        if (IsInputEnabled())
        {
            Debug.WriteLineIf(msg.IsPressOrReleased || msg.IsScrolled, msg);
            Send(v_connection, msg.ToMessage(Scale));
        }
    }

    /// <summary>
    /// Sends keyboard event information. It does nothing if events are disabled.
    /// </summary>
    /// <remarks>
    /// The host delivers these to whatever the guest has focused, and a guest which has not been
    /// clicked has focused nothing - see <see cref="KeyboardEventMessage"/>. Nothing here can detect
    /// that, and there is no acknowledgement to detect it with, so a message sent to an unfocused
    /// guest is silently discarded at the far end.
    /// </remarks>
    public void SendKeyboardEvent(KeyboardEventMessage msg)
    {
        Debug.WriteLine($"{nameof(RemoteLoader)}.{nameof(SendKeyboardEvent)}");

        if (IsInputEnabled())
        {
            Debug.WriteLine(msg);
            Send(v_connection, msg.ToMessage());
        }
    }

    /// <summary>
    /// Gets the process output history. The value may change at any time.
    /// </summary>
    public string? GetProcessOutput()
    {
        var sb = new StringBuilder();

        lock (_outputSync)
        {
            foreach (var s in _output)
            {
                sb.AppendLine(s);
            }
        }

        return sb.Length != 0 ? sb.ToString().TrimEnd() : null;
    }

    /// <summary>
    /// Ensures that the remote preview host is stopped.
    /// </summary>
    public void Stop()
    {
        AssertNotDisposed();

        lock (_startSync)
        {
            StopNoSync();
        }
    }

    /// <summary>
    /// Disposes.
    /// </summary>
    public void Dispose()
    {
        if (!v_disposed)
        {
            try
            {
                v_disposed = true;
                StopNoSync();
                _ackTimer.Dispose();

                // After StopNoSync, which has waited for the host to exit. Disposing the copier
                // deletes the mirrors, and a live host would hold them open.
                _copier?.Dispose();
                _copier = null;
            }
            catch
            {
            }
        }
    }

    private static Avalonia.Platform.PixelFormat ToBitmapFormat(Avalonia.Remote.Protocol.Viewport.PixelFormat fmt)
    {
        switch (fmt)
        {
            case Avalonia.Remote.Protocol.Viewport.PixelFormat.Bgra8888:
                return Avalonia.Platform.PixelFormat.Bgra8888;
            case Avalonia.Remote.Protocol.Viewport.PixelFormat.Rgb565:
                return Avalonia.Platform.PixelFormat.Rgb565;
            case Avalonia.Remote.Protocol.Viewport.PixelFormat.Rgba8888:
                return Avalonia.Platform.PixelFormat.Rgba8888;
            default:
                throw new NotSupportedException("Unsupported pixel format");
        }
    }

    /// <summary>
    /// Compares two version directory names. Needed because an ordinal sort places "11.3.12" below
    /// "11.3.2". Names carrying a pre-release suffix compare on their numeric part first.
    /// </summary>
    private static int CompareVersions(string a, string b)
    {
        if (Version.TryParse(StripPreRelease(a), out Version? va) &&
            Version.TryParse(StripPreRelease(b), out Version? vb))
        {
            int rslt = va.CompareTo(vb);

            if (rslt != 0)
            {
                return rslt;
            }
        }

        return string.CompareOrdinal(a, b);
    }

    private static string StripPreRelease(string version)
    {
        int pos = version.IndexOf('-');
        return pos > 0 ? version.Substring(0, pos) : version;
    }

    private static string? GetAvaloniaPackagesDirectory()
    {
        var src = GetNugetPackagesRoot();

        if (!string.IsNullOrEmpty(src))
        {
            return Path.Combine(src, "avalonia");
        }

        return null;
    }

    /// <summary>
    /// Locates the NuGet global packages folder. The environment variable is only one of three
    /// ways it can be set - a nuget.config globalPackagesFolder is invisible to it, and asking the
    /// CLI is the only reliable way to see that. The CLI call costs around a second, so the answer
    /// is cached for the process lifetime.
    /// </summary>
    private static string? GetNugetPackagesRoot()
    {
        var src = Environment.GetEnvironmentVariable("NUGET_PACKAGES");

        if (!string.IsNullOrEmpty(src))
        {
            return src;
        }

        lock (_nugetSync)
        {
            if (!_nugetRootResolved)
            {
                _nugetRootResolved = true;
                _nugetRoot = QueryNugetPackagesRoot();

                if (string.IsNullOrEmpty(_nugetRoot))
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    _nugetRoot = Path.Combine(Path.Combine(home, ".nuget"), "packages");
                }

                Debug.WriteLine("NuGet packages root: " + _nugetRoot);
            }

            return _nugetRoot;
        }
    }

    private static string? QueryNugetPackagesRoot()
    {
        try
        {
            var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

            if (string.IsNullOrEmpty(dotnet))
            {
                dotnet = "dotnet";
            }

            var info = new ProcessStartInfo
            {
                Arguments = "nuget locals global-packages --list",
                CreateNoWindow = true,
                FileName = dotnet,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var proc = Process.Start(info);

            if (proc == null)
            {
                return null;
            }

            var output = proc.StandardOutput.ReadToEndAsync();

            if (!proc.WaitForExit(15000))
            {
                proc.Kill(true);
                return null;
            }

            if (proc.ExitCode != 0)
            {
                return null;
            }

            // "global-packages: C:\Users\me\.nuget\packages\"
            foreach (var line in output.Result.Split('\n'))
            {
                int pos = line.IndexOf(':');

                if (pos > 0 && line.AsSpan(0, pos).Trim().EndsWith("global-packages", StringComparison.OrdinalIgnoreCase))
                {
                    var path = line.Substring(pos + 1).Trim();

                    if (path.Length != 0 && Directory.Exists(path))
                    {
                        return path;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed to query NuGet packages root: " + e.Message);
        }

        return null;
    }

    /// <summary>
    /// Returns true if user input should be forwarded to the guest. There is nothing to forward to
    /// before the first preview, and the user can turn forwarding off outright.
    /// </summary>
    private bool IsInputEnabled()
    {
        var factory = v_factory;
        return factory != null && !factory.Load.Flags.HasFlag(LoadFlags.DisableEvents);
    }

    private void AssertNotDisposed()
    {
        if (v_disposed)
        {
            throw new ObjectDisposedException(nameof(RemoteLoader));
        }
    }

    private void UpdateThread(object? obj)
    {
        Debug.WriteLine($"{nameof(RemoteLoader)}.{nameof(UpdateThread)}");
        var factory = ((LoadPayload?)obj)?.CreateFactory() ?? throw new ArgumentNullException(nameof(obj));

        lock (_startSync)
        {
            if (v_disposed)
            {
                return;
            }

            var current = v_factory;
            v_factory = null;

            if (v_process != null && (current == null || current.Load.AppAssemblyHashCode != factory.Load.AppAssemblyHashCode))
            {
                // Re-start if app assembly changes
                Debug.WriteLine($"App assembly change: {current?.Load.AppAssemblyHashCode.ToString() ?? "null"}, {factory.Load.AppAssemblyHashCode}");
                StopNoSync();
            }

            try
            {
                if (!IsRunning && !factory.IsImmediate)
                {
                    StartHostNoSync(factory.Load);
                }

                if (factory.IsImmediate)
                {
                    // There will be no reply for this
                    v_factory = factory;
                    InvokePreviewReady(CreateImmediatePreview(factory, Scale));
                }
                else
                {
                    SendXaml(v_connection, factory);
                    v_factory = factory;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("EXCEPTION:" + e);

                // Capture before StopNoSync(), which clears the output buffer. Host startup
                // failures are reported only on the host's stderr, so wiping it here left the
                // user with a bare "Timed out waiting for ..." and no cause.
                var output = GetProcessOutput();
                StopNoSync();

                var payload = CreatePreview(factory, new PreviewError(e.Message));
                payload.Output = output ?? payload.Output;
                InvokePreviewReady(payload);
            }
        }
    }

    private void StopNoSync()
    {
        v_factory = null;

        // The mirror itself is kept - it is what makes the next copy incremental - but the paths
        // belong to the host being stopped.
        _shadow = null;

        // Every host gets its own session. Left set, these would let the next host pass its gate on
        // the previous one's announcement - and a rebuild restarts the host, so that is routine.
        v_sessionId = null;
        v_sessionStarted = false;
        v_sessionMismatch = false;
        v_xamlPending = false;
        v_scalePending = false;

        // Any frame still awaiting acknowledgement belonged to the host being stopped. The clock is
        // reset with it so the next host's first frame is acknowledged at once.
        lock (_ackSync)
        {
            _ackTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            _ackConnection = null;
            _ackLast = -1;
        }

        // The natural size belongs to the control the host had loaded. A new host will restate it.
        lock (_viewportSync)
        {
            _naturalLatched = false;
            _naturalWidth = double.NaN;
            _naturalHeight = double.NaN;
        }

        v_listener?.Dispose();
        v_listener = null;

        var cnx = v_connection;
        v_connection = null;

        if (cnx != null)
        {
            Debug.WriteLine("Dispose of connection");
            cnx.OnMessage -= MessageHandler;
            cnx.OnException -= ErrorHandler;
            cnx.Dispose();
        }

        var proc = v_process;

        if (proc != null && !proc.HasExited)
        {
            try
            {
                Debug.WriteLine("Kill process");
                proc.Kill();

                // Kill only asks. The next shadow copy overwrites the very files the dying process
                // has mapped, and starts within milliseconds of this, so waiting is not optional
                // there - and where the copy is off, waiting costs a few milliseconds of a path
                // which already allows itself ten seconds.
                if (!proc.WaitForExit(ExitTimeout))
                {
                    Debug.WriteLine($"Host still running {ExitTimeout}ms after kill");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to kill process: " + e.Message);
            }
        }

        v_process = null;
        ClearOutput();
    }

    private void StartHostNoSync(LoadPayload load)
    {
        Debug.WriteLine($"{nameof(RemoteLoader)}.{nameof(StartHostNoSync)}");
        Debug.WriteLine("AppAssembly: " + load.AppAssembly);

        if (v_process != null)
        {
            // Not expected here
            Debug.WriteLine("WARNING - existing process");
            StopNoSync();
        }

        var host = ResolveDesignerHost(load);
        Debug.WriteLine("Host: " + host.FullName);

        // Everything below launches from these rather than from the payload, so that the shadow
        // copy is invisible to the rest of the class. The payload keeps stating the real output,
        // which is what change detection and the build watcher must go on seeing.
        _shadow = v_shadowCopy ? CreateShadowNoSync(load) : null;
        var appAssembly = _shadow?.AppAssembly ?? load.AppAssembly;
        var appConfig = _shadow?.AppConfigPath ?? load.AppConfigPath;
        var appDeps = _shadow?.AppDepsPath ?? load.AppDepsPath;

        // Identifies this host instance. The value is echoed back verbatim in
        // StartDesignerSessionMessage (measured against 12.0.5), so a host left over from an
        // earlier run which reaches a recycled port can be told apart from the one just started.
        var session = Guid.NewGuid().ToString();
        v_sessionId = session;
        v_sessionStarted = false;
        v_sessionMismatch = false;

        // Binds the listener as well, because the port number has to go into the host command line.
        var port = StartListenerNoSync();

        // Locate dotnet
        // https://github.com/dotnet/docs/blob/main/docs/core/tools/dotnet-environment-variables.md#dotnet_host_path
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

        if (string.IsNullOrEmpty(dotnet))
        {
            dotnet = "dotnet";
        }

        // --method is passed explicitly rather than relying on the host default. The host supports
        // 'avalonia-remote', 'win32' and 'html'; if the default ever changes we would silently
        // receive HtmlTransportStartedMessage and never a frame.
        var args = $@"exec --runtimeconfig ""{appConfig}"" --depsfile ""{appDeps}"" ""{host}"" --transport tcp-bson://127.0.0.1:{port}/ --method avalonia-remote --session-id ""{session}"" ""{appAssembly}""";

        Debug.WriteLine($"STARTING: {dotnet} {args}");

        var info = new ProcessStartInfo
        {
            Arguments = args,
            CreateNoWindow = true,
            FileName = dotnet,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,

            // Left where upstream put it, and specifically not pointed at either the output
            // directory or the shadow copy. A working directory is an open handle on that
            // directory, which is the one thing the shadow copy exists to avoid holding.
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        var proc = Process.Start(info) ??
            throw new InvalidOperationException($"Failed to start {host.Name} {load.AppAvaloniaVersion}");

        proc.OutputDataReceived += ProcessOutputHandler;
        proc.ErrorDataReceived += ProcessOutputHandler;
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();
#if DEBUG
        proc.EnableRaisingEvents = true;
        proc.Exited += ProcessExitedHandler;
#endif
        v_process = proc;
        Debug.WriteLine("Process started OK");

        // Wait for connection
        if (!SpinWait.SpinUntil(() => { return v_connection != null || v_disposed; }, Timeout))
        {
            StopNoSync();
            throw new TimeoutException("Timed out waiting for " + host.Name);
        }

        Debug.WriteLine("Connection received");

        // Handlers are attached in the listener callback, before v_connection is assigned, so
        // nothing arriving between the accept and here can be dropped. That matters now the first
        // XAML update waits on StartDesignerSessionMessage - losing it would stall every preview
        // for SessionTimeout rather than merely going unnoticed.
        var cnx = v_connection ?? throw new InvalidOperationException($"{nameof(v_connection)} is null");

        var fmt = new ClientSupportedPixelFormatsMessage();
        fmt.Formats = [ Avalonia.Remote.Protocol.Viewport.PixelFormat.Bgra8888,
            Avalonia.Remote.Protocol.Viewport.PixelFormat.Rgba8888,
            Avalonia.Remote.Protocol.Viewport.PixelFormat.Rgb565];

        if (!Send(cnx, fmt) || !SendScale(cnx, Scale))
        {
            throw new InvalidOperationException("Handshake failed to " + host.Name);
        }

        WaitForSessionNoSync(host);
        Debug.WriteLine("Connection OK");
    }

    /// <summary>
    /// Blocks until the host announces its designer session, so that the first UpdateXamlMessage
    /// follows it rather than racing ahead of it. The pixel format and DPI handshake above is left
    /// where it is - it is sent on accept and demonstrably works, and only the XAML update needs
    /// the session.
    /// </summary>
    private void WaitForSessionNoSync(PathItem host)
    {
        SpinWait.SpinUntil(() =>
            { return v_sessionStarted || v_sessionMismatch || v_disposed || !IsRunning; }, SessionTimeout);

        if (v_sessionMismatch)
        {
            throw new InvalidOperationException(
                $"A designer session belonging to another instance answered on the port used for {host.Name}");
        }

        if (!v_sessionStarted && !v_disposed)
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException($"{host.Name} exited before starting a designer session");
            }

            // Fallback. A host which never announces still gets its XAML, because refusing to
            // preview would be a worse failure than sending early - which is what we did before.
            AppendAppOutput($"Warning: {host.Name} did not start a designer session within " +
                $"{SessionTimeout}ms - sending XAML anyway");
        }
    }

    /// <summary>
    /// Binds the transport listener and returns the port it is on. <see cref="GetFreePort"/> closes
    /// its probe listener before this one binds, so another process can take the port in between;
    /// retry rather than fail a preview on a race a second attempt almost certainly wins.
    /// </summary>
    private int StartListenerNoSync()
    {
        Exception? last = null;

        for (int n = 0; n < 5; ++n)
        {
            int port = GetFreePort();

            try
            {
                // Subscribe before publishing the connection. StartHostNoSync spins until
                // v_connection is set, so anything attached after that could miss a message the
                // host sent immediately on connecting - StartDesignerSessionMessage among them.
                v_listener = new BsonTcpTransport().Listen(IPAddress.Loopback, port, c =>
                    {
                        c.OnException += ErrorHandler;
                        c.OnMessage += MessageHandler;
                        v_connection = c;
                    });

                return port;
            }
            catch (SocketException e)
            {
                Debug.WriteLine($"Port {port} unavailable: {e.Message}");
                last = e;
            }
        }

        throw last ?? new InvalidOperationException("Failed to open a listening port");
    }

    /// <summary>
    /// Mirrors the build output and returns the paths to launch from, or null where no copy could
    /// be taken - in which case the caller launches from the output directory as before. Under
    /// <see cref="_startSync"/>, and only ever with the host stopped.
    /// </summary>
    /// <remarks>
    /// Two directories can be involved. The application assembly is one, and where a library
    /// control is being previewed through an application the library's own output is the other,
    /// because <see cref="SendXaml"/> sends the library assembly from its own build directory
    /// rather than the copy of it in the application's.
    /// </remarks>
    private ShadowPaths? CreateShadowNoSync(LoadPayload load)
    {
        var appDir = GetDirectoryOrNull(load.AppAssembly);

        if (appDir == null)
        {
            // Nothing to copy. The launch below fails on the missing assembly, as it would anyway.
            return null;
        }

        try
        {
            var clock = Stopwatch.StartNew();

            // Stale roots are swept once per session from the constructor, not here.
            _copier ??= new ShadowCopier();
            _copier.Mirror(appDir);

            var projDir = GetDirectoryOrNull(load.ProjectAssembly);

            if (projDir != null && !projDir.Equals(appDir, StringComparison.OrdinalIgnoreCase))
            {
                _copier.Mirror(projDir);
            }

            var paths = new ShadowPaths();
            paths.AppAssembly = _copier.Remap(load.AppAssembly) ??
                throw new InvalidOperationException("Failed to shadow " + load.AppAssembly);

            paths.AppConfigPath = _copier.Remap(load.AppConfigPath);
            paths.AppDepsPath = _copier.Remap(load.AppDepsPath);
            paths.ProjectAssembly = _copier.Remap(load.ProjectAssembly);

            Debug.WriteLine($"Shadow copy took {clock.ElapsedMilliseconds}ms");
            return paths;
        }
        catch (Exception e)
        {
            // Reported rather than swallowed. The preview still works without a copy; what is lost
            // is that a build will again fail on the locked output, and the user would otherwise
            // have no way to connect the two.
            AppendAppOutput("Shadow copy failed, running from the build output instead - " + e.Message);
            return null;
        }
    }

    private static void SweepShadowRoots(object? state)
    {
        ShadowCopier.SweepStaleRoots(ShadowCopier.GetDefaultParent(),
            Path.GetFileName(ShadowCopier.GetDefaultRoot()));
    }

    private static string? GetDirectoryOrNull(string? file)
    {
        if (string.IsNullOrEmpty(file))
        {
            return null;
        }

        var dir = Path.GetDirectoryName(file);
        return !string.IsNullOrEmpty(dir) ? dir : null;
    }

    /// <summary>
    /// Returns the designer host to launch. MSBuild's answer is preferred; the NuGet cache lookup
    /// is the fallback for a project that could not be evaluated.
    /// </summary>
    private static PathItem ResolveDesignerHost(LoadPayload load)
    {
        var tool = load.AppPreviewerToolPath;

        if (!string.IsNullOrEmpty(tool) && File.Exists(tool))
        {
            Debug.WriteLine("Host from MSBuild: " + tool);
            return new PathItem(tool, PathKind.Assembly);
        }

        Debug.WriteLine("Find host for Avalonia: " + load.AppAvaloniaVersion);

        if (!IsAvaloniaVersion(load.AppAvaloniaVersion))
        {
            // Neither route has an answer. Say which, rather than reporting a version of "".
            throw new FileNotFoundException(
                "Cannot locate the Avalonia designer host - the project has no evaluated previewer " +
                "path and no detected Avalonia version. Restore and build the project.");
        }

        return FindDesignerHost(load.AppAvaloniaVersion);
    }

    private bool SendScale(IAvaloniaRemoteTransportConnection? cnx, double scale)
    {
        Debug.WriteLine("Send scale: " + scale);
        var msg = new ClientRenderInfoMessage();
        msg.DpiX = Dpi.X * scale;
        msg.DpiY = Dpi.Y * scale;
        return Send(cnx, msg);
    }

    private bool SendXaml(IAvaloniaRemoteTransportConnection? cnx, PreviewFactory factory)
    {
        Debug.WriteLine($"{nameof(RemoteLoader)}.{nameof(SendXaml)}");
        var msg = new UpdateXamlMessage();

        // Under _startSync, which every caller holds. The host resolves this itself, so it has to
        // be the copy the running host was started against and not the project's own output.
        msg.AssemblyPath = _shadow?.ProjectAssembly ?? factory.Load.ProjectAssembly;

        msg.Xaml = factory.GetXaml() ??
            throw new ArgumentNullException(nameof(msg.Xaml));

        // Needs to be rooted against project directory
        // "ie. "/Views/Name.axaml"
        var local = factory.Load.LocalPath;

        if (!string.IsNullOrEmpty(local) && local != factory.Load.FullPath)
        {
            msg.XamlFileProjectPath = '/' + local;
        }

        Debug.WriteLine("AssemblyPath: " + msg.AssemblyPath);
        Debug.WriteLine("XamlFileProjectPath: " + msg.XamlFileProjectPath);

        // A new generation of the markup, so the natural size is derived once more. See
        // DeriveNaturalSize.
        lock (_viewportSync)
        {
            _naturalLatched = false;
        }

        // Cleared again if the send fails. Left set on a send that never happened, every later
        // scale change would be deferred against a reply that cannot arrive, because nothing else
        // clears the flag until the host is restarted.
        v_xamlPending = true;

        if (!Send(cnx, msg))
        {
            v_xamlPending = false;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Clears the in-flight XAML flag and pushes any scale that was withheld while it was set.
    /// </summary>
    private void ClearXamlPending()
    {
        if (v_xamlPending)
        {
            v_xamlPending = false;

            if (v_scalePending)
            {
                v_scalePending = false;
                Debug.WriteLine("Sending deferred scale");
                SendScale(v_connection, Scale);
            }
        }
    }

    private bool Send(IAvaloniaRemoteTransportConnection? cnx, object msg)
    {

        if (cnx != null && IsRunning)
        {
            cnx.Send(msg).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.WriteLine(t.Exception, "FireAndForget: " + t.Exception);
                }
            }, TaskScheduler.Default);

            return true;
        }

        return false;
    }

    private static Bitmap? ToBitmap(FrameMessage frame)
    {
        if (frame.Width > 1 && frame.Height > 1 && frame.Data.Length > 0)
        {
            Debug.WriteLine($"{nameof(RemoteLoader)}.{nameof(ToBitmap)}");
            var data = Marshal.AllocHGlobal(frame.Data.Length);

            try
            {
                Debug.WriteLine("Create bitmap");
                Marshal.Copy(frame.Data, 0, data, frame.Data.Length);
                return new Bitmap(ToBitmapFormat(frame.Format), AlphaFormat.Premul, data,
                    new PixelSize(frame.Width, frame.Height), Dpi, frame.Stride);
            }
            finally
            {
                Marshal.FreeHGlobal(data);
            }
        }

        return null;
    }

    private void ClearOutput()
    {
        lock (_outputSync)
        {
            _output.Clear();
            _reported.Clear();
        }
    }

    /// <summary>
    /// Writes a line of AvantGarde's own to the host output buffer and notifies the UI. Unlike
    /// host output this is not gated on there being a factory, because the things worth saying
    /// here happen while the host is still starting.
    /// </summary>
    private void AppendAppOutput(string msg)
    {
        Debug.WriteLine(msg);
        InvokeOutputReceived(AppendOutput(OutputPrefix + msg));
    }

    private string? AppendOutput(string? msg)
    {
        var sb = new StringBuilder();
        int max = v_maxOutputLines;

        lock (_outputSync)
        {
            while (_output.Count >= max && _output.Count > 0)
            {
                _output.RemoveAt(0);
            }

            _output.Add(msg?.TrimEnd() ?? string.Empty);

            foreach (var s in _output)
            {
                sb.AppendLine(s);
            }
        }

        return sb.Length != 0 ? sb.ToString().TrimEnd() : null;
    }

    private void MessageHandler(IAvaloniaRemoteTransportConnection cnx, object msg)
    {
        try
        {
            Debug.WriteLine($"{nameof(RemoteLoader)}.{nameof(MessageHandler)}");
            Debug.WriteLine($"Message type: {msg.GetType().Name}");

            var factory = v_factory;

            if (msg is FrameMessage frame)
            {
                HandleFrame(cnx, factory, frame);
            }
            else
            if (msg is UpdateXamlResultMessage update)
            {
                HandleUpdateResult(factory, update);
            }
            else
            if (msg is StartDesignerSessionMessage session)
            {
                HandleSessionStart(session);
            }
            else
            if (msg is HtmlTransportStartedMessage html)
            {
                HandleHtmlTransport(factory, html);
            }
            else
            if (msg is RequestViewportResizeMessage resize)
            {
                HandleViewportResize(resize);
            }
            else
            {
                ReportUnhandledOnce(msg, null);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("EXCEPTION IN MESSAGE HANDLER: " + e);
        }
    }

    private void HandleFrame(IAvaloniaRemoteTransportConnection cnx, PreviewFactory? factory, FrameMessage frame)
    {
        Debug.WriteLine($"FRAME: {frame.SequenceId}, {frame.Width} x {frame.Height} px, {frame.Data.Length} bytes");
        ClearXamlPending();
        DeriveNaturalSize(frame);
        Debug.WriteLine($"factory null: {factory == null}");
        Debug.WriteLine($"IsImmediate: {factory?.IsImmediate == true}");

        if (factory?.IsImmediate == false)
        {
            var bmp = ToBitmap(frame);

            if (bmp != null)
            {
                InvokePreviewReady(CreatePreview(factory, bmp));
            }
        }

        // The frame itself is always processed as it arrives. Only the acknowledgement is paced,
        // because that is what governs when the host renders the next one.
        AckFrame(cnx, frame.SequenceId);
    }

    /// <summary>
    /// Acknowledges a frame, immediately or after a delay, or not at all while paused.
    /// </summary>
    /// <remarks>
    /// There is one pending slot and it holds the newest sequence number, never a queue. The 12.0.5
    /// host sends one frame at a time and waits, so it can have at most one outstanding anyway, but
    /// acknowledging a superseded frame would leave a host that did otherwise waiting forever.
    ///
    /// The connection is captured here rather than read from v_connection in the timer callback: a
    /// host restarted during the delay must not be sent an acknowledgement for the previous host's
    /// frame.
    /// </remarks>
    private void AckFrame(IAvaloniaRemoteTransportConnection cnx, long sequenceId)
    {
        IAvaloniaRemoteTransportConnection? send;
        long seq;

        lock (_ackSync)
        {
            if (v_disposed)
            {
                // A message can still be in flight while Dispose runs, and the timer is gone.
                return;
            }

            _ackConnection = cnx;
            _ackSequence = sequenceId;

            if (v_renderPaused)
            {
                // No timer is scheduled. IsRenderPaused releases it.
                Debug.WriteLine($"Ack withheld - paused: {sequenceId}");
                return;
            }

            int interval = FrameRateLimiter.GetInterval(v_maxFrameRate);
            int delay = FrameRateLimiter.GetDelay(_ackLast, _ackClock.ElapsedMilliseconds, interval);

            if (delay > 0)
            {
                Debug.WriteLine($"Ack deferred {delay}ms: {sequenceId}");
                _ackTimer.Change(delay, System.Threading.Timeout.Infinite);
                return;
            }

            TakeAckNoSync(out send, out seq);
        }

        SendAck(send, seq);
    }

    private void AckTimerHandler(object? state)
    {
        IAvaloniaRemoteTransportConnection? send;
        long seq;

        lock (_ackSync)
        {
            if (v_renderPaused || !TakeAckNoSync(out send, out seq))
            {
                // Paused since the timer was scheduled, or the frame was acknowledged by another
                // path. Either way the pending slot is still owed an ack and FlushAck will pay it.
                return;
            }
        }

        SendAck(send, seq);
    }

    private void FlushAck()
    {
        IAvaloniaRemoteTransportConnection? send;
        long seq;

        lock (_ackSync)
        {
            if (!TakeAckNoSync(out send, out seq))
            {
                return;
            }
        }

        SendAck(send, seq);
    }

    /// <summary>
    /// Under <see cref="_ackSync"/>. Clears the pending slot and records the time, returning false
    /// if there was nothing pending.
    /// </summary>
    private bool TakeAckNoSync(out IAvaloniaRemoteTransportConnection? cnx, out long sequenceId)
    {
        cnx = _ackConnection;
        sequenceId = _ackSequence;

        if (cnx == null)
        {
            return false;
        }

        _ackConnection = null;
        _ackLast = _ackClock.ElapsedMilliseconds;
        return true;
    }

    private void SendAck(IAvaloniaRemoteTransportConnection? cnx, long sequenceId)
    {
        var resp = new FrameReceivedMessage();
        resp.SequenceId = sequenceId;
        Send(cnx, resp);
    }

    private void HandleUpdateResult(PreviewFactory? factory, UpdateXamlResultMessage update)
    {
        Debug.WriteLine("UPDATE");
        ClearXamlPending();
        Debug.WriteLine("Exception: " + update.Exception?.Message);
        Debug.WriteLine("Error: " + update.Error);

        if (factory == null)
        {
            return;
        }

        var error = update.Error;
        int line = 0;
        int pos = 0;

        if (!string.IsNullOrWhiteSpace(update.Exception?.Message))
        {
            Debug.WriteLine("Line number: " + update.Exception.LineNumber);
            Debug.WriteLine("Line Position: " + update.Exception.LinePosition);
            error = update.Exception.Message;

            if (update.Exception.LineNumber.HasValue)
            {
                line = update.Exception.LineNumber.Value;

                if (update.Exception.LinePosition.HasValue)
                {
                    pos = update.Exception.LinePosition.Value;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            Debug.WriteLine("Failed");

            if (factory.ProcessedXaml != null)
            {
                // The markup sent was not the file on disk - grid lines, event stripping and asset
                // prefetching all rewrite it - so the error may be ours rather than the user's.
                // Say which options were applied. This replaces a silent one-shot resend of the
                // verbatim file, which could go on to show a preview of markup the user never
                // configured, with no indication that it had happened.
                AppendAppOutput($"The XAML sent was modified by preview options ({factory.Load.Flags}) - " +
                    "turn them off to rule the modification out. The reported line is a line of the " +
                    "modified markup and may not be the line of the file");
            }

            InvokePreviewReady(CreatePreview(factory, new PreviewError(error, line, pos)));
        }
    }

    /// <summary>
    /// Records the natural size of the control from the host's viewport request.
    /// </summary>
    /// <remarks>
    /// The message is the host stating the size it wants to render at, in scaled pixels. It is not
    /// a request the client can refuse: probing the 12.0.5 host established that the Width and
    /// Height of a ClientViewportAllocatedMessage sent in reply are ignored outright, both below
    /// the design size and above the content's desired size. Only its DPI has any effect, and that
    /// duplicates ClientRenderInfoMessage. So this is a notification, and the useful thing to do
    /// with it is to learn the natural size - which fit-to-window needs and nothing else could
    /// supply.
    ///
    /// Latched once per SendXaml, on purpose. The scale is derived from the natural size and the
    /// host restates the size at every scale change, so recomputing on each message would feed the
    /// output back into the input and let the scale walk. The natural size cannot change without
    /// the XAML changing, so there is nothing to recompute.
    /// </remarks>
    private void HandleViewportResize(RequestViewportResizeMessage msg)
    {
        // Recorded, not answered, and not used to derive the natural size.
        //
        // Not answered because a reply cannot change anything: probing the 12.0.5 host established
        // that the Width and Height of a ClientViewportAllocatedMessage are ignored outright, both
        // below the design size and above the content's desired size. Only its DPI has any effect,
        // and that duplicates ClientRenderInfoMessage, which is already wired.
        //
        // Not used to derive the natural size because the size stated here is in whatever DPI the
        // host had applied when it sent the message, which is not knowable from this side. The
        // first message after a XAML send arrives before a pending DPI change has been applied, so
        // dividing by the scale in force at the send reports half the true size at 200%. See
        // DeriveNaturalSize, which takes it from the frame instead - the frame states the DPI it
        // was rendered at, so it is self-describing.
        Debug.WriteLine($"Host requested viewport: {msg.Width} x {msg.Height}");
    }

    /// <summary>
    /// Records the natural size of the control - the size it renders at when the scale is 1.0 -
    /// from a frame, which carries both its pixel size and the DPI it was rendered at.
    /// </summary>
    /// <remarks>
    /// Deriving it this way is what keeps fit-to-window from oscillating. The fit scale is computed
    /// from the natural size and then pushed back to the host as DPI, so any derivation that does
    /// not divide out the exact DPI of the frame feeds its own output back into its input. Pixel
    /// size divided by the frame's own DPI is invariant under scale, so there is no loop to damp.
    /// </remarks>
    private void DeriveNaturalSize(FrameMessage frame)
    {
        var dpi = frame.DpiX > 0 && frame.DpiY > 0 ? new Vector(frame.DpiX, frame.DpiY) : Dpi;

        // Rounded to whole dips. The host rounds the pixel size it renders, so at a fractional DPI
        // the quotient lands a fraction of a dip off - a 525 px frame at 111.84 dpi gives 450.6.
        var width = Math.Round(frame.Width * Dpi.X / dpi.X);
        var height = Math.Round(frame.Height * Dpi.Y / dpi.Y);

        if (!(width > 0) || !(height > 0))
        {
            return;
        }

        lock (_viewportSync)
        {
            if (_naturalLatched)
            {
                // One derivation per XAML generation. The control's size cannot change without the
                // XAML changing, and re-deriving from later frames would take the value from a
                // frame rendered at the fit scale - which is computed from this value.
                return;
            }

            _naturalLatched = true;

            // Rounding leaves a dip of slack, so an unchanged control can derive 450 from one frame
            // and 451 from the next. Held steady across generations, because a natural size which
            // flips by a dip on every re-send would nudge the fit scale with it.
            if (Math.Abs(width - _naturalWidth) <= NaturalSizeTolerance &&
                Math.Abs(height - _naturalHeight) <= NaturalSizeTolerance)
            {
                return;
            }

            _naturalWidth = width;
            _naturalHeight = height;
        }

        Debug.WriteLine($"Natural size: {width} x {height} " +
            $"(from {frame.Width} x {frame.Height} px at {dpi.X} x {dpi.Y} dpi)");
    }

    private void HandleSessionStart(StartDesignerSessionMessage msg)
    {
        Debug.WriteLine("SessionId: " + msg.SessionId);
        var expect = v_sessionId;

        if (!string.IsNullOrEmpty(expect) && msg.SessionId != expect)
        {
            // The id went out on the host command line and comes back verbatim, so a mismatch
            // positively identifies a host from an earlier run which has reached a recycled port.
            AppendAppOutput($"Ignoring a designer session belonging to another instance " +
                $"(expected {expect}, received {msg.SessionId})");

            v_sessionMismatch = true;
            return;
        }

        v_sessionStarted = true;
    }

    private void HandleHtmlTransport(PreviewFactory? factory, HtmlTransportStartedMessage msg)
    {
        // Terminal, and worth more than an output line. The host was told --method avalonia-remote,
        // so reaching here means it is rendering to HTTP instead and no frame will ever arrive on
        // this connection. Without this the symptom is an indefinitely blank preview.
        var text = $"The designer host started an HTML transport on {msg.Uri} and will send no preview frames";
        AppendAppOutput(text);

        if (factory != null)
        {
            InvokePreviewReady(CreatePreview(factory, new PreviewError(text)));
        }
    }

    /// <summary>
    /// Reports a message type the client does not act on, once per host process. The host repeats
    /// several of these per preview, so reporting every occurrence would bury the output pane.
    /// </summary>
    private void ReportUnhandledOnce(object msg, string? detail)
    {
        var name = msg.GetType().Name;
        bool report;

        lock (_outputSync)
        {
            report = _reported.Add(name);
        }

        if (report)
        {
            var text = $"Message not handled: {name}";

            if (!string.IsNullOrEmpty(detail))
            {
                text += " - " + detail;
            }

            AppendAppOutput(text + " (further occurrences are not reported)");
        }
    }

    private void ProcessOutputHandler(object? sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
        {
            return;
        }

        // Always buffer. Output produced during StartHostNoSync arrives while v_factory is still
        // null, and that is exactly where fatal host startup errors appear (a host built for the
        // wrong TFM dies with "Could not load file or assembly 'System.Runtime'"). Only the UI
        // notification is gated on there being a factory to attach it to.
        var output = AppendOutput(e.Data);

        if (v_factory != null)
        {
            InvokeOutputReceived(output);
        }
    }

    private PreviewPayload CreatePreview(PreviewFactory factory, Bitmap bitamp)
    {
        var payload = factory.CreatePreview();
        payload.Source = bitamp;
        payload.Output = GetProcessOutput();
        SetNaturalSize(payload);
        return payload;
    }

    private PreviewPayload CreatePreview(PreviewFactory factory, PreviewError error)
    {
        var payload = factory.CreatePreview();
        payload.Error = error;
        payload.Output = GetProcessOutput();
        SetNaturalSize(payload);
        return payload;
    }

    private void SetNaturalSize(PreviewPayload payload)
    {
        lock (_viewportSync)
        {
            payload.NaturalWidth = _naturalWidth;
            payload.NaturalHeight = _naturalHeight;
        }
    }

    private PreviewPayload CreateImmediatePreview(PreviewFactory factory, double scale)
    {
        var payload = factory.CreatePreview();

        if (payload.Source != null && scale != 1.0)
        {
            var size = payload.Source.PixelSize;
            payload.Source = payload.Source.CreateScaledBitmap(new PixelSize((int)(size.Width * scale), (int)(size.Height * scale)));
        }

        payload.Output = GetProcessOutput();
        return payload;
    }

    private void InvokePreviewReady(PreviewPayload payload)
    {
        if (PreviewReady != null)
        {
            Debug.WriteLine($"{nameof(RemoteLoader)}.{nameof(InvokePreviewReady)}");
            Dispatcher.UIThread.Post( () => {
                try
                {
                    if (!v_disposed)
                    {
                        PreviewReady?.Invoke(payload);
                    }
                }
                catch
                {
                }
            });
        }
    }

    private void InvokeOutputReceived(string? output)
    {
        if (!string.IsNullOrEmpty(output) && OutputReceived != null)
        {
            // Debug.WriteLine($"{nameof(RemoteLoader)}.{nameof(InvokeOutputReceived)}");

            Dispatcher.UIThread.Post( () => {
                try
                {
                    if (!v_disposed)
                    {
                        OutputReceived?.Invoke(output);
                    }
                }
                catch
                {
                }
            });
        }
    }

    private void ProcessExitedHandler(object? sender, EventArgs e)
    {
        Debug.WriteLine("HOST EXITED");
    }

    private void ErrorHandler(IAvaloniaRemoteTransportConnection cnx, Exception e)
    {
        Debug.WriteLine("CONNECTION ERROR: " + e.Message);
    }

    /// <summary>
    /// The paths a running host was started against, where they are not the project's own. Held
    /// for the lifetime of that host, because the XAML updates sent to it have to name the same
    /// copy the host was launched from.
    /// </summary>
    private sealed class ShadowPaths
    {
        public string? AppAssembly;
        public string? AppConfigPath;
        public string? AppDepsPath;
        public string? ProjectAssembly;
    }

}