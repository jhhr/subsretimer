//  Copyright (C) 2012 Christopher Brochtrup
//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  Port of Subs Re-Timer 1.0's main form to GTK 4.

using System.Globalization;
using System.Text;
using SubsRetimer.Core;

namespace SubsRetimer.Editor
{
  /// <summary>
  /// The editor window: the two subtitle files side by side, coloured by the
  /// engine's gap and mismatch flags, with the selected pair shown in the
  /// detail strip at the bottom.
  ///
  /// The window is a view over <see cref="RetimerEngine"/> and computes no
  /// timing of its own. It subscribes to the engine's
  /// <see cref="RetimerEngine.Changed"/> once and repaints from it; nothing
  /// polls.
  /// </summary>
  public sealed class RetimerWindow : Gtk.ApplicationWindow
  {
    internal const string WindowTitle = "Subs Re-Timer";

    /// <summary>
    /// Name the window icon is looked up under in the icon theme; also the
    /// <c>Icon=</c> of <c>dist/io.github.jhhr.subsretimer.desktop</c>. <c>make install</c> and
    /// the Windows bundle put the PNGs from <c>assets/</c> under
    /// <c>share/icons/hicolor/&lt;size&gt;x&lt;size&gt;/apps</c>. A name the theme
    /// does not know leaves the window without an icon, never an error, so
    /// nothing here has to check.
    /// </summary>
    internal const string AppIconName = "subsretimer";

    /// <summary>Overlap at or above which the detail strip shows green.</summary>
    private const double GoodOverlap = 0.5;

    private readonly RetimerEngine _engine = new();
    private readonly LineListView _referenceList = new();
    private readonly LineListView _targetList = new();
    private readonly TimelineChart _chart = new();
    private readonly List<string> _savedPaths = new();

    private readonly Gtk.Label _average = Gtk.Label.New("");
    private readonly Gtk.Label _referenceName = Gtk.Label.New(NoFile);
    private readonly Gtk.Label _targetName = Gtk.Label.New(NoFile);
    private readonly Gtk.Label _referenceCounter = Gtk.Label.New("");
    private readonly Gtk.Label _targetCounter = Gtk.Label.New("");
    private readonly Gtk.Label _referenceHint = Gtk.Label.New("No reference file. Open or drop here the subtitles already timed to the video.");
    private readonly Gtk.Label _targetHint = Gtk.Label.New("No target file. Open or drop here the subtitles to be re-timed.");
    private readonly Dictionary<Side, Gtk.DropTarget> _dropTargets = new();
    private readonly Gtk.ScrolledWindow _referenceScroller = Gtk.ScrolledWindow.New();
    private readonly Gtk.ScrolledWindow _targetScroller = Gtk.ScrolledWindow.New();
    private readonly Gtk.Label _referenceStart = Gtk.Label.New("-");
    private readonly Gtk.Label _referenceText = Gtk.Label.New("");
    private readonly Gtk.Label _targetStart = Gtk.Label.New("-");
    private readonly Gtk.Label _targetText = Gtk.Label.New("");
    private readonly Gtk.Label _overlap = Gtk.Label.New("-");
    private readonly Gtk.Label _offset = Gtk.Label.New("-");

    /// <summary>The one-line result of the last Auto Align, shown under the lists.</summary>
    private readonly Gtk.Label _status = Gtk.Label.New("");

    private readonly Gtk.Button _timeShift = Gtk.Button.NewWithLabel("Time Shift");
    private readonly Gtk.Button _autoAlign = Gtk.Button.NewWithLabel("Auto Align");
    private readonly Gtk.Button _undo = Gtk.Button.NewWithLabel("Undo");
    private readonly Gtk.Button _redo = Gtk.Button.NewWithLabel("Redo");
    private readonly Gtk.Button _save = Gtk.Button.NewWithLabel("Save");
    private readonly Gtk.Button _saveAs = Gtk.Button.NewWithLabel("Save As...");

    private const string NoFile = "no file";

    /// <summary>What the status line says when <see cref="SubsRetimer.Core.AutoAlign"/> found no segments.</summary>
    internal const string NoAlignment = "Auto Align: no alignment found";

    /// <summary>
    /// Above zero while the engine's changes are not to be repainted one by
    /// one: while a file loads (the store has yet to catch up with the
    /// engine), while Auto Align applies its segments and while a save runs.
    /// Whoever raises it repaints once when done.
    /// </summary>
    private int _holdRefresh;

    /// <summary>
    /// How many flows that may put a prompt up are under way: a save that
    /// asks before replacing a file, or a new target that asks about unsaved
    /// changes. While one is, the target is spoken for.
    /// </summary>
    private int _busy;

    /// <summary>True while the close prompt is up: a second close request must not open a second one.</summary>
    private bool _closing;

    /// <summary>A close was asked for while another prompt was up; it runs once that flow is over.</summary>
    private bool _closeDeferred;

    /// <summary>
    /// Where Save writes the target, as a full path; null for the default
    /// <c>name_retimed.ext</c>. Set by <c>--output</c> and by Save As, reset
    /// when another target is loaded.
    /// </summary>
    private string? _outputPath;

    public RetimerWindow(Gtk.Application application)
    {
      SetApplication(application);
      SetTitle(WindowTitle);
      SetIconName(AppIconName);
      SetDefaultSize(1100, 700);

      RetimerStyles.Install();
      BuildActions(application);   // the menu built below points at these
      SetChild(BuildLayout());

      _engine.Changed += OnEngineChanged;
      _referenceList.SelectionChanged += OnSelectionChanged;
      _targetList.SelectionChanged += OnSelectionChanged;
      _chart.Clicked += ChartClick;
      // True vetoes the close; the prompt finishes the job when the user answers.
      OnCloseRequest += (_, _) => VetoClose();

      RefreshAll();
    }

    // ── Internal surface used by the tests and by the phases after this one ──

    /// <summary>The model behind the window. Tests drive it directly.</summary>
    internal RetimerEngine Engine => _engine;

    /// <summary>Files saved from this window, in order; the exit code of the command line follows it.</summary>
    internal IReadOnlyList<string> SavedPaths => _savedPaths;

    /// <summary>
    /// Called with each newly saved full path the moment the file is written,
    /// once per path, exactly as it is appended to <see cref="SavedPaths"/>.
    /// The command line prints it there under <c>--print-output</c>.
    /// </summary>
    internal Action<string>? PathSaved { get; set; }

    /// <summary>True when the target has timing changes that are not in any saved file.</summary>
    internal bool IsDirty => _engine.IsDirty;

    /// <summary>True when Time Shift can run: both files loaded and a row selected on each side.</summary>
    internal bool CanTimeShift => _engine.HasBoth && SelectedReference >= 0 && SelectedTarget >= 0;

