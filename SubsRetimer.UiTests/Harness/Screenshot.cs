//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  Ported from subs2srs.UiTests/Harness/Screenshot.cs (GPL-3.0-or-later).

namespace SubsRetimer.UiTests.Harness
{
  /// <summary>
  /// Optional PNG of a window's render tree, written only when
  /// <c>SUBSRETIMER_UITEST_ARTIFACTS</c> names a directory. Screenshots are
  /// diagnostics, never checks: this never throws and never asserts. Must be
  /// called on the GTK thread.
  /// </summary>
  public static class Screenshot
  {
    /// <summary>The directory PNGs go to, or null when the environment variable is unset.</summary>
    public static string? ArtifactDir
    {
      get
      {
        string? dir = Environment.GetEnvironmentVariable("SUBSRETIMER_UITEST_ARTIFACTS");
        return string.IsNullOrWhiteSpace(dir) ? null : dir;
      }
    }

    /// <summary>Save <paramref name="window"/> as <paramref name="name"/>.png. Returns the path, or null when nothing was written.</summary>
    public static string? TrySave(Gtk.Window window, string name)
    {
      string? dir = ArtifactDir;
      if (dir == null) return null;

      try
      {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, SanitizeName(name) + ".png");

        int w = window.GetWidth();
        int h = window.GetHeight();
        if (w <= 0 || h <= 0) return null;

        var paintable = Gtk.WidgetPaintable.New(window);
        var snapshot = Gtk.Snapshot.New();
        paintable.Snapshot(snapshot, w, h);
        var node = snapshot.ToNode();
        if (node == null) return null;

        Gsk.Renderer? renderer = null;
        try { renderer = ((Gtk.Native)window).GetRenderer(); } catch { }
        bool ownRenderer = false;
        if (renderer == null)
        {
          renderer = Gsk.CairoRenderer.New();
          var display = Gdk.Display.GetDefault();
          if (display == null || !renderer.RealizeForDisplay(display)) return null;
          ownRenderer = true;
        }

        var rect = Graphene.Rect.Alloc();
        rect.Init(0, 0, w, h);
        var texture = renderer.RenderTexture(node, rect);
        bool ok = texture.SaveToPng(path);
        if (ownRenderer) renderer.Unrealize();
        return ok ? path : null;
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"[screenshot] {name}: {ex.Message}");
        return null;
      }
    }

    private static string SanitizeName(string name)
    {
      foreach (char c in Path.GetInvalidFileNameChars())
        name = name.Replace(c, '_');
      return name;
    }
  }
}
