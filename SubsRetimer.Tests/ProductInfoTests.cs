//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using SubsRetimer.Core;
using Xunit;

namespace SubsRetimer.Tests
{
  public class ProductInfoTests
  {
    [Theory]
    [InlineData("0.1.0+4b43e55cafe", "0.1.0")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3-beta.1+sha", "1.2.3-beta.1")]
    [InlineData("", null)]
    [InlineData("+sha", null)]
    [InlineData(null, null)]
    public void Normalize_DropsTheBuildMetadata(string? informational, string? expected)
    {
      Assert.Equal(expected, ProductInfo.Normalize(informational));
    }

    [Fact]
    public void Version_IsTheBuildsVersion()
    {
      // Every assembly of the build carries Directory.Build.props's version.
      Assert.Matches(@"^\d+\.\d+\.\d+", ProductInfo.Version);
      Assert.Equal(ProductInfo.Version, ProductInfo.FromAssembly(typeof(Cli).Assembly));
      Assert.Equal(ProductInfo.Version, ProductInfo.FromAssembly(typeof(SubsRetimer.Editor.EditorHost).Assembly));
    }
  }
}