    /// <summary>Shift the target from the selected row so it starts with the selected reference row.</summary>
    internal void TimeShift()
    {
      if (!CanTimeShift) return;
      _engine.ShiftToMatch(SelectedReference, SelectedTarget);
    }

    /// <summary>True when Auto Align can run: both files are loaded. It needs no selection.</summary>
    internal bool CanAutoAlign => _engine.HasBoth;

    /// <summary>
    /// Align the whole target to the reference: one offset per run of lines,
    /// applied as one undoable shift per offset change, exactly as
    /// <c>subsretimer --auto</c> does it. The applied segments are also what
    /// the status line under the lists reports.
    /// </summary>
    /// <returns>The segments applied, in order; empty when nothing could be aligned.</returns>
    internal IReadOnlyList<AlignmentSegment> AutoAlign()
    {
      if (!CanAutoAlign) return Array.Empty<AlignmentSegment>();

      var segments = Core.AutoAlign.Compute(_engine.ReferenceLines, _engine.TargetLines);
      // One shift per segment, and each raises Changed: repaint once, after
      // the last, instead of once per segment.
      WithoutRefresh(() => Core.AutoAlign.Apply(_engine, segments));
      RefreshAll();

      SetStatus(segments.Count == 0
        ? NoAlignment
        : string.Join("; ", segments.Select(Core.AutoAlign.Describe)));
      return segments;
    }

    /// <summary>The status line under the lists: the last Auto Align result, or empty.</summary>
    internal string StatusText => _status.GetText();

    /// <summary>Show <paramref name="text"/> in the status line; an empty string hides it.</summary>
    private void SetStatus(string text)
    {
      _status.SetText(text);
      // The full text can be longer than the window; the label ellipsizes it
      // and the tooltip carries the rest.
      _status.SetTooltipText(text.Length == 0 ? null : text);
      _status.SetVisible(text.Length > 0);
    }

    /// <summary>Undo the last shift. False when there was nothing to undo.</summary>
    internal bool Undo() => _engine.Undo();

    /// <summary>Redo the last undone shift. False when there was nothing to redo.</summary>
    internal bool Redo() => _engine.Redo();

    /// <summary>
    /// The full path Save writes the target to: the <c>--output</c> path or
    /// the one Save As last wrote, else <c>name_retimed.ext</c> beside the
    /// target. Null while there is no target.
    /// </summary>
    internal string? SavePath =>
      _engine.Target == null ? null : _outputPath ?? Path.GetFullPath(RetimerIO.DefaultOutputPath(_engine.Target.Path));

    /// <summary>
    /// Save the target to <see cref="SavePath"/>. The default name is not one
    /// the user chose, so replacing a file there that this window did not
    /// write asks first, as <c>--auto</c> refuses to. False when there is
    /// nothing to save, the user said no, or the save failed.
    /// </summary>
    internal Task<bool> SaveAsync() => WhileBusy(SaveCoreAsync);

    /// <summary>
    /// Save the target to <paramref name="path"/>: what the Save As dialog
    /// calls once a name is chosen (that dialog has asked about replacing a
    /// file already). From then on Save writes there too.
    /// </summary>
    internal bool SaveAs(string path)
    {
      string full = Path.GetFullPath(path);
      if (!WriteTarget(full)) return false;
      _outputPath = full;
      return true;
    }

    /// <summary>
    /// Answers the unsaved-changes prompt instead of <c>Gtk.AlertDialog</c> when
    /// set: the index of the button to press. Lets a test drive the close and
    /// replace paths with no dialog on screen.
    /// </summary>
    internal Func<Task<int>>? CloseChoice { get; set; }

    /// <summary>
    /// Answers "replace the existing file?" instead of <c>Gtk.AlertDialog</c>
    /// when set, given the full path: true replaces it.
    /// </summary>
    internal Func<string, Task<bool>>? OverwriteChoice { get; set; }

    /// <summary>
    /// Receives each error (message, detail) instead of an error dialog when
    /// set, so that a test sees the error and no stray window is left open.
    /// </summary>
    internal Action<string, string>? ErrorShown { get; set; }

    /// <summary>The question the last prompt asked, whoever answered it. For the tests.</summary>
    internal string? LastPrompt { get; private set; }

    /// <summary>
    /// Encodings for files opened from the window, per side: what
    /// <c>--ref-encoding</c> and <c>--target-encoding</c> said. Null is
    /// UTF-8; a byte-order mark always wins.
    /// </summary>
    internal Encoding? ReferenceEncoding { get; set; }

    /// <inheritdoc cref="ReferenceEncoding"/>
    internal Encoding? TargetEncoding { get; set; }

    /// <summary>How often the whole window was repainted from the engine. Loading, a shift, an undo and Auto Align count one each; a save counts none.</summary>
    internal int RefreshCount { get; private set; }

    /// <summary>Close the window as the title bar's close button would, prompt and all.</summary>
    internal void RequestClose()
    {
      if (VetoClose()) return;
      Destroy();
    }

    internal void LoadReference(string path, Encoding? encoding = null) =>
      SetFile(Side.Reference, RetimerIO.Load(path, encoding));

    internal void LoadTarget(string path, Encoding? encoding = null) =>
      SetFile(Side.Target, RetimerIO.Load(path, encoding));

    /// <summary>
    /// Show an already loaded file. Used by <see cref="EditorHost"/>, which
    /// loads before GTK starts, with <paramref name="outputPath"/> from
    /// <c>--output</c> for the target; any other target is saved under the
    /// default name until Save As names another.
    /// </summary>
    internal void SetFile(Side side, SubtitleFile file, string? outputPath = null)
    {
      // The engine raises Changed as soon as it has the file, but this list's
      // store still holds the old rows: hold the repaint until it is rebuilt.
      WithoutRefresh(() =>
      {
        if (side == Side.Reference) _engine.LoadReference(file);
        else _engine.LoadTarget(file);
      });
      if (side == Side.Target) _outputPath = outputPath == null ? null : Path.GetFullPath(outputPath);

      // A new file makes the last alignment report meaningless.
      SetStatus("");
      ListFor(side).SetLines(side == Side.Reference ? _engine.ReferenceLines : _engine.TargetLines);
      (side == Side.Reference ? _referenceName : _targetName).SetText(file.FileName);
      (side == Side.Reference ? _referenceHint : _targetHint).SetVisible(false);
      (side == Side.Reference ? _referenceScroller : _targetScroller).SetVisible(true);
      RefreshAll();
    }

    internal void SelectReference(int index) => _referenceList.Select(index);

    internal void SelectTarget(int index) => _targetList.Select(index);

    /// <summary>The selected reference row, or -1.</summary>
    internal int SelectedReference => _referenceList.Selected;

    /// <summary>The selected target row, or -1.</summary>
    internal int SelectedTarget => _targetList.Selected;

    /// <summary>Why row <paramref name="index"/> of <paramref name="side"/> is coloured.</summary>
    internal RowState RowState(Side side, int index) => ListFor(side).StateAt(index);

