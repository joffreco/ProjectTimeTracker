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

    private readonly Button _addButton = new() { Text = "Add", Width = 80 };
    private readonly Button _editButton = new() { Text = "Edit", Width = 80, Enabled = false };
    private readonly Button _deleteButton = new() { Text = "Delete", Width = 80, Enabled = false };
    private readonly Label _hint = new()
    {
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Text = string.Empty,
        Visible = false
    };

    private readonly List<StatusEntry> _entries = [];

    public event EventHandler? AddRequested;
    public event EventHandler<Guid>? EditRequested;
    public event EventHandler<Guid>? DeleteRequested;

    public StatusesView()
    {
        Padding = new Padding(8);

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

        FlowLayoutPanel buttonsPanel = new()
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8)
        };
        buttonsPanel.Controls.Add(_addButton);
        buttonsPanel.Controls.Add(_editButton);
        buttonsPanel.Controls.Add(_deleteButton);
        buttonsPanel.Controls.Add(_hint);

        Controls.Add(_grid);
        Controls.Add(buttonsPanel);

        _addButton.Click += (_, _) => AddRequested?.Invoke(this, EventArgs.Empty);

        _editButton.Click += (_, _) =>
        {
            Guid? id = SelectedEventId();
            if (id is { } eventId)
            {
                EditRequested?.Invoke(this, eventId);
            }
        };
        _deleteButton.Click += (_, _) =>
        {
            Guid? id = SelectedEventId();
            if (id is { } eventId)
            {
                DeleteRequested?.Invoke(this, eventId);
            }
        };

        _grid.SelectionChanged += (_, _) => UpdateButtonStates();
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool hasSelection = SelectedEventId() is not null;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }

    public void SetEntries(IEnumerable<StatusEntry> entries)
    {
        _entries.Clear();
        _entries.AddRange(entries);
        // Newest first
        _entries.Sort((a, b) => b.LocalTime.CompareTo(a.LocalTime));
        _grid.DataSource = null;
        _grid.DataSource = _entries;
    }

    private Guid? SelectedEventId()
    {
        if (_grid.CurrentRow?.DataBoundItem is StatusEntry entry)
        {
            return entry.EventId;
        }

        return null;
    }
}

