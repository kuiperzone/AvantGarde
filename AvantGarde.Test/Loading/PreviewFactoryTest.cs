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

using AvantGarde.Projects;
using AvantGarde.Test.Internal;
using Xunit;
using Xunit.Abstractions;

namespace AvantGarde.Loading.Test;

public class PreviewFactoryTest(ITestOutputHelper helper) : TestUtilBase(helper)
{
    private const string LibraryProject =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>" +
        "<ItemGroup><PackageReference Include=\"Avalonia\" Version=\"12.0.5\"/></ItemGroup></Project>";

    private const string Control =
        "<UserControl xmlns=\"https://github.com/avaloniaui\"><TextBlock Text=\"Hello\"/></UserControl>";

    [Fact]
    public void CreatePreview_CarriesBuildableErrorThrough()
    {
        // The whole chain the Build button hangs off: CheckForError marks the missing assembly
        // buildable, LoadPayload picks the project error up for a XAML item, and the factory must
        // carry the flag into the error the preview control is given.
        var path = CreateFileContent("Name.Test.csproj", LibraryProject);
        CreateFileContent("MainView.axaml", Control);

        var project = new DotnetProject(path);
        project.Refresh();

        var item = project.Contents.FindFile("MainView.axaml");
        Assert.NotNull(item);
        Assert.Equal(PathKind.Xaml, item.Kind);

        var preview = new LoadPayload(item).CreateFactory().CreatePreview();

        Assert.Equal("Debug assembly not found", preview.Error?.Message);
        Assert.True(preview.Error?.CanBuild);
    }

    [Fact]
    public void CreatePreview_TransientMessageIsNotBuildable()
    {
        // "Resolving project..." and friends are raised against no project, so they must not
        // acquire a Build button.
        var preview = new LoadPayload(new ProjectError("Resolving project...")).CreateFactory().CreatePreview();

        Assert.Equal("Resolving project...", preview.Error?.Message);
        Assert.False(preview.Error?.CanBuild);
    }
}
