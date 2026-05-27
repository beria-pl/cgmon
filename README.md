# cgmon

Lightweight Windows system tray app that shows CPU and GPU temperatures in real time, directly in the taskbar notification area.

> **This project was fully created with [Claude](https://claude.ai) (Anthropic's AI assistant) via [Claude Code](https://claude.ai/code).**

---

## Features

- **One icon per sensor** — `C72` for CPU, `G61` for GPU (only shown when a GPU is detected)
- **Color-coded icons** — dark = normal, orange = warning, red = critical
- **Configurable thresholds** — separate warning/critical temperatures for CPU and GPU
- **Details window** — full sensor table with current / min / max values, color-coded by temperature
- **Pause / Resume** monitoring from the tray menu
- **Start with Windows** toggle (HKCU registry key)
- **Settings dialog** — polling interval and all four thresholds
- **Administrator restart** — context menu option to relaunch with UAC elevation for additional sensors (e.g. CPU MSR via WinRing0)
- **Stale-value detection** — tooltip shows `(stale)` when a cached reading is being displayed after a read failure
- **Error log** at `%AppData%\cgmon\errors.log`

---

## Requirements

- Windows 10 / 11 (x64 or x86)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build, or the Runtime to run

---

## Build

```powershell
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Single-file self-contained executable (no .NET runtime required on target machine)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\
```

The published executable will be at `publish\cgmon.exe`.

---

## Run

```powershell
dotnet run
# or after publish:
.\publish\cgmon.exe
```

The app starts silently — no main window. Look for one or two small icons in the system tray:

| Icon | Sensor |
|------|--------|
| **C**xx | CPU temperature |
| **G**xx | GPU temperature (only shown when a GPU sensor is detected) |

Right-click either icon to open the context menu. Double-click to open the Details window.

---

## Administrator mode

Some CPU sensors (MSR / Ring0 via WinRing0) require the process to run as administrator. If you see `C--` for CPU temperature, use the **Run as Administrator** option in the tray context menu — the app will re-launch with UAC elevation and CPU temperatures should appear.

```powershell
# Manual launch as admin
Start-Process .\publish\cgmon.exe -Verb RunAs
```

GPU sensors (NVAPI for NVIDIA, ADL for AMD) work without elevation.

---

## Icon colour coding

| Colour | Meaning |
|--------|---------|
| Dark | Normal (below warning threshold) |
| Orange | Warning (≥ warning threshold) |
| Red | Critical (≥ critical threshold) |

Default thresholds: **80 °C** warning, **90 °C** critical (configurable separately for CPU and GPU).

---

## Settings

Stored at `%AppData%\cgmon\settings.json`:

```json
{
  "PollingIntervalSeconds": 5,
  "StartWithWindows": false,
  "ShowCpuIcon": true,
  "ShowGpuIcon": true,
  "PreferredCpuSensorName": null,
  "PreferredGpuSensorName": null,
  "CpuWarningThreshold": 80,
  "CpuCriticalThreshold": 90,
  "GpuWarningThreshold": 80,
  "GpuCriticalThreshold": 90
}
```

Use **Settings** in the tray context menu to change polling interval and thresholds. Set `PreferredCpuSensorName` / `PreferredGpuSensorName` by editing the JSON directly — sensor names are visible in the Details window.

---

## Project structure

```
cgmon.csproj
Program.cs                    entry point — DPI + exception setup
TrayAppContext.cs             ApplicationContext orchestrator, monitoring loop
Logger.cs                     error-only file logger
NativeMethods.cs              DestroyIcon P/Invoke
Services/
  AppSettings.cs              settings model (JSON)
  HardwareMonitorService.cs   LibreHardwareMonitorLib wrapper
  SettingsService.cs          load/save settings.json
  StartupService.cs           HKCU Run registry key
  TemperatureIconRenderer.cs  renders 32×32 tray icons (System.Drawing)
  TemperatureReading.cs       single-sensor reading
  TemperatureSnapshot.cs      point-in-time snapshot of all readings
  TrayIconService.cs          manages NotifyIcon instances
UI/
  DetailsForm.cs              sensor table window
  SettingsForm.cs             threshold / interval settings dialog
```

---

## Third-party licence

This project uses **LibreHardwareMonitorLib** from [LibreHardwareMonitor/LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), licensed under the **Mozilla Public License 2.0 (MPL-2.0)**.

MPL-2.0 obligations when distributing:
- Make the MPL-2.0 licensed source files available (or link to the upstream repository).
- A copy of the MPL-2.0 licence text must accompany any distribution of the library.

Full licence text: [mozilla.org/en-US/MPL/2.0/](https://www.mozilla.org/en-US/MPL/2.0/)

---

## Licence

cgmon itself is released under the [MIT License](LICENSE).
