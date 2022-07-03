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
using System.Runtime.InteropServices;

namespace AvantGarde.Utility
{
    /// <summary>
    /// Opens document or URL in shell on multiple platforms. Works with dotnet.
    /// </summary>
    public static class ShellOpen
    {
        /// <summary>
        /// Start with given filename (document or URL).
        /// </summary>
        public static void Start(string? filename)
        {
            if (!string.IsNullOrWhiteSpace(filename))
            {
                try
                {
                    Process.Start(filename);
                }
                catch
                {
                    // Hack because of this: https://github.com/dotnet/corefx/issues/10361
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        filename = filename.Replace("&", "^&");
                        Process.Start(new ProcessStartInfo("cmd", $"/c start {filename}") { CreateNoWindow = true });
                    }
                    else
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Process.Start(
                            new ProcessStartInfo
                            {
                                FileName = "/bin/sh",
                                Arguments = $"-c \"xdg-open {filename}\"",
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden
                            }
                        );

                    }
                    else
                    {
                        // Mac
                        Process.Start("open", filename);
                    }
                }
            }
        }

    }
}
