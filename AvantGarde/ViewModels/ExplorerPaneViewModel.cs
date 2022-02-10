// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using Avalonia.Media;
using AvantGarde.Views;
using ReactiveUI;

namespace AvantGarde.ViewModels
{
    public class ExplorerPaneViewModel : AvantViewModel
    {
        private bool _isViewOpen = true;
        private bool _isLoaded;
        private string? _titleText;

        public ExplorerPane? Owner;

        public string? TitleText
        {
            get { return _titleText; }
            set { this.RaiseAndSetIfChanged(ref _titleText, value, nameof(TitleText)); }
        }

        public bool IsViewOpen
        {
            get { return _isViewOpen; }

            set
            {
                if (_isViewOpen != value)
                {
                    _isViewOpen = value;
                    this.RaisePropertyChanged(nameof(IsViewOpen));
                    this.RaisePropertyChanged(nameof(ToggleViewIcon));
                }
            }
        }

        public bool IsLoaded
        {
            get { return _isLoaded; }

            set
            {
                if (_isLoaded != value)
                {
                    _isLoaded = value;
                    this.RaisePropertyChanged(nameof(IsLoaded));
                    this.RaisePropertyChanged(nameof(SolutionIcon));
                    this.RaisePropertyChanged(nameof(CollapseIcon));
                }
            }
        }

        public IImage? SolutionIcon
        {
            get
            {
                return IsLoaded ? Global.Assets.Gear1Icon : Global.Assets.Gear1GrayIcon;
            }
        }

        public IImage? CollapseIcon
        {
            get
            {
                return IsLoaded ? Global.Assets.CollapseIcon : Global.Assets.CollapseGrayIcon;
            }
        }

        public IImage? ToggleViewIcon
        {
            get
            {
                return IsViewOpen ? Global.Assets.LeftIcon : Global.Assets.RightIcon;
            }
        }

        /// <summary>
        /// Override.
        /// </summary>
        protected override void ColorChangedHandler()
        {
            base.ColorChangedHandler();
            this.RaisePropertyChanged(nameof(SolutionIcon));
            this.RaisePropertyChanged(nameof(CollapseIcon));
            this.RaisePropertyChanged(nameof(ToggleViewIcon));
        }

    }
}
