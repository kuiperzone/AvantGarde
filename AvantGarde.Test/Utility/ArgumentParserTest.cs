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

using Xunit;

namespace AvantGarde.Utility.Test;

public class ArgumentParserTest
{
    [Fact]
    public void Constructor_LinuxStyle_IndexerGivesExpected()
    {
        // All systems
        var str = "FloatValue1 FloatValue2 -a A -b=B -c:C -xyz --Hello \"Hello World\" -u";
        var ap = new ArgumentParser(str);

        Assert.Equal("FloatValue1", ap.Values[0]);
        Assert.Equal("FloatValue2", ap.Values[1]);

        Assert.Equal("A", ap["a"]);
        Assert.Equal("B", ap["b"]);
        Assert.Equal("C", ap["c"]);

        Assert.Equal("true", ap["x"]);
        Assert.Equal("true", ap["y"]);
        Assert.Equal("true", ap["z"]);

        Assert.Equal("Hello World", ap["Hello"]);
        Assert.Equal("true", ap["u"]);

        Assert.Null(ap["None"]);

        // Verbatim string
        Assert.Equal(str, ap.ToString());
    }

    [Fact]
    public void Constructor_String_WindowsStyle_IndexerGivesExpected()
    {
        // Windows style
        if (ArgumentParser.AcceptWinStyle)
        {
            var str = "FloatValue1 FloatValue2 /a A /b=B /c:C /xyz /Hello:\"Hello World\" /u";
            var ap = new ArgumentParser(str);

            Assert.Equal("FloatValue1", ap.Values[0]);
            Assert.Equal("FloatValue2", ap.Values[1]);

            Assert.Equal("A", ap["a"]);
            Assert.Equal("B", ap["b"]);
            Assert.Equal("C", ap["c"]);

            Assert.Equal("true", ap["xyz"]);

            Assert.Equal("Hello World", ap["Hello"]);
            Assert.Equal("true", ap["u"]);

            Assert.Null(ap["None"]);
        }
    }

    [Fact]
    public void Constructor_Array_IndexerGivesExpected()
    {
        // Explicit Linux style
        var arr = new string[] { "FloatValue1", "FloatValue2", "-a", "A", "-b=B", "-c:C", "-xyz", "--Hello", "\"Hello World\"", "-u" };
        var ap = new ArgumentParser(arr);

        Assert.Equal("FloatValue1", ap.Values[0]);
        Assert.Equal("FloatValue2", ap.Values[1]);

        Assert.Equal("A", ap["a"]);
        Assert.Equal("B", ap["b"]);
        Assert.Equal("C", ap["c"]);

        Assert.Equal("true", ap["x"]);
        Assert.Equal("true", ap["y"]);
        Assert.Equal("true", ap["z"]);

        Assert.Equal("Hello World", ap["Hello"]);

        Assert.Equal("true", ap["u"]);

        Assert.Null(ap["None"]);
    }

    [Fact]
    public void Constructor_MultipleSeparators_IndexerGivesExpected()
    {
        // Known use case - where ' ', '=' and ':' - finds first.
        var str = "-p DefineConstants=FLAG";
        var ap = new ArgumentParser(str);
        Assert.Equal("DefineConstants=FLAG", ap["p"]);

        str = "-p:DefineConstants=FLAG";
        ap = new ArgumentParser(str);
        Assert.Equal("DefineConstants=FLAG", ap["p"]);

        str = "-p=DefineConstants=FLAG";
        ap = new ArgumentParser(str);
        Assert.Equal("DefineConstants=FLAG", ap["p"]);

        str = "-p : DefineConstants=FLAG";
        ap = new ArgumentParser(str);
        Assert.Equal("DefineConstants=FLAG", ap["p"]);

        str = "-p = DefineConstants=FLAG";
        ap = new ArgumentParser(str);
        Assert.Equal("DefineConstants=FLAG", ap["p"]);
    }

