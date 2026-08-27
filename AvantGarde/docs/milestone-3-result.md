# Milestone 3 result — AvantGarde itself on Avalonia 12

Date: 2026-07-28. Done out of plan order, at the user's direction (PLAN.md sequences this after
Milestones 1–2). Nothing in it depends on those, and it is independently verifiable.

**Outcome: complete.** AvantGarde builds and runs on **Avalonia 12.1.0 / net10.0**, 0 warnings,
0 errors, 48/48 tests green, app launches and renders correctly.

## Baseline recorded before starting

11.3.2 / net9.0: build clean (0 warnings), tests **48 passed / 0 failed**. Same counts after, so the
migration is regression-free against existing coverage. (Coverage of `Loading/` is still zero — see
PLAN.md Milestone 5.)

## Step 1 — ReactiveUI kept, on the renamed package

**PLAN.md's step 1 says to drop ReactiveUI. That was not done — it was kept, at the user's direction.**

The package was **renamed**: `Avalonia.ReactiveUI` → **`ReactiveUI.Avalonia`**. The old ID stops at
11.3.8, which is why a search for the old name suggested no 12.x existed. `ReactiveUI.Avalonia`
12.1.0 is current. (An earlier revision of this document asserted the drop was mandatory because no
12.x existed. That was wrong, and is corrected here.)

PLAN.md's *other* argument for dropping still stands on the facts: usage is only `ReactiveObject` as
a base plus `RaisePropertyChanged` / `RaiseAndSetIfChanged`, with no `WhenAnyValue`,
`ReactiveCommand`, `IViewFor` or any `System.Reactive` surface anywhere in the solution. The removal
was implemented and verified green (build clean, 48/48, no ReactiveUI/Splat/Rx assemblies in output)
before being reverted, so dropping it remains a known-good option if it is ever wanted.

### What the upgrade required

`ReactiveUI.Avalonia` 12.1.0 brings **ReactiveUI core 24.0.0** (plus `ReactiveUI.Primitives` 7.1.0,
`Splat` 20.2.0). Despite the major-version jump, `ReactiveObject`, `RaiseAndSetIfChanged` and
`RaisePropertyChanged` are unchanged — every view model compiled untouched. Two changes only:

1. **Namespace**: `using Avalonia.ReactiveUI;` → `using ReactiveUI.Avalonia;` (`Program.cs`).
2. **`UseReactiveUI()` now requires a builder action.** The parameterless overload is gone; the
   signature is `UseReactiveUI(Action<ReactiveUIBuilder>)`. `Program.BuildAvaloniaApp` now calls
   `.UseReactiveUI(builder => builder.WithAvalonia())` — `WithAvalonia()` registers the Avalonia
   platform services, and no view registration is needed since nothing implements `IViewFor`.

## Step 2 — Clowd.Clipboard dropped

PLAN.md predicted this package would "force an assembly-version conflict across the whole build". It
did not — the Avalonia-11-built package restored and loaded fine on 12. It was kept in place through
the bump precisely so the build could answer that question rather than a pre-emptive removal hiding it.

It was removed for a better reason: **Avalonia 12 makes it redundant.**
`Avalonia.Input.Platform.ClipboardExtensions.SetBitmapAsync(IClipboard, Bitmap)` puts a bitmap on the
clipboard cross-platform, which was Clowd's entire purpose. `PreviewPane.CopyToClipboard` collapsed
from a Windows `RuntimeInformation` branch plus a `MemoryStream`/`DataObject` fallback down to one
call. That also disposed of the obsolete `DataObject` and the obsolete `Bitmap.Save(Stream, int?)`.

## Step 3 — The bump

- `net9.0` → **`net10.0`** in both projects.
- All `Avalonia*` packages → **12.1.0**.
- **`Directory.Packages.props`** added at repo root (CPM). Note it is repo-wide: every
  `PackageReference` in *both* projects lost its `Version`, test packages included.
- **`Avalonia.Remote.Protocol` now referenced explicitly** rather than arriving transitively, so the
  protocol client version is pinned deliberately. This matters for Milestones 2 and 4.
- **`Avalonia.Diagnostics` dropped — no 12.x exists** (latest 11.3.18). Unlike ReactiveUI this is
  *not* a rename: `Avalonia.DevTools`, `DevTools.Avalonia` and `Diagnostics.Avalonia` are all
  unregistered, and nothing diagnostics-related ships in `avalonia/12.1.0/lib/`. The four
  `#if DEBUG this.AttachDevTools();` calls went with it.

  **This is a real capability loss: DevTools is no longer available in Debug builds.** Third-party
  fills exist on nuget (`AvaDiagnostics12`, `AvaDevTools`) but none is official and none has been
  vetted — adopting one into a GPL-3 codebase is a deliberate decision, not a migration step.

