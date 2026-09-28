# Season batch: extract, retime and make cards without the GUI

Status: revised 2026-09-28 with the user's answers (recorded under "Decisions
from your answers"). Remaining points are at the end.

Most of the work is in subs2srs (`jhhr/subs2srs`). The plan is kept here because it
started here. When subs2srs work begins, its half moves to that repository's `docs/`.

## The workflow

One folder per season, on **Windows**:

- `Show - 01.mkv`, `Show - 02.mkv`, …: each has the English subtitles as a track,
  timed to that video. The EN tracks are plain dialogue, without sign or song
  lines.
- `Show - 01.srt` (or `.ass`, or `Show - 01.ja.srt`): one Japanese file per
  episode, **named like its video**, not timed to it, and possibly closed-caption
  subs with sound cues and speaker labels.

For every episode:

1. Extract the English track.
2. Retime the JP file to it (REFERENCE = EN, TARGET = JP).
3. Make cards with subs2srs: Subs1 = retimed JP, Subs2 = EN, Video = the mkv, AI
   grouping through the `claude` CLI.

The result is one Anki import TSV plus its media folder for the season, made by
one command. An episode that fails a step is left out; the other episodes still
get their cards, with their real episode numbers.

## What works today and what blocks

| Step | Today | Verdict |
| --- | --- | --- |
| Extract EN | `mkvextract <mkv> tracks <id>:<out>` works by hand. Checked 2026-09-28 on mkvtoolnix 82: it wrote a UTF-8 SRT with a BOM. The subs2srs Extract dialog extracts *every* subtitle track into a folder you pick, and ignores extraction errors. | Works by hand. |
| Retime | `subsretimer --auto --output OUT -- EN JP` works per pair. An explicit `--output` overwrites, so re-runs work. Checked on an EN track extracted from an mkv with a 90 s cut: it found −90.000 s, with 2 of 2 lines matched. | Works on Linux. **No Windows build exists.** Never tried on a real episode (phase 0). Nothing a script can use to tell a bad alignment from a good one. |
| Cards | subs2srs has **no command line**. `Program.Main` ignores `args` and always starts GTK, and on Windows the exe is a `WinExe` with no console. It also cannot leave an episode out: the episode number is always the position in the sorted file list plus the start number. | **Blocker.** |

## Decisions from your answers (2026-09-28)

| Question | Answer | Consequence |
| --- | --- | --- |
| JP file names | They share the video's name | Pairing is a name match. No episode-number parsing and no pairing by sort order. |
| An episode that fails | Make cards for the other episodes | subs2srs needs an explicit episode-number list, so a gap does not renumber the later episodes (A4). The commands report skipped episodes instead of stopping. |
| AI provider | `claude` CLI only | No cost estimate and no `--max-cost`. The subscription usage limit becomes the main failure to plan for (A6). |
| Platform | Windows | subsretimer needs a Windows build (S3). The script is PowerShell. The end state is one `subs2srs-cli season` command. Japanese paths make the launcher's UTF-8 fix (C2) required. |
| Signs and songs | Not in EN; JP may be closed captions | Nothing needs filtering on the reference side. The quality gate measures how much of the EN file is covered, which CC cues in the JP file cannot lower (S1). |

## Design

1. **The video name is the key.** Each JP file is found by the video's name.
   The extracted and retimed files are `s2s\<video name>.en.<ext>` and
   `s2s\<video name>.ja.<ext>` inside the season folder. The subfolder keeps
   derived files away from the originals.
   - Each video's JP file is found by name. It is the subtitle file (`.ass`,
     `.ssa` or `.srt`) whose name is the video's name, optionally followed by a
     tag: `Show - 01.srt` or `Show - 01.ja.srt`.
   - An older extract named `Show - 01 - Track 03 - English.srt` does not match.
   - Zero or several matches skip that episode, and the summary names the files.
2. **Episode numbers come from the sorted list of videos.** Episode *k* is the
   *k*-th `*.mkv` in the order subs2srs already uses, plus the project's start
   number, whether or not the episodes before it succeeded.
