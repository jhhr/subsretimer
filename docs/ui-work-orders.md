# Editor window: work orders for phase agents

You are one of a line of agents, each building **one small phase** of the editor window.
A lead reviews your work when you report back. You have no memory of earlier phases; what
you need is here.

**Your context is the budget.** Aim to finish well under 200k tokens. The rules below say
how. They are about not reading huge files whole and not maintaining big documents; they
are **not** a licence to skip what you need to understand. Careful, correct work comes
first.

## 1. What to read (and what not to)

1. This file, all of it. It is short on purpose.
2. `docs/ui-plan.md`: "Decisions taken", "Where it lives", "APIs verified", "Facts checked
   in the code", "Open defaults", and under "What each phase covers" the paragraph for
   your phase and the one before it. Skip the rest unless your work order names it.
3. `README.md` for the command-line contract (section "Usage") if your phase touches
   `Cli.cs`.
4. Code in this repository is small (every file under 300 lines): read the files your
   phase touches whole. `SubsRetimer.Core/RetimerEngine.cs` is the model every phase
   builds on; read it once.
5. Worked GTK examples live in the sibling checkout `/home/user/subs2srs` (a different
   repository, read-only for you). `subs2srs/DialogPreview.cs` is 1,300 lines and
   `subs2srs/MainWindow.cs` is larger: **never read them whole**, search for the member
   and read that range. The plan's "Facts checked" section gives line ranges.

Do not read `docs/ui-work-orders-archive.md` (if it exists) unless section 3 below leaves
a specific question open; then search it.

## 2. Rules

**Scope**

- Build your phase only. If something from a later phase is needed, build the smallest
  part of it and say so.
- The spec is agreed with the user. Where it is silent, choose the simpler option and
  note it. Where it is **wrong or impossible**, do not improvise another design: finish
  what can be finished, leave the tree building and green, and report.
- Do not edit `.github/`, `dist/`, `Makefile`, `LICENSE`, anything under
  `/home/user/subs2srs`, or the GirCore version. If one needs a change, report exactly
  what; the lead makes those changes.
- GTK rules: GirCore **0.7.0**, managed APIs only, no P/Invoke. GirCore 0.7 has no XML
  docs and hides nested signal-args types: do not guess API names from the C docs. When
  unsure, reflect: a throwaway console project in the scratchpad directory referencing
  `GirCore.Gtk-4.0` that prints `typeof(Gtk.X).GetMembers()`, or look at how subs2srs
  calls it.
- Match the surrounding code: two-space indent, `//  Copyright (C) 2026 jhhr and
  contributors` + SPDX header, XML doc comments on public members, comments say why.
  Timing logic lives in `SubsRetimer.Core`; the window is a view over `RetimerEngine`
  and never computes timing itself. Every number in a user-visible string goes through
  `TimeFormat` or `CultureInfo.InvariantCulture`.

**Build and test**

- Build: `cd /home/user/subsretimer && dotnet build SubsRetimer/SubsRetimer.csproj > /tmp/claude-0/-home-user-subs2srs/210a75b9-c213-55ee-b546-67ab310f976f/scratchpad/build.log 2>&1; echo exit=$? >> /tmp/claude-0/-home-user-subs2srs/210a75b9-c213-55ee-b546-67ab310f976f/scratchpad/build.log`
  then `tail -5` the log or grep it for `error`. Building the executable builds Core and
  Gtk through references. The tests project builds separately.
- Tests: `dotnet test SubsRetimer.Tests/SubsRetimer.Tests.csproj --filter "FullyQualifiedName~ClassName"`
  for yours; the whole suite is about 60 tests and takes under a minute (one test spawns
  the real executable). Run the whole suite **once**, at the end, and again only if
  something failed. Nothing in `dotnet test` may need a display or the network.
- GTK smoke runs: `GSK_RENDERER=cairo xvfb-run -a <command>`. A GTK process that opens a
  window blocks in its main loop: drive it from a throwaway console program in the
  scratchpad directory (`/tmp/claude-0/-home-user-subs2srs/210a75b9-c213-55ee-b546-67ab310f976f/scratchpad`)
  that references `SubsRetimer.Gtk`, calls the internal test surface, pumps the loop
  (`while (GLib.MainContext.Default().Iteration(false)) {}` a few times), saves a PNG the
  way `subs2srs.UiTests/Harness/Screenshot.cs` does, and exits. Report the PNG path.
  Give every run a timeout (`timeout 60`).
- Every behaviour gets a test that can fail. Show it for the two or three that matter
  most by breaking the code for a moment; not for every test. Undo the break **by hand**:
  never `git checkout`/`git restore` a file to revert an experiment.
- Do not weaken or delete an existing test to get green. If one is wrong because the
  behaviour was meant to change, change it and say so.
- Two GTK4 traps to build around from the start: `ColumnView` binds Up/Down with every
  modifier, so key controllers go in the **capture** phase; `Widget.OnDestroy` does not
  fire for a window closed with `Close()`, so saved state lives in the window and the
  exit code is computed from it.

**Docs: almost none.** README, CHANGELOG and the plan's summary are brought up to date
once, by the last phase. You write only:

- one entry in the log (section 5), **25 lines at most**, appended at the end;
- "Done <date> (<commits>)" on your phase's line under "Phases" in `docs/ui-plan.md`, and
  a correction of any statement there that your work proved wrong.

Edit documents with the Edit/Write tools only: a shell heredoc or one-liner containing
backticks, `$` or non-ASCII text gets mangled and has corrupted documents before.

