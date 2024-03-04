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

using System.Globalization;
using System.Text;

namespace AvantGarde.Loading;

/// <summary>
/// Immutable class which holds dimension values.
/// </summary>
public sealed class ControlDimension
{
    private static readonly CultureInfo Culture = CultureInfo.CurrentCulture;
    private static readonly string ListSep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;

    /// <summary>
    /// Empty dimension.
    /// </summary>
    public static readonly ControlDimension Empty = new(null, null, null);

    /// <summary>
    /// Constructor. Set <see cref="HasRange"/> to false.
    /// </summary>
    public ControlDimension(double value)
    {
        Value = value;
    }

    /// <summary>
    /// Constructor. Set <see cref="HasRange"/> to true.
    /// </summary>
    public ControlDimension(double? value, double? min, double? max)
    {
        HasRange = true;
        Value = value ?? Value;
        Min = min ?? Min;
        Max = max ?? Max;
    }

    /// <summary>
    /// Gets the dimension in dips.
    /// </summary>
    public readonly double Value = double.NaN;

    /// <summary>
    /// Gets the minimum.
    /// </summary>
    public readonly double Min = 0;

    /// <summary>
    /// Gets the maximum.
    /// </summary>
    public readonly double Max = double.PositiveInfinity;

    /// <summary>
    /// Gets whether one or more values have been specified. The result is false if all
    /// values are default or invalid.
    /// </summary>
    public readonly bool HasRange;

    /// <summary>
    /// Overrides. Returns "Value [Min, Max]"
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder(64);
        sb.Append(Value.ToString(Culture));

        if (HasRange)
        {
            sb.Append(' ');
            AppendMinMax(sb);
        }

        return sb.ToString();
    }

    /// <summary>
    /// If verbose, the string is multi-line i.e. "Value px\n[Min, Max]".
    /// If display is false, the result is the same as ToString().
    /// </summary>
    public string ToString(bool verbose)
    {
        if (!verbose)
        {
            return ToString();
        }

        var sb = new StringBuilder(64);

        sb.Append(Value.ToString(Culture));

        if (Value > 0 && double.IsFinite(Value))
        {
            // Don't show for 0 or NaN
            sb.Append(" px");
        }

        if (HasRange)
        {
            if (verbose)
            {
                sb.Append('\n');
            }
            else
            {
                sb.Append(' ');
            }

            AppendMinMax(sb);
        }

        return sb.ToString();
    }

    private void AppendMinMax(StringBuilder sb)
    {
        sb.Append('[');
        sb.Append(Min.ToString(Culture));
        sb.Append(ListSep);
        sb.Append(' ');
        sb.Append(Max.ToString(Culture));
        sb.Append(']');
    }

}