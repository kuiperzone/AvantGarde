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
using System.Text;
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

    private readonly SolutionCache _cache = new();
    private readonly RemoteLoader _loader;
    private readonly DispatcherTimer _refreshTimer;
    private readonly StringBuilder _buildOutput = new();
    private bool _writeSettingsFlag;
    private bool _isBuilding;

    // Set where a build was detected but the preview was left running through it, which is what
    // shadow copy allows. See RefreshTimerHandler - the host still has the previous assemblies
    // loaded and nothing else would restart it.
    private bool _restartAfterBuild;

    // Added to watch for build changes
    private BuildWatcher? _buildWatcher;

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
        PreviewPane.BuildClicked += BuildProject;
        PreviewPane.PointerEventOccurred += PointerEventHandler;
        PreviewPane.KeyboardEventOccurred += KeyboardEventHandler;
        PreviewPane.FitScaleChanged += FitScaleChangedHandler;

        _cache.Read();
        _loader = new();
        _loader.PreviewReady += PreviewReadyHandler;
        _loader.OutputReceived += OutputReceivedHandler;
        _refreshTimer = new(RefreshInterval, DispatcherPriority.Normal, RefreshTimerHandler);

        Model.WelcomeWidth = ExplorerPane.MinWorkingWidth;
        Model.IsPinVisible = App.Settings.ShowPin;
        PreviewPane.WindowTheme = App.Settings.PreviewTheme;
        _loader.IsShadowCopyEnabled = App.Settings.IsShadowCopy;

        PropertyChanged += PropertyChangedHandler;
        LoadFlagCheckedHandler(PreviewPane.LoadFlags);
    }

    public async void OpenSolutionDialog()
    {
        var opts = new FilePickerOpenOptions();
        opts.Title = "Open Solution or Project";
        opts.AllowMultiple = false;

        var type = new FilePickerFileType("Solution (*.sln; *.slnx; *.csproj; *.fsproj)");
        type.Patterns = new string[] { "*.sln", "*.slnx", "*.csproj", "*.fsproj" };
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

        ClearBuildOutput();

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
            StartEvaluation();
            ResetWatcher(ExplorerPane.SelectedProject);
            PreviewPane.HasSolution = true;
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

        ClearBuildOutput();
        ResetWatcher(null);
        ExplorerPane.Solution = null;
        PreviewPane.HasSolution = false;
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
                ResetWatcher(ExplorerPane.SelectedProject);
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
                ResetWatcher(ExplorerPane.SelectedProject);
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
            ResetWatcher(ExplorerPane.SelectedProject);

            App.Settings.Write();
            Model.IsWelcomeVisible = GetIsWelcomeVisible(ExplorerPane.Solution != null);
            Model.IsPinVisible = App.Settings.ShowPin;
            PreviewPane.WindowTheme = App.Settings.PreviewTheme;

            // Takes effect on the next host start. Any restart owed by a build the running host
            // was going to sit through has already been dropped by the ResetWatcher above.
            _loader.IsShadowCopyEnabled = App.Settings.IsShadowCopy;
        }
    }

    public void RestartHost()
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(RestartHost)}");

        // Stop and restart
        _loader.Stop();
        UpdateLoader(ExplorerPane.SelectedItem);
    }

    /// <summary>
    /// Builds the selected project in the solution's build configuration, so that the user does not
    /// have to leave AvantGarde to clear a "assembly not found" error. Output goes to the OUTPUT
    /// pane as it arrives.
    /// </summary>
    public async void BuildProject()
    {
        var project = ExplorerPane.SelectedProject;

        if (project == null || _isBuilding)
        {
            return;
        }

        // The configuration must be the one the previewer looks in, not simply Debug - otherwise a
        // solution set to Release builds Debug and reports the same missing assembly afterwards.
        var path = project.FullName;
        var build = project.Solution.Properties.Build;

        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(BuildProject)} {path}, {build}");

        // Everything from here is guarded. The flag holds RefreshTimerHandler off, so leaving it set
        // on an exception would stop the application refreshing anything ever again.
        _isBuilding = true;

        try
        {
            PreviewPane.IsBuildEnabled = false;
            ClearBuildOutput();

            // The designer host holds the output assembly open, so it has to stop before MSBuild can
            // overwrite it - the same reason BuildWatcher stops it for a build started elsewhere.
            PreviewPane.IsPreviewSuspended = true;
            _loader.Stop();
            _loader.Update(new LoadPayload(new ProjectError("Building " + project.ProjectName + "...")));

            var rslt = await Task.Run(() => { return ProjectBuilder.Build(path, build, AppendBuildOutput); });
            Debug.WriteLine("BUILD RESULT: " + rslt);

            if (!rslt.IsSuccess)
            {
                AppendBuildOutput(rslt.Detail != null ? rslt.Message + ": " + rslt.Detail : rslt.Message ?? "Build failed");

                // The compiler diagnostics are the only thing that says why, and the pane they are
                // in is closed by default.
                PreviewPane.ShowOutput();
            }

            // Re-runs FindTargetAssembly, so a successful build clears the error. The preview itself
            // is left to RefreshTimerHandler, which already implements "a build just happened": it
            // waits for the output directory to stop changing before restarting the host. Restarting
            // here instead would race the tail of the build and report "Please wait...".
            ExplorerPane.Refresh(true);
        }
        catch (Exception e)
        {
            // Nothing above is expected to throw - ProjectBuilder returns failures rather than
            // raising them - so this is the last resort rather than a control path.
            Debug.WriteLine(e);
            AppendBuildOutput(e.Message);
            PreviewPane.ShowOutput();
        }
        finally
        {
            _isBuilding = false;
            PreviewPane.IsBuildEnabled = true;
        }
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
            Debug.WriteLine("ARGS: " + App.Arguments.ToString());
            string? path = App.Arguments.Values.Count != 0 ? App.Arguments.Values[0] : null;

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

                    // -s/--select applies here too. It was previously honoured only when the
                    // argument was a file within a project, which excluded the multi-project case
                    // where the solution must be opened for a library item to resolve its app.
                    var select = App.Arguments["s"] ?? App.Arguments["select"];

                    if (!string.IsNullOrEmpty(select))
                    {
                        ExplorerPane.TrySelect(select);
                    }

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
        _buildWatcher?.Dispose();
        _loader.Dispose();
        base.OnClosed(e);
    }

    private void BuildChanged()
    {
        Dispatcher.UIThread.Invoke(() => { RefreshTimerHandler(null, EventArgs.Empty); } );
    }

    private void ResetWatcher(DotnetProject? project)
    {
        // Any owed restart belonged to the watcher going away, and the new one starts with its
        // own idea of when the directory it watches last changed.
        _restartAfterBuild = false;

        // Dispose of any existing
        _buildWatcher?.Dispose();
        _buildWatcher = null;

        if (project != null)
        {
            _buildWatcher = new(project, BuildChanged);
        }
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

                // A guest with a caret or an animation renders forever, and a minimized window is
                // the one case where none of it can be seen. Withholding the frame acknowledgement
                // stops the host rendering rather than merely discarding the result.
                _loader.IsRenderPaused = WindowState == WindowState.Minimized;
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
        Model.HasImage = PreviewPane.Update(payload) && payload?.Source != null;
        Model.IsXamlViewable = PreviewPane.IsXamlViewable;
        Model.IsPlainTextViewable = PreviewPane.IsPlainTextViewable;

        if (string.IsNullOrEmpty(payload?.Output))
        {
            // The pane takes its output from the payload, and every payload until the designer host
            // has started carries none - which would wipe the build log while it is still the only
            // account of what happened. Reasserted rather than merged, because the host's own output
            // supersedes it as soon as there is any.
            RestoreBuildOutput();
        }
    }

    /// <summary>
    /// Appends a line of build output. Called from the build's own thread.
    /// </summary>
    private void AppendBuildOutput(string line)
    {
        lock (_buildOutput)
        {
            _buildOutput.AppendLine(line);
        }

        Dispatcher.UIThread.Post(RestoreBuildOutput);
    }

    private void ClearBuildOutput()
    {
        lock (_buildOutput)
        {
            _buildOutput.Clear();
        }
    }

    private void RestoreBuildOutput()
    {
        lock (_buildOutput)
        {
            if (_buildOutput.Length != 0)
            {
                PreviewPane.OutputText = _buildOutput.ToString().TrimEnd();
            }
        }
    }

    private void OutputReceivedHandler(string output)
    {
        // The designer host has something to say, which supersedes the build log and ends the
        // reassertion in PreviewReadyHandler - otherwise a log with no host to displace it would
        // follow the user to whatever they select next.
        ClearBuildOutput();
        PreviewPane.OutputText = output;
    }

    private void UpdateLoader(PathItem? item)
    {
        if (ExplorerPane.Solution?.IsEvaluating == true)
        {
            // Hold off until MSBuild has answered. Previewing now would start the designer host
            // against project values about to be superseded, and then restart it moments later.
            Debug.WriteLine("LOAD UPDATE DEFERRED - evaluating");
            _loader.Update(new LoadPayload(new ProjectError("Resolving project...")));
            return;
        }

        if (_buildWatcher == null || _buildWatcher.Elapsed > RefreshInterval)
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

        if (BuildWatcher.GetWatchDirectory(ExplorerPane.SelectedProject) != _buildWatcher?.DirectoryPath)
        {
            Debug.WriteLine("Reset watcher: " + ExplorerPane.SelectedProject ?? "{null}");
            ResetWatcher(ExplorerPane.SelectedProject);
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
        PreviewPane.ScaleIndex = sender.ScaleSelectedIndex;
        Model.SetScaleIndex(sender.ScaleSelectedIndex, false);

        if (sender.IsFitToWindow)
        {
            // Fit is not a rung of the ladder, so sender.ScaleFactor still holds the previous one.
            // Only the pane knows the viewport, so it computes the factor and this reads it back.
            PreviewPane.UpdateFitScale();
            FitScaleChangedHandler();
            return;
        }

        _loader.Scale = sender.ScaleFactor;
    }

    private void FitScaleChangedHandler()
    {
        var factor = PreviewPane.ScaleFactor;
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(FitScaleChangedHandler)} = {factor}");

        Model.SetFitScaleFactor(factor);
        _loader.Scale = factor;
    }

    private void PointerEventHandler(PointerEventMessage e)
    {
        Debug.WriteLineIf(e.IsPressOrReleased, $"{nameof(MainWindow)}.{nameof(PointerEventHandler)}");
        _loader.SendPointerEvent(e);
    }

    private void KeyboardEventHandler(KeyboardEventMessage e)
    {
        Debug.WriteLine($"{nameof(MainWindow)}.{nameof(KeyboardEventHandler)}");
        _loader.SendKeyboardEvent(e);
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

    /// <summary>
    /// Queues an MSBuild evaluation of the open solution on a worker thread, where one is needed.
    /// Never blocks the UI thread - a cold evaluation is around half a second per project.
    /// </summary>
    private void StartEvaluation()
    {
        var sol = ExplorerPane.Solution;

        if (sol == null || !sol.BeginEvaluation())
        {
            return;
        }

        Debug.WriteLine("START EVALUATION");

        // Show the resolving state now. BeginEvaluation has already marked the projects.
        ExplorerPane.Refresh(true);

        Task.Run(() =>
        {
            try
            {
                sol.Evaluate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (ExplorerPane.Solution == sol)
                {
                    Debug.WriteLine("EVALUATION COMPLETE");
                    ExplorerPane.Refresh(true);
                    UpdateLoader(ExplorerPane.SelectedItem);
                }
            });
        });
    }

    private void RefreshTimerHandler(object? _, EventArgs e)
    {
        if (_isBuilding)
        {
            // A build owns the preview state while it runs. The watcher is watching the output
            // directory being rewritten under it, and the suspension-clearing branch below would
            // otherwise fire during the quiet stretch before MSBuild writes anything and start a
            // host against the assembly being replaced.
            return;
        }

        try
        {
            bool refreshed = ExplorerPane.Refresh();

            if (ExplorerPane.Solution?.NeedsEvaluation == true)
            {
                // A project file, Directory.Build.props or Directory.Packages.props changed since
                // the last evaluation, or the build configuration was switched.
                StartEvaluation();
            }

            if (_buildWatcher == null)
            {
                // Ensure we create a watcher
                ResetWatcher(ExplorerPane.SelectedProject);
            }

            if (_buildWatcher != null && _buildWatcher.IsChanged())
            {
                Debug.WriteLine("BUILD CHANGE DETECTED");

                if (_loader.IsShadowCopyEnabled)
                {
                    // The host is running from a copy, so it is not what a build would trip over
                    // and there is nothing to gain by taking the preview down. It goes on showing
                    // the last frame until the output is quiet enough to restart against.
                    Debug.WriteLine($"Preview left running for: {_buildWatcher.DirectoryPath}");
                    _restartAfterBuild = true;
                }
                else
                {
                    Debug.WriteLine($"Halt preview host for: {_buildWatcher.DirectoryPath}");
                    PreviewPane.IsPreviewSuspended = true;

                    // Stop the preview host
                    _loader.Stop();
                }
            }
            else
            if (_buildWatcher != null && _buildWatcher.Elapsed > RefreshInterval &&
                (PreviewPane.IsPreviewSuspended || _restartAfterBuild))
            {
                Debug.WriteLine("RESTART AFTER BUILD");

                if (_restartAfterBuild)
                {
                    // Explicit, and not left to the app assembly change that UpdateThread detects.
                    // Getting it wrong here is worse than a redundant restart: a host left running
                    // is a host still serving the previous copy, and it would answer XAML updates
                    // from stale code without reporting anything.
                    _restartAfterBuild = false;
                    _loader.Stop();
                }

                PreviewPane.IsPreviewSuspended = false;
                UpdateLoader(ExplorerPane.SelectedItem);
            }
            else
            if (refreshed && !PreviewPane.IsPreviewSuspended && !_restartAfterBuild)
            {
                // The _restartAfterBuild guard matters only with shadow copy on, where the preview
                // is deliberately left up through a build. Refresh reports a change on every tick
                // while the output directory is being rewritten, and UpdateLoader answers a build
                // in flight with "Please wait..." - which would replace the live preview with a
                // placeholder, the very thing that is being avoided.
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