//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using SubsRetimer.Core;
using Xunit;

namespace SubsRetimer.Tests
{
  /// <summary>stdout and stderr read by another program: UTF-8 without a byte order mark whenever they are redirected.</summary>
  public partial class CliTests
  {
    /// <summary>
    /// A pair in a folder with a Japanese name, started as the subs2srs
    /// launcher starts subsretimer: no shell, both streams redirected and
    /// read as UTF-8, and CreateNoWindow, which on Windows gives the child a
    /// console of its own. Written in that console's code page, the path came
    /// out as question marks.
    ///
    /// Outside Windows .NET takes a console stream's encoding from the
    /// charset in LC_ALL, LC_MESSAGES or LANG, so a Latin-1 locale, which has
    /// no Japanese either, does the same there; Windows ignores the variable.
    /// With it the test fails on Linux too when redirected output follows the
    /// console's encoding.
    /// </summary>
    [Fact]
    public void RealProcess_JapaneseNames_ReachTheCallerIntact()
    {
      // Fixtures.WriteTemp names its files in ASCII; these names are the point.
      string dir = Path.Combine(Path.GetTempPath(), "subsretimer-tests", "字幕フォルダ-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(dir);
      try
      {
        var (rf, tg) = MakePair();
        string reference = Path.Combine(dir, "第1話 英語.srt");
        string target = Path.Combine(dir, "第1話 日本語.ass");
        File.Move(rf, reference);
        File.Move(tg, target);
        string saved = Path.GetFullPath(RetimerIO.DefaultOutputPath(target));

        var r = RunProcess(new[] { "--auto", "--print-output", reference, target }, psi =>
        {
          psi.CreateNoWindow = true;
          psi.Environment["LC_ALL"] = "en_US.ISO-8859-1";
        });

        Assert.Equal(0, r.Code);
        Assert.False(r.Out.StartsWith('﻿'), "stdout begins with a UTF-8 byte order mark");
        Assert.Equal(saved + Environment.NewLine, r.Out);
        Assert.True(File.Exists(saved), saved);
        Assert.Contains("saved " + saved + Environment.NewLine, r.Err);
        Assert.Contains("第1話 英語.srt: 120 lines, 第1話 日本語.ass: 120 lines", r.Err);
      }
      finally
      {
        Directory.Delete(dir, recursive: true);
      }
    }

    [Fact]
    public void Utf8Writer_WritesUtf8WithoutAByteOrderMark_AndFlushesEveryWrite()
    {
      using var stream = new MemoryStream();
      var writer = Program.Utf8Writer(stream);

      writer.WriteLine("字幕.srt");

      // Read before any Flush or Dispose: what a caller sees while the
      // process is still running. A preamble would come first, as the stream
      // starts at position 0.
      Assert.Equal(Encoding.UTF8.GetBytes("字幕.srt" + Environment.NewLine), stream.ToArray());
    }
  }
}
