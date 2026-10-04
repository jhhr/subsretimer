//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Editor;
using SubsRetimer.UiTests.Harness;
using Xunit;

namespace SubsRetimer.UiTests.Tests
{
  /// <summary>
  /// Phase 8's file drops. GTK 4 delivers no synthesised drags, so the tests
  /// call <c>RetimerWindow.DropFile</c> with the value the drop signal would
  /// carry: a <c>GFile</c> from a file manager, or the text form GTK writes a
  /// file list as.
  /// </summary>
  [Collection(GtkCollection.Name)]
  public class WindowDropTests
  {
    private readonly GtkFixture _gtk;

    public WindowDropTests(GtkFixture gtk) => _gtk = gtk;

    /// <summary>The <c>file://</c> URI of a path, escaped as GTK writes it.</summary>
    private static string Uri(string path) => Gio.FileHelper.NewForPath(path).GetUri()!;

    [GtkFact]
    public async Task DroppingTextWithTwoUris_LoadsTheFirstOnThatSide()
    {
      using var scope = new UiTestScope(_gtk);
      var files = SubtitleFixtures.Pair(scope.TempDir, count: 12);

      var window = await scope.OpenWindowAsync();
      bool taken = false;
      await scope.RunAsync(window, () =>
      {
        // What a drop of two files reaches the handler as: one URI a line.
        var value = new GObject.Value(Uri(files.ReferencePath) + "\r\n" + Uri(files.TargetPath) + "\r\n");
        taken = window.DropFile(Side.Reference, value);
      });

      var shown = scope.Read(() => new
      {
        Reference = window.Engine.ReferenceLines.Count,
        Target = window.Engine.TargetLines.Count,
        Name = window.Engine.Reference?.FileName,
      });

      Assert.True(taken);
      Assert.Equal(12, shown.Reference);
      // The first URI of the drop, not the second.
      Assert.Equal(Path.GetFileName(files.ReferencePath), shown.Name);
      // Only the first file of the drop, and only the side it was dropped on.
      Assert.Equal(0, shown.Target);
    }

    [GtkFact]
    public async Task DroppingAGFile_LoadsItOnTheSideItWasDroppedOn()
    {
      using var scope = new UiTestScope(_gtk);
      var files = SubtitleFixtures.Pair(scope.TempDir, count: 9);

      var window = await scope.OpenWindowAsync();
      bool reference = false, target = false;
      await scope.RunAsync(window, () =>
      {
        // The shape a real drop carries: the drop target asks for a GFile.
        reference = window.DropFile(
          Side.Reference, new GObject.Value((GObject.Object)Gio.FileHelper.NewForPath(files.ReferencePath)));
        target = window.DropFile(
          Side.Target, new GObject.Value((GObject.Object)Gio.FileHelper.NewForPath(files.TargetPath)));
      });

      var shown = scope.Read(() => new
      {
        Reference = window.Engine.ReferenceLines.Count,
        Target = window.Engine.TargetLines.Count,
        HasBoth = window.Engine.HasBoth,
      });

      Assert.True(reference);
      Assert.True(target);
      Assert.Equal(9, shown.Reference);
      Assert.Equal(9, shown.Target);
      Assert.True(shown.HasBoth);
    }

    [GtkFact]
    public async Task DroppingSomethingThatIsNoLocalFile_ChangesNothing()
    {
      using var scope = new UiTestScope(_gtk);

      var window = await scope.OpenWindowAsync();
      bool empty = true, remote = true;
      await scope.RunAsync(window, () =>
      {
        empty = window.DropFile(Side.Target, new GObject.Value("   \r\n"));
        remote = window.DropFile(Side.Target, new GObject.Value("https://example.invalid/subs.srt"));
      });

      var loaded = scope.Read(() => window.Engine.TargetLines.Count);

      // Refused without a dialog: the scope would fail the test on a stray window.
      Assert.False(empty);
      Assert.False(remote);
      Assert.Equal(0, loaded);
    }

    [GtkFact]
    public async Task EachPaneHasADropTargetForFiles()
    {
      using var scope = new UiTestScope(_gtk);
      var window = await scope.OpenWindowAsync();

      var shown = scope.Read(() => new
      {
        ReferenceTypes = window.DropTargetFor(Side.Reference).GetGtypes(),
        TargetTypes = window.DropTargetFor(Side.Target).GetGtypes(),
        ReferenceTakesFiles = window.DropTargetFor(Side.Reference).GetFormats()?.ContainGtype(Gio.FileHelper.GetGType()),
        Actions = window.DropTargetFor(Side.Target).GetActions(),
      });

      Assert.Equal(new[] { Gio.FileHelper.GetGType() }, shown.ReferenceTypes);
      Assert.Equal(new[] { Gio.FileHelper.GetGType() }, shown.TargetTypes);
      Assert.True(shown.ReferenceTakesFiles);   // the formats carry the GType, no mime type
      Assert.Equal(Gdk.DragAction.Copy, shown.Actions);
    }
  }
}
