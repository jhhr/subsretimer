//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using SubsRetimer.Core;
using SubsRetimer.Editor;
using SubsRetimer.UiTests.Harness;
using Xunit;

namespace SubsRetimer.UiTests.Tests
{
  /// <summary>
  /// One list on its own, in a plain window: how its store is rebuilt, and
  /// that the in-place refresh reaches every row GTK has on screen however
  /// GTK recycled the cells to get there.
  /// </summary>
  [Collection(GtkCollection.Name)]
  public class LineListViewTests
  {
    private readonly GtkFixture _gtk;

    public LineListViewTests(GtkFixture gtk) => _gtk = gtk;

    private static readonly Regex StartText = new(@"^\d+:\d\d:\d\d\.\d\d$", RegexOptions.Compiled);

    /// <summary>A list in a scrolled window of its own, shown and settled.</summary>
    private static async Task<(LineListView List, Gtk.Window Window, Gtk.ScrolledWindow Scroller)> OpenListAsync(UiTestScope scope)
    {
      LineListView list = null!;
      Gtk.ScrolledWindow scroller = null!;
      var window = await scope.OpenAsync(() =>
      {
        list = new LineListView();
        scroller = Gtk.ScrolledWindow.New();
        scroller.SetChild(list.View);
        var w = Gtk.Window.New();
        w.SetDefaultSize(420, 360);
        w.SetChild(scroller);
        return w;
      });
      return (list, window, scroller);
    }

    private static List<RetimerLine> Lines(int count, int seed) =>
      SubtitleFixtures.Dialogue(count, seed, 800);

    [GtkFact]
    public async Task SetLines_RebuildsTheStoreWithOneItemsChanged()
    {
      using var scope = new UiTestScope(_gtk);
      var (list, window, _) = await OpenListAsync(scope);

      int signals = 0;
      await scope.RunAsync(window, () =>
      {
        list.Store.OnItemsChanged += (_, _) => signals++;
        list.SetLines(Lines(300, 3));
      });
      Assert.Equal(1, signals);
      Assert.Equal(300u, scope.Read(() => list.Store.GetNItems()));

      // A shorter file replaces the rows in the same single step.
      await scope.RunAsync(window, () => list.SetLines(Lines(12, 4)));
      Assert.Equal(2, signals);
      Assert.Equal(12u, scope.Read(() => list.Store.GetNItems()));
    }

    /// <summary>
    /// Every mapped Start label in the list, read straight from the widget
    /// tree: what the user sees, whether or not the list knows the cell.
    /// </summary>
    private static List<string> StartsOnScreen(Gtk.Widget root)
    {
      var found = new List<string>();
      void Walk(Gtk.Widget widget)
      {
        if (widget is Gtk.Label label && widget.GetMapped())
        {
          string text = label.GetText();
          if (StartText.IsMatch(text)) found.Add(text);
        }
        for (var child = widget.GetFirstChild(); child != null; child = child.GetNextSibling())
          Walk(child);
      }
      Walk(root);
      return found;
    }

    /// <summary>
    /// GTK does not unbind a cell before it binds another one to the same row
    /// (measured: under scrolling a cell is unbound at a position another
    /// cell already holds). If that unbind removed the other cell's entry,
    /// the row would stay on screen but out of the list's reach, and a shift
    /// would leave it showing the old time. Random scrolls and reloads, and
    /// after each one every line is shifted by an hour: no Start on screen
    /// may still show the time before.
    /// </summary>
    [GtkFact]
    public async Task Refresh_AfterScrollingAndReloading_RewritesEveryRowOnScreen()
    {
      using var scope = new UiTestScope(_gtk);
      var (list, window, scroller) = await OpenListAsync(scope);
      var rnd = new Random(1);
      var lines = Lines(600, 7);
      await scope.RunAsync(window, () => list.SetLines(lines));

      var stale = new List<string>();
      for (int round = 0; round < 80; round++)
      {
        await scope.Fixture.RunOnGtkAsync(async () =>
        {
          switch (rnd.Next(0, 4))
          {
            case 0:
              lines = Lines(rnd.Next(50, 600), rnd.Next());
              list.SetLines(lines);
              break;
            case 1:
              var adjustment = scroller.GetVadjustment();
              adjustment.SetValue(adjustment.GetValue() + rnd.Next(-500, 500));
              break;
            default:
              list.ScrollTo(rnd.Next(0, lines.Count), focus: false);
              break;
          }
          await Pump.FramesAsync(window, rnd.Next(1, 3));
          await Pump.IdleAsync();

          // Every line an hour later, then the list's own refresh: nothing
          // is pumped in between, so no cell can be bound afresh meanwhile.
          foreach (var line in lines) { line.Start += TimeSpan.FromHours(1); line.End += TimeSpan.FromHours(1); }
          list.Refresh();
          foreach (string text in StartsOnScreen(list.View))
            if (text.StartsWith("0:", StringComparison.Ordinal)) stale.Add($"round {round}: {text}");
          foreach (var line in lines) { line.Start -= TimeSpan.FromHours(1); line.End -= TimeSpan.FromHours(1); }
          list.Refresh();
          return true;
        });
      }

      Assert.True(stale.Count == 0, "rows on screen that Refresh did not reach:\n" + string.Join("\n", stale));
    }
  }
}
