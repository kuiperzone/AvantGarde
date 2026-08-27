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

using AvantGarde.Loading;
using AvantGarde.Test.Internal;
using Xunit;
using Xunit.Abstractions;

namespace AvantGarde.Loading.Test;

/// <summary>
/// Covers the mirroring of a build output directory. It needs no project and no host, because
/// <see cref="ShadowCopier"/> takes and returns directory paths and nothing else.
/// </summary>
public class ShadowCopierTest(ITestOutputHelper helper) : TestUtilBase(helper)
{
    [Fact]
    public void Mirror_CopiesTheWholeTree()
    {
        var source = CreateOutputDirectory("net8.0");

        using var copier = new ShadowCopier(CreateNewScratch() + "root");
        var rslt = copier.Mirror(source);

        Assert.Equal(3, rslt.FileCount);
        Assert.Equal(3, rslt.CopyCount);
        Assert.Equal(0, rslt.DeleteCount);

        Assert.True(File.Exists(Path.Combine(rslt.Destination, "App.dll")));
        Assert.True(File.Exists(Path.Combine(rslt.Destination, "App.deps.json")));

        // Native libraries sit under runtimes/, so the copy has to recurse.
        var native = Path.Combine(rslt.Destination, "runtimes", "native.so");
        Assert.True(File.Exists(native));
        Assert.Equal("native", File.ReadAllText(native));
    }

    [Fact]
    public void Remap_ReturnsTheMirroredPath()
    {
        var source = CreateOutputDirectory("net8.0");

        using var copier = new ShadowCopier(CreateNewScratch() + "root");
        var rslt = copier.Mirror(source);

        Assert.Equal(Path.Combine(rslt.Destination, "App.dll"),
            copier.Remap(Path.Combine(source, "App.dll")));

        // A file which does not exist still remaps. The deps and runtimeconfig paths are derived
        // from the assembly name rather than found, so one of them can be absent.
        Assert.Equal(Path.Combine(rslt.Destination, "App.runtimeconfig.json"),
            copier.Remap(Path.Combine(source, "App.runtimeconfig.json")));

        // Subdirectories.
        Assert.Equal(Path.Combine(rslt.Destination, "runtimes", "native.so"),
            copier.Remap(Path.Combine(source, "runtimes", "native.so")));

        // The directory itself.
        Assert.Equal(rslt.Destination, copier.Remap(source));
    }

    [Fact]
    public void Remap_ReturnsNullWhereNothingWasMirrored()
    {
        var source = CreateOutputDirectory("net8.0");

        using var copier = new ShadowCopier(CreateNewScratch() + "root");
        Assert.Null(copier.Remap(Path.Combine(source, "App.dll")));

        copier.Mirror(source);
        Assert.Null(copier.Remap(Path.Combine(Scratch, "Elsewhere", "Other.dll")));
        Assert.Null(copier.Remap(null));
    }

    [Fact]
    public void Mirror_SecondPassCopiesOnlyWhatChanged()
    {
        var source = CreateOutputDirectory("net8.0");

        using var copier = new ShadowCopier(CreateNewScratch() + "root");
        copier.Mirror(source);

        // What a build does to one assembly of many. The length is deliberately unchanged - the
        // write time alone has to be enough, or a rebuild can go unnoticed.
        File.WriteAllText(Path.Combine(source, "App.dll"), "ASSEMBLY-2");

        var rslt = copier.Mirror(source);

        Assert.Equal(3, rslt.FileCount);
        Assert.Equal(1, rslt.CopyCount);
        Assert.Equal("ASSEMBLY-2", File.ReadAllText(Path.Combine(rslt.Destination, "App.dll")));
    }

