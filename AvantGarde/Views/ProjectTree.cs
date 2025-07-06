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

using System.Collections;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvantGarde.Projects;
using AvantGarde.ViewModels;

namespace AvantGarde.Views
{
    /// <summary>
    /// Underlying project tree view without surrounding controls. There is no XAML for this.
    /// </summary>
    public class ProjectTree : UserControl
    {
        private readonly TreeView _treeView;
        private DotnetSolution? _solution;
        private PathItem? _selected;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ProjectTree()
        {
            _treeView = new TreeView();
            _treeView.SelectionMode = SelectionMode.Single;
            _treeView.SelectionChanged += SelectionChangedHandler;

            Content = _treeView;
        }

        /// <summary>
        /// Occurs when selected item changes.
        /// </summary>
        public event Action? SelectionChanged;

        /// <summary>
        /// Occurs when use clicks project properties.
        /// </summary>
        public event Action<DotnetProject>? PropertiesClicked;

        /// <summary>
        /// Get or sets the solution.
        /// </summary>
        public DotnetSolution? Solution
        {
            get { return _solution; }

            set
            {
                if (_solution != value)
                {
                    value?.Refresh();
                    _solution = value;
                    Refresh();

                    if (_solution == null)
                    {
                        _selected = null;
                        SelectionChanged?.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the selected item. This can include projects as well as file nodes.
        /// </summary>
        public PathItem? SelectedItem
        {
            get
            {
                var view = _treeView.SelectedItem as TreeViewItem;
                return (PathItem?)view?.Tag;
            }

            set
            {
                var old = _treeView.SelectedItem as TreeViewItem;
                var sel = value?.Tag as TreeViewItem;
                Debug.WriteLine($"Set SelectedItem new: {sel?.ToString() ?? "null"}, {value?.ToString() ?? "null"}");
                Debug.WriteLine($"Set SelectedItem old: {old?.ToString() ?? "null"}, {old?.Tag?.ToString() ?? "null"}");
                Debug.WriteLine($"Contains: {_treeView.Items.Contains(sel)}");

                if (_treeView.SelectedItem != sel && (sel == null || _treeView.Items.Contains(sel)))
                {
                    Debug.WriteLine("Set confirmed");
                    _treeView.SelectedItem = value?.Tag;
                }
            }
        }

        /// <summary>
        /// Gets the project associated with the selected item.
        /// </summary>
        public DotnetProject? SelectedProject
        {
            get
            {
                var item = SelectedItem;

                if (item is NodeItem node)
                {
                    return node.Project;
                }

                if (item is DotnetProject project)
                {
                    return project;
                }

                return null;
            }
        }

        /// <summary>
        /// Refresh the view. Necessary where item states have changed. The <see cref="DotnetSolution.Refresh"
        /// is not called.
        /// </summary>
        public void Refresh()
        {
            Debug.WriteLine($"{nameof(ProjectTree)}.{nameof(Refresh)}");
            TreeViewItem? selected = null;
            List<TreeViewItem>? items = null;

            if (_solution != null)
            {
                items = new();
                Debug.WriteLine("Has solution");

                foreach (var project in _solution.Projects.Values)
                {
                    items.Add(CreateViewItem(project, ref selected));
                }
            }

            Debug.WriteLine($"ref selected: {selected?.Tag?.ToString() ?? "null"}");

            _treeView.ItemsSource = items;
            SelectedItem = (PathItem?)selected?.Tag;
            Debug.WriteLine($"Selected Now: {SelectedItem?.ToString() ?? "null"}");
        }

        /// <summary>
        /// Collapse all items.
        /// </summary>
        public void CollapseAll()
        {
            var selected = (TreeViewItem?)_treeView.SelectedItem;
            Collapse(_treeView.Items);

            if (selected?.Tag is NodeItem node && _treeView.SelectedItem != node.Project?.Tag)
            {
                // Leave top level selected
                _treeView.SelectedItem = node.Project?.Tag;
            }
        }

        private static void Collapse(IEnumerable? items)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    var view = (TreeViewItem)item;
                    Collapse(view.Items);
                    view.IsExpanded = false;
                }
            }
        }

        private static IImage? GetFileIcon(NodeItem item)
        {
            if (item.IsDirectory)
            {
                if (item.TotalFiles == 0)
                {
                    return AssetModel.GreyFolderTree;
                }

                return AssetModel.FolderTree;
            }

            switch (item.Kind)
            {
                case PathKind.Xml:
                    return AssetModel.XmlTree;
                case PathKind.Xaml:
                    return AssetModel.XamlTree;

                case PathKind.CSharp:
                    return AssetModel.CSharpTree;

                case PathKind.Solution:
                    return AssetModel.DotnetTree;

                case PathKind.Image:
                    return AssetModel.ImageTree;

                case PathKind.Assembly:
                    return AssetModel.BinTree;

                default:
                    return item.Kind.IsText() ? AssetModel.DocTree : AssetModel.UnknownTree;
            }
        }

        private static Image CreateImage(IImage? src, double size)
        {
            var img = new Image();

            // Scale to font
            img.Width = size;
            img.Height = size;
            img.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            img.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            img.Stretch = Stretch.Uniform;
            img.Source = src;

            return img;
        }

        private static TextBlock CreateTextBlock(string text)
        {
            var tb = new TextBlock();
            tb.FontSize = GlobalModel.Global.AppFontSize;
            tb.TextAlignment = TextAlignment.Left;
            tb.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            tb.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            tb.Text = text;
            return tb;
        }

        private static Button CreateButton(Image? content)
        {
            var bt = new Button();
            bt.MaxWidth = GlobalModel.Global.IconSize;
            bt.MaxHeight = GlobalModel.Global.IconSize;
            bt.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            bt.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            bt.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            bt.VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
            bt.Background = new SolidColorBrush(Colors.Transparent);
            bt.Content = content;
            return bt;
        }

        private static object CreateNodeHeader(NodeItem item)
        {
            var g0 = new Grid();
            g0.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            g0.ColumnDefinitions = new ColumnDefinitions("Auto *");
            g0.RowDefinitions = new RowDefinitions("Auto");

            // Icon on left
            var img = CreateImage(GetFileIcon(item), GlobalModel.Global.TreeIconSize);
            img.Margin = new Thickness(0, 0, 3, 0);
            Grid.SetColumn(img, 0);
            g0.Children.Add(img);

            var tb = CreateTextBlock(item.Name);
            Grid.SetColumn(tb, 1);
            g0.Children.Add(tb);

            return g0;
        }

        private TreeViewItem CreateViewItem(DotnetProject project, ref TreeViewItem? selected)
        {
            var view = new TreeViewItem();
            view.Header = CreateProjectHeader(project);

            // Use tag value to maintain state
            if (project.Tag is TreeViewItem last)
            {
                view.IsExpanded = last.IsExpanded;
                view.IsSelected = last.IsSelected;

                if (view.IsSelected)
                {
                    selected = view;
                }
            }

            // Hold data as tags
            project.Tag = view;
            view.Tag = project;

            // List directory contents directly
            var list = new List<TreeViewItem>();

            foreach (var item in project.Contents.Contents)
            {
                list.Add(CreateViewItem(item, ref selected));
            }

            view.ItemsSource = list;
            return view;
        }

        private TreeViewItem CreateViewItem(NodeItem node, ref TreeViewItem? selected)
        {
            var view = new TreeViewItem();
            view.Header = CreateNodeHeader(node);

            // Use tag value to maintain state
            if (node.Tag is TreeViewItem last)
            {
                view.IsExpanded = last.IsExpanded;
                view.IsSelected = last.IsSelected;

                if (view.IsSelected)
                {
                    selected = view;
                }
            }

            // Hold data as tags
            node.Tag = view;
            view.Tag = node;

            if (node.Contents.Count != 0)
            {
                // Populate
                var list = new List<TreeViewItem>();

                foreach (var item in node.Contents)
                {
                    list.Add(CreateViewItem(item, ref selected));
                }

                view.ItemsSource = list;
            }

            return view;
        }

        private object CreateProjectHeader(DotnetProject project)
        {
            var info = project.Error?.Message;
            var icon = AssetModel.WarnTree;

            if (project.Exists)
            {
                icon = AssetModel.ProjectGreyTree;

                if (info == null && project.AssemblyPath?.Exists == true)
                {
                    info = project.MakeLocalName(project.AssemblyPath?.FullName);
                    icon = AssetModel.ProjectTree;
                }
            }

            var g0 = new Grid();
            g0.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            g0.ColumnDefinitions = new ColumnDefinitions("Auto * Auto");
            g0.RowDefinitions = new RowDefinitions("Auto");
            g0.Margin = new Thickness(0, 3, 0, 3);

            // Icon on left
            var img = CreateImage(icon, GlobalModel.Global.TreeIconSize);
            img.Margin = new Thickness(0, 0, 0, 3);
            Grid.SetColumn(img, 0);
            g0.Children.Add(img);

            // Child grid in center
            var g1 = new Grid();
            g1.ColumnDefinitions = new ColumnDefinitions("*");
            g1.RowDefinitions = new RowDefinitions("Auto Auto");
            Grid.SetColumn(g1, 1);
            g0.Children.Add(g1);

            var tb = CreateTextBlock(project.ProjectName);
            tb.FontWeight = FontWeight.Bold;
            Grid.SetRow(tb, 0);
            g1.Children.Add(tb);

            tb = CreateTextBlock(info + " ");
            tb.FontSize = GlobalModel.Global.SmallFontSize;
            tb.FontStyle = FontStyle.Italic;
            Grid.SetRow(tb, 1);
            Grid.SetRowSpan(tb, 99);
            g1.Children.Add(tb);

            // Button right
            var bt = CreateButton(CreateImage(GlobalModel.Global.Assets.Gear2Icon, GlobalModel.Global.IconSize));
            bt.Margin = new Thickness(0, 0, 0, 1);
            bt.Tag = project;
            ToolTip.SetTip(bt, "Project properties");
            bt.Click += ProjectSettingsClickHandler;
            Grid.SetColumn(bt, 2);
            g0.Children.Add(bt);

            return g0;
        }

        private void ProjectSettingsClickHandler(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DotnetProject project)
            {
                PropertiesClicked?.Invoke(project);
            }
        }

