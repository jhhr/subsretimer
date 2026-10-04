//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;
using System.Text.Json;
using SubsRetimer.Core;
using Xunit;

namespace SubsRetimer.Tests
{
  /// <summary><c>--report</c>: the JSON file an <c>--auto</c> run leaves for subs2srs.</summary>
  public partial class CliTests
  {
    private static JsonElement ReadReport(string path)
    {
      byte[] bytes = File.ReadAllBytes(path);
      Assert.Equal((byte)'{', bytes[0]);   // UTF-8 without a byte-order mark
      using var doc = JsonDocument.Parse(bytes);
      return doc.RootElement.Clone();
    }

    private static string ReportBeside(string file) => Path.Combine(Path.GetDirectoryName(file)!, "report.json");

    [Fact]
    public void Parse_Report()
    {
      Assert.Null(Cli.Parse(new[] { "--auto", "a.srt", "b.ass" }).Report);
      Assert.Equal("r.json", Cli.Parse(new[] { "--auto", "--report", "r.json", "a.srt", "b.ass" }).Report);
      Assert.Throws<ArgumentException>(() => Cli.Parse(new[] { "--auto", "--report" }));
      Assert.Throws<ArgumentException>(() => Cli.Parse(new[] { "--report", "r.json", "a.srt", "b.ass" }));
    }

    [Fact]
    public void Auto_Report_DescribesASavedRun()
    {
      var (rf, tg) = MakePair();
      string dir = Path.GetDirectoryName(rf)!;
      // Given with a detour through "..": the report has full paths.
      string refGiven = Path.Combine(dir, "..", Path.GetFileName(dir), Path.GetFileName(rf));
      string output = Path.Combine(Path.GetDirectoryName(tg)!, "字幕.ass");
      string report = ReportBeside(tg);

      var r = Run("--auto", "--print-output", "-o", output, "--report", report, refGiven, tg);

      Assert.Equal(Cli.ExitSaved, r.Code);
      var j = ReadReport(report);
      Assert.Equal(1, j.GetProperty("version").GetInt32());

      var reference = j.GetProperty("reference");
      Assert.Equal(Path.GetFullPath(rf), reference.GetProperty("path").GetString());
      Assert.Equal(120, reference.GetProperty("lineCount").GetInt32());
      Assert.Equal("utf-8", reference.GetProperty("encoding").GetString());
      var target = j.GetProperty("target");
      Assert.Equal(Path.GetFullPath(tg), target.GetProperty("path").GetString());
      Assert.Equal(120, target.GetProperty("lineCount").GetInt32());
      Assert.Equal("utf-8", target.GetProperty("encoding").GetString());
      Assert.Equal(Path.GetFullPath(output), j.GetProperty("output").GetString());

      // 1-based and inclusive, as stderr prints them; ASS keeps centiseconds,
      // so the offsets may be a few ms off.
      var segments = j.GetProperty("segments").EnumerateArray().ToList();
      Assert.Equal(2, segments.Count);
      var expected = new[] { (First: 1, Last: 40, OffsetMs: 0), (First: 41, Last: 120, OffsetMs: -15000) };
      for (int i = 0; i < 2; i++)
      {
        var s = segments[i];
        Assert.Equal(expected[i].First, s.GetProperty("firstLine").GetInt32());
        Assert.Equal(expected[i].Last, s.GetProperty("lastLine").GetInt32());
        Assert.InRange(s.GetProperty("offsetMs").GetInt64(), expected[i].OffsetMs - 20, expected[i].OffsetMs + 20);
        int count = expected[i].Last - expected[i].First + 1;
        Assert.Equal(count, s.GetProperty("lineCount").GetInt32());
        Assert.Equal(count, s.GetProperty("matchedLines").GetInt32());
        Assert.Contains($"  lines {expected[i].First}-{expected[i].Last}: ", r.Err);
      }

      var coverage = j.GetProperty("referenceCoverage");
      Assert.Equal(120, coverage.GetProperty("covered").GetInt32());
      Assert.Equal(120, coverage.GetProperty("counted").GetInt32());
      Assert.Equal(1.0, coverage.GetProperty("share").GetDouble());
      var matched = j.GetProperty("targetMatched");
      Assert.Equal(120, matched.GetProperty("matched").GetInt32());
      Assert.Equal(120, matched.GetProperty("lineCount").GetInt32());
      Assert.Equal(1.0, matched.GetProperty("share").GetDouble());

      var mismatch = j.GetProperty("averageMismatchSeconds");
      double beforeAll = mismatch.GetProperty("before").GetProperty("all").GetDouble();
      double afterAll = mismatch.GetProperty("after").GetProperty("all").GetDouble();
      Assert.True(beforeAll > 0.5, $"before {beforeAll}");
      Assert.InRange(afterAll, 0, 0.02);
      Assert.InRange(mismatch.GetProperty("after").GetProperty("matched").GetDouble(), 0, 0.02);
      Assert.True(mismatch.GetProperty("before").GetProperty("matched").GetDouble() >= 0);

      Assert.Equal(JsonValueKind.Null, j.GetProperty("minMatch").ValueKind);
      Assert.Equal(0, j.GetProperty("exitCode").GetInt32());
      Assert.Equal(r.Out, j.GetProperty("saved").GetString() + Environment.NewLine);
      Assert.Equal(JsonValueKind.Null, j.GetProperty("reason").ValueKind);

      // Written as UTF-8 text, not escaped: a person can read the paths.
      Assert.Contains("字幕.ass", Encoding.UTF8.GetString(File.ReadAllBytes(report)));
    }

