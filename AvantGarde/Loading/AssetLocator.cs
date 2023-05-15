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
using Avalonia.Controls;
using Avalonia.Media;
using AvantGarde.Markup;
using AvantGarde.Projects;

namespace AvantGarde.Loading
{
    /// <summary>
    /// Locates Avalonia assets and resources.
    /// </summary>
    public class AssetLocator
    {
        // NOTES: https://github.com/AvaloniaUI/Avalonia/discussions/6997
        // https://github.com/AvaloniaUI/Avalonia/blob/5ece272a597ead5875fed27a7baf1cdc8a05324f/src/Markup/Avalonia.Markup.Xaml.Loader/CompilerExtensions/AvaloniaXamlIlLanguage.cs#L117-L126
        private static readonly Type[] s_assetTypes = new[] { typeof(IImage), typeof(WindowIcon),
            typeof(IResourceProvider) };

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <exception cref="ArgumentException">Project name or directory empty</exception>
        public AssetLocator(string projectName, string projectDirectory, string? xamlDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new ArgumentException("Empty", nameof(projectName));
            }

            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                throw new ArgumentException("Empty", nameof(projectDirectory));
            }

            ProjectName = projectName.Trim();
            ProjectDirectory = projectDirectory.Trim();

            if (!string.IsNullOrEmpty(xamlDirectory))
            {
                XamlDirectory = xamlDirectory;
            }
        }

        /// <summary>
        /// Constructor. The node must have a project.
        /// </summary>
        /// <exception cref="ArgumentNullException">Project</exception>
        public AssetLocator(NodeItem xamlItem)
        {
            ProjectName = xamlItem.Project?.ProjectName ?? throw new ArgumentNullException(nameof(NodeItem.Project));
            ProjectDirectory = xamlItem.Project.ParentDirectory;
            XamlDirectory = xamlItem.ParentDirectory;
        }

        /// <summary>
        /// Gets the project name.
        /// </summary>
        public string ProjectName;

        /// <summary>
        /// Gets the project directory. Does not check for existance.
        /// </summary>
        public string ProjectDirectory;

        /// <summary>
        /// Gets the directory of the xaml file. It can be null, in which case assets local to the
        /// XAML file cannot be located.
        /// </summary>
        public readonly string? XamlDirectory;

        /// <summary>
        /// Static method which determines whether attribute type expects an asset value.
        /// </summary>
        public static bool IsAsset(AttributeInfo info)
        {
            foreach (var type in s_assetTypes)
            {
                if (type.IsAssignableFrom(info.ValueType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Given an attribute value, returns the full filename. Returns null if the asset is not found.
        /// </summary>
        public string? GetAssetFileName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                if (GetResmAssetPath(value, out FileInfo? path))
                {
                    return path?.FullName;
                }

                if (GetAvaresAssetPath(value, out path))
                {
                    return path?.FullName;
                }

                return GetVanillaAssetPath(value)?.FullName;
            }
            catch(Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
        }

        private bool GetResmAssetPath(string value, out FileInfo? path)
        {
            // resm:MyApp.Assets.icon.png?assembly=MyAssembly
            const string Resm = "resm:";

            path = null;

            if (value.StartsWith(Resm))
            {
                value = value.Substring(Resm.Length);

                // Strip any assembly, this won't cope with it.
                int p = value.IndexOf('?');
                if (p > -1) value = value.Substring(0, p);

                // New left with: "MyApp.Assets.icon.png"
                if (value.StartsWith(ProjectName + '.'))
                {
                    // Now left with: "Assets.icon.png"
                    value = value.Substring(ProjectName.Length + 1);

                    var ext = Path.GetExtension(value);
                    value = Path.GetFileNameWithoutExtension(value).Replace('.', '/');

                    var info = new FileInfo(Path.Combine(ProjectDirectory, value + ext));

                    if (info.Exists == true)
                    {
                        path = info;
                    }
                }

                return true;
            }

            return false;
        }

        private bool GetAvaresAssetPath(string value, out FileInfo? path)
        {
            // avares://MyAssembly/Assets/icon.png
            const string Avares = "avares://";

            path = null;

            if (value.StartsWith(Avares))
            {
                value = value.Substring(Avares.Length).Replace('\\', '/');

                int p = value.IndexOf('/');

                if (p > 0 && p < value.Length - 2)
                {
                    value = value.Substring(p + 1);

                    // Now left with: "Assets/icon.png"
                    var info = new FileInfo(Path.Combine(ProjectDirectory, value));

                    if (info.Exists == true)
                    {
                        path = info;
                    }
                }

                return true;
            }

            return false;
        }

        private FileInfo? GetVanillaAssetPath(string value)
        {
            FileInfo? path = null;

            if (Path.IsPathRooted(value))
            {
                value = value.TrimStart('/', '\\');
                path = new FileInfo(Path.Combine(ProjectDirectory, value));
            }
            else
            if (XamlDirectory != null)
            {
                path = new FileInfo(Path.Combine(XamlDirectory, value));
            }

            if (path?.Exists == true)
            {
                return path;
            }

            return null;
        }

    }
}