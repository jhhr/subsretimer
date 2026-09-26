//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;
using SubsRetimer.Editor;
using SubsRetimer.UiTests.Harness;
using Xunit;

namespace SubsRetimer.UiTests.Tests
{
  /// <summary>
  /// Phase 3's editing: Time Shift, undo and redo, the star in the title,
  /// Save and Save As, and the prompt that closing with unsaved changes puts
  /// up (answered through the <c>CloseChoice</c> seam, so no dialog is shown).
  /// </summary>
  [Collection(GtkCollection.Name)]
  public class WindowEditTests
  {
    private readonly GtkFixture _gtk;

    public WindowEditTests(GtkFixture gtk) => _gtk = gtk;

    /// <summary>The row the tests shift from: the first one the target runs late on.</summary>
    private const int CutRow = 10;

    private static FixtureFiles Fixture(UiTestScope scope) =>
      SubtitleFixtures.Pair(scope.TempDir, count: 20, cuts: (CutRow, 15000));

    private async Task<RetimerWindow> OpenLoadedAsync(UiTestScope scope, FixtureFiles files)
    {
      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
      });
      return window;
    }

    /// <summary>Select the cut row on both sides and shift, which leaves the window dirty.</summary>
    private async Task ShiftAsync(UiTestScope scope, RetimerWindow window)
    {
      await scope.RunAsync(window, () =>
      {
        window.SelectReference(CutRow);
        window.SelectTarget(CutRow);
      });
      await scope.RunAsync(window, window.TimeShift);
    }

    [GtkFact]
    public async Task TimeShift_MovesTheTargetOntoTheReference_AndUndoRedoStepThroughIt()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var window = await OpenLoadedAsync(scope, files);

      var before = scope.Read(() => new
      {
        Title = window.GetTitle() ?? "",
        window.IsDirty,
        window.CanTimeShift,
        Start = window.Engine.TargetLines[CutRow].Start,
        Above = window.Engine.TargetLines[CutRow - 1].Start,
      });
      Assert.False(before.IsDirty);
      Assert.False(before.CanTimeShift);   // nothing is selected yet
      Assert.DoesNotContain("*", before.Title);
      Assert.Contains("target.ass", before.Title);

      await ShiftAsync(scope, window);

      var shifted = scope.Read(() => new
      {
        Title = window.GetTitle() ?? "",
        window.IsDirty,
        Target = window.Engine.TargetLines[CutRow].Start,
        Reference = window.Engine.ReferenceLines[CutRow].Start,
        Above = window.Engine.TargetLines[CutRow - 1].Start,
        window.Engine.CanUndo,
      });
      Assert.Equal(shifted.Reference, shifted.Target);
      Assert.Equal(before.Above, shifted.Above);   // the shift starts at the selected row
      Assert.True(shifted.IsDirty);
      Assert.StartsWith("*", shifted.Title);
      Assert.True(shifted.CanUndo);

      bool undone = false;
      await scope.RunAsync(window, () => undone = window.Undo());
      var afterUndo = scope.Read(() => new
      {
        Title = window.GetTitle() ?? "",
        Target = window.Engine.TargetLines[CutRow].Start,
        window.IsDirty,
      });
      Assert.True(undone);
      Assert.Equal(before.Start, afterUndo.Target);
      // Undoing back to the loaded state is clean again: the star goes.
      Assert.False(afterUndo.IsDirty);
      Assert.False(afterUndo.Title.StartsWith('*'));

      bool redone = false;
      await scope.RunAsync(window, () => redone = window.Redo());
      var afterRedo = scope.Read(() => window.Engine.TargetLines[CutRow].Start);
      Assert.True(redone);
      Assert.Equal(shifted.Reference, afterRedo);
    }

    [GtkFact]
    public async Task Save_WritesTheDefaultPath_RemembersIt_AndClearsTheStar()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var window = await OpenLoadedAsync(scope, files);
      await ShiftAsync(scope, window);

      bool saved = false;
      await scope.RunAsync(window, async () => saved = await window.SaveAsync());
      Assert.True(saved);

      string expected = Path.GetFullPath(RetimerIO.DefaultOutputPath(files.TargetPath));
      var state = scope.Read(() => new
      {
        Paths = window.SavedPaths.ToArray(),
        Title = window.GetTitle() ?? "",
        window.IsDirty,
        Reference = window.Engine.ReferenceLines[CutRow].Start,
      });

      Assert.Equal(new[] { expected }, state.Paths);
      Assert.True(File.Exists(expected), expected + " was not written");
      Assert.False(state.IsDirty);
      Assert.DoesNotContain("*", state.Title);

      // The file on disk carries the shifted timings, not the loaded ones.
      var written = RetimerIO.Load(expected);
      Assert.Equal(files.Target.Count, written.Lines.Count);
      Assert.Equal(state.Reference, written.Lines[CutRow].Start);
      Assert.Equal(files.Target[0].Start, written.Lines[0].Start);
    }

    [GtkFact]
    public async Task SaveAs_WritesTheChosenPath_AndSaveKeepsWritingThere()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var window = await OpenLoadedAsync(scope, files);
      await ShiftAsync(scope, window);

      string chosen = Path.Combine(scope.TempDir, "chosen.ass");
      bool saved = false;
      await scope.RunAsync(window, () => saved = window.SaveAs(chosen));
      Assert.True(saved);

      var state = scope.Read(() => new
      {
        Paths = window.SavedPaths.ToArray(),
        window.IsDirty,
        Reference = window.Engine.ReferenceLines[CutRow].Start,
      });
      Assert.Equal(new[] { Path.GetFullPath(chosen) }, state.Paths);
      Assert.True(File.Exists(chosen));
      Assert.False(state.IsDirty);
      Assert.Equal(state.Reference, RetimerIO.Load(chosen).Lines[CutRow].Start);

      // Save after Save As writes the chosen file again, with the newer
      // timings, and not the default name: the file the user picked stays
      // the one that is current. It is not a new path to report.
      await scope.RunAsync(window, () => window.Undo());
      bool again = false;
      await scope.RunAsync(window, async () => again = await window.SaveAsync());
      var after = scope.Read(() => new
      {
        Paths = window.SavedPaths.ToArray(),
        window.SavePath,
        window.IsDirty,
        Undone = window.Engine.TargetLines[CutRow].Start,
      });
      Assert.True(again);
      Assert.Equal(Path.GetFullPath(chosen), after.SavePath);
      Assert.Equal(new[] { Path.GetFullPath(chosen) }, after.Paths);
      Assert.False(after.IsDirty);
      Assert.Equal(after.Undone, RetimerIO.Load(chosen).Lines[CutRow].Start);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(files.TargetPath)));
    }

    [GtkFact]
    public async Task ClosingWhileDirty_Cancel_KeepsTheWindowOpen()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var window = await OpenLoadedAsync(scope, files);
      await ShiftAsync(scope, window);

      await scope.RunIdleAsync(() =>
      {
        window.CloseChoice = () => Task.FromResult(RetimerWindow.ChoiceCancel);
        window.RequestClose();
      });

      var state = scope.Read(() => new
      {
        Alive = UiTestScope.IsAlive(window),
        Visible = window.GetVisible(),
        Paths = window.SavedPaths.Count,
        window.IsDirty,
      });
      Assert.True(state.Alive, "Cancel closed the window");
      Assert.True(state.Visible);
      Assert.True(state.IsDirty);
      Assert.Equal(0, state.Paths);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(files.TargetPath)));
    }

    [GtkFact]
    public async Task ClosingWhileDirty_Discard_ClosesWithoutWriting()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var window = await OpenLoadedAsync(scope, files);
      await ShiftAsync(scope, window);

      await scope.RunIdleAsync(() =>
      {
        window.CloseChoice = () => Task.FromResult(RetimerWindow.ChoiceDiscard);
        window.RequestClose();
      });

      var state = scope.Read(() => new
      {
        Alive = UiTestScope.IsAlive(window),
        Visible = window.GetVisible(),
        Paths = window.SavedPaths.Count,
      });
      Assert.False(state.Alive, "Discard left the window open");
      Assert.False(state.Visible);
      Assert.Equal(0, state.Paths);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(files.TargetPath)));
    }

    [GtkFact]
    public async Task ClosingWhileDirty_Save_WritesTheFileAndCloses()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var window = await OpenLoadedAsync(scope, files);
      await ShiftAsync(scope, window);
      var reference = scope.Read(() => window.Engine.ReferenceLines[CutRow].Start);

      await scope.RunIdleAsync(() =>
      {
        window.CloseChoice = () => Task.FromResult(RetimerWindow.ChoiceSave);
        window.RequestClose();
      });

      string expected = Path.GetFullPath(RetimerIO.DefaultOutputPath(files.TargetPath));
      var state = scope.Read(() => new
      {
        Alive = UiTestScope.IsAlive(window),
        Paths = window.SavedPaths.ToArray(),
      });
      Assert.False(state.Alive, "the window stayed open after saving");
      Assert.Equal(new[] { expected }, state.Paths);
      Assert.True(File.Exists(expected));
      Assert.Equal(reference, RetimerIO.Load(expected).Lines[CutRow].Start);
    }
  }
}
