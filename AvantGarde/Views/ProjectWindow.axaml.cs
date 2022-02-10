// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AvantGarde.Projects;
using AvantGarde.Utility;

namespace AvantGarde.Views
{
    /// <summary>
    /// Shows project properties.
    /// </summary>
    public class ProjectWindow : AvantWindow
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
                    _appCombo.Items = temp;
                    _appCombo.SelectedItem = Project.GetApp()?.ProjectName;
                }
                else
                {
                    _appCombo.IsEnabled = false;
                    _appCombo.Items = new string[] { "N/A" };
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
                var dialog = new OpenFileDialog();
                dialog.Title = $"Project Assembly ({Project?.Solution?.Properties.Build})";
                dialog.Filters.Add(new FileDialogFilter() { Name = "Assembly (*.dll)", Extensions = { "dll" } });
                dialog.Directory = Project?.Solution?.ParentDirectory;

                var path = await dialog.ShowAsync(this);

                if (path?.Length > 0)
                {
                    Debug.WriteLine("BrowseButtonClickHandler: " + path[0]);
                    UpdateAssemblyPathControls(path[0]);
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