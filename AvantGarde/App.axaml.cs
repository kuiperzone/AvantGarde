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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvantGarde.Settings;
using AvantGarde.Utility;
using AvantGarde.ViewModels;
using AvantGarde.Views;

namespace AvantGarde
{
    public class App : Application
    {
        private static AppSettings? _settings;

        public App()
        {
#if DEBUG
            // Debug
            Trace.Listeners.Add(new TextWriterTraceListener(System.Console.Out));
#endif
            DataContext = new AvantViewModel();
        }

        public static ArgumentParser? Arguments { get; set; }

        public static AppSettings Settings
        {
            get { return _settings ?? throw new InvalidOperationException("Application must be initialized"); }
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            _settings ??= new AppSettings(this);
            _settings.Read();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Create with args
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }

    }
}