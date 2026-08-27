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

public class ProjectBuilderTest(ITestOutputHelper helper) : TestUtilBase(helper)
{
    // A bare SDK library. No PackageReference, so it restores from the SDK alone.
    private const string LibraryProject =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";

    [Fact]
    public void Build_BuildsRequestedConfiguration()
    {
        // Release, not Debug. The previewer looks in the solution's configuration, so a build which
        // silently used Debug would report the same missing assembly it was asked to clear.
        var path = CreateFileContent("Name.Test.csproj", LibraryProject);
        CreateFileContent("Class1.cs", "namespace Name.Test; public class Class1 { }");

        var lines = new List<string>();
        var rslt = ProjectBuilder.Build(path, BuildKind.Release, lines.Add);

        WriteLine(rslt.Output);

        Assert.True(rslt.IsSuccess, rslt.Output);
        Assert.Null(rslt.Message);
        Assert.NotEmpty(lines);
        Assert.True(File.Exists(Path.Combine(Scratch, "bin", "Release", "net10.0", "Name.Test.dll")));
        Assert.False(Directory.Exists(Path.Combine(Scratch, "bin", "Debug")));
    }

    [Fact]
    public void Build_FailureReportsFirstError()
    {
        var path = CreateFileContent("Name.Test.csproj", LibraryProject);
        CreateFileContent("Class1.cs", "this is not C#");

        var rslt = ProjectBuilder.Build(path, BuildKind.Debug);

        WriteLine(rslt.Output);

        Assert.False(rslt.IsSuccess);
        Assert.Equal("Build failed", rslt.Message);
        Assert.NotEmpty(rslt.Output);

        // The compiler diagnostic, which is what the OUTPUT pane must not swallow.
        Assert.Contains(": error ", rslt.Detail);
    }

    [Fact]
    public void Build_MissingProjectReportsFailure()
    {
        var rslt = ProjectBuilder.Build(Path.Combine(Scratch, "NoSuch.csproj"), BuildKind.Debug);

        Assert.False(rslt.IsSuccess);
        Assert.Equal("Project not found", rslt.Message);
        Assert.Empty(rslt.Output);
    }

    [Fact]
    public void FirstErrorLine_LocatesDiagnostic()
    {
        const string Output =
            "  Determining projects to restore...\n" +
            "  Name.Test -> obj\\Debug\n" +
            "Class1.cs(1,1): error CS1022: Type or namespace definition, or end-of-file expected\n" +
            "Class1.cs(2,1): error CS1002: ; expected\n";

        Assert.Equal("Class1.cs(1,1): error CS1022: Type or namespace definition, or end-of-file expected",
            ProjectBuilder.FirstErrorLine(Output));
    }

    [Fact]
    public void FirstErrorLine_NoneIsNull()
    {
        Assert.Null(ProjectBuilder.FirstErrorLine(null));
        Assert.Null(ProjectBuilder.FirstErrorLine(""));
        Assert.Null(ProjectBuilder.FirstErrorLine("  Build succeeded.\n    0 Error(s)"));
    }
}
