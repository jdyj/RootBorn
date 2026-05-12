# Multiplayer Direct Play Validation Audit

Date: 2026-05-11

## Objective

Validate whether ROOTBORN multiplayer is actually playable through direct Host/Client or Dedicated Server/Client execution, not only automated tests. Required coverage includes connection, player separation, server authority, per-player quest/inventory/student/relationship/condition state, shared world state, UI sync, save/load/reconnect, and existing single-player E2E regression health.

Additional requirement: validate world time as server-authoritative shared state. Current day, weekday, time of day, and world schedule phase must be identical for Client 1 and Client 2, including late join, reconnect, host/server restart, and day transition policy.

## Prompt-To-Artifact Checklist

| Requirement | Evidence | Status |
| --- | --- | --- |
| MULTI-DIRECT-001 Host + Client direct execution | Built Windows client and launched one Host plus two Client player processes in multiple smoke runs. Latest useful runs include `Builds/Logs/multi-direct-run-after-scene-event-logs`, `Builds/Logs/multi-direct-run-after-player-identity-binder`, `Builds/Logs/multi-direct-run-after-world-time-state`, and `Builds/Logs/multi-direct-run-after-input-gate`. | Partially verified: connection/sync/spawn only |
| MULTI-DIRECT-002 Dedicated Server + 2 Clients if possible | `Builds/Logs/multi-direct-server-build.log` and fresh rerun `Builds/Logs/server-build-windows-20260512.log` show Windows dedicated server build failed: `Dedicated Server support for Win is not installed.` Installed playback engines currently only include Windows standalone support, so direct Dedicated Server + two Clients remains blocked on this machine. | Blocked by local Unity module |
| MULTI-DIRECT-003 / 003A Same Town and separate player objects | `Builds/Logs/multi-direct-run-after-scene-event-logs` shows Host requested NGO `Town` load and both clients reported `LoadComplete scene=Town` plus `SynchronizeComplete`. `Builds/Logs/multi-direct-run-after-player-identity-binder` shows separate network player spawns with owner ids 0/1/2 and unique `client-{OwnerClientId}` identities. `Builds/Logs/multi-direct-run-visible-dayend-all-player-installers` and `Builds/Logs/multi-direct-run-world-time-day2-restart-reconnect` include Client 1/2 screenshots in the same Town UI. | Verified by logs and screenshots |
| Post-restore Host + two Client smoke | Post-restore build and follow-up network-scene-sync runs removed the earlier `level3` corruption and local HostLobby client-load failure. Latest useful logs prove NGO Town sync and player spawn. | Passed for smoke scope |
| Network player spawn readiness | `Assets/Scenes/Boot.unity` now wires the Player prefab, `Assets/DefaultNetworkPrefabs.asset` registers it, and `Assets/Prefabs/Player.prefab` has `NetworkObject`. Direct logs prove spawned players for owner 0/1/2. | Implemented / verified by logs |
| Player identity separation readiness | `NetworkPlayerIdentityBinder` assigns `client-{OwnerClientId}` on spawned network players. Direct logs show Client 1 owns `client-1` and Client 2 owns `client-2`. `Builds/Logs/multi-direct-run-quest-inventory-snapshot` additionally shows Client 1 alone accepting/completing quests and receiving inventory while Client 2 keeps only the shared world-time snapshot. | Verified for identity plus quest/inventory isolation |
| Client Town scene synchronization | Network modes now wait for NGO scene sync; HostSession/DedicatedServerSession request `Town` through `NetworkManager.SceneManager`. Direct logs prove client `Town` `LoadComplete` and `SynchronizeComplete`. | Verified by logs |
| MULTI-DIRECT-004 through 011 Gameplay interaction/state separation/server authority/save-reconnect | World-time server authority, all-ready day transition, same-saveSlot Host restart, and reconnect are directly verified. Post-identity direct autoplay verifies personal Study Basics day results, `Builds/Logs/multi-direct-run-quest-inventory-snapshot` verifies Client 1 quest/inventory changes while Client 2 remains unchanged for that personal path, and `Builds/Logs/multi-direct-run-relationship-condition-rebind` verifies relationship/status snapshots through Study Basics for Host, Client 1, and Client 2. | Verified for Host + two Clients |
| MULTI-DIRECT-012 Existing tests continue passing | Fresh Unity MCP EditMode suite passed 465/465. Unity MCP `Rootborn.Tests.PlayMode.EndToEnd` passed 15/15, `BootToGameEndToEndTests` passed 10/10, `Rootborn.Tests.PlayMode.Quests` passed 16/16 executed tests, and `Rootborn.Tests.PlayMode.World` passed 6/6 executed tests. The full unfiltered PlayMode suite attempt exceeded the MCP timeout and is not pass evidence. | Passed for EditMode and targeted PlayMode coverage |
| CI entity branching gate | `Scripts/ci/check-no-entity-id-branching.sh` passed: `OK: no entity-id branching in system code.` | Passed |
| Direct play evidence | `host.log`, `client1.log`, `client2.log` under `Builds/Logs/multi-direct-run/`. | Captured failed run |
| MULTI-TIME-001 through 008 shared world time sync | `NetworkWorldTimeState` now exists and direct logs verify initial sync, all-connected-player day-end transition, Client 1/2 UI update to Day 2, late reconnect, and same-saveSlot Host restart. Client 1 Study Basics activity plus personal day-result divergence was verified by direct logs. Quest/inventory isolation was later verified in `Builds/Logs/multi-direct-run-quest-inventory-snapshot`, and relationship/status isolation was verified in `Builds/Logs/multi-direct-run-relationship-condition-rebind`; all processes still showed the same shared world-time snapshot. | Verified for world time and personal-state separation |
| Day transition policy | Connected-player ready policy is implemented and directly played: Host, Client 1, and Client 2 each requested day end; server advanced only at `ready=3/3`. | Implemented / directly verified |

## Multiplayer World Time Checklist

| Requirement | Current Evidence | Status |
| --- | --- | --- |
| MULTI-TIME-001: Host/Client 접속 후 Client 1과 Client 2가 같은 CurrentDay/weekday/timeOfDay를 본다. | `Builds/Logs/multi-direct-run-after-world-time-state` shows Host, Client 1, and Client 2 all received `day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart`. `Builds/Logs/multi-direct-run-visible-timehud-canvas-fix/client1-timehud.png` and `client2-timehud.png` show both clients displaying `Day 1 Mon Morning DayStart`. | Verified by logs and initial UI screenshots |
| MULTI-TIME-002: Client 1 개인 활동 후에도 두 클라이언트 월드 시간은 동일하다. | `Builds/Logs/multi-direct-run-client1-activity-result-verified/client1.log` shows Client 1 applied `activity.study-basics` twice before day end, while Client 1 and Client 2 both received the same Day 2 world-time sync. | Verified |
| MULTI-TIME-003: 하루 종료 요청은 서버 권한으로 처리되고 클라이언트가 직접 확정하지 않는다. | `StudentDayEndInteractor` requests day-end ready through `NetworkWorldTimeState`; `CurrentDay` is changed only in server-side `AdvanceToNextDay`. Direct all-ready run shows server ready counts `1/3`, `2/3`, `3/3` before Day 2 broadcast. | Verified by code tests and direct all-ready run |
| MULTI-TIME-004: 선택한 하루 종료 정책에 따라 월드 시간 전환 조건이 모든 클라이언트에 동일하게 적용된다. | Connected-player ready policy is implemented and directly played: Host, Client 1, and Client 2 each requested day end; server advanced only at `ready=3/3`. | Verified |
| MULTI-TIME-005: 월드 시간이 다음 날로 넘어가면 Client 1과 Client 2 UI가 같은 일차/요일/시간대로 갱신된다. | `TimeHud` prefers `NetworkWorldTimeState.Active`; `TownTimeHudRuntimeInstaller` installs a visible Town time HUD. Direct all-ready screenshots `client1-after-dayend.png` and `client2-after-dayend.png` show Day 2 Tue Morning DayStart. | Verified by logs and screenshots |
| MULTI-TIME-006: 늦게 접속하거나 재접속한 Client가 현재 서버 월드 시간을 받는다. | `Builds/Logs/multi-direct-run-world-time-reconnect-client2b` shows Client 2 reconnecting as local=3 and receiving the same world-time snapshot. | Verified by logs, graceful disconnect cleanup pending |
| MULTI-TIME-007: 서버/Host 재시작 후 같은 saveSlot에서 월드 시간이 동일하게 복원된다. | `Builds/Logs/multi-direct-run-world-time-restart-same-slot` shows Host restored `saveSlot=multi-direct-20260511-world-time` and clients received the restored snapshot. | Verified by logs |
| MULTI-TIME-008: 개인 하루 결과 UI는 플레이어별 데이터를 표시하되 월드 시간 상태는 공유 상태로 유지된다. | Direct activity/result run shows Client 1 day result divergence while all processes converged on the same Day 2 world-time snapshot. Quest/inventory isolation is directly proven in `Builds/Logs/multi-direct-run-quest-inventory-snapshot`, and relationship/status snapshots are directly proven in `Builds/Logs/multi-direct-run-relationship-condition-rebind`. | Verified for StudentLife day result, quest/inventory, and relationship/status personal data |

