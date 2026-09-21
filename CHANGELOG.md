# Changelog

## Unreleased

**Features:**
- Interactive editor window (GTK 4): the two files side by side with orange rows where a cut is likely and gray rows with no counterpart, Time Shift, undo/redo, Save and Save As, menu, accelerators, mouse buttons and a prompt before closing with unsaved changes.
- Auto Align in the editor: the `--auto` alignment over the whole target, one undoable shift per offset, with the segments it used in a status line.
- Timeline chart under the menu: the seconds around the selected target line, with zoom buttons and the original's click behaviour.
- A subtitle file dropped on either pane is loaded as that side's file.
- The tool now carries the original Subs Re-Timer icon and its desktop entry appears in application menus; `make install` installs both.
- Windows: `make publish-windows` produces a self-contained `win-x64` build with the GTK 4 runtime bundled, and a `v*` tag publishes it as a zip.

**Fixed:**
- Undoing back to the loaded or saved state no longer leaves the file marked as changed.

## 0.1.0

**Features:**
- Core library: raw-preserving `.ass`/`.ssa`/`.srt` reading and writing, shift-from-line engine with undo/redo, gap and mismatch detection, average mismatch statistic (ported from Subs Re-Timer 1.0).
- Timing-based auto-align: piecewise-constant offset detection (candidate histogram, overlap scoring, dynamic programming with switch penalty, median refinement).
- `subsretimer` command line with `--auto`, `--output`, `--ref-encoding`, `--target-encoding`, `--print-output`, and the stdout/exit-code contract used by subs2srs.

**Not yet:**
- Interactive editor window.
