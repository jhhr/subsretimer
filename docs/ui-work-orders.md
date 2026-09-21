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

## 3. State of the code (kept by the lead; as of 2026-09-21, after phase 1)

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
  `--print-output`. `RunEditor` is a stub that exits 1.
- `SubsRetimer.Tests`: xUnit, 66 tests, `Fixtures.Lines` (periodic) and
  `Fixtures.Dialogue(count, seed)` (irregular timings; use this for anything about
  alignment or gaps).
- No GTK project yet. No UI tests yet.

## 4. Phases

Done: 1.

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
