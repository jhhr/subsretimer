//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  Ported from subs2srs.UiTests/Harness/Pump.cs (GPL-3.0-or-later).

namespace SubsRetimer.UiTests.Harness
{
  /// <summary>
  /// Lets the GTK main loop run while a test waits for it to settle. Every
  /// method here must be awaited on the GTK thread (inside
  /// <see cref="GtkFixture.RunOnGtkAsync{T}"/>); none of them blocks.
  /// </summary>
  public static class Pump
  {
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Poll <paramref name="condition"/> every 16 ms until it holds, or throw at the deadline.</summary>
    public static Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null, string? what = null)
    {
      if (condition()) return Task.CompletedTask;

      var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
      GLib.Functions.TimeoutAdd(0, 16, () =>
      {
        try
        {
          if (condition()) { tcs.TrySetResult(); return false; }
          if (DateTime.UtcNow > deadline)
          {
            tcs.TrySetException(new TimeoutException(
              $"Condition not met within {timeout ?? DefaultTimeout}: {what ?? "(unnamed)"}"));
            return false;
          }
          return true;
        }
        catch (Exception ex)
        {
          tcs.TrySetException(ex);
          return false;
        }
      });
      return tcs.Task;
    }

    /// <summary>Resolve once the main loop has gone idle.</summary>
    public static Task IdleAsync()
    {
      var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      GLib.Functions.IdleAdd(0, () => { tcs.TrySetResult(); return false; });
      return tcs.Task;
    }

    /// <summary>Resolve after <paramref name="ms"/> milliseconds of main-loop time.</summary>
    public static Task DelayAsync(uint ms)
    {
      var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      GLib.Functions.TimeoutAdd(0, ms, () => { tcs.TrySetResult(); return false; });
      return tcs.Task;
    }

    /// <summary>
    /// Wait until the window is mapped and two frames have been drawn, then
    /// one more idle. After this the list rows are bound and their cells can
    /// be read.
    /// </summary>
    public static async Task SettleAsync(Gtk.Window window, TimeSpan? timeout = null)
    {
      await WaitUntilAsync(() => window.GetMapped(), timeout, "window mapped");
      await FramesAsync(window, 2, timeout);
      await IdleAsync();
    }

    /// <summary>Wait for <paramref name="frames"/> frame-clock ticks on the widget.</summary>
    public static Task FramesAsync(Gtk.Widget widget, int frames, TimeSpan? timeout = null)
    {
      var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      int remaining = frames;
      try
      {
        widget.AddTickCallback((_, _) =>
        {
          if (--remaining <= 0) { tcs.TrySetResult(); return false; }
          return true;
        });
      }
      catch
      {
        return DelayAsync(100);   // no frame clock: fall back to wall time
      }

      // Safety net: a hidden or unmapped widget never ticks.
      var deadline = timeout ?? DefaultTimeout;
      GLib.Functions.TimeoutAdd(0, (uint)deadline.TotalMilliseconds, () =>
      {
        tcs.TrySetResult();
        return false;
      });
      return tcs.Task;
    }
  }
}
