//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

namespace SubsRetimer
{
  /// <summary>
  /// Points GLib, GTK and GdkPixbuf at the data files shipped next to
  /// <c>subsretimer.exe</c> by <c>dist/windows/bundle-gtk.ps1</c>: without
  /// them <c>gtk_init()</c> aborts on the missing GSettings schemas.
  ///
  /// Only variables that are unset are set, so a developer running against a
  /// full MSYS2 installation keeps their own environment, and only when the
  /// bundled layout is actually there, so a plain <c>dotnet run</c> is
  /// unaffected. Must run before any GTK type is touched; on anything but
  /// Windows it does nothing.
  /// </summary>
  internal static class WindowsRuntimeSetup
  {
    public static void Apply()
    {
      if (!OperatingSystem.IsWindows()) return;

      string baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
      string share = Path.Combine(baseDir, "share");
      string schemas = Path.Combine(share, "glib-2.0", "schemas");
      string loadersDir = Path.Combine(baseDir, "lib", "gdk-pixbuf-2.0", "2.10.0", "loaders");
      string loadersCache = Path.Combine(baseDir, "lib", "gdk-pixbuf-2.0", "2.10.0", "loaders.cache");

      // share/ also carries the icon theme the window's icon name is looked up in.
      if (Directory.Exists(share))
        SetIfUnset("XDG_DATA_DIRS", share);
      if (File.Exists(Path.Combine(schemas, "gschemas.compiled")))
        SetIfUnset("GSETTINGS_SCHEMA_DIR", schemas);
      if (File.Exists(loadersCache))
      {
        SetIfUnset("GDK_PIXBUF_MODULE_FILE", loadersCache);
        // The bundled loaders.cache holds bare file names, relative to this.
        SetIfUnset("GDK_PIXBUF_MODULEDIR", loadersDir);
      }

      // Software rendering is the most predictable default on Windows: the
      // GL and Vulkan paths render blank or crash on old GPUs, in VMs and
      // over Remote Desktop.
      SetIfUnset("GSK_RENDERER", "cairo");
    }

    private static void SetIfUnset(string name, string value)
    {
      if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
        Environment.SetEnvironmentVariable(name, value);
    }
  }
}
