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

using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
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

        private readonly Application? _app;
        private bool _isDarkTheme;
        private string _appFontFamily = GlobalModel.DefaultAppFamily;
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

            // We need this to initialize colors, otherwise we may end
            // up with invisible controls if no config file is present.
            var theme = _app.ActualThemeVariant;
            _app.RequestedThemeVariant = theme;
            GlobalModel.Global.Assets.IsDarkTheme = theme.Equals(ThemeVariant.Dark);
            GlobalModel.Global.Colors.IsDarkTheme = theme.Equals(ThemeVariant.Dark);
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
                _isDarkTheme = value;

                if (_app != null)
                {
                    _app.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
                    GlobalModel.Global.Assets.IsDarkTheme = value;
                    GlobalModel.Global.Colors.IsDarkTheme = value;
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

                    // This only works on Fluent.
                    // SEE: https://github.com/AvaloniaUI/Avalonia/discussions/7539
                    if (_app != null)
                    {
                        _app.Resources.Remove("ControlContentThemeFontSize");

                        if (_app.Resources.TryAdd("ControlContentThemeFontSize", _appFontSize))
                        {
                            GlobalModel.Global.AppFontSize = _appFontSize;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the application font family.
        /// </summary>
        public string AppFontFamily
        {
            get { return _appFontFamily; }

            set
            {
                value = value.Trim();

                if (value.Length == 0)
                {
                    value = GlobalModel.DefaultAppFamily;
                }

                _appFontFamily = value;

                if (_app != null)
                {
                    GlobalModel.Global.AppFontFamily = new FontFamily(value);
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
                    GlobalModel.Global.MonoFontFamily = new FontFamily(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the preview "window" color theme. Separate from application theme.
        /// </summary>
        public PreviewWindowTheme PreviewTheme { get; set; }

        /// <summary>
        /// Gets or sets welcome message.
        /// </summary>
        public bool ShowWelcome { get; set; } = true;

        /// <summary>
        /// Gets or sets show pin. Default is false on Linux, true on others.
        /// </summary>
        public bool ShowPin { get; set; } = !RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        /// <summary>
        /// Gets or sets whether window is maximized.
        /// </summary>
        public bool IsMaximized { get; set; }

        /// <summary>
        /// Gets or sets the window width.
        /// </summary>
        public double Width { get; set; } = 800;

        /// <summary>
        /// Gets or sets the window height.
        /// </summary>
        public double Height { get; set; } = 600;

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
                Debug.WriteLine("ITEM: " + item.Path);

                if (item.Path.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                {
                    Debug.WriteLine("Exists");
                    item.Update();
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                Debug.WriteLine("Upsert recent: " + path);
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

        /// <summary>
        /// Critical that new properties are assigned here.
        /// </summary>
        public void AssignFrom(AppSettings other)
        {
            IsDarkTheme = other.IsDarkTheme;
            AppFontSize = other.AppFontSize;
            MonoFontSize = other.MonoFontSize;
            AppFontFamily = other.AppFontFamily;
            MonoFontFamily = other.MonoFontFamily;
            PreviewTheme = other.PreviewTheme;
            IsMaximized = other.IsMaximized;
            Width = other.Width;
            Height = other.Height;
            ShowWelcome = other.ShowWelcome;
            ShowPin = other.ShowPin;
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