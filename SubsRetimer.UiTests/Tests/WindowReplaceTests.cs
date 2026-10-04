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
    public async Task ThePrompt_AsksAboutOpeningTheNewFile_NotAboutClosing()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var other = OtherFixture(scope);
      var window = await OpenDirtyAsync(scope, files);

      await OpenAsync(scope, window, Side.Target, other.TargetPath, RetimerWindow.ChoiceCancel);
      string? replacing = scope.Read(() => window.LastPrompt);

      await scope.RunIdleAsync(() =>
      {
        window.CloseChoice = () => Task.FromResult(RetimerWindow.ChoiceCancel);
        window.RequestClose();
      });
      string? closing = scope.Read(() => window.LastPrompt);

      Assert.Equal("Save the changes to target.ass before opening target.ass?", replacing);
      Assert.DoesNotContain("closing", replacing);
      Assert.Equal(RetimerWindow.CloseQuestion, closing);
    }

    [GtkFact]
    public async Task OpeningSomethingThatIsNotSubtitles_WhileDirty_ErrorsWithoutAskingOrSaving()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var window = await OpenDirtyAsync(scope, files);
      string video = Path.Combine(scope.TempDir, "episode.mkv");
      File.WriteAllBytes(video, new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0, 0, 0, 0 });
      string notReally = Path.Combine(scope.TempDir, "broken.srt");   // a subtitle name on a directory
      Directory.CreateDirectory(notReally);

      var errors = new List<string>();
      await scope.RunAsync(window, () => window.ErrorShown = (message, detail) => errors.Add(detail));
      // Save is the answer that would write a file for nothing: it must not be asked.
      var (videoLoaded, videoPrompts) = await OpenAsync(scope, window, Side.Target, video, RetimerWindow.ChoiceSave);
      var (dirLoaded, dirPrompts) = await OpenAsync(scope, window, Side.Target, notReally, RetimerWindow.ChoiceSave);

      var after = State(scope, window);
      Assert.False(videoLoaded);
      Assert.False(dirLoaded);
      Assert.Equal(0, videoPrompts + dirPrompts);
      Assert.Equal(2, errors.Count);
      Assert.Contains("episode.mkv is not an .ass, .ssa or .srt file", errors[0]);
      // Nothing was written and the edits are still there, unsaved.
      Assert.Empty(after.Saved);
      Assert.True(after.Dirty);
      Assert.Equal(20, after.Count);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(files.TargetPath)));
    }

    [GtkFact]
    public async Task DroppingSomethingThatIsNotSubtitles_WhileDirty_RefusesTheDropWithoutAsking()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var window = await OpenDirtyAsync(scope, files);
      string video = Path.Combine(scope.TempDir, "episode.mkv");
      File.WriteAllBytes(video, new byte[] { 0x1A, 0x45, 0xDF, 0xA3 });

      int prompts = 0;
      var errors = new List<string>();
      bool taken = true;
      await scope.RunAsync(window, () =>
      {
        window.ErrorShown = (_, detail) => errors.Add(detail);
        window.CloseChoice = () => { prompts++; return Task.FromResult(RetimerWindow.ChoiceSave); };
        taken = window.DropFile(Side.Target, new GObject.Value((GObject.Object)Gio.FileHelper.NewForPath(video)));
      });

      Assert.False(taken);
      Assert.Equal(0, prompts);
      Assert.Single(errors);
      Assert.True(State(scope, window).Dirty);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(files.TargetPath)));
    }

    [GtkFact]
    public async Task DroppingATargetWhileThePromptIsUp_IsRefused()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var other = OtherFixture(scope);
      var third = SubtitleFixtures.Pair(Path.Combine(scope.TempDir, "third"), count: 5, seed: 13);
      var window = await OpenDirtyAsync(scope, files);

      bool? droppedDuringPrompt = null;
      bool dropping = false;
      bool loaded = await scope.Fixture.RunOnGtkAsync(async () =>
      {
        window.CloseChoice = () =>
        {
          // The user drops another file while the question is still open
          // (once: a drop that did get through would ask again from inside).
          if (!dropping)
          {
            dropping = true;
            droppedDuringPrompt = window.DropFile(
              Side.Target, new GObject.Value((GObject.Object)Gio.FileHelper.NewForPath(third.TargetPath)));
          }
          return Task.FromResult(RetimerWindow.ChoiceCancel);
        };
        bool ok = await window.OpenPathAsync(Side.Target, other.TargetPath);
        await Pump.SettleAsync(window);
        return ok;
      });

      // Refused, so GTK shows it as refused instead of taking it and dropping it.
      Assert.False(droppedDuringPrompt);
      Assert.False(loaded);
      var after = State(scope, window);
      Assert.Equal(20, after.Count);
      Assert.True(after.Dirty);
    }

    [GtkFact]
    public async Task ClosingWhileTheReplacePromptIsUp_ClosesOnceItIsAnswered()
    {
      using var scope = new UiTestScope(_gtk);
      var files = Fixture(scope);
      var other = OtherFixture(scope);
      var window = await OpenDirtyAsync(scope, files);

      var asked = new List<string?>();
      await scope.Fixture.RunOnGtkAsync(async () =>
      {
        window.CloseChoice = () =>
        {
          asked.Add(window.LastPrompt);
          // First the replace prompt: the window manager's close button is
          // pressed while it is up, then Cancel. Then the close prompt that
          // the close request has been waiting to ask: Discard.
          if (asked.Count == 1)
          {
            window.RequestClose();
            return Task.FromResult(RetimerWindow.ChoiceCancel);
          }
          return Task.FromResult(RetimerWindow.ChoiceDiscard);
        };
        await window.OpenPathAsync(Side.Target, other.TargetPath);
        await Pump.IdleAsync();
        return true;
      });

      Assert.Equal(2, asked.Count);
      Assert.Contains("before opening", asked[0]);
      Assert.Equal(RetimerWindow.CloseQuestion, asked[1]);
      Assert.False(scope.Read(() => UiTestScope.IsAlive(window)), "the close asked for during the prompt was lost");
    }

    /// <summary>Japanese dialogue in Shift-JIS without a byte-order mark, as a subs2srs user's file often is.</summary>
    private static string ShiftJisFile(string dir, string name, string text)
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      Directory.CreateDirectory(dir);
      string path = Path.Combine(dir, name);
      string content = name.EndsWith(".srt", StringComparison.Ordinal)
        ? "1\n00:00:01,000 --> 00:00:02,500\n" + text + "\n\n2\n00:00:04,000 --> 00:00:05,000\n" + text + "2\n\n"
        : "[Script Info]\nScriptType: v4.00+\n\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n" +
          "Dialogue: 0,0:00:01.00,0:00:02.50,Default,,0,0,0,," + text + "\n";
      File.WriteAllBytes(path, Encoding.GetEncoding("shift_jis").GetBytes(content));
      return path;
    }

    [GtkFact]
    public async Task OpeningATargetThatIsNotValidInItsEncoding_IsRefused_AndTheCommandLinesEncodingIsUsed()
    {
      using var scope = new UiTestScope(_gtk);
      string sjis = ShiftJisFile(Path.Combine(scope.TempDir, "sjis"), "target.ass", "日本語のテキスト");
      var window = await scope.OpenWindowAsync();

      var errors = new List<string>();
      await scope.RunAsync(window, () => window.ErrorShown = (_, detail) => errors.Add(detail));
      var (refused, _) = await OpenAsync(scope, window, Side.Target, sjis, RetimerWindow.ChoiceCancel);

      // Read as UTF-8 the text would be saved back as U+FFFD: not loaded.
      Assert.False(refused);
      var error = Assert.Single(errors);
      Assert.Contains("not valid utf-8", error);
      Assert.Contains("--target-encoding", error);
      Assert.Null(scope.Read(() => window.Engine.Target));

      // With the encoding --target-encoding gave, the same file opens intact.
      await scope.RunAsync(window, () => window.TargetEncoding = Encoding.GetEncoding("shift_jis"));
      var (loaded, _) = await OpenAsync(scope, window, Side.Target, sjis, RetimerWindow.ChoiceCancel);
      Assert.True(loaded);
      Assert.Equal("日本語のテキスト", scope.Read(() => window.Engine.TargetLines[0].DisplayText));
    }

    [GtkFact]
    public async Task OpeningAReference_UsesTheReferenceEncoding()
    {
      using var scope = new UiTestScope(_gtk);
      string sjis = ShiftJisFile(Path.Combine(scope.TempDir, "sjis"), "reference.srt", "字幕");
      var window = await scope.OpenWindowAsync();

      await scope.RunAsync(window, () => window.ReferenceEncoding = Encoding.GetEncoding("shift_jis"));
      var (loaded, _) = await OpenAsync(scope, window, Side.Reference, sjis, RetimerWindow.ChoiceCancel);

      Assert.True(loaded);
      Assert.Equal("字幕", scope.Read(() => window.Engine.ReferenceLines[0].DisplayText));
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
