# Recommended Goal Prompts: After Resource Recipe Core Matrix

Date: 2026-05-21

이 문서는 현재 커밋 상태 기준으로 다음 goal 세션에 붙여 넣을 프롬프트를 추천 순서대로 정리한다. 이미 완료된 Resource/Recipe Town activity slice, dedicated smoke, core gameplay matrix 확장은 제외하고, 남은 문서/게이트 정합성부터 닫은 뒤 다음 기능 슬라이스로 이동한다.

## Current State

최근 완료된 기반:

- `2ec5d8675 [FEATURE][ASSET][TEST] Town resource recipe activity slice`
- `77e8b510f [NETWORK][TEST] Resource recipe activity dedicated smoke`
- `60ddcd872 [NETWORK][TEST] Core gameplay matrix coverage expansion`

현재 워크트리에는 시나리오 카탈로그 후속 작업의 RED 테스트가 남아 있다:

- `Assets/Tests/EditMode/Scenarios/StudentLifeScenarioRegistryCoverageGateTests.cs`

따라서 새 기능을 시작하기 전에 Prompt 0을 먼저 완료한다.

## Recommended Order

0. Resource/Recipe and Discovery Scenario Catalog Follow-up
1. Next Player-Facing Student Life Feature Selection
2. Career/Job Daily Plan Route Expansion
3. Relationship Activity Reward and Commitment Route
4. Knowledge/Status Activity Chain
5. Quest/Interior Reward Saved-State Regression

## Common Non-Negotiable Constraints

- 공유 워크트리로 취급한다. 관련 없는 dirty/untracked 파일을 되돌리거나 삭제하지 않는다.
- `Assets/**/*.cs`는 쉘/apply_patch로 직접 쓰지 않는다. Unity C# 파일은 MCP `script-update-or-create`를 사용한다.
- 모든 신규 동작, 회귀 게이트, QA 스크립트 변경은 실패 테스트를 먼저 만든다.
- 게임 엔티티와 진행 조건은 ScriptableObject 데이터와 전략 배열로 표현한다.
- production C#에 quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, clue, world-state, recipe, resource ID 분기를 추가하지 않는다.
- 목표성 UI는 `ObjectiveJournalPanel`이 소유한다. 상시 HUD는 `TrackedObjectiveHud` 하나만 허용한다.
- visible/player-facing 변경은 PlayMode에서 실제 플레이 흐름, Game View/Camera 스크린샷, 런타임 상태, 저장/로드 상태로 검증한다.
- `Assets/AddressableAssetsData/link.xml`과 `.meta` 삭제/변형을 보존 게이트로 확인한다.
- 완료 전에는 `check-addressables-linkxml-preserved.ps1`, `git diff --check`, `check-no-entity-id-branching.sh`, `git status --short`를 실행한다.

---

## Prompt 0: Resource/Recipe and Discovery Scenario Catalog Follow-up

아래 프롬프트를 가장 먼저 붙여 넣는다.

### Objective

Student Life scenario catalog와 coverage gate를 최신 상태로 맞춘다. Discovery dedicated smoke, Resource/Recipe PlayMode E2E, Resource/Recipe dedicated smoke, Core Matrix Resource/Recipe slice를 active scenario로 등록하고, 이미 구현된 항목이 deferred로 남아 있지 않게 한다.

### Why First

