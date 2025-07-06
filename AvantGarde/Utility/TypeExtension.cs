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

namespace AvantGarde.Utility;

/// <summary>
/// Provides extension methods.
/// </summary>
public static class TypeExtension
{
    private static readonly IReadOnlyDictionary<Type, string> _friendlyTypesNames = new Dictionary<Type, string>
    {
        { typeof(string), "string" },
        { typeof(object), "object" },
        { typeof(bool), "bool" },
        { typeof(byte), "byte" },
        { typeof(char), "char" },
        { typeof(decimal), "decimal" },
        { typeof(double), "double" },
        { typeof(short), "short" },
        { typeof(int), "int" },
        { typeof(long), "long" },
        { typeof(sbyte), "sbyte" },
        { typeof(float), "float" },
        { typeof(ushort), "ushort" },
        { typeof(uint), "uint" },
        { typeof(ulong), "ulong" },
        { typeof(void), "void" }
    };

    /// <summary>
    /// Gets a friendly name for primitive and generic types.
    /// </summary>
    public static string? GetFriendlyName(this Type? type, bool escape = false)
    {
        // Event: System.EventHandler`1[[Avalonia.Input.PointerEventArgs, Avalonia.Input, Version=0.10.12.0, Culture=neutral, PublicKeyToken=c8d484a7012f9a8b]] RuntimeType.PointerMoved
        if (type != null)
        {
            string? suffix = null;
            var nullable = Nullable.GetUnderlyingType(type);

            if (nullable != null)
            {
                suffix = "?";
                type = nullable;
            }

            if (_friendlyTypesNames.TryGetValue(type, out string? name))
            {
                return name + suffix;
            }

            if (type.IsArray && type.GetElementType() != null)
            {
                return type.GetElementType().GetFriendlyName(escape) + "[]" + suffix;
            }

            if (type.IsGenericType)
            {
                // I.e. System.EventHandler`1[Avalonia.Input.TextInputEventArgs]
                int p0 = type.Name.IndexOf('`');

                if (p0 > 0)
                {
                    var sb = new StringBuilder(type.Name.Remove(p0), 32);

                    if (escape)
                    {
                        sb.Append("&lt;");
                    }
                    else
                    {
                        sb.Append('<');
                    }

                    var gp = type.GetGenericArguments();

                    for (int n = 0; n < gp.Length; ++n)
                    {
                        if (n != 0)
                        {
                            sb.Append(", ");
                        }

                        sb.Append(gp[n].GetFriendlyName(escape));
                    }

                    if (escape)
                    {
                        sb.Append("&gt;");
                    }
                    else
                    {
                        _ = sb.Append('>');
                    }

                    return sb.ToString();
                }
            }

            return type.Name + suffix;
        }

        return null;
    }

}