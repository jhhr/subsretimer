//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Text;
using SubsRetimer.Core;
using SubsRetimer.Editor;
using Xunit;

namespace SubsRetimer.Tests
{
  public class CliTests
  {
    private static (int Code, string Out, string Err) Run(params string[] args)
    {
      var o = new StringWriter(); var e = new StringWriter();
      int code = Cli.Run(args, o, e);
      return (code, o.ToString(), e.ToString());
    }

    private static (string Ref, string Tgt) MakePair()
    {
      var reference = Fixtures.Dialogue(120);
      var target = new List<RetimerLine>();
      for (int i = 0; i < reference.Count; i++)
      {
        double off = i >= 40 ? 15000 : 0;
        target.Add(new RetimerLine { Start = reference[i].Start + TimeSpan.FromMilliseconds(off), End = reference[i].End + TimeSpan.FromMilliseconds(off), Text = "t" + i, RawIndex = i });
      }
      return (Fixtures.WriteTemp(".srt", Fixtures.SrtFile(reference)), Fixtures.WriteTemp(".ass", Fixtures.AssFile(target)));
    }

    [Fact]
    public void Parse_PositionalsAndOptions()
    {
      var o = Cli.Parse(new[] { "--auto", "-o", "out.ass", "--ref-encoding", "shift_jis", "--print-output", "a.srt", "b.ass" });
      Assert.True(o.Auto);
      Assert.Equal("out.ass", o.Output);
      Assert.Equal("shift_jis", o.RefEncoding);
      Assert.Equal("utf-8", o.TargetEncoding);
      Assert.True(o.PrintOutput);
      Assert.Equal("a.srt", o.Reference);
      Assert.Equal("b.ass", o.Target);
    }

    [Fact]
    public void Parse_RejectsUnknownOptionAndTooManyFiles()
    {
      Assert.Throws<ArgumentException>(() => Cli.Parse(new[] { "--bogus" }));
      Assert.Throws<ArgumentException>(() => Cli.Parse(new[] { "a", "b", "c" }));
      Assert.Throws<ArgumentException>(() => Cli.Parse(new[] { "-o" }));
    }

    [Fact]
    public void HelpAndVersion_GoToStdout()
    {
      var h = Run("--help");
      Assert.Equal(Cli.ExitSaved, h.Code);
      Assert.Contains("Usage:", h.Out);
      var v = Run("--version");
      Assert.Equal(Cli.ExitSaved, v.Code);
      Assert.Matches(@"^\d+\.\d+\.\d+", v.Out.Trim());
    }

    [Fact]
    public void BadOption_ExitsOneWithUsageOnStderr()
    {
      var r = Run("--nope");
      Assert.Equal(Cli.ExitError, r.Code);
      Assert.Equal("", r.Out);
      Assert.Contains("Unknown option", r.Err);
    }

    [Fact]
    public void Auto_SavesDefaultPath_PrintsOnlyPathToStdout()
    {
      var (rf, tg) = MakePair();
      var r = Run("--auto", "--print-output", rf, tg);
      Assert.Equal(Cli.ExitSaved, r.Code);
      string expected = Path.GetFullPath(RetimerIO.DefaultOutputPath(tg));
      Assert.Equal(expected + Environment.NewLine, r.Out);
      Assert.True(File.Exists(expected));
      // ASS carries centiseconds, the SRT reference milliseconds, so allow a few ms.
      var m = System.Text.RegularExpressions.Regex.Match(r.Err, @"lines 41-120: (-1[45]\.\d{3})s");
      Assert.True(m.Success, r.Err);
      Assert.Equal(-15.0, double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), tolerance: 0.01);

      // The saved file is aligned to the reference.
      var saved = RetimerIO.Load(expected);
      var reference = RetimerIO.Load(rf);
      for (int i = 0; i < reference.Lines.Count; i++)
        Assert.Equal(reference.Lines[i].Start.TotalMilliseconds, saved.Lines[i].Start.TotalMilliseconds, tolerance: 20.0);
    }

