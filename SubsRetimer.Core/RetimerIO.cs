//  Copyright (C) 2012 Christopher Brochtrup
//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using System.Text.RegularExpressions;

namespace SubsRetimer.Core
{
  /// <summary>
  /// Reads .ass/.ssa and .srt files into <see cref="SubtitleFile"/> and writes
  /// them back with only the timestamps replaced. Encoding, BOM, line
  /// endings, headers, styles, numbering and text survive untouched.
  /// </summary>
  public static class RetimerIO
  {
    private static readonly string[] DefaultAssFields =
      { "Layer", "Start", "End", "Style", "Name", "MarginL", "MarginR", "MarginV", "Effect", "Text" };

    private static readonly Regex SrtTimeLine = new(
      @"^(?<lead>\s*)(?<start>\d+:\d{1,2}:\d{1,2}(?:[,.]\d{1,3})?)\s*-->\s*(?<end>\d+:\d{1,2}:\d{1,2}(?:[,.]\d{1,3})?)(?<rest>.*)$",
      RegexOptions.Compiled);

    private static readonly Regex SrtIndexLine = new(@"^\s*\d+\s*$", RegexOptions.Compiled);

    public static bool IsSupported(string path)
    {
      string ext = Path.GetExtension(path).ToLowerInvariant();
      return ext is ".ass" or ".ssa" or ".srt";
    }

    public static SubtitleFormat FormatFor(string path)
    {
      string ext = Path.GetExtension(path).ToLowerInvariant();
      return ext switch
      {
        ".ass" or ".ssa" => SubtitleFormat.Ass,
        ".srt" => SubtitleFormat.Srt,
        _ => throw new NotSupportedException($"Unsupported subtitle format: {ext}")
      };
    }

    /// <summary>Default output path: <c>name_retimed.ext</c> next to the input.</summary>
    public static string DefaultOutputPath(string inputPath)
    {
      string dir = Path.GetDirectoryName(inputPath) ?? "";
      string name = Path.GetFileNameWithoutExtension(inputPath);
      string ext = Path.GetExtension(inputPath);
      return Path.Combine(dir, name + "_retimed" + ext);
    }

    // ── Loading ──────────────────────────────────────────────────────────

    /// <summary>Load a subtitle file. <paramref name="encoding"/> defaults to UTF-8; a BOM always wins.</summary>
    public static SubtitleFile Load(string path, Encoding? encoding = null)
    {
      byte[] bytes = File.ReadAllBytes(path);
      return Parse(path, bytes, encoding ?? new UTF8Encoding(false));
    }

    internal static SubtitleFile Parse(string path, byte[] bytes, Encoding fallback)
    {
      Encoding enc = fallback;
      bool hasBom = false;
      int skip = 0;

      if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
      {
        enc = new UTF8Encoding(true); hasBom = true; skip = 3;
      }
      else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
      {
        enc = new UnicodeEncoding(false, true); hasBom = true; skip = 2;
      }
      else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
      {
        enc = new UnicodeEncoding(true, true); hasBom = true; skip = 2;
      }

      string text = enc.GetString(bytes, skip, bytes.Length - skip);
      string newLine = text.Contains("\r\n") ? "\r\n" : "\n";
      var rawLines = text.Split('\n').ToList();
      if (newLine == "\r\n")
      {
        for (int i = 0; i < rawLines.Count; i++)
          if (rawLines[i].EndsWith('\r')) rawLines[i] = rawLines[i][..^1];
      }

      SubtitleFormat format = FormatFor(path);
      var lines = new List<RetimerLine>();
      IReadOnlyList<string> fields = Array.Empty<string>();

      if (format == SubtitleFormat.Ass)
        fields = ParseAss(rawLines, lines);
      else
        ParseSrt(rawLines, lines);

      // Stable sort by start time; ties keep file order.
      var sorted = lines.OrderBy(l => l.Start).ThenBy(l => l.RawIndex).ToList();

      return new SubtitleFile
      {
        Path = path,
        Format = format,
        Encoding = enc,
        HasBom = hasBom,
        NewLine = newLine,
        RawLines = rawLines,
        Lines = sorted,
        EventFields = fields
      };
    }

