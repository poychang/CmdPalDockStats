# Third-Party Notices

CmdPalDockStats's own source uses the repository's existing MIT license.
The extension and helper include packages with their own licenses. Preserve the
upstream notices with binary distributions and review all transitive dependencies
in the committed NuGet lock files before release.

| Dependency | License / upstream |
| --- | --- |
| LibreHardwareMonitorLib 0.9.6 | MPL-2.0; https://github.com/LibreHardwareMonitor/LibreHardwareMonitor |
| Microsoft.CommandPalette.Extensions | MIT; https://github.com/microsoft/PowerToys |
| Microsoft.Windows.CsWinRT | MIT; https://github.com/microsoft/CsWinRT |
| Shmuelie.WinRTServer | MIT; https://github.com/Shmuelie/WinRTServer |
| MSTest (test-only) | MIT; https://github.com/microsoft/testfx |

LHM is used without source modifications. If its source files are modified,
comply with MPL-2.0's source availability requirements for those files.
PawnIO/drivers are not installed or redistributed by this repository; they
require a separate license/signature/install review. This document is a starting
inventory, not a completed distribution license audit.