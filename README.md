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
- Interactive editor window: not yet ported. Running without `--auto`
  exits with an error for now.

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

## Contract for other programs

subs2srs launches `subsretimer` and reads the result back. The rules it
relies on:

- With `--print-output`, **stdout carries only saved paths**, one per line,
  flushed as each file is written. Everything else goes to stderr.
- Exit status `0`: at least one file was saved. `2`: nothing was saved
  (editor closed without saving, or a file had no timed lines). `1`: error,
  message on stderr.
- `--auto` never opens a window and never overwrites an existing output
  unless `--output` names it explicitly.

## Dependencies

Runtime: [.NET 10+](https://dotnet.microsoft.com/) runtime.
GTK 4 will be required once the editor window is ported.

## Build

```sh
make build
make test
```

## Install

```sh
sudo make install
```

Installs to `/usr/lib/subsretimer/`, launcher to `/usr/bin/subsretimer`.

## Credits

- [Christopher Brochtrup](https://sourceforge.net/projects/subs2srs/) — original author of Subs Re-Timer and subs2srs
- [nihil-admirari](https://github.com/nihil-admirari/subs2srs-net48-builds) — kept the original buildable
- [fkzys](https://github.com/fkzys/subs2srs-mono) — bundled it with the Mono subs2srs on Linux

## License

GPL-3.0-or-later. See [LICENSE](LICENSE).
