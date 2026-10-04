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

## 3. State of the code (kept by the lead; as of 2026-10-04, after phase 3.4b)

Branch `claude/hopeful-babbage-vrca6w`, from `main` at `91578ce`. After phase 2.1:

- `subs2srs/ProjectFiles.cs`: `Resolve()` fills every `Files` array from its pattern, cuts
  them to Episode End # (`EpisodeLimit(start, end)`) and calls
  `UpdateAudioFilenameFormats()`; `MainWindow.SaveSettings` calls it after copying the
  widgets. A UI test pins `SaveSettings`' arrays (`MainWindowFlowTests`,
  `PatternSet` harness).
- `subs2srs.Cli/` (assembly `subs2srs-cli`, lock file, `InternalsVisibleTo` both ways):
  `Program.Main` (UTF-8 writers for redirected streams; `OutputEncoding` UTF-8 only on a
  console, restored at exit) → `CliRunner.RunAsync(args, stdout, stderr, token)`, the
  in-process entry the tests use; `CliOptions` (parsing); `EpisodeList` (`Episode`
  rows with `Number`, files, `SkipReason`; `ForSeason` pure, `FromSeason`,
  `FromPatterns`/`Pair`); `TextTable`. Preferences are read with `PrefIO.ReadFile`
  (never written). `go` without `--dry-run` exits 1 "not built yet". Tests:
  `EpisodeListTests`, `CliTests`, `ProjectFilesTests`. CI restores the project.
- Suites after 2.1: unit 575 passed, 4 skipped; UI 22 passed. One Release UI run failed
  `PreviewGroupingScrollTests` with "leaked MainWindow" and did not recur in 14 runs: if
  you see it, report it with the log, do not retry it away.

- Phase 2.2: `Settings.EpisodeNumbers` (`int[]?`, `[JsonIgnore]`, nulled by `Reset`
  and `RestoreFrom`) and `Settings.EpisodeNumber(index)` (0-based; throws outside a set
  list) at all 36 former sites. Its length must equal the `Files` arrays'. Unit 583
  passed / 4 skipped, UI 22.

- Phase 2.2b: `SubsProcessor.StartAsync` returns `Task<PipelineResult>`
  (`subs2srs/PipelineResult.cs`: `Status` Completed/Cancelled/Failed, one-line
  `Message` "<step label> failed: <detail>", `CardsPerEpisode` by index, context-only
  lines left out). Cancelled only when the reporter's `Cancel` or token is set. The GUI's
  dialogs are unchanged (a failed step still shows "Action cancelled." there).
  `Settings.EpisodeCountForNames` (`int?`, `[JsonIgnore]`, cleared by `Reset`/
  `RestoreFrom`, so set it **after** `ProjectIO.Load`) and
  `Settings.EpisodeCountForPadding(runEpisodes)` drive the `episode_num` padding of the
  five pipeline `UtilsName`s. Unit 594 passed / 4 skipped, UI 22.
- Phase 2.3: `subs2srs/GoChecks.cs`: `Run(settings, audioStreamIndex, aiGroupingRuns)`
  → `List<GoProblem>` (`IsError`, `Message`): output dir, deck, ffmpeg, animated encoder,
  `claude` (terminal- model, when AI grouping runs), audio streams (warning).
  `AudioStreamIndex(settings)` maps the project's stream to the GUI's index.
  `ClaudeCliProvider.ResolveExecutable()`. `GoAsync` runs them after `SaveSettings` (all
  errors in one dialog, each warning confirmed). `go --dry-run` prints them on stderr
  under the table; errors exit 1. Season mode sets `VideoClips.Files` (ready episodes)
  before the checks. CLI tests that dry-run put a fake `ffmpeg` in the Tools Directory.
  Unit 607 / 4 skipped, UI 23.
- Phase 2.3b: `go` runs. `CliRunner.GoAsync`: load, `--grouping rules|off`, refuse AI
  mode without it (exit 1, before anything is read or written), resolve, `SetUpSeasonRun`
  (season mode: `Subs[0/1].FilePattern` = the first ready episode's own files,
  `Files`, `AudioClips.Files` cleared, `EpisodeNumbers`, `EpisodeCountForNames`,
  `UpdateAudioFilenameFormats`), checks (warnings through `UtilsMsg.showConfirm`, yes
  only with `--yes`), `StartAsync` with `subs2srs.Cli/ConsoleProgress.cs`, the table
  (`#`, `Episode`, `Status`, `Cards`) and `season TSV: <path> (k of n episodes); exit
  N`. `PipelineResult.ImportFile` (from `WorkerSrs.ImportFile`); a Failed or Cancelled
  run deletes its partly written TSV. Exit 0/3/1/130; no ready episode → table, exit 3,
  no pipeline. Unit 614 / 4 skipped, UI 23.
- Phase 2.4: `subs2srs.Cli/AiPrePass.cs` (`FirstSteps`, `Run`, `DropSkipped`,
  `KeepEpisodes`); outcomes `cached`, `grouped`, `k/n by rules` (kept, warned), `usage
  limit`, `failed` (skipped). Once the limit is set, only episodes needing a request are
  skipped (a cached one still runs). `go` then calls `StartAsync(progress, combinedAll,
  joins)`; the table has an `AI` column; `--dry-run` shows `cached`/`not cached`.
  Tests `CliAiPrePassTests`. Unit 621 / 4 skipped.
- Phase 2.5: `make build`/`install` publish and install `subs2srs-cli` beside the app
  (wrapper `dist/subs2srs-cli.sh`, no `cd`, `mkdir -p` of the XDG config and data
  folders); `make publish-windows` and `release.yml` publish it self-contained into
  `out\win-x64` after the app (the shared files come out byte-identical);
  `smoke.ps1` checks `subs2srs-cli.exe --version` and `go --help`; `release.yml` runs
  by hand (`workflow_dispatch`, version `0.0.0-dev`, no release without a tag).
- Phase 3.1: `subs2srs/MkvTracks.cs`: `MkvTrackInfo` (`IsText`, `IsImage`, `IsEnglish`,
  `Label` like `3 "English" 312 ev`), `Parse(json)`, `Pick(tracks, trackId?)` (track or
  reason), `ListAsync`/`PickAsync(file, trackId?, ct)` (`mkvmerge --output-charset UTF-8
  -J <full path>`; exit 1 = warnings, OK; every failure a reason, only a cancel throws),
  `MkvTracks.StartInfo(exe, args)` (sets `LC_ALL=C.UTF-8` off Windows when the locale is
  not UTF-8: mkvmerge drops non-ASCII names and cuts `-J` output otherwise),
  `RunnerOverride` (gets the `ProcessStartInfo`, returns `CliProcessResult`).
  `ConstantSettings.ExeMkvMerge`; `ResolveTool` searches `%ProgramFiles%\MKVToolNix` and
  `%ProgramFiles(x86)%\MKVToolNix` after PATH for the three mkv tools on Windows
  (`MkvToolNixDirs`, `MkvToolNixDirsOverride`). `[RequiresMkvToolnixFact]`. Fixtures in
  `subs2srs.Tests/Fixtures/mkv/`. CI installs MKVToolNix (d7911ff). Unit 665 / 4 skipped.
- Phase 3.2: `subs2srs/MkvExtract.cs`: `Extension(codecId)`, `OutputPath(season, video,
  track)` (`<season>/s2s/<video name>.en.<ext>`), `ExtractAsync(mkv, trackId, output, ct)`
  → `MkvExtractResult` (`Extracted`/`Kept`/`Failed`, `OutputPath`, `Reason`); a non-empty
  existing output is `Kept` before the tool is even looked for; failures and cancels
  delete the partial file; `RunnerOverride`. mkvextract 82 writes ASS and SRT as UTF-8
  with a BOM, LF, and reports errors on stdout. `MkvTracks.RunAsync` is internal and
  shared. "Kept" means that exact path: an earlier `.en.srt` beside a new `.en.ass`
  makes two `.en` files, which `go` skips; 3.4 decides. Unit 694 / 4 skipped.
- Windows mkvmerge has no `--command-line-charset` (the test helper `Mux` passed it;
  fixed in a53049d). CI is green on a53049d on both jobs, so the real mkvmerge and
  mkvextract tests (`--output-charset UTF-8` included) pass on Windows too.
- The user opened jhhr/subs2srs#5 from this branch (to `main`). `main` was merged in
  (95d3dfe): CI now runs on push only for the default branch, so a push to this branch
  is checked by the pull request's run alone.
- After phase 3.3 (152bfb7): `SubsRetimerLauncher.StartInfo(exe, request)` uses
  `makeToolStartInfo`; `RunAsync` goes through `RunnerOverride ??
  UtilsCommon.RunToolAsync` (the one shared process loop, also used by `MkvTracks` and
  `MkvExtract`): a cancel kills the tool and throws `OperationCanceledException`; tool
  failures come back as `Result.Failed` with the message in `StdErr`. `Request` has
  `OutputPath`, `MinMatch` (`double?`), `ReportPath`. `RetimeReport.Read(path)` →
  `ExitCode`, `Saved`, `Segments`, `ReferenceCoverage` (share or null), `Reason`
  (`RetimeReport.BelowMinMatch` / `NoTimedLines`), null when unreadable.
  `SubsRetimerLauncher.EditorCommand(exe, request)` prints no `--`, so give it full
  paths. Report fixtures in `subs2srs.Tests/Fixtures/retime/`. Unit 734 / 4 skipped.
- **`SUBSRETIMER_EXE`**: this container sets it to `/opt/subsretimer/subsretimer`, a
  2026-09-28 build without `--min-match`/`--report`, and the real-tool tests fail with
  it. Run the suites with `SUBSRETIMER_EXE=$S/subsretimer-bin/subsretimer` (subsretimer
  at 5d8475e, built by phase 3.3; rebuild it there from `/home/user/subsretimer` with
  `dotnet build SubsRetimer/SubsRetimer.csproj -c Release -o $S/subsretimer-bin` if it is
  missing). Unset, the real-tool tests skip (8 skips after 3.3b): CI runs that way.
- After phase 3.3b (3ac375a, 75c7246): `subs2srs.Cli/RetimeStage.cs`:
  `FindJpFiles(season, videos)` (give it every video: a name that is also a longer
  video's JP file is that video's; tags may be dotted, English when any word is
  `en`/`eng`), `FindEnFiles(season, videos)` (for `--only retime`), `FoundFile(Path,
  Problem)` with `Of`/`Missing`, `RetimeOptions.FromSettings(settings, minMatch, force)`
  (subsretimer resolved once; Subs1 encoding), `RetimeAsync(season, video, jp, en,
  options, ct)` → `RetimeOutcome` (`Kind`, `OutputPath`, `Report`, `Reason`,
  `EditorCommand`, `Ready`, `Column`). With no JP or no EN file it touches nothing (a
  `.ja` file may stay: leave a not-Ready episode out of `go`); it deletes files in every
  other path, so `--dry-run` must not call it. It prints nothing. Unit 782 / 4 skipped.
