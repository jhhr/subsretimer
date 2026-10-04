//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;
using Xunit;

namespace SubsRetimer.Tests
{
  /// <summary>
  /// The change notification the editor window repaints on, and the
  /// orange-row navigation it drives from <see cref="RetimerEngine.LargeGapFlags"/>.
  /// </summary>
  public class RetimerEngineChangeTests
  {
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    private static SubtitleFile Sub(int count) =>
      new SubtitleFile { Path = "x.ass", Format = SubtitleFormat.Ass, Lines = Fixtures.Lines(count) };

    // ── Changed / Version ────────────────────────────────────────────────

    [Fact]
    public void Loading_RaisesChangedAndBumpsVersion()
    {
      var e = new RetimerEngine();
      int fired = 0;
      e.Changed += () => fired++;
      Assert.Equal(0, e.Version);

      e.LoadReference(Sub(3));
      Assert.Equal(1, fired);
      Assert.Equal(1, e.Version);

      e.LoadTarget(Sub(3));
      Assert.Equal(2, fired);
      Assert.Equal(2, e.Version);
    }

    [Fact]
    public void Shifting_RaisesWhenLinesMove_NotForAZeroDelta()
    {
      var e = new RetimerEngine();
      e.LoadReference(Sub(4));
      e.LoadTarget(Sub(4));
      int fired = 0, version = e.Version;
      e.Changed += () => fired++;

      e.ShiftFrom(0, Ms(500));
      Assert.Equal(1, fired);

      e.ShiftFrom(0, TimeSpan.Zero); // no snapshot pushed, nothing moved
      Assert.Equal(1, fired);

      e.ShiftToMatch(0, 0); // the target is 500 ms late now, so this moves it back
      Assert.Equal(2, fired);

      e.ShiftToMatch(0, 0); // already matching: a zero delta, nothing to report
      Assert.Equal(2, fired);
      Assert.Equal(version + 2, e.Version);
    }

    [Fact]
    public void UndoRedo_RaiseOnlyWhenTheyDoSomething()
    {
      var e = new RetimerEngine();
      e.LoadTarget(Sub(4));
      e.ShiftFrom(0, Ms(1000));
      int fired = 0, version = e.Version;
      e.Changed += () => fired++;

      Assert.True(e.Undo());
      Assert.Equal(1, fired);
      Assert.False(e.Undo()); // nothing left to undo
      Assert.Equal(1, fired);
      Assert.Equal(version + 1, e.Version);

      Assert.True(e.Redo());
      Assert.Equal(2, fired);
      Assert.False(e.Redo()); // nothing left to redo
      Assert.Equal(2, fired);
      Assert.Equal(version + 2, e.Version);
    }

    [Fact]
    public void Save_RaisesOnceAndClearsDirty()
    {
      string path = Fixtures.WriteTemp(".srt", Fixtures.SrtFile(Fixtures.Lines(3)));
      var e = new RetimerEngine();
      e.LoadTarget(RetimerIO.Load(path));
      e.ShiftFrom(0, Ms(1500));
      int fired = 0, version = e.Version;
      e.Changed += () => fired++;

      e.Save();
      Assert.Equal(1, fired);
      Assert.Equal(version + 1, e.Version);
      Assert.False(e.IsDirty);
    }

    // ── Gap navigation ───────────────────────────────────────────────────

    [Fact]
    public void GapNavigation_WalksTheFlaggedRows()
    {
      var flags = new[] { false, true, false, false, true, false }; // gaps at 1 and 4

      Assert.Equal(1, RetimerEngine.NextLargeGap(flags, -1)); // nothing selected
      Assert.Equal(4, RetimerEngine.NextLargeGap(flags, 1));  // a flag is not its own answer
      Assert.Equal(-1, RetimerEngine.NextLargeGap(flags, 4)); // at the last flag
      Assert.Equal(-1, RetimerEngine.NextLargeGap(flags, 99));

      Assert.Equal(1, RetimerEngine.PreviousLargeGap(flags, 4));
      Assert.Equal(4, RetimerEngine.PreviousLargeGap(flags, 99)); // beyond the end
      Assert.Equal(-1, RetimerEngine.PreviousLargeGap(flags, 1)); // at the first flag
      Assert.Equal(-1, RetimerEngine.PreviousLargeGap(flags, 0));
      Assert.Equal(-1, RetimerEngine.PreviousLargeGap(flags, -1));
    }

    [Fact]
    public void GapNavigation_MiddlePositionSeesBothSides()
    {
      var flags = new[] { false, true, false, false, true, false };
      Assert.Equal(4, RetimerEngine.NextLargeGap(flags, 2));
      Assert.Equal(1, RetimerEngine.PreviousLargeGap(flags, 2));
    }

    [Fact]
    public void GapNavigation_NoFlagsAndEmptyLists()
    {
      var none = new bool[5];
      Assert.Equal(-1, RetimerEngine.NextLargeGap(none, -1));
      Assert.Equal(-1, RetimerEngine.NextLargeGap(none, 2));
      Assert.Equal(-1, RetimerEngine.PreviousLargeGap(none, 5));
      Assert.Equal(-1, RetimerEngine.PreviousLargeGap(none, 2));

      Assert.Equal(-1, RetimerEngine.NextLargeGap(Array.Empty<bool>(), -1));
      Assert.Equal(-1, RetimerEngine.PreviousLargeGap(Array.Empty<bool>(), 3));
    }

    [Fact]
    public void GapNavigation_OverLargeGapFlags()
    {
      var lines = Fixtures.Lines(6);
      for (int i = 3; i < lines.Count; i++) { lines[i].Start += Ms(30000); lines[i].End += Ms(30000); }
      var flags = RetimerEngine.LargeGapFlags(lines);

      Assert.Equal(3, RetimerEngine.NextLargeGap(flags, -1));
      Assert.Equal(-1, RetimerEngine.NextLargeGap(flags, 3));
      Assert.Equal(3, RetimerEngine.PreviousLargeGap(flags, lines.Count));
    }
  }
}
