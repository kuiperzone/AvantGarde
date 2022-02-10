// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

namespace AvantGarde.Loading
{
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
}