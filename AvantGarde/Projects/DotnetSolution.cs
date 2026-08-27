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
using System.Xml.Linq;

namespace AvantGarde.Projects;

/// <summary>
/// Class which holds one or more projects.
/// </summary>
public sealed class DotnetSolution : PathItem
{
    private readonly SortedList<string, DotnetProject> _projects = new();
    private readonly List<DotnetProject> _pending = new();
    private int _hashCode;

    /// <summary>
    /// Constructor with "csproj", "fsproj" or "sln" file path. A call to <see cref="Refresh"/> is needed after construction.
    /// </summary>
    /// <exception cref="ArgumentException">Path is empty"</exception>
    /// <exception cref="ArgumentException">Path must be a .sln, .csproj or .fsproj file"</exception>
    /// <exception cref="DirectoryNotFoundException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public DotnetSolution(string path)
        : base(path, PathKind.AnyFile)
    {
        AssertExists();
        AssertKind(PathKind.Solution);
        IsXmlSolutionFile = Extension == ".slnx";
        IsSolutionFile = IsXmlSolutionFile || Extension == ".sln";
        SolutionName = Path.GetFileNameWithoutExtension(Name);
        Projects = _projects;
    }

    /// <summary>
    /// Gets the solution name, without the file extension.
    /// Same as <see cref="PathItem.Name"/>, but lacks the extension.
    /// </summary>
    public string SolutionName { get; }

    /// <summary>
    /// Gets whether the file is a solution rather than a single project, i.e. ".sln" or ".slnx".
    /// </summary>
    public bool IsSolutionFile { get; }

    /// <summary>
    /// Gets whether the file is the XML solution format, ".slnx".
    /// </summary>
    public bool IsXmlSolutionFile { get; }

    /// <summary>
    /// Gets the <see cref="SolutionProperties"/> instance. The instance will be shared with all child items.
    /// Changes do not take effect until the owner instance is refreshed.
    /// </summary>
    public SolutionProperties Properties { get; } = new();

    /// <summary>
    /// Gets read-only projects keyed on <see cref="DotnetProject.ProjectName"/>. It is empty until
    /// <see cref="Refresh"/> is called. If the solution path points to .csproj/.fsproj file, it will contain a single item.
    /// </summary>
    public IReadOnlyDictionary<string, DotnetProject> Projects { get; }

    /// <summary>
    /// Overrides <see cref="PathItem.Refresh"/>. Updates <see cref="TargetFramework"/> and
    /// <see cref="TargetAssembly"/>. It also returns true if the assembly dll file changes.
    /// </summary>
    public override bool Refresh()
    {
        bool changed = base.Refresh();

        if (changed || _projects.Count == 0)
        {
            if (IsSolutionFile)
            {
                int n = 0;
                var paths = ReadProjectsInSolution();

                while (n < _projects.Values.Count)
                {
                    if (!paths.Contains(_projects.Values[n++].FullName))
                    {
                        _projects.Values.RemoveAt(--n);
                    }
                }

                foreach (var item in paths)
                {
                    if (!_projects.ContainsKey(Path.GetFileNameWithoutExtension(item)))
                    {
                        var project = new DotnetProject(item, this);
                        _projects.TryAdd(project.ProjectName, project);
                    }
                }
            }
            else
            if (_projects.Count == 0)
            {
                _projects.Add(SolutionName, new DotnetProject(FullName, this));
            }
        }

        // Rebuild hash
        var hash = base.GetHashCode();

        foreach (var item in Projects.Values)
        {
            changed |= item.Refresh();

            // Remove apps that may no longer be present
            var name = item.Properties.AppProjectName;

            if (name != null)
            {
                if (!Projects.TryGetValue(name, out DotnetProject? project) || !project.IsApp)
                {
                    item.Properties.AppProjectName = null;
                    item.Refresh();
                    changed = true;
                }
            }

            hash = HashCode.Combine(hash, item);
        }

        _hashCode = hash;
        return changed;
    }

