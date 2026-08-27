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

using Avalonia.Input;
using AvantGarde.Loading;
using AvantGarde.Test.Internal;
using Xunit;
using Xunit.Abstractions;

using ProtocolKey = Avalonia.Remote.Protocol.Input.Key;
using ProtocolModifiers = Avalonia.Remote.Protocol.Input.InputModifiers;
using ProtocolPhysicalKey = Avalonia.Remote.Protocol.Input.PhysicalKey;

namespace AvantGarde.Loading.Test;

/// <summary>
/// Covers the conversion of Avalonia input values to the remote protocol's own copies of them.
/// </summary>
/// <remarks>
/// The mapper takes primitives rather than event arguments precisely so that this can exist -
/// PointerWheelEventArgs and KeyEventArgs need an input device and a live application, so the
/// message classes which wrap them remain out of reach of a unit test.
/// </remarks>
public class InputMapperTest(ITestOutputHelper helper) : TestUtilBase(helper)
{
    [Fact]
    public void GetModifiers_NoneGivesEmpty()
    {
        Assert.Empty(InputMapper.GetModifiers(KeyModifiers.None));
    }

    [Fact]
    public void GetModifiers_CombinesKeysAndButtons()
    {
        var mods = InputMapper.GetModifiers(KeyModifiers.Control | KeyModifiers.Shift, true, false, true);

        WriteLine(string.Join(", ", mods));
        Assert.Equal(4, mods.Length);
        Assert.Contains(ProtocolModifiers.Control, mods);
        Assert.Contains(ProtocolModifiers.Shift, mods);
        Assert.Contains(ProtocolModifiers.LeftMouseButton, mods);
        Assert.Contains(ProtocolModifiers.MiddleMouseButton, mods);
        Assert.DoesNotContain(ProtocolModifiers.RightMouseButton, mods);
    }

    [Fact]
    public void GetModifiers_MetaGivesWindows()
    {
        // The protocol's name for the key Avalonia calls Meta. This was a TODO in
        // PointerEventMessage for the life of the file, so the mapping is asserted rather than
        // assumed.
        var mods = InputMapper.GetModifiers(KeyModifiers.Meta);

        Assert.Single(mods);
        Assert.Equal(ProtocolModifiers.Windows, mods[0]);
    }

    [Fact]
    public void GetKey_MapsByValue()
    {
        Assert.Equal(ProtocolKey.A, InputMapper.GetKey(Key.A));
        Assert.Equal(ProtocolKey.Back, InputMapper.GetKey(Key.Back));
        Assert.Equal(ProtocolKey.F12, InputMapper.GetKey(Key.F12));
        Assert.Equal(ProtocolKey.None, InputMapper.GetKey(Key.None));
    }

    [Fact]
    public void GetKey_EveryAvaloniaKeyIsDefined()
    {
        // The cast in GetKey is only exact while the two enumerations agree. If a later Avalonia
        // adds a key the protocol does not have, this fails rather than the previewer silently
        // sending a value the host cannot read.
        foreach (var key in Enum.GetValues<Key>())
        {
            Assert.True(Enum.IsDefined((ProtocolKey)key), "No protocol key for " + key);
            Assert.Equal(key.ToString(), InputMapper.GetKey(key).ToString());
        }
    }

    [Fact]
    public void GetPhysicalKey_EveryAvaloniaKeyIsDefined()
    {
        foreach (var key in Enum.GetValues<PhysicalKey>())
        {
            Assert.True(Enum.IsDefined((ProtocolPhysicalKey)key), "No protocol physical key for " + key);
            Assert.Equal(key.ToString(), InputMapper.GetPhysicalKey(key).ToString());
        }
    }

    [Fact]
    public void GetKey_UndefinedGivesNone()
    {
        Assert.Equal(ProtocolKey.None, InputMapper.GetKey((Key)9999));
        Assert.Equal(ProtocolPhysicalKey.None, InputMapper.GetPhysicalKey((PhysicalKey)9999));
    }

}
