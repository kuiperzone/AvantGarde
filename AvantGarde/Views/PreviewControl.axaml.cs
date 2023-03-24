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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvantGarde.Loading;
using AvantGarde.Settings;
using AvantGarde.ViewModels;

namespace AvantGarde.Views
{
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
        /// Occurs when the user clicks on "Goto" to locate error.
        /// </summary>
        public Action<PreviewError>? GotoClick;

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
        /// Updates the preview at the given scale.
        /// </summary>
        public void Update(PreviewPayload? payload, double scale)
        {
            Debug.WriteLine($"{nameof(PreviewControl)}.{nameof(PreviewControl.Update)}");

            Payload = payload;
            IsEmpty = payload?.Source == null;
            Debug.WriteLine("PAYLOAD: " + payload?.Name ?? "{null}");
            Debug.WriteLine("IsEmpty: " + IsEmpty);
            Debug.WriteLine("Dimension: " + payload?.Source?.PixelSize);
            Debug.WriteLine("Error: " + payload?.Error);

            if (payload?.Source != null)
            {
                _model.IsWindow = payload.IsWindow;
                _model.WindowTitleScale = scale;
                _model.WindowIcon = payload.WindowIcon;
                _model.WindowTitleText = payload.WindowTitle;
                _model.WindowCanResize = payload.WindowCanResize;

                _model.MainImage = payload.Source;
                _model.MainBackground = Brushes.Gray;
                _model.WidthText = payload.Width.ToString(true);
                _model.HeightText = payload.Height.ToString(true);
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
                _model.MessageText = payload.Error.Message;
                _model.MainImage ??= GlobalModel.Global.Assets.WarnIcon;
            }
            else
            {
                _model.HasErrorLocation = false;
                _model.MessageText = _model.MainImage == null ? "None" : null;
            }
        }

        /// <summary>
        /// Gets a bitmap of the image, which may include the window top-bar.
        /// </summary>
        public Bitmap? GetBitmap()
        {
            if (Payload?.Source != null &&  Payload.IsWindow == true)
            {
                var window = new Window();

                try
                {
                    var clone = new PreviewControl();
                    var temp = Payload.Clone();
                    temp.Error = null;

                    clone.Update(temp, Scale);

                    window.Content = clone;
                    window.SizeToContent |= SizeToContent.Width | SizeToContent.Height;
                    window.SizeToContent |= SizeToContent.Height;

                    // Keep window from displaying
                    window.ShowInTaskbar = false;
                    window.WindowState = WindowState.Minimized;
                    window.SystemDecorations = SystemDecorations.None;

                    window.Show();

                    double scale = 1.0;
                    int width = (int)(window.Width * scale);
                    int height = (int)(window.Height * scale);

                    double scale96 = scale * 96;
                    var pxz = new PixelSize(width, height);
                    var dpi = new Vector(scale96, scale96);
                    var bmp = new RenderTargetBitmap(pxz, dpi);

                    bmp.Render(window);
                    return bmp;
                }
                finally
                {
                    window?.Close();
                }
            }

            return Payload?.Source;
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
                PointerEventOccurred?.Invoke(new PointerEventMessage(visual, e));
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
    }
}