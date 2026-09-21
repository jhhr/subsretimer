//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;
using SubsRetimer.Editor;
using SubsRetimer.UiTests.Harness;
using Xunit;

namespace SubsRetimer.UiTests.Tests
{
  /// <summary>
  /// Loading over unsaved changes. Opening or dropping a new target throws
  /// the edits away, so it asks the same Save / Discard / Cancel question
  /// closing asks (answered through the <c>CloseChoice</c> seam, so no dialog
  /// is shown). A new reference costs nothing and never asks.
  /// </summary>
  [Collection(GtkCollection.Name)]
  public class WindowReplaceTests
  {
    private readonly GtkFixture _gtk;

    public WindowReplaceTests(GtkFixture gtk) => _gtk = gtk;

    /// <summary>The row the tests shift from: the first one the target runs late on.</summary>
    private const int CutRow = 10;

    /// <summary>How many lines the file that is loaded over the edited one has.</summary>
    private const int ReplacementCount = 7;

    private static FixtureFiles Fixture(UiTestScope scope) =>
      SubtitleFixtures.Pair(scope.TempDir, count: 20, cuts: (CutRow, 15000));

    /// <summary>A second pair, in its own directory and shorter, so a load is unmistakable.</summary>
    private static FixtureFiles OtherFixture(UiTestScope scope) =>
      SubtitleFixtures.Pair(Path.Combine(scope.TempDir, "other"), count: ReplacementCount, seed: 11);

