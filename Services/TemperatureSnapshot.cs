namespace cgmon.Services;

public class TemperatureSnapshot
{
    public float? CpuTemperature { get; init; }
    public float? GpuTemperature { get; init; }
    public bool HasGpu { get; init; }
    public bool CpuReadFailed { get; init; }
    public bool GpuReadFailed { get; init; }
    public DateTime Timestamp { get; init; }
    public IReadOnlyList<TemperatureReading> AllReadings { get; init; } = [];

    public static TemperatureSnapshot Empty { get; } = new() { Timestamp = DateTime.Now };
}
