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

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using AvantGarde.Loading;
using ReactiveUI;

namespace AvantGarde.ViewModels
{
    /// <summary>
    /// Base class for views with common preview options.
    /// </summary>
    public class PreviewOptionsViewModel : AvantViewModel
    {
        private readonly int _scaleNormIndex;
        private readonly List<string> _scaleItems = new();
        private LoadFlags _loadFlags = LoadFlags.None;
        private bool _hasContent;
        private bool _isXamlViewable;
        private bool _isPlainTextViewable;
        private bool _isXamlViewOpen;
        private int _scaleSelectedIndex;

        /// <summary>
        /// Occurs when a flag is checked or unchecked by user click.
        /// </summary>
        public event Action<LoadFlags>? LoadFlagChecked;

        /// <summary>
        /// Occurs when scale is changed.
        /// </summary>
        public event Action<PreviewOptionsViewModel>? ScaleChanged;

        public PreviewOptionsViewModel()
        {
            _scaleItems.Add("25%");
            _scaleItems.Add("50%");
            _scaleItems.Add("67%");
            _scaleItems.Add("75%");
            _scaleItems.Add("100%");
            _scaleItems.Add("125%");
            _scaleItems.Add("150%");
            _scaleItems.Add("200%");
            _scaleItems.Add("300%");
            _scaleItems.Add("400%");

            _scaleNormIndex = 4;
            _scaleSelectedIndex = _scaleNormIndex;
        }

        /// <summary>
        /// Gets or sets flags. Does not invoke <see cref="LoadFlagChecked"/>.
        /// </summary>
        public LoadFlags LoadFlags
        {
            get { return _loadFlags; }

            set
            {
                if (_loadFlags != value)
                {
                    _loadFlags = value;

                    this.RaisePropertyChanged(nameof(IsGridLinesChecked));
                    this.RaisePropertyChanged(nameof(IsGridColorsChecked));
                    this.RaisePropertyChanged(nameof(IsDisableEventsChecked));
                    this.RaisePropertyChanged(nameof(IsPrefetchAssetsChecked));

                    OnFlagChanged(false);
                }
            }
        }

        public bool HasAnyLoadFlag
        {
            get { return _loadFlags != LoadFlags.None; }
        }

        public IImage? LoadFlagIcon
        {
            get { return HasAnyLoadFlag ? Global.Assets.PreviewOptsHighIcon : Global.Assets.PreviewOptsIcon; }
        }

        public IImage? LoadFlagDarkIcon
        {
            get { return HasAnyLoadFlag ? AssetModel.PreviewOptsHighDark : AssetModel.PreviewOptsDark; }
        }

        public bool IsGridLinesChecked
        {
            get { return _loadFlags.HasFlag(LoadFlags.GridLines); }

            set
            {
                var opts = _loadFlags.Set(LoadFlags.GridLines, value);

                if (_loadFlags != opts)
                {
                    _loadFlags = opts;
                    this.RaisePropertyChanged(nameof(IsGridLinesChecked));
                    OnFlagChanged(true);
                }
            }
        }

        public bool IsGridColorsChecked
        {
            get { return _loadFlags.HasFlag(LoadFlags.GridColors); }

            set
            {
                var opts = _loadFlags.Set(LoadFlags.GridColors, value);

                if (_loadFlags != opts)
                {
                    _loadFlags = opts;
                    this.RaisePropertyChanged(nameof(IsGridColorsChecked));
                    OnFlagChanged(true);
                }
            }
        }

        public bool IsDisableEventsChecked
        {
            get { return _loadFlags.HasFlag(LoadFlags.DisableEvents); }

            set
            {
                var opts = _loadFlags.Set(LoadFlags.DisableEvents, value);

                if (_loadFlags != opts)
                {
                    _loadFlags = opts;
                    this.RaisePropertyChanged(nameof(IsDisableEventsChecked));
                    OnFlagChanged(true);
                }

            }
        }

        public bool IsPrefetchAssetsChecked
        {
            get { return _loadFlags.HasFlag(LoadFlags.PrefetchAssets); }

            set
            {
                var opts = _loadFlags.Set(LoadFlags.PrefetchAssets, value);

                if (_loadFlags != opts)
                {
                    _loadFlags = opts;
                    this.RaisePropertyChanged(nameof(IsPrefetchAssetsChecked));
                    OnFlagChanged(true);
                }
            }
        }

        public IReadOnlyList<string> ScaleItems
        {
            get {return _scaleItems; }
        }

        public int ScaleSelectedIndex
        {
            get { return _scaleSelectedIndex; }
            set { SetScaleIndex(value, true); }
        }

        public bool HasContent
        {
            get { return _hasContent; }
            set { this.RaiseAndSetIfChanged(ref _hasContent, value, nameof(HasContent)); }
        }

