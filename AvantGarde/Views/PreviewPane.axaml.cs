// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022
// LICENSE   : GPL-3.0-or-later
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
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

using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AvantGarde.Loading;
using AvantGarde.Projects;
using AvantGarde.Settings;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde.Views
{
    /// <summary>
    /// Implements the preview widget, scrollbox and related controls.
    /// </summary>
    public partial class PreviewPane : UserControl
    {
        private readonly CodeTextBox _plainText;
        private readonly PreviewControl _previewControl;
        private readonly Grid _xamlGrid;
        private readonly XamlCodeControl _xamlCode;

        private readonly DispatcherTimer _timer;
        private readonly PreviewPaneViewModel _model = new();
        private int _caretIndex = int.MinValue;

        public PreviewPane()
        {
            _model.Owner = this;
            DataContext = _model;
            AvaloniaXamlLoader.Load(this);

            _model.LoadFlagChecked += LoadFlagCheckedHandler;
            _model.ScaleChanged += ScaleChangedHandler;

            _previewControl = this.FindOrThrow<PreviewControl>("PreviewControl");
            _previewControl.PointerEventOccurred += PointerEventHandler;
            _previewControl.GotoClick += GotoClickHander;

            _plainText = this.FindOrThrow<CodeTextBox>("PlainTextBox");
            _xamlGrid = this.FindOrThrow<Grid>("XamlGrid");
            _xamlCode = this.FindOrThrow<XamlCodeControl>("XamlCode");

            _timer = new(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, TimerHandler);
            _timer.Start();

            Update(null);
        }

        /// <summary>
        /// Occurs when preview option flag is changed.
        /// </summary>
        public event Action<LoadFlags>? LoadFlagChecked;

        /// <summary>
        /// Occurs when user clicks restart/refresh.
        /// </summary>
        public event Action? RestartClicked;

        /// <summary>
        /// Occurs when scale is changed.
        /// </summary>
        public event Action<PreviewOptionsViewModel>? ScaleChanged;

        /// <summary>
        /// Occurs when the user interracts with the preview.
        /// </summary>
        public Action<PointerEventMessage>? PointerEventOccurred;

        /// <summary>
        /// Gets or sets the preview window color.
        /// </summary>
        public PreviewWindowTheme Theme
        {
            get { return _previewControl.Theme; }
            set { _previewControl.Theme = value; }
        }

        /// <summary>
        /// Gets or sets load options.
        /// </summary>
        public LoadFlags LoadFlags
        {
            get { return _model.LoadFlags; }
            set { _model.LoadFlags = value; }
        }

        /// <summary>
        /// Gets scale factor.
        /// </summary>
        public double ScaleFactor
        {
            get { return _model.ScaleFactor; }
        }

        /// <summary>
        /// Gets or sets the scale setting. Does not invoke change event.
        /// </summary>
        public int ScaleIndex
        {
            get { return _model.ScaleSelectedIndex; }
            set { _model.SetScaleIndex(value, false); }
        }

        /// <summary>
        /// Gets or sets whether the XAML code is open. Does not call event.
        /// </summary>
        public bool IsXamlViewOpen
        {
            get { return _model.IsXamlViewOpen; }

            set
            {
                if (_model.IsXamlViewOpen != value)
                {
                    _model.IsXamlViewOpen = value;
                    ResetSplitter();
                }
            }
        }

        /// <summary>
        /// Gets has any content, including a non-xaml image.
        /// </summary>
        public bool HasContent
        {
            get { return _model.HasContent; }
        }

        /// <summary>
        /// Gets whether showing plain text only.
        /// </summary>
        public bool IsPlainTextViewable
        {
            get { return _model.IsPlainTextViewable; }
        }

        /// <summary>
        /// Gets whether control has a XAML preview.
        /// </summary>
        public bool IsXamlViewable
        {
            get { return _model.IsXamlViewable; }
        }

        /// <summary>
        /// Gets or sets process output text, so that text may be modified after update.
        /// </summary>
        public string? OutputText
        {
            get { return _xamlCode.OutputText; }
            set { _xamlCode.OutputText = value; }
        }

        /// <summary>
        /// Does not invoke change event.
        /// </summary>
        public void SetNormScale()
        {
            _model.SetNormScale(false);
        }

        /// <summary>
        /// Does not invoke change event.
        /// </summary>
        public void IncScale()
        {
            _model.IncScale(false);
        }

        /// <summary>
        /// Does not invoke change event.
        /// </summary>
        public void DecScale()
        {
            _model.DecScale(false);
        }

        /// <summary>
        /// Copies preview bitmap.
        /// </summary>
        public void CopyToClipboard()
        {
            // TBD - need clipboard BMP support
            // var bmp = _previewControl.GetBitmap();
            throw new NotImplementedException();
        }

        /// <summary>
        /// Invokes <see cref="RestartClicked"/>/
        /// </summary>
        public void OnRestart()
        {
            try
            {
                RestartClicked?.Invoke();
            }
            catch(Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        /// <summary>
        /// Updates the content. The result is true if there is content (not necessarily a XAML preview).
        /// </summary>
        public bool Update(PreviewPayload? payload)
        {
            Debug.WriteLine($"{nameof(PreviewPane)}.{nameof(PreviewPane.Update)}");

            if (payload != null && !payload.IsProjectHeader)
            {
                Debug.WriteLine("Payload: " + payload.ItemKind);
                _model.HasContent = payload.Source != null || payload.Text != null;
                _model.IsXamlViewable = payload.ItemKind == PathKind.Xaml;
                _model.IsPlainTextViewable = payload.Text != null && payload.Source == null && !_model.IsXamlViewable;
                _previewControl.Update(payload, ScaleFactor);
            }
            else
            {
                Debug.WriteLine("Payload: none");
                _model.HasContent = false;
                _model.IsXamlViewable = false;
                _model.IsPlainTextViewable = false;
                _previewControl.Update(null, ScaleFactor);
            }

            if (_model.IsPlainTextViewable)
            {
                _plainText.Text = payload?.Text;
            }

            if (_xamlCode.Update(payload))
            {
                ResetSplitter();
            }

            return _model.HasContent;
        }

        private RowDefinition GetSplitRow()
        {
            var row = _xamlGrid.RowDefinitions[2];

            if (row != null)
            {
                return row;
            }

            Debug.WriteLine("ERROR: SplitGrid row 2 not found");
            throw new ArgumentNullException("SplitGrid row 2 not found");
        }

        private void ResetSplitter()
        {
            if (_model.IsXamlViewOpen && _model.IsXamlViewable)
            {
                Debug.WriteLine("Open");
                double h = (this.GetOwnerWindow()?.Height ?? 600) / 4;
                GetSplitRow().Height = new GridLength(h);
            }
            else
            {
                Debug.WriteLine("Closed");
                GetSplitRow().Height = new GridLength(0);
            }
        }

        private void SplitterDragHandler(object? sender, VectorEventArgs e)
        {
            IsXamlViewOpen = GetSplitRow().Height.Value > 0;
        }

        private void ScrollPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Unfocus TextBox
            FocusManager.Instance?.Focus(null);
        }

        private void PointerEventHandler(PointerEventMessage e)
        {
            if (!_model.IsDisableEventsChecked)
            {
                PointerEventOccurred?.Invoke(e);
            }
        }

        private void LoadFlagCheckedHandler(LoadFlags flags)
        {
            LoadFlagChecked?.Invoke(flags);
        }

        private void ScaleChangedHandler(PreviewOptionsViewModel sender)
        {
            ScaleChanged?.Invoke(sender);
        }

        private void GotoClickHander(PreviewError error)
        {
            Debug.WriteLine(nameof(PreviewPane) + "." + nameof(GotoClickHander));

            if (_model.IsXamlViewable)
            {
                IsXamlViewOpen = true;
                _xamlCode.SetCaretPos(error.LineNum, error.LinePos);
            }
        }

        private void TimerHandler(object? sender, EventArgs e)
        {
            // Update caret position.
            // Not sure of a better way to do this other than a timer.
            if (_plainText.IsVisible)
            {
                var idx = _plainText.CaretIndex;

                if (_caretIndex != idx)
                {
                    _caretIndex = idx;
                    _model.CaretText = _plainText.GetCaretLabel();
                }
            }
            else
            if (_xamlCode.IsVisible)
            {
                var idx = _xamlCode.GetCaretIndex();

                if (_caretIndex != idx)
                {
                    _caretIndex = idx;
                    _model.CaretText = _xamlCode.GetCaretLabel();
                }
            }
            else
            {
                _caretIndex = -1;
                _model.CaretText = null;
            }
        }
    }
}