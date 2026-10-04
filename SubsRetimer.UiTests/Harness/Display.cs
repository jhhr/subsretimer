//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Editor;
using Xunit;

namespace SubsRetimer.UiTests.Harness
{
  /// <summary>
  /// Whether this process can open a display, asked once and remembered.
  ///
  /// It has to be answered before anything else touches GTK:
  /// <c>gtk_init()</c> <em>exits</em> a display-less process, so a fixture that
  /// started the application first would take the whole test run down instead
  /// of skipping.
  /// </summary>
  internal static class Display
  {
    /// <summary>Why the GTK tests skip when there is no display.</summary>
    internal const string SkipReason =
      "no display: run the UI tests as GSK_RENDERER=cairo xvfb-run -a dotnet test SubsRetimer.UiTests/SubsRetimer.UiTests.csproj";

    private static readonly Lazy<bool> Check = new(EditorHost.CanOpenDisplay);

    /// <summary>True when a display can be opened, i.e. when the windows can be built.</summary>
    internal static bool Available => Check.Value;
  }

  /// <summary>
  /// A <see cref="FactAttribute"/> that skips instead of failing when no
  /// display is available. xUnit 2.9 cannot skip from inside a running test,
  /// so the decision is made once, while the tests are discovered.
  /// </summary>
  public sealed class GtkFactAttribute : FactAttribute
  {
    public GtkFactAttribute()
    {
      if (!Display.Available) Skip = Display.SkipReason;
    }
  }
}