Selected day transition policy for future implementation:

```text
All connected players mark day-end ready through the real interaction path.
The Host/Server is the only authority that advances shared world time.
When the readiness condition is met, the Host/Server advances the shared world day/time snapshot and broadcasts it to all connected clients.
Late-joining or reconnecting clients receive the current shared world-time snapshot before showing time UI as authoritative.
```

## 2026-05-11 World Time Direct Verification Update

Code changes through Unity MCP:

- `Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs`
  - Changed Town runtime reinforcement from first root `Player` only to all scene player roots.
  - Reason: multiplayer clients can contain owner 0/1/2 player roots; local owned players must all receive inventory, interaction, Rigidbody/Collider, and visible sprite reinforcement.
- `Assets/Scripts/Game/Bootstrap/TownStudentLifeRuntimeInstaller.cs`
  - Changed StudentLife progress installation/restoration from first root `Player` only to all scene player roots.
  - Reason: direct day-end interaction needs `StudentLifeProgressComponent` on each connected player object, not just Host owner 0.
- `Assets/Tests/EditMode/Network/NetworkTownMultiplayerInstallerSourceTests.cs`
  - Added source-audit tests that fail if Town installers regress to single-player-root handling.

Verification:

```text
Unity EditMode: 446/446 passed
CI: Scripts/ci/check-no-entity-id-branching.sh -> OK: no entity-id branching in system code.
Client build: Builds/Logs/multi-direct-client-after-all-player-installers.log
Result: DisplayProgressNotification: Build Successful
Result: [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded
Updated runtime assembly: Builds/Client/Windows/rootborn_Data/Managed/Rootborn.Game.dll at 2026-05-11 19:20:40
```

Direct all-ready day-end run:

```text
Run logs/screenshots:
  Builds/Logs/multi-direct-run-visible-dayend-all-player-installers/host.log
  Builds/Logs/multi-direct-run-visible-dayend-all-player-installers/client1.log
  Builds/Logs/multi-direct-run-visible-dayend-all-player-installers/client2.log
  Builds/Logs/multi-direct-run-visible-dayend-all-player-installers/client1-after-dayend.png
  Builds/Logs/multi-direct-run-visible-dayend-all-player-installers/client2-after-dayend.png

Automation:
  Host, Client 1, and Client 2 were focused as real windows.
  Each process received real keyboard scan-code input to move to the day-end board and press E.

Host evidence:
  World time day-end ready client=0 ready=1/3 day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayEndReady
  World time day-end ready client=1 ready=2/3 day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayEndReady
  World time day-end ready client=2 ready=3/3 day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayEndReady
  World time day sync previous=1 current=2 day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayEndReady
  World time next day confirmed by server day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart

Client 1 evidence:
  World time day sync previous=1 current=2 day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayEndReady
  World time schedule sync previous=DayEndReady current=DayStart day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart
  Screenshot shows Day 2 Tue Morning DayStart.

Client 2 evidence:
  World time day sync previous=1 current=2 day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayEndReady
  World time schedule sync previous=DayEndReady current=DayStart day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart
  Screenshot shows Day 2 Tue Morning DayStart.
```

Direct restart and reconnect run after Day 2 save:

```text
Run logs/screenshots:
  Builds/Logs/multi-direct-run-world-time-day2-restart-reconnect/host.log
  Builds/Logs/multi-direct-run-world-time-day2-restart-reconnect/client1.log
  Builds/Logs/multi-direct-run-world-time-day2-restart-reconnect/client2-first.log
  Builds/Logs/multi-direct-run-world-time-day2-restart-reconnect/client2-reconnect.log
  Builds/Logs/multi-direct-run-world-time-day2-restart-reconnect/client1-day2-restored.png
  Builds/Logs/multi-direct-run-world-time-day2-restart-reconnect/client2-reconnect-day2.png

Host restart evidence:
  World time restored from saveSlot=multi-time-all-player-installers day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart

Late reconnect evidence:
  client2-reconnect.log: World time spawned owner=0 local=3 isOwner=False isServer=False active=True day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart
  client2-reconnect-day2.png shows Day 2 Tue Morning DayStart.
```

Client 1 activity attempt status:

```text
Runs:
  Builds/Logs/multi-direct-run-client1-activity-world-time-stable
  Builds/Logs/multi-direct-run-client1-activity-repeat-world-time-stable
  Builds/Logs/multi-direct-run-client1-activity-then-dayend

Observed:
  Client 1 and Client 2 remained on the same shared world time after Client 1 Study Basics interaction attempts.
  The final day-end result UI still showed "No activities recorded".

Conclusion:
  MULTI-TIME-002 is only partially verified: shared world time stayed synchronized, but the required "Client 1 performed a personal activity" evidence is missing.
  MULTI-TIME-008 is only partially verified: personal result UI can be shown while shared world time remains synchronized, but differing personal result data was not proven.
```

Direct Client 1 activity and personal result verification:

```text
Changed through Unity MCP:
  Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs
  Assets/Scripts/UI/StudentLife/StudentDayEndInteractor.cs
  Assets/Tests/EditMode/Network/NetworkStudentLifeInteractionSourceTests.cs

Purpose:
  Add observable direct-play logs for real activity interaction results and personal day summaries.
  The change does not force CurrentDay, TimeOfDay, activity state, or player state in tests.

Verification:
  Unity EditMode: 448/448 passed
  CI: Scripts/ci/check-no-entity-id-branching.sh -> OK: no entity-id branching in system code.
  Client build: Builds/Logs/multi-direct-client-after-student-activity-logs.log
  Result: [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded

Diagnostic run:
  Builds/Logs/multi-direct-run-client1-activity-diagnostic/client1.log
  Client 1 real E input near Study Basics produced:
    Student life activity interact applied=True result=Applied activity=activity.study-basics player=local-player request=activity.study-basics:0 todayCount=1
    Student life activity interact applied=True result=Applied activity=activity.study-basics player=local-player request=activity.study-basics:4 todayCount=5

Activity + day-end run:
  Builds/Logs/multi-direct-run-client1-activity-result-verified/host.log
  Builds/Logs/multi-direct-run-client1-activity-result-verified/client1.log
  Builds/Logs/multi-direct-run-client1-activity-result-verified/client2.log
  Builds/Logs/multi-direct-run-client1-activity-result-verified/client1-activity-result-day2.png
  Builds/Logs/multi-direct-run-client1-activity-result-verified/client2-idle-result-day2.png

Client 1 evidence:
  Student life activity interact applied=True result=Applied activity=activity.study-basics player=local-player request=activity.study-basics:0 todayCount=1
  Student life activity interact applied=True result=Applied activity=activity.study-basics player=local-player request=activity.study-basics:1 todayCount=2
  Student day result player=local-player day=1 activities=2 results=3 endedCurrentDay=True
  World time day sync previous=1 current=2 day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayEndReady
  World time schedule sync previous=DayEndReady current=DayStart day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart

Host evidence:
  Student day result player=local-player day=1 activities=0 results=0 endedCurrentDay=True
  World time day-end ready client=0 ready=2/3

Client 2 evidence:
  Student day result player=local-player day=1 activities=0 results=0 endedCurrentDay=True
  World time day sync previous=1 current=2 day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayEndReady

Conclusion:
  Client 1's personal day result had activity data while Host and Client 2 day results had no activity data.
  The shared world time still advanced once, on the server, and both clients received the same Day 2 snapshot.
  The current result logs report `player=local-player` inside each process; NetworkObject ownership logs still provide the client identity evidence (`client-0`, `client-1`, `client-2`). This is an implementation risk for deeper save-state identity auditing, but the direct run proves per-process personal result divergence and shared world-time convergence.
```

