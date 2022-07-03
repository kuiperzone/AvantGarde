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

using System;
using Xunit;
using Xunit.Abstractions;

namespace AvantGarde.Utility.Test
{
    public class ArgumentParserTest
    {
        private const string ValueString = "Dir\\Folder/File Name!";
        private const string TestString = "-a=True --size=100 --height=400 \"Dir\\Folder/File Name!\" --String=\"Hello world\" --double=3.142 --Param=Test-:-work --debug";
        private static readonly string[] TestArgs = { "-a=True", "--size=100", "--height=400", "\"Dir\\Folder/File Name!\"", "--String=\"Hello world\"", "--double=3.142", "--Param=Test-:-work", "--debug" };

        private readonly ITestOutputHelper Helper;

        public ArgumentParserTest(ITestOutputHelper helper)
        {
            Helper = helper;
        }

        [Fact]
        public void ConstructorArray_Strict_IndexerGivesExpected()
        {
            var ap = new ArgumentParser(TestArgs);

            Assert.Equal(ValueString, ap.Value);

            Assert.Equal("100", ap["size"]);
            Assert.Equal("400", ap["height"]);
            Assert.Equal("Hello world", ap["string"]);
            Assert.Equal("3.142", ap["Double"]);
            Assert.Equal("Test-:-work", ap["param"]);
            Assert.Equal("True", ap["debug"]);
        }

        [Fact]
        public void ConstructorString_Strict_IndexerGivesExpected()
        {
            var ap = new ArgumentParser("/main/dir/test/dll -c");

            Assert.Equal("/main/dir/test/dll", ap.Value);
            Assert.Equal("True", ap["c"]);
            Assert.Equal("/main/dir/test/dll -c", ap.ToString());

            ap = new ArgumentParser(TestString);
            Assert.Equal(ValueString, ap.Value);

            Assert.Equal("100", ap["size"]);
            Assert.Equal("400", ap["height"]);
            Assert.Equal("Hello world", ap["string"]);
            Assert.Equal("3.142", ap["Double"]);
            Assert.Equal("Test-:-work", ap["param"]);
            Assert.Equal("True", ap["debug"]);

            Helper.WriteLine(ap.ToString());
        }

        [Fact]
        public void ConstructorString_Strict_ThrowsWithRepeatedKey()
        {
            Assert.Throws<FormatException>(() =>
                new ArgumentParser("-size=100 -string=123 -Size=200 value"));

            Assert.Throws<FormatException>(() =>
                new ArgumentParser("-size=100 -Size value -string=123"));

            Assert.Throws<FormatException>(() =>
                new ArgumentParser("-size=100 -string=123 -Debug value -debug"));
        }

        [Fact]
        public void ConstructorString_Strict_ThrowsWithRepeatedValue()
        {
            Assert.Throws<FormatException>(() =>
                new ArgumentParser("-size=100 value1 -string=123 value2"));

            Assert.Throws<FormatException>(() =>
                new ArgumentParser("value1 value2 -size=100 "));

            Assert.Throws<FormatException>(() =>
                new ArgumentParser("-size=100 value1 value2"));
        }

        [Fact]
        public void ConstructorString_NotStrict_NoThrowWithRepeatedKey()
        {
            const string Str = "-Size=100 -string=123 -size=200 -size -size:300 value";
            var ap = new ArgumentParser(Str, false);
            Assert.Equal("100", ap["size"]);

            Helper.WriteLine(ap.ToString());
            Assert.Equal(Str, ap.ToString());
        }

        [Fact]
        public void ConstructorString_NotStrict_NoThrowWithRepeatedValue()
        {
            const string Str = "-Size=100 -string=123 value1 -size=200 value2";

            var ap = new ArgumentParser(Str, false);
            Assert.Equal("value1", ap.Value);

            Helper.WriteLine(ap.ToString());
            Assert.Equal(Str, ap.ToString());
        }

        [Fact]
        public void ConstructorString_SentenceWithApostropie()
        {
            var ap = new ArgumentParser("-input=\"Wayne's world's\"");
            Assert.Equal("Wayne's world's", ap["input"]);
        }

        [Fact]
        public void Indexer_IgnoresCase()
        {
            var ap = new ArgumentParser(TestArgs);
            Assert.Equal("100", ap["SIZE"]);
        }

        [Fact]
        public void Indexer_GivesNullIfNotExist()
        {
            var ap = new ArgumentParser(TestArgs);
            Assert.Null(ap["NotExist"]);
        }

        [Fact]
        public void Indexer_GivesValueIfKeyNull()
        {
            var ap = new ArgumentParser(TestArgs);
            Assert.Equal(ValueString, ap[null]);
        }

