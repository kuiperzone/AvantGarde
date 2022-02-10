// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using Avalonia;
using Avalonia.Controls;

namespace AvantGarde.Utility
{
    /// <summary>
    /// Provies extension methods.
    /// </summary>
    public static class ControlExtension
    {
        /// <summary>
        /// Finds control or throws.
        /// </summary>
        /// <exception cref="ArgumentException">Control not found</exception>
        public static T FindOrThrow<T>(this IControl control, string name) where T : class, IControl
        {
            return control.FindControl<T>(name) ??
                throw new ArgumentException($"Child control {name} not found in parent {control.Name ?? "control"}");
        }

        /// <summary>
        /// Conditionally positions the Window at the screen or owner centre according to Window.WindowStartupLocation.
        /// Does nothing for WindowStartupLocation.Manual or OperatingSystem.IsWindows(). This is a work-around fix.
        /// </summary>
        public static bool SetCenterFix(this Window window)
        {
            if (window.WindowStartupLocation != WindowStartupLocation.Manual && !OperatingSystem.IsWindows())
            {
                var size = GetWindowPixelSize(window);
                var center = GetOwnerOrScreenCenter(window);

                var x = center.X - size.Width / 2;
                var y = center.Y - size.Height / 2;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = new PixelPoint(x, y);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the owner Window of the control.
        /// </summary>
        /// <exception cref="ArgumentException">Control has no owner window</exception>
        public static Window GetOwnerWindow(this IControl control)
        {
            if (control is Window window)
            {
                return window;
            }

            if (control.Parent != null)
            {
                return GetOwnerWindow(control.Parent);
            }

            throw new ArgumentException("Control has no owner window");
        }

        private static PixelPoint GetOwnerOrScreenCenter(Window window)
        {
            if (window.Owner is Window owner && window.WindowStartupLocation == WindowStartupLocation.CenterOwner)
            {
                var oz = GetWindowPixelSize(owner);
                var pos = owner.Position;
                return new PixelPoint(pos.X + oz.Width / 2, pos.Y + oz.Height / 2);
            }

            // Default to screen
            var sz = window.Screens.Primary.WorkingArea.Size;
            return new PixelPoint(sz.Width / 2, sz.Height / 2);
        }

        private static PixelSize GetWindowPixelSize(WindowBase window)
        {
            var scale = window.PlatformImpl.DesktopScaling;
            double w = double.IsFinite(window.Width) ? window.Width : window.DesiredSize.Width;
            double h = double.IsFinite(window.Height) ? window.Height : window.DesiredSize.Height;
            return PixelSize.FromSize(new Size(double.IsFinite(w) ? w : 800, double.IsFinite(h) ? h : 800), scale);
        }

    }
}