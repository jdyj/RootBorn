# Dedicated Server Multiplayer Validation Audit - 2026-05-18

## Scope

This audit follows `docs/superpowers/goals/2026-05-18-dedicated-server-multiplayer-validation-goal.md`.

The target was to close the remaining multiplayer gap by validating a real `rootborn-server.exe` dedicated server process with two separate `rootborn.exe` client processes. The previous blocker was missing Unity Windows Dedicated Server Build Support. That module is now installed and the dedicated runtime validation passes.

## Environment State

Branch:

```text
town-playable-baseline-recovery...origin/town-playable-baseline-recovery
```

Installed Unity playback engine evidence:

```text
C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\win64_server_development_mono
C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\win64_server_nondevelopment_mono
```

Unity Hub install evidence:

```text
UnitySetup-Windows-Server-Support-for-Editor-6000.3.13f1.exe /S /D=C:\Program Files\Unity\Hub\Editor\6000.3.13f1
```

Existing unrelated dirty/untracked workspace files were left untouched.

## Server Build Verification

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "C:\Users\jdyj\farmer" `
  -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64 `
  -logFile "Builds\Logs\server-build-windows-after-module-install-20260518.log" `
  -quit
```

Result:

```text
Build Finished, Result: Success.
[ROOTBORN] Server build -> Builds/Server/Windows/rootborn-server.exe result=Succeeded
```

Artifact:

```text
Builds\Server\Windows\rootborn-server.exe
```

## Client Build Verification

The latest fresh client build from this goal remained valid:

```text
Build Finished, Result: Success.
[ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded
```

Client artifact:

```text
Builds\Client\Windows\rootborn.exe
```

## Dedicated Server Smoke

New script:

```text
Scripts\qa\run-dedicated-multiplayer-smoke.ps1
```

Command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\qa\run-dedicated-multiplayer-smoke.ps1 `
  -Port 7851 `
  -RunName dedicated-multiplayer-verify-20260518-rerun `
  -WaitSeconds 125
```

Result:

```text
Result           : Passed
ServerExecutable : C:\Users\jdyj\farmer\Builds\Server\Windows\rootborn-server.exe
ClientExecutable : C:\Users\jdyj\farmer\Builds\Client\Windows\rootborn.exe
LogDir           : C:\Users\jdyj\farmer\Builds\Logs\dedicated-multiplayer-verify-20260518-rerun
Port             : 7851
SaveSlot         : dedicated-multiplayer-verify-20260518-rerun
```

Key verified log evidence:

```text
[ROOTBORN] Dedicated server started - port=7851 maxPlayers=4 saveSlot=dedicated-multiplayer-verify-20260518-rerun
[ROOTBORN] Dedicated server requested network scene load scene=Town status=Started
[ROOTBORN] Dedicated server client connected id=1
[ROOTBORN] Dedicated server client connected id=2
[ROOTBORN] Network player spawned owner=1 local=0 isOwner=False isServer=True playerId=client-1
[ROOTBORN] Network player spawned owner=2 local=0 isOwner=False isServer=True playerId=client-2
[ROOTBORN] World time day-end ready client=1 ready=2/2
[ROOTBORN] World time next day confirmed by server day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart
```

Client evidence:

```text
[ROOTBORN] Client started - joinIp=127.0.0.1 port=7851 saveSlot=dedicated-multiplayer-verify-20260518-rerun
[ROOTBORN] Network player spawned owner=1 local=1 isOwner=True isServer=False playerId=client-1
[ROOTBORN] Network player spawned owner=2 local=2 isOwner=True isServer=False playerId=client-2
[ROOTBORN] Student day result player=client-1 day=1 activities=1
[ROOTBORN] Student day result player=client-2 day=1 activities=1
[ROOTBORN] World time schedule sync previous=DayEndReady current=DayStart day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart
```

## CI Gate Verification

`Scripts\ci\run-multiplayer-validation-gate.ps1` now runs the dedicated server smoke when `-RequireDedicatedServer` is set and the server build succeeds.

Command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\ci\run-multiplayer-validation-gate.ps1 `
  -SkipClientBuild `
  -SmokePort 7854 `
  -SmokeRunName multiplayer-ci-dedicated-full-20260518 `
  -SmokeWaitSeconds 125 `
  -RequireDedicatedServer
```

Result:

```text
Result                  : Passed
SmokeLogDir             : C:\Users\jdyj\farmer\Builds\Logs\multiplayer-ci-dedicated-full-20260518
DedicatedSmokeLogDir    : C:\Users\jdyj\farmer\Builds\Logs\multiplayer-ci-dedicated-full-20260518-dedicated
ServerBuildLog          : C:\Users\jdyj\farmer\Builds\Logs\multiplayer-ci-dedicated-full-20260518-server-build.log
DedicatedServerRequired : True
```

This gate now covers:

- Host + two Client executable smoke through `run-direct-multiplayer-smoke.ps1`.
- Dedicated server player build.
- `rootborn-server.exe` + two `rootborn.exe` clients through `run-dedicated-multiplayer-smoke.ps1`.

## Host Smoke Versus Dedicated Smoke

Host smoke verifies `rootborn.exe -mode host`, where the server also owns `client-0`.

Dedicated smoke verifies `rootborn-server.exe -mode server`, where there is no local player and only connected clients own `client-1` and `client-2`. This separately exercises server-only scene loading, client connection approval, network player spawning, client-owned autoplay, and server-authoritative day advancement.

## Remaining Limits

- This is a headless executable smoke. It validates process-level networking and gameplay trace logs, not a rendered Game View.
- Client build was not rebuilt during the final CI gate command because `-SkipClientBuild` was intentionally used after the earlier fresh client build had already succeeded.
- Existing unrelated workspace changes remain outside this audit and were not staged.

## Outcome

Dedicated server runtime validation is complete for this goal.

The previous blocker is resolved:

- Unity Windows Dedicated Server support is installed.
- Fresh Windows dedicated server build succeeds.
- `Builds\Server\Windows\rootborn-server.exe` exists.
- Dedicated server + two client smoke passes.
- `-RequireDedicatedServer` CI gate now runs and requires the dedicated smoke.