    [Fact]
    public void Mirror_RemovesWhatTheSourceNoLongerHas()
    {
        var source = CreateOutputDirectory("net8.0");

        using var copier = new ShadowCopier(CreateNewScratch() + "root");
        var rslt = copier.Mirror(source);
        var stale = Path.Combine(rslt.Destination, "runtimes", "native.so");
        Assert.True(File.Exists(stale));

        File.Delete(Path.Combine(source, "runtimes", "native.so"));
        rslt = copier.Mirror(source);

        Assert.Equal(2, rslt.FileCount);
        Assert.Equal(0, rslt.CopyCount);
        Assert.Equal(1, rslt.DeleteCount);
        Assert.False(File.Exists(stale));

        // The directory goes with its last file, rather than being left empty.
        Assert.False(Directory.Exists(Path.Combine(rslt.Destination, "runtimes")));
    }

    [Fact]
    public void Mirror_TwoSourcesWithTheSameLeafNameDoNotCollide()
    {
        // The case in every multi-project solution: both output directories are called "net8.0",
        // and the two-assembly path mirrors both.
        var app = CreateOutputDirectory(Path.Combine("AvaloniaApp1", "bin", "Debug", "net8.0"));
        var lib = CreateOutputDirectory(Path.Combine("ClassLibrary1", "bin", "Debug", "net8.0"));

        using var copier = new ShadowCopier(CreateNewScratch() + "root");
        var appRslt = copier.Mirror(app);
        var libRslt = copier.Mirror(lib);

        Assert.NotEqual(appRslt.Destination, libRslt.Destination);

        Assert.Equal(Path.Combine(appRslt.Destination, "App.dll"),
            copier.Remap(Path.Combine(app, "App.dll")));

        Assert.Equal(Path.Combine(libRslt.Destination, "App.dll"),
            copier.Remap(Path.Combine(lib, "App.dll")));
    }

    [Fact]
    public void Mirror_SameSourceTwiceKeepsOneMirror()
    {
        var source = CreateOutputDirectory("net8.0");
        var root = CreateNewScratch() + "root";

        using var copier = new ShadowCopier(root);
        copier.Mirror(source);

        // Trailing separator, which is what Path.GetDirectoryName never returns but a caller can.
        copier.Mirror(source + Path.DirectorySeparatorChar);

        Assert.Single(Directory.EnumerateDirectories(root));
    }

    [Fact]
    public void Mirror_SkipsOtherPlatformsAndNativeSymbols()
    {
        var source = CreateOutputDirectory("net8.0");
        var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;

        // What an Avalonia output directory is mostly made of. "other-rid" stands in for the
        // win-x86, win-arm64, linux and osx trees a real one carries.
        CreateFile(source, Path.Combine("runtimes", rid, "native", "libSkiaSharp.dll"), "native");
        CreateFile(source, Path.Combine("runtimes", rid, "native", "libSkiaSharp.pdb"), "symbols");
        CreateFile(source, Path.Combine("runtimes", rid, "lib", "net8.0", "Managed.dll"), "managed");
        CreateFile(source, Path.Combine("runtimes", rid, "lib", "net8.0", "Managed.pdb"), "symbols");
        CreateFile(source, Path.Combine("runtimes", "other-rid", "native", "libSkiaSharp.dll"), "native");

        using var copier = new ShadowCopier(CreateNewScratch() + "root");
        var rslt = copier.Mirror(source);

        Assert.Equal(2, rslt.SkipCount);
        Assert.False(File.Exists(Path.Combine(rslt.Destination, "runtimes", "other-rid", "native", "libSkiaSharp.dll")));
        Assert.False(File.Exists(Path.Combine(rslt.Destination, "runtimes", rid, "native", "libSkiaSharp.pdb")));

        // The native library itself is needed, and so are managed symbols wherever they sit.
        Assert.True(File.Exists(Path.Combine(rslt.Destination, "runtimes", rid, "native", "libSkiaSharp.dll")));
        Assert.True(File.Exists(Path.Combine(rslt.Destination, "runtimes", rid, "lib", "net8.0", "Managed.pdb")));

        // Nothing outside a runtimes directory is ever skipped, symbols included.
        CreateFile(source, "App.pdb", "symbols");
        rslt = copier.Mirror(source);
        Assert.True(File.Exists(Path.Combine(rslt.Destination, "App.pdb")));
    }

