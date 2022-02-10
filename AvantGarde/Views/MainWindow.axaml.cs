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
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AvantGarde.Loading;
using AvantGarde.Markup;
using AvantGarde.Projects;
using AvantGarde.Settings;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde.Views
{
    public partial class MainWindow : AvantWindow<MainWindowViewModel>
    {
        private readonly ExplorerPane _explorerPane;
        private readonly Grid _splitGrid;
        private readonly PreviewPane _previewPane;
        private readonly SolutionCache _cache = new();
        private readonly RemoteLoader _loader;
        private readonly DispatcherTimer _refreshTimer;

        public MainWindow()
            : base(new MainWindowViewModel())
        {
            AvaloniaXamlLoader.Load(this);

            Title = "Avant Garde";
            Model.Owner = this;
            Model.ScaleChanged += ScaleChangedHandler;
            Model.LoadFlagChecked += LoadFlagCheckedHandler;

            MainMenu = this.FindOrThrow<Menu>("MainMenu");
            _explorerPane = this.FindOrThrow<ExplorerPane>("ExplorerPane");
            _explorerPane.SelectionChanged += SelectionChangedHandler;
            _explorerPane.OpenSolutionClicked += OpenSolutionDialog;
            _explorerPane.SolutionPropertiesClicked += ShowSolutionPropertiesDialog;
            _explorerPane.ProjectPropertiesClicked += ShowProjectPropertiesDialog;
            _explorerPane.ToggleViewClicked += ResetSplitter;

            _splitGrid = this.FindOrThrow<Grid>("SplitGrid");

            _previewPane = this.FindOrThrow<PreviewPane>("PreviewPane");
            _previewPane.ScaleChanged += ScaleChangedHandler;
            _previewPane.LoadFlagChecked += LoadFlagCheckedHandler;
            _previewPane.RestartClicked += RestartHost;
            _previewPane.PointerEventOccurred += PointerEventHandler;

            _cache.Read();
            _loader = new();
            _loader.PreviewReady += PreviewReadyHandler;
            _loader.OutputReceived += OutputReceivedHandler;
            _refreshTimer = new(TimeSpan.FromMilliseconds(1000), DispatcherPriority.Normal, RefreshTimerHandler);

            Model.WelcomeWidth = _explorerPane.MinWorkingWidth;
            _previewPane.Theme = App.Settings.PreviewTheme;

            LoadFlagCheckedHandler(_previewPane.LoadFlags);

#if DEBUG
            this.AttachDevTools();
#endif
        }

        /// <summary>
        /// Gets main menu. Need access to this to ensure it gets closed.
        /// </summary>
        public Menu MainMenu;

        public async void OpenSolutionDialog()
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Open Solution or Project";
            dialog.Filters.Add(new FileDialogFilter() { Name = "Solutions (*.sln; *.csproj)", Extensions = { "sln", "csproj" } });

            var paths = await dialog.ShowAsync(this);

            if (paths?.Length > 0)
            {
                OpenSolution(paths[0]);
            }
        }

        public void OpenSolution(string path)
        {
            Debug.WriteLine($"{nameof(MainWindow)}.{nameof(MainWindow.OpenSolution)}");
            Debug.WriteLine(path);

            try
            {
                var sol = new DotnetSolution(path);

                // Needs refresh to populate projects
                sol.Refresh();

                if (!_cache.AssignTo(sol))
                {
                    sol.Properties.AssignFrom(App.Settings.SolutionDefaults);
                }

                _explorerPane.Solution = sol;

                Model.HasSolution = true;
                Model.HasProject = _explorerPane.SelectedProject != null;
                Model.IsWelcomeVisible = GetIsWelcomeVisible(true);

                App.Settings.UpsertRecent(path);
                App.Settings.Write();

                ResetSplitter();
                _previewPane.Update(null);
                _refreshTimer.Start();
            }
            catch (Exception e)
            {
                ShowError(e);
                CloseSolution();
            }
        }

        public async void ShowExportSchemaDialog()
        {
            try
            {
                var dialog = new SaveFileDialog();
                dialog.Title = "Export Schema";
                dialog.Filters.Add(new FileDialogFilter() { Name = "XSD (*.xsd)", Extensions = { "xsd" } });
                dialog.InitialFileName = "AvaloniaSchema-" + MarkupDictionary.Version + ".xsd";

                var path = await dialog.ShowAsync(this);

                if (!string.IsNullOrEmpty(path))
                {
                    SchemaGenerator.SaveDocument(path, Model.IsFormattedXsdChecked, Model.IsAnnotationXsdChecked);
                }
            }
            catch (Exception e)
            {
                ShowError(e);
            }
        }

        public async void ShowSolutionDefaultsDialog()
        {
            var dialog = new SolutionWindow();
            dialog.Title = "Solution Defaults";
            dialog.Properties = App.Settings.SolutionDefaults;

            if (await dialog.ShowDialog<bool>(this))
            {
                App.Settings.Write();
            }
        }

        public void CloseSolution()
        {
            Debug.WriteLine($"{nameof(MainWindow)}.{nameof(MainWindow.CloseSolution)}");
            _explorerPane.Solution = null;
            _refreshTimer.Stop();

            Model.HasSolution = false;
            Model.HasProject = false;
            Model.IsWelcomeVisible = GetIsWelcomeVisible(false);
        }

        public void ToggleExplorerView()
        {
            _explorerPane.IsViewOpen = !_explorerPane.IsViewOpen;
            ResetSplitter();
        }

        public void Copy()
        {
            _previewPane.CopyToClipboard();
        }

        public async void ShowSolutionPropertiesDialog()
        {
            Debug.WriteLine($"{nameof(MainWindow)}.{nameof(MainWindow.ShowSolutionPropertiesDialog)}");

            if (_explorerPane?.Solution != null)
            {
                Debug.WriteLine(_explorerPane.Solution.SolutionName);

                var dialog = new SolutionWindow();
                dialog.Properties = _explorerPane.Solution.Properties;

                // Leave it to timer to pick up change
                if (await dialog.ShowDialog<bool>(this))
                {
                    _cache.Upsert(_explorerPane.Solution);
                    _cache.Write();
                }
            }
        }

        public async void ShowProjectPropertiesDialog(DotnetProject? project = null)
        {
            Debug.WriteLine($"{nameof(MainWindow)}.{nameof(MainWindow.ShowProjectPropertiesDialog)}");
            project ??= _explorerPane.SelectedProject;

            if (project != null && _explorerPane.Solution != null)
            {
                Debug.WriteLine(project.ProjectName);
                var dialog = new ProjectWindow();
                dialog.Project = project;

                // Leave it to timer to pick up change
                if (await dialog.ShowDialog<bool>(this))
                {
                    _cache.Upsert(_explorerPane.Solution);
                    _cache.Write();
                }
            }
        }

        public async void ShowPreferencesDialog()
        {
            var dialog = new SettingsWindow();
            dialog.Settings = App.Settings;

            if (await dialog.ShowDialog<bool>(this))
            {
                App.Settings.Write();
                Model.IsWelcomeVisible = GetIsWelcomeVisible(_explorerPane.Solution != null);
                _previewPane.Theme = App.Settings.PreviewTheme;
                _explorerPane.Refresh(true);
            }
        }

        public void RestartHost()
        {
            Debug.WriteLine($"{nameof(MainWindow)}.{nameof(MainWindow.RestartHost)}");
            _loader.Stop();
            UpdateLoader(_explorerPane.SelectedItem);
        }

        public void ToggleXamlView()
        {
            _previewPane.IsXamlViewOpen = !_previewPane.IsXamlViewOpen;
        }

        public async void ShowAboutDialog()
        {
            var dialog = new AboutWindow();
            await dialog.ShowDialog(this);
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var args = Environment.GetCommandLineArgs();

            if (args.Length > 1)
            {
                OpenSolution(args[1]);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer.Stop();
            _loader.Dispose();
            base.OnClosed(e);
        }

        private void AboutPressedHandler(object? sender, PointerPressedEventArgs e)
        {
            ShowAboutDialog();
        }

        private bool GetIsWelcomeVisible(bool hasSolution)
        {
            return !hasSolution && _explorerPane.IsViewOpen && App.Settings.ShowWelcome;
        }

        private void ResetSplitter()
        {
            var col = _splitGrid.ColumnDefinitions[0] ??
                throw new ArgumentNullException(nameof(_splitGrid.ColumnDefinitions));

            col.Width = GridLength.Auto;
            Model.IsWelcomeVisible = GetIsWelcomeVisible(_explorerPane.Solution != null);
        }

        private void PreviewReadyHandler(PreviewPayload? payload)
        {
            Model.HasContent = _previewPane.Update(payload);
            Model.IsXamlViewable = _previewPane.IsXamlViewable;
            Model.IsPlainTextViewable = _previewPane.IsPlainTextViewable;
        }

        private void OutputReceivedHandler(string output)
        {
            _previewPane.OutputText = output;
        }

        private void UpdateLoader(PathItem? item)
        {
            Debug.WriteLine($"{nameof(MainWindow)}.{nameof(MainWindow.UpdateLoader)}");
            _loader.Update(new LoadPayload(item, _previewPane.LoadFlags));
        }

        private void SelectionChangedHandler()
        {
            Debug.WriteLine($"{nameof(MainWindow)}.{nameof(MainWindow.SelectionChangedHandler)}");
            var item = _explorerPane.SelectedItem;
            Debug.WriteLine("NEW SELECTED: " + item?.Name ?? "{null}");

            UpdateLoader(item);

            Title = "Avant Garde" + (item != null ? " - " + item.Name : null);
            Model.HasProject = _explorerPane.SelectedProject != null;
        }

        private void LoadFlagCheckedHandler(LoadFlags value)
        {
            Debug.WriteLine($"{nameof(MainWindow)}.{nameof(MainWindow.LoadFlagCheckedHandler)}");
            Model.LoadFlags = value;
            _previewPane.LoadFlags = value;
            UpdateLoader(_explorerPane.SelectedItem);
        }

        private void ScaleChangedHandler(PreviewOptionsViewModel sender)
        {
            Debug.WriteLine($"{nameof(MainWindow)}.{nameof(MainWindow.ScaleChangedHandler)} = {sender.ScaleFactor}");
            _loader.Scale = sender.ScaleFactor;
            _previewPane.ScaleIndex = sender.ScaleSelectedIndex;
            Model.SetScaleIndex(sender.ScaleSelectedIndex, false);
        }

        private void PointerEventHandler(PointerEventMessage e)
        {
            Debug.WriteLineIf(e.IsPressOrReleased, $"{nameof(MainWindow)}.{nameof(MainWindow.PointerEventHandler)}");
            _loader.SendPointerEvent(e);
        }

        private void SplitterDragHandler(object? sender, VectorEventArgs e)
        {
            var col = _splitGrid.ColumnDefinitions[0];

            if (col != null)
            {
                _explorerPane.IsViewOpen = col.Width.Value >= _explorerPane.MinWorkingWidth;
                Model.IsWelcomeVisible = GetIsWelcomeVisible(_explorerPane.Solution != null);
            }
        }

        private void RefreshTimerHandler(object? sender, EventArgs e)
        {
            try
            {
                if (_explorerPane.Refresh())
                {
                    // Non-blocking
                    UpdateLoader(_explorerPane.SelectedItem);
                }
            }
            catch (Exception x)
            {
                ShowError(x);
                CloseSolution();
            }
        }

        private async void ShowError(Exception e)
        {
            var enabled = _refreshTimer.IsEnabled;
            _refreshTimer.Stop();

            await MessageBox.ShowDialog(this, e);

            if (enabled)
            {
                _refreshTimer.Start();
            }
        }

    }
}