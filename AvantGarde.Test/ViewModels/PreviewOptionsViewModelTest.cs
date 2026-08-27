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

using Avalonia;
using AvantGarde.Test.Internal;
using AvantGarde.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace AvantGarde.ViewModels.Test;

/// <summary>
/// Covers the fit-to-window arithmetic.
/// </summary>
/// <remarks>
/// Static only. The instance members - SetFitScaleFactor's deadband and DecScale stopping above the
/// fit entry - are equally worth covering, but PreviewOptionsViewModel cannot be constructed here:
/// AvantViewModel's constructor reaches GlobalModel.Global, whose static initialiser resolves
/// IAssetLoader and throws without a running Avalonia application. Bootstrapping a headless app for
/// the test project is Milestone 5's job, not this milestone's.
/// </remarks>
public class PreviewOptionsViewModelTest(ITestOutputHelper helper) : TestUtilBase(helper)
{
    [Fact]
    public void CalcFitScaleFactor_HeightIsBinding()
    {
        // 800 x 450 into 1600 x 450 - width would allow 2.0, height allows only 1.0.
        var factor = PreviewOptionsViewModel.CalcFitScaleFactor(new Size(800, 450), new Size(1600, 450));

        WriteLine(factor.ToString());
        Assert.Equal(1.0, factor);
    }

    [Fact]
    public void CalcFitScaleFactor_WidthIsBinding()
    {
        var factor = PreviewOptionsViewModel.CalcFitScaleFactor(new Size(800, 450), new Size(400, 450));

        WriteLine(factor.ToString());
        Assert.Equal(0.5, factor);
    }

    [Fact]
    public void CalcFitScaleFactor_ClampsToMaxFitScale()
    {
        // A bare TextBlock measures around 91 x 19. Unclamped this would ask the host for a DPI an
        // order of magnitude above normal, and a frame to match.
        var factor = PreviewOptionsViewModel.CalcFitScaleFactor(new Size(91, 19), new Size(932, 598));

        WriteLine(factor.ToString());
        Assert.Equal(PreviewOptionsViewModel.MaxFitScale, factor);
    }

    [Fact]
    public void CalcFitScaleFactor_UnknownNaturalSizeIsNaN()
    {
        // NaN is what the loader reports until the host has sent a frame.
        var factor = PreviewOptionsViewModel.CalcFitScaleFactor(new Size(double.NaN, double.NaN), new Size(932, 598));

        WriteLine(factor.ToString());
        Assert.True(double.IsNaN(factor));
    }

    [Fact]
    public void CalcFitScaleFactor_CollapsedViewportIsNaN()
    {
        // The pane can arrange to zero while the XAML view is dragged fully open.
        var factor = PreviewOptionsViewModel.CalcFitScaleFactor(new Size(800, 450), new Size(932, 0));

        WriteLine(factor.ToString());
        Assert.True(double.IsNaN(factor));
    }
}
