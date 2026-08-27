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
/// Immutable outcome of a single <see cref="ProjectBuilder"/> run against one project file.
/// </summary>
public sealed class ProjectBuildResult
{
    /// <summary>
    /// Constructs a successful result.
    /// </summary>
    public ProjectBuildResult(string projectPath, string output)
    {
        ProjectPath = projectPath;
        Output = output;
        IsSuccess = true;
    }

    /// <summary>
    /// Constructs a failed result. The message is short and user facing, the detail carries the
    /// first line MSBuild reported as an error where there is one.
    /// </summary>
    public ProjectBuildResult(string projectPath, string message, string? detail, string output = "")
    {
        ProjectPath = projectPath;
        Message = message;
        Detail = detail;
        Output = output;
    }

    /// <summary>
    /// Gets the project file the build ran against.
    /// </summary>
    public string ProjectPath { get; }

    /// <summary>
    /// Gets whether MSBuild exited zero.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a short failure message, or null on success.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets failure detail, or null.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    /// Gets everything the build wrote to stdout and stderr. Never null, but empty where the process
    /// could not be started at all.
    /// </summary>
    public string Output { get; }

    /// <summary>
    /// Overrides.
    /// </summary>
    public override string ToString()
    {
        if (IsSuccess)
        {
            return ProjectPath + " - succeeded";
        }

        return ProjectPath + " - " + Message;
    }
}
