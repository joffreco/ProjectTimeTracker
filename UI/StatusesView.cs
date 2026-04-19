using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.UI;

internal sealed class StatusEntry
{
    public required Guid EventId { get; init; }
    public required DateTime LocalTime { get; init; }
    public required string Source { get; init; }
    public required StateEventType EventType { get; init; }
    public required string Project { get; init; }
}

internal sealed class StatusesView : UserControl
{
    private readonly WeeklyTimelineView _timeline = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        ReadOnly = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly Button _addButton = new() { Text = "Add", Width = 90 };
    private readonly Button _editButton = new() { Text = "Edit", Width = 90, Enabled = false };
    private readonly Button _deleteButton = new() { Text = "Delete", Width = 90, Enabled = false };
    private readonly Label _hint = new()
    {
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(12, 8, 0, 0),
        Text = "Timeline: left click = edit, right click = delete."
    };

    private readonly List<StatusEntry> _entries = new();

    public event EventHandler? AddRequested;
    public event EventHandler<Guid>? EditRequested;
    public event EventHandler<Guid>? DeleteRequested;

    public StatusesView()
    {
        Padding = new Padding(8);

        FlowLayoutPanel topPanel = new()
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 6)
        };
        topPanel.Controls.Add(_addButton);
        topPanel.Controls.Add(_editButton);
        topPanel.Controls.Add(_deleteButton);
        topPanel.Controls.Add(_hint);

        // Grid columns
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "When",
            DataPropertyName = nameof(StatusEntry.LocalTime),
            DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm:ss" },
            FillWeight = 25
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Source",
            DataPropertyName = nameof(StatusEntry.Source),
            FillWeight = 10
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Type",
            DataPropertyName = nameof(StatusEntry.EventType),
            FillWeight = 20
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Project",
            DataPropertyName = nameof(StatusEntry.Project),
            FillWeight = 45
        });

        SplitContainer split = new()
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            Panel1MinSize = 80,
            Panel2MinSize = 80
        };
        split.Panel1.Controls.Add(_timeline);
        split.Panel2.Controls.Add(_grid);
        // Set initial splitter position once handle is created (avoids exceptions in some scenarios).
        split.HandleCreated += (_, _) =>
        {
            try
            {
                split.SplitterDistance = Math.Max(split.Panel1MinSize, split.Height / 2);
            }
            catch
            {
                // Ignore: layout might not be ready.
            }
        };

        Controls.Add(split);
        Controls.Add(topPanel);

        _addButton.Click += (_, _) => AddRequested?.Invoke(this, EventArgs.Empty);
        _editButton.Click += (_, _) =>
        {
            if (SelectedEventId() is { } id)
            {
                EditRequested?.Invoke(this, id);
            }
        };
        _deleteButton.Click += (_, _) =>
        {
            if (SelectedEventId() is { } id)
            {
                DeleteRequested?.Invoke(this, id);
            }
        };
        _grid.SelectionChanged += (_, _) =>
        {
            bool has = SelectedEventId() is not null;
            _editButton.Enabled = has;
            _deleteButton.Enabled = has;
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && SelectedEventId() is { } id)
            {
                EditRequested?.Invoke(this, id);
            }
        };

        _timeline.SegmentClicked += (_, id) => EditRequested?.Invoke(this, id);
        _timeline.SegmentRightClicked += (_, id) => DeleteRequested?.Invoke(this, id);
    }

    public void SetEntries(IEnumerable<StatusEntry> entries, DateTime nowLocal)
    {
        _entries.Clear();
        _entries.AddRange(entries);
        _entries.Sort((a, b) => b.LocalTime.CompareTo(a.LocalTime));

        _timeline.SetEntries(_entries, nowLocal);

        _grid.DataSource = null;
        _grid.DataSource = _entries;
    }

    private Guid? SelectedEventId() =>
        _grid.CurrentRow?.DataBoundItem is StatusEntry entry ? entry.EventId : null;
}
