# Season batch: work orders for phase agents

You are one of a line of agents, each building **one small phase** of the season batch
(`docs/season-batch-plan.md`). A lead reviews your work when you report back. You have no
memory of earlier phases; what you need is here.

**Your context is the budget.** Aim to finish well under 200k tokens. The rules below say
how. They are about not reading huge files whole and not maintaining big documents; they
are **not** a licence to skip what you need to understand. Careful, correct work comes
first.

## 1. What to read (and what not to)

1. This file, all of it. It is short on purpose.
2. `docs/season-batch-plan.md` (the spec, about 720 lines): "The workflow", "Decisions
   from your answers", "Design", the subsretimer item of your phase under "Changes in
   subsretimer (this repository)", and "Facts checked". Skip "Changes in subs2srs" and the
   interim script unless your work order names them.
3. `README.md`, section "Usage" and section "Contract for other programs", if your phase
   touches `Cli.cs`.
4. Code in this repository is small (every file under 550 lines): read the files your
   phase touches whole. `SubsRetimer/Cli.cs` (290 lines) is the command line;
   `SubsRetimer.Core/RetimerEngine.cs` (335) and `SubsRetimer.Core/AutoAlign.cs` (200)
   hold the timing logic. Read an existing test next to where yours will go and copy
   its shape (`SubsRetimer.Tests/CliTests.cs`, `AutoAlignTests.cs`, `Fixtures.cs`).
5. The sibling checkout `/home/user/jhhr/subs2srs` is another repository, read-only for
   you. Only `subs2srs/SubsRetimerLauncher.cs` (how subs2srs runs this tool) concerns
   phase 1.

