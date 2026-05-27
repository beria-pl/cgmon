using cgmon.Services;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace cgmon.UI;

public class GraphForm : Form
{
    private static readonly TimeSpan HistorySpan   = TimeSpan.FromMinutes(10);
    private static readonly Color    CpuLineColor  = Color.FromArgb(50, 100, 200);
    private static readonly Color    GpuLineColor  = Color.FromArgb(200, 80, 30);

    private readonly List<TempPoint> _history;
    private readonly Label           _cpuLabel;
    private readonly Label           _gpuLabel;
    private readonly Label           _timeLabel;
    private readonly GraphPanel      _graphPanel;

    public GraphForm(List<TempPoint> initialHistory)
    {
        _history = initialHistory;

        Text            = "Temperature Monitor";
        Size            = new Size(580, 380);
        MinimumSize     = new Size(420, 300);
        StartPosition   = FormStartPosition.CenterScreen;

        var header = new Panel { Dock = DockStyle.Top, Height = 58 };
        _cpuLabel  = MakeHeaderLabel("CPU: --",  14,  CpuLineColor);
        _gpuLabel  = MakeHeaderLabel("GPU: --",  210, GpuLineColor);
        _timeLabel = new Label
        {
            AutoSize  = true,
            Location  = new Point(14, 34),
            ForeColor = Color.Gray,
            Font      = new Font("Segoe UI", 8.5f),
        };
        header.Controls.AddRange(new Control[] { _cpuLabel, _gpuLabel, _timeLabel });

        _graphPanel = new GraphPanel(_history) { Dock = DockStyle.Fill };

        var status = new StatusStrip();
        status.Items.Add(new ToolStripStatusLabel(
            "Tip: run as administrator to expose additional sensors.")
        {
            ForeColor = Color.Gray,
        });

        Controls.Add(_graphPanel);
        Controls.Add(header);
        Controls.Add(status);

        if (_history.Count > 0)
        {
            var last = _history[^1];
            _cpuLabel.Text  = last.Cpu.HasValue ? $"CPU: {last.Cpu.Value:F0}°C"  : "CPU: --";
            _gpuLabel.Text  = last.Gpu.HasValue ? $"GPU: {last.Gpu.Value:F0}°C"  : "GPU: --";
            _timeLabel.Text = $"Last update: {last.Time:HH:mm:ss}";
        }
    }