    [Fact]
    public void Auto_WithoutPrintOutput_StdoutIsEmpty()
    {
      var (rf, tg) = MakePair();
      var r = Run("--auto", rf, tg);
      Assert.Equal(Cli.ExitSaved, r.Code);
      Assert.Equal("", r.Out);
    }

    [Fact]
    public void Auto_RefusesToOverwriteDefaultOutput_UnlessExplicit()
    {
      var (rf, tg) = MakePair();
      Assert.Equal(Cli.ExitSaved, Run("--auto", rf, tg).Code);
      var again = Run("--auto", rf, tg);
      Assert.Equal(Cli.ExitError, again.Code);
      Assert.Contains("already exists", again.Err);

      string explicitOut = RetimerIO.DefaultOutputPath(tg);
      var forced = Run("--auto", "-o", explicitOut, "--print-output", rf, tg);
      Assert.Equal(Cli.ExitSaved, forced.Code);
      Assert.Equal(Path.GetFullPath(explicitOut) + Environment.NewLine, forced.Out);
    }

    [Fact]
    public void Auto_MissingFile_ExitsOne()
    {
      var (rf, _) = MakePair();
      var r = Run("--auto", rf, "/nonexistent/x.ass");
      Assert.Equal(Cli.ExitError, r.Code);
      Assert.Contains("not found", r.Err);
      Assert.Equal("", r.Out);
    }

    [Fact]
    public void Auto_RequiresBothFiles()
    {
      var (rf, _) = MakePair();
      var r = Run("--auto", rf);
      Assert.Equal(Cli.ExitError, r.Code);
      Assert.Contains("requires both", r.Err);
    }

    [Fact]
    public void Auto_EmptyTarget_ExitsTwo()
    {
      var (rf, _) = MakePair();
      string empty = Fixtures.WriteTemp(".srt", "\n");
      var r = Run("--auto", rf, empty);
      Assert.Equal(Cli.ExitNothingSaved, r.Code);
      Assert.Equal("", r.Out);
    }

    [Fact]
    public void Parse_EditorModeIsTheDefault()
    {
      var both = Cli.Parse(new[] { "a.srt", "b.ass" });
      Assert.False(both.Auto);
      Assert.Equal("a.srt", both.Reference);
      Assert.Equal("b.ass", both.Target);

      var none = Cli.Parse(Array.Empty<string>());
      Assert.False(none.Auto);
      Assert.Null(none.Reference);
      Assert.Null(none.Target);
    }

    /// <summary>
    /// Through the real executable, not in process: GTK initialisation is
    /// global to a process, and the native library reads DISPLAY itself, so
    /// Environment.SetEnvironmentVariable would not reach it.
    ///
    /// Linux only: emptying DISPLAY and WAYLAND_DISPLAY takes the display
    /// away there and nowhere else. On Windows and macOS GDK always has one,
    /// so the real window would open inside the test run.
    /// </summary>
    [LinuxFact]
    public void Editor_WithoutADisplay_ExitsOneWithMessage()
    {
      var (rf, tg) = MakePair();
      var r = RunProcess(new[] { rf, tg }, WithoutDisplay);

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.Equal("", r.Out);
      Assert.Contains("cannot open a display", r.Err);
    }

    /// <summary>--check-editor never opens a window, so this real run cannot hang even where a display does open.</summary>
    [LinuxFact]
    public void CheckEditor_WithoutADisplay_ExitsOneAndSaysWhy()
    {
      var r = RunProcess(new[] { "--check-editor" }, WithoutDisplay);

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.Equal("", r.Out);
      Assert.Contains("cannot open a display", r.Err);
    }

    /// <summary>A child environment with no display: no X, no Wayland, and no backend (broadway, say) inherited from this one.</summary>
    private static void WithoutDisplay(ProcessStartInfo psi)
    {
      psi.Environment["DISPLAY"] = "";
      psi.Environment["WAYLAND_DISPLAY"] = "";
      psi.Environment.Remove("GDK_BACKEND");
      psi.Environment.Remove("BROADWAY_DISPLAY");
    }

