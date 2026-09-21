//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using SubsRetimer.Core;
using SubsRetimer.Editor;
using SubsRetimer.UiTests.Harness;
using Xunit;

namespace SubsRetimer.UiTests.Tests
{
  /// <summary>
  /// Phase 2's window: the two lists, the row colours, the counters and the
  /// detail strip, and the in-place refresh that keeps the store alone.
  /// </summary>
  [Collection(GtkCollection.Name)]
  public class WindowLoadTests
  {
    private readonly GtkFixture _gtk;

    public WindowLoadTests(GtkFixture gtk) => _gtk = gtk;

    /// <summary>The colour a row must have, from the engine's flags alone.</summary>
    private static RowState[] Expected(IReadOnlyList<RetimerLine> lines, IReadOnlyList<RetimerLine> others)
    {
      bool[] gap = RetimerEngine.LargeGapFlags(lines, others.Count > 0 ? others[0].Start : null);
      bool[] mismatch = RetimerEngine.MismatchFlags(lines, others);
      return Enumerable.Range(0, lines.Count)
        .Select(i => gap[i] ? RowState.Gap : mismatch[i] ? RowState.Mismatch : RowState.None)
        .ToArray();
    }

    [GtkFact]
    public async Task LoadingBothFiles_ColoursEveryRowFromTheEngineFlags()
    {
      using var scope = new UiTestScope(_gtk);
      // 20 lines, a silence before rows 6 and 14 (orange) and a target that
      // runs 15 s late from row 10 on (gray from there).
      var files = SubtitleFixtures.Pair(scope.TempDir, count: 20, cuts: (10, 15000));

      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
      });

      var shown = scope.Read(() => new
      {
        Reference = Rows(window, Side.Reference),
        Target = Rows(window, Side.Target),
        ReferenceCounter = window.Counters(Side.Reference),
        TargetCounter = window.Counters(Side.Target),
        ReferenceLines = window.Engine.ReferenceLines,
        TargetLines = window.Engine.TargetLines,
      });

      Assert.Equal(20, shown.ReferenceLines.Count);
      Assert.Equal(20, shown.TargetLines.Count);
      Assert.Equal(Expected(shown.ReferenceLines, shown.TargetLines), shown.Reference);
      Assert.Equal(Expected(shown.TargetLines, shown.ReferenceLines), shown.Target);

      // The fixture is only worth loading if it shows all three colours.
      Assert.Contains(RowState.Gap, shown.Target);
      Assert.Contains(RowState.Mismatch, shown.Target);
      Assert.Contains(RowState.None, shown.Target);

      // Nothing is selected yet: the counters count from 1, so 0 means none.
      Assert.Equal("0/20", shown.ReferenceCounter);
      Assert.Equal("0/20", shown.TargetCounter);
    }

    [GtkFact]
    public async Task SelectingAPairOnEachSide_FillsTheDetailStrip()
    {
      using var scope = new UiTestScope(_gtk);
      var files = SubtitleFixtures.Pair(scope.TempDir, count: 20, cuts: (10, 15000));

      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
      });
      // Row 12 is inside the late part, so the strip has a real offset to show.
      await scope.RunAsync(window, () =>
      {
        window.SelectReference(12);
        window.SelectTarget(12);
      });

      var shown = scope.Read(() => new
      {
        window.DetailSummary,
        window.SelectedReference,
        window.SelectedTarget,
        Counter = window.Counters(Side.Target),
        Reference = window.Engine.ReferenceLines[12],
        Target = window.Engine.TargetLines[12],
      });

      Assert.Equal(12, shown.SelectedReference);
      Assert.Equal(12, shown.SelectedTarget);
      Assert.Equal("13/20", shown.Counter);

      double overlap = RetimerEngine.Overlap(
        shown.Target.Start, shown.Target.End, shown.Reference.Start, shown.Reference.End);
      string percent = string.Format(
        CultureInfo.InvariantCulture, "{0:0}%", Math.Clamp(overlap, 0, 1) * 100);

      Assert.Contains(TimeFormat.FormatAss(shown.Reference.Start), shown.DetailSummary);
      Assert.Contains(TimeFormat.FormatAss(shown.Target.Start), shown.DetailSummary);
      Assert.Contains("overlap " + percent, shown.DetailSummary);
      Assert.Contains(TimeFormat.FormatOffset(shown.Reference.Start - shown.Target.Start), shown.DetailSummary);
      // The target is 15 s late here, so the two lines cannot overlap at all.
      Assert.Contains("overlap 0%", shown.DetailSummary);
      Assert.Contains("-15.000s", shown.DetailSummary);
    }

    [GtkFact]
    public async Task ShiftingRewritesTheBoundRows_WithoutRebuildingTheStore()
    {
      using var scope = new UiTestScope(_gtk);
      var files = SubtitleFixtures.Pair(scope.TempDir, count: 20, cuts: (10, 15000));

      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
      });
      await scope.RunAsync(window, () =>
      {
        window.SelectReference(10);
        window.SelectTarget(10);
      });

      var before = scope.Read(() => new
      {
        Row = window.BoundStartText(Side.Target, 10),
        Untouched = window.BoundStartText(Side.Target, 0),
        window.StoreRebuilds,
      });
      // The row has to be on screen for this test to say anything.
      Assert.NotNull(before.Row);
      Assert.Equal(2, before.StoreRebuilds);   // one per loaded file

      await scope.RunAsync(window, window.TimeShift);

      var after = scope.Read(() => new
      {
        Row = window.BoundStartText(Side.Target, 10),
        Untouched = window.BoundStartText(Side.Target, 0),
        window.StoreRebuilds,
        Reference = TimeFormat.FormatAss(window.Engine.ReferenceLines[10].Start),
      });

      Assert.NotEqual(before.Row, after.Row);
      Assert.Equal(after.Reference, after.Row);   // the cell shows the shifted time
      // The shift starts at row 10, so the rows above it keep their text ...
      Assert.Equal(before.Untouched, after.Untouched);
      // ... and no row was replaced: the store was not rebuilt.
      Assert.Equal(before.StoreRebuilds, after.StoreRebuilds);
    }

    private static RowState[] Rows(RetimerWindow window, Side side)
    {
      int count = side == Side.Reference
        ? window.Engine.ReferenceLines.Count
        : window.Engine.TargetLines.Count;
      return Enumerable.Range(0, count).Select(i => window.RowState(side, i)).ToArray();
    }
  }
}