**Commit**

- Work on branch `ui-editor` (create it from `main` if it does not exist; later phases
  find it). Commit when the whole suite is green; if it is not, do not commit: report.
- One commit per coherent step, imperative subject under 72 characters (`gtk: add the
  two line lists`), a body saying why when it is not obvious. End every message with
  exactly these two trailer lines:

      Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
      Claude-Session: https://claude.ai/code/session_019jgvMNvAz1BwYoChBasJpE

- Stage files by name. Never `git add -A`, never push, never amend earlier commits.

**Report** (your final message; all the lead sees; under 60 lines)

- What was built, by file, briefly. Commit hashes.
- The totals of the final full test run, copied, not paraphrased.
- Which tests you saw fail without the change.
- Choices made, deviations, anything fragile or unfinished. Say it plainly: a problem
  reported is cheap, one found later is not.

## 3. State of the code (kept by the lead; as of 2026-09-21, after phase 9)

- `SubsRetimer.Core`: `RetimerLine` (Start, End, Text, DisplayText, Style, Actor,
  RawIndex), `TimeFormat` (parse/format ASS and SRT times, `FormatOffset`),
  `SubtitleFile` + `RetimerIO` (load, render with only timestamps replaced, save with the
  original encoding/BOM/newline, `DefaultOutputPath` = `name_retimed.ext`),
  `RetimerEngine` (sorted lines, `ShiftFrom`, `ShiftToMatch`, `DeltaToMatch`, undo/redo
  snapshots, `IsDirty`, `Save`, `Changed` event + `Version` raised after every
  operation that changed something (never on a no-op), static `Overlap`, `ClosestIndex`, `BestOverlap`,
  `NextLargeGap`/`PreviousLargeGap` over a `bool[]`,
  `LargeGapFlags`, `MismatchFlags`, `AverageMismatchSeconds`), `AutoAlign` (`Compute`,
  `Apply`, `Describe`).
- `SubsRetimer` (executable `subsretimer`): `Cli.Parse/Run/RunAuto/RunEditor`, exit
  codes 0 saved / 2 nothing saved / 1 error, stdout carries only saved paths under
  `--print-output`. `RunEditor` loads the given files with `LoadChecked`, asks
  `Cli.CanOpenDisplay()` (exit 1 + message when false), runs `Cli.RunWindow(reference,
  target)` inside a catch-all (exit 1 + message), and returns 0 when the returned saved
  paths are non-empty, else 2. Both are `internal static Func` seams that `CliTests`
  swaps and restores in a `finally`; nothing under `dotnet test` may start GTK
  (`Gtk.Module.Initialize()` exits a display-less process).
- `SubsRetimer.Gtk` (assembly name; namespace `SubsRetimer.Editor`): `EditorHost`
  (`CanOpenDisplay` = `Gdk.Module.Initialize()` + `Gdk.Display.Open(null)`; `Run` creates
  the `Gtk.Application` and returns `window.SavedPaths`), `RetimerWindow :
  Gtk.ApplicationWindow` (owns the `RetimerEngine`, subscribes to `Changed` once, top
  strip, `Gtk.Paned` with a `LineListView` per side, detail strip; internal surface:
  `Engine`, `SavedPaths`, `LoadReference/LoadTarget(path, enc?)`, `SetFile(side, file)`,
  `SelectReference/SelectTarget(i)`, `SelectedReference/SelectedTarget` (-1 = none),
  `RowState(side, i)`, `Counters(side)`, `DetailSummary`, `BoundStartText(side, i)`,
  `StoreRebuilds`; since phase 3 also `TimeShift()`, `Undo()`, `Redo()`, `Save()`,
  `SaveAs(path)`, `IsDirty`, `CanTimeShift`, `RequestClose()` and the seam
  `Func<Task<int>>? CloseChoice` that stands in for the Save/Discard/Cancel dialog).
  Buttons Time Shift / Undo / Redo / Save / Save As sit at the right end of the detail
  strip; `RefreshButtons()` and `RefreshTitle()` run from the `Changed` handler and the
  selection path. Every save goes through `SaveTo(path?)`, which appends to `SavedPaths`
  (deduplicated). `OnCloseRequest` vetoes while dirty and `PromptThenClose` ends with
  `Destroy()`; `Widget.OnDestroy` fires for neither `Close()` nor `Destroy()`.
  `RunEditor` prints `SavedPaths` under `--print-output` and exits 0/2 from it.
  Since phase 4: `Gio.SimpleAction`s on the window (`win.open-reference`, `open-target`,
  `save`, `save-as`, `quit`, `undo`, `redo`, `time-shift`, `auto-align` (disabled until
  phase 5), `help`, `about`; constants on the window), a `Gtk.PopoverMenuBar` File/Edit/
  Help as the first child of the root box, accelerators through
  `SetAccelsForAction`; `RefreshButtons()` keeps action state and button sensitivity in
  step. Per list (`AttachInput`): a capture-phase `Gtk.EventControllerKey` → `OnListKey`
  (Left/Right closest line on the other side + focus; Ctrl+Up/Down gap jump; Return =
  Time Shift), `Gtk.GestureClick` button 3 / button 2 → `OnListClick` (selects the clicked
  row via `LineListView.IndexAt(x, y)`, then closest line / Time Shift). Internal:
  `SelectClosestOnOtherSide(side)`, `JumpToGap(side, forward)` (index or -1),
  `ActivateAction(name)`, `KeyTable`, `AboutText`, `ShowMessage`; on `LineListView`:
  `GapFlags`, `ScrollTo(index, focus)`, `IndexAt(x, y)`. GTK 4 event synthesis is not
  exposed in GirCore 0.7: the handlers are the test seam, not real key delivery.
  Since phase 5: `CanAutoAlign` (= `HasBoth`), `AutoAlign()` (returns the segments
  applied; `AutoAlign.Apply` pushes one undo step per segment **that moves**, a zero
  delta is silent), `StatusText` / `NoAlignment`, a dim status label between the lists
  and the detail strip (hidden while empty, cleared by `SetFile`), the Auto Align button
  after Time Shift and `win.auto-align` enabled from `RefreshButtons`.
  `ChoiceSave/Discard/Cancel` are internal constants.
