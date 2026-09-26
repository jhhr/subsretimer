//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;
using Xunit;

namespace SubsRetimer.Tests
{
  public class RetimerEngineTests
  {
    private static RetimerEngine Engine(List<RetimerLine> reference, List<RetimerLine> target)
    {
      var e = new RetimerEngine();
      e.LoadReference(new SubtitleFile { Path = "ref.ass", Format = SubtitleFormat.Ass, Lines = reference });
      e.LoadTarget(new SubtitleFile { Path = "tgt.ass", Format = SubtitleFormat.Ass, Lines = target });
      return e;
    }

    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Overlap_MatchesOriginalSemantics()
    {
      Assert.Equal(1.0, RetimerEngine.Overlap(Ms(0), Ms(1000), Ms(0), Ms(1000)));
      Assert.Equal(0.5, RetimerEngine.Overlap(Ms(0), Ms(1000), Ms(500), Ms(2000)));
      Assert.True(RetimerEngine.Overlap(Ms(0), Ms(1000), Ms(1500), Ms(2000)) < 0);
      Assert.Equal(0.0, RetimerEngine.Overlap(Ms(0), Ms(1000), Ms(1000), Ms(2000)));
    }

    [Fact]
    public void ClosestIndex_BinarySearch()
    {
      var lines = Fixtures.Lines(10); // starts at 1000, 3500, 6000, ...
      Assert.Equal(0, RetimerEngine.ClosestIndex(Ms(0), lines));
      Assert.Equal(0, RetimerEngine.ClosestIndex(Ms(2200), lines));
      Assert.Equal(1, RetimerEngine.ClosestIndex(Ms(2300), lines));
      Assert.Equal(9, RetimerEngine.ClosestIndex(Ms(999999), lines));
      Assert.Equal(-1, RetimerEngine.ClosestIndex(Ms(0), new List<RetimerLine>()));
    }

    /// <summary>
    /// The editor lands on this line when Left/Right, a right-click or a gap
    /// jump asks for the counterpart of the selected one, so which way a tie
    /// goes is a visible decision: the earlier line wins.
    /// </summary>
    [Fact]
    public void ClosestIndex_HalfwayBetweenTwoLines_TakesTheEarlierOne()
    {
      var lines = Fixtures.Lines(10); // starts at 1000, 3500, 6000, ...
      Assert.Equal(0, RetimerEngine.ClosestIndex(Ms(2250), lines));
      Assert.Equal(1, RetimerEngine.ClosestIndex(Ms(4750), lines));
      // A start exactly on a line is that line, not its predecessor.
      Assert.Equal(1, RetimerEngine.ClosestIndex(Ms(3500), lines));
    }

    /// <summary>
    /// A Time Shift with a negative delta can leave the target out of order
    /// (10 s, 20 s, then 5 s), and the list is never re-sorted. The closest
    /// line must still be the closest, not whatever a binary search lands on.
    /// </summary>
    [Fact]
    public void ClosestIndex_OutOfOrder_StillFindsTheClosestStart()
    {
      var lines = new List<RetimerLine>
      {
        new() { Start = Ms(10000), End = Ms(11000) },
        new() { Start = Ms(20000), End = Ms(21000) },
        new() { Start = Ms(5000), End = Ms(6000) },
      };
      Assert.False(RetimerEngine.IsSortedByStart(lines));
      Assert.Equal(2, RetimerEngine.ClosestIndex(Ms(5000), lines));
      Assert.Equal(2, RetimerEngine.ClosestIndex(Ms(0), lines));
      Assert.Equal(0, RetimerEngine.ClosestIndex(Ms(9000), lines));
      Assert.Equal(1, RetimerEngine.ClosestIndex(Ms(99000), lines));
      // Halfway between 5 s and 10 s: the earlier start wins, as it does sorted.
      Assert.Equal(2, RetimerEngine.ClosestIndex(Ms(7500), lines));
    }

