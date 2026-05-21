# Dedicated Route Relationship Long-Run Freshness Proof

Date: 2026-05-21

## Result

PASS after correcting the long-run freshness guard.

The first execution failed before launching server/client processes because the new long-run guard compared `Rootborn.Game.dll` timestamps against `Scripts/qa/run-dedicated-route-relationship-longrun.ps1`. Current Unity player builds preserve managed assembly timestamps when only a PowerShell runner changes, so that comparison can block valid artifacts permanently.

The guard now matches the dedicated smoke runner pattern: it validates required server/client artifacts, compares managed DLL freshness against the compiled autoplay C# source, and keeps the runner script name in remediation output.

## Initial Failure

Command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-longrun.ps1 -RunName network-dedicated-route-relationship-longrun-rebuild-proof -Cycles 2 -StartPort 7991 -WaitSeconds 100
```

Failure:

```text
Stale server Rootborn.Game.dll for run-dedicated-route-relationship-longrun.ps1.
Builds\Server\Windows\rootborn-server_Data\Managed\Rootborn.Game.dll is older than Scripts\qa\run-dedicated-route-relationship-longrun.ps1.
```

## Guard Correction

Updated:

```text
Scripts/qa/run-dedicated-route-relationship-longrun.ps1
```

The `$requiredSources` list now contains only:

```text
Assets\Scripts\Game\Common\DirectValidationRouteRelationshipAutoplayInstaller.cs
```

The runner still validates:

- server executable
- client executable
- server `Rootborn.Game.dll`
- client `Rootborn.Game.dll`
- server Addressables runtime `settings.json`
- client Addressables runtime `settings.json`

## Artifact Timestamps

| Artifact | Size | LastWriteTime |
| --- | ---: | --- |
| `Builds/Server/Windows/rootborn-server.exe` | 667648 | 2026-05-18 21:01:08 |
| `Builds/Server/Windows/rootborn-server_Data/Managed/Rootborn.Game.dll` | 922624 | 2026-05-21 19:23:59 |
| `Builds/Server/Windows/rootborn-server_Data/StreamingAssets/aa/settings.json` | 864 | 2026-05-21 20:20:49 |
| `Builds/Client/Windows/rootborn.exe` | 667648 | 2026-05-11 14:27:54 |
| `Builds/Client/Windows/rootborn_Data/Managed/Rootborn.Game.dll` | 922624 | 2026-05-21 19:32:08 |
| `Builds/Client/Windows/rootborn_Data/StreamingAssets/aa/settings.json` | 864 | 2026-05-21 21:18:04 |
| `Assets/Scripts/Game/Common/DirectValidationRouteRelationshipAutoplayInstaller.cs` | 26243 | 2026-05-21 05:57:35 |
| `Scripts/qa/run-dedicated-route-relationship-longrun.ps1` | 10784 | 2026-05-21 21:12:39 |

The managed DLL artifacts are newer than the compiled autoplay source.

## Successful Command

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-longrun.ps1 -RunName network-dedicated-route-relationship-longrun-rebuild-proof -Cycles 2 -StartPort 7991 -WaitSeconds 100
```

Runner output:

```text
Result           : Passed
ServerExecutable : C:\Users\jdyj\farmer\Builds\Server\Windows\rootborn-server.exe
ClientExecutable : C:\Users\jdyj\farmer\Builds\Client\Windows\rootborn.exe
LogDir           : C:\Users\jdyj\farmer\Builds\Logs\network-dedicated-route-relationship-longrun-rebuild-proof
Cycles           : 2
StartPort        : 7991
SaveSlot         : network-dedicated-route-relationship-longrun-rebuild-proof
Screenshots      : 2
```

## Evidence Files

`Builds/Logs/network-dedicated-route-relationship-longrun-rebuild-proof`

| File | Size | Evidence |
| --- | ---: | --- |
| `network-dedicated-route-relationship-longrun-cycle-01-client.log` | 6688 | action client log |
| `network-dedicated-route-relationship-longrun-cycle-01-server.log` | 5761 | action server log |
| `network-dedicated-route-relationship-longrun-cycle-01.png` | 24631 | action screenshot |
| `network-dedicated-route-relationship-longrun-cycle-02-client.log` | 5755 | observe-only reload client log |
| `network-dedicated-route-relationship-longrun-cycle-02-server.log` | 5769 | observe-only reload server log |
| `network-dedicated-route-relationship-longrun-cycle-02.png` | 24631 | observe-only reload screenshot |

## Marker Evidence

Cycle 1 client log includes:

- `autoplay selected daily route`
- `dailyPlan=day:1,selected:daily.route.career.interior-practice`
- `relationships=relationship.teacher.support:2`
- `autoplay completed route-relationship`

Cycle 2 client log includes:

- `dailyPlan=day:1,selected:daily.route.career.interior-practice`
- `relationships=relationship.teacher.support:2`
- `autoplay completed route-relationship-observe`

## Verification Commands

```powershell
tests_run EditMode testClass DedicatedRouteRelationshipAutoplayContractTests
powershell.exe -NoProfile -Command "`$errors = `$null; `$tokens = `$null; `$null = [System.Management.Automation.Language.Parser]::ParseFile('Scripts/qa/run-dedicated-route-relationship-longrun.ps1', [ref]`$tokens, [ref]`$errors); if (`$errors -and `$errors.Count -gt 0) { `$errors | Format-List; exit 1 }"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-longrun.ps1 -RunName network-dedicated-route-relationship-longrun-rebuild-proof -Cycles 2 -StartPort 7991 -WaitSeconds 100
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-longrun-rebuild-proof -Recurse -File | Select-Object FullName,Length,LastWriteTime
Select-String -Path Builds/Logs/network-dedicated-route-relationship-longrun-rebuild-proof/*.log -Pattern "autoplay completed route-relationship|autoplay completed route-relationship-observe|dailyPlan=day:.*selected:|relationships="
```

Notes:

- The first `tests_run` invocation passed before the guard correction with `9/9` tests.
- Later MCP Unity test calls returned no result data while the Unity Editor instance was open, so the fresh parser check and the actual long-run command are the direct post-correction verification.
- CLI batchmode build was blocked because the project was already open in Unity.

## Scope Notes

- Runtime gameplay, ScriptableObject data, scenes, prefabs, UI, save formats, and autoplay C# behavior were not changed.
- The change is limited to the long-run runner's freshness criterion and documentation.
- The runner still fails before process launch on missing artifacts or stale managed DLLs relative to compiled autoplay source.
