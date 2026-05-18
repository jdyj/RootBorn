# Dedicated Server Long-Run Multiplayer Simulation Audit

Date: 2026-05-18

## Objective

Implement the goal in `docs/superpowers/goals/2026-05-18-dedicated-server-long-run-multiplayer-simulation-goal.md` as a scriptable QA gate for ROOTBORN dedicated server multiplayer stability.

## Implemented Artifacts

- `Scripts/qa/test-dedicated-multiplayer-longrun-contract.ps1`
- `Scripts/qa/run-dedicated-multiplayer-longrun.ps1`
- `Scripts/ci/run-multiplayer-validation-gate.ps1`
- `docs/qa/dedicated-server-multiplayer-runbook.md`

## Prompt-To-Artifact Checklist

| Requirement | Evidence |
| --- | --- |
| Separate long-run script instead of overloading the two-client smoke | `Scripts/qa/run-dedicated-multiplayer-longrun.ps1` |
| Dedicated server plus up to four clients | `-ClientCount` defaults to `4` and validates range `2..4` |
| 10-minute default run | `-WaitSeconds` defaults to `600` |
| Run-specific logs | Script writes to `Builds\Logs\<RunName>` |
| Port collision failure | Script checks `Get-NetTCPConnection -LocalPort $Port` before launch |
| Process start failure/early exit surfaced | `Start-Process` uses `-PassThru`; wait loop fails on unexpected nonzero exits |
| Scheduled disconnect/reconnect | `-ReconnectClientIndex`, `-DisconnectAfterSeconds`, and `-ReconnectAfterSeconds` drive a client restart |
| Server restart with same save slot | `-RestartServerAfterFirstPass` restarts `rootborn-server.exe` with the same `-saveSlot` |
| Save reload evidence switch | `-RequireSaveReloadEvidence` requires same `saveSlot` evidence in restart logs |
| Server/client spawn evidence | Script asserts server `Network player spawned` and client owner logs |
| Ownership guard evidence | Script asserts server-observed `isOwner=False isServer=True` spawn logs and client `isOwner=True` owner logs |
| Player day-result evidence | Script asserts `Student day result player=client-` logs |
| World time/day progression evidence | Script asserts server Day 2 confirmation and client Day 2 sync |
| Fatal log scan | Script scans start failures, exceptions, bind errors, address-in-use, and owner mismatch |
| CI long-run option | `Scripts/ci/run-multiplayer-validation-gate.ps1` has `-RequireDedicatedLongRun` |
| Release/nightly knobs | CI exposes `-LongRunClientCount`, `-LongRunWaitSeconds`, `-LongRunReconnect`, `-LongRunRestartServer` |
| Documentation | `docs/qa/dedicated-server-multiplayer-runbook.md` includes long-run and CI examples |

## Verification Performed

RED:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\test-dedicated-multiplayer-longrun-contract.ps1
```

Expected and observed failure:

```text
Missing file: Scripts\qa\run-dedicated-multiplayer-longrun.ps1
```

GREEN/static verification:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\test-dedicated-multiplayer-longrun-contract.ps1
```

Observed:

```text
Result : Passed
```

Parser checks:

```powershell
[System.Management.Automation.Language.Parser]::ParseFile('Scripts\qa\run-dedicated-multiplayer-longrun.ps1', ...)
[System.Management.Automation.Language.Parser]::ParseFile('Scripts\ci\run-multiplayer-validation-gate.ps1', ...)
```

Observed: no parse errors.

Whitespace check:

```powershell
git diff --check -- Scripts\qa\test-dedicated-multiplayer-longrun-contract.ps1 Scripts\qa\run-dedicated-multiplayer-longrun.ps1 Scripts\ci\run-multiplayer-validation-gate.ps1
```

Observed: no whitespace errors.

Initial full long-run attempt:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\qa\run-dedicated-multiplayer-longrun.ps1 `
  -Port 7861 `
  -RunName dedicated-longrun-verify-20260518 `
  -ClientCount 4 `
  -WaitSeconds 600 `
  -ReconnectClientIndex 2 `
  -DisconnectAfterSeconds 180 `
  -ReconnectAfterSeconds 45 `
  -RestartServerAfterFirstPass `
  -RequireSaveReloadEvidence
```

Observed: the 600-second primary pass reached four clients, Day 2, and reconnect, but the fatal scanner failed on benign Unity/Transport text because `Select-String` matched `ERROR` case-insensitively against `Error` and `error`.

Fix: `Assert-NoFatalLogs` now uses `Select-String -CaseSensitive`. The contract test also asserts that fatal scanning remains case-sensitive.

