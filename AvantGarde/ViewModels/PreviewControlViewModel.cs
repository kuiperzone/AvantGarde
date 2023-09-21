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

using System.Reflection;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvantGarde.Settings;
using ReactiveUI;

namespace AvantGarde.ViewModels;

public class PreviewControlViewModel : AvantViewModel
{
    private readonly static IImage? _windowDecorWob;
    private readonly static IImage? _windowDecorBow;
    private readonly static IImage? _windowDecorNoResizeWob;
    private readonly static IImage? _windowDecorNoResizeBow;

    private readonly static double _decorWidth;
    private readonly static double _decorNoResizeWidth;

    private PreviewWindowTheme _theme = PreviewWindowTheme.DarkGray;
    private bool _isWindow;
    private double _windowTitleScale = 1;
    private IImage? _windowIcon;
    private string? _windowTitleText;
    private bool _windowCanResize;
    private IImage? _mainImage;
    private ISolidColorBrush? _mainBackground;
    private string? _widthText;
    private string? _heightText;
    private string? _messageText;
    private bool _hasErrorLocation;

    static PreviewControlViewModel()
    {
        var prefix = "avares://" + Assembly.GetAssembly(typeof(PreviewControlViewModel))?.GetName().Name + "/Assets/";
        _windowDecorWob = new Bitmap(AssetLoader.Open(new Uri(prefix + "WindowDecorWob.png")));
        _windowDecorBow = new Bitmap(AssetLoader.Open(new Uri(prefix + "WindowDecorBow.png")));
        _windowDecorNoResizeWob = new Bitmap(AssetLoader.Open(new Uri(prefix + "WindowDecorNoResizeWob.png")));
        _windowDecorNoResizeBow = new Bitmap(AssetLoader.Open(new Uri(prefix + "WindowDecorNoResizeBow.png")));

        _decorWidth = _windowDecorWob.Size.Width;
        _decorNoResizeWidth = _windowDecorNoResizeWob.Size.Width;
    }

    /// <summary>
    /// Gets or sets the preview theme.
    /// </summary>
    public PreviewWindowTheme Theme
    {
        get { return _theme; }

        set
        {
            if (_theme != value)
            {
                _theme = value;
                this.RaisePropertyChanged(nameof(IsWindow));
                this.RaisePropertyChanged(nameof(WindowBorder));
                this.RaisePropertyChanged(nameof(WindowTitleBackground));
                this.RaisePropertyChanged(nameof(WindowTitleForeground));
                this.RaisePropertyChanged(nameof(WindowDecorBitmap));
            }
        }
    }

    /// <summary>
    /// Gets the window preview border color.
    /// </summary>
    public ISolidColorBrush? WindowBorder
    {
        get { return _isWindow ? _theme.GetBorder() : null; }
    }

    /// <summary>
    /// Gets the window title area background color.
    /// </summary>
    public ISolidColorBrush WindowTitleBackground
    {
        get { return _theme.GetBackground(); }
    }

    /// <summary>
    /// Gets the window title area foreground color.
    /// </summary>
    public ISolidColorBrush WindowTitleForeground
    {
        get { return _theme.GetForeground(); }
    }

    /// <summary>
    /// Gets whether window top bar is visible. Always returns false for no theme.
    /// </summary>
    public bool IsWindow
    {
        get { return _isWindow && _theme != PreviewWindowTheme.None; }

        set
        {
            if (_isWindow != value)
            {
                _isWindow = value;
                this.RaisePropertyChanged(nameof(IsWindow));
                this.RaisePropertyChanged(nameof(WindowBorder));
            }
        }
    }

    /// <summary>
    /// Gets the window title scale. Affects topbar scale only.
    /// </summary>
    public double WindowTitleScale
    {
        get { return _windowTitleScale; }

        set
        {
            if (_windowTitleScale != value)
            {
                _windowTitleScale = value;
                this.RaisePropertyChanged(nameof(WindowIconHeight));
                this.RaisePropertyChanged(nameof(WindowDecorHeight));
                this.RaisePropertyChanged(nameof(WindowTitleFontSize));
            }
        }
    }

    public IImage? WindowIcon
    {
        get { return _windowIcon; }
        set { this.RaiseAndSetIfChanged(ref _windowIcon, value, nameof(WindowIcon)); }
    }

    public double WindowIconHeight
    {
        get { return _windowTitleScale * 24; }
    }

    public string? WindowTitleText
    {
        get { return _windowTitleText; }
        set { this.RaiseAndSetIfChanged(ref _windowTitleText, value, nameof(WindowTitleText)); }
    }

    public double WindowTitleFontSize
    {
        get { return _windowTitleScale * GlobalModel.DefaultFontSize; }
    }

    public bool WindowCanResize
    {
        get { return _windowCanResize; }

        set
        {
            if (_windowCanResize != value)
            {
                _windowCanResize = value;
                this.RaisePropertyChanged(nameof(WindowCanResize));
                this.RaisePropertyChanged(nameof(WindowDecorBitmap));
            }
        }
    }

    public double WindowDecorHeight
    {
        get { return _windowTitleScale * 20; }
    }

    /// <summary>
    /// Gets the window decor bitmap.
    /// </summary>
    public IImage? WindowDecorBitmap
    {
        get
        {
            if (_windowCanResize)
            {
                return _theme.IsDark() ? _windowDecorWob : _windowDecorBow;
            }

            return _theme.IsDark() ? _windowDecorNoResizeWob : _windowDecorNoResizeBow;
        }
    }

    public IImage? MainImage
    {
        get { return _mainImage; }

        set
        {
            if (_mainImage != value)
            {
                _mainImage = value;
                this.RaisePropertyChanged(nameof(MainImage));
                this.RaisePropertyChanged(nameof(MaxTotalWidth));
            }
        }
    }

    public ISolidColorBrush? MainBackground
    {
        get { return _mainBackground; }
        set { this.RaiseAndSetIfChanged(ref _mainBackground, value, nameof(MainBackground)); }
    }

    public double MaxTotalWidth
    {
        get { return _mainImage?.Size.Width ?? 0; }
    }

    public string? WidthText
    {
        get { return _widthText; }

        set
        {
            if (_widthText != value)
            {
                _widthText = value;
                this.RaisePropertyChanged(nameof(WidthText));
                this.RaisePropertyChanged(nameof(IsWidthVisible));
            }
        }
    }

    public bool IsWidthVisible
    {
        get { return !string.IsNullOrEmpty(_widthText); }
    }

    public string? HeightText
    {
        get { return _heightText; }

        set
        {
            if (_heightText != value)
            {
                _heightText = value;
                this.RaisePropertyChanged(nameof(HeightText));
                this.RaisePropertyChanged(nameof(IsHeightVisible));
            }
        }
    }

    public bool IsHeightVisible
    {
        get { return !string.IsNullOrEmpty(_heightText); }
    }

    public string? MessageText
    {
        get { return _messageText; }

        set
        {
            if (_messageText != value)
            {
                _messageText = value;
                this.RaisePropertyChanged(nameof(MessageText));
                this.RaisePropertyChanged(nameof(IsMessageVisible));
            }
        }
    }

    public bool IsMessageVisible
    {
        get { return !string.IsNullOrEmpty(_messageText); }
    }

    public double MaxMessageWidth
    {
        get { return  Global.Scale * 600; }
    }

    public bool HasErrorLocation
    {
        get { return _hasErrorLocation; }
        set { this.RaiseAndSetIfChanged(ref _hasErrorLocation, value, nameof(HasErrorLocation)); }
    }

}