## Step 4 — Actual API fallout (far smaller than predicted)

Only three compile breaks across the whole app:

| Break | Fix |
|---|---|
| `GotFocusEventArgs` not found | Renamed to `FocusChangedEventArgs`, now serving both GotFocus and LostFocus. `Views/CodeTextBox.cs` |
| `IClipboard.SetTextAsync` / `SetDataObjectAsync` gone; `DataObject` obsolete → `DataTransfer` | Clipboard helpers moved to `Avalonia.Input.Platform.ClipboardExtensions`; added the using, switched to `SetBitmapAsync`. `CodeTextBox.cs`, `PreviewPane.axaml.cs` |
| `Window.SystemDecorations` obsolete | → `Window.WindowDecorations` / `WindowDecorations.None`. `Views/PreviewControl.axaml.cs` |

Also fixed: `app.manifest` identified the app as `AvaloniaTest.Desktop`; now `AvantGarde.Desktop`.

### The protocol client compiled untouched — and that matters for Milestones 1–2

`RemoteLoader.cs` needed **no changes at all** against `Avalonia.Remote.Protocol` 12.1.0:
`BsonTcpTransport`, `IAvaloniaRemoteTransportConnection`, `UpdateXamlMessage`, the whole
`MessageHandler` dispatch. PLAN.md fact #19 says the protocol is wire-identical between 11.3.2 and
12.1.0; this shows the **managed API surface** is unchanged too. It is the strongest evidence so far
that Milestones 1 and 2 are a discovery/sequencing problem, not a protocol rewrite.

(The three uncommitted Milestone 0 diagnostic changes in that file were already present and are
unaffected by the bump.)

### PLAN.md's predicted fallout that did **not** materialise

Each of these was listed as expected breakage. All were verified working:

- **`App.axaml` `FluentTheme.Palettes` / `ColorPaletteResources`** — unchanged, and the custom
  light/dark palettes render correctly.
- **`Markup/MarkupDictionary.cs`** — the static ctor reflecting `Assembly.LoadFrom` over
  `Avalonia*.dll` for `XmlnsDefinitionAttribute` was expected to throw `TypeInitializationException`.
  It initialises cleanly against Avalonia.Base 12.1.0. `MarkupDictionaryTest` and
  `SchemaGeneratorTest` — the only signal that it broke — both pass.
- **`AvantWindow.ScaleSize()`**, `MainWindow.OnOpened` sizing — window opens at a sane size.
- Hardening `MarkupDictionary` against failure and caching it is still worth doing (PLAN.md
  Milestone 3 step 4), but it is now an improvement, not a fix.

## Verified how

1. `dotnet build AvantGarde.sln` — 0 warnings, 0 errors. Also clean in **Release**, which matters
   because the dropped `Avalonia.Diagnostics` reference was `Condition`-ed on Debug.
2. `dotnet test AvantGarde.Test` — 48/48 passed on net10.0.
3. Launched `AvantGarde.exe`, confirmed the process stays up and `Responding=True`, and captured the
   window: menus, toolbar assets, themed accent colours, welcome pane, status bar and zoom control
   all render correctly.

Capture note: `Graphics.CopyFromScreen` returns an all-black frame for this window (GPU-composited).
`PrintWindow` with `PW_RENDERFULLCONTENT` (flag `2`) works. Worth knowing before anyone reads a black
screenshot as a rendering regression.

## Not verified — needs hands-on UI

Runtime paths no automated test or launch check exercises:

- **Copy-to-clipboard** (`PreviewPane.CopyToClipboard` → the new `SetBitmapAsync`). Compiles and is
  the documented API, but the actual clipboard write is untested, on any platform.
- **`PreviewControl.GetBitmap()`** — still shows a real minimized undecorated `Window` to rasterize.
  It compiles against `WindowDecorations`, but whether it still produces a correct bitmap on 12 is
  unknown, and it is the input to the clipboard path above. PLAN.md wants it replaced with off-screen
  composition regardless.
- **`AppSettings.AppFontSize`** mutating `_app.Resources["ControlContentThemeFontSize"]` — flagged
  in-code as a Fluent-specific hack. Nothing proved it still takes effect.
- **Linux / macOS** — everything above was verified on Windows only.

## Packaging

`AvantGarde.pupnet.conf` and `publish.sh` were checked and pin no target framework
(`DotnetPublishArgs` passes only version/self-contained/debug flags), so the net9.0 → net10.0 move
does not break packaging. `CHANGES` and `ReleaseNotes.md` still say ".NET 9 / 11.3.2" — correct as
history for the shipped v1.6.0, and deliberately left alone. A new entry is a release decision.
