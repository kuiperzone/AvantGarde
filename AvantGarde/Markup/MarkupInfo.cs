// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

namespace AvantGarde.Markup
{
    /// <summary>
    /// Class which holds immutable design-time property and event information about a markup
    /// element type.
    /// </summary>
    public sealed class MarkupInfo
    {
        private static readonly Type ObjType = typeof(object);

        /// <summary>
        /// Constructor with optional attached property items.
        /// </summary>
        public MarkupInfo(Type classType, IEnumerable<AttributeInfo>? attached = null)
        {
            Name = classType.Name;
            ClassType = classType;

            var attributes = new Dictionary<string, AttributeInfo>(56);
            Attributes = attributes;

            if (attached != null)
            {
                foreach (var item in attached)
                {
                    attributes.TryAdd(item.Name, item);
                }
            }

            foreach (var item in classType.GetProperties())
            {
                // Must have public setter
                if (item.GetSetMethod(true)?.IsPublic == true)
                {
                    attributes.TryAdd(item.Name, new AttributeInfo(item));
                }
            }

            foreach (var item in classType.GetEvents())
            {
                attributes.TryAdd(item.Name, new AttributeInfo(item));
            }
        }

        /// <summary>
        /// Gets the control (short) name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Gets the control class type.
        /// </summary>
        public readonly Type ClassType;

        /// <summary>
        /// Gets a dictionary of property and event information.
        /// </summary>
        public readonly IReadOnlyDictionary<string, AttributeInfo> Attributes;

        /// <summary>
        /// Gets control information text.
        /// </summary>
        public string GetHelpDocument()
        {
            var sb = new StringBuilder(256);

            sb.Append("Class: ");
            sb.AppendLine(ClassType.Name);
            sb.AppendLine();

            sb.Append("Namespace: ");
            sb.AppendLine(ClassType.Namespace);
            sb.AppendLine();

            sb.Append("Base: ");
            sb.AppendLine(GetBaseClasses(ClassType));
            sb.AppendLine();

            var list = GetSelected(false);

            if (list.Count != 0)
            {
                bool first = true;
                sb.Append("Properties: ");

                foreach(var item in list)
                {
                    // Omit attached
                    if (!item.Contains('.'))
                    {
                        if (!first)
                        {
                            sb.Append(", ");
                        }

                        first = false;
                        sb.Append(item);
                    }
                }

                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("Properties: {none}");
            }

            sb.AppendLine();
            list = GetSelected(true);

            if (list.Count != 0)
            {
                sb.Append("Events: ");
                sb.Append(string.Join(", ", list));
            }
            else
            {
                sb.Append("Events: {none}");
            }

            return sb.ToString();
        }

        private static void AddAttribute(List<string> names, Dictionary<string, AttributeInfo> attribs, AttributeInfo info)
        {
            if (attribs.TryAdd(info.Name, info))
            {
                names.Add(info.Name);
            }
        }

        private static string GetBaseClasses(Type? type)
        {
            var sb = new StringBuilder(32);

            while (true)
            {
                type = type?.BaseType;

                if (type != null && type != ObjType)
                {
                    if (sb.Length != 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(type.Name);
                    continue;
                }

                return sb.ToString();
            }
        }

        private List<string> GetSelected(bool events)
        {
            var rslt = new List<string>(Attributes.Count);

            foreach (var item in Attributes.Values)
            {
                if (item.IsEvent == events)
                {
                    rslt.Add(item.Name);
                }
            }

            rslt.Sort();
            return rslt;
        }

    }
}

