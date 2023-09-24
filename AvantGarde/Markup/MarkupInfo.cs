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

using System.Text;

namespace AvantGarde.Markup;

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

