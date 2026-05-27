namespace cgmon.Services;

public enum ContextMenuAction
{
    OpenDetails,
    Refresh,
    TogglePause,
    ToggleStartWithWindows,
    OpenSettings,
    RestartAsAdmin,
    Exit,
}

public sealed class TrayIconService : IDisposable
{
    private NotifyIcon?       _cpuIcon;
    private NotifyIcon?       _gpuIcon;
    private ContextMenuStrip? _menu;
    private ToolStripMenuItem? _pauseItem;
    private ToolStripMenuItem? _startupItem;
    private TaskbarRestartWindow? _restartWindow;

    private float? _prevCpuTemp;
    private float? _prevGpuTemp;
    private bool   _disposed;

    // Re-registers icons when Explorer restarts and sends WM_TASKBARCREATED.
    private sealed class TaskbarRestartWindow : NativeWindow, IDisposable
    {
        private static readonly uint WmTaskbarCreated =
            NativeMethods.RegisterWindowMessage("TaskbarCreated");

        private readonly Action _onRestart;

        internal TaskbarRestartWindow(Action onRestart)
        {
            _onRestart = onRestart;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if ((uint)m.Msg == WmTaskbarCreated) _onRestart();
            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
    }

    private readonly Action<ContextMenuAction> _handler;

    public TrayIconService(Action<ContextMenuAction> handler)
    {
        _handler = handler;
    }

    public void Initialize(bool startWithWindows)
    {
        _menu = BuildMenu(startWithWindows);

        _cpuIcon = new NotifyIcon { ContextMenuStrip = _menu };
        _cpuIcon.DoubleClick += (_, _) => _handler(ContextMenuAction.OpenDetails);
        ApplyCpuIcon("C--", TemperatureLevel.Normal, "CPU: -- | --:--:--");
        _cpuIcon.Visible = true;

        _restartWindow = new TaskbarRestartWindow(ReregisterIcons);
    }

    private void ReregisterIcons()
    {
        // Toggle Visible to force Shell_NotifyIcon(NIM_ADD) for both icons.
        if (_cpuIcon != null) { _cpuIcon.Visible = false; _cpuIcon.Visible = true; }
        if (_gpuIcon != null) { _gpuIcon.Visible = false; _gpuIcon.Visible = true; }
    }

    // Call after any modal dialog or secondary window closes — WinForms' modal
    // loop can invalidate the shell registration of lazily-created NotifyIcons.
    public void ReregisterAfterDialog() => ReregisterIcons();

    public void Update(TemperatureSnapshot snap, AppSettings s)
    {
        if (_disposed) return;
        UpdateCpu(snap, s);
        // Also update if the GPU icon was already created — don't let it go stale/silent.
        if (snap.HasGpu || _gpuIcon != null) UpdateGpu(snap, s);
    }

    private void UpdateCpu(TemperatureSnapshot snap, AppSettings s)
    {
        float? t = snap.CpuTemperature;
        if (t != _prevCpuTemp)
        {
            _prevCpuTemp = t;
            string label = t.HasValue ? $"C{(int)t.Value}" : "C--";
            var level = GetLevel(t, s.CpuWarningThreshold, s.CpuCriticalThreshold);
            ApplyCpuIcon(label, level, BuildTooltip("CPU", t, snap.CpuReadFailed, snap.Timestamp));
        }
        else if (_cpuIcon != null)
        {
            // Always refresh timestamp in tooltip
            _cpuIcon.Text = BuildTooltip("CPU", t, snap.CpuReadFailed, snap.Timestamp);
        }
    }

    private void UpdateGpu(TemperatureSnapshot snap, AppSettings s)
    {
        if (_gpuIcon == null)
        {
            _gpuIcon = new NotifyIcon { ContextMenuStrip = _menu };
            _gpuIcon.DoubleClick += (_, _) => _handler(ContextMenuAction.OpenDetails);
            ApplyGpuIcon("G--", TemperatureLevel.Normal, "GPU: -- | --:--:--");
            _gpuIcon.Visible = true;
        }

        float? t = snap.GpuTemperature;
        if (t != _prevGpuTemp)
        {
            _prevGpuTemp = t;
            string label = t.HasValue ? $"G{(int)t.Value}" : "G--";
            var level = GetLevel(t, s.GpuWarningThreshold, s.GpuCriticalThreshold);
            ApplyGpuIcon(label, level, BuildTooltip("GPU", t, snap.GpuReadFailed, snap.Timestamp));
        }
        else if (_gpuIcon != null)
        {
            _gpuIcon.Text = BuildTooltip("GPU", t, snap.GpuReadFailed, snap.Timestamp);
        }
    }

