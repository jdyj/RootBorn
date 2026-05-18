# Dedicated Server Multiplayer Runbook

## Purpose

This runbook describes how to build and run ROOTBORN as a Windows dedicated server with separate Windows client players. Use it when validating real executable networking outside the Unity Editor.

## Prerequisites

- Unity `6000.3.13f1`.
- Windows Dedicated Server Build Support installed for this Unity editor.
- Current project path: `C:\Users\jdyj\farmer`.
- No open Unity Editor process for this project when running batchmode builds.
- Client and server ports must be free. The examples below use `7851`.

Check the dedicated server module:

```powershell
Get-ChildItem "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations" |
  Where-Object { $_.Name -match "server" } |
  Select-Object Name
```

Expected examples:

```text
win64_server_development_mono
win64_server_nondevelopment_mono
```

## Build Client

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "C:\Users\jdyj\farmer" `
  -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 `
  -logFile "Builds\Logs\client-build-dedicated-runbook.log" `
  -quit
```

Success criteria:

```text
Build Finished, Result: Success.
[ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded
```

Expected artifact:

```text
Builds\Client\Windows\rootborn.exe
```

## Build Dedicated Server

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "C:\Users\jdyj\farmer" `
  -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64 `
  -logFile "Builds\Logs\server-build-dedicated-runbook.log" `
  -quit
```

Success criteria:

```text
Build Finished, Result: Success.
[ROOTBORN] Server build -> Builds/Server/Windows/rootborn-server.exe result=Succeeded
```

Expected artifact:

```text
Builds\Server\Windows\rootborn-server.exe
```

If the log contains `Dedicated Server support for Win is not installed`, install Windows Dedicated Server Build Support through Unity Hub or:

```powershell
& "C:\Program Files\Unity Hub\Unity Hub.exe" -- --headless install-modules --version 6000.3.13f1 -m windows-server --errors
```

## Run Server And Clients Manually

Create a log directory:

```powershell
New-Item -ItemType Directory -Path "Builds\Logs\dedicated-manual" -Force
```

Start the dedicated server:

```powershell
Builds\Server\Windows\rootborn-server.exe `
  -batchmode -nographics `
  -logFile Builds\Logs\dedicated-manual\server-player.log `
  -mode server `
  -port 7851 `
  -maxPlayers 4 `
  -saveSlot dedicated-manual `
  -directValidationTrace
```

Start Client 1 in a second terminal:

```powershell
Builds\Client\Windows\rootborn.exe `
  -batchmode -nographics `
  -logFile Builds\Logs\dedicated-manual\client1-player.log `
  -mode client `
  -joinIp 127.0.0.1 `
  -port 7851 `
  -saveSlot dedicated-manual `
  -directValidationTrace `
  -directValidationAutoplay `
  -directValidationAutoplayDelaySeconds 55
```

Start Client 2 in a third terminal:

```powershell
Builds\Client\Windows\rootborn.exe `
  -batchmode -nographics `
  -logFile Builds\Logs\dedicated-manual\client2-player.log `
  -mode client `
  -joinIp 127.0.0.1 `
  -port 7851 `
  -saveSlot dedicated-manual `
  -directValidationTrace `
  -directValidationAutoplay `
  -directValidationAutoplayDelaySeconds 45
```

Stop processes after validation:

```powershell
Get-Process rootborn,rootborn-server -ErrorAction SilentlyContinue | Stop-Process -Force
```

## Expected Runtime Evidence

Server log:

```text
[ROOTBORN] Dedicated server started - port=7851 maxPlayers=4 saveSlot=dedicated-manual
[ROOTBORN] Dedicated server requested network scene load scene=Town status=Started
[ROOTBORN] Dedicated server client connected id=1
[ROOTBORN] Dedicated server client connected id=2
[ROOTBORN] Network player spawned owner=1 ... isServer=True playerId=client-1
[ROOTBORN] Network player spawned owner=2 ... isServer=True playerId=client-2
[ROOTBORN] World time next day confirmed by server day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart
```

Client logs:

```text
[ROOTBORN] Client started - joinIp=127.0.0.1 port=7851 saveSlot=dedicated-manual
[ROOTBORN] Network player spawned owner=1 ... isOwner=True ... playerId=client-1
[ROOTBORN] Network player spawned owner=2 ... isOwner=True ... playerId=client-2
[ROOTBORN] Student day result player=client-1 day=1 activities=1
[ROOTBORN] Student day result player=client-2 day=1 activities=1
[ROOTBORN] World time schedule sync ... day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart
```

Fatal log patterns to investigate:

```text
StartClient failed
StartHost failed
StartServer failed
Unhandled
NullReferenceException
InvalidOperationException
Exception
```

## Automated Smoke

Run only the dedicated server smoke:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\qa\run-dedicated-multiplayer-smoke.ps1 `
  -Port 7851 `
  -RunName dedicated-multiplayer-runbook `
  -WaitSeconds 125
