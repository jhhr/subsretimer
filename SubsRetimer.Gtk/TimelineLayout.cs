//  Copyright (C) 2012 Christopher Brochtrup
//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  Port of Subs Re-Timer 1.0's ChartSubs geometry (calculateScale,
//  getLinesInScale, convertTimeToPixelPos and the tick maths).

using System.Globalization;
using SubsRetimer.Core;

namespace SubsRetimer.Editor
{
  /// <summary>A rectangle in chart pixels, with the origin at the chart's top left.</summary>
  internal readonly record struct ChartRect(double X, double Y, double Width, double Height);

  /// <summary>
  /// Where everything the timeline chart shows belongs on its surface: the
  /// visible time window, which lines fall inside it, and the pixel positions
  /// of bars, ticks and labels.
  ///
  /// It is display-free on purpose — nothing here touches GTK or Cairo — so
  /// the arithmetic the chart depends on is unit-tested without a display.
  /// One instance describes one draw: width, height, scale and the active
  /// target line do not change while it lives.
  /// </summary>
  internal sealed class TimelineLayout
  {
    /// <summary>Fewest seconds the chart can show, as in the original.</summary>
    internal const int MinScaleSeconds = 4;

    /// <summary>Most seconds the chart can show, as in the original.</summary>
    internal const int MaxScaleSeconds = 120;

    /// <summary>Seconds visible when the window opens, as in the original.</summary>
    internal const int DefaultScaleSeconds = 10;

    // Vertical geometry, all as a fraction of the chart's height, from the
    // original's ChartSubs constants.
    private const double HeaderFraction = 0.2;
    private const double MinorTickTopFraction = 0.1;
    private const double BarHeightFraction = 0.25;
    private const double ReferenceRowFraction = 0.3;
    private const double TargetRowFraction = 0.625;

    /// <summary>A bar never gets thinner than this, so a short line stays visible at a wide scale.</summary>
    private const double MinBarWidth = 1.0;

    /// <param name="width">Width of the drawing surface in pixels.</param>
    /// <param name="height">Height of the drawing surface in pixels.</param>
    /// <param name="scaleSeconds">Seconds to show; clamped into [<see cref="MinScaleSeconds"/>, <see cref="MaxScaleSeconds"/>].</param>
    /// <param name="activeStart">Start of the active target line: the window is centred on it.</param>
    /// <param name="activeEnd">End of the active target line.</param>
    internal TimelineLayout(
      double width, double height, int scaleSeconds, TimeSpan activeStart, TimeSpan activeEnd)
    {
      Width = width;
      Height = height;
      ScaleSeconds = Clamp(scaleSeconds);
      ActiveStart = activeStart;
      ActiveEnd = activeEnd;

      var (start, end) = ScaleWindow(ScaleSeconds, activeStart, activeEnd);
      WindowStart = start;
      WindowEnd = end;
    }

    internal double Width { get; }

    internal double Height { get; }

    /// <summary>Seconds visible across the whole width, already clamped.</summary>
    internal int ScaleSeconds { get; }

    internal TimeSpan ActiveStart { get; }

    internal TimeSpan ActiveEnd { get; }

    /// <summary>First time shown, always a whole second (so the major ticks are whole seconds too).</summary>
    internal TimeSpan WindowStart { get; }

    /// <summary>Last time shown; exactly <see cref="ScaleSeconds"/> after <see cref="WindowStart"/>.</summary>
    internal TimeSpan WindowEnd { get; }

    /// <summary>Pixels between two major ticks, i.e. one second.</summary>
    internal double SecondWidth => Width / ScaleSeconds;

    /// <summary>Bottom of the header strip: the line the labels sit on and the minor ticks reach down to.</summary>
    internal double HeaderHeight => Height * HeaderFraction;

    /// <summary>Top of the minor ticks, which hang from the header line.</summary>
    internal double MinorTickTop => Height * MinorTickTopFraction;

    /// <summary>Height of a dialogue bar.</summary>
    internal double BarHeight => Height * BarHeightFraction;

    /// <summary>Clamp a scale into the range the chart supports.</summary>
    internal static int Clamp(int scaleSeconds) =>
      Math.Clamp(scaleSeconds, MinScaleSeconds, MaxScaleSeconds);

    /// <summary>The visible time window: start and end.</summary>
    internal (TimeSpan Start, TimeSpan End) ScaleWindow() => (WindowStart, WindowEnd);