    [Fact]
    public void ShiftFrom_BackPastTheLineAbove_LeavesMismatchFlagsRight()
    {
      // Reference: ten 1 s lines at 10 s, 20 s ... 100 s. Target: thirty
      // lines from 1000 s, then ten more from 2000 s that are the reference's
      // dialogue, 1990 s late. Forty lines, so BestOverlap's window of twelve
      // lines either side cannot cover the whole list by accident.
      var reference = Enumerable.Range(0, 10)
        .Select(i => new RetimerLine { Start = Ms(10000 + 10000 * i), End = Ms(11000 + 10000 * i), RawIndex = i })
        .ToList();
      var target = Enumerable.Range(0, 40)
        .Select(i => i < 30
          ? new RetimerLine { Start = Ms(1000000 + 10000 * i), End = Ms(1001000 + 10000 * i), RawIndex = i }
          : new RetimerLine { Start = Ms(2000000 + 10000 * (i - 30)), End = Ms(2001000 + 10000 * (i - 30)), RawIndex = i })
        .ToList();
      var e = Engine(reference, target);

      // Row 30 onto reference row 0: rows 30-39 land before row 29.
      e.ShiftToMatch(0, 30);
      Assert.False(RetimerEngine.IsSortedByStart(e.TargetLines));

      // Every reference line has its counterpart again ...
      Assert.All(RetimerEngine.MismatchFlags(e.ReferenceLines, e.TargetLines), f => Assert.False(f));
      // ... and only the first thirty target lines have none.
      Assert.Equal(
        Enumerable.Range(0, 40).Select(i => i < 30).ToArray(),
        RetimerEngine.MismatchFlags(e.TargetLines, e.ReferenceLines));
      // The window's Left/Right from reference row 0 lands on target row 30.
      Assert.Equal(30, RetimerEngine.ClosestIndex(e.ReferenceLines[0].Start, e.TargetLines));
    }

    [Fact]
    public void ShiftFrom_ShiftsTailOnly_AndTracksDirty()
    {
      var e = Engine(Fixtures.Lines(5), Fixtures.Lines(5));
      Assert.False(e.IsDirty);
      e.ShiftFrom(2, Ms(500));
      Assert.Equal(Ms(1000), e.TargetLines[0].Start);
      Assert.Equal(Ms(3500), e.TargetLines[1].Start);
      Assert.Equal(Ms(6500), e.TargetLines[2].Start);
      Assert.Equal(Ms(6500 + 1800), e.TargetLines[2].End);
      Assert.Equal(Ms(11500), e.TargetLines[4].Start);
      Assert.True(e.IsDirty);
    }

    [Fact]
    public void ShiftFrom_ZeroDeltaIsNoOp()
    {
      var e = Engine(Fixtures.Lines(3), Fixtures.Lines(3));
      e.ShiftFrom(0, TimeSpan.Zero);
      Assert.False(e.IsDirty);
      Assert.False(e.CanUndo);
    }

    [Fact]
    public void ShiftToMatch_UsesStartDifference()
    {
      var reference = Fixtures.Lines(5, startMs: 91000); // 90 s later than target
      var e = Engine(reference, Fixtures.Lines(5));
      Assert.Equal(Ms(90000), e.DeltaToMatch(0, 0));
      e.ShiftToMatch(0, 0);
      for (int i = 0; i < 5; i++) Assert.Equal(reference[i].Start, e.TargetLines[i].Start);
      Assert.Equal((0.0, 0.0), e.AverageMismatchSeconds());
    }

    [Fact]
    public void Undo_BackToTheLoadedState_IsCleanAgain()
    {
      var e = Engine(Fixtures.Lines(3), Fixtures.Lines(3));
      e.ShiftFrom(0, Ms(500));
      e.ShiftFrom(1, Ms(250));
      Assert.True(e.IsDirty);
      e.Undo();
      Assert.True(e.IsDirty);
      e.Undo();
      Assert.False(e.IsDirty);   // the timings match the file again
      e.Redo();
      Assert.True(e.IsDirty);
    }

    [Fact]
    public void Save_MarksTheCleanDepth_AndRedoCanReachItAgain()
    {
      var e = new RetimerEngine();
      e.LoadTarget(RetimerIO.Load(Fixtures.WriteTemp(".srt", Fixtures.SrtFile(Fixtures.Lines(3)))));
      e.ShiftFrom(0, Ms(500));
      string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".srt");
      try
      {
        e.Save(path);
        Assert.False(e.IsDirty);
        e.Undo();
        Assert.True(e.IsDirty);    // the loaded state differs from the saved file
        e.Redo();
        Assert.False(e.IsDirty);   // back to what was saved
        e.Undo();
        e.ShiftFrom(0, Ms(999));   // diverges from the saved history at the same depth
        Assert.True(e.IsDirty);
        e.Undo();
        Assert.True(e.IsDirty);    // the saved state is no longer reachable
      }
      finally { File.Delete(path); }
    }

