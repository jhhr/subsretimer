//  Copyright (C) 2012 Christopher Brochtrup
//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  This file is part of Subs Re-Timer.
//
//  Subs Re-Timer is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.

using System.Text.RegularExpressions;

namespace SubsRetimer.Core
{
  /// <summary>
  /// One timed subtitle line. <see cref="RawIndex"/> points at the line in
  /// <see cref="SubtitleFile.RawLines"/> that carries the timing (the
  /// <c>Dialogue:</c> line for ASS, the <c>--&gt;</c> line for SRT) so the file
  /// can be written back with only the timings changed.
  /// </summary>
  public sealed class RetimerLine
  {
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }

    /// <summary>Text as it appears in the file (ASS override tags and SRT markup kept).</summary>
    public string Text { get; init; } = "";

    /// <summary>ASS Style field; empty for SRT.</summary>
    public string Style { get; init; } = "";

    /// <summary>ASS Name/Actor field; empty for SRT.</summary>
    public string Actor { get; init; } = "";

    /// <summary>Index of the timing line in <see cref="SubtitleFile.RawLines"/>.</summary>
    public int RawIndex { get; init; }

    public TimeSpan Duration => End - Start;

    private static readonly Regex TagRegex = new(@"\{[^}]*\}|<[^>]+>", RegexOptions.Compiled);

    /// <summary>Text with override tags and markup removed and line breaks collapsed, for display.</summary>
    public string DisplayText
    {
      get
      {
        string t = TagRegex.Replace(Text, "");
        t = t.Replace("\\N", " ").Replace("\\n", " ").Replace("\\h", " ").Replace('\n', ' ');
        return Regex.Replace(t, @"\s+", " ").Trim();
      }
    }

    public RetimerLine Clone() => (RetimerLine)MemberwiseClone();

    public override string ToString() => $"{TimeFormat.FormatAss(Start)} {DisplayText}";
  }
}
