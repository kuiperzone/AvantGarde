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
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvantGarde.Projects;
using AvantGarde.Utility;

namespace AvantGarde.Views
{
    /// <summary>
    /// Shows project properties.
    /// </summary>
    public partial class ProjectWindow : AvantWindow
    {
        private readonly TextBlock _outputBlock;
        private readonly TextBlock _targetBlock;
        private readonly TextBlock _avaloniaBlock;

        private readonly ComboBox _appCombo;

        private readonly TextBox _assemblyOverrideBox;
        private readonly Button _browseButton;
        private readonly CheckBox _assemblyOverrideCheck;

        private readonly TextBlock _warnBlock1;
        private readonly TextBlock _warnBlock2;
        private readonly Image _warnImage;

        private readonly DispatcherTimer _timer;
        private DotnetProject? _clone;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProjectWindow()
        {
            AvaloniaXamlLoader.Load(this);

            _outputBlock = this.FindOrThrow<TextBlock>("OutputBlock");
            _targetBlock = this.FindOrThrow<TextBlock>("TargetBlock");
            _avaloniaBlock = this.FindOrThrow<TextBlock>("AvaloniaBlock");

            _appCombo = this.FindOrThrow<ComboBox>("AppCombo");

            _assemblyOverrideBox = this.FindOrThrow<TextBox>("AssemblyOverrideBox");
            _browseButton = this.FindOrThrow<Button>("BrowseButton");
            _assemblyOverrideCheck = this.FindOrThrow<CheckBox>("AssemblyOverrideCheck");

            _warnBlock1 = this.FindOrThrow<TextBlock>("WarnBlock1");
            _warnBlock2 = this.FindOrThrow<TextBlock>("WarnBlock2");
            _warnImage = this.FindOrThrow<Image>("WarnImage");

            _timer = new(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, UpdateTimerHandler);
            Closed += WindowClosedHandler;

#if DEBUG
            this.AttachDevTools();
#endif
        }

        /// <summary>
        /// Gets or sets the project. The instance is modified on OK.
        /// </summary>
        public DotnetProject? Project { get; set; }

        private void UpdateAssemblyPathControls(string? newPath = null)
        {
            Debug.WriteLine("Update assembly box");

            if (_assemblyOverrideCheck.IsChecked == true)
            {
                Debug.WriteLine("Override is true");
                _browseButton.IsEnabled = true;
                _assemblyOverrideBox.IsEnabled = true;

                if (newPath != null)
                {
                    _assemblyOverrideBox.Text = newPath;
                }
            }
            else
            {
                Debug.WriteLine("Override is false");
                _browseButton.IsEnabled = false;
                _assemblyOverrideBox.IsEnabled = false;
                _assemblyOverrideBox.Text = Project?.MakeLocalName(Project?.AssemblyPath?.FullName);
            }

            UpdateWarnings();
        }

        private void UpdateWarnings()
        {
            UpdateProject(_clone);

            if (_clone?.Refresh() == true)
            {
                var e = _clone.Error;
                _warnImage.IsVisible = !string.IsNullOrEmpty(e?.Message);

                _warnBlock1.IsVisible = _warnImage.IsVisible;
                _warnBlock1.Text = e?.Message;

                _warnBlock2.IsVisible = !string.IsNullOrEmpty(e?.Details);
                _warnBlock2.Text = e?.Details;
            }
        }

        private void UpdateProject(DotnetProject? project)
        {
            if (project != null)
            {
                // Do not refresh here
                if (_appCombo.IsEnabled)
                {
                    project.Properties.AppProjectName = _appCombo.SelectedItem as string;
                }

                var path = _assemblyOverrideBox.Text;

                if (_assemblyOverrideCheck.IsChecked == true)
                {
                    project.Properties.AssemblyOverride = project.MakeLocalName(path);
                }
                else
                {
                    project.Properties.AssemblyOverride = null;
                }
            }
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            if (Project != null)
            {
                Title = "Project - " + Project.ProjectName;

                // Keep internal clone we can make changes to
                _clone = new DotnetProject(Project.FullName, Project.Solution);

                _outputBlock.Text = Project.OutputType + " (" + Project.Solution?.Properties.Build + ")";
                _targetBlock.Text = Project.TargetFramework;
                _avaloniaBlock.Text = Project.AvaloniaVersion;

                if (!Project.IsApp)
                {
                    var temp = new List<string>();

                    if (Project.Solution != null)
                    {
                        foreach (var item in Project.Solution.Projects.Values)
                        {
                            if (item.IsApp)
                            {
                                temp.Add(item.ProjectName);
                            }
                        }
                    }

                    _appCombo.IsEnabled = true;
                    _appCombo.ItemsSource = temp;
                    _appCombo.SelectedItem = Project.GetApp()?.ProjectName;
                }
                else
                {
                    _appCombo.IsEnabled = false;
                    _appCombo.ItemsSource = new string[] { "N/A" };
                    _appCombo.SelectedIndex = 0;
                }

                _assemblyOverrideCheck.IsChecked = Project.Properties.AssemblyOverride != null;
                UpdateAssemblyPathControls(Project.Properties.AssemblyOverride);

                _timer.Start();
            }
        }

        private void WindowClosedHandler(object? sender, EventArgs e)
        {
            _timer.Stop();
            _clone = null;
        }

        private void UpdateTimerHandler(object? sender, EventArgs e)
        {
            UpdateWarnings();
        }

        private async void BrowseButtonClickHandler(object? sender, RoutedEventArgs e)
        {
            try
            {
                var opts = new FilePickerOpenOptions();
                opts.Title = $"Project Assembly ({Project?.Solution?.Properties.Build})";
                opts.AllowMultiple = false;

                var type = new FilePickerFileType("Assembly (*.dll)");
                type.Patterns = new string[] { "*.dll" };
                opts.FileTypeFilter = new FilePickerFileType[] { type };

                var paths = await StorageProvider.OpenFilePickerAsync(opts);

                if (paths?.Count > 0)
                {
                    Debug.WriteLine("BrowseButtonClickHandler: " + paths[0].Path.AbsolutePath);
                    UpdateAssemblyPathControls(paths[0].Path.AbsolutePath);
                }
            }
            catch (Exception x)
            {
                await MessageBox.ShowDialog(this, x);
            }
        }

        private void AssemblyCheckClickHandler(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("AssemblyCheckHandler Checked: " + (_assemblyOverrideCheck.IsChecked == true));
            UpdateAssemblyPathControls();
        }

        private void OkClickHandler(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("OK Click");
            UpdateProject(Project);
            Close(true);
        }

        private void CancelClickHandler(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Cancel Click");
            Close(false);
        }

    }

}