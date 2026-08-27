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

public class DotnetProjectTest(ITestOutputHelper helper) : TestUtilBase(helper)
{
    private const string ProjectNet5 =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net5.0</TargetFramework></PropertyGroup>" +
        "<ItemGroup><PackageReference Include=\"Avalonia\" Version=\"0.10.11\"/></ItemGroup></Project>";
    private const string ProjectNet6 =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net6.0</TargetFramework></PropertyGroup>" +
        "<ItemGroup><PackageReference Include=\"Avalonia\" Version=\"0.10.12\"/></ItemGroup></Project>";

    [Fact]
    public void Refresh_Updates()
    {
        var path = CreateFileContent("Name.Test.csproj", ProjectNet5);
        var item = new DotnetProject(path);

        Assert.NotEqual(0, item.GetHashCode());
        Assert.Equal("Name.Test", item.ProjectName);
        Assert.Equal("Name.Test.csproj", item.Name);
        Assert.Equal(PathKind.Solution, item.Kind);
        Assert.Equal(path, item.FullName);
        Assert.True(item.Exists);
        Assert.NotEqual(default, item.LastUtc);

        Assert.Empty(item.TargetFramework);
        Assert.Empty(item.AvaloniaVersion);
        Assert.False(item.AssemblyPath?.Exists == true);

        // First refresh
        int code = item.GetHashCode();
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.Equal("net5.0", item.TargetFramework);
        Assert.False(item.AssemblyPath?.Exists == true);

        // Again (no change)
        code = item.GetHashCode();
        Assert.False(item.Refresh());
        Assert.Equal(code, item.GetHashCode());
        Assert.Equal("net5.0", item.TargetFramework);
        Assert.False(item.AssemblyPath?.Exists == true);

        // Create assembly
        path = Scratch + "bin/debug/net5.0";
        Directory.CreateDirectory(path);
        path = CreateFileContent(path + "/Name.Test.dll", "Dummy");

        // Assembly now exists
        code = item.GetHashCode();

        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.Equal("net5.0", item.TargetFramework);
        Assert.Equal("0.10.11", item.AvaloniaVersion);

        WriteLine(item.AssemblyPath?.FullName);
        Assert.True(item.AssemblyPath?.Exists == true);
        Assert.Contains("net5.0", item.AssemblyPath?.FullName);

        // Change framework
        path = CreateFileContent("Name.Test.csproj", ProjectNet6);

        // Assembly not exist because we switch to .NET6
        code = item.GetHashCode();
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.Equal("net6.0", item.TargetFramework);
        Assert.False(item.AssemblyPath?.Exists == true);

        // Create it
        path = Scratch + "bin/debug/net6.0";
        Directory.CreateDirectory(path);
        path = CreateFileContent(path + "/Name.Test.dll", "Dummy");

        // Assembly now exists
        code = item.GetHashCode();
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.Equal("net6.0", item.TargetFramework);
        Assert.Equal("0.10.12", item.AvaloniaVersion);
        Assert.True(item.AssemblyPath?.Exists == true);
        Assert.Contains("net6.0", item.AssemblyPath?.FullName);

        // Again (no change)
        code = item.GetHashCode();
        Assert.False(item.Refresh());
        Assert.Equal(code, item.GetHashCode());
        Assert.Equal("net6.0", item.TargetFramework);
        Assert.True(item.AssemblyPath?.Exists == true);
        Assert.Contains("net6.0", item.AssemblyPath?.FullName);

        // Update assembly file
        CreateFileContent(path, "DummyChange");

        // Changed
        code = item.GetHashCode();
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.Equal("net6.0", item.TargetFramework);
        Assert.True(item.AssemblyPath?.Exists == true);
    }

