//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;
using SubsRetimer.Editor;
using SubsRetimer.UiTests.Harness;
using Xunit;

namespace SubsRetimer.UiTests.Tests
{
  /// <summary>
  /// Phase 5's Auto Align: the whole target aligned in one go, the status
  /// line under the lists, and one undo step per segment that moved.
  /// </summary>
  [Collection(GtkCollection.Name)]
  public class WindowAutoAlignTests
  {
    private readonly GtkFixture _gtk;

    public WindowAutoAlignTests(GtkFixture gtk) => _gtk = gtk;

    private const int FirstCut = 8;
    private const int SecondCut = 16;

    [GtkFact]
    public async Task AutoAlign_AlignsBothCuts_ReportsThemAndUndoesOneSegmentAtATime()
    {
      using var scope = new UiTestScope(_gtk);
      // The target's video has 15 s more material from row 8 and another 15 s
      // from row 16, so it runs +15 s and then +30 s late.
      var files = SubtitleFixtures.Pair(
        scope.TempDir, count: 24, seed: 5, silenceBefore: new[] { 5, 17 },
        cuts: new[] { (FirstCut, 15000d), (SecondCut, 15000d) });

      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
      });

      var before = scope.Read(() => new
      {
        window.StatusText,
        window.CanAutoAlign,
        Gray = GrayRowsFromFirstCut(window),
      });
      Assert.True(before.CanAutoAlign);
      Assert.Equal("", before.StatusText);
      // The late rows have nothing to overlap, so they start out gray.
      Assert.NotEmpty(before.Gray);

      IReadOnlyList<AlignmentSegment> segments = Array.Empty<AlignmentSegment>();
      await scope.RunAsync(window, () => segments = window.AutoAlign());

      Assert.Equal(3, segments.Count);
      Assert.Equal(new[] { 0, FirstCut, SecondCut }, segments.Select(s => s.StartIndex).ToArray());
      Assert.Equal(new[] { FirstCut, SecondCut, 24 }, segments.Select(s => s.EndIndexExclusive).ToArray());
      Assert.Equal(
        new[] { TimeSpan.Zero, TimeSpan.FromSeconds(-15), TimeSpan.FromSeconds(-30) },
        segments.Select(s => s.Offset).ToArray());

      var aligned = scope.Read(() => new
      {
        window.StatusText,
        Gray = GrayRowsFromFirstCut(window),
        Differences = Differences(window),
        window.IsDirty,
        window.Engine.CanUndo,
      });

      // The status line names every segment the run applied.
      Assert.Equal(string.Join("; ", segments.Select(Core.AutoAlign.Describe)), aligned.StatusText);
      Assert.Contains("-15.000s", aligned.StatusText);
      Assert.Contains("-30.000s", aligned.StatusText);

      Assert.All(aligned.Differences, d => Assert.Equal(TimeSpan.Zero, d));
      Assert.Empty(aligned.Gray);   // the shifted rows overlap again
      Assert.True(aligned.IsDirty);
      Assert.True(aligned.CanUndo);

      // Undo steps back one segment: only the second cut's 15 s come back.
      bool undone = false;
      await scope.RunAsync(window, () => undone = window.Undo());
      var afterUndo = scope.Read(() => new
      {
        Differences = Differences(window),
        window.Engine.CanRedo,
        window.StatusText,
      });
      Assert.True(undone);
      Assert.True(afterUndo.CanRedo);
      for (int i = 0; i < SecondCut; i++)
        Assert.Equal(TimeSpan.Zero, afterUndo.Differences[i]);
      for (int i = SecondCut; i < afterUndo.Differences.Length; i++)
        Assert.Equal(TimeSpan.FromSeconds(15), afterUndo.Differences[i]);
      // The report is about the run, not about the current state: undo leaves it.
      Assert.Equal(aligned.StatusText, afterUndo.StatusText);
    }

    [GtkFact]
    public async Task AutoAlign_WithOnlyOneFile_DoesNothingAndSaysNothing()
    {
      using var scope = new UiTestScope(_gtk);
      var files = SubtitleFixtures.Pair(scope.TempDir, count: 20, cuts: (10, 15000));

      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () => window.LoadTarget(files.TargetPath));

      IReadOnlyList<AlignmentSegment> segments = Array.Empty<AlignmentSegment>();
      await scope.RunAsync(window, () => segments = window.AutoAlign());

      var state = scope.Read(() => new { window.CanAutoAlign, window.StatusText, window.IsDirty });
      Assert.Empty(segments);
      Assert.False(state.CanAutoAlign);
      Assert.Equal("", state.StatusText);
      Assert.False(state.IsDirty);
    }

    /// <summary>How far each target line is from the reference line with the same index.</summary>
    private static TimeSpan[] Differences(RetimerWindow window)
    {
      var target = window.Engine.TargetLines;
      var reference = window.Engine.ReferenceLines;
      return Enumerable.Range(0, target.Count).Select(i => target[i].Start - reference[i].Start).ToArray();
    }

    /// <summary>The gray (unmatched) target rows from the first cut on.</summary>
    private static int[] GrayRowsFromFirstCut(RetimerWindow window) =>
      Enumerable.Range(FirstCut, window.Engine.TargetLines.Count - FirstCut)
        .Where(i => window.RowState(Side.Target, i) == RowState.Mismatch)
        .ToArray();
  }
}
