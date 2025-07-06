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

namespace AvantGarde.Projects;

/// <summary>
/// Separate thread to watch for build changes. The PreviewHost now locks assembly files. We use a
/// separate thread to detect the project being built to know when to shutdown the PreviewHost.
/// </summary>
public sealed class BuildWatcher : IDisposable
{
#if DEBUG
    /// <summary>
    /// Slower interval for debug so we get to read output.
    /// </summary>
    public const int Interval = 5000;
#else
    /// <summary>
    /// Interval poll interval.
    /// </summary>
    public const int Interval = 1000;
#endif
    private volatile bool _disposed;
    private readonly NodeItem _node;
    private readonly Thread _thread;
    private readonly object _syncObj = new();
    private TimeSpan _elapsed = TimeSpan.MaxValue;
    private bool _changed;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public BuildWatcher(DotnetProject project, Action? changed)
    {
        _node = new NodeItem(GetWatchDirectory(project), PathKind.Directory);
        Debug.WriteLine("BuildWatcher: " + _node.FullName);

        _node.Properties.SearchDepth = project.Contents.Properties.SearchDepth;
        _node.Properties.ExcludeDirectories = "";
        _node.Properties.FilePatterns = "*.cache;*.pdb;*.dll;*.exe";
        DirectoryPath = _node.FullName;

        Changed = changed;
        _thread = new Thread(RunThread);
        _thread.IsBackground = true;
        _thread.Start();
    }

    /// <summary>
    /// Gets the directory path.
    /// </summary>
    public string DirectoryPath { get; }

    public TimeSpan Elapsed
    {
        get
        {
            lock (_syncObj)
            {
                return _elapsed;
            }
        }
    }

    /// <summary>
    /// Invoked by an internal thread when the build directory contents change.
    /// The handler should not block. Assigned on construction.
    /// </summary>
    public Action? Changed { get; }

    [return: NotNullIfNotNull(nameof(project))]
    public static string? GetWatchDirectory(DotnetProject? project)
    {
        if (project == null)
        {
            return null;
        }

        var projDir = project.Contents.FullName;
        Debug.WriteLine("Project directory: " + projDir);

        if (project.Properties.AssemblyOverride != null)
        {
            // Take into account custom assembly may be outside project directory
            Debug.WriteLine("AssemblyOverride: " + project.Properties.AssemblyOverride);

            if (Path.IsPathRooted(project.Properties.AssemblyOverride))
            {
                Debug.WriteLine("IsRooted");
                return new PathItem(project.Properties.AssemblyOverride, PathKind.Assembly).ParentDirectory;
            }

            var temp = new PathItem(Path.Combine(projDir, project.Properties.AssemblyOverride), PathKind.Assembly).ParentDirectory;
            Debug.WriteLine("Full assembly directory: " + temp);
            return temp;
        }

        return projDir;
    }

    public bool IsChanged(bool reset = true)
    {
        lock (_syncObj)
        {
            if (_changed)
            {
                if (reset)
                {
                    _changed = false;
                }

                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Implements <see cref="IDisposable"/>.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        _thread.Interrupt();
    }

    private void RunThread()
    {
        try
        {
            var sw = Stopwatch.StartNew();

            while (!_disposed)
            {
                Debug.WriteLine("BUILDWATCHER THREAD: " + _node.FullName);
                Debug.WriteLine("BUILDWATCHER THREAD Exists: " + _node.Exists);
                Thread.Sleep(Interval);

                if (_node.Refresh())
                {
                    Debug.WriteLine("BUILDWATCHER THREAD Changed");
                    sw.Restart();

                    lock (_syncObj)
                    {
                        _elapsed = default;
                        _changed = true;
                    }

                    try
                    {
                        if (!_disposed)
                        {
                            Debug.WriteLine("Call invoke");
                            Changed?.Invoke();
                        }
                    }
                    catch(Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                }
                else
                {
                    lock (_syncObj)
                    {
                        if (_elapsed != TimeSpan.MaxValue)
                        {
                            _elapsed = sw.Elapsed;
                        }
                    }
                }
            }
        }
        catch (ThreadInterruptedException)
        {
            // Expected
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }

        Debug.WriteLine("BUILDWATCHER THREAD TERMINATED");
    }
}