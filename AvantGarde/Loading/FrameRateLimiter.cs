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

namespace AvantGarde.Loading;

/// <summary>
/// Paces the acknowledgement of preview frames. The designer host will not send a frame while an
/// earlier one is unacknowledged, so withholding <c>FrameReceivedMessage</c> throttles the host
/// itself rather than merely discarding work that has already been done.
/// </summary>
/// <remarks>
/// Measured against the Avalonia 12.0.5 host, the back-pressure is strict: with the acknowledgement
/// withheld, exactly one frame arrived and nothing followed for 20 seconds, and on release the
/// stream resumed with contiguous sequence numbers and no queued burst. Left unpaced, an animated
/// control renders at about 43 frames a second, each frame a full uncompressed bitmap.
///
/// The methods are static and take primitives, including the current time, so the pacing can be
/// tested without a clock, a host or a socket. See <see cref="RemoteLoader"/> for the state they
/// are driven from.
/// </remarks>
public static class FrameRateLimiter
{
    /// <summary>
    /// The largest accepted frame rate. Above this the interval rounds to zero and the limit would
    /// silently stop meaning anything.
    /// </summary>
    public const int MaxRate = 1000;

    /// <summary>
    /// Gets the minimum interval in milliseconds between frame acknowledgements for the given
    /// frame rate. A rate of 0 or less gives 0, i.e. no limit.
    /// </summary>
    /// <remarks>
    /// Rounded up, because the rate is a ceiling. At 30 fps, an interval of 33 ms would allow 30.3
    /// frames a second and 34 ms allows 29.4.
    /// </remarks>
    public static int GetInterval(int rate)
    {
        if (rate <= 0)
        {
            return 0;
        }

        return (int)Math.Ceiling(1000.0 / Math.Min(rate, MaxRate));
    }

    /// <summary>
    /// Gets how long in milliseconds to withhold the acknowledgement of a frame which arrived at
    /// <paramref name="now"/>, given the time the last one was acknowledged and the minimum
    /// interval. A result of 0 means send it immediately.
    /// </summary>
    /// <remarks>
    /// A negative <paramref name="last"/> means nothing has been acknowledged yet. The first frame
    /// of a preview is never delayed, which matters because it is the one the user is waiting for -
    /// the limit exists to bound a continuously rendering guest, not to add latency to an edit.
    /// </remarks>
    public static int GetDelay(long last, long now, int interval)
    {
        if (interval <= 0 || last < 0)
        {
            return 0;
        }

        long elapsed = now - last;

        if (elapsed >= interval)
        {
            return 0;
        }

        if (elapsed < 0)
        {
            // Cannot happen with a monotonic clock, but a delay longer than the interval would
            // stall the preview rather than pace it, so it is clamped rather than trusted.
            return interval;
        }

        return interval - (int)elapsed;
    }
}
