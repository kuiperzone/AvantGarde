# AvantGarde → best-in-class Avalonia 12 XAML previewer

## Context

`E:\Projects\dotnet\AvaloniaPreviewer\AvantGarde12` is a checkout of kuiperzone/AvantGarde v1.6.0 — a standalone, IDE-agnostic Avalonia XAML previewer built on Avalonia **11.3.2 / net9.0**. It works, but it is pinned to Avalonia 11 conventions and leaves most of the Avalonia remote-designer protocol unused.

Two reference implementations sit alongside it (both MIT, read for ideas only): `AvaloniaRider` (Kotlin/C#, actively green against Avalonia 12.0.5) and `AvaloniaVSCode` (branch `ARCHIVE`; upstream `main` is now a stub pointing at the paid Avalonia Pro).

Goal: make AvantGarde the best previewer available — correct on Avalonia 12, with the protocol features neither reference implementation ships. Decisions taken: **single GPL-3.0-or-later codebase**, **Avalonia 12.x user projects only**, **standalone app working on 12 is milestone one**.

---

## Verified facts this plan rests on

Established by reading the packages in `D:\NugetCache` (the machine's `NUGET_PACKAGES`), not assumed:

| Fact | Consequence |
|---|---|
| `Avalonia.Remote.Protocol` **12.1.0 is wire-identical to 11.3.2** — same message set, same GUIDs; `KeyEventMessage` gained `PhysicalKey`/`KeySymbol` | The migration is *not* a protocol rewrite. Biggest de-risking fact in the plan. |
| Designer host layout **changed**: 11.x = `tools/netstandard2.0/designer/` + `tools/net461/designer/*.exe`; 12.x = `tools/net8.0/designer/` **and** `tools/net10.0/designer/`, no netstandard2.0, no net461 | `RemoteLoader.FindDesignerHost` recursive-first-match now has **two candidates** and can pick `net10.0` for a net8.0 app → `dotnet exec` fails. This is the concrete break. |
| `Avalonia.props` (12.1.0) sets `AvaloniaPreviewerNetCoreToolPath` → `tools/net8.0/...`; `AvaloniaPreviewerNetFullToolPath` **removed** | Prefer `net8.0` (net8 IL rolls forward onto net10 runtime; the reverse fails). Mirror Avalonia's own default. |
| Host CLI in 12.1.0 unchanged: `--transport`, `--method`, `--html-url`, `--session-id`; only `tcp-bson` transport; only `avalonia-remote` and `html` methods | Existing launch command line stays valid. `--session-id` is available and unused. |
| `Avalonia.DesignerSupport` 12.1.0 exposes only `RemoteDesignerEntryPoint` / `DesignWindowLoader` / HTML transport — **no hit-test, selection, or source-position surface** | Element picker / preview→source navigation is **not expressible** on the stock host. Deferred (see below). |
| ~~**Host side genuinely consumes the unused messages.**~~ **WRONG for the viewport messages, TRUE for the input ones — see [milestone-4-result.md](milestone-4-result.md) and [milestone-4-input-result.md](milestone-4-input-result.md).** `Avalonia.Controls.Remote.Server` (in `Avalonia.Controls.dll` 12.1.0) *references* `ClientViewportAllocatedMessage`, `MeasureViewportMessage`, `KeyEventMessage`, `TextInputEventMessage`, `ScrollEventMessage`, `PointerMoved/PressedEventMessage`. `Avalonia.DesignerSupport` additionally names `RequestViewportResizeMessage`. | This was a **metadata scan**: it shows the type is referenced, **not** that the fields are acted on. Measured against the 12.0.5 host, `ClientViewportAllocatedMessage.Width/Height` is **ignored** and `MeasureViewportMessage` is **never answered**; only the allocation message's DPI has effect, and it duplicates `ClientRenderInfoMessage`. The client **cannot** tell the host how large to render. Input messages are untested — do not assume this row for them either. |
| Host binary contains the literal `Sending StartDesignerSessionMessage`; AvantGarde never handles that message and sends XAML on TCP-accept instead | Causes *no frames after a successful connect* — **not** a `SpinWait` timeout (see Milestone 0 diagnostic tree). |
| **`RemoteLoader.cs:463` never passes `--method`.** It relies on the host default; AvaloniaRider and AvaloniaVSCode both pass `--method avalonia-remote` explicitly, and the host's own usage example does too | If 12.x changed that default, `HtmlTransportStartedMessage` arrives, `MessageHandler` silently drops it, and no frame ever comes. One-string fix, removes a whole misdiagnosis branch. |
| `dotnet msbuild <proj> -getProperty:...` returns clean JSON for `TargetPath`/`TargetDir`/`TargetName`/`TargetFramework`, but `AvaloniaPreviewerNetCoreToolPath` came back **empty** on an unrestored project (`obj/` absent → NuGet `.g.props` not imported) | MSBuild evaluation is the right spine, but **requires restore** and needs a fallback. |
| Avalonia 12 libs ship `net8.0` + `net10.0` only (netstandard2.0 dropped). SDK 10.0.302 is installed. | AvantGarde should move `net9.0` → **`net10.0`**. |
| Cache has no 12.x of `Avalonia.ReactiveUI` or `Avalonia.Diagnostics` — inconclusive about nuget.org, but AvantGarde uses **no** real ReactiveUI surface (no `WhenAnyValue`, `ReactiveCommand`, `IViewFor`; only `RaiseAndSetIfChanged`) | Drop ReactiveUI because the dependency is unused, not because 12.x is missing. |
| `AvaloniaRider\testData\solutions\{AvaloniaMvvm, MultiProjectSolution}` are real **Avalonia 12.0.5** fixtures; the latter has a `ClassLibrary1` | Ready-made test corpus, incl. the class-library-plus-host case. |

Note AvantGarde **already** has AvaloniaRider's two-assembly split: `RemoteLoader.SendXaml` sends `Load.ProjectAssembly` as `UpdateXamlMessage.AssemblyPath` while the CLI gets `Load.AppAssembly`. Don't rebuild it.

---

## Milestone 0 — Spike: prove a 12.x round-trip (do this first, change nothing)

Because the protocol is wire-identical, this runs on the **unmodified 11.3.2 build** — no refactoring.

Two changes first, both trivial and both preventing misdiagnosis:

- Add `--method avalonia-remote` to the argument string at `RemoteLoader.cs:463`.
- Add logging to the `catch { }` blocks in `RemoteLoader.InvokePreviewReady` / `InvokeOutputReceived` / `Dispose` — they otherwise swallow exactly the failures being hunted.

Then:

1. `dotnet restore` + `dotnet build` `AvaloniaRider\testData\solutions\AvaloniaMvvm`.
2. Run AvantGarde, open that `.csproj`, select `Views/MainWindow.axaml`.
3. Classify the outcome against this tree — the three branches have **different** causes and lead to different milestones:

   | Symptom | Cause | Goes to |
   |---|---|---|
   | `FileNotFoundException` from `FindDesignerHost` | 12.x `tools/` layout / version detection | Milestone 1 |
   | **Timeout at 10 s** (`SpinWait`) | Host process never launched or crashed at startup — wrong host TFM against the app's runtimeconfig, or missing deps. Check OUTPUT pane + process exit code. Cannot be a sequencing issue: `SpinWait.SpinUntil(() => v_connection != null)` is satisfied on TCP **accept**, before any message is exchanged. | Milestone 1 |
   | **Connects, then no frames** | `--method`, or XAML sent before `StartDesignerSessionMessage` | Milestone 2 |
   | Frames arrive | Migration is mostly cosmetic | Milestone 1 |

4. Repeat with `MultiProjectSolution` (`ClassLibrary1` XAML, `AvaloniaApp1` as host) to exercise the two-assembly path.

**Deliverable:** a short written note recording which branch occurred. Milestones 1 and 2 are ordered by it.

---

## Milestone 1 — Replace path-guessing with MSBuild-driven discovery

The single highest-leverage change: one mechanism retires five documented limitations (README-stated) at once — undetectable Avalonia version, `Directory.Packages.props`/CPM sniffing, non-standard output paths needing a manual assembly override, `NUGET_PACKAGES`-env-only cache lookup, and the new two-host-TFM ambiguity.

**New:** `AvantGarde/Projects/MsBuildEvaluator.cs`

- `Task<IReadOnlyDictionary<string,string>> EvaluateAsync(string projectPath, params string[] properties)` — shells `dotnet msbuild <proj> -getProperty:X -getProperty:Y -nologo`, parses the JSON `{"Properties":{...}}` payload.
- Query set: `AvaloniaPreviewerNetCoreToolPath`, `TargetPath`, `TargetDir`, `TargetName`, `TargetFramework`, `OutputType`, `AvaloniaVersion`.
- **Cache per project path**, invalidate on `.csproj` / `Directory.Packages.props` / `Directory.Build.props` write-time change. Cold evaluation is seconds — never on the edit path.
- If `AvaloniaPreviewerNetCoreToolPath` comes back empty (unrestored project — confirmed reproducible), surface an actionable *"Restore the project"* error rather than a `FileNotFoundException`, mirroring AvaloniaVSCode's "Build the project first" affordance in `commands/createPreviewerAssets.ts`.

**`AvantGarde/Loading/RemoteLoader.cs`**

- `FindDesignerHost(string? version)` → keep as the **fallback only**, and make it 12-correct: resolve `tools/net8.0/designer/Avalonia.Designer.HostApp.dll` explicitly, then `tools/net10.0/...`. Stop depending on `NodeItem.FindFile` traversal order.
- Resolve the NuGet root properly: `NUGET_PACKAGES` env → `dotnet nuget locals global-packages --list` → `~/.nuget/packages`. (Today's env-only lookup works on this machine only because the var happens to be set to `D:\NugetCache`; it silently breaks for anyone using `nuget.config` `globalPackagesFolder`.)
- Fix the `GetFreePort()` TOCTOU race — bind the `BsonTcpTransport` listener first and read the assigned port from it, instead of closing a probe listener and reopening on the same number.
- `GetInstalledAvaloniaVersions()` sorts version strings ordinally; switch to semantic comparison.

**Resolution order — one source of truth, no mid-session swap.** On project open, evaluate MSBuild **once**, asynchronously, with an explicit *"resolving project…"* state in the UI; cache the result. The existing XML / `Directory.Packages.props` parse in `DotnetProject.cs` is used **only when evaluation fails or the project is unrestored** — never as a "fast path" that MSBuild later overrides, which would race and visibly flicker on open.

**`AvantGarde/Projects/DotnetProject.cs`** — retain the XML parse as that fallback. Add `.slnx` to `DotnetSolution.AssertExtensions`, the file-picker patterns, and the `FindArtifactsDirectory` walk terminator.

**Retire or demote the Avalonia-version UI.** `LoadPayload.AppAvaloniaVersion`, `DotnetProject.GetAvaloniaVersion`, the Avalonia-version combo in `Views/ProjectWindow.axaml.cs`, and the persisted `AvaloniaOverride` setting exist *solely* to feed `FindDesignerHost`. Under the MSBuild spine they no longer determine anything. Decide explicitly: either mark them fallback-only (and label the combo as such) or remove them — don't leave the settings dialog offering a control with no effect.

---

## Milestone 2 — Protocol correctness

All in `AvantGarde/Loading/RemoteLoader.cs`, `MessageHandler`.

1. **Handle `StartDesignerSessionMessage`.** Gate the first `UpdateXamlMessage` on it (AvaloniaRider does; AvantGarde does not). Keep a timeout fallback that sends anyway, so a host that never announces still works.
2. **Delete the one-shot verbatim resend.** `factory.GetResendAndReset()` → `SendXaml(cnx, factory, false)` silently retries with *unprocessed* XAML and then shows a working preview built from different markup than the user configured. It will manufacture phantom bugs during this migration. If a fallback is wanted, make it explicit and visible in the status bar.
3. Replace the two-branch `if/else if` with a real dispatch that at minimum logs unhandled message types to the OUTPUT pane instead of `Debug.WriteLine`-and-drop.
4. Pass `--session-id` and validate it on `StartDesignerSessionMessage`, so a stale host from a previous run cannot be mistaken for the current one.

---

## Milestone 3 — Move the app itself to Avalonia 12

Order matters; each step is independently verifiable.

1. **Drop ReactiveUI** (do this while still on 11.3.2). Mechanical: `AvantViewModel` and friends derive from `ReactiveObject` but use only `RaiseAndSetIfChanged`/`RaisePropertyChanged`; commands are already plain methods bound via Avalonia's method-to-command binding. Replace with a small `INotifyPropertyChanged` base or `CommunityToolkit.Mvvm`; remove `.UseReactiveUI()` from `Program.BuildAvaloniaApp()`. Removes ReactiveUI + Rx from the critical path.
2. **Resolve `Clowd.Clipboard.Avalonia 1.1.4`** — one call site, `Views/PreviewPane.axaml.cs`, Windows-only branch. The non-Windows path already uses `TopLevel.Clipboard` with `image/png`. Test whether Avalonia 12's Windows clipboard handles it natively and delete the dependency; otherwise a minimal P/Invoke helper. An unmaintained Avalonia-11-built package will otherwise force an assembly-version conflict across the whole build.
3. **Bump.** `AvantGarde.csproj` + `AvantGarde.Test.csproj`: `net9.0` → `net10.0`; all `Avalonia*` packages → 12.1.0. Add an **explicit** `PackageReference` to `Avalonia.Remote.Protocol` (it arrives transitively today) so the protocol client's version is pinned deliberately. Introduce `Directory.Packages.props` — versions are currently duplicated inline across two projects. Verify `Avalonia.Diagnostics` (Debug-only) has a 12.x; drop the reference if not.
4. **Expected fallout, in likely order:**
   - `App.axaml` `FluentTheme.Palettes` / `ColorPaletteResources` customization — theme internals shift across majors.
   - `AppSettings.AppFontSize` mutating `_app.Resources["ControlContentThemeFontSize"]` (already flagged in-code as a Fluent-specific hack).
   - `AvantWindow.ScaleSize()` manual `Width`/`Height`/`Min*`/`Max*` scaling in `OnOpened`, plus `MainWindow.OnOpened` setting size *before* `base.OnOpened`.
   - `Markup/MarkupDictionary.cs` — static ctor reflecting `Assembly.LoadFrom` over `Avalonia*.dll` for `XmlnsDefinitionAttribute`; throws `TypeInitializationException` on any change, and `GlobalModel.Avalonia` touches it early. Make failure non-fatal and cache the result.
   - `PreviewControl.GetBitmap()` — shows a real minimized undecorated `Window` purely to rasterize it. Replace with off-screen composition of the `PreviewControl` visual.
   - `app.manifest` still identifies as `AvaloniaTest.Desktop`.

No API archaeology needed: targeted greps found **zero** hits for `IStyle`, `AvaloniaLocator`, `IBitmap`, `PlatformImpl`, `IControl`, `SelectionModel`, `ItemsRepeater`, `DataGrid`, `OnPlatform`, or trimming/AOT settings. `AssetLoader.Open`, `StyleKeyOverride`, `TopLevel.GetTopLevel`, `StorageProvider`, `ItemsSource`, `ThemeVariant` are all already the current forms.

---

## Milestone 4 — The features that beat both references

Every message below is in the 12.1.0 protocol assembly **and** is referenced by `Avalonia.Controls.Remote.Server` host-side (both verified by reflection/metadata scan — see facts table), yet unused by AvantGarde. Items 1 and 2 are unimplemented in **both** AvaloniaRider and AvaloniaVSCode.

1. ~~**Viewport negotiation → real fit-to-window.**~~ **DONE, by a different mechanism — see [milestone-4-result.md](milestone-4-result.md).** The premise here is false: the host ignores `ClientViewportAllocatedMessage.Width/Height` and never answers `MeasureViewportMessage`, so there is no negotiation to have and neither message is sent. What shipped instead: consume `RequestViewportResizeMessage`, derive the control's natural size from `FrameMessage`'s own pixel-size-and-DPI, and drive fit-to-window through `ClientRenderInfoMessage` DPI. Fit is **auto-scale, not reflow**.
2. ~~**Keyboard + text input forwarding.**~~ **DONE — see [milestone-4-input-result.md](milestone-4-input-result.md).** `KeyEventMessage` and `TextInputEventMessage` both work, and the facts table's "host consumes the unused messages" row *is* true here, unlike for the viewport messages. One precondition the plan does not anticipate: the host routes these to the guest's focused element, and a guest which has not been clicked has focused nothing, so **keyboard forwarding depends on pointer forwarding** and cannot be armed any other way. The `Meta → Windows` TODO is resolved.
3. ~~**Scroll forwarding.**~~ **DONE — same note.** `ScrollEventMessage` needs no prior click, being hit-test routed. Ctrl+Wheel zooms; the pane keeps the wheel while the preview overflows its viewport; otherwise the guest gets it.
4. **Zoom-as-DPI is already correct** (`SendScale` sets `ClientRenderInfoMessage.DpiX/Y = 96 * scale`, giving genuine re-render rather than bitmap upscale). Add: fit-to-window and pixel-perfect 1:1 entries alongside the hardcoded 25–400 % ladder in `ViewModels/PreviewOptionsViewModel.cs`; suppress DPI pushes while a XAML update is in flight (AvaloniaRider's interlock). — **Mostly done: fit-to-window and the interlock shipped; pixel-perfect 1:1 did not.** It conflicts with the zoom-as-DPI model, which fixes bitmap DPI at 96 so that zoom enlarges. See [milestone-4-result.md](milestone-4-result.md).
5. ~~**FPS limiting via delayed ack.**~~ **DONE — see [milestone-4-fps-result.md](milestone-4-fps-result.md).** The back-pressure premise is **true**, and stronger than stated: measured, the host stalls until each frame is acknowledged and does *not* queue while it waits, so a withheld ack throttles its rendering and can never show a stale bitmap. Unpaced, an animated control runs at 43 fps and 14 MB/s. But the caret this item was meant to bound blinks at ~2 fps, **under any useful cap** — so a second gate shipped alongside: `IsRenderPaused`, wired to window minimize, withholds the ack outright.
6. ~~**Build on demand.**~~ **DONE — see [milestone-4-build-result.md](milestone-4-build-result.md).** A Build button on the `"assembly not found"` error, `ProjectBuilder` shelling `dotnet build` with `MsBuildEvaluator`'s process plumbing. The premise held; what it does not say is that the *recovery* must be left to `RefreshTimerHandler`, which already implements "a build just happened" — restarting the preview from the build's own completion lands on `UpdateLoader`'s `"Please wait..."` branch, because a build resets the very `BuildWatcher` clock that guard reads.
7. ~~**Shadow copy (opt-in).**~~ **DONE — see [milestone-4-shadow-result.md](milestone-4-shadow-result.md).** The premise held: with the copy on, a rebuild while previewing produces no lock diagnostics at all against 12 with it off, and `BuildWatcher` no longer suspends the preview. Two things this does not say. The working directory subtlety resolves the other way — it is left at `MyDocuments`, pointed at *neither* directory, because a working directory is itself an open handle and AvantGarde resolves assets from the source tree regardless. And copying `TargetDir` wholesale is not viable: the fixture's output is 566 MiB, 533 MiB of it native code and symbols for platforms this machine cannot execute, so the mirror excludes foreign `runtimes/<rid>/` subtrees and native `.pdb`s to reach 33 MiB and 89 ms.
8. **Theme injection.** Splice `Design.DesignStyle` with a `RequestedThemeVariant` setter into the sent XAML to preview light/dark without touching the file, with the snippet user-editable in settings (AvaloniaRider's `previewer/ThemeInjection.kt`, StAX-based; port the approach to `PreviewFactory.ProcessXaml`, which already does grid-line injection, event stripping and asset prefetch).

---

## Milestone 5 — Internal debt (unblocks the above, do opportunistically)

- **`Views/ProjectTree.cs`** — uses `TreeViewItem` containers as data items (`ItemsSource = List<TreeViewItem>`, `PathItem ↔ TreeViewItem` cross-linked via `.Tag`), and rebuilds the whole tree on every 1 s refresh that reports a change. Rewrite with `HierarchicalTreeDataTemplate` + `ObservableCollection` + bound `IsExpanded`. Fixes both the virtualization fragility and the dominant idle cost.
- **Polling → watchers.** No `FileSystemWatcher` exists anywhere. Two independent 1 s pollers (`MainWindow._refreshTimer` doing a recursive `DirectoryInfo` walk on the UI thread, plus `BuildWatcher`'s own thread), a 100 ms caret timer in `PreviewPane`, a 1 s theme/font poll timer in **every** `AvantViewModel` instance. Replace with real change notification. Also: hash-based change detection is `HashCode.Combine(hashBase, LastWriteTimeUtc, Length)`, so a save with identical size and timestamp is invisible.
- **Tests.** `AvantGarde.Test` has ~35 facts and **zero coverage of `Loading/`**. Add before touching the pipeline: `PreviewFactory.ProcessXaml` golden files per `LoadFlags` combination; `AssetLocator.GetAssetFileName` for `resm:` / `avares://` / bare paths; `PointerEventMessage.ToMessage(scale)` coordinate math; `DotnetProject.FindTargetAssembly` across traditional and `artifacts/` layouts. `MarkupDictionaryTest` / `SchemaGeneratorTest` assert against the referenced Avalonia version and will need updating with the bump — they are the only signal that `MarkupDictionary` broke.
- `DotnetProject.RuntimeId` has no `osx-arm64` branch. The exported XSD is generated from AvantGarde's *own* Avalonia version, not the user's — regenerate from the target project's assemblies instead.

---

## Deferred, with reasons

- **Element picker / preview↔source navigation.** Avalonia 12 adds `AvaloniaXamlCreateSourceInfo` (embeds file/line/column into generated code), but reflection confirms the stock protocol has **no** hit-test, selection, or source-position message and `Avalonia.DesignerSupport` exposes no such surface. Requires a custom designer host, an injected side-channel assembly, or in-process rendering. Real differentiator, but its own project.
- **Hot reload.** Neither reference implements it; the refresh model is re-send-XAML or restart-host.
- **HTML render method** (`--method html`). Supported by the host and used by AvaloniaVSCode via an iframe; low value for a native app that already has a working bitmap path.
- **Visual Studio extension.** Reachable, but note the accepted constraint: on a single GPL-3 codebase the VSIX ships GPL-3 and cannot later be closed-source or dual-licensed. Keep the previewer core (`Loading/` + `Projects/`) free of `Views/` and `ViewModels/` references so it can be split into its own assembly when that milestone arrives — an architectural discipline, not a licensing one. `Avalonia.Ide.CompletionEngine` (an uninitialized submodule of AvaloniaVSCode, external repo, license unverified) would need its own review before any XAML-completion work.

---

## Verification

**Per milestone, against the two Avalonia 12.0.5 fixtures in `AvaloniaRider\testData\solutions`** (restore + build first — the `AvaloniaMvvm` fixture has no `obj/`, which is precisely why `AvaloniaPreviewerNetCoreToolPath` evaluated empty):

1. `dotnet build AvantGarde.sln` clean, zero new warnings.
2. `dotnet test AvantGarde.Test` green.
3. `AvaloniaMvvm` → `Views/MainWindow.axaml` renders a frame; edit + save updates it; introduce a XAML syntax error and confirm the message plus a working **Goto** button; fix it and confirm recovery.
4. `MultiProjectSolution` → a `ClassLibrary1` `UserControl` renders with `AvaloniaApp1` selected as host project (two-assembly path).
5. Rebuild the fixture while the preview is open — confirm the host releases the assembly lock and the preview returns without a manual restart.
6. Milestone 1: assert `MsBuildEvaluator` returns a non-empty `AvaloniaPreviewerNetCoreToolPath` post-restore, and that an **unrestored** project produces the actionable restore message, not `FileNotFoundException`.
7. Milestone 2: confirm from the OUTPUT pane that `StartDesignerSessionMessage` is received before the first `UpdateXamlMessage`, and that no silent verbatim resend occurs.
8. Milestone 4: resize the AvantGarde window and confirm the guest re-renders at the new viewport size (not a scaled bitmap); type into a `TextBox` in the preview and confirm characters appear — **click it first**, or the guest has nothing focused to type into; wheel-scroll a `ScrollViewer` inside the preview; minimize the window with an animated control previewing and confirm frames stop entirely, then restore and confirm they resume. An `Animation` with `IterationCount="INFINITE"` is the provocation to use for anything on the frame path — see [milestone-4-fps-result.md](milestone-4-fps-result.md) for the markup.
9. **Differential probe, SDK-only:** launch the designer host by hand with `--method html`, copying the `Exec` line from `D:\NugetCache\avalonia\12.1.0\build\AvaloniaBuildTasks.targets:212`, and open the URL in a browser. Proves host + fixture + `dotnet exec` all work with zero AvantGarde involvement. Running the fixture through AvaloniaRider is a last resort — it needs a Rider install plus a Gradle build of a Kotlin plugin.
10. A regression pass on an Avalonia 12 project of your own with a non-standard output path, to exercise the MSBuild path against something the fixtures don't cover.
