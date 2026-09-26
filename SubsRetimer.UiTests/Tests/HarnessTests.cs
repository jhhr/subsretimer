//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.UiTests.Harness;
using Xunit;

namespace SubsRetimer.UiTests.Tests
{
  /// <summary>
  /// The scope's own promise: a window a test leaves open fails that test,
  /// is destroyed so the next test does not inherit it, and never hides the
  /// failure the test was already unwinding with.
  /// </summary>
  [Collection(GtkCollection.Name)]
  public class HarnessTests
  {
    private readonly GtkFixture _gtk;

    public HarnessTests(GtkFixture gtk) => _gtk = gtk;

    /// <summary>Open a toplevel the scope knows nothing about, as an error dialog would be.</summary>
    private void LeakAWindow() =>
      _gtk.RunOnGtk(() =>
      {
        var stray = Gtk.Window.New();
        stray.SetTitle("left open by the test");
        stray.Show();
      });

    [GtkFact]
    public void ALeakedWindow_AloneFailsTheTest_AndIsDestroyed()
    {
      int before = _gtk.RunOnGtk(UiTestScope.CountToplevels);

      var failure = Record.Exception(() =>
      {
        using var scope = new UiTestScope(_gtk);
        LeakAWindow();
      });

      Assert.NotNull(failure);
      Assert.Contains("1 toplevel window(s) still open after the test", failure!.Message);
      Assert.Equal(before, _gtk.RunOnGtk(UiTestScope.CountToplevels));
    }

    [GtkFact]
    public async Task ALeakedWindow_DoesNotHideTheFailureTheTestThrew()
    {
      int before = _gtk.RunOnGtk(UiTestScope.CountToplevels);

      var failure = await Record.ExceptionAsync(async () =>
      {
        using var scope = new UiTestScope(_gtk);
        var window = await scope.OpenWindowAsync();
        LeakAWindow();
        await scope.RunAsync(window, () => { });
        throw new InvalidOperationException("the assertion the test really failed on");
      });

      Assert.NotNull(failure);
      // The test's own failure is what the report leads to; the leak rides along.
      string report = failure!.ToString();
      Assert.Contains("the assertion the test really failed on", report);
      Assert.Contains("still open after the test", report);
      Assert.Equal(before, _gtk.RunOnGtk(UiTestScope.CountToplevels));
    }

    [GtkFact]
    public async Task NoLeak_LeavesTheTestsOwnFailureAlone()
    {
      var failure = await Record.ExceptionAsync(async () =>
      {
        using var scope = new UiTestScope(_gtk);
        await scope.OpenWindowAsync();
        throw new InvalidOperationException("only this");
      });

      Assert.IsType<InvalidOperationException>(failure);
      Assert.Equal("only this", failure!.Message);
    }
  }
}