    /// <summary>
    /// The window <paramref name="scaleSeconds"/> wide centred on the middle
    /// of the active line, its start snapped to a whole second and never
    /// before zero.
    /// </summary>
    internal static (TimeSpan Start, TimeSpan End) ScaleWindow(
      int scaleSeconds, TimeSpan activeStart, TimeSpan activeEnd)
    {
      int scale = Clamp(scaleSeconds);
      double midMs = (activeStart.TotalMilliseconds + activeEnd.TotalMilliseconds) / 2.0;
      // The original rounds both ends to the nearest second; rounding the
      // start alone gives the same window for an even scale and keeps an odd
      // one exactly `scale` seconds wide, which the ticks assume.
      double startMs = Math.Round((midMs - scale * 500.0) / 1000.0, MidpointRounding.AwayFromZero) * 1000.0;
      if (startMs < 0) startMs = 0;   // never show time before the file starts
      return (TimeSpan.FromMilliseconds(startMs), TimeSpan.FromMilliseconds(startMs + scale * 1000.0));
    }

    /// <summary>
    /// The indices of <paramref name="lines"/> that overlap the visible
    /// window, in order. A line that starts before the window and ends after
    /// it counts: it covers every pixel of the chart.
    /// </summary>
    internal IReadOnlyList<int> VisibleLines(IReadOnlyList<RetimerLine> lines)
    {
      var visible = new List<int>();
      for (int i = 0; i < lines.Count; i++)
      {
        var line = lines[i];
        if (line.End > WindowStart && line.Start < WindowEnd) visible.Add(i);
      }
      return visible;
    }

    /// <summary>
    /// Where <paramref name="time"/> falls across the width: 0 at
    /// <see cref="WindowStart"/>, <see cref="Width"/> at
    /// <see cref="WindowEnd"/>. Times outside the window map outside it.
    /// </summary>
    internal double XFor(TimeSpan time) =>
      (time.TotalMilliseconds - WindowStart.TotalMilliseconds) / (ScaleSeconds * 1000.0) * Width;

    /// <summary>
    /// The bar for <paramref name="line"/> in the reference (upper) or target
    /// (lower) row, clamped to at least <see cref="MinBarWidth"/> wide.
    /// </summary>
    internal ChartRect BarRect(RetimerLine line, Side row)
    {
      double x1 = XFor(line.Start);
      double x2 = XFor(line.End);
      double top = Height * (row == Side.Reference ? ReferenceRowFraction : TargetRowFraction);
      return new ChartRect(x1, top, Math.Max(x2 - x1, MinBarWidth), BarHeight);
    }

    /// <summary>The x of every major (one second) tick; the window's own edges are not ticks.</summary>
    internal IReadOnlyList<double> MajorTicks()
    {
      var ticks = new List<double>(ScaleSeconds);
      for (int i = 1; i < ScaleSeconds; i++) ticks.Add(SecondWidth * i);
      return ticks;
    }

    /// <summary>The x of every minor (half second) tick, including the ones before and after the last major tick.</summary>
    internal IReadOnlyList<double> MinorTicks()
    {
      var ticks = new List<double>(ScaleSeconds);
      for (int i = 1; i < ScaleSeconds * 2; i += 2) ticks.Add(SecondWidth * i * 0.5);
      return ticks;
    }

    /// <summary>
    /// How many seconds apart the labelled major ticks are. The wider the
    /// window, the fewer labels fit, exactly as the original graded it.
    /// </summary>
    internal int LabelEvery => ScaleSeconds switch
    {
      >= 90 => 8,
      >= 60 => 6,
      >= 30 => 4,
      >= 10 => 2,
      _ => 1,
    };

    /// <summary>The labelled ticks: where the label is centred and what it reads.</summary>
    internal IReadOnlyList<(double X, string Text)> TickLabels()
    {
      var labels = new List<(double, string)>();
      for (int i = 1; i < ScaleSeconds; i += LabelEvery)
        labels.Add((SecondWidth * i, TickLabel(WindowStart + TimeSpan.FromSeconds(i))));
      return labels;
    }

    /// <summary>A tick label: <c>H:MM:SS</c>, as the original wrote it.</summary>
    internal static string TickLabel(TimeSpan time) => string.Format(
      CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);
  }
}
