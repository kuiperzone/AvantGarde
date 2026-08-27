# Milestone 0 result — 12.x round-trip spike

Date: 2026-07-28. This is the written note [PLAN.md](PLAN.md) Milestone 0 asks for. Harvested from a
session scratchpad (`h1.log`, `h2.err`, `host-net8.err`) that is temp-cleaned; the raw logs are gone,
the findings are here.

The spike ran the designer host **by hand** against a hand-built Avalonia 12 fixture (`Fx12`, a
minimal `net8.0` app), bypassing AvantGarde entirely — the plan's verification step 9 (differential
probe), which isolates host + fixture + `dotnet exec` from AvantGarde's own code.

## Scope of what was actually run

This covers the **differential probe only** (plan verification step 9). Milestone 0 steps 2–4 —
running AvantGarde itself against `AvaloniaMvvm` and `MultiProjectSolution` — have **not** been done.
The classification below is therefore from the host's own behaviour, which is stronger evidence about
the host than an in-app run would be, but it does not exercise AvantGarde's discovery or UI path.

## Outcome: the failure is host-TFM selection

### 1. Wrong host TFM → hard startup failure (→ Milestone 1)

Running `tools/net10.0/designer/Avalonia.Designer.HostApp.dll` against the `net8.0` fixture:

```
Unhandled exception. System.IO.FileNotFoundException: Could not load file or assembly
'System.Runtime, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'.
```

The host dies before opening the TCP connection, so from AvantGarde's side this would present as the
**10 s `SpinWait` timeout** with no diagnostic — the stderr carrying the cause was being discarded.

**Careful with the scope of this result.** It was a hand-launched probe, so it proves a wrong host TFM
is *fatal*; it does **not** show that AvantGarde selects one. Reading `NodeItem.FindInternal`, it
iterates `_contents` in reverse over an ascending `SortedList`, so `net8.0` is visited before
`net10.0` and `netstandard2.0` holds no `designer/` to match — meaning today's traversal appears to
land on the **correct** host by accident. Not measured; derived by reading.

So PLAN.md's "can pick `net10.0`" describes fragility rather than an observed failure. The
prescription is unaffected — resolve `net8.0` explicitly and stop depending on `FindFile` traversal
order — it just isn't the first thing a user hits.

**The first thing a user hits is version resolution.** `FindDesignerHost` does
`Path.Combine(nugetRoot, "avalonia", version)`, an exact directory-name match. The `AvaloniaMvvm`
fixture is Avalonia **12.0.5**, and `D:\NugetCache\avalonia\12.0.5` **does not exist** (cache has
12.0.0, 12.0.2, 12.1.0) because the fixture is unrestored. That is a `FileNotFoundException` before
the TFM question ever arises. Restoring the fixture should populate it — which is exactly why the
plan insists on restore-first. Both failure modes are Milestone 1.

One thing that does work: `DotnetProject.GetAvaloniaVersion` matches `Include="Avalonia"` exactly, so
it correctly reads `12.0.5` and ignores the fixture's mismatched `Avalonia.Diagnostics 11.3.18` /
`Avalonia.ReactiveUI 11.3.9` references. (Those two also hint that 12.x builds of both packages may
not exist — relevant to Milestone 3 item 3.)

### 2. `StartDesignerSessionMessage` sequencing confirmed; `--method` remains defensive

A second probe used `--transport file://…` (the pseudo-transport that watches a XAML file), which per
the host's own usage text **always uses the HTTP preview method** regardless of `--method`:

```
Initializing application in design mode
Obtaining AppBuilder instance from Fx12.Program
HtmlTransportStartedMessage
    Uri: http://127.0.0.1:34567/
Triggering XAML update
Sending StartDesignerSessionMessage
StartDesignerSessionMessage
    SessionId: 2d3120d3-da05-4eb4-883f-cd6c67e60e71
```

The valuable part is the ordering: the host sends `StartDesignerSessionMessage` **after** announcing
its transport and *before* it is ready for real XAML traffic — the sequencing premise behind
Milestone 2 item 1, now observed rather than assumed.

