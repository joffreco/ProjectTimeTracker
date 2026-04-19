namespace ProjectTimeTracker.UI;

internal sealed class StatusEditDialog : Form
{
    private const string NoneItem = "(none)";

    private readonly DateTimePicker _datePicker;
    private readonly DateTimePicker _timePicker;
    private readonly ComboBox _projectCombo;

    public DateTime SelectedLocalTime
    {
        get
        {
            DateTime d = _datePicker.Value.Date;
            DateTime t = _timePicker.Value;
            return new DateTime(d.Year, d.Month, d.Day, t.Hour, t.Minute, t.Second, DateTimeKind.Unspecified);
        }
    }

    public string? SelectedProject =>
        _projectCombo.SelectedItem is string s && !string.Equals(s, NoneItem, StringComparison.Ordinal)
            ? s
            : null;

    public StatusEditDialog(IEnumerable<string> projects, DateTime initialLocalTime, string? initialProject)
    {
        Text = "Edit status";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(360, 180);

        Label dateLabel = new() { AutoSize = true, Location = new Point(12, 14), Text = "Date:" };
        _datePicker = new DateTimePicker
        {
            Location = new Point(110, 10),
            Size = new Size(230, 23),
            Format = DateTimePickerFormat.Short,
            Value = initialLocalTime.Date
        };

        Label timeLabel = new() { AutoSize = true, Location = new Point(12, 46), Text = "Time:" };
        _timePicker = new DateTimePicker
        {
            Location = new Point(110, 42),
            Size = new Size(230, 23),
            Format = DateTimePickerFormat.Time,
            ShowUpDown = true,
            Value = initialLocalTime
        };

        Label projectLabel = new() { AutoSize = true, Location = new Point(12, 78), Text = "Project:" };
        _projectCombo = new ComboBox
        {
            Location = new Point(110, 74),
            Size = new Size(230, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _projectCombo.Items.Add(NoneItem);
        foreach (string p in projects)
        {
            _projectCombo.Items.Add(p);
        }

        if (string.IsNullOrWhiteSpace(initialProject))
        {
            _projectCombo.SelectedItem = NoneItem;
        }
        else
        {
            int idx = _projectCombo.Items.IndexOf(initialProject);
            if (idx < 0)
            {
                _projectCombo.Items.Add(initialProject);
                idx = _projectCombo.Items.Count - 1;
            }
            _projectCombo.SelectedIndex = idx;
        }

        Button okButton = new()
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(184, 130),
            Size = new Size(75, 28)
        };
        Button cancelButton = new()
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(265, 130),
            Size = new Size(75, 28)
        };
        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(dateLabel);
        Controls.Add(_datePicker);
        Controls.Add(timeLabel);
        Controls.Add(_timePicker);
        Controls.Add(projectLabel);
        Controls.Add(_projectCombo);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }
}