    /// <summary>
    /// Gets whether any project wants an MSBuild evaluation. See <see cref="Evaluate"/>.
    /// </summary>
    public bool NeedsEvaluation
    {
        get
        {
            foreach (var item in Projects.Values)
            {
                if (item.NeedsEvaluation)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets whether an evaluation has been queued and not yet finished.
    /// </summary>
    public bool IsEvaluating
    {
        get { lock (_pending) { return _pending.Count != 0; } }
    }

    /// <summary>
    /// Marks every project needing evaluation and returns true where there is work to do. Call on
    /// the UI thread, then queue <see cref="Evaluate"/> on a worker.
    /// </summary>
    public bool BeginEvaluation()
    {
        lock (_pending)
        {
            if (_pending.Count != 0)
            {
                // A batch is already in flight. Starting a second one here would strand any project
                // it marks: the running batch copied its own list before this call and clears the
                // shared one when it ends, leaving the newly marked project flagged as evaluating
                // with nothing left to run or clear it - permanently "Resolving project...", and
                // never evaluated again. A stamp that moved meanwhile is picked up on the next
                // tick, once the running batch has finished.
                return false;
            }

            foreach (var item in Projects.Values)
            {
                if (item.BeginEvaluation())
                {
                    _pending.Add(item);
                }
            }

            return _pending.Count != 0;
        }
    }

    /// <summary>
    /// Performs the work queued by <see cref="BeginEvaluation"/>. Each project is a separate out of
    /// process MSBuild run of roughly half a second, so they go in parallel and the whole call must
    /// be made from a worker thread, never the UI thread. A subsequent <see cref="Refresh"/>
    /// applies the results.
    /// </summary>
    public void Evaluate()
    {
        DotnetProject[] pending;

        lock (_pending)
        {
            pending = _pending.ToArray();
        }

        try
        {
            if (pending.Length == 1)
            {
                pending[0].Evaluate();
            }
            else
            if (pending.Length != 0)
            {
                Parallel.ForEach(pending, item => { item.Evaluate(); });
            }
        }
        finally
        {
            lock (_pending)
            {
                _pending.Clear();
            }
        }
    }

    /// <summary>
    /// Looks for an item in the solution. If name is a leaf name only, the first matching item is returned.
    /// </summary>
    public PathItem? Find(string? name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            foreach (var project in Projects.Values)
            {
                var item = project.Contents.FindFile(name) ?? project.Contents.FindDirectory(name);

                if (item != null)
                {
                    return item;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Overrides to extend code to include changes to other properties.
    /// </summary>
    public override int GetHashCode()
    {
        return _hashCode;
    }

    private HashSet<string> ReadProjectsInSolution()
    {
        if (IsXmlSolutionFile)
        {
            return ReadProjectsInXmlSolution();
        }

        int pos = 0;
        var text = ReadAsText();
        var pathSet = new HashSet<string>();

        while(true)
        {
            pos = text.IndexOf("Project(", pos);

            if (pos > -1)
            {
                int end = text.IndexOf("EndProject", pos);

                if (end > pos)
                {
                    var path = ParseProjectPath(text.Substring(pos, end - pos));

                    if (path != null)
                    {
                        pathSet.Add(path);
                    }

                    pos = end;
                    continue;
                }
            }

            break;
        }

        return pathSet;
    }

    /// <summary>
    /// Reads the ".slnx" format, which nests Project elements inside optional Folder elements:
    /// &lt;Solution&gt;&lt;Folder Name="/src/"&gt;&lt;Project Path="src\App\App.csproj" /&gt;...
    /// </summary>
    private HashSet<string> ReadProjectsInXmlSolution()
    {
        var pathSet = new HashSet<string>();

        try
        {
            var doc = XDocument.Parse(ReadAsText());

            if (doc.Root == null)
            {
                return pathSet;
            }

            foreach (var item in doc.Root.Descendants())
            {
                if (!item.Name.LocalName.Equals("Project", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var attrib in item.Attributes())
                {
                    if (!attrib.Name.LocalName.Equals("Path", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var value = attrib.Value.Trim();

                    if (value.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                        value.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
                    {
                        var path = MakeFullName(value);

                        if (path != null)
                        {
                            pathSet.Add(path);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed to parse solution: " + e.Message);
        }

        return pathSet;
    }

    private string? ParseProjectPath(string line)
    {
        // Project("{FAE04EC0...}") = "Source\AvantGarde", "Source\AvantGarde\AvantGarde.csproj", "{97A47255...}"
        if (line.IndexOf('=') > 0)
        {
            var items = line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (items.Length > 1)
            {
                // Source\AvantGarde\AvantGarde.csproj
                line = items[1].Trim('"');

                if (line.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                    || line.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
                {
                    return MakeFullName(line);
                }
            }
        }

        return null;
    }
}