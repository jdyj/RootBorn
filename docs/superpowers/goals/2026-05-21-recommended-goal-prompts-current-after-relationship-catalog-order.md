# 추천 순서별 Goal 프롬프트: Relationship Commitment Catalog 이후

작성일: 2026-05-21

이 문서는 현재 커밋 상태 기준으로 다음 세션에 그대로 붙여 넣어 실행할 수 있는 goal 프롬프트를 추천 순서대로 정리한다. 이미 완료된 Dedicated smoke/matrix, Career/Job exact-once 보강, Relationship commitment scenario catalog 활성화는 제외한다.

## 추천 순서 요약

1. Knowledge/Status Activity Chain Scenario Catalog Activation
2. House Upgrade/Interior Loop E2E Hardening
3. Objective Journal Legacy HUD Final Ownership Audit
4. Dedicated Core Gameplay Matrix Follow-up
5. Quest/Interior Reward Saved-State Regression Hardening

## 공통 절대 조건

- 공유 워크트리로 취급한다. 관련 없는 dirty/untracked 파일은 되돌리거나 삭제하지 않는다.
- `Assets/**/*.cs`는 shell/apply_patch로 직접 쓰지 않고 Unity MCP `script-update-or-create` 또는 `script_execute`를 사용한다.
- 신규 동작, 회귀 보강, QA runner 변경은 실패 테스트를 먼저 만든다.
- 게임 엔티티별 진행 조건과 결과는 ScriptableObject 데이터와 전략 배열로 표현한다.
- production C#에 quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, knowledge, status ID별 분기를 추가하지 않는다.
- 목표성 UI는 `ObjectiveJournalPanel`이 소유하고, 상시 HUD는 `TrackedObjectiveHud` 하나만 허용한다.
- visible/player-facing 변경은 실제 PlayMode 사용자 흐름, Game View 또는 Camera screenshot, runtime state, 저장 후 reload state로 검증한다.
- runtime-only 검증은 saved scene, prefab, ScriptableObject, tilemap, serialized save state 검증을 대체하지 못한다.
- Addressables generated churn은 커밋하지 않는다. 완료 전 `check-addressables-linkxml-preserved.ps1`, `git diff --check`, `check-no-entity-id-branching.sh`, `git status --short`를 실행한다.

---

## Prompt 1: Knowledge/Status Activity Chain Scenario Catalog Activation

아래 프롬프트를 가장 먼저 붙여 넣는다.

```text
docs/superpowers/goals/2026-05-21-knowledge-status-activity-chain-goal.md 기반으로 Knowledge/Status Activity Chain scenario catalog activation을 진행해줘.

목표:
- 기존 `KnowledgeStatusActivityChainE2EScenarioTests`와 `StatusActivityObjectiveSource` 흐름이 이미 구현되어 있는지 먼저 확인한다.
- 구현과 PlayMode 증거가 존재하면 새 기능을 중복 구현하지 말고, scenario registry/catalog가 해당 player-facing 증거를 active 시나리오로 추적하도록 보강한다.
- 구현 또는 증거가 빠져 있으면 catalog만 억지로 추가하지 말고, RED 테스트로 누락을 드러낸 뒤 최소 범위에서 구현/검증한다.

필수 조사:
- `Assets/Tests/PlayMode/EndToEnd/KnowledgeStatusActivityChainE2EScenarioTests.cs`
- `Assets/Tests/EditMode/StudentLife/StatusActivityChainTests.cs`
- `Assets/Tests/EditMode/UI/Objectives/StatusActivityObjectiveSourceTests.cs`
- `Assets/Scripts/UI/Objectives/Sources/StatusActivityObjectiveSource.cs`
- `docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md`
- `Builds/Logs/knowledge-status-activity-chain-e2e.png`

필수 TDD:
- RED: `StudentLifeScenarioRegistryCoverageGateTests`에 `SCENARIO_REGISTRY_EDIT_012_CatalogActivatesKnowledgeStatusActivityChainScenario`를 추가한다.
- RED는 catalog에 `KNOW-STATUS-ACTIVITY-001` active row, PlayMode test file, method name, screenshot path가 없어서 실패해야 한다.
- GREEN: catalog에 active row를 추가하고, 기존 PlayMode 증거와 reload/duplicate-prevention 설명을 연결한다.

필수 catalog row:
- ID: `KNOW-STATUS-ACTIVITY-001`
- Category: `KNOW`
- Owner: `Knowledge/Status Activity Chain`
- Evidence Type: `PlayMode`
- Evidence: `Assets/Tests/PlayMode/EndToEnd/KnowledgeStatusActivityChainE2EScenarioTests.cs`
- Player-Facing Verification: `KNOWLEDGE_STATUS_E2E_001_ActivityChainHintReloadNoDuplicate`, screenshot `Builds/Logs/knowledge-status-activity-chain-e2e.png`, Objective Journal hint, tracked HUD, reload persistence, duplicate prevention

필수 검증:
```powershell
tests_run EditMode testClass StudentLifeScenarioRegistryCoverageGateTests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
Get-ChildItem Builds/Logs/knowledge-status-activity-chain-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

