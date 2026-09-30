# Privacy

CmdPalDockStats runs locally. It does not upload metrics, use accounts, call cloud
APIs or expose an HTTP server. Hardware samples stay in memory and travel through
a local access-controlled Named Pipe.

Settings include selected metric/device identifiers, display names and
preferences. They are stored in the current user's local application-data
directory, with local backups for recovery. No raw hardware serial numbers,
credentials or UAC approval are deliberately stored.

Diagnostic information can contain device names and identifiers. Review and
redact logs before sharing them. There is no automatic diagnostic export.
Build/package restore uses NuGet; this is not a runtime monitoring connection.