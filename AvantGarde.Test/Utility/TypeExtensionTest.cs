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

namespace AvantGarde.Utility.Test;

public class TypeExtensionTest(ITestOutputHelper helper) : TestUtilBase(helper)
{
    [Fact]
    public void GetFriendlyType_TupleInt()
    {
        var temp = typeof(Tuple<int, string>);
        var name = temp.GetFriendlyName();

        WriteLine(name);
        Assert.Equal("Tuple<int, string>", name);
    }

    [Fact]
    public void GetFriendlyType_GenericEvent()
    {
        var temp = typeof(TextBlock).GetEvent("PointerMoved");
        var name = temp?.EventHandlerType.GetFriendlyName();

        WriteLine(name);
        Assert.Equal("EventHandler<PointerEventArgs>", name);
    }
}