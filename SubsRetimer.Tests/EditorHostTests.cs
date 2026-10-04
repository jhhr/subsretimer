//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Editor;
using Xunit;

namespace SubsRetimer.Tests
{
  /// <summary>
  /// How <see cref="EditorHost.Probe(Func{bool})"/> tells a missing display
  /// from a missing GTK. The display opener is swapped for a stand-in, so
  /// nothing here loads GTK.
  /// </summary>
  public class EditorHostTests
  {
    /// <summary>What .NET says when libgtk-4 is not installed (measured, first lines).</summary>
    private const string MissingGtk =
      "Unable to load shared library 'libgtk-4.so.1' or one of its dependencies. In order to help diagnose loading problems, " +
      "consider using a tool like strace. If you're using glibc, consider setting the LD_DEBUG environment variable: \n" +
      "libgtk-4.so.1: cannot open shared object file: No such file or directory\n";

    [Fact]
    public void Probe_ADisplayThatOpens_IsReady()
    {
      Assert.Equal(new EditorProbe(EditorStatus.Ready), EditorHost.Probe(() => true));
    }

    [Fact]
    public void Probe_ADisplayThatDoesNotOpen_IsNoDisplay()
    {
      Assert.Equal(new EditorProbe(EditorStatus.NoDisplay), EditorHost.Probe(() => false));
    }

    [Fact]
    public void Probe_ALibraryThatDoesNotLoad_IsNoGtk_WithTheLoadersFirstSentence()
    {
      var probe = EditorHost.Probe(() => throw new DllNotFoundException(MissingGtk));

      Assert.Equal(EditorStatus.NoGtk, probe.Status);
      Assert.Equal("Unable to load shared library 'libgtk-4.so.1' or one of its dependencies.", probe.Detail);
    }

    [Fact]
    public void Probe_AFailedTypeInitialiser_ReportsWhatItWrapped()
    {
      var probe = EditorHost.Probe(() =>
        throw new TypeInitializationException("Gdk.Internal.Display", new EntryPointNotFoundException("gdk_display_open")));

      Assert.Equal(EditorStatus.NoGtk, probe.Status);
      Assert.Equal("gdk_display_open", probe.Detail);
    }
  }
}