- After phase 3.4 (dd759c0): `subs2srs.Cli/SeasonCommand.cs`: `RunAsync(options, stdout,
  stderr, token)` (dispatched from `CliRunner.RunAsync` on `options.Command`) builds one
  mutable `SeasonEpisode` per video (`Video`, `Pick`, `Extraction`, `En`, `Jp`,
  `Retime`, the dry run's `EnPlan`/`RetimePlan`, `Cancelled`); the table is
  `Columns(options)`, a list of `SeasonColumn(Header, Cell)`, printed by `PrintReport`
  (table, track note, `retimed JP files: k of n episodes; exit N`, editor commands).
  `CliOptions`: `season <dir>` positional, `Track`, `MinMatch`, `Force`, `Only`
  (`SeasonStage` Extract/Retime; `--only go` refused until 3.4b); go's `--grouping` and
  `--season` refused on season. `EpisodeList.SeasonVideos(dir)` (the `*.mkv`, sorted).
  `RetimeStage.WouldKeep` shared by run and dry run. `MkvTracks.PickAsync` refuses a
  non-Matroska container. `CliRunner.GoAsync` is still one method: `LoadProject`,
  `FromSeason`, `SetUpSeasonRun` (sets `EpisodeNumbers` from the ready episodes), checks,
  `AiPrePass`, `SubsProcessor.StartAsync`, `PrintTable`. Unit 809 / 4 skipped.
- After phase 3.4b (0a3803e): `season` runs `go` in the same process on the Ready
  episodes (`SeasonCommand.ForGo`; the others skipped with the retime's reason), with
  `--deck` (also on `go`), `--grouping`, `--only go`; Subs2 read as UTF-8 with one
  warning. `CliRunner.GoAsync` is split: `ApplyCardOptions`, `PlanGo` (dry run →
  `GoPlan`), `RunGoAsync(list, yes, stderr, token)` → `GoRun` (`Cells()`, `TsvLine`,
  `Stopped`); `go`'s output is unchanged. Unit 818 / 4 skipped (fresh
  `SUBSRETIMER_EXE`), 814 / 8 unset.
- A stray empty file `/dummy` (outside both repositories) was left by phase 3.4's agent;
  the user decides about it. Do not create files outside the repositories and `$S`.
- CI: the Windows UI job once hung in `PreviewGroupingTests.Preview_ProposesEditsAndExportsGrouping`
  (GTK thread wedged, 11 timeouts after it) on b912688 and passed on the next commit. Not
  caused by this work as far as known; if you see it, report it with the log.
- Correction to the facts below: only a step that returns null/false used to become
  `OperationCanceledException`; ffmpeg errors throw their own exception.

Facts checked by the lead before phase 2.1, so you need not re-derive them:

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

Done: 2.1, 2.2, 2.2b, 2.3, 2.3b, 2.4, 2.5, 2.6 (phase 2 complete), 3.1, 3.2.

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

### 2.2 — Explicit episode numbers (spec A4)

- `Settings.EpisodeNumbers` (not saved, like `Files`; null by default) and
  `Settings.EpisodeNumber(index)` falling back to `index + EpisodeStartNumber`, used at
  **every** site listed in section 3: the workers, the parser's episode, the per-episode
  time-shift rule, the log lines, and the GUI dialogs too, for one rule everywhere. Mind
  the 1-based `epNum` sites. Reset with the other per-run state.
- Nothing sets `EpisodeNumbers` yet except tests: the GUI's behaviour must not change.
- Tests: episodes numbered {1, 3} through the e2e pipeline give the TSV tags, sequence
  markers and every media file name of 1 and 3; a time-shift rule from episode 3 applies
  to the second file and not the first; null keeps today's numbering.

### 2.2b — A run result, and stable name padding (spec A5)

- `StartAsync` returns a result: `Completed`, `Cancelled` or `Failed`, a message, and the
  card count per episode. A worker failure must no longer read as a cancel. The GUI
  keeps its dialogs (`GoAsync` may show them from the result).
- Episode-number padding (found in 2.2, decided by the lead): `${0:episode_num}` is
  padded to the digits of the run's episode **count** (`UtilsName`,
  `getMaxNecessaryLeadingZeroes(totalNumEpisodes)`, with `totalEpisodes` taken from the
  `Files` arrays). A season run that skips episodes would then name the same episode
  `3` in one run and `03` in the next, and re-runs would not reuse media or keep tags
  (spec "Design" 7). Add `Settings.EpisodeCountForNames` (`int?`, `[JsonIgnore]`, reset
  like `EpisodeNumbers`): when set, every `UtilsName` the pipeline builds pads by it
  instead of the run's count. Phase 2.3 sets it, in season mode, to the season's video
  count within Episode End # (skipped episodes included), which gives the names a GUI
  run over every episode would give. Null keeps today's behaviour.
- Tests: a failing worker gives `Failed` with its message, a cancel `Cancelled`, a run
  the card counts per episode; with `EpisodeCountForNames = 10` a two-episode run pads
  episode 3 as `03` in tags and media names, and null keeps today's names.

### 2.3 — Checks before starting (spec A3)

- The checks of A3 as GTK-free code in `subs2srs/` (one function returning the problems
  found, so the CLI can print them all at once), called by `GoAsync` in place of its
  three emptiness checks, with the GUI's messages and confirm unchanged in wording where
  they exist: output dir creatable and writable, deck name, ffmpeg found, the animated
  snapshot encoder when that output is on, the audio-stream consistency across videos
  (a warning: the GUI asks, the CLI refuses unless `--yes`), and `claude` found when
  snippet mode is AI with a `terminal-` model (`ClaudeCli`).
- `go --dry-run` runs the checks too and lists their results under the table.
- Tests: each check on its own (a missing tool through the tools-dir override or a
  PATH without it; an unwritable output dir), and `GoAsync`'s refusal through the UI
  test harness for one of them.

### 2.3b — `go` runs the pipeline, the table (spec A7)

- `go` without `--dry-run`: load the project, resolve the episodes, run the checks, then
  set up `Settings` for the run and call `StartAsync` with a stderr progress reporter
  (copy `subs2srs.Eval`'s `ConsoleProgress`). Afterwards the season table (episode,
  status, cards) on stdout and the exit codes of spec "Design" 8 (0, 3, 1, 130). Ctrl+C
  cancels through the token.
