using ProjectTimeTracker.Domain;

namespace ProjectTimeTracker.UI;

/// <summary>
/// Lists per-invoiceable-project monthly totals starting April 2026 (Montreal time),
/// with confirm/un-confirm buttons for completed months. The current (in-progress) month
/// is shown read-only.
/// </summary>
internal sealed class InvoicingView : UserControl
{
    /// <summary>First month included in invoicing (Montreal time, inclusive).</summary>
    private static readonly DateTime FirstMonth = new(2026, 4, 1);

    private readonly TableLayoutPanel _grid = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        ColumnCount = 4,
        AutoSize = false,
        Padding = new Padding(8)
    };

    private readonly Label _emptyLabel = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Padding = new Padding(8),
        Text = "No invoiceable projects. Mark a project as invoiceable in the Projects view."
    };

    private IReadOnlyList<ProjectDefinition> _projects = Array.Empty<ProjectDefinition>();
    private IReadOnlyList<EventPoint> _events = Array.Empty<EventPoint>();
    private IReadOnlyDictionary<string, DateTime> _confirmations = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
    private DateTime _nowLocal = DateTime.Now;

    /// <summary>Raised when the user confirms an invoice for (project, year, month).</summary>
    public event EventHandler<(string Project, int Year, int Month)>? ConfirmRequested;
    /// <summary>Raised when the user un-confirms an invoice for (project, year, month).</summary>
    public event EventHandler<(string Project, int Year, int Month)>? UnconfirmRequested;

    public InvoicingView()
    {
        Padding = new Padding(0);
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        Controls.Add(_grid);
        Controls.Add(_emptyLabel);
        _emptyLabel.Visible = false;
    }

    public void SetData(
        IReadOnlyList<ProjectDefinition> projects,
        IReadOnlyList<EventPoint> events,
        IReadOnlyDictionary<string, DateTime> confirmations,
        DateTime nowLocal)
    {
        _projects = projects;
        _events = events;
        _confirmations = confirmations;
        _nowLocal = nowLocal;
        Rebuild();
    }

    private void Rebuild()
    {
        _grid.SuspendLayout();
        try
        {
            _grid.Controls.Clear();
            _grid.RowStyles.Clear();
            _grid.RowCount = 0;

            List<ProjectDefinition> invoiceable = _projects
                .Where(p => p.IsInvoiceable)
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _emptyLabel.Visible = invoiceable.Count == 0;
            if (invoiceable.Count == 0)
            {
                return;
            }

            // Build segments once from all events, in local (Montreal) time.
            List<Segment> allSegments = BuildSegments(_events, _nowLocal);

            DateTime currentMonth = new(_nowLocal.Year, _nowLocal.Month, 1);

            foreach (ProjectDefinition project in invoiceable)
            {
                AddHeaderRow(project.Name);

                // Newest month first.
                List<DateTime> months = new();
                for (DateTime m = currentMonth; m >= FirstMonth; m = m.AddMonths(-1))
                {
                    months.Add(m);
                }

                foreach (DateTime month in months)
                {
                    DateTime monthStart = month;
                    DateTime monthEnd = month.AddMonths(1);
                    bool isInProgress = month == currentMonth;

                    double minutes = SumProjectMinutes(allSegments, project.Name, monthStart, monthEnd);
                    AddMonthRow(project.Name, month, minutes, isInProgress);
                }

                AddSpacerRow();
            }
        }
        finally
        {
            _grid.ResumeLayout();
        }
    }

    private void AddHeaderRow(string projectName)
    {
        Label header = new()
        {
            Text = projectName,
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold),
            Margin = new Padding(2, 12, 2, 4)
        };
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        int row = _grid.RowCount++;
        _grid.Controls.Add(header, 0, row);
        _grid.SetColumnSpan(header, 4);
    }

    private void AddMonthRow(string projectName, DateTime month, double minutes, bool isInProgress)
    {
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        int row = _grid.RowCount++;

        Label monthLabel = new()
        {
            Text = month.ToString("yyyy MMMM") + (isInProgress ? "  (in progress)" : string.Empty),
            AutoSize = true,
            Margin = new Padding(8, 4, 4, 4),
            ForeColor = isInProgress ? SystemColors.GrayText : SystemColors.ControlText
        };
        _grid.Controls.Add(monthLabel, 0, row);

        Label totalLabel = new()
        {
            Text = FormatDuration(minutes),
            AutoSize = true,
            Margin = new Padding(4),
            Font = new Font(Font, FontStyle.Bold)
        };
        _grid.Controls.Add(totalLabel, 1, row);

        string key = Infrastructure.InvoiceConfirmationsRepository.MakeKey(projectName, month.Year, month.Month);
        bool isConfirmed = _confirmations.ContainsKey(key);

        if (isInProgress)
        {
            Label tag = new()
            {
                Text = "running",
                AutoSize = true,
                Margin = new Padding(4),
                ForeColor = SystemColors.GrayText
            };
            _grid.Controls.Add(tag, 2, row);
            _grid.Controls.Add(new Label { AutoSize = true, Margin = new Padding(4) }, 3, row);
            return;
        }

        Button toggleButton = new()
        {
            AutoSize = true,
            Margin = new Padding(4),
            Text = isConfirmed ? "Un-confirm" : "Confirm invoiced",
            BackColor = isConfirmed ? Color.LightGreen : SystemColors.Control
        };
        toggleButton.Click += (_, _) =>
        {
            if (isConfirmed)
            {
                UnconfirmRequested?.Invoke(this, (projectName, month.Year, month.Month));
            }
            else
            {
                ConfirmRequested?.Invoke(this, (projectName, month.Year, month.Month));
            }
        };
        _grid.Controls.Add(toggleButton, 2, row);

        Label statusLabel = new()
        {
            AutoSize = true,
            Margin = new Padding(4),
            ForeColor = isConfirmed ? Color.DarkGreen : SystemColors.GrayText,
            Text = isConfirmed && _confirmations.TryGetValue(key, out DateTime confirmedAtUtc)
                ? $"✓ Invoiced on {confirmedAtUtc.ToLocalTime():yyyy-MM-dd}"
                : string.Empty
        };
        _grid.Controls.Add(statusLabel, 3, row);
    }

    private void AddSpacerRow()
    {
        _grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
        int row = _grid.RowCount++;
        Panel p = new() { Height = 8, Dock = DockStyle.Fill };
        _grid.Controls.Add(p, 0, row);
        _grid.SetColumnSpan(p, 4);
    }

    /// <summary>Lightweight projection of a tracker event in local (Montreal) time.</summary>
    public sealed record EventPoint(Guid EventId, DateTime LocalTime, string? Project);

    private sealed record Segment(DateTime StartLocal, DateTime EndLocal, string? Project);

    private static List<Segment> BuildSegments(IReadOnlyList<EventPoint> events, DateTime nowLocal)
    {
        List<EventPoint> sorted = events.OrderBy(e => e.LocalTime).ToList();
        List<Segment> segments = new(sorted.Count);
        for (int i = 0; i < sorted.Count; i++)
        {
            DateTime start = sorted[i].LocalTime;
            DateTime end = i + 1 < sorted.Count ? sorted[i + 1].LocalTime : nowLocal;
            if (end <= start)
            {
                continue;
            }
            segments.Add(new Segment(start, end, sorted[i].Project));
        }
        return segments;
    }

    private static double SumProjectMinutes(List<Segment> segments, string projectName, DateTime monthStart, DateTime monthEnd)
    {
        double total = 0;
        foreach (Segment seg in segments)
        {
            if (seg.Project is null)
            {
                continue;
            }
            if (!string.Equals(seg.Project, projectName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (seg.EndLocal <= monthStart || seg.StartLocal >= monthEnd)
            {
                continue;
            }

            DateTime s = seg.StartLocal < monthStart ? monthStart : seg.StartLocal;
            DateTime e = seg.EndLocal > monthEnd ? monthEnd : seg.EndLocal;
            double minutes = (e - s).TotalMinutes;
            if (minutes > 0)
            {
                total += minutes;
            }
        }
        return total;
    }

    private static string FormatDuration(double minutes)
    {
        int total = (int)Math.Round(minutes);
        int h = total / 60;
        int m = total % 60;
        return $"{h}h {m:D2}m";
    }
}

