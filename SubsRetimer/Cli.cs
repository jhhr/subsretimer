//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using SubsRetimer.Core;
using SubsRetimer.Editor;

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
      public bool CheckEditor;
    }

    public static string Version => ProductInfo.Version;

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
  --check-editor          check that the editor can start here (GTK 4 and a
                          display), then exit: 0 if it can, 1 if not
  -h, --help              show this help
  --version               show the version

In the editor, Save writes to --output when it is given, else where Save As
last wrote, else to <TARGET>_retimed.<ext>, asking before it replaces a file
there that it did not write. Files opened from the editor are read in the
encodings given here. A TARGET that is not valid text in its encoding is
refused: saving it would change the characters that could not be read.

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
          case "--check-editor": o.CheckEditor = true; break;
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
      if (o.CheckEditor) return CheckEditor(stderr);

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
      var file = RetimerIO.Load(path, GetEncoding(encoding));
      // The target is written back, so text that did not decode would be
      // saved as U+FFFD. The reference is only read for its timings.
      if (role == "Target" && file.HasInvalidBytes)
        throw new FormatException(
          $"Target file is not valid {file.Encoding.WebName} text: {path}; " +
          "pass --target-encoding with its encoding (latin1 keeps every byte as it is)");
      return file;
    }

    /// <summary>An <c>--output</c> that names the other subtitle format is refused before anything runs.</summary>
    private static void CheckOutput(string? output, SubtitleFile target)
    {
      if (output == null) return;
      string? problem = RetimerIO.OutputFormatProblem(target.Format, output);
      if (problem != null) throw new ArgumentException("--output: " + problem);
    }

    private static int RunAuto(Options o, TextWriter stdout, TextWriter stderr)
    {
      if (o.Reference == null || o.Target == null)
        throw new ArgumentException("--auto requires both REFERENCE and TARGET.");

      var engine = new RetimerEngine();
      engine.LoadReference(LoadChecked(o.Reference, o.RefEncoding, "Reference"));
      engine.LoadTarget(LoadChecked(o.Target, o.TargetEncoding, "Target"));

      CheckOutput(o.Output, engine.Target!);
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

    /// <summary>
    /// Whether the editor can start here. A seam: a test can answer it without
    /// touching GTK, which must never start inside <c>dotnet test</c>.
    /// </summary>
    internal static Func<EditorProbe> ProbeEditor = EditorHost.Probe;

    /// <summary>The fuller probe <c>--check-editor</c> runs, GTK's initialisation included. A seam for the same reason.</summary>
    internal static Func<EditorProbe> ProbeEditorStart = EditorHost.ProbeStart;

    /// <summary>
    /// Runs the editor window, calling the request's <c>OnSaved</c> with each
    /// path as the window saves it, and returns every path it saved. The
    /// other seam, for the same reason.
    /// </summary>
    internal static Func<EditorRequest, IReadOnlyList<string>> RunWindow = EditorHost.Run;

    /// <summary>
    /// The stderr line for an editor that cannot start, or null when it can.
    /// A missing GTK is not a missing display, and saying so sends the user
    /// to the right fix.
    /// </summary>
    private static string? EditorProblem(EditorProbe probe) => probe.Status switch
    {
      EditorStatus.Ready => null,
      EditorStatus.NoDisplay => "subsretimer: cannot open a display; use --auto",
      _ => "subsretimer: GTK 4 could not be loaded" +
           (string.IsNullOrEmpty(probe.Detail) ? "" : " (" + probe.Detail + ")") +
           "; install GTK 4 or use --auto",
    };

    /// <summary>
    /// <c>--check-editor</c>: whether the editor could start, without opening
    /// it. Loads GTK and opens the display as the editor would; a packaging
    /// check (the Windows smoke test) relies on it. Nothing goes to stdout.
    /// </summary>
    private static int CheckEditor(TextWriter stderr)
    {
      string? problem = EditorProblem(ProbeEditorStart());
      if (problem != null)
      {
        stderr.WriteLine(problem);
        return ExitError;
      }
      stderr.WriteLine("subsretimer: the editor can start here");
      return ExitSaved;
    }

    private static int RunEditor(Options o, TextWriter stdout, TextWriter stderr)
    {
      // Validate what was given so bad paths fail before a window opens.
      Encoding referenceEncoding = GetEncoding(o.RefEncoding);
      Encoding targetEncoding = GetEncoding(o.TargetEncoding);
      SubtitleFile? reference = o.Reference != null ? LoadChecked(o.Reference, o.RefEncoding, "Reference") : null;
      SubtitleFile? target = o.Target != null ? LoadChecked(o.Target, o.TargetEncoding, "Target") : null;
      if (o.Output != null && target == null)
        throw new ArgumentException("--output needs TARGET: it names where that file is saved.");
      if (target != null) CheckOutput(o.Output, target);

      string? problem = EditorProblem(ProbeEditor());
      if (problem != null)
      {
        stderr.WriteLine(problem);
        return ExitError;
      }

      // stdout carries saved paths and nothing else. Each one is written and
      // flushed as the window saves it, the way --auto does it, so a program
      // waiting on the editor can read a path before the window closes.
      Action<string>? onSaved = null;
      if (o.PrintOutput)
        onSaved = path => { stdout.WriteLine(path); stdout.Flush(); };

      IReadOnlyList<string> saved;
      try
      {
        // The encodings also serve files opened from the window later.
        saved = RunWindow(new EditorRequest(
          reference, target, o.Output, referenceEncoding, targetEncoding, onSaved));
      }
      catch (Exception ex)
      {
        // Whatever the window throws while it starts, the command line keeps
        // its contract: a message on stderr and exit 1, never an unhandled
        // exception. EditorHost carries it out of GTK's activate callback.
        stderr.WriteLine("subsretimer: the editor failed: " + ex.Message);
        return ExitError;
      }

      // The window collected every file it wrote; the exit code follows it.
      return saved.Count > 0 ? ExitSaved : ExitNothingSaved;
    }
  }
}
