# Changelog

## 0.1.0

**Features:**
- Core library: raw-preserving `.ass`/`.ssa`/`.srt` reading and writing, shift-from-line engine with undo/redo, gap and mismatch detection, average mismatch statistic (ported from Subs Re-Timer 1.0).
- Timing-based auto-align: piecewise-constant offset detection (candidate histogram, overlap scoring, dynamic programming with switch penalty, median refinement).
- `subsretimer` command line with `--auto`, `--output`, `--ref-encoding`, `--target-encoding`, `--print-output`, and the stdout/exit-code contract used by subs2srs.

**Not yet:**
- Interactive editor window.
