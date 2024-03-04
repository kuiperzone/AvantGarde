// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022-24
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

namespace AvantGarde.Views;

/// <summary>
/// Shows solution properties.
/// </summary>
public partial class SolutionWindow : AvantWindow
{
    /// <summary>
    /// Default constructor.
    /// </summary>
    public SolutionWindow()
    {
        InitializeComponent();

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
        DepthUpDown.Value = properties.SearchDepth;
        ShowEmptyCheck.IsChecked = properties.ShowEmptyDirectories;
        DebugRadio.IsChecked = properties.Build == BuildKind.Debug;
        ReleaseRadio.IsChecked = properties.Build == BuildKind.Release;
        FilePatternBox.Text = properties.FilePatterns;
        ExcludeDirectoriesBox.Text = properties.ExcludeDirectories;
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
            if (DepthUpDown.Value != null)
            {
                Properties.SearchDepth = (int)DepthUpDown.Value;
            }

            Properties.ShowEmptyDirectories = ShowEmptyCheck.IsChecked == true;
            Properties.Build = DebugRadio.IsChecked == true ? BuildKind.Debug : BuildKind.Release;
            Properties.FilePatterns = FilePatternBox.Text ?? "";
            Properties.ExcludeDirectories = ExcludeDirectoriesBox.Text ?? "";
        }

        Close(true);
    }

    private void CancelClickHandler(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

}