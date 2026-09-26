# Editor window: implementation plan and record

Status: agreed and built on 2026-09-21, on branch `ui-editor`; all ten phases
are done. It was built phase by phase by agents following
`docs/ui-work-orders.md`, whose log is the hand-over record; the phase list at
the end of this file names the commits. The headless `--auto` path and the
subs2srs launcher were already there; this was the interactive editor the
original Subs Re-Timer had.

The plan is kept as the record of what was built: what follows describes the
code as it stands, and "Known limitations and open points" at the end lists
what it does not do and what no test on this machine could check.

## Decisions taken

- Pane labels are **Reference** (left, already timed to the video) and **Target**
  (right, the file being re-timed), matching the command line and the subs2srs
  launcher. Positions stay left/right as in the original.
- No startup file. Empty panes show a one-line hint and an Open button each.
- No dark-theme variants. subs2srs has none: its row colours are fixed light
  backgrounds with an explicit dark text colour (`color: #1A1A1A`), which stays
  legible under any GTK theme. Do the same here. Revisit only if subs2srs
  gains theme support.
- UI tests came before the timeline chart, so the chart landed with tests.
- Same platform rules as subs2srs, for the same reason: GirCore pinned at
  **0.7.0**, managed APIs only, no P/Invoke into GTK, `GSK_RENDERER=cairo` as
  the safe renderer. subs2srs's Windows bundle script is built around the DLL
  names that GirCore version loads, and reusing it is the cheapest path to a
  Windows retimer.

## Where it lives

- The class library `SubsRetimer.Gtk` (namespace `SubsRetimer.Editor`) holds
  `RetimerWindow` and its widgets and is referenced by the `subsretimer`
  executable. `Cli.RunEditor` opens the window through `EditorHost`, which
  creates the `Gtk.Application`, and keeps the stdout contract: under
  `--print-output` each path is written and flushed as the window saves it
  (since phase 11 through an `onSaved` callback that the window's save
  calls), while the returned list still decides the exit code — 0 if anything
  was saved, 2 if the window closed without saving. Since phase 12
  `EditorHost.Run` takes an `EditorRequest`: the two loaded files, the
  `--output` path Save writes the target to, the two encodings for files
  opened from the window, and that callback. `EditorHost.Probe()` tells a
  missing display from a GTK that does not load, and an exception while the
  window is built inside GTK's `activate` is carried out of the native
  callback and rethrown by `Run`. Nothing changed on the subs2srs side; its
  launcher already handled all of it.
- The window is a view over `RetimerEngine` and never computes timing itself.
  Core already provides sorted lines, `ShiftFrom`, `ShiftToMatch`,
  `ClosestIndex`, `BestOverlap`, `LargeGapFlags`, `MismatchFlags`,
  `AverageMismatchSeconds`, undo/redo, `IsDirty`, `Save` and `AutoAlign`.
- Two small Core additions, built in phase 1: the `Changed` event with a
  `Version` counter, so the window knows when to recolour, and the static
  `NextLargeGap(flags, from)` / `PreviousLargeGap(flags, from)`, so orange-row
  navigation is testable without GTK.
- `Directory.Build.props` at the repository root holds the one `<Version>`
  (and the shared `Authors`/`Copyright`/licence) for every project, so the
  command line's version option and the editor's About box cannot disagree.
  `release.yml` still overrides it with `-p:Version=` when it publishes.

## APIs verified in GirCore 0.7.0 (by reflection, 2026-09-21)

