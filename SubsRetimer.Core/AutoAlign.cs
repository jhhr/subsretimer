//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

namespace SubsRetimer.Core
{
  /// <summary>Tunables for <see cref="AutoAlign"/>.</summary>
  public sealed class AutoAlignOptions
  {
    /// <summary>Histogram bin width for candidate offsets.</summary>
    public int BinMs { get; init; } = 100;

    /// <summary>Only reference lines within this distance of a target line vote for an offset.</summary>
    public int WindowMs { get; init; } = 10 * 60 * 1000;

    /// <summary>Maximum number of distinct candidate offsets.</summary>
    public int MaxCandidates { get; init; } = 8;

    /// <summary>Cost of changing offset between consecutive lines, in units of "one fully overlapping line".</summary>
    public double SwitchPenalty { get; init; } = 2.5;

    /// <summary>Adjacent segments whose refined offsets differ by less than this are merged.</summary>
    public int MergeToleranceMs { get; init; } = 60;
  }

  /// <summary>A run of target lines that share one offset.</summary>
  public sealed record AlignmentSegment(int StartIndex, int EndIndexExclusive, TimeSpan Offset, int MatchedLines)
  {
    public int LineCount => EndIndexExclusive - StartIndex;
    public double MatchedFraction => LineCount == 0 ? 0 : (double)MatchedLines / LineCount;
  }

  /// <summary>
  /// Finds a piecewise-constant time offset that aligns the target file to
  /// the reference using timing structure only:
  /// 1. histogram start-time differences to get candidate offsets,
  /// 2. score each target line under each candidate by best overlap,
  /// 3. dynamic programming with a switch penalty picks one candidate per
  ///    line with as few changes as possible,
  /// 4. each segment's offset is refined to the median difference of its
  ///    overlapping pairs.
  /// Lines that overlap nothing under any candidate (sound cues, music) take
  /// their segment's offset, so they move together with their neighbours.
  /// </summary>
  public static class AutoAlign
  {
    public static IReadOnlyList<AlignmentSegment> Compute(
      IReadOnlyList<RetimerLine> reference, IReadOnlyList<RetimerLine> target, AutoAlignOptions? options = null)
    {
      options ??= new AutoAlignOptions();
      if (reference.Count == 0 || target.Count == 0) return Array.Empty<AlignmentSegment>();

      var candidates = CandidateOffsets(reference, target, options);
      if (candidates.Count == 0) candidates.Add(0);

      int n = target.Count, k = candidates.Count;

      // Scores: best overlap of each target line under each candidate, clamped to [0,1].
      var score = new double[n, k];
      for (int j = 0; j < n; j++)
        for (int c = 0; c < k; c++)
        {
          var d = TimeSpan.FromMilliseconds(candidates[c]);
          score[j, c] = Math.Clamp(RetimerEngine.BestOverlap(target[j].Start + d, target[j].End + d, reference), 0, 1);
        }

      // DP: cost[j,c] = -score[j,c] + min over c' of (cost[j-1,c'] + penalty if c' != c)
      var cost = new double[n, k];
      var back = new int[n, k];
      for (int c = 0; c < k; c++) { cost[0, c] = -score[0, c]; back[0, c] = -1; }
      for (int j = 1; j < n; j++)
      {
        int bestPrev = 0;
        for (int c = 1; c < k; c++) if (cost[j - 1, c] < cost[j - 1, bestPrev]) bestPrev = c;
        for (int c = 0; c < k; c++)
        {
          double stay = cost[j - 1, c];
          double sw = cost[j - 1, bestPrev] + options.SwitchPenalty;
          if (stay <= sw) { cost[j, c] = stay - score[j, c]; back[j, c] = c; }
          else { cost[j, c] = sw - score[j, c]; back[j, c] = bestPrev; }
        }
      }

      // Backtrack.
      var assign = new int[n];
      int last = 0;
      for (int c = 1; c < k; c++) if (cost[n - 1, c] < cost[n - 1, last]) last = c;
      for (int j = n - 1; j >= 0; j--) { assign[j] = last; last = back[j, last]; }

      // Group into segments and refine each offset.
      var segments = new List<AlignmentSegment>();
      int segStart = 0;
      for (int j = 1; j <= n; j++)
      {
        if (j == n || assign[j] != assign[segStart])
        {
          segments.Add(Refine(reference, target, segStart, j, candidates[assign[segStart]]));
          segStart = j;
        }
      }

      return MergeSimilar(segments, options.MergeToleranceMs);
    }