Full long-run verification:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\qa\run-dedicated-multiplayer-longrun.ps1 `
  -Port 7861 `
  -RunName dedicated-longrun-verify-20260518-rerun `
  -ClientCount 4 `
  -WaitSeconds 600 `
  -ReconnectClientIndex 2 `
  -DisconnectAfterSeconds 180 `
  -ReconnectAfterSeconds 45 `
  -RestartServerAfterFirstPass `
  -RequireSaveReloadEvidence
```

Observed:

```text
Result: Passed
LogDir: Builds\Logs\dedicated-longrun-verify-20260518-rerun
ClientCount: 4
WaitSeconds: 600
ReconnectClientIndex: 2
RestartServerAfterFirstPass: True
RequireSaveReloadEvidence: True
```

CI wrapper verification with existing build artifacts:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\ci\run-multiplayer-validation-gate.ps1 `
  -SmokePort 7864 `
  -SmokeRunName multiplayer-ci-longrun-verify-20260518 `
  -SmokeWaitSeconds 125 `
  -SkipClientBuild `
  -SkipServerBuild `
  -RequireDedicatedServer `
  -RequireDedicatedLongRun `
  -LongRunClientCount 4 `
  -LongRunWaitSeconds 600 `
  -LongRunReconnect `
  -LongRunRestartServer
```

Observed:

```text
Result: Passed
SmokeLogDir: Builds\Logs\multiplayer-ci-longrun-verify-20260518
Dedicated smoke executed and passed at Builds\Logs\multiplayer-ci-longrun-verify-20260518-dedicated
DedicatedLongRunLogDir: Builds\Logs\multiplayer-ci-longrun-verify-20260518-dedicated-longrun
DedicatedServerRequired: True
DedicatedLongRunRequired: True
```

Note: this run revealed that the final `DedicatedSmokeLogDir` summary field was blank when `-SkipServerBuild` was used, even though the dedicated smoke executed and passed. The summary field was corrected after this run so future skip-build validations still report the dedicated smoke log directory.

Fresh build release/nightly gate:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\ci\run-multiplayer-validation-gate.ps1 `
  -SmokePort 7870 `
  -SmokeRunName multiplayer-ci-release-longrun-20260518 `
  -SmokeWaitSeconds 125 `
  -RequireDedicatedServer `
  -RequireDedicatedLongRun `
  -LongRunClientCount 4 `
  -LongRunWaitSeconds 600 `
  -LongRunReconnect `
  -LongRunRestartServer
```

Observed: client build, direct multiplayer smoke, server build, and dedicated long-run passed. The run exposed two QA harness issues before the result could be treated as trustworthy:

- The dedicated smoke assumed `client1-player.log` must own `client-1`, but NGO can assign local client IDs in the opposite order while still producing correct per-client ownership. The smoke now asserts ownership coverage across both client logs instead of relying on process launch order.
- The CI wrapper invoked child PowerShell scripts without checking `$LASTEXITCODE`, so a child smoke failure could be followed by a passing long-run and final `Result: Passed`. The wrapper now uses `Invoke-CheckedPowerShell` for direct smoke, dedicated smoke, and long-run invocations.

Fix verification:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\test-dedicated-multiplayer-longrun-contract.ps1
```

Observed: `Result: Passed`.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\qa\run-dedicated-multiplayer-smoke.ps1 `
  -Port 7875 `
  -RunName dedicated-smoke-owner-flex-20260518 `
  -WaitSeconds 125
```

Observed: `Result: Passed`.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\ci\run-multiplayer-validation-gate.ps1 `
  -SmokePort 7876 `
  -SmokeRunName multiplayer-ci-fixed-wrapper-longrun-20260518 `
  -SmokeWaitSeconds 125 `
  -SkipClientBuild `
  -SkipServerBuild `
  -RequireDedicatedServer `
  -RequireDedicatedLongRun `
  -LongRunClientCount 4 `
  -LongRunWaitSeconds 600 `
  -LongRunReconnect `
  -LongRunRestartServer
```

Observed:

```text
Result: Passed
SmokeLogDir: Builds\Logs\multiplayer-ci-fixed-wrapper-longrun-20260518
DedicatedSmokeLogDir: Builds\Logs\multiplayer-ci-fixed-wrapper-longrun-20260518-dedicated
DedicatedLongRunLogDir: Builds\Logs\multiplayer-ci-fixed-wrapper-longrun-20260518-dedicated-longrun
DedicatedServerRequired: True
DedicatedLongRunRequired: True
```

## Remaining Risk

Fresh build production readiness has now been exercised once through the release/nightly gate. The post-fix wrapper verification reused those fresh artifacts because the QA harness script changes do not affect player binaries.
