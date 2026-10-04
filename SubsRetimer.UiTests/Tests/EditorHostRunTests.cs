//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Editor;
using SubsRetimer.UiTests.Harness;
using Xunit;

namespace SubsRetimer.UiTests.Tests
{
  /// <summary>
  /// <see cref="EditorHost.Run"/> itself, with a window that cannot be built.
  /// The window is built inside GTK's <c>activate</c> callback, where GirCore
  /// answers an escaping exception by printing it and ending the process;
  /// Run has to carry it out to the command line instead. Run's application
  /// runs nested on the fixture's GTK thread and ends within the test.
  /// </summary>
  [Collection(GtkCollection.Name)]
  public class EditorHostRunTests
  {
    private readonly GtkFixture _gtk;

    public EditorHostRunTests(GtkFixture gtk) => _gtk = gtk;

    [GtkFact]
    public async Task Run_AWindowThatFailsWhileActivating_IsRethrownByRun()
    {
      using var scope = new UiTestScope(_gtk);
      var create = EditorHost.CreateWindow;
      try
      {
        EditorHost.CreateWindow = _ => throw new InvalidOperationException("the window could not be built");

        var failure = await _gtk.RunOnGtkAsync(() =>
          Task.FromResult(Record.Exception(() => EditorHost.Run(new EditorRequest(null, null)))));

        var thrown = Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal("the window could not be built", thrown.Message);
      }
      finally
      {
        EditorHost.CreateWindow = create;
      }
    }

    [GtkFact]
    public async Task Run_AWindowThatFailsAfterItWasBuilt_IsDestroyedAndRethrown()
    {
      using var scope = new UiTestScope(_gtk);
      var files = SubtitleFixtures.Pair(scope.TempDir, count: 6);
      var create = EditorHost.CreateWindow;
      RetimerWindow? built = null;
      try
      {
        EditorHost.CreateWindow = application => built = new RetimerWindow(application);
        // The window is built, then loading the target into it fails: an
        // empty --output is no path at all.
        var request = new EditorRequest(null, Core.RetimerIO.Load(files.TargetPath), OutputPath: "");

        var failure = await _gtk.RunOnGtkAsync(() =>
          Task.FromResult(Record.Exception(() => EditorHost.Run(request))));

        Assert.IsType<ArgumentException>(failure);
        Assert.NotNull(built);
        // Not left behind on screen; the scope would also fail on it.
        Assert.False(scope.Read(() => UiTestScope.IsAlive(built!)), "the half-built window outlived Run");
      }
      finally
      {
        EditorHost.CreateWindow = create;
      }
    }
  }
}
