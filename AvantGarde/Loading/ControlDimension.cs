// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Text;

namespace AvantGarde.Loading
{
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
        public static readonly ControlDimension Empty = new ControlDimension(null, null, null);

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
}