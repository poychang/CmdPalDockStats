# Development Packaging

The build produces an unsigned MSIX under `artifacts/MSIX/` and a self-contained
x64 SensorHost under `artifacts/SensorHost/`. It does not install either component.

## Extension

The development package identity is `PoyChang.CmdPalDockStats`, with publisher
`CN=PoyChang`. This is not a Microsoft Store identity. Before distribution,
update the manifest/project identity consistently and use your approved signing
certificate. Do not reuse CmdPalTranslator's identity or COM class GUID.

For local testing, sign the generated package with Windows SDK SignTool using a
certificate whose subject matches the manifest publisher, then trust that
certificate on the test machine according to your organization's policy.
Install the signed MSIX with App Installer or `Add-AppxPackage -Path <signed.msix>`.
The unsigned file is a build artifact, not a ready-to-install release.

Alternatively, Visual Studio with MSIX tooling can deploy the project for local
debugging. Verify the registration and reload Command Palette after deployment.

## SensorHost

For non-elevated development, run `dotnet run --project src/CmdPalDockStats.SensorHost`.
For the extension's Diagnostics launch commands, the entire published helper
directory must be installed to:

```text
C:\Program Files\CmdPalDockStats\SensorHost\
```

Install this directory manually from an administrator terminal or with a reviewed
installer. Ensure ordinary users cannot modify the EXE, DLLs, configuration or
parent directories. Do not elevate a helper directly from the repository,
Downloads, AppData, or any user-writable output directory.

The app only requests UAC after the user explicitly selects **Enable advanced
sensors (UAC)**. It never automatically starts an elevated process. A different
administrator account is intentionally unsupported: the helper uses its own
SID/session, so the original user cannot connect. Canceling UAC leaves Windows
metrics running. Same-account elevation still requires manual validation.

Only one helper should be running per user/session. Stop the normal helper before
starting an elevated one. The helper exits after 60 seconds without connections;
there is no service, scheduled task or login autostart. Close its console to stop
it; remove its installation directory to uninstall after stopping it.

## Driver Dependencies

LHM 0.9.6 uses PawnIO-related components for some sensors. A NuGet restore does not
establish that the required driver is installed or accessible. Obtain required
signed dependencies only from upstream's documented release/install process,
review their licenses, and record the tested version and Windows security policy.
No driver is installed or redistributed by this scaffold. Never disable Memory
Integrity, Secure Boot or driver blocklists to make sensors work.

## Release Gates

Before release: signed clean-machine install/upgrade/uninstall, elevated helper
IPC, unauthorized-user and remote rejection tests, driver compatibility,
multi-display Dock behavior, CPU temperature end-to-end validation, second
hardware configuration, 24-hour stability and resource-cost measurement.