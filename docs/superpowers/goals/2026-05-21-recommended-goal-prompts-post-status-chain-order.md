# 추천 순서별 Goal 프롬프트: Status Activity Chain 이후

작성일: 2026-05-21

이 문서는 `Status Activity Chain` 구현 완료 이후 다음에 진행할 기능/안정화 작업을 추천 순서대로 실행할 수 있도록 정리한 goal 프롬프트다. 현재 기준으로 이미 완료된 항목은 제외하고, 기반 안정화와 player-facing PlayMode 검증 강도를 우선한다.

## 추천 순서 요약

1. House Upgrade/Interior Loop E2E Hardening
2. Objective Journal Legacy HUD Final Ownership Audit
3. Dedicated Core Gameplay Matrix Follow-up
4. Quest/Interior Reward Saved-State Regression Hardening

## 공통 절대 조건

- 공유 작업 트리로 취급한다. 관련 없는 dirty/untracked 파일은 되돌리거나 삭제하지 않는다.
- `Assets/**/*.cs`는 shell/apply_patch로 직접 쓰지 않고 Unity MCP `script-update-or-create`를 사용한다.
- 새 동작, 회귀 보강, QA runner 변경은 실패 테스트를 먼저 만든다.
- 게임 엔티티 진행 조건과 결과는 ScriptableObject 데이터와 전략 배열로 표현한다.
- production C#에 quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, knowledge, status ID별 분기를 추가하지 않는다.
- 목표성 UI는 `ObjectiveJournalPanel`이 소유하고, 상시 HUD는 `TrackedObjectiveHud` 하나만 허용한다.
- visible/player-facing 변경은 실제 PlayMode 사용자 흐름, Game View 또는 Camera screenshot, runtime state, 저장 후 reload 상태로 검증한다.
- runtime-only 검증은 saved scene, prefab, ScriptableObject, tilemap, serialized save state 검증을 대체하지 못한다.
- `Assets/AddressableAssetsData/link.xml`과 `.meta` 보존을 게이트로 확인한다.
- 완료 전 `check-addressables-linkxml-preserved.ps1`, `git diff --check`, `check-no-entity-id-branching.sh`, `git status --short`를 실행한다.

---

## Prompt 1: House Upgrade/Interior Loop E2E Hardening

### Objective

기존 House upgrade, room preset, furniture placement, save/load 조각을 하나의 player-facing E2E 루프로 묶어 보강한다. stage 1 upgrade, expanded House 진입, room preset 적용, furniture placement, reload 이후 visible/runtime/saved state가 일치해야 한다.

### Why First

House/Interior는 시각 상태, tilemap, placement, 저장 상태가 같이 맞아야 하는 고위험 영역이다. 이미 부분 기능과 테스트가 있으므로 새 기능을 넓히기 전에 통합 E2E를 먼저 단단하게 만드는 것이 우선이다.

### Start From

```text
docs/superpowers/goals/2026-05-21-house-upgrade-interior-loop-e2e-hardening-goal.md
docs/superpowers/specs/2026-05-17-house-upgrade-and-interior-construction-design.md
docs/superpowers/specs/2026-05-19-house-expansion-interior-loop-design.md
```

### Required Work

- stage-1 House data assets가 `GameDataRegistry.asset`에 등록되어 있고 null reference가 없는지 테스트로 고정한다.
- full loop E2E가 saved stage, selected preset, furniture record, tilemap object cell, occupancy cell, placement bounds, overlay cleanup을 모두 assert하도록 보강한다.
- preset 적용이 furniture/object/occupancy tilemap을 지우지 않도록 회귀 테스트를 추가하거나 강화한다.
- reload 이후 saved furniture record와 visible furniture cell이 중복되지 않는지 검증한다.
- 실제 Game View screenshot을 `Builds/Logs/house-upgrade-interior-loop-e2e.png`로 남긴다.

### Required PlayMode Scenario

```text
HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload
```

시나리오는 다음을 포함해야 한다.

1. 가능한 경우 MainMenu SaveSlot flow에서 시작한다.
2. player-facing UI 또는 지원되는 House/Town 진입 흐름으로 House upgrade provider를 연다.
3. stage 1 upgrade 가능 상태와 locked/insufficient 상태를 확인한다.
4. 지원되는 route로 stage 1 upgrade를 실행한다.
5. expanded House가 Game View에서 보이는지 확인한다.
6. room preset toolbar를 열고 유효한 preset 하나를 적용한다.
7. furniture placement UI를 열고 furniture 하나를 선택, 회전 지원 시 회전, visible placement를 수행한다.
8. selected furniture asset name, anchor cell, direction, object tilemap, occupancy tilemap, placement bounds, overlay state를 inspect한다.
9. screenshot을 캡처한다.
10. serialized House state와 furniture layout file을 inspect한다.
11. 같은 save slot 또는 House scene reload 후 stage, preset, furniture record, tilemap cells, occupancy cells, overlay cleanup이 중복 없이 복원되는지 확인한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Housing
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Housing
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Interiors
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeInteriorLoopE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload
Get-ChildItem Builds/Logs/house-upgrade-interior-loop-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Stage-1 House upgrade가 기존 ScriptableObject data로 유지된다.
- expanded House generation, preset application, furniture placement, save, reload가 하나의 E2E에서 증명된다.
- runtime visible state와 serialized state가 reload 후 일치한다.
- saved House stage, preset id, furniture id, anchor cell, direction, object tile, occupancy tile이 exact-once다.
- stale construction, preview, sample, debug overlay가 reload 후 보이지 않는다.