        public bool IsPlainTextViewable
        {
            get { return _isPlainTextViewable; }
            set { this.RaiseAndSetIfChanged(ref _isPlainTextViewable, value, nameof(IsPlainTextViewable)); }
        }

        public bool IsXamlViewable
        {
            get { return _isXamlViewable; }

            set
            {
                if (_isXamlViewable != value)
                {
                    _isXamlViewable = value;
                    this.RaisePropertyChanged(nameof(IsXamlViewable));
                    this.RaisePropertyChanged(nameof(IsXamlViewOpen));
                    this.RaisePropertyChanged(nameof(XamlViewIcon));
                    this.RaisePropertyChanged(nameof(XamlViewDarkIcon));
                }
            }
        }

        public bool IsXamlViewOpen
        {
            get { return _isXamlViewOpen && IsXamlViewable; }

            set
            {
                if (_isXamlViewOpen != value)
                {
                    _isXamlViewOpen = value;
                    this.RaisePropertyChanged(nameof(IsXamlViewOpen));
                    this.RaisePropertyChanged(nameof(XamlViewIcon));
                    this.RaisePropertyChanged(nameof(XamlViewDarkIcon));
                }
            }
        }

        public IImage? XamlViewIcon
        {
            get { return IsXamlViewOpen ? Global.Assets.DownIcon : Global.Assets.UpIcon; }
        }

        public IImage? XamlViewDarkIcon
        {
            get { return IsXamlViewOpen ? AssetModel.DownDark : AssetModel.UpDark; }
        }

        /// <summary>
        /// Gets the scale as a ratio value.
        /// </summary>
        public double ScaleFactor { get; private set; } = 1.0;

        public void GridLinesToggle()
        {
            IsGridLinesChecked = !IsGridLinesChecked;
        }

        public void GridColorsToggle()
        {
            IsGridColorsChecked = !IsGridColorsChecked;
        }

        public void DisableEventsToggle()
        {
            IsDisableEventsChecked = !IsDisableEventsChecked;
        }

        public void PrefetchAssetsToggle()
        {
            IsPrefetchAssetsChecked = !IsPrefetchAssetsChecked;
        }

        public void ClearLoadFlags()
        {
            if (_loadFlags != LoadFlags.None)
            {
                LoadFlags = LoadFlags.None;
                OnFlagChanged(true);
            }
        }

        public void SetScaleIndex(int index, bool invoke)
        {
            index = Math.Clamp(index, 0, _scaleItems.Count - 1);

            if (_scaleSelectedIndex != index)
            {
                _scaleSelectedIndex = index;
                OnScaleChanged(invoke);
            }
        }

        public void SetNormScale()
        {
            SetNormScale(true);
        }

        public void SetNormScale(bool invoke)
        {
            if (_scaleSelectedIndex != _scaleNormIndex)
            {
                _scaleSelectedIndex = _scaleNormIndex;
                OnScaleChanged(invoke);
            }
        }

        public void IncScale()
        {
            IncScale(true);
        }

        public void IncScale(bool invoke)
        {
            int idx = _scaleSelectedIndex + 1;

            if (idx < _scaleItems.Count)
            {
                _scaleSelectedIndex = idx;
                OnScaleChanged(invoke);
            }
        }

        public void DecScale()
        {
            DecScale(true);
        }

        public void DecScale(bool invoke = true)
        {
            int idx = _scaleSelectedIndex - 1;

            if (idx > -1)
            {
                _scaleSelectedIndex = idx;
                OnScaleChanged(invoke);
            }
        }

        /// <summary>
        /// Override.
        /// </summary>
        protected override void ColorChangedHandler()
        {
            base.ColorChangedHandler();
            this.RaisePropertyChanged(nameof(LoadFlagIcon));
        }

        /// <summary>
        /// Called when any flag changes. Override should call base.
        /// </summary>
        protected virtual void OnFlagChanged(bool invoke)
        {
            try
            {
                this.RaisePropertyChanged(nameof(HasAnyLoadFlag));
                this.RaisePropertyChanged(nameof(LoadFlagIcon));
                this.RaisePropertyChanged(nameof(LoadFlagDarkIcon));

                if (invoke)
                {
                    LoadFlagChecked?.Invoke(_loadFlags);
                }
            }
            catch(Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        private void OnScaleChanged(bool invoke)
        {
            try
            {
                var s = _scaleItems[_scaleSelectedIndex];
                ScaleFactor = double.Parse(s.TrimEnd('%')) / 100;
                this.RaisePropertyChanged(nameof(ScaleFactor));
                this.RaisePropertyChanged(nameof(ScaleSelectedIndex));

                if (invoke)
                {
                    ScaleChanged?.Invoke(this);
                }
            }
            catch(Exception e)
            {
                Debug.WriteLine(e);
            }
        }

    }
}