    /// <summary>The <c>selected/total</c> counter shown above a list.</summary>
    internal string Counters(Side side)
    {
      var list = ListFor(side);
      return string.Format(CultureInfo.InvariantCulture, "{0}/{1}", list.Selected + 1, list.Count);
    }

    /// <summary>Everything the detail strip shows, as one line a test can assert on.</summary>
    internal string DetailSummary
    {
      get
      {
        var detail = CurrentDetail();
        return string.Format(
          CultureInfo.InvariantCulture,
          "reference {0} | target {1} | overlap {2} | {3}",
          detail.ReferenceStart, detail.TargetStart, detail.Overlap, detail.Offset);
      }
    }

    /// <summary>The start time row <paramref name="index"/> is showing, or null when it has no bound cell.</summary>
    internal string? BoundStartText(Side side, int index) => ListFor(side).BoundStartText(index);

    /// <summary>How often either store was rebuilt. Only loading a file may increase it.</summary>
    internal int StoreRebuilds => _referenceList.StoreRebuilds + _targetList.StoreRebuilds;

    // ── Menu, actions and accelerators ───────────────────────────────────

    // The window carries the actions, so their detailed names are win.<name>.
    internal const string ActionOpenReference = "open-reference";
    internal const string ActionOpenTarget = "open-target";
    internal const string ActionSave = "save";
    internal const string ActionSaveAs = "save-as";
    internal const string ActionQuit = "quit";
    internal const string ActionUndo = "undo";
    internal const string ActionRedo = "redo";
    internal const string ActionTimeShift = "time-shift";
    internal const string ActionAutoAlign = "auto-align";
    internal const string ActionHelp = "help";
    internal const string ActionAbout = "about";

    private readonly Dictionary<string, Gio.SimpleAction> _actions = new();

    /// <summary>The keys and mouse buttons the window answers to: what Help shows.</summary>
    internal const string KeyTable =
      "Ctrl+O                Open the reference file\n" +
      "Ctrl+Shift+O          Open the target file\n" +
      "Ctrl+S                Save to <name>_retimed.<ext>, or where Save As last wrote\n" +
      "Ctrl+Shift+S          Save As...\n" +
      "Ctrl+Z / Ctrl+Y       Undo / Redo\n" +
      "Ctrl+Q                Quit\n" +
      "\n" +
      "In a list:\n" +
      "Left / Right          Select the closest line on the other side and go there\n" +
      "Ctrl+Up / Ctrl+Down   Previous / next orange row and its closest counterpart\n" +
      "Enter                 Time Shift: move the target onto the selected reference line\n" +
      "Right-click           Select the closest line on the other side\n" +
      "Middle-click          Time Shift\n" +
      "Drop a file           Load it as that side's subtitles\n" +
      "\n" +
      "In the timeline:\n" +
      "Left-click            Select the next target line and its closest counterpart\n" +
      "Right-click           Select the previous target line and its closest counterpart\n" +
      "Middle-click          Time Shift\n" +
      "Left-click + / -      Zoom in / out by 2 seconds\n" +
      "Right-click + / -     Zoom in / out by 10 seconds\n" +
      "Middle-click + / -    Zoom all the way in or out (4 to 120 seconds)";

    /// <summary>What About shows: the name, the version and the licence.</summary>
    internal static string AboutText =>
      string.Format(
        CultureInfo.InvariantCulture,
        "{0} {1}\n\nRe-time a subtitle file to match the timings of another.\n" +
        "Licensed under the GNU General Public License version 3 or later (GPL-3.0-or-later).",
        WindowTitle, Version);

    /// <summary>The editor's version: the one <c>--version</c> prints.</summary>
    private static string Version => ProductInfo.Version;

    /// <summary>
    /// The window's actions and the accelerators that reach them. Each one
    /// calls the very method its button calls; only the way in is new here.
    /// </summary>
    private void BuildActions(Gtk.Application application)
    {
      Register(ActionOpenReference, () => OpenFile(Side.Reference), "<Control>o");
      Register(ActionOpenTarget, () => OpenFile(Side.Target), "<Control><Shift>o");
      Register(ActionSave, () => _ = SaveAsync(), "<Control>s");
      Register(ActionSaveAs, SaveAsDialog, "<Control><Shift>s");
      Register(ActionQuit, RequestClose, "<Control>q");
      Register(ActionUndo, () => Undo(), "<Control>z");
      Register(ActionRedo, () => Redo(), "<Control>y");
      // Enter runs Time Shift, but only while a list has the focus, so it is
      // a key of the lists and not an accelerator of the whole window.
      Register(ActionTimeShift, TimeShift, null);
      Register(ActionAutoAlign, () => AutoAlign(), null);
      Register(ActionHelp, ShowHelp, null);
      Register(ActionAbout, ShowAbout, null);

      Gio.SimpleAction Register(string name, System.Action handler, string? accelerator)
      {
        var action = Gio.SimpleAction.New(name, null!);
        action.OnActivate += (_, _) => handler();
        AddAction(action);
        _actions[name] = action;
        if (accelerator != null)
          application.SetAccelsForAction("win." + name, new[] { accelerator });
        return action;
      }
    }

    private Gtk.Widget BuildMenuBar()
    {
      var file = Gio.Menu.New();
      file.Append("Open Reference...", "win." + ActionOpenReference);
      file.Append("Open Target...", "win." + ActionOpenTarget);
      file.Append("Save", "win." + ActionSave);
      file.Append("Save As...", "win." + ActionSaveAs);
      file.Append("Quit", "win." + ActionQuit);

      var edit = Gio.Menu.New();
      edit.Append("Undo", "win." + ActionUndo);
      edit.Append("Redo", "win." + ActionRedo);
      edit.Append("Time Shift", "win." + ActionTimeShift);
      edit.Append("Auto Align", "win." + ActionAutoAlign);

      var help = Gio.Menu.New();
      help.Append("Help", "win." + ActionHelp);
      help.Append("About", "win." + ActionAbout);

      var menu = Gio.Menu.New();
      menu.AppendSubmenu("File", file);
      menu.AppendSubmenu("Edit", edit);
      menu.AppendSubmenu("Help", help);
      return Gtk.PopoverMenuBar.NewFromModel(menu);
    }

    /// <summary>Fire the window action <paramref name="name"/> the way the menu does. False when it is unknown or disabled.</summary>
    internal bool ActivateAction(string name)
    {
      if (LookupAction(name) is not Gio.SimpleAction action || !action.GetEnabled()) return false;
      action.Activate(null!);
      return true;
    }

    private void SetActionEnabled(string name, bool enabled)
    {
      if (_actions.TryGetValue(name, out var action)) action.SetEnabled(enabled);
    }

    private void ShowHelp() => ShowMessage("Keyboard and mouse", KeyTable);

