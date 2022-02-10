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
    /// Class which extends <see cref="NodeProperties"/> for use with <see cref="DotnetSolution"/>.
    /// This class is intended to be JSON friendly.
    /// </summary>
    public sealed class SolutionProperties : NodeProperties
    {
        private const string DefaultFilePatterns = "*.axaml;*.xaml;*.paml;*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SolutionProperties()
        {
            FilePatterns = DefaultFilePatterns;
        }

        /// <summary>
        /// Gets or sets the build kind.
        /// </summary>
        public BuildKind Build { get; set; } = BuildKind.Debug;

        /// <summary>
        /// Assigns from other.
        /// </summary>
        public void AssignFrom(SolutionProperties other)
        {
            base.AssignFrom(other);
            Build = other.Build;
        }

        /// <summary>
        /// Override.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), Build);
        }

    }
}