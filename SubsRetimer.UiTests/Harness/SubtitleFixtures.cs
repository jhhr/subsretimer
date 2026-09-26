//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;
using SubsRetimer.Core;

namespace SubsRetimer.UiTests.Harness
{
  /// <summary>The two files a test loads, with the timings they were written from.</summary>
  /// <param name="ReferencePath">The SRT written from <paramref name="Reference"/>.</param>
  /// <param name="TargetPath">The ASS written from <paramref name="Target"/>.</param>
  internal sealed record FixtureFiles(
    string ReferencePath,
    string TargetPath,
    IReadOnlyList<RetimerLine> Reference,
    IReadOnlyList<RetimerLine> Target);

  /// <summary>
  /// Synthetic subtitle files for the UI tests: an SRT reference and an ASS
  /// target, so both readers and both writers are exercised. No third-party
  /// subtitle content is used.
  ///
  /// Timings are irregular, as <c>SubsRetimer.Tests.Fixtures.Dialogue</c>
  /// makes them: periodic lines would make several offsets indistinguishable
  /// and every "closest line" answer a coincidence. Every time is a whole
  /// centisecond, so a file survives the round trip through either format
  /// unchanged.
  /// </summary>
  internal static class SubtitleFixtures
  {
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    /// <summary>How long the silence before a <paramref name="silenceBefore"/> line is: well over <see cref="RetimerEngine.LargeGap"/>, so the row goes orange.</summary>
    private const double SilenceMs = 40000;

    /// <summary>
    /// <paramref name="count"/> lines of irregular dialogue: gaps of
    /// 0.2 to 4 s and durations of 0.6 to 3 s, plus a 40 s silence before
    /// every index in <paramref name="silenceBefore"/>.
    /// </summary>
    internal static List<RetimerLine> Dialogue(
      int count, int seed = 3, double startMs = 800, params int[] silenceBefore)
    {
      var rnd = new Random(seed);
      var lines = new List<RetimerLine>();
      double t = startMs;
      for (int i = 0; i < count; i++)
      {
        if (silenceBefore.Contains(i)) t += SilenceMs;
        double duration = 600 + rnd.Next(0, 240) * 10;   // whole centiseconds
        lines.Add(new RetimerLine
        {
          Start = Ms(t),
          End = Ms(t + duration),
          Text = "Line " + (i + 1).ToString(CultureInfo.InvariantCulture),
          RawIndex = i,
        });
        t += duration + 200 + rnd.Next(0, 380) * 10;
      }
      return lines;
    }

    /// <summary>
    /// A target derived from <paramref name="reference"/>: every line at
    /// index >= <c>FromIndex</c> gains <c>ExtraMs</c>, cumulatively. That is
    /// what extra material in the target's video does to its subtitles, and
    /// what Auto Align has to undo.
    /// </summary>
    internal static List<RetimerLine> Derive(
      IReadOnlyList<RetimerLine> reference, params (int FromIndex, double ExtraMs)[] cuts)
    {
      var lines = new List<RetimerLine>();
      for (int i = 0; i < reference.Count; i++)
      {
        double extra = cuts.Where(c => i >= c.FromIndex).Sum(c => c.ExtraMs);
        lines.Add(new RetimerLine
        {
          Start = reference[i].Start + Ms(extra),
          End = reference[i].End + Ms(extra),
          Text = reference[i].Text,
          RawIndex = i,
        });
      }
      return lines;
    }

    /// <summary>Write the pair into <paramref name="dir"/> as <c>reference.srt</c> and <c>target.ass</c>.</summary>
    internal static FixtureFiles Write(
      string dir, IReadOnlyList<RetimerLine> reference, IReadOnlyList<RetimerLine> target)
    {
      Directory.CreateDirectory(dir);
      string referencePath = Path.Combine(dir, "reference.srt");
      string targetPath = Path.Combine(dir, "target.ass");
      var utf8 = new UTF8Encoding(false);
      File.WriteAllText(referencePath, Srt(reference), utf8);
      File.WriteAllText(targetPath, Ass(target), utf8);
      return new FixtureFiles(referencePath, targetPath, reference, target);
    }

    /// <summary>
    /// The usual pair: <paramref name="count"/> lines, a silence before two of
    /// them, and a target that runs late from <paramref name="cuts"/> onwards,
    /// so that the lists show orange, gray and plain rows at once.
    /// </summary>
    internal static FixtureFiles Pair(
      string dir, int count = 20, int seed = 3, int[]? silenceBefore = null,
      params (int FromIndex, double ExtraMs)[] cuts)
    {
      var reference = Dialogue(count, seed, 800, silenceBefore ?? new[] { 6, 14 });
      return Write(dir, reference, Derive(reference, cuts));
    }

    private static string Srt(IReadOnlyList<RetimerLine> lines)
    {
      var sb = new StringBuilder();
      for (int i = 0; i < lines.Count; i++)
        sb.Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append('\n')
          .Append(TimeFormat.FormatSrt(lines[i].Start)).Append(" --> ")
          .Append(TimeFormat.FormatSrt(lines[i].End)).Append('\n')
          .Append("Reference ").Append(lines[i].Text).Append('\n').Append('\n');
      return sb.ToString();
    }

    private static string Ass(IReadOnlyList<RetimerLine> lines)
    {
      var sb = new StringBuilder(
        "[Script Info]\nScriptType: v4.00+\n\n[V4+ Styles]\n" +
        "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, " +
        "Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, " +
        "Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n" +
        "Style: Default,Arial,48,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,10,10,10,1\n\n" +
        "[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n");
      foreach (var line in lines)
        sb.Append("Dialogue: 0,")
          .Append(TimeFormat.FormatAss(line.Start)).Append(',')
          .Append(TimeFormat.FormatAss(line.End))
          .Append(",Default,,0,0,0,,Target ").Append(line.Text).Append('\n');
      return sb.ToString();
    }
  }
}
