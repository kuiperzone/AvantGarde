# Milestone 4 result — viewport negotiation and fit-to-window

Date: 2026-07-28. Implements [PLAN.md](PLAN.md) Milestone 4 **items 1 and 4 only**. Items 2, 3 and
5–8 are untouched and remain open — see "Not attempted" below.

**Outcome: complete, but not the feature the plan describes.** A probe run before any code was
written overturned the premise item 1 rests on. Fit-to-window works, on both fixtures, and is
delivered — but by a different mechanism, and the plan's facts table is wrong in a way worth
recording. Build clean in Debug *and* Release, 0 warnings. Tests **69/69**, up from 64.

## The probe that overturned the plan

PLAN.md's facts table asserts, as verified:

> **Host side genuinely consumes the unused messages.** `Avalonia.Controls.Remote.Server` references
> `ClientViewportAllocatedMessage`, `MeasureViewportMessage` … Milestone 4's headline features are
> real server-side capability, not just declared types. Verified, not assumed.

That was a **metadata scan**: it established that the host assembly *references the type*. It does
not establish that the host *acts on the fields*. Measured against the 12.0.5 designer host, it does
not:

| Probe | Result |
|---|---|
| `ClientViewportAllocatedMessage` 640 × 360 sent before the XAML | **Ignored.** Frames still 800 × 450. |
| Same, sent in reply to `RequestViewportResizeMessage` (i.e. after the XAML loaded) | **Ignored.** Frames still 800 × 450. |
| Same, against a control declaring **no** `d:DesignWidth/Height`, whose content measures 91 × 19 | **Ignored.** Frames 91 × 19. |
| Same, with `DpiX/DpiY = 192` | **Honoured.** Frame 2 came back 1600 × 900 @ 192. |
| `MeasureViewportMessage` 1000 × 1000 | **Never answered.** No reply of any kind, on any run. |

So `ClientViewportAllocatedMessage.Width/Height` is a dead field on this host, tested both below the
design size and above the content's desired size. Its only working field, DPI, duplicates
`ClientRenderInfoMessage`, which AvantGarde already sends. `MeasureViewportMessage` is inert.

The conclusion is the opposite of the plan's: **the client cannot tell this host how large to
render.** The host renders at the design size, or at the content's desired size where none is
declared, and states that size in `RequestViewportResizeMessage`. There is no negotiation. The
message is a notification.

This is the same class of correction [milestone-0-result.md](milestone-0-result.md) made: a fact
derived from reading metadata, which reading the running system contradicts.

## What item 1 became

Not "send viewport messages" — nothing is sent. **Consume `RequestViewportResizeMessage`, learn the
natural size, and drive fit-to-window through the DPI channel that already works.**

Neither `ClientViewportAllocatedMessage` nor `MeasureViewportMessage` is sent. Sending the former
would create a second DPI channel alongside `ClientRenderInfoMessage` for no gain; sending the
latter would be writing to a socket nothing reads.

Fit-to-window is therefore **auto-scale, not reflow**. The design surface fills the pane at a higher
DPI; the control does *not* relayout to the pane's shape. That is the right behaviour for a previewer
— it shows the control as designed, larger — but nobody reading "fit to window" later should expect
reflow, because the host makes reflow unreachable.

### The natural size comes from the frame, not the resize message

This was got wrong first and corrected by measurement, and it is the substantive part of the
milestone.

The obvious reading is that natural size = `RequestViewportResizeMessage` ÷ the scale in force when
the XAML was sent. It is wrong. At 200%, the trace reads:

```
Send scale: 2
RemoteLoader.SendXaml
Natural size latched: 400 x 225 (from 800 x 450 at scale 2)   <- wrong, it is 800 x 450
FRAME: 1, 800 x 450 px
FRAME: 2, 1600 x 900 px
```

The host renders once *before* applying the pending DPI and again after. The first resize request
after a send therefore arrives at the **unscaled** size, so dividing by the scale halves it.

`FrameMessage` carries `DpiX`/`DpiY` alongside its pixel size, so a frame is **self-describing**:

```
natural = frame.Width * 96 / frame.DpiX
```

Frame 1 gives 800 × 96/96 = 800. Frame 2 gives 1600 × 96/192 = 800. Both agree, at every scale.

