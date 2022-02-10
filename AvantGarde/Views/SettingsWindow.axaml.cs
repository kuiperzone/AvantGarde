// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
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
            _appFontUpDown.Minimum = GlobalModel.MinFontSize;
            _appFontUpDown.Maximum = GlobalModel.MaxFontSize;

            _monoFontUpDown = this.FindOrThrow<NumericUpDown>("MonoFontUpDown");
            _monoFontUpDown.Minimum = GlobalModel.MinFontSize;
            _monoFontUpDown.Maximum = GlobalModel.MaxFontSize;

            _monoFontBox = this.FindOrThrow<TextBox>("MonoFontBox");
            _previewCombo = this.FindOrThrow<ComboBox>("PreviewCombo");
            _welcomeCheck = this.FindOrThrow<CheckBox>("WelcomeCheck");

            _previewCombo.Items = Enum.GetValues(typeof(PreviewWindowTheme));
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
            _appFontUpDown.Value = settings.AppFontSize;
            _monoFontUpDown.Value = settings.MonoFontSize;
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
                Settings.AppFontSize = (int)_appFontUpDown.Value;
                Settings.MonoFontSize = (int)_monoFontUpDown.Value;
                Settings.MonoFontFamily = _monoFontBox.Text;
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