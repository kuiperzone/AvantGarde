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
/// Immutable outcome of a single <see cref="MsBuildEvaluator"/> run against one project file.
/// </summary>
public sealed class MsBuildResult
{
    private readonly Dictionary<string, string> _properties;

    /// <summary>
    /// Constructs a successful result.
    /// </summary>
    public MsBuildResult(string projectPath, Dictionary<string, string> properties)
    {
        ProjectPath = projectPath;
        _properties = properties;
        IsSuccess = true;
    }

    /// <summary>
    /// Constructs a failed result. The message is short and user facing, the detail may carry the
    /// underlying MSBuild output.
    /// </summary>
    public MsBuildResult(string projectPath, string message, string? detail)
    {
        ProjectPath = projectPath;
        _properties = new Dictionary<string, string>();
        Message = message;
        Detail = detail;
    }

    /// <summary>
    /// Gets the project file the evaluation ran against.
    /// </summary>
    public string ProjectPath { get; }

    /// <summary>
    /// Gets whether MSBuild returned a parsable property set. Note that this is true even where
    /// individual properties came back empty, which is a normal outcome for an unrestored project.
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
    /// Gets the value of the named property. The result is an empty string where the property was
    /// not requested, or evaluated empty. Never null, because MSBuild does not distinguish an
    /// undefined property from an empty one.
    /// </summary>
    public string GetProperty(string name)
    {
        if (_properties.TryGetValue(name, out string? value))
        {
            return value;
        }

        return string.Empty;
    }

    /// <summary>
    /// Returns <see cref="GetProperty"/>, or null where it is empty.
    /// </summary>
    public string? GetPropertyOrNull(string name)
    {
        var value = GetProperty(name);
        return value.Length != 0 ? value : null;
    }

    /// <summary>
    /// Overrides.
    /// </summary>
    public override string ToString()
    {
        if (IsSuccess)
        {
            return ProjectPath + " - " + _properties.Count + " properties";
        }

        return ProjectPath + " - " + Message;
    }
}
