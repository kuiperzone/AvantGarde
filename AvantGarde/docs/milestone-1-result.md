# Milestone 1 result — MSBuild-driven discovery

Date: 2026-07-28. Implements [PLAN.md](PLAN.md) Milestone 1, ordered here by the three discovery
failures [milestone-0-result.md](milestone-0-result.md) put on record rather than by the plan's file
list.

**Outcome: complete.** The unmodified `MultiProjectSolution` fixture — the acceptance test — resolves
all four projects and previews `ClassLibrary1/MyControl.axaml` through the two-assembly path. Build
clean in Debug *and* Release, 0 warnings. Tests **64/64**, up from 48.

## What replaced what

`dotnet msbuild <proj> -getProperty:…` is now the primary source for project layout. The project XML
parse is retained strictly as the fallback for a project that has not been, or cannot be, evaluated —
not as a fast path that MSBuild later overrides, which PLAN.md warns would race and flicker on open.

New: `Projects/MsBuildEvaluator.cs` (process + JSON parse) and `Projects/MsBuildResult.cs`.
Query set: `AvaloniaPreviewerNetCoreToolPath`, `TargetPath`, `TargetFramework`, `OutputType`,
`ProjectAssetsFile`.

| Was | Now |
|---|---|
| `TargetFramework` read from the project XML | evaluated — the fixture's lives in `Directory.Build.props` |
| assembly found by walking `bin/`, `artifacts/`, RID directories, guessing between layouts | `TargetPath`, stated outright. The walk survives as fallback |
| designer host = `<nugetRoot>/avalonia/<version>/tools/**` recursive first match | `AvaloniaPreviewerNetCoreToolPath`. `FindDesignerHost` survives as fallback |
| `NUGET_PACKAGES` env var only | env → `dotnet nuget locals global-packages --list` → `~/.nuget/packages`, cached |
| unrestored project → `FileNotFoundException` or "assembly not found" | **"Project not restored — Run 'dotnet restore' on the project"** |

## Three facts found by probing, worth keeping

1. **The MSBuild property `AvaloniaVersion` is a trap.** A project referencing Avalonia **12.0.5**
   evaluates it to **`11.0.2`** — it is a stale literal in Avalonia's own props and says nothing
   about the referenced package. It is deliberately *not* in the query set, and there is a comment
   in `MsBuildEvaluator` saying so. `DotnetProject`'s XML parse of `PackageReference Include="Avalonia"`
   remains the source of that version, and only feeds the fallback host lookup.

2. **A single `-getProperty` returns the bare value, not JSON.** MSBuild only emits the
   `{"Properties":{…}}` payload for two or more. `Evaluate` throws on fewer rather than carrying two
   parse paths.

3. **`AvaloniaPreviewerNetCoreToolPath` needs normalizing.** Avalonia's props builds it by
   concatenation: `D:\NugetCache\avalonia\12.0.5\buildTransitive\\..\tools\net8.0\designer\…`.
   Valid, but neither comparable nor presentable — hence `MsBuildEvaluator.NormalizePath`.

An empty tool path is ambiguous on its own: it means *either* unrestored *or* not an Avalonia
project. `ProjectAssetsFile` disambiguates — the property is defined either way, so the file's
existence on disk is the evidence. `ConsoleApp1` in the fixture (restored, no Avalonia) and an
unrestored `AvaloniaMvvm` copy are the two live cases, and they now report differently.

## Cost, and why it is off the refresh path

Cold evaluation measured **~0.6 s per project**; the four-project fixture takes ~2.1 s sequentially,
and runs in parallel in the app. That is far too slow for `Refresh()`, which the UI calls every
second, so:

- `DotnetProject.Evaluate()` is blocking and documented as worker-thread only.
- `BeginEvaluation()` marks projects on the UI thread *before* the work is queued, so the tree can
  show **"Resolving project..."** from the moment it is queued rather than briefly showing the stale
  XML-derived answer. That ordering is the whole point of the two-method split.
- `UpdateLoader` defers previewing while an evaluation is in flight, rather than starting the
  designer host against values about to be superseded and restarting it a second later. Verified: one
  host start per open, where the first cut had two.
- Results are cached per project and invalidated by a stamp over the project file, every
  `Directory.Build.props` / `Directory.Packages.props` above it up to the solution, and the build
  configuration. The 1 s timer re-triggers evaluation when that stamp moves.

**Evaluation writes nothing.** Measured by timestamping the fixture tree before and after: no file
changes, so it cannot trip `BuildWatcher`.