- After phase 6 (lead): `IsDirty` is no longer a sticky flag but "undo depth differs
  from the clean depth" (set on load and save, -1 once unreachable), so undoing back to
  the loaded or saved state clears the star.
- `SubsRetimer.UiTests` (xUnit, 25 tests, `[GtkFact]` skips every test when
  `EditorHost.CanOpenDisplay()` is false): `Harness/GtkFixture` (one GTK thread,
  `RunOnGtk`/`RunOnGtkAsync`), `Pump` (`IdleAsync`, `FramesAsync`, `SettleAsync`,
  `WaitUntilAsync`; never settle on a window an action may have destroyed, use
  `UiTestScope.RunIdleAsync`), `Screenshot` (`SUBSRETIMER_UITEST_ARTIFACTS`),
  `UiTestScope` (temp dir, `OpenWindowAsync`, `RunAsync`, `Read`, leak check through
  `Gtk.Window.GetToplevels()`), `SubtitleFixtures` (irregular whole-centisecond timings,
  40 s silences, cumulative cuts; writes `reference.srt` + `target.ass`). Tests in
  `Tests/Window{Load,Edit,Navigation,AutoAlign}Tests.cs` call the internal surface.
  Run: `make test-ui` (= `GSK_RENDERER=cairo xvfb-run -a dotnet test ...`); CI has a
  `ui-tests` job.
- Since phase 7: `TimelineLayout` (display-free: `ScaleSeconds` clamped 4..120, default
  10; `ScaleWindow`, `VisibleLines`, `XFor`, `BarRect`, ticks and labels; 8 unit tests)
  and `TimelineChart` (a `Gtk.Box` with `+`/`-` zoom buttons and a `DrawingArea` drawn
  through `SetDrawFunc`; `SetLines`, `SetActive`, `QueueDraw`, `ClickAt(button, x, y)`
  → `Clicked`, `DrawCount`/`LastDrawError`; a throwing draw is caught, never escapes the
  native callback). Packed between the menu bar and the top strip, 64 px. Window:
  `Chart`, `ChartClick(button)`, `MoveTargetSelection(step)`, `RefreshChart()`. Cairo in
  0.7: `LineWidth` is a property, the pressed button comes from
  `GestureSingle.GetCurrentButton()`. `Tests/WindowChartTests.cs` has 6 UI tests.
- Since phase 8: file drops work. One `Gtk.DropTarget.New(Gio.FileHelper.GetGType(),
  Copy)` per pane box (`AttachDrop`), `DropFile(side, GObject.Value)` is the handler and
  the test seam (`DropSignalArgs` cannot be hand-built), `PathFromDrop` takes a `GFile`
  or the first line of text (path or `file://` URI); several files → the first only.
  `DropTargetFor(side)`; pane hints say "Open or drop here". `Gdk.FileList` cannot be
  read in 0.7 and `DropTarget` has no `SetGtypes`. `Tests/WindowDropTests.cs`, 4 tests.
