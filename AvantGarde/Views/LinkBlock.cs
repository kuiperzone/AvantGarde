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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace AvantGarde.Views;

/// <summary>
/// Custom TextBlock with hover link.
/// </summary>
public class LinkBlock : TextBlock
{
    private IBrush? _holdForeground;
    private TextDecorationCollection? _holdDecor;

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

        PointerEntered += PointerEnteredHandler;
        PointerExited += PointerExitedHandler;
    }

    /// <summary>
    /// Custom property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> HoverForegroundProperty =
        AvaloniaProperty.Register<LinkBlock, IBrush?>(nameof(HoverForeground), Brushes.Purple);

    /// <summary>
    /// Gets or sets the hover foreground brush.
    /// </summary>
    public IBrush? HoverForeground
    {
        get { return GetValue(HoverForegroundProperty); }
        set { SetValue(HoverForegroundProperty, value); }
    }

    private void PointerEnteredHandler(object? _, PointerEventArgs e)
    {
        _holdForeground = Foreground;
        _holdDecor = TextDecorations;

        Foreground = HoverForeground;
        TextDecorations = Avalonia.Media.TextDecorations.Underline;
    }

    private void PointerExitedHandler(object? _, PointerEventArgs e)
    {
        Foreground = _holdForeground;
        TextDecorations = _holdDecor;
    }

}