//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;

namespace SubsRetimer.Core
{
  /// <summary>
  /// The product version, read once for everything that shows it: the command
  /// line's <c>--version</c> and the editor's About box. Every assembly of a
  /// build carries the same version (Directory.Build.props, or the
  /// <c>-p:Version=</c> a release publishes with), so this one reads its own.
  /// </summary>
  public static class ProductInfo
  {
    /// <summary>The version without the <c>+commit</c> suffix the SDK appends, e.g. <c>0.1.0</c>.</summary>
    public static string Version => FromAssembly(typeof(ProductInfo).Assembly);

    internal static string FromAssembly(Assembly assembly) =>
      Normalize(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
      ?? assembly.GetName().Version?.ToString(3)
      ?? "0.0.0";

    /// <summary>An informational version without its <c>+</c> build metadata; null when there is none.</summary>
    internal static string? Normalize(string? informationalVersion)
    {
      if (string.IsNullOrWhiteSpace(informationalVersion)) return null;
      string version = informationalVersion.Split('+')[0].Trim();
      return version.Length == 0 ? null : version;
    }
  }
}
