# CmdPalDockStats Development Instructions

## Task And Commit Workflow

- Read `todo.md` before starting work; `plan.md` defines product requirements and acceptance gates.
- Work on one identified task at a time. Complete its implementation and required validation before marking it done.
- After each completed task, update its checkbox in `todo.md` and relevant documentation, then immediately create a Git commit containing that task's code, tests, documentation and task status. The user has authorized one commit per completed task.
- Include the task ID in the commit message, for example `feat(TASK-007): add demand-based sensor sampling`.
- Do not mark partial or blocked work complete. Record the blocker and remaining verification instead.
- Review the staged changes and exclude unrelated user changes, generated build output, secrets and signing certificates.
- Do not push, amend existing commits, create branches or rewrite history unless explicitly requested.

## Project Boundaries

- Keep Core independent of the Command Palette SDK and LibreHardwareMonitor. Only SensorHost accesses LHM.
- Keep hardware operations read-only, local and isolated from the normal-privilege Extension.
- Do not fabricate sensor values, convert unknown/error readings to zero or silently substitute device identities or sensor semantics.
- Preserve stable Band and Slot IDs. Keep physical disks distinct from volumes.
- Never automatically elevate PowerToys or repeatedly prompt for UAC. Elevated helpers must load only from protected installation locations.
- Do not install drivers, trust certificates or change Windows security protections without explicit user authorization.
- Record real hardware and Dock evidence in `docs/compatibility.md`; compilation or simulated tests do not satisfy hardware acceptance gates.

## Validation

- Keep centrally pinned NuGet versions and committed lock files consistent.
- Build: `dotnet build src/CmdPalDockStats.slnx -p:Platform=x64`.
- Core tests: `dotnet test tests/CmdPalDockStats.Tests`.
- Non-hardware integration tests: `dotnet test tests/CmdPalDockStats.IntegrationTests --filter "TestCategory!=Hardware"`.
- Hardware tests require a supported GPU and no existing SensorHost: `dotnet test tests/CmdPalDockStats.IntegrationTests --filter TestCategory=Hardware`.
- Packaging: `./build-app-packages.ps1`; output is unsigned and is not an installable production release.