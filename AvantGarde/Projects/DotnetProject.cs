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

using System.Diagnostics;
using System.Xml.Linq;

namespace AvantGarde.Projects
{
    /// <summary>
    /// Class which extends <see cref="PathItem"/> to provide basic project information.
    /// </summary>
    public sealed class DotnetProject : PathItem
    {
        private static readonly EnumerationOptions EnumerateOptions = new()
            { IgnoreInaccessible = true, RecurseSubdirectories = true,
            ReturnSpecialDirectories = false, MatchType = MatchType.Simple };

        private int _hashCode;
        private int _propHash;
        private bool _refreshed;
        private bool _customOverride;

        /// Constructor with "csproj" file path and <see cref="Solution"/>. If null, a default
        /// instance will be created. A call to <see cref="Refresh"/> is needed after construction.
        public DotnetProject(string path, DotnetSolution? solution = null)
            : base(path, PathKind.Solution)
        {
            AssertExtension(".csproj");
            ProjectName = Path.GetFileNameWithoutExtension(Name);
            Properties.ProjectName = ProjectName;
            Solution = solution ?? new DotnetSolution(path);

            // Solution must present before Contents
            Contents = new NodeItem(ParentDirectory, PathKind.Directory, this);
            _hashCode = base.GetHashCode();

        }

        /// <summary>
        /// Gets the project name. Same as <see cref="PathItem.Name"/>, but lacks the extension.
        /// </summary>
        public string ProjectName { get; }

        /// <summary>
        /// Gets project specific properties. Changes do not take effect until the owner instance is refreshed.
        /// </summary>
        public ProjectProperties Properties { get; } = new();

        /// <summary>
        /// Gets the owning solution. The value can be null.
        /// </summary>
        public DotnetSolution Solution { get; }

        /// <summary>
        /// Gets the project source contents.
        /// </summary>
        public NodeItem Contents { get; }

        /// <summary>
        /// Gets the output type, i.e. "Exe". If not located, the value is empty. The initial value is empty until refreshed.
        /// </summary>
        public string OutputType { get; private set; } = string.Empty;

