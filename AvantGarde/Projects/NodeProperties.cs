// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace AvantGarde.Projects
{
    /// <summary>
    /// Class which holds exclusion and inclusion rules for <see cref="NodeItem"/>. This class is intended to
    /// be JSON friendly.
    /// </summary>
    public class NodeProperties
    {
        private const StringSplitOptions SplitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;
        private const string DefaultPattern = "*";
        private static readonly IEnumerable<string> DefaultPatternSequence = new[] { "*" };
        private const string DefaultExclude = "obj;ref";
        private static readonly IEnumerable<string> DefaultExcludeSequence = DefaultExclude.Split(';', SplitOptions);

        private int _searchDepth = 8;
        private string _excludeDirectories = DefaultExclude;
        private readonly List<string> _excludes = new(DefaultExcludeSequence);
        private string _filePatterns = DefaultPattern;
        private readonly List<string> _patterns = new(DefaultPatternSequence);

        /// <summary>
        /// Default constructor.
        /// </summary>
        public NodeProperties()
        {
            _filePatterns = DefaultPattern;
            _patterns.AddRange(DefaultPatternSequence);

            _excludeDirectories = DefaultExclude;
            _excludes.AddRange(DefaultExcludeSequence);
        }

        /// <summary>
        /// Gets or sets the maximum search depth.
        /// </summary>
        public int SearchDepth
        {
            get { return _searchDepth; }
            set { _searchDepth = Math.Max(value, 1); }
        }

        /// <summary>
        /// Gets or sets whether to show empty directories.
        /// </summary>
        public bool ShowEmptyDirectories { get; set; }

        /// <summary>
        /// Gets or sets a list of directory leaf names to exclude where values are separated with ';' character.
        /// Note that wild-cards are not supported. Case sensitivity is platform default. The initial value is "obj;ref".
        /// </summary>
        public string ExcludeDirectories
        {
            get { return _excludeDirectories; }

            set
            {
                value = value.Trim();

                if (_excludeDirectories != value)
                {
                    var temp = value.Split(';', SplitOptions);

                    _excludes.Clear();
                    _excludes.AddRange(temp);
                    _excludeDirectories = value;
                }

            }
        }

        /// <summary>
        /// Gets or sets a wildcard search pattern used to populate file items within directories. Case sensitivity
        /// is platform default, and the string may contain multiple pattern separated with ';' character.
        /// Example "*.axaml;*.dll". Setting an empty string is equivalent to setting "*". The initial value is "*".
        /// </summary>
        public string FilePatterns
        {
            get { return _filePatterns; }

            set
            {
                value = value.Trim();

                if (value.Length == 0)
                {
                    value = "*";
                }

                if (_filePatterns != value)
                {
                    var temp = value.Split(';', SplitOptions);

                    _patterns.Clear();
                    _patterns.AddRange(temp);
                    _filePatterns = value;
                }

            }
        }

        /// <summary>
        /// Assigns from other.
        /// </summary>
        public void AssignFrom(NodeProperties other)
        {
            SearchDepth = other.SearchDepth;
            ShowEmptyDirectories = other.ShowEmptyDirectories;
            FilePatterns = other.FilePatterns;
            ExcludeDirectories = other.ExcludeDirectories;
        }

        /// <summary>
        /// Gets the file pattern as a sequence. The contents are updated by setting <see cref="FilePatterns"/>
        /// </summary>
        public IEnumerable<string> GetFilePatternEnumerable()
        {
            return _patterns;
        }

        /// <summary>
        /// Returns true if the name is found in <see cref="ExcludeDirectories"/> with platform case sensitivity.
        /// </summary>
        public bool IsExcluded(string directoryName)
        {
            // List size is expected to be small
            foreach (var item in _excludes)
            {
                if (item.Equals(directoryName, PathItem.PlatformComparison))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Override.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(FilePatterns, SearchDepth, ShowEmptyDirectories, ExcludeDirectories);
        }

    }
}