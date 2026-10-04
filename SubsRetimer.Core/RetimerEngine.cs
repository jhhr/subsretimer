//  Copyright (C) 2012 Christopher Brochtrup
//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  Port of the algorithms in Subs Re-Timer 1.0 (FormMain.Utils.cs).

namespace SubsRetimer.Core
{
  /// <summary>The result of <see cref="RetimerEngine.ReferenceCoverage"/> and <see cref="RetimerEngine.TargetMatched"/>.</summary>
  /// <param name="Covered">Reference lines the target covers; for <c>TargetMatched</c>, target lines that overlap a reference line.</param>
  /// <param name="Counted">Reference lines with a duration: the ones that can be covered; for <c>TargetMatched</c>, every target line.</param>
  public readonly record struct Coverage(int Covered, int Counted)
  {
    /// <summary>
    /// <see cref="Covered"/> over <see cref="Counted"/>, from 0 to 1; for the
    /// reference, what <c>--min-match</c> is compared with. 0 when nothing
    /// was counted: coverage that cannot be measured does not pass a threshold.
    /// </summary>
    public double Share => Counted == 0 ? 0 : (double)Covered / Counted;

    /// <summary>
    /// The share as a whole percentage, rounded down in integer arithmetic,
    /// so 100 means every counted line and a percentage shown beside a
    /// threshold is never above the share that was compared with it.
    /// </summary>
    public int Percent => Counted == 0 ? 0 : (int)(Covered * 100L / Counted);
  }

  /// <summary>
  /// Holds the reference file (already timed to the video) and the target
  /// file (to be retimed), and implements the tool's single edit: shift
  /// every target line from a given index onward by a delta. Undo and redo
  /// snapshot the target timings.
  /// </summary>
  public sealed class RetimerEngine
  {
    /// <summary>Gap between consecutive lines that flags a probable cut (opening, sponsor, eyecatch).</summary>
    public static readonly TimeSpan LargeGap = TimeSpan.FromMilliseconds(28000);

    private readonly Stack<(TimeSpan Start, TimeSpan End)[]> _undo = new();
    private readonly Stack<(TimeSpan Start, TimeSpan End)[]> _redo = new();

    public SubtitleFile? Reference { get; private set; }
    public SubtitleFile? Target { get; private set; }

    public IReadOnlyList<RetimerLine> ReferenceLines =>
      Reference?.Lines ?? (IReadOnlyList<RetimerLine>)Array.Empty<RetimerLine>();

    public IReadOnlyList<RetimerLine> TargetLines =>
      Target?.Lines ?? (IReadOnlyList<RetimerLine>)Array.Empty<RetimerLine>();

    public bool HasBoth => ReferenceLines.Count > 0 && TargetLines.Count > 0;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// True when the target's timings differ from what was last loaded or
    /// saved. Tracked as the undo depth at which the two agree, so undoing
    /// back to that state is clean again; -1 once that state can no longer
    /// be reached (a new shift after an undo discards the redo history).
    /// </summary>
    public bool IsDirty => _undo.Count != _cleanDepth;
    private int _cleanDepth;

    // ── Change notification ──────────────────────────────────────────────

    /// <summary>
    /// Raised after any operation that changed what a view shows: loading a
    /// file, a shift that moved lines, an undo or redo that did something,
    /// and a save (which clears <see cref="IsDirty"/>). Not raised when the
    /// operation did nothing, so a handler may repaint unconditionally.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Counts the changes reported by <see cref="Changed"/>. A view that
    /// cannot subscribe (or that batches work) can compare this instead.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>Record one change and tell the subscribers. Called after the state is updated.</summary>
    private void Bump()
    {
      Version++;
      Changed?.Invoke();
    }

    public void LoadReference(SubtitleFile file)
    {
      Reference = file;
      Bump();
    }

    public void LoadTarget(SubtitleFile file)
    {
      Target = file;
      _undo.Clear();
      _redo.Clear();
      _cleanDepth = 0;
      Bump();
    }

    // ── Shifting ─────────────────────────────────────────────────────────

    /// <summary>Delta that would move target line <paramref name="targetIndex"/> onto reference line <paramref name="refIndex"/>.</summary>
    public TimeSpan DeltaToMatch(int refIndex, int targetIndex) =>
      ReferenceLines[refIndex].Start - TargetLines[targetIndex].Start;

