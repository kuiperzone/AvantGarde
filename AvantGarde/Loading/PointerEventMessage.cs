// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022
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
using Avalonia;
using Avalonia.Input;
using Avalonia.Remote.Protocol.Input;
using Avalonia.VisualTree;
using ProtocolButton = Avalonia.Remote.Protocol.Input.MouseButton;
using ProtocolModifiers = Avalonia.Remote.Protocol.Input.InputModifiers;

namespace AvantGarde.Loading
{
    /// <summary>
    /// Class which decodes and carries a pointer event information.
    /// </summary>
    public sealed class PointerEventMessage
    {
        private readonly Point _position;
        private readonly List<ProtocolModifiers> _modifiers = new();
        private readonly ProtocolButton _button = ProtocolButton.None;

        /// <summary>
        /// Constructor. Mouse move.
        /// </summary>
        public PointerEventMessage(Visual sender, PointerEventArgs e)
        {
            IsMoved = true;
            _position = GetModifiers(sender, e, _modifiers);
        }

        /// <summary>
        /// Constructor. Pointer pressed.
        /// </summary>
        public PointerEventMessage(Visual sender, PointerPressedEventArgs e)
        {
            IsPressed = true;
            _position = GetModifiers(sender, e, _modifiers);
            _button = GetPressButton(sender, e);
        }

        /// <summary>
        /// Constructor. Pointer released.
        /// </summary>
        public PointerEventMessage(Visual sender, PointerReleasedEventArgs e)
        {
            IsReleased = true;
            _position = GetModifiers(sender, e, _modifiers);
            _button = GetReleaseButton(e);
        }

        /// <summary>
        /// Gets whether is move event.
        /// </summary>
        public readonly bool IsMoved;

        /// <summary>
        /// Gets whether is press event.
        /// </summary>
        public readonly bool IsPressed;

        /// <summary>
        /// Gets whether is released event.
        /// </summary>
        public readonly bool IsReleased;

        /// <summary>
        /// Gets whether is press or release event.
        /// </summary>
        public bool IsPressOrReleased
        {
            get { return IsPressed || IsReleased; }
        }

        /// <summary>
        /// Create an instance of protocol message.
        /// </summary>
        /// <exception cref="ArgumentException">Invalid scale value</exception>
        public PointerEventMessageBase ToMessage(double scale)
        {
            if (scale < 0 || !double.IsFinite(scale))
            {
                throw new ArgumentException("Invalid scale value");
            }

            PointerEventMessageBase msg;

            if (IsMoved)
            {
                msg = new PointerMovedEventMessage();
            }
            else
            if (IsPressed)
            {
                var pressed = new PointerPressedEventMessage();
                pressed.Button = _button;
                msg = pressed;
            }
            else
            if (IsReleased)
            {
                var released = new PointerReleasedEventMessage();
                released.Button = _button;
                msg = released;
            }
            else
            {
                throw new ArgumentException("Invalid pointer type code");
            }

            msg.Modifiers = _modifiers.ToArray();
            msg.X = _position.X / scale;
            msg.Y = _position.Y / scale;

            return msg;
        }

        /// <summary>
        /// Overrides.
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("PointerEventMessage: ");

            if (IsMoved)
            {
                sb.Append(nameof(IsMoved));
            }
            else
            if (IsPressed)
            {
                sb.Append(nameof(IsPressed));
            }
            else
            if (IsReleased)
            {
                sb.Append(nameof(IsReleased));
            }

            sb.Append(", ");
            sb.Append(_position);

            foreach (var item in _modifiers)
            {
                sb.Append(", ");
                sb.Append(item);
            }

            return sb.ToString();
        }

        private static ProtocolButton GetReleaseButton(PointerReleasedEventArgs e)
        {
            switch (e.InitialPressMouseButton)
            {
                case Avalonia.Input.MouseButton.Left: return ProtocolButton.Left;
                case Avalonia.Input.MouseButton.Right: return ProtocolButton.Right;
                case Avalonia.Input.MouseButton.Middle: return ProtocolButton.Middle;
                default: return ProtocolButton.None;
            }
        }

        private static ProtocolButton GetPressButton(Visual sender, PointerEventArgs e)
        {
            var p = e.GetCurrentPoint(sender);

            if (p.Properties.IsLeftButtonPressed)
            {
                return ProtocolButton.Left;
            }

            if (p.Properties.IsRightButtonPressed)
            {
                return ProtocolButton.Right;
            }

            if (p.Properties.IsMiddleButtonPressed)
            {
                return ProtocolButton.Middle;
            }

            return ProtocolButton.None;
        }

        private static Point GetModifiers(Visual sender, PointerEventArgs e, List<ProtocolModifiers> mods)
        {
            var p = e.GetCurrentPoint(sender);

            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                mods.Add(ProtocolModifiers.Alt);
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                mods.Add(ProtocolModifiers.Control);
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                mods.Add(ProtocolModifiers.Shift);
            }

            // KeyModifiers.Meta?
            // ProtocolModifiers.Windows?

            if (p.Properties.IsLeftButtonPressed)
            {
                mods.Add(ProtocolModifiers.LeftMouseButton);
            }

            if (p.Properties.IsRightButtonPressed)
            {
                mods.Add(ProtocolModifiers.RightMouseButton);
            }

            if (p.Properties.IsMiddleButtonPressed)
            {
                mods.Add(ProtocolModifiers.MiddleMouseButton);
            }

            return p.Position;
        }

    }
}