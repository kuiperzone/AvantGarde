# Milestone 4 result — frame rate limiting by delayed ack

Date: 2026-07-29. Implements [PLAN.md](PLAN.md) Milestone 4 **item 5**. Items 6–8 remain open.
Continues [milestone-4-result.md](milestone-4-result.md) (items 1 and 4) and
[milestone-4-input-result.md](milestone-4-input-result.md) (items 2 and 3).

**Outcome: complete, and the plan's premise held — but it does not do what the plan says it is for.**
Build clean in Debug *and* Release, 0 warnings. Tests **84/84**, up from 76.

## The probe, again first

PLAN.md item 5 asserts "the back-pressure channel already exists". That is the same class of claim as
the viewport row which [milestone-4-result.md](milestone-4-result.md) proved **false**, so it was
measured before anything was written: a temporary block in `RemoteLoader.HandleFrame` withheld
`FrameReceivedMessage` entirely for 20 s and then released the newest sequence number, against the
12.0.5 host previewing a `Border` with an infinite opacity animation.

| Probe | Result |
|---|---|
| Ack withheld from the first frame | **One frame, then nothing for 20 s.** |
| Ack released after 20 s | Stream resumes **within 6 ms**. |
| Sequence numbers across the release | **Contiguous** — 1, then 2, 3, 4 … with no gap. |
| Frames delivered immediately on release | **No burst.** Gaps 3–46 ms, i.e. the host's ordinary render cadence. |
| Unpaced rate, animated control | **43.3 fps**, 1140 frames in 26 s, 320 KB each — about 14 MB/s over the socket. |

So the back-pressure is **strict and it is a stall, not a queue**: the host renders the next frame
only once the previous one is acknowledged, and it does not buffer while it waits. Withholding the
ack throttles the host's rendering, the wire and this side's decode all at once. A frame held back is
a frame never rendered, so a deferred ack can never show a stale bitmap.

## A frame rate cap does not bound the caret

CLAUDE.md and the input note both name the blinking caret as item 5's provocation. It is **not** what
item 5 fixes, and it cannot be: a caret renders about twice a second, far below any cap worth
setting, so every one of those frames passes the limit untouched. Discovering this after implementing
would have been the expensive order.

The lever that does bound an idle guest is the same channel gated on something other than rate:
**withhold the ack entirely when the preview cannot be seen at all**. So two things shipped, not one.

## What shipped

- **`Loading/FrameRateLimiter.cs`** (new) — the pacing decision, static and taking primitives
  including the current time, following `InputMapper`'s pattern so it is unit-testable without a
  clock, a host or a socket. `GetInterval` rounds **up**, because a rate cap is a ceiling.
- **`RemoteLoader.MaxFrameRate`** — frames a second the host may deliver, default **30**, 0 or less
  disables. Shaped like `Timeout` and `MaxOutputLines`: a `v_`-backed volatile property with no
  settings UI.
- **`RemoteLoader.IsRenderPaused`** — withholds the ack outright, released on clear. `MainWindow`
  sets it from `WindowState == Minimized`, in the `PropertyChangedHandler` case that was already
  watching that property.
- **A fourth lock, `_ackSync`**, guarding the pending ack, the clock and the timer. The ack is
  written from the transport thread and from a timer callback, and neither has business waiting on
  the lifecycle or the viewport. Like `_viewportSync` it is an inner lock, and the send is fired
  outside it.

The frame itself is always processed as it arrives — `ClearXamlPending`, `DeriveNaturalSize`,
`InvokePreviewReady` are untouched. Only the acknowledgement moves. Nothing sleeps on the transport
thread, which also carries `UpdateXamlResultMessage`: a blocking delay there would stall error
reporting behind the throttle.

### One pending slot, holding the newest

The slot is written, never queued, and the connection doubles as the "ack owed" flag. Two rules it
enforces, both of which would be silent failures:

- **Never acknowledge a superseded frame.** This host has at most one outstanding, so the case cannot
  arise today; a host that behaved otherwise would be left waiting forever for an ack it had already
  been sent the wrong number for.
- **Never drop a pending ack.** An ack cancelled and never sent is a preview that stops updating with
  no error anywhere. Pausing mid-delay does not discard the slot — the timer callback returns and
  leaves it owed, and clearing `IsRenderPaused` pays it.

The connection is **captured** when the frame arrives rather than read from `v_connection` in the
timer callback, so a host restarted during the delay cannot be sent the previous host's ack.
`StopNoSync` clears the slot and resets the clock, so the next host's first frame is acknowledged at
once.

## The achieved rate sits below the cap

Measured with the animation, cap 30: **23.9 fps** visible, against 43.3 unpaced. The requested
deferral was a median of 33 ms but the observed inter-frame period was 43 ms.

