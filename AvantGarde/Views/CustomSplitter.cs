// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022
// LICENSE   : GPL-3.0-or-later
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
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

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace AvantGarde.Views
{
    /// <summary>
    /// Custom GridSplitter.
    /// </summary>
    public class CustomSplitter : GridSplitter, IStyleable
    {
        private readonly DispatcherTimer _timer;
        private IBrush? _originalBackground;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CustomSplitter()
        {
            _timer = new(TimeSpan.FromMilliseconds(250), DispatcherPriority.Normal, TimerHandler);
            PointerMoved += PointerMovedHandler;
        }

        /// <summary>
        /// Custom property.
        /// </summary>
        public static readonly StyledProperty<IBrush?> HighlightProperty =
            AvaloniaProperty.Register<CustomSplitter, IBrush?>(nameof(Highlight), Brushes.Gray);

        /// <summary>
        /// Needed.
        /// </summary>
        Type IStyleable.StyleKey
        {
            get { return typeof(GridSplitter); }
        }

        /// <summary>
        /// Gets or sets the highlight brush.
        /// </summary>
        public IBrush? Highlight
        {
            get { return GetValue(HighlightProperty); }
            set { SetValue(HighlightProperty, value); }
        }

        private void PointerMovedHandler(object? sender, PointerEventArgs e)
        {
            _originalBackground ??= Background;
            Background = Highlight;
            _timer.Start();
        }

        private void TimerHandler(object? sender, EventArgs e)
        {
            if (!IsPointerOver)
            {
                Background = _originalBackground;
                _originalBackground = null;
                _timer.Stop();
            }
        }

    }
}
