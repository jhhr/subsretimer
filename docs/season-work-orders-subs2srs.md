# Season batch, subs2srs side: work orders for phase agents

You are one of a line of agents, each building **one small phase** of the season batch
(`docs/season-batch-plan.md` in the subsretimer repository) in the **subs2srs** repository.
A lead reviews your work when you report back. You have no memory of earlier phases; what
you need is here.

**Your context is the budget.** Aim to finish well under 200k tokens. The rules below say
how. They are about not reading huge files whole and not maintaining big documents; they
are **not** a licence to skip what you need to understand. Careful, correct work comes
first.

Two checkouts:

- `/home/user/subs2srs`: where you build. Branch `claude/hopeful-babbage-vrca6w`.
- `/home/user/subsretimer`: where this file and the spec live. You only append your log
  entry here and mark your phase in the spec. Its branch has the same name.

## 1. What to read (and what not to)

1. This file, all of it.
2. `/home/user/subs2srs/AGENTS.md`, all of it (130 lines). Its hard rules and gotchas
   apply to you; this file adds to them.
3. The spec, `/home/user/subsretimer/docs/season-batch-plan.md` (about 760 lines):
   "Decisions from your answers", "Design", "End state", under "Changes in subs2srs" the
   items your work order names, and the subs2srs entries of "Facts checked". Skip the
   subsretimer sections and the interim script unless your work order names them.
4. subs2srs docs, by section: `docs/architecture.md` "The pipeline", "Preview ↔ Go" and
   "Settings, preferences, files"; `docs/testing.md` "State isolation", "Harness" and
   "Testing media output". Others only if your work order names them.
5. Code: **never read these whole**; search, then read the range: `subs2srs/MainWindow.cs`
   (1,870 lines), `subs2srs/Settings.cs` (1,500), `subs2srs/WorkerSubs.cs` (1,360),
   `subs2srs/DialogPreview.cs` (2,000+). Files under 500 lines you may read whole when
   your phase changes them. Read an existing test next to where yours will go and copy
   its shape (`subs2srs.Tests/SubsProcessorE2ETests.cs`, `AiGroupingE2ETests.cs`,
   `EvalRunnerTests.cs`).