    private void ApplyCpuIcon(string label, TemperatureLevel level, string tooltip)
    {
        if (_cpuIcon == null) return;
        var newIcon = TemperatureIconRenderer.CreateIcon(label, level);
        var old = _cpuIcon.Icon;
        _cpuIcon.Icon = newIcon;
        _cpuIcon.Text = Truncate(tooltip, 63);
        old?.Dispose();
    }

    private void ApplyGpuIcon(string label, TemperatureLevel level, string tooltip)
    {
        if (_gpuIcon == null) return;
        var newIcon = TemperatureIconRenderer.CreateIcon(label, level);
        var old = _gpuIcon.Icon;
        _gpuIcon.Icon = newIcon;
        _gpuIcon.Text = Truncate(tooltip, 63);
        old?.Dispose();
    }

    public void SetPauseState(bool paused)
    {
        if (_pauseItem != null)
            _pauseItem.Text = paused ? "Resume Monitoring" : "Pause Monitoring";
    }

    public void SetStartWithWindows(bool enabled)
    {
        if (_startupItem != null)
            _startupItem.Checked = enabled;
    }

    private ContextMenuStrip BuildMenu(bool startWithWindows)
    {
        var strip = new ContextMenuStrip();

        strip.Items.Add("Open Details",    null, (_, _) => _handler(ContextMenuAction.OpenDetails));
        strip.Items.Add("Refresh",         null, (_, _) => _handler(ContextMenuAction.Refresh));

        _pauseItem = new ToolStripMenuItem("Pause Monitoring");
        _pauseItem.Click += (_, _) => _handler(ContextMenuAction.TogglePause);
        strip.Items.Add(_pauseItem);

        strip.Items.Add(new ToolStripSeparator());

        _startupItem = new ToolStripMenuItem("Start with Windows") { Checked = startWithWindows };
        _startupItem.Click += (_, _) => _handler(ContextMenuAction.ToggleStartWithWindows);
        strip.Items.Add(_startupItem);

        strip.Items.Add("Settings", null, (_, _) => _handler(ContextMenuAction.OpenSettings));
        strip.Items.Add("Run as Administrator", null,
            (_, _) => _handler(ContextMenuAction.RestartAsAdmin));

        strip.Items.Add(new ToolStripSeparator());

        strip.Items.Add("Exit", null, (_, _) => _handler(ContextMenuAction.Exit));

        // Re-register icons after the context menu closes — WinForms' message pump
        // can drop the GPU icon's shell registration when the menu is shown/hidden.
        strip.Closed += (_, _) => ReregisterIcons();

        return strip;
    }

    private static TemperatureLevel GetLevel(float? t, float warn, float crit)
    {
        if (!t.HasValue)       return TemperatureLevel.Normal;
        if (t.Value >= crit)   return TemperatureLevel.Critical;
        if (t.Value >= warn)   return TemperatureLevel.Warning;
        return TemperatureLevel.Normal;
    }

    private static string BuildTooltip(string prefix, float? t, bool failed, DateTime ts)
    {
        string val  = t.HasValue ? $"{t.Value:F0}°C" : "--";
        // Only annotate as stale when we ARE showing a cached value from a prior successful read.
        // When t is null we never had a reading — just show "--" with no extra annotation.
        string warn = failed && t.HasValue ? " (stale)" : "";
        return $"{prefix}: {val}{warn} | {ts:HH:mm:ss}";
    }

    public void ShowBalloonTip(string title, string text, int durationMs = 6000)
    {
        if (_cpuIcon == null) return;
        _cpuIcon.BalloonTipTitle = title;
        _cpuIcon.BalloonTipText  = text;
        _cpuIcon.BalloonTipIcon  = ToolTipIcon.Warning;
        _cpuIcon.ShowBalloonTip(durationMs);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _restartWindow?.Dispose();
        _restartWindow = null;
        DisposeNotifyIcon(ref _cpuIcon);
        DisposeNotifyIcon(ref _gpuIcon);
        _menu?.Dispose();
        _menu = null;
    }

    private static void DisposeNotifyIcon(ref NotifyIcon? ni)
    {
        if (ni == null) return;
        ni.Visible = false;
        ni.Icon?.Dispose();
        ni.Dispose();
        ni = null;
    }
}
