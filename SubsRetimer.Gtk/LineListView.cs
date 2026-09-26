//  Copyright (C) 2012 Christopher Brochtrup
//  Copyright (C) 2026 jhhr and contributors
//  SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using SubsRetimer.Core;

namespace SubsRetimer.Editor
{
  /// <summary>Which of the two files a call is about.</summary>
  public enum Side
  {
    /// <summary>The file already timed to the video (left pane).</summary>
    Reference,

    /// <summary>The file being re-timed (right pane).</summary>
    Target
  }

  /// <summary>Why a row is coloured. A gap wins over a mismatch.</summary>
  public enum RowState
  {
    /// <summary>Ordinary row.</summary>
    None,

    /// <summary>Starts more than <see cref="RetimerEngine.LargeGap"/> after the previous row (orange).</summary>
    Gap,

    /// <summary>No overlapping line in the other file (gray).</summary>
    Mismatch
  }

  /// <summary>
  /// One pane's list of subtitle lines: a <see cref="Gtk.ColumnView"/> over a
  /// <see cref="Gio.ListStore"/> of <see cref="Gtk.StringObject"/> (the row
  /// index as text) with the engine's lines beside it.
  ///
  /// The store is rebuilt only when a file is loaded. After a shift the rows
  /// are refreshed <em>in place</em>: every bound cell is remembered while it
  /// is bound, and <see cref="Refresh"/> rewrites its text and CSS class.
  /// Rebuilding instead would scroll the list back to the top and drop the
  /// focused row.
  /// </summary>
  internal sealed class LineListView
  {
    /// <summary>A cell widget while it is bound to a row.</summary>
    private sealed class BoundCell
    {
      internal BoundCell(Gtk.ListItem item, Gtk.Box box, Gtk.Label label)
      {
        Item = item;
        Box = box;
        Label = label;
      }

      internal Gtk.ListItem Item { get; }

      /// <summary>The box that fills the cell; it carries the row colour.</summary>
      internal Gtk.Box Box { get; }

      internal Gtk.Label Label { get; }
    }

    /// <summary>One column: what it shows and which of its cells are bound, by row index.</summary>
    private sealed class CellColumn
    {
      internal CellColumn(Func<RetimerLine, string> text) => Text = text;

      internal Func<RetimerLine, string> Text { get; }

      internal Dictionary<int, BoundCell> Bound { get; } = new();
    }

    private readonly Gio.ListStore _store;
    private readonly Gtk.SingleSelection _selection;
    private readonly CellColumn _startColumn;
    private readonly CellColumn _textColumn;
    private readonly CellColumn[] _columns;

    private IReadOnlyList<RetimerLine> _lines = Array.Empty<RetimerLine>();
    private bool[] _gap = Array.Empty<bool>();
    private bool[] _mismatch = Array.Empty<bool>();

    internal LineListView()
    {
      _store = Gio.ListStore.New(Gtk.StringObject.GetGType());
      _selection = Gtk.SingleSelection.New(_store);
      _selection.SetAutoselect(false);   // an empty selection is a real state here
      _selection.SetCanUnselect(true);
      _selection.OnSelectionChanged += (_, _) => SelectionChanged?.Invoke();

      _startColumn = new CellColumn(line => TimeFormat.FormatAss(line.Start));
      _textColumn = new CellColumn(line => line.DisplayText);
      _columns = new[] { _startColumn, _textColumn };

      View = Gtk.ColumnView.New(_selection);
      View.SetVexpand(true);
      View.SetShowColumnSeparators(true);
      // Start has a fixed width, Dialogue expands: never both on one column.
      View.AppendColumn(MakeColumn("Start", _startColumn, expand: false, fixedWidth: 96, ellipsize: false));
      View.AppendColumn(MakeColumn("Dialogue", _textColumn, expand: true, fixedWidth: 0, ellipsize: true));
    }

    /// <summary>The widget to put in a <see cref="Gtk.ScrolledWindow"/>.</summary>
    internal Gtk.ColumnView View { get; }

    /// <summary>Raised when the selected row changed, including when it was cleared.</summary>
    internal event Action? SelectionChanged;

    /// <summary>How often the store was rebuilt. A shift must not change it.</summary>
    internal int StoreRebuilds { get; private set; }