    private void ShowAbout() => ShowMessage(WindowTitle, AboutText);

    // ── Layout ───────────────────────────────────────────────────────────

    private Gtk.Widget BuildLayout()
    {
      var root = Gtk.Box.New(Gtk.Orientation.Vertical, 0);

      root.Append(BuildMenuBar());

      // The timeline sits where the original has it: a fixed-height strip
      // across the whole width, directly under the menu.
      root.Append(_chart.Widget);
      root.Append(Gtk.Separator.New(Gtk.Orientation.Horizontal));

      _average.SetMarginTop(6);
      _average.SetMarginBottom(6);
      root.Append(_average);
      root.Append(Gtk.Separator.New(Gtk.Orientation.Horizontal));

      var panes = Gtk.Paned.New(Gtk.Orientation.Horizontal);
      panes.SetVexpand(true);
      panes.SetWideHandle(true);
      panes.SetResizeStartChild(true);
      panes.SetResizeEndChild(true);
      panes.SetStartChild(BuildPane(Side.Reference));
      panes.SetEndChild(BuildPane(Side.Target));
      panes.SetPosition(550);
      root.Append(panes);

      root.Append(Gtk.Separator.New(Gtk.Orientation.Horizontal));

      // The Auto Align report belongs with the lists it describes, so it sits
      // directly under them and above the detail strip. Hidden while empty.
      _status.SetXalign(0f);
      _status.SetWrap(false);
      _status.SetEllipsize(Pango.EllipsizeMode.End);
      _status.SetMarginStart(6);
      _status.SetMarginEnd(6);
      _status.SetMarginTop(4);
      _status.AddCssClass("dim-label");
      _status.SetVisible(false);
      root.Append(_status);

      root.Append(BuildDetailStrip());
      return root;
    }

    private Gtk.Widget BuildPane(Side side)
    {
      bool reference = side == Side.Reference;
      var pane = Gtk.Box.New(Gtk.Orientation.Vertical, 0);

      var header = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
      header.SetMarginStart(6);
      header.SetMarginEnd(6);
      header.SetMarginTop(6);
      header.SetMarginBottom(6);

      var title = Gtk.Label.New(reference ? "Reference" : "Target");
      title.AddCssClass("heading");
      header.Append(title);

      var name = reference ? _referenceName : _targetName;
      name.SetEllipsize(Pango.EllipsizeMode.Middle);
      name.SetWrap(false);
      name.SetHexpand(true);
      name.SetXalign(0f);
      header.Append(name);

      var counter = reference ? _referenceCounter : _targetCounter;
      counter.AddCssClass("dim-label");
      header.Append(counter);

      var open = Gtk.Button.NewWithLabel(reference ? "Open Reference" : "Open Target");
      open.OnClicked += (_, _) => OpenFile(side);
      header.Append(open);
      pane.Append(header);

      var hint = reference ? _referenceHint : _targetHint;
      hint.SetVexpand(true);
      hint.SetWrap(true);
      hint.AddCssClass(RetimerStyles.Hint);
      pane.Append(hint);

      var scroller = reference ? _referenceScroller : _targetScroller;
      scroller.SetPolicy(Gtk.PolicyType.Automatic, Gtk.PolicyType.Automatic);
      scroller.SetVexpand(true);
      scroller.SetChild(ListFor(side).View);
      scroller.SetVisible(false);   // shown once a file is loaded
      pane.Append(scroller);

      AttachInput(side);
      AttachDrop(side, pane);
      return pane;
    }

    // ── Dropped files ────────────────────────────────────────────────────

    /// <summary>
    /// Let a whole pane take a dropped file, empty or not: the drop target
    /// sits on the pane's box, which covers both the hint shown before a file
    /// is loaded and the list shown after.
    ///
    /// The target asks for a <c>GFile</c>. GTK deserializes the
    /// <c>text/uri-list</c> a file manager offers into one, so a drag from a
    /// file manager matches while a drag of plain text does not.
    /// </summary>
    private void AttachDrop(Side side, Gtk.Widget pane)
    {
      var drop = Gtk.DropTarget.New(Gio.FileHelper.GetGType(), Gdk.DragAction.Copy);
      drop.OnDrop += (_, args) => DropFile(side, args.Value);
      pane.AddController(drop);
      _dropTargets[side] = drop;
    }

    /// <summary>The drop target of one pane, so a test can see what it accepts.</summary>
    internal Gtk.DropTarget DropTargetFor(Side side) => _dropTargets[side];

    /// <summary>
    /// Load the file carried by a drop on <paramref name="side"/>'s pane.
    /// True when the drop was used, which is the answer the drop signal wants.
    /// GTK 4 drops cannot be synthesised, so this is also the test seam.
    /// </summary>
    internal bool DropFile(Side side, GObject.Value value)
    {
      string? path = PathFromDrop(value);
      if (path == null) return false;   // nothing usable was dropped

      // While a prompt is up the target is spoken for: a new one now would be
      // lost to the prompt's own answer. Refused, the drop shows as refused.
      if (side == Side.Target && PromptPending) return false;

      // Read before anything is asked: a file that cannot be used refuses the
      // drop with an error, not after a question about unsaved changes.
      var file = ReadFile(side, path);
      if (file == null) return false;
      if (side == Side.Reference)
      {
        SetFile(side, file);
        return true;
      }

      // Replacing a dirty target asks first, and the drop signal cannot wait
      // for the answer: the drop is taken and the file is shown once the user
      // has chosen (at once when there is nothing to lose).
      _ = ReplaceTargetAsync(file);
      return true;
    }

    /// <summary>
    /// The first file of a dropped value: a <c>GFile</c>, or text with one
    /// path or <c>file://</c> URI per line, which is how GTK writes a file
    /// list as plain text. Null when the value holds neither.
    /// </summary>
    private static string? PathFromDrop(GObject.Value value)
    {
      // GirCore 0.7 binds no reader for a value's own GType, but a value that
      // does not hold text refuses to transform into a string, which tells the
      // two apart without provoking a GLib assertion.
      var text = new GObject.Value(GObject.Type.String);
      if (value.Transform(text))
      {
        foreach (string line in (text.GetString() ?? "").Split('\n'))
        {
          string one = line.Trim();
          if (one.Length == 0) continue;
          // Only the first file: a pane shows one file, and a drop of several
          // is a slip far more often than a request to load the last one.
          var dropped = one.Contains("://", StringComparison.Ordinal)
            ? Gio.FileHelper.NewForUri(one)
            : Gio.FileHelper.NewForPath(one);
          return dropped.GetPath();   // null for a URI that is not a local file
        }
        return null;
      }

      return (value.GetObject() as Gio.File)?.GetPath();
    }

    // ── Keys and mouse buttons on the lists ──────────────────────────────

