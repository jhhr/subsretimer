//  Copyright (C) 2012 Christopher Brochtrup
//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  Port of Subs Re-Timer 1.0's ChartSubs control to GTK 4 and Cairo.

using SubsRetimer.Core;

namespace SubsRetimer.Editor
{
  /// <summary>
  /// The timeline strip under the menu: the reference lines as bars in the
  /// upper row, the target lines in the lower row, over a one-second tick
  /// scale centred on the active target line, with two zoom buttons at its
  /// left.
  ///
  /// It is a view like the rest of the window and computes nothing but its
  /// own geometry, which lives in <see cref="TimelineLayout"/>. It owns its
  /// widgets instead of subclassing one, the way <see cref="LineListView"/>
  /// does; <see cref="Widget"/> is what the window packs.
  /// </summary>
  internal sealed class TimelineChart
  {
    /// <summary>Height of the whole strip in pixels, as in the original.</summary>
    internal const int ChartHeight = 64;

    /// <summary>Seconds a plain (left) click on a zoom button adds or removes.</summary>
    internal const int SmallZoomStep = 2;

    /// <summary>Seconds a right click on a zoom button adds or removes.</summary>
    internal const int LargeZoomStep = 10;

    internal const uint ButtonPrimary = 1;
    internal const uint ButtonMiddle = 2;
    internal const uint ButtonSecondary = 3;

    // Fixed light colours, as the row CSS has them: the chart is drawn, not
    // themed, and the plan rules out dark-theme variants.
    private static readonly (double R, double G, double B) Background = Rgb(0xFF, 0xFF, 0xFF);
    private static readonly (double R, double G, double B) Ticks = Rgb(0xA9, 0xA9, 0xA9);
    private static readonly (double R, double G, double B) LabelText = Rgb(0x1A, 0x1A, 0x1A);
    private static readonly (double R, double G, double B) BarInactive = Rgb(0x96, 0x96, 0x96);
    private static readonly (double R, double G, double B) ReferenceActive = Rgb(0x22, 0x8B, 0x22);
    private static readonly (double R, double G, double B) TargetActive = Rgb(0x41, 0x69, 0xE1);
    private static readonly (double R, double G, double B) BarText = Rgb(0xFF, 0xFF, 0xFF);

    private const string FontFamily = "Sans";
    private const double LabelFontSize = 9.0;
    private const double BarFontSize = 10.0;

    private readonly Gtk.Box _root = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);
    private readonly Gtk.DrawingArea _area = Gtk.DrawingArea.New();
    private readonly Gtk.Button _zoomIn = Gtk.Button.NewWithLabel("+");
    private readonly Gtk.Button _zoomOut = Gtk.Button.NewWithLabel("-");
    private readonly Gtk.DrawingAreaDrawFunc _draw;

    private IReadOnlyList<RetimerLine> _reference = Array.Empty<RetimerLine>();
    private IReadOnlyList<RetimerLine> _target = Array.Empty<RetimerLine>();
    private int _referenceActive = -1;
    private int _targetActive = -1;

    internal TimelineChart()
    {
      _area.SetContentHeight(ChartHeight);
      _area.SetHexpand(true);
      // Never vertically: the strip is a fixed 64 px and the lists below it
      // take every pixel the window can spare.
      _area.SetVexpand(false);
      // Held in a field: the callback lives as long as the widget does.
      _draw = (_, cr, width, height) => OnDraw(cr, width, height);
      _area.SetDrawFunc(_draw);

      var zoom = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
      zoom.SetHomogeneous(true);
      foreach (var (button, zoomIn) in new[] { (_zoomIn, true), (_zoomOut, false) })
      {
        button.SetSizeRequest(28, -1);
        button.AddCssClass(RetimerStyles.Zoom);
        button.SetTooltipText(zoomIn
          ? "Show less time: -2 s, right click -10 s, middle click the smallest scale"
          : "Show more time: +2 s, right click +10 s, middle click the largest scale");
        // Capture phase and "any button": GtkButton itself only reacts to the
        // primary button, and its own gesture would claim that one first.
        var clicks = Gtk.GestureClick.New();
        clicks.SetButton(0);
        clicks.SetPropagationPhase(Gtk.PropagationPhase.Capture);
        clicks.OnPressed += (sender, _) => OnZoomPressed(sender.GetCurrentButton(), zoomIn);
        button.AddController(clicks);
        zoom.Append(button);
      }
      _root.Append(zoom);

      foreach (uint button in new[] { ButtonPrimary, ButtonMiddle, ButtonSecondary })
      {
        var clicks = Gtk.GestureClick.New();
        clicks.SetButton(button);
        clicks.OnPressed += (_, args) => ClickAt(button, args.X, args.Y);
        _area.AddController(clicks);
      }
      _root.Append(_area);
      _root.SetSizeRequest(-1, ChartHeight);
      _root.SetVexpand(false);
    }

