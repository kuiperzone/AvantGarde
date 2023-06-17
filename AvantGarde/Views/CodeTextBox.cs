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

using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using AvantGarde.Utility;

namespace AvantGarde.Views
{
    /// <summary>
    /// A custom TextBox. By default, it has monospace font.
    /// </summary>
    public class CodeTextBox : TextBox
    {
        public CodeTextBox()
        {
            FontFamily = "monospace";
            CornerRadius = new Avalonia.CornerRadius(0);
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap;
            AcceptsReturn = true;

            GotFocus += GotFocusHandler;
            LostFocus += LostFocusHandler;
        }

        /// <summary>
        /// Gets or sets the tab character width. Used only for caret position calculation.
        /// </summary>
        public int TabWidth { get; set; } = 4;

        /// <summary>
        /// Gets the caret position as (line, col). If the box is empty, the result is (1, 1).
        /// </summary>
        public Tuple<int, int> GetCaretPos()
        {
            int line = 1;
            int col = 1;

            var text = Text;

            if (text != null)
            {
                int idx = CaretIndex;

                for (int n = 0; n < text.Length; ++n)
                {
                    if (n == idx)
                    {
                        break;
                    }

                    var c = text[n];

                    if (c == '\n')
                    {
                        line += 1;
                        col = 1;
                    }
                    else
                    if (c == '\t')
                    {
                        col += TabWidth;
                    }
                    else
                    if (c >= ' ' && !Char.IsControl(c))
                    {
                        col += 1;
                    }
                }
            }

            return Tuple.Create(line, col);
        }

        /// <summary>
        /// Gets carot postion as "Ln y, Row x".
        /// </summary>
        public string GetCaretLabel()
        {
            var c = GetCaretPos();
            var sb = new StringBuilder("Ln ");
            sb.Append(c.Item1);
            sb.Append(CultureInfo.CurrentCulture.TextInfo.ListSeparator);
            sb.Append(" Col ");
            sb.Append(c.Item2);

            return sb.ToString();
        }

        /// <summary>
        /// Sets the caret position based on column and line. It returns CaretIndex.
        /// </summary>
        public int SetCaretPos(int line, int col)
        {
            var text = Text;
            col = Math.Max(col, 1);

            if (col > 0 && text != null)
            {
                int x = 1;
                int y = 1;

                for (int n = 0; n < text.Length; ++n)
                {
                    if ((y == line && x == col) || y > line)
                    {
                        CaretIndex = n;
                        return n;
                    }

                    var c = text[n];

                    if (c == '\n')
                    {
                        y += 1;
                        x = 1;
                    }
                    else
                    if (c == '\t')
                    {
                        x += TabWidth;
                    }
                    else
                    if (c >= ' ' && !Char.IsControl(c))
                    {
                        x += 1;
                    }
                }

                // Set end
                CaretIndex = Math.Max(text.Length - 1, 0);
            }

            return CaretIndex;
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Control)
            {
                var clippy = this.GetOwnerWindow().Clipboard;
                //var clippy = Application.Current?.Clipboard;

                if (e.Key == Key.C)
                {
                    if (clippy != null && !string.IsNullOrEmpty(SelectedText))
                    {
                        await clippy.SetTextAsync(SelectedText);
                    }
                }
                else
                if (e.Key == Key.A)
                {
                    SelectAll();
                }
            }
        }

        private void GotFocusHandler(object? sender, GotFocusEventArgs e)
        {
        }

        private void LostFocusHandler(object? sender, RoutedEventArgs e)
        {
        }

    }
}