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

using AvantGarde.Loading;
using AvantGarde.Test.Internal;
using Xunit;
using Xunit.Abstractions;

namespace AvantGarde.Loading.Test;

/// <summary>
/// Covers the pacing of frame acknowledgements.
/// </summary>
/// <remarks>
/// The decision is a pure function of the last acknowledgement time, the current time and the
/// interval, which is what puts it within reach of a unit test - the loader supplies the clock and
/// the socket.
/// </remarks>
public class FrameRateLimiterTest(ITestOutputHelper helper) : TestUtilBase(helper)
{
    [Fact]
    public void GetInterval_ZeroOrLessDisables()
    {
        Assert.Equal(0, FrameRateLimiter.GetInterval(0));
        Assert.Equal(0, FrameRateLimiter.GetInterval(-1));
    }

    [Fact]
    public void GetInterval_RoundsUpSoTheRateIsACeiling()
    {
        // 1000 / 30 is 33.33. Rounding down would allow 30.3 frames a second.
        Assert.Equal(34, FrameRateLimiter.GetInterval(30));
        Assert.Equal(17, FrameRateLimiter.GetInterval(60));
        Assert.Equal(100, FrameRateLimiter.GetInterval(10));
    }

    [Fact]
    public void GetInterval_ClampsToMaxRate()
    {
        // Above the clamp the interval would round to zero, silently disabling the limit.
        Assert.Equal(1, FrameRateLimiter.GetInterval(FrameRateLimiter.MaxRate));
        Assert.Equal(1, FrameRateLimiter.GetInterval(FrameRateLimiter.MaxRate * 10));
    }

    [Fact]
    public void GetDelay_NoIntervalSendsNow()
    {
        Assert.Equal(0, FrameRateLimiter.GetDelay(1000, 1001, 0));
    }

    [Fact]
    public void GetDelay_FirstFrameIsNeverDelayed()
    {
        // A negative last time means nothing has been acknowledged yet. The first frame is the one
        // the user is waiting for.
        Assert.Equal(0, FrameRateLimiter.GetDelay(-1, 5000, 34));
    }

    [Fact]
    public void GetDelay_IntervalElapsedSendsNow()
    {
        Assert.Equal(0, FrameRateLimiter.GetDelay(1000, 1034, 34));
        Assert.Equal(0, FrameRateLimiter.GetDelay(1000, 9999, 34));
    }

    [Fact]
    public void GetDelay_WithinIntervalGivesTheRemainder()
    {
        Assert.Equal(34, FrameRateLimiter.GetDelay(1000, 1000, 34));
        Assert.Equal(24, FrameRateLimiter.GetDelay(1000, 1010, 34));
        Assert.Equal(1, FrameRateLimiter.GetDelay(1000, 1033, 34));
    }

    [Fact]
    public void GetDelay_ClampsToTheIntervalIfTheClockGoesBack()
    {
        // Cannot happen with a monotonic clock. A delay longer than the interval would stall the
        // preview rather than pace it.
        Assert.Equal(34, FrameRateLimiter.GetDelay(1000, 500, 34));
    }
}