    /// <summary>Shift target lines <paramref name="fromIndex"/>..end by <paramref name="delta"/>. A zero delta is a no-op.</summary>
    public void ShiftFrom(int fromIndex, TimeSpan delta)
    {
      if (Target == null) throw new InvalidOperationException("No target loaded.");
      if (fromIndex < 0 || fromIndex >= TargetLines.Count) throw new ArgumentOutOfRangeException(nameof(fromIndex));
      if (delta == TimeSpan.Zero) return;

      if (_cleanDepth > _undo.Count) _cleanDepth = -1; // the clean state was in the redo history
      _undo.Push(Snapshot());
      _redo.Clear();

      var lines = Target.Lines;
      for (int i = fromIndex; i < lines.Count; i++)
      {
        lines[i].Start += delta;
        lines[i].End += delta;
      }
      Bump();
    }

    /// <summary>The Time Shift button: shift from <paramref name="targetIndex"/> so it starts with reference line <paramref name="refIndex"/>.</summary>
    public void ShiftToMatch(int refIndex, int targetIndex) =>
      ShiftFrom(targetIndex, DeltaToMatch(refIndex, targetIndex));

    public bool Undo()
    {
      if (!CanUndo) return false;
      _redo.Push(Snapshot());
      Restore(_undo.Pop());
      Bump();
      return true;
    }

    public bool Redo()
    {
      if (!CanRedo) return false;
      _undo.Push(Snapshot());
      Restore(_redo.Pop());
      Bump();
      return true;
    }

    private (TimeSpan, TimeSpan)[] Snapshot() =>
      TargetLines.Select(l => (l.Start, l.End)).ToArray();

    private void Restore((TimeSpan Start, TimeSpan End)[] snap)
    {
      var lines = Target!.Lines;
      for (int i = 0; i < lines.Count; i++)
      {
        lines[i].Start = snap[i].Start;
        lines[i].End = snap[i].End;
      }
    }

    /// <summary>Save the target. Returns the path written. Default: <c>name_retimed.ext</c>.</summary>
    public string Save(string? outputPath = null)
    {
      if (Target == null) throw new InvalidOperationException("No target loaded.");
      string path = outputPath ?? RetimerIO.DefaultOutputPath(Target.Path);
      RetimerIO.Save(Target, path);
      _cleanDepth = _undo.Count;
      Bump(); // the title and the saved-paths list follow IsDirty
      return path;
    }

    // ── Matching (static, reusable by AutoAlign) ─────────────────────────

    /// <summary>
    /// Overlap of line 1 with line 2 as a fraction of line 1's duration:
    /// 1.0 is complete overlap, 0 or negative means none (the more negative,
    /// the further apart).
    /// </summary>
    public static double Overlap(TimeSpan s1, TimeSpan e1, TimeSpan s2, TimeSpan e2)
    {
      double d1 = (e1 - s1).TotalMilliseconds;
      if (d1 <= 0) d1 = 1;
      double inter = Math.Min(e1.TotalMilliseconds, e2.TotalMilliseconds)
                   - Math.Max(s1.TotalMilliseconds, s2.TotalMilliseconds);
      return inter / d1;
    }

    /// <summary>
    /// Index of the line in <paramref name="lines"/> whose start is closest to
    /// <paramref name="start"/>, the earlier one on a tie. -1 when empty.
    ///
    /// Any order is answered correctly. Files load sorted, but a Time Shift
    /// with a negative delta can move a line before the one above it, and the
    /// target list is never re-sorted (undo and the row numbers depend on
    /// that). A sorted list is binary-searched; anything else is scanned.
    /// </summary>
    public static int ClosestIndex(TimeSpan start, IReadOnlyList<RetimerLine> lines) =>
      IsSortedByStart(lines) ? ClosestIndexSorted(start, lines) : ClosestIndexScan(start, lines);

    /// <summary>True when every line starts no earlier than the one before it.</summary>
    public static bool IsSortedByStart(IReadOnlyList<RetimerLine> lines)
    {
      for (int i = 1; i < lines.Count; i++)
        if (lines[i].Start < lines[i - 1].Start) return false;
      return true;
    }

    /// <summary><paramref name="lines"/> itself when it is sorted by start, otherwise a stably sorted copy.</summary>
    internal static IReadOnlyList<RetimerLine> SortedByStart(IReadOnlyList<RetimerLine> lines) =>
      IsSortedByStart(lines) ? lines : lines.OrderBy(l => l.Start).ToList();

    /// <summary>Linear <see cref="ClosestIndex"/> for a list that is out of order: the earlier start wins a tie, then the lower index.</summary>
    private static int ClosestIndexScan(TimeSpan start, IReadOnlyList<RetimerLine> lines)
    {
      int best = -1;
      TimeSpan bestDistance = TimeSpan.MaxValue;
      for (int i = 0; i < lines.Count; i++)
      {
        TimeSpan distance = (lines[i].Start - start).Duration();
        if (best < 0 || distance < bestDistance ||
            (distance == bestDistance && lines[i].Start < lines[best].Start))
        {
          best = i;
          bestDistance = distance;
        }
      }
      return best;
    }