    /// <summary>The strip to pack into the window: the zoom buttons and the drawing area.</summary>
    internal Gtk.Widget Widget => _root;

    /// <summary>Seconds visible across the chart, always within the clamp.</summary>
    internal int ScaleSeconds { get; private set; } = TimelineLayout.DefaultScaleSeconds;

    /// <summary>Called with the GDK button number when the chart itself is clicked.</summary>
    internal Action<uint>? Clicked { get; set; }

    /// <summary>How often the chart has finished a draw. A draw that threw does not count.</summary>
    internal int DrawCount { get; private set; }

    /// <summary>The last exception a draw raised, or null. A broken chart must not take the window down.</summary>
    internal string? LastDrawError { get; private set; }

    /// <summary>Show fewer seconds, down to <see cref="TimelineLayout.MinScaleSeconds"/>.</summary>
    internal void ZoomIn(int step) => SetScale(ScaleSeconds - step);

    /// <summary>Show more seconds, up to <see cref="TimelineLayout.MaxScaleSeconds"/>.</summary>
    internal void ZoomOut(int step) => SetScale(ScaleSeconds + step);

    /// <summary>
    /// The geometry of the chart as it stands, or null while either side has
    /// no selection, when the original draws no bars and no labels.
    /// </summary>
    internal TimelineLayout? Layout => BuildLayout(_area.GetWidth(), _area.GetHeight());

    /// <summary>The lines of one side. The window hands over the engine's own lists.</summary>
    internal void SetLines(Side side, IReadOnlyList<RetimerLine> lines)
    {
      if (side == Side.Reference) _reference = lines;
      else _target = lines;
    }

    /// <summary>The selected row of one side, or -1. The window is centred on the target's.</summary>
    internal void SetActive(Side side, int index)
    {
      if (side == Side.Reference) _referenceActive = index;
      else _targetActive = index;
    }

    /// <summary>Ask for a repaint. Cheap: GTK coalesces this into the next frame.</summary>
    internal void QueueDraw() => _area.QueueDraw();

    /// <summary>
    /// A click on the chart surface, by GDK button number. The original's
    /// chart acts on the button alone, so <paramref name="x"/> and
    /// <paramref name="y"/> are passed on for the gesture's shape and for
    /// tests, not used to decide anything.
    /// </summary>
    internal void ClickAt(uint button, double x, double y) => Clicked?.Invoke(button);

    /// <summary>A click on a zoom button: 2 s, 10 s on the right button, the whole way on the middle one.</summary>
    private void OnZoomPressed(uint button, bool zoomIn)
    {
      switch (button)
      {
        case ButtonSecondary:
          SetScale(zoomIn ? ScaleSeconds - LargeZoomStep : ScaleSeconds + LargeZoomStep);
          break;
        case ButtonMiddle:
          SetScale(zoomIn ? TimelineLayout.MinScaleSeconds : TimelineLayout.MaxScaleSeconds);
          break;
        default:
          SetScale(zoomIn ? ScaleSeconds - SmallZoomStep : ScaleSeconds + SmallZoomStep);
          break;
      }
    }

    private void SetScale(int seconds)
    {
      int clamped = TimelineLayout.Clamp(seconds);
      if (clamped == ScaleSeconds) return;
      ScaleSeconds = clamped;
      QueueDraw();
    }

    /// <summary>The layout for a surface of this size, or null when there is nothing to centre on.</summary>
    private TimelineLayout? BuildLayout(double width, double height)
    {
      if (_referenceActive < 0 || _referenceActive >= _reference.Count) return null;
      if (_targetActive < 0 || _targetActive >= _target.Count) return null;

      var active = _target[_targetActive];
      return new TimelineLayout(width, height, ScaleSeconds, active.Start, active.End);
    }

    private void OnDraw(Cairo.Context cr, int width, int height)
    {
      try
      {
        var layout = BuildLayout(width, height);
        // The ticks do not depend on the selection, so they are drawn from a
        // window at zero when there is none, exactly as the original does.
        var geometry = layout
          ?? new TimelineLayout(width, height, ScaleSeconds, TimeSpan.Zero, TimeSpan.Zero);

        DrawScale(cr, geometry);
        if (layout != null)
        {
          DrawBars(cr, layout, Side.Reference, _reference, _referenceActive, ReferenceActive);
          DrawBars(cr, layout, Side.Target, _target, _targetActive, TargetActive);
          DrawLabels(cr, layout);
        }
        DrawCount++;
      }
      catch (Exception ex)
      {
        // A chart that cannot draw must not kill the window: a draw function
        // runs from a native callback, where an exception ends the process.
        LastDrawError = ex.ToString();
      }
    }

