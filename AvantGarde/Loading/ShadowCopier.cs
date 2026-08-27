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
using System.Runtime.InteropServices;

namespace AvantGarde.Loading;

/// <summary>
/// Maintains temporary mirrors of build output directories, so that the designer host can be run
/// against a copy rather than against the project's own output. The host loads assemblies with
/// LoadFrom and holds them open for its lifetime, which is why a build started while a preview is
/// running otherwise has to be preceded by stopping it.
/// </summary>
/// <remarks>
/// Mirrors are incremental. The first one taken in a session copies the whole output directory;
/// after that only what a build actually rewrote is copied, which is what makes it viable to
/// re-mirror on every host start.
///
/// Nothing here launches or knows about the host. It takes directory paths and returns directory
/// paths, which is what makes it testable without a project or a process.
/// </remarks>
public sealed class ShadowCopier : IDisposable
{
    /// <summary>
    /// Directory created beneath the system temporary directory. Each running instance of the
    /// application owns a subdirectory of it named for its process id.
    /// </summary>
    public const string TempDirectoryName = "AvantGarde-Shadow";

    // Attempts and the pause between them, where a file cannot be read because something else is
    // writing it. See CopyWithRetry - the something else is MSBuild, and it holds any one file for
    // a small fraction of a second.
    private const int CopyRetries = 5;
    private const int CopyRetryDelay = 200;

    // Deliberately not PathItem.PlatformComparison, which is InvariantCulture and therefore case
    // sensitive on every platform. Prefix matching one path against another needs the case rule
    // the file system actually applies, or a mirror is silently not found.
    private static readonly StringComparison PathComparison =
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
        StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static readonly StringComparer PathComparer =
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
        StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private readonly object _sync = new();

    // Source directory -> mirror directory, longest source first. See Remap: a project output
    // directory nested inside the application's would otherwise be able to match the wrong entry.
    private readonly List<KeyValuePair<string, string>> _mirrors = new();

    private bool _disposed;

    /// <summary>
    /// Constructor. The mirrors are created beneath a directory of this process's own, which
    /// <see cref="Dispose"/> removes.
    /// </summary>
    public ShadowCopier()
        : this(GetDefaultRoot())
    {
    }

    /// <summary>
    /// Constructor with an explicit root directory, which is created on demand. The directory
    /// belongs to the instance - <see cref="Dispose"/> deletes it and everything beneath it.
    /// </summary>
    /// <exception cref="ArgumentException">Root null or empty</exception>
    public ShadowCopier(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = root;
    }

    /// <summary>
    /// Gets the root directory holding this instance's mirrors.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// Returns the root directory used by this process, i.e.
    /// "/tmp/AvantGarde-Shadow/&lt;pid&gt;".
    /// </summary>
    public static string GetDefaultRoot()
    {
        var parent = GetDefaultParent();
        return Path.Combine(parent, Environment.ProcessId.ToString());
    }

    /// <summary>
    /// Returns the directory holding the roots of every instance, i.e. "/tmp/AvantGarde-Shadow".
    /// </summary>
    public static string GetDefaultParent()
    {
        return Path.Combine(Path.GetTempPath(), TempDirectoryName);
    }

