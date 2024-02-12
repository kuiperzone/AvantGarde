// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022-23
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
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvantGarde.Loading;
using AvantGarde.Markup;
using AvantGarde.Projects;
using AvantGarde.Settings;
using AvantGarde.ViewModels;

namespace AvantGarde.Views;

public partial class MainWindow : AvantWindow<MainWindowViewModel>
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan BuildDelay = TimeSpan.FromMilliseconds(900);

    private readonly SolutionCache _cache = new();
    private readonly RemoteLoader _loader;
    private readonly DispatcherTimer _refreshTimer;
    private bool _writeSettingsFlag;

    // Added to watch for build changes
    private NodeItem? _buildWatcher;
    private DateTime _suspendPreviewUntil = DateTime.MinValue;

    public MainWindow()
        : base(new MainWindowViewModel())
    {
        InitializeComponent();

        Title = "Avant Garde";
        Model.Owner = this;
        Model.ScaleChanged += ScaleChangedHandler;
        Model.LoadFlagChecked += LoadFlagCheckedHandler;

        ExplorerPane.SelectionChanged += SelectionChangedHandler;
        ExplorerPane.OpenSolutionClicked += OpenSolutionDialog;
        ExplorerPane.SolutionPropertiesClicked += ShowSolutionPropertiesDialog;
        ExplorerPane.ProjectPropertiesClicked += ShowProjectPropertiesDialog;
        ExplorerPane.ToggleViewClicked += ResetSplitter;

        PreviewPane.ScaleChanged += ScaleChangedHandler;
        PreviewPane.LoadFlagChecked += LoadFlagCheckedHandler;
        PreviewPane.RestartClicked += RestartHost;
        PreviewPane.PointerEventOccurred += PointerEventHandler;

        _cache.Read();
        _loader = new();
        _loader.PreviewReady += PreviewReadyHandler;
        _loader.OutputReceived += OutputReceivedHandler;
        _refreshTimer = new(RefreshInterval, DispatcherPriority.Normal, RefreshTimerHandler);

        Model.WelcomeWidth = ExplorerPane.MinWorkingWidth;
        Model.IsPinVisible = App.Settings.ShowPin;
        PreviewPane.WindowTheme = App.Settings.PreviewTheme;

        PropertyChanged += PropertyChangedHandler;
        LoadFlagCheckedHandler(PreviewPane.LoadFlags);
#if DEBUG
        this.AttachDevTools();
#endif
    }

    public async void OpenSolutionDialog()
    {
        var opts = new FilePickerOpenOptions();
        opts.Title = "Open Solution or Project";
        opts.AllowMultiple = false;

        var type = new FilePickerFileType("Solution (*.sln; *.csproj; *.fsproj)");
        type.Patterns = new string[] { "*.sln", "*.csproj", "*.fsproj" };
        opts.FileTypeFilter = new FilePickerFileType[] { type };

        var paths = await StorageProvider.OpenFilePickerAsync(opts);

        if (paths?.Count > 0)
        {
            OpenSolution(Uri.UnescapeDataString(paths[0].Path.AbsolutePath));
        }
    }

    public void OpenSolution(string path, bool openExplorer = true)
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(OpenSolution)}");
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

            ExplorerPane.Solution = sol;
            _buildWatcher = CreateBuildWatcher(ExplorerPane.SelectedProject);
            PreviewPane.IsPreviewSuspended = false;

            Model.HasSolution = true;
            Model.HasProject = ExplorerPane.SelectedProject != null;
            Model.IsWelcomeVisible = GetIsWelcomeVisible(true);

            App.Settings.UpsertRecent(path);
            _writeSettingsFlag = true;

            SetExplorerView(openExplorer);
            PreviewPane.Update(null);
        }
        catch (Exception e)
        {
            MessageBox.ShowDialog(this, e);
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
            await MessageBox.ShowDialog(this, e);
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
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(CloseSolution)}");
        ExplorerPane.Solution = null;

        _buildWatcher = null;
        _suspendPreviewUntil = DateTime.MinValue;

        Model.HasSolution = false;
        Model.HasProject = false;
        Model.IsWelcomeVisible = GetIsWelcomeVisible(false);
    }

    public void SetExplorerView(bool? open = null)
    {
        ExplorerPane.IsViewOpen = open ?? !ExplorerPane.IsViewOpen;
        ResetSplitter();
    }

    public void Copy()
    {
        PreviewPane.CopyToClipboard();
    }

    public async void ShowSolutionPropertiesDialog()
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(ShowSolutionPropertiesDialog)}");

        if (ExplorerPane?.Solution != null)
        {
            Debug.WriteLine(ExplorerPane.Solution.SolutionName);

            var dialog = new SolutionWindow();
            dialog.Properties = ExplorerPane.Solution.Properties;

            // Leave it to timer to pick up change
            if (await dialog.ShowDialog<bool>(this))
            {
                _cache.Upsert(ExplorerPane.Solution);
                _cache.Write();
            }
        }
    }

    public async void ShowProjectPropertiesDialog(DotnetProject? project = null)
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(ShowProjectPropertiesDialog)}");
        project ??= ExplorerPane.SelectedProject;

        if (project != null && ExplorerPane.Solution != null)
        {
            Debug.WriteLine(project.ProjectName);
            var dialog = new ProjectWindow();
            dialog.Project = project;

            // Leave it to timer to pick up change
            if (await dialog.ShowDialog<bool>(this))
            {
                _cache.Upsert(ExplorerPane.Solution);
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
            Model.IsWelcomeVisible = GetIsWelcomeVisible(ExplorerPane.Solution != null);
            Model.IsPinVisible = App.Settings.ShowPin;
            PreviewPane.WindowTheme = App.Settings.PreviewTheme;
        }
    }

    public void RestartHost()
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(RestartHost)}");

        // Stop and restart
        _loader.Stop();
        UpdateLoader(ExplorerPane.SelectedItem);
    }

    public void ToggleXamlView()
    {
        PreviewPane.IsXamlViewOpen = !PreviewPane.IsXamlViewOpen;
    }

    public async void ShowAboutDialog()
    {
        var dialog = new AboutWindow();
        await dialog.ShowDialog(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(OnOpened)}");

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

                    foreach (var file in item.GetDirectoryInfo().EnumerateFiles("*.?sproj"))
                    {
                        var extension = file.Extension;
                        if (extension != ".csproj" && extension != ".fsproj") {
                            continue;
                        }
                        OpenSolution(file.FullName, openExplorer);
                        ExplorerPane.TrySelect(App.Arguments["s"] ?? App.Arguments["select"] ?? fullname);
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

    [return: NotNullIfNotNull(nameof(project))]
    private static string? GetWatchDirectory(DotnetProject? project)
    {
        if (project != null)
        {
            var projDir = project.Contents.FullName;

            if (project.Properties.AssemblyOverride != null)
            {
                // Take into account custom assembly may be outside project directory
                var asmDir = new PathItem(project.Properties.AssemblyOverride, PathKind.Assembly).FullName;

                if (!asmDir.EndsWith(Path.DirectorySeparatorChar))
                {
                    asmDir += Path.DirectorySeparatorChar;
                }

                if (!asmDir.StartsWith(projDir, PathItem.PlatformComparison))
                {
                    return asmDir;
                }
            }

            return projDir;
        }

        return null;
    }

    private static NodeItem? CreateBuildWatcher(DotnetProject? project)
    {
        if (project != null)
        {
            // We specifically want to watch for files which, when changed, indicate a
            // build is in progress. This should exclude *.axaml and *.cs files.
            var node = new NodeItem(GetWatchDirectory(project), PathKind.Directory);
            node.Properties.SearchDepth = project.Contents.Properties.SearchDepth;
            node.Properties.ExcludeDirectories = "";
            node.Properties.FilePatterns = "*.cache;*.pdb;*.dll;*.exe";
            node.Refresh();
            return node;
        }

        return null;
    }

    private void AboutPressedHandler(object? sender, PointerPressedEventArgs e)
    {
        ShowAboutDialog();
    }

    private void PropertyChangedHandler(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        switch (e.Property.Name)
        {
            case nameof(Width):
                if (WindowState != WindowState.Maximized && App.Settings.Width != Width)
                {
                    App.Settings.Width = DescaledWidth;
                    _writeSettingsFlag = true;
                }
                break;
            case nameof(Height):
                if (WindowState != WindowState.Maximized && App.Settings.Height != Height)
                {
                    App.Settings.Height = DescaledHeight;
                    _writeSettingsFlag = true;
                }
                break;
            case nameof(WindowState):
                App.Settings.IsMaximized = WindowState == WindowState.Maximized;
                _writeSettingsFlag = true;
                break;
        }
    }

    private bool GetIsWelcomeVisible(bool hasSolution)
    {
        return !hasSolution && ExplorerPane.IsViewOpen && App.Settings.ShowWelcome;
    }

    private void ResetSplitter()
    {
        var col = SplitGrid.ColumnDefinitions[0] ??
            throw new ArgumentNullException(nameof(SplitGrid.ColumnDefinitions));

        col.Width = GridLength.Auto;
        Model.IsWelcomeVisible = GetIsWelcomeVisible(ExplorerPane.Solution != null);
    }

    private void PreviewReadyHandler(PreviewPayload? payload)
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(PreviewReadyHandler)}");
        Model.HasContent = PreviewPane.Update(payload);
        Model.IsXamlViewable = PreviewPane.IsXamlViewable;
        Model.IsPlainTextViewable = PreviewPane.IsPlainTextViewable;
    }

    private void OutputReceivedHandler(string output)
    {
        PreviewPane.OutputText = output;
    }

    private void UpdateLoader(PathItem? item)
    {
        if (DateTime.UtcNow > _suspendPreviewUntil)
        {
            Debug.WriteLine("");
            Debug.WriteLine("");
            Debug.WriteLine("######################################");
            Debug.WriteLine($"LOAD UPDATE: {item?.Name ?? "[null]"}");
            Debug.WriteLine("######################################");
            _loader.Update(new LoadPayload(item, PreviewPane.LoadFlags));
        }
        else
        {
            Debug.WriteLine($"LOAD UPDATE DELAY");
            _loader.Update(new LoadPayload(new ProjectError("Please wait...")));
        }
    }

    private void SelectionChangedHandler()
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(SelectionChangedHandler)}");
        var item = ExplorerPane.SelectedItem;
        Debug.WriteLine("NEW SELECTED: " + item?.Name ?? "{null}");

        if (GetWatchDirectory(ExplorerPane.SelectedProject) != _buildWatcher?.FullName)
        {
            Debug.WriteLine("Create new watcher");
            _buildWatcher = CreateBuildWatcher(ExplorerPane.SelectedProject);
        }

        UpdateLoader(item);

        Title = "Avant Garde" + (item != null ? " - " + item.Name : null);
        Model.HasProject = ExplorerPane.SelectedProject != null;
    }

    private void LoadFlagCheckedHandler(LoadFlags value)
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(LoadFlagCheckedHandler)}");
        Model.LoadFlags = value;
        PreviewPane.LoadFlags = value;
        UpdateLoader(ExplorerPane.SelectedItem);
    }

    private void ScaleChangedHandler(PreviewOptionsViewModel sender)
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(ScaleChangedHandler)} = {sender.ScaleFactor}");
        _loader.Scale = sender.ScaleFactor;
        PreviewPane.ScaleIndex = sender.ScaleSelectedIndex;
        Model.SetScaleIndex(sender.ScaleSelectedIndex, false);
    }

    private void PointerEventHandler(PointerEventMessage e)
    {
        Debug.WriteLineIf(e.IsPressOrReleased, $"{nameof(MainWindow)}.{nameof(PointerEventHandler)}");
        _loader.SendPointerEvent(e);
    }

    private void SplitterDragHandler(object? sender, VectorEventArgs e)
    {
        var col = SplitGrid.ColumnDefinitions[0];

        if (col != null)
        {
            ExplorerPane.IsViewOpen = col.Width.Value >= ExplorerPane.MinWorkingWidth;
            Model.IsWelcomeVisible = GetIsWelcomeVisible(ExplorerPane.Solution != null);
        }
    }

    private void RefreshTimerHandler(object? sender, EventArgs e)
    {
        try
        {
            bool refreshed = ExplorerPane.Refresh();
            _buildWatcher ??= CreateBuildWatcher(ExplorerPane.SelectedProject);

            if (_buildWatcher != null && _buildWatcher.Refresh())
            {
                Debug.WriteLine("BUILD CHANGE DETECTED");
                Debug.WriteLine($"Halt preview host for: {_buildWatcher.FullName}");
                PreviewPane.IsPreviewSuspended = true;
                _suspendPreviewUntil = DateTime.UtcNow + BuildDelay;

                // Stop the preview host
                _loader.Stop();
            }
            else
            if (PreviewPane.IsPreviewSuspended && DateTime.UtcNow > _suspendPreviewUntil)
            {
                Debug.WriteLine("RESTART AFTER BUILD");
                PreviewPane.IsPreviewSuspended = false;
                UpdateLoader(ExplorerPane.SelectedItem);
            }
            else
            if (refreshed && !PreviewPane.IsPreviewSuspended)
            {
                // Non-blocking
                Debug.WriteLine("EXPLORER REFRESH");
                Debug.WriteLine($"Selected: {ExplorerPane.SelectedItem?.ToString() ?? "null"}");
                UpdateLoader(ExplorerPane.SelectedItem);
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
            MessageBox.ShowDialog(this, x);
            CloseSolution();
        }
    }

}