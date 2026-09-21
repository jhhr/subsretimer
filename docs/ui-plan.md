# Editor window: implementation plan

Status: plan, agreed 2026-09-21. Not started. The headless `--auto` path and the
subs2srs launcher are done; this is the interactive editor the original Subs
Re-Timer had.

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

## Order of work

1. **Skeleton and lists.** `SubsRetimer.Gtk`, `RetimerWindow`, lists fed from
   the engine, colours, detail strip, selection sync, counters, Open buttons
   with an `.ass/.ssa/.srt` filter. Wire `RunEditor`, keeping the exit codes.
   Done when a headless harness can open two files, select rows and see the
   right colours.
2. **Editing.** Time Shift, Undo, Redo, dirty tracking, Save and Save As with
   the stdout print, close prompt. Then the keyboard table and the
   closest-line gestures.
3. **Auto Align in the editor** with the segment summary in the status line.
4. **UI tests.** Port subs2srs's `subs2srs.UiTests/Harness` (about 480 lines:
   one GTK thread fixture, `Pump` for settling, `Screenshot`, a scope that
   fails on leaked windows) into `SubsRetimer.UiTests`. Tests call handlers
   directly, as subs2srs does; `SUBSRETIMER_UITEST_ARTIFACTS=<dir>` saves a
   PNG per window. Expose an `internal` test surface rather than reflecting.
   Extend `ci.yml` with a Linux job that installs GTK 4 and runs the UI tests
   under Xvfb with `GSK_RENDERER=cairo`.
5. **Timeline chart.** `Gtk.DrawingArea` + Cairo: two rows of bars for the
   lines in the visible window, scale ticks and labels, active lines
   highlighted, zoom in/out. Left-click advances the target selection,
   right-click goes back, middle-click shifts. A port of the original
   `ChartSubs.cs` (about 440 lines of GDI+, one-to-one onto Cairo).
6. **Drag-and-drop spike.** Try `GObject.Type.String` drops (file managers
   often offer `text/plain` beside `text/uri-list`) and reading `Gdk.FileList`
   through `Value.GetBoxed`. If neither works cleanly in 0.7, leave drops out;
   Open buttons and command-line arguments cover the workflow.
7. **Packaging and docs.** Drop `NoDisplay` from the desktop file, add an icon,
   adapt subs2srs's `dist/windows/bundle-gtk.ps1` and `smoke.ps1` for a
   Windows zip on `v*` tags. Update this repo's README status and the
   subs2srs docs line that says the editor is not ported.

Steps 1 to 3 are the useful product and can be built and smoke-tested
headless. Step 4 before step 5 so the chart lands with tests.
