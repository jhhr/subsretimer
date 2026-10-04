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
    public void Apply_TwoMovingSegments_OneUndoStepEachRestoringTheStageBefore()
    {
      // Both segments move, so both push an undo snapshot: 10 s of lead-in in
      // the target's video everywhere, 15 s more from line 60 on.
      var reference = Fixtures.Dialogue(140);
      var target = Derive(reference, new[] { (0, 10000.0), (60, 15000.0) });
      var original = target.Select(l => l.Start).ToArray();

      var segs = AutoAlign.Compute(reference, target);
      Assert.Equal(2, segs.Count);
      Assert.Equal(60, segs[1].StartIndex);
      Assert.Equal(-10000, segs[0].Offset.TotalMilliseconds, tolerance: 60.0);
      Assert.Equal(-25000, segs[1].Offset.TotalMilliseconds, tolerance: 60.0);

      var e = Engine(reference, target);
      AutoAlign.Apply(e, segs);

      // After the apply every line carries its own segment's offset...
      Starts(e, i => original[i] + segs[i < segs[1].StartIndex ? 0 : 1].Offset);

      // ...the first Undo takes back the second segment's shift only...
      Assert.True(e.Undo());
      Starts(e, i => original[i] + segs[0].Offset);

      // ...and the second takes the file back to how it was loaded.
      Assert.True(e.Undo());
      Starts(e, i => original[i]);
      Assert.False(e.CanUndo);

      void Starts(RetimerEngine engine, Func<int, TimeSpan> expected)
      {
        for (int i = 0; i < original.Length; i++)
          Assert.Equal(expected(i).TotalMilliseconds, engine.TargetLines[i].Start.TotalMilliseconds, tolerance: 0.5);
      }
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

    /// <summary>
    /// A closed-caption file made from <paramref name="dialogue"/>, as another
    /// subtitler would time it for another cut of the video: the cuts of
    /// <see cref="Derive"/>, start and end each off by up to ±250 ms, about
    /// 15% of the lines split in two, and a 0.5 s sound cue in about 15% of
    /// the gaps of 1.5 s or more. Sorted by start, as a loaded file is.
    /// </summary>
    private static List<RetimerLine> ClosedCaptions(List<RetimerLine> dialogue, (int Index, double Ms)[] cuts, int seed) =>
      ClosedCaptions(dialogue, cuts, seed, out _);

    /// <summary>
    /// The same file as the overload above, also giving the index in
    /// <paramref name="dialogue"/> each returned line came from: both halves of
    /// a split line and the cue after a line count as that line's.
    /// </summary>
    private static List<RetimerLine> ClosedCaptions(List<RetimerLine> dialogue, (int Index, double Ms)[] cuts, int seed, out int[] source)
    {
      var rnd = new Random(seed);
      var lines = new List<(RetimerLine Line, int Source)>();
      for (int i = 0; i < dialogue.Count; i++)
      {
        double off = cuts.Where(c => i >= c.Index).Sum(c => c.Ms);
        double s = dialogue[i].Start.TotalMilliseconds + off + rnd.Next(-250, 251);
        double e = dialogue[i].End.TotalMilliseconds + off + rnd.Next(-250, 251);
        if (rnd.Next(100) < 15)
        {
          double m = Math.Round((s + e) / 2);
          lines.Add((new RetimerLine { Start = Ms(s), End = Ms(m), Text = "first half" }, i));
          lines.Add((new RetimerLine { Start = Ms(m), End = Ms(e), Text = "second half" }, i));
        }
        else lines.Add((new RetimerLine { Start = Ms(s), End = Ms(e), Text = dialogue[i].Text }, i));

        if (i + 1 < dialogue.Count && (dialogue[i + 1].Start - dialogue[i].End).TotalMilliseconds >= 1500 && rnd.Next(100) < 15)
        {
          double cue = dialogue[i].End.TotalMilliseconds + off + 500;
          lines.Add((new RetimerLine { Start = Ms(cue), End = Ms(cue + 500), Text = "♪" }, i));
        }
      }
      // OrderBy is stable, so lines with equal starts keep the order they were made in.
      var sorted = lines.OrderBy(p => p.Line.Start).ToList();
      source = sorted.Select(p => p.Source).ToArray();
      return sorted
        .Select((p, k) => new RetimerLine { Start = p.Line.Start, End = p.Line.End, Text = p.Line.Text, RawIndex = k })
        .ToList();
    }

    [Theory]
    [InlineData(1, 59)]
    [InlineData(1, 36)]
    [InlineData(41, 141)]
    public void JitteredClosedCaptions_EveryBlockKeepsItsOwnOffset(int dialogueSeed, int seed)
    {
      // Two subtitlers' timings differ by a few hundred ms per line, which
      // spreads each true offset over several histogram bins. These pairs
      // once lost the 40-line leading block's -4 s from the candidates, so
      // the block went tens of seconds off: (1, 59) because -19 s and -27 s
      // took two slots each, (1, 36) because the -4 s peak ranked below
      // chance peaks. (41, 141) still needs more than 8 candidates when the
      // histogram is smoothed.
      (int Index, double Ms)[] cuts = { (0, 4000.0), (40, 15000.0), (200, 8000.0) };
      var reference = Fixtures.Dialogue(300, dialogueSeed);
      var target = ClosedCaptions(Fixtures.Dialogue(300, dialogueSeed), cuts, seed, out int[] source);

      var segs = AutoAlign.Compute(reference, target);

      // Every line gets the offset of the block it came from, give or take
      // a second. Only a line made from one of the two dialogue lines on
      // either side of a cut may fall in the neighbouring segment (the
      // boundary limitation in docs/ui-plan.md, not this test's subject).
      var wrong = new List<string>();
      foreach (var seg in segs)
        for (int k = seg.StartIndex; k < seg.EndIndexExclusive; k++)
        {
          double expected = -cuts.Where(c => source[k] >= c.Index).Sum(c => c.Ms);
          bool nearCut = cuts.Any(c => c.Index > 0 && source[k] >= c.Index - 2 && source[k] <= c.Index + 1);
          if (Math.Abs(seg.Offset.TotalMilliseconds - expected) > 1000 && !nearCut)
            wrong.Add(FormattableString.Invariant($"line {k + 1} (dialogue {source[k] + 1}): {seg.Offset.TotalSeconds:0.000} s, expected {expected / 1000:0.000} s"));
        }
      Assert.True(wrong.Count == 0,
        $"{wrong.Count} lines off; segments {string.Join("; ", segs.Select(AutoAlign.Describe))}; first: {string.Join(", ", wrong.Take(3))}");
    }

    [Fact]
    public void ReferenceCoverage_TellsTheRightClosedCaptionFileFromAnotherEpisodes()
    {
      // The target's video has a 4 s lead-in, 15 s more from line 40 and
      // 8 s more from line 200; so does the other episode's.
      var cuts = new[] { (0, 4000.0), (40, 15000.0), (200, 8000.0) };
      var reference = Fixtures.Dialogue(300, seed: 1);
      var rightFile = ClosedCaptions(Fixtures.Dialogue(300, seed: 1), cuts, seed: 101);
      var wrongFile = ClosedCaptions(Fixtures.Dialogue(300, seed: 2), cuts, seed: 201);
      Assert.True(rightFile.Count > reference.Count + 50, "the fixture should add split lines and cues");

      double right = AlignedCoverage(rightFile);
      double wrong = AlignedCoverage(wrongFile);

      // Split lines and cues leave the right file near full coverage, and
      // the other episode, at the offsets auto-align found for it, far
      // below.
      Assert.True(right >= 0.9, $"right pair {right:0.000}");
      Assert.True(right - wrong >= 0.25, $"right pair {right:0.000}, wrong pair {wrong:0.000}");

      double AlignedCoverage(List<RetimerLine> target)
      {
        var e = Engine(reference, target);
        AutoAlign.Apply(e, AutoAlign.Compute(e.ReferenceLines, e.TargetLines));
        return e.ReferenceCoverage().Share;
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
