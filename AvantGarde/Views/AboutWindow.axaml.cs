// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde.Views
{
    /// <summary>
    /// About AvantGarde.
    /// </summary>
    public partial class AboutWindow : AvantWindow
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AboutWindow()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void WebPressedHandler(object? sender, PointerPressedEventArgs e)
        {
            ShellOpen.Start(GlobalModel.WebpageUrl);
        }

        private void OkClickHandler(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }

}