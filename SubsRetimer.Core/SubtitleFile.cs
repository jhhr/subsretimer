//  Copyright (C) 2012 Christopher Brochtrup
//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;

namespace SubsRetimer.Core
{
  public enum SubtitleFormat
  {
    Ass,
    Srt
  }

  /// <summary>
  /// A loaded subtitle file: the raw lines exactly as read, plus the timed
  /// lines that reference them. Only timings are ever changed; everything
  /// else is written back verbatim.
  /// </summary>
  public sealed class SubtitleFile
  {
    public string Path { get; init; } = "";
    public SubtitleFormat Format { get; init; }
    public Encoding Encoding { get; init; } = new UTF8Encoding(false);
    public bool HasBom { get; init; }

    /// <summary>Line terminator used by the file ("\r\n" or "\n").</summary>
    public string NewLine { get; init; } = "\n";

    /// <summary>Every line of the file without its terminator.</summary>
    public List<string> RawLines { get; init; } = new();

    /// <summary>Timed lines, sorted by start time (ties keep file order).</summary>
    public List<RetimerLine> Lines { get; init; } = new();

    /// <summary>For ASS: the field names of the <c>[Events]</c> Format line.</summary>
    public IReadOnlyList<string> EventFields { get; init; } = Array.Empty<string>();

    public string FileName => System.IO.Path.GetFileName(Path);
  }
}
