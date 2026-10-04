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

## 3. State of the code (kept by the lead; as of 2026-10-04, before phase 1.1)

- The editor branch is merged (`main` at `a209301`, merged into this branch). 121 unit
  tests pass.
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

Done: none yet.

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