```

This starts:

- `Builds\Server\Windows\rootborn-server.exe -mode server`
- `Builds\Client\Windows\rootborn.exe -mode client`
- a second `Builds\Client\Windows\rootborn.exe -mode client`

Logs are written to:

```text
Builds\Logs\dedicated-multiplayer-runbook
```

## Automated Long-Run

Run the dedicated server long-run simulation when validating release or nightly multiplayer stability. This starts one dedicated server and up to four clients, optionally disconnects and reconnects one client, and can restart the server with the same save slot.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\qa\run-dedicated-multiplayer-longrun.ps1 `
  -Port 7861 `
  -RunName dedicated-longrun-runbook `
  -SaveSlot dedicated-longrun-runbook `
  -ClientCount 4 `
  -WaitSeconds 600 `
  -ReconnectClientIndex 2 `
  -DisconnectAfterSeconds 180 `
  -ReconnectAfterSeconds 45 `
  -RestartServerAfterFirstPass `
  -RequireSaveReloadEvidence
```

This validates:

- four separate client player processes connecting to `rootborn-server.exe`;
- server-side player spawn evidence for every client;
- client owner spawn evidence for every client;
- per-player `Student day result` evidence;
- server-authoritative Day 2 transition;
- client world-time sync;
- scheduled disconnect and reconnect evidence;
- same `saveSlot` evidence after server restart;
- fatal log pattern absence.

Logs are written to:

```text
Builds\Logs\dedicated-longrun-runbook
```

For a shorter local smoke of the long-run harness, reduce `-WaitSeconds` and disable restart. This is useful for script syntax and process wiring only; it does not replace the 600-second release/nightly validation.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\qa\run-dedicated-multiplayer-longrun.ps1 `
  -Port 7861 `
  -RunName dedicated-longrun-local `
  -ClientCount 2 `
  -WaitSeconds 125 `
  -ReconnectClientIndex 0
```

## CI Validation Gate

Run the full executable multiplayer gate with the dedicated server requirement:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\ci\run-multiplayer-validation-gate.ps1 `
  -SmokePort 7854 `
  -SmokeRunName multiplayer-ci-dedicated-runbook `
  -SmokeWaitSeconds 125 `
  -RequireDedicatedServer
```

Run the release/nightly long-run gate:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\ci\run-multiplayer-validation-gate.ps1 `
  -SmokePort 7854 `
  -SmokeRunName multiplayer-ci-dedicated-longrun `
  -SmokeWaitSeconds 125 `
  -RequireDedicatedServer `
  -RequireDedicatedLongRun `
  -LongRunClientCount 4 `
  -LongRunWaitSeconds 600 `
  -LongRunReconnect `
  -LongRunRestartServer
```

This gate runs:

- Client build unless `-SkipClientBuild` is set.
- Host + two Client smoke through `Scripts\qa\run-direct-multiplayer-smoke.ps1`.
- Dedicated server build unless `-SkipServerBuild` is set.
- Dedicated server + two Client smoke through `Scripts\qa\run-dedicated-multiplayer-smoke.ps1`.
- Dedicated server long-run through `Scripts\qa\run-dedicated-multiplayer-longrun.ps1` only when `-RequireDedicatedLongRun` is set.

Use `-SkipClientBuild` only when a fresh client build has already succeeded in the same validation pass.

## Troubleshooting

Port already in use:

```powershell
Get-NetTCPConnection -LocalPort 7851 -ErrorAction SilentlyContinue
```

Leftover processes:

```powershell
Get-CimInstance Win32_Process -Filter "name = 'rootborn.exe' or name = 'rootborn-server.exe' or name = 'Unity.exe'" |
  Select-Object ProcessId,Name,CommandLine
```

Missing server artifact:

```powershell
Test-Path Builds\Server\Windows\rootborn-server.exe
```

Missing client artifact:

```powershell
Test-Path Builds\Client\Windows\rootborn.exe
```

If batchmode returns before Unity exits, wait for the Unity process tied to this project before reading the final log.
