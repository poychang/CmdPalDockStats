# 0001: Extension and Isolated Read-Only SensorHost

Status: accepted for the initial development prototype, pending M0 host validation.

Use CmdPalTranslator's .NET 10, COM startup, centralized NuGet versions and MSIX
registration pattern, with a new package identity and COM GUID. The SDK includes
its compatible Toolkit; a separate Toolkit package is not necessary.

Core depends only on Contracts. Windows uses PDH English counter paths to avoid
localized-counter name lookups. CPU is `% Processor Time`, not task-manager
utility normalization. Physical disks and volumes are deliberately separate.
Network rates use monotonic time and reset the baseline after counter regression
or gaps longer than 30 seconds. Zero is valid; null and stale are not zero.

The SDK ListPage backing the Band has the fixed command ID
`net.poychang.cmdpaldockstats.band.main`. All callers reuse one Band, runtime and
snapshot. Slot IDs survive reordering/renaming, and deleted pinned slots produce
a missing-item page. Only exact sensor selection is implemented initially.

SensorHost is the only LHM consumer. Its hardware access is serialized. The pipe
exposes only versioned snapshot reads, with a 1 MiB frame limit and 4096 metric
limit. ACL grants the host user and SYSTEM, denies Network SID; post-handshake
impersonation verifies the client SID and the native API verifies session ID.
Same-user elevation is intended; cross-account elevation is refused by separate
SID-specific names/ACLs. Real elevated and unauthorized-client tests remain gates.

Plain JIT/self-contained distribution is the baseline. No trimming or AOT until
COM, LHM and native dependencies have been independently validated. SensorHost
must run from a protected install directory before it can be elevated by the UI.

The prototype samples Windows sources together and LHM sources together. Demand
activation, hardware rescanning, a driver-hang watchdog, durable physical-disk
fingerprints and separate low-frequency storage/battery polling are deferred.

References:

- https://github.com/poychang/CmdPalTranslator
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/adding-dock-support
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/using-form-pages
- https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/tag/v0.9.6