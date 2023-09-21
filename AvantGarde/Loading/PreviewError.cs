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
/// A simple immutable class holding an error string and position details.
/// </summary>
public class PreviewError
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public PreviewError(string message, int line = 0, int pos = 0)
    {
        Message = message;
        LineNum = line;
        LinePos = pos;
    }

    /// <summary>
    /// Gets the message.
    /// </summary>
    public readonly string Message;

    /// <summary>
    /// Gets the line number. A value of 0 or less is NA.
    /// </summary>
    public readonly int LineNum;

    /// <summary>
    /// Gets the line position. A value of 0 or less is NA.
    /// </summary>
    public readonly int LinePos;

    /// <summary>
    /// Returns <see cref="Message"/>.
    /// </summary>
    public override string ToString()
    {
        return Message;
    }
}