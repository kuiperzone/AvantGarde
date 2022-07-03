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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvantGarde.Projects;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde.Views
{
    /// <summary>
    /// Project explorer control, including a toolbar.
    /// </summary>
    public partial class ExplorerPane : UserControl
    {
        private readonly ProjectTree _tree;
        private readonly ExplorerPaneViewModel _model = new();

        /// <summary>
        /// Constructor.
        /// </summary>
        public ExplorerPane()
        {
            DataContext = _model;
            AvaloniaXamlLoader.Load(this);

            _tree = this.FindOrThrow<ProjectTree>("ProjectTree");
            _tree.SelectionChanged += (() => { SelectionChanged?.Invoke(); });
            _tree.PropertiesClicked += (p => { ProjectPropertiesClicked?.Invoke(p); });

            SetSolution(null);
        }

        /// <summary>
        /// Occurs when the selected item changes. The supplied value is null if nothing is selected.
        /// </summary>
        public event Action? SelectionChanged;

        /// <summary>
        /// Occurs when user clicks on the open file button.
        /// </summary>
        public event Action? OpenSolutionClicked;

        /// <summary>
        /// Occurs when user clicks on the solution properties button.
        /// </summary>
        public event Action? SolutionPropertiesClicked;

        /// <summary>
        /// Occurs when user clicks on a project properties button.
        /// </summary>
        public event Action<DotnetProject>? ProjectPropertiesClicked;

        /// <summary>
        /// Occurs when user clicks on the toggle view button.
        /// </summary>
        public event Action? ToggleViewClicked;

        /// <summary>
        /// Gets or sets whether the control is open. The initial value is true. Does not call clicked event.
        /// </summary>
        public bool IsViewOpen
        {
            get { return _model.IsViewOpen; }

            set
            {
                if (_model.IsViewOpen != value)
                {
                    _model.IsViewOpen = value;

                    if (Parent != null)
                    {
                        Measure(Size.Infinity);
                    }
                }
            }
        }

        /// <summary>
        /// Get or sets the solution. Setting null closes the solution.
        /// </summary>
        public DotnetSolution? Solution
        {
            get { return _tree.Solution; }

            set
            {
                if (_tree.Solution != value)
                {
                    SetSolution(value);
                }
            }
        }

        /// <summary>
        /// Gets the selected item. This can include projects as well as file nodes.
        /// </summary>
        public PathItem? SelectedItem
        {
            get { return _tree.SelectedItem; }
        }

        /// <summary>
        /// Gets the project associated with the selected item.
        /// </summary>
        public DotnetProject? SelectedProject
        {
            get { return _tree.SelectedProject; }
        }

        /// <summary>
        /// Gets the minimum working width.
        /// </summary>
        public double MinWorkingWidth
        {
            get { return _model. Global.IconSize * 4; }
        }

        /// <summary>
        /// Calls <see cref="DotnetSolution.Refresh"/> and updates the view. The result is true if the solution has
        /// changed.
        /// </summary>
        public bool Refresh(bool force = false)
        {
            var changed = Solution?.Refresh() == true;

            if (changed || force)
            {
                _tree.Refresh();
            }

            return changed;
        }

        /// <summary>
        /// Attempts to select an item given leaf or full name. Returns true on success.
        /// </summary>
        public bool TrySelect(string? name)
        {
            var item = _tree.Solution?.Find(name);

            if (item != null)
            {
                _tree.SelectedItem = item;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Collapse all items.
        /// </summary>
        public void CollapseAll()
        {
            _tree.CollapseAll();
        }

        private void SetSolution(DotnetSolution? value)
        {
            _tree.Solution = value;
            _model.TitleText = value?.SolutionName?.ToUpperInvariant();
            _model.IsLoaded = value != null;

            if (Parent != null)
            {
                Measure(Size.Infinity);
            }
        }

        private void OpenSolutionClickHandler(object? sender, RoutedEventArgs? e)
        {
            OpenSolutionClicked?.Invoke();
        }

        private void SolutionPropertiesClickHandler(object? sender, RoutedEventArgs? e)
        {
            if (Solution != null)
            {
                SolutionPropertiesClicked?.Invoke();
            }
        }

        private void CollapseClickHandler(object? sender, RoutedEventArgs? e)
        {
            CollapseAll();
        }

        private void ToggleViewClickHandler(object? sender, RoutedEventArgs? e)
        {
            IsViewOpen = !IsViewOpen;
            ToggleViewClicked?.Invoke();
        }

    }
}