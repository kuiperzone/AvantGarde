// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace AvantGarde.Views
{
    /// <summary>
    /// A custom TextBox. By default, it has monospace font.
    /// </summary>
    public class CodeTextBox : TextBox, IStyleable
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
        /// Dunno what this does, but it's needed.
        /// </summary>
        Type IStyleable.StyleKey
        {
            get { return typeof(TextBox); }
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

            string text = Text;

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
        /// Sets the carot position based on column and line. It returns CaretIndex.
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
                var clippy = Application.Current?.Clipboard;

                if (e.Key == Key.C && clippy != null && !string.IsNullOrEmpty(SelectedText))
                {
                    await clippy.SetTextAsync(SelectedText);
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