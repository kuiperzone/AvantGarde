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

using System;
using System.IO;
using AvantGarde.Test.Internal;
using Xunit;
using Xunit.Abstractions;

namespace AvantGarde.Projects.Test;

public class DotnetProjectTest : TestUtilBase
{
    private const string ProjectNet5 =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net5.0</TargetFramework></PropertyGroup>" +
        "<ItemGroup><PackageReference Include=\"Avalonia\" Version=\"0.10.11\"/></ItemGroup></Project>";
    private const string ProjectNet6 =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net6.0</TargetFramework></PropertyGroup>" +
        "<ItemGroup><PackageReference Include=\"Avalonia\" Version=\"0.10.12\"/></ItemGroup></Project>";

    public DotnetProjectTest(ITestOutputHelper helper)
        : base(helper)
    {
    }

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