3. **A separate console program, `subs2srs-cli.exe`** (new project
   `subs2srs.Cli`, built like `subs2srs.Eval`), shipped in the Windows zip next
   to `subs2srs.exe`. The GUI exe cannot write to a console.
4. **Settings come from a project file.** You set the show up once in the GUI
   (card fields, audio, snapshots, snippet mode AI, deck, output dir, and a
   Subs1 encoding matching the JP files) and save an `.s2s.json`. The CLI takes
   the files from the season folder instead of the project's patterns.
5. **subs2srs does the orchestration. subsretimer still retimes one pair per
   run** and only gains what a batch needs from one run: a quality gate, a
   report, and a Windows build.
6. **AI grouping runs first, the way the Preview does it, and an episode
   without an AI grouping is skipped rather than grouped by the rules.**
   - The CLI runs the first two pipeline steps and the AI grouping for every
     episode. It drops the episodes that could not be grouped, then hands the
     line lists and groupings to the normal run. That hand-off already exists:
     `StartAsync(reporter, combinedAll, joins)`, used by Preview → Go.
   - The `claude` usage limit stays tripped for the rest of the process
     (`ClaudeCliProvider.UsageLimit`), so once it trips, every remaining episode
     is skipped at once, without further calls.
   - An episode where only some chunks fell back to the rules (a malformed
     answer, repaired) still gets cards, with a warning in the summary.
     Retrying would probably fail the same way.
7. **Re-runs are cheap and fill the gaps.**
   - AI answers are cached by content. Media that already exists is reused.
     Extraction skips EN files that exist. Retiming is milliseconds.
   - Running the same command after the usage limit resets therefore only does
     the missing episodes, then rewrites the season TSV with every episode that
     is done.
   - Exception: an episode with chunk fallbacks is not cached, so a re-run asks
     again and may group it differently.
8. **Exit codes for `go` and `season`:**
   - `0`: every episode done.
   - `3`: done, but some episodes were skipped; the summary says why.
   - `1`: error before any work.
   - `130`: cancelled.

## End state

```powershell
subs2srs-cli season 'D:\Anime\Show S1' --project 'D:\Anki\show.s2s.json' --deck Show_S1
```

`--dry-run` prints the table below without extracting, retiming or asking the
model: the EN track each episode would use, its JP file, and whether its AI
grouping is already cached. The table after a real run looks like this
(illustrative):

```
Episode          EN track            Retime                         AI            Cards
Show - 01        3 "English" 312 ev  2 cuts, 97% of EN covered      grouped       148
Show - 02        3 "English" 305 ev  below --min-match (41%)        -             skipped
Show - 03        3 "English" 298 ev  no JP file named like video    -             skipped
Show - 04        3 "English" 301 ev  3 cuts, 95% of EN covered      usage limit   skipped
season TSV: D:\Anki\Show_S1\Show_S1.tsv (1 of 4 episodes); exit 3
```

## Changes in subsretimer (this repository)

- **S1. `--min-match FRACTION`**, for `--auto` only.
  - After alignment it computes the share of *reference* lines that overlap a
    retimed target line: `MismatchFlags(ReferenceLines, TargetLines)`, counting
    the false entries.
  - The reference is the clean EN track. Measured that way, sound cues and
    speaker-only lines in a CC JP file cannot pull the number down, which they
    would if it counted target lines.
  - Below the threshold nothing is saved, stderr says why, and the exit code is
    `2` ("nothing saved"). That code already exists in the contract, and the
    subs2srs launcher maps it to `NothingSaved`.
  - Off by default, so nothing changes for current callers. The threshold comes
    from phase 0.
- **S2. `--report PATH`**: a JSON file, written even when nothing is saved, with:
  - the input paths and line counts;
  - the segments (from, to, offset in ms, matched);
  - the share of reference lines covered and the share of target lines matched;
  - the average mismatch before and after;
  - the saved path, or the reason nothing was saved.

  subs2srs reads it for the season table instead of parsing stderr, which the
  contract keeps for humans.