    /// <summary>The white background, the header line and both kinds of tick.</summary>
    private static void DrawScale(Cairo.Context cr, TimelineLayout layout)
    {
      SetColour(cr, Background);
      cr.Rectangle(0, 0, layout.Width, layout.Height);
      cr.Fill();

      SetColour(cr, Ticks);
      cr.LineWidth = 1.0;

      // Major ticks run the full height, minor ticks hang from the header.
      foreach (double x in layout.MajorTicks())
      {
        cr.MoveTo(Crisp(x), 0);
        cr.LineTo(Crisp(x), layout.Height);
      }
      foreach (double x in layout.MinorTicks())
      {
        cr.MoveTo(Crisp(x), layout.MinorTickTop);
        cr.LineTo(Crisp(x), layout.HeaderHeight);
      }
      cr.MoveTo(0, Crisp(layout.HeaderHeight));
      cr.LineTo(layout.Width, Crisp(layout.HeaderHeight));
      cr.Stroke();
    }

    /// <summary>One row of dialogue bars, the active line in its own colour.</summary>
    private static void DrawBars(
      Cairo.Context cr, TimelineLayout layout, Side row,
      IReadOnlyList<RetimerLine> lines, int activeIndex, (double R, double G, double B) activeColour)
    {
      cr.SelectFontFace(FontFamily, Cairo.FontSlant.Normal, Cairo.FontWeight.Normal);
      cr.SetFontSize(BarFontSize);

      foreach (int index in layout.VisibleLines(lines))
      {
        var rect = layout.BarRect(lines[index], row);

        SetColour(cr, index == activeIndex ? activeColour : BarInactive);
        cr.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
        cr.Fill();

        // A hairline in the background colour at the start of the bar keeps
        // two adjacent lines of dialogue apart.
        SetColour(cr, Background);
        cr.LineWidth = 1.0;
        cr.MoveTo(Crisp(rect.X), rect.Y);
        cr.LineTo(Crisp(rect.X), rect.Y + rect.Height);
        cr.Stroke();

        DrawBarText(cr, lines[index].DisplayText, rect);
      }
    }

    /// <summary>The line's text inside its bar, clipped to it.</summary>
    private static void DrawBarText(Cairo.Context cr, string text, ChartRect rect)
    {
      if (text.Length == 0 || rect.Width < 4) return;

      cr.Save();
      cr.Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
      cr.Clip();
      SetColour(cr, BarText);
      cr.TextExtents(text, out var extents);
      cr.MoveTo(rect.X + 2, Baseline(rect.Y + rect.Height / 2.0, extents));
      cr.ShowText(text);
      cr.Restore();
    }

    /// <summary>The time over every labelled major tick, on an opaque patch so the tick does not run through it.</summary>
    private static void DrawLabels(Cairo.Context cr, TimelineLayout layout)
    {
      cr.SelectFontFace(FontFamily, Cairo.FontSlant.Normal, Cairo.FontWeight.Normal);
      cr.SetFontSize(LabelFontSize);
      double centre = layout.HeaderHeight / 2.0;

      foreach (var (x, text) in layout.TickLabels())
      {
        cr.TextExtents(text, out var extents);
        double left = x - extents.Width / 2.0 - extents.XBearing;
        double baseline = Baseline(centre, extents);

        SetColour(cr, Background);
        cr.Rectangle(left + extents.XBearing - 2, baseline + extents.YBearing - 1,
          extents.Width + 4, extents.Height + 2);
        cr.Fill();

        SetColour(cr, LabelText);
        cr.MoveTo(left, baseline);
        cr.ShowText(text);
      }
    }

    /// <summary>The baseline that centres a run of text's ink on <paramref name="centreY"/>.</summary>
    private static double Baseline(double centreY, Cairo.TextExtents extents) =>
      centreY - extents.YBearing - extents.Height / 2.0;

    /// <summary>Put a one pixel wide line on a pixel instead of across two of them.</summary>
    private static double Crisp(double x) => Math.Floor(x) + 0.5;

    private static void SetColour(Cairo.Context cr, (double R, double G, double B) colour) =>
      cr.SetSourceRgb(colour.R, colour.G, colour.B);

    private static (double R, double G, double B) Rgb(int r, int g, int b) =>
      (r / 255.0, g / 255.0, b / 255.0);
  }
}
