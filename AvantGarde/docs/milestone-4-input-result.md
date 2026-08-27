# Milestone 4 result — keyboard, text and scroll forwarding

Date: 2026-07-28. Implements [PLAN.md](PLAN.md) Milestone 4 **items 2 and 3**. Items 5–8 remain
open. Continues [milestone-4-result.md](milestone-4-result.md), which covered items 1 and 4.

**Outcome: complete, and this time the plan's premise held.** The user can click into the preview
and type, and the wheel reaches a `ScrollViewer` inside the guest. Build clean in Debug *and*
Release, 0 warnings. Tests **76/76**, up from 69 — and the five new facts are the **first coverage
of `Loading/`** the repo has had.

## The probe, again first

[milestone-4-result.md](milestone-4-result.md) established that PLAN.md's "host side genuinely
consumes the unused messages" row is a metadata scan, and false for the viewport messages. It says
in as many words that the input messages are untested and the row must not be assumed for them
either. So they were measured before any code was written, against the 12.0.5 host, by sending the
messages by hand from a temporary block in `RemoteLoader` and saving each frame as a PNG.

| Probe | Result |
|---|---|
| `TextInputEventMessage` "A", no click first | **Nothing.** Frame unchanged, no new frame. |
| `KeyEventMessage` B down/up, no click first | **Nothing.** |
| Pointer press + release over a `TextBox` | Focus border and caret appear. |
| `TextInputEventMessage` "C" after that click | **"ZZ" becomes "ZZC".** |
| `KeyEventMessage` Back down/up after that click | **"ZZC" becomes "ZZ".** |
| `ScrollEventMessage` `DeltaY = -3` over a `ScrollViewer`, **no click at any point** | **Scrolls three lines.** |

So all three message types work, and the row is true for input even though it was false for the
viewport. But there is a precondition the plan does not mention.

### Keyboard forwarding depends on pointer forwarding

The host routes key and text events to whatever the guest has **focused**, and a guest that has
never been clicked has focused nothing. There is no protocol message that activates the guest window
or moves focus into it. So keyboard input is unreachable until a pointer press has landed on a
focusable control, and nothing on this side can detect the difference — there is no acknowledgement.
A key sent to an unfocused guest is discarded silently at the far end.

That is a documented precondition rather than a defect: the click that a user would make anyway is
the thing that arms the feature. It is recorded in `KeyboardEventMessage`'s remarks and in
`RemoteLoader.SendKeyboardEvent`.

Scroll is different and needs no click, because `ScrollEventMessage` derives from
`PointerEventMessageBase` and is routed by hit test rather than by focus.

## What shipped

- **`Loading/InputMapper.cs`** (new) — the shared conversion to the protocol's own enumerations.
  Static, and takes primitives rather than event arguments, which is what makes it testable.
- **`Loading/KeyboardEventMessage.cs`** (new) — carries a key transition or a unit of composed text,
  the keyboard counterpart to `PointerEventMessage`.
- **`Loading/PointerEventMessage.cs`** — gained a `PointerWheelEventArgs` constructor and an
  `IsScrolled` branch producing `ScrollEventMessage`. Its `List<InputModifiers>` field became the
  array the protocol wants, built once by `InputMapper`.
- **`RemoteLoader.SendKeyboardEvent`**, with the `DisableEvents` gate factored out of
  `SendPointerEvent` into `IsInputEnabled` so both paths test it once.
- **`PreviewControl`** is now `Focusable`, takes focus on a pointer press, and overrides `OnKeyDown`,
  `OnKeyUp` and `OnTextInput`.

`Key` and `PhysicalKey` are copies of Avalonia's own — all 223 and 165 names match by name *and*
value against 12.1.0 — so the conversion is a cast. It is guarded by `Enum.IsDefined` and two tests
walk every Avalonia value, so a future divergence fails a test rather than sending the host a value
it cannot read. The long-standing `KeyModifiers.Meta → InputModifiers.Windows` TODO is resolved and
asserted.

## Two things measurement caught that reasoning had not

### Consuming a KeyDown suppresses the text that would have followed it

The first `Handled` policy was "consume unmodified keys, so arrows and space reach the guest instead
of scrolling the pane". It looked right and it broke typing outright: letters never appeared in the
guest while Backspace still worked. The Win32 backend drops the `WM_CHAR` for a key event the
application handled, so consuming the `KeyDown` destroys the `TextInput` that carries the character.
Backspace survived because it produces no character.

The policy is now an allow-list of keys that produce no text and that the enclosing `ScrollViewer`
would otherwise act on: arrows, `PageUp`/`PageDown`, `Home`/`End`. Everything else is forwarded but
left to bubble.

Modified gestures are never consumed, and this is not a matter of taste: every `HotKey` in
`MainWindow.axaml` carries Ctrl or Alt, `HotKey` installs a `KeyBinding` on the window, and
consuming here would disable the menu accelerators whenever the preview has focus. Measured with the
preview focused, `Ctrl+OemPlus` still zooms — and the trace shows the `OemPlus` **KeyDown never
reaches the preview at all**, only the KeyUp, because the key binding takes it first. `Tab` is not in
the allow-list either, so focus can always leave the preview; guest Tab-navigation is therefore not
usable, which is the accepted cost of not inventing a focus-release affordance.

### `PreviewControl`'s `x:Name` fields are all null

`CreateWheelMessage` first read the preview `Image` by its generated field to get coordinates
relative to the bitmap. The coordinates came out as `892, 537` — outside the 800 × 450 guest surface,
so the guest ignored the scroll and nothing moved.