Do not read `/home/user/subsretimer/docs/season-work-orders.md` (phase 1's archive).

## 2. Rules

**Scope**

- Build your phase only. If something from a later phase is needed, build the smallest
  part of it and say so.
- The spec is agreed with the user. Where it is silent, choose the simpler option and
  note it. Where it is **wrong or impossible**, do not improvise another design: finish
  what can be finished, leave the tree building and green, and report.
- Do not edit `.github/`, `dist/`, `depends/`, the `Makefile`, `CHANGELOG.md`, the files
  under `docs/`, or package versions. If one needs a change, report exactly what; the
  lead makes it. (CHANGELOG and docs are brought up to date once, by the last phase.)
  `tests.md` is the exception: add a row for each new test file, as AGENTS.md asks.
- GTK-free code that the GUI and the command line share goes in `subs2srs/` (one
  namespace, flat folder) and must not touch GTK types. Code only the command line
  needs goes in `subs2srs.Cli/`.
- Match the surrounding code: naming, comment density, idiom, the file's own indent and
  **line endings** (AGENTS.md lists the CRLF files: `SubsProcessor.cs`, `WorkerSubs.cs`
  and `WorkerVars.cs` among them). After every edit session run `git diff --stat` and
  check that only the lines you meant changed.

**Build and test**

- Scratchpad: `/tmp/claude-0/-home-user-subsretimer/d251e84e-97b9-5ed0-94cf-8b728fa17524/scratchpad`,
  below `$S`. Logs and throwaway programs go there, never in either repository.
- Build: `cd /home/user/subs2srs && dotnet build subs2srs/subs2srs.csproj > $S/s2s-build.log 2>&1; echo exit=$? >> $S/s2s-build.log`,
  then `tail -5` it or grep it for `error`. Build `subs2srs.Cli/subs2srs.Cli.csproj` the
  same way once it exists.
- Unit and e2e tests: `dotnet test subs2srs.Tests/subs2srs.Tests.csproj --filter "FullyQualifiedName~ClassName" > $S/s2s-test.log 2>&1`
  for yours. The whole suite (534 passed, 4 skipped before phase 2) takes about 40 s with
  its build: run it **once** at the end, and again only if something failed. ffmpeg,
  ffprobe, mkvmerge and mkvextract are installed here, so the e2e tests run; check the
  skip count stays 4.
- UI tests only if your phase touched `MainWindow`, a dialog or a UI string:
  `GDK_BACKEND=x11 GSK_RENDERER=cairo xvfb-run -a -s "-screen 0 1280x1024x24" dotnet test subs2srs.UiTests/subs2srs.UiTests.csproj > $S/s2s-ui.log 2>&1`
  (21 tests, about 10 s), once at the end.
- Before your last commit, build and test once with `-c Release` too (CI runs Release),
  and `dotnet restore <project> --locked-mode` for every project whose references or
  packages you changed (CI restores that way; a new project needs its own
  `packages.lock.json`).
- **Never start `claude`, never touch the network.** AI tests use `FakeChatProvider` or
  `ClaudeCliProvider.RunnerOverride` (docs/testing.md).
- Every behaviour gets a test that can fail. Show it for the two or three that matter
  most by breaking the code for a moment; not for every test. Undo the break **by hand**:
  never `git checkout`/`git restore` a file to revert an experiment.
- Do not weaken or delete an existing test to get green. If one is wrong because the
  behaviour was meant to change, change it and say so.

**Docs: almost none.** You write only:

- one entry in the log (section 5 of this file), **25 lines at most**, appended at the
  end;
- "Done <date> (<commits>)" on your phase's line under "Phases" in the spec, and a
  correction of any spec statement your work proved wrong;
- `tests.md` rows in subs2srs for new test files.

Edit documents with the Edit/Write tools only: a shell heredoc or one-liner containing
backticks, `$` or non-ASCII text gets mangled and has corrupted documents before.

**Commit**

- In `/home/user/subs2srs`, on `claude/hopeful-babbage-vrca6w`. Commit when the whole
  suites are green; if they are not, do not commit: report.
- One commit per coherent step, subject `area: imperative summary` under 72 characters
  (`cli: ...`, `pipeline: ...`, `tests: ...`), a body saying why when it is not obvious.
  End every message with exactly these two trailer lines, after a blank line:

      Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
      Claude-Session: https://claude.ai/code/session_01EUD7eAkNWq1B625BRnujNX

- Then commit your log entry and the spec's phase line in `/home/user/subsretimer`
  (`docs: log phase 2.N ...`, same trailers).
- Stage files by name. Never `git add -A`, never push, never amend earlier commits.

**Report** (your final message; all the lead sees; under 60 lines)

- What was built, by file, briefly. Commit hashes in both repositories.
- The totals of the final full test runs, copied, not paraphrased (skips included).
- Which tests you saw fail without the change.
- Choices made, deviations, anything fragile or unfinished. Say it plainly: a problem
  reported is cheap, one found later is not.

## 3. State of the code (kept by the lead; as of 2026-10-04, before phase 2.1)

subs2srs `main` at `91578ce`. Facts checked by the lead, so you need not re-derive them:

- `subs2srs/subs2srs.csproj`: the GUI app; `Exe`, or `WinExe` on `win-x64`;
  `InternalsVisibleTo` `subs2srs.Tests` and `subs2srs.UiTests`; lock file on.
  `subs2srs.Eval` (a console exe referencing the app project, with its own
  `packages.lock.json`) is the template for `subs2srs.Cli`: `Program.Main` parses
  options, reads preferences with `PrefIO.read()` (or `--prefs`/`--no-prefs`), turns
  file logging off, echoes the log to stderr on `--verbose`, cancels on Ctrl+C through a
  token, maps a cancel to exit 130. Its `EvalRunner.ConsoleProgress` (private) is an
  `IProgressReporter` writing one stderr line (`\r` in a terminal). `subs2srs.Tests`
  references `subs2srs.Eval` the same way the CLI tests can reference `subs2srs.Cli`.
- `UtilsMsg.showErrMsg/showInfoMsg/showConfirm` already write `ERROR:`/`INFO:`/`CONFIRM:`
  lines to stderr, then call the GTK hooks if set; with no `OnShowConfirm` hook a confirm
  answers **no**. The GUI sets the hooks in `Program.Main`.
- `ProjectIO.Load(path)` restores `Settings.Instance` from an `.s2s.json`; every `Files`
  array is `[JsonIgnore]` and comes back empty. `VideoClips.AudioStream` is saved.
- `MainWindow.SaveSettings()` (`MainWindow.cs:1129-1277`) copies widgets into
  `Settings.Instance`, expands the patterns (`UtilsSubs.getSubsFiles` for subs,
  `UtilsCommon.getNonHiddenFiles` for video and audio) and truncates every `Files` array
  to `EpisodeEndNumber - EpisodeStartNumber + 1` when an end number is set.
  `GoAsync` (`MainWindow.cs:1346`) checks only that Subs1, output dir and deck name are
  not empty, calls `SaveSettings()` and `ConstantSettings.UpdateAudioFilenameFormats()`,
  asks `UtilsVideo.validateAudioStreamConsistency(files, streamIdx)` when audio comes
  from more than one video, then awaits `SubsProcessor.StartAsync(reporter, combinedAll,
  joins)`.
- `UtilsCommon.getNonHiddenFiles(pattern)`: `Directory.GetFiles(dir, pattern)`, not
  recursive, sorted with `List<string>.Sort()` (culture-sensitive), hidden files dropped.
- `SubsProcessor` (internal, CRLF) `StartAsync` returns `Task`, reports only through
  `UtilsMsg`, and turns every worker failure into `OperationCanceledException` ("Action
  cancelled."). `DoWork` runs: Combine subs, Inactivate lines (both skipped when
  `combinedAll` is passed), AI grouping (only when `WorkerSubs.aiGroupingOnGoApplies`:
  mode AI, the `AiGroupingOnGo` preference, and no joins for every episode), Group into
  snippets, context lines, Remove inactive lines, then the TSV and media workers.
- `WorkerSubs.runAiGrouping` loops over episodes, calls `AiGrouper.Group(lines, limits,
  options, progress, token)` and on `ProviderException` logs and leaves the episode to
  the rules. `AiGrouper.GroupAsync` returns a cached result when there is one
  (`FromCache`), throws `ProviderException` only when every chunk failed (or nothing
  could be asked), and otherwise returns `AiGroupingResult` with `Chunks` and
  `FailedChunks` (failed chunks grouped by the rules; such a result is not cached).
  `ClaudeCliProvider.UsageLimit` (static, sticky for the process) is set when a chunk
  hits the subscription limit.
- Episode numbers are computed in place as `index + EpisodeStartNumber` (or `epNum +
  start - 1`) in `WorkerSrs.cs` (370-527, tags, sequence markers, every media name),
  `WorkerAudio.cs` (240, 368, 389), `WorkerSnapshot.cs:82`,
  `WorkerAnimatedSnapshot.cs:92`, `WorkerVideo.cs` (155, 224), and in `WorkerSubs.cs`
  (96 and 130: the episode passed to the subtitle parser; 117 and 151: the per-episode
  **time-shift rule**; 985, 994, 1078: log lines). The GUI-only dialogs
  (`DialogPreview`, `DialogDuelingSubtitles`) compute it too.

## 4. Phases

Done: none yet.

### 2.1 — The `subs2srs-cli` project and the episode list (spec A1, A2)

- New project `subs2srs.Cli/subs2srs.Cli.csproj`: `Exe` on every platform, assembly
  `subs2srs-cli`, referencing `subs2srs`, lock file on; `InternalsVisibleTo
  subs2srs.Cli` in the app project (and the CLI's own for `subs2srs.Tests`), the tests
  project referencing it. Bootstrap as in spec A1 (preferences read, never written;
  `UtilsCommon.RegisterEncodings()`; `Console.OutputEncoding = UTF8`; `--yes` answers
  confirms; `--verbose`; Ctrl+C).
- Command: `subs2srs-cli go --project FILE [--season DIR] [--dry-run] [--yes]
  [--verbose] [--prefs FILE | --no-prefs]`, `--help`, `--version`. In this phase only
  `--dry-run` works: it resolves the episode list and prints it (stdout: one row per
  episode with its number, Subs1, Subs2, video, and the skip reason; stderr:
  diagnostics). Without `--dry-run` it refuses with exit 1 ("not built yet") until
  phase 2.3.
- A2, the file resolution moved out of `MainWindow.SaveSettings` into a GTK-free
  function over `Settings` (pattern expansion, the end-number truncation,
  `UpdateAudioFilenameFormats`) that `SaveSettings` now calls after copying the widgets.
  The GUI's behaviour must not change.
- A2, the season list, as a pure function (a directory listing in, rows out) plus a thin
  file-system wrapper: videos are the folder's `*.mkv` in `getNonHiddenFiles` order,
  numbered from the project's start number; Subs1 is `s2s/<video name>.ja.<ext>` and
  Subs2 `s2s/<video name>.en.<ext>` (`ass`, `ssa`, `srt`), exactly one each, matched by
  exact name (no wildcards: names contain `[`); otherwise the episode is skipped with
  the reason. The episode number of a later episode never depends on an earlier one
  being skipped. A project end number filters episodes out (not "skipped").
- Pattern mode (no `--season`): the project's own patterns through the shared function;
  unequal counts of Subs1, Subs2 (when given) and videos (when needed) are refused with
  exit 1 and the lists side by side.
- Exit codes for `--dry-run`: 0 every episode resolved, 3 some skipped, 1 error.
- Tests: the season function (gaps keep numbers; zero or two candidates; `.ja`/`.en`
  tags; bracketed and Japanese names; an old `Show - 01 - Track 03 - English.srt` is not
  a match), the shared resolution giving the same `Files` as before for a GUI-style
  pattern set, the pattern-mode refusal, and one in-process run of the command on a
  temp folder (exit 3 with a missing JP file). The UI tests must stay green (you
  changed `MainWindow`).
- The lead adds the CI restore line and the `Makefile` entries after this phase; say
  exactly what they need.

### 2.2 — Explicit episode numbers and a run result (spec A4, A5)

- `Settings.EpisodeNumbers` (not saved, like `Files`) and `Settings.EpisodeNumber(index)`
  falling back to `index + EpisodeStartNumber`, used at **every** site listed in
  section 3, the time-shift rules and the parser's episode included (the GUI dialogs
  too, for one rule everywhere). Mind the 1-based `epNum` sites.
- `StartAsync` returns a result: `Completed`, `Cancelled` or `Failed`, a message, and the
  card count per episode. A worker failure must no longer read as a cancel. The GUI
  keeps its dialogs.
- Tests: episodes {1, 3} through the pipeline give names, tags and the time-shift rule
  of 1 and 3; a failing worker gives `Failed`, a cancel `Cancelled`.

### 2.3 — Checks, `go` runs the pipeline, the table (spec A3, A7)

- The checks of A3, shared with `GoAsync`; `go` without `--dry-run` runs `StartAsync`
  with the resolved episodes and `EpisodeNumbers`; progress on stderr; the season table
  and exit codes (spec "Design" 8). With snippet mode AI, refuse unless `--grouping
  rules|off` is given: the AI pre-pass is phase 2.4.

### 2.4 — The AI pre-pass (spec A6)

- As in spec A6, with one refinement found by the lead: an episode whose failed chunks
  came from the usage limit (`ClaudeCliProvider.UsageLimit` set) is skipped, not kept
  with those chunks grouped by the rules; a later run redoes it whole (such results are
  not cached). Other partial failures keep the episode, with a warning in the table.

### 2.5 — Packaging (spec A8)

- Lead and agent split decided after 2.3: `Makefile` install, the Windows bundle and
  release zip, `smoke.ps1` with two apphosts.

### 2.6 — Documentation

- subs2srs README, CHANGELOG, `docs/architecture.md` (a section on the command line,
  and the outdated "Until the tool's editor is ported, non-auto runs exit 1" in the
  launcher contract), `docs/testing.md`, `docs/open-items.md`; the spec's subs2srs
  section made to describe what was built; the interim script in the spec checked
  against the real `go`.

### Phase 3 (B, C, D)

Cut after phase 2, in this file.

## 5. Log (newest last; 25 lines at most per entry)

Template:

    ### Phase <id> — <date> — <commits>
    Built: ...
    Choices / deviations: ...
    The next phase must know: ...
    Left open: ...
