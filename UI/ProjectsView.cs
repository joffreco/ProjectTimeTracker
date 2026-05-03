using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.UI;

internal sealed class ProjectsView : UserControl
{
    private readonly ListView _listView = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        CheckBoxes = true,
        HideSelection = false,
        GridLines = false
    };

    private readonly Button _addButton = new() { Text = "Add", Width = 80 };
    private readonly Button _editButton = new() { Text = "Edit", Width = 80 };
    private readonly Button _deleteButton = new() { Text = "Delete", Width = 80 };

    private bool _suppressItemCheck;

    public event EventHandler? AddRequested;
    public event EventHandler<string>? EditRequested;
    public event EventHandler<string>? DeleteRequested;
    /// <summary>Raised when the user toggles the invoiceable checkbox of a project.</summary>
    public event EventHandler<(string ProjectName, bool IsInvoiceable)>? InvoiceableToggled;

    public ProjectsView()
    {
        Padding = new Padding(8);

        _listView.Columns.Add("Project", 240);
        _listView.Columns.Add("Invoiceable", 100);

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

        Controls.Add(_listView);
        Controls.Add(buttonsPanel);

        _addButton.Click += (_, _) => AddRequested?.Invoke(this, EventArgs.Empty);
        _editButton.Click += (_, _) =>
        {
            if (SelectedName() is { } name)
            {
                EditRequested?.Invoke(this, name);
            }
        };
        _deleteButton.Click += (_, _) =>
        {
            if (SelectedName() is { } name)
            {
                DeleteRequested?.Invoke(this, name);
            }
        };

        _listView.SelectedIndexChanged += (_, _) => UpdateButtonStates();
        _listView.ItemChecked += (_, e) =>
        {
            if (_suppressItemCheck)
            {
                return;
            }
            if (e.Item.Tag is string name)
            {
                e.Item.SubItems[1].Text = e.Item.Checked ? "Yes" : "No";
                InvoiceableToggled?.Invoke(this, (name, e.Item.Checked));
            }
        };
        _listView.MouseDoubleClick += (_, _) =>
        {
            if (SelectedName() is { } name)
            {
                EditRequested?.Invoke(this, name);
            }
        };

        UpdateButtonStates();
    }

    public void SetProjects(IEnumerable<ProjectDefinition> projects)
    {
        string? previous = SelectedName();
        _suppressItemCheck = true;
        _listView.BeginUpdate();
        try
        {
            _listView.Items.Clear();
            foreach (ProjectDefinition p in projects)
            {
                ListViewItem item = new(p.Name) { Tag = p.Name, Checked = p.IsInvoiceable };
                item.SubItems.Add(p.IsInvoiceable ? "Yes" : "No");
                _listView.Items.Add(item);
            }

            if (previous is not null)
            {
                foreach (ListViewItem item in _listView.Items)
                {
                    if (string.Equals(item.Tag as string, previous, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Selected = true;
                        item.Focused = true;
                        break;
                    }
                }
            }
        }
        finally
        {
            _listView.EndUpdate();
            _suppressItemCheck = false;
        }

        UpdateButtonStates();
    }

    private string? SelectedName()
    {
        if (_listView.SelectedItems.Count == 0)
        {
            return null;
        }
        return _listView.SelectedItems[0].Tag as string;
    }

    private void UpdateButtonStates()
    {
        bool hasSelection = _listView.SelectedItems.Count > 0;
        _editButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
    }
}