    internal IReadOnlyList<RetimerLine> Lines => _lines;

    internal int Count => _lines.Count;

    /// <summary>The selected row, or -1 when nothing is selected.</summary>
    internal int Selected
    {
      get
      {
        uint selected = _selection.GetSelected();
        return selected == Gtk.Constants.INVALID_LIST_POSITION || selected >= (uint)_lines.Count
          ? -1
          : (int)selected;
      }
    }

    internal RetimerLine? SelectedLine
    {
      get
      {
        int index = Selected;
        return index < 0 ? null : _lines[index];
      }
    }

    /// <summary>Select a row; an index outside the list clears the selection.</summary>
    internal void Select(int index)
    {
      if (index < 0 || index >= _lines.Count) _selection.SetSelected(Gtk.Constants.INVALID_LIST_POSITION);
      else _selection.SetSelected((uint)index);
    }

    /// <summary>
    /// The orange-row flags the window last handed over. Gap navigation reads
    /// them from here so that they are not computed a second time.
    /// </summary>
    internal bool[] GapFlags => _gap;

    /// <summary>Bring row <paramref name="index"/> into view, optionally moving the keyboard focus to it.</summary>
    internal void ScrollTo(int index, bool focus)
    {
      if (index < 0 || index >= _lines.Count) return;
      // GirCore 0.7: the column and the scroll info are nullable in C and
      // marshal fine as null here (measured), which means "no column, keep
      // the list's own idea of where to put the row".
      View.ScrollTo((uint)index, null!, focus ? Gtk.ListScrollFlags.Focus : Gtk.ListScrollFlags.None, null!);
    }

    /// <summary>
    /// The row under a point in <see cref="View"/> coordinates, or -1 when
    /// the point is not on a row. The widget under the point is picked and
    /// its ancestors are compared with the cells that are bound right now;
    /// GirCore hands back the same managed wrapper for a widget we made, so
    /// reference equality identifies the cell.
    /// </summary>
    internal int IndexAt(double x, double y)
    {
      var widget = View.Pick(x, y, Gtk.PickFlags.Default);
      for (int depth = 0; widget != null && depth < 8; depth++, widget = widget.GetParent())
      {
        // A point in the cell's padding picks the cell widget, whose first
        // child is the box; a point on the text picks the label inside it.
        int index = IndexOfCell(widget);
        if (index >= 0) return index;
        var child = widget.GetFirstChild();
        if (child != null)
        {
          index = IndexOfCell(child);
          if (index >= 0) return index;
        }
      }
      return -1;
    }

    private int IndexOfCell(Gtk.Widget widget)
    {
      foreach (var column in _columns)
        foreach (var (index, cell) in column.Bound)
          if (ReferenceEquals(cell.Box, widget)) return index;
      return -1;
    }

    /// <summary>The model behind the view. Tests count its items-changed signals.</summary>
    internal Gio.ListStore Store => _store;

    /// <summary>
    /// Show a different set of lines. The one place the store is rebuilt, in
    /// a single splice: one items-changed for the whole file instead of one
    /// per line, each of which the selection and the view would handle.
    /// </summary>
    internal void SetLines(IReadOnlyList<RetimerLine> lines)
    {
      _lines = lines;
      _gap = new bool[lines.Count];
      _mismatch = new bool[lines.Count];

      var rows = new GObject.Object[lines.Count];
      for (int i = 0; i < rows.Length; i++)
        rows[i] = Gtk.StringObject.New(i.ToString(CultureInfo.InvariantCulture));
      foreach (var column in _columns) column.Bound.Clear();
      _store.Splice(0, _store.GetNItems(), rows, (uint)rows.Length);
      StoreRebuilds++;
    }

    /// <summary>Set the row colours and repaint the bound cells.</summary>
    internal void SetFlags(bool[] gap, bool[] mismatch)
    {
      _gap = gap;
      _mismatch = mismatch;
      Refresh();
    }

    /// <summary>Why row <paramref name="index"/> is coloured. A gap wins over a mismatch.</summary>
    internal RowState StateAt(int index)
    {
      if (index < 0 || index >= _lines.Count) return RowState.None;
      if (index < _gap.Length && _gap[index]) return RowState.Gap;
      if (index < _mismatch.Length && _mismatch[index]) return RowState.Mismatch;
      return RowState.None;
    }

