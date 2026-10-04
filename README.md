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
Usage: subsretimer [options] [REFERENCE] [TARGET]

Re-time TARGET so that its lines match the timings of REFERENCE.

  REFERENCE               subtitle file already timed to the video (left pane)
  TARGET                  subtitle file to be retimed (right pane)

Options:
  --auto                  run auto-align and save without opening the editor
  -o, --output PATH       output path (default: <TARGET>_retimed.<ext>)
  --min-match FRACTION    with --auto: save only if TARGET covers at least
                          this share of REFERENCE's lines (0 to 1; default
                          0, off), else save nothing and exit 2
  --report PATH           with --auto: write a JSON report of the run to
                          PATH when it exits 0 or 2; a file already there
                          is deleted first, so an error leaves none
  --ref-encoding NAME     encoding of REFERENCE (default: utf-8)
  --target-encoding NAME  encoding of TARGET (default: utf-8)
  --print-output          print the path of each saved file to stdout
  --check-editor          check that the editor can start here (GTK 4 and a
                          display), then exit: 0 if it can, 1 if not
  -h, --help              show this help
  --version               show the version

In the editor, Save writes to --output when it is given, else where Save As
last wrote, else to <TARGET>_retimed.<ext>, asking before it replaces a file
there that it did not write. Files opened from the editor are read in the
encodings given here. A TARGET that is not valid text in its encoding is
refused: saving it would change the characters that could not be read.

