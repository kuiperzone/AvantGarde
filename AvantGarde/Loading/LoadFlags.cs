// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;

namespace AvantGarde.Loading
{
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
}