    [Fact]
    public void Editor_OutputAndEncodings_ReachTheWindow()
    {
      var (rf, tg) = MakePair();
      string output = Path.Combine(Path.GetDirectoryName(tg)!, "chosen.ass");
      EditorRequest? seen = null;

      var r = WithEditorSeams(
        probe: Ready,
        runWindow: request => { seen = request; return Array.Empty<string>(); },
        () => Run("-o", output, "--ref-encoding", "windows-1252", "--target-encoding", "utf-8", rf, tg));

      Assert.Equal(Cli.ExitNothingSaved, r.Code);
      Assert.NotNull(seen);
      // Save writes where --output says, as --auto does ...
      Assert.Equal(output, seen!.OutputPath);
      // ... and files opened from the window later are read as the command line said.
      Assert.Equal("windows-1252", seen.ReferenceEncoding?.WebName);
      Assert.Equal("utf-8", seen.TargetEncoding?.WebName);
    }

    [Fact]
    public void Editor_OutputWithoutATarget_ExitsOneBeforeTheWindow()
    {
      var (rf, _) = MakePair();
      bool opened = false;
      var r = WithEditorSeams(
        probe: Ready,
        runWindow: _ => { opened = true; return Array.Empty<string>(); },
        () => Run("-o", "out.ass", rf));

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.False(opened);
      Assert.Contains("--output needs TARGET", r.Err);
    }

    [Fact]
    public void Editor_OutputInTheOtherFormat_ExitsOneBeforeTheWindow()
    {
      var (rf, tg) = MakePair();   // the target is .ass
      bool opened = false;
      var r = WithEditorSeams(
        probe: Ready,
        runWindow: _ => { opened = true; return Array.Empty<string>(); },
        () => Run("-o", Path.ChangeExtension(tg, ".srt"), rf, tg));

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.False(opened);
      Assert.Contains("ASS subtitles are saved as .ass or .ssa, not .srt", r.Err);
    }

    [Fact]
    public void Auto_OutputInTheOtherFormat_ExitsOneAndWritesNothing()
    {
      var (rf, tg) = MakePair();
      string output = Path.ChangeExtension(tg, ".out.srt");
      var r = Run("--auto", "-o", output, "--print-output", rf, tg);

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.Equal("", r.Out);
      Assert.False(File.Exists(output));
      Assert.Contains("not .srt", r.Err);
    }

