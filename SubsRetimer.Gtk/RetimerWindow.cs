//  Copyright (C) 2012 Christopher Brochtrup
//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later
//
//  Port of Subs Re-Timer 1.0's main form to GTK 4.

using System.Globalization;
using System.Reflection;
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

    /// <summary>Overlap at or above which the detail strip shows green.</summary>
    private const double GoodOverlap = 0.5;

    private readonly RetimerEngine _engine = new();
    private readonly LineListView _referenceList = new();
    private readonly LineListView _targetList = new();
    private readonly List<string> _savedPaths = new();

    private readonly Gtk.Label _average = Gtk.Label.New("");
    private readonly Gtk.Label _referenceName = Gtk.Label.New(NoFile);
    private readonly Gtk.Label _targetName = Gtk.Label.New(NoFile);
    private readonly Gtk.Label _referenceCounter = Gtk.Label.New("");
    private readonly Gtk.Label _targetCounter = Gtk.Label.New("");
    private readonly Gtk.Label _referenceHint = Gtk.Label.New("No reference file. Open the subtitles already timed to the video.");
    private readonly Gtk.Label _targetHint = Gtk.Label.New("No target file. Open the subtitles to be re-timed.");
    private readonly Gtk.ScrolledWindow _referenceScroller = Gtk.ScrolledWindow.New();
    private readonly Gtk.ScrolledWindow _targetScroller = Gtk.ScrolledWindow.New();
    private readonly Gtk.Label _referenceStart = Gtk.Label.New("-");
    private readonly Gtk.Label _referenceText = Gtk.Label.New("");
    private readonly Gtk.Label _targetStart = Gtk.Label.New("-");
    private readonly Gtk.Label _targetText = Gtk.Label.New("");
    private readonly Gtk.Label _overlap = Gtk.Label.New("-");
    private readonly Gtk.Label _offset = Gtk.Label.New("-");

    private readonly Gtk.Button _timeShift = Gtk.Button.NewWithLabel("Time Shift");
    private readonly Gtk.Button _undo = Gtk.Button.NewWithLabel("Undo");
    private readonly Gtk.Button _redo = Gtk.Button.NewWithLabel("Redo");
    private readonly Gtk.Button _save = Gtk.Button.NewWithLabel("Save");
    private readonly Gtk.Button _saveAs = Gtk.Button.NewWithLabel("Save As...");

    private const string NoFile = "no file";

    /// <summary>True while a file is being loaded, when the store has yet to catch up with the engine.</summary>
    private bool _loading;

    /// <summary>True while the unsaved-changes prompt is up: a second close request must not open a second one.</summary>
    private bool _prompting;

    public RetimerWindow(Gtk.Application application)
    {
      SetApplication(application);
      SetTitle(WindowTitle);
      SetDefaultSize(1100, 700);

      RetimerStyles.Install();
      BuildActions(application);   // the menu built below points at these
      SetChild(BuildLayout());

      _engine.Changed += OnEngineChanged;
      _referenceList.SelectionChanged += OnSelectionChanged;
      _targetList.SelectionChanged += OnSelectionChanged;
      // True vetoes the close; the prompt finishes the job when the user answers.
      OnCloseRequest += (_, _) => VetoClose();

      RefreshAll();
    }

    // ── Internal surface used by the tests and by the phases after this one ──

    /// <summary>The model behind the window. Tests drive it directly.</summary>
    internal RetimerEngine Engine => _engine;

    /// <summary>Files saved from this window, in order; the exit code of the command line follows it.</summary>
    internal IReadOnlyList<string> SavedPaths => _savedPaths;

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

    /// <summary>Undo the last shift. False when there was nothing to undo.</summary>
    internal bool Undo() => _engine.Undo();

    /// <summary>Redo the last undone shift. False when there was nothing to redo.</summary>
    internal bool Redo() => _engine.Redo();

    /// <summary>Save the target to <c>name_retimed.ext</c>. False when there is nothing to save or the save failed.</summary>
    internal bool Save() => SaveTo(null);

    /// <summary>Save the target to <paramref name="path"/>: what the Save As dialog calls once a name is chosen.</summary>
    internal bool SaveAs(string path) => SaveTo(path);

    /// <summary>
    /// Answers the unsaved-changes prompt instead of <c>Gtk.AlertDialog</c> when
    /// set: the index of the button to press. Lets a test drive the close path
    /// with no dialog on screen.
    /// </summary>
    internal Func<Task<int>>? CloseChoice { get; set; }

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

    /// <summary>Show an already loaded file. Used by <see cref="EditorHost"/>, which loads before GTK starts.</summary>
    internal void SetFile(Side side, SubtitleFile file)
    {
      // The engine raises Changed as soon as it has the file, but this list's
      // store still holds the old rows: hold the repaint until it is rebuilt.
      _loading = true;
      try
      {
        if (side == Side.Reference) _engine.LoadReference(file);
        else _engine.LoadTarget(file);
      }
      finally
      {
        _loading = false;
      }

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
      "Ctrl+S                Save as <name>_retimed.<ext>\n" +
      "Ctrl+Shift+S          Save As...\n" +
      "Ctrl+Z / Ctrl+Y       Undo / Redo\n" +
      "Ctrl+Q                Quit\n" +
      "\n" +
      "In a list:\n" +
      "Left / Right          Select the closest line on the other side and go there\n" +
      "Ctrl+Up / Ctrl+Down   Previous / next orange row and its closest counterpart\n" +
      "Enter                 Time Shift: move the target onto the selected reference line\n" +
      "Right-click           Select the closest line on the other side\n" +
      "Middle-click          Time Shift";

    /// <summary>What About shows: the name, the version and the licence.</summary>
    internal static string AboutText =>
      string.Format(
        CultureInfo.InvariantCulture,
        "{0} {1}\n\nRe-time a subtitle file to match the timings of another.\n" +
        "Licensed under the GNU General Public License version 3 or later (GPL-3.0-or-later).",
        WindowTitle, Version);

    /// <summary>
    /// The editor's version. <c>Cli.Version</c> lives in the executable,
    /// which this library cannot reference, so the same attribute is read
    /// from this assembly; both carry the repository's version.
    /// </summary>
    private static string Version =>
      typeof(RetimerWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion.Split('+')[0]
      ?? typeof(RetimerWindow).Assembly.GetName().Version?.ToString(3)
      ?? "0.0.0";

    /// <summary>
    /// The window's actions and the accelerators that reach them. Each one
    /// calls the very method its button calls; only the way in is new here.
    /// </summary>
    private void BuildActions(Gtk.Application application)
    {
      Register(ActionOpenReference, () => OpenFile(Side.Reference), "<Control>o");
      Register(ActionOpenTarget, () => OpenFile(Side.Target), "<Control><Shift>o");
      Register(ActionSave, () => Save(), "<Control>s");
      Register(ActionSaveAs, SaveAsDialog, "<Control><Shift>s");
      Register(ActionQuit, RequestClose, "<Control>q");
      Register(ActionUndo, () => Undo(), "<Control>z");
      Register(ActionRedo, () => Redo(), "<Control>y");
      // Enter runs Time Shift, but only while a list has the focus, so it is
      // a key of the lists and not an accelerator of the whole window.
      Register(ActionTimeShift, TimeShift, null);
      Register(ActionAutoAlign, () => { }, null).SetEnabled(false);   // filled in by phase 5
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
      return pane;
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
    /// The editing buttons at the end of the detail strip. Auto Align joins
    /// them next to Time Shift in a later phase; the box is laid out for it.
    /// </summary>
    private Gtk.Widget BuildButtons()
    {
      var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
      box.SetValign(Gtk.Align.Center);
      box.SetHalign(Gtk.Align.End);

      _timeShift.OnClicked += (_, _) => TimeShift();
      _undo.OnClicked += (_, _) => Undo();
      _redo.OnClicked += (_, _) => Redo();
      _save.OnClicked += (_, _) => Save();
      _saveAs.OnClicked += (_, _) => SaveAsDialog();

      // A wider gap groups the buttons: edit, history, save.
      _undo.SetMarginStart(12);
      _save.SetMarginStart(12);

      box.Append(_timeShift);
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
      if (_loading) return;   // SetFile repaints once the store is rebuilt
      RefreshAll();
    }

    private void OnSelectionChanged()
    {
      RefreshCounters();
      RefreshDetail();
      RefreshButtons();
    }

    /// <summary>
    /// Recompute both sides' flags and repaint. Both sides, because a
    /// reference row's colour depends on the target as well.
    /// </summary>
    private void RefreshAll()
    {
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
      RefreshTitle();
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
      _undo.SetSensitive(_engine.CanUndo);
      _redo.SetSensitive(_engine.CanRedo);
      // Saving an unchanged file is allowed: it is how a copy in the retimed
      // name (or another format's encoding) is written.
      _save.SetSensitive(_engine.Target != null);
      _saveAs.SetSensitive(_engine.Target != null);

      // The menu and the accelerators reach the same methods, so they are
      // greyed out together with the buttons.
      SetActionEnabled(ActionTimeShift, CanTimeShift);
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
        SetFile(side, RetimerIO.Load(path));
      }
      catch (Exception ex)
      {
        ShowError("The subtitle file could not be opened.", ex.Message);
      }
    }

    // ── Saving ───────────────────────────────────────────────────────────

    /// <summary>
    /// Write the target to <paramref name="path"/>, or to the default
    /// <c>name_retimed.ext</c> when it is null, and remember the full path.
    /// Everything the write can throw becomes a dialog; the answer says
    /// whether the file is on disk, which the close prompt needs.
    /// </summary>
    private bool SaveTo(string? path)
    {
      if (_engine.Target == null) return false;
      try
      {
        // Full paths, because they are what --print-output hands to the
        // program that launched the editor.
        string saved = Path.GetFullPath(_engine.Save(path));
        if (!_savedPaths.Contains(saved)) _savedPaths.Add(saved);
        return true;
      }
      catch (Exception ex)
      {
        ShowError("The subtitle file could not be saved.", ex.Message);
        return false;
      }
    }

    /// <summary>
    /// Button handler: async void because a GTK callback has no caller to
    /// await it. The default output name is offered next to the target file.
    /// </summary>
    private async void SaveAsDialog()
    {
      try
      {
        if (_engine.Target == null) return;
        string suggested = Path.GetFullPath(RetimerIO.DefaultOutputPath(_engine.Target.Path));

        var dialog = Gtk.FileDialog.New();
        dialog.SetTitle("Save the retimed subtitles");
        dialog.SetInitialName(Path.GetFileName(suggested));
        string? folder = Path.GetDirectoryName(suggested);
        if (!string.IsNullOrEmpty(folder)) dialog.SetInitialFolder(Gio.FileHelper.NewForPath(folder));

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
        SaveTo(path);
      }
      catch (Exception ex)
      {
        ShowError("The subtitle file could not be saved.", ex.Message);
      }
    }

    // ── Closing ──────────────────────────────────────────────────────────

    private const int ChoiceSave = 0;
    private const int ChoiceDiscard = 1;
    private const int ChoiceCancel = 2;

    /// <summary>
    /// The close-request handler: true keeps the window open. With unsaved
    /// changes it puts the prompt up and answers true; the prompt closes the
    /// window itself once the user has chosen.
    /// </summary>
    private bool VetoClose()
    {
      if (_prompting) return true;   // one prompt at a time
      if (!_engine.IsDirty) return false;

      _prompting = true;
      _ = PromptThenClose();
      return true;
    }

    private async Task PromptThenClose()
    {
      try
      {
        int choice = await AskAboutUnsavedChanges();
        // Cancel, or anything else the dialog might answer, keeps the window.
        if (choice != ChoiceSave && choice != ChoiceDiscard) return;
        // A failed save keeps it too: the changes are still only in here.
        if (choice == ChoiceSave && !Save()) return;
        Destroy();
      }
      finally
      {
        _prompting = false;
      }
    }

    /// <summary>Which of Save / Discard / Cancel the user chose. Anything unanswered counts as Cancel.</summary>
    private async Task<int> AskAboutUnsavedChanges()
    {
      if (CloseChoice != null) return await CloseChoice();

      var dialog = new Gtk.AlertDialog();
      dialog.SetMessage("Save the changes before closing?");
      dialog.SetDetail("The re-timed lines have not been written to a file.");
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

    private void ShowError(string message, string detail) => ShowMessage(message, detail);

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