    /// <summary><see cref="ClosestIndex"/> for a list known to be sorted by start: a binary search, for the loops that call it per line.</summary>
    internal static int ClosestIndexSorted(TimeSpan start, IReadOnlyList<RetimerLine> lines)
    {
      if (lines.Count == 0) return -1;
      int lo = 0, hi = lines.Count - 1;
      while (lo < hi)
      {
        int mid = (lo + hi) / 2;
        if (lines[mid].Start < start) lo = mid + 1; else hi = mid;
      }
      // lo is the first line with Start >= start; compare with its predecessor
      if (lo > 0 && (start - lines[lo - 1].Start) <= (lines[lo].Start - start).Duration())
        return lo - 1;
      return lo;
    }

    /// <summary>
    /// Best overlap of a (possibly shifted) line against <paramref name="others"/>,
    /// which must be sorted by start (a loaded file is; see
    /// <see cref="IsSortedByStart"/>). Negative when nothing overlaps.
    /// </summary>
    public static double BestOverlap(TimeSpan start, TimeSpan end, IReadOnlyList<RetimerLine> others, int window = 12)
    {
      int c = ClosestIndexSorted(start, others);
      if (c < 0) return double.NegativeInfinity;
      double best = double.NegativeInfinity;
      int from = Math.Max(0, c - window), to = Math.Min(others.Count - 1, c + window);
      for (int i = from; i <= to; i++)
      {
        double o = Overlap(start, end, others[i].Start, others[i].End);
        if (o > best) best = o;
      }
      return best;
    }

    public static double BestOverlap(RetimerLine line, IReadOnlyList<RetimerLine> others, int window = 12) =>
      BestOverlap(line.Start, line.End, others, window);

    // ── Flags shown as row colors in the editor ──────────────────────────

    /// <summary>
    /// True for lines that start more than <see cref="LargeGap"/> after the
    /// previous line (orange rows). For the first line, the gap is measured
    /// from <paramref name="firstBaseline"/> when given (the other file's
    /// first line), otherwise it is never flagged.
    /// </summary>
    public static bool[] LargeGapFlags(IReadOnlyList<RetimerLine> lines, TimeSpan? firstBaseline = null)
    {
      var flags = new bool[lines.Count];
      for (int i = 0; i < lines.Count; i++)
      {
        TimeSpan? prev = i > 0 ? lines[i - 1].Start : firstBaseline;
        if (prev.HasValue && lines[i].Start - prev.Value > LargeGap) flags[i] = true;
      }
      return flags;
    }

    /// <summary>
    /// First flagged index strictly after <paramref name="from"/>, or -1 when
    /// there is none. <paramref name="from"/> may be -1 (nothing selected:
    /// the first flagged index is returned) or beyond the end.
    /// </summary>
    public static int NextLargeGap(bool[] flags, int from)
    {
      ArgumentNullException.ThrowIfNull(flags);
      if (from >= flags.Length - 1) return -1; // also keeps from + 1 from overflowing
      for (int i = from < 0 ? 0 : from + 1; i < flags.Length; i++)
        if (flags[i]) return i;
      return -1;
    }

    /// <summary>
    /// Last flagged index strictly before <paramref name="from"/>, or -1 when
    /// there is none. <paramref name="from"/> may be -1 (nothing selected:
    /// always -1) or beyond the end (the last flagged index is returned).
    /// </summary>
    public static int PreviousLargeGap(bool[] flags, int from)
    {
      ArgumentNullException.ThrowIfNull(flags);
      if (from <= 0) return -1; // nothing is strictly before index 0
      for (int i = Math.Min(from - 1, flags.Length - 1); i >= 0; i--)
        if (flags[i]) return i;
      return -1;
    }

    /// <summary>
    /// True for lines with no overlap in <paramref name="others"/> (gray rows).
    /// All false when either side is empty. Either list may be out of order.
    /// </summary>
    public static bool[] MismatchFlags(IReadOnlyList<RetimerLine> lines, IReadOnlyList<RetimerLine> others)
    {
      var flags = new bool[lines.Count];
      if (others.Count == 0) return flags;
      others = SortedByStart(others);   // BestOverlap searches it
      for (int i = 0; i < lines.Count; i++)
        flags[i] = BestOverlap(lines[i], others) <= 0;
      return flags;
    }

    // ── Coverage: the quality gate of --min-match ────────────────────────

