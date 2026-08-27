# Milestone 2 result — protocol correctness

Date: 2026-07-28. Implements [PLAN.md](PLAN.md) Milestone 2, all four items, plus the two fixes that
fell out of doing them.

**Outcome: complete.** Both Avalonia 12.0.5 fixtures still preview end to end. Build clean in Debug
*and* Release, 0 warnings. Tests **64/64** — unchanged, because `Loading/` still has no coverage and
every item here was verified by running the app.

Worth restating the framing [milestone-0-result.md](milestone-0-result.md) established: **nothing
observed in Milestone 0 pointed at the protocol.** This milestone fixes silent drops and a silent
substitution, not a user-visible breakage. Its strongest justification remains the one measured
fact — the host sends `RequestViewportResizeMessage` three times per preview and it was discarded
without trace.

## Two facts established by probe before any code was written

Both were open questions the plan could not answer, and each decided a design.

1. **The host echoes `--session-id` verbatim.** Launching `Avalonia.Designer.HostApp.dll` 12.0.5 by
   hand with `--session-id PROBE-SESSION-1234` produced:

   ```
   Sending StartDesignerSessionMessage
   StartDesignerSessionMessage
       SessionId: PROBE-SESSION-1234
   ```

   Without this, item 4's validation could not exist — a host that minted its own id would make
   every comparison fail. (The probe needs `--html-url` alongside `--transport file://`, otherwise
   the host dies on `IPAddress.Parse` before printing anything.)

2. **`StartDesignerSessionMessage` does arrive on `tcp-bson`, and it arrived *after* `SendXaml`.**
   A baseline run of the unmodified app against `AvaloniaMvvm`:

   ```
   Connection received
   Connection OK
   RemoteLoader.SendXaml                        <- XAML sent first
   Message type: StartDesignerSessionMessage
   ```

   Milestone 0 had only observed the message on the `file://` transport. This confirms it on the
   real one, and confirms AvantGarde was racing ahead of it. That ordering flip is the acceptance
   proof for item 1, and it is the only thing that could prove it.

## What changed

### 1. The first `UpdateXamlMessage` waits for the session

`StartHostNoSync` now calls `WaitForSessionNoSync` after the handshake and before returning to
`UpdateThread`, which is what sends the XAML. The wait ends on any of four conditions, and the three
outcomes are distinct:

| Condition | Result |
|---|---|
| session announced | proceed |
| session id belongs to another instance | **throw** — see item 4 |
| host process exited | throw, naming the exit rather than a bare timeout |
| `SessionTimeout` (5 s) elapsed | warn to the OUTPUT pane and **send anyway** |

The timeout fallback is the plan's requirement: a host that never announces still previews. 5 s is a
private const rather than a new public property — measured, the message arrives within milliseconds
of the accept, so the value only matters in a pathology.

**The handshake was deliberately not moved.** AvaloniaRider sends `ClientSupportedPixelFormatsMessage`
after the session starts; AvantGarde sends it on accept and that demonstrably works. Leaving it put
means the timeout-fallback path is byte-identical to the old behaviour, and Milestone 4 rewrites
this area anyway.

### Handlers now attach in the listener callback

This is load-bearing for the gate, not tidying. `cnx.OnMessage += MessageHandler` used to run *after*
`SpinWait` returned on the TCP accept, so anything the host sent immediately on connecting could be
dropped in that window. Harmless when nothing waited on those messages; with the gate in place it
would have converted a rare invisible drop into a reproducible 5 s stall on **every** preview.
`StartListenerNoSync` now subscribes inside the `BsonTcpTransport.Listen` callback, before assigning
`v_connection` — the field `StartHostNoSync` spins on.

### 2. The one-shot verbatim resend is gone

`PreviewFactory.GetResendAndReset` and `_resendFlag` are deleted, and `GetXaml(bool)` is now
`GetXaml()` — the `processed` argument had only one caller and only one value left.

The old behaviour: a `UpdateXamlResultMessage` carrying an error silently re-sent the file verbatim,
so a preview could be built from markup the user never configured, with nothing on screen saying so.
Replaced by an explicit OUTPUT line naming the options that modified the markup, emitted only when
`ProcessedXaml` is non-null — i.e. only when the sent XAML really was not the file.

### 3. Real dispatch

`MessageHandler`'s two-branch `if/else if` is now six branches delegating to named handlers, with
everything else reaching `ReportUnhandledOnce`. Three points of design:

- **Once per host process, not once per message.** `RequestViewportResizeMessage` arrives three
  times per preview; reporting each would bury the 100-line OUTPUT ring buffer. The reported-type
  set is cleared in `ClearOutput`, so a restarted host reports afresh.
- **`HtmlTransportStartedMessage` gets a `PreviewError`, not an output line.** It is terminal — the
  host is rendering to HTTP and no frame will ever arrive on that connection — so a line in a ring
  buffer is not enough. This closes the branch milestone-0 finding 2 left open: `--method
  avalonia-remote` is passed defensively, and if it ever stops being honoured the symptom is now a
  message in the preview pane rather than an indefinitely blank preview.
- **AvantGarde's own output lines are prefixed `[AvantGarde] `** so they are distinguishable from
  host stdout in the shared buffer.

### 4. `--session-id`, generated per host start and validated

