// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;

namespace AvantGarde.ViewModels
{
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

            // Either a timer or events linked to a global or static object
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
}