    /// <summary>
    /// The keys and mouse buttons of one list. The key controller sits in the
    /// <em>capture</em> phase: <c>ColumnView</c> binds Up and Down with every
    /// modifier, so a bubble-phase controller never sees Ctrl+Up.
    /// </summary>
    private void AttachInput(Side side)
    {
      var view = ListFor(side).View;

      var keys = Gtk.EventControllerKey.New();
      keys.SetPropagationPhase(Gtk.PropagationPhase.Capture);
      keys.OnKeyPressed += (_, args) => OnListKey(side, args.Keyval, args.State);
      view.AddController(keys);

      var closest = Gtk.GestureClick.New();
      closest.SetButton(3);   // right: the closest line on the other side
      closest.OnPressed += (_, args) => OnListClick(side, args.X, args.Y, timeShift: false);
      view.AddController(closest);

      var shift = Gtk.GestureClick.New();
      shift.SetButton(2);   // middle: Time Shift
      shift.OnPressed += (_, args) => OnListClick(side, args.X, args.Y, timeShift: true);
      view.AddController(shift);
    }

    /// <summary>
    /// A key pressed while <paramref name="side"/>'s list has the focus. True
    /// means it was handled here; false leaves the list its own navigation.
    /// </summary>
    private bool OnListKey(Side side, uint key, Gdk.ModifierType state)
    {
      bool control = (state & Gdk.ModifierType.ControlMask) != 0;
      switch (key)
      {
        // Sideways is "the other list", from either of them: the two lists
        // are read side by side and the closest line is the counterpart.
        case Gdk.Constants.KEY_Left:
        case Gdk.Constants.KEY_KP_Left:
        case Gdk.Constants.KEY_Right:
        case Gdk.Constants.KEY_KP_Right:
          if (control) return false;
          SelectClosestOnOtherSide(side, moveFocus: true);
          return true;

        case Gdk.Constants.KEY_Up:
        case Gdk.Constants.KEY_KP_Up:
          if (!control) return false;
          JumpToGap(side, forward: false);
          return true;

        case Gdk.Constants.KEY_Down:
        case Gdk.Constants.KEY_KP_Down:
          if (!control) return false;
          JumpToGap(side, forward: true);
          return true;

        case Gdk.Constants.KEY_Return:
        case Gdk.Constants.KEY_KP_Enter:
          if (!CanTimeShift) return false;
          TimeShift();
          return true;

        default:
          return false;
      }
    }

    /// <summary>A right or middle click on a list: the clicked row is selected first, then the action runs.</summary>
    private void OnListClick(Side side, double x, double y, bool timeShift)
    {
      int row = ListFor(side).IndexAt(x, y);
      if (row >= 0) ListFor(side).Select(row);

      if (timeShift)
      {
        TimeShift();
        ScrollToSelection();
      }
      else if (SelectClosestOnOtherSide(side) < 0)
      {
        ScrollToSelection();
      }
    }

    // ── Moving around the two lists ──────────────────────────────────────

    /// <summary>
    /// Select, on the other side, the line whose start is closest to the one
    /// selected on <paramref name="from"/>. Returns the row selected, or -1
    /// when this side has no selection or the other side is empty.
    /// </summary>
    internal int SelectClosestOnOtherSide(Side from) => SelectClosestOnOtherSide(from, moveFocus: false);

    private int SelectClosestOnOtherSide(Side from, bool moveFocus)
    {
      var line = ListFor(from).SelectedLine;
      if (line == null) return -1;

      Side other = Other(from);
      int index = RetimerEngine.ClosestIndex(line.Start, LinesOf(other));
      if (index < 0) return -1;

      ListFor(other).Select(index);
      ScrollToSelection(moveFocus ? other : null);
      return index;
    }

    /// <summary>
    /// Select the previous or next orange (large gap) row of
    /// <paramref name="side"/> and its closest counterpart on the other side.
    /// Returns the row selected, or -1 when there is no such row, in which
    /// case nothing moves.
    /// </summary>
    internal int JumpToGap(Side side, bool forward)
    {
      var list = ListFor(side);
      bool[] flags = list.GapFlags;   // the colours the window already computed
      int index = forward
        ? RetimerEngine.NextLargeGap(flags, list.Selected)
        : RetimerEngine.PreviousLargeGap(flags, list.Selected);
      if (index < 0) return -1;

      list.Select(index);
      // The counterpart scrolls both lists; with nothing to select over there
      // this row still has to come into view.
      if (SelectClosestOnOtherSide(side) < 0) ScrollToSelection();
      return index;
    }

    /// <summary>The timeline strip under the menu. Tests drive its zoom and its clicks through it.</summary>
    internal TimelineChart Chart => _chart;

    /// <summary>
    /// A click on the timeline, by GDK button number: the left button walks
    /// the target selection forward, the right one back, and the middle one
    /// runs Time Shift, as in the original.
    /// </summary>
    internal void ChartClick(uint button)
    {
      switch (button)
      {
        case TimelineChart.ButtonPrimary:
          MoveTargetSelection(1);
          break;
        case TimelineChart.ButtonSecondary:
          MoveTargetSelection(-1);
          break;
        case TimelineChart.ButtonMiddle:
          if (!CanTimeShift) break;
          TimeShift();
          ScrollToSelection();
          break;
      }
    }

    /// <summary>
    /// Select the target row <paramref name="step"/> away, clamped to the
    /// list, and bring its closest reference line along. With nothing
    /// selected yet the first row is the answer.
    /// </summary>
    private void MoveTargetSelection(int step)
    {
      int count = _targetList.Count;
      if (count == 0) return;

      int selected = SelectedTarget;
      _targetList.Select(selected < 0 ? 0 : Math.Clamp(selected + step, 0, count - 1));
      // The counterpart scrolls both lists; without one this row still has to come into view.
      if (SelectClosestOnOtherSide(Side.Target) < 0) ScrollToSelection();
    }

    /// <summary>Bring both selected rows into view; <paramref name="focus"/> also moves the keyboard focus into that list.</summary>
    private void ScrollToSelection(Side? focus = null)
    {
      _referenceList.ScrollTo(SelectedReference, focus == Side.Reference);
      _targetList.ScrollTo(SelectedTarget, focus == Side.Target);
    }

    private IReadOnlyList<RetimerLine> LinesOf(Side side) =>
      side == Side.Reference ? _engine.ReferenceLines : _engine.TargetLines;

    private static Side Other(Side side) => side == Side.Reference ? Side.Target : Side.Reference;