- **S3. Windows build.**
  - A release workflow on `v*` tags that publishes `win-x64` self-contained and
    attaches `subsretimer-<version>-win-x64.zip`.
  - A `windows-latest` job in `ci.yml` beside the Ubuntu one.
    `CliTests.RealProcess_HonoursStdoutContract` will fail there as written: it
    expects the path followed by `"\n"`, and `WriteLine` ends lines with
    `"\r\n"` on Windows. The subs2srs launcher trims every line, so the contract
    itself holds; the test should compare against `Environment.NewLine`.
  - **Japanese paths on stdout.** A saved path printed with `--print-output`
    must reach subs2srs intact.
    - .NET picks stdout's encoding on Windows from whether the child has a
      console: the console's code page if it has one, UTF-8 if it has none.
    - Write UTF-8 whenever stdout is redirected, so the path does not depend on
      how the tool was started. C2 makes the reading side UTF-8.
    - Do not set `Console.OutputEncoding` for this: that changes the code page
      of the console the tool shares with its parent.
    - Pin it with a test that saves to a path with Japanese characters.
  - `--auto` needs no GTK, so the zip is small. The editor, when it lands, brings
    the GTK bundle with it (editor plan, phase 9).
  - Until the zip exists: `dotnet publish SubsRetimer\SubsRetimer.csproj -c Release -o C:\Tools\subsretimer`
    with the .NET 10 SDK you already use to build subs2srs, and put that folder
    on `PATH` or in subs2srs's *Tools Directory*.
- **S4.** README contract table, usage text and CHANGELOG; tests in `CliTests`
  (gate, report contents, exit 2 with nothing written) and `AutoAlignTests`
  (the coverage number on a fixture with CC-style cue lines in the target).

Not needed: a directory mode (decision 5); an overwrite flag (an explicit
`--output` already overwrites); reference-side sign filtering (the EN tracks are
clean). CC cue lines in the target already move with their neighbours; that is
documented `--auto` behaviour.

## Changes in subs2srs

### A. `subs2srs-cli go`: headless card generation

- **A1. Project and bootstrap.** New console project `subs2srs.Cli`: `Exe` on
  every platform, assembly name `subs2srs-cli`, referencing `subs2srs` like
  `subs2srs.Eval` does. It needs `InternalsVisibleTo`, because `SubsProcessor`,
  `WorkerSubs` and `UtilsSubs` are internal. Bootstrap copies
  `subs2srs.Eval/Program.cs`:
  - It reads the GUI's `preferences.json`, so the tool directory, the `claude`
    model settings and the **AI cache** are shared, and a later Preview of an
    episode reuses its answer for free. It never writes preferences.
  - It calls `UtilsCommon.RegisterEncodings()` and sets
    `Console.OutputEncoding = UTF8`, so Japanese file names print correctly on
    Windows.
  - `UtilsMsg` hooks go to stderr. A confirm answers no unless `--yes`.
  - `--verbose` turns on `Logger.Instance.Echo`. Ctrl+C cancels through a token.
- **A2. Episode list.** Two sources, one resolved list of
  `(episode number, Subs1, Subs2, video)` rows:
  - `--season DIR`: the videos in the folder, sorted as `getNonHiddenFiles`
    sorts them and numbered from the project's start number (decision 2).
    Subs1 and Subs2 are `s2s\<video name>.ja.*` and `s2s\<video name>.en.*`,
    exactly one each. An episode missing either one is skipped with the reason.
  - No `--season`: the project's own patterns, paired by index as the GUI does.
    Counts must be equal, or the command refuses and prints the lists side by
    side. Today a missing Subs2 or video file ends in an `IndexOutOfRange`
    part-way through (`WorkerSubs.cs:128`, `WorkerAudio.cs:109`, …).

  The pattern expansion, episode-range truncation, audio-stream choice and
  `UpdateAudioFilenameFormats()` move out of `MainWindow.SaveSettings`
  (`MainWindow.cs:1129-1277`) into a GTK-free function the GUI also calls.
  `ProjectIO.Load` leaves every `Files` array empty, so the CLI cannot run
  without that step.