Do not read `docs/ui-plan.md` or `docs/ui-work-orders.md` (the editor's finished plan)
unless your work order names a section of them.

## 2. Rules

**Scope**

- Build your phase only. If something from a later phase is needed, build the smallest
  part of it and say so.
- The spec is agreed with the user. Where it is silent, choose the simpler option and
  note it. Where it is **wrong or impossible**, do not improvise another design: finish
  what can be finished, leave the tree building and green, and report.
- Do not edit `.github/`, `dist/`, `Makefile`, `LICENSE`, `Directory.Build.props`,
  anything under `/home/user/jhhr/subs2srs`, or package versions. If one needs a change,
  report exactly what; the lead makes those changes.
- The command-line contract is what subs2srs relies on: **stdout carries only saved
  paths** (under `--print-output`), every diagnostic goes to stderr; exit 0 saved, 2
  nothing saved, 1 error. A new option keeps that. Every option is listed in
  `Cli.Usage`.
- Match the surrounding code: two-space indent, `//  Copyright (C) 2026 jhhr and
  contributors` + SPDX header on new files, XML doc comments on public members,
  comments say why. Timing logic lives in `SubsRetimer.Core`; `Cli.cs` only calls it
  and formats. Every number in a user-visible string or a file goes through
  `TimeFormat` or `CultureInfo.InvariantCulture`.

**Build and test**

- Scratchpad (for logs and throwaway programs):
  `/tmp/claude-0/-home-user-subsretimer/d251e84e-97b9-5ed0-94cf-8b728fa17524/scratchpad`,
  below `$S`.
- Build: `cd /home/user/subsretimer && dotnet build SubsRetimer/SubsRetimer.csproj > $S/build.log 2>&1; echo exit=$? >> $S/build.log`,
  then `tail -5` the log or grep it for `error`. The tests project builds with
  `dotnet test`.
- Tests: `dotnet test SubsRetimer.Tests/SubsRetimer.Tests.csproj --filter "FullyQualifiedName~ClassName.Method" > $S/test.log 2>&1`
  for yours; the whole suite (121 tests before phase 1) takes under 30 seconds with its build. Run it
  **once**, at the end, and again only if something failed. Nothing in `dotnet test`
  may need a display, GTK or the network. Leave `SubsRetimer.UiTests` alone: phase 1
  does not touch the editor, and the lead runs those.
- Tests that run the real executable (`CliTests.RunProcess`) start
  `dotnet subsretimer.dll` from the test output folder; give each a timeout as the
  existing helper does.
- Every behaviour gets a test that can fail. Show it for the two or three that matter
  most by breaking the code for a moment; not for every test. Undo the break **by hand**:
  never `git checkout`/`git restore` a file to revert an experiment.
- Do not weaken or delete an existing test to get green. If one is wrong because the
  behaviour was meant to change, change it and say so.
- The tests must also pass on Windows (a `windows-latest` CI job arrives in phase 1.3):
  compare line ends with `Environment.NewLine`, build paths with `Path.Combine`, and
  decode a child's output with an explicit encoding.

**Docs: almost none.** README and CHANGELOG are brought up to date once, by phase 1.4.
You write only:

- one entry in the log (section 5), **25 lines at most**, appended at the end;
- "Done <date> (<commits>)" on your phase's line under "Phases" in
  `docs/season-batch-plan.md`, and a correction of any statement there that your work
  proved wrong.

Edit documents with the Edit/Write tools only: a shell heredoc or one-liner containing
backticks, `$` or non-ASCII text gets mangled and has corrupted documents before. Put
throwaway scripts in files in the scratchpad.

**Commit**

- Work on branch `claude/hopeful-babbage-vrca6w` (it is checked out). Commit when the
  whole suite is green; if it is not, do not commit: report.
- One commit per coherent step, subject `area: imperative summary` under 72 characters
  (`core: measure how much of the reference the target covers`, `cli: ...`,
  `tests: ...`), a body saying why when it is not obvious. End every message with
  exactly these two trailer lines, after a blank line:

      Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
      Claude-Session: https://claude.ai/code/session_01EUD7eAkNWq1B625BRnujNX

- Stage files by name. Never `git add -A`, never push, never amend earlier commits.

**Report** (your final message; all the lead sees; under 60 lines)

- What was built, by file, briefly. Commit hashes.
- The totals of the final full test run, copied, not paraphrased.
- Which tests you saw fail without the change.
- Choices made, deviations, anything fragile or unfinished. Say it plainly: a problem
  reported is cheap, one found later is not.

## 3. State of the code (kept by the lead; as of 2026-10-04, after phase 1.1)

- The editor branch is merged (`main` at `a209301`, merged into this branch). 143 unit
  tests pass after phase 1.1.
- Phase 1.1 added `RetimerEngine.CoverageFlags(lines, others)` (static; the union of
  `others` covers at least half the line), `ReferenceCoverage()` → `Coverage(Covered,
  Counted)` with `Share` (0..1) and `Percent` (floored), `Cli.Options.MinMatch`
  (`double?`) and the gate in `RunAuto` (after `Apply`, before `Save`, exit 2). stderr
  of `--auto`: file line, segments, `average mismatch ...`, `reference covered: 97% (291
  of 300 lines)`, then `saved ...` or the gate line. All of it invariant culture: the
  test host's culture is fi-FI, so a number formatted in the current culture prints
  `0,5` in in-process tests.
- `AutoAlignTests.ClosedCaptions(dialogue, cuts, seed)` builds a CC-style target with
  known cuts, ±250 ms jitter, split lines and sound cues. A scratch harness at
  `$S/sim` (`Program.cs`, references Core) sweeps seeds and prints coverage; it is not
  part of the repository.
- Phase 1.1b: `AutoAlign.CandidateOffsets` smooths the histogram (1-2-3-2-1 over ±2
  bins) before picking peaks, and `MaxCandidates` is 12. 146 unit and 55 UI tests pass.
- Phase 1.2: `--report PATH` (`Options.Report`; records in `SubsRetimer/AutoReport.cs`;
  `RetimerEngine.TargetMatched()` → `Coverage`). `RunAuto` computes the full output path
  first, then `RemoveStaleReport` (refuses a report path equal to an input or the output,
  and a missing folder), then loads. `reason`: null, `"below min-match"`, `"no timed
  lines"`. Tests in `CliTests.Report.cs` (`CliTests` is `partial`). 157 unit tests pass.
- Phase 1.3: `Program.Main` swaps in `Program.Utf8Writer` (UTF-8, no BOM, `AutoFlush`) via
  `Console.SetOut`/`SetError` for each redirected stream. `CliTests.RunProcess` reads
  stdout as raw bytes (a BOM would show) and stderr as UTF-8. The lead added the
  `windows-tests` job to `ci.yml` and a Japanese-names check (section 5) to
  `dist/windows/smoke.ps1`. 159 unit tests pass.
- `SubsRetimer.Core`: `RetimerLine` (Start, End, Text, RawIndex, ...), `SubtitleFile` +
  `RetimerIO` (load, save with the original encoding/BOM/newline, `DefaultOutputPath`,
  `OutputFormatProblem`), `RetimerEngine` (`ReferenceLines`, `TargetLines`, `HasBoth`,
  `ShiftFrom`, undo/redo, `Save`; statics `Overlap`, `BestOverlap` (needs a list sorted
  by start), `ClosestIndex`, `SortedByStart` (internal), `MismatchFlags`,
  `LargeGapFlags`; instance `AverageMismatchSeconds()` → `(All, Matched)`),
  `AutoAlign` (`Compute` → `AlignmentSegment(StartIndex, EndIndexExclusive, Offset,
  MatchedLines)`, `Apply`, `Describe` with 1-based line numbers).
- `SubsRetimer` (executable `subsretimer`): `Program.Main` → `Cli.Run(args, Console.Out,
  Console.Error)`. `Cli.Parse` fills `Options`; `Run` catches IO/argument/format
  exceptions into exit 1. `RunAuto`: `LoadChecked` both files (a target with invalid
  bytes is refused), `CheckOutput`, exit 2 when either side has no timed lines, refuse
  to overwrite the default output, align, print the summary to stderr, `engine.Save`,
  print the path under `--print-output`. Editor mode goes through the `internal static
  Func` seams `ProbeEditor`, `ProbeEditorStart`, `RunWindow`, which `CliTests` swaps
  and restores; nothing under `dotnet test` may start GTK.
- Tests: `Fixtures.Dialogue(count, seed)` gives irregular, speech-like timings;
  `Fixtures.WriteTemp(ext, content, encoding?)` writes a file in a fresh temp folder;
  `CliTests.Run(args)` runs `Cli.Run` in-process; `CliTests.RunProcess(args,
  configure?)` runs the real executable.

## 4. Phases

Done: 1.1, 1.1b, 1.2, 1.3.

### 1.1 — Coverage and `--min-match` (spec S1)

- Core: the reference coverage of S1. A reference line counts as covered when the union
  of the target lines covers at least half its duration; lines with no duration are not
  counted. The target list may be out of start order after `AutoAlign.Apply`. Expose the
  per-line flags (like `MismatchFlags`) and the share; the editor may want the flags
  later, but do not touch the editor now.
- CLI: `--min-match FRACTION`, a number from 0 to 1 read with the invariant culture,
  `0` (the default) meaning off. Refused (exit 1) without `--auto` and outside 0..1.
  After the segments, stderr always says how much of the reference is covered. Below
  the threshold: nothing saved, an existing output left as it is, stderr says why
  (coverage and threshold), exit 2.
- Tests (Core): a right pair with CC-style extra cue lines and split lines stays near
  full coverage; a wrong pair (another seed) after `AutoAlign` comes out clearly lower
  (assert a gap, not exact numbers); out-of-order target; empty inputs; a zero-length
  reference line. Tests (CLI): the gate's exit 2 with nothing written, an existing
  `--output` untouched, the threshold met saves as before, the option refused without
  `--auto` and out of range, the coverage line on stderr.
- Not in this phase: `--report`, README/CHANGELOG.

### 1.1b — Candidate offsets that survive jitter (corrective; spec "Remaining points")

Found in 1.1: in 3 of 20 seeded CC-style pairs, auto-align gave the 40-line leading
block a wrong offset (+28 to +87 s). The start differences of two subtitlers' timings
spread each true offset's histogram peak over more than the ±3-bin suppression in
`AutoAlign.CandidateOffsets`, so one offset takes two of the 8 candidate slots, the
slots run out, and a true offset (−4 s there) is never a candidate. Real JP/EN pairs
differ by a few hundred ms per line, so this is likely in phase 0 too.

- Reproduce first: a seeded test on `ClosedCaptions` where lines end up more than 1 s
  from their true offset today (the cuts are known, so the true offset of every target
  line is too). Keep it as the regression test.
- Fix it in `CandidateOffsets` with the smallest change that removes it, for example
  smoothing the histogram over neighbouring bins before picking peaks, or more
  candidates. Do not change the DP, the switch penalty, `Refine` or `MergeSimilar`.
- Acceptance: every existing test passes unchanged. A sweep in the scratch harness
  (20 seeds or more; jitter 0, 100, 250 and 400 ms; the 1.1 cuts and one other cut
  layout) counts lines misplaced by more than 1 s, before and after: put that table in
  the report. A 1,000-line pair still aligns well under a second.
- Not in this phase: the short leading block folded into the next segment (switch
  penalty) and the clamp at 0:00:00, both in `docs/ui-plan.md`, "Known limitations".

### 1.2 — `--report PATH` (spec S2)

- A JSON file for `--auto` runs, written for exit 0 and exit 2 (including "no timed
  lines"), with the fields S2 lists; a report already at that path is deleted before
  anything else, so exit 1 leaves none. Refused without `--auto`. UTF-8 without BOM,
  camelCase names, a top-level `"version": 1`. Paths are full paths.
- Tests: the fields on a saved run, on a gated run (exit 2, reason, no saved path),
  on "no timed lines"; a stale report removed on exit 1; numbers written with `.`
  whatever the culture.

### 1.3 — UTF-8 output when redirected; stdout tests that hold on Windows (spec S3)

- `Program.Main`: when stdout is redirected, write it as UTF-8 without BOM; the same
  for stderr. Never set `Console.OutputEncoding`. Paths still flushed as they are
  printed.
- Tests: `RealProcess_HonoursStdoutContract` compares with `Environment.NewLine`. A new
  real-process test saves a pair with Japanese folder and file names, started as the
  subs2srs launcher starts it (`UseShellExecute = false`, `CreateNoWindow = true`,
  stdout and stderr read as UTF-8), and checks stdout is exactly the full path plus
  `Environment.NewLine` and stderr names the files intact. On Linux this passes
  without the change; it is the Windows job that makes it able to fail. Say so in
  the report rather than inventing a Linux failure.
- The lead then adds the `windows-latest` job to `ci.yml` and the Japanese pair to
  `dist/windows/smoke.ps1`.

### 1.4 — Documentation (spec S4)

- README: "Usage" (the new options), "Contract for other programs" (the report, the
  gate's exit 2, UTF-8 when redirected), and a short note on reading the coverage
  number; CHANGELOG under Unreleased; the spec's subsretimer section and "Facts
  checked" made to describe what was built. No code: suspected bugs go in the report.

### Later phases (subs2srs)

Phases 2 and 3 run in `jhhr/subs2srs`. Their work orders are written in that
repository's `docs/` when phase 1 is done; the draft cut is in the spec under
"Phases".

## 5. Log (newest last; 25 lines at most per entry)

Template:

    ### Phase <id> — <date> — <commits>
    Built: ...
    Choices / deviations: ...
    The next phase must know: ...
    Left open: ...

### Phase 1.1 — 2026-10-04 — a86e27c, e831e27
Built: `RetimerEngine.CoverageFlags(lines, others)` (static; true = the union of `others`
covers at least half the line; a line with no duration is false) and `ReferenceCoverage()`
→ `Coverage(Covered, Counted)` with `Share` (0..1; 0 when nothing is counted) and `Percent`
(rounded down in integer arithmetic). The record is in `RetimerEngine.cs`. Neither list
need be sorted. `Cli`: `--min-match FRACTION` (`Options.MinMatch`, `double?`, null if not
given); stderr: segments, mismatch, `reference covered: 50% (60 of 120 lines)`, then either
`subsretimer: reference covered 50% is below --min-match 0.9; nothing saved` (exit 2,
before `Save`) or `saved ...`.
Choices / deviations: both refusals (a bad value, no `--auto`) are thrown in `Cli.Parse`,
so the usage text follows as for other bad options. The percentage is floored, so a 100%
means every line and a shown percentage never exceeds the share compared. The mismatch
line is now invariant too: in-process tests run in the machine's culture (fi-FI here);
the shipped exe has InvariantGlobalization.
Numbers: `AutoAlignTests` right pair (300 lines, start and end each ±250 ms, 15% split,
15% cues, cuts of 4, 15 and 8 s) 0.997, wrong pair (seed 2) 0.497. Scratch run over 20
seeds: right 0.91 to 0.997, wrong 0.43 to 0.57.
The next phase must know: the report's coverage fields come from `engine.ReferenceCoverage()`
(after `Apply`). The share of target lines matched (S2) is not built.
Left open: in 3 of those 20 seeds AutoAlign gave the 40-line leading block a wrong offset:
with ±250 ms jitter each true offset fills two of the 8 candidate slots (±3-bin
suppression), and -4 s was not among them. Coverage stayed 0.91 to 0.93, so a 0.9
threshold would not catch it (S1 says as much). Core tuning, not this phase.

### Lead — 2026-10-04 — after phase 1.1
Reviewed the Core diff (union, binary search, half rule) and the gate; 143 passed on my
own run. Added phase 1.1b for the candidate-offset finding above, before 1.2: phase 0 on
real episodes is where it would show, and the fix is local to `CandidateOffsets`. Phase
1.1 used about 200k tokens, mostly on the 20-seed exploration; later phases should keep
experiments to what their work order asks.

### Phase 1.1b — 2026-10-04 — 1aa7813
Built: `AutoAlign.CandidateOffsets` sums each bin with its neighbours (a triangle over ±2
bins, weights 1-2-3-2-1) before picking peaks; the ±3-bin suppression, DP, `Refine` and
`MergeSimilar` are as they were. `AutoAlignOptions.MaxCandidates` default 8 → 12. Test:
`AutoAlignTests.JitteredClosedCaptions_EveryBlockKeepsItsOwnOffset`, three seeded pairs,
the true offset of each line taken from a new `ClosedCaptions(..., out int[] source)`
overload (the old one returns the same lines). (1, 59) and (1, 36) fail without the
smoothing, (41, 141) with 8 candidates.
Choices / deviations: a box kernel makes a sharp peak a plateau (±1 bins shifted offsets
100 ms toward 0 at zero jitter, more boundary misses); a 5-bin box merges two offsets
400 ms apart; picking only local maxima lost that close cut at 0-100 ms jitter. 16
candidates let a chance offset take 12 lines next to a cut (sweep seed 45, 250 ms).
Sweep (`$S/sim`: `dotnet out/sim.dll sweep 30`; 300 lines, 15% split, 15% cues): pairs
with a whole block >1 s off, of 30, at jitter 0/100/250/400 ms, 1.1 cuts: before
0/0/4/15, after 0/0/0/0. Cuts +0.4 s at line 100 and +3 s at 200: none either way; the
0.4 s step is told apart as before at 0 and 100 ms (15 and 22 lines >0.15 s off in both
runs) and mostly merged at 250/400 ms, before and after (4498 → 4205, 4032 → 2443 lines).
Numbers: the coverage test's pair right 0.997 (same), wrong 0.497 → 0.530; the 20-seed
scratch run right min 0.910 → 0.987, wrong max 0.570 → 0.543. `Compute` on 1,000 lines
about 20 ms before and after.
The next phase must know: no CLI change. Lines from the two dialogue lines either side
of a cut can still take the neighbouring offset (the boundary item in ui-plan.md).
Left open: the histogram counts every pair within ±10 min, so a short block stays a weak
peak: with seeds 31-60 too, 2 of 60 pairs still lose the -4 s block at 400 ms jitter.

### Lead — 2026-10-04 — after phase 1.1b
Reviewed the `CandidateOffsets` diff; 146 unit and 55 UI tests passed on my own run
(the editor's Auto Align uses the same code). Quality held over the first two phases:
both showed their key tests failing and reported honestly. Both used about 200k tokens;
1.2 and 1.3 are plumbing and should need far less.

### Phase 1.2 — 2026-10-04 — 753a90f, e74f258
Built: `RetimerEngine.TargetMatched()` → `Coverage` (target lines overlapping any reference
line, of all; none against an empty reference, where `MismatchFlags` flags nothing).
`SubsRetimer/AutoReport.cs`: records written by System.Text.Json, camelCase, indented, UTF-8
without BOM, non-ASCII unescaped. `Cli`: `--report PATH`, refused in `Parse` without `--auto`
or a value. Tests in `CliTests.Report.cs`, a partial of `CliTests`. Shape, abbreviated
(paths full, doubles unrounded; "no timed lines": `segments` [], the 3 measurements null):

    { "version": 1, "reference": { "path", "lineCount", "encoding" }, "target": { same },
      "output": "<intended path>", "segments": [ { "firstLine": 41, "lastLine": 120,
        "offsetMs": -15000, "matchedLines": 80, "lineCount": 80 } ],
      "referenceCoverage": { "covered", "counted", "share" },
      "targetMatched": { "matched", "lineCount", "share" },
      "averageMismatchSeconds": { "before": { "all", "matched" }, "after": { same } },
      "minMatch": 0.9 | null, "exitCode": 0 | 2, "saved": "<path>" | null,
      "reason": null | "below min-match" | "no timed lines" }

Order in `RunAuto`: both files given, then the stale report deleted (exit 1 there for a
missing folder, or a path equal to REFERENCE, TARGET or the output), then the loads. The
report follows the save and its stderr line, before stdout. A failed write: exit 1, stdout
empty, a half-written report removed, the saved subtitle file stays (not tested).
Choices / deviations: the same-file refusal is mine (the delete would destroy TARGET).
`--auto` with one file stays exit 1 before the delete, a command-line error.
The next phase must know: `Program.cs` untouched. Left open: `--report ""` fails in
`Path.GetFullPath` (exit 1, no usage text).

### Lead — 2026-10-04 — after phase 1.2
Reviewed the diff; 157 passed on my own run. Ran the built exe on a pair with Japanese
file names under `--min-match 0.9 --report`: exit 0, report readable, names unescaped.
Kept the agent's two additions (same-file refusal, early missing-folder failure).

### Phase 1.3 — 2026-10-04 — 66d5b18, eadb3e4
Built: `Program.Main` swaps in `Program.Utf8Writer(stream)` (`StreamWriter`, `new
UTF8Encoding(false)`, `AutoFlush = true`) with `Console.SetOut`/`SetError` for each stream
that is redirected; a console keeps .NET's writer; `Console.OutputEncoding` untouched.
`RunProcess` decodes stderr as UTF-8 (`StandardErrorEncoding`) and reads stdout as bytes
from `BaseStream`, decoded with `GetString`, so a BOM shows as U+FEFF in every real-process
test. `RealProcess_HonoursStdoutContract` expects `Environment.NewLine`. New, in
`CliTests.Utf8.cs`: `RealProcess_JapaneseNames_ReachTheCallerIntact` (folder `字幕フォルダ-<guid>`
under the temp `subsretimer-tests`, files `第1話 英語.srt`/`第1話 日本語.ass`, `CreateNoWindow`;
exit 0, stdout exactly path + newline with no BOM, the file exists, stderr's `saved` line
and both names in the summary; the folder deleted) and `Utf8Writer_...` (bytes, no BOM,
there before any Flush).
Choices / deviations: SetOut/SetError, so each stream has one writer and any later Console
write is UTF-8 too. AutoFlush, not a flush at the end of Main: nothing flushes a
StreamWriter at exit (checked in a scratch app). Found: outside Windows .NET takes a console
stream's encoding from `LC_ALL`/`LC_MESSAGES`/`LANG`; with `LC_ALL=en_US.ISO-8859-1` the
built exe printed the path as `??????` on Linux. The Japanese test sets that `LC_ALL` in the
child (Windows ignores it), so it fails on Linux without the change; without it the test
passes on Linux before and after, as the work order expected.
Seen failing: the Japanese test without the two lines in Main; Utf8Writer, HonoursStdout
and Japanese with a BOM-emitting encoding, and again without AutoFlush (stderr empty; stdout
survives through Cli's own Flush). 159 unit tests pass.
The next phase must know: redirected stdout/stderr are UTF-8 without BOM, lines end in
`Environment.NewLine` (CRLF on Windows), for the README contract.
Left open: nothing run on Windows; the code-page failure there awaits the windows-latest job.

### Lead — 2026-10-04 — after phase 1.3
Reviewed `Program.cs` and the test helper; 159 passed on my own run. Added the
`windows-tests` CI job (bc013aa) and the smoke test's Japanese pair (566ca47). Checked
the smoke section under pwsh 7.6 with a wrapper in place of the exe and
`LC_ALL=en_US.ISO-8859-1`: it passed with this branch's build and failed with the build
from before eadb3e4.

### Phase 1.4 — 2026-10-04 — 35ecf5f, 1e73193
Built: README "Usage" now holds `Cli.Usage` exactly (diffed against `--help`; it had left
out the first two and last two lines), plus "Reading the coverage line" (what it measures,
why on the reference, how to pick a threshold; no default promised). "How auto-align
works": 100 ms bins within ±10 min, the smoothing, up to 12 peaks. "Contract": exit 2
below `--min-match`, the gate's stderr line, UTF-8 without BOM and platform line ends
when redirected, the PowerShell `[Console]::OutputEncoding` line, and a "The report"
subsection with a field table. CHANGELOG: two features, two fixes. Plan: Status,
the Retime row of "What works today", S1-S4 and the subsretimer facts as built, Phases
item 1 done, the script's `$MinMatch` comment, two Remaining points.
Choices / deviations: the stderr and JSON examples come from the built exe on a
generated pair (right pair 100%, a wrong pair 67%, exit 2, `reason` "below min-match";
an empty target gave "no timed lines" with null measurements). The PowerShell line says
"PowerShell, Windows PowerShell 5.1 included" rather than 5.1 only: 7 decodes captured
output with `[Console]::OutputEncoding` too, as far as I know (not run on Windows). The
CHANGELOG says a Japanese path "could" come out as `?`: no Windows run has shown it.
Found: a stale report survives an exit 1 raised before `RemoveStaleReport` (bad option,
bad `--min-match`, `--auto` with one file, `--output ""`); `Cli.Usage` says "a file already
there is deleted first, so an error leaves none". The README says to read the report only
after exit 0 or 2. Also `--auto --report R --help` exits 0 with no report.
Left open: the `v*` tag (user); phase 2 work orders in subs2srs. 159 unit tests pass.
