// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AvantGarde.Projects
{
    /// <summary>
    /// Class which provides path information for an item which can be either a file or directory.
    /// It is a wrapper for FileSystemInfo, but provides indication of change.
    /// </summary>
    public class PathItem
    {
        private readonly FileSystemInfo _info;
        private DateTime _lastUtc;
        private int _hashCode;
        private int _hashBase;

        static PathItem()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                PlatformComparison = StringComparison.InvariantCulture;
            }
            else
            {
                PlatformComparison = StringComparison.Ordinal;
            }
        }

        /// <summary>
        /// Constructor with path string and optional <see cref="PathKind"/> value.
        /// </summary>
        /// <exception cref="ArgumentException">Path is empty"</exception>
        public PathItem(string path, PathKind kind)
        {
            path = CleanPath(path);

            if (kind == PathKind.Directory)
            {
                var dir = new DirectoryInfo(path);
                _info = dir;
                IsDirectory = true;
                Kind = PathKind.Directory;
                ParentDirectory = CleanPath(dir.Parent?.FullName ?? string.Empty);
                Extension = string.Empty;
            }
            else
            {
                var file = new FileInfo(path);
                _info = file;
                ParentDirectory = CleanPath(file.Directory?.FullName ?? string.Empty);
                Extension = file.Extension.ToLowerInvariant();
                Kind = kind == PathKind.AnyFile ? GetFileKind(Extension) : kind;
            }

            Name = _info.Name;
            FullName = _info.FullName;
            _hashBase = HashCode.Combine(GetType(), FullName);
            _hashCode = RefreshInternal(_hashBase);
        }

        /// <summary>
        /// Constructor. This instance will share the info object.
        /// </summary>
        public PathItem(FileSystemInfo info)
        {
            Name = info.Name;
            FullName = CleanPath(info.FullName);

            if (info is FileInfo file)
            {
                ParentDirectory = CleanPath(file.Directory?.FullName ?? string.Empty);
                Extension = file.Extension.ToLowerInvariant();
                Kind = GetFileKind(Extension);
            }
            else
            {
                IsDirectory = true;
                Kind = PathKind.Directory;
                ParentDirectory = CleanPath(((DirectoryInfo)info).Parent?.FullName ?? string.Empty);
                Extension = string.Empty;
            }

            _info = info;
            _hashBase = HashCode.Combine(GetType(), FullName);
            _hashCode = RefreshInternal(_hashBase);
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        /// <exception cref="ArgumentException">Path is empty"</exception>
        public PathItem(PathItem other)
        {
            Kind = other.Kind;
            IsDirectory = Kind == PathKind.Directory;
            Name = other.Name;
            FullName = other.FullName;
            Extension = other.Extension;
            ParentDirectory = other.ParentDirectory;
            Exists = other.Exists;
            Length = other.Length;

            _info ??= IsDirectory ? new DirectoryInfo(FullName) : new FileInfo(FullName);
            _lastUtc = other._lastUtc;
            _hashBase = HashCode.Combine(GetType(), FullName);
            _hashCode = other._hashCode;
        }

        /// <summary>
        /// Platform case sensitivity.
        /// </summary>
        public static readonly StringComparison PlatformComparison;

        /// <summary>
        /// Gets the path kind.
        /// </summary>
        public readonly PathKind Kind;

        /// <summary>
        /// Gets whether <see cref="PathKind.Kind"/> specifies <see cref="PathKind.Directory"/>.
        /// </summary>
        public readonly bool IsDirectory;

        /// <summary>
        /// Gets the leaf name part.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Gets the fully qualified path.
        /// </summary>
        public readonly string FullName;

        /// <summary>
        /// Gets the extension including the dot, i.e. ".csproj". The value is always lowercase. It is empty for
        /// directories.
        /// </summary>
        public readonly string Extension;

        /// <summary>
        /// Gets the fully qualified directory part. The string is not terminated with a separator and, for an item
        /// in the root directory, the value will be empty.
        /// </summary>
        public readonly string ParentDirectory;

        /// <summary>
        /// Gets whether the path exists. The value is updated by <see cref="Refresh"/>.
        /// </summary>
        public bool Exists { get; private set; }

        /// <summary>
        /// Gets the file length in bytes. The value is updated by <see cref="Refresh"/>.
        /// For a directory it is always 0. It is also 0 if <see cref="Exists"/> if false.
        /// </summary>
        public long Length { get; private set; }

        /// <summary>
        /// Gets the last write time of a file or the creation time of a directory expressed in UTC. The
        /// value is default DateTime where <see cref="Exists"/> is false. The value is updated by
        /// <see cref="Refresh"/>. It can be overridden.
        /// </summary>
        public virtual DateTime LastUtc
        {
            get { return _lastUtc; }
        }

        /// <summary>
        /// Gets or sets a tag value. The value is not copied by the copy constructor and does not form part
        /// of any refresh operation.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Normalizes path separators and trims spaces and any trailing separator characters.
        /// </summary>
        public static string CleanPath(string path)
        {
            if (Path.PathSeparator == '\\')
            {
                return path.Trim().Replace('/', '\\');
            }

            return path.Trim().Replace('\\', '/');
        }

        /// <summary>
        /// If name shares the same directory as this item, the result is the path trimmed left of the directory.
        /// Otherwise, the result is cleaned name only. If name is null or empty, the result is null.
        /// I.e. "/ProjectDir/Views/View.axaml" to "Views/View.axaml".
        /// </summary>
        public string? MakeLocalName(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                name = CleanPath(name);
                var local = IsDirectory ? FullName : ParentDirectory;

                if (!local.EndsWith(Path.DirectorySeparatorChar))
                {
                    local += Path.DirectorySeparatorChar;
                }

                if (name.StartsWith(local, PlatformComparison))
                {
                    return name.Substring(local.Length);
                }

                return name;
            }

            return null;
        }

        /// <summary>
        /// If the name is not rooted, the result is the combined with the directory of this item. If the name
        /// is rooted, the result is the cleaned name only. If name is null or empty, the result is null.
        /// I.e. "Views/View.axaml" to "/ProjectDir/Views\View.axaml".
        /// </summary>
        public string? MakeFullName(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                name = CleanPath(name);
                var local = IsDirectory ? FullName : ParentDirectory;

                if (Path.IsPathRooted(name))
                {
                    return name;
                }

                return Path.Combine(local, name);
            }

            return null;
        }

        /// <summary>
        /// Refreshes <see cref="Exists"/> and <see cref="LastUtc"/>. It returns true if changed.
        /// It can be overidden.
        /// </summary>
        public virtual bool Refresh()
        {
            var code = RefreshInternal(_hashBase);
            bool changed = code != _hashCode;
            _hashCode = code;
            return changed;
        }

        /// <summary>
        /// Throws exception is <cref="Exists"/> is false.
        /// </summary>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        public void AssertExists()
        {
            if (!Exists)
            {
                if (IsDirectory)
                {
                    throw new DirectoryNotFoundException("Not found " + FullName);
                }

                throw new FileNotFoundException("Not found " + FullName);
            }
        }

        /// <summary>
        /// Reads the file as text and returns the contents. If the file does not exist or is not a known text file,
        /// the result is an empty string.
        /// </summary>
        /// <exception cref="IOException">Not found or, Encoding not detected or supported</exception>
        /// <exception cref="InvalidOperationException">Path is not a file or, File too large</exception>
        public string ReadAsText(int maxLength = 10 * 1024 * 1024)
        {
            using var stream = GetFileInfo().OpenRead();

            if (maxLength <= 0 || stream.Length == 0)
            {
                return string.Empty;
            }

            if (stream.Length > maxLength)
            {
                throw new InvalidOperationException("File too large " + Name);
            }

            // This should get vast majority including UTF8
            var builder = new StringBuilder((int)stream.Length);
            var encoding = GetFileBom(stream);
            var content = TryEncoding(stream, encoding, builder);

            if (content != null)
            {
                return content;
            }

            // Supported on .NET Core
            // ISO-8859-1/1252 is 6.7% used encoding.
            encoding = Encoding.Latin1;
            content = TryEncoding(stream, encoding, builder);

            if (content != null)
            {
                return content;
            }

            // UTF-16 LE
            encoding = Encoding.Unicode;
            content = TryEncoding(stream, encoding, builder);

            if (content != null)
            {
                return content;
            }

            throw new IOException("Encoding not detected or supported " + Name);
        }

        /// <summary>
        /// Opens and returns a read-only stream.
        /// </summary>
        /// <exception cref="IOException">Not found</exception>
        /// <exception cref="InvalidOperationException">Path is not a file</exception>
        public Stream ReadAsStream()
        {
            return GetFileInfo().OpenRead();
        }

        /// <summary>
        /// Gets a hash code which changes each time the file meta changes. It is intended that it be
        /// extended by subclasses.
        /// </summary>
        public override int GetHashCode()
        {
            return _hashCode;
        }

        /// <summary>
        /// If verbose is true, it returns <see cref="Name"/>. If verbose is false, returns ToString().
        /// Maybe overridden.
        /// </summary>
        public virtual string ToString(bool verbose)
        {
            return verbose ? FullName : ToString();
        }

        /// <summary>
        /// Overrides and returns <see cref="Name"/>.
        /// </summary>
        public override string ToString()
        {
            return FullName;
        }

        /// <summary>
        /// Determines file kind from extension, i.e. ".sln". Input to be lowercase and include the dot.
        /// </summary>
        protected static PathKind GetFileKind(string ext)
        {
            switch (ext)
            {
                case ".sln":
                case ".csproj":
                    return PathKind.Solution;

                case ".cs":
                    return PathKind.CSharp;

                case ".xaml":
                case ".axaml":
                case ".paml":
                    return PathKind.Xaml;

                case ".xsd":
                case ".xml":
                case ".xhtm":
                case ".xhtml":
                    return PathKind.Xml;

                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".ico":
                case ".gif":
                    return PathKind.Image;

                case ".txt":
                case ".text":
                case ".cpp":
                case ".h":
                case ".hpp":
                case ".mak":
                case ".md":
                case ".json":
                case ".css":
                case ".ini":
                case ".conf":
                case ".config":
                case ".log":
                case ".htm":
                case ".html":
                case ".bat":
                case ".sh":
                case ".targets":
                case ".props":
                case ".gitignore":
                    return PathKind.Document;

                case ".dll":
                    return PathKind.Assembly;

                default:
                    return PathKind.OtherFile;
            }
        }

        /// <summary>
        /// Assert extension.
        /// </summary>
        /// <exception cref="ArgumentException">Path must be a {ext} file</exception>
        protected void AssertExtension(string ext)
        {
            if (Extension != ext)
            {
                throw new ArgumentException($"Path must be a {ext} file");
            }
        }

        /// <summary>
        /// Assert path kind.
        /// </summary>
        /// <exception cref="ArgumentException">Path must be a {kind} file</exception>
        protected void AssertKind(PathKind kind)
        {
            if (Kind != kind)
            {
                throw new ArgumentException($"Path must be a {kind} file");
            }
        }

        /// <summary>
        /// Get underlying FileInfo.
        /// </summary>
        internal DirectoryInfo GetDirectoryInfo()
        {
            if (IsDirectory)
            {
                return (DirectoryInfo)_info;
            }

            throw new InvalidOperationException("Path is not a directory");
        }

        /// <summary>
        /// Get underlying FileInfo.
        /// </summary>
        internal FileInfo GetFileInfo()
        {
            if (!IsDirectory)
            {
                return (FileInfo)_info;
            }

            throw new InvalidOperationException("Path is not a file");
        }

        private static Encoding GetFileBom(Stream stream)
        {
            // Read the BOM
            // https://stackoverflow.com/questions/3825390/effective-way-to-find-any-files-encoding
            if (stream.Length > 3)
            {
                stream.Seek(0, SeekOrigin.Begin);

                var bom = new byte[4];
                stream.Read(bom, 0, 4);

                // Analyze the BOM
                if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
                if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; // UTF-16LE
                if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; // UTF-16BE
                if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            }

            // Default
            return Encoding.UTF8;
        }

        private static string? TryEncoding(Stream stream, Encoding encoding, StringBuilder builder)
        {
            // https://stackoverflow.com/questions/90838/how-can-i-detect-the-encoding-codepage-of-a-text-file
            stream.Seek(0, SeekOrigin.Begin);
            var verifier = Encoding.GetEncoding(encoding.CodePage, new EncoderExceptionFallback(), new DecoderExceptionFallback());

            try
            {
                using var reader = new StreamReader(stream, verifier, false, 1024, true);

                while (true)
                {
                    var line = reader.ReadLine();

                    if (line == null)
                    {
                        return builder.ToString();
                    }

                    builder.AppendLine(line);
                }
            }
            catch (DecoderFallbackException)
            {
                builder.Clear();
                return null;
            }
        }

        private int RefreshInternal(int hashBase)
        {
            _info.Refresh();

            if (_info.Exists)
            {
                Exists = true;

                // Time seem to fluctuate and even drift backwards by ~0.1 second (.NET5).
                var now = IsDirectory ? _info.CreationTimeUtc : _info.LastWriteTimeUtc;

                if (now > _lastUtc || _lastUtc.Ticks - now.Ticks > TimeSpan.TicksPerSecond * 5)
                {
                    _lastUtc = now;
                }

                Length = !IsDirectory ? ((FileInfo)_info).Length : 0;
            }
            else
            {
                Exists = false;
                _lastUtc = default;
                Length = 0;
            }

            return HashCode.Combine(hashBase, _lastUtc, Length);
        }

    }
}