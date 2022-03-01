// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022
// LICENSE   : GPL-3.0-or-later
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
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

using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvantGarde.Projects;
using AvantGarde.ViewModels;

namespace AvantGarde.Views
{
    /// <summary>
    /// Underlying project tree view without surrounding controls. The is no XAML for this.
    /// </summary>
    public class ProjectTree : UserControl
    {
        private readonly TreeView _treeView;
        private DotnetSolution? _solution;
        private PathItem? _selectedItem;
        private TreeViewItem? _selectedView;

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
                    _selectedView = null;
                    _selectedItem = null;

                    Refresh();

                    if (_treeView.ItemCount != 0)
                    {
                        foreach (var p in _treeView.Items)
                        {
                            _selectedView = (TreeViewItem?)p;
                            _selectedItem = (PathItem?)_selectedView?.Tag;
                            _treeView.SelectedItem = p;
                            break;
                        }
                    }

                    SelectionChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Gets the selected item. This can include projects as well as file nodes.
        /// </summary>
        public PathItem? SelectedItem
        {
            get { return _selectedItem; }

            set
            {
                var view = value?.Tag as TreeViewItem;
                _treeView.SelectedItem = view;

                /*
                if (view != null)
                {
                    _selectedItem = value;
                    _selectedView = view;
                    _treeView.SelectedItem = view;
                }
                else
                {
                    _selectedItem = null;
                    _selectedView = null;
                    _treeView.SelectedItem = null;
                }
                */
            }
        }

        /// <summary>
        /// Gets the project associated with the selected item.
        /// </summary>
        public DotnetProject? SelectedProject
        {
            get
            {
                if (SelectedItem is NodeItem node)
                {
                    return node.Project;
                }

                if (SelectedItem is DotnetProject project)
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
            TreeViewItem? selected = null;
            List<TreeViewItem>? items = null;

            if (_solution != null)
            {
                items = new();

                foreach (var project in _solution.Projects.Values)
                {
                    items.Add(CreateViewItem(project, ref selected));
                }
            }

            _treeView.Items = items;
            _selectedItem = (PathItem?)selected?.Tag;
            _treeView.SelectedItem = selected;
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
                _selectedItem = node;
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

            view.Items = list;
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
            bt.Click += ProjectSettingsClickHandler;
            Grid.SetColumn(bt, 2);
            g0.Children.Add(bt);

            return g0;
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

                view.Items = list;
            }

            return view;
        }

        private object CreateNodeHeader(NodeItem item)
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

        private Image CreateImage(IImage? src, double size)
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

        private TextBlock CreateTextBlock(string text)
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

        private Button CreateButton(Image? content)
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

        private void ProjectSettingsClickHandler(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DotnetProject project)
            {
                PropertiesClicked?.Invoke(project);
            }
        }

        private void SelectionChangedHandler(object? sender, SelectionChangedEventArgs e)
        {
            PathItem? item = null;
            TreeViewItem? view = null;

            if (e.AddedItems.Count > 0)
            {
                view = (TreeViewItem?)e.AddedItems[0];
                item = (PathItem?)view?.Tag;
            }

            if (_selectedItem != item)
            {
                _selectedItem = item;
                _selectedView = view;
                SelectionChanged?.Invoke();
            }
            else
            if (view != null && _selectedView == view && view.ItemCount != 0)
            {
                // This is a bit of hack. No new item actually selected here,
                // but we are responding to click on already selected item and
                // providing convenient toggle of folders. This seems more responsive
                // than using PointerDown event. Must not be used in conjuction
                // with new selection, otherwise we get alternativing behaviour.
                view.IsExpanded = !view.IsExpanded;
            }

        }
    }
}