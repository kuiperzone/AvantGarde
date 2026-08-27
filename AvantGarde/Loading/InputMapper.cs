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

using ProtocolKey = Avalonia.Remote.Protocol.Input.Key;
using ProtocolModifiers = Avalonia.Remote.Protocol.Input.InputModifiers;
using ProtocolPhysicalKey = Avalonia.Remote.Protocol.Input.PhysicalKey;

namespace AvantGarde.Loading;

/// <summary>
/// Maps Avalonia input values onto the remote designer protocol's own copies of them. The methods
/// are static and take primitives rather than event arguments, so that they can be tested without
/// an input device or a running application.
/// </summary>
public static class InputMapper
{
    /// <summary>
    /// Converts keyboard modifiers, with no pointer buttons held.
    /// </summary>
    public static ProtocolModifiers[] GetModifiers(KeyModifiers keys)
    {
        return GetModifiers(keys, false, false, false);
    }

    /// <summary>
    /// Converts keyboard modifiers together with the pointer buttons currently held.
    /// </summary>
    public static ProtocolModifiers[] GetModifiers(KeyModifiers keys, bool left, bool right, bool middle)
    {
        var list = new List<ProtocolModifiers>();

        if (keys.HasFlag(KeyModifiers.Alt))
        {
            list.Add(ProtocolModifiers.Alt);
        }

        if (keys.HasFlag(KeyModifiers.Control))
        {
            list.Add(ProtocolModifiers.Control);
        }

        if (keys.HasFlag(KeyModifiers.Shift))
        {
            list.Add(ProtocolModifiers.Shift);
        }

        // The protocol calls it Windows and Avalonia calls it Meta, but both name the same physical
        // key - Avalonia's Win32 backend raises Meta for VK_LWIN/VK_RWIN, and its macOS backend
        // raises it for Command. This resolves the long-standing TODO in PointerEventMessage.
        if (keys.HasFlag(KeyModifiers.Meta))
        {
            list.Add(ProtocolModifiers.Windows);
        }

        if (left)
        {
            list.Add(ProtocolModifiers.LeftMouseButton);
        }

        if (right)
        {
            list.Add(ProtocolModifiers.RightMouseButton);
        }

        if (middle)
        {
            list.Add(ProtocolModifiers.MiddleMouseButton);
        }

        return list.ToArray();
    }

    /// <summary>
    /// Converts a key code. Undefined values map to <see cref="ProtocolKey.None"/>.
    /// </summary>
    public static ProtocolKey GetKey(Key key)
    {
        // The two enumerations are copies of one another: every one of the 223 names in
        // Avalonia.Input.Key appears in Avalonia.Remote.Protocol.Input.Key with the same numeric
        // value, verified against 12.1.0. The cast is therefore exact, and the guard exists only so
        // that a future divergence degrades to None instead of sending a value the host cannot read.
        var rslt = (ProtocolKey)key;
        return Enum.IsDefined(rslt) ? rslt : ProtocolKey.None;
    }

    /// <summary>
    /// Converts a physical key code. Undefined values map to <see cref="ProtocolPhysicalKey.None"/>.
    /// </summary>
    public static ProtocolPhysicalKey GetPhysicalKey(PhysicalKey key)
    {
        // As GetKey - the 165 names and values match across the two assemblies.
        var rslt = (ProtocolPhysicalKey)key;
        return Enum.IsDefined(rslt) ? rslt : ProtocolPhysicalKey.None;
    }

}
