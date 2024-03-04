// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022-24
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

using System.Reflection;
using System.Text;
using AvantGarde.Utility;

namespace AvantGarde.Markup;

/// <summary>
/// Immutable class which provides information about an attribute of <see cref="MarkupInfo"/>.
/// </summary>
public sealed class AttributeInfo
{
    private readonly string? _help = null;

    /// <summary>
    /// Property constructor.
    /// </summary>
    public AttributeInfo(PropertyInfo info)
    {
        Name = info.Name;
        ValueType = info.PropertyType;
        DeclaringType = info.DeclaringType ??
            throw new InvalidOperationException("Failed to get property DeclaringType for " + Name);
    }

    /// <summary>
    /// Event constructor.
    /// </summary>
    public AttributeInfo(EventInfo info)
    {
        IsEvent = true;
        Name = info.Name;
        ValueType = info.EventHandlerType ??
            throw new InvalidOperationException("Failed to get event EventHandlerType for " + Name);

        DeclaringType = info.DeclaringType ??
            throw new InvalidOperationException("Failed to get event DeclaringType for " + Name);
    }

    /// <summary>
    /// Assignment constructor. The <see cref="IsEvent"/> value is false.
    /// </summary>
    public AttributeInfo(string name, Type type, Type declaring, string? help = "")
    {
        Name = name;
        ValueType = type;
        DeclaringType = declaring;
        _help = help;
    }

    /// <summary>
    /// Gets the attribute name.
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// Gets whether the attribute pertains to an event. Otherwise it is a property.
    /// </summary>
    public readonly bool IsEvent;

    /// <summary>
    /// Gets the value or event handler type.
    /// </summary>
    public readonly Type ValueType;

    /// <summary>
    /// Gets the declaring type.
    /// </summary>
    public readonly Type DeclaringType;

    /// <summary>
    /// Gets a help document string.
    /// </summary>
    public string GetHelpDocument()
    {
        if (_help != null)
        {
            return _help;
        }

        var sb = new StringBuilder(64);
        var vname = ValueType.GetFriendlyName(true);
        var dname = DeclaringType.GetFriendlyName(true);

        if (IsEvent)
        {
            sb.AppendLine("Event:");
            sb.AppendLine();
            sb.Append(vname);
            sb.Append(' ');
            sb.Append(dname);
            sb.Append('.');
            sb.Append(Name);
            return sb.ToString();
        }

        // Property
        sb.AppendLine("Property:");
        sb.AppendLine();
        sb.Append(vname);
        sb.Append(' ');
        sb.Append(dname);
        sb.Append('.');
        sb.Append(Name);

        if (ValueType.IsEnum)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("enum ");
            sb.Append(vname);
            sb.Append(" = {");

            var enums = Enum.GetValues(ValueType);

            for (int n = 0; n < enums.Length; ++n)
            {
                if (n != 0)
                {
                    sb.Append(", ");
                }

                // Max
                if (n == 12 && enums.Length > 12)
                {
                    sb.Append(" ...");
                    break;
                }

                sb.Append(enums.GetValue(n));
            }

            sb.Append('}');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Overrides.
    /// </summary>
    public override string ToString()
    {
        return GetHelpDocument();
    }

}