수용 기준:
- Knowledge/Status Activity Chain이 active scenario catalog에 등록된다.
- evidence file, PlayMode method, screenshot path가 catalog gate로 고정된다.
- scenario sync gate가 통과한다.
- 새 기능 중복 구현 없이 기존 player-facing 증거를 추적한다.

커밋:
`[TEST][DOCS] Knowledge Status activity scenario catalog 활성화`
```

---

## Prompt 2: House Upgrade/Interior Loop E2E Hardening

Prompt 1 완료 후 붙여 넣는다.

```text
House Upgrade/Interior Loop E2E hardening을 진행해줘.

목표:
- 기존 House upgrade, room preset, furniture placement, save/load 조각을 하나의 player-facing E2E 루프로 묶어 보강한다.
- stage 1 upgrade, expanded House 진입, room preset 적용, furniture placement, reload 이후 visible/runtime/saved state가 일치해야 한다.

시작 문서:
- `docs/superpowers/goals/2026-05-21-house-upgrade-interior-loop-e2e-hardening-goal.md`
- `docs/superpowers/specs/2026-05-17-house-upgrade-and-interior-construction-design.md`
- `docs/superpowers/specs/2026-05-19-house-expansion-interior-loop-design.md`

필수 작업:
- stage-1 House data assets가 `GameDataRegistry.asset`에 등록되어 있고 null reference가 없는지 테스트로 고정한다.
- full loop E2E가 saved stage, selected preset, furniture record, tilemap object cell, occupancy cell, placement bounds, overlay cleanup을 모두 assert하도록 보강한다.
- preset 적용이 furniture/object/occupancy tilemap을 지우지 않도록 회귀 테스트를 추가하거나 강화한다.
- reload 이후 saved furniture record와 visible furniture cell이 중복되지 않는지 검증한다.
- 실제 Game View screenshot을 `Builds/Logs/house-upgrade-interior-loop-e2e.png`로 남긴다.

필수 PlayMode 시나리오:
- `HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload`
- MainMenu SaveSlot flow에서 시작한다.
- player-facing UI 또는 지정된 House/Town 진입 흐름으로 House upgrade provider를 연다.
- stage 1 upgrade 가능/불가능 상태를 확인한다.
- 지정된 route로 stage 1 upgrade를 수행한다.
- expanded House가 Game View에서 보이는지 확인한다.
- room preset toolbar를 열고 유효한 preset 하나를 적용한다.
- furniture placement UI를 열고 furniture 하나를 선택, 회전 지원 시 회전, visible placement를 수행한다.
- selected furniture asset name, anchor cell, direction, object tilemap, occupancy tilemap, placement bounds, overlay state를 inspect한다.
- screenshot을 캡처한다.
- serialized House state와 furniture layout file을 inspect한다.
- 같은 save slot reload 후 stage, preset, furniture record, tilemap cells, occupancy cells, overlay cleanup이 중복 없이 복원되는지 확인한다.