- Season mode setup, from 2.1 and 2.2b: set `Subs[0/1].FilePattern` as well as `Files`
  (the pipeline reads `Subs[1].FilePattern != ""` at WorkerSrs.cs:259, 344 and
  WorkerSubs.cs:707, and checks both patterns for VobSub at WorkerSubs.cs:66, 723); clear
  `AudioClips.Files` (WorkerAudio.cs:67-68 indexes it even with audio from the video);
  set `VideoClips.Files`, `EpisodeNumbers` (the resolved, non-skipped episodes) and
  `EpisodeCountForNames` (the season's video count within Episode End #) after
  `ProjectIO.Load`; call `UpdateAudioFilenameFormats()`.
- With snippet mode AI, refuse unless `--grouping rules|off` is given (`--grouping`
  overrides the project's mode for this run): the AI pre-pass is phase 2.4.
- Tests (e2e, small: the test video, two or three episodes, rules grouping): a season
  folder with episode 2's JP file missing gives exit 3, cards for 1 and 3 with their
  numbers and stable padding, and the table; pattern mode with equal counts gives exit
  0; a failing step gives exit 1 with the result's message; AI mode without `--grouping`
  is refused.

### 2.4 — The AI pre-pass (spec A6)

- `go` with snippet mode AI and no `--grouping` no longer refuses: before the pipeline it
  runs "Combine subs" and "Inactivate lines" for the ready episodes (the same
  `WorkerSubs` calls `DoWork` makes, on a `WorkerVars` like `StartAsync`'s), then, episode
  by episode, `AiGrouper.Group` with `AiGroupingOptions.FromSettings()` and records one
  outcome per episode: `cached` (`FromCache`), `grouped`, `grouped, k of n chunks by
  the rules` (a warning), or skipped with the reason (`ProviderException`: its message;
  the usage limit: say so).
- The usage limit (`ClaudeCliProvider.UsageLimit`, sticky for the process): once it is
  set, every remaining episode is skipped without asking the provider, and an episode
  whose failed chunks came from the limit is skipped too, not kept with those chunks
  grouped by the rules (a re-run redoes it whole: such results are not cached). Find
  out how to tell "the limit was hit during this episode" (the property before and
  after the call is the simple way).
- Then drop the skipped episodes from the line lists, the `Files` arrays and
  `EpisodeNumbers` (keep `EpisodeCountForNames`), and call `StartAsync(reporter,
  combinedAll, joins)` with the joins from `AiGrouper.ApplyToLines`, so the pipeline
  skips its first steps and its own AI step. The *Remove duplicate lines* table spans
  the episodes that run, as in the GUI.
- If every episode is skipped by the pre-pass, do not start the pipeline: table, exit 3.
- The table gains an `AI` column (`cached`, `grouped`, `k/n by rules`, `usage limit`,
  `failed`, or `-` when grouping is not AI). `--dry-run` fills it with `cached` or
  `not cached` without asking the model (`AiGrouper.Estimate` reports `Cached`), which
  needs the same two first steps.
- Tests (no network, never `claude`): with `FakeChatProvider` over a 3-episode season,
  all grouped, exit 0, the cards follow the fake's grouping; a cached episode is not
  asked again (count the fake's requests); with `ClaudeCliProvider.RunnerOverride`
  scripted to answer episode 1 and hit the usage limit on episode 2, episodes 2 and 3
  are skipped, no request is made for 3, cards exist for 1 only, exit 3, and a second
  run with the limit reset and the runner answering does only 2 and 3 (1 from the
  cache) and rewrites the TSV with all three; a chunk that fails for another reason
  keeps its episode with the warning. Reset every static hook in `Dispose`.

### 2.5 — Packaging (spec A8)

- Lead and agent split decided after 2.3: `Makefile` install, the Windows bundle and
  release zip, `smoke.ps1` with two apphosts.

### 2.6 — Documentation

- subs2srs README, CHANGELOG, `docs/architecture.md` (a section on the command line,
  and the outdated "Until the tool's editor is ported, non-auto runs exit 1" in the
  launcher contract), `docs/testing.md`, `docs/open-items.md`; the spec's subs2srs
  section made to describe what was built; the interim script in the spec checked
  against the real `go`.

### Phase 3 (B, C, D): `subs2srs-cli season`

Read the spec's sections B, C and D and "Design" points 1, 7 and 9 for every phase 3 work
order, and the interim script in the spec (the behaviour phase 3 takes over, tested on
PowerShell). Facts checked by the lead (2026-10-04, subs2srs at 3f31e62):

- Tools are found by `ConstantSettings.ResolveTool(name)`: the *Tools Directory*
  preference, then PATH. There is no `mkvmerge` entry yet (`ExeMkvInfo`,
  `ExeMkvExtract` exist, `Settings.cs:293-301`), and no fallback to MKVToolNix's
  Windows install folder: the user's interim run failed on exactly that
  (`mkvextract` not found with MKVToolNix installed in `C:\Program Files\MKVToolNix`).
- `SubsRetimerLauncher` (`subs2srs/SubsRetimerLauncher.cs`, 126 lines) builds its own
  `ProcessStartInfo` without UTF-8 encodings; `BuildArguments` passes `--auto`,
  `--print-output`, both encodings and `--` + the two paths; `ParseResult` takes the
  last non-empty stdout line; `RunAsync` never throws for tool failures.
- subsretimer's side (its README "Contract for other programs"): `--min-match F`
  (exit 2 below it), `--report PATH` (JSON, `version` 1; `reason` null /
  `"below min-match"` / `"no timed lines"`; `segments`, `referenceCoverage.share`,
  `saved`), `--output` overwrites, stdout/stderr UTF-8 when redirected, exit 1 with a
  message naming `--target-encoding` for a target that does not decode.
- mkvextract (82) writes SRT as UTF-8 with a BOM and exits 2 for a track id that does
  not exist. mkvmerge and mkvextract are installed here (`/usr/bin`); CI does not
  install them yet (the lead adds `mkvtoolnix` to CI when a phase needs it).

#### 3.1 — Track list and pick, and finding MKVToolNix (spec B1, B2)

- Pure: parse `mkvmerge -J` output into tracks (id, type, codec id, language and IETF
  language, track name, forced, default, event count) and pick the EN track as B2
  says, or return why there is none (no English text track; only image tracks). An
  explicit track id overrides (and is checked to be a text subtitle track).
- The runner: `mkvmerge -J <file>` through `UtilsCommon.makeToolStartInfo`, with a
  process seam for tests. `ConstantSettings.ExeMkvMerge` and its path like the others.
- MKVToolNix lookup: for `mkvmerge`, `mkvextract` and `mkvinfo`, after the Tools
  Directory and PATH, on Windows also `%ProgramFiles%\MKVToolNix` (and
  `%ProgramFiles(x86)%\MKVToolNix`). This also fixes the GUI's MKV dialogs.
- Tests: JSON fixtures recorded from real `mkvmerge -J` on mkv files you make here with
  mkvmerge from generated subtitles (no third-party content; several tracks: eng ASS,
  eng SRT, eng forced, jpn, a track with an IETF `en-US`), plus hand-written JSON for
  image tracks (`S_HDMV/PGS`, `S_VOBSUB`), marked as hand-written; the pick for each;
  the override; the Windows fallback through an injected folder list (no real
  `Program Files` needed).

#### 3.2 — Extraction (spec B3)

- Build the process with `MkvTracks.StartInfo(exe, args)` (the locale fix of 3.1:
  mkvextract has the same non-ASCII problem under a C locale) and pass full paths (a
  name starting with `@` is read as an option file).
- `mkvextract <mkv> tracks <id>:<out>`; skip an existing
  output; exit 2 or more fails the episode and deletes the partial file; the error text
  is kept for the table. The output name is `s2s/<video name>.en.<ext>` with `<ext>`
  from the codec (ASS/SSA → `ass`/`ssa`, UTF8 → `srt`).
- Tests: the pure name/ext choice; a real extraction of a generated mkv
  (`[RequiresMkvToolnixFact]`, a new skip attribute like `RequiresFfmpegFact`); a bad
  track id leaves no file; an existing output is not touched.

#### 3.3 — The launcher (spec C2, Design 9)

Split from the old 3.3 by the lead: this phase is `SubsRetimerLauncher` only (126 lines,
`subs2srs/`, LF); 3.3b uses it. Facts (lead, 2026-10-04): `RunAsync` builds its own
`ProcessStartInfo` with no encodings; on a cancel it returns exit 2 "Cancelled" and
**leaves the child running** (`WaitForExitAsync(token)` only stops waiting). The one
caller is `DialogSubsRetimer.cs:215-253` (`Saved` / `NothingSaved` / failed message).
`MkvTracks.StartInfo(exe, args)` and `MkvTracks.RunAsync(psi, ct)` (internal: kill on
cancel, wait, rethrow) are the patterns to reuse; subsretimer is a .NET program, so the
`LC_ALL` part is harmless but not needed.

- Through `UtilsCommon.makeToolStartInfo` (UTF-8 pipes, no window), arguments by
  `ArgumentList`. A cancel kills subsretimer and waits for it, and the caller can tell
  a cancel from exit 2 (throw `OperationCanceledException`, or a `Cancelled` result:
  your choice; the dialog keeps its behaviour).
- `Request` gains `MinMatch` (`double?`, passed only when set, invariant culture) and
  `ReportPath`; `OutputPath` exists. `BuildArguments` keeps `--` before the paths.
- `RetimeReport.Read(path)` (or a method on the launcher): the fields 3.3b's table needs
  from subsretimer's `--report` JSON (`version` 1): the exit code, `saved`, the
  segment count (`segments`), `referenceCoverage.share`, `reason` (null /
  `"below min-match"` / `"no timed lines"`). A missing or unreadable report is null,
  not an exception. Read subsretimer's `SubsRetimer/AutoReport.cs` and
  `SubsRetimer.Tests/CliTests.Report.cs` in `/home/user/subsretimer` for the shape.
- `EditorCommand(exe, request)`: the command that opens subsretimer's editor on a pair
  (Design 9): no `--auto`, `--min-match`, `--report` or `--print-output`; with
  `--target-encoding` (and `--ref-encoding` unless UTF-8) and `--output`. Quoted for
  the platform's shell, the platform a parameter so both are tested: POSIX single
  quotes; on Windows double quotes, in a form that runs in PowerShell (the user's
  shell; mind a full exe path with spaces, which PowerShell needs `& ` for). Check
  how subsretimer's `Cli.cs` takes the editor's arguments.
- Tests (extend `SubsRetimerLauncherTests`): the arguments; the start info's encodings;
  the report on JSON written by the real tool (generate it once with subsretimer
  built from `/home/user/subsretimer` into `$S`, copy it into a fixture); the editor
  command on both platforms with spaces, quotes and Japanese names; a cancel kills
  the child (a scripted runner, or a real child process that sleeps); one test gated
  on `SUBSRETIMER_EXE` that runs the real tool on a pair in a Japanese folder and
  gets the saved path back intact. Run that one locally with the variable set.
- UI tests: the dialog uses the launcher.

#### 3.3b — The retime stage (spec C1, C2, Design 7 and 9)

Command-line only: `subs2srs.Cli/`, next to `EpisodeList` (which already finds
`s2s/<name>.ja.*` / `.en.*` for `go --season`; read it first).

- C1, pure: the JP file of a video is the one subtitle file (`.ass`, `.ssa`, `.srt`) in
  the season folder named `<video name>.<ext>` or `<video name>.<tag>.<ext>`, a tag
  other than `en`/`eng` (an English file beside the video is not the JP one); zero or
  several skip the episode with the names. Names with dots (`Show.S01E01.1080p`) and
  names that prefix another (`Ep 1`, `Ep 10`) must work; compare names, do not glob.
- Per episode, given the video, its EN file and its JP file: output
  `s2s/<video name>.ja.<ext of the JP file>`; keep it when it is newer than both its
  EN and JP files (an earlier run's, or a fix saved from the editor), unless `force`;
  otherwise delete every `s2s/<video name>.ja.*` first (also when keeping, delete the
  other `.ja.*` beside the kept one: `go` skips an episode with two), then run the
  launcher with `--auto`, `MinMatch` (null: not passed; 3.4 sets the default),
  `--report s2s/<video name>.retime.json`, `--target-encoding` the project's Subs1
  encoding, `--ref-encoding utf-8` (extracted EN is UTF-8), `--output OUT`.
- The result per episode for 3.4's table: kept / retimed (`2 cuts, 97% of EN covered`
  from the report) / below `--min-match` (`41%`) / no timed lines / failed (the
  tool's message for exit 1; subsretimer not found) / no JP file (the names) / no EN
  file; and for exit 2 the editor command (3.3's `EditorCommand`). An exit 1 gets no
  command (Design 9). A cancel propagates. The command's paths are full paths (it has no
  `--`); its exe is `subsretimer` when PATH finds that same file, else the full path.
- For `--only retime` 3.4 needs the EN file without extracting: the one `s2s/<video
  name>.en.*` (none or several: the reason). Provide it here.
- Tests: the JP lookup (pure); keep-if-newer, `force`, the stale-output delete and the
  sibling delete with a scripted launcher (a seam like `MkvExtract.RunnerOverride`);
  the table text per outcome; one env-gated real-tool test (`SUBSRETIMER_EXE`) end to
  end on a two-episode folder (one retimed, one below `--min-match` with its editor
  command). Run it locally with the variable set.

#### 3.4 — `subs2srs-cli season`: extract and retime (spec B, C, D, End state)

Split by the lead: this phase is the command and its first two stages; 3.4b adds `go`.
Read `subs2srs.Cli/CliRunner.cs` (`go`, `--season`, how options, the project, the table
and exit codes are done), `MkvTracks`, `MkvExtract` and `RetimeStage` (all built for
this) first; reuse them, do not re-implement.

- `season DIR --project FILE [--track ID] [--min-match F] [--force] [--only
  extract|retime] [--dry-run] [--yes] [--verbose] ...`, parsed and loaded as `go`
  does. Per episode (the season's videos, as `go --season` lists them; a video that is
  not an mkv cannot be extracted: say so): the EN track (`MkvTracks.PickAsync`,
  `--track` overriding), the extraction (`MkvExtract`), the JP file
  (`RetimeStage.FindJpFiles` with **every** video), the retime
  (`RetimeStage.RetimeAsync`, options resolved once). A failed extraction is passed on
  as `FoundFile.Missing(reason)`. Episodes run one after another; a cancel stops the
  run (exit 130).
- Two `.en` files: the picked track's output is the episode's EN file; any other
  `s2s/<name>.en.<ass|ssa|srt>` is an earlier extraction of another track (made from
  the mkv, so not hand work) and is deleted. Same name, other track (`--track` changed
  between runs) cannot be seen from the file: `--force` extracts again as well as
  retimes again; say so in the help text.
- `--min-match`: no default until the user's phase 0 run gives a number; not given, it
  is not passed (the help text says so).
- One table: Episode, EN track (`3 "English" 312 ev`; the problem when none), Retime
  (`RetimeOutcome.Column`), then the editor commands under "align by hand, then run
  again:". A line under the table when the picked track id or name differs between
  episodes. Progress and per-episode lines go to stderr as `go`'s do.
- `--only extract` stops after extraction; `--only retime` takes the EN files from
  `s2s` (`RetimeStage.FindEnFiles`) and does not need MKVToolNix. `--dry-run` extracts,
  retimes and deletes **nothing** (`RetimeAsync` deletes files: do not call it): it
  shows the track each episode would use, its JP file (or the problem), and whether its
  EN file and its retime are there and would be kept.
- Exit codes for these stages as Design 8: 0 every episode ready, 3 some not, 1 an error
  before any work (no such folder, no project, no mkv videos), 130 cancelled.
- Tests: a generated season (mkv files made with `MkvTracksTests.Mux` from generated
  subtitles, JP files beside them; `[RequiresMkvToolnixFact]`) through `season` with a
  scripted retimer (`SubsRetimerLauncher.RunnerOverride`): one episode without a JP
  file, one below `--min-match` (editor command printed), the rest retimed; a re-run
  keeps everything; `--force`; `--track`; the second `.en` deleted; `--only retime`;
  `--dry-run` writes and deletes nothing. Parsing and the table without mkvmerge.

#### 3.4b — `subs2srs-cli season`: the cards (spec D, Design 6 and 8, End state)

Lead's facts (after 3.4): `CliRunner.GoAsync` (`CliRunner.cs:114-200`) is one method
from `LoadProject` to `PrintTable`; split it so `go` and `season` share the part from
the episode list on (checks, `AiPrePass`, `SubsProcessor.StartAsync`, the TSV cleanup,
the exit code) and `season` gets back what its AI, Status and Cards columns need,
without printing `go`'s own table. An episode `season` leaves out of `go` is a skipped
`Episode` in the `EpisodeList` (so `SetUpSeasonRun` numbers the others as before:
episode *k* stays *k*), its reason the retime's. `SeasonCommand.Columns` takes the new
columns; its last line becomes `go`'s season TSV line.

- After the retime stage, `go --season` in the same process for the episodes whose
  retime is `Ready` only (the others keep the `.ja` file they may have, an editor fix
  among them, and are shown skipped with their retime reason). `--deck NAME` overrides
  the project's deck name (for `go` too), `--grouping rules|off`, `--yes`, and `--only
  go` (cards alone, from what `s2s` holds, as `go --season` does). One table: Episode,
  EN track, Retime, AI, Status, Cards; the season TSV line; the editor commands; `go`'s
  exit codes (Design 8). `--dry-run` adds the AI cache state.
- The EN files are mkvextract's UTF-8: a season run reads Subs2 as UTF-8 and warns once
  when the project's Subs2 encoding says otherwise.
- Tests: the generated season end to end with a scripted retimer and rules grouping
  (cards for the ready episodes only; exit 3); `--only go`; `--deck`; `--dry-run` with
  the AI cache state.

#### 3.5 — Documentation

- As 2.6 did for phase 2: README, CHANGELOG, `docs/architecture.md`, `docs/testing.md`,
  `docs/open-items.md` in subs2srs; the spec's B, C, D as built; the interim script
  replaced by the one command in the spec's "End state" (keep the script as a fallback
  for a machine without subs2srs-cli, or say it is retired).
- Known stale, collected by the lead from the phase 3 logs (check each against the code;
  the code is right, the docs follow it):
  - subs2srs `docs/architecture.md`: the launcher section (UTF-8 pipes, kill on cancel,
    `MinMatch`/`ReportPath`, `RetimeReport`, `EditorCommand`, "options the launcher does
    not pass yet"); a `season` section beside the `go` one (stages, `SeasonEpisode` rows
    and columns, keep-if-newer, which files each stage deletes and which it never does,
    go taking only Ready episodes, the UTF-8 Subs2 override); MKVToolNix lookup
    (`%ProgramFiles%\MKVToolNix`) and `UtilsCommon.RunToolAsync`.
  - `docs/testing.md`: `SUBSRETIMER_EXE` (the real-tool tests skip unset; a stale build
    fails them), `[RequiresEnvFact]`, `[RequiresPosixShellFact]`, `[RequiresMkvToolnixFact]`,
    the `Fixtures/retime` and `Fixtures/mkv` fixtures, setting file times instead of
    sleeping (`SetBack`).
  - `docs/open-items.md`: deferred item 2 (launcher start info and `[RequiresEnvFact]`
    done); new: go's checks run only after extract and retime (an up-front check would
    be friendlier); keep-if-newer trusts file times (a JP file copied with its old time
    keeps an old retime: `--force`); a changed `--track` needs `--force`; the Windows
    paths of phase 3 (PowerShell quoting of the editor command, case-insensitive names)
    are checked by CI's tests only, not by hand.
  - README: `subs2srs-cli season` (an example run, the table, the editor commands, the
    exit codes), `--deck` on `go`; CHANGELOG: `season`, `go --deck`, the MKVToolNix
    folder fallback (also for the GUI's MKV dialogs), the Retimer dialog: closing it
    stops an auto-align, Japanese saved paths come back intact on Windows.
  - The spec (`/home/user/subsretimer/docs/season-batch-plan.md`): B, C, D and Design 7
    and 9 as built (segments, not cuts; dotted JP tags; no delete on a lookup problem;
    the editor command in PowerShell double quotes with full paths and no `--`); the
    End state table and its last lines as `season` prints them now (run it on a
    generated season to copy real output); phase 0 still sets the `--min-match`
    default; phase 0's steps can use `season --dry-run` and `season --only extract`.

## 5. Log (newest last; 25 lines at most per entry)

Template:

    ### Phase <id> — <date> — <commits>
    Built: ...
    Choices / deviations: ...
    The next phase must know: ...
    Left open: ...

### Phase 2.1 — 2026-10-04 — 4bd733a, 447d195, 2154efc
Built: `ProjectFiles.Resolve()` (GTK-free: patterns into `Files`, cut to End #, `UpdateAudioFilenameFormats`),
called at the end of `SaveSettings`' try; `ProjectFiles.EpisodeLimit`. A UI test written before the move pins
`SaveSettings`' arrays (`PatternSet` harness); `ProjectFilesTests` holds `Resolve` to them. `subs2srs.Cli`
(assembly `subs2srs-cli`, RIDs linux-x64;win-x64 as the app, lock file): `Program`, `CliRunner.RunAsync(args,
stdout, stderr, token)`, `CliOptions`, `EpisodeList` (`ForSeason` pure, `FromSeason`, `FromPatterns`/`Pair`),
`TextTable`. `PrefIO.ReadFile` and `JsonPath` (internal). Tests: `EpisodeListTests`, `CliTests`.
Choices / deviations: `InternalsVisibleTo` must name the assembly, `subs2srs-cli`. Tag match: whole name,
OrdinalIgnoreCase. All skipped is exit 3. `go` without `--dry-run` exits 1 before loading anything. Pattern
mode counts videos only when used (audio from video, snapshots, animated, clips) and audio files when audio
comes from files; `--season` refuses audio from files. Preferences: `PrefIO.read` writes a missing file, so
the CLI reads with `ReadFile` (any file name for `--prefs`). Console: redirected streams get UTF-8 writers;
when either stream is a console, `OutputEncoding` = UTF-8 and the old code page is restored on exit.
The next phase must know: the pipeline reads `Subs[1].FilePattern != ""` (WorkerSrs.cs:259, 344;
WorkerSubs.cs:707) and vobsub checks on both patterns (WorkerSubs.cs:66, 723), so a season run must set the
patterns, not only `Files`. WorkerAudio.cs:67-68 indexes `AudioClips.Files[ep-1]` even with audio from the
video: a leftover audio pattern with fewer files than episodes crashes (GUI too); clear `AudioClips.Files` in
season runs. Season mode must call `UpdateAudioFilenameFormats()` itself (pattern mode gets it from
`Resolve`). The project's Subs2 encoding applies to the `.en` file. `--yes`/`--verbose` are wired, unobserved.
Left open: one Release UI run failed `PreviewGroupingScrollTests` ("leaked MainWindow", which hides the body's
failure); not reproduced in 9 more runs; cause unknown. CI restore and Makefile lines: in the report.

### Lead — 2026-10-04 — after phase 2.1
Reviewed `ProjectFiles`, the `SaveSettings` diff, `EpisodeList` and `PrefIO`; Release unit
575 passed / 4 skipped and the UI suite 5 times green on my own runs. Added the CI
restore line and the Makefile entries (60737f0), pushed the subs2srs branch.
Phase 2.1 used about 330k tokens: too big. I split the old 2.2 into 2.2 (A4) and 2.2b (A5),
and moved 2.1's findings for real runs into 2.3's work order.

### Phase 2.2 — 2026-10-04 — 53117ed
Built: `Settings.EpisodeNumbers` (`int[]?`, `[JsonIgnore]`, nulled by `Reset` and `RestoreFrom`, so
`ProjectIO.Load` and the Preview's snapshot/restore clear it like `Files`) and `Settings.EpisodeNumber(index)`
(0-based; the list when set, an index outside it throws `ArgumentOutOfRangeException`; else `index + start`).
Every site of section 3 uses it: WorkerSrs (16), WorkerAudio (3), WorkerSnapshot, WorkerAnimatedSnapshot,
WorkerVideo (2) as `EpisodeNumber(epNum - 1)`, WorkerSubs (7), DialogPreview (combo, validation files),
DialogDuelingSubtitles (4). `Logger.writeSettingsToLog` logs `episodeNumbers` ("" when null).
`grep -n EpisodeStartNumber subs2srs/*.cs` now finds only the property, widget copies, the helper and the log.
Tests: `SubsProcessorE2ETests` theory (numbers {1, 3} with start 7; null with start 5 gives 5, 6): tags,
sequence markers, and audio, snapshot, video clip and (encoder present) webp names, and no other file in
`.media`; a time-shift rule from 3 shifts only the second episode. Unit tests in `SettingsSnapshotTests`,
`ProjectIOTests`. Seen failing: the helper ignoring the list; WorkerSnapshot given `epNum` (1-based).
Choices / deviations: the parser's episode reaches only `SubsParserVOBSUB`'s bitmap names (compiled out by
default), not `InfoLine`/`InfoCombined`. The "episode k of n" AI progress labels are positions; left alone.
The next phase must know: `EpisodeNumbers.Length` must equal the `Files` count, or the run throws at the
first site past the list. `${0:episode_num}` is zero-padded to the digits of the number of episodes in the run
(`UtilsName`), not of the largest number: a season run of 9 episodes names episode 10 `10` and episode 3 `3`,
a later run of 10 names it `03`, so tags and media names change between runs and existing media is not reused.
Today's GUI does the same with a start number; whether `go` should pad by the largest number is for the lead.
Left open: the time-shift test covers Subs1 only; the Subs2 site is the same one-line change, untested.

### Lead — 2026-10-04 — after phase 2.2
Reviewed `Settings.EpisodeNumber` and the worker sites; Release unit 583 / 4 skipped and UI
22 on my own run. subs2srs CI (Linux and Windows) green on 60737f0; pushed 53117ed.
Decided the padding finding: `Settings.EpisodeCountForNames`, set by `go` in season mode
to the season's video count, so names do not change with skips; added to 2.2b.

### Phase 2.2b — 2026-10-04 — b912688
Built: `PipelineResult` (`Status` Completed/Cancelled/Failed, one-line `Message`, `CardsPerEpisode` by index), returned
by `SubsProcessor.StartAsync`. A step returning null/false now throws a private `StepStoppedException`; Cancelled iff
the reporter's `Cancel` or token is set (also when a step threw after the cancel), else Failed with "<progress label>
failed: <detail>": `WorkerAudio.Error`, the exception's message, or an `AggregateException`'s first inner one. An
uncreatable output dir is Failed too. `Settings.EpisodeCountForNames` (`int?`, `[JsonIgnore]`, nulled by `Reset` and
`RestoreFrom`, logged) and `Settings.EpisodeCountForPadding(runEpisodes)`, used by the five worker `UtilsName`s (Srs,
Audio, Snapshot, AnimatedSnapshot, Video; their `totalEpisodes` is `CombinedAll.Count`, still used for progress text).
Choices / deviations: the dialogs are unchanged, so the GUI still reads "Action cancelled." when a step returns false
without a cancel (one line in `StartAsync`'s catch if the lead wants it changed); `GoAsync` ignores the result. A card
is a line left after "Remove inactive lines" that is not `OnlyNeededForContext`: with context on, the list also holds
context-only lines with no TSV line, so "the list length" would overcount. `UtilsSubs.extractVobsubFilesFromText` and
`SubsParserVOBSUB` build `UtilsName` with count 0 for VobSub prefix/suffix only: left alone, as are the GUI dialogs.
Found: section 3's "every worker failure becomes `OperationCanceledException`" holds only for null/false returns. WorkerSubs
steps return null only on cancel (parsers throw); WorkerSrs, Snapshot, Animated and Video return false only on cancel and
throw on failure (`UtilsCommon.startFFmpeg` throws "ffmpeg exited with code N: <last stderr line>"); WorkerAudio returns
false on cancel, after its own error dialog (empty demux/decode output: ffmpeg exited 0, not reached in tests), and when
the mp3 it cuts from does not exist (its "cancellation race" branch: no message, so "it stopped without an error message").
Tests (`SubsProcessorE2ETests`): `CompletedRun_CountsTheCardsOfEachEpisode` ([3, 1], checked per TSV tag),
`FailingWorker_GivesFailed_WithTheStepAndItsMessage` (missing mp3; a text file as video, with audio and snapshots only),
`CancelWhileAWorkerRuns_GivesCancelled_NotFailed`, `UnwritableOutputDir_GivesFailed`; the 2.2 theory gained count 10 with
{3, 4} (`03`, `04`) and null (`3`, `4`); units in `SettingsSnapshotTests`, `ProjectIOTests`. Seen failing: status by
exception type (missing mp3 read Cancelled), never Cancelled, the helper ignoring the count, cards by list length.
The next phase must know: map `CardsPerEpisode[i]` through `Settings.EpisodeNumber(i)`; set `EpisodeCountForNames` after
loading the project (`ProjectIO.Load` clears it). Unit 594 passed / 4 skipped, Debug and Release; no UI file touched.

### Lead — 2026-10-04 — after phase 2.2b
Reviewed `SubsProcessor` (CRLF kept) and `PipelineResult`; Release unit 594 / 4 skipped and
UI 22 on my own run; pushed b912688. Split the old 2.3 into 2.3 (A3 checks, shared with
`GoAsync`) and 2.3b (`go` runs, table, exit codes), with 2.1's and 2.2b's setup findings
in 2.3b. Open for the docs phase: the GUI still says "Action cancelled." when a step
failed; the result knows better, the dialog was kept by the spec.

### Phase 2.3 — 2026-10-04 — 3372ae2
Built: `GoChecks.Run(settings, audioStreamIndex, aiGroupingRuns)` (`subs2srs/GoChecks.cs`) → every `GoProblem` (Error or
Warning, message) in order: output dir (blank: the GUI's "Please provide Output Directory."; else created if missing, a file
written and deleted, the folders it created removed: `Cannot write to output directory "<dir>": <reason>`), deck name (the
GUI's message), ffmpeg (`FFmpegMissingMessage`), else the animated encoder when on (`MissingEncoderHint`), `claude` for a
`terminal-` model when `aiGroupingRuns` (new `ClaudeCliProvider.ResolveExecutable()`, which the constructor now uses; blank =
none, as `AiGrouper` decides), then the audio-stream warning (same condition and text as before). `GoChecks.AudioStreamIndex`
= `AudioStream.DisplayNum` (the GUI list's position), else 0. `GoAsync` keeps its three widget checks and messages, then after
`SaveSettings` runs the checks in `Task.Run` with Go disabled meanwhile (the await is now unconditional: no double Go);
errors in one `showErrMsg`, each warning `showConfirm`ed; `aiGroupingRuns` = `aiGroupingOnGoApplies(previewVars ?? new)`.
`go --dry-run`: the checks on **stderr** after the table (`error: `/`warning: `, further lines indented, "all passed" when
none, and what go does at a warning with or without `--yes`); an error exits 1, a warning keeps 0/3. Season mode sets
`VideoClips.Files` to the ready episodes' videos first (the only piece of 2.3b's setup built); `aiGroupingRuns` = mode AI.
Tests: `GoChecksTests` (11), `CliTests` +2 (errors under the table and exit 1, `claude` in AI mode with the On Go preference
off; an audio warning with and without `--yes`), UI `Go_WithAnOutputDirUnderAFile_SaysWhy_AndDoesNotStart`. Seen failing:
the output-dir check off (2 GoChecks, the CLI and the UI test: the run started, bare "Cannot write to output directory."), the
CLI passing the On Go preference instead of the mode. Unit 607 / 4 skipped, UI 23, Debug and Release.
Choices / deviations: stderr, not stdout, so stdout stays the table (the usage says so; existing tests parse it). The dry-run
CliTests got `FakeFfmpeg` (an empty `ffmpeg` in the Tools Directory; on PATH for the real process) so they need no ffmpeg.
`claude` missing is an error in the GUI too: Go used to group every uncached episode by the rules silently; it now refuses
with the reason, only when AI runs on Go. A configured Claude CLI Path is not checked on disk. No "exists but unwritable"
test: the container runs as root; a file, and a path under one, cover it.
The next phase must know: call `GoChecks.Run` after the season setup and `--grouping`, with `GoChecks.AudioStreamIndex`, and
send a warning through `UtilsMsg.showConfirm` (answers `--yes`). `validateAudioStreamConsistency` numbers episodes by position
(i + 1), not `EpisodeNumber`, and prints "Reference (episode -1)" when no video has the stream; left alone.

### Lead — 2026-10-04 — after phase 2.3
Reviewed `GoChecks` and the `GoAsync` diff; Release unit 607 / 4 skipped and UI 23 (twice)
on my own runs; pushed 3372ae2. Kept the agent's choices: checks on stderr; the GUI now
refuses Go when AI grouping would run on Go and `claude` is missing (it used to fall
back to the rules without a word). Both go in the docs phase's CHANGELOG.

### Phase 2.3b — 2026-10-04 — 6a60ac4, 08f7e99
Built: `CliRunner.GoAsync` runs `go`: load; `--grouping rules|off` (`CliOptions.Grouping`) over the mode, else AI mode refused
(exit 1, right after `LoadProject`); the list; `SetUpSeasonRun` (ready episodes' `Files`; `Subs[0/1].FilePattern` = the first
ready episode's own file, no wildcard; `AudioClips` cleared; `EpisodeNumbers`; `EpisodeCountForNames` = `list.Episodes.Count`;
`UpdateAudioFilenameFormats`; dry runs get it too); none ready: table, exit 3, no run; `GoChecks` (errors listed, exit 1; each
warning through `UtilsMsg.showConfirm`, so `--yes`); `StartAsync` with `ConsoleProgress` (new file; step labels as lines, `\r`
only on a terminal). Stdout: `#`, `Episode` (video name), `Status` (done / skipped: why / failed / cancelled), `Cards`
(`CardsPerEpisode[i]` for the i-th ready episode), then `season TSV: <path> (k of n episodes); exit N` (`TSV:` in pattern
mode, "not written" when none). `PipelineResult.ImportFile` from `WorkerSrs.ImportFile` (set once the writer opened).
Choices / deviations: a Failed or Cancelled run that opened the TSV deletes it and says so (its cards lack media; media stay; an
older TSV is untouched when the run stopped earlier). Failed adds `subs2srs-cli: <Message>` after UtilsMsg's own "ERROR: Error:
..." with its stack (or "Action cancelled." for the audio worker's false); UtilsMsg's INFO line is the success line. Redirected
progress prints a text only at a new tenth of its percentage (or with none): the workers report every line and clip. A dry run
in AI mode without `--grouping` still lists and checks `claude`; only a real go refuses. The padding test has ten videos (4-10
empty, no subs): with three, padding for 3 and for the 2 that ran are both one digit, so it could not fail.
Tests (`CliTests` +7, the "not built yet" one replaced): season exit 3 (tags and media `Show_01`/`Show_03` only, rows, TSV line),
patterns exit 0 with an AI project, On Go on and `--grouping rules` (fake provider never asked), failing audio step (exit 1,
message, TSV deleted), none ready, failed check, warning without `--yes`, AI refused. Seen failing: no `EpisodeCountForNames`,
`--grouping` not applied (the fake was asked), cards mapped by list position. Unit 614 / 4 skipped, Debug and Release; no UI
file touched. Built exe, SIGINT: before the run 130 "cancelled."; during it 130, table with `cancelled`, TSV deleted.
The next phase must know: 2.4 replaces the AI refusal with the pre-pass and passes `combinedAll`/`joins` to `StartAsync`.
Left open: on a terminal UtilsMsg's INFO line lands after the last progress text on its line (it writes before any hook runs).

### Lead — 2026-10-04 — after phase 2.3b
Reviewed `SubsProcessor`/`WorkerSrs` (`ImportFile`) and the CLI's run; Release unit 614 /
4 skipped and UI 23 on my own run; pushed 08f7e99. The Windows UI hang on b912688 did
not recur on 3372ae2 (all green); recorded in section 3. Wrote 2.4 out in full. For the
docs phase: Design 8 should list a failed step under exit 1; the decimal comma in
"Processing completed in 0,02 minutes" is the pipeline's old `String.Format`.

### Phase 2.4 — 2026-10-04 — e8f14b4
Built: `subs2srs.Cli/AiPrePass.cs`. `FirstSteps` = `combineAllSubs` + `inactivateLines` on a `WorkerVars` like StartAsync's
(`SubsProcessor.getMediaDir` now `internal static`), the "no lines" check, a failure → `CliException` "<step> failed: ..."
(exit 1, no table). `Run`: per episode a stderr line `AI grouping: episode N (k of m)`, `AiGrouper.Group` with
`FromSettings()` and the `ConsoleProgress` (chunk progress under it); outcome `cached`/`grouped`/`k/n by rules`/`usage
limit`/`failed` (`AiOutcome`). Limit: `UsageLimit` set → `Estimate().Cached` decides: not cached = skipped, no call; set
after `Group` with failed chunks, or with a `ProviderException` → skipped. `Report`: a warning per partial episode, one
line with the CLI's limit words. `DropSkipped`/`KeepEpisodes`: Subs1/Subs2/video/audio `Files` and `EpisodeNumbers`
(made explicit in pattern mode; `EpisodeCountForNames ??=` the count); a list of another length is left alone. `GoAsync`:
the refusal is gone; checks → pre-pass → all skipped: table, exit 3 → `StartAsync(progress, combinedAll, joins)`.
Tables: `#, Episode, AI, Status, Cards` and dry run `#, Video, Subs1, Subs2, [Audio], AI, Status`; usage text.
Choices / deviations: after the limit a **cached** episode still gets its cards (no request is made for it); design 6
says "every remaining episode is skipped", the lead may want the letter. The duplicate-lines table spans every ready
episode, also ones the AI step then drops: re-inactivating after the drop would change the lines the model grouped
and the cache keys. A dry run whose subtitles do not parse shows `unknown`, says why, keeps its exit code. Cancel during
the pre-pass: exit 130 without a table (nothing written).
Tests: `CliAiPrePassTests` (8; 1 ffmpeg): all grouped (pipeline starts at "Group into snippets", On Go on), cached runs
and dry runs; partial chunk failure kept + warning, asked again next run; all skipped; no lines → exit 1 / `unknown`;
usage limit theory (one chunk answered / none) + second run; episode 2 dropped (its "video" a text file) with 3's text
and media; `KeepEpisodes` unit. CliTests' tables gained the column; the AI-refused test removed. Seen failing: video
list not dropped (run exit 1), the "limit during an episode with failed chunks" branch off (`1/2 by rules`), the limit
ignored in the catch (`failed`). Unit 621 passed / 4 skipped, Debug and Release; no UI file touched.
Left open: VobSub subtitles in AI mode are untested (the pre-pass's WorkerVars points at the media dir, as Go's does).

### Lead — 2026-10-04 — after phase 2.4
Reviewed `AiPrePass` (the usage-limit paths) and its tests; Release unit 621 / 4 skipped on
my own run; pushed e8f14b4. Kept the agent's choices: a cached episode after the limit
still gets cards (it needs no request); *Remove duplicate lines* spans every ready episode
before the drop (re-inactivating would change the cache keys). Phase 2.5 may edit the
Makefile, `dist/` and `release.yml` (not `ci.yml`).

### Phase 2.5 — 2026-10-04 — f0705c1, 3b373a1
Built: `make build` also publishes `subs2srs.Cli` (framework-dependent, its own default publish dir); `make install`
copies it into `$(LIBDIR)` after the app's files and installs `dist/subs2srs-cli.sh` as `$(BINDIR)/subs2srs-cli`;
`uninstall` removes it. `publish-windows` and `release.yml` publish `subs2srs-cli.exe` self-contained into
`out\win-x64` after `subs2srs.exe` (same `-p:Version`; the CLI's `--version` is subs2srs.dll's). `smoke.ps1` runs
`--version` and `go --help` (Start-Process, streams in temp files, read as UTF-8, MSYS2 off PATH): exit 0 and an
expected stdout line. `release.yml`: `workflow_dispatch` (version `0.0.0-dev` off a tag), attach only on a tag, the zip
checked for both exes, `$PSNativeCommandUseErrorActionPreference` in the publish step (only the last command's exit code
failed it before, so a failing `--locked-mode` restore went unnoticed). `bundle-gtk.ps1`: comments only.
Checked: publishing both into one folder (clean tree each time, both orders) leaves all 206 win-x64 self-contained / 19
linux files of an app-only publish byte-identical, `subs2srs.exe`/`.deps.json`/`.runtimeconfig.json` included (the CLI
publish copies the referenced app's); `subs2srs.exe` stays GUI subsystem, `subs2srs-cli.exe` is console. Installed to
a stage: the CLI's `--help`/`--version` exit 0, the GUI ran 12 s under Xvfb and wrote its preferences, a dry run through
the wrapper with relative `--project`/`--season` works and opens no libgtk. Unit 621 passed / 4 skipped (Release).
Found: "preferences are found by an absolute path" holds only when `~/.config` exists: `Environment.GetFolderPath`
returns "" for a missing folder, so `SettingsFilename`, `LogDir`, `AiCacheDirFull` and `ClaudeCli.WorkDir` become
relative and resolve under the cwd (seen: `subs2srs/Logs/log-*.txt` in the project folder; the GUI's launcher hides it
with its `mkdir` and `cd`). The CLI wrapper therefore creates `~/.config` and `~/.local/share` (no cd). Running
`/usr/lib/subs2srs/subs2srs-cli` directly still has it; the app fix is `SpecialFolderOption.DoNotVerify`, for the lead.
Left open: the Windows zip and smoke are unrun (no Windows here); the CLI exe has no `app.manifest` (no long-path or
UTF-8 code page manifest) and no icon; each `go` leaves an empty `log-*.txt` in the Logs folder (logging off).

### Lead — 2026-10-04 — after phase 2.5
Reviewed the Makefile, wrapper and `release.yml` diffs; pushed 3b373a1 and started
`release.yml` by hand on the branch to build and smoke-test the zip on Windows. Open
for the docs phase (`docs/open-items.md`): `Environment.GetFolderPath` returns "" when
`~/.config` or `~/.local/share` is missing, so preferences, logs and the AI cache then
resolve under the working directory (the wrapper creates them; running the binary
directly does not); every `go` leaves an empty `log-*.txt`; the CLI has no app manifest
or icon.

### Phase 2.6 — 2026-10-04 — 055e9bd
Built (docs only): README "Command line" (usage, the options block copied from the real `--help` and diffed
against it, season layout, checks, AI pre-pass and usage limit, an example table in `TextTable`'s format, exit
codes, preferences, install); CHANGELOG Unreleased (Command line, Go, Tests, Build/CI); AGENTS.md layout row and
run line for `subs2srs.Cli`; architecture.md "The command line" (bootstrap, season setup and why, `GoChecks`,
the pre-pass hand-off, `PipelineResult`) and the launcher contract (editor exists: exit 0/2; newer options);
testing.md CLI tests; gtk-and-windows.md packaging, smoke, `release.yml` by hand; open-items.md the items
below. Spec: Status, the Cards row of "What works today", design 6 (a cached episode after the limit runs),
design 8 (exit 1 for a failed step, TSV deleted; 130 too), section A as built, the interim script's prose.
Checked by running: `--help`/`--version`; a dry run on a demo season (its audio warning said "episodes: 1, 2"
for episodes 1 and 3: `validateAudioStreamConsistency` numbers by position); with no `~/.config` the CLI read
`subs2srs/preferences.json` and wrote `subs2srs/Logs/log-*.txt` under the cwd; each run's log file is 3 bytes
(a BOM), made because `RunAsync` reads `Logger.Instance.Echo` before turning logging off.
Choices / deviations, beyond the work order: README's Subs Re-Timer paragraph and open-items' deferred subsretimer
item 1 said the tool's editor was not ported; fixed (item removed, spec E's reference renumbered).
gtk-and-windows.md said the tool has no Windows build; fixed. open-items also lists the one-off
`PreviewGroupingScrollTests` "leaked MainWindow" failure next to the Windows hang.
Script: its `go` call matches the real options and exit codes; prose added for what it did not say (no `--yes`,
so an audio warning stops it with exit 1; the project's encodings; `[Console]::OutputEncoding` when captured;
how to get `subs2srs-cli.exe` before a tag). Not run: no PowerShell here. Code block byte-identical.
Left open: tests.md lacks rows for older files (`SubsProcessorE2ETests`, `InfoStreamTests`, `PrefDefaultTests`,
`SnapshotsTests`, UI `MainWindowFlowTests`, `DialogPrefFlowTests`, `WindowSmokeTests`); every new file has one.
AGENTS.md says `subs2srs.Eval` is the only place outside the app that calls a real provider; `go` does too.
Unit 621 passed / 4 skipped after the docs (unchanged).

### Lead — 2026-10-04 — after phase 2.6 (phase 2 complete)
Reviewed the README's CLI section against `--help`; added one line to AGENTS.md (the CLI
may call a provider too; 3f31e62); pushed both repos. `release.yml` by hand on the branch
(run 37227831045) was green: the CLI's `--version` and `go --help` passed the Windows
smoke test, the zip holds both exes (62.5 MiB). Wrote the phase 3 work orders (3.1-3.5)
with the facts checked in subs2srs at 3f31e62.

### Phase 3.1 — 2026-10-04 — bf4b5de
Built: `subs2srs/MkvTracks.cs`: `MkvTrackInfo` (Id, Type, Codec, CodecId, Language, LanguageIetf, Name, Forced,
Default, `Events` = `num_index_entries`, null if absent; `IsText`/`IsImage`/`IsEnglish`; `Label` `3 "English" 312 ev`),
`Parse(json)` → `MkvTrackList` (Tracks or Error), `Pick(tracks, trackId?)` → `MkvTrackPick` (Track or Reason),
`ListAsync`/`PickAsync(file, trackId?, ct)` (never throw but on cancel), `RunnerOverride` (gets the `ProcessStartInfo`).
`ConstantSettings.ExeMkvMerge`/`PathMkvMergeExeFull`; `ResolveTool` then `MkvToolNixDirs(isWindows, getEnv)`
for mkvmerge/mkvextract/mkvinfo only, `MkvToolNixDirsOverride` for tests. Tests `MkvTracksTests` (44), fixtures
`subs2srs.Tests/Fixtures/mkv` (two recorded verbatim with mkvmerge v82.0, relative `file_name`, so no paths; one
hand-written), `[RequiresMkvToolnixFact]`. Seen failing when broken: forced not ignored, no install-folder fallback,
no LC_ALL fix (the real test and the start-info test).
Choices: WebVTT does not count (neither tool reads it). Missing or 0 count = 0, a tie goes to the lower id (mkvmerge
82 writes `num_index_entries: 0`, not nothing, for a file without cues, and no `NUMBER_OF_FRAMES` tags any more).
The override takes any ASS/SSA/SRT track, `jpn` and forced included (how a mistagged track is used). Exit code 1
(warnings) is read as success, 2+ fails (deviation from "non-zero exit"). `enm` is not English. Reasons:
`no subtitle track`, `only image subtitle tracks (PGS/VobSub), which need OCR`, `no English text subtitle track
(2: image S_HDMV/PGS, 4: forced)`, `mkvmerge exited with code 2: The file '...' could not be opened ...`.
Found: under the C/POSIX locale (this container, `LANG` unset) mkvmerge 82 drops a non-ASCII file argument (exit 2,
"The file ''") or cannot open it, and cuts `-J` output at the first non-ASCII character (invalid JSON).
`MkvTracks.StartInfo` sets `LC_ALL=C.UTF-8` when LC_ALL/LC_CTYPE/LANG is not UTF-8 and passes `--output-charset UTF-8`.
The next phase must know: use `MkvTracks.StartInfo(exe, args)` for mkvextract too (same locale problem), and a full
path (a name starting with `@` is an option file). `CliProcessResult` is reused as the runner's result.
CI: the new real test skips without `mkvtoolnix` (apt) installed; the rest run anywhere (fake `mkvmerge` in a Tools Dir).
Left open: `--output-charset UTF-8` on Windows is unverified (no Windows here). Unit 665 passed / 4 skipped (Debug, Release).

### Lead — 2026-10-04 — after phase 3.1
Reviewed `MkvTracks` and the `ResolveTool` change; Release unit 665 / 4 skipped on my own
run; pushed bf4b5de. Added MKVToolNix to both CI jobs (apt; Chocolatey into Program
Files) so the real-mkvmerge tests run there (d7911ff). Kept the agent's choices: exit 1
from mkvmerge is warnings; `--track` accepts any text track, Japanese or forced too;
WebVTT is not a text track for us. The locale finding goes into 3.2's work order.

### Phase 3.2 — 2026-10-04 — 2f77756
Built: `subs2srs/MkvExtract.cs`: `Extension(codecId)` (`ass`/`ssa`/`srt`, else null), `OutputPath(season, video, track)`
→ `<season>/s2s/<video name>.en.<ext>` (throws for a non-text track), `ExtractAsync(mkv, trackId, output, ct)` →
`MkvExtractResult` (`Status` Extracted/Kept/Failed, full `OutputPath`, `Reason`), `RunnerOverride`, `NotFoundMessage`.
Order: an existing non-empty output is Kept (before the tool lookup: a re-run needs no mkvextract); tool; the mkv exists
(else "no such file: <path>"); the folder created; `mkvextract --output-charset UTF-8 <mkv> tracks <id>:<out>` through
`MkvTracks.StartInfo`, full paths. Exit 0/1 with a non-empty output is Extracted; exit 2+ (`mkvextract exited with code 2:
<its Error: line>`, else the last stderr/stdout line), no exit code, not starting, or no/empty output fail and delete the
file; a cancel deletes it and throws. `MkvTracks.RunAsync` is now internal and shared, and waits up to 5 s for the killed
tool to exit (Windows: the file is closed before the delete). `[RequiresMkvToolnixFact]` needs mkvextract too.
Tests `MkvExtractTests` (29, 2 real): seen failing when broken: no delete on exit 2 or on cancel, no LC_ALL fix (real
test: "Unknown mode"), the kept check off (bytes and mtime changed, the tool ran).
Found (mkvextract 82, Linux): ASS and SRT tracks are written as UTF-8 **with a BOM** (an ASS muxed without one gets one),
LF line endings. Messages go to **stdout** (`Error: ...`, `Warning: ...`, `Progress: n%` ended by `\r`); stderr is
empty. A bad id: exit 2 `No track with the ID 9 was found in the source file.`, no file. A missing mkv, and a Japanese
path under the C locale, both give exit 2 `Unknown mode '<path>'` (it falls back to the old argument order). It makes
missing output folders itself and overwrites an existing output.
Choices: an empty existing output is extracted again (no extraction leaves one). The call takes a track id, not the
`MkvTrackInfo`. `MkvExtract.SubsFolder`/`EnTag` repeat `EpisodeList.SubsFolder`/`Subs2Tag` (the app cannot see the CLI);
a test pins that `OutputPath`'s name is the Subs2 `EpisodeList.ForSeason` takes. MkvTracksTests' `Mux`/`Srt`/`Ass` are
internal for reuse.
The next phase must know: "kept" is the exact path only: an `.en.srt` from an earlier run next to a new `.en.ass` (another
track picked) gives two `.en` files and `go` skips the episode ("2 .en files"); 3.4 should decide (keep any one `.en.*`?).
Reasons from mkvextract hold full paths. Left open: Windows unchecked (line endings, `--output-charset`); the real cancel
(kill, wait, delete) is tested only through the scripted runner. Unit 694 passed / 4 skipped (Debug, Release); no UI file.

### Lead — 2026-10-04 — after phase 3.2
Reviewed `MkvExtract`; Release unit 694 / 4 skipped on my own run; pushed 2f77756. CI
with MKVToolNix: Linux green; Windows failed one test in the helper `Mux`
(`--command-line-charset` is not an option there), fixed in a53049d; both jobs green
on it (runs 22 and 23).

### Phase 3.3 — 2026-10-04 — 152bfb7
Built: `SubsRetimerLauncher`: `StartInfo(exe, request)` through `makeToolStartInfo` (UTF-8 pipes, no window, `ArgumentList`);
`RunAsync` runs `RunnerOverride ?? UtilsCommon.RunToolAsync` (the MKVToolNix loop moved out of `MkvTracks` and shared; both
MKV classes call it): a cancel (also one before the start) kills the tool with its children, waits up to 5 s and throws
`OperationCanceledException`; a runner without exit code is a failure. `Request` gains `MinMatch` (`double?`, invariant, passed
whenever set, 0 too) and `ReportPath`. `EditorCommand(exe, request[, windows])`: `[--ref-encoding R` unless utf-8`]
--target-encoding T [--output O] REF TARGET`, no `--`; POSIX single quotes (`'\''`); Windows PowerShell double quotes, a
backtick before `` ` ``, `$`, `"` and the typographic double quotes, `& ` before a quoted exe; ASCII words of letters, digits,
`-_./:=+` (and `\` on Windows) stay bare. `subs2srs/RetimeReport.cs`: `Read(path)` → `ExitCode`, `Saved`, `Segments` (count),
`ReferenceCoverage` (share or null), `Reason` (`BelowMinMatch`, `NoTimedLines`); null when missing, not JSON, version ≠ 1 or
a field of the wrong type. `DialogSubsRetimer`: closing it kills an auto-align; an open editor is left running (`WaitAsync`,
only the waiting stops, as before: killing it would lose unsaved work).
Tests `SubsRetimerLauncherTests` (53, 3 env-gated); fixtures `Fixtures/retime` written by subsretimer at 5d8475e (its temp
folder replaced by `/home/user/Anime`); a real sleeping child killed (`sh`; `powershell` on Windows, never run); the POSIX
command run through `/bin/sh` (new `[RequiresPosixShellFact]`). Seen failing when broken: the old start info (no UTF-8),
`MinMatch` in the current culture (the real tool too: "not '0,8'"), no kill in the loop ("the child still runs").
Found: this container sets `SUBSRETIMER_EXE=/opt/subsretimer/subsretimer`, a 2026-09-28 build without `--min-match` and
`--report`, so the old real-tool tests always ran here. With it the new real test fails ("Unknown option: --min-match"): run
the suite with `SUBSRETIMER_EXE` = a current build (as I did) or unset. The two old `RealTool_*` tests now use
`[RequiresEnvFact]` (open item), so unset gives 7 skips instead of 4.
Choices: a cancel throws (as `MkvExtract`), no Cancelled result. No `--` in the editor command (spec's Design 9 has none;
PowerShell may drop it), so 3.3b must pass full paths. Windows quoting follows the work order (double quotes), not the
End state's single quotes; it is checked as strings only (no PowerShell here). 3.3b picks the exe name to print.
Docs stale for 3.5: architecture.md's launcher section, testing.md ("return" when unset), open-items deferred item 2.
Unit 734 passed / 4 skipped (Debug, Release; `SUBSRETIMER_EXE` = fresh build); UI 23 passed.

### Lead — 2026-10-04 — after phase 3.3
Reviewed the launcher, `RetimeReport`, `RunToolAsync` and the dialog change; Release unit
734 / 4 skipped (fresh `SUBSRETIMER_EXE`) and 731 / 7 skipped (unset, as CI); UI 23
passed. Merged `main` (ec77996, CI triggers) as the user asked: 95d3dfe, pushed. Kept
the agent's choices. For 3.3b: print the exe in the editor command as `subsretimer`
when PATH finds that same file, else its full path. The Windows branch of the
real-child cancel test runs first on CI.

### Phase 3.3b — 2026-10-04 — 3ac375a
Built: `subs2srs.Cli/RetimeStage.cs`. `FindJpFiles(season, videos[, names])` (C1, pure core): `<name>.<ext>` or `<name>.<tag>.<ext>`
(ass/ssa/srt, ignoring case; the tag one word, no dot, not `en`/`eng`), compared as names; a file that is also a longer video's JP
file is that one's (`Movie.Extended.srt`); `no JP file named like the video` / `2 JP files (a.srt, a.ja.srt)`. `FindEnFiles(season,
videos[, s2sNames])` for `--only retime`, through `EpisodeList.Named`/`Problem` (now internal): go's reasons. `FoundFile(Path,
Problem)`; `RetimeOptions(Exe, TargetEncoding, MinMatch, Force)`, `FromSettings` = `ResolveExe()` + `Subs[0].Encoding`.
`RetimeAsync(season, video, jp, en, options, ct)` → `RetimeOutcome` (`Kind`, `OutputPath`, `Report`, `Reason`, `EditorCommand`,
`Ready`, `Column`). No JP or EN: the episode's `.ja` files deleted. Kept: output non-empty, written after EN and JP, no `Force` (no
tool needed), the other `.ja` deleted. Else `.ja` files and report deleted, exe null fails, then the launcher: `--auto --output
s2s/<name>.ja.<JP ext, lower case> [--min-match] --report s2s/<name>.retime.json`, ref utf-8, target the Subs1 encoding, full paths.
Not saved: the output deleted; exit 2 → kind by the report's reason (`NotSaved` without one) + `EditorCommand(CommandName(exe), ..)`;
other codes → `Failed` with subsretimer's first `subsretimer: ` line (else its first line; the code unless 1). A cancel deletes the
output and report and rethrows. So after the stage an episode has a `.ja` file exactly when `Ready`. `CommandName`: `subsretimer`
when `ConstantSettings.FindInPath` gives the same full path. Columns: `kept`, `2 cuts, 97% of EN covered`, `below --min-match
(41%)`, `no timed lines`, `not saved`, `failed: <message>`, the JP/EN reason; percentages floored as subsretimer prints them.
Tests `RetimeStageTests` (41, 1 env-gated: the real tool on two episodes, one retimed then kept, one below 0.8 with its command; the
names `go --season` takes). Seen failing: keep-if-newer ignoring the JP time; no sibling/stale delete (7); a plain prefix match (4).
Choices: a tag is one word (`.ja.cc.srt` is not a JP file; the interim script took any rest). Only names go reads are deleted
(`<name>.ja.<ass|ssa|srt>`; a `.ja.srt.bak` stays). No JP/EN file deletes even a newer `.ja` (an editor fix for a JP file that is
gone or now has a second candidate): otherwise go makes cards the table calls skipped. Segments are printed as cuts, as the work
order's example has it (a one-offset pair reads `1 cut`). The stage prints nothing: 3.4 owns the loop and its stderr lines.
The next phase must know: give `FindJpFiles` every video of the folder; resolve the options once (a missing subsretimer fails only
episodes not kept); a failed extraction is `FoundFile.Missing(..)`; `--dry-run` must not call `RetimeAsync` (it deletes). Keep-if-
newer trusts mtimes: a JP file copied with its old time (Explorer keeps it) leaves the old retime kept; `--force` redoes it.
Unit 775 passed / 4 skipped (Debug, Release; `SUBSRETIMER_EXE` = fresh build), 771 / 8 skipped unset (Release). No UI file touched.
After the lead's review, 75c7246: no JP or no EN file (none, or a second candidate) deletes nothing, so an editor fix survives a
lookup problem; not `Ready`, so season gives go only Ready episodes. A tag is one or more non-empty dot-separated words (`ja.cc`),
English when any word is `en`/`eng`. The column says `2 segments, 97% of EN covered` (`RetimeReport.Segments` doc fixed too).
Unit 782 / 4 skipped (Debug, Release; fresh `SUBSRETIMER_EXE`), 778 / 8 unset (Release).

### Lead — 2026-10-04 — after phase 3.3b
Reviewed `RetimeStage`; asked for the three changes above (no delete on a lookup
problem, "segments", dotted tags), reviewed 75c7246. Release unit 782 / 4 skipped
(fresh `SUBSRETIMER_EXE`), 778 / 8 unset; pushed 3ac375a and 75c7246. CI on 95d3dfe
green on both jobs, the Windows real-child cancel test included. Split 3.4 into 3.4
(the command, extract and retime) and 3.4b (`go` inside `season`), with the open points
decided in their work orders.

### Phase 3.4 — 2026-10-04 — dd759c0
Built: `subs2srs.Cli/SeasonCommand.cs`: `SeasonEpisode` (one mutable row per episode: `Video`, `Pick`, `Extraction`, `En`, `Jp`,
`Retime`, dry-run `EnPlan`/`RetimePlan`, `Cancelled`), `SeasonColumn(Header, Cell)`, `SeasonCommand.RunAsync` (load, videos via
the new `EpisodeList.SeasonVideos` cut at Episode End #, MKVToolNix required up front unless `--only retime`, JP files from every
video, retime options once, then per episode pick → delete the episode's other `.en.*` (with `--force` its own too) → extract →
retime), `Columns(options)`, `PrintReport` (table, track note, `retimed JP files: k of n episodes; exit N` or `EN files: ...`,
"align by hand, then run again:" + commands), `TrackNote`. `CliOptions`: `season <dir>` positional, `--track`, `--min-match` (0-1,
invariant, as subsretimer), `--force`, `--only extract|retime` (`SeasonStage`), usage text; season-only options refused for go and
`--season`/`--grouping` for season. `CliRunner.LoadProject` internal; `RetimeStage.WouldKeep` (shared by the run and the dry run),
`Delete`, `PathComparison` internal. `MkvTrackList.ContainerType`; `PickAsync` refuses a non-Matroska container (an MP4 named .mkv:
mkvmerge lists its tracks, mkvextract fails "no EBML head").
Tests `CliSeasonTests` (26; 4 real MKVToolNix), `MkvTracksTests` +1. Seen failing when broken: other `.en` not deleted (`--track`
test), MKVToolNix required for `--only retime`, the container check. Also run by hand on a real muxed season with the real subsretimer
(retimed, below --min-match with its command, kept on the re-run).
Choices: EN is extracted even without a JP file (as `--only extract` would; cheap; the next run needs it). No pick (no track, mkvmerge
error) deletes no `.en` file. The retime of an episode without a track says `no EN track` (the EN track column says why); a failed
extraction says `extraction failed: <mkvextract's reason>`. Tables: full run Episode/EN track/Retime; `--only extract` Episode/EN
track/EN file (`extracted`/`kept`/`failed: ..`); `--only retime` Episode/Retime; dry run adds EN file (`to extract`, `kept`, `to
extract again`, `; deletes <names>`), JP file, Retime (`kept`/`to retime`/the problem); no exit line on a dry run (as go). The track
note groups by id + name, names groups of up to 3 episodes, and suggests `--track` only when not given. A cancel prints the table
(rows not done `cancelled`) then exits 130. `--track` with `--only retime` and `--min-match` with `--only extract` are usage errors.
A missing subsretimer is not checked up front (3.3b's rule); a dry run warns. Per-episode stderr lines `[k/n] <name>: ...`, real
runs only. For 3.4b: append AI/Status/Cards to `Columns`, replace the last line by go's TSV line; `SeasonEpisode.Video` is the
same full path `EpisodeList.FromSeason` gives.
Unit 809 passed / 4 skipped (Debug, Release; fresh `SUBSRETIMER_EXE`), 805 / 8 skipped unset (Release). No UI file touched.

### Lead — 2026-10-04 — after phase 3.4
Reviewed `SeasonCommand`, the options and the container check; Release unit 809 / 4
skipped (fresh `SUBSRETIMER_EXE`), 805 / 8 unset; pushed dd759c0. Kept the agent's
choices (EN extracted without a JP file; no deletes without a pick; tables per mode;
the dry run's columns). The agent left an empty `/dummy` outside the repositories; its
delete was blocked and is the user's call. 3.4b's work order gained the facts about
`GoAsync`.

### Phase 3.4b — 2026-10-04 — 0a3803e
Built: `CliRunner.GoAsync` split: `ApplyCardOptions` (`--grouping`, `--deck`), `PlanGo` (dry run: set-up, cached column, checks →
`GoPlan`), `RunGoAsync(list, yes, stderr, token)` → `GoRun(List, Pre, Result, ExitCode)` with `Cells()` (`GoCells(Ai, Status,
Cards)` per episode), `TsvLine(exit)` and `Stopped` (a stop before the pipeline); `PrintTable(GoRun)`. go's output unchanged (its
tests untouched, green). `EpisodeList.FromSeason(dir, videos, settings)`, `OfSeason`. `SeasonCommand`: `SetUpCards` (audio from
audio files refused up front; card options; Subs2 = utf-8, one warning when the project says otherwise, `--only go` and dry runs
too); after the loop `ForGo` (Ready retimes: Subs1 its `OutputPath`, Subs2 the EN file; others skipped with the retime's column,
`retime failed: ..`; numbers `i + start`), or for `--only go` `FromSeason` over the same videos; a `CliException` or cancel from
`RunGoAsync` prints the table (`not made`/`cancelled`) and rethrows. Columns + AI, Status, Cards (dry run + AI; `--only go` dry run
AI, Status); the last line is go's TSV line. `CliOptions`: `--deck` (go and season, empty refused), `--only go`, `--grouping` on
season; refused: `--track`/`--min-match`/`--force` with `--only go`, `--grouping`/`--deck` with `--only extract|retime`.
Choices: go's list is built from the retime outcomes, not re-read from s2s, so a leftover `.ja` of an episode not Ready (JP file
gone, no EN pick) makes no cards; `--only go` takes what s2s holds. Status of such an episode is `skipped: <retime column>` (repeats
the Retime cell, as go's AI-skipped rows repeat the AI cell). The dry run's AI cell is `cached`/`not cached` only where the retime
would be kept (`-` for "to retime": its file is not there yet); it also runs go's checks (exit 1 on an error, as go's dry run). A
cancel during extract/retime shows every episode's Status `cancelled`. go's checks run after extract and retime: a missing deck or
ffmpeg is found only then (the work is kept for the re-run); an up-front check is possible.
Tests: `CliSeasonTests` +2 facts, usage rows +6 net; the full-run tests changed because the behaviour did (go runs): the project
is TSV-only with rules, the scripted retimer writes the JP file as its output, the editor fix is a valid SRT. `CliTests` +1 (`go
--deck`). Seen failing: go given what s2s holds (3 tests: leftover `.ja`, `--track 7`, AI cache), no UTF-8 Subs2 (`Tschüss.`
missing), `--deck` ignored (2). Fragile: keep-if-newer compares mtimes; a re-run test right after a run needs `SetBack` (EN and
retime in one clock tick made episode 4 "to retime" once). Untested: a cancel inside go's pre-pass in season (same catch as the
tested failed check). By hand: the built console on a real two-episode muxed season with the real subsretimer and ffmpeg (audio,
snapshots, Shift-JIS Subs2 project, `--deck`): retimed, 5 cards, umlauts intact, exit 3.
Unit 818 passed / 4 skipped (Debug, Release; fresh `SUBSRETIMER_EXE`), 814 / 8 skipped unset (Release). No UI file, no packages.

### Lead — 2026-10-04 — after phase 3.4b
Reviewed the `GoAsync` split and `ForGo`; `CliTests` only gained a test, so `go` is
unchanged. Release unit 818 / 4 skipped (fresh `SUBSRETIMER_EXE`), 814 / 8 unset;
pushed 0a3803e. CI on dd759c0 green on both jobs (the real-mkvmerge season tests on
Windows included). Kept the agent's choices; its open points (checks after the work,
file times) go to open-items in 3.5, whose work order now lists the stale docs.
