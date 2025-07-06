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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace AvantGarde.Utility;

/// <summary>
/// An argument parser class which, on construction, accepts argument input and provides an
/// immutable case sensitive dictionary of name-values.
/// </summary>
public class ArgumentParser
{
    private const string ErrorPrefix = "Syntax error";

    private enum Prefix { None, Short, Long, Value }
    private static readonly Regex StringSplitter = new("(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", RegexOptions.Compiled);

    private readonly List<string> _values;
    private readonly List<string> _keys;
    private readonly Dictionary<string, string> _dictionary;
    private readonly string? _string;

    /// <summary>
    /// Default constructor which reads arguments from <see cref="Environment.GetCommandLineArgs()"/>.
    /// </summary>
    public ArgumentParser()
        : this(GetEnvironmentArgs())
    {
    }

    /// <summary>
    /// Constructor with argument input string. A null value is treated the same as an empty
    /// string. ArgumentException is thrown if arguments contain repeated keys or more than one
    /// floating value (without a key).
    /// </summary>
    /// <exception cref="ArgumentException">Syntax error - invalid input</exception>
    public ArgumentParser(string? args)
        : this(SplitString(args))
    {
        // Verbatim
        _string = args?.Trim() ?? "";
    }
    /// <summary>
    /// Constructor with argument input array. ArgumentException is thrown if arguments contain
    /// repeated keys or more than one floating value (without a key).
    /// </summary>
    /// <exception cref="ArgumentException">Syntax error - invalid input</exception>
    public ArgumentParser(IEnumerable<string> args)
    {
        // Note, args could contain many variations, including:
        // -p,HelloWorld
        // -p=,HelloWorld
        // -p,:,HelloWorld
        // -p,:,"Hello World"
        // -p="Hello World"
        _values = new();
        Values = _values;

        _keys = new();
        Keys = _keys;
        _dictionary = new();

        string? trailName = null;
        Prefix trailKind = Prefix.None;
        bool acceptWin = AcceptWinStyle;

        foreach (var item in args)
        {
            var kind = SplitKeyValue(item, out string? key, out string? value, ref acceptWin);

            if (kind == Prefix.Value)
            {
                if (trailKind != Prefix.None)
                {
                    AddKeyValue(trailKind, trailName, value);
                    trailKind = Prefix.None;
                }
                else
                {
                    if (_keys.Count != 0)
                    {
                        throw new ArgumentException($"{ErrorPrefix}: repeated input floating value or command {value}");
                    }

                    // Cannot be null
                    _values.Add(value ?? "");
                }
            }
            else
            if (kind != Prefix.None)
            {
                // Do we have a trailing key with no value (assume flag)?
                AddKeyValue(trailKind, trailName, "true");
                trailKind = Prefix.None;

                if (!AddKeyValue(kind, key, value))
                {
                    trailKind = kind;
                    trailName = key;
                }
            }
        }

        // Ensure any trailing is added
        AddKeyValue(trailKind, trailName, "true");
    }

    private ArgumentParser(List<string> values, List<string> keys, Dictionary<string, string> dict, string? str)
    {
        // Clone only
        _values = values;
        Values = values;

        _keys = keys;
        Keys = keys;
        _dictionary = dict;

        _string = str;
    }

