namespace ProjectTimeTracker.UI;

internal sealed class ProjectEditDialog : Form
{
    private readonly TextBox _textBox;
    private readonly CheckBox _invoiceableBox;

    public string Value => _textBox.Text.Trim();
    public bool IsInvoiceable => _invoiceableBox.Checked;

    public ProjectEditDialog(string title, string prompt, string initialValue = "", bool initialInvoiceable = false)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 160);

        Label promptLabel = new()
        {
            AutoSize = true,
            Location = new Point(12, 12),
            Text = prompt
        };

        _textBox = new TextBox
        {
            Location = new Point(12, 38),
            Size = new Size(356, 23),
            Text = initialValue
        };
        _textBox.SelectAll();

        _invoiceableBox = new CheckBox
        {
            Location = new Point(12, 72),
            AutoSize = true,
            Text = "Invoiceable (include in monthly invoicing)",
            Checked = initialInvoiceable
        };

        Button okButton = new()
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(212, 118),
            Size = new Size(75, 28)
        };

        Button cancelButton = new()
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(293, 118),
            Size = new Size(75, 28)
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(promptLabel);
        Controls.Add(_textBox);
        Controls.Add(_invoiceableBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    public static (string Name, bool IsInvoiceable)? Prompt(IWin32Window? owner, string title, string prompt,
        string initialValue = "", bool initialInvoiceable = false)
    {
        using ProjectEditDialog dialog = new(title, prompt, initialValue, initialInvoiceable);
        DialogResult result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (result != DialogResult.OK)
        {
            return null;
        }

        string value = dialog.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return (value, dialog.IsInvoiceable);
    }
}

