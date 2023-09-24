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

using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;

namespace AvantGarde.ViewModels;

/// <summary>
/// View model class common to all windows. It also serves as a base class.
/// </summary>
public class AvantViewModel : ReactiveObject
{
    private readonly DispatcherTimer _timer;
    private bool _isDark;
    private double _appFontSize;
    private double _monoFontSize;
    private FontFamily _monoFontFamily;

    public AvantViewModel()
    {
        _isDark = Global.Colors.IsDarkTheme;
        _appFontSize =  Global.AppFontSize;
        _monoFontSize =  Global.MonoFontSize;
        _monoFontFamily =  Global.MonoFontFamily;

        _timer = new(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, RefreshTimerHandler);
        _timer.Start();
    }

    /// <summary>
    /// Gets global binding properties.
    /// </summary>
    public GlobalModel Global { get; } = GlobalModel.Global;

    /// <summary>
    /// Override to receive theme change.
    /// </summary>
    protected virtual void ColorChangedHandler()
    {
    }

    /// <summary>
    /// Override to receive font size change.
    /// </summary>
    protected virtual void FontChangedHandler()
    {
    }

    private void RefreshTimerHandler(object? sender, EventArgs e)
    {
        if (Global.Colors.IsDarkTheme != _isDark)
        {
            _isDark = Global.Colors.IsDarkTheme;
            ColorChangedHandler();
        }

        if (Global.AppFontSize != _appFontSize || Global.MonoFontSize != _monoFontSize || Global.MonoFontFamily != _monoFontFamily)
        {
            _appFontSize = Global.AppFontSize;
            _monoFontSize = Global.MonoFontSize;
            _monoFontFamily = Global.MonoFontFamily;
            FontChangedHandler();
        }
    }

}
