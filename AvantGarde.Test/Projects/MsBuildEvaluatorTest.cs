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

using AvantGarde.Test.Internal;
using Xunit;
using Xunit.Abstractions;

namespace AvantGarde.Projects.Test;

public class MsBuildEvaluatorTest(ITestOutputHelper helper) : TestUtilBase(helper)
{
    private const string MinimalProject = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>";
    private const string DirectoryBuildProps =
        "<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>";

    [Fact]
    public void ParseProperties_ReadsPayload()
    {
        const string Payload = "{\n  \"Properties\": {\n    \"TargetFramework\": \"net8.0\",\n" +
            "    \"AvaloniaPreviewerNetCoreToolPath\": \"\"\n  }\n}";

        var values = MsBuildEvaluator.ParseProperties(Payload);

        Assert.NotNull(values);
        Assert.Equal("net8.0", values["TargetFramework"]);
        Assert.Equal("", values["AvaloniaPreviewerNetCoreToolPath"]);
    }

    [Fact]
    public void ParseProperties_SkipsLeadingNoise()
    {
        // Some SDK versions print a line before the payload.
        const string Payload = "Determining projects to restore...\n{\"Properties\":{\"OutputType\":\"Exe\"}}";

        var values = MsBuildEvaluator.ParseProperties(Payload);

        Assert.NotNull(values);
        Assert.Equal("Exe", values["OutputType"]);
    }

    [Fact]
    public void ParseProperties_RejectsNonPayload()
    {
        Assert.Null(MsBuildEvaluator.ParseProperties(null));
        Assert.Null(MsBuildEvaluator.ParseProperties(""));
        Assert.Null(MsBuildEvaluator.ParseProperties("MSBUILD : error MSB1009: Project file does not exist."));
        Assert.Null(MsBuildEvaluator.ParseProperties("{\"Items\":{}}"));
    }

    [Fact]
    public void NormalizePath_ResolvesRelativeSegments()
    {
        // Avalonia's props builds the previewer path by concatenation, giving a doubled separator
        // and a parent segment in the middle.
        var src = Path.Combine(Scratch, "avalonia", "buildTransitive") +
            Path.DirectorySeparatorChar + Path.DirectorySeparatorChar + ".." +
            Path.DirectorySeparatorChar + "tools";

        var rslt = MsBuildEvaluator.NormalizePath(src);

        Assert.Equal(Path.Combine(Scratch, "avalonia", "tools"), rslt);
    }

    [Fact]
    public void NormalizePath_EmptyIsNull()
    {
        Assert.Null(MsBuildEvaluator.NormalizePath(null));
        Assert.Null(MsBuildEvaluator.NormalizePath("   "));
    }

    [Fact]
    public void Evaluate_RequiresTwoProperties()
    {
        // A single -getProperty makes MSBuild write the bare value rather than a JSON payload.
        Assert.Throws<ArgumentException>(() =>
            MsBuildEvaluator.Evaluate("Any.csproj", BuildKind.Debug, "TargetPath"));
    }

    [Fact]
    public void Evaluate_MissingProjectFails()
    {
        var rslt = MsBuildEvaluator.Evaluate(Path.Combine(Scratch, "NoSuch.csproj"), BuildKind.Debug);

        Assert.False(rslt.IsSuccess);
        Assert.Equal("Project not found", rslt.Message);
    }

    [Fact]
    public void Evaluate_ReadsTargetFrameworkFromDirectoryBuildProps()
    {
        // The case that motivates the whole evaluator: the property is not in the project file at
        // all, so no amount of project XML parsing can find it.
        CreateFileContent("Directory.Build.props", DirectoryBuildProps);
        var path = CreateFileContent("Name.Test.csproj", MinimalProject);

        var rslt = MsBuildEvaluator.Evaluate(path, BuildKind.Debug);
        WriteLine(rslt.Message ?? rslt.ToString());

        Assert.True(rslt.IsSuccess);
        Assert.Equal("net8.0", rslt.GetProperty(MsBuildEvaluator.TargetFrameworkProperty));
        Assert.Contains("Name.Test.dll", rslt.GetProperty(MsBuildEvaluator.TargetPathProperty));
    }

    [Fact]
    public void Evaluate_HonoursBuildConfiguration()
    {
        CreateFileContent("Directory.Build.props", DirectoryBuildProps);
        var path = CreateFileContent("Name.Test.csproj", MinimalProject);

        var debug = MsBuildEvaluator.Evaluate(path, BuildKind.Debug);
        var release = MsBuildEvaluator.Evaluate(path, BuildKind.Release);

        Assert.True(debug.IsSuccess);
        Assert.True(release.IsSuccess);
        Assert.Contains("Debug", debug.GetProperty(MsBuildEvaluator.TargetPathProperty));
        Assert.Contains("Release", release.GetProperty(MsBuildEvaluator.TargetPathProperty));
    }

    [Fact]
    public void Evaluate_UnrestoredProjectHasNoAssetsFile()
    {
        // ProjectAssetsFile is defined whether or not restore has run, so its existence on disk is
        // what distinguishes an unrestored project from one that simply lacks Avalonia.
        CreateFileContent("Directory.Build.props", DirectoryBuildProps);
        var path = CreateFileContent("Name.Test.csproj", MinimalProject);

        var rslt = MsBuildEvaluator.Evaluate(path, BuildKind.Debug);

        Assert.True(rslt.IsSuccess);
        var assets = rslt.GetProperty(MsBuildEvaluator.ProjectAssetsFileProperty);
        Assert.NotEqual(string.Empty, assets);
        Assert.False(File.Exists(assets));
        Assert.Equal(string.Empty, rslt.GetProperty(MsBuildEvaluator.PreviewerToolPathProperty));
    }
}