| Need | Available |
| --- | --- |
| Custom drawing | `Gtk.DrawingArea.SetDrawFunc((area, cr, w, h) => …)` with a full `Cairo.Context`: `MoveTo`, `LineTo`, `Rectangle`, `Fill`, `Stroke`, `ShowText`, `TextExtents(text, out extents)`, `Save`/`Restore`, `Clip`, `SetSourceRgb`, `SelectFontFace`, `SetFontSize`. Measured in phase 7: the line width is the **property** `LineWidth`, there is no `SetLineWidth`, and `PressedSignalArgs` carries only `NPress`/`X`/`Y`, so the button comes from `GestureSingle.GetCurrentButton()` |
| Lists | `Gtk.ColumnView` + `Gio.ListStore` of `Gtk.StringObject` with a parallel `List<RetimerLine>` (GirCore 0.7 cannot subclass `GObject.Object`); `ColumnView.ScrollTo(pos, col, ListScrollFlags, ScrollInfo)` |
| Mouse buttons | `Gtk.GestureClick.SetButton(n)`, `OnPressed`/`OnReleased` |
| Dialogs | `Gtk.FileDialog.OpenAsync/SaveAsync(window)`, `SetInitialName`; `Gtk.AlertDialog.SetButtons`, `ChooseAsync` |
| Menus and keys | `Gio.SimpleAction`, `Gtk.PopoverMenuBar.NewFromModel`, `Gtk.Application.SetAccelsForAction`, `Gtk.ShortcutController` |
| Layout helpers | `Gtk.Fixed`, `Gtk.Overlay` |
| File drag-and-drop | **Works, through `GFile`** (measured in phase 8; the older "not cleanly" here was wrong): `Gtk.DropTarget.New(Gio.FileHelper.GetGType(), Gdk.DragAction.Copy)`, `OnDrop` → `args.Value.GetObject() as Gio.File` → `GetPath()`. `Gdk.FileList` is indeed a dead end (`GetGType`/`NewFromArray` are bound, no `GetFiles`), and there is no `DropTarget.SetGtypes`, so a target takes one GType. `GetFormats()` on a target lists only that GType: GirCore binds none of the `gdk_content_formats_union_*` helpers that expand it to mime types. A GTK file drag offers `GdkFileList GFile gchararray text/uri-list text/plain;charset=utf-8`, so a `GFile` or a `GObject.Type.String` target both match; as text it arrives as plain paths, one a line. |

## The window, mapped from the original

It keeps the original's mental model: work top to bottom, orange rows are
where a shift is needed, gray rows have no counterpart in the other file.

- **Top strip**: average mismatch as `all : matched` seconds; `selected/total`
  counters over each list.
- **Two lists**: `Gtk.ColumnView` with Start and Dialogue columns in a
  `ScrolledWindow`, the file name above each. Row colour by CSS class on both
  cells of the row (`retimer-gap`, `retimer-mismatch`), set in the bind
  handler. After a shift the bound rows are refreshed in place; the store is
  rebuilt only by loading a file (rebuilding scrolls to the top and destroys
  the focused row).
- **Detail strip**: selected text and start time per side, overlap percent
  with green/red background, signed difference, then the buttons **Time
  Shift** (enabled only when both sides have a selection), **Auto Align**,
  **Undo**, **Redo**, **Save** and **Save As...**.
- **Menu**: File (Open Reference, Open Target, Save, Save As, Quit), Edit
  (Undo, Redo, Time Shift, Auto Align), Help (Help, About). Help shows
  `RetimerWindow.KeyTable`, the same table the README carries.
- **Timeline**: a 64 px strip between the menu and the top strip, centred on
  the selected target line; reference bars above, target bars below, ticks
  and labels, two zoom buttons. Bars are drawn only once both sides have a
  selection.
- **Drops**: a subtitle file dropped on either pane is loaded as that side's
  file, through a `GFile` drop target on the pane's box.
- **Auto Align**: `AutoAlign.Compute` + `Apply`, one undoable step per
  segment that moves (a segment whose offset equals the previous one's shifts
  nothing and pushes no step), then recolour; status line uses the same segment
  text the CLI prints.
- **Behaviours**: selecting a row updates the detail strip. Right-click or
  Left/Right selects the closest line on the other side by start time. Ctrl+Up
  and Ctrl+Down jump to the previous/next orange row and select its closest
  counterpart. Enter and middle-click do Time Shift. Ctrl+Z/Y undo and redo.
  Ctrl+S saves to `<name>_retimed.<ext>`; Ctrl+Shift+S opens Save As with that
  name preset. Closing with unsaved changes asks, and so does opening or
  dropping a new target over them. Title shows `*` when dirty.

Two GTK4 traps shaped the code:

- `ColumnView` binds Up/Down with every modifier, so each list's key
  controller sits in the **capture** phase; in the bubble phase Ctrl+Up never
  arrives.
- `Widget.OnDestroy` fires for neither `Close()` nor `Destroy()`, so the saved
  state lives in the window (`SavedPaths`) and the exit code is computed from
  what `EditorHost.Run` returns, not from a destroy handler.

## Facts checked in the code (2026-09-21)

So that phases do not re-derive them:

- `RetimerEngine` (`SubsRetimer.Core/RetimerEngine.cs`, ~230 lines) keeps
  `ReferenceLines` / `TargetLines` sorted by start; `ShiftFrom(fromIndex,
  delta)` moves every target line from that index and pushes an undo snapshot
  of `(Start, End)`; `Undo`/`Redo` return false when there is nothing to do;
  `Save(outputPath?)` returns the path written and marks the undo depth it
  saved at as clean (since phase 6 `IsDirty` is "the undo depth differs from
  that clean depth", so undoing back to the loaded or saved state is clean
  again; -1 once the clean state is out of reach).
  `LargeGapFlags(lines, firstBaseline?)` flags a line whose start is more than
  28 s after the previous line's start (the first line only against the other
  file's first start). `MismatchFlags(lines, others)` is true where
  `BestOverlap` is 0. Since phase 1 it also has `Changed` / `Version` (raised
  by every operation that changed something) and the static
  `NextLargeGap(flags, from)` / `PreviousLargeGap(flags, from)`.
- `Cli.RunEditor` (`SubsRetimer/Cli.cs`) validates the given paths with
  `LoadChecked`, then (since phase 2) opens the window through the seams
  `Cli.CanOpenDisplay` / `Cli.RunWindow` and turns the saved paths into the
  exit code, catching everything the editor can throw.
  `Cli.Run` maps `IOException`, `ArgumentException`, `FormatException` and
  friends to exit 1 with the message on stderr; other exceptions propagate.
- **Detecting "no display" in GirCore 0.7** (measured in phase 2): the route
  this plan first assumed does not work. `Gtk.Module.Initialize()` calls
  `gtk_init()`, which prints "Failed to open display" and **exits the
  process** when there is none, and every GTK or GDK call before it fails
  with `DllNotFoundException` because that method is what installs GirCore's
  library resolver. `Gdk.Module.Initialize()` installs the resolver alone;
  `Gdk.Display.GetDefault()` is null until something opens a display, so the
  question is answered by `Gdk.Display.Open(null)` returning null. A display
  opened that way becomes the default and is reused by GTK afterwards.
  `EditorHost.CanOpenDisplay` does exactly this.
- subs2srs starts GTK with `Gtk.Application.New(id, Gio.ApplicationFlags.FlagsNone)`
  and `RunWithSynchronizationContext(null)` (`subs2srs/Program.cs` line 75).
  Its UI test fixture uses `ApplicationFlags.NonUnique`, `Hold()` to keep the
  loop alive with no windows, and sets a `GtkSynchronizationContext` in
  `OnActivate` (`subs2srs.UiTests/Harness/GtkFixture.cs`). That last part was
  not ported (phase 6): `RunWithSynchronizationContext(null)` installs
  GirCore's own main-loop context, and it was measured that an `await` inside
  a GTK lambda continues on the GTK thread without it.
- Worked examples of the list pattern in GirCore 0.7:
  `subs2srs/DialogPreview.cs` lines 497 to 665 (CSS provider via
  `Gtk.CssProvider.New()` + `LoadFromString` + `Gtk.StyleContext.AddProviderForDisplay`;
  `Gtk.SignalListItemFactory` setup/bind; `Gtk.ColumnViewColumn.New(title,
  factory)`; row colour by `AddCssClass`/`RemoveCssClass` on the cell widget)
  and `subs2srs/GtkColumnViewHelper.cs` (column sizing: never combine
  `SetExpand(true)` with `SetFixedWidth` on one column).
- Screenshot of a window without a display server:
  `subs2srs.UiTests/Harness/Screenshot.cs` (`Gtk.WidgetPaintable` →
  `Gtk.Snapshot` → `Gsk.CairoRenderer.RenderTexture` → `Texture.SaveToPng`).
- The subsretimer repository is LF-only (no CRLF files to protect).
- This container has .NET SDK 10.0.112, `libgtk-4-1` and `xvfb-run`; the
  GirCore 0.7.0 packages are in the NuGet cache. Editor smoke runs are
  `GSK_RENDERER=cairo xvfb-run -a …`.

## Phases

Work orders, rules and the hand-over log: `docs/ui-work-orders.md`. Each phase
leaves the tree building, the tests green and committed on `ui-editor`.

1. **Core: change notification and gap navigation.** Done 2026-09-21
   (`core: notify on change and navigate large gaps`).