    [Fact]
    public void Mirror_KeepsTheLessSpecificRuntimeIdentifiers()
    {
        var source = CreateOutputDirectory("net8.0");
        var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
        var os = rid.Split('-')[0];

        // "win" against a process running as "win-x64". A native library placed there is one the
        // resolver can reach, so dropping it would break the host with a missing DLL.
        CreateFile(source, Path.Combine("runtimes", os, "native", "libSkiaSharp.dll"), "native");
        CreateFile(source, Path.Combine("runtimes", "any", "lib", "Managed.dll"), "managed");

        using var copier = new ShadowCopier(CreateNewScratch() + "root");
        var rslt = copier.Mirror(source);

        Assert.Equal(0, rslt.SkipCount);
        Assert.True(File.Exists(Path.Combine(rslt.Destination, "runtimes", os, "native", "libSkiaSharp.dll")));
        Assert.True(File.Exists(Path.Combine(rslt.Destination, "runtimes", "any", "lib", "Managed.dll")));
    }

    [Fact]
    public async Task Mirror_WaitsForAFileSomethingElseIsWriting()
    {
        // The mid-build case. A mirror can start while MSBuild still has the assembly open, and
        // giving up on it means falling back to launching from the output directory - taking the
        // very lock the copy exists to avoid, at the moment the user is provably building.
        var source = CreateOutputDirectory("net8.0");
        var path = Path.Combine(source, "App.dll");

        using var copier = new ShadowCopier(CreateNewScratch() + "root");
        var held = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None);

        var release = Task.Run(() =>
        {
            Thread.Sleep(300);
            held.Dispose();
        });

        var rslt = copier.Mirror(source);
        await release;

        Assert.Equal(3, rslt.CopyCount);
        Assert.True(File.Exists(Path.Combine(rslt.Destination, "App.dll")));
    }

    [Fact]
    public void Dispose_RemovesTheRoot()
    {
        var source = CreateOutputDirectory("net8.0");
        var root = CreateNewScratch() + "root";

        var copier = new ShadowCopier(root);
        copier.Mirror(source);
        Assert.True(Directory.Exists(root));

        copier.Dispose();
        Assert.False(Directory.Exists(root));

        // The source is untouched by any of it.
        Assert.True(File.Exists(Path.Combine(source, "App.dll")));
    }

    [Fact]
    public void SweepStaleRoots_RemovesOnlyDeadProcessRoots()
    {
        var parent = CreateNewScratch() + "parent";

        // A root belonging to this process, one belonging to a process id which cannot exist, and
        // something this class did not create.
        var live = Path.Combine(parent, Environment.ProcessId.ToString());
        var dead = Path.Combine(parent, "2147483646");
        var alien = Path.Combine(parent, "not-a-pid");

        foreach (var dir in new string[] { live, dead, alien })
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "App.dll"), "ASSEMBLY");
        }

        ShadowCopier.SweepStaleRoots(parent, Environment.ProcessId.ToString());

        Assert.True(Directory.Exists(live));
        Assert.True(Directory.Exists(alien));
        Assert.False(Directory.Exists(dead));
    }

    [Fact]
    public void SweepStaleRoots_DoesNotThrowOnAMissingParent()
    {
        ShadowCopier.SweepStaleRoots(Path.Combine(Scratch, "NoSuchDirectory"), "1");
    }

    /// <summary>
    /// Creates a directory under the scratch which looks like a build output - an assembly, the
    /// deps file beside it, and something in a subdirectory.
    /// </summary>
    private string CreateOutputDirectory(string local)
    {
        var dir = Path.Combine(Scratch, local);
        Directory.CreateDirectory(Path.Combine(dir, "runtimes"));

        File.WriteAllText(Path.Combine(dir, "App.dll"), "ASSEMBLY");
        File.WriteAllText(Path.Combine(dir, "App.deps.json"), "{}");
        File.WriteAllText(Path.Combine(dir, "runtimes", "native.so"), "native");

        return dir;
    }

    /// <summary>
    /// Creates a file, and the directories leading to it, under the given root.
    /// </summary>
    private static void CreateFile(string root, string local, string content)
    {
        var path = Path.Combine(root, local);
        var dir = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, content);
    }

}
