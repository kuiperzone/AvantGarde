# Milestone 4 item 6 — Build on demand

AvantGarde never invoked MSBuild for anything but property evaluation, so a project the user had not
pre-built showed `"<Config> assembly not found"` and nothing more. There is now a **Build** button on
that error, and only on that error.

Measured end to end against both fixtures, driven through UI Automation rather than by hand (see
"How it was driven" below).

## What shipped

- **`Projects/ProjectBuilder.cs`** — shells `dotnet build "<proj>" --nologo -p:Configuration=<kind>`
  using the same process plumbing as `MsBuildEvaluator` (shared `GetDotnetPath`, same
  `MSBUILDTERMINALLOGGER=off` and telemetry opt-out), but streaming output line by line through an
  `Action<string>` instead of reading it at the end. `ProjectBuildResult` carries success, a short
  message, the first MSBuild `error` line, and everything captured.
- **`ProjectError.IsBuildable`** → **`PreviewError.CanBuild`** → `PreviewControlViewModel
  .HasBuildAction` → the button, mirroring the existing `HasErrorLocation`/"Goto ..." chain exactly.
  Both buttons now share a `StackPanel` so they can coexist.
- **`MainWindow.BuildProject`** — stops the host, suspends the preview, shows
  `"Building <Project>..."`, runs the build on a worker, streams its output to the OUTPUT pane, and
  on failure opens that pane. `PreviewPane.ShowOutput()` and `XamlCodeControl.ShowOutput()` are new.

## Three things measurement decided

**1. The recovery path already existed, and writing a second one produces "Please wait...".**

`UpdateLoader` refuses to preview while `BuildWatcher.Elapsed <= RefreshInterval`, and a build resets
that clock by definition. So an eager `IsPreviewSuspended = false; UpdateLoader(...)` at the end of
the build lands on the `"Please wait..."` branch, not on a preview. `RefreshTimerHandler` already
implements "a build just happened" — suspend on change, clear once the output directory goes quiet —
because `BuildWatcher` was built for builds started *outside* AvantGarde. `BuildProject` therefore
ends with `ExplorerPane.Refresh(true)` and nothing else; the timer does the rest. Confirmed: the
preview returns with no manual restart.

**2. The refresh timer has to be held off for the duration of the build.**

The converse of the above. Between the click and MSBuild's first write, `Elapsed` is still large, so
the timer's un-suspend branch fires *during* the build and starts a designer host against the
assembly being replaced. `RefreshTimerHandler` now returns immediately while `_isBuilding`. The
watcher's `_changed` flag survives that, so the normal suspend/quiet/restart sequence runs as soon as
the flag clears.

**3. `ShowOutput` cannot gate on the code view being viewable.**

The obvious guard — only open the split when `IsXamlViewable` — is dead code at the one moment it
matters. `"Building <Project>..."` is a `LoadPayload(ProjectError)` with `ItemKind == AnyFile`, so
while it is displayed the code view is *not* viewable and the OUTPUT pane the failed build needs is
hidden. `ShowOutput` now sets the flag unconditionally; `ResetSplitter` opens the row when the next
XAML-bearing payload arrives, which is the one the timer sends moments later. Verified by breaking
`ViewLocator.cs` in the `AvaloniaMvvm` fixture: the compiler diagnostics appear in an OUTPUT pane
that opens by itself, with the Build button still there for a retry.

## Output ownership

The OUTPUT pane takes its text from `PreviewPayload.Output`, which is the *host's* output and is
empty until a host starts. Every payload during and after a failed build would therefore wipe the
build log — the only account of what went wrong. `PreviewReadyHandler` reasserts the build text
whenever the incoming payload has none. It is a reassert, not a merge: the host's output supersedes
it the moment there is any, which is visible in the retry screenshot where the pane switches from
compiler errors to `Initializing application in design mode`.

The reassert needs a matching end, or a log with no host to displace it follows the user to whatever
they select next. `ClearBuildOutput` runs at the start of a build, on the first line of host output,
and on opening or closing a solution.

## The pane was scrolled off the message

`XamlCodeControl.OutputText` tail-followed with `CaretIndex = int.MaxValue`. That is fine for host
output, whose lines are short, but MSBuild diagnostics begin with an absolute project path, so
scrolling to the *end* of the last line put `error CSxxxx` off the left edge — the pane opened by
itself and showed a path where the reason should be. The caret now goes to the start of the last
line: vertical tail-following is unchanged, horizontal scrolling is gone. Host output benefits too.

Caught only by looking at the screenshot. Grepping the trace confirmed the diagnostics were
*captured*, which is a different claim from *readable*.

## Deliberately not done

- **The app project is never built implicitly.** The button builds the project that reported the
  error, which is always the selected item's project. Where the *app* assembly is the missing one,
  `LoadPayload` surfaces no `ProjectError` at all — `GetApp()` returns a project regardless of
  whether its assembly exists — and the failure appears later, from `RemoteLoader`. That gap predates
  this item and is left alone. In the fixture the point is moot: `AvaloniaApp1` references
  `ClassLibrary1`, so building either from its own error is enough.
- **No button on a custom assembly path.** `CheckForError`'s missing-assembly branch also fires for a
  `ProjectProperties.AssemblyOverride` that points nowhere, and building cannot put a file where the
  user pointed. `IsBuildable` is therefore `!_customOverride`, so that case keeps the error and loses
  the button.
- **No "Project not restored" affordance.** `dotnet build` restores, so extending the button there is
  a one-line change. The plan attaches it to the missing assembly; the restore error is one the user
  is told to fix themselves, and widening it was not asked for.
- **No View-menu entry.** Item 6 asks for an affordance on the error.
- **Nothing cancels a running build.** The button disables itself and the window stays responsive,
  but a wedged MSBuild runs to `ProjectBuilder.Timeout` (10 minutes — deliberately not
  `MsBuildEvaluator.Timeout`, which is a 30 s guard sized for a sub-second property evaluation).

## Verification

`dotnet build AvantGarde.sln` clean (0 warnings, Debug *and* Release), `dotnet test AvantGarde.Test`
92/92 — eight new facts: five over `ProjectBuilder` (including a real `Release` build asserting the
configuration reaches MSBuild, and a real failing build asserting the diagnostic is captured), two
over the `ProjectError` → `PreviewError` chain, one over `CheckForError` covering both the buildable
missing assembly and the non-buildable override.

Both fixtures, with `bin/` removed and `obj/` left in place so the error is the missing assembly
rather than a missing restore:

- `AvaloniaMvvm` → `Views/MainWindow.axaml`: "Debug assembly not found" + Build → click → build runs,
  preview appears unaided.
- `AvaloniaMvvm` with a deliberate C# syntax error: build fails, OUTPUT opens showing `CS1040`/`CS1002`,
  error and button remain; fixing the source and clicking again recovers.
- `MultiProjectSolution` → `ClassLibrary1/MyControl.axaml` with only `ClassLibrary1/bin` removed:
  Build → the control renders through `AvaloniaApp1`, exercising the two-assembly path.

## How it was driven

`AvantGarde.exe <sln> -s=<leaf>` opens and selects, as before. Clicking without a human is new: the
Avalonia window exposes UI Automation on Windows, so PowerShell with `UIAutomationClient` finds the
button by its `Name` ("Build") and invokes its `InvokePattern`. That plus the existing
`PrintWindow` + `PW_RENDERFULLCONTENT` screenshot makes the whole path scriptable.
