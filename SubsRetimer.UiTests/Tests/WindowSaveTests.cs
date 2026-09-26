//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using SubsRetimer.Core;
using SubsRetimer.Editor;
using SubsRetimer.UiTests.Harness;
using Xunit;

namespace SubsRetimer.UiTests.Tests
{
  /// <summary>
  /// Where Save writes: the <c>--output</c> path, else where Save As last
  /// wrote, else <c>name_retimed.ext</c>, asking before it replaces a file
  /// there that the window did not write. The prompts are answered through
  /// the <c>CloseChoice</c> and <c>OverwriteChoice</c> seams and errors are
  /// collected through <c>ErrorShown</c>, so no dialog is shown.
  /// </summary>
  [Collection(GtkCollection.Name)]
  public class WindowSaveTests
  {
    private readonly GtkFixture _gtk;

    public WindowSaveTests(GtkFixture gtk) => _gtk = gtk;

    private const int CutRow = 10;

    private static FixtureFiles Fixture(UiTestScope scope) =>
      SubtitleFixtures.Pair(scope.TempDir, count: 20, cuts: (CutRow, 15000));

    /// <summary>A window with both files and one shift, i.e. dirty; <paramref name="output"/> plays <c>--output</c>.</summary>
    private static async Task<RetimerWindow> OpenShiftedAsync(
      UiTestScope scope, FixtureFiles files, string? output = null)
    {
      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () =>
      {
        window.SetFile(Side.Reference, RetimerIO.Load(files.ReferencePath));
        window.SetFile(Side.Target, RetimerIO.Load(files.TargetPath), output);
        window.SelectReference(CutRow);
        window.SelectTarget(CutRow);
      });
      await scope.RunAsync(window, window.TimeShift);
      return window;
    }

    [GtkFact]
    public async Task Save_WithAnOutputPath_WritesThere_AndReplacesItWithoutAsking()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      string output = Path.Combine(scope.TempDir, "out", "chosen.ass");
      Directory.CreateDirectory(Path.GetDirectoryName(output)!);
      File.WriteAllText(output, "an earlier run");
      var window = await OpenShiftedAsync(scope, files, output);

      int asked = 0;
      bool saved = false;
      await scope.RunAsync(window, async () =>
      {
        window.OverwriteChoice = _ => { asked++; return Task.FromResult(false); };
        saved = await window.SaveAsync();
      });

