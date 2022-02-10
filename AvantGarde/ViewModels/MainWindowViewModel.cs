// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvantGarde.Utility;
using AvantGarde.Views;
using ReactiveUI;

namespace AvantGarde.ViewModels
{
    public class MainWindowViewModel : PreviewOptionsViewModel
    {
        private MainWindow? _owner;
        private bool _isTopmost;
        private bool _isWelcomeVisible;
        private double _welcomeWidth = 150;
        private bool _hasSolution;
        private bool _hasProject;
        private IEnumerable? _recentFiles;
        private bool _isFormattedXsdChecked;
        private bool _isAnnotationXsdChecked = true;

        public MainWindowViewModel()
        {
            _isTopmost = App.Settings.IsTopmost;
            _isWelcomeVisible = App.Settings.ShowWelcome;
            UpdateRecentMenu();
        }

        public MainWindow? Owner
        {
            get { return _owner; }

            set
            {
                if (value != null)
                {
                    value.Topmost = IsTopmost;
                }

                _owner = value;
            }
        }

        public bool IsTopmost
        {
            get { return _isTopmost; }

            set
            {
                if (_isTopmost != value)
                {
                    _isTopmost = value;

                    if (Owner != null)
                    {
                        Owner.Topmost = value;
                    }

                    App.Settings.IsTopmost = value;
                    App.Settings.Write();

                    this.RaisePropertyChanged(nameof(IsTopmost));
                }
            }
        }

        public bool IsWelcomeVisible
        {
            get { return _isWelcomeVisible; }
            set { this.RaiseAndSetIfChanged(ref _isWelcomeVisible, value, nameof(IsWelcomeVisible)); }
        }

        public double WelcomeWidth
        {
            get { return _welcomeWidth; }
            set { this.RaiseAndSetIfChanged(ref _welcomeWidth, value, nameof(WelcomeWidth)); }
        }

        public bool HasSolution
        {
            get { return _hasSolution; }
            set { this.RaiseAndSetIfChanged(ref _hasSolution, value, nameof(HasSolution)); }
        }

        public bool HasProject
        {
            get { return _hasProject; }
            set { this.RaiseAndSetIfChanged(ref _hasProject, value, nameof(HasProject)); }
        }

        public IEnumerable? RecentFiles
        {
            get { return _recentFiles; }
            set { this.RaiseAndSetIfChanged(ref _recentFiles, value, nameof(RecentFiles)); }
        }

        public bool IsFormattedXsdChecked
        {
            get { return _isFormattedXsdChecked; }
            set { this.RaiseAndSetIfChanged(ref _isFormattedXsdChecked, value, nameof(IsFormattedXsdChecked)); }
        }

        public bool IsAnnotationXsdChecked
        {
            get { return _isAnnotationXsdChecked; }
            set { this.RaiseAndSetIfChanged(ref _isAnnotationXsdChecked, value, nameof(IsAnnotationXsdChecked)); }
        }

        public void OpenSolutionCommand()
        {
            Owner?.OpenSolutionDialog();
            UpdateRecentMenu();
        }

        public void CloseSolutionCommand()
        {
            Owner?.CloseSolution();
        }

        public void ExportSchemaCommand()
        {
            Owner?.ShowExportSchemaDialog();
        }

        public void SolutionDefaultsCommand()
        {
            Owner?.ShowSolutionDefaultsDialog();
        }

        public void ToggleExplorerViewCommand()
        {
            Owner?.ToggleExplorerView();
        }

        public void ExitCommand()
        {
            Owner?.Close();
        }

        public void SolutionPropertiesCommand()
        {
            Owner?.ShowSolutionPropertiesDialog();
        }

        public void ProjectPropertiesCommand()
        {
            Owner?.ShowProjectPropertiesDialog();
        }

        public void CopyCommand()
        {
            Owner?.Copy();
        }

        public void PreferencesCommand()
        {
            Owner?.ShowPreferencesDialog();
        }

        public void RestartCommand()
        {
            Owner?.RestartHost();
        }

        public void ToggleXamlCommand()
        {
            Owner?.ToggleXamlView();
        }

        public void WebpageCommand()
        {
            ShellOpen.Start(GlobalModel.WebpageUrl);
        }

        public void AboutCommand()
        {
            Owner?.ShowAboutDialog();
        }

        /// <summary>
        /// Override.
        /// </summary>
        protected override void ColorChangedHandler()
        {
            base.ColorChangedHandler();
        }

        private void UpdateRecentMenu()
        {
            var menus = new List<Control>();

            foreach (var item in App.Settings.RecentFiles)
            {
                var m = new MenuItem();
                m.Header = item.Path;
                m.Tag = item.Path;
                m.Click += OpenRecentClickHandler;
                menus.Add(m);
            }

            if (menus.Count != 0)
            {
                menus.Add(new Separator());

                var m = new MenuItem();
                m.Header = "Clear";
                m.Click += ClearRecentClickHandler;
                menus.Add(m);
            }

            Debug.WriteLine("Recent count: " + menus.Count);
            RecentFiles = menus;
        }

        private void OpenRecentClickHandler(object? sender, RoutedEventArgs e)
        {
            Owner?.MainMenu.Close();
            Owner?.OpenSolution((string?)((Control?)sender)?.Tag ?? string.Empty);
            UpdateRecentMenu();
        }

        private void ClearRecentClickHandler(object? sender, RoutedEventArgs e)
        {
            Owner?.MainMenu.Close();
            App.Settings.ClearRecent();
            App.Settings.Write();
            RecentFiles = Array.Empty<string>();
        }

    }
}
