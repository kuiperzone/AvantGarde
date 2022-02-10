// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvantGarde.Projects;
using AvantGarde.Utility;

namespace AvantGarde.Views
{
    /// <summary>
    /// Shows solution properties.
    /// </summary>
    public partial class SolutionWindow : AvantWindow
    {
        private readonly NumericUpDown _depthUpDown;
        private readonly CheckBox _showEmptyCheck;
        private readonly RadioButton _debugRadio;
        private readonly RadioButton _releaseRadio;
        private readonly TextBox _filePatternBox;
        private readonly TextBox _excludeDirectoriesBox;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SolutionWindow()
        {
            AvaloniaXamlLoader.Load(this);

            _depthUpDown = this.FindOrThrow<NumericUpDown>("DepthUpDown");
            _showEmptyCheck = this.FindOrThrow<CheckBox>("ShowEmptyCheck");
            _debugRadio = this.FindOrThrow<RadioButton>("DebugRadio");
            _releaseRadio = this.FindOrThrow<RadioButton>("ReleaseRadio");
            _filePatternBox = this.FindOrThrow<TextBox>("FilePatternBox");
            _excludeDirectoriesBox = this.FindOrThrow<TextBox>("ExcludeDirectoriesBox");

#if DEBUG
            this.AttachDevTools();
#endif
        }

        /// <summary>
        /// Gets or sets the properties. The instance is modified on OK.
        /// </summary>
        public SolutionProperties? Properties { get; set; }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
;
            if (Properties != null)
            {
                UpdateView(Properties);
            }
        }

        private void UpdateView(SolutionProperties properties)
        {
            _depthUpDown.Value = properties.SearchDepth;
            _showEmptyCheck.IsChecked = properties.ShowEmptyDirectories;
            _debugRadio.IsChecked = properties.Build == BuildKind.Debug;
            _releaseRadio.IsChecked = properties.Build == BuildKind.Release;
            _filePatternBox.Text = properties.FilePatterns;
            _excludeDirectoriesBox.Text = properties.ExcludeDirectories;
        }

        private void ResetClickHandler(object? sender, RoutedEventArgs e)
        {
            UpdateView(new SolutionProperties());
        }

        private void OkClickHandler(object? sender, RoutedEventArgs e)
        {
            if (Properties != null)
            {
                Properties.SearchDepth = (int)_depthUpDown.Value;
                Properties.ShowEmptyDirectories = _showEmptyCheck.IsChecked == true;
                Properties.Build = _debugRadio.IsChecked == true ? BuildKind.Debug : BuildKind.Release;
                Properties.FilePatterns = _filePatternBox.Text;
                Properties.ExcludeDirectories = _excludeDirectoriesBox.Text;
            }

            Close(true);
        }

        private void CancelClickHandler(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

    }

}