That is not merely more accurate, it is what makes the feature safe. The fit factor is computed from
the natural size and pushed back to the host *as DPI*, so any derivation that does not divide out
the exact DPI of the frame feeds its own output into its input. Dividing by the frame's own DPI is
invariant under scale, so there is no loop to damp.

Two further guards, both from measured drift rather than theory:

- **Latched once per `SendXaml`.** The control's size cannot change without the XAML changing.
- **A 1.5-dip tolerance across generations.** At a fractional DPI the quotient lands off by a
  fraction of a dip — a 525 px frame at 111.84 dpi gives 450.6 — which was observed flipping the
  natural height between 450 and 451 on the `MultiProjectSolution` fixture.

## What item 4 became

- **`Fit` heads the scale ladder** in `PreviewOptionsViewModel`, ahead of the 25–400 % rungs.
  `DecScale` stops at 25 % rather than stepping into it, because it is a mode and not a rung.
- **Clamped to 400 %** (`MaxFitScale`). Verified against a control with no design size: natural
  91 × 19 in a 932 × 598 pane computes 10.24 and is held at 4.0.
- **Resize is debounced 150 ms.** `SizeChanged` fires per pixel during a drag; without this each
  pixel would push a DPI change to the host. The frame path *starts* the timer rather than
  restarting it, because frames can arrive faster than the interval — animated content does — and
  restarting on each would hold it off its tick indefinitely.
- **`IncScale` leaves fit onto the first rung above the settled factor**, not index + 1. `+` from a
  fit of 1.06 would otherwise land on 25 % and shrink the preview, on a button marked "increase".
  `DecScale` correspondingly stops at 25 % rather than stepping into fit.
- **The in-flight interlock is implemented**, per the plan: a scale change while a XAML update is
  outstanding sets `v_scalePending` instead of sending, and is flushed by whichever of the result or
  the first frame arrives. `v_xamlPending` is cleared again if the send itself fails — left set on a
  send that never happened, every later scale change would be deferred against a reply that cannot
  arrive.

### The chrome correction

Fitting to the scroll viewer's bounds **overflowed**, caught on a screenshot rather than in a log.
`PreviewControl` draws a window top-bar above the bitmap and dimension labels either side of it —
measured at 108 × 110 dips — so the bitmap never gets the whole viewport.

The top-bar scales with the zoom (`WindowTitleScale`), so the space available depends on the very
factor being solved for. Rather than model that, `PreviewControl.ChromeSize` measures it
(`Bounds − bitmap size`) and the fit is rechecked on each frame. Termination is guaranteed by a
**0.5 % deadband** in `SetFitScaleFactor`: a fit that no longer moves pushes no scale, so no frame
comes back to trigger another recheck. Measured, it settles in one step.

## The lock that had to move

`Scale`'s getter and setter took `_startSync` — the lock `UpdateThread` holds across the whole of
`StartHostNoSync`, a 10 s process wait plus a 5 s session wait. That was survivable while only the
scale dropdown touched it. Fit-to-window puts scale on the **resize** path, where a 15 s block during
host start would be a visible UI freeze, on exactly the gesture that provokes it.

Scale and the natural size now live under a new `_viewportSync`, which is never held across blocking
work and is always the inner lock: code may take it while holding `_startSync`, never the reverse.
This makes three locks in `RemoteLoader` — see the CLAUDE.md convention note about knowing which
applies before adding state.

## A side benefit worth naming

The dimension labels are now correct for the first time. They came from locally parsed
`d:DesignWidth/Height`, so a control declaring no design size showed `NaN`. `PreviewControl` now
prefers the host's own size, retaining any min/max from the markup. The probe control rendered
91 × 19 and the labels say so.

## Verification

Against both Avalonia 12.0.5 fixtures, driven with the milestone-0 harness (Debug build, stdout
redirected, `-s=<leaf>`), plus `PrintWindow` screenshots.

1. `dotnet build AvantGarde.sln` — 0 warnings, 0 errors, Debug **and** Release.
2. `dotnet test AvantGarde.Test` — **69/69** (64 + 5 new facts for `CalcFitScaleFactor`).
3. **Natural size, `AvaloniaMvvm`** — `800 x 450 (from 800 x 450 px at 96 x 96 dpi)`, and
   unchanged when the same content arrives as `1600 x 900 px at 192 x 192 dpi`. The invariant holds.
