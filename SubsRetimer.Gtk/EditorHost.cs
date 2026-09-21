//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;

namespace SubsRetimer.Editor
{
  /// <summary>
  /// Starts GTK and runs the editor window. The only part of the editor the
  /// command line touches, so that <c>Cli</c> can be tested without GTK.
  /// </summary>
  public static class EditorHost
  {
    public const string ApplicationId = "io.github.jhhr.subsretimer";

    /// <summary>
    /// True when a display can be opened, i.e. when the editor can run.
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
    public static bool CanOpenDisplay()
    {
      try
      {
        Gdk.Module.Initialize();
        if (Gdk.Display.GetDefault() != null) return true;
        return Gdk.Display.Open(null!) != null;
      }
      catch (Exception)
      {
        return false;   // a missing or unusable GTK stack is "no editor", not a crash
      }
    }

    /// <summary>
    /// Open the window with the files already loaded and run the GTK main
    /// loop. Returns the paths saved before the window closed, in order
    /// (nothing can be saved yet).
    /// </summary>
    /// <param name="onSaved">
    /// Called on the GTK thread with each saved full path as the file is
    /// written, once per path and in the order of the returned list. The
    /// command line prints them from here, so that a program waiting on the
    /// editor learns of a file while the window is still open.
    /// </param>
    public static IReadOnlyList<string> Run(
      SubtitleFile? reference, SubtitleFile? target, Action<string>? onSaved = null)
    {
      var application = Gtk.Application.New(ApplicationId, Gio.ApplicationFlags.NonUnique);
      RetimerWindow? window = null;

      application.OnActivate += (_, _) =>
      {
        window = new RetimerWindow(application) { PathSaved = onSaved };
        if (reference != null) window.SetFile(Side.Reference, reference);
        if (target != null) window.SetFile(Side.Target, target);
        window.Show();
      };

      application.RunWithSynchronizationContext(null);
      return window?.SavedPaths ?? Array.Empty<string>();
    }
  }
}