2. **Gtk skeleton: window, lists, colours, detail strip, `RunEditor`.**
   Done 2026-09-21 (`gtk: add the editor window with the two line lists`,
   `cli: open the editor window from the command line`).
3. **Editing: Time Shift, undo/redo, dirty state, Save/Save As, close prompt.**
   Done 2026-09-21 (`gtk: edit, save and ask before closing with changes`,
   `cli: print the paths the editor saved under --print-output`).
4. **Keyboard and mouse: menu, accelerators, closest-line and gap navigation.**
   Done 2026-09-21 (`gtk: add the menu, accelerators and list navigation`).
5. **Auto Align in the editor.** Done 2026-09-21
   (`gtk: align the whole target from the editor window`).
6. **UI test harness, tests for phases 2 to 5, CI job.** Done 2026-09-21
   (`tests: add a GTK UI test harness for the editor window`, `tests: cover
   the editor window's phases 2 to 5 with UI tests`); the lead added the
   `ui-tests` CI job and `make test-ui` from the phase report.
7. **Timeline chart.** Done 2026-09-21 (`gtk: draw the timeline chart under
   the menu`, `tests: cover the timeline chart with UI tests`).
8. **Drag-and-drop spike.** Done 2026-09-21 (`gtk: load a subtitle file
   dropped on either pane`, `tests: cover the dropped-file handler with UI
   tests`); drops kept, through `GFile`. A real drag is still a manual check.
9. **Packaging: desktop file, icon, Windows bundle.** Done 2026-09-21
   (`dist: give the tool the original Subs Re-Timer icon`, `dist: bundle the
   GTK runtime for a self-contained Windows build`); the lead added the
   release workflow on `v*` tags (`build: publish a Windows zip on v* tags`).
10. **Documentation pass.** Done 2026-09-21 (`docs: describe the editor in the
    README, changelog and plan`).
11. **Corrective fixes after the documentation pass.** Done 2026-09-21
    (`gtk: ask before a new target replaces unsaved changes`, `cli: print each
    saved path as the editor writes it`, `gtk: list the timeline's bindings in
    the key table`, `build: keep the version in one Directory.Build.props`,
    `docs: record the phase 11 fixes`).
12. **Fixes from two code reviews of the branch.** Done 2026-09-26; each
    finding was checked with a test that fails on the code before the fix, and
    the ones that held were fixed. The log in `docs/ui-work-orders.md` lists
    them and the commits.

Phases 1 to 5 were the useful product and could be built and smoke-tested
headless; phase 6 came before phase 7 so the chart landed with tests.

### What each phase covers

1. `RetimerEngine.Changed` event and a `Version` counter, raised on load,
   shift, undo, redo and save; static `NextLargeGap(flags, from)` /
   `PreviousLargeGap(flags, from)` over a `bool[]` from `LargeGapFlags`,
   returning -1 when there is none. Unit tests only.
2. `SubsRetimer.Gtk` class library (GirCore.Gtk-4.0 0.7.0) with
   `RetimerWindow`: top strip, two `ColumnView` lists fed from the engine
   (`Gio.ListStore` of `Gtk.StringObject` holding the row index, parallel
   `List<RetimerLine>`), row colours by CSS class, file names, `selected/total`
   counters, detail strip (text, start, overlap percent with green/red
   background, signed difference), selection sync into the detail strip, Open
   Reference / Open Target buttons with an `.ass/.ssa/.srt` filter. Rows are
   refreshed **in place** on `Changed`: track the bound cell widgets and update
   their text and classes; never rebuild the store. `Cli.RunEditor` creates the
   `Gtk.Application`, opens the window with the given files and returns 2 on
   close (nothing saved yet), 1 with a stderr message when GTK cannot open a
   display. An `internal` test surface (`InternalsVisibleTo` for
   `SubsRetimer.Tests` and `SubsRetimer.UiTests`) exposes load, select and
   the row-state query the tests need.
3. Time Shift button (`ShiftToMatch` from the selected pair, enabled only with
   both selections), Undo/Redo, `*` in the title when dirty, Save to
   `<name>_retimed.<ext>` and Save As with that name preset, saved paths
   collected so `RunEditor` prints them under `--print-output` and exits 0,
   close prompt when dirty (`Gtk.AlertDialog`, Save / Discard / Cancel).