    /// <summary>Open the window with both files loaded and one shift applied, i.e. dirty.</summary>
    private async Task<RetimerWindow> OpenDirtyAsync(UiTestScope scope, FixtureFiles files)
    {
      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
        window.SelectReference(CutRow);
        window.SelectTarget(CutRow);
      });
      await scope.RunAsync(window, window.TimeShift);
      Assert.True(scope.Read(() => window.IsDirty), "the shift left the window clean");
      return window;
    }

    /// <summary>
    /// Answer the prompt with <paramref name="choice"/> and open
    /// <paramref name="path"/> on <paramref name="side"/>, awaiting the load
    /// on the GTK thread. Returns whether the file was loaded and how often
    /// the prompt was answered.
    /// </summary>
    private async Task<(bool Loaded, int Prompts)> OpenAsync(
      UiTestScope scope, RetimerWindow window, Side side, string path, int choice)
    {
      int prompts = 0;
      bool loaded = await scope.Fixture.RunOnGtkAsync(async () =>
      {
        window.CloseChoice = () => { prompts++; return Task.FromResult(choice); };
        bool ok = await window.OpenPathAsync(side, path);
        await Pump.SettleAsync(window);
        return ok;
      });
      return (loaded, prompts);
    }

    /// <summary>What the window shows: the target's name, its length and the edited row.</summary>
    private static (string? Name, int Count, TimeSpan Cut, bool Dirty, string[] Saved) State(
      UiTestScope scope, RetimerWindow window) =>
      scope.Read(() => (
        window.Engine.Target?.FileName,
        window.Engine.TargetLines.Count,
        window.Engine.TargetLines[Math.Min(CutRow, window.Engine.TargetLines.Count - 1)].Start,
        window.IsDirty,
        window.SavedPaths.ToArray()));

    [GtkFact]
    public async Task OpeningANewTargetWhileDirty_Cancel_KeepsTheEditedTarget()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var other = OtherFixture(scope);
      var window = await OpenDirtyAsync(scope, files);

      var shifted = State(scope, window);
      var (loaded, prompts) = await OpenAsync(
        scope, window, Side.Target, other.TargetPath, RetimerWindow.ChoiceCancel);

      var after = State(scope, window);
      Assert.False(loaded);
      Assert.Equal(1, prompts);
      // Nothing moved: the same file, the same length, the same edited timings.
      Assert.Equal(20, after.Count);
      Assert.Equal(shifted.Cut, after.Cut);
      Assert.True(after.Dirty);
      Assert.Empty(after.Saved);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(files.TargetPath)));
    }

    [GtkFact]
    public async Task OpeningANewTargetWhileDirty_Discard_LoadsItAndIsCleanAgain()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var other = OtherFixture(scope);
      var window = await OpenDirtyAsync(scope, files);

      var (loaded, prompts) = await OpenAsync(
        scope, window, Side.Target, other.TargetPath, RetimerWindow.ChoiceDiscard);

      var after = State(scope, window);
      Assert.True(loaded);
      Assert.Equal(1, prompts);
      Assert.Equal(ReplacementCount, after.Count);
      // The dropped edits were never written anywhere.
      Assert.False(after.Dirty);
      Assert.Empty(after.Saved);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(files.TargetPath)));
      // The reference is untouched: only the target was replaced.
      Assert.Equal(20, scope.Read(() => window.Engine.ReferenceLines.Count));
    }

    [GtkFact]
    public async Task OpeningANewTargetWhileDirty_Save_WritesTheRetimedFileThenLoads()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var other = OtherFixture(scope);
      var window = await OpenDirtyAsync(scope, files);

      var shifted = State(scope, window);
      var (loaded, prompts) = await OpenAsync(
        scope, window, Side.Target, other.TargetPath, RetimerWindow.ChoiceSave);

      string expected = Path.GetFullPath(RetimerIO.DefaultOutputPath(files.TargetPath));
      var after = State(scope, window);
      Assert.True(loaded);
      Assert.Equal(1, prompts);
      Assert.Equal(new[] { expected }, after.Saved);
      Assert.True(File.Exists(expected), expected + " was not written");
      // The file on disk carries the edits that were about to be replaced.
      Assert.Equal(shifted.Cut, RetimerIO.Load(expected).Lines[CutRow].Start);
      // And the new file is what the window shows now.
      Assert.Equal(ReplacementCount, after.Count);
      Assert.False(after.Dirty);
    }

    [GtkFact]
    public async Task OpeningANewReferenceWhileDirty_NeverAsks()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var other = OtherFixture(scope);
      var window = await OpenDirtyAsync(scope, files);

      var shifted = State(scope, window);
      // Cancel would stop a load that asked; the reference must not ask at all.
      var (loaded, prompts) = await OpenAsync(
        scope, window, Side.Reference, other.ReferencePath, RetimerWindow.ChoiceCancel);

      var after = State(scope, window);
      Assert.True(loaded);
      Assert.Equal(0, prompts);
      Assert.Equal(ReplacementCount, scope.Read(() => window.Engine.ReferenceLines.Count));
      // The edited target is still there, still dirty.
      Assert.Equal(20, after.Count);
      Assert.Equal(shifted.Cut, after.Cut);
      Assert.True(after.Dirty);
    }

    [GtkFact]
    public async Task DroppingANewTargetWhileDirty_Cancel_KeepsTheEditedTarget()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var other = OtherFixture(scope);
      var window = await OpenDirtyAsync(scope, files);

      var shifted = State(scope, window);
      int prompts = 0;
      bool taken = await scope.Fixture.RunOnGtkAsync(async () =>
      {
        window.CloseChoice = () => { prompts++; return Task.FromResult(RetimerWindow.ChoiceCancel); };
        // The drop signal gets its answer at once; the question is asked after.
        bool ok = window.DropFile(
          Side.Target, new GObject.Value((GObject.Object)Gio.FileHelper.NewForPath(other.TargetPath)));
        await Pump.SettleAsync(window);
        return ok;
      });

      var after = State(scope, window);
      Assert.True(taken);   // the drop is accepted whatever the answer turns out to be
      Assert.Equal(1, prompts);
      Assert.Equal(20, after.Count);
      Assert.Equal(shifted.Cut, after.Cut);
      Assert.True(after.Dirty);
    }

    [GtkFact]
    public async Task DroppingANewTargetWhileClean_LoadsItWithoutAsking()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var other = OtherFixture(scope);

      var window = await scope.OpenWindowAsync();
      int prompts = 0;
      await scope.RunAsync(window, () =>
      {
        window.CloseChoice = () => { prompts++; return Task.FromResult(RetimerWindow.ChoiceCancel); };
        window.LoadTarget(files.TargetPath);
      });
      await scope.RunAsync(window, () => window.DropFile(
        Side.Target, new GObject.Value((GObject.Object)Gio.FileHelper.NewForPath(other.TargetPath))));

      Assert.Equal(0, prompts);
      Assert.Equal(ReplacementCount, scope.Read(() => window.Engine.TargetLines.Count));
    }
  }
}
