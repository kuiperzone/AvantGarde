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
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using AvantGarde.Loading;
using AvantGarde.Settings;
using AvantGarde.ViewModels;

namespace AvantGarde.Views;

/// <summary>
/// Implements the central preview widget, without surrounding area or related controls.
/// </summary>
public partial class PreviewControl : UserControl
{
    private readonly PreviewControlViewModel _model = new();

    /// <summary>
    /// Constructor.
    /// </summary>
    public PreviewControl()
    {
        DataContext = _model;
        AvaloniaXamlLoader.Load(this);
        Update(null, 1.0);
    }

    /// <summary>
    /// Occurs when the user interacts with the preview.
    /// </summary>
    public Action<PointerEventMessage>? PointerEventOccurred;

    /// <summary>
    /// Occurs when the user types while the preview has focus. The handler returns true if it
    /// forwarded the message to the guest, which decides whether the gesture is also consumed - a
    /// key the guest never received must be left for AvantGarde to act on instead.
    /// </summary>
    public Func<KeyboardEventMessage, bool>? KeyboardEventOccurred;

    /// <summary>
    /// Occurs when the wheel is turned over the preview bitmap, carrying both the gesture and the
    /// message which would forward it to the guest. The handler decides which of the two it wants
    /// and marks the arguments handled if it consumed them - only the enclosing pane knows whether
    /// the wheel should zoom, pan its own scroll viewer, or reach the guest.
    /// </summary>
    public Action<PointerWheelEventArgs, PointerEventMessage>? WheelEventOccurred;

    /// <summary>
    /// Occurs when the user clicks on "Goto" to locate error.
    /// </summary>
    public Action<PreviewError>? GotoClick;

    /// <summary>
    /// Occurs when the user clicks on "Build" to clear a missing assembly.
    /// </summary>
    public Action? BuildClick;

    /// <summary>
    /// Gets or sets whether the Build button is enabled. It is disabled while a build runs.
    /// </summary>
    public bool IsBuildEnabled
    {
        get { return _model.IsBuildEnabled; }
        set { _model.IsBuildEnabled = value; }
    }

    /// <summary>
    /// Gets or sets the preview window color.
    /// </summary>
    public PreviewWindowTheme WindowTheme
    {
        get { return _model.Theme; }
        set { _model.Theme = value; }
    }

    /// <summary>
    /// Gets whether has content. This may be true if non-xaml image.
    /// </summary>
    public bool IsEmpty { get; private set; }

    /// <summary>
    /// Gets the current payload.
    /// </summary>
    public PreviewPayload? Payload  { get; private set; }

    /// <summary>
    /// Gets the current scale value.
    /// </summary>
    public double Scale { get; private set; } = 1.0;

    /// <summary>
    /// Gets the size in dips of what the control draws around the preview bitmap - the window
    /// top-bar above it and the dimension labels either side. Fit-to-window has to allow for it,
    /// and it is measured rather than modelled because it varies with the payload: the top-bar is
    /// there only for a window, the labels only when dimensions are shown, and the top-bar scales
    /// with the zoom. The result is empty until a bitmap has been arranged.
    /// </summary>
    public Size ChromeSize
    {
        get
        {
            var image = _model.MainImage?.Size ?? default;
            var bounds = Bounds.Size;

            if (!(image.Width > 0) || !(image.Height > 0) ||
                bounds.Width < image.Width || bounds.Height < image.Height)
            {
                return default;
            }

            return new Size(bounds.Width - image.Width, bounds.Height - image.Height);
        }
    }