4. `Gio.SimpleAction` menu (Open Reference, Open Target, Save, Save As, Undo,
   Redo, Time Shift, Auto Align placeholder, Help, About), accelerators through
   `SetAccelsForAction`, a key controller in the **capture** phase for
   Left/Right (closest line on the other side), Ctrl+Up/Down (previous/next
   orange row and its counterpart), Enter (Time Shift); right-click = closest
   line, middle-click = Time Shift, through `Gtk.GestureClick.SetButton`.
5. Auto Align button and menu item: `AutoAlign.Compute` + `Apply`, one undo
   step per breakpoint, status line with the CLI's `Describe` text.
6. `SubsRetimer.UiTests`: port of `subs2srs.UiTests/Harness` (GTK thread
   fixture, `Pump`, `Screenshot`, leak-checking scope), tests that call the
   window's internal surface for the behaviours of phases 2 to 5,
   `SUBSRETIMER_UITEST_ARTIFACTS=<dir>` for PNGs. The CI job is added by the
   lead from the agent's report.
7. `Gtk.DrawingArea` + Cairo timeline: two rows of bars over the visible
   window, scale ticks and labels, active lines highlighted, zoom, left-click
   advances the target selection, right-click goes back, middle-click shifts;
   a one-to-one port of the original `ChartSubs.cs`. With UI tests.
8. Try file drops: `GObject.Type.String` (`text/plain` beside `text/uri-list`)
   and `Gdk.FileList` through `Value.GetBoxed`. Keep it only if it works
   cleanly in 0.7; otherwise record why in the report and leave drops out.
   Outcome: neither of those two, but a third way — a drop target for the
   `GFile` GType — is clean and was kept; see the row above and the log.
9. Drop `NoDisplay` from the desktop file, add an icon, adapt subs2srs's
   `dist/windows/bundle-gtk.ps1` and `smoke.ps1`; the release workflow on
   `v*` tags is added by the lead.
10. README status, editor usage and key table, CHANGELOG, and this document
    turned into the record of what was built, with "Known limitations and open
    points" and the manual checklist completed from the log; the subs2srs docs
    line that says the editor is not ported is a separate repository
    (reported, done by the lead).

## Known limitations and open points

Found while building; none is a window bug.

**Auto Align, in Core.** The button runs exactly what `--auto` runs, so these
show in both:

- It folds a leading block shorter than `AutoAlignOptions.SwitchPenalty`
  (2.5 fully matched lines) into the following segment, so a three-line intro
  before a 15 s cut ends up 15 s early. A Core tuning question, not the
  editor's.
- A segment boundary can land one line after the real cut (seen once: cut at
  line 10, boundary at 11), leaving that line off while still counted as
  matched. Same class as the point above.
- It can push early lines negative; `TimeFormat` clamps negatives to zero when
  formatting and saving, so such rows show and save as `0:00:00.00`.
  Pre-existing and deliberate, but the editor makes it easy to hit.

**What no test on this machine can check.** All of it is in the manual
checklist below:

- Real key delivery to the capture-phase controllers: GirCore 0.7 exposes no
  event synthesis, so the tests call the methods the key handler calls
  (`SelectClosestOnOtherSide`, `JumpToGap`, `TimeShift`), not the keys. The
  same holds for the mouse buttons on the lists and on the timeline chart
  (`ChartClick`), and for a file dropped on a pane, where the test does call
  the handler, `DropFile`, with the `GObject.Value` a drop would carry — but a
  real drag from a file manager is still unproven.
- The editor window on Windows. `dist/windows/smoke.ps1` checks the bundle's
  layout, `--version`, `--check-editor` (GTK loads from the bundle, a display
  opens, `gtk_init()` reads the bundled schemas) and `--auto`, and
  deliberately does not look for a window: a console process's
  `MainWindowHandle` is its console, so that check would pass with no GTK at
  all. Neither PowerShell script has run on Windows. In phase 12 `smoke.ps1`
  was parsed by PowerShell 7 and its steps after the layout check ran on
  Linux against the Linux build; `bundle-gtk.ps1` has never run.
- The Wayland side of the desktop entry. On X11 the window's `WM_CLASS` is
  `subsretimer` (measured with `xprop` under Xvfb), which `StartupWMClass`
  names; on Wayland GTK 4 sends the application id as the app_id, which the
  entry's file name now matches. No compositor was available to watch it.

**Chosen and left as they are:**

- A drop of several files loads the first; a pane shows one file, and a
  multiple drop is a slip more often than a request.