    [Fact]
    public void Constructor_ThrowsWithRepeatedKey()
    {
        var str = "FloatValue -a=A -b=B -a=C";
        Assert.Throws<ArgumentException>(() => new ArgumentParser(str));

        str = "FloatValue -a=A -b=B -a";
        Assert.Throws<ArgumentException>(() => new ArgumentParser(str));

        str = "FloatValue -a=A -a -b=B";
        Assert.Throws<ArgumentException>(() => new ArgumentParser(str));
    }

    [Fact]
    public void Constructor_ThrowsWithErroneousValues()
    {
        var str = "FloatValue -a=A Value2 -b=B";
        Assert.Throws<ArgumentException>(() => new ArgumentParser(str));

        str = "FloatValue Value2 -a=A -b=B Value2";
        Assert.Throws<ArgumentException>(() => new ArgumentParser(str));
    }

    [Fact]
    public void Constructor_String_SentenceWithApostropieOK()
    {
        var ap = new ArgumentParser("--input=\"Wayne's world's\"");
        Assert.Equal("Wayne's world's", ap["input"]);
    }

    [Fact]
    public void GetOrThrow_ThrowsIfNotExist()
    {
        var str = "FloatValue -a=A -b=B";
        var ap = new ArgumentParser(str);

        // OK
        Assert.Equal("A", ap.GetOrThrow("a"));
        Assert.Equal("A", ap.GetOrThrow('a', "Alpha"));

        // Not OK
        Assert.Throws<ArgumentException>(() => ap.GetOrThrow("K"));
        Assert.Throws<ArgumentException>(() => ap.GetOrThrow('K', "kay"));
    }

    [Fact]
    public void GetOrThrow_Generic_ConvertsValue()
    {
        var str = "FloatValue --double=3.142 --enum Value2 --int=-3 --bool1 False --bool2 Yes --bool3 NO";
        var ap = new ArgumentParser(str);

        // OK
        Assert.Equal(3.142, ap.GetOrThrow<double>("double"));
        Assert.Equal(TestType.Value2, ap.GetOrThrow<TestType>("enum"));

        // Must use equal as separator for minus (above)
        Assert.Equal(-3, ap.GetOrThrow<int>("int"));

        Assert.False(ap.GetOrThrow<bool>("bool1"));
        Assert.True(ap.GetOrThrow<bool>("bool2"));
        Assert.False(ap.GetOrThrow<bool>("bool3"));

        // Overload - two keys
        Assert.False(ap.GetOrThrow<bool>('b', "bool1"));
        Assert.True(ap.GetOrThrow<bool>('b', "bool2"));
        Assert.False(ap.GetOrThrow<bool>('b', "bool3"));
    }

    [Fact]
    public void GetOrThrow_Generic_ThrowsOnInvalid()
    {
        var str = "FloatValue --double=3.142 --enum Value2 --int=-3 --bool1 False --bool2 Yes --bool3 NO";
        var ap = new ArgumentParser(str);

        Assert.Throws<FormatException>(() => ap.GetOrThrow<double>("enum"));
        Assert.Throws<FormatException>(() => ap.GetOrThrow<TestType>("double"));
        Assert.Throws<FormatException>(() => ap.GetOrThrow<bool>("int"));
    }

    [Fact]
    public void GetOrDefault_GivesDefaultIfNotExist()
    {
        var str = "FloatValue -a=A -b=B";
        var ap = new ArgumentParser(str);

        // OK
        Assert.Equal("A", ap.GetOrDefault("a", "X"));
        Assert.Equal("A", ap.GetOrDefault('a', "Alpha", "X"));

        // Defaults
        Assert.Equal("X", ap.GetOrDefault("K", "X"));
        Assert.Equal("X", ap.GetOrDefault('K', "Kay", "X"));
    }

