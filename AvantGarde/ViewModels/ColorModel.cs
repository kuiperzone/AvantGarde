// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022-25
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

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace AvantGarde.ViewModels;

/// <summary>
/// Class which provides theme binding colors.
/// </summary>
public class ColorModel : ReactiveObject
{
    private bool _isDarkTheme;

    // Variation to FluentTheme colors.
    // WindowBackground must match RegionColor in App.xaml
    private static readonly SolidColorBrush _windowBackgroundLight = new SolidColorBrush(Color.Parse("#fffafafa"));
    private static readonly SolidColorBrush _windowBackgroundDark = new SolidColorBrush(Color.Parse("#ff242424"));

    private static readonly SolidColorBrush _previewBackgroundLight = new SolidColorBrush(Color.Parse("#e5e5e5"));
    private static readonly SolidColorBrush _previewBackgroundDark = new SolidColorBrush(Color.Parse("#333333"));

    private static readonly SolidColorBrush _linkForegroundLight = new SolidColorBrush(Color.Parse("#593358"));
    private static readonly SolidColorBrush _linkForegroundDark = new SolidColorBrush(Color.Parse("#896d86"));
    private static readonly SolidColorBrush _linkHoverLight = new SolidColorBrush(Color.Parse("#334459"));
    private static readonly SolidColorBrush _linkHoverDark = new SolidColorBrush(Color.Parse("#6d7889"));

    private static readonly ImageBrush _previewTileDark;
    private static readonly ImageBrush _previewTileLight;

    static ColorModel()
    {
        _previewTileDark = CreateCheckerBrush(_windowBackgroundDark.Color, _previewBackgroundDark.Color);
        _previewTileLight = CreateCheckerBrush(_windowBackgroundLight.Color, _previewBackgroundLight.Color);
    }

    /// <summary>
    /// Gets or sets whether to use light or dark theme icons. Dark icons are dark on light.
    /// </summary>
    public bool IsDarkTheme
    {
        get { return _isDarkTheme; }

        set
        {
            if (_isDarkTheme != value)
            {
                _isDarkTheme = value;

                this.RaisePropertyChanged(nameof(AvantForeground));
                this.RaisePropertyChanged(nameof(AvantBackground));
                this.RaisePropertyChanged(nameof(AvantHighground));

                this.RaisePropertyChanged(nameof(WindowBackground));

                this.RaisePropertyChanged(nameof(LinkForeground));
                this.RaisePropertyChanged(nameof(LinkHover));

                this.RaisePropertyChanged(nameof(PreviewBackground));
                this.RaisePropertyChanged(nameof(PreviewTile));
                this.RaisePropertyChanged(nameof(CodeBoxForeground));
                this.RaisePropertyChanged(nameof(CodeBoxSelectionForeground));
            }
        }
    }

    /// <summary>
    /// Gets a foreground color compatible with <see cref="AvantBackground"/>.
    /// </summary>
    public ISolidColorBrush AvantForeground { get; } = Brushes.White;

    /// <summary>
    /// Gets a "theme" background color.
    /// </summary>
    public ISolidColorBrush AvantBackground { get; } = new SolidColorBrush(Color.Parse("#593358"));

    /// <summary>
    /// Gets the "theme" high background color. Intended to be compatible with either black or white foreground.
    /// </summary>
    public ISolidColorBrush AvantHighground { get; } = new SolidColorBrush(Color.Parse("#896D86"));

    /// <summary>
    /// Gets window background. Overrides FluentTheme.
    /// </summary>
    public ISolidColorBrush WindowBackground
    {
        get { return _isDarkTheme ? _windowBackgroundDark : _windowBackgroundLight; }
    }

    /// <summary>
    /// Gets link foreground.
    /// </summary>
    public ISolidColorBrush LinkForeground
    {
        get { return _isDarkTheme ? _linkForegroundDark : _linkForegroundLight; }
    }

    /// <summary>
    /// Gets link hover foreground
    /// </summary>
    public ISolidColorBrush LinkHover
    {
        get { return _isDarkTheme ? _linkHoverDark : _linkHoverLight; }
    }

    /// <summary>
    /// Gets primary preview background solid color.
    /// </summary>
    public ISolidColorBrush PreviewBackground
    {
        get { return _isDarkTheme ? _previewBackgroundDark : _previewBackgroundLight; }
    }

    /// <summary>
    /// Gets preview background tile.
    /// </summary>
    public IImageBrush PreviewTile
    {
        get { return _isDarkTheme ? _previewTileDark : _previewTileLight; }
    }

    /// <summary>
    /// Gets code text foreground.
    /// </summary>
    public ISolidColorBrush CodeBoxForeground { get; } = Brushes.Gray;

    /// <summary>
    /// Gets code text selection foreground.
    /// </summary>
    public ISolidColorBrush CodeBoxSelectionForeground { get; } = Brushes.White;

    private static ImageBrush CreateCheckerBrush(Color c0, Color c1, int cellSize = 16)
    {
        int size = cellSize * 2;
        var dpi = new Vector(96, 96);
        var bmp = new WriteableBitmap(new PixelSize(size, size), dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);

        using (var fb = bmp.Lock())
        {
            int stride = fb.RowBytes;
            var buffer = new byte[stride * size];

            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    bool alt = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                    var c = alt ? c0 : c1;

                    int offset = y * stride + x * 4;
                    buffer[offset + 0] = c.B;
                    buffer[offset + 1] = c.G;
                    buffer[offset + 2] = c.R;
                    buffer[offset + 3] = c.A;
                }
            }

            Marshal.Copy(buffer, 0, fb.Address, buffer.Length);
        }

        var brush = new ImageBrush(bmp);
        brush.AlignmentX = AlignmentX.Left;
        brush.AlignmentY = AlignmentY.Top;
        brush.TileMode = TileMode.Tile;
        brush.Stretch = Stretch.Uniform;
        brush.DestinationRect = new RelativeRect(new Avalonia.Size(size, size), RelativeUnit.Absolute);
        return brush;
    }
}