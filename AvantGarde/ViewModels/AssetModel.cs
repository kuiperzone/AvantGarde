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
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace AvantGarde.ViewModels
{
    /// <summary>
    /// Class which provides image assets suitable for both light and dark theme. The instance derives from
    /// ReactiveObject, which means that values can be updated automagically. An instance can
    /// be accessed only by a singleton.
    /// </summary>
    public class AssetModel : ReactiveObject
    {
        private readonly static IAssetLoader? _loader;
        private readonly static string? _prefix;

        private bool _isDarkTheme;

        static AssetModel()
        {
            _loader = AvaloniaLocator.Current.GetService<IAssetLoader>();

            if (_loader != null)
            {
                _prefix = "avares://" + Assembly.GetAssembly(typeof(AssetModel))?.GetName().Name + "/Assets/";

                MainIcon = new Bitmap(_loader.Open(new Uri(_prefix + "AvantGarde128.png")));
                AboutImage = new Bitmap(_loader.Open(new Uri(_prefix + "AvantGarde1024.png")));
                ThemeFont = LoadFont("JosefinSans-VariableFont_wght", "Josefin Sans");

                var tree = _prefix + "Tree/";
                BinTree = new Bitmap(_loader.Open(new Uri(tree + "Bin64.png")));
                CSharpTree = new Bitmap(_loader.Open(new Uri(tree + "CSharp64.png")));
                DocTree = new Bitmap(_loader.Open(new Uri(tree + "Doc64.png")));
                DotnetTree = new Bitmap(_loader.Open(new Uri(tree + "Dotnet64.png")));
                FolderTree = new Bitmap(_loader.Open(new Uri(tree + "Folder64.png")));
                GreyFolderTree = new Bitmap(_loader.Open(new Uri(tree + "GreyFolder64.png")));
                ImageTree = new Bitmap(_loader.Open(new Uri(tree + "Image64.png")));
                ProjectTree = new Bitmap(_loader.Open(new Uri(tree + "Project64.png")));
                ProjectGreyTree = new Bitmap(_loader.Open(new Uri(tree + "ProjectGrey64.png")));
                UnknownTree = new Bitmap(_loader.Open(new Uri(tree + "Unknown64.png")));
                WarnTree = new Bitmap(_loader.Open(new Uri(tree + "Warn64.png")));
                XmlTree = new Bitmap(_loader.Open(new Uri(tree + "Xml64.png")));
                XamlTree = new Bitmap(_loader.Open(new Uri(tree + "Xaml64.png")));

                // Theme sensitive
                var icon = _prefix + "Icons/";
                CollapseDark = new Bitmap(_loader.Open(new Uri(icon + "CollapseD64.png")));
                CollapseGray = new Bitmap(_loader.Open(new Uri(icon + "CollapseG64.png")));
                CollapseLight = new Bitmap(_loader.Open(new Uri(icon + "CollapseL64.png")));
                CopyDark = new Bitmap(_loader.Open(new Uri(icon + "CopyD64.png")));
                CopyLight = new Bitmap(_loader.Open(new Uri(icon + "CopyL64.png")));
                DownDark = new Bitmap(_loader.Open(new Uri(icon + "DownD64.png")));
                DownLight = new Bitmap(_loader.Open(new Uri(icon + "DownL64.png")));
                Gear1Dark = new Bitmap(_loader.Open(new Uri(icon + "Gear1D64.png")));
                Gear1Gray = new Bitmap(_loader.Open(new Uri(icon + "Gear1G64.png")));
                Gear1Light = new Bitmap(_loader.Open(new Uri(icon + "Gear1L64.png")));
                Gear2Dark = new Bitmap(_loader.Open(new Uri(icon + "Gear2D64.png")));
                Gear2Light = new Bitmap(_loader.Open(new Uri(icon + "Gear2L64.png")));
                InfoDark = new Bitmap(_loader.Open(new Uri(icon + "InfoD64.png")));
                InfoLight = new Bitmap(_loader.Open(new Uri(icon + "InfoL64.png")));
                LeftDark = new Bitmap(_loader.Open(new Uri(icon + "LeftD64.png")));
                LeftLight = new Bitmap(_loader.Open(new Uri(icon + "LeftL64.png")));
                MinusDark = new Bitmap(_loader.Open(new Uri(icon + "MinusD64.png")));
                MinusLight = new Bitmap(_loader.Open(new Uri(icon + "MinusL64.png")));
                OpenDark = new Bitmap(_loader.Open(new Uri(icon + "OpenD64.png")));
                OpenLight = new Bitmap(_loader.Open(new Uri(icon + "OpenL64.png")));
                PlusDark = new Bitmap(_loader.Open(new Uri(icon + "PlusD64.png")));
                PlusLight = new Bitmap(_loader.Open(new Uri(icon + "PlusL64.png")));
                PreviewOptsDark = new Bitmap(_loader.Open(new Uri(icon + "PreviewOptsD64.png")));
                PreviewOptsLight = new Bitmap(_loader.Open(new Uri(icon + "PreviewOptsL64.png")));
                PreviewOptsHighDark = new Bitmap(_loader.Open(new Uri(icon + "PreviewOptsHighD64.png")));
                PreviewOptsHighLight = new Bitmap(_loader.Open(new Uri(icon + "PreviewOptsHighL64.png")));
                ReloadDark = new Bitmap(_loader.Open(new Uri(icon + "ReloadD64.png")));
                ReloadLight = new Bitmap(_loader.Open(new Uri(icon + "ReloadL64.png")));
                RightDark = new Bitmap(_loader.Open(new Uri(icon + "RightD64.png")));
                RightLight = new Bitmap(_loader.Open(new Uri(icon + "RightL64.png")));
                ScaleX1Dark = new Bitmap(_loader.Open(new Uri(icon + "ScaleX1D64.png")));
                ScaleX1Light = new Bitmap(_loader.Open(new Uri(icon + "ScaleX1L64.png")));
                SmallPinDark = new Bitmap(_loader.Open(new Uri(icon + "PinD48.png")));
                SmallPinLight = new Bitmap(_loader.Open(new Uri(icon + "PinL48.png")));
                UpDark = new Bitmap(_loader.Open(new Uri(icon + "UpD64.png")));
                UpLight = new Bitmap(_loader.Open(new Uri(icon + "UpL64.png")));
                WarnDark = new Bitmap(_loader.Open(new Uri(icon + "WarnD64.png")));
                WarnLight = new Bitmap(_loader.Open(new Uri(icon + "WarnL64.png")));
            }

        }

        public static IImage? MainIcon { get; }
        public static IImage? AboutImage { get; }
        public static FontFamily? ThemeFont { get; }

        // Project tree theme insensitive
        // We like to keep things alpha sorted.
        public static IImage? BinTree { get; }
        public static IImage? CSharpTree { get; }
        public static IImage? DocTree { get; }
        public static IImage? DotnetTree { get; }
        public static IImage? FolderTree { get; }
        public static IImage? GreyFolderTree { get; }
        public static IImage? ImageTree { get; }
        public static IImage? ProjectTree { get; }
        public static IImage? ProjectGreyTree { get; }
        public static IImage? UnknownTree { get; }
        public static IImage? WarnTree { get; }
        public static IImage? XmlTree { get; }
        public static IImage? XamlTree { get; }

        // Icons, light and dark variants
        public static IImage? CollapseDark { get; }
        public static IImage? CollapseGray { get; }
        public static IImage? CollapseLight { get; }
        public static IImage? CopyDark { get; }
        public static IImage? CopyLight { get; }
        public static IImage? DownDark { get; }
        public static IImage? DownLight { get; }
        public static IImage? Gear1Dark { get; }
        public static IImage? Gear1Gray { get; }
        public static IImage? Gear1Light { get; }
        public static IImage? Gear2Dark { get; }
        public static IImage? Gear2Light { get; }
        public static IImage? InfoDark { get; }
        public static IImage? InfoLight { get; }
        public static IImage? LeftDark { get; }
        public static IImage? LeftLight { get; }
        public static IImage? MinusDark { get; }
        public static IImage? MinusLight { get; }
        public static IImage? OpenDark { get; }
        public static IImage? OpenLight { get; }
        public static IImage? PlusDark { get; }
        public static IImage? PlusLight { get; }
        public static IImage? PreviewOptsDark { get; }
        public static IImage? PreviewOptsLight { get; }
        public static IImage? PreviewOptsHighDark { get; }
        public static IImage? PreviewOptsHighLight { get; }
        public static IImage? ReloadDark { get; }
        public static IImage? ReloadLight { get; }
        public static IImage? RightDark { get; }
        public static IImage? RightLight { get; }
        public static IImage? ScaleX1Dark { get; }
        public static IImage? ScaleX1Light { get; }
        public static IImage? SmallPinDark { get; }
        public static IImage? SmallPinLight { get; }
        public static IImage? UpDark { get; }
        public static IImage? UpLight { get; }
        public static IImage? WarnDark { get; }
        public static IImage? WarnLight { get; }

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

                    this.RaisePropertyChanged(nameof(CollapseIcon));
                    this.RaisePropertyChanged(nameof(CopyIcon));
                    this.RaisePropertyChanged(nameof(DownIcon));
                    this.RaisePropertyChanged(nameof(Gear1Icon));
                    this.RaisePropertyChanged(nameof(Gear2Icon));
                    this.RaisePropertyChanged(nameof(InfoIcon));
                    this.RaisePropertyChanged(nameof(LeftIcon));
                    this.RaisePropertyChanged(nameof(MinusIcon));
                    this.RaisePropertyChanged(nameof(OpenIcon));
                    this.RaisePropertyChanged(nameof(PlusIcon));
                    this.RaisePropertyChanged(nameof(PreviewOptsIcon));
                    this.RaisePropertyChanged(nameof(PreviewOptsHighIcon));
                    this.RaisePropertyChanged(nameof(ReloadIcon));
                    this.RaisePropertyChanged(nameof(RightIcon));
                    this.RaisePropertyChanged(nameof(ScaleX1Icon));
                    this.RaisePropertyChanged(nameof(SmallPinIcon));
                    this.RaisePropertyChanged(nameof(UpIcon));
                    this.RaisePropertyChanged(nameof(WarnIcon));
                }

            }
        }

        /// <summary>
        /// Get icon according to theme.
        /// </summary>
        public IImage? CollapseIcon
        {
            get { return IsDarkTheme ? CollapseDark : CollapseLight; }
        }

        public IImage? CollapseGrayIcon
        {
            get { return CollapseGray; }
        }

        public IImage? CopyIcon
        {
            get { return IsDarkTheme ? CopyDark : CopyLight; }
        }

        public IImage? DownIcon
        {
            get { return IsDarkTheme ? DownDark : DownLight; }
        }

        public IImage? Gear1Icon
        {
            get { return IsDarkTheme ? Gear1Dark : Gear1Light; }
        }

        public IImage? Gear1GrayIcon
        {
            get { return Gear1Gray; }
        }

        public IImage? Gear2Icon
        {
            get { return IsDarkTheme ? Gear2Dark : Gear2Light; }
        }

        public IImage? InfoIcon
        {
            get { return IsDarkTheme ? InfoDark : InfoLight; }
        }

        public IImage? LeftIcon
        {
            get { return IsDarkTheme ? LeftDark : LeftLight; }
        }

        public IImage? MinusIcon
        {
            get { return IsDarkTheme ? MinusDark : MinusLight; }
        }

        public IImage? OpenIcon
        {
            get { return IsDarkTheme ? OpenDark : OpenLight; }
        }

        public IImage? PlusIcon
        {
            get { return IsDarkTheme ? PlusDark : PlusLight; }
        }

        public IImage? PreviewOptsIcon
        {
            get { return IsDarkTheme ? PreviewOptsDark : PreviewOptsLight; }
        }

        public IImage? PreviewOptsHighIcon
        {
            get { return IsDarkTheme ? PreviewOptsHighDark : PreviewOptsHighLight; }
        }

        public IImage? ReloadIcon
        {
            get { return IsDarkTheme ? ReloadDark : ReloadLight; }
        }

        public IImage? RightIcon
        {
            get { return IsDarkTheme ? RightDark : RightLight; }
        }

        public IImage? ScaleX1Icon
        {
            get { return IsDarkTheme ? ScaleX1Dark : ScaleX1Light; }
        }

        public IImage? SmallPinIcon
        {
            get { return IsDarkTheme ? SmallPinDark : SmallPinLight; }
        }

        public IImage? UpIcon
        {
            get { return IsDarkTheme ? UpDark : UpLight; }
        }

        public IImage? WarnIcon
        {
            get { return IsDarkTheme ? WarnDark : WarnLight; }
        }

        private static FontFamily? LoadFont(string uri, string name)
        {
            try
            {
                return new FontFamily(new Uri(_prefix + "Fonts/" + uri), name);
            }
            catch
            {
                return null;
            }
        }

    }
}