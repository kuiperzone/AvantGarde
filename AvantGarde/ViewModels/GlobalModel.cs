// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Media;
using AvantGarde.Markup;
using ReactiveUI;

namespace AvantGarde.ViewModels
{
    /// <summary>
    /// Global binding properties. An instance can be accessed only by a singleton.
    /// </summary>
    public class GlobalModel : ReactiveObject
    {
        private double _appFontSize = DefaultFontSize;
        private double _monoFontSize = DefaultFontSize;
        private FontFamily _monoFontFamily = DefaultMonoFamily;

        /// <summary>
        /// Constructor. Use singleton.
        /// </summary>
        protected GlobalModel()
        {
        }

        /// <summary>
        /// Minimum font size.
        /// </summary>
        public const double MinFontSize = 6;

        /// <summary>
        /// Maximum font size.
        /// </summary>
        public const double MaxFontSize = 24;

        /// <summary>
        /// Default font size.
        /// </summary>
        public const double DefaultFontSize = 14;

        /// <summary>
        /// Default mono font family.
        /// </summary>
        public const string DefaultMonoFamily = "Source Code Pro, monospace";

        public static string WebpageName { get; } = "Project Page";

        public static string WebpageUrl { get; } = "https://github.com/kuiperzone/AvantGarde";

        /// <summary>
        /// Gets the application version.
        /// </summary>
        public static string Version { get; } = GetVersion();

        /// <summary>
        /// Gets the Avalonia framework version.
        /// </summary>
        public static string Avalonia { get; } = MarkupDictionary.Version;

        /// <summary>
        /// Global singleton.
        /// </summary>
        public static GlobalModel Global = new();

        /// <summary>
        /// Asset provider.
        /// </summary>
        public AssetModel Assets { get; } = new AssetModel();

        /// <summary>
        /// Color provider.
        /// </summary>
        public ColorModel Colors { get; } = new ColorModel();

        /// <summary>
        /// Gets a scale value. The value is 1 when <see cref="AppFontSize"/> and <see cref="DefaultFontSize"/> are equal.
        /// </summary>
        public double Scale { get; private set; } = 1.0;

        /// <summary>
        /// Gets or sets the normal application UI font size. The value also defines button sizes.
        /// </summary>
        public double AppFontSize
        {
            get { return _appFontSize; }

            set
            {
                value = Math.Clamp(value, MinFontSize, MaxFontSize);

                if (_appFontSize != value)
                {
                    _appFontSize = value;
                    Scale = value / DefaultFontSize;

                    this.RaisePropertyChanged(nameof(Scale));
                    this.RaisePropertyChanged(nameof(AppFontSize));
                    this.RaisePropertyChanged(nameof(SmallFontSize));
                    this.RaisePropertyChanged(nameof(LargeFontSize));
                    this.RaisePropertyChanged(nameof(TreeIconSize));
                    this.RaisePropertyChanged(nameof(IconSize));
                    this.RaisePropertyChanged(nameof(MenuIconSize));
                    this.RaisePropertyChanged(nameof(LargeIconSize));
                }
            }
        }

        /// <summary>
        /// Gets a size smaller than <see cref="AppFontSize"/>.
        /// </summary>
        public double SmallFontSize
        {
            get { return AppFontSize * 0.85; }
        }

        /// <summary>
        /// Gets a size larger than <see cref="AppFontSize"/>.
        /// </summary>
        public double LargeFontSize
        {
            get { return AppFontSize * 1.25; }
        }

        /// <summary>
        /// Gets a size much larger than <see cref="AppFontSize"/>.
        /// </summary>
        public double HugeFontSize
        {
            get { return AppFontSize * 5.0; }
        }

        /// <summary>
        /// Gets or sets the monospace font size.
        /// </summary>
        public double MonoFontSize
        {
            get { return _monoFontSize; }

            set
            {
                value = Math.Clamp(value, MinFontSize, MaxFontSize);
                this.RaiseAndSetIfChanged(ref _monoFontSize, value, nameof(MonoFontSize));
            }
        }

        /// <summary>
        /// Gets or sets the monospace font family.
        /// </summary>
        public FontFamily MonoFontFamily
        {
            get { return _monoFontFamily; }
            set
            {
                if (_monoFontFamily.Name != value)
                {
                    _monoFontFamily = value;
                    this.RaisePropertyChanged(nameof(MonoFontFamily));
                }
            }
        }

        /// <summary>
        /// Gets the size of tree icons.
        /// </summary>
        public double TreeIconSize
        {
            get { return Scale * 32; }
        }

        /// <summary>
        /// Gets the size of button icons.
        /// </summary>
        public double IconSize
        {
            get { return Scale * 38; }
        }

        /// <summary>
        /// Gets a size smaller than <see cref="IconSize"/>.
        /// </summary>
        public double MenuIconSize
        {
            get { return IconSize * 0.85; }
        }

        /// <summary>
        /// Gets a size smaller than <see cref="IconSize"/>.
        /// </summary>
        public double LargeIconSize
        {
            get { return IconSize / 0.85; }
        }

        /// <summary>
        /// Gets minimum button width used on dialog boxes.
        /// </summary>
        public double MinStdButtonWidth
        {
            get { return Scale * 80; }
        }

        /// <summary>
        /// Gets minimum button height used on dialog boxes.
        /// </summary>
        public double MinStdButtonHeight
        {
            get { return Scale * 28; }
        }

        private static string GetVersion()
        {
            string rslt = Assembly.GetAssembly(typeof(Program))?.GetName()?.Version?.ToString(3) ?? "Unknown";
#if DEBUG
            rslt += " (Debug)";
#endif
            return rslt;
        }

    }
}