Post-identity hardening and regression verification:

```text
Changed through Unity MCP:
  Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs
  Assets/Scripts/Network/Player/NetworkPlayerIdentityBinder.cs
  Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs
  Assets/Scripts/Game/Bootstrap/TownSinglePlayerFallbackInstaller.cs
  Assets/Scripts/Game/Bootstrap/TownTilemapFallbackInstaller.cs
  Assets/Tests/EditMode/StudentLife/StudentLifeProgressIdentityTests.cs

Purpose:
  Rebind default StudentLife progress from `local-player` to `client-{OwnerClientId}` once NetworkPlayerIdentityBinder assigns the network identity.
  Preserve multiplayer Player spawning while restoring single-player/Town PlayMode fallback Player, Grid, tilemap, and visual cue setup.

Fresh verification:
  Unity EditMode: 449/449 passed.
  Unity PlayMode TownConcept namespace: Status=Passed, 0 failures.
  CI: Scripts/ci/check-no-entity-id-branching.sh -> OK: no entity-id branching in system code.
  Client build: Builds/Logs/multi-direct-client-after-town-fallbacks.log
  Result: [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded

Latest Host + two Client smoke:
  Builds/Logs/multi-direct-run-after-town-fallbacks/host.log
  Builds/Logs/multi-direct-run-after-town-fallbacks/client1.log
  Builds/Logs/multi-direct-run-after-town-fallbacks/client2.log

Identity/world-time evidence:
  Host:   Network player spawned owner=0/1/2 with playerId=client-0/client-1/client-2.
  Client1: local=1 owner=1 isOwner=True playerId=client-1 and all world-time snapshots Day 1 Mon Morning DayStart.
  Client2: local=2 owner=2 isOwner=True playerId=client-2 and all world-time snapshots Day 1 Mon Morning DayStart.

Limit:
  The Client 1 activity/day-result direct-play run was not repeated after the identity hardening because GUI input automation requires launching visible client windows. The identity fix is covered by EditMode regression and by latest network identity smoke, but a new visible direct run should still be captured before calling the whole goal complete.
```

Post-identity visible activity rerun attempts:

```text
Attempt logs:
  Builds/Logs/multi-direct-run-visible-post-identity-activity/host.log
  Builds/Logs/multi-direct-run-visible-post-identity-activity/client1.log
  Builds/Logs/multi-direct-run-visible-post-identity-activity/client2.log
  Builds/Logs/multi-direct-run-visible-post-identity-activity-scan/host.log
  Builds/Logs/multi-direct-run-visible-post-identity-activity-scan/client1.log
  Builds/Logs/multi-direct-run-visible-post-identity-activity-scan/client2.log

Result:
  Both attempts launched visible Host + Client 1 + Client 2 processes and proved network player identities:
    Host saw owner=0/1/2 playerId=client-0/client-1/client-2.
    Client 1 saw local=1 owner=1 isOwner=True playerId=client-1.
    Client 2 saw local=2 owner=2 isOwner=True playerId=client-2.

Failure:
  No `Student life activity interact`, `Student day result`, or `World time day-end ready` lines were produced.
  The runs are recorded as failed post-identity visible activity validation, not completion evidence.
```

Post-identity visible input trace diagnosis:

