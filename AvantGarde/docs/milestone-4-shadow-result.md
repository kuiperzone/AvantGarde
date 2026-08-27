# Milestone 4 item 7 — Shadow copy

The designer host loads every assembly with `LoadFrom` and holds it open for its lifetime, so a
build of the project being previewed fights the preview for the output directory. That is the whole
reason `BuildWatcher` exists, why the preview comes down on every build started from an IDE, and why
`MainWindow.BuildProject` has to stop the host before shelling MSBuild.

The host now runs from a mirror of the build output instead. **Opt-in, off by default** —
Preferences → Shadow Copy.

Measured against both fixtures. The contrast is the point:

| Rebuild while previewing | MSBuild lock diagnostics | Preview |
| --- | --- | --- |
| Shadow copy off | 12 × `MSB3061 ... locked by ".NET Host"` | replaced, then restarted |
| Shadow copy on | none | never interrupted |

Note what the left column does *not* say. **Both builds succeeded.** With the copy off, MSBuild fails
to delete the previewer's assemblies, retries, and gets through only because `BuildWatcher` tears the
host down partway — the preview is the price of the build succeeding. With the copy on there is
nothing to tear down and nothing to retry. The claim measured here is the removal of the teardown and
the retries, not the removal of a build failure.

## What shipped

- **`Loading/ShadowCopier.cs`** — mirrors a directory incrementally and remaps paths into the
  mirror. It takes and returns directory paths and knows nothing about projects, payloads or
  processes, which is what makes it unit-testable; `Loading/ShadowResult.cs` carries the counts.
  Each running instance owns `%TEMP%/AvantGarde-Shadow/<pid>/`, and each mirrored source directory a
  subdirectory of that.
- **`RemoteLoader`** — `IsShadowCopyEnabled`, a mirror taken in `StartHostNoSync`, and the three
  paths that had to move: the `dotnet exec` command line's `--runtimeconfig`, `--depsfile` and
  assembly argument, plus `UpdateXamlMessage.AssemblyPath` in `SendXaml`. The `LoadPayload` keeps
  stating the real output, because that is what `AppAssemblyHashCode` and the build watcher read.
- **`MainWindow.RefreshTimerHandler`** — with the copy on, a detected build no longer suspends the
  preview; it records that a restart is owed and takes it once the output goes quiet.
- **`AppSettings.IsShadowCopy`** and a checkbox in Preferences.

Two directories can be mirrored, not one. Where a library control is previewed through an
application, `SendXaml` sends the library assembly from the library's own build directory rather
than the copy of it in the application's output, so both are mirrored and both are remapped.

## What measurement decided

**1. Copying the output verbatim is not viable, and the reason is not the assemblies.**

The `AvaloniaMvvm` fixture's output directory holds **566 MiB** across 83 files. 533 MiB of that is
native code and native debug symbols under `runtimes/` for platforms this machine cannot execute —
three copies of `libSkiaSharp.pdb` alone are 244 MiB. Copying it all would put seconds onto the
first preview of every session and leave half a gigabyte in `%TEMP%`.

The mirror therefore excludes two things, both describable as *files the runtime provably never
opens* rather than files judged unlikely to matter:

- `runtimes/<rid>/` subtrees for identifiers this process cannot load from. The host resolver builds
  its native search path from the running process's own runtime identifier, so no other one under
  there can be probed. The less specific forms it falls back to (`win` for `win-x64`, `any`, and
  `unix` off Windows) are kept.
- `.pdb` files under a `runtimes/*/native/` directory. Those are symbols for a native library, read
  only by a native debugger attached to the host. Managed symbols live under `lib/` and are copied —
  the host does read those, for the line numbers in a stack trace.

Result: 44 files, **33 MiB, 89 ms** for the first mirror of a session; **3–7 ms** for each one after,
copying only what the build rewrote. A skip is stated in the trace with its byte count.

**2. A mirror can start while MSBuild is still writing, and the fallback is worse than waiting.**

Visible in the very first trace: a `LOAD UPDATE`, a mirror and a host start, all *before*
`BUILD CHANGE DETECTED`. The explorer refresh and the build watcher are independent pollers, and
`bin/` is not excluded from the explorer's walk, so the refresh notices the output changing first —
by up to a full watcher interval, which is 5 s in a Debug build.

That matters more here than it used to. Copying a file MSBuild currently holds open throws, and the
failure path launches from the output directory — taking the lock this whole item exists to avoid,
at the one moment the user is provably building. `CopyWithRetry` waits instead: 5 attempts, 200 ms
apart, which covers the fraction of a second MSBuild holds any one file. Covered by a test that
holds a file open with `FileShare.None` and releases it mid-mirror.

The race itself is not closed. Closing it needs the explorer refresh to distinguish a source change
from an output change, which is Milestone 5's polling-to-watchers work.

**3. Nothing else would restart the host, and a stale host fails silently.**

The preview being left up through a build means the host survives it, still serving the previous
copy. If the post-build restart were left to `UpdateThread`'s app-assembly-change detection and that
detection missed, the host would go on answering XAML updates from stale code with nothing to say
so — the silent-failure class the plan calls the enemy. `RefreshTimerHandler` therefore calls
`_loader.Stop()` explicitly on the branch that clears `_restartAfterBuild`.

