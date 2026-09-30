# CmdPalDockStats

A PowerToys Command Palette extension for a customizable **My Stats** Dock Band,
with particular attention to real hardware temperature sensors.

**Status: development prototype, not a completed M0 or production release.**
The structure follows [CmdPalTranslator](https://github.com/poychang/CmdPalTranslator),
while separating the Windows providers and hardware helper as specified in [plan.md](plan.md).

Follow-up tasks and their acceptance criteria are tracked in [todo.md](todo.md).
Complete, validate and commit each task separately, including its updated checklist.

## Current Implementation

- A fixed-ID My Stats Band with live CPU/RAM values and user-selected metrics.
- CPU usage, physical memory, physical-disk read/write rates, volume capacity,
  per-interface network rates and battery/AC/charging/remaining-time status.
- LibreHardwareMonitor 0.9.6 in a separate, read-only SensorHost process: GPU,
  CPU, storage, motherboard and other sensors as actually reported by hardware.
- Temperature-first Sensor Explorer and exact source selection, add/remove,
  move up/down, rename, rebind, per-slot units and sampling preferences.
- Atomic local JSON settings, backup recovery, unknown-schema protection,
  warm-up/stale/error states and provider diagnostics.
- Bounded local Named Pipe IPC with caller SID/session checks and an ACL that denies network logons.

No synthetic temperatures, inferred sensor names, GPU-engine aggregation,
hardware writes, telemetry or cloud services are used by the application.
Missing sensor readings are displayed as unavailable rather than zero.

## Structure

```text
src/CmdPalDockStats.slnx
src/CmdPalDockStats.Extension/    COM app, Dock, pages, commands, MSIX assets
src/CmdPalDockStats.Core/         models, rates, formatting, settings, scheduler
src/CmdPalDockStats.Contracts/    metric DTOs and versioned read-only protocol
src/CmdPalDockStats.Windows/      PDH, native APIs, network and Pipe client
src/CmdPalDockStats.SensorHost/   LibreHardwareMonitor and Pipe server
tests/CmdPalDockStats.Tests/      hardware-independent unit tests
tests/CmdPalDockStats.IntegrationTests/  Windows and opt-in hardware tests
packaging/                       asset generator and installation instructions
docs/compatibility.md            verified hardware and remaining gates
docs/decisions/                  SDK, IPC and deployment decisions
```

## Development

Windows 11 x64, .NET 10 SDK and PowerToys with Command Palette Dock are required.
MSIX generation additionally needs Windows SDK/build tools. NuGet versions are
centrally pinned in `Directory.Packages.props`; commit the generated lock files.

```powershell
dotnet restore src/CmdPalDockStats.slnx --locked-mode
dotnet build src/CmdPalDockStats.slnx -p:Platform=x64
dotnet test tests/CmdPalDockStats.Tests
dotnet test tests/CmdPalDockStats.IntegrationTests --filter "TestCategory!=Hardware"
dotnet test tests/CmdPalDockStats.IntegrationTests --filter TestCategory=Hardware
```

The hardware test launches a real SensorHost and requires an LHM-supported GPU
with an available temperature. Do not run it alongside an already running helper.

Probe actual temperatures without installing the extension:

```powershell
dotnet run --project src/CmdPalDockStats.SensorHost -- --once
```

Run the helper at normal privileges during development in a separate terminal:

```powershell
dotnet run --project src/CmdPalDockStats.SensorHost
```

Build the unsigned development MSIX and self-contained helper:

```powershell
./build-app-packages.ps1
```

See [packaging/README.md](packaging/README.md) for signing, deployment and the
separate protected helper installation. No certificate, driver or privileged
process is installed automatically by the build.

## Usage After Deployment

1. Open Command Palette, reload extensions, then open **CmdPalDockStats**.
2. Start SensorHost from **Diagnostics**, or run the development helper above.
3. Open **Sensor Explorer**, choose a metric and select **Add to My Stats**.
4. In **My Stats**, use the item's additional commands to move, rename, rebind or remove it.
5. In PowerToys Dock settings, add the **My Stats** Band. The extension does not change host pinning settings.

CPU/RAM are selected initially. Network interfaces, temperature sensors and
battery metrics are never silently picked for the user. Preferences control
global units and finite sampling intervals; slot-specific units override them.
Settings are stored at `%LOCALAPPDATA%\CmdPalDockStats\settings.json`.

## Important Limits

- GPU temperature and same-user, non-elevated IPC were verified on one laptop;
  CPU temperatures were enumerated but unavailable without elevation.
- Dock rendering, live collection reorder, package installation, elevated IPC,
  UAC cancellation and a second hardware configuration still need manual verification.
- SensorHost currently updates all enabled LHM hardware together, with a minimum
  two-second cache. Demand-based hardware activation, separate storage/battery
  intervals, rescanning and a hung-helper watchdog are not implemented yet.
- PDH physical-disk instances are not durable fingerprints. Recheck disk slots
  after hardware changes; automatic remapping is not implemented.
- Exact selectors only; automatic temperature policies, presets, export,
  power-saving scheduling and charts remain future work.
- ARM64 is not hardware-validated; the SensorHost distribution is x64 only.

See [docs/compatibility.md](docs/compatibility.md) for evidence and outstanding
acceptance gates, and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for licenses.