using cgmon.Services;

namespace cgmon.UI;

public class DetailsForm : Form
{
    private readonly Label _cpuLabel;
    private readonly Label _gpuLabel;
    private readonly Label _timeLabel;
    private readonly DataGridView _grid;

    public DetailsForm()
    {
        Text = "Temperature Monitor – Details";
        Size = new Size(700, 460);
        MinimumSize = new Size(480, 320);
        StartPosition = FormStartPosition.CenterScreen;

        // Header panel
        var header = new Panel { Dock = DockStyle.Top, Height = 58 };
        _cpuLabel = MakeHeaderLabel("CPU: --", 10);
        _gpuLabel = MakeHeaderLabel("GPU: --", 200);
        _timeLabel = new Label
        {
            AutoSize = true,
            Location = new Point(10, 34),
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8.5f),
        };
        header.Controls.AddRange(new Control[] { _cpuLabel, _gpuLabel, _timeLabel });

        // Grid
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.None,
        };
        BuildColumns();

        // Status bar
        var status = new StatusStrip();
        var hint = new ToolStripStatusLabel("Tip: run as administrator to expose additional sensors.")
        {
            ForeColor = Color.Gray,
        };
        status.Items.Add(hint);

        Controls.Add(_grid);
        Controls.Add(header);
        Controls.Add(status);
    }

    private void BuildColumns()
    {
        Add("Sensor",    150);
        Add("Hardware",  160);
        Add("Type",      110);
        Add("°C",         60, DataGridViewContentAlignment.MiddleRight);
        Add("Min",        55, DataGridViewContentAlignment.MiddleRight);
        Add("Max",        55, DataGridViewContentAlignment.MiddleRight);
        Add("Updated",    72);

        void Add(string header, int w,
            DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = w,
                SortMode = DataGridViewColumnSortMode.Automatic,
            };
            col.DefaultCellStyle.Alignment = align;
            _grid.Columns.Add(col);
        }
    }

    public void OnSnapshotUpdated(object? sender, TemperatureSnapshot snap)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => OnSnapshotUpdated(sender, snap)); return; }

        _cpuLabel.Text = snap.CpuTemperature.HasValue
            ? $"CPU: {snap.CpuTemperature.Value:F0}°C" : "CPU: --";
        _gpuLabel.Text = snap.GpuTemperature.HasValue
            ? $"GPU: {snap.GpuTemperature.Value:F0}°C" : snap.HasGpu ? "GPU: --" : "GPU: n/a";
        _timeLabel.Text = $"Last update: {snap.Timestamp:HH:mm:ss}";

        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (var r in snap.AllReadings)
        {
            int i = _grid.Rows.Add(
                r.SensorName,
                r.HardwareName,
                r.HardwareType,
                r.Value?.ToString("F1") ?? "--",
                r.Min?.ToString("F1")   ?? "--",
                r.Max?.ToString("F1")   ?? "--",
                r.LastUpdated.ToString("HH:mm:ss"));

            // Colour row by temperature
            if (r.Value.HasValue)
            {
                var row = _grid.Rows[i];
                if (r.Value >= 90f)       row.DefaultCellStyle.BackColor = Color.FromArgb(255, 200, 200);
                else if (r.Value >= 80f)  row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 180);
            }
        }
        _grid.ResumeLayout();
    }

    private static Label MakeHeaderLabel(string text, int x) => new()
    {
        Text = text,
        AutoSize = true,
        Location = new Point(x, 8),
        Font = new Font("Segoe UI", 13f, FontStyle.Bold),
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cpuLabel.Dispose();
            _gpuLabel.Dispose();
            _timeLabel.Dispose();
            _grid.Dispose();
        }
        base.Dispose(disposing);
    }
}
