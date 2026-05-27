namespace cgmon.Services;

public class AppSettings
{
    public int PollingIntervalSeconds { get; set; } = 5;
    public bool StartWithWindows { get; set; } = false;
    public bool ShowCpuIcon { get; set; } = true;
    public bool ShowGpuIcon { get; set; } = true;
    public string? PreferredCpuSensorName { get; set; }
    public string? PreferredGpuSensorName { get; set; }
    public float CpuWarningThreshold { get; set; } = 80f;
    public float CpuCriticalThreshold { get; set; } = 90f;
    public float GpuWarningThreshold { get; set; } = 80f;
    public float GpuCriticalThreshold { get; set; } = 90f;
}
