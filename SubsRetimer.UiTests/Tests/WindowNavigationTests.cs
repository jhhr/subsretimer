//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;
using SubsRetimer.Editor;
using SubsRetimer.UiTests.Harness;
using Xunit;

namespace SubsRetimer.UiTests.Tests
{
  /// <summary>
  /// Phase 4's navigation, through the handlers the keys and the mouse call:
  /// GirCore 0.7 exposes no event synthesis, so the delivery of a key press
  /// itself stays a manual check.
  /// </summary>
  [Collection(GtkCollection.Name)]
  public class WindowNavigationTests
  {
    private readonly GtkFixture _gtk;

    public WindowNavigationTests(GtkFixture gtk) => _gtk = gtk;

    private async Task<(RetimerWindow Window, FixtureFiles Files)> OpenLoadedAsync(UiTestScope scope)
    {
      // Silences before rows 6 and 14 give both lists two orange rows.
      var files = SubtitleFixtures.Pair(scope.TempDir, count: 20, cuts: (10, 15000));
      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
      });
      return (window, files);
    }

    [GtkFact]
    public async Task SelectClosestOnOtherSide_PicksTheNearestStart_AndAnswersMinusOneWithNoSelection()
    {
      using var scope = new UiTestScope(_gtk);
      var (window, _) = await OpenLoadedAsync(scope);

      // Nothing is selected, so there is nothing to be close to.
      int none = 0;
      await scope.RunAsync(window, () => none = window.SelectClosestOnOtherSide(Side.Reference));
      Assert.Equal(-1, none);
      Assert.Equal(-1, scope.Read(() => window.SelectedTarget));

      await scope.RunAsync(window, () => window.SelectReference(12));
      int fromReference = 0;
      await scope.RunAsync(window, () => fromReference = window.SelectClosestOnOtherSide(Side.Reference));

      var state = scope.Read(() => new
      {
        window.SelectedTarget,
        Expected = RetimerEngine.ClosestIndex(
          window.Engine.ReferenceLines[12].Start, window.Engine.TargetLines),
      });
      Assert.Equal(state.Expected, fromReference);
      Assert.Equal(state.Expected, state.SelectedTarget);

      // The same from the target side, which selects on the reference side.
      await scope.RunAsync(window, () => window.SelectTarget(4));
      int fromTarget = 0;
      await scope.RunAsync(window, () => fromTarget = window.SelectClosestOnOtherSide(Side.Target));

      var back = scope.Read(() => new
      {
        window.SelectedReference,
        Expected = RetimerEngine.ClosestIndex(
          window.Engine.TargetLines[4].Start, window.Engine.ReferenceLines),
      });
      Assert.Equal(back.Expected, fromTarget);
      Assert.Equal(back.Expected, back.SelectedReference);
    }

    /// <summary>
    /// A Time Shift that moves target rows back past the rows above them
    /// leaves the list out of order. Left/Right from a reference line must
    /// still land on the target line that starts with it.
    /// </summary>
    [GtkFact]
    public async Task SelectClosestOnOtherSide_AfterAShiftLeftTheTargetOutOfOrder_FindsTheMovedLine()
    {
      using var scope = new UiTestScope(_gtk);
      // 40 lines; the target is five minutes late, and from row 30 ten, so
      // no target line starts with reference row 0 until the shift.
      var files = SubtitleFixtures.Pair(
        scope.TempDir, count: 40, silenceBefore: Array.Empty<int>(), cuts: new[] { (0, 300000d), (30, 300000d) });
      var window = await scope.OpenWindowAsync();
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
        window.SelectReference(0);
        window.SelectTarget(30);
      });
      // Target row 30 onto reference row 0: rows 30-39 now start before row 1.
      await scope.RunAsync(window, window.TimeShift);
      Assert.False(scope.Read(() => RetimerEngine.IsSortedByStart(window.Engine.TargetLines)));

      int found = -1;
      await scope.RunAsync(window, () =>
      {
        window.SelectReference(0);
        found = window.SelectClosestOnOtherSide(Side.Reference);
      });

      var state = scope.Read(() => new
      {
        window.SelectedTarget,
        Reference = window.Engine.ReferenceLines[0].Start,
        Target = window.Engine.TargetLines[30].Start,
      });
      Assert.Equal(state.Reference, state.Target);
      Assert.Equal(30, found);
      Assert.Equal(30, state.SelectedTarget);
    }

    [GtkFact]
    public async Task JumpToGap_WalksTheOrangeRowsForwardsAndBackwards()
    {
      using var scope = new UiTestScope(_gtk);
      var (window, _) = await OpenLoadedAsync(scope);

      int[] orange = scope.Read(() => Enumerable.Range(0, window.Engine.ReferenceLines.Count)
        .Where(i => window.RowState(Side.Reference, i) == RowState.Gap)
        .ToArray());
      Assert.Equal(2, orange.Length);   // the fixture's two silences

      int first = 0, second = 0, past = 0, back = 0, before = 0;
      await scope.RunAsync(window, () => first = window.JumpToGap(Side.Reference, forward: true));
      Assert.Equal(orange[0], first);
      // The counterpart on the other side comes along.
      Assert.True(scope.Read(() => window.SelectedTarget) >= 0);

      await scope.RunAsync(window, () => second = window.JumpToGap(Side.Reference, forward: true));
      Assert.Equal(orange[1], second);

      // Nothing orange after the last one: -1, and the selection stays put.
      await scope.RunAsync(window, () => past = window.JumpToGap(Side.Reference, forward: true));
      Assert.Equal(-1, past);
      Assert.Equal(orange[1], scope.Read(() => window.SelectedReference));

      await scope.RunAsync(window, () => back = window.JumpToGap(Side.Reference, forward: false));
      Assert.Equal(orange[0], back);

      await scope.RunAsync(window, () => before = window.JumpToGap(Side.Reference, forward: false));
      Assert.Equal(-1, before);
      Assert.Equal(orange[0], scope.Read(() => window.SelectedReference));
    }

    [GtkFact]
    public async Task ActivateAction_RefusesADisabledOrUnknownAction()
    {
      using var scope = new UiTestScope(_gtk);
      var window = await scope.OpenWindowAsync();

      // An empty window can do none of these.
      var empty = scope.Read(() => new
      {
        TimeShift = window.ActivateAction(RetimerWindow.ActionTimeShift),
        AutoAlign = window.ActivateAction(RetimerWindow.ActionAutoAlign),
        Undo = window.ActivateAction(RetimerWindow.ActionUndo),
        Save = window.ActivateAction(RetimerWindow.ActionSave),
        Unknown = window.ActivateAction("no-such-action"),
      });
      Assert.False(empty.TimeShift);
      Assert.False(empty.AutoAlign);
      Assert.False(empty.Undo);
      Assert.False(empty.Save);
      Assert.False(empty.Unknown);

      var files = SubtitleFixtures.Pair(scope.TempDir, count: 20, cuts: (10, 15000));
      await scope.RunAsync(window, () =>
      {
        window.LoadReference(files.ReferencePath);
        window.LoadTarget(files.TargetPath);
      });
      // Still no selection, so Time Shift is still out; Auto Align is in.
      Assert.False(scope.Read(() => window.ActivateAction(RetimerWindow.ActionTimeShift)));

      await scope.RunAsync(window, () =>
      {
        window.SelectReference(10);
        window.SelectTarget(10);
      });

      bool fired = false;
      await scope.RunAsync(window, () => fired = window.ActivateAction(RetimerWindow.ActionTimeShift));
      var state = scope.Read(() => new
      {
        Target = window.Engine.TargetLines[10].Start,
        Reference = window.Engine.ReferenceLines[10].Start,
      });
      Assert.True(fired);
      Assert.Equal(state.Reference, state.Target);   // the action ran the shift
    }
  }
}