    /// <summary>Japanese dialogue in Shift-JIS, no BOM: not valid UTF-8, the default.</summary>
    private static string ShiftJisTarget()
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      var lines = Fixtures.Dialogue(120);
      for (int i = 0; i < lines.Count; i++)
        lines[i] = new RetimerLine { Start = lines[i].Start + TimeSpan.FromSeconds(15), End = lines[i].End + TimeSpan.FromSeconds(15), Text = "こんにちは" + i, RawIndex = i };
      return Fixtures.WriteTemp(".ass", Fixtures.AssFile(lines), Encoding.GetEncoding("shift_jis"));
    }

    [Fact]
    public void Auto_TargetNotValidInItsEncoding_ExitsOneInsteadOfSavingItChanged()
    {
      var (rf, _) = MakePair();
      string tg = ShiftJisTarget();

      var wrong = Run("--auto", "--print-output", rf, tg);
      Assert.Equal(Cli.ExitError, wrong.Code);
      Assert.Equal("", wrong.Out);
      Assert.Contains("not valid utf-8 text", wrong.Err);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(tg)));

      // Named, the same file goes through and keeps every byte of its text.
      var right = Run("--auto", "--target-encoding", "shift_jis", "--print-output", rf, tg);
      Assert.Equal(Cli.ExitSaved, right.Code);
      byte[] saved = File.ReadAllBytes(RetimerIO.DefaultOutputPath(tg));
      byte[] text = Encoding.GetEncoding("shift_jis").GetBytes("こんにちは7");
      Assert.True(saved.AsSpan().IndexOf(text) >= 0, "the Shift-JIS text did not survive the save");
    }

    [Fact]
    public void Editor_TargetNotValidInItsEncoding_ExitsOneBeforeTheWindow()
    {
      var (rf, _) = MakePair();
      string tg = ShiftJisTarget();
      bool opened = false;
      var r = WithEditorSeams(
        probe: Ready,
        runWindow: _ => { opened = true; return Array.Empty<string>(); },
        () => Run(rf, tg));

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.False(opened);
      Assert.Contains("--target-encoding", r.Err);
    }

    [Fact]
    public void Editor_GtkMissing_SaysSoInsteadOfBlamingTheDisplay()
    {
      var (rf, tg) = MakePair();
      bool opened = false;
      var r = WithEditorSeams(
        probe: () => new EditorProbe(EditorStatus.NoGtk, "Unable to load shared library 'libgtk-4.so.1' or one of its dependencies."),
        runWindow: _ => { opened = true; return Array.Empty<string>(); },
        () => Run(rf, tg));

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.False(opened);
      Assert.Equal("", r.Out);
      Assert.Contains("GTK 4 could not be loaded", r.Err);
      Assert.Contains("libgtk-4.so.1", r.Err);
      Assert.DoesNotContain("display", r.Err);
    }

    [Theory]
    [InlineData(EditorStatus.Ready, Cli.ExitSaved, "can start")]
    [InlineData(EditorStatus.NoDisplay, Cli.ExitError, "cannot open a display")]
    [InlineData(EditorStatus.NoGtk, Cli.ExitError, "GTK 4 could not be loaded")]
    public void CheckEditor_ReportsTheProbeAndNeverOpensTheWindow(EditorStatus status, int code, string message)
    {
      bool opened = false;
      var r = WithEditorSeams(
        probe: () => new EditorProbe(status),
        runWindow: _ => { opened = true; return Array.Empty<string>(); },
        () => Run("--check-editor"));

      Assert.Equal(code, r.Code);
      Assert.False(opened);
      Assert.Equal("", r.Out);
      Assert.Contains(message, r.Err);
    }

    [Fact]
    public void Version_IsTheProductVersion()
    {
      Assert.Equal(ProductInfo.Version, Cli.Version);
      Assert.Equal(ProductInfo.Version, Run("--version").Out.Trim());
    }

    [Fact]
    public void Editor_WindowSavedNothing_ExitsTwo()
    {
      var (rf, tg) = MakePair();
      var r = WithEditorSeams(
        probe: Ready,
        runWindow: request =>
        {
          // Both files are loaded before the window opens.
          Assert.NotNull(request.Reference);
          Assert.NotNull(request.Target);
          return Array.Empty<string>();
        },
        () => Run(rf, tg));

      Assert.Equal(Cli.ExitNothingSaved, r.Code);
      Assert.Equal("", r.Out);
    }

    [Fact]
    public void Editor_WindowSaved_PrintsEveryPathAndExitsZero()
    {
      var (rf, tg) = MakePair();
      string first = Path.GetFullPath(RetimerIO.DefaultOutputPath(tg));
      string second = Path.GetFullPath(Path.ChangeExtension(tg, ".copy.ass"));

      var r = WithEditorSeams(
        probe: Ready,
        // The window reports each path as it writes the file; what the print
        // proves is that the callback, not the returned list, does the printing.
        runWindow: request =>
        {
          var paths = new[] { first, second };
          foreach (string path in paths) request.OnSaved?.Invoke(path);
          return paths;
        },
        () => Run("--print-output", rf, tg));

      Assert.Equal(Cli.ExitSaved, r.Code);
      Assert.Equal(first + Environment.NewLine + second + Environment.NewLine, r.Out);
    }

    [Fact]
    public void Editor_WithoutPrintOutput_SavedPathsStayOffStdout()
    {
      var (rf, tg) = MakePair();
      var r = WithEditorSeams(
        probe: Ready,
        // Without --print-output there is no callback to report to at all.
        runWindow: request =>
        {
          Assert.Null(request.OnSaved);
          return new[] { Path.GetFullPath(RetimerIO.DefaultOutputPath(tg)) };
        },
        () => Run(rf, tg));

      Assert.Equal(Cli.ExitSaved, r.Code);
      Assert.Equal("", r.Out);
    }

    [Fact]
    public void Editor_SavedNothing_PrintsNothingEvenWithPrintOutput()
    {
      var (rf, tg) = MakePair();
      var r = WithEditorSeams(
        probe: Ready,
        runWindow: _ => Array.Empty<string>(),
        () => Run("--print-output", rf, tg));

      Assert.Equal(Cli.ExitNothingSaved, r.Code);
      Assert.Equal("", r.Out);
    }

    [Fact]
    public void Editor_WindowFailed_ExitsOneAndTheExceptionDoesNotEscape()
    {
      var (rf, tg) = MakePair();
      var r = WithEditorSeams(
        probe: Ready,
        runWindow: _ => throw new InvalidOperationException("gtk fell over"),
        () => Run(rf, tg));

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.Equal("", r.Out);
      Assert.Contains("the editor failed: gtk fell over", r.Err);
    }

    private static EditorProbe Ready() => new(EditorStatus.Ready);

    /// <summary>Swap the editor seams for the duration of one call and put them back.</summary>
    private static T WithEditorSeams<T>(
      Func<EditorProbe> probe,
      Func<EditorRequest, IReadOnlyList<string>> runWindow,
      Func<T> body)
    {
      var display = Cli.ProbeEditor;
      var start = Cli.ProbeEditorStart;
      var window = Cli.RunWindow;
      try
      {
        Cli.ProbeEditor = probe;
        Cli.ProbeEditorStart = probe;
        Cli.RunWindow = runWindow;
        return body();
      }
      finally
      {
        Cli.ProbeEditor = display;
        Cli.ProbeEditorStart = start;
        Cli.RunWindow = window;
      }
    }

    [Fact]
    public void RealProcess_HonoursStdoutContract()
    {
      var (rf, tg) = MakePair();
      var r = RunProcess(new[] { "--auto", "--print-output", rf, tg });

      Assert.Equal(0, r.Code);
      Assert.Equal(Path.GetFullPath(RetimerIO.DefaultOutputPath(tg)) + "\n", r.Out);
      Assert.Contains("saved", r.Err);
    }

    /// <summary>Run the real executable; it sits next to the test assembly because the test project references it.</summary>
    private static (int Code, string Out, string Err) RunProcess(
      IEnumerable<string> args, Action<ProcessStartInfo>? configure = null)
    {
      string exe = Path.Combine(AppContext.BaseDirectory, "subsretimer.dll");
      Assert.True(File.Exists(exe), exe);

      var psi = new ProcessStartInfo("dotnet")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
      };
      psi.ArgumentList.Add(exe);
      foreach (var a in args) psi.ArgumentList.Add(a);
      configure?.Invoke(psi);

      using var p = Process.Start(psi)!;
      var stdout = p.StandardOutput.ReadToEndAsync();
      var stderr = p.StandardError.ReadToEndAsync();
      // A child that opened a window after all would wait for a user forever;
      // end it and fail instead of hanging the test run.
      if (!p.WaitForExit(ProcessTimeout))
      {
        p.Kill(entireProcessTree: true);
        p.WaitForExit();
        Assert.Fail($"subsretimer {string.Join(" ", args)} did not exit within {ProcessTimeout}");
      }
      p.WaitForExit();   // drains the redirected streams
      return (p.ExitCode, stdout.Result, stderr.Result);
    }

    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(60);
  }
}
