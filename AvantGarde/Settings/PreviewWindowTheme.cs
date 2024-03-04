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