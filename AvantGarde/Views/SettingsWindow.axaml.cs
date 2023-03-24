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
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvantGarde.Settings;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde.Views
{
    /// <summary>
    /// Shows settings.
    /// </summary>
    public partial class SettingsWindow : AvantWindow
    {
        private readonly RadioButton _lightRadio;
        private readonly RadioButton _darkRadio;
        private readonly NumericUpDown _appFontUpDown;
        private readonly NumericUpDown _monoFontUpDown;
        private readonly TextBox _monoFontBox;
        private readonly ComboBox _previewCombo;
        private readonly CheckBox _welcomeCheck;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SettingsWindow()
        {
            AvaloniaXamlLoader.Load(this);
            _lightRadio = this.FindOrThrow<RadioButton>("LightRadio");
            _darkRadio = this.FindOrThrow<RadioButton>("DarkRadio");

            _appFontUpDown = this.FindOrThrow<NumericUpDown>("AppFontUpDown");
            _appFontUpDown.Minimum = (decimal)GlobalModel.MinFontSize;
            _appFontUpDown.Maximum = (decimal)GlobalModel.MaxFontSize;

            _monoFontUpDown = this.FindOrThrow<NumericUpDown>("MonoFontUpDown");
            _monoFontUpDown.Minimum = (decimal)GlobalModel.MinFontSize;
            _monoFontUpDown.Maximum = (decimal)GlobalModel.MaxFontSize;

            _monoFontBox = this.FindOrThrow<TextBox>("MonoFontBox");
            _previewCombo = this.FindOrThrow<ComboBox>("PreviewCombo");
            _welcomeCheck = this.FindOrThrow<CheckBox>("WelcomeCheck");

            _previewCombo.ItemsSource = Enum.GetValues(typeof(PreviewWindowTheme));
            _previewCombo.SelectedItem = PreviewWindowTheme.DarkGray;

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
            _lightRadio.IsChecked = !settings.IsDarkTheme;
            _darkRadio.IsChecked = settings.IsDarkTheme;
            _appFontUpDown.Value = (decimal)settings.AppFontSize;
            _monoFontUpDown.Value = (decimal)settings.MonoFontSize;
            _monoFontBox.Text = settings.MonoFontFamily;
            _previewCombo.SelectedItem = settings.PreviewTheme;
            _welcomeCheck.IsChecked = settings.ShowWelcome;
        }

        private void ResetClickHandler(object? sender, RoutedEventArgs e)
        {
            UpdateView(new AppSettings());
        }

        private void OkClickHandler(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("OK Click");

            if (Settings != null)
            {
                Settings.IsDarkTheme = _darkRadio.IsChecked == true;

                if (_appFontUpDown.Value != null)
                {
                    Settings.AppFontSize = (double)_appFontUpDown.Value;
                }

                if (_monoFontUpDown.Value != null)
                {
                    Settings.MonoFontSize = (double)_monoFontUpDown.Value;
                }

                Settings.MonoFontFamily = _monoFontBox.Text ?? Settings.MonoFontFamily;
                Settings.PreviewTheme = (PreviewWindowTheme?)_previewCombo.SelectedItem ?? PreviewWindowTheme.DarkGray;
                Settings.ShowWelcome = _welcomeCheck.IsChecked == true;
                Debug.WriteLine(Settings.PreviewTheme);
            }

            Close(true);
        }

        private void CancelClickHandler(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

    }

}