    private Gtk.Widget BuildDetailStrip()
    {
      var strip = Gtk.Box.New(Gtk.Orientation.Horizontal, 12);
      strip.SetMarginStart(6);
      strip.SetMarginEnd(6);
      strip.SetMarginTop(6);
      strip.SetMarginBottom(6);

      strip.Append(BuildDetailSide(Side.Reference));

      var middle = Gtk.Box.New(Gtk.Orientation.Vertical, 2);
      middle.SetValign(Gtk.Align.Center);
      _overlap.SetSizeRequest(72, -1);
      _overlap.AddCssClass(RetimerStyles.OverlapGood);
      middle.Append(_overlap);
      middle.Append(_offset);
      strip.Append(middle);

      strip.Append(BuildDetailSide(Side.Target));
      strip.Append(BuildButtons());
      return strip;
    }

    /// <summary>
    /// The editing buttons at the end of the detail strip: the two that change
    /// timings, then history, then saving.
    /// </summary>
    private Gtk.Widget BuildButtons()
    {
      var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
      box.SetValign(Gtk.Align.Center);
      box.SetHalign(Gtk.Align.End);

      _timeShift.OnClicked += (_, _) => TimeShift();
      _autoAlign.OnClicked += (_, _) => AutoAlign();
      _undo.OnClicked += (_, _) => Undo();
      _redo.OnClicked += (_, _) => Redo();
      _save.OnClicked += (_, _) => _ = SaveAsync();
      _saveAs.OnClicked += (_, _) => SaveAsDialog();

      // A wider gap groups the buttons: edit, history, save.
      _undo.SetMarginStart(12);
      _save.SetMarginStart(12);

      box.Append(_timeShift);
      box.Append(_autoAlign);
      box.Append(_undo);
      box.Append(_redo);
      box.Append(_save);
      box.Append(_saveAs);
      return box;
    }

    private Gtk.Widget BuildDetailSide(Side side)
    {
      bool reference = side == Side.Reference;
      var box = Gtk.Box.New(Gtk.Orientation.Vertical, 2);
      box.SetHexpand(true);
      // Both halves stay the same width whatever the selected text is.
      box.SetSizeRequest(200, -1);

      var start = reference ? _referenceStart : _targetStart;
      start.SetXalign(0f);
      box.Append(start);

      var text = reference ? _referenceText : _targetText;
      text.SetXalign(0f);
      text.SetWrap(false);
      text.SetEllipsize(Pango.EllipsizeMode.End);
      text.SetMaxWidthChars(10);   // let it shrink; the ellipsis does the rest
      box.Append(text);

      return box;
    }

    // ── Repainting ───────────────────────────────────────────────────────

    private void OnEngineChanged()
    {
      if (_holdRefresh > 0) return;   // whoever holds it repaints when done
      RefreshAll();
    }

    /// <summary>Run <paramref name="action"/> without repainting on each change it makes; the caller repaints after.</summary>
    private void WithoutRefresh(Action action)
    {
      _holdRefresh++;
      try
      {
        action();
      }
      finally
      {
        _holdRefresh--;
      }
    }

    private void OnSelectionChanged()
    {
      RefreshCounters();
      RefreshDetail();
      RefreshButtons();
      RefreshChart();
    }

    /// <summary>
    /// Recompute both sides' flags and repaint. Both sides, because a
    /// reference row's colour depends on the target as well.
    /// </summary>
    private void RefreshAll()
    {
      RefreshCount++;
      var reference = _engine.ReferenceLines;
      var target = _engine.TargetLines;

      _referenceList.SetFlags(
        RetimerEngine.LargeGapFlags(reference, target.Count > 0 ? target[0].Start : null),
        RetimerEngine.MismatchFlags(reference, target));
      _targetList.SetFlags(
        RetimerEngine.LargeGapFlags(target, reference.Count > 0 ? reference[0].Start : null),
        RetimerEngine.MismatchFlags(target, reference));

      var (all, matched) = _engine.AverageMismatchSeconds();
      _average.SetText(string.Format(
        CultureInfo.InvariantCulture, "Avg mismatch  all: {0:0.000}s  matched: {1:0.000}s", all, matched));

      RefreshCounters();
      RefreshDetail();
      RefreshButtons();
      RefreshChart();
      RefreshTitle();
    }

    /// <summary>
    /// Hand the chart the lines and the two selections and ask for a
    /// repaint. The chart holds no state of its own beyond the zoom, so this
    /// runs after every change and after every selection.
    /// </summary>
    private void RefreshChart()
    {
      _chart.SetLines(Side.Reference, _engine.ReferenceLines);
      _chart.SetLines(Side.Target, _engine.TargetLines);
      _chart.SetActive(Side.Reference, SelectedReference);
      _chart.SetActive(Side.Target, SelectedTarget);
      _chart.QueueDraw();
    }

    /// <summary>The window title carries the target's name and a <c>*</c> while it is dirty.</summary>
    private void RefreshTitle()
    {
      string title = _engine.Target == null ? WindowTitle : WindowTitle + " - " + _engine.Target.FileName;
      SetTitle(_engine.IsDirty ? "*" + title : title);
    }

    private void RefreshButtons()
    {
      _timeShift.SetSensitive(CanTimeShift);
      _autoAlign.SetSensitive(CanAutoAlign);
      _undo.SetSensitive(_engine.CanUndo);
      _redo.SetSensitive(_engine.CanRedo);
      // Saving an unchanged file is allowed: it is how a copy in the retimed
      // name (or another format's encoding) is written.
      _save.SetSensitive(_engine.Target != null);
      _saveAs.SetSensitive(_engine.Target != null);

      // The menu and the accelerators reach the same methods, so they are
      // greyed out together with the buttons.
      SetActionEnabled(ActionTimeShift, CanTimeShift);
      SetActionEnabled(ActionAutoAlign, CanAutoAlign);
      SetActionEnabled(ActionUndo, _engine.CanUndo);
      SetActionEnabled(ActionRedo, _engine.CanRedo);
      SetActionEnabled(ActionSave, _engine.Target != null);
      SetActionEnabled(ActionSaveAs, _engine.Target != null);
    }

    private void RefreshCounters()
    {
      _referenceCounter.SetText(Counters(Side.Reference));
      _targetCounter.SetText(Counters(Side.Target));
    }

    private void RefreshDetail()
    {
      var detail = CurrentDetail();
      _referenceStart.SetText(detail.ReferenceStart);
      _referenceText.SetText(detail.ReferenceText);
      _targetStart.SetText(detail.TargetStart);
      _targetText.SetText(detail.TargetText);
      _overlap.SetText(detail.Overlap);
      _offset.SetText(detail.Offset);

      _overlap.RemoveCssClass(RetimerStyles.OverlapGood);
      _overlap.RemoveCssClass(RetimerStyles.OverlapBad);
      if (detail.Good.HasValue)
        _overlap.AddCssClass(detail.Good.Value ? RetimerStyles.OverlapGood : RetimerStyles.OverlapBad);
    }

    /// <summary>What the detail strip shows for the current pair of selections.</summary>
    private readonly record struct Detail(
      string ReferenceStart, string ReferenceText,
      string TargetStart, string TargetText,
      string Overlap, string Offset, bool? Good);

