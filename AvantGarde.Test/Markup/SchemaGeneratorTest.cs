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

using Avalonia.Controls;
using AvantGarde.Test.Internal;
using Xunit;
using Xunit.Abstractions;

namespace AvantGarde.Markup.Test
{
    public class SchemaGeneratorTest : TestUtilBase
    {
        public SchemaGeneratorTest(ITestOutputHelper helper)
            : base(helper)
        {
        }

        [Fact]
        public void GetSchema()
        {
            var schema = SchemaGenerator.GetSchema(false, false);
            var schemaF = SchemaGenerator.GetSchema(true, false);
            var schemaA = SchemaGenerator.GetSchema(false, true);
            var schemaFA = SchemaGenerator.GetSchema(true, true);

            Assert.NotEqual(schema, schemaF);
            Assert.NotEqual(schema, schemaA);
            Assert.NotEqual(schema, schemaFA);
            Assert.NotEqual(schemaA, schemaF);
        }

        [Fact]
        public void SaveDocument()
        {
            SchemaGenerator.SaveDocument("Avalonia.xsd", false);
            SchemaGenerator.SaveDocument("Avalonia.Formatted.xsd", true);
        }

        [Fact]
        public void ScratchText()
        {
            var temp = new Control();
            WriteLine(temp.Width);
            WriteLine(temp.MinWidth);
            WriteLine(temp.MaxWidth);
        }

    }
}