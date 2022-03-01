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
using Avalonia;
using AvantGarde.Utility;
using AvantGarde.ViewModels;

namespace AvantGarde
{
    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                App.Arguments = new ArgumentParser(args);

                if (App.Arguments.Get("v", false) || App.Arguments.Get("version", false))
                {
                    Console.WriteLine("Avant Garde: " + GlobalModel.Version);
                    Console.WriteLine(GlobalModel.Copyright);
                    Console.WriteLine();
                    return 0;
                }

                if (App.Arguments.Get("h", false) || App.Arguments.Get("help", false))
                {
                    var Indent = new string(' ', 4);
                    Console.WriteLine("Usage:");
                    Console.WriteLine(Indent + nameof(AvantGarde) + " [filename] [-options]");
                    Console.WriteLine(Indent + "where filename is path to .sln, .csproj or any file within project");
                    Console.WriteLine();

                    Console.WriteLine("Options:");

                    Console.WriteLine(Indent + "-h, --help");
                    Console.WriteLine(Indent + "Show help information.");
                    Console.WriteLine();

                    Console.WriteLine(Indent + "-v, --version");
                    Console.WriteLine(Indent + "Show version information.");
                    Console.WriteLine();

                    Console.WriteLine(Indent + "-m, --min-explorer");
                    Console.WriteLine(Indent + "Show with minimized explorer and non-maximized main window.");
                    Console.WriteLine();

                    Console.WriteLine(Indent + "-s=name");
                    Console.WriteLine(Indent + "Select and preview given item on opening.");
                    Console.WriteLine(Indent + "Name can be a leaf name or fully qualified path.");
                    Console.WriteLine();
                    return 0;
                }

                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }

        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
        }
    }
}