`PreviewControl` calls `AvaloniaXamlLoader.Load(this)` rather than the generated
`InitializeComponent()`, so **none** of its `x:Name` fields are ever assigned. The field was null,
and `GetCurrentPoint(null)` does not throw — it quietly returns coordinates relative to the window.
The wheel handler now builds its message from the event's `sender`, as the three pointer handlers
beside it already did, and the coordinates land at `398, 337`.

Worth knowing before reaching for a named control anywhere in that file.

## The wheel has three claimants

Settled in `PreviewPane.WheelEventHandler`, in this order:

1. **Ctrl+Wheel zooms**, as it does nearly everywhere, and wins outright.
2. **The pane keeps the wheel while the preview is larger than the viewport** on the axis being
   scrolled. Panning a preview too big to see is what the wheel did before this milestone and taking
   it away would be a regression.
3. **Otherwise the guest gets it** — which is the common case, fit-to-window among it.

Rule 2 tests the scroll *extent*, not the current offset. "Pane first, guest once the pane reaches
its end" would hand the gesture over mid-drag, so an identical wheel turn would do different things
depending on where the pane happened to be sitting.

`ScrollEventMessage.DeltaX/DeltaY` are **not** divided by the scale. The delta is in wheel notches on
both sides of the wire, whereas `X`/`Y` are positions in the guest's dips and are divided as the
other pointer messages are.

## Verification

Against the Avalonia 12.0.5 fixtures, driven through the OS with `SetCursorPos`/`mouse_event`/
`keybd_event` on the real window — the milestone-4 pattern of using the actual gesture rather than
calling the handler — with `PrintWindow` screenshots and the Debug stdout trace.

1. `dotnet build AvantGarde.sln` — 0 warnings, 0 errors, Debug **and** Release.
2. `dotnet test AvantGarde.Test` — **76/76** (69 + 7 new facts for `InputMapper`).
3. **Scroll reaches the guest.** One wheel turn over the guest list: `IsScrolled 0, -3, 398, 337`,
   and the list moves from LINE 01 to LINE 04. The coordinates are inside the 800 × 450 surface,
   which is the check that would have caught the null-field defect.
4. **Typing reaches the guest.** Click, then `a`, `b`, `c`, then Backspace: the trace shows
   `KeyDown A` / `Text 'a'` / `KeyUp A` per letter and `KeyDown Back` with no text, and the guest
   `TextBox` reads `ZZab`.
5. **Menu accelerators survive preview focus.** `Ctrl+OemPlus` with the preview focused →
   `Send scale: 1.25`. The preview sees only `KeyDown LeftCtrl` and the `OemPlus` KeyUp.
6. **Ctrl+Wheel zooms**, four turns → 1.5, 2, 3, 4, each a real re-render at DPI.
7. **The pane keeps the wheel when it can scroll.** At 400% the preview exceeds the viewport; a
   plain wheel turn pans the pane and produces **no** further `IsScrolled` message.
8. **Arrows are consumed.** At 400%, with the pane scrollable, Down × 3 forwards three
   `KeyDown Down` messages and the pane does not move. Note the before/after screenshots do **not**
   hash equal, and are not meant to: the guest's caret was blinking. They were compared by eye, and
   differ in the caret alone.
9. **Two-assembly path, `MultiProjectSolution` unmodified** — `MyControl.axaml` from
   `ClassLibrary1.dll` still previews and a wheel over it produces an in-surface
   `IsScrolled 0, -2, 375, 185`. Nothing moved, because that control has no scrollable content;
   this establishes the render path is unregressed, not that guest scrolling works there. Guest
   scrolling was verified on `AvaloniaMvvm` only.
10. **Disable Events still disables.** With the flag set (`Ctrl+D3`), no keyboard or scroll message
    is sent, and — the part worth testing — arrow keys go back to scrolling the pane. A preview
    which is not forwarding a key must not consume it either, which is why
    `KeyboardEventOccurred` reports whether it forwarded and `Handled` follows that.

The temporary `ProbeInput.axaml` was deleted from the `AvaloniaMvvm` fixture, and the probe block
reverted from `RemoteLoader` — a stray `.axaml` under an Avalonia project would be globbed on the
next fixture rebuild.

## Not fixed, deliberately

- **Guest Tab-navigation is unusable**, per the `Handled` policy above. Consuming Tab would trap
  keyboard focus in the preview.
- **`Space` scrolls the pane** rather than being consumed, when the pane has room to scroll. It is
  in the ScrollViewer's key set but it also types, and typing wins.
- **A click into the guest starts the caret blinking, and a blinking caret renders a frame forever**
  — around two a second, indefinitely, for as long as a guest `TextBox` holds focus. Harmless but
  wasteful, and it is exactly what item 5, FPS limiting by delayed ack, exists to bound. Noted here
  because this milestone is what makes it easy to provoke.
- **Clicking the preview now moves keyboard focus** away from the XAML code box. Required for item 2;
  worth knowing.
- **The message classes themselves remain untested.** `KeyEventArgs` and `PointerWheelEventArgs`
  need an input device and a live application. The mapper takes primitives specifically so the part
  that can be tested is; constructing the wrappers waits on the headless-app bootstrap that
  Milestone 5 owns.
- **Two host starts per opened preview**, still the `BuildWatcher` first-poll defect from
  milestone-1. It also disrupted the first probe run, killing the host mid-sequence. Still Milestone 5.
