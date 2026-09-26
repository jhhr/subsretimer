//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Editor;
using Xunit;

namespace SubsRetimer.Tests
{
  /// <summary>
  /// The desktop entry has to match the running window, or a desktop shows
  /// the window with a generic icon and apart from its launcher. GTK 4 sends
  /// the application id as the Wayland app_id, and a compositor looks for
  /// <c>&lt;app_id&gt;.desktop</c>; on X11 the window's WM_CLASS is
  /// "subsretimer" (measured with xprop), which StartupWMClass names.
  /// </summary>
  public class DesktopEntryTests
  {
    /// <summary>The repository root: the directory that holds Directory.Build.props.</summary>
    private static string RepositoryRoot()
    {
      for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) return dir.FullName;
      throw new DirectoryNotFoundException("no Directory.Build.props above " + AppContext.BaseDirectory);
    }

    private static Dictionary<string, string> Entry(string path) =>
      File.ReadAllLines(path)
        .Where(line => line.Contains('=') && !line.StartsWith('#'))
        .Select(line => line.Split('=', 2))
        .ToDictionary(kv => kv[0].Trim(), kv => kv[1].Trim());

    [Fact]
    public void TheDesktopEntry_IsNamedAfterTheApplicationId_AndNamesTheWindowsClassAndIcon()
    {
      string root = RepositoryRoot();
      string path = Path.Combine(root, "dist", EditorHost.ApplicationId + ".desktop");
      Assert.True(File.Exists(path), path + " is missing: the window's app_id finds no desktop entry");

      var entry = Entry(path);
      Assert.Equal("subsretimer", entry["StartupWMClass"]);
      Assert.Equal(RetimerWindow.AppIconName, entry["Icon"]);
      Assert.Equal("subsretimer", entry["Exec"].Split(' ')[0]);

      // make install puts it where a desktop looks, under the same name.
      string makefile = File.ReadAllText(Path.Combine(root, "Makefile"));
      Assert.Contains("DESKTOP   = " + EditorHost.ApplicationId + ".desktop", makefile);
      Assert.Contains("install -Dm644 dist/$(DESKTOP) \"$(APPDIR)/$(DESKTOP)\"", makefile);
    }
  }
}
