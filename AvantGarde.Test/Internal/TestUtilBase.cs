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
using System.Text;
using Xunit.Abstractions;

namespace AvantGarde.Test.Internal;

/// <summary>
/// A base class for use with Xunit test classes which provides common utility methods and a
/// test output helper. Test classes should derive as needed.
/// </summary>
public class TestUtilBase : IDisposable
{
    private static readonly object s_syncObj = new object();
    private static readonly Random s_rand = new Random(unchecked((int)DateTime.UtcNow.Ticks));
    private static readonly TextWriterTraceListener s_trace = new(System.Console.Out, nameof(TestUtilBase));
    private string? _scratch = null;
    private ITestOutputHelper? _helper = null;
    private bool _consoleTitle;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public TestUtilBase()
    {
#if DEBUG
        ConsoleOutput = true;

        if (Trace.Listeners.IndexOf(s_trace) < 0)
        {
            Trace.Listeners.Add(s_trace);
        }
#endif
    }

    /// <summary>
    /// Constructor with specified xunit helper.
    /// </summary>
    public TestUtilBase(ITestOutputHelper helper)
        : this()
    {
        _helper = helper;
    }

    /// <summary>
    /// Calls <see cref="Dispose(bool)"/>.
    /// </summary>
    ~TestUtilBase()
    {
        Dispose(false);
    }

    /// <summary>
    /// Gets whether to <see cref="WriteLine"/> outputs to Console. The default is true for DEBUG and
    /// false otherwise.
    /// </summary>
    public bool ConsoleOutput { get; } = false;

    /// <summary>
    /// Returns a scratch directory unique to the test under the user's temporary directory. The
    /// directory is created on the first call and will initially be empty. The result will
    /// have a trailing separator.
    /// </summary>
    public string Scratch
    {
        get
        {
            _scratch ??= CreateScratch();
            return _scratch;
        }
    }

    /// <summary>
    /// Gets or sets whether to delete the <see cref="Scratch"/> directory on test termination.
    /// The default is true.
    /// </summary>
    public bool AutoRemoveScratch { get; set; } = true;

    /// <summary>
    /// Generates a random string or name using only uppercase ASCII letters.
    /// </summary>
    public static string GetRandomName(int length = 10)
    {
        const string SampleChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        var sb = new StringBuilder(length);

        lock (s_syncObj)
        {
            for (int n = 0; n < length; ++n)
            {
                sb.Append(SampleChars[s_rand.Next(SampleChars.Length)]);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a new randomly named scratch directory under the <see cref="Scratch"/>
    /// directory. The result will have a trailing separator.
    /// </summary>
    public string CreateNewScratch()
    {
        string path = Scratch + GetRandomName();
        Directory.CreateDirectory(path);
        return path + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Creates randomly named file under <see cref="Scratch"/> directory containing the text
    /// content supplied. Returns the path of the file created. Equivalent to:
    /// CreateFileContent("", content)
    /// </summary>
    public string CreateFileContent(string content, Encoding? enc = null)
    {
        return CreateFileContent(string.Empty, content, enc);
    }

    /// <summary>
    /// Creates randomly named file under <see cref="Scratch"/> directory containing the text
    /// content supplied. Returns the path of the file created. Equivalent to:
    /// CreateFileContent(null, lines)
    /// </summary>
    public string CreateFileContent(IEnumerable<string> lines, Encoding? enc = null)
    {
        return CreateFileContent(string.Empty, lines, enc);
    }

    /// <summary>
    /// Creates a text file with the given text content using default UTF8 encoding, overwriting
    /// any existing file. If the filename path is not rooted, the file will be created under
    /// <see cref="Scratch"/> directory. If filename is null or empty, a random one is
    /// generated. The full path is returned.
    /// </summary>
    public string CreateFileContent(string filename, string content, Encoding? enc = null)
    {
        enc ??= Encoding.UTF8;

        if (string.IsNullOrEmpty(filename))
        {
            filename = GetRandomName();
        }

        if (!Path.IsPathRooted(filename))
        {
            filename = Scratch + filename;
        }

        using (var writer = new StreamWriter(File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.Read)))
        {
            writer.Write(content);
        }

        return filename;
    }

    /// <summary>
    /// Creates a text file with the given text lines using default UTF8 encoding. Each line
    /// will be terminated with the Environment.NewLine value. Otherwise equivalent to:
    /// CreateFileContent(path, content).
    /// </summary>
    public string CreateFileContent(string filename, IEnumerable<string> lines, Encoding? enc = null)
    {
        string content = string.Empty;

        if (lines != null)
        {
            foreach (var line in lines)
            {
                if (line != null)
                {
                    content += line.TrimEnd('\r', '\n') + Environment.NewLine;
                }
            }
        }

        return CreateFileContent(filename, content, enc);
    }

    /// <summary>
    /// Simple sleep.
    /// </summary>
    public void Sleep(int ms)
    {
        Thread.Sleep(ms);
    }

    /// <summary>
    /// Writes a new line to the output.
    /// </summary>
    public void WriteLine()
    {
        _helper?.WriteLine(string.Empty);

        if (ConsoleOutput)
        {
            WriteTitle();
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Formats a line of text and writes it to the output.
    /// </summary>
    public void WriteLine(object? msg, params object[] args)
    {
        msg ??= string.Empty;
        var s = msg.ToString();

        if (args.Length != 0)
        {
            _helper?.WriteLine(s, args);
        }
        else
        {
            _helper?.WriteLine(s);
        }

        if (ConsoleOutput)
        {
            WriteTitle();

            if (args.Length != 0)
            {
                Console.WriteLine(s ?? string.Empty, args);
            }
            else
            {
                Console.WriteLine(s ?? string.Empty);
            }
        }
    }

    private void WriteTitle()
    {
        if (!_consoleTitle)
        {
            _consoleTitle = true;
            Console.WriteLine();
            Console.WriteLine();

            Console.Write("TEST: ");
            Console.WriteLine(GetType().Name);
        }
    }

    /// <summary>
    /// Calls protected <see cref="Dispose(bool)"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The base implementation deletes the <see cref="Scratch"/> directory, and overriding
    /// methods should, therefore, call the base implementation.
    /// </summary>
    protected virtual void Dispose(bool _)
    {
        if (_scratch != null && AutoRemoveScratch)
        {
            try
            {
               Directory.Delete(_scratch, true);
            }
            catch
            {
            }
        }

        _scratch = null;
    }

    private string CreateScratch()
    {
        var path = Path.GetTempPath() + GetType().Name + "-" + GetRandomName() + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(path);
        return path;
    }

}
