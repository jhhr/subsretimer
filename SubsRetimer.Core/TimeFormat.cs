//  Copyright (C) 2012 Christopher Brochtrup
//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;

namespace SubsRetimer.Core
{
  /// <summary>Parsing and formatting of ASS (<c>H:MM:SS.cc</c>) and SRT (<c>HH:MM:SS,mmm</c>) timestamps.</summary>
  public static class TimeFormat
  {
    private static readonly Regex AssRegex =
      new(@"^\s*(?<h>\d+):(?<m>\d{1,2}):(?<s>\d{1,2})(?:[.,](?<f>\d{1,3}))?\s*$", RegexOptions.Compiled);

    private static readonly Regex SrtRegex =
      new(@"^\s*(?<h>\d+):(?<m>\d{1,2}):(?<s>\d{1,2})(?:[,.](?<f>\d{1,3}))?\s*$", RegexOptions.Compiled);

    /// <summary>Parse an ASS timestamp. The fraction is centiseconds when two digits, milliseconds when three.</summary>
    public static bool TryParseAss(string text, out TimeSpan time) => TryParse(AssRegex, text, out time);

    /// <summary>Parse an SRT timestamp (<c>HH:MM:SS,mmm</c>; a dot is also accepted).</summary>
    public static bool TryParseSrt(string text, out TimeSpan time) => TryParse(SrtRegex, text, out time);

    private static bool TryParse(Regex regex, string text, out TimeSpan time)
    {
      time = TimeSpan.Zero;
      var m = regex.Match(text);
      if (!m.Success) return false;

      int h = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
      int min = int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture);
      int s = int.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture);
      int ms = 0;
      if (m.Groups["f"].Success)
      {
        string f = m.Groups["f"].Value;
        // "5" -> 500 ms, "50" -> 500 ms, "500" -> 500 ms
        ms = int.Parse(f.PadRight(3, '0'), CultureInfo.InvariantCulture);
      }

      time = new TimeSpan(0, h, min, s, ms);
      return true;
    }

    /// <summary>Format as <c>H:MM:SS.cc</c>, rounded to the nearest centisecond. Negative times clamp to zero.</summary>
    public static string FormatAss(TimeSpan time)
    {
      long cs = (long)Math.Round(Math.Max(0, time.TotalMilliseconds) / 10.0, MidpointRounding.AwayFromZero);
      long h = cs / 360000; cs -= h * 360000;
      long m = cs / 6000; cs -= m * 6000;
      long s = cs / 100; cs -= s * 100;
      return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}.{3:00}", h, m, s, cs);
    }

    /// <summary>Format as <c>HH:MM:SS,mmm</c>. Negative times clamp to zero.</summary>
    public static string FormatSrt(TimeSpan time)
    {
      long ms = (long)Math.Round(Math.Max(0, time.TotalMilliseconds), MidpointRounding.AwayFromZero);
      long h = ms / 3600000; ms -= h * 3600000;
      long m = ms / 60000; ms -= m * 60000;
      long s = ms / 1000; ms -= s * 1000;
      return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00},{3:000}", h, m, s, ms);
    }

    /// <summary>Human readable signed offset, e.g. <c>+1.250s</c>.</summary>
    public static string FormatOffset(TimeSpan offset)
    {
      string sign = offset < TimeSpan.Zero ? "-" : "+";
      return string.Format(CultureInfo.InvariantCulture, "{0}{1:0.000}s", sign, Math.Abs(offset.TotalSeconds));
    }
  }
}
