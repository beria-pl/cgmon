using LibreHardwareMonitor.Hardware;

namespace cgmon.Services;

public sealed class HardwareMonitorService : IDisposable
{
    private Computer? _computer;
    private readonly object _lock = new();
    private float? _lastValidCpuTemp;
    private float? _lastValidGpuTemp;
    private bool _diagnosticLogged;
    private bool _gpuEverDetected;

    public void Initialize()
    {
        try
        {
            // Assign _computer only after Open() succeeds so ReadTemperatures
            // knows whether initialisation actually completed.
            var c = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
            c.Open();
            _computer = c;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to initialize hardware monitor", ex);
        }
    }

    public TemperatureSnapshot ReadTemperatures(AppSettings settings)
    {
        if (_computer == null)
            return new TemperatureSnapshot
            {
                Timestamp = DateTime.Now,
                CpuTemperature = _lastValidCpuTemp,
                GpuTemperature = _lastValidGpuTemp,
                CpuReadFailed = true,
                GpuReadFailed = true,
            };

        lock (_lock)
        {
            var readings = new List<TemperatureReading>();
            float? cpuTemp = null;
            float? gpuTemp = null;
            bool cpuFound = false;
            bool gpuFound = false;
            bool hasGpu = false;
            var now = DateTime.Now;

            try
            {
                foreach (var hw in GetAllHardware(_computer))
                {
                    bool isCpu = hw.HardwareType == HardwareType.Cpu;
                    bool isGpu = hw.HardwareType is HardwareType.GpuNvidia
                                                 or HardwareType.GpuAmd
                                                 or HardwareType.GpuIntel;

                    if (isGpu) hasGpu = true;

                    try { hw.Update(); }
                    catch (Exception ex)
                    {
                        // Log but do NOT skip — still try to read cached sensor values.
                        Logger.LogError($"Update failed for {hw.Name}", ex);
                    }

                    var tempSensors = hw.Sensors
                        .Where(s => s.SensorType == SensorType.Temperature)
                        .ToList();

                    foreach (var s in tempSensors)
                    {
                        readings.Add(new TemperatureReading
                        {
                            SensorName   = s.Name,
                            HardwareName = hw.Name,
                            HardwareType = hw.HardwareType.ToString(),
                            Value        = s.Value,
                            Min          = s.Min,
                            Max          = s.Max,
                            LastUpdated  = now,
                        });
                    }

                    if (isCpu && !cpuFound)
                    {
                        cpuTemp = SelectCpuTemp(tempSensors, settings.PreferredCpuSensorName);
                        if (cpuTemp.HasValue) cpuFound = true;
                    }

                    if (isGpu && !gpuFound)
                    {
                        gpuTemp = SelectGpuTemp(tempSensors, settings.PreferredGpuSensorName);
                        if (gpuTemp.HasValue) gpuFound = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error reading temperatures", ex);
            }

            if (cpuFound) _lastValidCpuTemp = cpuTemp;
            if (gpuFound) _lastValidGpuTemp = gpuTemp;
            if (hasGpu)   _gpuEverDetected  = true;

            // One-time diagnostic: log what the library actually sees so we can diagnose
            // "no CPU temp" without needing a debugger attached.
            if (!_diagnosticLogged)
            {
                _diagnosticLogged = true;
                LogDiagnostics(_computer, cpuFound, gpuFound);
            }

            return new TemperatureSnapshot
            {
                CpuTemperature = cpuFound  ? cpuTemp : _lastValidCpuTemp,
                GpuTemperature = gpuFound  ? gpuTemp : _lastValidGpuTemp,
                HasGpu         = _gpuEverDetected,
                CpuReadFailed  = !cpuFound,
                GpuReadFailed  = hasGpu && !gpuFound,
                Timestamp      = now,
                AllReadings    = readings,
            };
        }
    }

    // CPU: Package > Core average > first available
    private static float? SelectCpuTemp(List<ISensor> sensors, string? preferred)
    {
        if (!string.IsNullOrEmpty(preferred))
        {
            var s = sensors.FirstOrDefault(x =>
                x.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase) && x.Value.HasValue);
            if (s != null) return s.Value;
        }

        var pkg = sensors.FirstOrDefault(x =>
            x.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) && x.Value.HasValue);
        if (pkg != null) return pkg.Value;

        var cores = sensors
            .Where(x => x.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) && x.Value.HasValue)
            .Select(x => x.Value!.Value)
            .ToList();
        if (cores.Count > 0) return cores.Average();

        return sensors.FirstOrDefault(x => x.Value.HasValue)?.Value;
    }

    // GPU: GPU Core > Hot Spot > first available
    private static float? SelectGpuTemp(List<ISensor> sensors, string? preferred)
    {
        if (!string.IsNullOrEmpty(preferred))
        {
            var s = sensors.FirstOrDefault(x =>
                x.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase) && x.Value.HasValue);
            if (s != null) return s.Value;
        }

        var core = sensors.FirstOrDefault(x =>
            x.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase) && x.Value.HasValue);
        if (core != null) return core.Value;

        var hot = sensors.FirstOrDefault(x =>
            x.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase) && x.Value.HasValue);
        if (hot != null) return hot.Value;

        return sensors.FirstOrDefault(x => x.Value.HasValue)?.Value;
    }

    private static IEnumerable<IHardware> GetAllHardware(Computer computer)
    {
        foreach (var hw in computer.Hardware)
            foreach (var item in Flatten(hw))
                yield return item;
    }

    private static IEnumerable<IHardware> Flatten(IHardware hw)
    {
        yield return hw;
        foreach (var sub in hw.SubHardware)
            foreach (var item in Flatten(sub))
                yield return item;
    }

    /// <summary>True when sensors were found but all values were null — likely needs admin.</summary>
    public bool CpuSensorsFoundButEmpty { get; private set; }

    private void LogDiagnostics(Computer computer, bool cpuFound, bool gpuFound)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Diagnostic — cpuFound={cpuFound}, gpuFound={gpuFound}");
            foreach (var hw in GetAllHardware(computer))
            {
                var temps = hw.Sensors
                    .Where(s => s.SensorType == SensorType.Temperature)
                    .ToList();
                sb.AppendLine($"  [{hw.HardwareType}] {hw.Name}  ({temps.Count} temp sensors)");
                foreach (var s in temps)
                    sb.AppendLine($"    {s.Name} = {(s.Value.HasValue ? s.Value.Value.ToString("F1") : "null")}");

                if (hw.HardwareType == HardwareType.Cpu && temps.Count > 0 && !cpuFound)
                    CpuSensorsFoundButEmpty = true;
            }
            if (!cpuFound)
                sb.AppendLine("  → No CPU temperature returned. " +
                    "Running as administrator may expose Ring0/MSR sensors.");
            Logger.LogError(sb.ToString());
        }
        catch { }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            try { _computer?.Close(); } catch { }
            _computer = null;
        }
    }
}