        private void SelectionChangedHandler(object? _, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("TREE SELECT CHANGE");
            Debug.WriteLine($"Counts {e.AddedItems.Count}, {e.RemovedItems.Count}");

            if (e.AddedItems.Count > 0)
            {
                var view = e.AddedItems[0] as TreeViewItem;
                var item = (PathItem?)view?.Tag;
                Debug.WriteLine($"Added: {item?.ToString() ?? "null"}");
                Debug.WriteLine($"Current selected: {_selected?.ToString() ?? "null"}");

                if (item?.FullName != _selected?.FullName)
                {
                    // Change is not easy to figure out, as setting TreeView.SelectedItem to itself
                    // seems to call change handler twice, once with AddedItems, and once
                    // with removed (when there has been no actual change). We hold a reference
                    // to detect path change.
                    _selected = item;

                    Debug.WriteLine($"Invoke {nameof(SelectionChanged)}");
                    SelectionChanged?.Invoke();
                }
                else
                if (view != null && view.ItemCount != 0 && _selected?.Tag == view)
                {
                    // This is a bit of hack. No new item actually selected here,
                    // but we are responding to click on already selected item and
                    // providing convenient toggle of folders. This seems more responsive
                    // than using PointerDown event. Must not be used in conjunction
                    // with new selection, otherwise we get alternating behaviour.
                    view.IsExpanded = !view.IsExpanded;
                }

            }
        }

    }
}