### Commit

```text
[TEST] House upgrade interior loop E2E 강화
```

---

## Prompt 2: Objective Journal Legacy HUD Final Ownership Audit

### Objective

persistent objective-like UI의 최종 소유권을 감사한다. 목표, 퀘스트, 캠페인, route, career, relationship, housing, resource/recipe, knowledge/status guidance는 `ObjectiveJournalPanel`과 exactly one `TrackedObjectiveHud`를 통해서만 표시되어야 한다.

### Why Second

Daily Plan, Career/Job, Relationship, Resource/Recipe, Status, Housing guidance가 Objective Journal로 모이고 있다. 이 시점에서 legacy HUD 재발을 막는 ownership gate를 고정해야 이후 기능이 UI를 흩뜨리지 않는다.

### Start From

```text
docs/superpowers/goals/2026-05-21-objective-journal-legacy-hud-final-ownership-audit-goal.md
docs/superpowers/specs/2026-05-19-objective-journal-legacy-hud-migration-design.md
docs/superpowers/specs/2026-05-19-objective-journal-integrated-tracking-design.md
```

### Required Work

- `ObjectiveJournalUiOwnershipGateTests`를 강화해 persistent objective-like factory와 reviewed exception을 명확히 분리한다.
- default Objective Journal sources가 duplicate stable key를 만들면 실패하도록 source/aggregator test를 강화한다.
- tracking restore가 새 HUD를 만들지 않고 기존 `TrackedObjectiveHud`를 재사용하는지 테스트한다.
- player-facing E2E를 추가하거나 강화해 `Tab`으로 Journal을 열고 row click/track/reload를 검증한다.
- transient result, reward, dialogue, location log, quest log, career detail panel은 persistent objective guidance가 아닌 경우 유지한다.

### Required PlayMode Scenario

```text
OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud
```

시나리오는 다음을 포함해야 한다.

1. MainMenu SaveSlot flow에서 시작한다.
2. Town에 진입한다.
3. 최소 두 개 이상의 objective guidance source state를 실제 사용자 흐름으로 만든다.
4. `Tab`으로 Objective Journal을 연다.
5. visible row/button을 클릭하고 하나의 item을 track한다.
6. exactly one `TrackedObjectiveHud`를 확인한다.
7. legacy persistent goal HUD가 없는지 inspect한다.
8. progress action 하나를 수행해 Journal과 tracked HUD update를 확인한다.
9. screenshot을 `Builds/Logs/objective-journal-ownership-e2e.png`로 캡처한다.
10. 같은 save slot reload 후 duplicate row, duplicate stable key, duplicate HUD가 없는지 확인한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalOwnershipE2EScenarioTests.OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud
Get-ChildItem Builds/Logs/objective-journal-ownership-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- persistent objective-like UI는 `ObjectiveJournalPanel`과 one `TrackedObjectiveHud`만 소유한다.
- reviewed non-objective panel은 transient 또는 user-opened detail/log/result surface로만 남는다.
- default sources가 duplicate stable key를 내보내지 않는다.
- reload 후 Journal rows와 tracked HUD가 중복되지 않는다.

### Commit

```text
[REFACTOR][UI][TEST] Objective Journal HUD ownership 최종 감사
```

---

## Prompt 3: Dedicated Core Gameplay Matrix Follow-up

### Objective

Dedicated server/client gameplay matrix를 현재 core gameplay slice까지 확장한다. route/relationship, career/job route, resource/recipe, Status 또는 House 최신 slice, Objective Journal reload behavior가 dedicated process 조건에서 깨지지 않아야 한다.

### Why Third

Editor PlayMode 검증만으로는 dedicated server/client process, build artifact, log evidence, reload persistence 문제를 잡기 어렵다. 다만 실행 시간이 길고 orchestration 성격이 강하므로 House와 Objective Journal 안정화 이후에 둔다.

### Start From

```text
docs/superpowers/goals/2026-05-21-dedicated-core-gameplay-matrix-goal.md
Scripts/qa/run-dedicated-core-gameplay-matrix.ps1
```

### Required Work

