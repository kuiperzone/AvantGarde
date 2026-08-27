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
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using AvantGarde.Loading;
using AvantGarde.Projects;
using AvantGarde.Settings;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde.Views;

/// <summary>
/// Implements the preview widget, scrollbox and related controls.
/// </summary>
public partial class PreviewPane : UserControl
{
    // A drag fires SizeChanged per pixel. Recomputing the fit on each one would push a DPI change
    // to the designer host at the same rate, so the pane settles first.
    private static readonly TimeSpan FitInterval = TimeSpan.FromMilliseconds(150);

    // Keeps the fitted preview clear of the scroll viewer's edges, and stops a factor which lands
    // exactly on the bounds from summoning the scrollbars it was sized to avoid.
    private const double FitInset = 8;

    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _fitTimer;
    private readonly PreviewPaneViewModel _model = new();
    private int _caretIndex = int.MinValue;
    private Size _naturalSize;

    public PreviewPane()
    {
        _model.Owner = this;
        DataContext = _model;
        InitializeComponent();

        _model.LoadFlagChecked += LoadFlagCheckedHandler;
        _model.ScaleChanged += ScaleChangedHandler;

        XamlCode.IsVisible = false;
        PreviewControl.PointerEventOccurred += PointerEventHandler;
        PreviewControl.KeyboardEventOccurred += KeyboardEventHandler;
        PreviewControl.WheelEventOccurred += WheelEventHandler;
        PreviewControl.GotoClick += GotoClickHander;
        PreviewControl.BuildClick += BuildClickHandler;

        _timer = new(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, TimerHandler);
        _timer.Start();

        _fitTimer = new(FitInterval, DispatcherPriority.Normal, FitTimerHandler);

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
    /// Occurs when the user clicks Build on a missing assembly error.
    /// </summary>
    public event Action? BuildClicked;

    /// <summary>
    /// Occurs when scale is changed.
    /// </summary>
    public event Action<PreviewOptionsViewModel>? ScaleChanged;

    /// <summary>
    /// Occurs when the fit-to-window factor has been recomputed and <see cref="ScaleFactor"/> now
    /// differs. Kept separate from <see cref="ScaleChanged"/>, which reports the user picking a
    /// scale - routing this through it would re-enter the handler that asked for the fit.
    /// </summary>
    public event Action? FitScaleChanged;

    /// <summary>
    /// Occurs when the user interacts with the preview.
    /// </summary>
    public Action<PointerEventMessage>? PointerEventOccurred;

    /// <summary>
    /// Occurs when the user types while the preview has focus.
    /// </summary>
    public Action<KeyboardEventMessage>? KeyboardEventOccurred;

    /// <summary>
    /// Gets or sets the preview window color.
    /// </summary>
    public PreviewWindowTheme WindowTheme
    {
        get { return PreviewControl.WindowTheme; }
        set { PreviewControl.WindowTheme = value; }
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
    /// Gets or sets whether a solution is open.
    /// </summary>
    public bool HasSolution
    {
        get { return _model.HasSolution; }
        set { _model.HasSolution = value; }
    }

    /// <summary>
    /// Gets or sets whether the Build button is enabled. It is disabled while a build runs.
    /// </summary>
    public bool IsBuildEnabled
    {
        get { return PreviewControl.IsBuildEnabled; }
        set { PreviewControl.IsBuildEnabled = value; }
    }

    /// <summary>
    /// Gets or sets whether preview is suspended.
    /// </summary>
    public bool IsPreviewSuspended
    {
        get { return _model.IsPreviewSuspended; }
        set { _model.IsPreviewSuspended = value; }
    }

    /// <summary>
    /// Gets scale factor.
    /// </summary>
    public double ScaleFactor
    {
        get { return _model.ScaleFactor; }
    }

    /// <summary>
    /// Gets whether the scale is fit-to-window.
    /// </summary>
    public bool IsFitToWindow
    {
        get { return _model.IsFitToWindow; }
    }

    /// <summary>
    /// Recomputes the fit-to-window factor against the current pane size and the natural size last
    /// stated by the designer host. The result is true if <see cref="ScaleFactor"/> changed. Does
    /// nothing unless fit-to-window is selected and a natural size is known.
    /// </summary>
    public bool UpdateFitScale()
    {
        if (!_model.IsFitToWindow)
        {
            return false;
        }

        // The bitmap does not get the whole scroll viewer - the preview control draws a top-bar and
        // dimension labels around it, and fitting to the bare bounds overflows by exactly that much.
        var chrome = PreviewControl.ChromeSize;
        var bounds = PreviewScroll.Bounds.Size;

        var viewport = new Size(
            Math.Max(bounds.Width - FitInset - chrome.Width, 0),
            Math.Max(bounds.Height - FitInset - chrome.Height, 0));

        var factor = PreviewOptionsViewModel.CalcFitScaleFactor(_naturalSize, viewport);

        Debug.WriteLine($"{nameof(PreviewPane)}.{nameof(UpdateFitScale)}: " +
            $"natural {_naturalSize}, bounds {bounds}, chrome {chrome}, viewport {viewport}, factor {factor}");

        return _model.SetFitScaleFactor(factor);
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
    /// Gets has an image.
    /// </summary>
    public bool HasImage
    {
        get { return _model.HasImage; }
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
        get { return XamlCode.OutputText; }
        set { XamlCode.OutputText = value; }
    }

    /// <summary>
    /// Opens the code view with the OUTPUT tab selected. The split does not necessarily open at
    /// once: a transient message such as "Building..." is not a XAML item, so the code view is not
    /// viewable while one is showing and <see cref="ResetSplitter"/> keeps the row closed. It opens
    /// on the next update that does carry XAML, which is what a caller at the end of a build wants.
    /// </summary>
    public void ShowOutput()
    {
        XamlCode.ShowOutput();
        IsXamlViewOpen = true;
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
    public async void CopyToClipboard()
    {
        var window = TopLevel.GetTopLevel(this);

        if (window == null)
        {
            return;
        }

        var bmp = PreviewControl.GetBitmap();

        if (bmp != null && window.Clipboard != null)
        {
            // Avalonia 12 provides a cross-platform bitmap clipboard. This replaces both the
            // Windows-only Clowd.Clipboard helper (an unmaintained Avalonia 11 package) and the
            // manual image/png DataObject used elsewhere -- DataObject is obsolete in 12.
            await window.Clipboard.SetBitmapAsync(bmp);
        }
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
    /// Updates the content. The result is true if there is content (not necessarily an image preview).
    /// </summary>
    public bool Update(PreviewPayload? payload)
    {
        Debug.WriteLine($"{nameof(PreviewPane)}.{nameof(Update)}");
        bool rslt = false;

        // Deferred to the timer rather than computed here, because the fit needs
        // PreviewControl.ChromeSize and that is only meaningful once the new bitmap has been
        // arranged - which has not happened at this point.
        //
        // Rechecked on every frame, not only when the natural size changes, because the top-bar
        // included in the chrome scales with the zoom: the first fit after a large zoom change is
        // measured against the old top-bar and comes out conservative. The recheck converges in a
        // step or two and then stops of its own accord, on SetFitScaleFactor's deadband - a fit
        // which no longer moves pushes no scale, so no frame comes back to trigger another.
        if (payload?.NaturalWidth > 0 && payload.NaturalHeight > 0)
        {
            _naturalSize = new Size(payload.NaturalWidth, payload.NaturalHeight);

            // Started rather than restarted. Frames can arrive faster than the interval - animated
            // content does - and restarting on each would hold the timer off its tick forever, so
            // the fit would never be recomputed at all.
            if (_model.IsFitToWindow && !_fitTimer.IsEnabled)
            {
                _fitTimer.Start();
            }
        }

        if (payload != null && !payload.IsProjectHeader)
        {
            Debug.WriteLine("Payload: " + payload.ItemKind);
            _model.HasImage = payload.Source != null;
            _model.IsXamlViewable = payload.ItemKind == PathKind.Xaml;
            _model.IsPlainTextViewable = payload.Text != null && payload.Source == null && !_model.IsXamlViewable;
            _model.IsCheckered = payload.IsWindow;
            PreviewControl.Update(payload, ScaleFactor);

            rslt = payload.Source != null || payload.Text != null;
        }
        else
        {
            Debug.WriteLine("Payload: none");
            _model.HasImage = false;
            _model.IsXamlViewable = false;
            _model.IsPlainTextViewable = false;
            _model.IsCheckered = false;
            PreviewControl.Update(null, ScaleFactor);
        }

        if (_model.IsPlainTextViewable)
        {
            PlainTextBox.Text = payload?.Text;
        }

        // We are setting this here because XAML binding not working for unknown reason
        XamlCode.IsVisible = _model.IsXamlViewable;

        if (XamlCode.Update(payload))
        {
            ResetSplitter();
        }

        return rslt;
    }

    private RowDefinition GetSplitRow()
    {
        var row = XamlGrid.RowDefinitions[2];

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

    private void PointerEventHandler(PointerEventMessage e)
    {
        if (!_model.IsDisableEventsChecked)
        {
            PointerEventOccurred?.Invoke(e);
        }
    }

    /// <summary>
    /// Forwards a keyboard message to the guest, returning whether it went. False leaves the key to
    /// AvantGarde: with events disabled the preview must not swallow keys it is not passing on.
    /// </summary>
    private bool KeyboardEventHandler(KeyboardEventMessage e)
    {
        if (_model.IsDisableEventsChecked || KeyboardEventOccurred == null)
        {
            return false;
        }

        KeyboardEventOccurred.Invoke(e);
        return true;
    }

    /// <summary>
    /// Decides what a wheel turn over the preview bitmap means. There are three claims on the
    /// gesture and they are settled in this order.
    /// </summary>
    /// <remarks>
    /// Ctrl+Wheel is zoom, as it is nearly everywhere, and takes precedence.
    ///
    /// Otherwise the pane keeps the wheel while the preview is larger than the viewport on the axis
    /// being scrolled, because panning a preview too big to see is what the wheel did before this
    /// and taking that away would be a regression. Only when the pane has nothing to scroll does the
    /// wheel go to the guest - which is the common case, fit-to-window among it.
    ///
    /// Testing the extent rather than the scroll offset is deliberate: a rule of "the pane first,
    /// then the guest once the pane reaches its end" would hand the gesture over mid-drag, so the
    /// same wheel turn would do different things depending on where the pane happened to be.
    /// </remarks>
    private void WheelEventHandler(PointerWheelEventArgs e, PointerEventMessage msg)
    {
        if (e.Handled)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Delta.Y > 0)
            {
                _model.IncScale();
            }
            else
            if (e.Delta.Y < 0)
            {
                _model.DecScale();
            }

            e.Handled = true;
            return;
        }

        if (_model.IsDisableEventsChecked || CanPaneScroll(e.Delta))
        {
            return;
        }

        PointerEventOccurred?.Invoke(msg);
        e.Handled = true;
    }

    /// <summary>
    /// Returns true if the pane's own scroll viewer has somewhere to go on the axis the given wheel
    /// delta acts upon.
    /// </summary>
    private bool CanPaneScroll(Vector delta)
    {
        if (Math.Abs(delta.X) > Math.Abs(delta.Y))
        {
            return PreviewScroll.Extent.Width > PreviewScroll.Viewport.Width;
        }

        return PreviewScroll.Extent.Height > PreviewScroll.Viewport.Height;
    }

    private void LoadFlagCheckedHandler(LoadFlags flags)
    {
        LoadFlagChecked?.Invoke(flags);
    }

    private void ScaleChangedHandler(PreviewOptionsViewModel sender)
    {
        ScaleChanged?.Invoke(sender);
    }

    private void PreviewScrollSizeChangedHandler(object? sender, SizeChangedEventArgs e)
    {
        if (_model.IsFitToWindow)
        {
            // Restart, so the fit is computed once the drag stops rather than at every step.
            _fitTimer.Stop();
            _fitTimer.Start();
        }
    }

    private void FitTimerHandler(object? sender, EventArgs e)
    {
        _fitTimer.Stop();

        if (UpdateFitScale())
        {
            InvokeFitScaleChanged();
        }
    }

    private void InvokeFitScaleChanged()
    {
        try
        {
            FitScaleChanged?.Invoke();
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private void BuildClickHandler()
    {
        Debug.WriteLine(nameof(PreviewPane) + "." + nameof(BuildClickHandler));

        try
        {
            BuildClicked?.Invoke();
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }
    }

    private void GotoClickHander(PreviewError error)
    {
        Debug.WriteLine(nameof(PreviewPane) + "." + nameof(GotoClickHander));

        if (_model.IsXamlViewable)
        {
            IsXamlViewOpen = true;
            XamlCode.SetCaretPos(error.LineNum, error.LinePos);
        }
    }

    private void TimerHandler(object? sender, EventArgs e)
    {
        // Update caret position.
        // Not sure of a better way to do this other than a timer.
        if (PlainTextBox.IsVisible)
        {
            var idx = PlainTextBox.CaretIndex;

            if (_caretIndex != idx)
            {
                _caretIndex = idx;
                _model.CaretText = PlainTextBox.GetCaretLabel();
            }
        }
        else
        if (XamlCode.IsVisible)
        {
            var idx = XamlCode.GetCaretIndex();

            if (_caretIndex != idx)
            {
                _caretIndex = idx;
                _model.CaretText = XamlCode.GetCaretLabel();
            }
        }
        else
        {
            _caretIndex = -1;
            _model.CaretText = null;
        }
    }
}