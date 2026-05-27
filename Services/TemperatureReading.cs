namespace cgmon.Services;

public class TemperatureReading
{
    public string SensorName { get; init; } = string.Empty;
    public string HardwareName { get; init; } = string.Empty;
    public string HardwareType { get; init; } = string.Empty;
    public float? Value { get; init; }
    public float? Min { get; init; }
    public float? Max { get; init; }
    public DateTime LastUpdated { get; init; }
}
