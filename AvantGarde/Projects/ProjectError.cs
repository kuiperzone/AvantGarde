// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

namespace AvantGarde.Projects
{
    /// <summary>
    /// A simple immutable class holding an error string and further details.
    /// </summary>
    public class ProjectError
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public ProjectError(DotnetProject project, string message, string? details = null)
        {
            ProjectName = project.ProjectName;
            Message = message;
            Details = details;
        }

        /// <summary>
        /// Gets the project name.
        /// </summary>
        public readonly string ProjectName;

        /// <summary>
        /// Gets the message.
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// Gets the details string.
        /// </summary>
        public readonly string? Details;

        /// <summary>
        /// Returns <see cref="Message"/>.
        /// </summary>
        public override string ToString()
        {
            return ProjectName + " - " + Message;
        }
    }
}