The converse guard matters too. `DotnetProject.Refresh` folds `AssemblyPath` into its hash, so while
the output directory is being rewritten the explorer reports a change on every tick, and
`UpdateLoader` answers a build in flight with `"Please wait..."`. Left alone, that branch would
replace the live preview with a placeholder — delivering the flicker this item removes by a
different route. It is suppressed while a restart is owed.

**4. `Process.Kill` only asks.**

`StopNoSync` killed the host and moved on. The next mirror begins within milliseconds and overwrites
the very files the dying process still has mapped, which is an intermittent `IOException` on the
copy — the kind that reproduces once a week. It now waits, bounded at 2 s.

**5. `PathItem.PlatformComparison` cannot be used for path matching.**

It is `InvariantCulture` on Windows and macOS and `Ordinal` elsewhere — case *sensitive* in all
three, so the platform branch buys nothing. `Remap` prefix-matches one path against another and
needs the case rule the file system actually applies, or a mirror is silently not found.
`ShadowCopier` defines its own comparison and says why. The upstream constant is left alone.

## Where the copies go, and who cleans up

A root is claimed by process id and deleted on `Dispose`. Roots belonging to process ids that are no
longer live are swept once per session, from the `RemoteLoader` constructor on a worker thread —
deliberately not from the first mirror, and deliberately not conditional on the setting. A root
stranded by a crash would otherwise sit there until some later session happened to turn the option
back on, which is a plausible sequence precisely because the option is one a user tries and drops.

Liveness decides, and deletability deliberately does not. Testing whether a sibling root's files can
be deleted would look like a neat way to detect a live instance, but a running host locks only the
assemblies it has loaded — a recursive delete of another instance's root would fail *after* having
removed part of it. Verified: a hard-killed instance leaves its root behind and the next launch
removes it; a graceful close removes its own.

## Deliberately not done

- **The working directory is unchanged.** It stays at `MyDocuments`, pointed at neither the output
  directory nor the mirror. A working directory is an open handle on that directory, which is the
  one thing this item exists not to hold. PLAN's "keep the original working directory so relative
  asset paths still resolve" is satisfied by not pointing it at the copy, and asset resolution does
  not depend on it anyway: `AssetLocator` resolves from the source tree and `PreviewFactory` rewrites
  asset paths to absolute before the XAML is sent.
- **`MainWindow.BuildProject` still stops the host and suspends.** The Build button appears only on
  a missing assembly, where there is no running host and no preview to preserve. Verified with the
  copy on: `bin/` removed, Build clicked through UI Automation, mirror taken in 58 ms, preview
  returns unaided.
- **A copy that cannot be taken still falls back to launching in place**, with the reason and the
  consequence on the OUTPUT pane. Refusing to preview would respect the opt-in more strictly, but a
  permanent failure — an unwritable temp directory — would then cost the preview entirely, with the
  cause visible only to someone who thought to look at the setting.
- **The setting is global, not per solution.** PLAN asks for opt-in, not for a per-project axis.
- **The empty `%TEMP%/AvantGarde-Shadow` parent is left behind.** Only the per-instance root inside
  it is removed.

## Verification

`dotnet build AvantGarde.sln` clean (0 warnings, Debug *and* Release), `dotnet test AvantGarde.Test`
**105/105** — thirteen new facts over `ShadowCopier`: the recursive copy, remapping (including a file
that does not exist, subdirectories and the directory itself), the incremental second pass over a
file whose length did not change, removal of what the source no longer has, two output directories
both named `net8.0` not colliding, the exclusion rules in both directions, the retry, root removal,
and the pid-based sweep.

Both fixtures, driven with `AvantGarde.exe <sln> -s=<leaf>` and the trace captured from stdout:

- `AvaloniaMvvm` → `Views/MainWindow.axaml`: host launched from the mirror (confirmed in the
  `dotnet exec` line and by `Get-CimInstance Win32_Process`), frames arriving. `dotnet build
  --no-incremental` while previewing: **build clean, no `MSB3061`**, trace shows
  `BUILD CHANGE DETECTED` → `Preview left running` → `RESTART AFTER BUILD`, and no `"Please wait..."`.
- The same rebuild with the setting **off**: 12 `MSB3061` diagnostics naming `.NET Host` as the
  holder of `AvaloniaMvvm.dll`, `Avalonia.Base.dll` and the rest; trace shows the original
  `Halt preview host` path intact.
- `MultiProjectSolution` → `ClassLibrary1/MyControl.axaml`: two mirrors with distinct names,
  `UpdateXamlMessage.AssemblyPath` naming the mirrored `ClassLibrary1.dll`. Rebuilding
  `ClassLibrary1` alone while previewing: build clean, preview never interrupted, restart re-copies
  the library's 3 files and nothing else. Screenshot confirms the control renders through
  `AvaloniaApp1`.
- Cleanup: a killed instance's root swept by the next launch; a gracefully closed instance removes
  its own.
- The setting itself, through the dialog rather than through the JSON: Preferences opens with the box
  reflecting the stored value, ticking it and pressing OK writes `IsShadowCopy: true`, the running
  loader picks it up for its next host start, and a fresh launch starts its host from the mirror —
  which is the read path (`AppSettings.AssignFrom`, where a new property is easy to forget) proved
  from the other end.
