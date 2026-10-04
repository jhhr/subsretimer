//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Encodings.Web;
using System.Text.Json;
using SubsRetimer.Core;

namespace SubsRetimer
{
  /// <summary>
  /// The <c>--report</c> file of an <c>--auto</c> run: what subs2srs reads
  /// for its season table instead of parsing stderr, which stays for
  /// humans. Written as camelCase JSON in UTF-8 without a BOM, with every
  /// path in full. A field that the run never reached (no timed lines: no
  /// alignment) is an empty list for the segments and null for the
  /// measurements, so the shape is the same on every outcome.
  /// </summary>
  internal sealed record AutoReport(
    int Version,
    ReportFile Reference,
    ReportFile Target,
    string Output,
    IReadOnlyList<ReportSegment> Segments,
    ReportCoverage? ReferenceCoverage,
    ReportMatched? TargetMatched,
    ReportMismatch? AverageMismatchSeconds,
    double? MinMatch,
    int ExitCode,
    string? Saved,
    string? Reason)
  {
    /// <summary>Raised when a field changes meaning or goes away; added fields keep it.</summary>
    public const int CurrentVersion = 1;

    /// <summary><see cref="Reason"/> when the reference coverage was below <c>--min-match</c>.</summary>
    public const string BelowMinMatch = "below min-match";

    /// <summary><see cref="Reason"/> when either file had no timed lines, so nothing was aligned.</summary>
    public const string NoTimedLines = "no timed lines";

    private static readonly JsonSerializerOptions Json = new()
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      WriteIndented = true,
      // Japanese file names stay readable instead of becoming \uXXXX. The
      // default encoder escapes them for HTML pages; this file is read by
      // programs and people, never embedded in a page.
      Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Write the report to <paramref name="path"/>. A file left half written
    /// is removed before the error goes on, so a run that fails here leaves
    /// no report, as every failing run does.
    /// </summary>
    public void Write(string path)
    {
      // Numbers are written the JSON way, with a '.', whatever the culture.
      byte[] json = JsonSerializer.SerializeToUtf8Bytes(this, Json);
      try { File.WriteAllBytes(path, json); }
      catch
      {
        try { File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        throw;
      }
    }
  }

  /// <summary>An input file: its full path, the timed lines read from it, and the encoding it was read in.</summary>
  internal sealed record ReportFile(string Path, int LineCount, string Encoding)
  {
    public static ReportFile Of(SubtitleFile file) =>
      new(System.IO.Path.GetFullPath(file.Path), file.Lines.Count, file.Encoding.WebName);
  }

  /// <summary>
  /// A run of target lines that moved together: the first and last line,
  /// 1-based and inclusive as stderr prints them, the offset in whole
  /// milliseconds, and how many of its lines overlapped the reference.
  /// </summary>
  internal sealed record ReportSegment(int FirstLine, int LastLine, long OffsetMs, int MatchedLines, int LineCount)
  {
    public static ReportSegment Of(AlignmentSegment s) =>
      new(s.StartIndex + 1, s.EndIndexExclusive, (long)Math.Round(s.Offset.TotalMilliseconds), s.MatchedLines, s.LineCount);
  }

  /// <summary>The reference coverage of <c>--min-match</c> (<see cref="RetimerEngine.ReferenceCoverage"/>).</summary>
  internal sealed record ReportCoverage(int Covered, int Counted, double Share)
  {
    public static ReportCoverage Of(Coverage c) => new(c.Covered, c.Counted, c.Share);
  }

  /// <summary>The target lines that overlap a reference line at all (<see cref="RetimerEngine.TargetMatched"/>).</summary>
  internal sealed record ReportMatched(int Matched, int LineCount, double Share)
  {
    public static ReportMatched Of(Coverage c) => new(c.Covered, c.Counted, c.Share);
  }

  /// <summary>One <see cref="RetimerEngine.AverageMismatchSeconds"/>: over every target line, and over the matched ones.</summary>
  internal sealed record ReportAverages(double All, double Matched);

  /// <summary>The average mismatch before and after the alignment, in seconds.</summary>
  internal sealed record ReportMismatch(ReportAverages Before, ReportAverages After);
}
