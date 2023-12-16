// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022-23
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

    private readonly object _startSync = new();
    private readonly object _outputSync = new();
    private readonly List<string> _output = new();

    private volatile bool v_disposed;
    private volatile int v_timeout = 10000;
    private volatile IDisposable? v_listener;
    private volatile Process? v_process;
    private volatile IAvaloniaRemoteTransportConnection? v_connection;
    private volatile PreviewFactory? v_factory = null;
    private volatile int v_maxOutputLines = 100;

    private double _scale = 1.0;


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
    /// Gets or sets the scale. Setting a change will cause a new preview to be delivered.
    /// </summary>
    public double Scale
    {
        get { lock (_startSync) { return _scale; } }

        set
        {
            bool changed = false;
            value = Math.Max(value, 0.01);

            lock (_startSync)
            {
                if (value != _scale)
                {
                    _scale = value;
                    changed = true;
                }
            }

            if (changed)
            {
                SendScale(v_connection, value);

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

                list.Sort();

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
        // This should work under both Windows and Linux (MacOS?)
        // ~/.nuget/packages/avalonia/<avalonia-version>/tools/netcoreapp2.0/designer/Avalonia.Designer.HostApp.dll
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var src = GetAvaloniaPackagesDirectory();

        if (src != null)
        {
            src = Path.Combine(src, version);
            src = Path.Combine(src, "tools");

            // From here on in, we'll find it (adds a little flexibility).
            // Currently, there will be only one instance.
            var node = new NodeItem(src, PathKind.Directory);
            node.Properties.ShowEmptyDirectories = false;
            node.Properties.FilePatterns = DotnetHostName;
            node.Refresh();

            var path = node.FindFile(DotnetHostName, StringComparison.OrdinalIgnoreCase);

            if (path != null)
            {
                return path;
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
        var factory = v_factory;

        if (factory != null && !factory.Load.Flags.HasFlag(LoadFlags.DisableEvents))
        {
            Debug.WriteLineIf(msg.IsPressOrReleased, msg);
            Send(v_connection, msg.ToMessage(_scale));
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

    private static string? GetAvaloniaPackagesDirectory()
    {
        string? src = Environment.GetEnvironmentVariable("NUGET_PACKAGES");

        if (string.IsNullOrEmpty(src))
        {
            src = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            src = Path.Combine(src, ".nuget");
            src = Path.Combine(src, "packages");
        }

        if (!string.IsNullOrEmpty(src))
        {
            return Path.Combine(src, "avalonia");
        }

        return null;
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
                    InvokePreviewReady(CreateImmediatePreview(factory, _scale));
                }
                else
                {
                    SendXaml(v_connection, factory, true);
                    v_factory = factory;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("EXCEPTION:" + e);
                StopNoSync();

                InvokePreviewReady(CreatePreview(factory, new PreviewError(e.Message)));
            }
        }
    }

    private void StopNoSync()
    {
        v_factory = null;
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

        Debug.WriteLine("Find host for Avalonia: " + load.AppAvaloniaVersion);
        var host = FindDesignerHost(load.AppAvaloniaVersion);
        Debug.WriteLine("Host: " + host.FullName);

        var port = GetFreePort();

        // Locate dotnet
        // https://github.com/dotnet/docs/blob/main/docs/core/tools/dotnet-environment-variables.md#dotnet_host_path
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

        if (string.IsNullOrEmpty(dotnet))
        {
            dotnet = "dotnet";
        }

        var args = $@"exec --runtimeconfig ""{load.AppConfigPath}"" --depsfile ""{load.AppDepsPath}"" ""{host}"" --transport tcp-bson://127.0.0.1:{port}/ ""{load.AppAssembly}""";

        Debug.WriteLine($"STARTING: {dotnet} {args}");
        v_listener = new BsonTcpTransport().Listen(IPAddress.Loopback, port, c => { v_connection = c; });

        var info = new ProcessStartInfo
        {
            Arguments = args,
            CreateNoWindow = true,
            FileName = dotnet,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
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
        var cnx = v_connection ?? throw new InvalidOperationException($"{nameof(v_connection)} is null");
        cnx.OnException += ErrorHandler;
        cnx.OnMessage += MessageHandler;

        var fmt = new ClientSupportedPixelFormatsMessage();
        fmt.Formats = new[] { Avalonia.Remote.Protocol.Viewport.PixelFormat.Bgra8888,
            Avalonia.Remote.Protocol.Viewport.PixelFormat.Rgba8888,
            Avalonia.Remote.Protocol.Viewport.PixelFormat.Rgb565};

        if (!Send(cnx, fmt) || !SendScale(cnx, _scale))
        {
            throw new InvalidOperationException("Handshake failed to " + host.Name);
        }

        Debug.WriteLine("Connection OK");
    }

    private bool SendScale(IAvaloniaRemoteTransportConnection? cnx, double scale)
    {
        Debug.WriteLine("Send scale: " + scale);
        var msg = new ClientRenderInfoMessage();
        msg.DpiX = Dpi.X * scale;
        msg.DpiY = Dpi.Y * scale;
        return Send(cnx, msg);
    }

    private bool SendXaml(IAvaloniaRemoteTransportConnection? cnx, PreviewFactory factory, bool processed)
    {
        Debug.WriteLine($"{nameof(RemoteLoader)}.{nameof(SendXaml)}");
        var msg = new UpdateXamlMessage();
        msg.AssemblyPath = factory.Load.ProjectAssembly;

        msg.Xaml = factory.GetXaml(processed) ??
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
        return Send(cnx, msg);
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
        }
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
                Debug.WriteLine($"FRAME: {frame.SequenceId}, {frame.Width} x {frame.Height} px, {frame.Data.Length} bytes");
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

                var resp = new FrameReceivedMessage();
                resp.SequenceId = frame.SequenceId;
                Send(cnx, resp);
            }
            else
            if (msg is UpdateXamlResultMessage update)
            {
                Debug.WriteLine("UPDATE");
                Debug.WriteLine("Exception: " + update.Exception?.Message);
                Debug.WriteLine("Error: " + update.Error);

                if (factory != null)
                {
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
                        if (factory.GetResendAndReset())
                        {
                            Debug.WriteLine("Resend");
                            SendXaml(cnx, factory, false);
                        }
                        else
                        {
                            Debug.WriteLine("Failed");
                            InvokePreviewReady(CreatePreview(factory, new PreviewError(error, line, pos)));
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("EXCEPTION IN MESSAGE HANDLER: " + e);
        }
    }

    private void ProcessOutputHandler(object? sender, DataReceivedEventArgs e)
    {
        if (v_factory != null && !string.IsNullOrWhiteSpace(e.Data))
        {
            InvokeOutputReceived(AppendOutput(e.Data));
        }
    }

    private PreviewPayload CreatePreview(PreviewFactory factory, Bitmap bitamp)
    {
        var payload = factory.CreatePreview();
        payload.Source = bitamp;
        payload.Output = GetProcessOutput();
        return payload;
    }

    private PreviewPayload CreatePreview(PreviewFactory factory, PreviewError error)
    {
        var payload = factory.CreatePreview();
        payload.Error = error;
        payload.Output = GetProcessOutput();
        return payload;
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

}