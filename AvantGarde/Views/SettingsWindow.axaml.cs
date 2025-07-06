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
using Avalonia.Interactivity;
using AvantGarde.Settings;
using AvantGarde.ViewModels;

namespace AvantGarde.Views;

/// <summary>
/// Shows settings.
/// </summary>
public partial class SettingsWindow : AvantWindow
{
    private bool _reset;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public SettingsWindow()
    {
        InitializeComponent();

        AppFontUpDown.Minimum = (decimal)GlobalModel.MinFontSize;
        AppFontUpDown.Maximum = (decimal)GlobalModel.MaxFontSize;
        MonoFontUpDown.Minimum = (decimal)GlobalModel.MinFontSize;
        MonoFontUpDown.Maximum = (decimal)GlobalModel.MaxFontSize;

        PreviewCombo.ItemsSource = Enum.GetValues(typeof(PreviewWindowTheme));
        PreviewCombo.SelectedItem = PreviewWindowTheme.DarkGray;
#if DEBUG
        this.AttachDevTools();
#endif
    }

    /// <summary>
    /// Gets or sets the settings. The instance is modified on OK.
    /// </summary>
    public AppSettings? Settings { get; set; }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (Settings != null)
        {
            UpdateView(Settings);
        }
    }

    private void UpdateView(AppSettings settings)
    {
        LightRadio.IsChecked = !settings.IsDarkTheme;
        DarkRadio.IsChecked = settings.IsDarkTheme;
        AppFontBox.Text = settings.AppFontFamily;

        AppFontUpDown.Value = (decimal)settings.AppFontSize;
        MonoFontUpDown.Value = (decimal)settings.MonoFontSize;

        MonoFontBox.Text = settings.MonoFontFamily;
        PreviewCombo.SelectedItem = settings.PreviewTheme;
        WelcomeCheck.IsChecked = settings.ShowWelcome;
        PinCheck.IsChecked = settings.ShowPin;
    }

    private void ResetClickHandler(object? sender, RoutedEventArgs e)
    {
        _reset = true;
        UpdateView(new AppSettings());
    }

    private void OkClickHandler(object? sender, RoutedEventArgs e)
    {
        if (Settings != null)
        {
            if (_reset)
            {
                // Resets window position
                var temp = new AppSettings();
                Settings.Width = temp.Width;
                Settings.Height = temp.Height;
                Settings.IsMaximized = temp.IsMaximized;
            }

            Settings.IsDarkTheme = DarkRadio.IsChecked == true;

            if (AppFontUpDown.Value != null)
            {
                Settings.AppFontSize = (double)AppFontUpDown.Value;
            }

            if (MonoFontUpDown.Value != null)
            {
                Settings.MonoFontSize = (double)MonoFontUpDown.Value;
            }

            Settings.AppFontFamily = AppFontBox.Text ?? Settings.AppFontFamily;
            Settings.MonoFontFamily = MonoFontBox.Text ?? Settings.MonoFontFamily;
            Settings.PreviewTheme = (PreviewWindowTheme?)PreviewCombo.SelectedItem ?? PreviewWindowTheme.DarkGray;
            Settings.ShowWelcome = WelcomeCheck.IsChecked == true;
            Settings.ShowPin = PinCheck.IsChecked == true;
            Debug.WriteLine(Settings.PreviewTheme);
        }

        Close(true);
    }

    private void CancelClickHandler(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

}