    private static IReadOnlyList<string> ParseAss(List<string> rawLines, List<RetimerLine> lines)
    {
      bool inEvents = false;
      List<string> fields = DefaultAssFields.ToList();

      for (int i = 0; i < rawLines.Count; i++)
      {
        string raw = rawLines[i];
        string trimmed = raw.Trim();

        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
          inEvents = trimmed.Equals("[Events]", StringComparison.OrdinalIgnoreCase);
          continue;
        }
        if (!inEvents) continue;

        if (trimmed.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
        {
          fields = trimmed["Format:".Length..]
            .Split(',').Select(f => f.Trim()).Where(f => f.Length > 0).ToList();
          continue;
        }

        if (!trimmed.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
          continue;

        var line = ParseAssDialogue(raw, fields, i);
        if (line != null) lines.Add(line);
      }

      return fields;
    }

    private static int FieldIndex(IReadOnlyList<string> fields, params string[] names)
    {
      for (int i = 0; i < fields.Count; i++)
        foreach (string n in names)
          if (fields[i].Equals(n, StringComparison.OrdinalIgnoreCase)) return i;
      return -1;
    }

    private static RetimerLine? ParseAssDialogue(string raw, IReadOnlyList<string> fields, int rawIndex)
    {
      int colon = raw.IndexOf(':');
      if (colon < 0) return null;
      string[] parts = raw[(colon + 1)..].Split(',', fields.Count);
      if (parts.Length < fields.Count) return null;

      int iStart = FieldIndex(fields, "Start");
      int iEnd = FieldIndex(fields, "End");
      if (iStart < 0 || iEnd < 0) return null;

      if (!TimeFormat.TryParseAss(parts[iStart], out var start)) return null;
      if (!TimeFormat.TryParseAss(parts[iEnd], out var end)) return null;

      int iStyle = FieldIndex(fields, "Style");
      int iName = FieldIndex(fields, "Name", "Actor");
      int iText = FieldIndex(fields, "Text");

      return new RetimerLine
      {
        Start = start,
        End = end,
        Style = iStyle >= 0 ? parts[iStyle].Trim() : "",
        Actor = iName >= 0 ? parts[iName].Trim() : "",
        Text = iText >= 0 ? parts[iText] : "",
        RawIndex = rawIndex
      };
    }

    private static void ParseSrt(List<string> rawLines, List<RetimerLine> lines)
    {
      int i = 0;
      while (i < rawLines.Count)
      {
        var m = SrtTimeLine.Match(rawLines[i]);
        if (!m.Success) { i++; continue; }

        if (!TimeFormat.TryParseSrt(m.Groups["start"].Value, out var start) ||
            !TimeFormat.TryParseSrt(m.Groups["end"].Value, out var end))
        {
          i++; continue;
        }

        int timeIndex = i;
        var textLines = new List<string>();
        i++;
        while (i < rawLines.Count && rawLines[i].Trim().Length > 0 &&
               !(SrtIndexLine.IsMatch(rawLines[i]) && i + 1 < rawLines.Count && SrtTimeLine.IsMatch(rawLines[i + 1])))
        {
          textLines.Add(rawLines[i]);
          i++;
        }

        lines.Add(new RetimerLine
        {
          Start = start,
          End = end,
          Text = string.Join("\n", textLines),
          RawIndex = timeIndex
        });
      }
    }

    // ── Saving ───────────────────────────────────────────────────────────

    /// <summary>Render the file with the current timings of <see cref="SubtitleFile.Lines"/>.</summary>
    public static string Render(SubtitleFile file)
    {
      var raw = new List<string>(file.RawLines);

      foreach (var line in file.Lines)
      {
        raw[line.RawIndex] = file.Format == SubtitleFormat.Ass
          ? RenderAssDialogue(raw[line.RawIndex], file.EventFields, line)
          : RenderSrtTimeLine(raw[line.RawIndex], line);
      }

      return string.Join(file.NewLine, raw);
    }

    private static string RenderAssDialogue(string raw, IReadOnlyList<string> fields, RetimerLine line)
    {
      int colon = raw.IndexOf(':');
      string prefix = raw[..(colon + 1)];
      string[] parts = raw[(colon + 1)..].Split(',', fields.Count);
      int iStart = FieldIndex(fields, "Start"), iEnd = FieldIndex(fields, "End");
      parts[iStart] = LeadingSpace(parts[iStart]) + TimeFormat.FormatAss(line.Start);
      parts[iEnd] = LeadingSpace(parts[iEnd]) + TimeFormat.FormatAss(line.End);
      return prefix + string.Join(",", parts);
    }

    private static string LeadingSpace(string field) => field[..(field.Length - field.TrimStart().Length)];

    private static string RenderSrtTimeLine(string raw, RetimerLine line)
    {
      var m = SrtTimeLine.Match(raw);
      return m.Groups["lead"].Value
        + TimeFormat.FormatSrt(line.Start) + " --> " + TimeFormat.FormatSrt(line.End)
        + m.Groups["rest"].Value;
    }

    /// <summary>Write the file to <paramref name="outputPath"/> with the same encoding, BOM and line endings.</summary>
    public static void Save(SubtitleFile file, string outputPath)
    {
      string text = Render(file);
      Encoding enc = file.Encoding;
      if (enc is UTF8Encoding) enc = new UTF8Encoding(file.HasBom);
      else if (enc is UnicodeEncoding u) enc = new UnicodeEncoding(u.GetPreamble().Length == 2 && u.GetPreamble()[0] == 0xFE, file.HasBom);

      byte[] preamble = file.HasBom ? enc.GetPreamble() : Array.Empty<byte>();
      byte[] body = enc.GetBytes(text);
      using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
      fs.Write(preamble, 0, preamble.Length);
      fs.Write(body, 0, body.Length);
    }
  }
}
