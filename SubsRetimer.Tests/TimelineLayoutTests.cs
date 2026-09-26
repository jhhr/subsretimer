//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;
using SubsRetimer.Editor;
using Xunit;

namespace SubsRetimer.Tests
{
  /// <summary>
  /// The timeline chart's arithmetic. <see cref="TimelineLayout"/> touches no
  /// GTK type, so all of this runs without a display.
  /// </summary>
  public class TimelineLayoutTests
  {
    private const double Width = 800;
    private const double Height = 64;

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static RetimerLine Line(double startSeconds, double endSeconds, string text = "x") =>
      new() { Start = S(startSeconds), End = S(endSeconds), Text = text };

    /// <summary>A layout centred on a line from 12.3 s to 14.1 s: the middle is 13.2 s.</summary>
    private static TimelineLayout Layout(int scaleSeconds = 10) =>
      new(Width, Height, scaleSeconds, S(12.3), S(14.1));

    [Fact]
    public void ScaleWindow_IsCentredOnTheActiveLineAndAWholeSecondWide()
    {
      var layout = Layout();

      // 13.2 - 5 = 8.2, snapped to the nearest second.
      Assert.Equal(S(8), layout.WindowStart);
      Assert.Equal(S(18), layout.WindowEnd);
      Assert.Equal(layout.ScaleWindow(), (layout.WindowStart, layout.WindowEnd));

      // The active line is inside, and the window's middle is within half a
      // second of the line's middle: the snap to a whole second is the only
      // thing allowed to move it.
      Assert.True(layout.ActiveStart >= layout.WindowStart && layout.ActiveEnd <= layout.WindowEnd);
      double windowMid = (layout.WindowStart + (layout.WindowEnd - layout.WindowStart) / 2).TotalSeconds;
      Assert.True(Math.Abs(windowMid - 13.2) <= 0.5);
    }

    [Fact]
    public void ScaleWindow_NeverStartsBeforeZero()
    {
      var layout = new TimelineLayout(Width, Height, 10, S(0.5), S(1.2));

      Assert.Equal(TimeSpan.Zero, layout.WindowStart);
      Assert.Equal(S(10), layout.WindowEnd);
    }

    [Fact]
    public void VisibleLines_KeepsEverythingThatTouchesTheWindow()
    {
      var layout = Layout();   // 8 s to 18 s
      var lines = new List<RetimerLine>
      {
        Line(1, 3),      // 0: entirely before
        Line(6, 9),      // 1: straddles the start
        Line(12.3, 14.1),// 2: the active line, inside
        Line(17, 20),    // 3: straddles the end
        Line(25, 27),    // 4: entirely after
      };

      Assert.Equal(new[] { 1, 2, 3 }, layout.VisibleLines(lines));

      // A line longer than the window covers every pixel of it, so it shows.
      Assert.Equal(new[] { 0 }, layout.VisibleLines(new List<RetimerLine> { Line(2, 40) }));
      // Touching the edge is not overlapping it: such a bar has no width.
      Assert.Empty(layout.VisibleLines(new List<RetimerLine> { Line(4, 8) }));
    }

    [Fact]
    public void XFor_MapsTheWindowOntoTheWholeWidth()
    {
      var layout = Layout();

      Assert.Equal(0, layout.XFor(layout.WindowStart), 6);
      Assert.Equal(Width, layout.XFor(layout.WindowEnd), 6);
      Assert.Equal(Width / 2, layout.XFor(S(13)), 6);
      // One second is one major tick apart.
      Assert.Equal(Width / 10, layout.SecondWidth, 6);

      // A bar is the same mapping, in the row its side owns.
      var bar = layout.BarRect(Line(12.3, 14.1), Side.Target);
      Assert.Equal(layout.XFor(S(12.3)), bar.X, 6);
      Assert.Equal(layout.XFor(S(14.1)) - layout.XFor(S(12.3)), bar.Width, 6);
      Assert.True(layout.BarRect(Line(12.3, 14.1), Side.Reference).Y < bar.Y);
    }

    [Fact]
    public void ScaleSeconds_IsClampedToWhatTheChartSupports()
    {
      Assert.Equal(TimelineLayout.MinScaleSeconds, new TimelineLayout(Width, Height, 1, S(20), S(21)).ScaleSeconds);
      Assert.Equal(TimelineLayout.MaxScaleSeconds, new TimelineLayout(Width, Height, 500, S(20), S(21)).ScaleSeconds);
      Assert.Equal(30, new TimelineLayout(Width, Height, 30, S(20), S(21)).ScaleSeconds);

      // The window follows the clamped value, not the one asked for.
      Assert.Equal(S(4), new TimelineLayout(Width, Height, 1, S(20), S(22)).WindowEnd
        - new TimelineLayout(Width, Height, 1, S(20), S(22)).WindowStart);
    }

    [Fact]
    public void Ticks_AreOnePerSecondWithHalfSecondsBetweenThem()
    {
      var layout = Layout();   // 10 s across 800 px

      var major = layout.MajorTicks();
      Assert.Equal(9, major.Count);                  // the edges are not ticks
      Assert.Equal(80, major[0], 6);
      Assert.Equal(720, major[^1], 6);

      var minor = layout.MinorTicks();
      Assert.Equal(10, minor.Count);
      Assert.Equal(40, minor[0], 6);
      Assert.Equal(760, minor[^1], 6);

      // Every second second is labelled at this scale, starting one second in.
      Assert.Equal(2, layout.LabelEvery);
      var labels = layout.TickLabels();
      Assert.Equal(5, labels.Count);
      Assert.Equal("0:00:09", labels[0].Text);
      Assert.Equal(80, labels[0].X, 6);
      Assert.Equal("0:00:17", labels[^1].Text);
    }

    [Fact]
    public void LabelEvery_ThinsOutAsTheWindowWidens()
    {
      Assert.Equal(1, new TimelineLayout(Width, Height, 4, S(20), S(21)).LabelEvery);
      Assert.Equal(2, new TimelineLayout(Width, Height, 10, S(20), S(21)).LabelEvery);
      Assert.Equal(4, new TimelineLayout(Width, Height, 30, S(20), S(21)).LabelEvery);
      Assert.Equal(6, new TimelineLayout(Width, Height, 60, S(20), S(21)).LabelEvery);
      Assert.Equal(8, new TimelineLayout(Width, Height, 120, S(20), S(21)).LabelEvery);
    }

    [Fact]
    public void TickLabel_ReadsHoursMinutesSeconds()
    {
      Assert.Equal("0:00:09", TimelineLayout.TickLabel(S(9)));
      Assert.Equal("1:02:03", TimelineLayout.TickLabel(new TimeSpan(1, 2, 3)));
    }
  }
}
