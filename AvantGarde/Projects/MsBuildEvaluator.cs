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
using System.Text.Json;

namespace AvantGarde.Projects;

/// <summary>
/// Evaluates MSBuild properties for a project by shelling "dotnet msbuild -getProperty:...". This
/// replaces guessing at project layout from the project XML: MSBuild is the only thing that knows
/// what a project really evaluates to, including properties supplied by Directory.Build.props,
/// Directory.Packages.props, SDK defaults and NuGet's generated .g.props.
/// </summary>
public static class MsBuildEvaluator
{
    /// <summary>
    /// Path to the designer host, supplied by Avalonia's own build props. This is the authoritative
    /// answer to "which designer host serves this project", superseding a NuGet cache path built
    /// from a version string.
    /// </summary>
    public const string PreviewerToolPathProperty = "AvaloniaPreviewerNetCoreToolPath";

    /// <summary>
    /// Full path of the built output assembly.
    /// </summary>
    public const string TargetPathProperty = "TargetPath";

    /// <summary>
    /// The evaluated TargetFramework.
    /// </summary>
    public const string TargetFrameworkProperty = "TargetFramework";

    /// <summary>
    /// The evaluated OutputType, i.e. "WinExe".
    /// </summary>
    public const string OutputTypeProperty = "OutputType";

    /// <summary>
    /// Path of NuGet's restore asset file. The property is defined whether or not the project has
    /// been restored, so the file's existence - not the property - is what says restore has run.
    /// </summary>
    public const string ProjectAssetsFileProperty = "ProjectAssetsFile";

    // NOTE: the MSBuild property "AvaloniaVersion" is deliberately absent. It is defined by
    // Avalonia's own props with a stale literal - a 12.0.5 project evaluates it to "11.0.2" - so it
    // says nothing about the referenced package version. DotnetProject's XML parse remains the
    // source of that, and only for the fallback host lookup.
    private static readonly string[] DefaultProperties =
    {
        PreviewerToolPathProperty,
        TargetPathProperty,
        TargetFrameworkProperty,
        OutputTypeProperty,
        ProjectAssetsFileProperty,
    };

    /// <summary>
    /// Gets or sets the evaluation timeout in milliseconds. A cold evaluation measures well under a
    /// second per project, so this is a guard against a hung SDK rather than a working limit.
    /// </summary>
    public static int Timeout { get; set; } = 30000;

    /// <summary>
    /// Evaluates the default property set against the given project and build configuration. It
    /// blocks for the duration of an out of process MSBuild run and must not be called on the UI
    /// thread. It does not throw.
    /// </summary>
    public static MsBuildResult Evaluate(string projectPath, BuildKind build)
    {
        return Evaluate(projectPath, build, DefaultProperties);
    }

    /// <summary>
    /// Evaluates the given properties. See <see cref="Evaluate(string, BuildKind)"/>.
    /// </summary>
    public static MsBuildResult Evaluate(string projectPath, BuildKind build, params string[] properties)
    {
        Debug.WriteLine($"{nameof(MsBuildEvaluator)}.{nameof(Evaluate)} {projectPath}, {build}");

        if (properties.Length < 2)
        {
            // With a single -getProperty, MSBuild writes the bare value instead of a JSON payload.
            // Requiring two keeps one parse path rather than two.
            throw new ArgumentException("At least two properties are required", nameof(properties));
        }

        if (!File.Exists(projectPath))
        {
            return new MsBuildResult(projectPath, "Project not found", projectPath);
        }

        var args = new StringBuilder();
        args.Append("msbuild ");
        args.Append('"');
        args.Append(projectPath);
        args.Append("\" -nologo -p:Configuration=");
        args.Append(build.ToString());

        foreach (var item in properties)
        {
            args.Append(" -getProperty:");
            args.Append(item);
        }

        try
        {
            var info = new ProcessStartInfo
            {
                Arguments = args.ToString(),
                CreateNoWindow = true,
                FileName = GetDotnetPath(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty,
            };

            info.Environment["MSBUILDTERMINALLOGGER"] = "off";
            info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

            Debug.WriteLine($"EVALUATING: {info.FileName} {info.Arguments}");
            using var proc = Process.Start(info) ??
                throw new InvalidOperationException("Failed to start " + info.FileName);

            // Read before waiting. A project with many imports can fill the pipe buffer, and a
            // WaitForExit ahead of the read would deadlock against it.
            var output = proc.StandardOutput.ReadToEndAsync();
            var error = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(Timeout))
            {
                try
                {
                    proc.Kill(true);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Failed to kill evaluation: " + e.Message);
                }

                return new MsBuildResult(projectPath, "Project evaluation timed out",
                    "MSBuild did not respond within " + (Timeout / 1000) + " seconds");
            }

            var stdout = output.Result;
            var stderr = error.Result;

            if (proc.ExitCode != 0)
            {
                Debug.WriteLine("EVALUATION FAILED: " + stdout);
                return new MsBuildResult(projectPath, "Cannot evaluate project", FirstMeaningfulLine(stderr, stdout));
            }

            var values = ParseProperties(stdout);

            if (values == null)
            {
                return new MsBuildResult(projectPath, "Cannot evaluate project", FirstMeaningfulLine(stdout, stderr));
            }

            return new MsBuildResult(projectPath, values);
        }
        catch (Exception e)
        {
            Debug.WriteLine("EVALUATION EXCEPTION: " + e);
            return new MsBuildResult(projectPath, "Cannot evaluate project", e.Message);
        }
    }

    /// <summary>
    /// Parses the {"Properties":{...}} payload written by "dotnet msbuild -getProperty:". The result
    /// is null if the text is not such a payload. Leading non-JSON noise, which some SDK versions
    /// emit, is skipped.
    /// </summary>
    public static Dictionary<string, string>? ParseProperties(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        int start = text.IndexOf('{');

        if (start < 0)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text.Substring(start));

            if (!doc.RootElement.TryGetProperty("Properties", out JsonElement props) ||
                props.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var rslt = new Dictionary<string, string>();

            foreach (var item in props.EnumerateObject())
            {
                rslt.Add(item.Name, item.Value.GetString() ?? string.Empty);
            }

            return rslt;
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed to parse evaluation output: " + e.Message);
            return null;
        }
    }

    /// <summary>
    /// Normalizes a path as evaluated by MSBuild. Avalonia's props builds the previewer tool path by
    /// concatenation, giving "...\buildTransitive\\..\tools\net8.0\...", which is valid but neither
    /// comparable nor presentable. The result is null where the input is empty or not a valid path.
    /// </summary>
    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed to normalize path: " + e.Message);
            return null;
        }
    }

    /// <summary>
    /// Returns the dotnet executable to shell. Shared with <see cref="ProjectBuilder"/>, which runs
    /// the same host against the same project.
    /// </summary>
    internal static string GetDotnetPath()
    {
        // https://github.com/dotnet/docs/blob/main/docs/core/tools/dotnet-environment-variables.md#dotnet_host_path
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

        if (string.IsNullOrEmpty(dotnet))
        {
            return "dotnet";
        }

        return dotnet;
    }

    private static string? FirstMeaningfulLine(params string?[] sources)
    {
        foreach (var text in sources)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                foreach (var line in text.Split('\n'))
                {
                    var trimmed = line.Trim();

                    if (trimmed.Length != 0)
                    {
                        return trimmed;
                    }
                }
            }
        }

        return null;
    }
}
