//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  Ported from subs2srs.UiTests/Harness/GtkFixture.cs (GPL-3.0-or-later).

namespace SubsRetimer.UiTests.Harness
{
  /// <summary>
  /// Owns the single GTK main loop of the test process. The loop runs on a
  /// background thread; tests marshal work onto it with
  /// <see cref="RunOnGtk{T}"/> and <see cref="RunOnGtkAsync{T}"/>.
  ///
  /// Rules: never block (<c>.Wait()</c>/<c>.Result</c>) inside a lambda that
  /// runs on the GTK thread, and never open a dialog that spins its own loop.
  /// </summary>
  public sealed class GtkFixture : IDisposable
  {
    /// <summary>Application id of the test process; <c>NonUnique</c>, so it never talks to a running editor.</summary>
    public const string ApplicationId = "io.github.jhhr.subsretimer.uitests";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly Thread? _thread;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Gtk.Application? _app;

    /// <summary>True when the loop is running; false when there is no display and every test skips.</summary>
    public bool Available { get; }

    /// <summary>The application every window is built with.</summary>
    public Gtk.Application App =>
      _app ?? throw new InvalidOperationException(Display.SkipReason);

    /// <summary>Managed id of the GTK thread, to tell re-entrant calls from marshalled ones.</summary>
    public int MainThreadId { get; private set; }

    public GtkFixture()
    {
      // Software rendering is the portable choice (Xvfb, VMs, CI).
      if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GSK_RENDERER")))
        Environment.SetEnvironmentVariable("GSK_RENDERER", "cairo");

      // xUnit builds the collection fixture even when every test in it skips,
      // so the display check has to guard the application as well.
      Available = Display.Available;
      if (!Available) return;

      _thread = new Thread(MainLoop)
      {
        Name = "GTK main loop",
        IsBackground = true,
      };
      _thread.Start();

      if (!_ready.Task.Wait(DefaultTimeout))
        throw new TimeoutException("The GTK application did not activate within " + DefaultTimeout + ".");
      _ready.Task.GetAwaiter().GetResult();   // rethrow a startup failure
    }

    private void MainLoop()
    {
      try
      {
        var app = Gtk.Application.New(ApplicationId, Gio.ApplicationFlags.NonUnique);
        _app = app;
        app.OnActivate += (_, _) =>
        {
          MainThreadId = Thread.CurrentThread.ManagedThreadId;
          app.Hold();   // keep the loop alive with zero windows
          _ready.TrySetResult();
        };
        // Installs GirCore's main-loop synchronization context on this thread,
        // so a continuation after an await inside a GTK lambda comes back here.
        app.RunWithSynchronizationContext(null!);
      }
      catch (Exception ex)
      {
        _ready.TrySetException(ex);
      }
    }

    public bool IsOnGtkThread => Thread.CurrentThread.ManagedThreadId == MainThreadId;

    /// <summary>Run a function on the GTK thread and wait for its result here.</summary>
    public T RunOnGtk<T>(Func<T> func, TimeSpan? timeout = null)
    {
      if (IsOnGtkThread) return func();

      var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
      GLib.Functions.IdleAdd(0, () =>
      {
        try { tcs.TrySetResult(func()); }
        catch (Exception ex) { tcs.TrySetException(ex); }
        return false;
      });
      if (!tcs.Task.Wait(timeout ?? DefaultTimeout))
        throw new TimeoutException("The GTK thread did not complete the request in time.");
      return tcs.Task.GetAwaiter().GetResult();
    }

    public void RunOnGtk(Action action, TimeSpan? timeout = null)
      => RunOnGtk(() => { action(); return true; }, timeout);

    /// <summary>
    /// Start an async function on the GTK thread (its continuations stay on
    /// that thread) and wait for it to finish here.
    /// </summary>
    public Task<T> RunOnGtkAsync<T>(Func<Task<T>> func, TimeSpan? timeout = null)
    {
      var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
      GLib.Functions.IdleAdd(0, () =>
      {
        try
        {
          func().ContinueWith(t =>
          {
            if (t.IsFaulted) tcs.TrySetException(t.Exception!.InnerExceptions);
            else if (t.IsCanceled) tcs.TrySetCanceled();
            else tcs.TrySetResult(t.Result);
          }, TaskScheduler.Default);
        }
        catch (Exception ex) { tcs.TrySetException(ex); }
        return false;
      });
      return WithTimeout(tcs.Task, timeout ?? DefaultTimeout);
    }

    public Task RunOnGtkAsync(Func<Task> func, TimeSpan? timeout = null)
      => RunOnGtkAsync(async () => { await func(); return true; }, timeout);

    private static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout)
    {
      var done = await Task.WhenAny(task, Task.Delay(timeout));
      if (done != task)
        throw new TimeoutException($"The GTK operation did not complete within {timeout}.");
      return await task;
    }

    public void Dispose()
    {
      if (!Available) return;
      try
      {
        RunOnGtk(() =>
        {
          App.Release();   // the Hold() from OnActivate
          App.Quit();
        }, TimeSpan.FromSeconds(10));
      }
      catch
      {
        // Best effort: the loop is torn down with the process anyway.
      }
      _thread?.Join(TimeSpan.FromSeconds(10));
    }
  }
}
