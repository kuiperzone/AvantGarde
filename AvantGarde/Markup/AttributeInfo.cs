// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Text;
using AvantGarde.Utility;

namespace AvantGarde.Markup
{
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
        public string GetHelpDocument(bool escape = true)
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
                sb.Append(".");
                sb.Append(Name);
                return sb.ToString();
            }

            // Property
            sb.AppendLine("Property:");
            sb.AppendLine();
            sb.Append(vname);
            sb.Append(" ");
            sb.Append(dname);
            sb.Append(".");
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

                sb.Append("}");
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
}