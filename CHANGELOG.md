# Changelog

## Unreleased

**Features:**
- Interactive editor window (GTK 4): the two files side by side with orange rows where a cut is likely and gray rows with no counterpart, Time Shift, undo/redo, Save and Save As, menu, accelerators, mouse buttons and a prompt before closing with unsaved changes.
- Auto Align in the editor: the `--auto` alignment over the whole target, one undoable shift per offset, with the segments it used in a status line.
- Timeline chart under the menu: the seconds around the selected target line, with zoom buttons and the original's click behaviour.
- A subtitle file dropped on either pane is loaded as that side's file.
- The tool now carries the original Subs Re-Timer icon and its desktop entry appears in application menus; `make install` installs both.
- Windows: `make publish-windows` produces a self-contained `win-x64` build with the GTK 4 runtime bundled, and a `v*` tag publishes it as a zip.
- With `--print-output` the editor prints each saved path the moment the file is written, as `--auto` does, instead of only when the window closes.
- `--check-editor` says whether the editor can start here (GTK 4 loads, a display opens) without opening it; the Windows release's smoke test now runs it, so a bundle whose GTK does not start fails the release.

**Fixed:**
- Undoing back to the loaded or saved state no longer leaves the file marked as changed.
- Opening or dropping a new target file over unsaved changes now asks Save / Discard / Cancel instead of throwing the changes away without a word.
- Help → Help now also lists the timeline's mouse buttons and its zoom buttons.
- `-o`/`--output` is honoured in the editor: Save writes there, as `--auto` does. It was ignored and Save always wrote `<TARGET>_retimed.<ext>`.
- After Save As, Save (and Save in the close and replace prompts) writes the file chosen there instead of going back to `<TARGET>_retimed.<ext>`.
- Save in the editor asks before it replaces an existing `<TARGET>_retimed.<ext>` it did not write, instead of overwriting an earlier result without a word.
- Saving under the other format's extension (an ASS target as `.srt`) is refused, in Save As and with `--output`, instead of writing ASS text into a `.srt` file.
- Files opened or dropped in the editor are read in the `--ref-encoding`/`--target-encoding` encodings instead of always as UTF-8, and a target that is not valid text in its encoding is refused in both modes instead of being saved with U+FFFD in place of every character that could not be read.
- A new target is checked before the unsaved-changes question, so a video or a broken file dropped on the target pane is an error at once, not after a Save it could never need.
- The replace-target prompt asks about opening the new file, not about closing; a target dropped while a prompt is up is refused instead of accepted and ignored; and closing the window while that prompt is up closes it once the prompt is answered instead of being lost.
- On Wayland the running editor is matched to its desktop entry, and so gets its icon: the entry is installed as `io.github.jhhr.subsretimer.desktop`, the application id, and carries `StartupWMClass=subsretimer` for X11.
- A machine without GTK 4 is told that GTK 4 could not be loaded, not that there is no display.
- An exception while the editor window starts reaches the command line as `subsretimer: the editor failed: ...` and exit 1, instead of GirCore ending the process with a stack trace.
- Left/Right, right-click, gap jumps and the gray rows find the right counterpart after a Time Shift that moved target lines before the ones above them.
- Rows scrolled into view could miss the in-place refresh after a shift and keep showing the old time.
- The timeline draws Japanese (and any script "Sans" lacks) through Pango instead of as empty boxes.
- The timeline's `+` and `-` buttons now work from the keyboard (Space, Enter).
- Auto Align repaints the window once instead of once per segment, a save repaints only the title, and loading a file fills its list in one step.

## 0.1.0

**Features:**
- Core library: raw-preserving `.ass`/`.ssa`/`.srt` reading and writing, shift-from-line engine with undo/redo, gap and mismatch detection, average mismatch statistic (ported from Subs Re-Timer 1.0).
- Timing-based auto-align: piecewise-constant offset detection (candidate histogram, overlap scoring, dynamic programming with switch penalty, median refinement).
- `subsretimer` command line with `--auto`, `--output`, `--ref-encoding`, `--target-encoding`, `--print-output`, and the stdout/exit-code contract used by subs2srs.

**Not yet:**
- Interactive editor window.