**What this probe does _not_ show:** that the host defaults to HTML when `--method` is omitted. The
`file://` transport forces HTML on its own, so `HtmlTransportStartedMessage` here proves nothing
about the default. That question is still open. The `--method avalonia-remote` change already applied
to `RemoteLoader.cs` therefore stands as **defensive**, justified by PLAN.md fact #26 (both reference
implementations pass it explicitly, as does the host's own usage example) — not by measurement. It
costs nothing and removes a whole misdiagnosis branch; leave it in. If the default ever is HTML,
`MessageHandler` drops `HtmlTransportStartedMessage` silently and the symptom is an indefinitely
blank preview, which is precisely the branch worth pre-empting.

### 3. Host CLI surface (unchanged from 11.x)

A malformed invocation dumped the usage text, which is the authoritative statement of the 12.1.0 CLI:

- `--transport`: `tcp-bson://…` or `file://…` (the `file` pseudo-transport **always uses HTTP preview**)
- `--session-id`: available, currently unused by AvantGarde (Milestone 2 item 4)
- `--method`: `avalonia-remote` | `win32` | `html`
- `--html-url`

## Corrections to PLAN.md's facts table

Verified against `D:\NugetCache` on 2026-07-28. Both are refinements, not reversals — every
consequence the plan draws still holds.

