namespace cgmon.Services;

public readonly record struct TempPoint(DateTime Time, float? Cpu, float? Gpu);