- Since phase 9: `assets/` (the original's icon, GPL-3), `ApplicationIcon` and
  `RuntimeIdentifiers linux-x64;win-x64` in the csproj (`OutputType` stays `Exe`: the
  CLI needs a real stdout, so a console accompanies the editor on Windows),
  `RetimerWindow.SetIconName("subsretimer")` with a UI test, hicolor icon install in the
  `Makefile`, `dist/windows/bundle-gtk.ps1` + `smoke.ps1` (smoke checks the bundle layout,
  `--version` and `--auto`; it does not start the editor), `make publish-windows`,
  `SubsRetimer/WindowsRuntimeSetup.cs` (sets XDG/GSettings/pixbuf/GSK variables when the
  bundle layout is present, first thing in `Main`), `.github/workflows/release.yml`
  (Windows zip on `v*` tags, lead). Neither PowerShell script has run yet: no `pwsh` here. `LineListView` (`Gio.ListStore` of `Gtk.StringObject`, bound-cell
  map filled in bind / emptied in unbind, `Refresh()` rewrites text and CSS classes in
  place, store rebuilt only in `SetLines`), `RetimerStyles` (one CSS provider:
  `retimer-gap`, `retimer-mismatch`, `retimer-overlap-good/bad`, hint). A dismissed
  `Gtk.FileDialog` surfaces as a `GLib.GException`; `Gtk.AlertDialog` is `new`-ed.
- `SubsRetimer.Tests`: xUnit, 84 tests, `Fixtures.Lines` (periodic) and
  `Fixtures.Dialogue(count, seed)` (irregular timings; use this for anything about
  alignment or gaps).

## 4. Phases

Done: 1, 2, 3, 4, 5, 6, 7, 8, 9.

### 1 — Core: change notification and gap navigation

Read `RetimerEngine.cs` and `RetimerEngineTests.cs` whole.

- Add to `RetimerEngine`: `public event Action? Changed` and `public int Version`,
  incremented and raised by `LoadReference`, `LoadTarget`, `ShiftFrom` (and so
  `ShiftToMatch`), `Undo`/`Redo` when they return true, and `Save`. Not raised when
  nothing changed.
- Add static `NextLargeGap(bool[] flags, int from)` and `PreviousLargeGap(bool[] flags,
  int from)`: the first flagged index strictly after / before `from`, else -1; `from`
  may be -1 (no selection) or out of range.
- Not in this phase: anything in `SubsRetimer` or GTK.
- Tests: Version/Changed for each operation and for the no-op Undo; the two navigation
  functions at the ends, with no flags, and with `from` = -1.

### 2 — Gtk skeleton: window, lists, colours, detail strip, RunEditor

Read the plan's "The window, mapped from the original" and "APIs verified" sections;
`Cli.cs` and `CliTests.cs` whole; `subs2srs/DialogPreview.cs` lines 497 to 665 and
`subs2srs/Program.cs` lines 70 to 95 in the sibling checkout.

- New project `SubsRetimer.Gtk/SubsRetimer.Gtk.csproj` (class library, net10.0,
  nullable, `GirCore.Gtk-4.0` 0.7.0, reference to Core, `InternalsVisibleTo` for
  `SubsRetimer.Tests` and `SubsRetimer.UiTests`), referenced from
  `SubsRetimer/SubsRetimer.csproj` (replace the placeholder comment).
- `RetimerWindow : Gtk.ApplicationWindow` built as the plan's window section says, minus
  editing, menu, keys and Auto Align. Lists: `Gio.ListStore` of `Gtk.StringObject`
  holding the row index as text, parallel `IReadOnlyList<RetimerLine>` from the engine,
  `Gtk.SingleSelection` with autoselect off, Start and Dialog columns, the Dialog column
  expanding. Row colour by CSS class on both cell widgets (`retimer-gap` orange,
  `retimer-mismatch` gray; gap wins), set in bind. On `engine.Changed` refresh the bound
  cells in place (keep a map from row index to bound widgets, filled in bind and
  emptied in unbind); never `RemoveAll`/re-append the store after the first fill.
  Loading a file replaces that list's rows (the one time the store is rebuilt).
- Detail strip per side: text, start time; between them the overlap percent with a green
  or red background class, the signed difference (`TimeFormat.FormatOffset`); the top
  strip shows `all : matched` average mismatch and `selected/total` per list.
- Open Reference / Open Target buttons using `Gtk.FileDialog.OpenAsync` with a
  `Gtk.FileFilter` for `.ass/.ssa/.srt`; load errors go to a `Gtk.AlertDialog`.
- Internal test surface on the window: `LoadReference(path, encoding?)`,
  `LoadTarget(...)`, `SelectReference(int)`, `SelectTarget(int)`,
  `SelectedReference`/`SelectedTarget`, `RowState(side, index)` returning an enum
  `None | Gap | Mismatch`, `DetailText` (or equivalent) so a test can assert what the
  strip shows. Keep it small and `internal`.
- `Cli.RunEditor`: after `LoadChecked`, create `Gtk.Application.New("io.github.jhhr.subsretimer",
  Gio.ApplicationFlags.NonUnique)`, open the window with the given files in
  `OnActivate`, `RunWithSynchronizationContext(null)`; return 2 (nothing saved) when the
  window closes. If GTK cannot initialise (no display: `Gtk.Module.Initialize()` /
  `Gdk.Display.GetDefault()` is null, check what 0.7 offers), write
  `subsretimer: cannot open a display; use --auto` to stderr and return 1. Never let a
  GTK exception escape to `Cli.Run`.
- Replace `CliTests.Editor_NotAvailableYet_ExitsOne` with a test that runs editor mode
  with `DISPLAY` and `WAYLAND_DISPLAY` empty and expects exit 1 and that message. Do not
  start GTK in any test that runs under `dotnet test`.
- Smoke: the throwaway program described under "Build and test" opens two fixture files
  (write them from `Fixtures.Dialogue`-like data, or reuse the test fixtures), selects a
  row on each side, prints the row states and the detail text, saves a PNG. Report the
  path and what it shows.
- Not in this phase: Time Shift, Save, undo, keys, menu, chart, drag-and-drop.
- Tests (in `SubsRetimer.Tests`, no GTK): the CLI display test above. Everything
  else is smoke; phase 6 brings real UI tests.

### 3 — Editing: Time Shift, undo/redo, dirty state, Save/Save As, close prompt

Read the plan's window section, `RetimerWindow` whole, `Cli.RunEditor`.

- Time Shift button: `engine.ShiftToMatch(selectedRef, selectedTarget)`, enabled only
  when both sides have a selection. Undo/Redo buttons bound to `CanUndo`/`CanRedo`.
- Title `Subs Re-Timer - <target name>` with `*` when `IsDirty`.
- Save: `engine.Save()` to the default output path; Save As: `Gtk.FileDialog.SaveAsync`
  with `SetInitialName` = default name. Each saved path is appended to
  `RetimerWindow.SavedPaths`; `RunEditor` prints them to stdout under `--print-output`
  after the loop ends and returns 0 if any, else 2.
- Close with unsaved changes: `Gtk.AlertDialog` Save / Discard / Cancel via
  `ChooseAsync`; `OnCloseRequest` returns true to veto while the dialog is open.
- Internal surface: `TimeShift()`, `Undo()`, `Redo()`, `Save()`, `SaveAs(path)`,
  `SavedPaths`, `IsDirty`.
- Tests: CLI-level test of the `--print-output` stdout contract for editor mode is
  impossible without a display; instead test `RunEditor`'s exit-code mapping through a
  seam (an injectable "run the window" delegate returning the saved paths).

### 4 — Keyboard and mouse: menu, accelerators, closest-line and gap navigation

Read the plan's "Behaviours" bullet and the two GTK4 traps; `RetimerWindow` whole.

- `Gio.SimpleAction`s for Open Reference, Open Target, Save, Save As, Undo, Redo, Time
  Shift, Auto Align (disabled placeholder until phase 5), Help, About;
  `Gtk.PopoverMenuBar.NewFromModel`; `Application.SetAccelsForAction` for Ctrl+O,
  Ctrl+Shift+O, Ctrl+S, Ctrl+Shift+S, Ctrl+Z, Ctrl+Y, Return.
- A `Gtk.EventControllerKey` in **capture** phase on each list: Left/Right select the
  closest line on the other side (`RetimerEngine.ClosestIndex`) and move focus there;
  Ctrl+Up/Ctrl+Down select the previous/next orange row in the focused list
  (`PreviousLargeGap`/`NextLargeGap`) and its closest counterpart; Enter = Time Shift.
- `Gtk.GestureClick` with button 3 = closest line on the other side, button 2 = Time
  Shift; `ScrollTo` the selected row on both sides.
- Internal surface: `SelectClosestOnOtherSide(side)`, `JumpToGap(side, forward)`.
- Tests: the closest-line and gap-jump behaviours are pure index arithmetic once the
  engine is involved; test them through the internal surface only if a display-free path
  exists, otherwise leave them to phase 6 and say so.

### 5 — Auto Align in the editor

Read `AutoAlign.cs` whole and `Cli.RunAuto`.

- Auto Align button and menu action: `AutoAlign.Compute` on the engine's lines, then
  `Apply` (one undo step per breakpoint, already how `Apply` works: confirm), then
  recolour through `Changed`; a status line under the lists shows `Describe` for each
  segment or "no alignment found". Disabled unless `HasBoth`.
- Internal surface: `AutoAlign()` returning the segments.
- Tests: none new without a display beyond confirming `Apply` produces one undo step per
  segment (add to `AutoAlignTests` if missing).

### 6 — UI test harness, tests for phases 2 to 5, CI job

Read `subs2srs.UiTests/Harness/*.cs` (474 lines in five files) whole and one small test
in `subs2srs.UiTests` for the shape.

- New project `SubsRetimer.UiTests` (xUnit, references Gtk, Core, the executable). Port
  the harness: GTK thread fixture with `ApplicationFlags.NonUnique` and `Hold()`, `Pump`
  for settling, `Screenshot` keyed on `SUBSRETIMER_UITEST_ARTIFACTS`, a scope that fails
  on leaked windows. Parallelisation off.
- Tests through the internal surface: load two files → row states match
  `LargeGapFlags`/`MismatchFlags`; select → detail text; Time Shift → target moved,
  dirty, undo restores; Save → file exists and `SavedPaths` has it; close with dirty →
  the veto path; closest-line and gap-jump selection; Auto Align → segments applied and
  status text. A PNG per window when the env var is set.
- Do not edit `.github/`: report the job the lead should add (install `libgtk-4-1
  xvfb`, `GSK_RENDERER=cairo xvfb-run -a dotnet test SubsRetimer.UiTests/...`).
- Tests: those above; show two of them fail when the behaviour is broken.

### 7 — Timeline chart

Read the plan's phase 7 paragraph; the original `ChartSubs.cs` is not in this repository:
the lead attaches its text or a summary in the phase prompt.

- `TimelineChart : Gtk.DrawingArea` with `SetDrawFunc`: two rows of bars for the lines
  inside the visible time window around the selected target line, ticks and labels,
  selected lines highlighted, zoom in/out buttons; left-click advances the target
  selection, right-click goes back, middle-click = Time Shift. Pure layout arithmetic
  (which lines are visible, bar x/width for a time) in a display-free helper with unit
  tests.
- UI tests: chart renders without error and reacts to selection.

### 8 — Drag-and-drop spike

- Time-boxed: try `Gtk.DropTarget` with `GObject.Type.String` and with `Gdk.FileList`
  through `Value.GetBoxed`. Keep the code only if a real file manager drop works under
  Xvfb via a synthetic drop or a documented manual check; otherwise commit nothing but
  the log entry saying what was tried and why it failed.

### 9 — Packaging: desktop file, icon, Windows bundle

- Report (do not edit) the `dist/` and `Makefile` changes: drop `NoDisplay`, an icon,
  `bundle-gtk.ps1`/`smoke.ps1` adapted from `subs2srs/dist/windows/`; write the
  adapted scripts in the report or in the scratchpad for the lead to place.

### 10 — Documentation pass

- Bring `README.md` (status, editor usage, key table), `CHANGELOG.md` (Unreleased),
  and `docs/ui-plan.md` up to date from the code and this log; add a "Known limitations
  and open points" section to the plan. No code: suspected bugs go in the report.

## 5. Log (newest last; 25 lines at most per entry)

Template:

    ### Phase <id> — <date> — <commits>
    Built: ...
    Choices / deviations: ...
    The next phase must know: ...
    Left open: ...

### Phase 1 — 2026-09-21 — `core: notify on change and navigate large gaps`

Built: in `RetimerEngine`, `public event Action? Changed` and `public int
Version`, both driven by one private `Bump()` called *after* the state is
updated, from `LoadReference`, `LoadTarget`, `ShiftFrom` (so also
`ShiftToMatch`), `Undo` and `Redo` on their `true` return, and `Save`. Static
`NextLargeGap(flags, from)` / `PreviousLargeGap(flags, from)` next to
`LargeGapFlags`: first flagged index strictly after / before `from`, else -1;
`from` may be -1 or out of range, a null `flags` throws. New test file
`SubsRetimer.Tests/RetimerEngineChangeTests.cs`, 8 tests; suite 66 passed.

Choices / deviations: `ShiftFrom` raises exactly when it pushed an undo
snapshot, so a zero delta (including `ShiftToMatch` on already matching lines)
is silent. `Save` raises on every successful save, not only a dirty one: the
title, the saved-paths list and the Save button all follow a save. The plan
line names the commit subject, not a hash: the doc is inside that commit.

The next phase must know: subscribe to `Changed` in the window constructor and
never poll; `Version` is for a view that batches. `Changed` runs synchronously
on the caller's thread and handler exceptions are not caught — one that throws
propagates out of `ShiftFrom`/`Save`.

Left open: nothing from this phase. No GTK code, no display needed.

### Phase 2 — 2026-09-21 — `gtk: add the editor window with the two line lists`, `cli: open the editor window from the command line`

Built: `SubsRetimer.Gtk`, namespace `SubsRetimer.Editor` (a namespace
`SubsRetimer.Gtk` shadows the global `Gtk` in every file): `RetimerWindow`
(top strip, two panes, detail strip, Open buttons), `LineListView` (store,
columns, bound-cell map, colours), `RetimerStyles`, `EditorHost` (display
check, `Gtk.Application`). `Cli.RunEditor` opens it behind the seams
`Cli.CanOpenDisplay` / `Cli.RunWindow`; 69 tests pass.

Choices / deviations: the overlap percent is the target line's overlap with
the reference, green from 50 % up. `Gtk.AlertDialog` has no `New()` in 0.7:
`new Gtk.AlertDialog()`. A dismissed `FileDialog` arrives as a
`GLib.GException` with no readable code, so every one from `OpenAsync`
counts as dismissed. Surface beyond the order: `SetFile`, `StoreRebuilds`
and `BoundStartText(side, index)`, which prove an in-place refresh.

The next phase must know: `Gtk.Module.Initialize()` calls `gtk_init()` and
exits a display-less process, so nothing under `dotnet test` may touch GTK;
the no-display test spawns the real executable. The working check is
`Gdk.Module.Initialize()` + `Gdk.Display.Open(null)` (plan, "Facts
checked"). `RetimerWindow.SavedPaths` is the saving seam and `RunEditor`
already returns 0 when it is non-empty; only `--print-output` is missing.

Left open: `Makefile` `clean` should also remove `SubsRetimer.Gtk/bin` and
`SubsRetimer.Gtk/obj` (reported; `ci.yml` needs nothing). No UI tests yet.

### Phase 3 — 2026-09-21 — `gtk: edit, save and ask before closing with changes`, `cli: print the paths the editor saved under --print-output`

Built: five buttons at the end of `RetimerWindow`'s detail strip (Time
Shift, Undo, Redo, Save, Save As...), sensitivity from `RefreshButtons()`
called by the `Changed` handler and the selection path; `RefreshTitle()`
(`*` + `Subs Re-Timer - <target name>`); `SaveTo` (full path, appended to
`SavedPaths`, errors to a `Gtk.AlertDialog`), the Save As `FileDialog`,
the close prompt. Surface: `TimeShift`, `Undo`, `Redo`, `Save`,
`SaveAs(path)`, `IsDirty`, `CanTimeShift`, `RequestClose`, `CloseChoice`.
`Cli.RunEditor` prints saved paths under `--print-output`; 3 new tests.

Choices / deviations: `SavedPaths` never takes the same path twice, so a
second save cannot print a line twice. Save and Save As stay sensitive
when nothing changed (that is how a copy is written). Cancel, a dismissed
prompt, an unknown answer and a failed save all keep the window open.

The next phase must know: `OnCloseRequest` is
`ReturningSignalHandler<Gtk.Window, bool>` (`+= (_, _) => VetoClose()`,
true vetoes); the prompt then calls `Destroy()` itself, which detaches
the window from the application (`OnDestroy` still does not fire).
`RequestClose()` runs the same veto, so it needs no realized window.
Phase 4's actions should call `TimeShift`/`Undo`/`Redo`/`Save`/
`SaveAsDialog` and leave enabling to `RefreshButtons`. Suite: 72 passed.

Left open: nothing. Scratchpad driver `smoke3` covers it all.

### Phase 4 — 2026-09-21 — `gtk: add the menu, accelerators and list navigation`

Built: eleven `Gio.SimpleAction`s on the window (open-reference,
open-target, save, save-as, quit, undo, redo, time-shift, auto-align
disabled until phase 5, help, about), a `Gtk.PopoverMenuBar`
File/Edit/Help, the seven accelerators the order lists, and
`RefreshButtons` greying the actions with the buttons. Per list: a
capture-phase `Gtk.EventControllerKey` (Left/Right, Ctrl+Up/Down, Enter)
and two `Gtk.GestureClick`s (3 = closest line, 2 = Time Shift). Surface:
`SelectClosestOnOtherSide(side)`, `JumpToGap(side, forward)`,
`ActivateAction(name)`, `KeyTable`, `AboutText`; on `LineListView`
`GapFlags`, `ScrollTo`, `IndexAt`. One engine test (a `ClosestIndex`
tie: the earlier line wins). Suite: 73 passed.

Choices / deviations: Left and Right both mean "the other list", from
either side, as the plan words it; only they move the focus. Enter is a
list key, not an accelerator, so it acts only with a list focused. About
reads this assembly's informational version (0.1.0), not `Cli.Version`.

The next phase must know: `ScrollTo(pos, null, flags, null)` marshals
fine in GirCore 0.7; a click finds its row through `Widget.Pick` and
reference equality on the bound cell boxes. Auto Align belongs in
`RefreshButtons`, replacing its `SetEnabled(false)` in `BuildActions`.

Left open: the keys and the clicks wait for phase 6 (driver `smoke4`).

### Phase 5 — 2026-09-21 — `tests: check each Auto Align undo step restores the stage before`, `gtk: align the whole target from the editor window`

Built: `RetimerWindow.AutoAlign()` (`Compute` + `Apply` on the engine's
lines, the segments returned), `CanAutoAlign` = `engine.HasBoth`, an **Auto
Align** button after Time Shift and the `win.auto-align` action phase 4 left
disabled, both greyed by `RefreshButtons`; a `_status` label between the
lists and the detail strip showing the `Describe` text of every segment
joined with `"; "` (`StatusText`, `NoAlignment` when empty), hidden while
empty, ellipsized with the full text in its tooltip, cleared by `SetFile`.
One new test in `AutoAlignTests`. Suite: 74 passed.

Choices / deviations: `Apply` pushes an undo step per segment *that moves*:
its delta is `seg.Offset - applied`, and `ShiftFrom` returns early on a zero
delta, so a segment repeating the previous offset (a leading zero segment,
above all) costs no step. The status text is the segment list only, so it
stays true either way; the plan's Auto Align bullet now says so.

The next phase must know: the status line is a plain `Gtk.Label` under the
lists, not in the top strip; `StatusText` is its text. `AutoAlign()` on an
empty window returns no segments and leaves the status alone. A 3-line
leading block is below `SwitchPenalty` (2.5 matched lines), so `Compute`
folds it into the next segment and those rows stay gray — not a window bug.

Left open: nothing. Driver `smoke5` covers it; UI tests are phase 6's.

### Phase 6 — 2026-09-21 — `tests: add a GTK UI test harness for the editor window`, `tests: cover the editor window's phases 2 to 5 with UI tests`

Built: `SubsRetimer.UiTests` (xUnit, references Core and Gtk, no
parallelisation). `Harness/`: `GtkFixture` (one `NonUnique` application on a
background thread, `Hold()` in `OnActivate`, `RunOnGtk`/`RunOnGtkAsync`),
`Pump` (idle / frames / settle / until), `Screenshot`
(`SUBSRETIMER_UITEST_ARTIFACTS`), `UiTestScope` (opens the window, settles
after every action, screenshots and destroys on dispose, fails on a leaked
toplevel), `SubtitleFixtures` (SRT reference + ASS target, irregular timings,
silences, cuts), `Display` + `GtkFactAttribute`. 14 tests, phases 2 to 5.

Choices / deviations: no `GtkSynchronizationContext` was ported —
`RunWithSynchronizationContext(null)` installs GirCore's own main-loop context
(measured: an `await` in a GTK lambda continues on the GTK thread; liveness is
`Gtk.Window.GetToplevels()`). `RetimerWindow`'s three `Choice*` constants
became `internal` so a test can name them. `RetimerEngine.Undo` sets `IsDirty`
unconditionally, so the star stays after undoing back to the loaded state: the
test asserts that, the work order expected it to clear (reported).

The next phase must know: `[GtkFact]`, never `[Fact]` — the display is checked
once at discovery and the fixture starts no application without one, because
`gtk_init()` would exit the test process. What changes the engine goes through
`scope.RunAsync(window, …)`, what may destroy it through `RunIdleAsync`.

Left open: the CI job and the `Makefile` lines are in the report (not editable).

### Lead — 2026-09-21 — after phase 6
Changed: `RetimerEngine.IsDirty` is now computed from a clean undo depth (commit
`core: undoing back to the loaded or saved state is clean again`), with two engine tests
and the UI assertion flipped; CI `ui-tests` job and `make test-ui` added.
The next phase must know: UI tests exist and must stay green (`make test-ui`); add tests
for new behaviour there, in the existing files' shape.

### Phase 7 — 2026-09-21 — `gtk: draw the timeline chart under the menu`, `tests: cover the timeline chart with UI tests`

Built: `TimelineLayout` (display-free: clamped `ScaleSeconds` 4..120,
`ScaleWindow`, `VisibleLines`, `XFor`, `BarRect`, major/minor ticks,
`TickLabels`/`LabelEvery`) and `TimelineChart` (a `Gtk.Box` with the two zoom
buttons and a `Gtk.DrawingArea`; `ScaleSeconds`, `ZoomIn`/`ZoomOut(step)`,
`Layout`, `SetLines`/`SetActive`/`QueueDraw`, `ClickAt(button, x, y)` →
`Clicked`, `DrawCount`/`LastDrawError`), the 64 px strip between the menu and
the top strip, and on the window `Chart`, `RefreshChart()` and
`ChartClick(button)` (left/right walk the target selection and reselect the
reference, middle = Time Shift). 8 unit + 6 UI tests; suites 84 and 20 passed.

Choices / deviations: the window starts on a whole second and is exactly
`ScaleSeconds` long (the original rounds both ends with an integer half-range,
leaving an odd scale narrower than its own ticks); a line spanning the whole
window is visible (the original dropped it); a bar is never thinner than 1 px;
the chart owns its widgets instead of subclassing one, as `LineListView` does;
a draw that throws is caught into `LastDrawError`, since an exception in that
native callback would end the process. Colours: ground white, ticks `#A9A9A9`,
labels `#1A1A1A`, bars `#969696` with white text, active `#228B22` / `#4169E1`.

The next phase must know: `Chart.Layout` is null until both sides have a
selection (only ticks are drawn then); the zoom buttons need the `retimer-zoom`
class, or the strip grows past 64 px; real mouse delivery to the chart is a
manual check, as for the lists' keys. Left open: nothing (driver `smoke7`).

### Phase 8 — 2026-09-21 — `gtk: load a subtitle file dropped on either pane`, `tests: cover the dropped-file handler with UI tests`

Built: drops work and were kept. On each pane's box (not the list: the
scroller is hidden until a file is loaded, so an empty pane could take
nothing) a `Gtk.DropTarget.New(Gio.FileHelper.GetGType(),
Gdk.DragAction.Copy)`; `OnDrop` calls `DropFile(side, args.Value)`, which
resolves the value to a path and goes through the existing `SetFile`, with
`OpenFile`'s error dialog. Surface: `DropFile(side, value)`,
`DropTargetFor(side)`. A `KeyTable` line and both pane hints mention drops.
`Tests/WindowDropTests.cs`, 4 UI tests; suites 84 and 24 passed.