1. **`tools/netstandard2.0` was not dropped in 12.x.** It still exists in 12.1.0, but now contains
   only `Avalonia.Build.Tasks.dll` — no `designer/` subdirectory. The load-bearing claim (exactly two
   *designer host* candidates in 12.x, `net8.0` and `net10.0`) is correct.

   | | 11.3.2 | 12.1.0 |
   |---|---|---|
   | `tools/netstandard2.0/` | Build.Tasks **+ designer/** | Build.Tasks only |
   | `tools/net461/` | `designer/*.exe` | absent |
   | `tools/net8.0/`, `tools/net10.0/` | absent | `designer/*.dll` (both) |

2. **`AvaloniaPreviewerNetCoreToolPath` confirmed** — 12.1.0 `build/Avalonia.props` points at
   `tools\net8.0\designer\`, 11.3.2 at `tools\netstandard2.0\designer\`;
   `AvaloniaPreviewerNetFullToolPath` is present only in 11.3.2. Preferring `net8.0` mirrors
   Avalonia's own default, as the plan says.

3. Local Avalonia packages available for testing: `11.2.3`, `11.3.2`, `11.3.12`, `12.0.0`, `12.0.2`,
   `12.1.0`. SDKs installed: 8.0.129, 8.0.423, 9.0.316, 10.0.103, 10.0.302.

## Working-tree changes this produced

Uncommitted in `AvantGarde/Loading/RemoteLoader.cs`:

- `--method avalonia-remote` passed explicitly (finding 2).
- `ProcessOutputHandler` now buffers **all** host output, not just output arriving after `v_factory`
  is set — fatal startup errors like finding 1 appear before that and were being thrown away. Only
  the UI notification stays gated on `v_factory`.
- The `catch` in `UpdateThread` captures output **before** `StopNoSync()` clears the buffer, so the
  error payload carries the host's stderr instead of a bare "Timed out waiting for …".

Without these three, finding 1 is invisible from inside the app. (They were committed with the
Avalonia 12 build, `6f2b132`; the working tree is clean.)

---

# Part 2 — the in-app round trip

Date: 2026-07-28, same day. This completes Milestone 0: PLAN.md steps 2–4, which Part 1 explicitly
left undone. AvantGarde itself was run against both fixtures, on the Avalonia 12.1.0 / net10.0 build
from [milestone-3-result.md](milestone-3-result.md).

## How it was run — the reproducible harness

Worth recording, because Part 1's raw logs were lost to a temp clean and this is what makes the runs
repeatable:

- **`Debug.WriteLine` reaches stdout.** Launch the Debug build with `-RedirectStandardOutput` and the
  whole internal trace — `FindTargetAssembly` step-by-step, every `MessageHandler` message type,
  frame sizes — lands in a file. No debugger needed. This is a far better read channel than the
  OUTPUT pane.
- **`AvantGarde.exe <file> [-s=<leaf>]`** opens a project or solution and selects an item, so no
  clicking is required. Two defects had to be fixed before that worked; see below.
- **Screenshots** need `PrintWindow` with `PW_RENDERFULLCONTENT` (flag `2`);
  `Graphics.CopyFromScreen` returns black, as noted in the milestone-3 write-up.
- **`Get-CimInstance Win32_Process | ? CommandLine -like '*Designer.HostApp*'`** prints the host
  launch line while the preview is up — the direct measurement of which host TFM was selected.

## Fixture 1 — `AvaloniaMvvm`: frames arrive

Restored and built first (`dotnet restore` + `dotnet build`), which populated
`D:\NugetCache\avalonia\12.0.5` and so removed Part 1's version-resolution blocker.

`AvantGarde.exe …\AvaloniaMvvm\Views\MainWindow.axaml` renders the window: title bar, Fluent theming,
`Welcome to Avalonia!` from the `Design.DataContext` view model. Two `FrameMessage`s at 800 × 450,
no `UpdateXamlResultMessage` error.

This is PLAN.md's **"Frames arrive"** branch — *"migration is mostly cosmetic"*. For the
single-project, restored, TargetFramework-in-csproj case, the Avalonia 12 previewer path already
works end to end with no code change beyond Part 1's three diagnostics.

### Two Part 1 claims promoted from derived to measured

1. **`net8.0` is the host actually selected.** Part 1 derived this from reading
   `NodeItem.FindInternal`'s traversal order and flagged it "not measured". Measured now:

   ```
   dotnet exec --runtimeconfig …\AvaloniaMvvm\bin\Debug\net8.0\AvaloniaMvvm.runtimeconfig.json
               --depsfile    …\AvaloniaMvvm\bin\Debug\net8.0\AvaloniaMvvm.deps.json
               D:\NugetCache\avalonia\12.0.5\tools\net8.0\designer\Avalonia.Designer.HostApp.dll
               --transport tcp-bson://127.0.0.1:52465/ --method avalonia-remote
               …\AvaloniaMvvm\bin\Debug\net8.0\AvaloniaMvvm.dll
   ```

   `tools/net8.0/designer/` — the correct one, and by accident, exactly as derived. Milestone 1
   should still resolve it explicitly; it is a latent fragility, not a live bug.

2. **Version resolution really was the blocker, and restore really is the fix.** Part 1 predicted
   restoring the fixture would populate `12.0.5` and unblock `FindDesignerHost`. It did.

### `RequestViewportResizeMessage` arrives, unhandled, three times per preview

Not predicted anywhere. The host sends it during the first XAML update and again after each frame:

```
Message type: RequestViewportResizeMessage      <- dropped
Message type: RequestViewportResizeMessage      <- dropped
Message type: UpdateXamlResultMessage
Message type: FrameMessage                      FRAME: 1, 800 x 450 px
Message type: RequestViewportResizeMessage      <- dropped
Message type: FrameMessage                      FRAME: 2, 800 x 450 px
```

`MessageHandler`'s two-branch `if/else if` drops it silently. This is direct evidence for two planned
items that were until now argued from metadata alone: **Milestone 2 item 3** (real dispatch, log the
unhandled) and **Milestone 4 item 1** (viewport negotiation — the host is *asking* for a viewport
size and getting no answer).

## Fixture 2 — `MultiProjectSolution`: blocked before the previewer is reached

All four projects report **"Debug assembly not found"** even when freshly built. Cause, from the
trace:

```
FindTargetAssembly , Debug
Failed - framework is null or empty
```

`TargetFramework` in this solution lives in **`Directory.Build.props`**, not in any `.csproj`.
`DotnetProject.ParseProject` parses only the project XML, so `TargetFramework` is empty,
`FindTargetAssembly` returns null on its first guard, and every project is unusable.

This is a **third, distinct Milestone 1 case**, alongside Part 1's version resolution and host-TFM
selection — and the most damaging, because the project is perfectly well-formed and fully built. It
is exactly what `dotnet msbuild -getProperty:TargetFramework` answers correctly. **Treat it as
Milestone 1's acceptance test:** open this solution unmodified and get four resolved assemblies.

### The two-assembly path itself is verified good on 12.0.5

To separate the discovery failure from the protocol path, `<TargetFramework>net8.0</TargetFramework>`
was **temporarily** inlined into the four `.csproj` files, and reverted afterwards — the fixture on
disk is faithful again, so the failure above still reproduces. With discovery unblocked, previewing
`ClassLibrary1/MyControl.axaml` with `AvaloniaApp1` as the app project renders "My User Control", and
the split is confirmed measured:

- host CLI gets `AvaloniaApp1` — its `runtimeconfig.json`, `deps.json` and `.dll`;
- `UpdateXamlMessage.AssemblyPath` gets `ClassLibrary1.dll`;
- `XamlFileProjectPath` = `/MyControl.axaml`; frames at 800 × 450.

So `RemoteLoader.SendXaml`'s existing split (PLAN.md: "don't rebuild it") works unchanged on
Avalonia 12. Milestone 1 has to *reach* it, not repair it.

## Fixture upkeep

`AvaloniaRider\testData\solutions\MultiProjectSolution` **does not compile** — `MyControl` is missing
`partial`, `Program.cs` calls `LogToDebug()` (gone in 12; now `LogToTrace()`), and `App.xaml`
references `Avalonia.Themes.Default` (a 0.10-era package that does not exist for 12). AvaloniaRider
evidently never builds it.

Rather than modify a reference checkout, a working copy lives at
**`E:\Projects\dotnet\AvaloniaPreviewer\fixtures\MultiProjectSolution`** with those three fixed and
`Avalonia.Themes.Fluent` added. It builds clean. `TargetFramework` deliberately stays in
`Directory.Build.props` — that is the Milestone 1 case. `AvaloniaMvvm` needed no such treatment and
is still used in place.

## Two AvantGarde defects fixed to make the verification possible

Both are real user-facing bugs found by trying to drive the app, not test scaffolding.

1. **`-s` / `--select` was ignored whenever the argument was a `.sln` or `.csproj`.**
   `MainWindow.OnOpened` returned early on `PathKind.Solution`, so the documented option worked only
   when a file *within* a project was passed. That excluded the case where it matters most: a library
   item needs the whole solution open for its app project to resolve. Now applied in both branches.

2. **`ProjectTree.SelectedItem` could not select anything below the top level.** The setter guarded
   on `_treeView.Items.Contains(sel)`, and `Items` holds only the project nodes, so every file item
   failed the test and the assignment was silently skipped. Replaced with a recursive `ContainsItem`.

   The second defect is why the log for the successful `AvaloniaMvvm` run still shows
   `Contains: False` — that preview arrived through a *later* tree rebuild restoring `IsSelected`,
   not through the programmatic selection. Worth knowing: this is the `Views/ProjectTree.cs`
   TreeViewItem-as-data-item design PLAN.md Milestone 5 wants rewritten, and it is not merely a
   performance concern — it silently loses state.

## Milestone 0 verdict

| Fixture | Branch | Goes to |
|---|---|---|
| `AvaloniaMvvm`, restored + built | **Frames arrive** | nothing; already works |
| `MultiProjectSolution`, unmodified | `TargetFramework` invisible → assembly not found | **Milestone 1** |
| `AvaloniaMvvm`, unrestored (Part 1) | `FileNotFoundException` from `FindDesignerHost` | **Milestone 1** |
| wrong host TFM (Part 1, hand-launched) | fatal `System.Runtime` load failure | **Milestone 1** |

Every branch that occurred points at Milestone 1. Nothing observed points at Milestone 2 — the
protocol path is sound where it is reached — which confirms the ordering the plan and Part 1
proposed. Milestone 2 remains worth doing (the dropped `RequestViewportResizeMessage` above is a real
silent-drop, and the verbatim resend is still armed), but it is not what blocks users on Avalonia 12.