필수 검증:
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

커밋:
`[TEST] House upgrade interior loop E2E 강화`
```

---

## Prompt 3: Objective Journal Legacy HUD Final Ownership Audit

Prompt 2 완료 후 붙여 넣는다. 만약 새 기능 중 별도 상시 goal HUD가 생기는 징후가 보이면 Prompt 2보다 먼저 진행한다.

```text
Objective Journal Legacy HUD final ownership audit을 진행해줘.

목표:
- persistent objective-like UI의 최종 소유권을 감사한다.
- 목표, 퀘스트, 캠페인, route, career, relationship, housing, resource/recipe, knowledge/status guidance는 `ObjectiveJournalPanel`과 exactly one `TrackedObjectiveHud`를 통해서만 표시되어야 한다.

시작 문서:
- `docs/superpowers/goals/2026-05-21-objective-journal-legacy-hud-final-ownership-audit-goal.md`
- `docs/superpowers/specs/2026-05-19-objective-journal-legacy-hud-migration-design.md`
- `docs/superpowers/specs/2026-05-19-objective-journal-integrated-tracking-design.md`

필수 작업:
- `ObjectiveJournalUiOwnershipGateTests`를 강화해 persistent objective-like factory와 reviewed exception을 명확히 분리한다.
- default Objective Journal sources가 duplicate stable key를 만들면 실패하도록 source/aggregator test를 강화한다.
- tracking restore가 새 HUD를 만들지 않고 기존 `TrackedObjectiveHud`를 재사용하는지 테스트한다.
- player-facing E2E를 추가하거나 강화해 `Tab`으로 Journal을 열고 row click/track/reload를 검증한다.
- transient result, reward, dialogue, location log, quest log, career detail panel은 persistent objective guidance가 아닌 경우 허용한다.

필수 PlayMode 시나리오:
- `OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud`
- MainMenu SaveSlot flow에서 시작한다.
- Town에 진입한다.
- 최소 두 개 이상의 objective guidance source state를 실제 사용자 흐름으로 만든다.
- `Tab`으로 Objective Journal을 연다.
- visible row/button을 클릭하고 하나의 item을 track한다.
- exactly one `TrackedObjectiveHud`를 확인한다.
- legacy persistent goal HUD가 없는지 inspect한다.
- progress action 하나를 수행해 Journal과 tracked HUD update를 확인한다.
- screenshot을 `Builds/Logs/objective-journal-ownership-e2e.png`로 캡처한다.
- 같은 save slot reload 후 duplicate row, duplicate stable key, duplicate HUD가 없는지 확인한다.

필수 검증:
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

커밋:
`[REFACTOR][UI][TEST] Objective Journal HUD ownership 최종 감사`
```

---

## Prompt 4: Dedicated Core Gameplay Matrix Follow-up

Prompt 1~3 완료 후 붙여 넣는다. Multiplayer validation이 release blocker면 더 앞당길 수 있다.

```text
Dedicated Core Gameplay Matrix follow-up을 진행해줘.

목표:
- Dedicated server/client gameplay matrix를 현재 core gameplay slice까지 확장한다.
- route/relationship, career/job route, resource/recipe, Knowledge/Status 또는 House 최신 slice, Objective Journal reload behavior가 dedicated process 조건에서 깨지지 않아야 한다.

시작 지점:
- `docs/superpowers/goals/2026-05-21-dedicated-core-gameplay-matrix-goal.md`
- `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1`

필수 작업:
- matrix runner가 최신 player-facing slice를 포함하도록 확장한다.
- 각 slice는 isolated run name, isolated save slot, derived port, action pass, observe-only reload pass를 사용한다.
- 각 cycle은 screenshot, completion marker, runtime snapshot, saved/restored-state evidence를 남긴다.
- runner는 slice script missing, non-zero exit, missing log directory, zero-byte screenshot, missing marker, duplicate-state marker 누락을 fail closed로 처리한다.
- cleanup 후 launched server/client process가 남지 않아야 한다.

