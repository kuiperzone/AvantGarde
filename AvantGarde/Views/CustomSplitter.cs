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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace AvantGarde.Views;

/// <summary>
/// Custom GridSplitter. Note. This is no longer working in Avalonia 11.
/// Leave code. Have raised bug report.
/// </summary>
public class CustomSplitter : GridSplitter
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
    /// Gets or sets the highlight brush.
    /// </summary>
    public IBrush? Highlight
    {
        get { return GetValue(HighlightProperty); }
        set { SetValue(HighlightProperty, value); }
    }

    /// <summary>
    /// Important.
    /// </summary>
    protected override Type StyleKeyOverride
    {
        get { return typeof(GridSplitter); }
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
