//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;

namespace SubsRetimer
{
  public static class Program
  {
    public static int Main(string[] args)
    {
      // Before anything can touch GTK: on Windows the bundled GTK runtime is
      // only found through these variables. A no-op on every other platform.
      WindowsRuntimeSetup.Apply();

      // A redirected stream is read by a program (subs2srs reads both pipes
      // as UTF-8), so a saved path, Japanese or not, must not depend on how
      // the tool was started. .NET would otherwise write in the console's
      // encoding: on Windows the console's code page whenever the process has
      // a console, which a child started with CreateNoWindow does; elsewhere
      // the charset in LC_ALL, LC_MESSAGES or LANG. Console.OutputEncoding is
      // left alone: setting it changes the code page of the console this
      // process shares with its parent. A stream that is a console keeps
      // .NET's own writer, in the encoding that console displays.
      //
      // Through SetOut and SetError, not only the writers handed to Cli.Run:
      // then each stream has one writer, and anything that ever writes to
      // Console directly writes UTF-8 too.
      if (Console.IsOutputRedirected) Console.SetOut(Utf8Writer(Console.OpenStandardOutput()));
      if (Console.IsErrorRedirected) Console.SetError(Utf8Writer(Console.OpenStandardError()));

      return Cli.Run(args, Console.Out, Console.Error);
    }

    /// <summary>
    /// The writer for a redirected standard stream: UTF-8 without a byte order
    /// mark (a reader would take one as part of the first path), flushed on
    /// every write. Nothing flushes a writer when the process exits, whether
    /// <see cref="Main"/> returns or an exception escapes it, and a caller
    /// reads each saved path while the editor is still open.
    /// </summary>
    internal static TextWriter Utf8Writer(Stream stream) =>
      new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
  }
}
