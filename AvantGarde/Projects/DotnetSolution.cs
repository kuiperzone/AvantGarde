// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022
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

namespace AvantGarde.Projects
{
    /// <summary>
    /// Class which holds one or more projects.
    /// </summary>
    public sealed class DotnetSolution : PathItem
    {
        private readonly SortedList<string, DotnetProject> _projects = new();
        private int _hashCode;

        /// <summary>
        /// Constructor with "csproj" or "sln" file path. A call to <see cref="Refresh"/> is needed after construction.
        /// </summary>
        /// <exception cref="ArgumentException">Path is empty"</exception>
        /// <exception cref="ArgumentException">Path must be a .sln or .csproj file"</exception>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public DotnetSolution(string path)
            : base(path, PathKind.AnyFile)
        {
            AssertExists();
            AssertKind(PathKind.Solution);
            IsSolutionFile = Extension == ".sln";
            SolutionName = Path.GetFileNameWithoutExtension(Name);
            Projects = _projects;
        }

        /// <summary>
        /// Gets the solution name, without the file extension.
        /// Same as <see cref="PathItem.Name"/>, but lacks the extension.
        /// </summary>
        public string SolutionName { get; }

        /// <summary>
        /// Gets whether the file has a ".sln" extension.
        /// </summary>
        public bool IsSolutionFile { get; }

        /// <summary>
        /// Gets the <see cref="SolutionProperties"/> instance. The instance will be shared with all child items.
        /// Changes do not take effect until the owner instance is refreshed.
        /// </summary>
        public SolutionProperties Properties { get; } = new();

        /// <summary>
        /// Gets read-only projects keyed on <see cref="DotnetProject.ProjectName"/>. It is empty until
        /// <see cref="Refresh"/> is called. If the solution path points to .csproj file, it will contain a single item.
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

                    if (line.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        return MakeFullName(line);
                    }
                }
            }

            return null;
        }
    }
}