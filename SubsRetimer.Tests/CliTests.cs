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
    public void Editor_NotAvailableYet_ExitsOne()
    {
      var (rf, tg) = MakePair();
      var r = Run(rf, tg);
      Assert.Equal(Cli.ExitError, r.Code);
      Assert.Contains("--auto", r.Err);
    }

    [Fact]
    public void RealProcess_HonoursStdoutContract()
    {
      // The executable sits next to the test assembly because the test project references it.
      string exe = Path.Combine(AppContext.BaseDirectory, "subsretimer.dll");
      Assert.True(File.Exists(exe), exe);
      var (rf, tg) = MakePair();

      var psi = new ProcessStartInfo("dotnet")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
      };
      foreach (var a in new[] { exe, "--auto", "--print-output", rf, tg }) psi.ArgumentList.Add(a);
      using var p = Process.Start(psi)!;
      string stdout = p.StandardOutput.ReadToEnd();
      string stderr = p.StandardError.ReadToEnd();
      p.WaitForExit();

      Assert.Equal(0, p.ExitCode);
      Assert.Equal(Path.GetFullPath(RetimerIO.DefaultOutputPath(tg)) + "\n", stdout);
      Assert.Contains("saved", stderr);
    }
  }
}