    [Fact]
    public void Evaluate_SuppliesTargetFrameworkTheXmlParseCannotSee()
    {
        // TargetFramework declared only in Directory.Build.props. Without MSBuild evaluation the
        // framework is empty, the assembly search bails on its first guard, and a fully built
        // project reports "assembly not found".
        CreateFileContent("Directory.Build.props",
            "<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

        var path = CreateFileContent("Name.Test.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>" +
            "<PackageReference Include=\"Avalonia\" Version=\"12.0.5\"/></ItemGroup></Project>");

        var binDir = Path.Combine(Scratch, "bin", "Debug", "net8.0");
        Directory.CreateDirectory(binDir);
        CreateFileContent(Path.Combine(binDir, "Name.Test.dll"), "Dummy");

        var item = new DotnetProject(path);
        item.Refresh();

        // Before evaluation
        Assert.Empty(item.TargetFramework);
        Assert.Null(item.AssemblyPath);
        Assert.True(item.NeedsEvaluation);

        item.Evaluate();
        Assert.False(item.NeedsEvaluation);
        Assert.True(item.Refresh());

        Assert.Equal("net8.0", item.TargetFramework);
        Assert.True(item.AssemblyPath?.Exists == true);
        Assert.Equal(Path.Combine(binDir, "Name.Test.dll"), item.AssemblyPath?.FullName);
    }

    [Fact]
    public void Evaluate_UnrestoredProjectReportsRestore()
    {
        CreateFileContent("Directory.Build.props",
            "<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");

        var path = CreateFileContent("Name.Test.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>" +
            "<PackageReference Include=\"Avalonia\" Version=\"12.0.5\"/></ItemGroup></Project>");

        var item = new DotnetProject(path);
        item.Evaluate();
        item.Refresh();

        // Actionable, rather than the "assembly not found" or FileNotFoundException that an
        // unrestored project produced before.
        Assert.False(item.IsRestored);
        Assert.Null(item.PreviewerToolPath);
        Assert.Equal("Project not restored", item.Error?.Message);

        // Building would restore too, but the affordance is attached to the missing assembly alone.
        Assert.False(item.Error?.IsBuildable);
    }

    [Fact]
    public void CheckForError_MissingAssemblyIsBuildable()
    {
        var path = CreateFileContent("Name.Test.csproj", ProjectNet6);
        var item = new DotnetProject(path);
        item.Refresh();

        // The one error AvantGarde can clear itself, and the only one carrying a Build button.
        Assert.Equal("Debug assembly not found", item.Error?.Message);
        Assert.True(item.Error?.IsBuildable);

        // Except where the user stated the path. Building would not put a file there.
        item.Properties.AssemblyOverride = "elsewhere/Name.Test.dll";
        Assert.True(item.Refresh());

        Assert.Equal("Debug assembly not found", item.Error?.Message);
        Assert.False(item.Error?.IsBuildable);
    }

    [Fact]
    public void Evaluate_MissingProjectReportsFailure()
    {
        var path = CreateFileContent("Name.Test.csproj", ProjectNet6);
        var item = new DotnetProject(path);

        File.Delete(path);
        item.Evaluate();
        item.Refresh();

        Assert.NotNull(item.Evaluation);
        Assert.False(item.Evaluation.IsSuccess);
    }

    [Fact]
    public void Refresh_Artifacts()
    {
        var solDir = Path.Combine(Scratch, "Solution");
        var projDir = Path.Combine(solDir, "Name.Test");
        Directory.CreateDirectory(projDir);

        var path = CreateFileContent(Path.Combine(projDir, "Name.Test.csproj"), ProjectNet6);
        var item = new DotnetProject(path);

        Assert.True(item.Refresh());
        Assert.Equal("net6.0", item.TargetFramework);
        Assert.False(item.AssemblyPath?.Exists == true);

        // Now put in dummy solution ABOVE project (where artifacts live)
        CreateFileContent(Path.Combine(solDir, "Name.Test.sln"), "Dummy");

        item.Refresh();
        Assert.Equal("net6.0", item.TargetFramework);
        Assert.False(item.AssemblyPath?.Exists == true);

        // Create assembly under artifacts
        // debug_net8.0/projectName.dll
        // artifacts/bin/debug/projectName.dll
        // artifacts/bin/[projectName]/debug_net8.0/projectName.dll
        var artDir = Path.Combine(solDir, "artifacts", "bin", "debug");
        Directory.CreateDirectory(artDir);
        path = CreateFileContent(Path.Combine(artDir, "Name.Test.dll"), "Dummy");

        // Exists now
        item.Refresh();
        Assert.True(item.AssemblyPath?.Exists == true);

        // New location
        Directory.Delete(artDir, true);
        artDir = Path.Combine(solDir, "artifacts", "bin", "debug_net6.0");

        Directory.CreateDirectory(artDir);
        path = CreateFileContent(Path.Combine(artDir, "Name.Test.dll"), "Dummy");

        item.Refresh();
        Assert.True(item.AssemblyPath?.Exists == true);

        // New location
        Directory.Delete(artDir, true);
        artDir = Path.Combine(solDir, "artifacts", "bin", "Name.Test", "debug_net6.0");
        Directory.CreateDirectory(artDir);
        path = CreateFileContent(Path.Combine(artDir, "Name.Test.dll"), "Dummy");

        item.Refresh();
        Assert.True(item.AssemblyPath?.Exists == true);

        // New location
        Directory.Delete(artDir, true);
        artDir = Path.Combine(solDir, "artifacts/bin/Name.Test/debug/net6.0");
        Directory.CreateDirectory(artDir);
        path = CreateFileContent(Path.Combine(artDir, "Name.Test.dll"), "Dummy");

        item.Refresh();
        Assert.True(item.AssemblyPath?.Exists == true);
    }

}