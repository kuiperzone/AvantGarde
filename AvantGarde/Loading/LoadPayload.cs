// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022-24
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
using AvantGarde.Projects;

namespace AvantGarde.Loading;

/// <summary>
/// Immutable class which holds everything needed to generate a preview. It is used to decouple the
/// interface from the <see cref="RemoteLoader"/> class instance which utilizes an internal thread.
/// </summary>
public class LoadPayload
{
    /// <summary>
    /// Constructs an empty instance.
    /// </summary>
    public LoadPayload(ProjectError? error = null)
    {
        Flags = LoadFlags.None;
        Error = error;
    }

    /// <summary>
    /// Constructs and populates from the <see cref="PathItem"/> and supplied options. If item is null,
    /// the <see cref="IsEmpty"/> it true. The item should have been recently refreshed prior.
    /// </summary>
    public LoadPayload(PathItem? item, LoadFlags opts = LoadFlags.None)
    {
        Debug.WriteLine($"{nameof(LoadPayload)} item {item?.ToString() ?? "null"}");

        Flags = opts;

        if (item != null)
        {
            DotnetProject? project = item as DotnetProject;

            Name = item.Name;
            FullPath = item.FullName;
            ItemKind = item.Kind;
            IsProjectHeader = project != null || item is DotnetSolution;
            IsEmpty = !item.Exists || item.Length == 0 || item.IsDirectory;

            var node = item as NodeItem;

            if (node?.Project != null)
            {
                project = node.Project;
                Locator = new AssetLocator(node);
            }

            if (project != null)
            {
                LocalPath = project.MakeLocalName(FullPath);
                ProjectAssembly = project.AssemblyPath?.FullName;

                if (ItemKind == PathKind.Xaml)
                {
                    Error = project.Error;
                }

                var app = project.GetApp();

                if (app != null)
                {
                    AppAssembly = app.AssemblyPath?.FullName;
                    AppAssemblyHashCode = app.AssemblyPath?.GetHashCode() ?? 0;
                    AppTargetFramework = app.TargetFramework;
                    AppAvaloniaVersion = !string.IsNullOrEmpty(app.AvaloniaVersion) ? app.AvaloniaVersion : app.Properties.AvaloniaOverride;
                    AppDepsPath = ChangeExtension(AppAssembly, ".deps.json");
                    AppConfigPath = ChangeExtension(AppAssembly, ".runtimeconfig.json");
                }
            }

        }
    }

    /// <summary>
    /// Gets parsing options.
    /// </summary>
    public LoadFlags Flags { get; }

    /// <summary>
    /// Gets whether the file does not exist or is empty.
    /// </summary>
    public bool IsEmpty { get; } = true;

    /// <summary>
    /// Gets the item name.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the item full file path.
    /// </summary>
    public string? FullPath { get; }

    /// <summary>
    /// Gets the item path local to project directory.
    /// </summary>
    public string? LocalPath { get; }

    /// <summary>
    /// Gets the item path kind.
    /// </summary>
    public PathKind ItemKind { get; } = PathKind.AnyFile;

    /// <summary>
    /// Gets whether the item is project header.
    /// </summary>
    public bool IsProjectHeader { get; }

    /// <summary>
    /// Gets any error originating from the project.
    /// </summary>
    public ProjectError? Error { get; }

    /// <summary>
    /// Gets an asset locator for item.
    /// </summary>
    public AssetLocator? Locator { get; }

    /// <summary>
    /// Gets the file path to the project assembly.
    /// </summary>
    public string? ProjectAssembly { get; }

    /// <summary>
    /// Gets the file path to the application assembly. This may be the same as <see cref="ProjectAssembly"/>.
    /// </summary>
    public string? AppAssembly { get; }

    /// <summary>
    /// Gets the <see cref="AppAssembly"/> HashCode at the time of construction. If <see cref="AppAssembly"/> is
    /// null, the value is 0.
    /// </summary>
    public int AppAssemblyHashCode { get; }

    /// <summary>
    /// Gets the application TargetFramework.
    /// </summary>
    public string? AppTargetFramework { get; }

    /// <summary>
    /// Gets the application Avalonia framework version.
    /// </summary>
    public string? AppAvaloniaVersion { get; }

    /// <summary>
    /// Gets the assembly ".deps.json" file path.
    /// </summary>
    public string? AppDepsPath { get; }

    /// <summary>
    /// Gets the assembly ".runtimeconfig.json" file path.
    /// </summary>
    public string? AppConfigPath { get; }

    /// <summary>
    /// Creates an instance of <see cref="PreviewFactory"/>.
    /// </summary>
    public PreviewFactory CreateFactory()
    {
        return new PreviewFactory(this);
    }

    private static string? ChangeExtension(string? path, string ext)
    {
        if (!string.IsNullOrEmpty(path))
        {
            var old = Path.GetExtension(path);

            if (path.Length > old.Length)
            {
                return string.Concat(path.AsSpan(0, path.Length - old.Length), ext);
            }
        }

        return null;
    }

}