Choices / deviations: the plan's row said drops were impossible; it was
wrong about `Gio.File` and is corrected. `Gdk.FileList` really is a dead
end (no `GetFiles` is bound), and there is no `SetGtypes`, so a target
takes one GType. A drop of several files loads the first.

The next phase must know: `GetFormats()` on a drop target lists its GType
and no mime type (GirCore binds no `union_*` helper), so it says nothing
about what a drop matches; that was measured from a file source's own
formats: `GdkFileList GFile gchararray text/uri-list text/plain;charset=utf-8`.
No reader for a value's GType is bound either:
`Value.Transform(new Value(Type.String))` is the probe (false for an object
value), and it is how `DropFile` also takes text — paths or URIs, one a line.

Left open: a real drag is a manual check, as keys and clicks are.

### Phase 9 — 2026-09-21 — `dist: give the tool the original Subs Re-Timer icon`, `dist: bundle the GTK runtime for a self-contained Windows build`

Built: `assets/` (the original tool's red R: `subsretimer.ico` + 16/32/48 PNGs,
`README.md` naming the origin and GPL-3), a conditional `<ApplicationIcon>`
(verified embedded in a cross-published exe), `RetimerWindow.SetIconName` with
a UI test, `NoDisplay` dropped and the PNGs installed to
`share/icons/hicolor/<n>x<n>/apps/subsretimer.png`
by `make install` / removed by `uninstall`. `dist/windows/bundle-gtk.ps1`,
`smoke.ps1`, `THIRD-PARTY-README.txt`, `make publish-windows`, the `win-x64`
`RuntimeIdentifiers`, and `SubsRetimer/WindowsRuntimeSetup.cs` called first in
`Main`. Suites: 84 and 25 passed.

