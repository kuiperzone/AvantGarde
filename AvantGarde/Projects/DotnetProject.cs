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
using System.Runtime.InteropServices;
using System.Xml.Linq;
using AvantGarde.Loading;

namespace AvantGarde.Projects;

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

    static DotnetProject()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (RuntimeInformation.OSArchitecture == Architecture.X64)
            {
                RuntimeId = "win-x64";
            }
            else
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
            {
                RuntimeId = "win-arm64";
            }
        }
        else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (RuntimeInformation.OSArchitecture == Architecture.X64)
            {
                RuntimeId = "linux-x64";
            }
            else
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
            {
                RuntimeId = "linux-arm64";
            }
        }
        else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (RuntimeInformation.OSArchitecture == Architecture.X64)
            {
                RuntimeId = "osx-x64";
            }
        }
    }

    /// Constructor with "csproj" of "fsproj" file path and <see cref="Solution"/>. If null, a default
    /// instance will be created. A call to <see cref="Refresh"/> is needed after construction.
    public DotnetProject(string path, DotnetSolution? solution = null)
        : base(path, PathKind.Solution)
    {
        AssertExtensions(".csproj", ".fsproj");
        ProjectName = Path.GetFileNameWithoutExtension(Name);
        Properties.ProjectName = ProjectName;
        Solution = solution ?? new DotnetSolution(path);

        // Solution must present before Contents
        Contents = new NodeItem(ParentDirectory, PathKind.Directory, this);
        _hashCode = base.GetHashCode();
    }

    /// <summary>
    /// Gets the default expected runtime ID (i.e. "linux-x64"), if known.
    /// </summary>
    public static string? RuntimeId { get; }

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
    /// Note, this is the version (if any) given in the csproj/fsproj file and not any override.
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
            if (root.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
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
            if (root.Name.LocalName.Equals("PackageReference", StringComparison.OrdinalIgnoreCase) ||
                root.Name.LocalName.Equals("PackageVersion", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var a0 in root.Attributes())
                {
                    if (a0.Name.LocalName.Equals("Include", StringComparison.OrdinalIgnoreCase) &&
                        a0.Value.Equals("Avalonia", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var a1 in root.Attributes())
                        {
                            var temp = a1.Name.LocalName;

                            if (temp.Equals("Version", StringComparison.OrdinalIgnoreCase) ||
                                temp.Equals("VersionOverride", StringComparison.OrdinalIgnoreCase))
                            {
                                if (RemoteLoader.IsAvaloniaVersion(a1.Value))
                                {
                                    return a1.Value;
                                }
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

    private XDocument? GetDirectoryPackages()
    {
        var root = Solution.GetFileInfo().Directory;
        var current = GetFileInfo().Directory;

        while (current != null && current != root)
        {
            var path = Path.Combine(current.FullName, "Directory.Packages.props");
            var item = new PathItem(path, PathKind.Xml);

            if (item.Exists)
            {
                return XDocument.Parse(item.ReadAsText());
            }

            current = current.Parent;
        }
        return null;
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

            if (string.IsNullOrWhiteSpace(AvaloniaVersion))
            {
                doc = GetDirectoryPackages();

                if (doc != null)
                {
                    AvaloniaVersion = GetAvaloniaVersion(doc.Root);
                }
            }

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

        if (string.IsNullOrEmpty(AvaloniaVersion) && Properties.AvaloniaOverride == null)
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
        Debug.WriteLine($"{nameof(FindTargetAssembly)} {framework}, {build}");

        if (string.IsNullOrEmpty(framework))
        {
            Debug.WriteLine("Failed - framework is null or empty");
            return null;
        }

        PathItem? item0 = null;

        // Find within traditional project;
        PathItem? item1 = FindAssemblyUnderRoot(Contents, ProjectName, framework, true, build);
        Debug.WriteLine($"Traditional assembly found: {item1 != null}, {item1?.FullName}");

        // Locate common artifacts file
        var artifacts = FindArtifactsDirectory(Contents);

        if (artifacts != null)
        {
            Debug.WriteLine("Find under artifacts directory");
            item0 = FindAssemblyUnderRoot(artifacts, ProjectName, framework, false, build);
            Debug.WriteLine($"Artifact assembly found: {item1 != null}, {item1?.FullName}");
        }

        if (item0 != null && item1 != null)
        {
            Debug.WriteLine("Compare timestamps");
            return item0.LastUtc > item1.LastUtc ? item0 : item1;
        }

        return item1 ?? item0;
    }

    private static PathItem? FindAssemblyUnderRoot(NodeItem root, string project, string framework, bool mandatoryFramework, BuildKind build)
    {
        // Locate assembly path somewhere within a build directory tree.
        // We accommodate traditional structures under the project bin directory, and also the
        // new single "artifacts" directory for .NET8. See below which describes both structures:
        // https://github.com/dotnet/designs/blob/simplify-output-paths-2/accepted/2023/simplify-output-paths.md

        // These are valid trees: (square brackets indicate optional)
        // project/bin/Debug/[net7.0]/[linux-x64]/projectName.dll
        // artifacts/bin/debug/projectName.dll
        // artifacts/bin/[projectName]/debug_net8.0/projectName.dll

        string assemblyName = project + ".dll";
        Debug.WriteLine($"{nameof(FindAssemblyUnderRoot)} under {root}");
        Debug.WriteLine($"{nameof(project)} {project}");
        Debug.WriteLine($"{nameof(framework)} {framework}");
        Debug.WriteLine($"{nameof(build)} {build}");
        Debug.WriteLine($"{nameof(RuntimeId)} {RuntimeId}");

        // Make a copy
        var node = new NodeItem(root.GetDirectoryInfo());
        node.Properties.FilePatterns = assemblyName;
        node.Refresh();

        // Presence of "bin" mandatory
        Debug.WriteLine("Look for mandatory bin");
        node = node.FindDirectory("bin", StringComparison.OrdinalIgnoreCase);
        Debug.WriteLine($"Bin: {node?.FullName}");

        if (node == null)
        {
            Debug.WriteLine("Failed - bin directory not found");
            return null;
        }

        Debug.WriteLine($"Look for optional project {project}");
        var temp = node.FindDirectory(project, StringComparison.OrdinalIgnoreCase);
        Debug.WriteLine($"Project: {temp?.FullName}");

        if (temp != null)
        {
            Debug.WriteLine("Project found OK");
            node = temp;
        }

        // Combined debug_net8.0 (artifacts only)
        var pivot = build + "_" + framework;
        Debug.WriteLine($"Look for pivot configuration/framework {pivot}");
        temp = node.FindDirectory(pivot, StringComparison.OrdinalIgnoreCase);
        Debug.WriteLine($"Pivot: {temp?.FullName}");

        if (temp == null)
        {
            Debug.WriteLine($"Look for mandatory configuration {build}");
            temp = node.FindDirectory(build.ToString(), StringComparison.OrdinalIgnoreCase);
            Debug.WriteLine($"Configuration: {temp?.FullName}");

            if (temp == null)
            {
                Debug.WriteLine($"Failed - could not find configuration {build}");
                return null;
            }

            // OK
            node = temp;

            Debug.WriteLine($"Look for optional framework {framework}");
            temp = node.FindDirectory(framework, StringComparison.OrdinalIgnoreCase);
            Debug.WriteLine($"Framework: {temp?.FullName}");

            if (temp == null && mandatoryFramework)
            {
                Debug.WriteLine($"Failed - could not find configuration {framework}");
                return null;
            }

            if (temp != null)
            {
                Debug.WriteLine("Framework found OK");
                node = temp;
            }
        }
        else
        {
            Debug.WriteLine($"Combined {pivot} found OK");
            node = temp;
        }

        if (RuntimeId != null)
        {
            // linux-x64 or win-x64 etc.
            Debug.WriteLine($"Look for optional runtime {RuntimeId}");
            temp = node.FindDirectory(RuntimeId, StringComparison.OrdinalIgnoreCase);

            if (temp != null)
            {
                Debug.WriteLine("RuntimeId found OK");
                node = temp;
            }
        }

        Debug.WriteLine("Finally look for mandatory assembly file");
        return node.FindFile(assemblyName, StringComparison.OrdinalIgnoreCase);
    }

    private static NodeItem? FindArtifactsDirectory(PathItem dir, int max = 5)
    {
        int count = 0;
        Debug.WriteLine($"{nameof(FindArtifactsDirectory)} upwards from {dir}");

        EnumerationOptions opts = new();
        opts.IgnoreInaccessible = true;
        opts.RecurseSubdirectories = false;
        opts.ReturnSpecialDirectories = true;
        opts.AttributesToSkip = FileAttributes.System;
        opts.MatchType = MatchType.Simple;
        opts.MatchCasing = MatchCasing.PlatformDefault;

        while (true)
        {
            Debug.WriteLine($"Iterate {dir}");
            var artifacts = Path.Combine(dir.FullName, "artifacts");

            if (Directory.Exists(artifacts))
            {
                Debug.WriteLine($"Found {artifacts}");
                return new NodeItem(artifacts, PathKind.Directory);
            }

            if (++count < max && dir.Exists && dir.ParentDirectory.Length != 0)
            {
                // Terminate upward traversal on encountering either Directory.Build.props or *.sln file.
                var props = Path.Combine(dir.FullName, "Directory.Build.props");

                if (File.Exists(props))
                {
                    Debug.WriteLine("Found Directory.Build.props - terminate here");
                    return null;
                }

                var sol = dir.GetDirectoryInfo().EnumerateFiles("*.sln", opts);

                if (sol.Any())
                {
                    Debug.WriteLine("Found *.sln file - terminate here");
                    return null;
                }

                dir = new NodeItem(dir.ParentDirectory, PathKind.Directory);
                continue;
            }

            Debug.WriteLine("Failed return null");
            return null;
        }
   }

}