The gap is the Windows timer granularity — `System.Threading.Timer` resolves to about 15 ms, so a
33 ms delay fires nearer 40 — plus the host's own render and transmit time. The cap is honoured as a
ceiling, which is what it claims to be, but a cap of *n* delivers meaningfully fewer than *n*. Worth
knowing before anyone tunes the number. Raising the resolution would mean `timeBeginPeriod`, a
process-wide setting, for a preview window; not worth it.

## Verification

Against the Avalonia 12.0.5 fixtures, driven through the OS — `ShowWindow` for minimize and restore
on the real window, file writes for edits — with `PrintWindow` screenshots and the Debug stdout
trace timestamped as it was read.

1. `dotnet build AvantGarde.sln` — 0 warnings, 0 errors, Debug **and** Release.
2. `dotnet test AvantGarde.Test` — **84/84** (76 + 8 new facts for `FrameRateLimiter`).
3. **The cap binds.** Animated control, 15 s window: 359 frames, 23.9 fps, against 43.3 unpaced.
   1178 deferrals logged, median 33 ms.
4. **Minimizing stops the host dead.** 15 s minimized: **0 frames**. Restoring resumes at 23.2 fps.
5. **Resume pays the owed ack.** In that run the pause landed while an ack was deferred, so the timer
   fired, saw the pause and left the slot owed; frames resumed 5 ms after `Render paused: False`.
   Both orderings were exercised — see 6 for the other.
6. **A frame arriving while paused is withheld and still displayed.** Minimize, then edit the file
   while minimized: `Ack withheld - paused: 1`, the payload is delivered anyway, and on restore the
   preview already shows the edit. This is the branch the animation runs rarely hit.
7. **The host still compiles and still reports errors while paused.** Measured rather than inferred,
   because it is a claim about host behaviour and this repo has been wrong about those before.
   Minimize, then write markup the *host* must reject — `TextBlockk`, well-formed XML naming a type
   that does not exist — and `UpdateXamlResultMessage` arrives 1.1 s later, while the frame channel
   is stalled. **A malformed-XML error does not test this**: `PreviewFactory` parses the markup
   before sending, so "Unexpected end of file" never reaches the host and would prove nothing about
   the pause.
8. **No regression on the ordinary path.** `AvaloniaMvvm` `MainWindow.axaml` renders; an edit
   (alignment and font size) appears; a deliberate syntax error shows the message and a **Goto**
   button; restoring the file recovers. Screenshots at each step.
9. **Two-assembly path unregressed.** `MultiProjectSolution`'s `MyControl.axaml` from
   `ClassLibrary1.dll` still previews. Two frames, 52 ms apart, neither deferred — correctly, since
   the first ack is never delayed and 52 ms exceeds the interval.
10. **Fit-to-window is unaffected, because the cap cannot bind there.** Re-run of the milestone-4
    resize gesture with the cap in place: select **Fit**, then shrink the window over 40 steps in
    4.4 s. The drag produces **no** scale pushes at all — one lands 52 ms after it ends — and each
    push yields two frames, the first arriving 18 ms later and **never deferred**, since the previous
    ack was sent long before. Only the duplicate second frame of each pair is deferred, by 20 ms, and
    it is identical to the first. Resize latency is therefore unchanged by pacing: the path runs at a
    few frames a second, nowhere near the limit.

The probe used a temporary `AnimProbe.axaml` in the `AvaloniaMvvm` fixture, since deleted — a stray
`.axaml` under an Avalonia project would be globbed on the next fixture rebuild. It is worth
recreating for any future frame-path work, because it makes a continuously rendering guest trivial to
provoke and needs no click, unlike the caret:

```xml
<Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d" d:DesignWidth="400" d:DesignHeight="200">
    <Window.Styles>
        <Style Selector="Border.pulse">
            <Style.Animations>
                <Animation Duration="0:0:2" IterationCount="INFINITE">
                    <KeyFrame Cue="0%"><Setter Property="Opacity" Value="0.1"/></KeyFrame>
                    <KeyFrame Cue="100%"><Setter Property="Opacity" Value="1.0"/></KeyFrame>
                </Animation>
            </Style.Animations>
        </Style>
    </Window.Styles>
    <Border Classes="pulse" Background="Red" Width="100" Height="100"/>
</Window>
```

## Not fixed, deliberately

- **The caret still blinks and still costs frames** while the window is visible, now capped at 30 a
  second instead of unbounded. Only minimizing stops it. Bounding it further would need a signal the
  protocol does not carry — there is no way to ask the host to stop animating.
- **`MaxFrameRate` has no settings UI.** It follows `Timeout` and `MaxOutputLines`, which are also
  code-only. If it is ever exposed, note that the achieved rate is below the set one.
- **Only minimize pauses.** Occlusion by another window, and a preview pane scrolled out of view, are
  both invisible-preview cases that go on rendering. Avalonia surfaces no occlusion signal, and the
  pane is always visible when a solution is open.
- **Two host starts per opened preview**, still the `BuildWatcher` first-poll defect from
  milestone-1, and visible again in every trace here. Still Milestone 5.
