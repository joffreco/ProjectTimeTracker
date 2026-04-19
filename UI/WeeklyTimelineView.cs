using System.Drawing.Drawing2D;

namespace ProjectTimeTracker.UI;

internal sealed class WeeklyTimelineView : UserControl
{
    private const int RowHeight = 92;
    private const int RowGap = 6;
    private const int LabelHeight = 16;
    private const int TicksHeight = 14;
    private const int BarHeight = 32;
    private const int TotalsHeight = 22;
    private const int LeftPad = 110;
    private const int RightPad = 12;

    private readonly List<WeekRow> _rows = new();
    private readonly List<HitItem> _hitItems = new();
    private readonly ToolTip _tooltip = new();
    private string? _lastTip;

    private float _zoom = 1f;
    private const float MinZoom = 1f;
    private const float MaxZoom = 50f;
    private const float ZoomStep = 1.25f;

    public event EventHandler<Guid>? SegmentClicked;
    public event EventHandler<Guid>? SegmentRightClicked;

    public WeeklyTimelineView()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        TabStop = true;
        MouseClick += OnMouseClick;
        MouseMove += OnMouseMove;
    }

    private sealed record Segment(Guid EventId, DateTime StartLocal, DateTime EndLocal, string Project);

    private sealed class WeekRow
    {
        public required DateTime MondayLocal { get; init; }
        public List<Segment> Segments { get; } = new();
    }

    private sealed record HitItem(Rectangle Rect, Segment Segment);

    public void SetEntries(IEnumerable<StatusEntry> entries, DateTime nowLocal)
    {
        List<StatusEntry> sorted = entries.OrderBy(e => e.LocalTime).ToList();

        // Build chronological "active project" segments from event timeline.
        List<Segment> segments = new();
        for (int i = 0; i < sorted.Count; i++)
        {
            DateTime start = sorted[i].LocalTime;
            DateTime end = i + 1 < sorted.Count ? sorted[i + 1].LocalTime : nowLocal;
            if (end <= start)
            {
                continue;
            }
            segments.Add(new Segment(sorted[i].EventId, start, end, sorted[i].Project));
        }

        _rows.Clear();
        if (segments.Count == 0)
        {
            AutoScrollMinSize = Size.Empty;
            Invalidate();
            return;
        }

        DateTime firstMonday = StartOfWeek(segments[0].StartLocal);
        DateTime lastEnd = segments[^1].EndLocal;
        DateTime endMonday = StartOfWeek(lastEnd).AddDays(7);

        for (DateTime monday = firstMonday; monday < endMonday; monday = monday.AddDays(7))
        {
            DateTime weekStart = monday;
            DateTime weekEnd = monday.AddDays(7);
            WeekRow row = new() { MondayLocal = monday };

            foreach (Segment seg in segments)
            {
                if (seg.EndLocal <= weekStart)
                {
                    continue;
                }
                if (seg.StartLocal >= weekEnd)
                {
                    break;
                }

                DateTime s = seg.StartLocal < weekStart ? weekStart : seg.StartLocal;
                DateTime ed = seg.EndLocal > weekEnd ? weekEnd : seg.EndLocal;
                row.Segments.Add(new Segment(seg.EventId, s, ed, seg.Project));
            }

            _rows.Add(row);
        }

        _rows.Reverse(); // newest week on top

        UpdateAutoScrollSize();
        Invalidate();
    }

    private int ContentHeight => _rows.Count * (RowHeight + RowGap) + 16;

    private int ViewportBarWidth => Math.Max(140, ClientSize.Width - LeftPad - RightPad);

    private int ZoomedBarWidth => (int)Math.Round(ViewportBarWidth * _zoom);

    private void UpdateAutoScrollSize()
    {
        int width = LeftPad + ZoomedBarWidth + RightPad;
        int height = ContentHeight;
        if (AutoScrollMinSize.Width != width || AutoScrollMinSize.Height != height)
        {
            AutoScrollMinSize = new Size(width, height);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateAutoScrollSize();
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        // Required so the wheel events reach this control without an explicit click.
        if (!Focused && CanFocus)
        {
            Focus();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if ((ModifierKeys & Keys.Shift) == Keys.Shift)
        {
            // Horizontal scroll: one notch ≈ 1/8 of the visible bar width.
            int viewport = Math.Max(80, ClientSize.Width - LeftPad - RightPad);
            int step = Math.Max(20, viewport / 8);
            int direction = e.Delta > 0 ? -1 : 1;
            int targetX = Math.Max(0, -AutoScrollPosition.X + direction * step);
            AutoScrollPosition = new Point(targetX, -AutoScrollPosition.Y);
            Invalidate();
            return;
        }

        ZoomAt(e.X, e.Delta > 0);
    }

    private void ZoomAt(int cursorX, bool zoomIn)
    {
        // Only zoom over the bar area (right of LeftPad).
        int barLeft = LeftPad;
        int relX = Math.Max(0, cursorX - barLeft);

        int oldBarWidth = Math.Max(1, ZoomedBarWidth);
        // Content X under cursor before zoom (taking horizontal scroll into account).
        int contentX = relX - AutoScrollPosition.X;
        double ratio = (double)contentX / oldBarWidth;

        float newZoom = zoomIn ? _zoom * ZoomStep : _zoom / ZoomStep;
        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - _zoom) < 0.0001f)
        {
            return;
        }

        _zoom = newZoom;
        UpdateAutoScrollSize();

        int newBarWidth = Math.Max(1, ZoomedBarWidth);
        int newContentX = (int)Math.Round(ratio * newBarWidth);
        int desiredScrollX = newContentX - relX;
        // AutoScrollPosition uses positive coords on assignment but reports negative on read.
        AutoScrollPosition = new Point(Math.Max(0, desiredScrollX), -AutoScrollPosition.Y);
        Invalidate();
    }

    private static DateTime StartOfWeek(DateTime dt)
    {
        int diff = ((int)dt.DayOfWeek + 6) % 7; // Monday = 0
        return dt.Date.AddDays(-diff);
    }

    private static Color ColorFor(string project)
    {
        if (string.IsNullOrWhiteSpace(project) || string.Equals(project, "none", StringComparison.OrdinalIgnoreCase))
        {
            return Color.FromArgb(225, 225, 225);
        }

        int hash = 0;
        foreach (char c in project)
        {
            hash = unchecked(hash * 31 + char.ToLowerInvariant(c));
        }
        int hue = Math.Abs(hash) % 360;
        return FromHsl(hue, 0.55, 0.62);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double hp = h / 60.0;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        double r = 0, g = 0, b = 0;
        if (hp < 1) { r = c; g = x; }
        else if (hp < 2) { r = x; g = c; }
        else if (hp < 3) { g = c; b = x; }
        else if (hp < 4) { g = x; b = c; }
        else if (hp < 5) { r = x; b = c; }
        else { r = c; b = x; }

        double m = l - c / 2;
        return Color.FromArgb(
            Math.Clamp((int)Math.Round((r + m) * 255), 0, 255),
            Math.Clamp((int)Math.Round((g + m) * 255), 0, 255),
            Math.Clamp((int)Math.Round((b + m) * 255), 0, 255));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        _hitItems.Clear();

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

        int barWidth = ZoomedBarWidth;
        int dayStripeWidth = barWidth / 7;
        int totalBarWidth = dayStripeWidth * 7;

        using Font labelFont = new("Segoe UI", 8.25F);
        using Font weekFont = new("Segoe UI", 9F, FontStyle.Bold);
        using Font dayFont = new("Segoe UI", 8F, FontStyle.Regular);
        string[] dayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        int y = 6;
        foreach (WeekRow row in _rows)
        {
            // Week label (left column)
            g.DrawString($"Week of\n{row.MondayLocal:yyyy-MM-dd}", weekFont, Brushes.Black, 6, y);

            // Day headers
            for (int d = 0; d < 7; d++)
            {
                int dx = LeftPad + d * dayStripeWidth;
                g.DrawString($"{dayNames[d]} {row.MondayLocal.AddDays(d):dd}", dayFont, Brushes.DimGray, dx + 4, y);
            }

            int barTop = y + LabelHeight + TicksHeight;
            int barBottom = barTop + BarHeight;

            // Day cell backgrounds (alternating)
            for (int d = 0; d < 7; d++)
            {
                int dx = LeftPad + d * dayStripeWidth;
                Color bg = d % 2 == 0 ? Color.FromArgb(248, 248, 248) : Color.FromArgb(240, 240, 240);
                using SolidBrush br = new(bg);
                g.FillRectangle(br, dx, barTop, dayStripeWidth, BarHeight);
            }

            // Day separators
            using (Pen sep = new(Color.LightGray))
            {
                for (int d = 0; d <= 7; d++)
                {
                    int dx = LeftPad + d * dayStripeWidth;
                    g.DrawLine(sep, dx, barTop, dx, barBottom);
                }
                g.DrawRectangle(sep, LeftPad, barTop, totalBarWidth, BarHeight);
            }

            // Time-of-day ticks above the bar (adaptive to zoom).
            DrawTimeTicks(g, y + LabelHeight, barTop, totalBarWidth, dayFont);

            // Segments
            DateTime weekStart = row.MondayLocal;
            const double weekTotalMinutes = 7 * 24 * 60.0;
            using StringFormat sf = new()
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            foreach (Segment seg in row.Segments)
            {
                double startMin = (seg.StartLocal - weekStart).TotalMinutes;
                double endMin = (seg.EndLocal - weekStart).TotalMinutes;
                int x1 = LeftPad + (int)Math.Round(startMin / weekTotalMinutes * totalBarWidth);
                int x2 = LeftPad + (int)Math.Round(endMin / weekTotalMinutes * totalBarWidth);
                if (x2 <= x1)
                {
                    x2 = x1 + 1;
                }

                Rectangle rect = new(x1, barTop + 2, x2 - x1, BarHeight - 4);
                Color color = ColorFor(seg.Project);
                using (SolidBrush brush = new(color))
                {
                    g.FillRectangle(brush, rect);
                }
                using (Pen border = new(Color.FromArgb(80, 0, 0, 0)))
                {
                    g.DrawRectangle(border, rect);
                }

                _hitItems.Add(new HitItem(rect, seg));

                if (rect.Width > 28)
                {
                    Color textColor = IsDark(color) ? Color.White : Color.Black;
                    using SolidBrush textBrush = new(textColor);
                    Rectangle textRect = new(rect.X + 3, rect.Y, rect.Width - 6, rect.Height);
                    g.DrawString(seg.Project, labelFont, textBrush, textRect, sf);
                }
            }

            // Per-project totals for this week.
            DrawWeekTotals(g, row, barTop + BarHeight + 2, totalBarWidth, labelFont);

            y += RowHeight + RowGap;
        }
    }

    private static void DrawWeekTotals(Graphics g, WeekRow row, int top, int totalBarWidth, Font font)
    {
        // Aggregate minutes per project (case-insensitive, but keep original casing for display).
        Dictionary<string, double> minutes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> displayName = new(StringComparer.OrdinalIgnoreCase);
        foreach (Segment seg in row.Segments)
        {
            // Skip "none": idle time is not counted in the per-project totals.
            if (string.IsNullOrWhiteSpace(seg.Project) ||
                string.Equals(seg.Project, "none", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            double m = (seg.EndLocal - seg.StartLocal).TotalMinutes;
            if (m <= 0)
            {
                continue;
            }
            if (!minutes.ContainsKey(seg.Project))
            {
                minutes[seg.Project] = 0;
                displayName[seg.Project] = seg.Project;
            }
            minutes[seg.Project] += m;
        }

        if (minutes.Count == 0)
        {
            return;
        }

        // Sort by descending duration.
        List<KeyValuePair<string, double>> sorted = minutes
            .OrderByDescending(kv => kv.Value)
            .ToList();

        int x = LeftPad;
        int swatchSize = 10;
        int padBetween = 14;
        int textPad = 4;
        int rightLimit = LeftPad + totalBarWidth;
        int yMid = top + (TotalsHeight - swatchSize) / 2;

        foreach (KeyValuePair<string, double> kv in sorted)
        {
            string label = $"{displayName[kv.Key]}  {FormatDuration(kv.Value)}";
            SizeF size = g.MeasureString(label, font);
            int needed = swatchSize + textPad + (int)Math.Ceiling(size.Width) + padBetween;
            if (x + needed > rightLimit && x > LeftPad)
            {
                // Out of space: truncate with ellipsis indicator.
                using SolidBrush dim = new(Color.DimGray);
                g.DrawString("…", font, dim, x, top + 2);
                break;
            }

            Color color = ColorFor(kv.Key);
            using (SolidBrush sw = new(color))
            {
                g.FillRectangle(sw, x, yMid, swatchSize, swatchSize);
            }
            using (Pen border = new(Color.FromArgb(80, 0, 0, 0)))
            {
                g.DrawRectangle(border, x, yMid, swatchSize, swatchSize);
            }

            using SolidBrush textBrush = new(Color.Black);
            g.DrawString(label, font, textBrush, x + swatchSize + textPad, top + 2);

            x += needed;
        }
    }

    private static string FormatDuration(double minutes)
    {
        int total = (int)Math.Round(minutes);
        int h = total / 60;
        int m = total % 60;
        return h > 0 ? $"{h}h {m:D2}m" : $"{m}m";
    }

    private static void DrawTimeTicks(Graphics g, int labelTop, int barTop, int totalBarWidth, Font font)
    {
        if (totalBarWidth <= 0)
        {
            return;
        }

        double pxPerHour = totalBarWidth / 168.0;
        int tickMinutes = ChooseTickIntervalMinutes(pxPerHour);
        int totalMinutes = 7 * 24 * 60;
        bool showMinutes = tickMinutes < 60;

        using Pen tickPen = new(Color.Gray);
        using Pen minorPen = new(Color.FromArgb(180, 200, 200, 200));
        using SolidBrush textBrush = new(Color.DimGray);

        // Optional minor ticks at half the major interval (no labels).
        int minorMinutes = tickMinutes >= 2 && pxPerHour * (tickMinutes / 60.0) >= 30
            ? tickMinutes / 2
            : 0;
        if (minorMinutes > 0)
        {
            for (int m = 0; m <= totalMinutes; m += minorMinutes)
            {
                if (m % tickMinutes == 0)
                {
                    continue;
                }
                int x = LeftPad + (int)Math.Round((double)m / totalMinutes * totalBarWidth);
                g.DrawLine(minorPen, x, barTop, x, barTop - 3);
            }
        }

        for (int m = 0; m <= totalMinutes; m += tickMinutes)
        {
            int x = LeftPad + (int)Math.Round((double)m / totalMinutes * totalBarWidth);
            g.DrawLine(tickPen, x, barTop, x, barTop - 5);

            // Skip 00:00 of each day to avoid clashing with the day-name header.
            if (m % (24 * 60) == 0)
            {
                continue;
            }

            int hour = (m / 60) % 24;
            int minute = m % 60;
            string label = showMinutes ? $"{hour:D2}:{minute:D2}" : $"{hour:D2}h";
            SizeF size = g.MeasureString(label, font);
            float lx = x - size.Width / 2f;
            g.DrawString(label, font, textBrush, lx, labelTop);
        }
    }

    private static int ChooseTickIntervalMinutes(double pxPerHour)
    {
        // Pick the finest interval whose label spacing stays readable.
        // Thresholds doubled vs the dense version: roughly half as many ticks.
        if (pxPerHour >= 480) return 5;
        if (pxPerHour >= 240) return 10;
        if (pxPerHour >= 120) return 15;
        if (pxPerHour >= 60) return 30;
        if (pxPerHour >= 24) return 60;
        if (pxPerHour >= 12) return 120;
        if (pxPerHour >= 6) return 180;
        if (pxPerHour >= 3.6) return 360;
        if (pxPerHour >= 1.8) return 720;
        return 1440;
    }

    private static bool IsDark(Color c) => (c.R * 0.299 + c.G * 0.587 + c.B * 0.114) < 140;

    private HitItem? HitTest(Point clientPoint)
    {
        Point p = new(clientPoint.X - AutoScrollPosition.X, clientPoint.Y - AutoScrollPosition.Y);
        // Reverse order so topmost segment wins (last drawn on top).
        for (int i = _hitItems.Count - 1; i >= 0; i--)
        {
            if (_hitItems[i].Rect.Contains(p))
            {
                return _hitItems[i];
            }
        }
        return null;
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        HitItem? hit = HitTest(e.Location);
        if (hit is null)
        {
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            SegmentRightClicked?.Invoke(this, hit.Segment.EventId);
        }
        else if (e.Button == MouseButtons.Left)
        {
            SegmentClicked?.Invoke(this, hit.Segment.EventId);
        }
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        HitItem? hit = HitTest(e.Location);
        if (hit is null)
        {
            if (_lastTip is not null)
            {
                _tooltip.SetToolTip(this, string.Empty);
                _lastTip = null;
            }
            Cursor = Cursors.Default;
            return;
        }

        Cursor = Cursors.Hand;
        TimeSpan duration = hit.Segment.EndLocal - hit.Segment.StartLocal;
        string tip = $"{hit.Segment.Project}\n" +
                     $"{hit.Segment.StartLocal:yyyy-MM-dd HH:mm} → {hit.Segment.EndLocal:HH:mm}\n" +
                     $"{(int)duration.TotalHours}h {duration.Minutes:D2}m\n" +
                     "Left click: edit | Right click: delete";
        if (tip != _lastTip)
        {
            _tooltip.SetToolTip(this, tip);
            _lastTip = tip;
        }
    }
}

