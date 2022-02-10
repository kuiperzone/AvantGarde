// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
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