**Only one batch runs at a time**, enforced in `DotnetSolution.BeginEvaluation` by refusing to start
while `_pending` is non-empty. Without that, a stamp moving *during* a batch strands a project: the
running batch copied its own list before the second `BeginEvaluation` marked the project, then clears
the shared list on exit, leaving that project flagged as evaluating with nothing left to clear it —
permanently "Resolving project...", and never evaluated again for the session. Exercised by touching
a `.csproj` and `Directory.Build.props` every 200 ms across the whole in-flight window: three
evaluation batches started, three completed, and the tree settled correctly with nothing stuck.

## Also in this milestone

- **`.slnx`** — recognised as a solution kind, added to the file picker and the `FindArtifactsDirectory`
  walk terminator (`*.sln*`), and actually **parsed**: `DotnetSolution.ReadProjectsInXmlSolution`
  reads `Project Path=` at any depth, since `.slnx` nests projects inside `Folder` elements. Listing
  the extension without parsing it would have produced an empty solution.
- **`FindDesignerHost` made 12-correct** — resolves `net8.0`, then `net10.0`, then `netstandard2.0`
  explicitly, instead of depending on `NodeItem.FindFile` traversal order. Milestone 0 measured that
  the old order landed on `net8.0` by accident; this makes it deliberate. A recursive search remains
  for a layout newer than anything anticipated.
- **`GetInstalledAvaloniaVersions` sorts semantically.** An ordinal sort put `11.3.12` below `11.3.2`.
- **Port race narrowed.** `GetFreePort` closes its probe listener before `BsonTcpTransport` binds, so
  the port can be taken in between. Not fixable outright — the transport does not report the port it
  bound — so `StartListenerNoSync` retries up to five times instead of failing the preview.
- **The Avalonia-version combo is demoted, not removed** (PLAN.md asks for an explicit decision). It
  still feeds the fallback, which is the only escape hatch when MSBuild cannot run, so it keeps its
  effect; its tooltip now says it normally has none.

## Verification

1. `dotnet build AvantGarde.sln` — 0 warnings, 0 errors, Debug **and** Release.
2. `dotnet test AvantGarde.Test` — **64/64**. New: `MsBuildEvaluatorTest` (10) and `DotnetSolutionTest`
   (3), plus 3 in `DotnetProjectTest`. Several run MSBuild for real against scratch projects, which
   needs no restore and no network — `TargetFramework` from `Directory.Build.props`, configuration
   honoured, missing project reported, unrestored project detected.
3. **Acceptance — `MultiProjectSolution`, unmodified**: was four × "Debug assembly not found"; now
   `AvaloniaApp1`, `AvaloniaApp2` and `ClassLibrary1` all resolve to their `bin\Debug\net8.0` output,
   and `ConsoleApp1` correctly reports "Avalonia Package not found".
4. **Two-assembly path, unmodified fixture**: `ClassLibrary1/MyControl.axaml` with `AvaloniaApp1` as
   the app project renders "My User Control". Host from MSBuild, `UpdateXamlMessage.AssemblyPath` =
   `ClassLibrary1.dll`, frames at 800 × 450. Milestone 0 could only get here by temporarily inlining
   `TargetFramework`.
5. **Regression — `AvaloniaMvvm`**: still renders; host now resolved from MSBuild rather than the
   cache walk, arriving at the identical path.
6. **Unrestored** — a copy of `AvaloniaMvvm` with `obj/` and `bin/` deleted reports
   **"Project not restored"** in the tree and the preview pane.

## Not fixed, deliberately

Observed during this work; none blocks Milestone 1, all worth knowing.

- **`XamlFileProjectPath` has mixed separators** — `/Views\MainWindow.axaml`. Renders correctly on
  both fixtures, so it is tolerated rather than changed silently. First suspect if an asset
  resolution bug ever appears.
- **The dimension label reads `NaN`** for any control without an explicit `Width`/`Height`.
  `PreviewFactory` parses `d:DesignWidth`/`d:DesignHeight` into separate fields the label ignores.
  Pre-existing; Milestone 4 item 1 rewrites this area anyway.
- **One spurious `BuildWatcher` restart per open.** The watcher's first poll always reports a change,
  so every opened preview is suspended and restarted once. Confirmed present before this milestone
  and unchanged by it — a Milestone 5 item (polling → real watchers).
- **The cache stamp stops at the solution directory.** `GetEvaluationStamp` walks up from the project
  to the solution root, so a `Directory.Build.props` *above* the solution never invalidates the
  cache. Rare, and the walk is capped at eight levels anyway; reopening the solution picks it up.
- **`ProjectTree` still uses `TreeViewItem` as data items.** Two of its failure modes were fixed here
  and in Milestone 0 (nested selection could not be set; selection was dropped on rebuild), but the
  design PLAN.md Milestone 5 wants rewritten is intact.