    /// <summary>
    /// Gets whether the parser accepts Windows style input, i.e. in the form "/a /b
    /// /key:value". Where <see cref="AcceptWinStyle"/> is true, the parser also accepts
    /// Linux style input.
    /// </summary>
    public static bool AcceptWinStyle { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Gets leading floating values contained in the input. A floating value is one which lacks
    /// an argument key and, if present, must appear before options. For example, given the
    /// input "command --debug", the first index of <see cref="Values"/> will contain "command".
    /// The sequence is empty if the input lacks floating values.
    /// </summary>
    public IReadOnlyList<string> Values { get; }

    /// <summary>
    /// Gets the number of argument keys. For example, given the input "command --size=8
    /// --debug", <see cref="Count"/> will be 2 rather than 3, as "command" is given in <see
    /// cref="Values"/>.
    /// </summary>
    public int Count
    {
        get { return _dictionary.Count; }
    }

    /// <summary>
    /// Gets a sequence of keys in the order they were received.
    /// </summary>
    public IReadOnlyList<string> Keys { get; }

    /// <summary>
    /// Gets the value for a given argument key name (case sensitive). The key string should not be prefixed with
    /// "-" or "/". The indexer returns null if the specified key does not exist. For a simple flag given to the
    /// constructor as "--debug", the result of ["debug"] will be "true".
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public string? this[string key]
    {
        get
        {
            if (_dictionary.TryGetValue(key ?? throw new ArgumentNullException(nameof(key)), out string? rslt))
            {
                return rslt;
            }

            return null;
        }
    }

    /// <summary>
    /// Static utility which conditionally adds quotes around a string if it contains a space
    /// and does not begin with a quote character. Given "Hello World", it returns "\"Hello
    /// World\"". If the string is null, the result is null.
    /// </summary>
    [return: NotNullIfNotNull("s")]
    public static string? QuoteAsNeeded(string? s)
    {
        if (s != null && s.Contains(' '))
        {
            if (!s.Contains('"'))
            {
                return '"' + s + '"';
            }

            // Contains '"' char - use "'" quote if not already quoted
            if (!s.StartsWith('"') && !s.EndsWith('"') && !s.StartsWith('\'') && !s.EndsWith('\''))
            {
                return '\'' + s.Replace("'", "''") + '\'';
            }
        }

        return s;
    }

    /// <summary>
    /// Gets a mandatory string value for the given key, or throws ArgumentException if the key
    /// does not exist. The key string should not be prefixed with "-" or "/".
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    public string GetOrThrow(string key)
    {
        return this[key] ?? throw new ArgumentException($"{ErrorPrefix}:  mandatory '{key}' value not specified");
    }

    /// <summary>
    /// Overload of <see cref="GetOrThrow(string)"/> with key pair. This is useful for returning
    /// a value with both short and long name forms. The key string should not be prefixed with
    /// "-" or "/". Example: GetOrThrow('v' "version").
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    public string GetOrThrow(char shortKey, string longKey)
    {
        return GetOrDefault(shortKey.ToString(), null) ??
            GetOrThrow(longKey ?? throw new ArgumentNullException(nameof(longKey)));
    }

    /// <summary>
    /// Gets a mandatory value for the given key, or throws ArgumentException if the key does
    /// not exist. On return, the value is converted to the specified type (assuming
    /// InvariantCulture). If the key exists but value cannot be converted, FormatException is
    /// thrown. The key string should not be prefixed with "-" or "/".
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    /// <exception cref="FormatException">Invalid value format (conversion error)</exception>
    public T GetOrThrow<T>(string key)
        where T : IConvertible
    {
        var str = this[key] ?? throw new ArgumentException($"{ErrorPrefix}: mandatory '{key}' value not specified");
        return ConvertType<T>(key, str);
    }

    /// <summary>
    /// Overload of <see cref="AssertKey{T}(string)"/> with key pair and conversion type. This
    /// is useful for returning a value with both short and long name forms. The key string
    /// should not be prefixed with "-" or "/". Example: parser.GetOrThrow&lt;bool&gt;('v',
    /// "version").
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    /// <exception cref="FormatException">Invalid value format (conversion error)</exception>
    public T GetOrThrow<T>(char shortKey, string longKey)
        where T : IConvertible
    {
        var s = shortKey.ToString();

        if (_keys.Contains(s))
        {
            return GetOrThrow<T>(s);
        }

        return GetOrThrow<T>(longKey ?? throw new ArgumentNullException(nameof(longKey)));
    }

    /// <summary>
    /// Gets an optional value for the given key, or returns the supplied default if the key
    /// does not exist (the default may be null). The key string should not be prefixed with "-"
    /// or "/". Example: parser.GetOrDefault("style", "Linux").
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    [return: NotNullIfNotNull("def")]
    public string? GetOrDefault(string key, string? def)
    {
        return this[key] ?? def;
    }

    /// <summary>
    /// Overload with key pair. This is useful for returning a value with both short and long
    /// name forms. The key string should not be prefixed with "-" or "/".
    /// Example: parser.GetOrDefault('s', "style", "Linux").
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    [return: NotNullIfNotNull("def")]
    public string? GetOrDefault(char shortKey, string longKey, string? def)
    {
        return GetOrDefault(shortKey.ToString(),
            GetOrDefault(longKey ?? throw new ArgumentNullException(nameof(longKey)), def));
    }

    /// <summary>
    /// Gets an optional value for the given key, or returns the supplied default if the key
    /// does not exist (the default may be null for class types). On return, the value is
    /// converted to the specified type (assumes InvariantCulture). If the key exists but value
    /// cannot be converted, FormatException is thrown. The key string should not be prefixed
    /// with "-" or "/". Example: parser.GetOrDefault("style", StyleKind.Linux).
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    /// <exception cref="FormatException">Invalid value format (conversion error)</exception>
    [return: NotNullIfNotNull("def")]
    public T? GetOrDefault<T>(string key, T? def)
        where T : IConvertible
    {
        var str = this[key];

        if (str != null)
        {
            return ConvertType<T>(key, str);
        }

        return def;
    }

    /// <summary>
    /// Overload with key pair and conversion type. This is useful for returning a value with
    /// both short and long name forms. The key string should not be prefixed with "-" or "/".
    /// Example: parser.GetOrDefault('s', "style", StyleKind.Linux).
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    /// <exception cref="FormatException">Invalid value format (conversion error)</exception>
    [return: NotNullIfNotNull("def")]
    public T? GetOrDefault<T>(char shortKey, string longKey, T? def)
        where T : IConvertible
    {
        return GetOrDefault(shortKey.ToString(),
            GetOrDefault(longKey ?? throw new ArgumentNullException(nameof(shortKey)), def));
    }

    /// <summary>
    /// Asserts a mandatory string value for the given key, or throws ArgumentException if the
    /// key does not exist. The key string should not be prefixed with "-" or "/".
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    public void AssertKey(string key)
    {
        GetOrThrow(key);
    }

    /// <summary>
    /// Overload of <see cref="AssertKey(string)"/> with key pair. This is useful for returning
    /// a value with both short and long name forms. The key string should not be prefixed with
    /// "-" or "/". Example: AssertKey('v', "version").
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    public void AssertKey(char shortKey, string longKey)
    {
        GetOrThrow(shortKey, longKey);
    }

    /// <summary>
    /// Gets a mandatory value for the given key, or throws ArgumentException if the key does
    /// not exist. On return, the value is converted to the specified type (assuming
    /// InvariantCulture). If the key exists but value cannot be converted, FormatException is
    /// thrown. The key string should not be prefixed with "-" or "/".
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    /// <exception cref="FormatException">Invalid value format (conversion error)</exception>
    public void AssertKey<T>(string key)
        where T : IConvertible
    {
        GetOrThrow<T>(key);
    }

    /// <summary>
    /// Overload of <see cref="AssertKey{T}(string)"/> with key pair and conversion type. This
    /// is useful for returning a value with both short and long name forms. The key string
    /// should not be prefixed with "-" or "/". Example: parser.AssertKey&lt;bool&gt;('v',
    /// "version").
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Mandatory key option not specified</exception>
    /// <exception cref="FormatException">Invalid value format (conversion error)</exception>
    public void AssertKey<T>(char shortKey, string longKey)
        where T : IConvertible
    {
        GetOrThrow<T>(shortKey, longKey);
    }

    /// <summary>
    /// Clones the instance, allowing for optional removal of <see cref="Values"/> and specified
    /// keys. The result <see cref="Values"/> sequence will be empty if removeValues is true.
    /// Example: parser.Clone(false, "r", "run").
    /// </summary>
    public ArgumentParser Clone(bool removeValues = false, params string[] removeKeys)
    {
        var values = _values;
        var keys = _keys;
        var dict = _dictionary;
        var str = _string;

        if (removeValues)
        {
            str = null;
            values = new();
        }

        if (removeKeys.Length != 0)
        {
            str = null;
            keys = new(_keys);
            dict = new(_dictionary);

            for (int n = 0; n < removeKeys.Length; ++n)
            {
                var temp = removeKeys[n].Trim().TrimStart('-', '/');

                if (dict.Remove(temp))
                {
                    int idx = keys.IndexOf(temp);

                    if (idx > -1)
                    {
                        keys.RemoveAt(idx);
                    }
                }
            }

            if (keys.Count != dict.Count)
            {
                // Not expected
                throw new InvalidOperationException("Unexpected error - key and dictionary counts different in clone");
            }
        }

        return new ArgumentParser(values, keys, dict, str);
    }

    /// <summary>
    /// Overrides and argument input. For the array constructor variants, the result may not
    /// match the input verbatim, but will be equivalent.
    /// </summary>
    public override string ToString()
    {
        return _string ?? BuildString();
    }

    private static string[] GetEnvironmentArgs()
    {
        // First is always exec name
        var src = Environment.GetCommandLineArgs();
        var dst = new string[src.Length - 1];
        Array.Copy(src, 1, dst, 0, dst.Length);
        return dst;
    }

    private static T ConvertType<T>(string key, string value)
        where T : IConvertible
    {
        var t = typeof(T);

        if (t.IsEnum)
        {
            if (Enum.TryParse(t, value, true, out object? rslt) && rslt != null)
            {
                return (T)rslt;
            }

            throw new FormatException($"{ErrorPrefix}: invalid value for '{key}' option {value}");
        }

        if (t == typeof(bool))
        {
            // Flexibility for boolean
            value = value.ToLowerInvariant();

            if (value == "true" || value == "yes" || value == "y")
            {
                return (T)(object)true;
            }

            if (value == "false" || value == "no" || value == "n")
            {
                return (T)(object)false;
            }
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch
        {
            throw new FormatException($"{ErrorPrefix}: invalid value for '{key}' option {value}");
        }
    }

    private static string RemoveQuotes(string s)
    {
        if (s.Length > 1)
        {
            if ((s.StartsWith('"') && s.EndsWith('"')) || s.StartsWith('\'') && s.EndsWith('\''))
            {
                return s.Substring(1, s.Length - 2);
            }
        }

        return s;
    }

    private static string[] SplitString(string? args)
    {
        // Map: "-size=100 -height:'400' -string \"Dir\\Folder/Path Name!\" --debug"
        // To : { "-size=100", "-height:'400'", "-string", "\"Dir\\Folder/Path Name!\"", "--debug" };
        if (!string.IsNullOrEmpty(args))
        {
            return StringSplitter.Split(args);
        }

        return Array.Empty<string>();
    }


    private static Prefix SplitKeyValue(string item, out string? key, out string? value, ref bool acceptWin)
    {
        key = null;
        value = null;

        if (item.Length == 0 || item == "=" || item == ":")
        {
            return Prefix.None;
        }

        var rslt = Prefix.Value;

        if (item[0] == '/' && acceptWin)
        {
            // Only allowed on windows as leading "/" prefix
            // may be confused with rooted (leading "/") path values
            rslt = Prefix.Long;
            item = item.TrimStart('/');
        }
        else
        if (item[0] == '-')
        {
            acceptWin = false;
            rslt = item.StartsWith("--") ? Prefix.Long : Prefix.Short;
            item = item.TrimStart('-');
        }

        if (item.Length == 0)
        {
            // Invalid
            return Prefix.None;
        }

        if (rslt == Prefix.Value)
        {
            value = RemoveQuotes(item);
            return rslt;
        }

        // Have key but may be split?
        key = item;

        // 0123456789
        // "size=1:00" : idx=4
        // "size:1=00" : idx=4
        // "size"      : idx=-1
        int idx = item.IndexOf('=');
        int c = item.IndexOf(':');

        if (c > 0 && (idx < 0 || c < idx))
        {
            idx = c;
        }

        if (idx > -1)
        {
            key = item.Substring(0, idx);
            item = item.Substring(idx + 1);

            if (item.Length != 0)
            {
                value = RemoveQuotes(item);
            }
        }

        return rslt;
    }

    private string BuildString()
    {
        // Build string on fly
        var sb = new StringBuilder(256);

        foreach (var item in _values)
        {
            if (sb.Length != 0)
            {
                sb.Append(' ');
            }

            sb.Append(QuoteAsNeeded(item));
        }

        foreach (var item in _keys)
        {
            var value = _dictionary[item];

            if (sb.Length != 0)
            {
                sb.Append(' ');
            }

            if (item.Length > 1)
            {
                sb.Append("--");
            }
            else
            {
                sb.Append('-');
            }

            sb.Append(item);

            if (value.Length != 0)
            {
                sb.Append('=');
                sb.Append(QuoteAsNeeded(value));
            }
        }

        return sb.ToString();
    }

    private bool AddKeyValue(Prefix kind, string? name, string? value)
    {
        if (kind != Prefix.None && kind != Prefix.Value)
        {
            if (string.IsNullOrEmpty(name))
            {
                // Not expected - error if throws
                throw new InvalidOperationException("Unexpected error - key is null or empty with item");
            }

            if (!string.IsNullOrEmpty(value))
            {
                if (kind == Prefix.Long)
                {
                    if (_dictionary.TryAdd(name, value))
                    {
                        _keys.Add(name);

                        // Return true if consumed
                        return true;
                    }


                    throw new ArgumentException($"{ErrorPrefix}: repeated input argument {name}");
                }

                if (kind == Prefix.Short)
                {
                    // Handle single or combined short flags, ie. -abc [true]
                    foreach (var f in name)
                    {
                        if (_dictionary.TryAdd(f.ToString(), value))
                        {
                            _keys.Add(f.ToString());
                            continue;
                        }

                        throw new ArgumentException($"{ErrorPrefix}: repeated input argument {f}");
                    }

                    // Return true if consumed
                    return true;
                }
            }
        }

        return false;
    }

}
