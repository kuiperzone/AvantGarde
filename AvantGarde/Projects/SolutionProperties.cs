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

namespace AvantGarde.Projects;

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