        /// <summary>
        /// Gets whether <see cref="OutputType"/> is an Exe.
        /// </summary>
        public bool IsApp
        {
            get { return OutputType.EndsWith("Exe", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Gets the TargetFramework. If not located, the value is empty. The initial value is empty until refreshed.
        /// </summary>
        public string TargetFramework { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the Avalonia version. If not located, the value is empty. The initial value is empty until refreshed.
        /// </summary>
        public string AvaloniaVersion { get; private set; } = string.Empty;

        /// <summary>
        /// Gets the build target assembly path. The value is null if the assembly cannot be located. If not null,
        /// the file exists at the time of refresh. The initial value is always null until refreshed. If
        /// <see cref="ProjectProperties.AssemblyOverride"/> is not null, it returns this value instead after refresh.
        /// </summary>
        public PathItem? AssemblyPath { get; private set; }

        /// <summary>
        /// Gets any error from the last refresh. The initial value is always null.
        /// </summary>
        public ProjectError? Error { get; private set; }

        /// <summary>
        /// Overrides <see cref="PathItem.Refresh"/>. Updates <see cref="TargetFramework"/> and
        /// <see cref="TargetAssembly"/>. It also returns true if the assembly dll file changes.
        /// </summary>
        public override bool Refresh()
        {
            ProjectError? error = null;
            bool changed = base.Refresh();

            if (changed || !_refreshed)
            {
                changed = true;
                error = ParseProject();
            }

            bool newCustom = false;
            int hash = HashCode.Combine(Solution.Properties.GetHashCode(), Properties.GetHashCode());

            if (_propHash != hash || !_refreshed)
            {
                changed = true;
                _propHash = hash;
                var path = MakeFullName(Properties.AssemblyOverride);

                _customOverride = path != null;

                if (path != null && path != AssemblyPath?.FullName)
                {
                    newCustom = true;
                    AssemblyPath = new PathItem(path, PathKind.AnyFile);
                }
            }

            if (!newCustom)
            {
                AssemblyPath?.Refresh();

                if (!_customOverride && (changed || AssemblyPath == null || !AssemblyPath.Exists))
                {
                    AssemblyPath = FindTargetAssembly(TargetFramework, Solution.Properties.Build);
                }
            }

            changed |= Contents.Refresh();
            hash = HashCode.Combine(hash, base.GetHashCode(), Contents, TargetFramework, AvaloniaVersion, AssemblyPath);

            changed |= hash != _hashCode;
            _hashCode = hash;
            _refreshed = true;

            if (changed)
            {
                Error = error ?? CheckForError();
            }

            return changed;
        }

        /// <summary>
        /// Returns application project. This is either self or that of <see cref="ProjectProperties.AppProjectName"/>.
        /// The result is null if none is found.
        /// </summary>
        public DotnetProject? GetApp()
        {
            if (IsApp)
            {
                return this;
            }

            var app = Properties.AppProjectName;

            if (app != null && Solution != null && Solution.Projects.TryGetValue(app, out DotnetProject? project))
            {
                return project;
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

        /// <summary>
        /// Overrides and returns <see cref="ProjectName"/>.
        /// </summary>
        public override string ToString()
        {
            return ProjectName;
        }

        private static string GetElementValue(XElement? root, string name)
        {
            if (root != null)
            {
                if (root.Name.LocalName == name)
                {
                    return root.Value;
                }

                foreach (var item in root.Elements())
                {
                    var s = GetElementValue(item, name);

                    if (s.Length != 0)
                    {
                        return s;
                    }
                }
            }

            return string.Empty;
        }

        private static string GetAvaloniaVersion(XElement? root)
        {
            if (root != null)
            {
                if (root.Name.LocalName == "PackageReference")
                {
                    foreach (var a0 in root.Attributes())
                    {
                        if (a0.Name.LocalName == "Include" && a0.Value == "Avalonia")
                        {
                            foreach (var a1 in root.Attributes())
                            {
                                if (a1.Name.LocalName == "Version")
                                {
                                    return a1.Value;
                                }
                            }

                            break;
                        }
                    }
                }

                foreach (var item in root.Elements())
                {
                    var s = GetAvaloniaVersion(item);

                    if (s.Length != 0)
                    {
                        return s;
                    }
                }
            }

            return string.Empty;
        }

        private ProjectError? ParseProject()
        {
            try
            {
                OutputType = string.Empty;
                TargetFramework = string.Empty;
                AvaloniaVersion = string.Empty;

                var doc = XDocument.Parse(ReadAsText());

                OutputType = GetElementValue(doc.Root, "OutputType");
                TargetFramework = GetElementValue(doc.Root, "TargetFramework");
                AvaloniaVersion = GetAvaloniaVersion(doc.Root);

                if (OutputType.Length == 0)
                {
                    OutputType = "Library";
                }

                return null;
            }
            catch (FileNotFoundException e)
            {
                Debug.WriteLine(e);
                return new ProjectError(this, "Project file not found", FullName);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return new ProjectError(this, "Invalid project", e.Message);
            }
        }

        private ProjectError? CheckForError()
        {
            if (!Exists)
            {
                return new ProjectError(this, "Project not found", FullName);
            }

            if (AssemblyPath == null || !AssemblyPath.Exists)
            {
                return new ProjectError(this, Solution.Properties.Build + " assembly not found", "Build project or set custom path");
            }

            if (AssemblyPath.Kind != PathKind.Assembly)
            {
                return new ProjectError(this, "Invalid assembly path", "Path must be a DLL file");
            }

            if (string.IsNullOrEmpty(TargetFramework))
            {
                return new ProjectError(this, "TargetFramework not found", "Project must specifiy a TargetFramework");
            }

            if (string.IsNullOrEmpty(AvaloniaVersion))
            {
                return new ProjectError(this, "Avalonia Package not found", "Project must reference Avalonia to preview controls");
            }

            if (GetApp() == null)
            {
                return new ProjectError(this, "Lib assembly requires an application", "Select an App project to preview controls");
            }

            return null;
        }

        private PathItem? FindTargetAssembly(string? framework, BuildKind build)
        {
            if (!string.IsNullOrEmpty(framework))
            {
                var assemblyName = ProjectName + ".dll";

                var temp = new NodeItem(Contents.GetDirectoryInfo());
                temp.Properties.FilePatterns = assemblyName;

                temp.Refresh();

                // Presence of "bin" preferred
                NodeItem node = temp;
                var dir = temp.FindFirst("bin", false, StringComparison.OrdinalIgnoreCase);

                if (dir != null)
                {
                    node = dir;
                    Debug.WriteLine("Bin directory found");
                }

                // We need debug/net5.0 etc
                dir = node.FindFirst(build.ToString(), false, StringComparison.OrdinalIgnoreCase);

                if (dir != null)
                {
                    Debug.WriteLine("Build Debug/Release directory found");
                    dir = dir.FindFirst(framework, false, StringComparison.OrdinalIgnoreCase);

                    if (dir != null)
                    {
                        Debug.WriteLine("Framework directory found");
                        return dir.FindFirst(assemblyName, true, StringComparison.OrdinalIgnoreCase);
                    }

                }
            }

            return null;
        }

   }
}