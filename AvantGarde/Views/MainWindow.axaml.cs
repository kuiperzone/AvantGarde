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
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvantGarde.Loading;
using AvantGarde.Markup;
using AvantGarde.Projects;
using AvantGarde.Settings;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde.Views;

public partial class MainWindow : AvantWindow<MainWindowViewModel>
{
    private readonly ExplorerPane _explorerPane;
    private readonly Grid _splitGrid;
    private readonly PreviewPane _previewPane;
    private readonly SolutionCache _cache = new();
    private readonly RemoteLoader _loader;
    private readonly DispatcherTimer _refreshTimer;
    private bool _writeSettingsFlag;

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
        _previewPane.WindowTheme = App.Settings.PreviewTheme;

        PropertyChanged += PropertyChangedHandler;
        LoadFlagCheckedHandler(_previewPane.LoadFlags);
#if DEBUG
        this.AttachDevTools();
#endif
    }

    public async void OpenSolutionDialog()
    {
        var opts = new FilePickerOpenOptions();
        opts.Title = "Open Solution or Project";
        opts.AllowMultiple = false;

        var type = new FilePickerFileType("Solution (*.sln; *.csproj)");
        type.Patterns = new string[] { "*.sln", "*.csproj" };
        opts.FileTypeFilter = new FilePickerFileType[] { type };

        var paths = await StorageProvider.OpenFilePickerAsync(opts);

        if (paths?.Count > 0)
        {
            OpenSolution(paths[0].Path.AbsolutePath);
        }
    }

    public void OpenSolution(string path, bool openExplorer = true)
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
            _writeSettingsFlag = true;

            SetExplorerView(openExplorer);
            _previewPane.Update(null);
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
            var opts = new FilePickerSaveOptions();
            opts.Title = "Export Avalonia Schema";
            opts.DefaultExtension = "xsd";
            opts.ShowOverwritePrompt = true;
            opts.SuggestedFileName = "AvaloniaSchema-" + MarkupDictionary.Version + ".xsd";

            var type = new FilePickerFileType("XSD (*.xsd)");
            type.Patterns = new string[] { "*.xsd" };
            opts.FileTypeChoices = new FilePickerFileType[] { type };

            var path = await StorageProvider.SaveFilePickerAsync(opts);

            if (path != null)
            {
                SchemaGenerator.SaveDocument(path.Path.AbsolutePath, Model.IsFormattedXsdChecked, Model.IsAnnotationXsdChecked);
            }
        }
        catch (Exception e)
        {
            ShowError(e);
        }
        finally
        {
            EndDialog();
        }
    }

    public async void ShowSolutionDefaultsDialog()
    {
        var dialog = new SolutionWindow();
        dialog.Title = "Solution Defaults";
        dialog.Properties = App.Settings.SolutionDefaults;

        try
        {
            StartDialog();

            if (await dialog.ShowDialog<bool>(this))
            {
                App.Settings.Write();
            }
        }
        finally
        {
            EndDialog();
        }
    }

    public void CloseSolution()
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(MainWindow.CloseSolution)}");
        _explorerPane.Solution = null;

        Model.HasSolution = false;
        Model.HasProject = false;
        Model.IsWelcomeVisible = GetIsWelcomeVisible(false);
    }

    public void SetExplorerView(bool? open = null)
    {
        _explorerPane.IsViewOpen = open ?? !_explorerPane.IsViewOpen;
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

            try
            {
                StartDialog();

                // Leave it to timer to pick up change
                if (await dialog.ShowDialog<bool>(this))
                {
                    _cache.Upsert(_explorerPane.Solution);
                    _cache.Write();
                }
            }
            finally
            {
                EndDialog();
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

            try
            {
                StartDialog();

                // Leave it to timer to pick up change
                if (await dialog.ShowDialog<bool>(this))
                {
                    _cache.Upsert(_explorerPane.Solution);
                    _cache.Write();
                }
            }
            finally
            {
                EndDialog();
            }
        }
    }

    public async void ShowPreferencesDialog()
    {
        var dialog = new SettingsWindow();
        dialog.Settings = App.Settings;

        try
        {
            StartDialog();

            if (await dialog.ShowDialog<bool>(this))
            {
                App.Settings.Write();
                Model.IsWelcomeVisible = GetIsWelcomeVisible(_explorerPane.Solution != null);
                _previewPane.WindowTheme = App.Settings.PreviewTheme;
                _explorerPane.Refresh(true);
            }
        }
        finally
        {
            EndDialog();
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
        StartDialog();
        var dialog = new AboutWindow();
        await dialog.ShowDialog(this);
        EndDialog();
    }

    protected override void OnOpened(EventArgs e)
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(MainWindow.OnOpened)}");

        Width = App.Settings.Width;
        Height = App.Settings.Height;

        base.OnOpened(e);
        _refreshTimer.Start();

        if (App.Arguments != null)
        {
            var path = App.Arguments.Value;
            Debug.WriteLine(App.Arguments.ToString());

            var openExplorer = !(App.Arguments.GetOrDefault("m", false) || App.Arguments.GetOrDefault("min-explorer", false));

            if (openExplorer && App.Settings.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }

            if (path != null)
            {
                var item = new PathItem(path, PathKind.AnyFile);

                if (item.Kind == PathKind.Solution)
                {
                    OpenSolution(item.FullName, openExplorer);
                    return;
                }

                var fullname = item.FullName;

                while (item.ParentDirectory.Length != 0 && item.Exists)
                {
                    item = new PathItem(item.ParentDirectory, PathKind.Directory);

                    foreach (var file in item.GetDirectoryInfo().EnumerateFiles("*.csproj"))
                    {
                        OpenSolution(file.FullName, openExplorer);
                        _explorerPane.TrySelect(App.Arguments["s"] ?? App.Arguments["select"] ?? fullname);
                        return;
                    }
                }
            }

            SetExplorerView(openExplorer);
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

    private void PropertyChangedHandler(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        switch (e.Property.Name)
        {
            case nameof(Window.Width):
                if (WindowState != WindowState.Maximized && App.Settings.Width != Width)
                {
                    App.Settings.Width = DescaledWidth;
                    _writeSettingsFlag = true;
                }
                break;
            case nameof(Window.Height):
                if (WindowState != WindowState.Maximized && App.Settings.Height != Height)
                {
                    App.Settings.Height = DescaledHeight;
                    _writeSettingsFlag = true;
                }
                break;
            case nameof(Window.WindowState):
                App.Settings.IsMaximized = WindowState == WindowState.Maximized;
                _writeSettingsFlag = true;
                break;
        }
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

            if (_writeSettingsFlag)
            {
                Debug.WriteLine("Write settings");
                _writeSettingsFlag = false;
                App.Settings.Write();
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
        try
        {
            StartDialog();
            await MessageBox.ShowDialog(this, e);
        }
        finally
        {
            EndDialog();
        }
    }

    // TBD - for possible future removal
    private void StartDialog()
    {
        // Temporary fix
        // https://github.com/AvaloniaUI/Avalonia/issues/7694
        Topmost = false;
        _refreshTimer.Stop();
    }

    // TBD - for possible future removal
    private void EndDialog()
    {
        Topmost = Model.IsTopmost;
        _refreshTimer.Start();
    }
}