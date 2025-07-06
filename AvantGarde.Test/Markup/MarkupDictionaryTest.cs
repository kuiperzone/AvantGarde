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

using Avalonia.Controls;
using AvantGarde.Test.Internal;
using Xunit;
using Xunit.Abstractions;

namespace AvantGarde.Markup.Test;

public class MarkupDictionaryTest(ITestOutputHelper helper) : TestUtilBase(helper)
{
    [Fact]
    public void Types_NotEmpty()
    {
        Assert.NotEmpty(MarkupDictionary.Types);
        WriteLine(string.Join(", ", MarkupDictionary.Types.Keys));
    }

    [Fact]
    public void GetMarkupInfo_TextBlock()
    {
        var info = AssertControlInfo(typeof(TextBlock));
        WriteLine(info.GetHelpDocument());
        WriteLine();

        WriteLine(info.Attributes["DoubleTapped"].GetHelpDocument());
        WriteLine();

        WriteLine(info.Attributes["Height"].GetHelpDocument());
        WriteLine();

        var a = info.Attributes["Text"];
        Assert.NotEmpty(a.GetHelpDocument());
        WriteLine(a.GetHelpDocument());
        WriteLine();

        a = info.Attributes["FontSize"];
        Assert.NotEmpty(a.GetHelpDocument());
        WriteLine(a.GetHelpDocument());
        WriteLine();

        a = info.Attributes["MinWidth"];
        Assert.NotEmpty(a.GetHelpDocument());
        WriteLine(a.GetHelpDocument());
        WriteLine();

        a = info.Attributes["Cursor"];
        Assert.NotEmpty(a.GetHelpDocument());
        WriteLine(a.GetHelpDocument());
        WriteLine();

        a = info.Attributes["VerticalAlignment"];
        Assert.NotEmpty(a.GetHelpDocument());
        WriteLine(a.GetHelpDocument());
        WriteLine();
    }

    [Fact]
    public void GetMarkupInfo_Grid()
    {
        var info = AssertControlInfo(typeof(Grid));
        WriteLine(info.GetHelpDocument());
        WriteLine();
    }

    [Fact]
    public void GetMarkupInfo_Window()
    {
        var info = AssertControlInfo(typeof(Window));
        WriteLine(info.GetHelpDocument());
        WriteLine();
    }

    private static MarkupInfo AssertControlInfo(Type type)
    {
        var name = type.Name;
        var info = MarkupDictionary.GetMarkupInfo(name) ??
            throw new ArgumentNullException(name);

        Assert.Equal(name, info.Name);
        Assert.Equal(type, info.ClassType);
        Assert.True(info.Attributes.Count > 0);
        Assert.NotEmpty(info.GetHelpDocument());

        Assert.False(info.Attributes["Height"].IsEvent);
        Assert.NotEmpty(info.Attributes["Height"].GetHelpDocument());

        Assert.True(info.Attributes["DoubleTapped"].IsEvent);
        Assert.NotEmpty(info.Attributes["DoubleTapped"].GetHelpDocument());

        return info;
    }

}