필수 Process Scenario:
- `NETWORK_DEDICATED_MATRIX_001_CoreGameplayReload`
- fresh client/server artifacts를 확인한다.
- route/relationship dedicated cycle을 실행한다.
- career/job route dedicated cycle을 실행한다.
- resource/recipe dedicated cycle을 실행한다.
- Knowledge/Status 또는 House upgrade/interior loop 중 최신 구현 slice dedicated cycle을 실행한다.
- 각 client cycle마다 screenshot을 캡처한다.
- 모든 slice에서 persisted state reload를 검증한다.
- cleanup 후 process leak이 없는지 확인한다.

필수 검증:
```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix-followup -StartPort 7931 -WaitSeconds 120
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix-followup -Recurse -File | Select-Object FullName,Length,LastWriteTime
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

커밋:
`[NETWORK][TEST] Dedicated core gameplay matrix follow-up 추가`
```

---

## Prompt 5: Quest/Interior Reward Saved-State Regression Hardening

Prompt 4 이후 붙여 넣는다. Quest/interior reward 쪽 중복 지급 또는 placement 저장 회귀가 보이면 더 앞당긴다.

```text
Quest/Interior Reward Saved-State Regression hardening을 진행해줘.

목표:
- Quest-to-interior reward slice의 transaction과 saved-state 회귀를 보강한다.
- duplicate reward claim, reload, House placement save가 furniture unlock token, placed tile, occupancy cell, saved layout record를 중복시키면 안 된다.

시작 지점:
- `docs/superpowers/goals/2026-05-21-quest-interior-reward-saved-state-regression-goal.md`

필수 작업:
- `QuestInteriorUnlockRewardTests`를 강화해 duplicate unlock token과 saved-state count를 부풀리지 못하게 한다.
- `QuestInteriorRewardE2EScenarioTests.QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload`를 강화한다.
- reward claim 전 furniture locked 상태와 claim 후 visible/unlocked 상태를 확인한다.
- duplicate claim attempt가 unlock save file을 변경하지 않는지 검증한다.
- serialized furniture unlock data가 unlock token을 정확히 한 번만 포함하는지 검증한다.
- serialized House furniture layout이 reward furniture id와 tile name을 정확히 한 번만 포함하는지 검증한다.
- visible House tilemap과 occupancy tilemap이 reload 후 정확히 하나의 restored placed furniture instance를 가지는지 검증한다.

필수 PlayMode 시나리오:
- `QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload`
- MainMenu SaveSlot flow에서 시작한다.
- reward trigger를 visible interaction/UI로 수행한다.
- reward 수령 UI 또는 placement result를 확인한다.
- furniture unlock, reward claimed, layout state를 inspect한다.
- screenshot을 `Builds/Logs/quest-interior-reward-e2e.png`로 캡처한다.
- 같은 save slot reload 후 reward claimed state와 interior state가 유지되는지 확인한다.
- duplicate reward trigger가 item, unlock token, layout record, placement tile을 중복 생성하지 않는지 확인한다.
- 가능한 경우 inventory full 또는 duplicate claim failure path가 state를 변경하지 않는지 확인한다.

필수 검증:
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

커밋:
`[TEST] Quest interior reward 저장 회귀 강화`
```

## 실행 메모

- Prompt 1은 구현보다 catalog/evidence tracking 성격이 강하다. 이미 구현이 충분하면 빠르게 끝내고 Prompt 2로 넘어간다.
- 각 prompt 완료 후에는 추천 순서를 다시 갱신해 이미 완료된 항목을 제외한다.
- visible 결과가 포함되는 prompt는 자동 테스트 통과만으로 완료 보고하지 말고 Game View/Camera screenshot과 reload state를 함께 보고한다.