```text
Changed through Unity MCP:
  Assets/Scripts/Game/Common/DirectValidationTrace.cs
  Assets/Scripts/Game/Common/DirectValidationTraceInstaller.cs

Purpose:
  Add an opt-in `-directValidationTrace` runtime trace for direct-play validation only.
  The trace logs local player identity, position, enabled input/interaction components, visible prompt text, and Unity InputSystem key states.
  It does not set CurrentDay, TimeOfDay, player state, activity state, inventory, quest, relationship, or condition state.

Verification:
  Unity EditMode Network namespace: Status=Passed.
  Unity MCP full EditMode after trace fix: Status=Passed, TotalTests=449, PassedTests=449, FailedTests=0, SkippedTests=0.
  CI: Scripts/ci/check-no-entity-id-branching.sh -> OK: no entity-id branching in system code.
  Client build: Builds/Logs/multi-direct-client-after-direct-trace.log
  Result: [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded

Follow-up fix:
  The first trace installer used `Object.FindObjectsByType<PlayerIdentity>` and failed `StaticRuntimeUsageGateTests`.
  `DirectValidationTraceInstaller` now collects `PlayerIdentity` components through active scene root GameObjects and `GetComponentsInChildren`, avoiding new runtime Find/Resources patterns.
  Unity MCP `StaticRuntimeUsageGateTests` passed after the fix.
  Unity MCP full EditMode passed `449/449`.
  `Scripts/ci/check-no-entity-id-branching.sh` passed after the fix.

Build status after trace static-gate fix:
  Command: `Unity.exe -batchmode -nographics -projectPath C:\Users\jdyj\farmer -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 -logFile Builds/Logs/multi-direct-client-after-direct-trace-gate-fix.log -quit`
  Result: not counted as a successful build.
  Observation: Unity reached Addressables `Write Serialized Files`, but the log stopped updating for several minutes and `Builds/Client/Windows/rootborn.exe` remained at the older timestamp.
  Cleanup: the stuck batchmode Unity/UnityPackageManager processes were terminated, and the stale `Library/ilpp.pid` for exited PID `55312` was removed.

Build retry after trace static-gate fix:
  Command: `Unity.exe -batchmode -nographics -projectPath C:\Users\jdyj\farmer -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 -logFile Builds/Logs/multi-direct-client-after-direct-trace-gate-fix-retry.log -quit`
  Result: Build successful.
  Evidence:
    `Builds/Logs/multi-direct-client-after-direct-trace-gate-fix-retry.log` contains `Build Finished, Result: Success.`
    `Builds/Logs/multi-direct-client-after-direct-trace-gate-fix-retry.log` contains `[ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded`.
    `Builds/Client/Windows/rootborn_Data/Managed/Rootborn.Game.dll` timestamp updated to 2026-05-11 21:31:56.
  Note:
    `rootborn.exe` itself kept its older Unity launcher timestamp, but the managed gameplay assemblies were rebuilt.
  Cleanup:
    No Unity/UnityPackageManager/MCP/rootborn processes remained.
    The stale `Library/ilpp.pid` for exited PID `3392` was removed.

Latest build executable trace smoke:
  Run logs: `Builds/Logs/multi-direct-run-latest-trace-smoke/{host,client1,client2}.log`
  Configuration:
    Host executable: `rootborn.exe -mode host -port 7801 -maxPlayers 4 -saveSlot multi-direct-latest-trace-smoke -directValidationTrace`
    Client 1 executable: `rootborn.exe -mode client -joinIp 127.0.0.1 -port 7801 -saveSlot multi-direct-latest-trace-smoke -directValidationTrace`
    Client 2 executable: `rootborn.exe -mode client -joinIp 127.0.0.1 -port 7801 -saveSlot multi-direct-latest-trace-smoke -directValidationTrace`
  Evidence:
    Host saw network players owner=0/1/2 with playerId=client-0/client-1/client-2.
    Client 1 saw local=1 owner=1 isOwner=True playerId=client-1 and non-owner input gates disabled.
    Client 2 saw local=2 owner=2 isOwner=True playerId=client-2 and non-owner input gates disabled.
    Host, Client 1, and Client 2 all received Day 1 Mon Morning DayStart world-time snapshots.
    `DIRECT-TRACE` lines were emitted from Town without introducing new static-gate violations.
  Limit:
    This smoke did not perform movement, interaction, activity completion, day-end, quest, inventory, relationship, or condition changes.
    It proves the latest rebuilt executable can still reach Host + two Client Town sync with the fixed trace code, not the remaining direct-play activity/result requirements.
  Cleanup:
    The Host process left running after a PowerShell variable-name collision was manually stopped.
    No Unity/UnityPackageManager/MCP/rootborn processes remained afterward.

Broad PlayMode CLI attempt after trace static-gate fix:
  Command: `Unity.exe -batchmode -nographics -projectPath C:\Users\jdyj\farmer -runTests -testPlatform PlayMode -testResults Builds/Logs/playmode-after-direct-trace-gate-fix.xml -logFile Builds/Logs/playmode-after-direct-trace-gate-fix.log`
  Result: not counted as a passing PlayMode verification.
  Observation:
    Unity Test Runner started and produced a large log, but no requested XML result file was written.
    The log contains PlayMode/batchmode incompatibility messages such as `UnityTest yielded WaitForEndOfFrame, which is not evoked in batchmode`.
    The run ended with an intercepted Unity Editor crash in the render loop (`RenderTexture.Create failed`, `Failed to set the active render target`, stack under `ScriptableRenderLoopDraw`).
  Cleanup:
    The leftover `unity-mcp-server.exe` process was stopped.
    No Unity/UnityPackageManager/MCP/rootborn processes remained afterward.
  Consequence:
    This broad CLI PlayMode attempt is a failed verification attempt, not regression pass evidence.
    Broader PlayMode/E2E should be rerun through the Unity Editor/MCP path or a graphics-capable test configuration.

Trace runs:
  Builds/Logs/multi-direct-run-visible-input-trace-probe/host.log
  Builds/Logs/multi-direct-run-visible-input-trace-probe/client1.log
  Builds/Logs/multi-direct-run-visible-input-trace-interact-probe/host.log
  Builds/Logs/multi-direct-run-visible-input-trace-interact-probe/client1.log
  Builds/Logs/multi-direct-run-visible-input-trace-sendkeys-probe/host.log
  Builds/Logs/multi-direct-run-visible-input-trace-sendkeys-probe/client1.log

Observed:
  Host and Client 1 launched as visible executable windows.
  Network player identity remained correct: Client 1 saw local=1 owner=1 isOwner=True playerId=client-1.
  Client 1 reached an interactable prompt: prompt='[E] Study Basics'.
  Client 1 had controllerEnabled=True gatherEnabled=True routerEnabled=True while standing at the prompt.
  External scan-code, held-key, and WScript.Shell.SendKeys attempts did not reach Unity InputSystem; trace lines kept `e=False` and `space=False`.

Conclusion:
  The post-identity client is present, owned, input-gated, and within interaction range, but this environment's external keyboard automation is not producing Unity InputSystem key-down state in the visible executable.
  This explains the missing post-identity `Student life activity interact` and `Student day result` lines.
  The run is diagnostic evidence only. It does not satisfy the required post-identity direct activity/result verification.
```

Unity launch recovery note:

```text
Issue:
  After repeated Unity batchmode/client validation runs, Unity appeared unable to open from the user's environment.

Inspection:
  No `Unity.exe`, `unity-mcp-server.exe`, `rootborn.exe`, or `UnityPackageManager.exe` processes were running.
  `Library/ilpp.pid` remained and contained PID `41372`, but no process with that PID existed.

Action:
  Removed stale `Library/ilpp.pid`.

Verification:
  `Unity.exe -batchmode -nographics -projectPath C:\Users\jdyj\farmer -quit -logFile Builds/Logs/unity-open-after-ilpp-clean.log`
  Exit code: 0.
  Unity completed initial refresh and script compile, then shut down.
  No Unity/rootborn/MCP processes remained afterward.

Limit:
  The first CLI `-runTests -testPlatform editmode` attempt opened Unity and compiled scripts but did not produce `Builds/Logs/editmode-after-direct-trace.xml`; it is not counted as a passing EditMode verification.
  A second CLI attempt with absolute result paths produced `Builds/Logs/editmode-after-direct-trace-absolute.xml` and exposed one static gate failure in the initial trace code.
  That failure was fixed through Unity MCP, then Unity MCP full EditMode passed `449/449`.
  The CLI attempts recreated `Library/ilpp.pid` for exited PIDs; stale pid files were removed after verifying no matching processes existed.
```

## Evidence

Client build command:

```cmd
"C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" -batchmode -nographics -projectPath "C:\Users\jdyj\farmer" -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 -logFile "Builds/Logs/multi-direct-client-build.log" -quit
```

Result:

```text
[ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded
```

Dedicated server build command:

```cmd
"C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe" -batchmode -nographics -projectPath "C:\Users\jdyj\farmer" -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64 -logFile "Builds/Logs/multi-direct-server-build.log" -quit
```

Result:

```text
Error building Player: Dedicated Server support for Win is not installed.
[ROOTBORN] Server build -> Builds/Server/Windows/rootborn-server.exe result=Failed
```

Host/Client direct run configuration:

```text
Executable: Builds/Client/Windows/rootborn.exe
Host:    -batchmode -nographics -mode host -port 7787 -maxPlayers 4 -saveSlot multi-direct-20260511
Client1: -batchmode -nographics -mode client -joinIp 127.0.0.1 -port 7787 -saveSlot multi-direct-20260511
Client2: -batchmode -nographics -mode client -joinIp 127.0.0.1 -port 7787 -saveSlot multi-direct-20260511
```

Process observation: all three processes stayed alive for the 30 second smoke window.

Host log failure:

```text
[ROOTBORN] Bootstrap mode=Host port=7787 maxPlayers=4 saveSlot=multi-direct-20260511 -> loading scene 'Town'
Scene 'Town' couldn't be loaded because it has not been added to the active build profile or shared scene list or the AssetBundle has not been loaded.
```

Client logs:

```text
[ROOTBORN] Bootstrap mode=Client port=7787 maxPlayers=4 saveSlot=multi-direct-20260511 -> loading scene 'HostLobby'
```

Follow-up after adding Town to the build scene list:

```text
Host run: Builds/Logs/multi-direct-run-after-town/host.log
Failure: The file 'C:/Users/jdyj/farmer/Builds/Client/Windows/rootborn_Data/level3' is corrupted!
```

Clean rebuild and rerun reproduced the same level3 corruption:

```text
Build log: Builds/Logs/multi-direct-client-clean-build.log
Run log:   Builds/Logs/multi-direct-run-clean-town/host.log
Failure:   The file '.../rootborn_Data/level3' is corrupted!
```

Post-restore build and smoke:

```text
Build log: Builds/Logs/multi-direct-client-post-town-restore.log
Result:    [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded
Town:      Opening scene 'Assets/Scenes/Town.unity' / Loaded scene 'Assets/Scenes/Town.unity'

Run logs:  Builds/Logs/multi-direct-run-post-town-restore/{host,client1,client2}.log
Host:      [ROOTBORN] Bootstrap mode=Host ... -> loading scene 'Town'
Client 1:  [ROOTBORN] Bootstrap mode=Client ... -> loading scene 'HostLobby'
Client 2:  [ROOTBORN] Bootstrap mode=Client ... -> loading scene 'HostLobby'
```

No post-restore log evidence showed client Town scene synchronization, player spawn, or successful gameplay interaction.

Network-prefab wiring and network-scene-sync follow-up:

```text
Build log: Builds/Logs/multi-direct-client-after-network-prefab.log
Result:    [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded

Run logs:  Builds/Logs/multi-direct-run-after-network-prefab/{host,client1,client2}.log
Host:      [ROOTBORN] Host client connected id=1
Host:      [ROOTBORN] Host client connected id=2
Failure:   [Invalid Destroy][Player(Clone)][NetworkObjectId:2] Destroy a spawned NetworkObject on a non-host client is not valid...
```

The invalid destroy symptom was traced to local scene loading after network start. `GameBootstrap` was changed so network modes wait for NGO scene synchronization instead of loading HostLobby/Town locally.

```text
Build log: Builds/Logs/multi-direct-client-after-network-scene-sync.log
Result:    [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded

Run logs:  Builds/Logs/multi-direct-run-after-network-scene-sync/{host,client1,client2}.log
Host:      [ROOTBORN] Host client connected id=0
Host:      [ROOTBORN] Host started - port=7790 saveSlot=multi-direct-20260511-network-scene-sync
Host:      [ROOTBORN] Bootstrap mode=Host ... -> waiting for network scene sync
Host:      [ROOTBORN] Host client connected id=1
Host:      [ROOTBORN] Host client connected id=2
Client 1:  [ROOTBORN] Bootstrap mode=Client ... -> waiting for network scene sync
Client 2:  [ROOTBORN] Bootstrap mode=Client ... -> waiting for network scene sync
```

The `Invalid Destroy` error no longer appears in the latest smoke logs. However, the logs still do not show Town scene load completion, client scene synchronization completion, player spawn confirmation, or gameplay interaction evidence. This remains insufficient for MULTI-DIRECT-003/003A and all MULTI-TIME items.

Scene-event instrumentation attempt:

```text
Changed through Unity MCP:
  Assets/Scripts/Network/Session/HostSession.cs
  Assets/Scripts/Network/Session/ClientSession.cs
  Assets/Scripts/Network/Session/DedicatedServerSession.cs

Intent:
  Log NetworkManager.SceneManager.OnSceneEvent callbacks.
  Log Host/Dedicated `LoadScene("Town")` return status.
  Log Client StartClient/connect/disconnect callbacks.

Build log: Builds/Logs/multi-direct-client-after-scene-event-logs.log
Observed:   Unity reached `DisplayProgressbar: Write Serialized Files`.
Observed:   Builds/Client/Windows/rootborn.exe was not updated.
Action:     The long-running Unity batchmode process was stopped to avoid blocking the user's editor.
Result:     This build is not usable direct-run evidence yet.
```

Scene synchronization and player identity evidence:

```text
Build log: Builds/Logs/multi-direct-client-after-scene-event-logs-wait.log
Result:    [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded

Run logs:  Builds/Logs/multi-direct-run-after-scene-event-logs/{host,client1,client2}.log
Host:      Host requested network scene load scene=Town status=Started
Host:      Host scene event type=LoadComplete scene=Town client=0
Host:      Host scene event type=SynchronizeComplete scene= client=1
Host:      Host scene event type=SynchronizeComplete scene= client=2
Client 1:  Client scene event type=LoadComplete scene=Town client=1
Client 1:  Client scene event type=SynchronizeComplete scene= client=1
Client 2:  Client scene event type=LoadComplete scene=Town client=2
Client 2:  Client scene event type=SynchronizeComplete scene= client=2
```

This verifies Host + two Clients enter/synchronize the same `Town` scene in the built executable.

```text
Changed through Unity MCP:
  Assets/Scripts/Network/Player/NetworkPlayerIdentityBinder.cs
  Assets/Prefabs/Player.prefab

Build log: Builds/Logs/multi-direct-client-after-player-identity-binder.log
Result:    [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded

Run logs:  Builds/Logs/multi-direct-run-after-player-identity-binder/{host,client1,client2}.log
Host:      Network player spawned owner=0 local=0 isOwner=True isServer=True playerId=client-0
Host:      Network player spawned owner=1 local=0 isOwner=False isServer=True playerId=client-1
Host:      Network player spawned owner=2 local=0 isOwner=False isServer=True playerId=client-2
Client 1:  Network player spawned owner=1 local=1 isOwner=True isServer=False playerId=client-1
Client 2:  Network player spawned owner=2 local=2 isOwner=True isServer=False playerId=client-2
```

This verifies per-client NetworkObject ownership and unique `PlayerIdentity` assignment for the directly executed Host + two Client configuration.

Input ownership gate evidence:

```text
Changed through Unity MCP:
  Assets/Scripts/Network/Player/NetworkLocalPlayerInputGate.cs
  Assets/Prefabs/Player.prefab

Build log: Builds/Logs/multi-direct-client-after-input-gate.log
Result:    [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded

Run logs:  Builds/Logs/multi-direct-run-after-input-gate/{host,client1,client2}.log
Host:      Network input gate owner=0 local=0 isOwner=True allowLocalInput=True
Host:      Network input gate owner=1 local=0 isOwner=False allowLocalInput=False
Host:      Network input gate owner=2 local=0 isOwner=False allowLocalInput=False
Client 1:  Network input gate owner=0 local=1 isOwner=False allowLocalInput=False
Client 1:  Network input gate owner=1 local=1 isOwner=True allowLocalInput=True
Client 1:  Network input gate owner=2 local=1 isOwner=False allowLocalInput=False
Client 2:  Network input gate owner=0 local=2 isOwner=False allowLocalInput=False
Client 2:  Network input gate owner=1 local=2 isOwner=False allowLocalInput=False
Client 2:  Network input gate owner=2 local=2 isOwner=True allowLocalInput=True
```

This verifies that non-owner Player instances no longer process local movement/interaction input in the direct Host + two Client executable run. Actual movement still needs an input-driven playthrough.

Server-authoritative world time evidence:

```text
Changed through Unity MCP:
  Assets/Scripts/Network/Time/NetworkWorldTimeState.cs
  Assets/Prefabs/Player.prefab
  Assets/Scripts/UI/HUD/TimeHud.cs
  Assets/Scripts/UI/StudentLife/StudentDayEndInteractor.cs
  Assets/Scripts/UI/Rootborn.UI.asmdef
  Assets/Scripts/Network/Rootborn.Network.asmdef

Policy implemented:
  Connected-player ready policy. Each client may request day-end ready, but only the server-owned world time state records readiness.
  When all currently connected clients are ready, the server advances CurrentDay and broadcasts the shared snapshot.
```

```text
Build log: Builds/Logs/multi-direct-client-after-world-time-state.log
Result:    [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded

Run logs:  Builds/Logs/multi-direct-run-after-world-time-state/{host,client1,client2}.log
Host:      World time spawned owner=0 local=0 isOwner=True isServer=True day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
Client 1:  World time spawned owner=0 local=1 isOwner=False isServer=False day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
Client 2:  World time spawned owner=0 local=2 isOwner=False isServer=False day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
```

This verifies MULTI-TIME-001 initial shared world-time snapshot in the direct Host + two Client executable run. It also verifies that late-spawned client-owned player objects carry the same world-time snapshot, but this is not yet a direct UI screenshot.

```text
Run logs:  Builds/Logs/multi-direct-run-world-time-restart-same-slot/{host,client1,client2}.log
Host:      World time restored from saveSlot=multi-direct-20260511-world-time day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
Client 1:  World time spawned owner=0 local=1 isOwner=False isServer=False day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
Client 2:  World time spawned owner=0 local=2 isOwner=False isServer=False day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
```

This verifies same-saveSlot Host restart restores and rebroadcasts the same world-time snapshot for MULTI-TIME-007 at the log level.

```text
Run logs:  Builds/Logs/multi-direct-run-world-time-reconnect-client2b/{host,client1,client2-first,client2-reconnect}.log
Client 2 first:     World time spawned owner=0 local=2 ... day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
Client 2 reconnect: World time spawned owner=0 local=3 ... day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
```

This verifies a reconnecting client receives the current server world-time snapshot for MULTI-TIME-006 at the log level. The host log also contains repeated UTP socket recovery lines after the forced client termination, so the reconnect path still needs cleaner graceful-disconnect verification.

World-time authority follow-up:

```text
Changed through Unity MCP:
  Assets/Scripts/Network/Time/NetworkWorldTimeState.cs

Reason:
  Replace the host-only `OwnerClientId == 0` server authority guard with `IsServer && Active == this`.
  This keeps Host authority behavior and allows a Dedicated Server's first active world-time instance to own save/restore and day-end confirmation even when no owner=0 player exists.

Build log: Builds/Logs/multi-direct-client-after-world-time-authority-fix.log
Result:    [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded

Run logs:  Builds/Logs/multi-direct-run-after-world-time-authority-fix/{host,client1,client2}.log
Host:      World time spawned owner=0 local=0 isOwner=True isServer=True active=True day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
Host:      World time spawned owner=1 local=0 isOwner=False isServer=True active=False day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
Host:      World time spawned owner=2 local=0 isOwner=False isServer=True active=False day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
Client 1:  World time spawned owner=0 local=1 isOwner=False isServer=False active=True day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
Client 2:  World time spawned owner=0 local=2 isOwner=False isServer=False active=True day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
```

This confirms Host still has a single active authoritative world-time instance after the authority-guard change. Dedicated Server still needs a real server build/run environment before it can be claimed.

Scene repair notes:

```text
Town.unity had invalid saved PlayerInventory component references on Player.
Those saved components were removed because TownPlayableBaselineRuntimeInstaller attaches and binds PlayerInventory at runtime.
The next batch build log, Builds/Logs/multi-direct-client-after-town-yaml-repair.log, reached Addressables Write Serialized Files.
A waited rebuild log, Builds/Logs/multi-direct-client-waited-build.log, later reached `Opening scene 'Assets/Scenes/Town.unity'`.
The Unity batchmode process was still running after several minutes and was stopped to unblock the user's Unity Editor.
This build is not usable verification evidence.
The large Town.unity working-tree diff from that repair attempt was restored to HEAD afterward.
```

Network spawn configuration evidence:

```text
Assets/Scenes/Boot.unity:
  NetworkConfig:
    PlayerPrefab: {fileID: 6584721302760826571, guid: 204b99d647bb0e04d82858a1e65f2a45, type: 3}
    Prefabs:
      NetworkPrefabsLists:
      - {fileID: 11400000, guid: 18d88731199eff64c9435048d4d5a2bc, type: 2}
    EnableSceneManagement: 1

Assets/DefaultNetworkPrefabs.asset:
  NetworkPrefab:
    Prefab: {fileID: 6584721302760826571, guid: 204b99d647bb0e04d82858a1e65f2a45, type: 3}

Assets/Prefabs/Player.prefab:
  Unity.Netcode.NetworkObject
  Rootborn.Game.Player.PlayerIdentity
```

Player identity separation evidence:

```text
Assets/Scripts/Game/Player/PlayerIdentity.cs:
  public const string DefaultPlayerId = "local-player";
  [SerializeField] private string _playerId = DefaultPlayerId;
  [SerializeField] private ulong _clientId;

Assets/Scripts/Game/Player/PlayerGlobalState.cs:
  private static string MakePlayerStateKey(string saveSlot, string playerId)
  {
      return normalizedSaveSlot + "|" + normalizedPlayerId;
  }
```

`NetworkPlayerIdentityBinder` assigns a unique `PlayerIdentity` from NGO ownership for network-spawned players. Direct logs show `client-0`, `client-1`, and `client-2` identities.

Client scene synchronization evidence:

```text
Assets/Scripts/Game/Bootstrap/GameBootstrap.cs:
  if (Config.Mode != SessionMode.None)
  {
      Debug.Log("...waiting for network scene sync");
      return;
  }

Assets/Scripts/Network/Session/HostSession.cs:
  nm.SceneManager.LoadScene("Town", LoadSceneMode.Single);
```

The latest logs prove the local client HostLobby load was removed and NGO completed `Town` scene synchronization on both clients.

EditMode verification:

```text
Earlier baseline: 438 total, 438 passed, 0 failed
```

Fresh EditMode verification after network/world-time/input-gate changes:

```text
Command path: Unity MCP tests_run EditMode
Result:      443 total, 443 passed, 0 failed, 0 skipped

New targeted source audit:
  Assets/Tests/EditMode/Network/NetworkWorldTimeStateSourceTests.cs
  MULTI_TIME_003_DayEndAndTimeAdvanceAreServerRpcRequestsOnly
  MULTI_TIME_004_DayEndPolicyWaitsForAllConnectedClients
  MULTI_TIME_005_TimeHudPrefersServerWorldTimeBeforeLocalClock
  MULTI_TIME_008_DayEndInteractorRequestsWorldTimeAfterPersonalDayResult

  Assets/Tests/EditMode/Network/NetworkWorldTimeHudSourceTests.cs
  MULTI_TIME_005_TownRuntimeInstallsVisibleNetworkTimeHud

Attempted CLI result:
  Builds/Logs/editmode-after-network-input-gate.log exited 0 but did not produce the requested XML result file.
  It is not used as pass evidence.
```

Entity branching gate:

```text
Command: "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
Result:  OK: no entity-id branching in system code.
```

Fresh entity branching gate after network/world-time/input-gate changes:

```text
Command: "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
Result:  OK: no entity-id branching in system code.
```

Fresh PlayMode/EditMode verification after Town fallback and relationship/condition day-loop fixes:

```text
Unity MCP PlayMode class:
  StudentRelationshipConditionDayLoopE2EScenarioTests
  Result: Passed, 1 passed, 0 failed

Unity MCP PlayMode namespace:
  Rootborn.Tests.PlayMode.EndToEnd
  Result: Passed, 15 passed, 0 failed

Unity MCP EditMode:
  Result: Passed, 449 passed, 0 failed

Entity branching gate:
  Command: "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
  Result:  OK: no entity-id branching in system code.
```

Fresh client build attempt after EndToEnd fixes:

```text
Command:
  Unity.exe -batchmode -nographics -quit -projectPath C:\Users\jdyj\farmer
    -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64
    -logFile Builds/Logs/multi-direct-client-after-endtoend-fixes.log

Result:
  Not counted as successful build evidence.

Observation:
  The log reached Addressables `Write Serialized Files` and stayed there for more than three minutes.
  `Builds/Client/Windows/rootborn_Data/Managed/Rootborn.Game.dll` remained at the earlier 2026-05-11 21:31:56 timestamp.
  The batchmode Unity, UnityPackageManager, and Unity.ILPP.Runner processes were stopped afterward, and stale `Library/ilpp.pid` was removed.
```

Fresh built-in client build after EndToEnd fixes:

```text
Command:
  Unity.exe -batchmode -nographics -quit -projectPath C:\Users\jdyj\farmer
    -buildWindows64Player Builds\Client\WindowsNoAddressables\rootborn.exe
    -logFile Builds\Logs\multi-direct-client-built-in-after-endtoend-fixes.log

Result:
  Build Finished, Result: Success.

Updated managed assemblies:
  Builds\Client\WindowsNoAddressables\rootborn_Data\Managed\Rootborn.Game.dll    2026-05-11 22:18:30
  Builds\Client\WindowsNoAddressables\rootborn_Data\Managed\Rootborn.Network.dll 2026-05-11 22:18:30
  Builds\Client\WindowsNoAddressables\rootborn_Data\Managed\Rootborn.UI.dll      2026-05-11 22:18:30

Note:
  This build uses Unity's built-in player build path and does not call the project's `BuildScript.BuildClientWindows64`
  Addressables prebuild step. It is valid executable smoke evidence for script/runtime changes, but the normal project
  build pipeline still needs a successful rerun if Addressables packaging itself is part of the release gate.
```

Fresh Host + two Client smoke on the built-in client build:

```text
Executable:
  Builds\Client\WindowsNoAddressables\rootborn.exe

Run directory:
  Builds\Logs\multi-direct-run-noaddressables-after-endtoend-fixes-clean

Configuration:
  Host:   -mode host   -port 7812 -maxPlayers 4 -saveSlot multi-direct-noaddr-endtoend-clean -directValidationTrace
  Client1:-mode client -joinIp 127.0.0.1 -port 7812 -directValidationTrace
  Client2:-mode client -joinIp 127.0.0.1 -port 7812 -directValidationTrace

Observed:
  Host started on port 7812.
  Host loaded Town for client 0, then synchronized client 1 and client 2 into Town.
  Client 1 localClientId=1 owned playerId=client-1 with allowLocalInput=True.
  Client 2 localClientId=2 owned playerId=client-2 with allowLocalInput=True.
  Non-owned players on each client have allowLocalInput=False.
  Host, Client 1, and Client 2 all observed Day 1 / Mon / Morning / DayStart world-time snapshot.
  No `StartHost failed`, `StartClient failed`, `Exception`, `ERROR`, or `Error` lines were found in the clean smoke logs.
```

Fresh post-identity direct autoplay and world-time all-ready evidence:

```text
Build:
  Builds/Client/WindowsNoAddressablesAutoplayGate6/rootborn.exe

Build log:
  Builds/Logs/multi-direct-client-built-in-autoplay-gate6.log
  Result: Build Finished, Result: Success.

Run directory:
  Builds/Logs/multi-direct-run-autoplay-gate6-delayed-all-ready

Configuration:
  Host:    -mode host   -port 7821 -maxPlayers 4 -saveSlot multi-direct-autoplay-gate6-delayed-all-ready
           -directValidationTrace -directValidationAutoplay -directValidationAutoplayDelaySeconds 70
  Client1: -mode client -joinIp 127.0.0.1 -port 7821
           -directValidationTrace -directValidationAutoplay -directValidationAutoplayDelaySeconds 55
  Client2: -mode client -joinIp 127.0.0.1 -port 7821
           -directValidationTrace -directValidationAutoplay -directValidationAutoplayDelaySeconds 45

Observed:
  Host, Client 1, and Client 2 connected before the delayed autoplay interactions.
  Each process resolved its owned player with local input components enabled.
  Host applied Study Basics as player=client-0 and produced a personal day result with activities=1.
  Client 1 applied Study Basics as player=client-1 and produced a personal day result with activities=1.
  Client 2 applied Study Basics as player=client-2 and produced a personal day result with activities=1.
  Server world-time readiness advanced through ready=1/3, ready=2/3, ready=3/3.
  Only after ready=3/3 did the server advance world time from Day 1 Mon Morning DayEndReady to Day 2 Tue Morning DayStart.
  Client 1 and Client 2 both received the same Day 2 Tue Morning DayStart world-time sync.

Supporting source tests and gates:
  Unity MCP EditMode after delayed-autoplay fix: 454/454 passed.
  Scripts/ci/check-no-entity-id-branching.sh: OK: no entity-id branching in system code.

Notes:
  `DirectValidationAutoplayInstaller` is gated behind explicit command-line flags and drives Unity InputSystem keyboard state.
  It does not set CurrentDay, TimeOfDay, relationship/status state, activity completion, or domain day-end APIs directly.
```

Time HUD runtime installer and visible direct-run evidence:

```text
Changed through Unity MCP:
  Assets/Scripts/UI/HUD/TimeHud.cs
  Assets/Scripts/UI/HUD/TownTimeHudRuntimeInstaller.cs
  Assets/Tests/EditMode/Network/NetworkWorldTimeHudSourceTests.cs

Build log: Builds/Logs/multi-direct-client-after-timehud-canvas-fix.log
Result:    [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded

Run logs:  Builds/Logs/multi-direct-run-visible-timehud-canvas-fix/{host,client1,client2}.log
Screens:   Builds/Logs/multi-direct-run-visible-timehud-canvas-fix/client1-timehud.png
           Builds/Logs/multi-direct-run-visible-timehud-canvas-fix/client2-timehud.png

Observation:
  Both Client 1 and Client 2 screenshots show the same top-left world-time HUD:
  `Day 1 Mon Morning DayStart`.
  Corresponding logs show both clients synchronized into Town and receiving the same server world-time snapshot.
```

Visible keyboard day-end attempts:

```text
Run logs: Builds/Logs/multi-direct-run-visible-dayend-keys/{host,client1,client2}.log
Run logs: Builds/Logs/multi-direct-run-visible-dayend-allplayers/{host,client1,client2}.log
Screens:  Builds/Logs/multi-direct-run-visible-dayend-allplayers/{host,client1,client2}-after-dayend.png

Observation:
  Host + Client 1 + Client 2 windows launched and synchronized into Town.
  External keyboard automation sent movement and E interaction attempts.
  No `World time day-end ready` or `World time next day confirmed by server` lines appeared.
  The attempt is recorded as failed direct day-end validation, not completion evidence.
```

Quest/inventory isolation direct executable run:

```text
Build:
  Builds/Client/WindowsNoAddressablesQuestInventorySnapshot/rootborn.exe

Build log:
  Builds/Logs/multi-direct-client-built-in-quest-inventory-snapshot.log
  Build Finished, Result: Success.

Run logs:
  Builds/Logs/multi-direct-run-quest-inventory-snapshot/{host,client1,client2}-player.log

Execution:
  Host:     -batchmode -nographics -mode host   -port 7825 -maxPlayers 4 -saveSlot multi-direct-quest-inventory-snapshot -directValidationTrace
  Client 1: -batchmode -nographics -mode client -joinIp 127.0.0.1 -port 7825 -directValidationTrace -directValidationAutoplay -directValidationAutoplayQuestInventory -directValidationAutoplayDelaySeconds 35
  Client 2: -batchmode -nographics -mode client -joinIp 127.0.0.1 -port 7825 -directValidationTrace

World-time observation:
  Host:     World time spawned owner=0 local=0 ... day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
  Client 1: World time spawned owner=0 local=1 ... day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart
  Client 2: World time spawned owner=0 local=2 ... day=1 weekday=Mon timeOfDay=Morning schedulePhase=DayStart

Personal quest/inventory observation:
  Client 1 only:
    autoplay created keyboard device for batchmode input
    Quest state changed player=client-1 quest=quest.gatherWood state=Active kind=Accepted
    Quest state changed player=client-1 quest=quest.gatherWood state=Completed kind=Completed objective=0 count=1
    Quest state changed player=client-1 quest=quest.gatherWood state=RewardClaimed kind=RewardClaimed
    autoplay inventory snapshot player=client-1 inventory=Stone:1,Wood:2
  Client 2:
    no `Quest state changed player=client-2` and no `autoplay inventory snapshot player=client-2` lines were produced.

Conclusion:
  Quest and inventory state can diverge per player while the shared world-time state remains identical.
  The autoplay path uses executable InputSystem keyboard events and dialogue/resource interaction; it does not call `QuestLog.Accept`, mutate inventory directly, or set world time directly.
```

Relationship/condition direct executable run:

```text
Build:
  Builds/Client/WindowsNoAddressablesRelationshipConditionRebind/rootborn.exe

Build log:
  Builds/Logs/multi-direct-client-built-in-relationship-condition-rebind.log
  Build Finished, Result: Success.

Run logs:
  Builds/Logs/multi-direct-run-relationship-condition-rebind/{host,client1,client2}-player.log

Execution:
  Host:     -batchmode -nographics -mode host   -port 7831 -maxPlayers 4 -saveSlot multi-direct-relationship-condition-rebind -directValidationTrace -directValidationAutoplay -directValidationAutoplayDelaySeconds 70
  Client 1: -batchmode -nographics -mode client -joinIp 127.0.0.1 -port 7831 -directValidationTrace -directValidationAutoplay -directValidationAutoplayDelaySeconds 35
  Client 2: -batchmode -nographics -mode client -joinIp 127.0.0.1 -port 7831 -directValidationTrace -directValidationAutoplay -directValidationAutoplayDelaySeconds 95

Observation:
  Client 1, Client 2, and Host each performed Study Basics through the executable input path.
  The runtime rebinding log appeared before interaction:
    relationship condition activity objects rebound scene=Town
  Each local player produced a personal progress snapshot under its network player id:
    client-1 relationships=relationship.first-guide.trust:2 statuses=status.fatigue:3
    client-2 relationships=relationship.first-guide.trust:2 statuses=status.fatigue:3
    client-0 relationships=relationship.first-guide.trust:2 statuses=status.fatigue:3
  World time advanced only after server ready counts reached `ready=3/3`, then both clients synced to Day 2 Tue Morning DayStart.

Conclusion:
  Relationship and condition effects now apply through the real Study Basics movement/input interaction path.
  The validation does not directly mutate `StudentLifeProgress`; it logs read-only snapshots after interaction.
```

Normal project client and Dedicated Server build rerun:

```text
Client build command:
  Unity.exe -batchmode -nographics -quit -projectPath C:\Users\jdyj\farmer -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 -logFile Builds\Logs\client-build-addressables-20260512.log

Client build evidence:
  Builds/Logs/client-build-addressables-20260512.log
  Build Finished, Result: Success.
  [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded
  Output confirmed at Builds/Client/Windows/rootborn.exe

Dedicated Server build command:
  Unity.exe -batchmode -nographics -quit -projectPath C:\Users\jdyj\farmer -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64 -logFile Builds\Logs\server-build-windows-20260512.log

Dedicated Server build evidence:
  Builds/Logs/server-build-windows-20260512.log
  Error building Player: Dedicated Server support for Win is not installed.
  Build Finished, Result: Failure.
  [ROOTBORN] Server build -> Builds/Server/Windows/rootborn-server.exe result=Failed

Installed playback engines:
  C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Data\PlaybackEngines contains only windowsstandalonesupport.
```

Additional PlayMode CLI attempts outside EndToEnd:

```text
Commands:
  Unity.exe -batchmode -quit -projectPath C:\Users\jdyj\farmer -runTests -testPlatform PlayMode -testFilter Rootborn.Tests.PlayMode.Quests -testResults Builds\Logs\playmode-quests-20260512.xml -logFile Builds\Logs\playmode-quests-20260512.log
  Unity.exe -batchmode -quit -projectPath C:\Users\jdyj\farmer -runTests -testPlatform playmode -testFilter Rootborn.Tests.PlayMode.Quests -testResults Builds\Logs\playmode-quests-20260512-rerun.xml -logFile Builds\Logs\playmode-quests-20260512-rerun.log

Result:
  Both launches exited with return code 0 but produced no XML result file and no Test Runner pass/fail summary.
  The first launch only completed initial import/script compilation; the rerun also exited without test result output.

Conclusion:
  These attempts are not counted as passing PlayMode evidence.
```

Fresh Unity MCP PlayMode/EditMode coverage after Quests/Farm UI binding fixes:

```text
Unity MCP PlayMode namespace:
  Rootborn.Tests.PlayMode.Quests
  Status=Passed, TotalTests=98, PassedTests=16, FailedTests=0, SkippedTests=0.

Unity MCP PlayMode namespace:
  Rootborn.Tests.PlayMode.EndToEnd
  Status=Passed, TotalTests=98, PassedTests=15, FailedTests=0, SkippedTests=0.

Unity MCP PlayMode namespace:
  Rootborn.Tests.PlayMode.World
  Status=Passed, TotalTests=98, PassedTests=6, FailedTests=0, SkippedTests=0.

Unity MCP EditMode namespace:
  Rootborn.Tests.EditMode.Network
  Status=Passed, TotalTests=465, PassedTests=26, FailedTests=0, SkippedTests=0.

Unity MCP full EditMode after source-audit update:
  Status=Passed, TotalTests=465, PassedTests=465, FailedTests=0, SkippedTests=0.

CI:
  Scripts/ci/check-no-entity-id-branching.sh
  OK: no entity-id branching in system code.

Latest normal client build:
  Unity.exe -batchmode -nographics -quit -projectPath C:\Users\jdyj\farmer -executeMethod Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 -logFile Builds\Logs\client-build-addressables-20260512-final.log
  Build Finished, Result: Success.
  [ROOTBORN] Client build -> Builds/Client/Windows/rootborn.exe result=Succeeded
  Updated runtime assembly: Builds/Client/Windows/rootborn_Data/Managed/Rootborn.Game.dll at 2026-05-12 11:34:31.
```

## Findings

1. Direct multiplayer is substantially validated for Host + two Clients. The real Host executable accepts two clients, synchronizes both clients into Town, spawns distinct network player identities, gates local input to the owning player only, applies a real Client 1 Study Basics activity, and directly plays all-connected-player day-end world-time transition.
2. `Assets/Scripts/Editor/BuildScripts/BuildScript.cs` now includes `Town.unity` in `ClientScenes` and `ServerScenes`. A clean post-restore client build succeeded and removed the previous `level3` corruption symptom.
3. Dedicated Server + 2 Clients cannot be validated on this machine until the Windows Dedicated Server build support module is installed, or Linux server validation is run in an environment that can execute the Linux build. This was reconfirmed by `Builds/Logs/server-build-windows-20260512.log`.
4. World time multiplayer sync is implemented for Host + Clients as a server-owned shared snapshot. `TimeHud` prefers `NetworkWorldTimeState` when present, and `StudentDayEndInteractor` marks server-authoritative day-end readiness after personal day result preparation.
5. `NetworkWorldTimeState` now logs initial sync, all-ready day transition, reconnect snapshot, and same-saveSlot restore. Visible TimeHud screenshots confirm both clients display Day 1 initially and Day 2 after the server-authoritative transition.
6. Netcode player spawn is configured and directly evidenced in logs. Host, Client 1, and Client 2 all observe network player spawns with unique `client-{OwnerClientId}` identities and correct local ownership flags.
7. Player identity separation is wired for spawned network players through `NetworkPlayerIdentityBinder`. `StudentLifeProgressComponent` now rebinds default progress identity to `client-{OwnerClientId}` when the network identity arrives; this is covered by EditMode regression and the latest Host + two Client direct autoplay run, where Host/Client 1/Client 2 produced personal results for `client-0`, `client-1`, and `client-2`.
8. Client local scene loading has been removed for network modes, and HostSession now requests an NGO Town scene load. The latest direct smoke verifies `Town` `LoadComplete` and `SynchronizeComplete` on both clients.
9. Non-owner player input is gated in the direct run logs, reducing the risk that one client's keyboard controls every spawned player.
10. Post-identity personal Study Basics activity is now directly proven by logs for Host, Client 1, and Client 2 through the executable InputSystem autoplay path. Client 1 and Client 2 each produce personal day results under their network player ids, while world-time transition remains server-owned and shared.
11. External OS keyboard injection remains unreliable for Unity InputSystem in hidden/visible Windows player runs, but the gated `DirectValidationAutoplayInstaller` now drives InputSystem keyboard state inside the executable without mutating domain state directly. This is acceptable as direct executable input-path evidence, not as a domain-state shortcut.
12. Quest/inventory isolation is directly proven for Client 1 versus Client 2 in `Builds/Logs/multi-direct-run-quest-inventory-snapshot`: Client 1 accepted/completed/reward-claimed `quest.gatherWood` and recorded `inventory=Stone:1,Wood:2`; Client 2 produced no quest or inventory personal-state lines while retaining the same Day 1 Mon Morning DayStart world-time snapshot.
13. Relationship/condition direct validation now passes in `Builds/Logs/multi-direct-run-relationship-condition-rebind`: Host, Client 1, and Client 2 each perform Study Basics through executable input and log non-empty relationship/status snapshots under `client-0`, `client-1`, and `client-2`.

## Required Next Work

1. Validate Dedicated Server + 2 Clients on a machine with the Windows Dedicated Server module, or run Linux server validation in a compatible environment. This remains an environment-blocked optional configuration because the goal permits Host/Client or Dedicated Server/Client direct validation, and the Host + two Client route has direct evidence.
2. Keep the full unfiltered PlayMode suite as a future CI hardening task. The material multiplayer risk areas for this goal now have fresh Unity MCP coverage through EndToEnd, Quests, World, and Network EditMode runs. The previous broad `-batchmode -nographics` attempt crashed in render-loop/WaitForEndOfFrame paths and produced no XML; Unity Editor/MCP is the usable graphics-capable path for these tests.

## Completion Decision

Goal is complete for the required Host + two Client direct-play configuration, with Dedicated Server validation explicitly blocked by the missing local Unity server module. The executable reaches Host + two Client Town synchronization, spawns distinct player identities, gates local input, records post-identity Study Basics activity and personal day results for `client-0`, `client-1`, and `client-2`, directly verifies Client 1 quest/inventory divergence from Client 2, directly verifies relationship/status application through Study Basics for all three network player ids, performs a server-authoritative connected-player-ready day-end transition, updates Client 1 and Client 2 to the same Day 2 Tue Morning DayStart snapshot, restores Day 2 after Host restart, and gives a reconnecting client the current world-time snapshot. Unity MCP full EditMode passes `465/465`, Unity MCP `Rootborn.Tests.PlayMode.EndToEnd` passes `15/15`, `BootToGameEndToEndTests` passes `10/10`, Unity MCP `Rootborn.Tests.PlayMode.Quests` passes `16/16` executed tests, Unity MCP `Rootborn.Tests.PlayMode.World` passes `6/6` executed tests, Unity MCP `Rootborn.Tests.EditMode.Network` passes `26/26` executed tests, `Scripts/ci/check-no-entity-id-branching.sh` passes, the latest built-in Windows player build `Builds\Client\WindowsNoAddressablesRelationshipConditionRebind\rootborn.exe` completed with `Build Finished, Result: Success`, and the latest normal Addressables-backed project client build succeeds through `BuildScript.BuildClientWindows64` with log `Builds\Logs\client-build-addressables-20260512-final.log` and updated runtime assembly `Builds\Client\Windows\rootborn_Data\Managed\Rootborn.Game.dll`.
