using cgmon.Services;

namespace cgmon.UI;

public class SettingsForm : Form
{
    public AppSettings Settings { get; private set; }

    private readonly NumericUpDown _polling;
    private readonly NumericUpDown _cpuWarn;
    private readonly NumericUpDown _cpuCrit;
    private readonly NumericUpDown _gpuWarn;
    private readonly NumericUpDown _gpuCrit;

    public SettingsForm(AppSettings current)
    {
        Settings = current;
        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(310, 215);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(10, 8, 10, 8),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));

        _polling = Spin(1, 3600, current.PollingIntervalSeconds);
        _cpuWarn = Spin(40, 100, (int)current.CpuWarningThreshold);
        _cpuCrit = Spin(40, 110, (int)current.CpuCriticalThreshold);
        _gpuWarn = Spin(40, 100, (int)current.GpuWarningThreshold);
        _gpuCrit = Spin(40, 110, (int)current.GpuCriticalThreshold);

        AddRow(layout, 0, "Polling interval (s):",   _polling);
        AddRow(layout, 1, "CPU warning (°C):",        _cpuWarn);
        AddRow(layout, 2, "CPU critical (°C):",       _cpuCrit);
        AddRow(layout, 3, "GPU warning (°C):",        _gpuWarn);
        AddRow(layout, 4, "GPU critical (°C):",       _gpuCrit);

        var btnPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            Height = 38,
            Padding = new Padding(4),
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 72 };
        var ok     = new Button { Text = "OK",     DialogResult = DialogResult.OK,     Width = 72 };
        ok.Click += (_, _) => Apply();
        btnPanel.Controls.AddRange(new Control[] { cancel, ok });

        CancelButton = cancel;
        AcceptButton = ok;

        Controls.Add(layout);
        Controls.Add(btnPanel);
    }

    private static NumericUpDown Spin(int min, int max, int val) => new()
    {
        Minimum = min,
        Maximum = max,
        Value   = Math.Clamp(val, min, max),
        Dock    = DockStyle.Fill,
    };

    private static void AddRow(TableLayoutPanel tbl, int row, string label, Control ctrl)
    {
        tbl.Controls.Add(new Label
        {
            Text      = label,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, row);
        tbl.Controls.Add(ctrl, 1, row);
    }

    private void Apply()
    {
        // Validate: warning must be below critical
        if (_cpuWarn.Value >= _cpuCrit.Value)
        {
            MessageBox.Show("CPU warning must be lower than critical threshold.",
                "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }
        if (_gpuWarn.Value >= _gpuCrit.Value)
        {
            MessageBox.Show("GPU warning must be lower than critical threshold.",
                "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Settings = new AppSettings
        {
            PollingIntervalSeconds = (int)_polling.Value,
            StartWithWindows       = Settings.StartWithWindows,
            ShowCpuIcon            = Settings.ShowCpuIcon,
            ShowGpuIcon            = Settings.ShowGpuIcon,
            PreferredCpuSensorName = Settings.PreferredCpuSensorName,
            PreferredGpuSensorName = Settings.PreferredGpuSensorName,
            CpuWarningThreshold    = (float)_cpuWarn.Value,
            CpuCriticalThreshold   = (float)_cpuCrit.Value,
            GpuWarningThreshold    = (float)_gpuWarn.Value,
            GpuCriticalThreshold   = (float)_gpuCrit.Value,
        };
    }
}
