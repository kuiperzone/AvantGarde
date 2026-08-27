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

using System.Diagnostics;
using Avalonia;
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
        /// <summary>
        /// Index of the fit-to-window entry in <see cref="ScaleItems"/>. It heads the list because
        /// the rest of it is an ordered ladder and inserting elsewhere would break Inc/Dec.
        /// </summary>
        public const int FitScaleIndex = 0;

        /// <summary>
        /// Upper bound on the factor fit-to-window will compute. A control with a tiny natural size
        /// - a bare TextBlock measures around 91 x 19 - would otherwise demand an absurd DPI of the
        /// host and a frame to match.
        /// </summary>
        public const double MaxFitScale = 4.0;

        /// <summary>
        /// Relative change below which a recomputed fit factor is discarded. The preview control's
        /// top-bar scales with the zoom, so the space left for the bitmap depends on the very factor
        /// being solved for; without a deadband the correction can alternate between two values a
        /// fraction of a percent apart and never settle. Far below anything visible.
        /// </summary>
        public const double FitScaleDeadband = 0.005;

        private readonly int _scaleNormIndex;
        private readonly List<string> _scaleItems = new();
        private bool _hasSolution;
        private LoadFlags _loadFlags = LoadFlags.None;
        private bool _hasImage;
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
            _scaleItems.Add("Fit");
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

            _scaleNormIndex = _scaleItems.IndexOf("100%");
            _scaleSelectedIndex = _scaleNormIndex;
        }

        public bool HasSolution
        {
            get { return _hasSolution; }
            set { this.RaiseAndSetIfChanged(ref _hasSolution, value, nameof(HasSolution)); }
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

        public bool HasImage
        {
            get { return _hasImage; }
            set { this.RaiseAndSetIfChanged(ref _hasImage, value, nameof(HasImage)); }
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
                }
            }
        }

        public IImage? XamlViewIcon
        {
            get { return IsXamlViewOpen ? AssetModel.DownDark : AssetModel.UpDark; }
        }

        /// <summary>
        /// Gets the scale as a ratio value.
        /// </summary>
        public double ScaleFactor { get; private set; } = 1.0;

        /// <summary>
        /// Gets whether the scale is fit-to-window, in which case <see cref="ScaleFactor"/> is
        /// supplied by <see cref="SetFitScaleFactor"/> rather than read from the ladder.
        /// </summary>
        public bool IsFitToWindow
        {
            get { return _scaleSelectedIndex == FitScaleIndex; }
        }

        /// <summary>
        /// Computes the factor which fits a control of the given natural size into the given
        /// viewport, or NaN if either is not yet known. Never enlarges beyond
        /// <see cref="MaxFitScale"/>.
        /// </summary>
        public static double CalcFitScaleFactor(Size natural, Size viewport)
        {
            if (!(natural.Width > 0) || !(natural.Height > 0) ||
                !(viewport.Width > 0) || !(viewport.Height > 0))
            {
                return double.NaN;
            }

            var factor = Math.Min(viewport.Width / natural.Width, viewport.Height / natural.Height);

            if (!double.IsFinite(factor) || factor <= 0)
            {
                return double.NaN;
            }

            return Math.Clamp(factor, 0.01, MaxFitScale);
        }

        /// <summary>
        /// Sets <see cref="ScaleFactor"/> while fit-to-window is selected. The result is true if the
        /// value changed. Does not invoke <see cref="ScaleChanged"/> - the caller drives the loader,
        /// and re-entering the event from a size change is how a resize loop starts.
        /// </summary>
        public bool SetFitScaleFactor(double factor)
        {
            if (!IsFitToWindow || !double.IsFinite(factor) || factor <= 0)
            {
                return false;
            }

            if (Math.Abs(factor - ScaleFactor) <= ScaleFactor * FitScaleDeadband)
            {
                return false;
            }

            ScaleFactor = factor;
            this.RaisePropertyChanged(nameof(ScaleFactor));
            return true;
        }

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
            // Stepping up out of fit must not land on 25%, which is what index+1 would give and
            // would shrink the preview on a button marked "increase". Lands on the first rung above
            // the factor the fit had settled at instead.
            int idx = IsFitToWindow ? GetRungAbove(ScaleFactor) : _scaleSelectedIndex + 1;

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

            // Stops at the smallest percentage rather than stepping into fit-to-window, which is a
            // mode rather than a rung of the ladder.
            if (idx > FitScaleIndex)
            {
                _scaleSelectedIndex = idx;
                OnScaleChanged(invoke);
            }
        }

        /// <summary>
        /// Override.
        /// </summary>
        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
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

        /// <summary>
        /// Returns the index of the lowest rung whose factor exceeds the given value, or the top
        /// rung if none does. Never returns <see cref="FitScaleIndex"/>.
        /// </summary>
        private int GetRungAbove(double factor)
        {
            for (int n = FitScaleIndex + 1; n < _scaleItems.Count; ++n)
            {
                if (GetRungFactor(n) > factor)
                {
                    return n;
                }
            }

            return _scaleItems.Count - 1;
        }

        /// <summary>
        /// Parses a ladder entry. Not valid for <see cref="FitScaleIndex"/>, which carries no
        /// percentage.
        /// </summary>
        private double GetRungFactor(int index)
        {
            return double.Parse(_scaleItems[index].TrimEnd('%')) / 100;
        }

        private void OnScaleChanged(bool invoke)
        {
            try
            {
                if (!IsFitToWindow)
                {
                    // The fit entry carries no percentage. Its factor is computed against the
                    // viewport and pushed in by SetFitScaleFactor, so the last ladder value is
                    // held here until that happens.
                    ScaleFactor = GetRungFactor(_scaleSelectedIndex);
                }

                this.RaisePropertyChanged(nameof(ScaleFactor));
                this.RaisePropertyChanged(nameof(IsFitToWindow));
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