    private Detail CurrentDetail()
    {
      var reference = _referenceList.SelectedLine;
      var target = _targetList.SelectedLine;

      string referenceStart = reference == null ? "-" : TimeFormat.FormatAss(reference.Start);
      string targetStart = target == null ? "-" : TimeFormat.FormatAss(target.Start);
      string referenceText = reference?.DisplayText ?? "";
      string targetText = target?.DisplayText ?? "";

      if (reference == null || target == null)
        return new Detail(referenceStart, referenceText, targetStart, targetText, "-", "-", null);

      // How much of the target line the reference line covers, and the shift
      // that would put them on top of each other.
      double overlap = RetimerEngine.Overlap(target.Start, target.End, reference.Start, reference.End);
      string percent = string.Format(
        CultureInfo.InvariantCulture, "{0:0}%", Math.Clamp(overlap, 0, 1) * 100);
      string offset = TimeFormat.FormatOffset(reference.Start - target.Start);

      return new Detail(referenceStart, referenceText, targetStart, targetText, percent, offset, overlap >= GoodOverlap);
    }

    // ── Opening files ────────────────────────────────────────────────────

    /// <summary>
    /// Button handler: async void because a GTK callback has no caller to
    /// await it. Everything it can throw is turned into a dialog.
    /// </summary>
    private async void OpenFile(Side side)
    {
      try
      {
        var dialog = Gtk.FileDialog.New();
        dialog.SetTitle(side == Side.Reference ? "Open reference subtitles" : "Open target subtitles");

        var filter = Gtk.FileFilter.New();
        filter.SetName("Subtitles (.ass, .ssa, .srt)");
        filter.AddSuffix("ass");
        filter.AddSuffix("ssa");
        filter.AddSuffix("srt");
        var filters = Gio.ListStore.New(Gtk.FileFilter.GetGType());
        filters.Append(filter);
        dialog.SetFilters(filters);
        dialog.SetDefaultFilter(filter);

        Gio.File? chosen;
        try
        {
          chosen = await dialog.OpenAsync(this);
        }
        catch (GLib.GException)
        {
          return;   // the user dismissed the dialog; GirCore 0.7 reports that as an error
        }

        string? path = chosen?.GetPath();
        if (path == null) return;
        await OpenPathAsync(side, path);
      }
      catch (Exception ex)
      {
        ShowError("The subtitle file could not be opened.", ex.Message);
      }
    }

    /// <summary>
    /// Show the file at <paramref name="path"/> on <paramref name="side"/>,
    /// asking about unsaved changes first when it would throw them away.
    /// Every way into the window goes through here except the command line,
    /// which loads before the window exists and so has nothing to lose.
    /// The file is read first: one that cannot be used is reported and never
    /// gets as far as the question.
    /// </summary>
    /// <returns>True when the file was loaded.</returns>
    internal async Task<bool> OpenPathAsync(Side side, string path)
    {
      if (side == Side.Target && PromptPending) return false;   // one prompt at a time

      var file = ReadFile(side, path);
      if (file == null) return false;
      if (side == Side.Reference)
      {
        SetFile(side, file);   // only the target carries the edits; a new reference costs nothing
        return true;
      }
      return await ReplaceTargetAsync(file);
    }

    /// <summary>
    /// Read <paramref name="path"/> for <paramref name="side"/> in that side's
    /// encoding. Null, and an error, when it is not a subtitle file, cannot
    /// be read, or is a target that did not decode cleanly.
    /// </summary>
    private SubtitleFile? ReadFile(Side side, string path)
    {
      try
      {
        // By name first: a dropped video must not be read whole to find out.
        if (!RetimerIO.IsSupported(path))
          throw new NotSupportedException(Path.GetFileName(path) + " is not an .ass, .ssa or .srt file.");

        var file = RetimerIO.Load(path, side == Side.Reference ? ReferenceEncoding : TargetEncoding);
        // A target is written back: characters read as U+FFFD would be saved
        // as U+FFFD. The reference is only looked at, so it may be imperfect.
        if (side == Side.Target && file.HasInvalidBytes)
          throw new InvalidDataException(string.Format(
            CultureInfo.InvariantCulture,
            "{0} is not valid {1} text, and saving it would change the characters that could not be read. " +
            "Start subsretimer with --target-encoding and the file's encoding to open it.",
            file.FileName, file.Encoding.WebName));
        return file;
      }
      catch (Exception ex)
      {
        ShowError("The subtitle file could not be opened.", ex.Message);
        return null;
      }
    }

    /// <summary>
    /// Show <paramref name="file"/> as the target. With unsaved changes this
    /// asks the same Save / Discard / Cancel question closing asks; Save that
    /// failed, Cancel and a dismissed prompt all keep the current target.
    /// </summary>
    /// <returns>True when the file was loaded.</returns>
    private Task<bool> ReplaceTargetAsync(SubtitleFile file) => WhileBusy(async () =>
    {
      if (!_engine.IsDirty)
      {
        SetFile(Side.Target, file);
        return true;
      }

      string current = _engine.Target?.FileName ?? "the target";
      int choice = await AskAboutUnsavedChanges(
        string.Format(CultureInfo.InvariantCulture, "Save the changes to {0} before opening {1}?", current, file.FileName),
        "The re-timed lines have not been written to a file. Opening another target replaces them.");
      // A failed or refused save keeps the changes: they are still only in here.
      if (choice == ChoiceDiscard || (choice == ChoiceSave && await SaveCoreAsync()))
      {
        SetFile(Side.Target, file);
        return true;
      }
      return false;
    });

    // ── Saving ───────────────────────────────────────────────────────────

    /// <summary>
    /// Run a flow that may put a prompt up. Until it is over the target is
    /// spoken for (<see cref="PromptPending"/>), and a close asked for in the
    /// meantime waits for it instead of being lost.
    /// </summary>
    private async Task<bool> WhileBusy(Func<Task<bool>> flow)
    {
      _busy++;
      try
      {
        return await flow();
      }
      finally
      {
        _busy--;
        if (_busy == 0 && _closeDeferred)
        {
          _closeDeferred = false;
          RequestClose();
        }
      }
    }

    /// <summary>True while a prompt is up or a flow that asks one is under way.</summary>
    private bool PromptPending => _busy > 0 || _closing;

    /// <summary>
    /// Save to <see cref="SavePath"/>, asking before replacing a file whose
    /// name the user did not choose and this window did not write. For use
    /// inside a flow that is busy already; <see cref="SaveAsync"/> otherwise.
    /// </summary>
    private async Task<bool> SaveCoreAsync()
    {
      string? path = SavePath;
      if (path == null) return false;

      bool chosen = _outputPath != null;   // --output or Save As named it
      if (!chosen && File.Exists(path) && !_savedPaths.Contains(path) && !await ConfirmOverwrite(path))
        return false;
      return WriteTarget(path);
    }

