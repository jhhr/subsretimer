//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  Ported from subs2srs.UiTests/Harness/UiTestScope.cs (GPL-3.0-or-later).

using System.Runtime.CompilerServices;
using SubsRetimer.Editor;
using Xunit;

namespace SubsRetimer.UiTests.Harness
{
  /// <summary>
  /// One test's windows and files. It opens windows on the GTK thread, runs
  /// actions there and lets the loop settle afterwards, and on dispose
  /// screenshots what is still open, destroys it and fails the test if a
  /// window (a forgotten dialog, say) outlived it.
  /// </summary>
  internal sealed class UiTestScope : IDisposable
  {
    private readonly GtkFixture _gtk;
    private readonly List<Gtk.Window> _open = new();
    private readonly int _toplevelsBefore;
    private readonly string _name;

    /// <param name="gtk">The shared main loop.</param>
    /// <param name="name">The test's name; it names the screenshots and the temp directory.</param>
    internal UiTestScope(GtkFixture gtk, [CallerMemberName] string name = "test")
    {
      _gtk = gtk;
      _name = name;
      _toplevelsBefore = gtk.RunOnGtk(CountToplevels);
      TempDir = Path.Combine(
        Path.GetTempPath(), "subsretimer-uitests", name + "-" + Guid.NewGuid().ToString("N")[..8]);
      Directory.CreateDirectory(TempDir);
    }

    /// <summary>A directory of this test's own: fixtures go in, saved files come out.</summary>
    internal string TempDir { get; }

    internal GtkFixture Fixture => _gtk;

    /// <summary>Build the editor window on the GTK thread, show it and wait until it is drawn.</summary>
    internal Task<RetimerWindow> OpenWindowAsync() => OpenAsync(() => new RetimerWindow(_gtk.App));

    /// <summary>Build a window on the GTK thread, show it and wait until it settled.</summary>
    internal Task<T> OpenAsync<T>(Func<T> ctor) where T : Gtk.Window
    {
      return _gtk.RunOnGtkAsync(async () =>
      {
        var window = ctor();
        _open.Add(window);
        window.Show();
        await Pump.SettleAsync(window);
        return window;
      });
    }

    /// <summary>
    /// Run <paramref name="action"/> on the GTK thread and let the loop
    /// settle, so the rows are re-bound and the strip repainted before the
    /// test looks. Everything that changes the engine goes through here.
    /// </summary>
    internal Task RunAsync(Gtk.Window window, Action action)
    {
      return _gtk.RunOnGtkAsync(async () =>
      {
        action();
        await Pump.SettleAsync(window);
        return true;
      });
    }

    /// <summary>
    /// Run <paramref name="action"/> on the GTK thread and only wait for the
    /// loop to go idle: for actions that may destroy the window, which then
    /// never draws another frame.
    /// </summary>
    internal Task RunIdleAsync(Action action)
    {
      return _gtk.RunOnGtkAsync(async () =>
      {
        action();
        await Pump.IdleAsync();
        return true;
      });
    }

    /// <summary>Read something off the GTK thread. Queries only: it does not pump.</summary>
    internal T Read<T>(Func<T> func) => _gtk.RunOnGtk(func);

    /// <summary>True while GTK still lists the window as a toplevel, i.e. while it is not destroyed.</summary>
    internal static bool IsAlive(Gtk.Window window)
    {
      var list = Gtk.Window.GetToplevels();
      uint n = list.GetNItems();
      nint handle = window.Handle.DangerousGetHandle();
      for (uint i = 0; i < n; i++)
      {
        var item = list.GetObject(i);
        if (item != null && item.Handle.DangerousGetHandle() == handle) return true;
      }
      return false;
    }

    /// <summary>How many toplevel windows GTK has now.</summary>
    internal static int CountToplevels() => (int)Gtk.Window.GetToplevels().GetNItems();

    /// <summary>Toplevels this test added and has not got rid of.</summary>
    internal int OpenWindowCount => _gtk.RunOnGtk(CountToplevels) - _toplevelsBefore;

    public void Dispose()
    {
      int screenshots = 0;
      _gtk.RunOnGtk(() =>
      {
        foreach (var window in _open.ToArray())
        {
          if (!IsAlive(window)) { _open.Remove(window); continue; }
          // A PNG of the window as the test left it, if anyone asked for one.
          Screenshot.TrySave(window, screenshots++ == 0 ? _name : _name + "-" + screenshots);
          try { window.Destroy(); } catch { /* it is going away either way */ }
          _open.Remove(window);
        }
      });
      // Let the destroy notifications drain before counting.
      _gtk.RunOnGtkAsync(() => Pump.IdleAsync()).GetAwaiter().GetResult();

      int remaining = OpenWindowCount;
      try { Directory.Delete(TempDir, true); } catch { /* diagnostics only */ }

      Assert.True(remaining == 0, $"{remaining} toplevel window(s) still open after the test");
    }
  }
}
