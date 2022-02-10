// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using Avalonia.Media;

namespace AvantGarde.Settings
{
    /// <summary>
    /// Preview window title color. Do not change order as values stored in JSON.
    /// </summary>
    public enum PreviewWindowTheme
    {
        DarkGray = 0,
        AvantPurple,
        StockBlue,
        PlainWhite,

        /// <summary>
        /// Hides emulated window top bar.
        /// </summary>
        None = 255,
    }

    /// <summary>
    /// Extension class.
    /// </summary>
    public static class PreviewWindowThemeExtension
    {
        private readonly static ISolidColorBrush Purple = new SolidColorBrush(Color.Parse("#593358"));
        private readonly static ISolidColorBrush Blue = new SolidColorBrush(Color.Parse("#0B548D"));
        private readonly static ISolidColorBrush Gray = new SolidColorBrush(Color.Parse("#252526"));

        /// <summary>
        /// Returns true if theme is white on a dark color.
        /// </summary>
        public static bool IsDark(this PreviewWindowTheme theme)
        {
            switch (theme)
            {
                case PreviewWindowTheme.PlainWhite: return false;
                default: return true;
            }
        }

        public static ISolidColorBrush GetBorder(this PreviewWindowTheme theme)
        {
            switch (theme)
            {
                case PreviewWindowTheme.PlainWhite: return Brushes.Black;
                default: return GetBackground(theme);
            }
        }

        public static ISolidColorBrush GetForeground(this PreviewWindowTheme theme)
        {
            return theme.IsDark() ? Brushes.White : Brushes.Black;
        }

        public static ISolidColorBrush GetBackground(this PreviewWindowTheme theme)
        {
            switch (theme)
            {
                case PreviewWindowTheme.AvantPurple: return Purple;
                case PreviewWindowTheme.StockBlue: return Blue;
                case PreviewWindowTheme.PlainWhite: return Brushes.White;
                default: return Gray;
            }
        }

    }
}