//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

namespace SubsRetimer
{
  public static class Program
  {
    public static int Main(string[] args)
    {
      // Before anything can touch GTK: on Windows the bundled GTK runtime is
      // only found through these variables. A no-op on every other platform.
      WindowsRuntimeSetup.Apply();
      return Cli.Run(args, Console.Out, Console.Error);
    }
  }
}
