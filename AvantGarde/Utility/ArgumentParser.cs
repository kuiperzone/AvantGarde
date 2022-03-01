// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas (C) 2022
// LICENSE   : GPL-3.0-or-later
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
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

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
        private static readonly Regex ArgSplitter = new Regex(@"^-{1,2}|=|:", RegexOptions.Compiled);
        private static readonly Regex QuoteRemover = new Regex(@"^['""]?(.*?)['""]?$", RegexOptions.Compiled);
        private static readonly Regex StringSplitter = new Regex("(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", RegexOptions.Compiled);

        private readonly string? _string;
        private readonly StringDictionary _dictionary = new();

        /// <summary>
        /// Constructor with argument string.
        /// See: <see cref="ArgumentParser(IEnumerable{string}, bool)"/>.
        /// </summary>
        public ArgumentParser(string args, bool strict = true)
            : this(SplitSingle(args), strict, false, null)
        {
        }

        /// <summary>
        /// Constructor with argument array. If strict is true, FormatException is thrown if the
        /// arguments contain repeated keys or more than one floating value (without a key). If
        /// strict is false, repeated keys and repeated floating values are ignored.
        /// </summary>
        public ArgumentParser(IEnumerable<string> args, bool strict = true)
            : this(args, strict, false, null)
        {
        }

        private ArgumentParser(IEnumerable<string> args, bool strict, bool stripValue, string[]? stripKeys)
        {
            const string KeyExits = "Repeated option value: ";
            const string MultipleValues = "Contains more than one value: ";

            string[] parts;
            string? key = null;
            string? appendStr = null;
            var sb = new StringBuilder();

            // Valid parameters forms:
            // {-,/,--}param{ ,=,:}((",')value(",'))
            // Examples:
            // -param1 value1 --param2 /param3:"Test-:-work"
            //   /param4=happy -param5 '--=nice=--'
            foreach (var str in args)
            {
                if (string.IsNullOrWhiteSpace(str))
                {
                    continue;
                }
                appendStr = null;

                // Look for new parameters (-,/ or --)
                // and a possible enclosed value (=,:)
                parts = ArgSplitter.Split(str, 3);

                switch (parts.Length)
                {
                case 1:

                    // Found a value for the last parameter
                    // found (space separator)
                    parts[0] = QuoteRemover.Replace(parts[0], "$1");

                    if (key != null)
                    {
                        if (!_dictionary.ContainsKey(key))
                        {
                            appendStr = str;
                            _dictionary.Add(key, parts[0]);
                        }
                        else
                        if (strict)
                        {
                            throw new FormatException(KeyExits + key);
                        }

                        key = null;
                    }
                    else
                    if (Value == null)
                    {
                        if (!stripValue)
                        {
                            appendStr = str;
                            Value = parts[0];
                        }
                    }
                    else
                    if (strict)
                    {
                        // Error: no parameter waiting for a value
                        throw new FormatException(MultipleValues + str);
                    }

                    break;

                case 2:
                    // Found just a parameter
                    // The last parameter is still waiting.
                    // With no value, set it to true.
                    if (key != null)
                    {
                        if (!_dictionary.ContainsKey(key))
                        {
                            appendStr = str;
                            _dictionary.Add(key, bool.TrueString);
                        }
                        else
                        if (strict)
                        {
                            throw new FormatException(KeyExits + key);
                        }
                    }

                    key = Strip(parts[1], stripKeys);
                    break;

                case 3:
                    // Parameter with enclosed value.
                    // The last parameter is still waiting.
                    // With no value, set it to true.
                    if (key != null)
                    {
                        if (!_dictionary.ContainsKey(key))
                        {
                            _dictionary.Add(key, bool.TrueString);
                        }
                        else
                        if (strict)
                        {
                            throw new FormatException(KeyExits + key);
                        }
                    }

                    key = parts[1];

                    // Remove possible enclosing characters (",')
                    if (!_dictionary.ContainsKey(key))
                    {
                        key = Strip(key, stripKeys);

                        if (key != null)
                        {
                            appendStr = str;
                            parts[2] = QuoteRemover.Replace(parts[2], "$1");
                            _dictionary.Add(key, parts[2]);
                        }
                    }
                    else
                    if (strict)
                    {
                        throw new FormatException(KeyExits + key);
                    }

                    key = null;
                    break;
                }

                if (appendStr != null)
                {
                    sb.Append(appendStr);
                    sb.Append(" ");
                }

                // Leave for below
                appendStr = str;
            }

            // In case a parameter is still waiting
            if (key != null)
            {
                if (!_dictionary.ContainsKey(key))
                {
                    sb.Append(appendStr);
                    _dictionary.Add(key, bool.TrueString);
                }
                else
                if (strict)
                {
                    throw new FormatException(KeyExits + key);
                }
            }

            _string = sb.ToString()?.Trim();
            Strict = strict;
        }

        /// <summary>
        /// Gets whether the input was strictly parsed. See the constructor for details.
        /// </summary>
        public bool Strict { get; }

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
            get { return string.IsNullOrEmpty(key) ? Value : _dictionary[key]; }
        }

        /// <summary>
        /// Gets a mandatory value for the given key. The value is converted to the specified type
        /// using InvariantCulture. If the key does not exist, ArgumentException is thrown. If
        /// the key exists but value cannot be parsed, FormatException is thrown. Key case is ignored. If
        /// key is null, <see cref="Value"/> converted and returned.
        /// </summary>
        /// <exception cref="ArgumentException">Mandatory value not specified<exception>
        /// <exception cref="ArgumentException">Mandatory key not specified<exception>
        public T Get<T>(string? key)
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
        /// <see cref="Strict"/> is true (otherwise def is returned). Key case is ignored. If
        /// key is null, <see cref="Value"/> converted and returned.
        /// </summary>
        public T? Get<T>(string? key, T? def)
            where T : IConvertible
        {
            var str = this[key];

            if (str != null)
            {
                return ConvertType(str, Strict, def);
            }

            return def;
        }

        /// <summary>
        /// Gets an array of key names. The order is undefined and values will be lowercase.
        /// A new array instance is returned on each call.
        /// </summary>
        public string[] GetKeys()
        {
            int count = _dictionary.Count;
            var result = Array.Empty<string>();

            if (count > 0)
            {
                result = new string[count];
                count = 0;

                foreach (string key in _dictionary.Keys)
                {
                    result[count++] = key;
                }
            }

            return result;
        }

        /// <summary>
        /// Clones the instance, allowing the removal of specified keys. The <see cref="Value"/>
        /// of the result will be null if stripValue is true.
        /// </summary>
        public ArgumentParser Clone(bool stripValue, params string[] stripKeys)
        {
            if (stripKeys != null)
            {
                // Clean up
                for (int n = 0; n < stripKeys.Length; ++n)
                {
                    stripKeys[n] = stripKeys[n].Trim().TrimStart('-', '/');
                }
            }

            return new ArgumentParser(SplitSingle(_string), Strict, stripValue, stripKeys);
        }

        /// <summary>
        /// Overrides ToString() and returns the command line as a string. The result will be
        /// cleaned of superfluous spaces and any arguments that were ignored.
        /// </summary>
        public override string ToString()
        {
            return _string ?? string.Empty;
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

        private static T? ConvertType<T>(string s, bool strict, T? def)
            where T : IConvertible
        {
            if (!strict)
            {
                try
                {
                    return ConvertType<T>(s);
                }
                catch
                {
                    return def;
                }
            }

            return ConvertType<T>(s);
        }

        private static string[] SplitSingle(string? args)
        {
            // Map: "-size=100 /height:'400' -string \"Dir\\Folder/Path Name!\" --debug"
            // To : { "-size=100", "/height:'400'", "-string", "\"Dir\\Folder/Path Name!\"", "--debug" };

            if (!string.IsNullOrEmpty(args))
            {
                return StringSplitter.Split(args);
            }

            return Array.Empty<string>();
        }

        private static string? Strip(string? key, string[]? stripKeys)
        {
            if (key == null || stripKeys == null)
            {
                return key;
            }

            foreach (var k in stripKeys)
            {
                if (string.Equals(k, key, StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }
            }

            return key;
        }
    }
}