- A pane's drop target takes one GType, `GFile`: GirCore 0.7 binds no
  `DropTarget.SetGtypes`, and `GetFormats()` on a target lists that GType and
  no mime type, because none of the `gdk_content_formats_union_*` helpers are
  bound. `Gdk.FileList` cannot be read at all (`GetGType`/`NewFromArray` are
  bound, `GetFiles` is not).
- `OutputType` stays `Exe` on Windows too, because `--auto` has to reach a
  real stdout, so a console window accompanies the editor when it is started
  from Explorer. subs2srs launches it with the console hidden.
- Help's `KeyTable` lists the lists' keys and buttons and, since phase 11, the
  timeline's clicks and its zoom buttons. The README's "Keys and mouse" table
  is the same 18 entries; keep the two in step if either gains a binding.
- Replacing the target asks about unsaved changes (phase 11). The new file is
  read first, so one that cannot be used refuses the drop with an error, and
  while any prompt is up a target drop is refused outright (phase 12). A drop
  that is answered Cancel cannot be taken back, though: the drop signal cannot
  wait for the answer, so `DropFile` accepts it at once and loads the file
  only after the prompt, and GTK sees it as accepted.
- A Time Shift with a negative delta can move target rows before the rows
  above them, and the target list is never re-sorted (undo, the row numbers
  and Auto Align's segments are all by index). Since phase 12 every "closest
  line" lookup and the gray rows are right in any order. The orange rows are
  still measured against the row above in the list, which is what the list
  shows.
- A target that is not valid text in its encoding is refused, in both modes
  and from the window, instead of being saved with U+FFFD where its
  characters were. The reference is only read for its timings and loads
  either way.

### Manual checklist

What the suites cannot reach. Run it on a real Linux desktop, and steps 1 to 8
again on Windows from the output of `make publish-windows`, unzipped on a
machine with no MSYS2 and no GTK of its own.

1. `subsretimer ref.srt target.ass`: both lists fill, orange and gray rows
   appear, the counters and the average mismatch look sane, and Help → Help
   and Help → About open.
2. In a list, Left and Right select the closest line on the other side and
   move the focus there.
3. Ctrl+Up and Ctrl+Down walk the orange rows. This is what the capture-phase
   key controller exists for: if they move the selection by one row instead,
   `ColumnView` took the key first.
4. With a row selected on each side, Enter runs Time Shift; middle-click on a
   row does the same, right-click selects the closest line on the other side.
5. On the timeline: left click walks the target selection on, right click
   back, middle click shifts; `+` and `-` zoom by 2 s, by 10 s with the right
   button and all the way with the middle button; the strip stays 64 px high
   and follows the selection.
6. Drag a subtitle file from the file manager onto each pane: it loads. Drag
   something that is not a subtitle file: the error dialog appears and nothing
   loads, and over unsaved changes nothing is asked first.
7. Auto Align on a pair with a real cut, then Ctrl+Z back to the start: the
   status line reports the segments and the title's `*` disappears at the
   loaded state.
8. Save (Ctrl+S) with a `<name>_retimed.<ext>` already beside the target: the
   Replace prompt appears. Save As elsewhere, shift, Save: the Save As file
   changes and the default name does not. Then close with unsaved changes and
   try Cancel, Discard and Save in turn; shift again and drop another target
   on the right pane for the same three answers (the question names the new
   file). With `--print-output`, each saved path appears on stdout as the file
   is written, once, and nothing else does.
9. Linux: `sudo make install`, then start Subs Re-Timer from the desktop menu
   and check its icon and that the running window groups with the launcher,
   on X11 and on Wayland. Windows: the bundled `subsretimer.exe` opens the
   window at all (`smoke.ps1` stops just short of that), shows the red R
   icon, and still answers `--version` and `--auto` in the console beside it.
10. The timeline with Japanese dialogue: the bars show the text, not boxes.
    Tab to the `+` button and press Space: the scale changes.

## Open defaults

Chosen by the lead where the original tool gave no guidance; change if wrong:

- Window title `Subs Re-Timer`, application id `io.github.jhhr.subsretimer`.
- Row colours: orange `#FFD8A8`, gray `#E0E0E0`, both with `color: #1A1A1A`;
  overlap background green `#C8F0C8` / red `#F5C0C0`.
- Default window size 1100 × 700.
