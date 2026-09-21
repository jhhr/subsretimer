# subsretimer

Re-time a subtitle file to match the timings of another subtitle file.

This is a GTK4 / .NET port of **Subs Re-Timer** by Christopher Brochtrup,
the companion tool that shipped with [subs2srs](https://sourceforge.net/projects/subs2srs/).
It is useful in one specific situation: you have two subtitle files for the
same episode, one already timed to your video and one that is not (typically
because its video had a sponsor segment, opening or eyecatch cut differently),
and you want the second one to follow the first.

It is not a general subtitle editor like Aegisub.

## Status

- `--auto` headless alignment: working, tested.
- Interactive editor window: working on Linux and Windows (GTK 4). Running
  without `--auto` opens it.

## Usage

```
subsretimer [options] [REFERENCE] [TARGET]

  REFERENCE               subtitle file already timed to the video (left pane)
  TARGET                  subtitle file to be retimed (right pane)

Options:
  --auto                  run auto-align and save without opening the editor
  -o, --output PATH       output path (default: <TARGET>_retimed.<ext>)
  --ref-encoding NAME     encoding of REFERENCE (default: utf-8)
  --target-encoding NAME  encoding of TARGET (default: utf-8)
  --print-output          print the path of each saved file to stdout
  -h, --help              show this help
  --version               show the version
```

Supported formats: `.ass`, `.ssa`, `.srt`. Output keeps the input's encoding,
byte-order mark, line endings, header, styles, numbering and text; only the
timestamps change.

Without `--auto` the editor window opens. Both file arguments are optional
there: a pane left empty is filled from the window instead. Exit status is `0`
when something was saved, `2` when the window was closed without saving, and
`1` on an error — including `cannot open a display; use --auto`, the answer on
a machine with no display server.

Batch example, one season with English subs timed to the video and Japanese
subs that are not:

```sh
for jp in JP/*.ass; do
  en="EN/$(basename "${jp%.ass}").srt"
  subsretimer --auto "$en" "$jp"
done
```

### How auto-align works

Both files describe the same dialogue on the same footage, so their timing
structure is a shared fingerprint. `--auto` histograms start-time
differences to find a few candidate offsets, scores every target line under
each candidate by how well it overlaps a reference line, then picks one
candidate per line with a dynamic program that penalises changing offset.
The result is a piecewise-constant shift with a handful of breakpoints, one
per cut (sponsor, opening, eyecatch). Lines with no counterpart in the
reference (sound cues, music in closed-caption subs) simply move with their
neighbours. Each segment's offset is refined to the median difference of its
overlapping pairs.

## Using the editor

`subsretimer REFERENCE TARGET` opens both files; either argument can be left
out. In the window a pane is filled by its **Open Reference** / **Open Target**
button, by File → Open Reference / Open Target (Ctrl+O, Ctrl+Shift+O), or by
dropping a subtitle file on the pane from a file manager — a drop of several
files loads the first.

**The two lists** are the reference on the left and the target on the right,
each with its file name, a `selected/total` counter and the Start and Dialogue
columns. The strip above them gives the average mismatch between the files,
over all lines and over the matched ones alone. Row colours say where to look:

- **orange** — the line starts more than 28 seconds after the one above it
  (the first line is measured against the other file's first line). That is
  where a cut usually is.
- **gray** — no line in the other file overlaps this one: a sound cue, or a
  stretch that is still out of sync. Nothing is gray until both files are
  loaded, and a row that is both shows orange.

**The detail strip** under the lists shows the selected line of each side with
its start time, and between them how much of the target line the reference
line covers (green from 50 % up, red below) and the signed difference of the
two start times — the shift Time Shift would apply.

**Time Shift** moves the selected target line, and every line after it, so
that it starts with the selected reference line: find the same moment of
dialogue on both sides and press Enter. It needs a selection in each list.
Undo and redo (Ctrl+Z, Ctrl+Y) step through the shifts.

**Auto Align** does to the whole target what `--auto` does, without leaving
the window: one offset per run of lines, applied as one undoable shift each.
The status line under the lists then reports the segments it used, or
`Auto Align: no alignment found`.

**The timeline** under the menu draws the seconds around the selected target
line, reference lines in the upper row and target lines in the lower one, with
the selected pair highlighted; until both sides have a selection it shows only
the scale. Clicking it works as in the original: the left button moves the
target selection on by a line, the right button moves it back, the middle
button does a Time Shift. The `+` and `-` buttons beside it change the scale
by 2 seconds, by 10 with the right mouse button, and all the way to 4 or 120
seconds with the middle one.

**Saving.** Save (Ctrl+S) writes `<name>_retimed.<ext>` beside the target
file; Save As (Ctrl+Shift+S) offers that name elsewhere. The title carries the
target's name and a `*` while there are unsaved changes, and closing with
changes asks Save / Discard / Cancel. Under `--print-output` every path the
window saved is printed when it closes.

### Keys and mouse

The window shows this table under Help → Help.

| Key or button | What it does |
| --- | --- |
| Ctrl+O | Open the reference file |
| Ctrl+Shift+O | Open the target file |
| Ctrl+S | Save as `<name>_retimed.<ext>` |
| Ctrl+Shift+S | Save As... |
| Ctrl+Z / Ctrl+Y | Undo / Redo |
| Ctrl+Q | Quit |
| Left / Right (in a list) | Select the closest line on the other side and go there |
| Ctrl+Up / Ctrl+Down (in a list) | Previous / next orange row and its closest counterpart |
| Enter (in a list) | Time Shift: move the target onto the selected reference line |
| Right-click (in a list) | Select the closest line on the other side |
| Middle-click (in a list) | Time Shift |
| Drop a file (on a pane) | Load it as that side's subtitles |

## Contract for other programs

subs2srs launches `subsretimer` and reads the result back. The rules it
relies on:

- With `--print-output`, **stdout carries only saved paths**, one per line:
  `--auto` flushes its path as the file is written, the editor prints what it
  saved when the window closes. Everything else goes to stderr.
- Exit status `0`: at least one file was saved. `2`: nothing was saved
  (editor closed without saving, or a file had no timed lines). `1`: error,
  message on stderr — including no display to open the editor on.
- `--auto` never opens a window and never overwrites an existing output
  unless `--output` names it explicitly.

## Dependencies

Runtime: [.NET 10+](https://dotnet.microsoft.com/) runtime, plus
[GTK 4](https://gtk.org/) for the editor window.

## Build

```sh
make build     # publish the executable
make test      # unit and command-line tests
make test-ui   # the editor's GTK tests
```

`make test-ui` opens real windows, so it needs GTK 4 and a display; it wraps
the run in `xvfb-run -a`, which covers a headless Linux box as long as Xvfb is
installed.

### Windows

```sh
make publish-windows
```

This publishes a self-contained `win-x64` build into `out/win-x64` and copies
the GTK 4 runtime in beside it, so the result runs on a machine with no GTK
installed. GTK comes from an MSYS2 UCRT64 installation with
`mingw-w64-ucrt-x86_64-gtk4` and `mingw-w64-ucrt-x86_64-ntldd` in it,
`C:/msys64` unless `MSYS2=` says otherwise. The two bundling scripts in
`dist/windows/` are PowerShell; on a machine without PowerShell 7 run
`make publish-windows PWSH=powershell`. For a plain `dotnet run` on Windows,
put `C:\msys64\ucrt64\bin` on `PATH` instead: GTK is then found there.

## Install

```sh
sudo make install
```

Installs to `/usr/lib/subsretimer/`, launcher to `/usr/bin/subsretimer`, the
desktop entry to `/usr/share/applications/` and the icon to
`/usr/share/icons/hicolor/<size>x<size>/apps/` (refreshing the icon cache is
left to the packager). `sudo make uninstall` removes them again.

## Credits

- [Christopher Brochtrup](https://sourceforge.net/projects/subs2srs/) — original author of Subs Re-Timer and subs2srs
- [nihil-admirari](https://github.com/nihil-admirari/subs2srs-net48-builds) — kept the original buildable
- [fkzys](https://github.com/fkzys/subs2srs-mono) — bundled it with the Mono subs2srs on Linux

## License

GPL-3.0-or-later. See [LICENSE](LICENSE).