    /// <summary>
    /// Updates the preview at the given scale.
    /// </summary>
    public void Update(PreviewPayload? payload, double scale, bool showDimensions = true)
    {
        Debug.WriteLine($"{nameof(PreviewControl)}.{nameof(Update)}");
        Debug.WriteLine("PAYLOAD: " + payload?.Name ?? "{null}");
        Debug.WriteLine("Dimension: " + payload?.Source?.PixelSize);
        Debug.WriteLine("Error: " + payload?.Error);

        Payload = payload;
        IsEmpty = payload?.Source == null;

        if (payload?.Source != null)
        {
            _model.IsWindow = payload.IsWindow;
            _model.WindowTitleScale = scale;
            _model.WindowIcon = payload.WindowIcon;
            _model.WindowTitleText = payload.WindowTitle;
            _model.WindowCanResize = payload.WindowCanResize;

            _model.MainImage = payload.Source;
            _model.MainBackground = GlobalModel.Global.Colors.PreviewTile;

            if (showDimensions)
            {
                _model.WidthText = GetDimensionText(payload.Width, payload.NaturalWidth);
                _model.HeightText = GetDimensionText(payload.Height, payload.NaturalHeight);
            }
            else
            {
                _model.WidthText = null;
                _model.HeightText = null;
            }
        }
        else
        {
            _model.IsWindow = false;
            _model.MainImage = null;
            _model.MainBackground = null;
            _model.WidthText = null;
            _model.HeightText = null;
        }

        if (payload?.Error != null)
        {
            Debug.WriteLine("Error line: " + payload.Error.LineNum);
            _model.HasErrorLocation = payload.Error.LineNum > 0;
            _model.HasBuildAction = payload.Error.CanBuild;
            _model.MessageText = payload.Error.Message;
            _model.MainImage ??= GlobalModel.Global.Assets.WarnIcon;
        }
        else
        {
            _model.HasErrorLocation = false;
            _model.HasBuildAction = false;
            _model.MessageText = _model.MainImage == null ? "None" : null;
        }
    }

    /// <summary>
    /// Gets a bitmap of the image, which may include the window top-bar.
    /// </summary>
    public Bitmap? GetBitmap()
    {
        if (Payload?.Source != null && Payload.IsWindow == true)
        {
            var window = new Window();

            try
            {
                var clone = new PreviewControl();
                var temp = Payload.Clone();
                temp.Error = null;

                clone.Update(temp, Scale, false);

                // Keep window from displaying
                window.ShowInTaskbar = false;
                window.WindowState = WindowState.Minimized;
                window.WindowDecorations = WindowDecorations.None;

                window.Content = clone;
                window.SizeToContent = SizeToContent.WidthAndHeight;

                window.Show();

                var pxz = new PixelSize((int)window.DesiredSize.Width, (int)window.DesiredSize.Height);
                var bmp = new RenderTargetBitmap(pxz, new Vector(96, 96));

                bmp.Render(window);

                window.Close();
                return bmp;
            }
            finally
            {
                window?.Close();
            }
        }

        return Payload?.Source;
    }

    /// <summary>
    /// Formats a dimension label, preferring the size the designer host actually rendered at over
    /// the locally parsed d:DesignWidth/Height. The two agree where a design size is declared; where
    /// one is not, the host's value is the only one there is and the declared value shows as NaN.
    /// Any min/max from the markup is retained, as it still describes the control.
    /// </summary>
    private static string GetDimensionText(ControlDimension declared, double natural)
    {
        if (!double.IsFinite(natural) || natural <= 0)
        {
            return declared.ToString(true);
        }

        natural = Math.Round(natural);

        if (declared.HasRange)
        {
            return new ControlDimension(natural, declared.Min, declared.Max).ToString(true);
        }

        return new ControlDimension(natural).ToString(true);
    }

