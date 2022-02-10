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

namespace AvantGarde.Views
{
    /// <summary>
    /// Custom TextBlock with hover link.
    /// </summary>
    public class LinkBlock : TextBlock, IStyleable
    {
        private IBrush _holdForeground;
        private TextDecorationCollection _holdDecor;

        /// <summary>
        /// Constructor.
        /// </summary>
        public LinkBlock()
        {
            _holdForeground = Brushes.Blue;
            _holdDecor = TextDecorations;

            FontWeight = FontWeight.SemiBold;
            Foreground = _holdForeground;
            Cursor = new Cursor(StandardCursorType.Hand);
            PointerEnter += PointerEnterHandler;
            PointerLeave += PointerLeaveHandler;
        }

        /// <summary>
        /// Custom property.
        /// </summary>
        public static readonly StyledProperty<IBrush?> HoverForegroundProperty =
            AvaloniaProperty.Register<CustomSplitter, IBrush?>(nameof(HoverForeground), Brushes.Purple);

        /// <summary>
        /// Needed.
        /// </summary>
        Type IStyleable.StyleKey
        {
            get { return typeof(TextBlock); }
        }

        /// <summary>
        /// Gets or sets the hover foreground brush.
        /// </summary>
        public IBrush? HoverForeground
        {
            get { return GetValue(HoverForegroundProperty); }
            set { SetValue(HoverForegroundProperty, value); }
        }

        private void PointerEnterHandler(object? _, PointerEventArgs e)
        {
            _holdForeground = Foreground;
            _holdDecor = TextDecorations;

            Foreground = HoverForeground;
            TextDecorations = Avalonia.Media.TextDecorations.Underline;
        }

        private void PointerLeaveHandler(object? _, PointerEventArgs e)
        {
            Foreground = _holdForeground;
            TextDecorations = _holdDecor;
        }

    }
}