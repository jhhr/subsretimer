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

    /// <summary>Show a different set of lines. The one place the store is rebuilt.</summary>
    internal void SetLines(IReadOnlyList<RetimerLine> lines)
    {
      _lines = lines;
      _gap = new bool[lines.Count];
      _mismatch = new bool[lines.Count];

      _store.RemoveAll();
      foreach (var column in _columns) column.Bound.Clear();
      for (int i = 0; i < lines.Count; i++)
        _store.Append(Gtk.StringObject.New(i.ToString(CultureInfo.InvariantCulture)));
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
        // Unbind runs before GTK gives the cell its new row, so the position
        // is still the row this cell was showing.
        uint position = item.GetPosition();
        if (position != Gtk.Constants.INVALID_LIST_POSITION) column.Bound.Remove((int)position);
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
