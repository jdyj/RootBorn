# Direct Multiplayer Executable Smoke Audit - 2026-05-18

## Scope

This audit closes the previous gap where multiplayer had only been verified through Unity PlayMode / in-editor NGO host-mode simulation. The executable smoke uses the built Windows player as separate OS processes:

- Host process: `rootborn.exe -mode host`
- Client 1 process: `rootborn.exe -mode client -joinIp 127.0.0.1`
- Client 2 process: `rootborn.exe -mode client -joinIp 127.0.0.1`

The run uses opt-in direct validation flags that drive Unity InputSystem keyboard state and emit read-only trace logs. It does not call gameplay domain APIs directly to mutate state.

## Repeatable Command

```powershell
powershell.exe -ExecutionPolicy Bypass -File Scripts\qa\run-direct-multiplayer-smoke.ps1 -Port 7842 -RunName direct-multiplayer-script-20260518 -WaitSeconds 125
```

Result:

```text
Result     : Passed
Executable : C:\Users\jdyj\farmer\Builds\Client\Windows\rootborn.exe
LogDir     : C:\Users\jdyj\farmer\Builds\Logs\direct-multiplayer-script-20260518
Port       : 7842
SaveSlot   : direct-multiplayer-script-20260518
```

CI wrapper command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\ci\run-multiplayer-validation-gate.ps1 -SkipClientBuild -SkipServerBuild -SmokePort 7844 -SmokeRunName direct-multiplayer-ci-gate-20260518 -SmokeWaitSeconds 125
```

Result:

```text
Result                  : Passed
SmokeLogDir             : C:\Users\jdyj\farmer\Builds\Logs\direct-multiplayer-ci-gate-20260518
DedicatedServerRequired : False
```

## Evidence

Log directory:

```text
Builds\Logs\direct-multiplayer-script-20260518
```

Observed process logs:

- `host-player.log`: 11255 bytes
- `client1-player.log`: 9998 bytes
- `client2-player.log`: 9998 bytes

Key evidence:

- Host started on port `7842`.
- Client 1 started and connected to `127.0.0.1:7842`.
- Client 2 started and connected to `127.0.0.1:7842`.
- Host observed network players `client-0`, `client-1`, and `client-2`.
- Client 1 owned `client-1` with `isOwner=True`.
- Client 2 owned `client-2` with `isOwner=True`.
- Host, Client 1, and Client 2 each completed the Study Basics input path and produced a day result for their own player id.
- Server authority day-end policy advanced through `ready=1/3`, `ready=2/3`, and `ready=3/3`.
- Server confirmed `day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart` only after `ready=3/3`.
- Client 1 and Client 2 both received the Day 2 / Tue / Morning / DayStart schedule sync.

Representative lines:

```text
[ROOTBORN] Host started - port=7842 saveSlot=direct-multiplayer-script-20260518
[ROOTBORN] Client started - joinIp=127.0.0.1 port=7842 saveSlot=direct-multiplayer-script-20260518
[ROOTBORN] Network player spawned owner=1 local=1 isOwner=True isServer=False playerId=client-1
[ROOTBORN] Network player spawned owner=2 local=2 isOwner=True isServer=False playerId=client-2
[ROOTBORN] Student day result player=client-0 day=1 activities=1 results=5 endedCurrentDay=True
[ROOTBORN] Student day result player=client-1 day=1 activities=1 results=5 endedCurrentDay=True
[ROOTBORN] Student day result player=client-2 day=1 activities=1 results=5 endedCurrentDay=True
[ROOTBORN] World time day-end ready client=0 ready=3/3 day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayEndReady
[ROOTBORN] World time next day confirmed by server day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart
[ROOTBORN] World time schedule sync previous=DayEndReady current=DayStart day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart
```

The QA script also checks for fatal startup and runtime patterns:

- `StartClient failed`
- `StartHost failed`
- `Unhandled`
- `NullReferenceException`
- `InvalidOperationException`
- `Exception`
- `ERROR`

No fatal pattern was found in the passed run.

## Build Note

The first attempt to rebuild through CLI batchmode was blocked because the Unity Editor already had the same project open:

```text
Multiple Unity instances cannot open the same project.
Project: C:/Users/jdyj/farmer
```

After closing the open Editor, the fresh client build completed:

```text
Builds\Logs\client-build-fresh-20260518.log
Build Finished, Result: Success.
[ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded
```

The refreshed runtime assemblies include:

```text
Builds\Client\Windows\rootborn_Data\Managed\Rootborn.Game.dll    2026-05-18 18:28
Builds\Client\Windows\rootborn_Data\Managed\Rootborn.Network.dll 2026-05-18 18:28
Builds\Client\Windows\rootborn_Data\Managed\Rootborn.UI.dll      2026-05-18 18:28
```

The fresh client build was then revalidated with:

```powershell
powershell.exe -ExecutionPolicy Bypass -File Scripts\qa\run-direct-multiplayer-smoke.ps1 -Port 7843 -RunName direct-multiplayer-fresh-client-20260518 -WaitSeconds 125
```

Result:

```text
Result : Passed
LogDir : C:\Users\jdyj\farmer\Builds\Logs\direct-multiplayer-fresh-client-20260518
```

`Builds\Server\Windows\rootborn-server.exe` is not present in the local workspace, so this run used Host + two Clients rather than a dedicated server executable plus external clients.

A fresh Windows dedicated server build was attempted:

```text
Builds\Logs\server-build-windows-20260518.log
Build Finished, Result: Failure.
[ROOTBORN] Server build -> Builds/Server/Windows/rootborn-server.exe result=Failed
Error building Player: Dedicated Server support for Win is not installed.
```

Installed playback engines under the local Unity editor currently include only:

```text
C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Data\PlaybackEngines\windowsstandalonesupport
```

The new CI wrapper treats this as an explicit local environment block unless `-RequireDedicatedServer` is passed, in which case the missing module fails the gate.

## Outcome

Direct executable multiplayer smoke is passed for Host + two Client processes on the refreshed Windows client build, including identity separation, player-owned activity results, server-authoritative all-ready day transition, and Day 2 synchronization on both clients. Dedicated server executable validation remains blocked by the missing local Unity Windows Dedicated Server support module, and the block is now represented in the CI wrapper instead of being a silent omission.