    /// <summary>
    /// Write the target to the full path <paramref name="path"/> and remember
    /// it. Everything the write can throw becomes an error; the answer says
    /// whether the file is on disk, which the prompts need.
    /// </summary>
    private bool WriteTarget(string path)
    {
      if (_engine.Target == null) return false;
      try
      {
        // A save changes nothing but the star in the title.
        WithoutRefresh(() => _engine.Save(path));
        RefreshTitle();

        // Full paths, because they are what --print-output hands to the
        // program that launched the editor.
        if (!_savedPaths.Contains(path))
        {
          _savedPaths.Add(path);
          // Report it now, not when the window closes: the program that
          // launched the editor can act on the file while it is still open.
          // A reporter that fails (a closed stdout) must not turn a good save
          // into an error dialog; the file is on disk either way.
          try { PathSaved?.Invoke(path); }
          catch (Exception) { /* the save stands */ }
        }
        return true;
      }
      catch (Exception ex)
      {
        ShowError("The subtitle file could not be saved.", ex.Message);
        return false;
      }
    }

    /// <summary>Whether to replace the existing file at <paramref name="path"/>. Anything unanswered is no.</summary>
    private async Task<bool> ConfirmOverwrite(string path)
    {
      string message = string.Format(CultureInfo.InvariantCulture, "Replace {0}?", Path.GetFileName(path));
      LastPrompt = message;
      if (OverwriteChoice != null) return await OverwriteChoice(path);

      var dialog = new Gtk.AlertDialog();
      dialog.SetMessage(message);
      dialog.SetDetail(string.Format(
        CultureInfo.InvariantCulture,
        "A file with this name already exists in {0}, and this window did not write it. Saving replaces its contents.",
        Path.GetDirectoryName(path)));
      dialog.SetButtons(new[] { "Replace", "Cancel" });
      dialog.SetDefaultButton(1);
      dialog.SetCancelButton(1);
      try
      {
        return await dialog.ChooseAsync(this) == 0;
      }
      catch (GLib.GException)
      {
        return false;   // dismissed, the same as Cancel
      }
    }

    /// <summary>
    /// Button handler: async void because a GTK callback has no caller to
    /// await it. Offers the name Save would write, which is the default
    /// output name next to the target until Save As or --output named another.
    /// </summary>
    private async void SaveAsDialog()
    {
      try
      {
        string? suggested = SavePath;
        if (suggested == null || _engine.Target == null) return;

        var dialog = Gtk.FileDialog.New();
        dialog.SetTitle("Save the retimed subtitles");
        dialog.SetInitialName(Path.GetFileName(suggested));
        string? folder = Path.GetDirectoryName(suggested);
        if (!string.IsNullOrEmpty(folder)) dialog.SetInitialFolder(Gio.FileHelper.NewForPath(folder));

        // The file is written in the target's own format, so offer its names.
        var filter = Gtk.FileFilter.New();
        if (_engine.Target.Format == SubtitleFormat.Ass)
        {
          filter.SetName("ASS subtitles (.ass, .ssa)");
          filter.AddSuffix("ass");
          filter.AddSuffix("ssa");
        }
        else
        {
          filter.SetName("SRT subtitles (.srt)");
          filter.AddSuffix("srt");
        }
        var filters = Gio.ListStore.New(Gtk.FileFilter.GetGType());
        filters.Append(filter);
        dialog.SetFilters(filters);
        dialog.SetDefaultFilter(filter);

        Gio.File? chosen;
        try
        {
          chosen = await dialog.SaveAsync(this);
        }
        catch (GLib.GException)
        {
          return;   // the user dismissed the dialog; GirCore 0.7 reports that as an error
        }

        string? path = chosen?.GetPath();
        if (path == null) return;
        SaveAs(path);
      }
      catch (Exception ex)
      {
        ShowError("The subtitle file could not be saved.", ex.Message);
      }
    }

    // ── Closing ──────────────────────────────────────────────────────────

    // Internal, not private: this is the prompt's button order, and a test
    // answering CloseChoice has to name the same buttons.
    internal const int ChoiceSave = 0;
    internal const int ChoiceDiscard = 1;
    internal const int ChoiceCancel = 2;

    /// <summary>What the close prompt asks.</summary>
    internal const string CloseQuestion = "Save the changes before closing?";

    /// <summary>
    /// The close-request handler: true keeps the window open. With unsaved
    /// changes it puts the prompt up and answers true; the prompt closes the
    /// window itself once the user has chosen. While another prompt is up the
    /// close waits for it, and runs once that is answered.
    /// </summary>
    private bool VetoClose()
    {
      if (_closing) return true;   // one close prompt at a time
      if (_busy > 0)
      {
        _closeDeferred = true;
        return true;
      }
      if (!_engine.IsDirty) return false;

      _closing = true;
      _ = PromptThenClose();
      return true;
    }

    private async Task PromptThenClose()
    {
      try
      {
        int choice = await AskAboutUnsavedChanges(CloseQuestion, "The re-timed lines have not been written to a file.");
        // Cancel, or anything else the dialog might answer, keeps the window.
        if (choice != ChoiceSave && choice != ChoiceDiscard) return;
        // A failed or refused save keeps it too: the changes are still only in here.
        if (choice == ChoiceSave && !await SaveCoreAsync()) return;
        Destroy();
      }
      finally
      {
        _closing = false;
      }
    }

    /// <summary>Which of Save / Discard / Cancel the user chose. Anything unanswered counts as Cancel.</summary>
    private async Task<int> AskAboutUnsavedChanges(string message, string detail)
    {
      LastPrompt = message;
      if (CloseChoice != null) return await CloseChoice();

      var dialog = new Gtk.AlertDialog();
      dialog.SetMessage(message);
      dialog.SetDetail(detail);
      dialog.SetButtons(new[] { "Save", "Discard", "Cancel" });
      dialog.SetDefaultButton(ChoiceSave);
      dialog.SetCancelButton(ChoiceCancel);
      try
      {
        return await dialog.ChooseAsync(this);
      }
      catch (GLib.GException)
      {
        return ChoiceCancel;   // dismissed, the same as Cancel
      }
    }

    private void ShowError(string message, string detail)
    {
      if (ErrorShown != null) ErrorShown(message, detail);
      else ShowMessage(message, detail);
    }

    /// <summary>A plain one-button dialog: an error, the key table or the About text.</summary>
    private void ShowMessage(string message, string detail)
    {
      // GirCore 0.7 has no Gtk.AlertDialog.New().
      var dialog = new Gtk.AlertDialog();
      dialog.SetMessage(message);
      dialog.SetDetail(detail);
      dialog.SetButtons(new[] { "OK" });
      dialog.Show(this);
    }

    private LineListView ListFor(Side side) => side == Side.Reference ? _referenceList : _targetList;
  }
}
