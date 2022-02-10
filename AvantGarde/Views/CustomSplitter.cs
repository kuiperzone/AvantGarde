// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
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