- **A3. Checks before starting**, shared with the GUI's `GoAsync`, which today
  only checks that three text boxes are not empty:
  - The output dir can be created and written, and the deck name is not empty.
  - ffmpeg is found. If animated snapshots are on, their encoder is found:
    `WorkerAnimatedSnapshot.cs:42` throws mid-run otherwise.
  - The audio streams are consistent across videos. The GUI asks at
    `MainWindow.cs:1374-1386`; the CLI refuses unless `--yes`.
  - `claude` is found when grouping is AI.
- **A4. Explicit episode numbers.** Add `Settings.EpisodeNumbers`, not saved in
  the project (like `Files`), and one helper, `Settings.EpisodeNumber(index)`,
  that falls back to `index + EpisodeStartNumber`.
  - It replaces the ~25 places that compute the number themselves: `WorkerSrs`
    (tags, sequence markers, every media file name), `WorkerAudio`,
    `WorkerSnapshot` and `WorkerAnimatedSnapshot`. Some of them use a 1-based
    `epNum + start - 1`.
  - Leaving an episode out then keeps the numbers of the others.
  - A mechanical change, with a test that runs episodes {1, 3} and checks the
    names and tags.
- **A5. `SubsProcessor.StartAsync` returns a result**: status (`Completed`,
  `Cancelled` or `Failed`), message, and card count per episode.
  - Today it returns a plain `Task`, swallows every exception and reports only
    through `UtilsMsg`.
  - A failing worker surfaces as `OperationCanceledException`, so the user reads
    "Action cancelled."; the result must tell a worker failure from a real
    cancel.
  - The GUI keeps its dialogs.
- **A6. AI first, then the run** (decision 6).
  - The CLI runs "Combine subs" and "Inactivate lines" over all episodes, then
    for each episode calls `AiGrouper.Group` and records the outcome: cached,
    grouped, *k of n chunks by rules*, or failed with the reason (usage limit,
    CLI error).
  - Failed episodes are removed from the line lists, the `Files` arrays and
    `EpisodeNumbers`. The rest go to `StartAsync(reporter, combinedAll, joins)`,
    which skips its own first steps and AI step.
  - *Remove duplicate lines* works across the whole season (one table spans
    every episode, `WorkerSubs.cs:704`). Running all episodes in one pipeline
    keeps that behaviour, which one run per episode would not.
  - `--grouping rules|off` overrides the project's mode for a run without the
    model.
- **A7. Output.** Progress on stderr, reusing `ConsoleProgress` from
  `subs2srs.Eval` (`\r` in a terminal, plain lines otherwise). At the end, the
  season table and exit code of decision 8.
- **A8. Packaging**, per subs2srs's `AGENTS.md`:
  - `packages.lock.json` for the new project.
  - A CI restore step per project.
  - `Makefile` install of `/usr/bin/subs2srs-cli`.
  - `subs2srs-cli.exe` in the Windows bundle and release zip. Check that
    `bundle-gtk.ps1` and `smoke.ps1` cope with two apphosts in one folder.
- **A9. Tests**, on the harness of `SubsProcessorE2ETests` and `AiGroupingE2ETests`,
  with the fake provider:
  - A `--season` folder with one episode missing its JP file gives exit 3, and
    the other episodes keep their numbers.
  - A usage-limit failure on episode 2 skips 2 and every later episode, with no
    further provider calls.
  - A pattern-mode count mismatch is refused.
  - A2 gives the same `Files` arrays as the GUI path.

### B. EN track choice and extraction (inside `season`)

- **B1. List tracks with `mkvmerge -J`**, not by parsing `mkvinfo` text
  (`UtilsMkv.cs:54-151`, which matches English strings and reads neither the
  track name nor the flags). Checked on mkvmerge 82, the JSON has
  `properties.track_name`, `language`, `forced_track`, `default_track`,
  `codec_id` and `num_index_entries` (the event count). mkvmerge ships with
  mkvextract, so there is no new dependency.
