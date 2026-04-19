namespace ProjectTimeTracker.UI;

internal sealed class ProjectsView : UserControl
{
    private readonly ListBox _listBox = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false
    };

    private readonly Button _addButton = new() { Text = "Add", Width = 80 };
    private readonly Button _editButton = new() { Text = "Edit", Width = 80 };
    private readonly Button _deleteButton = new() { Text = "Delete", Width = 80 };

    public event EventHandler? AddRequested;
    public event EventHandler<string>? EditRequested;
    public event EventHandler<string>? DeleteRequested;

    public ProjectsView()
    {
        Padding = new Padding(8);

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

        Controls.Add(_listBox);
        Controls.Add(buttonsPanel);

        _addButton.Click += (_, _) => AddRequested?.Invoke(this, EventArgs.Empty);
        _editButton.Click += (_, _) =>
        {
            if (_listBox.SelectedItem is string name)
            {
                EditRequested?.Invoke(this, name);
            }
        };
        _deleteButton.Click += (_, _) =>
        {
            if (_listBox.SelectedItem is string name)
            {
                DeleteRequested?.Invoke(this, name);
            }
        };

        _listBox.SelectedIndexChanged += (_, _) => UpdateButtonStates();
        UpdateButtonStates();
    }

    public void SetProjects(IEnumerable<string> projects)
    {
        string? previous = _listBox.SelectedItem as string;
        _listBox.BeginUpdate();
        try
        {
            _listBox.Items.Clear();
            foreach (string p in projects)
            {
                _listBox.Items.Add(p);
            }

            if (previous is not null)
            {
                int idx = _listBox.Items.IndexOf(previous);
                if (idx >= 0)
                {
                    _listBox.SelectedIndex = idx;
                }
            }
        }
        finally
        {
            _listBox.EndUpdate();
        }

        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool hasSelection = _listBox.SelectedItem is string;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }
}

