//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Text;
using SubsRetimer.Core;

namespace SubsRetimer.Editor
{
  /// <summary>Whether the editor can start here, and if not, which of its two needs is missing.</summary>
  public enum EditorStatus
  {
    /// <summary>GTK 4 loaded and a display opened.</summary>
    Ready,

    /// <summary>GTK 4 loaded, but there is no display to open a window on.</summary>
    NoDisplay,

    /// <summary>GTK 4 itself could not be loaded: not installed, incomplete, or the wrong build.</summary>
    NoGtk
  }

  /// <summary>The answer of <see cref="EditorHost.Probe()"/>.</summary>
  /// <param name="Status">What was found.</param>
  /// <param name="Detail">For <see cref="EditorStatus.NoGtk"/>, the first line of what the loader said; otherwise null.</param>
  public sealed record EditorProbe(EditorStatus Status, string? Detail = null);

  /// <summary>What the command line hands the editor window.</summary>
  /// <param name="Reference">Reference file already loaded, or null to leave the pane empty.</param>
  /// <param name="Target">Target file already loaded, or null to leave the pane empty.</param>
  /// <param name="OutputPath">Where Save writes <paramref name="Target"/> (<c>--output</c>); null for <c>name_retimed.ext</c>.</param>
  /// <param name="ReferenceEncoding">Encoding for reference files opened from the window; null for UTF-8.</param>
  /// <param name="TargetEncoding">Encoding for target files opened from the window; null for UTF-8.</param>
  /// <param name="OnSaved">
  /// Called on the GTK thread with each saved full path as the file is
  /// written, once per path and in the order of the returned list. The
  /// command line prints them from here, so that a program waiting on the
  /// editor learns of a file while the window is still open.
  /// </param>
  public sealed record EditorRequest(
    SubtitleFile? Reference,
    SubtitleFile? Target,
    string? OutputPath = null,
    Encoding? ReferenceEncoding = null,
    Encoding? TargetEncoding = null,
    Action<string>? OnSaved = null);

  /// <summary>
  /// Starts GTK and runs the editor window. The only part of the editor the
  /// command line touches, so that <c>Cli</c> can be tested without GTK.
  /// </summary>
  public static class EditorHost
  {
    public const string ApplicationId = "io.github.jhhr.subsretimer";

    /// <summary>
    /// True when a display can be opened, i.e. when the editor can run.
    /// <see cref="Probe()"/> says why not.
    /// </summary>
    public static bool CanOpenDisplay() => Probe().Status == EditorStatus.Ready;

    /// <summary>
    /// Whether the editor can run here, without starting it.
    ///
    /// GirCore 0.7 gives no gentler route: <c>Gtk.Module.Initialize()</c>
    /// calls <c>gtk_init()</c>, which prints "Failed to open display" and
    /// <em>exits the process</em> when there is none, and every other GTK or
    /// GDK call before it fails to find the shared library at all.
    /// <c>Gdk.Module.Initialize()</c> only installs GirCore's library
    /// resolver, and <c>gdk_display_open</c> then answers the question by
    /// returning null. A display opened here becomes the default display and
    /// is reused by the GTK initialisation that follows.
    /// </summary>
    public static EditorProbe Probe() => Probe(OpenDisplay);

    /// <summary>
    /// <see cref="Probe()"/> around any way of opening the display. A library
    /// that cannot be loaded surfaces as an exception (a
    /// <see cref="DllNotFoundException"/> when libgtk-4 is missing), which is
    /// not the same answer as a display that is not there.
    /// </summary>
    internal static EditorProbe Probe(Func<bool> openDisplay)
    {
      try
      {
        return new EditorProbe(openDisplay() ? EditorStatus.Ready : EditorStatus.NoDisplay);
      }
      catch (Exception ex)
      {
        var cause = ex is TypeInitializationException { InnerException: { } inner } ? inner : ex;
        return new EditorProbe(EditorStatus.NoGtk, FirstLine(cause.Message));
      }
    }

    /// <summary>
    /// <see cref="Probe()"/>, and once the display is open GTK's own
    /// initialisation as well, which is what <c>--check-editor</c> runs. That
    /// step reads GTK's data files (on Windows <c>gtk_init()</c> aborts
    /// without the bundled GSettings schemas), so a broken bundle fails the
    /// check instead of the first real start. Not part of
    /// <see cref="Probe()"/>: GTK belongs to the thread that initialises it,
    /// and a probe must not choose that thread for whoever runs GTK next.
    /// </summary>
    public static EditorProbe ProbeStart()
    {
      var probe = Probe();
      if (probe.Status != EditorStatus.Ready) return probe;
      try
      {
        Gtk.Module.Initialize();
        return probe;
      }
      catch (Exception ex)
      {
        return new EditorProbe(EditorStatus.NoGtk, FirstLine(ex.Message));
      }
    }

    private static bool OpenDisplay()
    {
      Gdk.Module.Initialize();
      if (Gdk.Display.GetDefault() != null) return true;
      return Gdk.Display.Open(null!) != null;
    }

    /// <summary>The first line of a loader message; .NET's DllNotFoundException runs to a dozen.</summary>
    private static string FirstLine(string message)
    {
      string first = message.Split('\n')[0].Trim();
      // "... or one of its dependencies. In order to help diagnose ..." keeps only the first sentence.
      int help = first.IndexOf(" In order to help", StringComparison.Ordinal);
      return help > 0 ? first[..help] : first;
    }

    /// <summary>
    /// Builds the window. A seam for the tests, which need a window that
    /// fails while the application activates.
    /// </summary>
    internal static Func<Gtk.Application, RetimerWindow> CreateWindow = application => new RetimerWindow(application);

    /// <summary>
    /// Open the window with the files already loaded and run the GTK main
    /// loop. Returns the paths saved before the window closed, in order.
    /// </summary>
    /// <exception cref="Exception">
    /// Whatever building the window threw. <c>activate</c> is a native
    /// callback, and GirCore answers an exception escaping one by ending the
    /// process with a stack trace; caught here, it reaches the command line,
    /// which turns it into its own message and exit status.
    /// </exception>
    public static IReadOnlyList<string> Run(EditorRequest request)
    {
      var application = Gtk.Application.New(ApplicationId, Gio.ApplicationFlags.NonUnique);
      RetimerWindow? window = null;
      ExceptionDispatchInfo? failure = null;

      application.OnActivate += (_, _) =>
      {
        try
        {
          window = CreateWindow(application);
          window.PathSaved = request.OnSaved;
          window.ReferenceEncoding = request.ReferenceEncoding;
          window.TargetEncoding = request.TargetEncoding;
          if (request.Reference != null) window.SetFile(Side.Reference, request.Reference);
          if (request.Target != null) window.SetFile(Side.Target, request.Target, request.OutputPath);
          window.Show();
        }
        catch (Exception ex)
        {
          failure = ExceptionDispatchInfo.Capture(ex);
          window?.Destroy();
          application.Quit();
        }
      };

      application.RunWithSynchronizationContext(null);
      failure?.Throw();
      return window?.SavedPaths ?? Array.Empty<string>();
    }
  }
}
