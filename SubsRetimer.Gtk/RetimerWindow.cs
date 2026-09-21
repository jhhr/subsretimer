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

    private const string NoFile = "no file";

    /// <summary>True while a file is being loaded, when the store has yet to catch up with the engine.</summary>
    private bool _loading;

    public RetimerWindow(Gtk.Application application)
    {
      SetApplication(application);
      SetTitle(WindowTitle);
      SetDefaultSize(1100, 700);

      RetimerStyles.Install();
      SetChild(BuildLayout());

      _engine.Changed += OnEngineChanged;
      _referenceList.SelectionChanged += OnSelectionChanged;
      _targetList.SelectionChanged += OnSelectionChanged;

      RefreshAll();
    }

    // ── Internal surface used by the tests and by the phases after this one ──

    /// <summary>The model behind the window. Tests drive it directly.</summary>
    internal RetimerEngine Engine => _engine;

    /// <summary>Files saved from this window, in order. Filled once saving exists; the exit code follows it.</summary>
    internal IReadOnlyList<string> SavedPaths => _savedPaths;

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

    // ── Layout ───────────────────────────────────────────────────────────

    private Gtk.Widget BuildLayout()
    {
      var root = Gtk.Box.New(Gtk.Orientation.Vertical, 0);

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

      return pane;
    }

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
      return strip;
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

    private void ShowError(string message, string detail)
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
