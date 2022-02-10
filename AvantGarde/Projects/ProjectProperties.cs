// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;

namespace AvantGarde.Projects
{
    /// <summary>
    /// Class which holds <see cref="DotnetProject"/> properties. This class is intended to be JSON friendly.
    /// </summary>
    public sealed class ProjectProperties
    {
        private string? _appProjectName;
        private string? _assemblyOverride;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProjectProperties()
        {
            ProjectName = string.Empty;
        }

        public ProjectProperties(string name)
        {
            ProjectName = name;
        }

        /// <summary>
        /// Gets or sets the name of the project owning these properties.
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// Gets or sets the application host project name, excluding the extension. Applicable only where
        /// where <see cref="DotnetProject.IsApp"/> is false. Setting an empty string sets null.
        /// </summary>
        public string? AppProjectName
        {
            get { return _appProjectName; }

            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _appProjectName = value.Trim();
                }
                else
                {
                    _appProjectName = null;
                }
            }
        }

        /// <summary>
        /// Gets or sets the build target assembly. This can be used to override the default assembly path.
        /// The default value is null. The value may be a fully qualified path or an unrooted path local to the owner
        /// project directory. Assigning an empty string sets the property to null.
        /// </summary>
        public string? AssemblyOverride
        {
            get { return _assemblyOverride; }
            set { _assemblyOverride = !string.IsNullOrWhiteSpace(value) ? PathItem.CleanPath(value) : null; }
        }

        /// <summary>
        /// Assigns from other.
        /// </summary>
        public void AssignFrom(ProjectProperties other)
        {
            ProjectName = other.ProjectName;
            _appProjectName = other._appProjectName;
            _assemblyOverride = other._assemblyOverride;
        }

        /// <summary>
        /// Overrides.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(_assemblyOverride, _appProjectName);
        }

    }
}