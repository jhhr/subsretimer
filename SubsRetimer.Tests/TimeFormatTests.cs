//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;
using Xunit;

namespace SubsRetimer.Tests
{
  public class TimeFormatTests
  {
    [Theory]
    [InlineData("0:00:00.00", 0)]
    [InlineData("0:01:31.08", 91080)]
    [InlineData("1:02:03.45", 3723450)]
    [InlineData("0:00:01.5", 1500)]
    [InlineData("0:00:01,50", 1500)]
    [InlineData("10:00:00.00", 36000000)]
    public void ParseAss(string text, double ms)
    {
      Assert.True(TimeFormat.TryParseAss(text, out var t));
      Assert.Equal(ms, t.TotalMilliseconds);
    }

    [Theory]
    [InlineData("00:00:00,000", 0)]
    [InlineData("00:01:31,080", 91080)]
    [InlineData("01:02:03,456", 3723456)]
    [InlineData("00:00:01.500", 1500)]
    public void ParseSrt(string text, double ms)
    {
      Assert.True(TimeFormat.TryParseSrt(text, out var t));
      Assert.Equal(ms, t.TotalMilliseconds);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1:2")]
    [InlineData("")]
    public void ParseRejectsGarbage(string text)
    {
      Assert.False(TimeFormat.TryParseAss(text, out _));
      Assert.False(TimeFormat.TryParseSrt(text, out _));
    }

    [Fact]
    public void FormatAss_RoundsToCentiseconds()
    {
      Assert.Equal("0:01:31.08", TimeFormat.FormatAss(TimeSpan.FromMilliseconds(91080)));
      Assert.Equal("0:00:00.01", TimeFormat.FormatAss(TimeSpan.FromMilliseconds(5)));
      Assert.Equal("0:00:00.00", TimeFormat.FormatAss(TimeSpan.FromMilliseconds(4)));
      Assert.Equal("1:02:03.46", TimeFormat.FormatAss(TimeSpan.FromMilliseconds(3723456)));
    }

    [Fact]
    public void FormatSrt_KeepsMilliseconds()
    {
      Assert.Equal("00:01:31,080", TimeFormat.FormatSrt(TimeSpan.FromMilliseconds(91080)));
      Assert.Equal("01:02:03,456", TimeFormat.FormatSrt(TimeSpan.FromMilliseconds(3723456)));
    }

    [Fact]
    public void Format_ClampsNegativeToZero()
    {
      Assert.Equal("0:00:00.00", TimeFormat.FormatAss(TimeSpan.FromSeconds(-3)));
      Assert.Equal("00:00:00,000", TimeFormat.FormatSrt(TimeSpan.FromSeconds(-3)));
    }

    [Fact]
    public void FormatOffset_Signed()
    {
      Assert.Equal("+1.250s", TimeFormat.FormatOffset(TimeSpan.FromMilliseconds(1250)));
      Assert.Equal("-90.000s", TimeFormat.FormatOffset(TimeSpan.FromSeconds(-90)));
      Assert.Equal("+0.000s", TimeFormat.FormatOffset(TimeSpan.Zero));
    }
  }
}
