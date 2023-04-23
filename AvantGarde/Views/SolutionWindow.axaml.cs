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
                // Accept warning - check needed for Avalonia 11
                if (_depthUpDown.Value != null)
                {
                    Properties.SearchDepth = (int)_depthUpDown.Value;
                }

                Properties.ShowEmptyDirectories = _showEmptyCheck.IsChecked == true;
                Properties.Build = _debugRadio.IsChecked == true ? BuildKind.Debug : BuildKind.Release;
                Properties.FilePatterns = _filePatternBox.Text ?? "";
                Properties.ExcludeDirectories = _excludeDirectoriesBox.Text ?? "";
            }

            Close(true);
        }

        private void CancelClickHandler(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

    }

}