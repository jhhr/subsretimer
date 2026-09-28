# Season batch: extract, retime and make cards without the GUI

Status: proposal, 2026-09-28. Not agreed yet; see "Questions for you" at the end.
Most of the work is in subs2srs (`jhhr/subs2srs`). The plan is kept here because it
started here. When subs2srs work begins, its half moves to that repository's `docs/`.

## The workflow

One folder per season:

- `NN.mkv`: each has the English subtitles as a track, timed to that video.
- One Japanese subtitle file per episode, from somewhere else and not timed to
  these videos.

For every episode:

1. Extract the English track.
2. Retime the JP file to it (REFERENCE = EN, TARGET = JP).
3. Run subs2srs with Subs1 = retimed JP, Subs2 = EN and Video = the mkv, with AI
   grouping on.

The result is one Anki import TSV plus its media folder for the whole season,
produced by one script.

## What works today and what blocks

| Step | Today | Verdict |
| --- | --- | --- |
| Extract EN | `mkvextract <mkv> tracks <id>:<out>` by hand works (checked 2026-09-28 on mkvtoolnix 82: it wrote a UTF-8 SRT with a BOM). The subs2srs Extract dialog takes many files, but it extracts *every* subtitle track, names them `<name> - Track NN - English.ass` (so "Full" and "Signs & Songs" can't be told apart), writes into a folder you pick, and ignores extraction errors. | Works by hand. Needs a track picker to be safe. |
| Retime | `subsretimer --auto --output OUT -- EN JP` works per pair. An explicit `--output` overwrites, so re-runs work. Checked on an EN track extracted from an mkv with a 90 s cut: found −90.000 s, 2/2 matched. | Works. **Never tried on a real episode** (see phase 0). No machine-readable quality signal for a batch. |
| Cards | subs2srs has **no command line**: `Program.Main` ignores `args` and always starts GTK. On Windows the exe is a `WinExe`, with no console. | **Blocker.** |

The one hard blocker is headless card generation in subs2srs. The rest is about
safety (wrong track, episodes paired wrong, bad alignment, silent AI fallback)
and convenience.

## Proposed decisions

1. **Name every derived file after the video.** Extracted and retimed files are
   `<video stem>.en.<ext>` and `<video stem>.ja.<ext>`, in a work folder
   `<season>/s2s/`. Why:
   - subs2srs pairs `Subs1[i]`, `Subs2[i]` and `Video[i]` by index after a
     culture-sensitive sort, and never checks that the counts match. With
     identical stems the three lists sort the same way by construction.
   - The subfolder keeps a `*.ass` pattern from also matching the JP originals.
   - The only real pairing decision left is JP file ↔ video. It is made once,
     printed, and can be overridden.
2. **A separate console program, `subs2srs-cli`** (new project `subs2srs.Cli` in
   the subs2srs repository, built like `subs2srs.Eval`), not flags on the GUI exe.
   The GUI exe cannot write to a console on Windows and always starts GTK.
   `subs2srs.Eval` and the e2e tests already show the pipeline runs without GTK.
3. **Settings come from a project file.** You set the show up once in the GUI
   (card fields, audio, snapshots, snippet mode AI, deck, output dir) and save
   an `.s2s.json`. The CLI loads it and overrides only what changes per season:
   file patterns, output dir, deck name, episode range, grouping mode.
   `ProjectIO` already saves everything, which avoids a hundred flags.
4. **subs2srs does the orchestration; subsretimer still retimes one pair per run.**
   Pairing, track choice and the retime loop go into `subs2srs-cli` subcommands.
   Why:
   - Episodes are paired in one place.
   - subs2srs already has the mkv code and the subsretimer launcher, and it has a
     planned GUI batch-retime mode that can share the code.
   - It runs the same on Windows without bash or jq.

   subsretimer gets only what a batch needs from a single run: a quality gate
   and a report.
5. **Stop before making cards if any episode failed a step, and make re-runs
   cheap.** Each step skips outputs that already exist (unless `--force`), AI
   answers are cached by content, and the media workers already skip files that
   exist. Fixing one episode and running the script again costs little.
   Skipping a failed episode and carrying on is not offered: pairing is by
   index, so a missing episode would shift every later one, and the episode
   numbers in card names and tags would be wrong.

## End state: the script

```sh
DIR="/anime/Show S1"
subs2srs-cli extract-subs --videos "$DIR/*.mkv" --lang eng --out "$DIR/s2s"
subs2srs-cli retime --videos "$DIR/*.mkv" --target "$DIR/*.ja.srt" --work "$DIR/s2s" \
  --target-encoding utf-8 --min-match 0.6
subs2srs-cli go --project ~/anki/show.s2s.json \
  --subs1 "$DIR/s2s/*.ja.srt" --subs2 "$DIR/s2s/*.en.ass" --video "$DIR/*.mkv" \
  --deck Show_S1 --grouping ai --require-ai --max-cost 5
```

Each command exits non-zero when something needs a look, so `set -e` (or `&&`)
stops the run before any cards are made. Every command takes `--dry-run`: it
prints the track choices, the pairing table and the AI cost estimate, and
does nothing else. Flag names here are a sketch.

## Changes in subsretimer (this repository)

- **S1. `--min-match FRACTION`**, for `--auto` only. After alignment, the matched
  fraction is the share of target lines that overlap a reference line (the sum
  of `AlignmentSegment.MatchedLines` over the target line count).
  - Below the threshold, nothing is saved: stderr says why and the exit code is
    `2` ("nothing saved"). That code already exists in the contract, and the
    subs2srs launcher already maps it to `NothingSaved`.
  - Off by default, so nothing changes for current callers.
  - The threshold to use comes from phase 0.
- **S2. `--report PATH`**: a JSON file with the reference and target paths, line
  counts, the segments (from, to, offset in ms, matched), the matched fraction,
  the average mismatch before and after, and the saved path or the reason
  nothing was saved. It is written even when nothing is saved.
  - It goes to a file because stdout is reserved for saved paths.
  - The subs2srs batch reads it for its season summary instead of parsing
    stderr, which the contract keeps for humans.
- **S3. Only if phase 0 shows it helps: `--ref-skip-styled`.** When aligning,
  ignore reference lines whose text starts with `{`. That is the same rule as
  subs2srs's *Remove styled lines* (`SubsParserASS.cs:78`, on by default). Signs
  and karaoke in fansub EN tracks are noise for timing. The output file is not
  affected. Make it the default only if real episodes align better with it.
- **S4.** README contract table, usage text and CHANGELOG for S1 to S3; tests in
  `CliTests` and `AutoAlignTests`.

Not needed:

- A directory or batch mode: decision 4.
- An overwrite flag: an explicit `--output` already overwrites.
- Reading the reference straight from an mkv: the EN file is needed as Subs2
  anyway.

## Changes in subs2srs

### A. `subs2srs-cli go`: headless card generation (the blocker)

- **A1. Project and bootstrap.** New console project `subs2srs.Cli`, `Exe` on
  every platform, assembly name `subs2srs-cli`. It references `subs2srs` like
  `subs2srs.Eval` does, and needs `InternalsVisibleTo` because `SubsProcessor`,
  `WorkerSubs` and `UtilsSubs` are internal. Bootstrap copies `subs2srs.Eval/Program.cs`:
  - Preferences: the GUI's `preferences.json` by default, so the API keys, tool
    directory and **AI cache** are shared and a later Preview reuses the
    answers for free. `--prefs FILE` and `--no-prefs` as in Eval. It never
    writes preferences.
  - `UtilsCommon.RegisterEncodings()`.
  - `UtilsMsg` hooks print to stderr. A confirm returns false unless `--yes`.
  - `--verbose` turns on `Logger.Instance.Echo`.
  - Ctrl+C cancels through a token.
  - Exit codes: 0 done, 1 error or refused, 130 cancelled.
- **A2. Move file resolution out of `MainWindow.SaveSettings`**
  (`MainWindow.cs:1129-1277`) into a GTK-free function used by both the GUI and
  the CLI. It covers:
  - Pattern expansion (`UtilsSubs.getSubsFiles`, `UtilsCommon.getNonHiddenFiles`).
  - Truncation to the episode range.
  - The audio stream (from the project's `VideoClips.AudioStream` in the CLI).
  - `ConstantSettings.UpdateAudioFilenameFormats()`.

  `ProjectIO.Load` leaves every `Files` array empty (`[JsonIgnore]`), so the CLI
  cannot run without this step. The GUI keeps its preference side effects
  (default output dir) in `MainWindow`.
- **A3. Checks before starting.** Today `GoAsync` only checks that three text
  boxes are not empty. The same function serves the CLI and the GUI's `GoAsync`,
  which also fixes the GUI crashing mid-run:
  - Subs1, Subs2 and Video counts are equal after truncation. Otherwise print the
    lists side by side and refuse. Today fewer Subs2 or video files is an
    `IndexOutOfRange` mid-run (`WorkerSubs.cs:128`, `WorkerAudio.cs:109`, …),
    and extra files are silently ignored.
  - The stems agree at each index. This is a warning, and the pairing table is
    shown with `--dry-run`.
  - Output dir exists or can be created and written; the deck name is not empty.
  - ffmpeg is found. If animated snapshots are on, their encoder is found:
    `WorkerAnimatedSnapshot.cs:42` throws mid-run otherwise.
  - Audio streams are consistent across videos. The GUI asks at
    `MainWindow.cs:1374-1386`; the CLI refuses unless `--yes`.
- **A4. `SubsProcessor.StartAsync` returns a result.** Today it returns a plain
  `Task`, swallows every exception and reports only through `UtilsMsg`. A
  failing worker surfaces as `OperationCanceledException`, so the user reads
  "Action cancelled.".
  - Return a status (`Completed`, `Cancelled` or `Failed`), a message, and
    per-episode counts and AI outcomes.
  - Separate a worker failure from a real cancel.
  - The GUI keeps its dialogs.
- **A5. AI grouping in batch.**
  - `--grouping ai|rules|off` overrides `Snippets.Mode`. `ai` also turns on *AI
    Grouping On Go* for this process only; the preference default is off
    (`Settings.cs:146`).
  - **Estimate before spending.** Run the first two pipeline steps per episode
    (no ffmpeg; the Preview does the same), skip episodes whose
    `AiGroupingCache.KeyFor` hits, and sum `AiGrouper.Estimate`. `--max-cost USD`
    refuses before the first request, as `subs2srs.Eval` does.
  - **Make fallbacks visible.** `runAiGrouping` records a per-episode outcome:
    cached, grouped, *k of n chunks fell back to rules*, or *failed, used rules*
    with the reason. Today this goes only to `Logger.info`. The outcome is
    printed in the final summary.
  - `--require-ai`: stop after the AI step, before any media, if any episode is
    not fully AI-grouped.
    - Answers are cached only when no chunk failed, so a re-run asks again only
      for the failed episodes.
    - The `claude` CLI usage limit stays tripped for the rest of the process
      (`ClaudeCliProvider.UsageLimit`), so without this flag every later
      episode would silently use the rules.
- **A6. Progress** on stderr, using `subs2srs.Eval`'s `ConsoleProgress`: `\r` in a
  terminal, plain lines otherwise.
- **A7. Packaging**, per the rules in subs2srs's `AGENTS.md`:
  - `packages.lock.json` for the new project.
  - A CI restore step per project.
  - The `Makefile`: build, and install `/usr/bin/subs2srs-cli`.
  - The Windows bundle and release zip ship `subs2srs-cli.exe` next to
    `subs2srs.exe`. Check that `bundle-gtk.ps1` and `smoke.ps1` cope with two
    apphosts in one folder.
  - The AUR `PKGBUILD` (outside the repository).
- **A8. Tests**, on the harness of `SubsProcessorE2ETests` and `AiGroupingE2ETests`:
  - A run through the CLI entry point with a fake provider, checking the exit
    codes.
  - Refusal on a count mismatch.
  - `--require-ai` stopping before media.
  - `--max-cost` refusing.
  - A2 giving the same `Files` arrays as the GUI path.

### B. `subs2srs-cli extract-subs`

- **B1. List tracks with `mkvmerge -J`**, not by parsing `mkvinfo` text
  (`UtilsMkv.cs:54-151`, which matches English strings and reads neither the
  track name nor the flags). The JSON has what the picker needs; checked on
  mkvmerge 82: `properties.track_name`, `language`, `forced_track`,
  `default_track`, `codec_id` and `num_index_entries` (the event count). The
  GUI dialogs get the same list and show names and flags. mkvmerge ships with
  mkvextract, so there is no new dependency.
- **B2. `SubtitleTrackPicker`**, a pure function tested on recorded `mkvmerge -J`
  output. Among tracks in the wanted language (`eng`, or IETF `en`) with a text
  codec (`S_TEXT/ASS`, `SSA`, `UTF8`) that are not forced and whose name does not
  match `sign|song|karaoke|forced|commentary|lyrics`, it picks the one with the
  most events.
  - Overrides: `--track ID` and `--track-name REGEX`.
  - The *default* flag is useless: mkvmerge sets it on every track unless told
    otherwise, as seen in the 2026-09-28 check.
- **B3. The command.** For each video:
  - Pick a track and extract it to `<work>/<stem>.en.<ext>`. Skip the episode if
    that file exists, unless `--force`.
  - **Report extraction errors.** `DialogMkvExtract.cs:345-346` drops them today.
  - Print a table: episode, track id, name, events.
  - Warn when the pick differs between episodes.
  - Fail the episode if it has only image subtitles (PGS, VobSub): subsretimer
    reads text only, and OCR is out of scope.

### C. `subs2srs-cli retime`

- **C1. Pair JP files to videos**, in a pure function tested on real-world file
  name sets. It tries, in order:
  1. The JP stem starts with the video stem.
  2. The episode number parsed from both names (`S01E03`, `E03`, ` - 03`, `第3話`,
     `#03`), ignoring `1080p`, `x265`, `10bit` and `[CRC32]`. The number must be
     unique on both sides.
  3. Sorted order, only with `--pair-by-order` and equal counts.

  A missing or ambiguous episode refuses the whole run and prints the table.
  `--dry-run` writes the table as `pairs.tsv` (video, TAB, JP file); you can
  fix it by hand and pass it back with `--pairs FILE`.
- **C2. For each pair**, call `SubsRetimerLauncher` with:
  - `--output <work>/<stem>.ja.<ext>`, `--min-match F` (S1) and
    `--report <work>/<stem>.retime.json` (S2);
  - the JP encoding from `--target-encoding` (e.g. `shift_jis`). The retimed
    file keeps that encoding, so the project's Subs1 encoding must match it.

  An output newer than both inputs is skipped unless `--force`. First fix the
  launcher's pipes: it builds its own `ProcessStartInfo` instead of
  `UtilsCommon.makeToolStartInfo`, so a Japanese path comes back garbled on
  Windows (listed in subs2srs `docs/open-items.md`).
- **C3. Season summary**: episode, segments, offsets, matched %, and status. Exit
  1 if any episode was not saved.
- **C4.** The same pairing function and loop are the GUI Retimer dialog's planned
  wildcard batch mode (subs2srs `docs/open-items.md`, deferred item 2).

### D. Later, optional

- `subs2srs-cli season`: B, C and `go` in one command, with a single `--dry-run`
  showing tracks, pairs and the AI estimate.
- Natural sort in `getNonHiddenFiles`, so `ep2` sorts before `ep10`. This only
  matters for files not named after the video. It changes the order for
  existing projects with unpadded names, so it needs a CHANGELOG line.

## Phases

0. **One real episode, by hand, with today's tools** (you, about half an hour).
   The auto-align has only seen synthetic data; everything after this assumes it
   works on real pairs.
   - `mkvmerge -i ep01.mkv`: which EN tracks are there, and are they text (ASS or
     SRT) or images (PGS)?
   - `mkvextract ep01.mkv tracks <id>:ep01.en.ass`, then
     `subsretimer --auto --output ep01.ja.ass -- ep01.en.ass <JP file>`. Read
     stderr: expect one segment per cut (usually 1 to 4), most lines matched,
     and a small mismatch afterwards.
   - `mpv ep01.mkv --sub-file=ep01.ja.ass`: check the start, after the OP,
     after the eyecatch and the end.
   - Try again with the sign and karaoke lines removed from the reference:
     `grep -vE '^Dialogue:([^,]*,){9}\{' ep01.en.ass > ep01.en.nosigns.ass`.
     Fewer segments or more matches decides S3.
   - Note the matched fraction of a good episode; it sets the `--min-match`
     default.
1. **subsretimer S1, S2, S4.** Small, this repository.
2. **subs2srs A.** The biggest phase; about four agent phases: A1+A2, A3+A4, A5,
   A6-A8. From here the interim script below runs a season on Linux.
3. **subs2srs B and C.** The end-state script works, including on Windows
   without bash.
4. **Optional:** S3 if phase 0 showed sign noise, D, B1 and C4 in the GUI.

Once the plan is agreed, work orders follow, as for the editor
(`docs/ui-work-orders.md`).

### Interim script (after phase 2, before phase 3)

The EN track id and extension are set once per season. Releases keep the same
track layout for a whole season; check one episode with `mkvmerge -i`. JP files
are paired by sorted order, and the pairs are printed.

```bash
#!/usr/bin/env bash
# usage: TRACK=3 EXT=ass JP_GLOB='*.ja.srt' season.sh "/anime/Show S1" show.s2s.json
set -euo pipefail
shopt -s nullglob
export LC_ALL=C                      # one sort order for both lists
DIR=$1 PROJECT=$2
TRACK=${TRACK:?EN track id, see mkvmerge -i}
EXT=${EXT:-ass}                      # the EN track's format: ass or srt
JP_GLOB=${JP_GLOB:-*.ja.ass}
WORK="$DIR/s2s"; mkdir -p "$WORK"

videos=("$DIR"/*.mkv)
jps=("$DIR"/$JP_GLOB)
if (( ${#videos[@]} != ${#jps[@]} )); then
  echo "${#videos[@]} videos but ${#jps[@]} JP files" >&2; exit 1
fi

for i in "${!videos[@]}"; do
  v=${videos[i]} jp=${jps[i]}
  stem=$(basename "${v%.mkv}")
  echo "== $stem <- $(basename "$jp")" >&2
  en="$WORK/$stem.en.$EXT"
  [[ -e $en ]] || mkvextract "$v" tracks "$TRACK:$en" > /dev/null
  subsretimer --auto ${MIN_MATCH:+--min-match "$MIN_MATCH"} \
    --output "$WORK/$stem.ja.${jp##*.}" -- "$en" "$jp"
done

subs2srs-cli go --project "$PROJECT" \
  --subs1 "$WORK/*.ja.${jps[0]##*.}" --subs2 "$WORK/*.en.$EXT" --video "$DIR/*.mkv" \
  --grouping ai --require-ai
```

## Facts checked (2026-09-28)

So that phases do not re-derive them. subs2srs paths are relative to that
repository.

- `subs2srs/Program.cs:33`: `Main(string[] args)` never reads `args`. It wires
  `UtilsMsg` to GTK dialogs, then runs `Gtk.Application`. `subs2srs.csproj:4`:
  `WinExe` on `win-x64`.
- `SubsProcessor.StartAsync` (`SubsProcessor.cs:38-80`) catches everything and
  reports only through `UtilsMsg`. Worker failures become "Action cancelled.".
- `UtilsCommon.getNonHiddenFiles` (`UtilsCommon.cs:122-135`) uses
  `Directory.GetFiles(dir, pattern)`, is not recursive, and sorts with
  `List<string>.Sort()` (culture-sensitive, not natural). A bare `*.ass` with
  no directory part returns nothing.
- Episodes are driven by `Subs[0].Files.Length`. `Subs[1].Files[epIdx]`
  (`WorkerSubs.cs:128`) and `Files[ep-1]` in the media workers have no count
  check.
- `ProjectIO.Load` restores all of `Settings` (patterns, encodings, snippet mode,
  AI model, output, deck, episode start and end) but not the `Files` arrays.
  Preferences (`AiGroupingOnGo`, keys, cache dir) are not in the project.
- The AI cache key (`AiGroupingCache.KeyFor`) hashes the prompt version, model,
  limits, chunk size, extra instructions and the kept Subs1 lines. It does not
  include a path, so the GUI and the CLI share answers.
- `subs2srs.Eval` is a console exe referencing `subs2srs`, with `--prefs`,
  `--no-prefs`, `--verbose`, `--max-cost` and a Ctrl+C token. It is the
  template for A1.
- `SubsRetimerLauncher.cs` has no GTK code. Wildcards are refused only in
  `DialogSubsRetimer.IsSingleSubFile`.
- mkvmerge 82 in this container: `mkvmerge -J` exposes track name, language,
  forced, default and event count. The default flag was set on every track.
  `mkvextract <mkv> tracks <id>:<out>` writes SRT as UTF-8 with a BOM, which
  subsretimer and subs2srs both honour.
- subsretimer `--auto` with an explicit `--output` overwrites
  (`Cli.RunAuto`), so re-runs work without a new flag.

## Questions for you

1. **JP file names.** Do they carry the video's name, an episode number, or
   neither? This decides C1's default and whether sorted order is ever safe.
2. **An episode that fails alignment.** The proposal stops the season before
   `go` until it is fixed (decision 5). Is that right, or do you want cards for
   the other episodes anyway? That needs `go` to take an explicit per-episode
   file list and keep the real episode numbers.
3. **Which AI provider for a whole season.** With the `claude` CLI, the
   subscription usage limit can trip mid-season; with `--require-ai` the run
   stops and a later re-run continues from the cache. With an API key, cost is
   the limit, and `--max-cost` guards it.
4. **Windows or Linux for batch runs.** On Linux the interim script is usable as
   soon as phase 2 is done. On Windows, phase 3 (or a PowerShell port of the
   script) is needed first.
5. **Do the EN tracks have sign and song lines?** Phase 0 answers this. It
   decides S3, and whether card backs need more filtering than *Remove styled
   lines* already does.