    /// <summary>
    /// Override. Forwards the key press to the guest.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        HandleKey(e, true);
    }

    /// <summary>
    /// Override. Forwards the key release to the guest.
    /// </summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        HandleKey(e, false);
    }

    /// <summary>
    /// Override. Forwards composed text to the guest.
    /// </summary>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);

        if (!e.Handled && !string.IsNullOrEmpty(e.Text))
        {
            // Consumed only if it went somewhere. Nothing in AvantGarde acts on text input reaching
            // the window, so consuming costs nothing and keeps a typed character from being taken
            // as a menu access key - but with forwarding switched off there is nothing to consume
            // it on behalf of.
            e.Handled = KeyboardEventOccurred?.Invoke(new KeyboardEventMessage(e)) == true;
        }
    }

    /// <summary>
    /// Sends a key transition to the guest, and decides whether AvantGarde keeps the gesture.
    /// </summary>
    private void HandleKey(KeyEventArgs e, bool isDown)
    {
        if (e.Handled)
        {
            return;
        }

        bool forwarded = KeyboardEventOccurred?.Invoke(new KeyboardEventMessage(e, isDown)) == true;

        // Every key is offered to the guest; only the Handled flag is in question, and it decides
        // whether AvantGarde acts on the gesture as well. Consuming is the exception rather than the
        // rule, for two measured reasons - and never happens for a key which was not forwarded,
        // because with events disabled these keys must go on scrolling the pane as they did before.
        //
        // Consuming a KeyDown suppresses the TextInput which would have followed it - the Win32
        // backend drops the WM_CHAR for a key event the application handled - so a blanket
        // "consume unmodified keys" stops the preview receiving any typed character at all. That
        // was measured against the fixture: letters vanished while Backspace, which produces no
        // character, still worked.
        //
        // And every HotKey in MainWindow.axaml carries Ctrl or Alt. HotKey installs a KeyBinding on
        // the window, evaluated on the bubbled KeyDown, so consuming a modified gesture here would
        // disable the menu accelerators whenever the preview has focus.
        //
        // What is left is the set of keys which produce no text and which the enclosing ScrollViewer
        // would otherwise act on, scrolling the pane instead of reaching the guest. Space belongs to
        // that set by behaviour but not by consequence - it types - so it is left to bubble.
        e.Handled = forwarded && IsScrollKey(e.Key) && e.KeyModifiers == KeyModifiers.None;
    }

    /// <summary>
    /// Returns true for keys the pane's scroll viewer acts on, and which carry no text of their own.
    /// </summary>
    private static bool IsScrollKey(Key key)
    {
        switch (key)
        {
            case Key.Left:
            case Key.Right:
            case Key.Up:
            case Key.Down:
            case Key.PageUp:
            case Key.PageDown:
            case Key.Home:
            case Key.End:
                return true;
            default:
                return false;
        }
    }

    private void PreviewPointerMovedHandler(object? sender, PointerEventArgs e)
    {
        if (sender is Visual visual)
        {
            PointerEventOccurred?.Invoke(new PointerEventMessage(visual, e));
        }
    }

    private void PreviewPointerPressedHandler(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Visual visual)
        {
            // Both sides of the click matter. This one gives the preview the keyboard focus within
            // AvantGarde, so that OnKeyDown and OnTextInput start firing here; the forwarded press
            // below gives the guest a focused control for them to be delivered to, which it does not
            // otherwise have. Keyboard forwarding does not work until the user has clicked the
            // preview, and this is why.
            Focus();

            PointerEventOccurred?.Invoke(new PointerEventMessage(visual, e));
        }
    }

    private void PreviewPointerWheelHandler(object? sender, PointerWheelEventArgs e)
    {
        if (sender is Visual visual)
        {
            // The message is built here, against the sender, for the same reason the pointer
            // handlers above do it: the class does not call the generated InitializeComponent, so
            // its x:Name fields are never assigned and reaching for the image by name would give
            // null - which GetCurrentPoint accepts, silently returning coordinates relative to the
            // window instead of to the preview.
            WheelEventOccurred?.Invoke(e, new PointerEventMessage(visual, e));
        }
    }

    private void PreviewPointerReleasedHandler(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Visual visual)
        {
            PointerEventOccurred?.Invoke(new PointerEventMessage(visual, e));
        }
    }

    private void GotoClickHandler(object? sender, RoutedEventArgs e)
    {
        if (Payload?.Error != null)
        {
            GotoClick?.Invoke(Payload.Error);
        }
    }

    private void BuildClickHandler(object? sender, RoutedEventArgs e)
    {
        BuildClick?.Invoke();
    }
}