//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using System.Text;
using SubsRetimer.Core;

namespace SubsRetimer
{
  /// <summary>
  /// Command-line contract. Other programs (subs2srs) rely on it:
  ///
  ///   stdout carries only saved paths, one per line, when --print-output is
  ///   given; every diagnostic goes to stderr.
  ///   Exit 0: at least one file saved. Exit 2: nothing saved (window closed
  ///   without saving, or nothing to do). Exit 1: error, message on stderr.
  /// </summary>
  public static class Cli
  {
    public const int ExitSaved = 0;
    public const int ExitError = 1;
    public const int ExitNothingSaved = 2;

    public sealed class Options
    {
      public string? Reference;
      public string? Target;
      public bool Auto;
      public string? Output;
      public string RefEncoding = "utf-8";
      public string TargetEncoding = "utf-8";
      public bool PrintOutput;
      public bool ShowHelp;
      public bool ShowVersion;
    }

    public static string Version =>
      typeof(Cli).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
      ?? typeof(Cli).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public const string Usage = @"Usage: subsretimer [options] [REFERENCE] [TARGET]

Re-time TARGET so that its lines match the timings of REFERENCE.

  REFERENCE               subtitle file already timed to the video (left pane)
  TARGET                  subtitle file to be retimed (right pane)

Options:
  --auto                  run auto-align and save without opening the editor
  -o, --output PATH       output path (default: <TARGET>_retimed.<ext>)
  --ref-encoding NAME     encoding of REFERENCE (default: utf-8)
  --target-encoding NAME  encoding of TARGET (default: utf-8)
  --print-output          print the path of each saved file to stdout
  -h, --help              show this help
  --version               show the version

Exit status: 0 a file was saved, 2 nothing was saved, 1 error.
Supported formats: .ass, .ssa, .srt";

    public static Options Parse(string[] args)
    {
      var o = new Options();
      var positional = new List<string>();
      for (int i = 0; i < args.Length; i++)
      {
        string a = args[i];
        string? Next()
        {
          if (i + 1 >= args.Length) throw new ArgumentException($"{a} requires a value");
          return args[++i];
        }
        switch (a)
        {
          case "--auto": o.Auto = true; break;
          case "-o": case "--output": o.Output = Next(); break;
          case "--ref-encoding": o.RefEncoding = Next()!; break;
          case "--target-encoding": o.TargetEncoding = Next()!; break;
          case "--print-output": o.PrintOutput = true; break;
          case "-h": case "--help": o.ShowHelp = true; break;
          case "--version": o.ShowVersion = true; break;
          case "--": positional.AddRange(args.Skip(i + 1)); i = args.Length; break;
          default:
            if (a.StartsWith('-') && a.Length > 1) throw new ArgumentException($"Unknown option: {a}");
            positional.Add(a);
            break;
        }
      }
      if (positional.Count > 2) throw new ArgumentException("At most two files can be given (REFERENCE and TARGET).");
      if (positional.Count > 0) o.Reference = positional[0];
      if (positional.Count > 1) o.Target = positional[1];
      return o;
    }

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
      Options o;
      try { o = Parse(args); }
      catch (ArgumentException ex)
      {
        stderr.WriteLine("subsretimer: " + ex.Message);
        stderr.WriteLine(Usage);
        return ExitError;
      }

      if (o.ShowHelp) { stdout.WriteLine(Usage); return ExitSaved; }
      if (o.ShowVersion) { stdout.WriteLine(Version); return ExitSaved; }

      try
      {
        return o.Auto ? RunAuto(o, stdout, stderr) : RunEditor(o, stdout, stderr);
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FormatException)
      {
        stderr.WriteLine("subsretimer: " + ex.Message);
        return ExitError;
      }
    }

    private static Encoding GetEncoding(string name)
    {
      try
      {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(name);
      }
      catch (ArgumentException)
      {
        throw new ArgumentException($"Unknown encoding: {name}");
      }
    }

    private static SubtitleFile LoadChecked(string path, string encoding, string role)
    {
      if (!File.Exists(path)) throw new FileNotFoundException($"{role} file not found: {path}");
      if (!RetimerIO.IsSupported(path)) throw new NotSupportedException($"{role} file is not .ass/.ssa/.srt: {path}");
      return RetimerIO.Load(path, GetEncoding(encoding));
    }

    private static int RunAuto(Options o, TextWriter stdout, TextWriter stderr)
    {
      if (o.Reference == null || o.Target == null)
        throw new ArgumentException("--auto requires both REFERENCE and TARGET.");

      var engine = new RetimerEngine();
      engine.LoadReference(LoadChecked(o.Reference, o.RefEncoding, "Reference"));
      engine.LoadTarget(LoadChecked(o.Target, o.TargetEncoding, "Target"));

      if (!engine.HasBoth)
      {
        stderr.WriteLine("subsretimer: nothing to do, one of the files has no timed lines.");
        return ExitNothingSaved;
      }

      string output = Path.GetFullPath(o.Output ?? RetimerIO.DefaultOutputPath(o.Target));
      if (o.Output == null && File.Exists(output))
        throw new IOException($"Output already exists, pass --output to overwrite: {output}");

      var before = engine.AverageMismatchSeconds();
      var segments = AutoAlign.Compute(engine.ReferenceLines, engine.TargetLines);
      AutoAlign.Apply(engine, segments);
      var after = engine.AverageMismatchSeconds();

      stderr.WriteLine($"{engine.Reference!.FileName}: {engine.ReferenceLines.Count} lines, " +
                       $"{engine.Target!.FileName}: {engine.TargetLines.Count} lines");
      foreach (var seg in segments) stderr.WriteLine("  " + AutoAlign.Describe(seg));
      stderr.WriteLine($"average mismatch (matched lines): {before.Matched:0.000}s -> {after.Matched:0.000}s");

      string saved = engine.Save(output);
      stderr.WriteLine("saved " + saved);
      if (o.PrintOutput) { stdout.WriteLine(saved); stdout.Flush(); }
      return ExitSaved;
    }

    private static int RunEditor(Options o, TextWriter stdout, TextWriter stderr)
    {
      // Validate what was given so bad paths fail fast even before the editor exists.
      if (o.Reference != null) LoadChecked(o.Reference, o.RefEncoding, "Reference");
      if (o.Target != null) LoadChecked(o.Target, o.TargetEncoding, "Target");

      stderr.WriteLine("subsretimer: the interactive editor is not available in this build yet; use --auto.");
      return ExitError;
    }
  }
}