Choices / deviations: `OutputType` stays `Exe` everywhere (ordered), so a
console window accompanies the editor on Windows. `WindowsRuntimeSetup` is new
code this phase had to add: without `GSETTINGS_SCHEMA_DIR` and friends
`gtk_init()` aborts in the bundle. One name, `subsretimer`, ties the desktop
entry, the window's icon name and the installed PNGs together, and the bundle
copies the PNGs into its own `share\icons\hicolor`.

The next phase must know: `smoke.ps1` does not open a window — a console
process's `MainWindowHandle` is its console, so that check would pass with no
GTK at all; it checks the bundle layout, `--version` and `--auto` instead. GTK
starting from the bundle is a manual check, and this box has no PowerShell.

Left open: `.github/workflows/release.yml` is in the report (not editable).

### Phase 10 — 2026-09-21 — `docs: describe the editor in the README, changelog and plan`

Built: `README.md`: the status says the editor works on Linux and Windows, a
"Using the editor" section (opening files, the two lists and their colours,
detail strip, Time Shift, Auto Align, timeline, saving, close prompt) with a
key and mouse table copied from `KeyTable`, editor exit codes in the usage
section, `make test-ui` and `make publish-windows` under Build, desktop entry
and icons under Install. `CHANGELOG.md`: an Unreleased section, one line per
user-visible change plus the `IsDirty` fix. `docs/ui-plan.md` is now the
record: status, phase 10 done, the lead's CI and release workflow noted on
phases 6 and 9, corrections (Dialogue column, in-place refresh, Quit, the
timeline and drops in the window map, clean-depth `IsDirty`, the
`GtkSynchronizationContext` that was not ported), "Known limitations and open
points" completed and a nine-step manual checklist. No code touched; both
suites re-run unchanged: 84 passed and 25 passed.

Choices / deviations: the contract bullet claimed `--print-output` flushes
each path as it is written; that holds for `--auto` only, the editor prints
them when it closes, and the bullet now says so. subs2srs's README has no
Windows build section to take wording from (that checkout has no `docs/` and
no `dist/windows/`), so the MSYS2 note comes from this repository's `Makefile`
and `bundle-gtk.ps1`.

Left open: `KeyTable` lists no timeline click or zoom button, so Help is
thinner than the README there. In the report, with two smaller finds.