    /// <summary>
    /// The start time this row's cell is showing right now, or null when the
    /// row has no bound cell (it is scrolled out of view). Lets a test tell a
    /// real in-place refresh from a recomputed model.
    /// </summary>
    internal string? BoundStartText(int index) =>
      _startColumn.Bound.TryGetValue(index, out var cell) ? cell.Label.GetText() : null;

    /// <summary>Rewrite every bound cell from the lines and the flags.</summary>
    internal void Refresh()
    {
      foreach (var column in _columns)
      {
        List<int>? stale = null;
        foreach (var (index, cell) in column.Bound)
        {
          // A cell that GTK moved to another row without telling us (or one
          // left over from a shorter list) must not be written to.
          if (index >= _lines.Count || cell.Item.GetPosition() != (uint)index)
          {
            (stale ??= new List<int>()).Add(index);
            continue;
          }
          Apply(column, index, cell);
        }
        if (stale != null)
          foreach (int index in stale) column.Bound.Remove(index);
      }
    }

    private Gtk.ColumnViewColumn MakeColumn(string title, CellColumn column, bool expand, int fixedWidth, bool ellipsize)
    {
      var factory = Gtk.SignalListItemFactory.New();

      factory.OnSetup += (_, args) =>
      {
        var item = (Gtk.ListItem)args.Object;
        // The label sits in a box so the row colour fills the whole cell.
        var box = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);
        box.SetHexpand(true);

        var label = Gtk.Label.New("");
        label.SetHalign(Gtk.Align.Start);
        label.SetHexpand(true);
        label.SetWrap(false);
        label.SetMarginStart(4);
        label.SetMarginEnd(4);
        if (ellipsize) label.SetEllipsize(Pango.EllipsizeMode.End);

        box.Append(label);
        item.SetChild(box);
      };

      factory.OnBind += (_, args) =>
      {
        var item = (Gtk.ListItem)args.Object;
        uint position = item.GetPosition();
        if (position >= (uint)_lines.Count) return;   // stale row, nothing to show
        if (item.GetChild() is not Gtk.Box box) return;
        if (box.GetFirstChild() is not Gtk.Label label) return;

        var cell = new BoundCell(item, box, label);
        column.Bound[(int)position] = cell;
        Apply(column, (int)position, cell);
      };

      factory.OnUnbind += (_, args) =>
      {
        var item = (Gtk.ListItem)args.Object;
        // Only this cell's own entry goes. GTK does not promise that the
        // position still names the row this cell was showing, nor that a
        // cell is unbound before another is bound to its row (measured: under
        // scrolling a cell is unbound at a position another cell now holds),
        // and removing that other cell's entry would leave a row on screen
        // that Refresh never rewrites.
        uint position = item.GetPosition();
        if (position != Gtk.Constants.INVALID_LIST_POSITION &&
            column.Bound.TryGetValue((int)position, out var cell) && SameObject(cell.Item, item))
          column.Bound.Remove((int)position);
      };

      var listColumn = Gtk.ColumnViewColumn.New(title, factory);
      if (expand)
      {
        listColumn.SetExpand(true);
      }
      else
      {
        listColumn.SetFixedWidth(fixedWidth);
        listColumn.SetResizable(true);
      }
      return listColumn;
    }

    /// <summary>
    /// Whether two wrappers stand for the same GObject. The list items are
    /// GTK's, not ours, so the handles are compared rather than trusting
    /// GirCore to hand back the same wrapper every time.
    /// </summary>
    private static bool SameObject(GObject.Object a, GObject.Object b) =>
      a.Handle.DangerousGetHandle() == b.Handle.DangerousGetHandle();

    private void Apply(CellColumn column, int index, BoundCell cell)
    {
      cell.Label.SetText(column.Text(_lines[index]));

      var state = StateAt(index);
      cell.Box.RemoveCssClass(RetimerStyles.Gap);
      cell.Box.RemoveCssClass(RetimerStyles.Mismatch);
      if (state == RowState.Gap) cell.Box.AddCssClass(RetimerStyles.Gap);
      else if (state == RowState.Mismatch) cell.Box.AddCssClass(RetimerStyles.Mismatch);
    }
  }
}
