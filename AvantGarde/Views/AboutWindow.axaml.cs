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

using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde.Views;

/// <summary>
/// About AvantGarde.
/// </summary>
public partial class AboutWindow : AvantWindow
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public AboutWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void WebPressedHandler(object? sender, PointerPressedEventArgs e)
    {
        ShellOpen.Start(GlobalModel.WebUrl);
    }

    private void OkClickHandler(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}