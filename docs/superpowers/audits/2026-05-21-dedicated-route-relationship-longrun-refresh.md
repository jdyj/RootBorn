# Dedicated Route Relationship Long-Run Refresh

Date: 2026-05-21

## Result

PASS.

The existing dedicated route/relationship long-run runner was executed against the current Windows server/client build artifacts with two cycles: one action cycle and one observe-only reload cycle.

## Command

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-route-relationship-longrun.ps1 -RunName network-dedicated-route-relationship-longrun-refresh -Cycles 2 -StartPort 7981 -WaitSeconds 100
```

Runner output:

```text
Result           : Passed
ServerExecutable : C:\Users\jdyj\farmer\Builds\Server\Windows\rootborn-server.exe
ClientExecutable : C:\Users\jdyj\farmer\Builds\Client\Windows\rootborn.exe
LogDir           : C:\Users\jdyj\farmer\Builds\Logs\network-dedicated-route-relationship-longrun-refresh
Cycles           : 2
StartPort        : 7981
SaveSlot         : network-dedicated-route-relationship-longrun-refresh
Screenshots      : 2
```

## Evidence Files

`Builds/Logs/network-dedicated-route-relationship-longrun-refresh`

| File | Size | Evidence |
| --- | ---: | --- |
| `network-dedicated-route-relationship-longrun-cycle-01-client.log` | 6670 | action client log |
| `network-dedicated-route-relationship-longrun-cycle-01-server.log` | 5743 | action server log |
| `network-dedicated-route-relationship-longrun-cycle-01.png` | 24631 | action screenshot |
| `network-dedicated-route-relationship-longrun-cycle-02-client.log` | 5732 | observe-only reload client log |
| `network-dedicated-route-relationship-longrun-cycle-02-server.log` | 5751 | observe-only reload server log |
| `network-dedicated-route-relationship-longrun-cycle-02.png` | 24631 | observe-only reload screenshot |

## Marker Evidence

Cycle 1 client log includes:

- `autoplay selected daily route`
- `dailyPlan=day:1,selected:daily.route.career...`
- `relationships=relationship.teacher.support:...`
- `autoplay completed route-relationship`

Cycle 2 client log includes:

- `dailyPlan=day:1,selected:daily.route.career...`
- `relationships=relationship.teacher.support:...`
- `autoplay completed route-relationship-observe`

The runner also completed without fatal-log failure and reported `Screenshots : 2`.

## Scope Notes

- This refresh did not change runtime code, tests, scenes, prefabs, ScriptableObjects, save formats, or QA runners.
- This refresh verifies the existing route/relationship long-run path only. Broader reconnect/restart expansion remains a separate future network hardening candidate.
- `Assets/AddressableAssetsData/link.xml` and `.meta` remained present after verification.

## Verification Commands

```powershell
Get-ChildItem Builds/Logs/network-dedicated-route-relationship-longrun-refresh -File | Select-Object Name,Length,LastWriteTime
Select-String -Path Builds/Logs/network-dedicated-route-relationship-longrun-refresh/*.log -Pattern "autoplay completed route-relationship|autoplay completed route-relationship-observe|dailyPlan=day:.*selected:|relationships="
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
git status --short
```

All commands completed successfully. `git status --short` showed only the new goal document before this audit was added.
