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

public class NodeItemTest : TestUtilBase
{
    public NodeItemTest(ITestOutputHelper helper)
        : base(helper)
    {
    }

    [Fact]
    public void Refresh_UpdatesForFile()
    {
        var path = CreateFileContent("Text.txt", "Hello");
        var item = new NodeItem(path, PathKind.AnyFile);

        // Initial
        Assert.Equal(PathKind.Document, item.Kind);
        Assert.Equal("Text.txt", item.Name);
        Assert.Equal(path, item.FullName);
        Assert.True(item.Exists);
        Assert.NotEqual(default, item.LastUtc);
        Assert.NotEqual(0, item.GetHashCode());

        Assert.Empty(item.Contents);
        Assert.Equal(1, item.TotalFiles);

        // First refresh
        int code = item.GetHashCode();
        Assert.False(item.Refresh());
        Assert.Equal(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.NotEqual(default, item.LastUtc);
        Assert.Equal(1, item.TotalFiles);

        // Again no change
        code = item.GetHashCode();
        Assert.False(item.Refresh());
        Assert.Equal(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.Equal(1, item.TotalFiles);

        // Update
        CreateFileContent(path, "Hello World");
        code = item.GetHashCode();
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.Equal(1, item.TotalFiles);

        // Delete
        File.Delete(path);
        code = item.GetHashCode();
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.False(item.Exists);
        Assert.Equal(0, item.TotalFiles);
        Assert.Equal(default, item.LastUtc);
    }

    [Fact]
    public void Refresh_UpdatesForDirectory()
    {
        var temp = PathItem.CleanPath(CreateNewScratch());
        var sub = Directory.CreateDirectory(temp + "sub") + "/";
        var item = new NodeItem(temp, PathKind.Directory);

        // Important
        item.Properties.ShowEmptyDirectories = true;

        Assert.Equal(PathKind.Directory, item.Kind);
        Assert.NotEmpty(item.Name);
        Assert.Equal(temp, item.FullName);
        Assert.True(item.Exists);
        Assert.NotEqual(default, item.LastUtc);
        Assert.NotEqual(0, item.GetHashCode());

        Assert.Empty(item.Contents);
        Assert.Equal(0, item.TotalFiles);
        Assert.NotEmpty(item.Properties.FilePatterns);
        Assert.NotEmpty(item.Properties.ExcludeDirectories);

        // First refresh
        int code = item.GetHashCode();
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.NotEqual(default, item.LastUtc);
        Assert.Equal(0, item.TotalFiles);
        Assert.Single(item.Contents);

        // Create file in sub-directory
        var file = CreateFileContent(sub + "Text.txt", "Hello");
        code = item.GetHashCode();
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.NotEqual(default, item.LastUtc);
        Assert.Equal(1, item.TotalFiles);
        Assert.Single(item.Contents);

        // Again no change
        code = item.GetHashCode();
        Assert.False(item.Refresh());
        Assert.Equal(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.Equal(1, item.TotalFiles);
        Assert.Single(item.Contents);

        // Two new files
        code = item.GetHashCode();
        CreateFileContent(temp + "Text1.txt", "Hello World");
        CreateFileContent(temp + "Text2.txt", "Hello World");
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.Equal(3, item.TotalFiles);
        Assert.Equal(3, item.Contents.Count);

        // Delete
        code = item.GetHashCode();
        File.Delete(file);
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.Equal(2, item.TotalFiles);
        Assert.Equal(3, item.Contents.Count);
    }

    [Fact]
    public void ExcludedDirectories_Excludes()
    {
        var temp = PathItem.CleanPath(CreateNewScratch());
        var sub = Directory.CreateDirectory(temp + "Exclude") + "/";
        CreateFileContent(sub + "Text1.txt", "Hello World");

        var item = new NodeItem(Scratch, PathKind.Directory);
        Assert.True(item.Refresh());

        // Has the file
        Assert.Equal(1, item.TotalFiles);

        item.Properties.ExcludeDirectories = "Exclude";
        Assert.True(item.Refresh());

        // Now it doesn't
        Assert.Equal(0, item.TotalFiles);
    }

    [Fact]
    public void FilePattern_FiltersOnPattern()
    {
        var temp = PathItem.CleanPath(CreateNewScratch());
        var sub = Directory.CreateDirectory(temp + "SubDir") + "/";
        CreateFileContent(sub + "Text1.txt", "Hello World");
        CreateFileContent(sub + "Text1.axaml", "Hello World");

        var item = new NodeItem(Scratch, PathKind.Directory);
        Assert.True(item.Refresh());
        WriteLine(item.Properties.FilePatterns);
        WriteLine(item.ToString(true));

        // Has both
        Assert.Equal(2, item.TotalFiles);
        item.Properties.FilePatterns = "*.axaml";
        Assert.True(item.Refresh());

        // Now has 1
        Assert.Equal(1, item.TotalFiles);
    }

    [Fact]
    public void FindDirectory_FindsName()
    {
        var temp = CreateNewScratch() + "sub";
        var sub = Directory.CreateDirectory(temp) + "/";
        CreateFileContent(sub + "Text1.txt", "Hello World");

        var item = new NodeItem(Scratch, PathKind.Directory);
        item.Refresh();

        Assert.Null(item.FindDirectory("NotExist"));
        Assert.Null(item.FindDirectory("Text1.txt"));

        Assert.Equal(temp, item.FindDirectory(temp)?.FullName);
        Assert.Equal(temp, item.FindDirectory(temp.ToUpperInvariant(), StringComparison.InvariantCultureIgnoreCase)?.FullName);
    }

    [Fact]
    public void FindFile_FindsName()
    {
        var temp = CreateNewScratch() + "sub";
        var sub = Directory.CreateDirectory(temp) + "/";
        CreateFileContent(sub + "Text1.txt", "Hello World");

        var item = new NodeItem(Scratch, PathKind.Directory);
        item.Refresh();

        Assert.Null(item.FindFile("NotExist"));
        Assert.Equal("Text1.txt", item.FindFile("Text1.txt")?.Name);
        Assert.Equal("Text1.txt", item.FindFile("TEXT1.txt", StringComparison.OrdinalIgnoreCase)?.Name);
    }

    [Fact]
    public void FindNode_FindsPath()
    {
        var temp = CreateNewScratch();
        var sub = Directory.CreateDirectory(temp + "sub") + "/";
        var path = CreateFileContent(sub + "Text1.txt", "Hello World");

        var item = new NodeItem(Scratch, PathKind.Directory);
        item.Refresh();

        Assert.Null(item.FindNode("NotExist"));
        Assert.Null(item.FindNode(""));

        Assert.Equal("Text1.txt", item.FindNode(path)?.Name);
        Assert.Equal("Text1.txt", item.FindNode(path.ToUpperInvariant(), StringComparison.OrdinalIgnoreCase)?.Name);
    }

}