      var state = scope.Read(() => new
      {
        Paths = window.SavedPaths.ToArray(),
        Shifted = window.Engine.TargetLines[CutRow].Start,
      });
      Assert.True(saved);
      // --output named the file, exactly as it does for --auto: no question.
      Assert.Equal(0, asked);
      Assert.Equal(new[] { output }, state.Paths);
      Assert.Equal(state.Shifted, RetimerIO.Load(output).Lines[CutRow].Start);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(files.TargetPath)));
    }

    [GtkFact]
    public async Task Save_OverARetimedFileItDidNotWrite_AsksFirst()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      string retimed = Path.GetFullPath(RetimerIO.DefaultOutputPath(files.TargetPath));
      File.WriteAllText(retimed, "fixed by hand in an earlier session");
      var window = await OpenShiftedAsync(scope, files);

      var asked = new List<string>();
      bool answer = false;
      bool saved = true;
      await scope.RunAsync(window, async () =>
      {
        window.OverwriteChoice = path => { asked.Add(path); return Task.FromResult(answer); };
        saved = await window.SaveAsync();
      });

      // No: the earlier file stays exactly as it was and the edits stay unsaved.
      Assert.False(saved);
      Assert.Equal(new[] { retimed }, asked);
      Assert.Equal("fixed by hand in an earlier session", File.ReadAllText(retimed));
      Assert.True(scope.Read(() => window.IsDirty));
      Assert.Empty(scope.Read(() => window.SavedPaths.ToArray()));
      Assert.Equal("Replace target_retimed.ass?", scope.Read(() => window.LastPrompt));

      // Yes: replaced.
      answer = true;
      await scope.RunAsync(window, async () => saved = await window.SaveAsync());
      Assert.True(saved);
      Assert.Equal(2, asked.Count);
      Assert.Equal(scope.Read(() => window.Engine.TargetLines[CutRow].Start), RetimerIO.Load(retimed).Lines[CutRow].Start);

      // The window wrote it now, so saving it again does not ask again.
      await scope.RunAsync(window, () => window.Undo());
      await scope.RunAsync(window, async () => saved = await window.SaveAsync());
      Assert.True(saved);
      Assert.Equal(2, asked.Count);
    }

    [GtkFact]
    public async Task Save_WithNothingThere_WritesWithoutAsking()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var window = await OpenShiftedAsync(scope, files);

      int asked = 0;
      await scope.RunAsync(window, async () =>
      {
        window.OverwriteChoice = _ => { asked++; return Task.FromResult(false); };
        await window.SaveAsync();
      });

      Assert.Equal(0, asked);
      Assert.True(File.Exists(RetimerIO.DefaultOutputPath(files.TargetPath)));
    }

    [GtkFact]
    public async Task ClosingAfterSaveAs_Save_WritesTheChosenFile()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var window = await OpenShiftedAsync(scope, files);
      string chosen = Path.Combine(scope.TempDir, "final.ass");
      await scope.RunAsync(window, () => window.SaveAs(chosen));

      // More edits after Save As, then close and answer Save.
      await scope.RunAsync(window, () =>
      {
        window.SelectReference(CutRow + 2);
        window.SelectTarget(CutRow + 2);
      });
      await scope.RunAsync(window, window.TimeShift);
      var latest = scope.Read(() => window.Engine.TargetLines[CutRow + 2].Start);

      await scope.RunIdleAsync(() =>
      {
        window.CloseChoice = () => Task.FromResult(RetimerWindow.ChoiceSave);
        window.RequestClose();
      });

      Assert.False(scope.Read(() => UiTestScope.IsAlive(window)), "the window stayed open after saving");
      // The file the user chose has the edits; nothing went to the default name.
      Assert.Equal(latest, RetimerIO.Load(chosen).Lines[CutRow + 2].Start);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(files.TargetPath)));
      Assert.Equal(new[] { Path.GetFullPath(chosen) }, scope.Read(() => window.SavedPaths.ToArray()));
    }

    [GtkFact]
    public async Task SaveAs_TheOtherFormatsExtension_IsRefusedWithAnError()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);   // the target is .ass
      var window = await OpenShiftedAsync(scope, files);
      string wrong = Path.Combine(scope.TempDir, "target.srt");

      var errors = new List<string>();
      bool saved = true;
      await scope.RunAsync(window, () =>
      {
        window.ErrorShown = (message, detail) => errors.Add(message + " " + detail);
        saved = window.SaveAs(wrong);
      });

      Assert.False(saved);
      Assert.False(File.Exists(wrong));
      var error = Assert.Single(errors);
      Assert.Contains("ASS subtitles are saved as .ass or .ssa, not .srt", error);
      // Nothing changed: still dirty, and Save still means the default name.
      Assert.True(scope.Read(() => window.IsDirty));
      Assert.Equal(
        Path.GetFullPath(RetimerIO.DefaultOutputPath(files.TargetPath)),
        scope.Read(() => window.SavePath));
    }

    [GtkFact]
    public async Task ANewTarget_IsSavedUnderItsOwnDefaultName_NotTheLastSaveAs()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var other = SubtitleFixtures.Pair(Path.Combine(scope.TempDir, "other"), count: 7, seed: 11);
      var window = await OpenShiftedAsync(scope, files, output: Path.Combine(scope.TempDir, "from-cli.ass"));
      await scope.RunAsync(window, () => window.SaveAs(Path.Combine(scope.TempDir, "chosen.ass")));

      await scope.RunAsync(window, () => window.LoadTarget(other.TargetPath));

      Assert.Equal(
        Path.GetFullPath(RetimerIO.DefaultOutputPath(other.TargetPath)),
        scope.Read(() => window.SavePath));
    }

    [GtkFact]
    public async Task Save_RepaintsNothingButTheTitle_AndAutoAlignRepaintsOnce()
    {
      using var scope = new UiTestScope(_gtk);
      var files = SubtitleFixtures.Pair(
        scope.TempDir, count: 24, seed: 5, silenceBefore: new[] { 5, 17 },
        cuts: new[] { (8, 15000d), (16, 15000d) });
      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
      });

      int before = scope.Read(() => window.RefreshCount);
      IReadOnlyList<AlignmentSegment> segments = Array.Empty<AlignmentSegment>();
      await scope.RunAsync(window, () => segments = window.AutoAlign());
      var aligned = scope.Read(() => new { window.RefreshCount, Title = window.GetTitle() ?? "", Gray = window.RowState(Side.Target, 20) });

      // Two segments moved (the first one's offset is zero), one repaint.
      Assert.Equal(3, segments.Count);
      Assert.Equal(before + 1, aligned.RefreshCount);
      Assert.StartsWith("*", aligned.Title);
      Assert.NotEqual(RowState.Mismatch, aligned.Gray);   // the one repaint saw the final timings

      await scope.RunAsync(window, async () => await window.SaveAsync());
      var saved = scope.Read(() => new { window.RefreshCount, Title = window.GetTitle() ?? "" });
      Assert.Equal(aligned.RefreshCount, saved.RefreshCount);
      Assert.False(saved.Title.StartsWith('*'), saved.Title);
    }
  }
}