    [Fact]
    public void UndoRedo_RestoresTimings()
    {
      var e = Engine(Fixtures.Lines(4), Fixtures.Lines(4));
      e.ShiftFrom(0, Ms(1000));
      e.ShiftFrom(2, Ms(2000));
      Assert.Equal(Ms(9000), e.TargetLines[2].Start);

      Assert.True(e.Undo());
      Assert.Equal(Ms(7000), e.TargetLines[2].Start);
      Assert.Equal(Ms(2000), e.TargetLines[0].Start);
      Assert.True(e.CanRedo);

      Assert.True(e.Undo());
      Assert.Equal(Ms(1000), e.TargetLines[0].Start);
      Assert.False(e.Undo());

      Assert.True(e.Redo());
      Assert.True(e.Redo());
      Assert.Equal(Ms(9000), e.TargetLines[2].Start);
      Assert.False(e.Redo());

      // a new shift clears redo
      e.Undo();
      e.ShiftFrom(0, Ms(1));
      Assert.False(e.CanRedo);
    }

    [Fact]
    public void LoadTarget_ClearsHistory()
    {
      var e = Engine(Fixtures.Lines(2), Fixtures.Lines(2));
      e.ShiftFrom(0, Ms(10));
      e.LoadTarget(new SubtitleFile { Lines = Fixtures.Lines(2) });
      Assert.False(e.CanUndo);
      Assert.False(e.IsDirty);
    }

    [Fact]
    public void LargeGapFlags_FlagsAfter28s()
    {
      var lines = Fixtures.Lines(4);
      lines[2].Start += Ms(30000); lines[2].End += Ms(30000);
      lines[3].Start += Ms(30000); lines[3].End += Ms(30000);
      var flags = RetimerEngine.LargeGapFlags(lines);
      Assert.Equal(new[] { false, false, true, false }, flags);

      // first line measured against a baseline from the other file
      var withBaseline = RetimerEngine.LargeGapFlags(lines, Ms(-40000));
      Assert.True(withBaseline[0]);
    }

    [Fact]
    public void MismatchFlags_GrayLinesHaveNoOverlap()
    {
      var reference = Fixtures.Lines(4);
      var target = Fixtures.Lines(4);
      target.Insert(2, new RetimerLine { Start = Ms(100000), End = Ms(101000), Text = "♪", RawIndex = 99 });
      target = target.OrderBy(l => l.Start).ToList();
      var flags = RetimerEngine.MismatchFlags(target, reference);
      Assert.Equal(new[] { false, false, false, false, true }, flags);
      Assert.All(RetimerEngine.MismatchFlags(target, new List<RetimerLine>()), f => Assert.False(f));
    }

    [Fact]
    public void AverageMismatch_MatchedIgnoresUnmatchedLines()
    {
      var reference = Fixtures.Lines(4);
      var target = Fixtures.Lines(4);
      foreach (var l in target) { l.Start += Ms(200); l.End += Ms(200); }
      target.Add(new RetimerLine { Start = Ms(500000), End = Ms(501000), RawIndex = 9 });
      var e = Engine(reference, target);
      var (all, matched) = e.AverageMismatchSeconds();
      Assert.Equal(0.2, matched, 3);
      Assert.True(all > matched);
    }

    [Fact]
    public void Save_WritesDefaultPathAndClearsDirty()
    {
      string path = Fixtures.WriteTemp(".srt", Fixtures.SrtFile(Fixtures.Lines(3)));
      var e = new RetimerEngine();
      e.LoadReference(RetimerIO.Load(path));
      e.LoadTarget(RetimerIO.Load(path));
      e.ShiftFrom(0, Ms(1500));
      string saved = e.Save();
      Assert.Equal(RetimerIO.DefaultOutputPath(path), saved);
      Assert.True(File.Exists(saved));
      Assert.False(e.IsDirty);
      Assert.Contains("00:00:02,500 --> 00:00:04,300", File.ReadAllText(saved));
    }
  }
}
