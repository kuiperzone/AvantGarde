// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022-25
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
using System.Text;

namespace AvantGarde.Projects;

/// <summary>
/// Builds a project by shelling "dotnet build". It uses the same process plumbing as
/// <see cref="MsBuildEvaluator"/>, but streams its output line by line rather than reading it at the
/// end - a build takes long enough that the caller wants to show progress while it runs.
/// </summary>
public static class ProjectBuilder
{
    /// <summary>
    /// Gets or sets the build timeout in milliseconds. Deliberately not
    /// <see cref="MsBuildEvaluator.Timeout"/>: that one guards a sub-second property evaluation,
    /// whereas a cold build of a solution legitimately runs for minutes.
    /// </summary>
    public static int Timeout { get; set; } = 600000;

    /// <summary>
    /// Builds the project in the given configuration, calling the handler with each line of output
    /// as it arrives. It blocks for the duration of the build and must not be called on the UI
    /// thread. The handler is called on a background thread. It does not throw.
    /// </summary>
    public static ProjectBuildResult Build(string projectPath, BuildKind build, Action<string>? output = null)
    {
        Debug.WriteLine($"{nameof(ProjectBuilder)}.{nameof(Build)} {projectPath}, {build}");

        if (!File.Exists(projectPath))
        {
            return new ProjectBuildResult(projectPath, "Project not found", projectPath);
        }

        var text = new StringBuilder();

        void Capture(string? line)
        {
            if (line == null)
            {
                return;
            }

            lock (text)
            {
                text.AppendLine(line);
            }

            try
            {
                output?.Invoke(line);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        try
        {
            var args = new StringBuilder();
            args.Append("build ");
            args.Append('"');
            args.Append(projectPath);
            args.Append("\" --nologo -p:Configuration=");
            args.Append(build.ToString());

            var info = new ProcessStartInfo
            {
                Arguments = args.ToString(),
                CreateNoWindow = true,
                FileName = MsBuildEvaluator.GetDotnetPath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty,
            };

            // The terminal logger redraws in place with escape sequences, which is unreadable once
            // the lines are appended to a text box rather than written to a console.
            info.Environment["MSBUILDTERMINALLOGGER"] = "off";
            info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

            Debug.WriteLine($"BUILDING: {info.FileName} {info.Arguments}");
            using var proc = Process.Start(info) ??
                throw new InvalidOperationException("Failed to start " + info.FileName);

            proc.OutputDataReceived += (_, e) => Capture(e.Data);
            proc.ErrorDataReceived += (_, e) => Capture(e.Data);
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            if (!proc.WaitForExit(Timeout))
            {
                try
                {
                    proc.Kill(true);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Failed to kill build: " + e.Message);
                }

                return new ProjectBuildResult(projectPath, "Build timed out",
                    "MSBuild did not complete within " + (Timeout / 1000) + " seconds", GetText(text));
            }

            // The overload taking a timeout does not wait on the output handlers, so the last lines
            // can still be in flight at this point. The parameterless one does.
            proc.WaitForExit();
            var captured = GetText(text);

            if (proc.ExitCode != 0)
            {
                Debug.WriteLine("BUILD FAILED: " + proc.ExitCode);
                return new ProjectBuildResult(projectPath, "Build failed", FirstErrorLine(captured), captured);
            }

            return new ProjectBuildResult(projectPath, captured);
        }
        catch (Exception e)
        {
            Debug.WriteLine("BUILD EXCEPTION: " + e);
            return new ProjectBuildResult(projectPath, "Cannot build project", e.Message, GetText(text));
        }
    }

    /// <summary>
    /// Returns the first line MSBuild reported as an error, or null. MSBuild's diagnostic format is
    /// "file(line,col): error CODE: text", and the summary repeats each one, so the first hit is the
    /// first real failure rather than the summary.
    /// </summary>
    public static string? FirstErrorLine(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Contains(": error ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("error ", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
        }

        return null;
    }

    private static string GetText(StringBuilder text)
    {
        lock (text)
        {
            return text.ToString().TrimEnd();
        }
    }
}
