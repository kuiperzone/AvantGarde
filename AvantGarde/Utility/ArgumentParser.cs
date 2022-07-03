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

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AvantGarde.Utility
{
    /// <summary>
    /// Simple argument parser which accepts input either as a single string or string array. It holds arguments as
    /// key-value pairs in a case insensitive dictionary. The class is immutable and, therefore, instance thread-safe.
    /// Arguments keys are to be prefixed with '-' or "--", as shown by the following command line example:
    /// -size=100 -string \"Dir\\Folder/File Name!\" --debug.
    /// Here, the flag "debug" flag will be interpreted as the key-value pair: -debug=True.
    /// </summary>
    public class ArgumentParser
    {
        // This code was originally derived from MIT licensed work by Richard Lopes but has been
        // substantially modified from: https://www.codeproject.com/Articles/3111/C-NET-Command-Line-Arguments-Parser
        private const string KeyExists = "Repeated option value: ";
        private const string MultipleValues = "Contains more than one value: ";

        private static readonly Regex ArgSplitter = new Regex(@"^-{1,2}|=", RegexOptions.Compiled);
        private static readonly Regex QuoteRemover = new Regex(@"^['""]?(.*?)['""]?$", RegexOptions.Compiled);
        private static readonly Regex StringSplitter = new Regex("(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", RegexOptions.Compiled);

        private readonly char _sep = ' ';
        private readonly string? _string;
        private readonly List<string> _keys = new();
        private readonly Dictionary<string, string> _dictionary = new();

        /// <summary>
        /// Constructor with argument string.
        /// See: <see cref="ArgumentParser(IEnumerable{string}, bool)"/>.
        /// </summary>
        public ArgumentParser(string args, bool strict = true)
            : this(SplitString(args), args, strict)
        {
        }

        /// <summary>
        /// Constructor with argument array. If strict is true, FormatException is thrown if the
        /// arguments contain repeated keys or more than one floating value (without a key). If
        /// strict is false, repeated keys and repeated floating values are ignored.
        /// </summary>
        public ArgumentParser(IEnumerable<string> args, bool strict = true)
            : this(args, null, strict)
        {
        }

        private ArgumentParser(IEnumerable<string> args, string? str, bool strict)
        {
            StringBuilder? sb = str == null ? new(128) : null;
            Tuple<string?, string?, string?>? lastKey = null;

            foreach (var temp in args)
            {
                if (sb != null)
                {
                    if (sb.Length != 0)
                    {
                        sb.Append(' ');
                    }

                    sb.Append(temp);
                }

                var part = SplitPart(temp);

                if (part == null)
                {
                    continue;
                }

                if (part.Item2 != null && part.Item3 != null)
                {
                    // Key AND value
                    AddTrailingKey(lastKey, strict);
                    lastKey = null;

                    // Handle this key and value
                    if (_dictionary.TryAdd(part.Item2.ToLowerInvariant(), part.Item3))
                    {
                        _sep = '=';
                        _keys.Add(part.Item2);
                    }
                    else
                    if (strict)
                    {
                        throw new FormatException(KeyExists + part.Item2);
                    }
                }
                else
                if (part.Item2 != null && part.Item3 == null)
                {
                    // Key only
                    AddTrailingKey(lastKey, strict);

                    // Hold
                    lastKey = part;
                }
                else
                if (part.Item2 == null && part.Item3 != null)
                {
                    if (lastKey?.Item2 != null)
                    {
                        if (_dictionary.TryAdd(lastKey.Item2.ToLowerInvariant(), part.Item3))
                        {
                            _keys.Add(lastKey.Item2);
                        }
                        else
                        if (strict)
                        {
                            throw new FormatException(KeyExists + part.Item2);
                        }

                        lastKey = null;
                    }
                    else
                    if (Value == null)
                    {
                        Value = part.Item3;
                    }
                    else
                    if (strict)
                    {
                        // Error: no parameter waiting for a value
                        throw new FormatException(MultipleValues + part.Item3);
                    }
                }
            }

            AddTrailingKey(lastKey, strict);

            if (_keys.Count != _dictionary.Count)
            {
                throw new InvalidOperationException("Error - key and dictionary counts different in constructor");
            }

            _string = str ?? sb?.ToString();
            IsStrict = strict;
        }

        private ArgumentParser(List<string> keys, Dictionary<string, string> dict, string? value, string? str,
            char sep, bool strict)
        {
            // Clone only
            _sep = sep;
            _keys = keys;
            _dictionary = dict;

            Value = value;

            _string = str;
            IsStrict = strict;

            if (_string == null)
            {
                var sb = new StringBuilder(Quote(value), 128);

                foreach (var name in keys)
                {
                    if (sb.Length != 0)
                    {
                        sb.Append(' ');
                    }

                    if (name.Length > 1)
                    {
                        sb.Append('-');
                    }

                    sb.Append('-');
                    sb.Append(name);

                    var temp = dict[name.ToLowerInvariant()];

                    if (temp.Length != 0)
                    {
                        sb.Append(sep);
                        sb.Append(Quote(temp));
                    }
                }

                _string = sb.ToString();
            }

        }

        /// <summary>
        /// Gets whether the input was strictly parsed. See the constructor for details.
        /// </summary>
        public bool IsStrict { get; }

        /// <summary>
        /// Gets the floating value (or command) contained in the input. A floating value is one
        /// which lacks an argument key. Only one such value is allowed in the arguments. For
        /// example, given the input "command -debug", <see cref="Value"/> will be "command".
        /// The value is null if the input lacked a floating value.
        /// </summary>
        public string? Value { get; }

        /// <summary>
        /// Gets the number of argument keys. For example, given the input "command -size=8 -debug",
        /// <see cref="Count"/> will be 2 rather then 3, as "command" is the <see cref="Value"/>.
        /// </summary>
        public int Count
        {
            get { return _dictionary.Count; }
        }

        /// <summary>
        /// Gets the value for a given argument key name. Key case is ignored. The result is null if
        /// the specified key does not exist. For a simple flag argument string given to the
        /// constructor as "-debug", the result of ["debug"] will be "True". If key is null or
        /// empty, the result is that of <see cref="Value"/>.
        /// </summary>
        public string? this[string? key]
        {
            get
            {
                if (string.IsNullOrEmpty(key))
                {
                    return Value;
                }

                if (_dictionary.TryGetValue(key.ToLowerInvariant(), out string? rslt))
                {
                    if (rslt.Length == 0)
                    {
                        // Default
                        return bool.TrueString;
                    }

                    return rslt;
                }

                return null;
            }
        }

        /// <summary>
        /// Static utility which adds quotes around a string if it contains a space and does not
        /// begin with a quote character. If the string is null, the result is null.
        /// </summary>
        public static string? Quote(string? s)
        {
            if (s != null && !s.StartsWith('"') && s.Contains(' '))
            {
                return $"\"{s}\"";
            }

            return s;
        }

        /// <summary>
        /// Gets a mandatory string value for the given key. If the key does not exist, ArgumentException is
        /// thrown. If the key exists but value cannot be parsed, FormatException is thrown. Key case is ignored.
        /// If key is null, <see cref="Value"/> converted and returned.
        /// </summary>
        /// <exception cref="ArgumentException">Mandatory value not specified</exception>
        /// <exception cref="ArgumentException">Mandatory key not specified</exception>
        public string GetOrThrow(string? key)
        {
            var str = this[key];

            if (str == null)
            {
                if (key == null)
                {
                    throw new ArgumentException($"Mandatory value not specified");
                }

                throw new ArgumentException($"Mandatory '{key}' not specified");
            }

            return str;
        }

        /// <summary>
        /// Gets a mandatory value for the given key. The value is converted to the specified type
        /// using InvariantCulture. If the key does not exist, ArgumentException is thrown. If
        /// the key exists but value cannot be parsed, FormatException is thrown. Key case is ignored. If
        /// key is null, <see cref="Value"/> converted and returned.
        /// </summary>
        /// <exception cref="ArgumentException">Mandatory value not specified</exception>
        /// <exception cref="ArgumentException">Mandatory key not specified</exception>
        public T GetOrThrow<T>(string? key)
            where T : IConvertible
        {
            var str = this[key];

            if (str == null)
            {
                if (key == null)
                {
                    throw new ArgumentException($"Mandatory value not specified");
                }

                throw new ArgumentException($"Mandatory '{key}' not specified");
            }

            return ConvertType<T>(str);
        }

        /// <summary>
        /// Gets an optional value for the given key. The value is converted to the specified type
        /// using InvariantCulture. If the key does not exist, the given default value is returned
        /// instead. If the key exists but value cannot be parsed, FormatException is thrown if
        /// <see cref="IsStrict"/> is true (otherwise def is returned). Key case is ignored. If
        /// key is null, <see cref="Value"/> converted and returned.
        /// </summary>
        public T GetOrDefault<T>(string? key, T def)
            where T : IConvertible
        {
            var str = this[key];

            if (str != null)
            {
                return ConvertType(str, IsStrict, def);
            }

            return def;
        }

        /// <summary>
        /// Gets an array of key names. A new array instance is returned on each call.
        /// </summary>
        public string[] GetKeys()
        {
            return _keys.ToArray();
        }

        /// <summary>
        /// Clones the instance, allowing the removal of specified keys. The <see cref="Value"/>
        /// of the result will be null if stripValue is true.
        /// </summary>
        public ArgumentParser Clone(bool stripValue = false, params string[] stripKeys)
        {
            var keys = _keys;
            var dict = _dictionary;
            var value = !stripValue ? Value : null;
            var str = Value == value ? _string : null;

            if (stripKeys != null && stripKeys.Length != 0)
            {
                keys = new(_keys);
                dict = new(_dictionary);

                for (int n = 0; n < stripKeys.Length; ++n)
                {
                    var temp = stripKeys[n].Trim().TrimStart('-').ToLowerInvariant();

                    if (dict.Remove(temp))
                    {
                        str = null;

                        for (int k = 0; k < keys.Count; ++k)
                        {
                            if (keys[k].Equals(temp, StringComparison.InvariantCultureIgnoreCase))
                            {
                                Console.WriteLine("Removed from keys: " + temp);
                                keys.RemoveAt(k);
                                break;
                            }
                        }
                    }
                }

                if (keys.Count != dict.Count)
                {
                    throw new InvalidOperationException("Error - key and dictionary counts different in clone");
                }
            }

            return new ArgumentParser(keys, dict, value, str, _sep, IsStrict);
        }

        /// <summary>
        /// Overrides and returns string.
        /// </summary>
        public override string ToString()
        {
            return _string ?? "";
        }

        private static T ConvertType<T>(string s)
            where T : IConvertible
        {
            if (typeof(T) == typeof(bool))
            {
                // Flexibility for boolean
                s = s.ToLowerInvariant();

                if (s == "true" || s == "yes" || s == "y")
                {
                    return (T)(object)true;
                }

                if (s == "false" || s == "no" || s == "n")
                {
                    return (T)(object)false;
                }
            }

            return (T)Convert.ChangeType(s, typeof(T), CultureInfo.InvariantCulture);
        }

        private static T ConvertType<T>(string s, bool strict, T def)
            where T : IConvertible
        {
            if (strict)
            {
                return ConvertType<T>(s);
            }

            try
            {
                return ConvertType<T>(s);
            }
            catch
            {
                return def;
            }
        }

        private bool AddTrailingKey(Tuple<string?, string?, string?>? part, bool strict)
        {
            if (part?.Item2 != null)
            {
                // No value
                if (_dictionary.TryAdd(part.Item2.ToLowerInvariant(), ""))
                {
                    // Assume flag is true
                    _keys.Add(part.Item2);
                    return true;
                }

                if (strict)
                {
                    throw new FormatException(KeyExists + part.Item2);
                }
            }

            return false;
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

        private static Tuple<string?, string?, string?>? SplitPart(string? str)
        {
            // Item1 = prefix ("--" or "-")
            // Item2 = key
            // Item3 = value
            if (!string.IsNullOrEmpty(str))
            {
                // Look for new parameters (-,/ or --)
                // and a possible enclosed value (=,:)
                var parts = ArgSplitter.Split(str, 3);

                switch(parts.Length)
                {
                    case 1:
                        // Found a value only (but may belong to previous key)
                        return new Tuple<string?, string?, string?>(null, null, QuoteRemover.Replace(parts[0], "$1"));
                    case 2:
                        // Found a key only
                        return new Tuple<string?, string?, string?>(parts[0], parts[1], null);
                    case 3:
                        return new Tuple<string?, string?, string?>(parts[0], parts[1], QuoteRemover.Replace(parts[2], "$1"));
                }
            }

            return null;
        }

    }
}