// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvantGarde.Settings;
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
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }

    }
}