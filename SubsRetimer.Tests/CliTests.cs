//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using SubsRetimer.Core;
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

    [Fact]
    public void Editor_WithoutADisplay_ExitsOneWithMessage()
    {
      // Through the real executable, not in process: GTK initialisation is
      // global to a process, and the native library reads DISPLAY itself, so
      // Environment.SetEnvironmentVariable would not reach it.
      var (rf, tg) = MakePair();
      var r = RunProcess(new[] { rf, tg }, psi =>
      {
        psi.Environment["DISPLAY"] = "";
        psi.Environment["WAYLAND_DISPLAY"] = "";
      });

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.Equal("", r.Out);
      Assert.Contains("cannot open a display", r.Err);
    }

    [Fact]
    public void Editor_WindowSavedNothing_ExitsTwo()
    {
      var (rf, tg) = MakePair();
      var r = WithEditorSeams(
        canOpenDisplay: () => true,
        runWindow: (reference, target, _) =>
        {
          // Both files are loaded before the window opens.
          Assert.NotNull(reference);
          Assert.NotNull(target);
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
        canOpenDisplay: () => true,
        // The window reports each path as it writes the file; what the print
        // proves is that the callback, not the returned list, does the printing.
        runWindow: (_, _, onSaved) =>
        {
          var paths = new[] { first, second };
          foreach (string path in paths) onSaved?.Invoke(path);
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
        canOpenDisplay: () => true,
        // Without --print-output there is no callback to report to at all.
        runWindow: (_, _, onSaved) =>
        {
          Assert.Null(onSaved);
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
        canOpenDisplay: () => true,
        runWindow: (_, _, _) => Array.Empty<string>(),
        () => Run("--print-output", rf, tg));

      Assert.Equal(Cli.ExitNothingSaved, r.Code);
      Assert.Equal("", r.Out);
    }

    [Fact]
    public void Editor_WindowFailed_ExitsOneAndTheExceptionDoesNotEscape()
    {
      var (rf, tg) = MakePair();
      var r = WithEditorSeams(
        canOpenDisplay: () => true,
        runWindow: (_, _, _) => throw new InvalidOperationException("gtk fell over"),
        () => Run(rf, tg));

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.Equal("", r.Out);
      Assert.Contains("gtk fell over", r.Err);
    }

    /// <summary>Swap the editor seams for the duration of one call and put them back.</summary>
    private static T WithEditorSeams<T>(
      Func<bool> canOpenDisplay,
      Func<SubtitleFile?, SubtitleFile?, Action<string>?, IReadOnlyList<string>> runWindow,
      Func<T> body)
    {
      var display = Cli.CanOpenDisplay;
      var window = Cli.RunWindow;
      try
      {
        Cli.CanOpenDisplay = canOpenDisplay;
        Cli.RunWindow = runWindow;
        return body();
      }
      finally
      {
        Cli.CanOpenDisplay = display;
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
      string stdout = p.StandardOutput.ReadToEnd();
      string stderr = p.StandardError.ReadToEnd();
      p.WaitForExit();
      return (p.ExitCode, stdout, stderr);
    }
  }
}
