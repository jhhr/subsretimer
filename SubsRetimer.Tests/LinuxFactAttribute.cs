//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace SubsRetimer.Tests
{
  /// <summary>
  /// A <see cref="FactAttribute"/> that skips on anything but Linux: for tests
  /// that take the display away through DISPLAY and WAYLAND_DISPLAY, which
  /// only X11 and Wayland read. xUnit 2.9 cannot skip from inside a running
  /// test, so the decision is made while the tests are discovered.
  /// </summary>
  public sealed class LinuxFactAttribute : FactAttribute
  {
    public LinuxFactAttribute()
    {
      if (!OperatingSystem.IsLinux())
        Skip = "takes the display away through DISPLAY and WAYLAND_DISPLAY, which only Linux reads";
    }
  }
}
