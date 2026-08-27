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

using System.Text;
using Avalonia.Input;
using Avalonia.Remote.Protocol.Input;

using ProtocolKey = Avalonia.Remote.Protocol.Input.Key;
using ProtocolModifiers = Avalonia.Remote.Protocol.Input.InputModifiers;
using ProtocolPhysicalKey = Avalonia.Remote.Protocol.Input.PhysicalKey;

namespace AvantGarde.Loading;

/// <summary>
/// Class which decodes and carries keyboard event information, being either a key transition or a
/// unit of composed text. The pointer equivalent is <see cref="PointerEventMessage"/>.
/// </summary>
/// <remarks>
/// The designer host routes these to whatever the guest has focused, which is nothing at all until
/// a pointer press gives it something - measured against the 12.0.5 host, key and text messages
/// sent before any click land nowhere, and the same messages after a click on a TextBox type into
/// it. So keyboard forwarding depends on pointer forwarding, and cannot work on its own.
/// </remarks>
public sealed class KeyboardEventMessage
{
    private readonly ProtocolModifiers[] _modifiers;
    private readonly ProtocolKey _key = ProtocolKey.None;
    private readonly ProtocolPhysicalKey _physicalKey = ProtocolPhysicalKey.None;
    private readonly string? _keySymbol;
    private readonly string _text = string.Empty;

    /// <summary>
    /// Constructor. Key pressed or released.
    /// </summary>
    public KeyboardEventMessage(KeyEventArgs e, bool isDown)
    {
        IsDown = isDown;
        _modifiers = InputMapper.GetModifiers(e.KeyModifiers);
        _key = InputMapper.GetKey(e.Key);
        _physicalKey = InputMapper.GetPhysicalKey(e.PhysicalKey);
        _keySymbol = e.KeySymbol;
    }

    /// <summary>
    /// Constructor. Text input.
    /// </summary>
    public KeyboardEventMessage(TextInputEventArgs e)
    {
        IsText = true;
        _text = e.Text ?? string.Empty;

        // TextInputEventArgs carries no modifiers, and the guest does not need them: the text is
        // already the result of applying them, so an empty set is accurate rather than a shortfall.
        _modifiers = Array.Empty<ProtocolModifiers>();
    }

    /// <summary>
    /// Gets whether is a key down event. Only meaningful when <see cref="IsText"/> is false.
    /// </summary>
    public readonly bool IsDown;

    /// <summary>
    /// Gets whether is a text input event rather than a key transition.
    /// </summary>
    public readonly bool IsText;

    /// <summary>
    /// Create an instance of protocol message.
    /// </summary>
    public InputEventMessageBase ToMessage()
    {
        if (IsText)
        {
            var text = new TextInputEventMessage();
            text.Text = _text;
            text.Modifiers = _modifiers;
            return text;
        }

        var key = new KeyEventMessage();
        key.IsDown = IsDown;
        key.Key = _key;
        key.PhysicalKey = _physicalKey;
        key.KeySymbol = _keySymbol;
        key.Modifiers = _modifiers;
        return key;
    }

    /// <summary>
    /// Overrides.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("KeyboardEventMessage: ");

        if (IsText)
        {
            sb.Append("Text '");
            sb.Append(_text);
            sb.Append('\'');
        }
        else
        {
            sb.Append(IsDown ? "KeyDown " : "KeyUp ");
            sb.Append(_key);
            sb.Append(" (");
            sb.Append(_physicalKey);
            sb.Append(')');
        }

        foreach (var item in _modifiers)
        {
            sb.Append(", ");
            sb.Append(item);
        }

        return sb.ToString();
    }

}
