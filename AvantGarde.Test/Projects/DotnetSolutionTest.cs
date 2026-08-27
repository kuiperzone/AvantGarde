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

public class DotnetSolutionTest(ITestOutputHelper helper) : TestUtilBase(helper)
{
    private const string Project =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>" +
        "<ItemGroup><PackageReference Include=\"Avalonia\" Version=\"12.0.5\"/></ItemGroup></Project>";

    [Fact]
    public void Refresh_ReadsXmlSolutionFormat()
    {
        // .slnx nests Project elements inside optional Folder elements.
        const string Slnx =
            "<Solution>" +
            "  <Folder Name=\"/src/\">" +
            "    <Project Path=\"Alpha\\Alpha.csproj\" />" +
            "    <Project Path=\"Beta\\Beta.csproj\" />" +
            "  </Folder>" +
            "  <Project Path=\"Gamma\\Gamma.csproj\" />" +
            "  <Project Path=\"NotAProject.txt\" />" +
            "</Solution>";

        foreach (var name in new string[] { "Alpha", "Beta", "Gamma" })
        {
            Directory.CreateDirectory(Path.Combine(Scratch, name));
            CreateFileContent(Path.Combine(Scratch, name, name + ".csproj"), Project);
        }

        var path = CreateFileContent("Name.Test.slnx", Slnx);
        var item = new DotnetSolution(path);

        Assert.True(item.IsSolutionFile);
        Assert.True(item.IsXmlSolutionFile);

        item.Refresh();

        Assert.Equal(3, item.Projects.Count);
        Assert.True(item.Projects.ContainsKey("Alpha"));
        Assert.True(item.Projects.ContainsKey("Beta"));
        Assert.True(item.Projects.ContainsKey("Gamma"));
    }

    [Fact]
    public void Refresh_ReadsTraditionalSolutionFormat()
    {
        const string Sln =
            "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Alpha\", \"Alpha\\Alpha.csproj\", \"{1}\"\r\n" +
            "EndProject\r\n";

        Directory.CreateDirectory(Path.Combine(Scratch, "Alpha"));
        CreateFileContent(Path.Combine(Scratch, "Alpha", "Alpha.csproj"), Project);

        var path = CreateFileContent("Name.Test.sln", Sln);
        var item = new DotnetSolution(path);

        Assert.True(item.IsSolutionFile);
        Assert.False(item.IsXmlSolutionFile);

        item.Refresh();

        Assert.Single(item.Projects);
        Assert.True(item.Projects.ContainsKey("Alpha"));
    }

    [Fact]
    public void Refresh_ProjectFileIsSingleProject()
    {
        var path = CreateFileContent("Name.Test.csproj", Project);
        var item = new DotnetSolution(path);

        Assert.False(item.IsSolutionFile);
        Assert.False(item.IsXmlSolutionFile);

        item.Refresh();

        Assert.Single(item.Projects);
        Assert.True(item.Projects.ContainsKey("Name.Test"));
    }
}
