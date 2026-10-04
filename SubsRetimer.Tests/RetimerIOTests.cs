//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using SubsRetimer.Core;
using Xunit;

namespace SubsRetimer.Tests
{
  public class RetimerIOTests
  {
    private static readonly TimeSpan S1 = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan S2 = TimeSpan.FromMilliseconds(5000);

    // ── ASS ──────────────────────────────────────────────────────────────

    [Fact]
    public void Ass_ParsesFieldsAndKeepsRawIndex()
    {
      string content = Fixtures.AssHeader +
        Fixtures.AssDialogue(S2, S2 + S1, "second, with comma", "Sign", "Bob") + "\n" +
        "Comment: 0,0:00:00.00,0:00:01.00,Default,,0,0,0,,not a dialogue\n" +
        Fixtures.AssDialogue(S1, S1 + S1, "{\\i1}first{\\i0}\\Nline") + "\n";
      var file = RetimerIO.Load(Fixtures.WriteTemp(".ass", content));

      Assert.Equal(SubtitleFormat.Ass, file.Format);
      Assert.Equal(2, file.Lines.Count);
      // sorted by start: the later-in-file line comes first
      Assert.Equal(S1, file.Lines[0].Start);
      Assert.Equal("{\\i1}first{\\i0}\\Nline", file.Lines[0].Text);
      Assert.Equal("first line", file.Lines[0].DisplayText);
      Assert.Equal(S2, file.Lines[1].Start);
      Assert.Equal("second, with comma", file.Lines[1].Text);
      Assert.Equal("Sign", file.Lines[1].Style);
      Assert.Equal("Bob", file.Lines[1].Actor);
      Assert.StartsWith("Dialogue:", file.RawLines[file.Lines[1].RawIndex]);
    }

    [Fact]
    public void Ass_RoundTripIsByteIdentical_Lf()
    {
      string content = Fixtures.AssFile(Fixtures.Lines(5));
      string path = Fixtures.WriteTemp(".ass", content);
      var file = RetimerIO.Load(path);
      string outPath = Fixtures.TempPath(".ass");
      RetimerIO.Save(file, outPath);
      Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(outPath));
    }