- matrix runner가 최신 player-facing slice를 포함하도록 확장한다.
- 각 slice는 isolated run name, isolated save slot, derived port, action pass, observe-only reload pass를 사용한다.
- 각 cycle은 screenshot, completion marker, runtime snapshot, saved/restored-state evidence를 남긴다.
- runner는 slice script missing, non-zero exit, missing log directory, zero-byte screenshot, missing marker, duplicate-state marker 누락을 fail closed로 처리한다.
- cleanup 후 launched server/client process가 남지 않아야 한다.

### Required Process Scenario

```text
NETWORK_DEDICATED_MATRIX_001_CoreGameplayReload
```

시나리오는 다음을 포함해야 한다.

1. fresh client/server artifacts를 확인한다.
2. route/relationship dedicated cycle을 실행한다.
3. career/job route dedicated cycle을 실행한다.
4. resource/recipe dedicated cycle을 실행한다.
5. Status activity chain 또는 House upgrade/interior loop 중 최신 구현 slice dedicated cycle을 실행한다.
6. 각 client cycle마다 screenshot을 캡처한다.
7. 모든 slice에서 persisted state reload를 검증한다.
8. cleanup 후 process leak이 없는지 확인한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix -StartPort 7931 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix -Recurse -File | Select-Object FullName,Length,LastWriteTime
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Dedicated matrix가 현재 core gameplay slice 여러 개를 action/reload 방식으로 검증한다.
- 모든 slice의 screenshot, log, runtime state, saved state가 일치한다.
- reload 후 Journal/HUD/gameplay state가 중복되지 않는다.
- 성공/실패 후 server/client process leak이 없다.
- 실패 출력은 build artifact, network process, gameplay saved-state 문제를 구분한다.

### Commit

```text
[NETWORK][TEST] Dedicated core gameplay matrix follow-up 추가
```

---

## Prompt 4: Quest/Interior Reward Saved-State Regression Hardening

### Objective

Quest-to-interior reward slice의 transaction과 saved-state 회귀를 보강한다. duplicate reward claim, reload, House placement save가 furniture unlock token, placed tile, occupancy cell, saved layout record를 중복시키면 안 된다.

### Why Fourth

Reward와 inventory/placement 저장은 조용히 깨지기 쉽다. 앞선 player-facing slice와 matrix 기반이 안정화된 뒤, reward transaction의 atomic/idempotent 속성을 별도 회귀로 고정한다.

### Start From

```text
docs/superpowers/goals/2026-05-21-quest-interior-reward-saved-state-regression-goal.md
```

### Required Work

- `QuestInteriorUnlockRewardTests`를 강화해 duplicate unlock token이 saved-state count를 부풀리지 못하게 한다.
- `QuestInteriorRewardE2EScenarioTests.QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload`를 강화한다.
- reward claim 전 furniture locked 상태와 claim 후 visible/unlocked 상태를 확인한다.
- duplicate claim attempt가 unlock save file을 변경하지 않는지 검증한다.
- serialized furniture unlock data가 unlock token을 정확히 한 번만 포함하는지 검증한다.
- serialized House furniture layout이 reward furniture id와 tile name을 정확히 한 번만 포함하는지 검증한다.
- visible House tilemap과 occupancy tilemap이 reload 후 정확히 하나의 restored placed furniture instance를 가지는지 검증한다.

### Required PlayMode Scenario

```text
QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload
```

시나리오는 다음을 포함해야 한다.

1. MainMenu SaveSlot flow에서 시작한다.
2. reward trigger를 visible interaction/UI로 수행한다.
3. reward 수령 UI 또는 placement result를 확인한다.
4. furniture unlock, reward claimed, layout state를 inspect한다.
5. screenshot을 `Builds/Logs/quest-interior-reward-e2e.png`로 캡처한다.
6. 같은 save slot reload 후 reward claimed state와 interior state가 유지되는지 확인한다.
7. duplicate reward trigger가 item, unlock token, layout record, placement tile을 중복 생성하지 않는지 확인한다.
8. 가능한 경우 inventory full 또는 duplicate claim failure path가 state를 변경하지 않는지 확인한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Quests
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardE2EScenarioTests.QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload
Get-ChildItem Builds/Logs/quest-interior-reward-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- reward claimed state가 reload 후 유지되고 재수령되지 않는다.
- serialized furniture unlock data에 unlock token이 정확히 한 번만 있다.
- serialized House furniture layout에 reward furniture id와 tile name이 정확히 한 번만 있다.
- runtime House tilemaps에 visible reward furniture tile과 occupancy cell이 하나씩만 있다.
- visible/interior 변경은 Game View screenshot과 serialized state로 검증된다.
- Objective Journal ownership과 Modern UI Style2 규약을 깨지 않는다.

### Commit

```text
[TEST] Quest interior reward 저장 회귀 강화
```

## 실행 메모

- Prompt 1이 이미 완전히 충족된 상태로 확인되면 새 커밋을 만들지 말고 verification evidence만 기록한 뒤 Prompt 2로 넘어간다.
- 각 prompt 완료 후에는 이 추천 순서를 다시 갱신해 이미 완료된 항목을 제외한다.
