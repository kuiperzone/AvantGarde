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

using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Metadata;
using AvantGarde.Utility;

namespace AvantGarde.Markup;

/// <summary>
/// Static class which provides type information about Avalonia.
/// </summary>
public static class MarkupDictionary
{
    private static readonly object _syncObj;
    public static readonly Type _avaloniaObjectType; // <-  FOR REMOVAL
    private static readonly IReadOnlyList<AttributeInfo>? _attached;
    private static readonly Dictionary<Type, MarkupInfo> _cache;

    /// <summary>
    /// Static constructor.
    /// </summary>
    static MarkupDictionary()
    {
        _syncObj = new();
        _avaloniaObjectType = typeof(AvaloniaObject);

        ControlObjectType = typeof(Control);
        Version = Assembly.GetAssembly(ControlObjectType)?.GetName()?.Version?.ToString(3) ??
            throw new InvalidOperationException("Failed to get Avalonia version");

        var types = GetAllNamespaceTypes();

        // Determine attached properties
        var attType = typeof(Avalonia.AttachedProperty<>);
        var attached = new List<AttributeInfo>(64);

        foreach (var item in types.Values)
        {
            if (_avaloniaObjectType.IsAssignableFrom(item))
            {
                foreach (var f in item.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == attType)
                    {
                        var type = f.FieldType.GetGenericArguments()[0];
                        var decl = f.DeclaringType ?? throw new ArgumentNullException(nameof(f.DeclaringType));
                        var name = decl.GetFriendlyName(true) + "." + f.Name.Replace("Property", ""); ;
                        var help = "Attached property type: " + type.GetFriendlyName(true);

                        attached.Add(new AttributeInfo(name, type, decl, help));
                    }
                }
            }
        }


        _cache = new(types.Count / 4);
        Types = types;

        _attached = attached;
        ControlInfo = new MarkupInfo(ControlObjectType, _attached);
    }

    /// <summary>
    /// Gets a friendly name for the markup framework. I.e. "Avalonia".
    /// </summary>
    public const string Name = "Avalonia";

    /// <summary>
    /// Gets the XML "targetNamespace".
    /// </summary>
    public const string XmlNamespace = "https://github.com/avaloniaui";

    /// <summary>
    /// Gets the markup framework version.
    /// </summary>
    public static readonly string Version;

    /// <summary>
    /// Gets a predefined Avalonia.Controls.Control type. FOR REMOVAL
    /// </summary>
    public static readonly Type ControlObjectType;

    /// <summary>
    /// Gets the <cref="MarkupInfo"/> for <see cref="ControlObjectType"/>.  FOR REMOVAL
    /// </summary>
    public static readonly MarkupInfo ControlInfo;

    /// <summary>
    /// Gets a dictionary of available markup types keyed on short-name.
    /// </summary>
    public static IReadOnlyDictionary<string, Type> Types { get; }

    /// <summary>
    /// Returns an instance of <see cref="MarkupInfo"/> given a local name. The result is null if the
    /// name is not found or name is null.
    /// </summary>
    public static MarkupInfo? GetMarkupInfo(string? name)
    {
        lock (_syncObj)
        {
            if (name != null && Types.TryGetValue(name, out var type))
            {
                // Expensive to create, but we can cache
                if (_cache.TryGetValue(type, out var info))
                {
                    return info;
                }

                info = new MarkupInfo(type, _avaloniaObjectType.IsAssignableFrom(type) ? _attached : null);
                _cache.Add(type, info);
                return info;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates an instance of <see cref="MarkupInfo"/> from the given type.
    /// </summary>
    public static MarkupInfo CreateMarkupInfo(Type type)
    {
        return new MarkupInfo(type, _avaloniaObjectType.IsAssignableFrom(type) ? _attached : null);
    }

    private static bool PopulateAssembly(Assembly assembly, Dictionary<Assembly, HashSet<string>> xmlns)
    {
        if (!xmlns.ContainsKey(assembly))
        {
            Debug.WriteLine(null);
            Debug.WriteLine("ASSEMBLY: " + assembly.GetName().Name + " " + assembly.GetName().Version);
            Debug.WriteLine("Location: " + assembly.Location);

            var attribs = assembly.GetCustomAttributes(typeof(XmlnsDefinitionAttribute), false);

            if (attribs.Length != 0)
            {
                var ns = new HashSet<string>();

                foreach (var a in attribs)
                {
                    var x = (XmlnsDefinitionAttribute)a;

                    if (x.XmlNamespace == XmlNamespace)
                    {
                        ns.Add(x.ClrNamespace);
                        Debug.WriteLine(x.ClrNamespace);
                    }
                }

                if (ns.Count != 0)
                {
                    xmlns.Add(assembly, ns);
                }
            }

            return true;
        }

        return false;
    }

    private static Dictionary<string, Type> GetAllNamespaceTypes()
    {
        Debug.WriteLine("INITALIZE DICTIONARY");

        // https://github.com/AvaloniaUI/Avalonia/search?p=2&q=XmlnsDefinition
        var xmlns = new Dictionary<Assembly, HashSet<string>>();

        var dirName = Path.GetDirectoryName(Assembly.GetAssembly(ControlObjectType)?.Location) ??
            throw new InvalidOperationException("Failed to get assembly location");

        Debug.WriteLine("Source location: " + dirName);

        // Don't use AppDomain. Not necessarily listed there.
        foreach (var item in Directory.GetFiles(dirName, "Avalonia*.dll"))
        {
            // LoadFrom(), not LoadFile()
            PopulateAssembly(Assembly.LoadFrom(item), xmlns);
        }

        int total = 0;
        var result = new Dictionary<string, Type>(512);

        foreach (var item in xmlns)
        {
            var asm = item.Key;
            var ns = item.Value;

            foreach (var type in asm.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && !type.IsGenericType &&
                    type.Namespace != null && ns.Contains(type.Namespace))
                {
                    if (result.TryAdd(type.Name, type))
                    {
                        total += 1;
                    }
                }
            }
        }

        Debug.WriteLine(null);
        Debug.WriteLine("ASSEMBLIES: " + xmlns.Count);
        Debug.WriteLine("TOTAL TYPES: " + total);
        Debug.WriteLine(null);

        if (total == 0)
        {
            throw new InvalidOperationException("Failed to get Avalonia information");
        }

        return result;
    }

}
