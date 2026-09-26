//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

namespace SubsRetimer.Editor
{
  /// <summary>
  /// The editor's CSS classes and the one provider that defines them.
  /// Colours are fixed light backgrounds with an explicit dark foreground,
  /// the way subs2srs does it, so they stay legible under any GTK theme.
  /// </summary>
  internal static class RetimerStyles
  {
    /// <summary>Row that starts more than <c>RetimerEngine.LargeGap</c> after the previous one.</summary>
    internal const string Gap = "retimer-gap";

    /// <summary>Row with no counterpart in the other file.</summary>
    internal const string Mismatch = "retimer-mismatch";

    internal const string OverlapGood = "retimer-overlap-good";
    internal const string OverlapBad = "retimer-overlap-bad";

    /// <summary>Muted one-line hint shown in an empty pane.</summary>
    internal const string Hint = "retimer-hint";

    /// <summary>The timeline's two zoom buttons: they have to fit into a 64 px strip.</summary>
    internal const string Zoom = "retimer-zoom";

    private const string Css =
      // Row colours. The class sits on the box that fills the cell, so the
      // background covers the whole cell and not just the label's text.
      "." + Gap + " { background-color: #FFD8A8; color: #1A1A1A; }" +
      "." + Mismatch + " { background-color: #E0E0E0; color: #1A1A1A; }" +
      "." + Gap + " label, ." + Mismatch + " label { color: inherit; }" +
      // Selected rows keep their meaning but get a darker shade and an outline.
      "columnview listview > row:selected ." + Gap +
          " { background-color: #F0B060; outline: 2px solid #3584E4; outline-offset: -2px; }" +
      "columnview listview > row:selected ." + Mismatch +
          " { background-color: #BDBDBD; outline: 2px solid #3584E4; outline-offset: -2px; }" +
      // Detail strip: overlap percentage.
      "." + OverlapGood + " { background-color: #C8F0C8; color: #1A1A1A; }" +
      "." + OverlapBad + " { background-color: #F5C0C0; color: #1A1A1A; }" +
      "." + Hint + " { color: alpha(currentColor, 0.65); font-style: italic; }" +
      // The theme's button padding alone is taller than half the chart strip.
      "." + Zoom + " { padding: 0; min-height: 0; min-width: 0; }" +
      // Tight cells, so a list of subtitle lines shows as many rows as it can.
      "columnview > listview > row > cell { padding: 1px 0; margin: 0; }";

    private static bool _installed;

    /// <summary>
    /// Add the provider to the default display once per process. Does
    /// nothing when there is no display (nothing can be shown anyway).
    /// </summary>
    internal static void Install()
    {
      if (_installed) return;
      var display = Gdk.Display.GetDefault();
      if (display == null) return;

      var provider = Gtk.CssProvider.New();
      provider.LoadFromString(Css);
      // Above the theme's own rules, as in subs2srs.
      Gtk.StyleContext.AddProviderForDisplay(display, provider, 800);
      _installed = true;
    }
  }
}