Exit status: 0 a file was saved, 2 nothing was saved, 1 error.
Supported formats: .ass, .ssa, .srt
```

Supported formats: `.ass`, `.ssa`, `.srt`. Output keeps the input's encoding,
byte-order mark, line endings, header, styles, numbering and text; only the
timestamps change. That is why a target whose bytes are not valid in the
encoding it is read with (a Shift-JIS file read as the default UTF-8, say) is
an error: its text would be saved with U+FFFD in place of what could not be
read. Name the encoding with `--target-encoding`; `latin1` keeps any byte as it
is. The output is always written in the target's own format, so `--output`
refuses the other format's extension (`.srt` for an ASS target, and back).

Without `--auto` the editor window opens. Both file arguments are optional
there: a pane left empty is filled from the window instead. Exit status is `0`
when something was saved, `2` when the window was closed without saving, and
`1` on an error — including `cannot open a display; use --auto`, the answer on
a machine with no display server, and `GTK 4 could not be loaded`, the answer
on one without GTK 4. `--check-editor` asks the same question without opening
the window.

Batch example, one season with English subs timed to the video and Japanese
subs that are not:

```sh
for jp in JP/*.ass; do
  en="EN/$(basename "${jp%.ass}").srt"
  subsretimer --auto "$en" "$jp"
done
```

### Reading the coverage line

`--auto` prints to stderr the segments it found (each run of target lines
that moved together, and by how much), the average mismatch before and
after, and a line like

```
reference covered: 97% (291 of 300 lines)
```

That is the share of REFERENCE lines of which the retimed TARGET lines,
together, cover at least half the duration: a line that TARGET splits in two
still counts, and a REFERENCE line with no duration is left out. It is
measured on the reference so that lines only TARGET has, such as the sound
cues and speaker lines of closed captions, cannot lower it. A wrong pair
still covers part of the reference by chance, so a bad alignment shows as a
lower number, not as zero. The number flags a pair that aligned badly as a
whole; a few misplaced lines (a short cold open before a cut, say) barely
move it.

`--min-match` turns it into a gate: below the threshold nothing is saved and
the exit status is `2`. There is no recommended threshold yet. To choose one
for your files, run a pair you know is right and a pair you know is wrong
(episode 2's TARGET against episode 1's REFERENCE), and put the threshold in
the gap between their two numbers. The percentage shown is rounded down;
`--min-match` compares the exact share. Its FRACTION is written with a dot
(`0.85`) in every locale.

### How auto-align works

Both files describe the same dialogue on the same footage, so their timing
structure is a shared fingerprint. `--auto` histograms, in 100 ms bins, the
start-time differences of every pair of a reference and a target line that
start within ten minutes of each other. It smooths the histogram over the
neighbouring bins, because two subtitlers' timings differ by a few hundred
ms per line and would otherwise split one offset's peak, and takes up to 12
of its highest peaks as candidate offsets. It scores every target line under
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
file, and asks first when a file of that name is already there and this window
did not write it (a result from an earlier session, say). Save As
(Ctrl+Shift+S) writes wherever you choose, in the target's own format, and
from then on Save writes there too; `--output` on the command line sets that
file from the start, and Save replaces it without asking, as `--auto` does.
The title carries the target's name and a `*` while there are unsaved
changes, and closing with changes asks Save / Discard / Cancel, where Save
writes where Save would. Opening or dropping a **new target** over unsaved
changes asks the same question about the file being replaced, after checking
that the new file can be opened at all; a new reference never asks, since the
edits are the target's. Files opened in the window are read in the encodings
the command line named (`--ref-encoding`, `--target-encoding`; UTF-8 if not).
Under `--print-output` each saved path is printed as soon as the file is
written.

### Keys and mouse

The window shows this table under Help → Help.

| Key or button | What it does |
| --- | --- |
| Ctrl+O | Open the reference file |
| Ctrl+Shift+O | Open the target file |
| Ctrl+S | Save to `<name>_retimed.<ext>`, or where Save As last wrote |
| Ctrl+Shift+S | Save As... |
| Ctrl+Z / Ctrl+Y | Undo / Redo |
| Ctrl+Q | Quit |
| Left / Right (in a list) | Select the closest line on the other side and go there |
| Ctrl+Up / Ctrl+Down (in a list) | Previous / next orange row and its closest counterpart |
| Enter (in a list) | Time Shift: move the target onto the selected reference line |
| Right-click (in a list) | Select the closest line on the other side |
| Middle-click (in a list) | Time Shift |
| Drop a file (on a pane) | Load it as that side's subtitles |
| Left-click (in the timeline) | Select the next target line and its closest counterpart |
| Right-click (in the timeline) | Select the previous target line and its closest counterpart |
| Middle-click (in the timeline) | Time Shift |
| Left-click + / - (in the timeline) | Zoom in / out by 2 seconds |
| Right-click + / - (in the timeline) | Zoom in / out by 10 seconds |
| Middle-click + / - (in the timeline) | Zoom all the way in or out (4 to 120 seconds) |

## Contract for other programs

subs2srs launches `subsretimer` and reads the result back. The rules it
relies on:

- With `--print-output`, **stdout carries only saved paths**, one per line,
  each printed and flushed as that file is written — in both modes, so the
  editor reports a save while its window is still open. Everything else goes
  to stderr.
- Exit status `0`: at least one file was saved. `2`: nothing was saved
  (editor closed without saving, a file had no timed lines, or the reference
  coverage was below `--min-match`). `1`: error, message on stderr —
  including no display to open the editor on.
- `--auto` never opens a window and never overwrites an existing output
  unless `--output` names it explicitly. The editor writes `--output` when
  it is given, and asks before its Save replaces any other file it did not
  write.
- Below `--min-match`, `--auto` saves nothing, leaves an existing `--output`
  file as it was, says why on stderr
  (`subsretimer: reference covered 67% is below --min-match 0.9; nothing saved`)
  and exits `2`.
- `--report PATH` writes a JSON report of an `--auto` run (below), so a
  caller need not parse stderr, which is meant for people.
- When stdout or stderr is redirected, to a pipe or a file, it is written as
  UTF-8 without a byte-order mark, whatever the console's code page or the
  locale, and its lines end as usual on the platform: `\r\n` on Windows,
  `\n` elsewhere. Written to a console, they follow the console's encoding
  as before.
- PowerShell, Windows PowerShell 5.1 included, decodes the output of a
  program it captures (`$saved = subsretimer ...`) with
  `[Console]::OutputEncoding`: set
  `[Console]::OutputEncoding = [Text.Encoding]::UTF8` first, or a Japanese
  path can come back garbled.
- Both modes refuse a target that is not valid text in its encoding (exit
  `1`), because saving it would change its text.
- `--check-editor` exits `0` when the editor can start and `1`, with the
  reason on stderr, when it cannot, without opening a window.

### The report

`--report PATH` is written when an `--auto` run exits `0`, after the file is
saved and before its path goes to stdout, so a caller that reads the path
finds the report already there; and when it exits `2`, below `--min-match` or
with no timed lines. A file already at PATH is deleted before either input is
read, so a run that then fails (exit `1`) leaves none. An error in the
command line itself (an unknown option, `--auto` without both files) exits
`1` before that and leaves PATH alone: read the report only after exit `0`
or `2`.

The file is UTF-8 without a byte-order mark, with camelCase names, every
path in full and numbers written with a `.`. Its top-level fields:

| Field | Value |
| --- | --- |
| `version` | `1`. Raised when a field changes meaning or goes away, not when one is added. |
| `reference`, `target` | `path`, `lineCount` and `encoding` (the one the file was read in) |
| `output` | the path the run saves to, or would have saved to |
| `segments` | one per run of target lines moved together: `firstLine` and `lastLine` (1-based, as on stderr), `offsetMs`, `matchedLines`, `lineCount` |
| `referenceCoverage` | `covered`, `counted`, `share` (0 to 1): the numbers of the coverage line |
| `targetMatched` | `matched`, `lineCount`, `share`: the target lines that overlap any reference line at all |
| `averageMismatchSeconds` | `before` and `after`, each with `all` and `matched` |
| `minMatch` | the `--min-match` given, or `null` |
| `exitCode` | `0` or `2` |
| `saved` | the saved path, or `null` |
| `reason` | `null` when saved, `"below min-match"`, or `"no timed lines"` |

With no timed lines nothing was aligned: `segments` is `[]` and
`referenceCoverage`, `targetMatched` and `averageMismatchSeconds` are `null`.

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
`dist/windows/` are PowerShell: they run under PowerShell 7 (`pwsh`) when it
is installed and under the Windows PowerShell 5.1 every Windows has
otherwise; `PWSH=` picks one. For a plain `dotnet run` on Windows,
put `C:\msys64\ucrt64\bin` on `PATH` instead: GTK is then found there.

## Install

```sh
sudo make install
```

Installs to `/usr/lib/subsretimer/`, launcher to `/usr/bin/subsretimer`, the
desktop entry to `/usr/share/applications/io.github.jhhr.subsretimer.desktop`
(named after the application id, which is how Wayland desktops match it to the
window) and the icon to
`/usr/share/icons/hicolor/<size>x<size>/apps/` (refreshing the icon cache is
left to the packager). `sudo make uninstall` removes them again.

On Windows there is nothing to install: a self-contained zip with the GTK
runtime inside is attached to each GitHub release (built by
`.github/workflows/release.yml`) — unzip it anywhere and run `subsretimer.exe`.

## Credits

- [Christopher Brochtrup](https://sourceforge.net/projects/subs2srs/) — original author of Subs Re-Timer and subs2srs
- [nihil-admirari](https://github.com/nihil-admirari/subs2srs-net48-builds) — kept the original buildable
- [fkzys](https://github.com/fkzys/subs2srs-mono) — bundled it with the Mono subs2srs on Linux

## License

GPL-3.0-or-later. See [LICENSE](LICENSE).
