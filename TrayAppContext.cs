using cgmon.Services;
using cgmon.UI;

namespace cgmon;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly SettingsService        _settingsSvc;
    private readonly HardwareMonitorService _hardware;
    private readonly TrayIconService        _tray;
    private readonly StartupService         _startup;

    private AppSettings               _settings;
    private CancellationTokenSource   _cts = new();
    private Task?                     _loop;
    private readonly SynchronizationContext _sync;
    private TemperatureSnapshot?      _lastSnap;
    private bool                      _paused;
    private DetailsForm?              _details;

    public event EventHandler<TemperatureSnapshot>? SnapshotUpdated;

    public TrayAppContext()
    {
        _sync        = SynchronizationContext.Current ?? new SynchronizationContext();
        _settingsSvc = new SettingsService();
        _settings    = _settingsSvc.Load();
        _startup     = new StartupService();

        // Sync settings flag with actual registry state
        _settings.StartWithWindows = _startup.IsEnabled();

        _hardware = new HardwareMonitorService();
        _tray     = new TrayIconService(OnMenuAction);

        _hardware.Initialize();
        _tray.Initialize(_settings.StartWithWindows);

        _loop = Task.Run(() => MonitorLoop(_cts.Token));
    }

    private async Task MonitorLoop(CancellationToken ct)
    {
        // Brief startup delay so the app is fully initialised before first read
        try { await Task.Delay(400, ct); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(
            Math.Max(1, _settings.PollingIntervalSeconds)));

        do
        {
            if (!_paused) await ReadAndUpdateAsync();
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task ReadAndUpdateAsync()
    {
        try
        {
            var snap = await Task.Run(() => _hardware.ReadTemperatures(_settings));
            _lastSnap = snap;
            _sync.Post(_ =>
            {
                if (IsDisposed_) return;
                _tray.Update(snap, _settings);
                SnapshotUpdated?.Invoke(this, snap);
                MaybeShowAdminHint(snap);
            }, null);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error in monitoring loop", ex);
        }
    }

    private void MaybeShowAdminHint(TemperatureSnapshot snap)
    {
        if (_adminHintShown) return;
        // Only hint once, and only when CPU sensors were detected by LHM but all values were null
        // (the tell-tale sign of missing Ring0/admin access).
        if (!snap.CpuTemperature.HasValue && _hardware.CpuSensorsFoundButEmpty)
        {
            _adminHintShown = true;
            _tray.ShowBalloonTip(
                "CPU temperature unavailable",
                "CPU sensors found but no values returned.\n" +
                "Right-click → Run as Administrator to enable Ring0 access.");
        }
    }

    // Used to guard against posting to UI after disposal
    private volatile bool IsDisposed_;
    private bool _adminHintShown;

    private void OnMenuAction(ContextMenuAction action)
    {
        switch (action)
        {
            case ContextMenuAction.OpenDetails:
                OpenDetails();
                break;
            case ContextMenuAction.Refresh:
                _ = ReadAndUpdateAsync();
                break;
            case ContextMenuAction.TogglePause:
                _paused = !_paused;
                _tray.SetPauseState(_paused);
                break;
            case ContextMenuAction.ToggleStartWithWindows:
                _settings.StartWithWindows = !_settings.StartWithWindows;
                _startup.SetEnabled(_settings.StartWithWindows);
                _settingsSvc.Save(_settings);
                _tray.SetStartWithWindows(_settings.StartWithWindows);
                break;
            case ContextMenuAction.OpenSettings:
                OpenSettings();
                break;
            case ContextMenuAction.RestartAsAdmin:
                RestartAsAdmin();
                break;
            case ContextMenuAction.Exit:
                Exit();
                break;
        }
    }

    private void OpenDetails()
    {
        if (_details is { IsDisposed: false })
        {
            _details.BringToFront();
            return;
        }

        var form = new DetailsForm();
        _details = form;

        void OnClosed(object? s, FormClosedEventArgs e)
        {
            SnapshotUpdated -= form.OnSnapshotUpdated;
            form.FormClosed -= OnClosed;
            // Re-register both icons in case the window closing dropped the GPU icon's shell registration.
            _tray.ReregisterAfterDialog();
        }

        form.FormClosed += OnClosed;
        SnapshotUpdated += form.OnSnapshotUpdated;

        if (_lastSnap != null) form.OnSnapshotUpdated(this, _lastSnap);
        form.Show();
    }

    private void OpenSettings()
    {
        using var dlg = new SettingsForm(_settings);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _settings = dlg.Settings;
            _settingsSvc.Save(_settings);
            RestartLoop();
        }
        // Re-register both icons — the modal loop can drop the GPU icon's shell registration.
        _tray.ReregisterAfterDialog();
    }

    private void RestartLoop()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts  = new CancellationTokenSource();
        _loop = Task.Run(() => MonitorLoop(_cts.Token));
    }

    private void RestartAsAdmin()
    {
        try
        {
            var exe = Environment.ProcessPath ?? Application.ExecutablePath;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName       = exe,
                Verb           = "runas",
                UseShellExecute = true,
            });
            Exit();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled the UAC prompt — do nothing.
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to restart as administrator", ex);
        }
    }

    private void Exit()
    {
        _cts.Cancel();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            IsDisposed_ = true;
            _cts.Cancel();
            _tray.Dispose();
            _hardware.Dispose();
            _cts.Dispose();
            _details?.Dispose();
        }
        base.Dispose(disposing);
    }
}