    [Fact]
    public void Auto_Report_BelowMinMatch_SaysWhyAndNamesNoSavedFile()
    {
      var (rf, tg) = MakeHalfPair();
      string output = Path.Combine(Path.GetDirectoryName(tg)!, "chosen.ass");
      string report = ReportBeside(tg);

      var r = Run("--auto", "--min-match", "0.9", "--print-output", "-o", output, "--report", report, rf, tg);

      Assert.Equal(Cli.ExitNothingSaved, r.Code);
      Assert.Equal("", r.Out);
      Assert.False(File.Exists(output));
      var j = ReadReport(report);
      Assert.Equal(2, j.GetProperty("exitCode").GetInt32());
      Assert.Equal(JsonValueKind.Null, j.GetProperty("saved").ValueKind);
      Assert.Equal("below min-match", j.GetProperty("reason").GetString());
      Assert.Equal(0.9, j.GetProperty("minMatch").GetDouble());
      Assert.Equal(Path.GetFullPath(output), j.GetProperty("output").GetString());

      var coverage = j.GetProperty("referenceCoverage");
      Assert.Equal(60, coverage.GetProperty("covered").GetInt32());
      Assert.Equal(120, coverage.GetProperty("counted").GetInt32());
      Assert.Equal(0.5, coverage.GetProperty("share").GetDouble());
      // Every target line found its place; the reference is what is missing.
      Assert.Equal(1.0, j.GetProperty("targetMatched").GetProperty("share").GetDouble());
      var segment = Assert.Single(j.GetProperty("segments").EnumerateArray());
      Assert.Equal(1, segment.GetProperty("firstLine").GetInt32());
      Assert.Equal(60, segment.GetProperty("lastLine").GetInt32());
    }

    [Fact]
    public void Auto_Report_NoTimedLines_HasEmptySegmentsAndNullMeasurements()
    {
      var (rf, _) = MakePair();
      string empty = Fixtures.WriteTemp(".srt", "\n");
      string report = ReportBeside(empty);

      var r = Run("--auto", "--print-output", "--report", report, rf, empty);

      Assert.Equal(Cli.ExitNothingSaved, r.Code);
      Assert.Equal("", r.Out);
      var j = ReadReport(report);
      Assert.Equal(2, j.GetProperty("exitCode").GetInt32());
      Assert.Equal("no timed lines", j.GetProperty("reason").GetString());
      Assert.Equal(JsonValueKind.Null, j.GetProperty("saved").ValueKind);
      Assert.Equal(120, j.GetProperty("reference").GetProperty("lineCount").GetInt32());
      Assert.Equal(0, j.GetProperty("target").GetProperty("lineCount").GetInt32());
      Assert.Equal(Path.GetFullPath(RetimerIO.DefaultOutputPath(empty)), j.GetProperty("output").GetString());
      Assert.Equal(JsonValueKind.Array, j.GetProperty("segments").ValueKind);
      Assert.Empty(j.GetProperty("segments").EnumerateArray());
      Assert.Equal(JsonValueKind.Null, j.GetProperty("referenceCoverage").ValueKind);
      Assert.Equal(JsonValueKind.Null, j.GetProperty("targetMatched").ValueKind);
      Assert.Equal(JsonValueKind.Null, j.GetProperty("averageMismatchSeconds").ValueKind);
      Assert.Equal(JsonValueKind.Null, j.GetProperty("minMatch").ValueKind);
    }

