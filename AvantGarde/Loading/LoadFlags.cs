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

namespace AvantGarde.Loading;

/// <summary>
/// Markup load option flags.
/// </summary>
[Flags]
public enum LoadFlags
{
    /// <summary>
    /// Default.
    /// </summary>
    None = 0x0000,

    /// <summary>
    /// Show grid lines.
    /// </summary>
    GridLines = 0x0001,

    /// <summary>
    /// Color grid lines.
    /// </summary>
    GridColors = 0x0002,

    /// <summary>
    /// Disable XAML events.
    /// </summary>
    DisableEvents = 0x0004,

    /// <summary>
    /// Pre-fetched assets prior to load.
    /// </summary>
    PrefetchAssets = 0x0008,
}

/// <summary>
/// Extension class.
/// </summary>
public static class LoadFlagsExtension
{
    /// <summary>
    /// Sets or unsets.
    /// </summary>
    public static LoadFlags Set(this LoadFlags opts, LoadFlags flag, bool value = true)
    {
        if (value)
        {
            return opts | flag;
        }

        return opts & ~flag;
    }

}