    /// <summary>
    /// True for lines of which <paramref name="others"/>, together, cover at
    /// least half the duration: the union of their intervals counts, so a
    /// line that the other file splits in two is still covered, and two
    /// overlapping lines are not counted twice. A line with no duration (end
    /// at or before start) is never covered; <see cref="ReferenceCoverage"/>
    /// leaves it out of the count. Either list may be out of order.
    ///
    /// Stricter than "any overlap" (<see cref="MismatchFlags"/>) on purpose:
    /// another episode's file, aligned at the offsets where it overlaps
    /// most, still touches most lines, but covers half of far fewer.
    /// </summary>
    public static bool[] CoverageFlags(IReadOnlyList<RetimerLine> lines, IReadOnlyList<RetimerLine> others)
    {
      var flags = new bool[lines.Count];
      var (starts, ends) = Union(others);
      if (starts.Length == 0) return flags;

      for (int i = 0; i < lines.Count; i++)
      {
        long s = lines[i].Start.Ticks, e = lines[i].End.Ticks;
        if (e <= s) continue;

        // First interval that ends after the line starts; the intervals are
        // disjoint and sorted, so their ends increase too. Every interval
        // the line meets follows it, however many there are: no window.
        int lo = 0, hi = starts.Length;
        while (lo < hi)
        {
          int mid = (lo + hi) / 2;
          if (ends[mid] <= s) lo = mid + 1; else hi = mid;
        }
        long covered = 0;
        for (int k = lo; k < starts.Length && starts[k] < e; k++)
          covered += Math.Min(e, ends[k]) - Math.Max(s, starts[k]);

        flags[i] = 2 * covered >= e - s;   // in ticks, so exactly half counts
      }
      return flags;
    }

    /// <summary>
    /// The union of the lines' intervals as disjoint intervals sorted by
    /// start, in ticks. Lines with no duration add nothing. The input is not
    /// changed and may be in any order.
    /// </summary>
    private static (long[] Starts, long[] Ends) Union(IReadOnlyList<RetimerLine> lines)
    {
      var spans = lines
        .Where(l => l.End > l.Start)
        .Select(l => (Start: l.Start.Ticks, End: l.End.Ticks))
        .OrderBy(x => x.Start)
        .ToList();

      var starts = new List<long>();
      var ends = new List<long>();
      foreach (var (s, e) in spans)
      {
        if (ends.Count > 0 && s <= ends[^1])
          ends[^1] = Math.Max(ends[^1], e);
        else
        {
          starts.Add(s);
          ends.Add(e);
        }
      }
      return (starts.ToArray(), ends.ToArray());
    }

    /// <summary>
    /// How much of the reference the target covers, by
    /// <see cref="CoverageFlags"/>: the reference lines covered, of those
    /// with a duration. Measured on the reference so that lines only the
    /// target has (sound cues and speaker lines in closed captions) cannot
    /// lower it. Meant for after the alignment: the target may be out of
    /// order by then.
    /// </summary>
    public Coverage ReferenceCoverage()
    {
      var reference = ReferenceLines;
      bool[] flags = CoverageFlags(reference, TargetLines);
      return new Coverage(flags.Count(f => f), reference.Count(l => l.End > l.Start));
    }

    /// <summary>
    /// The target lines that overlap some reference line at all, of every
    /// target line: the rows the editor does not gray (<see cref="MismatchFlags"/>).
    /// The looser rule, measured on the other side: it says how much of the
    /// target found a place, where <see cref="ReferenceCoverage"/> says how
    /// much of the reference was filled. Meant for after the alignment, like
    /// that one.
    /// </summary>
    public Coverage TargetMatched()
    {
      var target = TargetLines;
      // MismatchFlags grays nothing against an empty reference, which suits
      // the editor; but then no line overlaps anything, so none is matched.
      if (ReferenceLines.Count == 0) return new Coverage(0, target.Count);
      bool[] mismatched = MismatchFlags(target, ReferenceLines);
      return new Coverage(mismatched.Count(f => !f), target.Count);
    }

    /// <summary>
    /// Average distance in seconds between each target line's start and the
    /// closest reference start. <c>All</c> counts every line; <c>Matched</c>
    /// only lines that overlap something, which is the more useful number
    /// when the files have different line counts.
    /// </summary>
    public (double All, double Matched) AverageMismatchSeconds()
    {
      var t = TargetLines; var r = SortedByStart(ReferenceLines);
      if (t.Count == 0 || r.Count == 0) return (0, 0);

      double sumAll = 0, sumMatched = 0;
      int matched = 0;
      foreach (var line in t)
      {
        int c = ClosestIndexSorted(line.Start, r);
        double d = Math.Abs((line.Start - r[c].Start).TotalSeconds);
        sumAll += d;
        if (BestOverlap(line, r) > 0) { sumMatched += d; matched++; }
      }
      return (sumAll / t.Count, matched > 0 ? sumMatched / matched : 0);
    }
  }
}
