# Editor window: implementation plan

Status: agreed 2026-09-21, in progress on branch `ui-editor`. Built phase by
phase by agents following `docs/ui-work-orders.md`; the phase list at the end
of this file records what is done. The headless `--auto` path and the subs2srs
launcher are done; this is the interactive editor the original Subs Re-Timer
had.

## Decisions taken

- Pane labels are **Reference** (left, already timed to the video) and **Target**
  (right, the file being re-timed), matching the command line and the subs2srs
  launcher. Positions stay left/right as in the original.
- No startup file. Empty panes show a one-line hint and an Open button each.
- No dark-theme variants. subs2srs has none: its row colours are fixed light
  backgrounds with an explicit dark text colour (`color: #1A1A1A`), which stays
  legible under any GTK theme. Do the same here. Revisit only if subs2srs
  gains theme support.
- UI tests come before the timeline chart.
- Same platform rules as subs2srs, for the same reason: GirCore pinned at
  **0.7.0**, managed APIs only, no P/Invoke into GTK, `GSK_RENDERER=cairo` as
  the safe renderer. subs2srs's Windows bundle script is built around the DLL
  names that GirCore version loads, and reusing it is the cheapest path to a
  Windows retimer.

## Where it lives

- New class library `SubsRetimer.Gtk` holding `RetimerWindow` and its widgets,
  referenced by the `subsretimer` executable. `Cli.RunEditor` creates a
  `Gtk.Application`, opens the window with whatever files were given, and
  keeps the stdout contract: each save prints the path when `--print-output`
  is set; exit 0 if anything was saved, 2 if the window closed without saving.
  Nothing changes on the subs2srs side; its launcher already handles both.
- The window is a view over `RetimerEngine` and never computes timing itself.
  Core already provides sorted lines, `ShiftFrom`, `ShiftToMatch`,
  `ClosestIndex`, `BestOverlap`, `LargeGapFlags`, `MismatchFlags`,
  `AverageMismatchSeconds`, undo/redo, `IsDirty`, `Save` and `AutoAlign`.
- Two small Core additions: a change notification (event or version counter)
  so the window knows when to recolour, and `NextLargeGap(from)` /
  `PreviousLargeGap(from)` so orange-row navigation is testable without GTK.

## APIs verified in GirCore 0.7.0 (by reflection, 2026-09-21)

| Need | Available |
| --- | --- |
| Custom drawing | `Gtk.DrawingArea.SetDrawFunc((area, cr, w, h) => …)` with a full `Cairo.Context`: `MoveTo`, `LineTo`, `Rectangle`, `Fill`, `Stroke`, `ShowText`, `TextExtents`, `Save`/`Restore`, `Clip` |
| Lists | `Gtk.ColumnView` + `Gio.ListStore` of `Gtk.StringObject` with a parallel `List<RetimerLine>` (GirCore 0.7 cannot subclass `GObject.Object`); `ColumnView.ScrollTo(pos, col, ListScrollFlags, ScrollInfo)` |
| Mouse buttons | `Gtk.GestureClick.SetButton(n)`, `OnPressed`/`OnReleased` |
| Dialogs | `Gtk.FileDialog.OpenAsync/SaveAsync(window)`, `SetInitialName`; `Gtk.AlertDialog.SetButtons`, `ChooseAsync` |
| Menus and keys | `Gio.SimpleAction`, `Gtk.PopoverMenuBar.NewFromModel`, `Gtk.Application.SetAccelsForAction`, `Gtk.ShortcutController` |
| Layout helpers | `Gtk.Fixed`, `Gtk.Overlay` |
| File drag-and-drop | **Not cleanly**: `Gtk.DropTarget` exists but `Gdk.FileList` exposes no way to read the files and `Gio.File` (an interface) has no `GetGType` for the drop type. See step 6. |

## The window, mapped from the original

Keep the original's mental model: work top to bottom, orange rows are where a
shift is needed, gray rows have no counterpart in the other file.

- **Top strip**: average mismatch as `all : matched` seconds; `selected/total`
  counters over each list.
- **Two lists**: `Gtk.ColumnView` with Start and Dialog columns in a
  `ScrolledWindow`, the file name above each. Row colour by CSS class on both
  cells of the row (`retimer-gap`, `retimer-mismatch`), set in the bind
  handler. After a shift, re-bind rows in place; never rebuild the model
  (rebuilding scrolls to the top and destroys the focused row).
- **Detail strip**: selected text and start time per side, overlap percent
  with green/red background, signed difference, **Time Shift** (enabled only
  when both sides have a selection), **Auto Align**.
- **Menu**: Open Reference, Open Target, Save, Save As, Undo, Redo, Time
  Shift, Auto Align, Help, About.
- **Auto Align**: `AutoAlign.Compute` + `Apply`, one undoable step per
  breakpoint, then recolour; status line uses the same segment text the CLI
  prints.
- **Behaviours**: selecting a row updates the detail strip. Right-click or
  Left/Right selects the closest line on the other side by start time. Ctrl+Up
  and Ctrl+Down jump to the previous/next orange row and select its closest
  counterpart. Enter and middle-click do Time Shift. Ctrl+Z/Y undo and redo.
  Ctrl+S saves to `<name>_retimed.<ext>`; Ctrl+Shift+S opens Save As with that
  name preset. Closing with unsaved changes asks. Title shows `*` when dirty.

Two GTK4 traps to build around from the start:

- `ColumnView` binds Up/Down with every modifier: the key controller must be
  in the **capture** phase or Ctrl+Up never arrives.
- `Widget.OnDestroy` does not fire for a window closed with `Close()`. Track
  the saved state in the window and compute the exit code from it, not from a
  destroy handler.

## Facts checked in the code (2026-09-21)

So that phases do not re-derive them:

- `RetimerEngine` (`SubsRetimer.Core/RetimerEngine.cs`, ~230 lines) keeps
  `ReferenceLines` / `TargetLines` sorted by start; `ShiftFrom(fromIndex,
  delta)` moves every target line from that index and pushes an undo snapshot
  of `(Start, End)`; `Undo`/`Redo` return false when there is nothing to do;
  `Save(outputPath?)` returns the path written and clears `IsDirty`.
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
  `OnActivate` (`subs2srs.UiTests/Harness/GtkFixture.cs`).
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
5. **Auto Align in the editor.** Not started.
6. **UI test harness, tests for phases 2 to 5, CI job.** Not started.
7. **Timeline chart.** Not started.
8. **Drag-and-drop spike.** Not started.
9. **Packaging: desktop file, icon, Windows bundle.** Not started.
10. **Documentation pass.** Not started.

Phases 1 to 5 are the useful product and can be built and smoke-tested
headless. Phase 6 before phase 7 so the chart lands with tests.

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
9. Drop `NoDisplay` from the desktop file, add an icon, adapt subs2srs's
   `dist/windows/bundle-gtk.ps1` and `smoke.ps1`; the release workflow on
   `v*` tags is added by the lead.
10. README status, CHANGELOG, this document's "Known limitations and open
    points", the subs2srs docs line that says the editor is not ported
    (separate repository: reported, done by the lead).

## Open defaults

Chosen by the lead where the original tool gave no guidance; change if wrong:

- Window title `Subs Re-Timer`, application id `io.github.jhhr.subsretimer`.
- Row colours: orange `#FFD8A8`, gray `#E0E0E0`, both with `color: #1A1A1A`;
  overlap background green `#C8F0C8` / red `#F5C0C0`.
- Default window size 1100 × 700.
