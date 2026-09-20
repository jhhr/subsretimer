//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;
using Xunit;

namespace SubsRetimer.Tests
{
  public class AutoAlignTests
  {
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    /// <summary>
    /// Build a target from a reference by shifting blocks of lines: every
    /// line at index >= cut[i].Index gets an additional cut[i].Ms. Positive
    /// means the target's video has extra material (target runs late).
    /// </summary>
    private static List<RetimerLine> Derive(List<RetimerLine> reference, (int Index, double Ms)[] cuts, int jitterSeed = 0)
    {
      var rnd = new Random(jitterSeed);
      var target = new List<RetimerLine>();
      for (int i = 0; i < reference.Count; i++)
      {
        double off = cuts.Where(c => i >= c.Index).Sum(c => c.Ms);
        double jitter = jitterSeed == 0 ? 0 : rnd.Next(-80, 81);
        target.Add(new RetimerLine
        {
          Start = reference[i].Start + Ms(off + jitter),
          End = reference[i].End + Ms(off + jitter),
          Text = reference[i].Text,
          RawIndex = i
        });
      }
      return target;
    }

    private static RetimerEngine Engine(List<RetimerLine> reference, List<RetimerLine> target)
    {
      var e = new RetimerEngine();
      e.LoadReference(new SubtitleFile { Lines = reference });
      e.LoadTarget(new SubtitleFile { Lines = target });
      return e;
    }

    [Fact]
    public void AlreadyAligned_SingleZeroSegment()
    {
      var reference = Fixtures.Dialogue(60);
      var segs = AutoAlign.Compute(reference, Fixtures.Dialogue(60));
      var s = Assert.Single(segs);
      Assert.Equal(0, s.StartIndex);
      Assert.Equal(60, s.EndIndexExclusive);
      Assert.Equal(TimeSpan.Zero, s.Offset);
      Assert.Equal(60, s.MatchedLines);
    }

    [Fact]
    public void GlobalShift_IsRecovered()
    {
      var reference = Fixtures.Dialogue(80);
      var target = Derive(reference, new[] { (0, 15000.0) });
      var segs = AutoAlign.Compute(reference, target);
      var s = Assert.Single(segs);
      Assert.Equal(-15000, s.Offset.TotalMilliseconds, tolerance: 60.0);
    }

    [Fact]
    public void OpeningAndEyecatchCuts_ProduceThreeSegments()
    {
      // 200 lines; sponsor before line 30 (+15 s), eyecatch before line 120 (+8 s) in the target's video.
      var reference = Fixtures.Dialogue(200);
      var target = Derive(reference, new[] { (30, 15000.0), (120, 8000.0) });

      var segs = AutoAlign.Compute(reference, target);

      Assert.Equal(3, segs.Count);
      Assert.Equal(0, segs[0].StartIndex);
      Assert.Equal(0, segs[0].Offset.TotalMilliseconds, tolerance: 60.0);
      Assert.Equal(30, segs[1].StartIndex);
      Assert.Equal(-15000, segs[1].Offset.TotalMilliseconds, tolerance: 60.0);
      Assert.Equal(120, segs[2].StartIndex);
      Assert.Equal(-23000, segs[2].Offset.TotalMilliseconds, tolerance: 60.0);
      Assert.Equal(200, segs[2].EndIndexExclusive);

      var e = Engine(reference, target);
      AutoAlign.Apply(e, segs);
      for (int i = 0; i < 200; i++)
        Assert.Equal(reference[i].Start.TotalMilliseconds, e.TargetLines[i].Start.TotalMilliseconds, tolerance: 60.0);
      Assert.Equal(2, CountUndo(e)); // first segment is a zero shift and pushes nothing
    }

    [Fact]
    public void NegativeCut_TargetMissingOpening()
    {
      // The target's video lacks a 90 s opening that the reference's video has.
      var reference = Fixtures.Dialogue(150);
      var target = Derive(reference, new[] { (40, -90000.0) });
      var segs = AutoAlign.Compute(reference, target);
      Assert.Equal(2, segs.Count);
      Assert.Equal(40, segs[1].StartIndex);
      Assert.Equal(90000, segs[1].Offset.TotalMilliseconds, tolerance: 60.0);
    }

    [Fact]
    public void JitterAndExtraLines_StillAligned()
    {
      var reference = Fixtures.Dialogue(180);
      var target = Derive(reference, new[] { (50, 12000.0), (130, 6000.0) }, jitterSeed: 7);

      // Sprinkle CC-only lines (sound cues) with no counterpart, each strictly
      // between two dialogue lines so it cannot straddle a cut boundary.
      var cues = new List<(int LineIndex, RetimerLine Cue)>();
      for (int i = 5; i + 1 < target.Count; i += 9)
      {
        if ((target[i + 1].Start - target[i].End).TotalMilliseconds < 1000) continue;
        var cue = new RetimerLine { Start = target[i].End + Ms(300), End = target[i].End + Ms(900), Text = "♪", RawIndex = 1000 + i };
        cues.Add((i, cue));
      }
      Assert.True(cues.Count > 5);
      target.AddRange(cues.Select(c => c.Cue));
      target = target.OrderBy(l => l.Start).ThenBy(l => l.RawIndex).ToList();

      var segs = AutoAlign.Compute(reference, target);
      var e = Engine(reference, target);
      AutoAlign.Apply(e, segs);

      Assert.Equal(3, segs.Count);
      // Dialogue lines end within jitter of their reference line...
      var after = e.AverageMismatchSeconds();
      Assert.True(after.Matched < 0.1, $"matched mismatch {after.Matched}");
      // ...and every sound cue kept its 300 ms distance to the line it followed.
      foreach (var (lineIndex, _) in cues)
      {
        var line = e.TargetLines.Single(l => l.RawIndex == lineIndex);
        var cue = e.TargetLines.Single(l => l.RawIndex == 1000 + lineIndex);
        Assert.Equal(300, (cue.Start - line.End).TotalMilliseconds, tolerance: 0.5);
      }
    }

    [Fact]
    public void EmptyInputs_NoSegments()
    {
      Assert.Empty(AutoAlign.Compute(new List<RetimerLine>(), Fixtures.Dialogue(3)));
      Assert.Empty(AutoAlign.Compute(Fixtures.Dialogue(3), new List<RetimerLine>()));
    }

    [Fact]
    public void Describe_IsOneBased()
    {
      var s = new AlignmentSegment(0, 30, Ms(-15000), 28);
      Assert.Equal("lines 1-30: -15.000s (matched 28/30)", AutoAlign.Describe(s));
    }

    private static int CountUndo(RetimerEngine e)
    {
      int n = 0;
      while (e.Undo()) n++;
      for (int i = 0; i < n; i++) e.Redo();
      return n;
    }
  }
}