    /// <summary>Apply segments to the engine as a sequence of undoable shifts, first segment first.</summary>
    public static void Apply(RetimerEngine engine, IReadOnlyList<AlignmentSegment> segments)
    {
      TimeSpan applied = TimeSpan.Zero;
      foreach (var seg in segments)
      {
        TimeSpan delta = seg.Offset - applied;
        engine.ShiftFrom(seg.StartIndex, delta);
        applied = seg.Offset;
      }
    }

    /// <summary>One-line description of a segment for logs and the CLI summary.</summary>
    public static string Describe(AlignmentSegment seg) =>
      $"lines {seg.StartIndex + 1}-{seg.EndIndexExclusive}: {TimeFormat.FormatOffset(seg.Offset)} (matched {seg.MatchedLines}/{seg.LineCount})";

    // ── Steps ────────────────────────────────────────────────────────────

    internal static List<long> CandidateOffsets(
      IReadOnlyList<RetimerLine> reference, IReadOnlyList<RetimerLine> target, AutoAlignOptions o)
    {
      var hist = new Dictionary<long, int>();
      int ri = 0;
      foreach (var t in target)
      {
        double ts = t.Start.TotalMilliseconds;
        while (ri < reference.Count && reference[ri].Start.TotalMilliseconds < ts - o.WindowMs) ri++;
        for (int i = ri; i < reference.Count && reference[i].Start.TotalMilliseconds <= ts + o.WindowMs; i++)
        {
          long bin = (long)Math.Round((reference[i].Start.TotalMilliseconds - ts) / o.BinMs);
          hist[bin] = hist.GetValueOrDefault(bin) + 1;
        }
      }

      // Peaks with non-maximum suppression (±3 bins).
      var picked = new List<long>();
      foreach (var (bin, _) in hist.OrderByDescending(p => p.Value).ThenBy(p => Math.Abs(p.Key)))
      {
        if (picked.Count >= o.MaxCandidates) break;
        if (picked.Any(p => Math.Abs(p - bin) <= 3)) continue;
        picked.Add(bin);
      }
      return picked.Select(b => b * o.BinMs).ToList();
    }

    private static AlignmentSegment Refine(
      IReadOnlyList<RetimerLine> reference, IReadOnlyList<RetimerLine> target, int from, int to, long offsetMs)
    {
      var d = TimeSpan.FromMilliseconds(offsetMs);
      var diffs = new List<double>();
      for (int j = from; j < to; j++)
      {
        var s = target[j].Start + d;
        var e = target[j].End + d;
        if (RetimerEngine.BestOverlap(s, e, reference) <= 0) continue;
        int c = RetimerEngine.ClosestIndex(s, reference);
        diffs.Add((reference[c].Start - s).TotalMilliseconds);
      }

      double refined = offsetMs;
      if (diffs.Count > 0)
      {
        diffs.Sort();
        double median = diffs.Count % 2 == 1
          ? diffs[diffs.Count / 2]
          : (diffs[diffs.Count / 2 - 1] + diffs[diffs.Count / 2]) / 2.0;
        refined = offsetMs + median;
      }

      return new AlignmentSegment(from, to, TimeSpan.FromMilliseconds(Math.Round(refined)), diffs.Count);
    }

    private static IReadOnlyList<AlignmentSegment> MergeSimilar(List<AlignmentSegment> segs, int toleranceMs)
    {
      var merged = new List<AlignmentSegment>();
      foreach (var s in segs)
      {
        if (merged.Count > 0)
        {
          var p = merged[^1];
          if (Math.Abs((p.Offset - s.Offset).TotalMilliseconds) <= toleranceMs)
          {
            merged[^1] = p with { EndIndexExclusive = s.EndIndexExclusive, MatchedLines = p.MatchedLines + s.MatchedLines };
            continue;
          }
        }
        merged.Add(s);
      }
      return merged;
    }
  }
}
