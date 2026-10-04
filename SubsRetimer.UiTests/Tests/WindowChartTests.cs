//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;
using SubsRetimer.Editor;
using SubsRetimer.UiTests.Harness;
using Xunit;

namespace SubsRetimer.UiTests.Tests
{
  /// <summary>
  /// Phase 7's timeline chart: what it says it is showing, what its clicks do
  /// to the selection, its zoom, and that it really draws. The geometry
  /// itself is unit-tested in <c>SubsRetimer.Tests.TimelineLayoutTests</c>;
  /// here it is checked against what the live widget reports.
  /// </summary>
  [Collection(GtkCollection.Name)]
  public class WindowChartTests
  {
    private readonly GtkFixture _gtk;

    public WindowChartTests(GtkFixture gtk) => _gtk = gtk;

    /// <summary>A loaded window with row 8 selected on the target and its counterpart on the reference.</summary>
    private async Task<(RetimerWindow Window, FixtureFiles Files)> OpenSelectedAsync(UiTestScope scope)
    {
      var files = SubtitleFixtures.Pair(scope.TempDir, count: 20, cuts: (10, 15000));
      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
        window.SelectTarget(8);
        window.SelectClosestOnOtherSide(Side.Target);
      });
      return (window, files);
    }

    [GtkFact]
    public async Task Chart_ShowsTheActiveTargetLineAndAgreesWithTheLayoutClass()
    {
      using var scope = new UiTestScope(_gtk);
      var (window, _) = await OpenSelectedAsync(scope);

      var seen = scope.Read(() =>
      {
        var layout = window.Chart.Layout!;
        var active = window.Engine.TargetLines[window.SelectedTarget];
        // The same arithmetic asked for directly, from the size the widget has.
        var expected = new TimelineLayout(
          layout.Width, layout.Height, window.Chart.ScaleSeconds, active.Start, active.End);
        return new
        {
          layout.WindowStart,
          layout.WindowEnd,
          layout.Width,
          ActiveStart = active.Start,
          ActiveEnd = active.End,
          Reference = layout.VisibleLines(window.Engine.ReferenceLines),
          Target = layout.VisibleLines(window.Engine.TargetLines),
          ExpectedStart = expected.WindowStart,
          ExpectedReference = expected.VisibleLines(window.Engine.ReferenceLines),
          ExpectedTarget = expected.VisibleLines(window.Engine.TargetLines),
          TargetIndex = window.SelectedTarget,
        };
      });

      Assert.True(seen.Width > 0, "the chart was never given a width");
      Assert.Equal(seen.ExpectedStart, seen.WindowStart);
      // The line the window is centred on is inside it, and so is its bar.
      Assert.True(seen.ActiveStart >= seen.WindowStart && seen.ActiveEnd <= seen.WindowEnd);
      Assert.Contains(seen.TargetIndex, seen.Target);
      Assert.Equal(seen.ExpectedTarget, seen.Target);
      Assert.Equal(seen.ExpectedReference, seen.Reference);
      // The target runs 15 s late from row 10 on, so around row 8 both sides
      // still share the window: the chart is only useful when it shows both.
      Assert.NotEmpty(seen.Reference);
    }

    [GtkFact]
    public async Task Chart_HasNoLayoutUntilBothSidesHaveASelection()
    {
      using var scope = new UiTestScope(_gtk);
      var window = await scope.OpenWindowAsync();
      Assert.Null(scope.Read(() => window.Chart.Layout));

      var files = SubtitleFixtures.Pair(scope.TempDir, count: 20, cuts: (10, 15000));
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
      });
      Assert.Null(scope.Read(() => window.Chart.Layout));

      await scope.RunAsync(window, () => window.SelectTarget(3));
      Assert.Null(scope.Read(() => window.Chart.Layout));   // the reference side is still empty

      await scope.RunAsync(window, () => window.SelectReference(3));
      Assert.NotNull(scope.Read(() => window.Chart.Layout));
    }

    [GtkFact]
    public async Task ChartClick_LeftWalksTheTargetForwardAndRightWalksItBack()
    {
      using var scope = new UiTestScope(_gtk);
      var (window, _) = await OpenSelectedAsync(scope);

      await scope.RunAsync(window, () => window.Chart.ClickAt(TimelineChart.ButtonPrimary, 10, 10));
      var forward = scope.Read(() => new
      {
        window.SelectedTarget,
        window.SelectedReference,
        Closest = RetimerEngine.ClosestIndex(
          window.Engine.TargetLines[9].Start, window.Engine.ReferenceLines),
      });
      Assert.Equal(9, forward.SelectedTarget);
      // The reference follows the target, as a right click on a list does.
      Assert.Equal(forward.Closest, forward.SelectedReference);

      await scope.RunAsync(window, () => window.Chart.ClickAt(TimelineChart.ButtonSecondary, 10, 10));
      Assert.Equal(8, scope.Read(() => window.SelectedTarget));

      // The ends of the list are ends: a click there changes nothing.
      await scope.RunAsync(window, () => window.SelectTarget(0));
      await scope.RunAsync(window, () => window.Chart.ClickAt(TimelineChart.ButtonSecondary, 10, 10));
      Assert.Equal(0, scope.Read(() => window.SelectedTarget));

      int last = scope.Read(() => window.Engine.TargetLines.Count - 1);
      await scope.RunAsync(window, () => window.SelectTarget(last));
      await scope.RunAsync(window, () => window.Chart.ClickAt(TimelineChart.ButtonPrimary, 10, 10));
      Assert.Equal(last, scope.Read(() => window.SelectedTarget));
    }

    [GtkFact]
    public async Task ChartClick_MiddleRunsTimeShift()
    {
      using var scope = new UiTestScope(_gtk);
      var (window, _) = await OpenSelectedAsync(scope);

      // Pick a pair that is not aligned yet: row 12 of the target is 15 s late.
      await scope.RunAsync(window, () =>
      {
        window.SelectTarget(12);
        window.SelectReference(12);
      });
      var before = scope.Read(() => new
      {
        Target = window.Engine.TargetLines[12].Start,
        Reference = window.Engine.ReferenceLines[12].Start,
        window.IsDirty,
      });
      Assert.NotEqual(before.Reference, before.Target);
      Assert.False(before.IsDirty);

      await scope.RunAsync(window, () => window.Chart.ClickAt(TimelineChart.ButtonMiddle, 10, 10));

      var after = scope.Read(() => new
      {
        Target = window.Engine.TargetLines[12].Start,
        Reference = window.Engine.ReferenceLines[12].Start,
        window.IsDirty,
      });
      Assert.Equal(after.Reference, after.Target);
      Assert.True(after.IsDirty);
    }

    [GtkFact]
    public async Task Zoom_MovesTheScaleInStepsAndStopsAtTheClamp()
    {
      using var scope = new UiTestScope(_gtk);
      var (window, _) = await OpenSelectedAsync(scope);

      Assert.Equal(TimelineLayout.DefaultScaleSeconds, scope.Read(() => window.Chart.ScaleSeconds));

      await scope.RunAsync(window, () => window.Chart.ZoomIn(TimelineChart.SmallZoomStep));
      Assert.Equal(8, scope.Read(() => window.Chart.ScaleSeconds));
      // The window narrows with the scale, and the layout says so.
      Assert.Equal(TimeSpan.FromSeconds(8), scope.Read(() =>
        window.Chart.Layout!.WindowEnd - window.Chart.Layout!.WindowStart));

      await scope.RunAsync(window, () => window.Chart.ZoomOut(TimelineChart.LargeZoomStep));
      Assert.Equal(18, scope.Read(() => window.Chart.ScaleSeconds));

      await scope.RunAsync(window, () => window.Chart.ZoomIn(1000));
      Assert.Equal(TimelineLayout.MinScaleSeconds, scope.Read(() => window.Chart.ScaleSeconds));

      await scope.RunAsync(window, () => window.Chart.ZoomOut(1000));
      Assert.Equal(TimelineLayout.MaxScaleSeconds, scope.Read(() => window.Chart.ScaleSeconds));
    }

    /// <summary>
    /// Space or Enter on a focused zoom button activates it, which GTK turns
    /// into "clicked", the same signal a plain left click ends in. Each
    /// activation is one small step. GTK shows the button pressed for a
    /// moment first, so "clicked" arrives a quarter of a second later.
    /// </summary>
    [GtkFact]
    public async Task ZoomButtons_ActivatedFromTheKeyboard_ZoomOneSmallStep()
    {
      using var scope = new UiTestScope(_gtk);
      var (window, _) = await OpenSelectedAsync(scope);

      async Task ActivateAndWait(Gtk.Button button, int expected) =>
        await scope.Fixture.RunOnGtkAsync(async () =>
        {
          button.Activate();
          await Pump.WaitUntilAsync(
            () => window.Chart.ScaleSeconds == expected, TimeSpan.FromSeconds(3), "the zoom button's clicked");
          await Pump.DelayAsync(400);   // and no second step follows
          return true;
        });

      await ActivateAndWait(window.Chart.ZoomInButton, TimelineLayout.DefaultScaleSeconds - TimelineChart.SmallZoomStep);
      Assert.Equal(TimelineLayout.DefaultScaleSeconds - TimelineChart.SmallZoomStep, scope.Read(() => window.Chart.ScaleSeconds));

      await ActivateAndWait(window.Chart.ZoomOutButton, TimelineLayout.DefaultScaleSeconds);
      Assert.Equal(TimelineLayout.DefaultScaleSeconds, scope.Read(() => window.Chart.ScaleSeconds));
    }

    /// <summary>Draw <paramref name="text"/> the way a bar's text is drawn, on a surface of its own, and return the pixels.</summary>
    private static byte[] DrawBar(string text)
    {
      using var surface = new Cairo.ImageSurface(Cairo.Format.Argb32, 240, 20);
      var cr = new Cairo.Context(surface);
      cr.SetSourceRgb(0.3, 0.3, 0.3);   // the bar
      cr.Paint();
      TimelineChart.DrawBarText(cr, PangoCairo.Functions.CreateLayout(cr), text, new ChartRect(0, 0, 240, 20));
      surface.Flush();
      return surface.GetData().ToArray();
    }

    /// <summary>
    /// Bar text goes through Pango, which falls back to another font for the
    /// characters the default one lacks. Cairo's toy text API did not: every
    /// Japanese character came out as the same empty box, so Japanese text
    /// drew exactly like any other run of characters "Sans" does not have.
    /// Pango tells them apart whatever fonts this machine has: real glyphs
    /// where a font covers them, boxes with the code point in them where not.
    /// </summary>
    [GtkFact]
    public async Task BarText_DrawsCharactersTheDefaultFontLacks()
    {
      using var scope = new UiTestScope(_gtk);
      await scope.OpenWindowAsync();   // GTK, and with it Pango, is up

      var drawn = scope.Read(() => new
      {
        Japanese = DrawBar("日本語"),
        Unassigned = DrawBar("\U000F0001\U000F0002\U000F0003"),
        Empty = DrawBar(""),
      });

      Assert.NotEqual(drawn.Empty, drawn.Japanese);   // something was drawn at all
      Assert.NotEqual(drawn.Unassigned, drawn.Japanese);
    }

    [GtkFact]
    public async Task Chart_DrawsWithoutThrowing()
    {
      using var scope = new UiTestScope(_gtk);
      var (window, _) = await OpenSelectedAsync(scope);

      // Ask for a repaint and let the frame clock deliver it.
      await scope.Fixture.RunOnGtkAsync(async () =>
      {
        window.Chart.QueueDraw();
        await Pump.FramesAsync(window, 3);
        await Pump.IdleAsync();
        return true;
      });

      var drawn = scope.Read(() => new { window.Chart.DrawCount, window.Chart.LastDrawError });
      Assert.Null(drawn.LastDrawError);
      Assert.True(drawn.DrawCount > 0, "the chart never finished a draw");

      // A PNG of the window with the chart on it, when the run asked for one.
      scope.Read(() => Screenshot.TrySave(window, "chart-timeline") ?? "");
    }
  }
}
