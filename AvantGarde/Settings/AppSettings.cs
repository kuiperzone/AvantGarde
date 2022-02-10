// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Themes.Fluent;
using AvantGarde.Projects;
using AvantGarde.ViewModels;

namespace AvantGarde.Settings
{
    /// <summary>
    /// Application settings. Handles UI theme change.
    /// </summary>
    public sealed class AppSettings : JsonSettings
    {
        private const int MaxRecent = 10;
        private static FluentTheme? _theme;

        private Application? _app;
        private bool _isDarkTheme;
        private double _appFontSize = GlobalModel.DefaultFontSize;
        private string _monoFontFamily = GlobalModel.DefaultMonoFamily;
        private double _monoFontSize = GlobalModel.DefaultFontSize;
        private readonly List<RecentFile> _recentFiles = new();

        /// <summary>
        /// Default constructor for JSON.
        /// </summary>
        public AppSettings()
        {
        }

        /// <summary>
        /// Application constructor. Application must have been initialized first.
        /// </summary>
        public AppSettings(Application app)
        {
            _app = app;
            _theme = (FluentTheme)app.Styles[0];
        }

        /// <summary>
        /// Gets or sets the UI theme. Setting with instance constructed with Application
        /// will change the theme throughout.
        /// </summary>
        public bool IsDarkTheme
        {
            get { return _isDarkTheme; }

            set
            {
                if (_isDarkTheme != value)
                {
                    _isDarkTheme = value;

                    if (_app != null && _theme != null)
                    {
                        _theme.Mode = value ? FluentThemeMode.Dark : FluentThemeMode.Light;
                        GlobalModel.Global.Assets.IsDarkTheme = value;
                        GlobalModel.Global.Colors.IsDarkTheme = value;
                    }
                }


            }
        }

        /// <summary>
        /// Gets or sets the application font size. Setting with instance constructed with Application
        /// will change throughout application.
        /// </summary>
        public double AppFontSize
        {
            get { return _appFontSize; }

            set
            {
                value = Math.Clamp(value, GlobalModel.MinFontSize, GlobalModel.MaxFontSize);

                if (_appFontSize != value)
                {
                    _appFontSize = value;

                    // We can only set this once
                    // Until Window.FontSize can be used as way to change app FontSize,
                    // the application must be restarted to take effect.
                    // SEE: https://github.com/AvaloniaUI/Avalonia/discussions/7539
                    if (_app != null && _app.Resources.TryAdd("ControlContentThemeFontSize", _appFontSize))
                    {
                        GlobalModel.Global.AppFontSize = _appFontSize;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the application monospace font size. Setting with instance constructed with Application
        /// will change throughout application.
        /// </summary>
        public double MonoFontSize
        {
            get { return _monoFontSize; }

            set
            {
                _monoFontSize = Math.Clamp(value, GlobalModel.MinFontSize, GlobalModel.MaxFontSize);

                if (_app != null)
                {
                    GlobalModel.Global.MonoFontSize = _monoFontSize;
                }
            }
        }

        /// <summary>
        /// Gets or sets the monospace font family.
        /// </summary>
        public string MonoFontFamily
        {
            get { return _monoFontFamily; }

            set
            {
                value = value.Trim();

                if (!value.Contains("monospace", StringComparison.OrdinalIgnoreCase))
                {
                    if (value.Length != 0)
                    {
                        value += ", monospace";
                    }
                    else
                    {
                        value = "monospace";
                    }
                }

                _monoFontFamily = value;

                if (_app != null)
                {
                    GlobalModel.Global.MonoFontFamily = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the preview "window" color theme. Separate from application theme.
        /// </summary>
        public PreviewWindowTheme PreviewTheme { get; set; }

        /// <summary>
        /// Gets or sets whether to pin main window.
        /// </summary>
        public bool IsTopmost { get; set; }

        /// <summary>
        /// Gets or sets welcome message.
        /// </summary>
        public bool ShowWelcome { get; set; } = true;

        /// <summary>
        /// Gets or sets default values.
        /// </summary>
        public SolutionProperties SolutionDefaults { get; set; } = new();

        /// <summary>
        /// Time sorted recent files. Settable by JSON.
        /// </summary>
        public List<RecentFile> RecentFiles
        {
            get { return _recentFiles; }

            set
            {
                _recentFiles.Clear();

                foreach (var item in value)
                {
                    if (!string.IsNullOrEmpty(item.Path))
                    {
                        _recentFiles.Add(item);
                    }
                }

                SortAndCap();
            }
        }

        /// <summary>
        /// Clears recent files.
        /// </summary>
        public void ClearRecent()
        {
            _recentFiles.Clear();
        }

        /// <summary>
        /// Inserts or updates (makes recent) a recent file path.
        /// </summary>
        public void UpsertRecent(string path)
        {
            bool exists = false;

            foreach (var item in _recentFiles)
            {
                if (item.Path.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                {
                    item.Update();
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                _recentFiles.Add(new RecentFile(path));
            }

            SortAndCap();
        }

        /// <summary>
        /// Implements.
        /// </summary>
        public override bool Read()
        {
            return ReadInternal();
        }

        public void AssignFrom(AppSettings other)
        {
            IsDarkTheme = other.IsDarkTheme;
            AppFontSize = other.AppFontSize;
            MonoFontSize = other.MonoFontSize;
            MonoFontFamily = other.MonoFontFamily;
            PreviewTheme = other.PreviewTheme;
            IsTopmost = other.IsTopmost;
            ShowWelcome = other.ShowWelcome;
            SolutionDefaults = other.SolutionDefaults;
            RecentFiles = other.RecentFiles;
        }

        private bool ReadInternal()
        {
            var temp = Read<AppSettings>();

            if (temp != null)
            {
                AssignFrom(temp);
                return true;
            }

            return false;
        }

        private void SortAndCap()
        {
            _recentFiles.Sort();

            while (_recentFiles.Count > MaxRecent)
            {
                _recentFiles.RemoveAt(_recentFiles.Count - 1);
            }
        }


    }

}