- **B2. The pick.** Among English tracks (`eng`, or IETF `en`) with a text codec
  (`S_TEXT/ASS`, `SSA`, `UTF8`) that are not forced, take the one with the most
  events.
  - `--track ID` overrides.
  - The *default* flag is useless: mkvmerge sets it on every track unless told
    otherwise, as seen in the 2026-09-28 check.
  - Image-only tracks (PGS, VobSub) skip the episode: subsretimer reads text,
    and OCR is out of scope.
  - The table warns when the pick differs between episodes.
- **B3. Extraction** runs `mkvextract <mkv> tracks <id>:s2s\<name>.en.<ext>` and
  skips an existing file.
  - Exit code 2 or more fails the episode and deletes the partial file. The
    extract dialog drops errors today (`DialogMkvExtract.cs:345-346`).

### C. Retime (inside `season`)

- **C1.** Find the JP file by name (decision 1).
- **C2. The launcher.** First make `SubsRetimerLauncher` use
  `UtilsCommon.makeToolStartInfo`, so the pipes are UTF-8. Today a Japanese
  path comes back garbled on Windows (subs2srs `docs/open-items.md`). Then, per
  episode, call it with:
  - `--output s2s\<name>.ja.<ext>`, `--min-match` (S1) and
    `--report s2s\<name>.retime.json` (S2);
  - `--target-encoding` set to the project's Subs1 encoding. The retimed file
    keeps its encoding, so Subs1 then reads it correctly.

  An old output is deleted first, so a failed retime never leaves a stale file
  for `go` to pick up.

### D. `subs2srs-cli season`

B, C and `go --season` in one process with one table and one `--dry-run`. The
three stages can also run alone (`--only extract|retime|go`) when one needs
redoing.

### E. Later, optional

- The GUI Extract dialog uses B1 and shows track names and flags.
- The GUI Retimer dialog's planned wildcard batch uses C (subs2srs
  `docs/open-items.md`, deferred item 2).
- Natural sort in `getNonHiddenFiles`, so `ep2` sorts before `ep10`. It changes
  the order for existing projects with unpadded names; mention it in the
  CHANGELOG.

## Phases

0. **One real episode by hand, on Windows** (you, under an hour). The auto-align
   has only seen synthetic data. The numbers from this step set the
   `--min-match` default.
   1. Check the tools in PowerShell: `mkvmerge --version`, `ffmpeg -version`,
      `claude --version`, and subsretimer built as in S3.
   2. Run `mkvmerge -i 'Show - 01.mkv'` and note the EN track id and whether it
      is ASS or SRT.
   3. Extract and retime:
      ```powershell
      mkvextract 'Show - 01.mkv' tracks 3:'Show - 01.en.ass'
      subsretimer --auto --output 'Show - 01.ja.srt' -- 'Show - 01.en.ass' 'Show - 01.srt'
      ```
      On stderr, expect one segment per cut (usually 1 to 4), most lines
      matched, and a small mismatch after.
   4. Play the video with the retimed JP file (mpv:
      `--sub-file='Show - 01.ja.srt'`). Check the start, after the OP, after the
      eyecatch, and the end.
   5. **Negative control:** retime the JP file of episode 2 against the EN of
      episode 1. S1's coverage number must come out clearly lower than for the
      right pair; that gap is where the threshold goes.
   6. In the GUI, set Subs1 = retimed JP, Subs2 = EN, and run the Preview with AI
      grouping through `claude`. This checks the CLI provider on Windows and
      shows the time per episode.
1. **subsretimer S1 to S4.** Small; this repository.
2. **subs2srs A.** The biggest phase, about four agent phases: A1+A2, A3+A4+A5,
   A6+A7, then A8+A9. After it, the PowerShell script below runs a season.
3. **subs2srs B, C, D.** The script becomes one command, with automatic track
   choice and the season table.
