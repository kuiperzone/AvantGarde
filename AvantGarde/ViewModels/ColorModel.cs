// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using Avalonia.Media;
using ReactiveUI;

namespace AvantGarde.ViewModels
{
    /// <summary>
    /// Class which provides theme binding colors.
    /// </summary>
    public class ColorModel : ReactiveObject
    {
        private bool _isDarkTheme;

        // Similar but different from FluentTheme colors.
        private readonly ISolidColorBrush _windowBackgroundLight = Brushes.White;
        private readonly ISolidColorBrush _windowBackgroundDark = new SolidColorBrush(Color.Parse("#1E1E1E"));
        private readonly ISolidColorBrush _previewBackgroundLight = new SolidColorBrush(Color.Parse("#E5E5E5"));
        private readonly ISolidColorBrush _previewBackgroundDark = new SolidColorBrush(Color.Parse("#333333"));

        private readonly ISolidColorBrush _linkForegroundLight = new SolidColorBrush(Color.Parse("#593358"));
        private readonly ISolidColorBrush _linkForegroundDark = new SolidColorBrush(Color.Parse("#896D86"));
        private readonly ISolidColorBrush _linkHoverLight = new SolidColorBrush(Color.Parse("#334459"));
        private readonly ISolidColorBrush _linkHoverDark = new SolidColorBrush(Color.Parse("#6D7889"));

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
        /// Gets preview background.
        /// </summary>
        public ISolidColorBrush PreviewBackground
        {
            get { return _isDarkTheme ? _previewBackgroundDark : _previewBackgroundLight; }
        }

        /// <summary>
        /// Gets code text foreground.
        /// </summary>
        public ISolidColorBrush CodeBoxForeground
        {
            get { return Brushes.Gray; }
        }

        /// <summary>
        /// Gets code text selection foreground.
        /// </summary>
        public ISolidColorBrush CodeBoxSelectionForeground
        {
            get { return Brushes.White; }
        }

    }
}