    /// <summary>
    /// Removes roots left behind by instances which are no longer running, skipping the directory
    /// named by keep. It does not throw.
    /// </summary>
    /// <remarks>
    /// A root is claimed by process id, and liveness decides. Testing whether the files can be
    /// deleted instead would be wrong: a running host locks the assemblies it has loaded but not
    /// the rest of the directory, so a recursive delete of a live instance's root would fail only
    /// after having removed part of it.
    /// </remarks>
    public static void SweepStaleRoots(string parent, string? keep = null)
    {
        try
        {
            if (!Directory.Exists(parent))
            {
                return;
            }

            foreach (var dir in Directory.EnumerateDirectories(parent))
            {
                var name = Path.GetFileName(dir);

                if (!string.IsNullOrEmpty(keep) && name.Equals(keep, PathComparison))
                {
                    continue;
                }

                // Only directories this class creates are removed, so anything which does not
                // carry a process id for a name is left where it is.
                if (!int.TryParse(name, out int pid) || IsProcessAlive(pid))
                {
                    continue;
                }

                try
                {
                    Debug.WriteLine("Remove stale shadow root: " + dir);
                    Directory.Delete(dir, true);
                }
                catch (Exception e)
                {
                    // Another instance may be sweeping the same directory.
                    Debug.WriteLine("Failed to remove " + dir + ": " + e.Message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    /// <summary>
    /// Mirrors the given directory and returns the result. Calling it again for the same source
    /// updates the existing mirror rather than making a second one.
    /// </summary>
    /// <exception cref="ArgumentException">Source null or empty</exception>
    /// <exception cref="DirectoryNotFoundException">Source does not exist</exception>
    /// <exception cref="IOException">Copy failed</exception>
    public ShadowResult Mirror(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        AssertNotDisposed();

        source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));

        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException("Directory not found " + source);
        }

        var dest = Path.Combine(Root, GetMirrorName(source));
        var rslt = MirrorDirectory(source, dest);
        AddMirror(source, dest);
        return rslt;
    }

    /// <summary>
    /// Returns the mirrored equivalent of a file or directory path, or null where the path is not
    /// under any directory which has been mirrored.
    /// </summary>
    public string? Remap(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        path = Path.GetFullPath(path);

        lock (_sync)
        {
            foreach (var pair in _mirrors)
            {
                if (path.Equals(pair.Key, PathComparison))
                {
                    return pair.Value;
                }

                var prefix = pair.Key + Path.DirectorySeparatorChar;

                if (path.StartsWith(prefix, PathComparison))
                {
                    return Path.Combine(pair.Value, path.Substring(prefix.Length));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Removes every mirror and the root directory itself. It does not throw. The instance remains
    /// usable and a subsequent <see cref="Mirror"/> starts afresh.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _mirrors.Clear();
        }

        try
        {
            if (Directory.Exists(Root))
            {
                Debug.WriteLine("Remove shadow root: " + Root);
                Directory.Delete(Root, true);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    /// <summary>
    /// Implements <see cref="IDisposable"/>.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Clear();
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);

            // Guards against the id having been reused by something else since. It is not a
            // certainty - two instances of this application share a process name - but that case
            // errs towards keeping a directory, which costs disk and nothing else.
            return proc.ProcessName.Equals(Process.GetCurrentProcess().ProcessName,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // Thrown where there is no such process.
            return false;
        }
    }

    /// <summary>
    /// Returns a directory name for the mirror of the given source. The leading part is for
    /// legibility in a trace; the hash is what makes it unique, and it has to, because the output
    /// directories of two projects in the same solution are both called "net8.0".
    /// </summary>
    private static string GetMirrorName(string source)
    {
        var leaf = Path.GetFileName(source);

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            leaf = leaf.Replace(c, '_');
        }

        if (leaf.Length == 0)
        {
            leaf = "dir";
        }

        return leaf + "-" + GetStableHash(source).ToString("X8");
    }

    /// <summary>
    /// FNV-1a over the path. String.GetHashCode is randomized per process, which would be
    /// tolerable here but makes a failure impossible to reproduce from a trace.
    /// </summary>
    private static uint GetStableHash(string source)
    {
        uint hash = 2166136261;

        foreach (var c in source)
        {
            // Paths are compared case-insensitively on Windows, so the name has to be too.
            hash = (hash ^ char.ToLowerInvariant(c)) * 16777619;
        }

        return hash;
    }

    private static ShadowResult MirrorDirectory(string source, string dest)
    {
        var rslt = new ShadowResult(source, dest);
        Directory.CreateDirectory(dest);

        var src = new DirectoryInfo(source);
        var existing = new HashSet<string>(PathComparer);

        foreach (var file in src.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var local = Path.GetRelativePath(source, file.FullName);

            if (IsExcluded(local))
            {
                rslt.SkipCount += 1;
                rslt.SkipByteCount += file.Length;
                continue;
            }

            var target = Path.Combine(dest, local);
            existing.Add(local);

            rslt.FileCount += 1;
            rslt.ByteCount += file.Length;

            if (IsCopyNeeded(file, target))
            {
                var dir = Path.GetDirectoryName(target);

                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                CopyWithRetry(file, target);

                // File.Copy preserves the write time on Windows but this is not guaranteed
                // everywhere, and IsCopyNeeded reads it back on the next pass.
                File.SetLastWriteTimeUtc(target, file.LastWriteTimeUtc);

                rslt.CopyCount += 1;
                rslt.CopyByteCount += file.Length;
            }
        }

        rslt.DeleteCount = RemoveExtraneous(dest, existing);
        Debug.WriteLine(rslt);
        return rslt;
    }

    /// <summary>
    /// Returns whether a file, given by its path relative to the source, is left out of the
    /// mirror. Everything outside a top level "runtimes" directory is copied.
    /// </summary>
    /// <remarks>
    /// It is not an optimization so much as the difference between viable and not. The Avalonia
    /// fixture used to develop this holds 566 MiB in its output directory, of which 533 MiB is
    /// native code and native debug symbols for platforms this machine cannot execute. Copying it
    /// all would put seconds onto the first preview of every session.
    ///
    /// Both rules describe files the runtime provably never opens, rather than files judged
    /// unlikely to matter. A skip is stated in the trace, and turning the option off restores the
    /// original behaviour entirely.
    /// </remarks>
    private static bool IsExcluded(string local)
    {
        var parts = local.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (parts.Length < 3 || !parts[0].Equals("runtimes", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsRuntimeCompatible(parts[1]))
        {
            // The host resolver builds its native search path from the running process's own
            // runtime identifier, so no other one under here can ever be probed.
            return true;
        }

        if (Path.GetExtension(local).Equals(".pdb", StringComparison.OrdinalIgnoreCase) &&
            Array.Exists(parts, s => s.Equals("native", StringComparison.OrdinalIgnoreCase)))
        {
            // Symbols for a native library, which only a native debugger attached to the host
            // would read. Managed symbols live under "lib" and are copied - the host does read
            // those, for the line numbers in a stack trace.
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns whether a "runtimes" subdirectory name is one this process could load from.
    /// </summary>
    private static bool IsRuntimeCompatible(string rid)
    {
        var current = RuntimeInformation.RuntimeIdentifier;

        if (string.IsNullOrEmpty(current))
        {
            // Unknown, so exclude nothing.
            return true;
        }

        if (rid.Equals(current, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // The less specific forms the identifier falls back to, i.e. "win" for "win-x64".
        if (current.StartsWith(rid + "-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (rid.Equals("any", StringComparison.OrdinalIgnoreCase) ||
            rid.Equals("base", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Everything but Windows is a unix, and packages predating the current identifiers use it.
        return !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            rid.StartsWith("unix", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copies one file, retrying a sharing violation.
    /// </summary>
    /// <remarks>
    /// A mirror can begin while a build is still running - the explorer refresh notices the output
    /// directory changing before the build watcher's own poll does, and restarts the preview - and
    /// MSBuild holds each file it writes open while it writes it. The alternative to waiting is
    /// the fallback, which launches from the output directory and takes the lock this whole class
    /// exists to avoid, at the one moment the user is provably building.
    /// </remarks>
    private static void CopyWithRetry(FileInfo source, string target)
    {
        for (int n = 0; n < CopyRetries; ++n)
        {
            try
            {
                source.CopyTo(target, true);
                return;
            }
            catch (IOException e)
            {
                Debug.WriteLine($"Retry {n + 1} of {CopyRetries} for {source.Name}: {e.Message}");
                Thread.Sleep(CopyRetryDelay);
            }
        }

        // The last attempt is left to throw.
        source.CopyTo(target, true);
    }

    private static bool IsCopyNeeded(FileInfo source, string target)
    {
        var info = new FileInfo(target);

        // Exact, deliberately. A tolerance would be kinder to a file system holding a coarser
        // time than the source's - the cost of getting that wrong is copying more than necessary -
        // whereas the cost of tolerating a difference is running a preview against an assembly a
        // build has already replaced, with nothing to say so. Two builds less than a second apart
        // are enough to reach that if the length happens to match.
        return !info.Exists || info.Length != source.Length ||
            info.LastWriteTimeUtc != source.LastWriteTimeUtc;
    }

    /// <summary>
    /// Removes anything in the mirror which the source no longer has, and returns how many files
    /// went. A stale assembly left behind is not obviously harmless - the host resolves some of
    /// its dependencies by probing the application directory - and the host is always stopped
    /// before a mirror is taken, so there is nothing holding these open.
    /// </summary>
    private static int RemoveExtraneous(string dest, HashSet<string> keep)
    {
        int count = 0;
        var dir = new DirectoryInfo(dest);

        foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            if (!keep.Contains(Path.GetRelativePath(dest, file.FullName)))
            {
                Debug.WriteLine("Remove stale shadow file: " + file.FullName);
                file.Delete();
                count += 1;
            }
        }

        foreach (var sub in dir.EnumerateDirectories("*", SearchOption.AllDirectories))
        {
            // Enumerated afresh, as removing the files above can have emptied one.
            if (sub.Exists && !sub.EnumerateFileSystemInfos().Any())
            {
                sub.Delete(true);
            }
        }

        return count;
    }

    private void AddMirror(string source, string dest)
    {
        lock (_sync)
        {
            for (int n = 0; n < _mirrors.Count; ++n)
            {
                if (_mirrors[n].Key.Equals(source, PathComparison))
                {
                    return;
                }
            }

            _mirrors.Add(new KeyValuePair<string, string>(source, dest));

            // Longest source first, so that Remap of a path under a nested source cannot match
            // the directory containing it.
            _mirrors.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
        }
    }

    private void AssertNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ShadowCopier));
        }
    }

}
