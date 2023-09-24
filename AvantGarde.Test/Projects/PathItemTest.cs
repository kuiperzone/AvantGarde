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
using System.Runtime.InteropServices;
using System.Text;
using AvantGarde.Test.Internal;
using Xunit;
using Xunit.Abstractions;

namespace AvantGarde.Projects.Test;

public class PathItemTest : TestUtilBase
{
    public PathItemTest(ITestOutputHelper helper)
        : base(helper)
    {
    }

    [Fact]
    public void Refresh_UpdatesForFile()
    {
        var path = CreateFileContent("Text.txt", "Hello");
        var item = new PathItem(path, PathKind.AnyFile);

        // Initial
        Assert.Equal(PathKind.Document, item.Kind);
        Assert.Equal("Text.txt", item.Name);
        Assert.Equal(path, item.FullName);
        Assert.True(item.Exists);
        Assert.NotEqual(default, item.LastUtc);
        Assert.True(item.Length > 0);
        Assert.NotEqual(0, item.GetHashCode());

        // No change
        int code = item.GetHashCode();
        Assert.False(item.Refresh());
        Assert.Equal(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.True(item.Length > 0);

        // Update
        code = item.GetHashCode();
        CreateFileContent(path, "Hello World");
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.True(item.Length > 0);

        // No change
        code = item.GetHashCode();
        Assert.False(item.Refresh());
        Assert.Equal(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.True(item.Length > 0);

        // Delete
        File.Delete(path);
        code = item.GetHashCode();
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.False(item.Exists);
        Assert.Equal(default, item.LastUtc);
        Assert.Equal(0, item.Length);
    }

    [Fact]
    public void Refresh_UpdatesForDirectory()
    {
        var path = Scratch + "test";
        Directory.CreateDirectory(path);
        var item = new PathItem(path, PathKind.Directory);

        Assert.Equal(PathKind.Directory, item.Kind);
        Assert.NotEmpty(item.Name);
        Assert.False(item.Name.EndsWith('/'));
        Assert.Equal(path, item.FullName);
        Assert.True(item.Exists);
        Assert.NotEqual(default, item.LastUtc);
        Assert.Equal(0, item.Length);
        Assert.NotEqual(0, item.GetHashCode());

        // No change
        int code = item.GetHashCode();
        Assert.False(item.Refresh());
        Assert.Equal(code, item.GetHashCode());
        Assert.True(item.Exists);
        Assert.Equal(0, item.Length);

        // Delete
        Directory.Delete(item.FullName);
        code = item.GetHashCode();
        Assert.True(item.Refresh());
        Assert.NotEqual(code, item.GetHashCode());
        Assert.False(item.Exists);
        Assert.Equal(default, item.LastUtc);
        Assert.Equal(0, item.Length);
    }

    [Fact]
    public void CopyConstructor_File()
    {
        var path = CreateFileContent("Text.cs", "Hello");
        var item = new PathItem(path, PathKind.AnyFile);
        item.Refresh();

        File.Delete(path);
        var copy = new PathItem(item);

        Assert.Equal(PathKind.CSharp, copy.Kind);
        Assert.Equal("Text.cs", copy.Name);
        Assert.Equal(path, copy.FullName);
        Assert.True(copy.Exists);
        Assert.NotEqual(default, copy.LastUtc);
        Assert.Equal(item.Length, copy.Length);
        Assert.Equal(item.GetHashCode(), copy.GetHashCode());

        // Change because have deleted
        Assert.True(copy.Refresh());
        Assert.False(copy.Exists);
        Assert.Equal(default, copy.LastUtc);
        Assert.Equal(0, copy.Length);
        Assert.NotEqual(item.GetHashCode(), copy.GetHashCode());
    }

    [Fact]
    public void CopyConstructor_Directory()
    {
        var item = new PathItem(Scratch, PathKind.Directory);
        var copy = new PathItem(item);

        Assert.Equal(PathKind.Directory, copy.Kind);
        Assert.NotEmpty(copy.Name);
        Assert.False(copy.Name.EndsWith('/'));
        Assert.Equal(PathItem.CleanPath(Scratch), copy.FullName);
        Assert.True(copy.Exists);
        Assert.NotEqual(default, copy.LastUtc);
        Assert.Equal(0, copy.Length);
        Assert.Equal(item.GetHashCode(), copy.GetHashCode());

        Assert.False(copy.Refresh());
        Assert.True(copy.Exists);
        Assert.NotEqual(default, copy.LastUtc);
        Assert.Equal(0, copy.Length);
        Assert.Equal(item.GetHashCode(), copy.GetHashCode());
    }

    [Fact]
    public void Constructor_RootExist()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var item = new PathItem("C:\\", PathKind.Directory);
            Assert.Equal("C:\\", item.Name);
            Assert.Equal("C:\\", item.FullName);
            Assert.Equal("", item.ParentDirectory);
        }
        else
        {
            var item = new PathItem("/", PathKind.Directory);
            Assert.Equal("/", item.Name);
            Assert.Equal("/", item.FullName);
            Assert.Equal("", item.ParentDirectory);
        }
    }

    [Fact]
    public void Constructor_EmptyPathThrows()
    {
        Assert.Throws<ArgumentException>(() => new PathItem("", PathKind.Document));
        Assert.Throws<ArgumentException>(() => new PathItem("", PathKind.Directory));
    }

    [Fact]
    public void Exists_FileNotFound()
    {
        var item = new PathItem("Kdiehde/djiedah.png", PathKind.AnyFile);
        Assert.Equal(PathKind.Image, item.Kind);
        Assert.False(item.Exists);
    }

    [Fact]
    public void Exists_DirectoryNotFound()
    {
        var item = new PathItem("Kdiehde/djiedah.cs", PathKind.Directory);
        Assert.Equal(PathKind.Directory, item.Kind);
        Assert.False(item.Exists);
    }

    [Fact]
    public void ReadAsText_ReadsUtf8()
    {
        var content = "Hello World£";
        var path = CreateFileContent(content, Encoding.UTF8);
        var item = new PathItem(path, PathKind.AnyFile);
        Assert.Equal(content, item.ReadAsText().Trim());
    }

    [Fact]
    public void ReadAsText_ReadsLatin1()
    {
        var content = "Hello World£";
        var path = CreateFileContent("temp.txt", content, Encoding.Latin1);
        var item = new PathItem(path, PathKind.AnyFile);
        Assert.Equal(content, item.ReadAsText().Trim());
    }

    [Fact]
    public void ReadAsText_ReadsUnicode()
    {
        var content = "Hello World£";
        var path = CreateFileContent("temp.txt", content, Encoding.Unicode);
        var item = new PathItem(path, PathKind.AnyFile);
        Assert.Equal(content, item.ReadAsText().Trim());
    }

    [Fact]
    public void ReadAsText_ThrowsIfDirectory()
    {
        var item = new PathItem(Scratch, PathKind.Directory);
        Assert.Throws<InvalidOperationException>(() => item.ReadAsText());
    }

    [Fact]
    public void ReadAsText_ThrowsIfFileNotFound()
    {
        var path = Scratch + "NotFound.txt";
        var item = new PathItem(path, PathKind.AnyFile);
        Assert.Throws<FileNotFoundException>(() => { item.ReadAsText(); });
    }

}