    [Fact]
    public void GetOrDefault_Generic_ConvertsValue()
    {
        var str = "FloatValue --double=3.142 --enum Value2 --int=-3 --bool1 False --bool2 Yes --bool3 NO";
        var ap = new ArgumentParser(str);

        // OK
        Assert.Equal(3.142, ap.GetOrDefault("double", 99.8));
        Assert.Equal(TestType.Value2, ap.GetOrDefault("enum", TestType.None));

        // Must use equal as separator for minus (above)
        Assert.Equal(-3, ap.GetOrDefault("int", 88));

        Assert.False(ap.GetOrDefault("bool1", true));
        Assert.True(ap.GetOrDefault("bool2", false));
        Assert.False(ap.GetOrDefault("bool3", true));

        // Overload - two keys
        Assert.False(ap.GetOrDefault('b', "bool1", true));
        Assert.True(ap.GetOrDefault('b', "bool2", false));
        Assert.False(ap.GetOrDefault('b', "bool3", true));

    }

    [Fact]
    public void GetOrDefault_Generic_ThrowsOnInvalid()
    {
        var str = "FloatValue --double=3.142 --enum Value2 --int=-3 --bool1 False --bool2 Yes --bool3 NO";
        var ap = new ArgumentParser(str);

        Assert.Throws<FormatException>(() => ap.GetOrDefault("enum", 44.3));
        Assert.Throws<FormatException>(() => ap.GetOrDefault("double", TestType.None));
        Assert.Throws<FormatException>(() => ap.GetOrDefault("int", false));
    }

    [Fact]
    public void Clone_StripsValueAndKeys()
    {
        var str = "FloatValue -a A -b=B -c:C -xyz --Hello \"Hello World\" -u";
        var ap1 = new ArgumentParser(str);

        var ap2 = ap1.Clone(true, "a", "y", "c");

        Assert.Empty(ap2.Values);
        Assert.Null(ap2["a"]);
        Assert.NotNull(ap2["b"]);
        Assert.Equal("-b=B -x=true -z=true --Hello=\"Hello World\" -u=true", ap2.ToString());
    }

    [Fact]
    public void Clone_ClonesVerbatim()
    {
        var str = "FloatValue -a A -b=B -c:C -xyz --Hello \"Hello World\" -u";
        var ap1 = new ArgumentParser(str);

        var ap2 = ap1.Clone();

        Assert.Equal("FloatValue", ap2.Values[0]);
        Assert.Equal(str, ap2.ToString());
    }

    [Fact]
    public void ToString_ReturnsExpected()
    {
        // Verbatim string
        var str = "FloatValue -a A -b=B -c:C -xyz --Hello \"Hello World\" -u";
        var ap = new ArgumentParser(str);
        Assert.Equal(str, ap.ToString());

        // String will be built - not verbatim
        var arr = new string[] { "FloatValue", "-a", "A", "-b=B", "-c:C", "-xyz", "--Hello", "\"Hello World\"", "-u" };
        var exp = "FloatValue -a=A -b=B -c=C -x=true -y=true -z=true --Hello=\"Hello World\" -u=true";
        ap = new ArgumentParser(arr);
        Assert.Equal(exp, ap.ToString());
    }

    [Fact]
    public void RegressionTest()
    {
        // We use this as regression with commands we may use
        var ap = new ArgumentParser("mongod --port 27018 --directoryperdb --dbpath /home/kuiper/Scratch/MONGODB");
        Assert.Equal("mongod", ap.Values[0]);
        Assert.Equal("27018", ap["port"]);
        Assert.Equal("true", ap["directoryperdb"]);
        Assert.Equal("/home/kuiper/Scratch/MONGODB", ap["dbpath"]);

        Assert.Equal("--port=27018 --directoryperdb=true --dbpath=/home/kuiper/Scratch/MONGODB", ap.Clone(true).ToString());
    }

    private enum TestType
    {
        None,
        Value1,
        Value2,
    }
}
