namespace ProjectTimeTracker.UI;

internal sealed class InputDialog : Form
{
    private readonly TextBox _textBox;

    public string Value => _textBox.Text.Trim();

    public InputDialog(string title, string prompt, string initialValue = "")
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 120);

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

        Button okButton = new()
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(212, 78),
            Size = new Size(75, 28)
        };

        Button cancelButton = new()
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(293, 78),
            Size = new Size(75, 28)
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(promptLabel);
        Controls.Add(_textBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }

    public static string? Prompt(IWin32Window? owner, string title, string prompt, string initialValue = "")
    {
        using InputDialog dialog = new(title, prompt, initialValue);
        DialogResult result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (result != DialogResult.OK)
        {
            return null;
        }

        string value = dialog.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

