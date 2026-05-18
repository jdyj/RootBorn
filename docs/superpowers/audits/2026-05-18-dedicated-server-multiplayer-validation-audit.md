# Dedicated Server Multiplayer Validation Audit - 2026-05-18

## Scope

This audit follows `docs/superpowers/goals/2026-05-18-dedicated-server-multiplayer-validation-goal.md`.

The target was to close the remaining multiplayer gap by validating a real `rootborn-server.exe` dedicated server process with two separate `rootborn.exe` client processes. The local environment still cannot build the Windows dedicated server player because Unity's Windows Dedicated Server support module is not installed, so the result is an explicit environment block, not a completed dedicated-server runtime validation.

## Current State Check

Branch:

```text
town-playable-baseline-recovery...origin/town-playable-baseline-recovery
```

Relevant recent commits:

```text
5feac007f [TEST][DOCS] 멀티플레이 검증 CI 게이트 추가
e51bbd0b9 [TEST][DOCS] 멀티플레이 실행 파일 스모크 검증 추가
8cbc6d1b7 [TEST] PlayMode 멀티플레이 시뮬레이션 게이트 보강
d33cfbafa [DOCS] PlayMode 멀티플레이 게이트 goal 추가
```

Installed Unity playback engines:

```text
C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Data\PlaybackEngines\windowsstandalonesupport
```

Observed state:

- `Builds\Server\Windows\rootborn-server.exe` is not present.
- No `Unity.exe` process was running before the build attempts.
- Existing unrelated dirty/untracked workspace files were left untouched.

## Fresh Server Build Attempt

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "C:\Users\jdyj\farmer" `
  -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64 `
  -logFile "Builds\Logs\server-build-windows-dedicated-verify-20260518.log" `
  -quit
```

Result:

```text
Build Finished, Result: Failure.
[ROOTBORN] Server build -> Builds/Server/Windows/rootborn-server.exe result=Failed
Error building Player: Dedicated Server support for Win is not installed.
```

Failure classification:

- Primary cause: missing local Unity Windows Dedicated Server support module.
- Secondary build log noise: Addressables/SBP reports failure because the player build configuration cannot proceed.
- Artifact result: `Builds\Server\Windows\rootborn-server.exe` was not created.

## Fresh Client Build Recheck

Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "C:\Users\jdyj\farmer" `
  -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 `
  -logFile "Builds\Logs\client-build-dedicated-verify-20260518.log" `
  -quit
```

Result:

```text
Build Finished, Result: Success.
[ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded
```

The Unity launcher executable timestamp did not change, but the build log confirmed success and the player data/managed assemblies remain available under:

```text
Builds\Client\Windows\rootborn.exe
Builds\Client\Windows\rootborn_Data\Managed\Rootborn.Game.dll
Builds\Client\Windows\rootborn_Data\Managed\Rootborn.Network.dll
Builds\Client\Windows\rootborn_Data\Managed\Rootborn.UI.dll
```

## CI Gate With Dedicated Requirement

Command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Scripts\ci\run-multiplayer-validation-gate.ps1 `
  -SkipClientBuild `
  -SmokePort 7852 `
  -SmokeRunName multiplayer-ci-dedicated-verify-20260518 `
  -SmokeWaitSeconds 125 `
  -RequireDedicatedServer
```

Result:

```text
Result     : Passed
Executable : C:\Users\jdyj\farmer\Builds\Client\Windows\rootborn.exe
LogDir     : C:\Users\jdyj\farmer\Builds\Logs\multiplayer-ci-dedicated-verify-20260518
Port       : 7852
SaveSlot   : multiplayer-ci-dedicated-verify-20260518

Dedicated server build is blocked because Unity Windows Dedicated Server support is not installed.
```

Process result:

- Exit code: `1`
- This is expected for `-RequireDedicatedServer` when the local dedicated server module is missing.

The CI gate still ran the existing Host + two Client executable smoke first. That smoke passed before the dedicated server requirement failed. This preserves the current executable multiplayer coverage while making the missing dedicated server module a hard block when explicitly required.

## Host + Client Versus Dedicated Server Coverage

Covered by current executable smoke:

- Host process starts through `rootborn.exe -mode host`.
- Client 1 and Client 2 start as separate `rootborn.exe -mode client` processes.
- Network identities are assigned as `client-0`, `client-1`, and `client-2`.
- Client-owned input and player day results are verified.
- Server-authoritative world time advances only after all connected players are ready.

Not covered because of the environment block:

- `rootborn-server.exe` process startup.
- Dedicated server listen/start log.
- Client connection to a server-only process.
- Dedicated server authority path separate from Host mode.
- `Scripts\qa\run-dedicated-multiplayer-smoke.ps1`, because there is no server executable to run.

## Required Environment Action

Install Unity Windows Dedicated Server support for Unity `6000.3.13f1` through Unity Hub, then rerun:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" `
  -batchmode -nographics `
  -projectPath "C:\Users\jdyj\farmer" `
  -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64 `
  -logFile "Builds\Logs\server-build-windows-dedicated-verify-20260518.log" `
  -quit
```

After `Builds\Server\Windows\rootborn-server.exe` exists, add and run `Scripts\qa\run-dedicated-multiplayer-smoke.ps1`, then wire that smoke into `Scripts\ci\run-multiplayer-validation-gate.ps1` behind `-RequireDedicatedServer`.

## Outcome

Dedicated server runtime validation is environment-blocked, not complete.

The blocked path is properly evidenced:

- Dedicated server build was directly attempted.
- Failure log identifies `Dedicated Server support for Win is not installed`.
- No `rootborn-server.exe` artifact exists.
- Fresh client build was rechecked and succeeded.
- Existing Host + two Client executable smoke still passes.
- CI gate with `-RequireDedicatedServer` fails explicitly when the dedicated server module is missing.