    [Fact]
    public void Auto_Report_AnEarlierReportIsGoneWhenTheRunFails()
    {
      var (rf, tg) = MakePair();
      string report = ReportBeside(tg);

      // An input that is not there.
      File.WriteAllText(report, "{ \"version\": 1, \"exitCode\": 0 }");
      var missing = Run("--auto", "--report", report, rf, Path.Combine(Path.GetDirectoryName(tg)!, "missing.ass"));
      Assert.Equal(Cli.ExitError, missing.Code);
      Assert.Contains("not found", missing.Err);
      Assert.False(File.Exists(report));

      // A refusal after both files were read: the default output exists.
      Assert.Equal(Cli.ExitSaved, Run("--auto", rf, tg).Code);
      File.WriteAllText(report, "{ \"version\": 1, \"exitCode\": 0 }");
      var exists = Run("--auto", "--print-output", "--report", report, rf, tg);
      Assert.Equal(Cli.ExitError, exists.Code);
      Assert.Contains("already exists", exists.Err);
      Assert.Equal("", exists.Out);
      Assert.False(File.Exists(report));
    }

    [Fact]
    public void Auto_ReportInAFolderThatIsNotThere_ExitsOneBeforeSaving()
    {
      var (rf, tg) = MakePair();
      string report = Path.Combine(Path.GetDirectoryName(tg)!, "no-such-folder", "report.json");

      var r = Run("--auto", "--print-output", "--report", report, rf, tg);

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.Equal("", r.Out);
      Assert.Contains("--report: folder not found", r.Err);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(tg)));
    }

    [Fact]
    public void Auto_ReportNamingTheRunsOwnFile_ExitsOneAndDeletesNothing()
    {
      // Deleting the stale report comes before reading the inputs; a report
      // path that is TARGET would destroy it.
      var (rf, tg) = MakePair();
      byte[] target = File.ReadAllBytes(tg);

      var asTarget = Run("--auto", "--report", tg, rf, tg);
      Assert.Equal(Cli.ExitError, asTarget.Code);
      Assert.Contains("--report names the same file as TARGET", asTarget.Err);
      Assert.Equal(target, File.ReadAllBytes(tg));
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(tg)));

      string output = Path.Combine(Path.GetDirectoryName(tg)!, "chosen.ass");
      var asOutput = Run("--auto", "-o", output, "--report", output, rf, tg);
      Assert.Equal(Cli.ExitError, asOutput.Code);
      Assert.Contains("--report names the same file as --output", asOutput.Err);
      Assert.False(File.Exists(output));
    }

    [Fact]
    public void Report_WithoutAuto_OrWithoutAPath_ExitsOneAndTouchesNoFile()
    {
      var (rf, tg) = MakePair();
      string report = ReportBeside(tg);
      File.WriteAllText(report, "earlier");
      bool opened = false;

      var r = WithEditorSeams(
        probe: Ready,
        runWindow: _ => { opened = true; return Array.Empty<string>(); },
        () => Run("--report", report, rf, tg));

      Assert.Equal(Cli.ExitError, r.Code);
      Assert.False(opened);
      Assert.Equal("", r.Out);
      Assert.Contains("--report requires --auto", r.Err);
      Assert.Contains("Usage:", r.Err);
      Assert.Equal("earlier", File.ReadAllText(report));

      var noPath = Run("--auto", "--print-output", rf, tg, "--report");
      Assert.Equal(Cli.ExitError, noPath.Code);
      Assert.Equal("", noPath.Out);
      Assert.Contains("--report requires a value", noPath.Err);
      Assert.False(File.Exists(RetimerIO.DefaultOutputPath(tg)));
    }

    [Fact]
    public void Auto_Report_NumbersAreWrittenWithADotInEveryCulture()
    {
      // In process the culture is the machine's; German writes 0,5.
      var (rf, tg) = MakeHalfPair();
      string report = ReportBeside(tg);
      var culture = CultureInfo.CurrentCulture;
      (int Code, string Out, string Err) r;
      try
      {
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        r = Run("--auto", "--min-match", "0.9", "--report", report, rf, tg);
      }
      finally { CultureInfo.CurrentCulture = culture; }

      Assert.Equal(Cli.ExitNothingSaved, r.Code);
      string text = Encoding.UTF8.GetString(File.ReadAllBytes(report));
      Assert.Contains("\"share\": 0.5", text);
      Assert.Contains("\"minMatch\": 0.9", text);
      Assert.DoesNotMatch(@"\d,\d", text);
      // The mismatch seconds have fractions too, and parse as JSON numbers.
      var before = ReadReport(report).GetProperty("averageMismatchSeconds").GetProperty("before");
      Assert.True(before.GetProperty("all").GetDouble() > 0);
    }
  }
}