    public void OnSnapshotUpdated(object? sender, TemperatureSnapshot snap)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => OnSnapshotUpdated(sender, snap)); return; }

        _history.Add(new TempPoint(snap.Timestamp, snap.CpuTemperature, snap.GpuTemperature));

        var cutoff = snap.Timestamp - HistorySpan;
        int toRemove = 0;
        while (toRemove < _history.Count && _history[toRemove].Time < cutoff) toRemove++;
        if (toRemove > 0) _history.RemoveRange(0, toRemove);

        _cpuLabel.Text  = snap.CpuTemperature.HasValue
            ? $"CPU: {snap.CpuTemperature.Value:F0}°C" : "CPU: --";
        _gpuLabel.Text  = snap.GpuTemperature.HasValue
            ? $"GPU: {snap.GpuTemperature.Value:F0}°C"
            : snap.HasGpu ? "GPU: --" : "GPU: n/a";
        _timeLabel.Text = $"Last update: {snap.Timestamp:HH:mm:ss}";

        _graphPanel.Invalidate();
    }

    private sealed class GraphPanel : Panel
    {
        private const float TempMin   = 20f;
        private const float TempMax   = 100f;
        private const float TempRange = TempMax - TempMin;

        private readonly List<TempPoint> _data;

        public GraphPanel(List<TempPoint> data)
        {
            _data         = data;
            DoubleBuffered = true;
            BackColor      = SystemColors.Window;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var b = ClientRectangle;
            const int mL = 42, mR = 12, mT = 10, mB = 28;
            var plot = new Rectangle(b.Left + mL, b.Top + mT,
                                     b.Width  - mL - mR,
                                     b.Height - mT - mB);
            if (plot.Width < 20 || plot.Height < 20) return;

            var now   = DateTime.Now;
            var start = now - HistorySpan;

            float Tx(DateTime t) =>
                plot.Left + (float)(t - start).TotalSeconds
                            / (float)HistorySpan.TotalSeconds * plot.Width;
            float Ty(float v) =>
                plot.Bottom - Math.Clamp((v - TempMin) / TempRange, 0f, 1f) * plot.Height;

            DrawGrid(g, plot, Ty);

            g.SetClip(plot);
            if (_data.Count >= 2)
            {
                DrawLine(g, _data, p => p.Cpu, CpuLineColor, Tx, Ty);
                DrawLine(g, _data, p => p.Gpu, GpuLineColor, Tx, Ty);
            }
            g.ResetClip();

            DrawBorder(g, plot);
            DrawLegend(g, plot);
        }

        private static void DrawGrid(Graphics g, Rectangle plot, Func<float, float> ty)
        {
            using var linePen = new Pen(SystemColors.ControlLight, 1f);
            using var font    = new Font("Segoe UI", 7.5f);
            using var brush   = new SolidBrush(SystemColors.GrayText);
            var rightFmt = new StringFormat
            {
                Alignment     = StringAlignment.Far,
                LineAlignment = StringAlignment.Center,
            };
            var centerFmt = new StringFormat { Alignment = StringAlignment.Center };

            // Horizontal temperature lines
            for (float t = TempMin; t <= TempMax; t += 20)
            {
                float y = ty(t);
                g.DrawLine(linePen, plot.Left, y, plot.Right, y);
                g.DrawString($"{t:F0}", font, brush,
                    new RectangleF(0, y - 9, plot.Left - 3, 18), rightFmt);
            }

            // Vertical time lines
            for (int m = 0; m <= 10; m += 2)
            {
                float x = plot.Left + (float)m / 10f * plot.Width;
                g.DrawLine(linePen, x, plot.Top, x, plot.Bottom);
                int ago  = 10 - m;
                string lbl = ago == 0 ? "now" : $"-{ago}m";
                g.DrawString(lbl, font, brush,
                    new RectangleF(x - 18, plot.Bottom + 3, 36, 16), centerFmt);
            }
        }

        private static void DrawLine(
            Graphics g, List<TempPoint> data,
            Func<TempPoint, float?> getVal, Color color,
            Func<DateTime, float> tx, Func<float, float> ty)
        {
            using var pen = new Pen(color, 2f) { LineJoin = LineJoin.Round };
            var pts = new List<PointF>(data.Count);

            void Flush()
            {
                if (pts.Count >= 2) g.DrawLines(pen, pts.ToArray());
                pts.Clear();
            }

            foreach (var p in data)
            {
                float? v = getVal(p);
                if (!v.HasValue) { Flush(); continue; }
                pts.Add(new PointF(tx(p.Time), ty(v.Value)));
            }
            Flush();
        }

        private static void DrawBorder(Graphics g, Rectangle plot)
        {
            using var pen = new Pen(SystemColors.ControlDark, 1f);
            g.DrawRectangle(pen, plot);
        }

        private static void DrawLegend(Graphics g, Rectangle plot)
        {
            using var font     = new Font("Segoe UI", 8f);
            using var cpuPen   = new Pen(CpuLineColor, 2.5f);
            using var gpuPen   = new Pen(GpuLineColor, 2.5f);
            using var cpuBrush = new SolidBrush(CpuLineColor);
            using var gpuBrush = new SolidBrush(GpuLineColor);

            float lx = plot.Left + 8, ly = plot.Top + 8;
            g.DrawLine(cpuPen,  lx,      ly + 5, lx + 14,  ly + 5);
            g.DrawString("CPU", font, cpuBrush, lx + 18,  ly);
            g.DrawLine(gpuPen,  lx + 58, ly + 5, lx + 72,  ly + 5);
            g.DrawString("GPU", font, gpuBrush, lx + 76,  ly);
        }
    }

    private static Label MakeHeaderLabel(string text, int x, Color color) => new()
    {
        Text      = text,
        AutoSize  = true,
        Location  = new Point(x, 8),
        Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
        ForeColor = color,
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cpuLabel.Dispose();
            _gpuLabel.Dispose();
            _timeLabel.Dispose();
            _graphPanel.Dispose();
        }
        base.Dispose(disposing);
    }
}