4. **Optional: E.**

Once the plan is agreed, work orders follow, as for the editor
(`docs/ui-work-orders.md`).

### Interim PowerShell script (after phases 1 and 2, before phase 3)

The EN track id and format are set once per season: releases keep one track
layout for a season. Check one episode with `mkvmerge -i`.

Tested 2026-09-28 on PowerShell 7.6 under Linux, with a stub `subs2srs-cli`, on
file names with `[Group]` brackets and spaces:

- Episodes matched `<name>.srt` and `<name>.ja.srt`, and ignored an old
  `<name> - Track 03 - English.srt` extract.
- An episode without a JP file and one with two candidates were skipped.
- A bad track id left no partial file.
- A re-run skipped extraction and retimed again.

Not tested on Windows PowerShell 5.1.

```powershell
# season.ps1: extract the EN track, retime the JP file, then make cards.
# .\season.ps1 -Season 'D:\Anime\Show S1' -Project 'D:\Anki\show.s2s.json' -Track 3
param(
  [Parameter(Mandatory)] [string] $Season,
  [Parameter(Mandatory)] [string] $Project,
  [Parameter(Mandatory)] [int] $Track,     # EN track id, from mkvmerge -i on one episode
  [string] $EnExt = 'ass',                 # the EN track's format: ass or srt
  [string] $JpEncoding = 'utf-8',          # e.g. shift_jis
  [double] $MinMatch = 0                   # 0 = off; needs subsretimer --min-match
)
$ErrorActionPreference = 'Stop'
$work = Join-Path $Season 's2s'
[void][IO.Directory]::CreateDirectory($work)

# -LiteralPath and name comparisons throughout: [Group] in a name is a wildcard to -Path.
$files  = @(Get-ChildItem -LiteralPath $Season -File)
$subs   = @($files | Where-Object { @('.ass', '.ssa', '.srt') -contains $_.Extension.ToLowerInvariant() })
$videos = @($files | Where-Object { $_.Extension -eq '.mkv' } | Sort-Object Name)
$gate   = @()
if ($MinMatch -gt 0) { $gate = @('--min-match', $MinMatch.ToString([cultureinfo]::InvariantCulture)) }

$summary = foreach ($v in $videos) {
  $stem = $v.BaseName
  $status = 'ok'
  $en = Join-Path $work "$stem.en.$EnExt"
  if (-not (Test-Path -LiteralPath $en)) {
    & mkvextract $v.FullName tracks "${Track}:$en" | Out-Null
    if ($LASTEXITCODE -ge 2) {
      $status = "mkvextract exit $LASTEXITCODE"
      Remove-Item -LiteralPath $en -ErrorAction SilentlyContinue
    }
  }
  # The JP file shares the video's name: "<stem>.srt" or "<stem>.<tag>.srt".
  $jp = @($subs | Where-Object {
    $_.BaseName -eq $stem -or $_.BaseName.StartsWith("$stem.", [StringComparison]::OrdinalIgnoreCase) })
  if ($status -eq 'ok' -and $jp.Count -ne 1) { $status = "$($jp.Count) JP files match" }
  if ($status -eq 'ok') {
    $out = Join-Path $work ("$stem.ja" + $jp[0].Extension)
    Remove-Item -LiteralPath $out -ErrorAction SilentlyContinue   # never leave a stale retime
    & subsretimer --auto @gate --target-encoding $JpEncoding --output $out '--' $en $jp[0].FullName
    if ($LASTEXITCODE -ne 0) { $status = "subsretimer exit $LASTEXITCODE" }
  }
  [pscustomobject]@{ Episode = $stem; Status = $status }
}
$summary | Format-Table -AutoSize | Out-String -Width 300 | Write-Host

# Episodes without both files in s2s\ are skipped by go; the others keep their numbers.
& subs2srs-cli go --project $Project --season $Season
exit $LASTEXITCODE
```

## Facts checked (2026-09-28)