    [Fact]
    public void Ass_RoundTripIsByteIdentical_CrlfWithBom()
    {
      string content = Fixtures.AssFile(Fixtures.Lines(5), "\r\n");
      string path = Fixtures.WriteTemp(".ass", content, bom: true);
      var file = RetimerIO.Load(path);
      Assert.True(file.HasBom);
      Assert.Equal("\r\n", file.NewLine);
      string outPath = Fixtures.TempPath(".ass");
      RetimerIO.Save(file, outPath);
      Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(outPath));
    }

    [Fact]
    public void Ass_CustomFormatOrder_OnlyTimesChange()
    {
      string content =
        "[Events]\nFormat: Start, End, Style, Layer, Text\n" +
        "Dialogue: 0:00:01.00,0:00:02.00,Default,0,hello, world\n";
      var file = RetimerIO.Load(Fixtures.WriteTemp(".ass", content));
      Assert.Single(file.Lines);
      Assert.Equal("Default", file.Lines[0].Style);
      Assert.Equal("hello, world", file.Lines[0].Text);

      file.Lines[0].Start += TimeSpan.FromSeconds(10);
      file.Lines[0].End += TimeSpan.FromSeconds(10);
      Assert.Equal(
        "[Events]\nFormat: Start, End, Style, Layer, Text\n" +
        "Dialogue: 0:00:11.00,0:00:12.00,Default,0,hello, world\n",
        RetimerIO.Render(file));
    }

    [Fact]
    public void Ass_ShiftedSave_ChangesOnlyTimestamps()
    {
      var lines = Fixtures.Lines(3);
      string path = Fixtures.WriteTemp(".ass", Fixtures.AssFile(lines));
      var file = RetimerIO.Load(path);
      foreach (var l in file.Lines) { l.Start += TimeSpan.FromSeconds(90); l.End += TimeSpan.FromSeconds(90); }
      string rendered = RetimerIO.Render(file);

      string[] before = File.ReadAllText(path).Split('\n');
      string[] after = rendered.Split('\n');
      Assert.Equal(before.Length, after.Length);
      for (int i = 0; i < before.Length; i++)
      {
        if (before[i].StartsWith("Dialogue:"))
        {
          Assert.Equal(before[i].Split(',', 10)[9], after[i].Split(',', 10)[9]); // text
          Assert.NotEqual(before[i], after[i]);
        }
        else Assert.Equal(before[i], after[i]);
      }
      Assert.Contains("Dialogue: 0,0:01:31.00,0:01:32.80,", rendered);
    }

    // ── SRT ──────────────────────────────────────────────────────────────

    [Fact]
    public void Srt_ParsesMultilineTextAndKeepsNumbering()
    {
      string content =
        "1\n00:00:01,000 --> 00:00:02,000\nfirst\nsecond line\n\n" +
        "2\n00:00:05,000 --> 00:00:06,500 X1:10 X2:20\n<i>styled</i>\n\n";
      var file = RetimerIO.Load(Fixtures.WriteTemp(".srt", content));

      Assert.Equal(SubtitleFormat.Srt, file.Format);
      Assert.Equal(2, file.Lines.Count);
      Assert.Equal("first\nsecond line", file.Lines[0].Text);
      Assert.Equal("first second line", file.Lines[0].DisplayText);
      Assert.Equal("styled", file.Lines[1].DisplayText);
      Assert.Equal(TimeSpan.FromMilliseconds(6500), file.Lines[1].End);

      file.Lines[1].Start += TimeSpan.FromSeconds(1);
      file.Lines[1].End += TimeSpan.FromSeconds(1);
      Assert.Equal(
        "1\n00:00:01,000 --> 00:00:02,000\nfirst\nsecond line\n\n" +
        "2\n00:00:06,000 --> 00:00:07,500 X1:10 X2:20\n<i>styled</i>\n\n",
        RetimerIO.Render(file));
    }

    [Fact]
    public void Srt_RoundTripIsByteIdentical_CrlfWithBom()
    {
      string content = Fixtures.SrtFile(Fixtures.Lines(6), "\r\n");
      string path = Fixtures.WriteTemp(".srt", content, bom: true);
      var file = RetimerIO.Load(path);
      string outPath = Fixtures.TempPath(".srt");
      RetimerIO.Save(file, outPath);
      Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(outPath));
    }

    [Fact]
    public void Srt_TextThatLooksLikeAnIndexIsKeptWhenNotFollowedByTime()
    {
      string content = "1\n00:00:01,000 --> 00:00:02,000\n42\nanswer\n\n";
      var file = RetimerIO.Load(Fixtures.WriteTemp(".srt", content));
      Assert.Single(file.Lines);
      Assert.Equal("42\nanswer", file.Lines[0].Text);
    }

    [Fact]
    public void Srt_NonUtf8EncodingRoundTrips()
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      var sjis = Encoding.GetEncoding("shift_jis");
      string content = "1\n00:00:01,000 --> 00:00:02,000\n日本語のテキスト\n\n";
      string path = Fixtures.WriteTemp(".srt", content, sjis);
      var file = RetimerIO.Load(path, sjis);
      Assert.Equal("日本語のテキスト", file.Lines[0].Text);
      string outPath = Fixtures.TempPath(".srt");
      RetimerIO.Save(file, outPath);
      Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(outPath));
      Assert.False(file.HasInvalidBytes);
    }

    // ── Bytes the encoding cannot read ───────────────────────────────────

    [Fact]
    public void Load_BytesNotValidInTheEncoding_AreFlagged()
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      string content = "1\n00:00:01,000 --> 00:00:02,000\n日本語のテキスト\n\n";
      string path = Fixtures.WriteTemp(".srt", content, Encoding.GetEncoding("shift_jis"));

      // Read as the default UTF-8, the Shift-JIS text cannot survive: the
      // file loads (its timings are fine) but says so.
      var wrong = RetimerIO.Load(path);
      Assert.True(wrong.HasInvalidBytes);
      Assert.Contains('\uFFFD', wrong.Lines[0].Text);
      Assert.Equal(TimeSpan.FromSeconds(1), wrong.Lines[0].Start);

      Assert.False(RetimerIO.Load(path, Encoding.GetEncoding("shift_jis")).HasInvalidBytes);
    }

    [Fact]
    public void Load_ValidUtf8_WithOrWithoutBom_IsNotFlagged()
    {
      string content = "1\n00:00:01,000 --> 00:00:02,000\nこんにちは é\n\n";
      Assert.False(RetimerIO.Load(Fixtures.WriteTemp(".srt", content)).HasInvalidBytes);
      Assert.False(RetimerIO.Load(Fixtures.WriteTemp(".srt", content, bom: true)).HasInvalidBytes);
    }

    // ── Output names ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(SubtitleFormat.Ass, "out.ass", true)]
    [InlineData(SubtitleFormat.Ass, "out.SSA", true)]
    [InlineData(SubtitleFormat.Ass, "out.srt", false)]
    [InlineData(SubtitleFormat.Srt, "out.srt", true)]
    [InlineData(SubtitleFormat.Srt, "out.ass", false)]
    [InlineData(SubtitleFormat.Srt, "out.ssa", false)]
    // Not a subtitle name at all: the caller's business, as before.
    [InlineData(SubtitleFormat.Ass, "out.tmp", true)]
    [InlineData(SubtitleFormat.Srt, "out", true)]
    public void OutputFormatProblem_OnlyRefusesTheOtherFormatsExtension(SubtitleFormat format, string path, bool fine)
    {
      Assert.Equal(fine, RetimerIO.OutputFormatProblem(format, path) == null);
    }

    [Fact]
    public void Save_AsTheOtherFormatsExtension_ThrowsAndWritesNothing()
    {
      var file = RetimerIO.Load(Fixtures.WriteTemp(".ass", Fixtures.AssFile(Fixtures.Lines(3))));
      string outPath = Fixtures.TempPath(".srt");

      var ex = Assert.Throws<NotSupportedException>(() => RetimerIO.Save(file, outPath));
      Assert.Contains("ASS subtitles are saved as .ass or .ssa", ex.Message);
      Assert.False(File.Exists(outPath));
    }

    // ── Misc ─────────────────────────────────────────────────────────────

    [Fact]
    public void DefaultOutputPath_AppendsRetimed()
    {
      string p = Path.Combine("dir", "ep01.ass");
      Assert.Equal(Path.Combine("dir", "ep01_retimed.ass"), RetimerIO.DefaultOutputPath(p));
      Assert.Equal("x_retimed.srt", RetimerIO.DefaultOutputPath("x.srt"));
    }

    [Fact]
    public void UnsupportedExtensionThrows()
    {
      Assert.False(RetimerIO.IsSupported("a.txt"));
      Assert.Throws<NotSupportedException>(() => RetimerIO.FormatFor("a.txt"));
    }
  }
}