A fresh GUID per `StartHostNoSync`, passed on the host command line and compared on arrival. A
mismatch sets `v_sessionMismatch`, which ends the wait immediately and throws — it does **not** warn
and carry on, because carrying on would mean previewing against the stale host, which is the exact
confusion the item exists to prevent.

`v_sessionId`, `v_sessionStarted` and `v_sessionMismatch` are all reset in `StopNoSync`. That matters
more than it looks: the `BuildWatcher` restarts the host once per open (a known Milestone 5 item), so
a second host start per session is routine, and stale state would let its gate pass instantly on the
first host's announcement.

## Verification

All against the Avalonia 12.0.5 fixtures, driven with the milestone-0 harness (Debug build, stdout
redirected, `-s=<leaf>`).

1. `dotnet build AvantGarde.sln` — 0 warnings, 0 errors, Debug **and** Release.
2. `dotnet test AvantGarde.Test` — 64/64.
3. **Item 1, `AvaloniaMvvm`** — the ordering inverted, on both host starts:

   ```
   Connection received
   Message type: StartDesignerSessionMessage
   SessionId: 3d1e40d4-0885-487f-861d-cb73e25871b4
   Connection OK
   RemoteLoader.SendXaml
   ```

   No timeout warning appears, so the gate is being satisfied rather than falling through. Frames
   still arrive, 800 × 450, twice per host.
4. **Item 1 + two-assembly path, `MultiProjectSolution` unmodified** — same ordering,
   `AssemblyPath` = `ClassLibrary1.dll`, `XamlFileProjectPath` = `/MyControl.axaml`, frames at
   800 × 450. Milestone 1's acceptance test is unaffected by the gate sitting upstream of it.
5. **Item 2** — a green run with default options proves nothing, because `ProcessedXaml` is only
   non-null when `LoadFlags != None`, so the resend could never have fired. Exercised properly by
   temporarily defaulting `PreviewOptionsViewModel._loadFlags` to `GridLines | DisableEvents` and
   breaking `MyControl.axaml` with an unresolvable type. Result: one `SendXaml`, one error, no
   `Resend`, and the OUTPUT line
   *"The XAML sent was modified by preview options (GridLines, DisableEvents)…"*. Both temporary
   changes reverted.
6. **Item 3** — `Message not handled: RequestViewportResizeMessage - host asked for a 800 x 450
   viewport (further occurrences are not reported)` appears exactly once per host start, on both
   fixtures. The plan's metadata-derived claim is now a line in the user's OUTPUT pane.
7. **Item 4** — the mismatch branch was exercised by temporarily sending the host a literal
   `--session-id MISMATCH-PROBE` while the loader expected its GUID:

   ```
   Message type: StartDesignerSessionMessage
   SessionId: MISMATCH-PROBE
   Ignoring a designer session belonging to another instance (expected 35b11d59-…, received MISMATCH-PROBE)
   EXCEPTION:System.InvalidOperationException: A designer session belonging to another instance answered on the port …
   ```

   No `SendXaml` followed, and the error reached the preview pane. Reverted.

## A regression item 2 exposed, and a partial fix

Deleting the resend surfaced a pre-existing defect it had been accidentally masking.

**Error line numbers refer to the markup that was sent, not the file.** With preview options on, the
host compiles `ProcessedXaml`, and `XDocument` does not round-trip layout. Under the old behaviour
the verbatim resend followed and reported the file's true line, so the user saw the right number by
accident; with the resend gone they see the processed one.

Partially fixed: `XDocument.Parse` now takes `LoadOptions.PreserveWhitespace` and `ProcessXaml`
writes back with `SaveOptions.DisableFormatting`, so the body keeps its source indentation. Measured
on an error at source line 8, column 9: reported position went from 6 to **10** — correct — but the
line stayed at **3**.

**Why the line cannot be fixed this way.** Attributes carry no whitespace of their own, so a start
tag spread over six lines (as every Avalonia file's root is) collapses to one and shifts everything
below it by five. A real fix needs a line map from processed markup back to source, or not
transforming at all — neither is Milestone 2. What is done instead is to say so: the OUTPUT line
above now ends *"The reported line is a line of the modified markup and may not be the line of the
file"*. Only affects previews with a `LoadFlags` option enabled; the default path sends the file
verbatim and is exact.

## Not fixed, deliberately

- **`MeasureViewportMessage` and `ClientViewportAllocatedMessage` are still never sent.** Answering
  `RequestViewportResizeMessage` is Milestone 4 item 1; this milestone only stops discarding it in
  silence.

  **Superseded by [milestone-4-result.md](milestone-4-result.md), and the framing above was wrong.**
  Neither message is sent because neither *can* be: the 12.0.5 host ignores
  `ClientViewportAllocatedMessage.Width/Height` outright and never answers `MeasureViewportMessage`.
  There is nothing to answer `RequestViewportResizeMessage` *with* — it is a notification, and
  Milestone 4 consumes it rather than replying to it. The `ReportUnhandledOnce` branch for it is
  gone.
- **Two host starts per opened preview.** Visible in every trace here. It is the `BuildWatcher`
  first-poll-always-reports-a-change defect recorded in milestone-1, unchanged and still Milestone 5.
- **`Dispose`'s empty `catch`, and the two in `InvokePreviewReady` / `InvokeOutputReceived`.**
  PLAN.md Milestone 0 asked for logging in these; they are on the UI-post path where there is
  nothing useful to do with an exception during shutdown. Left as they are.