So that phases do not re-derive them. subs2srs paths are relative to that
repository.

- `subs2srs/Program.cs:33`: `Main(string[] args)` never reads `args`. It wires
  `UtilsMsg` to GTK dialogs, then runs `Gtk.Application`. `subs2srs.csproj:4`:
  `WinExe` on `win-x64`.
- `SubsProcessor.StartAsync(reporter, combinedAll = null, joins = null)`
  (`SubsProcessor.cs:38`) catches everything and reports only through
  `UtilsMsg`. Worker failures become "Action cancelled.". The `combinedAll` and
  `joins` arguments are the Preview → Go hand-off (`List<bool[]>`, one per
  episode).
- The episode number is `index + EpisodeStartNumber` (or `epNum + start - 1`),
  computed in place about 25 times: `WorkerSrs.cs:370-527`,
  `WorkerAudio.cs:240,368,389`, `WorkerSnapshot.cs:82`, `WorkerAnimatedSnapshot.cs:92`.
- *Remove duplicate lines* keeps one table across all episodes
  (`WorkerSubs.cs:704-708`, `881-899`); it is off by default.
- *Remove lines with no counterpart* for Subs1 (on by default,
  `WorkerSubs.cs:253`) drops JP lines that overlap no EN line. That covers pure
  CC sound-cue lines. Speaker labels inside dialogue lines stay in the text.
- The season TSV is rewritten on every run (`WorkerSrs.cs:126`,
  `new StreamWriter(…, false, …)`). Existing audio and snapshots are reused
  (`WorkerAudio.cs:88-89`, `WorkerSnapshot`).
- `UtilsCommon.getNonHiddenFiles` (`UtilsCommon.cs:122-135`) uses
  `Directory.GetFiles(dir, pattern)`, is not recursive, and sorts with
  `List<string>.Sort()` (culture-sensitive, not natural).
- `ProjectIO.Load` restores all of `Settings` except the `Files` arrays.
  Preferences (`AiGroupingOnGo`, cache dir, `claude` settings) are not in the
  project.
- The AI cache key (`AiGroupingCache.KeyFor`) hashes the prompt version, model,
  limits, chunk size, extra instructions and the kept Subs1 lines. It does not
  include a path, so the GUI and the CLI share answers. Results with a failed
  chunk are not cached.
- `ClaudeCliProvider.UsageLimit` is sticky for the process; `Reset()` is only
  called by tests.
- `subs2srs.Eval` is a console exe referencing `subs2srs`, with `--prefs`,
  `--no-prefs`, `--verbose` and a Ctrl+C token. It is the template for A1.
- `SubsRetimerLauncher.cs` has no GTK code and builds its own
  `ProcessStartInfo` (no UTF-8 pipes).
- mkvmerge 82: `mkvmerge -J` exposes track name, language, forced, default and
  event count. The default flag was set on every track. `mkvextract` writes SRT
  as UTF-8 with a BOM, which subsretimer and subs2srs both honour, and exits
  with 2 for a track id that does not exist.
- subsretimer `--auto` with an explicit `--output` overwrites (`Cli.RunAuto`).
  `CliTests.RealProcess_HonoursStdoutContract` runs `dotnet subsretimer.dll`
  and expects stdout to be exactly the path plus `"\n"`.
- `SubsRetimerLauncher.ParseResult` splits stdout on `'\n'` and trims each line,
  so a CRLF from a Windows subsretimer is harmless.

## Remaining points

- **Video clips.** If the project makes video clips, check the cost of a re-run:
  `WorkerVideo` first transcodes each episode's whole range to a temp file, and
  it is not checked whether it skips an episode whose clips all exist.
- **Speaker labels in CC subs** (`（太郎）…`) stay in the card text. Stripping
  them would be a Subs1 filter in subs2srs, not part of this plan. Say if you
  want it.
- **Windows PowerShell 5.1**: the script avoids 7-only syntax but has only run
  on 7.6. If phase 0 shows 5.1 drops the quoted `'--'`, run it under `pwsh`.