4. **The discriminating case — entering fit at a non-1.0 scale.** Driven 100 % → 200 % → host
   restart (so the XAML is *sent* at 200 %) → fit. Natural stayed 800 × 450, and the fit settled in
   **one** step. This is the test that would expose a wrong divisor as a walking scale; it does not
   walk.
5. **Two-assembly path, `MultiProjectSolution` unmodified** — `MyControl.axaml` from
   `ClassLibrary1.dll`, same behaviour, fit to 932 × 598.
6. **Clamp** — a `UserControl` with no `d:DesignWidth/Height`, natural 91 × 19, factor held at 4.0.
7. **Chrome** — `natural 800x450, bounds 967x606, chrome 108x88, viewport 851x510, factor 1.06375`
   → frame 851 × 479. 851 + 108 = 959 ≤ 967 and 479 + 88 = 567 ≤ 606. Screenshot confirms nothing
   clipped and both dimension labels visible.
8. `RequestViewportResizeMessage` no longer reaches `ReportUnhandledOnce`; the OUTPUT pane no longer
   carries `Message not handled: RequestViewportResizeMessage`.
9. **PLAN.md verification item 8 — the actual resize gesture**, driven through the OS with
   `MoveWindow` rather than by entering fit at a fixed size. The guest re-renders at each new
   viewport, and this is the run that exercises `PreviewScrollSizeChangedHandler`, the debounce and
   the relocated lock:

   | Scroll viewer bounds | Factor | Frame |
   |---|---|---|
   | 967 × 606 | 1.06375 | 851 × 479 |
   | 680 × 585 | 0.705 | 564 × 318 |
   | 960 × 685 | 1.055 | 844 × 475 |
   | 860 × 645 | 0.93 | 744 × 419 |

   Every fit is followed by a recheck returning the *same* factor and stopping — the deadband
   terminating the convergence, in the real gesture rather than in theory.
10. **The debounce coalesces.** A sweep of ten `MoveWindow` calls 25 ms apart produced **one**
    `UpdateFitScale` and **one** `Send scale`, at the final size — not ten.
11. **Both interlock branches fired**, in the same run: `Scale deferred - XAML update in flight`
    followed by `Sending deferred scale`, when the fit landed while the first XAML update was still
    outstanding.

All probe code was reverted, and the temporary `ProbeNoSize.axaml` deleted from the fixture — a
stray `.axaml` under an Avalonia project would be globbed on the next fixture rebuild.

## Not attempted

Deliberately out of scope, and still open:

- **Items 2 and 3 — key, text and scroll forwarding.** A clean separate pass: additive, cannot
  regress the default render path, and reuses the `PointerEventMessage` → `SendPointerEvent` spine
  that already works. `PointerEventMessage`'s `KeyModifiers.Meta → InputModifiers.Windows` TODO is
  still there.
- **Items 5–8** — FPS limiting by delayed ack, build on demand, shadow copy, theme injection. Each
  pulls in machinery unrelated to the protocol.

## Not fixed, deliberately

- **Pixel-perfect 1:1**, listed under item 4, is **not** implemented. It conflicts with the
  zoom-as-DPI model: frames are turned into bitmaps at a fixed 96 dpi, so a bitmap's displayed size
  equals its pixel size and zoom works by making it bigger. True 1:1 on a HiDPI display needs the
  bitmap's DPI to track the display while the zoom ladder stays independent — two things the single
  `Dpi` constant in `RemoteLoader.ToBitmap` currently conflates. Worth doing; not a one-liner, and
  it would change what every existing zoom level looks like.
- **`PreviewOptionsViewModel`'s instance members are untested.** `AvantViewModel`'s constructor
  reaches `GlobalModel.Global`, whose static initialiser resolves `IAssetLoader` and throws without
  a running Avalonia application, so the view model cannot be constructed in a unit test. The
  deadband and `DecScale`'s floor are covered by the fixture runs only. Bootstrapping a headless app
  for the test project belongs to Milestone 5.
- **`ScaleFactor` is 1.0 while the ladder index says otherwise, until the first change.** The
  constructor sets `_scaleSelectedIndex` without calling `OnScaleChanged`. Harmless because the
  index defaults to 100 %, which agrees — but it is why an attempt to start the app at 200 % by
  moving the default index alone had no effect. Pre-existing; left alone.
- **Two host starts per opened preview**, still the `BuildWatcher` first-poll defect from
  milestone-1. Visible in every trace here. Still Milestone 5.