현재 Resource/Recipe 기능과 dedicated/core matrix 검증은 완료됐지만 카탈로그는 아직 `RESOURCE-RECIPE-001`, `NET-RESOURCE-RECIPE-001`를 deferred로 표시한다. 이 상태에서 새 기능을 추가하면 시나리오 게이트가 실제 검증 표면을 반영하지 못한다.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:verification-before-completion`

### Required Work

- 현재 dirty 상태의 `StudentLifeScenarioRegistryCoverageGateTests.cs` RED 테스트를 보존한다.
- `docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md`를 갱신한다.
- active row를 추가하거나 갱신한다:
  - `RESOURCE-RECIPE-001`: `Assets/Tests/PlayMode/EndToEnd/TownResourceRecipeActivityE2EScenarioTests.cs`
  - `NET-RESOURCE-RECIPE-001`: `Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1`
  - `NET-CORE-MATRIX-RESOURCE-001`: `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1`
- `NET-CORE-MATRIX-001` 설명에 route/relationship, career, completion, discovery, resource/recipe slice가 모두 포함되도록 갱신한다.
- Resource/Recipe 관련 deferred row를 제거한다.
- 아직 구현되지 않은 `JOB-CAREER-BRIDGE-001` deferred row는 유지한다.

### Required TDD

RED:

- `SCENARIO_REGISTRY_EDIT_003_CatalogMapsResourceRecipeScenariosAfterFeatureAndDedicatedSmoke`가 Resource/Recipe active mapping 부재로 실패한다.

GREEN:

- scenario registry EditMode 테스트가 통과한다.
- PowerShell/Bash scenario registry sync gate가 통과한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Scenarios
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Scenario catalog가 현재 active PlayMode/dedicated/core matrix 검증 표면과 일치한다.
- Resource/Recipe coverage가 deferred가 아니라 active로 표현된다.
- 모든 active row는 실제 파일 또는 QA runner에 매핑된다.
- 새 gameplay behavior는 추가하지 않는다.

### Commit

```text
[TEST][DOCS] Resource recipe scenario catalog update
```

---

## Prompt 1: Next Player-Facing Student Life Feature Selection

Prompt 0이 커밋된 뒤 붙여 넣는다.

### Objective

다음 player-facing Student Life 기능 슬라이스를 코드/데이터/시나리오 카탈로그 기준으로 전수조사하고, 가장 작은 구현 단위의 goal 파일을 작성한다.

### Why Second

Resource/Recipe 검증망까지 닫힌 뒤에는 바로 큰 기능을 구현하기보다, 현재 데이터와 테스트 공백을 다시 보고 다음 slice를 명확히 고르는 것이 안전하다.

### Required Skills

- `superpowers:brainstorming`
- `superpowers:writing-plans`

### Required Work

- 다음 영역을 조사한다:
  - career/job Daily Plan route와 progression 공백
  - relationship 활동 보상/commitment route 공백
  - knowledge/status 활동 chain 공백
  - quest/interior reward saved-state 회귀 위험
  - scenario catalog의 deferred row와 active coverage 불일치
- 후보별로 player value, 구현 크기, 검증 난이도, 네트워크/저장 영향, Objective Journal 영향도를 비교한다.
- 추천 후보 1개를 선택하고 goal 파일을 작성한다:

```text
docs/superpowers/goals/YYYY-MM-DD-<selected-feature>-goal.md
```

- goal 파일에는 반드시 실제 PlayMode 시나리오와 dedicated smoke 필요 여부를 포함한다.

### Required Verification

```powershell
git diff --check
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git status --short
```

### Acceptance Criteria

- 다음 구현 후보가 하나로 좁혀진다.
- 선택 근거가 code/test/data evidence 기반으로 적힌다.
- goal 파일은 TDD, PlayMode, screenshot, reload, duplicate prevention 요구를 포함한다.
- 아직 구현 코드는 변경하지 않는다.

### Commit

```text
[DOCS] Next Student Life feature goal selection
```

---

## Prompt 2: Career/Job Daily Plan Route Expansion

Prompt 1에서 다른 후보가 더 높게 나오지 않으면 이 프롬프트를 붙여 넣는다.

### Objective

Career와 Job 진행을 Daily Plan route로 더 자연스럽게 연결하는 작은 player-facing slice를 추가한다. 한 활동이 career/job 진행 상태를 변화시키고, Objective Journal이 다음 행동을 안내하며, 저장/로드와 중복 실행이 안전해야 한다.

### Why Third

Career Daily Plan route와 dedicated smoke 기반이 이미 있으므로, 확장 비용 대비 player value가 높다.

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- 한 개의 career/job route expansion만 선택한다.
- route availability, outcome, progress, Journal hint를 모두 SO 데이터/전략 배열로 연결한다.
- completed/selected/available route 상태가 Objective Journal Goals/Hints에서 충돌하지 않게 한다.
- save slot reload 후 route, career/job progress, tracked HUD 상태가 한 번만 복원되게 한다.
- network-visible progression이면 dedicated smoke를 추가한다.

### Required PlayMode Scenario

1. MainMenu SaveSlot flow에서 시작한다.
2. Town에 진입한다.
3. player-facing UI로 Daily Plan route를 확인한다.
4. career/job 관련 활동을 visible interaction으로 수행한다.
5. route availability 또는 progress 변화가 보이는지 확인한다.
6. `Tab`으로 Objective Journal을 열어 Goals/Hints row를 확인한다.
7. Game View/Camera screenshot을 캡처한다.
8. 같은 save slot을 reload한다.
9. route/progress/Journal/HUD 상태가 중복 없이 복원되는지 확인한다.
10. 같은 활동을 반복해 duplicate progress나 reward가 없는지 확인한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Careers
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.CareerJobDailyPlanRouteExpansionE2EScenarioTests
Get-ChildItem Builds/Logs/career-job-daily-plan-route-expansion-e2e*.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Career/job route expansion은 SO 데이터 기반이다.
- Objective Journal과 `TrackedObjectiveHud` ownership 규칙을 지킨다.
- PlayMode screenshot, reload, duplicate prevention이 통과한다.
- dedicated smoke 필요성이 있으면 별도 QA runner 또는 후속 goal로 명시된다.

### Commit

```text
[FEATURE][ASSET][TEST] Career job Daily Plan route expansion
```

---

## Prompt 3: Relationship Activity Reward and Commitment Route

Prompt 2 완료 뒤 붙여 넣는다.

### Objective

Relationship 활동 하나를 reward/commitment route와 연결한다. 플레이어가 Town에서 NPC와 상호작용하고, 관계 진행/commitment hint/Journal guidance가 저장/로드 후에도 중복 없이 유지되게 한다.

### Why Fourth

Relationship milestone은 이미 존재하지만, 활동 보상과 장기 commitment route의 연결이 얇다. Daily Plan과 Objective Journal 기반을 활용해 작은 수직 slice로 확장하기 좋다.

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- 한 NPC 또는 relationship definition만 대상으로 한다.
- 활동 outcome은 relationship progress, milestone, commitment hint 중 하나만 변화시킨다.
- reward/commitment state는 원자적이고 멱등이어야 한다.
- Journal row stable key 충돌과 duplicate HUD를 방지한다.
- save/reload 후 same interaction 반복 시 중복 지급이 없어야 한다.

### Required PlayMode Scenario

1. MainMenu SaveSlot flow에서 시작한다.
2. Town에 진입한다.
3. 지정 NPC에게 player movement로 접근한다.
4. visible interaction/dialogue/UI 선택으로 relationship activity를 수행한다.
5. reward 또는 commitment guidance가 표시되는지 확인한다.
6. `Tab`으로 Objective Journal을 열어 relationship guidance row를 확인한다.
7. screenshot을 캡처한다.
8. 같은 save slot reload 후 relationship/progress/Journal/HUD 상태를 확인한다.
9. 같은 interaction 반복 시 duplicate reward, duplicate milestone, duplicate Journal row가 없는지 확인한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Relationships
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.RelationshipActivityCommitmentE2EScenarioTests
Get-ChildItem Builds/Logs/relationship-activity-commitment-e2e*.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Relationship reward/commitment 진행은 SO 데이터 기반이다.
- Reward transaction은 원자적이고 멱등이다.
- Objective Journal row와 tracked HUD가 중복되지 않는다.
- PlayMode screenshot과 reload 검증이 존재한다.

### Commit

```text
[FEATURE][TEST] Relationship activity commitment route
```

---

## Prompt 4: Knowledge/Status Activity Chain

Prompt 3 완료 뒤 붙여 넣는다.

### Objective

Knowledge 또는 Status progression을 Town activity chain으로 연결한다. 활동 선택이 지식 발견 또는 상태 변화를 만들고, 다음 행동이 Objective Journal에 표시되며, day result와 reload에서 중복 없이 유지되게 한다.

### Why Fifth

Knowledge/Status는 자유 경로 성장 원칙에 직접 연결된다. 학교/등교 루트 하나가 아니라 활동, 탐험, 독학, 휴식 같은 대체 성장 경로를 제공하는 기반이 된다.

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Knowledge 또는 Status 중 하나만 선택한다.
- 한 activity chain만 만든다.
- progression condition/outcome은 SO 전략 배열로 작성한다.
- day result, Journal hint, save/reload, duplicate prevention을 포함한다.
- performance risk를 점검한다: 전체 스캔, Update polling, 반복 Instantiate/Destroy를 추가하지 않는다.

### Required PlayMode Scenario

1. MainMenu SaveSlot flow에서 시작한다.
2. Town에 진입한다.
3. activity source까지 player movement로 이동한다.
4. visible interaction/UI 선택으로 activity를 수행한다.
5. Knowledge discovery 또는 Status change가 보이는지 확인한다.
6. day result 또는 transient feedback을 확인한다.
7. `Tab`으로 Objective Journal을 열어 next-step hint를 확인한다.
8. screenshot을 캡처한다.
9. reload 후 Knowledge/Status/Journal/HUD 상태가 중복 없이 복원되는지 확인한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Knowledge
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Status
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.KnowledgeStatusActivityChainE2EScenarioTests
Get-ChildItem Builds/Logs/knowledge-status-activity-chain-e2e*.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Knowledge/Status progression은 데이터 기반이다.
- 자유 경로 성장 원칙에 맞는 대체 성장 경로가 하나 추가된다.
- Objective Journal guidance와 day result가 중복 없이 동작한다.
- PlayMode screenshot, reload, duplicate prevention이 통과한다.

### Commit

```text
[FEATURE][TEST] Knowledge status activity chain
```

---

## Prompt 5: Quest/Interior Reward Saved-State Regression

Prompt 4 완료 뒤 붙여 넣는다.

### Objective

Quest 또는 Interior reward saved-state 회귀를 강화한다. 보상 수령, 인벤토리 수용 가능 여부, 중복 수령 방지, 배치/인테리어 상태 저장/로드가 실제 player-facing flow와 테스트로 증명되어야 한다.

### Why Sixth

기능 확장 후에는 보상/인벤토리/인테리어처럼 상태 손상이 큰 영역을 다시 단단히 해야 한다. 이 prompt는 새 기능 추가보다 회귀 방지와 transaction 안정성에 집중한다.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Quest reward 또는 Interior reward 중 하나만 대상으로 한다.
- reward 지급 전 inventory capacity와 duplicate claim을 검증한다.
- 실패 시 inventory와 reward state가 변경되지 않게 한다.
- 성공 지급은 원자적으로 처리한다.
- reload 후 재호출해도 reward가 재지급되지 않게 한다.
- visible placement/interior change가 있으면 PlayMode player-facing flow와 screenshot으로 검증한다.

### Required PlayMode Scenario

1. MainMenu SaveSlot flow에서 시작한다.
2. Town 또는 relevant scene에 진입한다.
3. quest/interior reward trigger를 visible interaction으로 수행한다.
4. reward 수령 UI 또는 배치 결과를 확인한다.
5. inventory/reward state를 검사한다.
6. screenshot을 캡처한다.
7. reload 후 reward claimed state와 inventory/interior state를 확인한다.
8. 같은 reward trigger를 반복해 duplicate item, partial grant, duplicate placement가 없는지 확인한다.
9. inventory full 또는 duplicate claim 실패 path가 state를 변경하지 않는지 확인한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Inventory
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Quests
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardSavedStateE2EScenarioTests
Get-ChildItem Builds/Logs/quest-interior-reward-saved-state-e2e*.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Reward transaction은 capacity, duplicate claim, failure rollback을 검증한다.
- Save/load 후 reward 재호출이 멱등이다.
- Visible interior/placement 변경이 있으면 실제 Game View screenshot으로 검증된다.
- Objective Journal ownership과 Modern UI Style2 규약을 깨지 않는다.

### Commit

```text
[TEST][BUGFIX] Quest interior reward saved-state regression
```