        [Fact]
        public void Get_Mandatory_Converts()
        {
            var ap = new ArgumentParser(TestArgs);

            Assert.Equal(ValueString, ap.GetOrThrow<string>(null));

            Assert.Equal(100, ap.GetOrThrow<int>("size"));
            Assert.Equal(400, ap.GetOrThrow<int>("height"));
            Assert.Equal(3.142, ap.GetOrThrow<double>("Double"));
            Assert.Equal("Hello world", ap.GetOrThrow<string>("string"));
            Assert.Equal("Test-:-work", ap.GetOrThrow<string>("param"));
            Assert.True(ap.GetOrThrow<bool>("debug"));
        }

        [Fact]
        public void GetOrThrow_ThrowsIfNotExist()
        {
            var ap = new ArgumentParser(TestArgs);

            Assert.Throws<ArgumentException>(() =>
                ap.GetOrThrow<string>("not-exist"));
        }

        [Fact]
        public void GetOrThrow_ThrowsIfNotConverted()
        {
            var ap = new ArgumentParser(TestArgs);

            Assert.Throws<FormatException>(() =>
                ap.GetOrThrow<int>("string"));
        }


        [Fact]
        public void GetOrDefault_Converts()
        {
            var ap = new ArgumentParser(TestArgs);

            Assert.Equal(ValueString, ap.GetOrDefault(null, "failed"));

            Assert.Equal(100, ap.GetOrDefault("size", -1));
            Assert.Equal(400, ap.GetOrDefault("height", -1));
            Assert.Equal(3.142, ap.GetOrDefault("Double", 45.2));
            Assert.Equal("Hello world", ap.GetOrDefault("string", "failed"));
            Assert.Equal("Test-:-work", ap.GetOrDefault("param", "failed"));
            Assert.True(ap.GetOrDefault<bool>("debug", false));
        }

        [Fact]
        public void GetOrDefault_GivesDefaultIfNotExist()
        {
            var ap = new ArgumentParser(TestArgs);
            Assert.Equal(45.2, ap.GetOrDefault("Double-not-exist", 45.2));
        }

        [Fact]
        public void GetOrDefault_Strict_ThrowsIfNotConverted()
        {
            var ap = new ArgumentParser(TestArgs, true);

            Assert.Throws<FormatException>(() =>
                ap.GetOrDefault("string", 3));
        }

        [Fact]
        public void GetOrDefault_NotStrict_GivesDefaultIfNotConverted()
        {
            var ap = new ArgumentParser(TestArgs, false);
            Assert.Equal(3, ap.GetOrDefault("string", 3));
        }

        [Fact]
        public void Clone_StripsValueAndKeys()
        {
            var ap1 = new ArgumentParser(TestArgs);
            Helper.WriteLine(ap1.ToString());

            var ap2 = ap1.Clone(true, "HEIGHT", "Param", "String");
            Helper.WriteLine(ap2.ToString());

            Assert.Equal("-a=True --size=100 --double=3.142 --debug", ap2.ToString());
        }

        [Fact]
        public void ToString_ReturnsExpected()
        {
            // We use this as regression with commands we may use
            var ap = new ArgumentParser(TestArgs);

            Helper.WriteLine(ap.ToString());
            Assert.Equal(TestString, ap.ToString());

            ap = new ArgumentParser("Tral.Process -id=MCP --flag0 false --flag1");
            Helper.WriteLine(ap.ToString());
            Helper.WriteLine(ap[null]);
            Helper.WriteLine(ap["id"]);
            Helper.WriteLine(ap["flag0"]);
            Helper.WriteLine(ap["flag1"]);
            Helper.WriteLine(ap.ToString());

            Assert.Equal("Tral.Process", ap[null]);
            Assert.Equal("MCP", ap["id"]);
            Assert.Equal("false", ap["flag0"]);
            Assert.Equal("True", ap["flag1"]);

            Assert.Equal("Tral.Process -id=MCP --flag0 false --flag1", ap.ToString());


            ap = new ArgumentParser("mongod --port 27018 --directoryperdb --dbpath /home/kuiper/Scratch/MONGODB");
            Helper.WriteLine(ap.ToString());
            Helper.WriteLine(ap[null]);
            Helper.WriteLine(ap["port"]);
            Helper.WriteLine(ap["directoryperdb"]);
            Helper.WriteLine(ap["dbpath"]);
            Helper.WriteLine(ap.ToString());

            Assert.Equal("mongod", ap[null]);
            Assert.Equal("27018", ap["port"]);
            Assert.Equal("True", ap["directoryperdb"]);
            Assert.Equal("/home/kuiper/Scratch/MONGODB", ap["dbpath"]);

            var temp = ap.Clone(true).ToString();
            Helper.WriteLine(temp);

            Assert.Equal("--port 27018 --directoryperdb --dbpath /home/kuiper/Scratch/MONGODB", temp);
        }

        [Fact]
        public void ShowGetKeys()
        {
            // Visual only
            var ap = new ArgumentParser(TestArgs);

            foreach (var k in ap.GetKeys())
            {
                Helper.WriteLine(k);
            }
        }

    }
}
