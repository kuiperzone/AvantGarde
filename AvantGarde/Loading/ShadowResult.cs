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

namespace AvantGarde.Loading;

/// <summary>
/// The outcome of a single <see cref="ShadowCopier.Mirror"/> call. The counts distinguish what the
/// mirror holds from what this pass actually had to write, which is the difference between the
/// first mirror of a session and every one after it.
/// </summary>
public sealed class ShadowResult
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public ShadowResult(string source, string destination)
    {
        Source = source;
        Destination = destination;
    }

    /// <summary>
    /// Gets the directory mirrored.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets the mirror directory.
    /// </summary>
    public string Destination { get; }

    /// <summary>
    /// Gets the number of files the source holds.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Gets the number of files copied on this pass.
    /// </summary>
    public int CopyCount { get; set; }

    /// <summary>
    /// Gets the number of stale files removed from the mirror on this pass.
    /// </summary>
    public int DeleteCount { get; set; }

    /// <summary>
    /// Gets the number of source files the mirror deliberately leaves out. See
    /// <see cref="ShadowCopier"/> - they are native code for other platforms and native debug
    /// symbols, and they dominate the size of an Avalonia build output.
    /// </summary>
    public int SkipCount { get; set; }

    /// <summary>
    /// Gets the total size of the source in bytes, excluding what was skipped.
    /// </summary>
    public long ByteCount { get; set; }

    /// <summary>
    /// Gets the number of bytes copied on this pass.
    /// </summary>
    public long CopyByteCount { get; set; }

    /// <summary>
    /// Gets the total size in bytes of the files skipped.
    /// </summary>
    public long SkipByteCount { get; set; }

    /// <summary>
    /// Overrides.
    /// </summary>
    public override string ToString()
    {
        return $"Shadow {Source} -> {Destination}: {CopyCount} of {FileCount} files copied " +
            $"({CopyByteCount / 1024} of {ByteCount / 1024} KiB), {DeleteCount} removed, " +
            $"{SkipCount} skipped ({SkipByteCount / 1024} KiB)";
    }

}
