# Compatibility Evidence

Date: 2026-09-30. These observations are from this development machine only.

| Component | Version / evidence |
| --- | --- |
| Windows | Windows 11 x64, build 26200 |
| .NET SDK | 10.0.401 |
| Command Palette SDK + bundled Toolkit | 0.9.260303001; Extension build passed |
| CsWinRT / WinRTServer | 2.2.0 / 2.2.1 |
| LibreHardwareMonitor | NuGet 0.9.6, upstream tag v0.9.6, commit 3d331e3370efb858411f19511373eff65a218701 |
| Installed PowerToys sparse package | 0.101.2362.0; installed version only, Dock interaction not verified |
| CPU | Intel Core i9-13900H; sensors enumerated, temperatures unavailable at standard privileges |
| GPU | NVIDIA GeForce RTX 4070 Laptop GPU; Core, Hot Spot and Memory Junction temperatures available |
| Windows provider | Real CPU warm-up/read, RAM and physical-disk PDH arrays verified by integration test |
| SensorHost IPC | Same-account, same-session, non-elevated snapshot round trip passed; unsupported protocol rejected without process exit |
| Full solution | x64 Debug build passed, no warnings or errors; locked restore passed |
| Automated tests | 10 passed: 7 Core, 2 Provider and 1 real hardware/IPC test |
| Packaging | x64 self-contained helper and unsigned MSIX generated successfully, no warnings; installation not performed |

GPU IPC test observed 54 C Core, 60.8125 C Hot Spot and 66 C Memory Junction.
These are individual samples, not accuracy or thermal-performance claims.
The sensor sources are distinct and are not substituted for one another.

## Not Yet Verified

- Real CPU temperature in the actual Dock; M0 remains open.
- Minimum supported PowerToys version, horizontal/vertical Dock, collection
  rebuild, nested navigation, display scaling and cross-thread notifications.
- Same-user elevated helper, different-account UAC refusal, cancellation,
  other-user/session/network ACL rejection and protected installation.
- Actual PawnIO driver version, signatures, dependency installation and security-policy compatibility.
- Two-machine coverage, storage temperatures, battery transitions, network
  disconnection/resume, hotplug identity and ARM64.
- Package signing/deployment lifecycle and 24-hour performance/stability gates.

Do not infer a PermissionRequired or DependencyMissing state from a null CPU
reading alone. The current implementation reports Unavailable and directs the
user to investigate privileges, driver dependencies and hardware support.