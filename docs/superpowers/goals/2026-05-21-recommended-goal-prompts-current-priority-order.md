# 추천 순서 Goal 프롬프트: 현재 우선순위 실행안

작성일: 2026-05-21

이 문서는 현재 작업 흐름 기준으로 다음 goal 세션에 그대로 붙여 넣을 수 있는 프롬프트를 추천 순서대로 정리한다. 우선순위는 기반 안정화, 실제 dedicated/PlayMode 검증, 시나리오 카탈로그 정합성, 그 다음 player-facing 기능 확장 순서다.

## 추천 순서 요약

1. Dedicated Core Gameplay Matrix 마무리 및 안정화
2. Scenario Catalog Coverage Gate 정합성 회복
3. Next Player-Facing Student Life Feature Selection
4. Career/Job Daily Plan Route Expansion
5. Relationship Activity Reward and Commitment Route
6. Knowledge/Status Activity Chain
7. Quest/Interior Reward Saved-State Regression

## 공통 절대 조건

- 공유 워크트리로 취급한다. 관련 없는 dirty/untracked 파일은 삭제, 되돌리기, 이동, 스테이징, 커밋하지 않는다.
- `Assets/**/*.cs`는 shell/apply_patch로 직접 쓰지 않는다. Unity C# 파일은 MCP `script-update-or-create` 또는 Unity Editor 경유로 수정한다.
- 신규 동작, QA runner, 테스트 게이트 변경은 실패 테스트를 먼저 만든다.
- 게임 엔티티 진행 조건은 ScriptableObject 데이터와 전략 배열 기반으로 유지한다.
- production C#에 route/activity/location/NPC/quest/reward/career/job/resource/recipe/furniture ID별 분기를 추가하지 않는다.
- 목표성 UI는 `ObjectiveJournalPanel`이 소유한다. 상시 HUD는 `TrackedObjectiveHud` 하나만 허용한다.
- visible/player-facing 변경은 실제 PlayMode 사용자 흐름, Game View 또는 Camera screenshot, runtime state, 저장/로드 검증으로 확인한다.
- `Assets/AddressableAssetsData/link.xml`과 `.meta` 삭제를 방지한다.
- 완료 전 공통 게이트를 실행한다:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

---

## Prompt 1: Dedicated Core Gameplay Matrix 마무리 및 안정화

아래 프롬프트를 가장 먼저 붙여 넣는다.

```text
docs/superpowers/goals/2026-05-21-dedicated-core-gameplay-matrix-goal.md 진행.

현재 목표는 dedicated server/client Core Gameplay Matrix를 끝까지 통과시키는 것이다. 기존 runner `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1`가 route/relationship, career Daily Plan route, daily plan completion, discovery route unlock, resource/recipe activity slice를 순서대로 실행해야 한다.

현재 진행 중인 변경이 있으면 먼저 읽고 이어서 작업한다. 관련 없는 dirty 파일은 되돌리지 않는다.

필수 작업:
- matrix 실패 slice를 하나씩 TDD로 고친다.
- 실패 원인이 QA orchestration이면 runner/autoplay contract를 고친다.
- gameplay behavior 변경이 필요한 경우에만 좁게 수정하고, 반드시 실패 테스트를 먼저 둔다.
- 각 slice는 action pass, reload/observe pass, screenshot, saved/restored-state marker, cleanup evidence를 남겨야 한다.
- missing screenshot, missing completion marker, stale artifact, process leak은 fail closed로 처리한다.
- build artifact가 stale이면 Unity Editor 또는 CLI로 client/server build를 갱신하고 timestamp를 확인한다.
- `Assets/AddressableAssetsData/link.xml`과 `.meta`가 삭제되지 않았는지 확인한다.

필수 테스트:
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network`
- 실패 slice 관련 narrow EditMode contract test
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix -StartPort 7931 -WaitSeconds 120`
- `Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix -Recurse -File | Select-Object FullName,Length,LastWriteTime`
- 공통 게이트 전체

완료 기준:
- matrix 모든 slice가 통과한다.
- 각 slice log directory에 screenshot과 action/reload evidence가 존재한다.
- server/client process leak이 없다.
- stale build artifact 없이 현재 source가 반영된다.
- Addressables link.xml 보존 게이트가 통과한다.

커밋:
`[NETWORK][TEST] Dedicated core gameplay matrix 안정화`
```

---

## Prompt 2: Scenario Catalog Coverage Gate 정합성 회복

Prompt 1 커밋 후 붙여 넣는다.

```text
Student Life scenario catalog와 coverage gate를 현재 구현/검증 상태에 맞게 정리해줘.

목표:
- 최근 완료된 dedicated core gameplay matrix, discovery route unlock, resource/recipe activity 검증이 scenario catalog에 active coverage로 반영되도록 한다.
- 이미 구현된 PlayMode/dedicated/core matrix 시나리오가 deferred로 남아 있지 않게 한다.
- 아직 구현되지 않은 항목만 deferred로 유지한다.

필수 작업:
- `Assets/Tests/EditMode/Scenarios/StudentLifeScenarioRegistryCoverageGateTests.cs`의 실패 테스트를 먼저 작성하거나 기존 RED를 유지한다.
- `docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md`를 최신 상태로 갱신한다.
- Resource/Recipe, Discovery, Dedicated Core Matrix 항목을 실제 파일/runner와 매핑한다.
- active row는 반드시 실제 테스트 파일 또는 QA runner를 가리켜야 한다.
- gameplay behavior는 변경하지 않는다. 문서/게이트 정합성 작업으로 제한한다.

필수 테스트:
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.Scenarios`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1`
- `& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh`
- 공통 게이트 전체

완료 기준:
- catalog와 coverage gate가 실제 구현된 PlayMode/dedicated 검증 표면과 일치한다.
- 구현되지 않은 기능이 active로 표시되지 않는다.
- 구현된 기능이 deferred로 남지 않는다.

커밋:
`[TEST][DOCS] 시나리오 카탈로그 정합성 갱신`
```

---

## Prompt 3: Next Player-Facing Student Life Feature Selection

Prompt 2 커밋 후 붙여 넣는다.

```text
다음 player-facing Student Life 기능 개발 후보를 전수조사하고, 추천 1순위 goal 파일을 작성해줘.

목표:
- 현재 코드, 데이터, 테스트, scenario catalog 기준으로 다음 기능 개발 후보를 비교한다.
- 바로 구현하지 말고, 가장 적합한 다음 slice의 goal prompt를 MD 파일로 작성한다.

후보:
- Career/Job Daily Plan route expansion
- Relationship activity reward and commitment route
- Knowledge/Status activity chain
- Quest/Interior reward saved-state regression
- 다른 catalog deferred 항목 중 현재 기반에 가장 잘 붙는 항목

평가 기준:
- player value
- 구현 범위와 리스크
- 기존 SO 데이터/전략 배열 재사용 가능성
- Objective Journal 영향
- save/reload와 duplicate prevention 난이도
- PlayMode/dedicated 검증 가능성
- 성능 리스크

필수 산출물:
- `docs/superpowers/goals/2026-05-21-selected-student-life-feature-goal.md` 형식으로 실제 선택 기능명을 반영한 파일
- 문서에는 Objective, Required Work, Required TDD, Required PlayMode Scenario, Required Verification, Acceptance Criteria, Commit Message를 포함한다.
- visible/player-facing 흐름은 실제 사용자 입력/이동/UI/트리거와 screenshot 검증을 포함한다.

필수 검증:
- `git diff --check`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1`
- `git status --short`

완료 기준:
- 다음 구현 목표가 하나로 좁혀진다.
- 선택 이유가 code/test/data evidence 기반으로 설명된다.
- 아직 구현 코드는 변경하지 않는다.

커밋:
`[DOCS] 다음 Student Life 기능 goal 선정`
```

---

## Prompt 4: Career/Job Daily Plan Route Expansion

Prompt 3에서 더 높은 우선순위 후보가 나오지 않으면 붙여 넣는다.

```text
Career/Job Daily Plan route expansion을 TDD로 구현해줘.

목표:
- Career 또는 Job 진행을 Daily Plan route와 연결하는 작은 player-facing slice를 추가한다.
- 플레이어가 Town에서 route를 선택하고 활동을 수행하면 career/job progress 또는 hint가 바뀌고, Objective Journal이 다음 행동을 안내해야 한다.
- 저장/로드 후 route/progress/Journal/HUD 상태가 중복 없이 복원되어야 한다.

필수 작업:
- 한 개의 career/job route expansion만 선택한다.
- route availability, outcome, progress, Journal hint는 SO 데이터와 전략 배열 기반으로 연결한다.
- production C#에 career/job/route/activity ID별 분기를 추가하지 않는다.
- route 완료 상태, selected 상태, recommendation 상태가 Objective Journal source와 충돌하지 않게 한다.
- 저장/로드 후 같은 활동 반복 시 duplicate progress/reward가 없어야 한다.

필수 TDD:
- route availability가 데이터 조건을 따르지 않으면 실패하는 EditMode test.
- route outcome이 career/job progress를 정확히 한 번만 반영하는 EditMode test.
- reload 후 selected/completed/progress 상태가 중복 없이 복원되는 test.
- Objective Journal row stable key와 tracked HUD 중복 방지 test.

필수 PlayMode 시나리오:
- MainMenu SaveSlot flow에서 시작한다.
- Town 진입 후 실제 UI로 Daily Plan route를 확인한다.
- 플레이어 이동/상호작용/UI 선택으로 career/job 관련 활동을 수행한다.
- visible feedback과 Objective Journal row를 확인한다.
- Game View 또는 Camera screenshot을 캡처한다.
- 같은 save slot reload 후 route/progress/Journal/HUD 상태를 확인한다.
- 같은 활동 반복 시 duplicate progress/reward가 없는지 확인한다.

필수 검증:
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife`
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.Careers`
- `tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.CareerJobDailyPlanRouteExpansionE2EScenarioTests`
- `Get-ChildItem Builds/Logs/career-job-daily-plan-route-expansion-e2e*.png | Select-Object Name,Length,LastWriteTime`
- 공통 게이트 전체

커밋:
`[FEATURE][ASSET][TEST] Career Job Daily Plan route 확장`
```

---

## Prompt 5: Relationship Activity Reward and Commitment Route

Prompt 4 커밋 후 붙여 넣는다.

```text
Relationship activity reward and commitment route를 TDD로 구현해줘.

목표:
- Town에서 NPC와 실제 상호작용하여 relationship progress, reward, commitment hint 중 하나가 변화하는 작은 slice를 추가한다.
- 변화는 Objective Journal에서 다음 행동으로 안내되고 저장/로드 후 중복 없이 유지되어야 한다.

필수 작업:
- NPC 또는 relationship definition 하나만 대상으로 한다.
- outcome은 SO 데이터와 전략 배열로 표현한다.
- reward가 있다면 지급 전 capacity와 duplicate claim을 검증하고 실패 시 상태를 변경하지 않는다.
- Journal row stable key와 tracked HUD 중복을 방지한다.
- 같은 interaction 반복 시 duplicate reward/milestone/progress가 없어야 한다.

필수 TDD:
- relationship activity outcome이 데이터 조건을 따르는 EditMode test.
- reward/commitment state가 멱등인 test.
- inventory/reward 실패 path에서 rollback되는 test.
- Journal/HUD 중복 방지 test.

필수 PlayMode 시나리오:
- MainMenu SaveSlot flow에서 시작한다.
- Town에서 플레이어 이동으로 지정 NPC에게 접근한다.
- visible interaction/dialogue/UI 선택으로 relationship activity를 수행한다.
- reward 또는 commitment guidance를 확인한다.
- Tab으로 Objective Journal을 열어 relationship guidance row를 확인한다.
- screenshot을 캡처한다.
- 같은 save slot reload 후 relationship/progress/Journal/HUD 상태를 확인한다.
- 같은 interaction 반복 시 duplicate reward/milestone/Journal row가 없는지 확인한다.

필수 검증:
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.Relationships`
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives`
- `tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.RelationshipActivityCommitmentE2EScenarioTests`
- `Get-ChildItem Builds/Logs/relationship-activity-commitment-e2e*.png | Select-Object Name,Length,LastWriteTime`
- 공통 게이트 전체

커밋:
`[FEATURE][TEST] Relationship activity commitment route 추가`
```

---

## Prompt 6: Knowledge/Status Activity Chain

Prompt 5 커밋 후 붙여 넣는다.

```text
Knowledge 또는 Status activity chain을 TDD로 구현해줘.

목표:
- Town 활동 하나를 통해 Knowledge discovery 또는 Status change가 발생하고, 다음 행동이 Objective Journal에 표시되는 작은 slice를 추가한다.
- 자유 경로 성장 원칙에 맞게 학교 수업 단일 루트가 아닌 대체 성장 경로로 작동해야 한다.

필수 작업:
- Knowledge 또는 Status 중 하나만 선택한다.
- activity chain 하나만 추가한다.
- progression condition/outcome은 SO 데이터와 전략 배열로 작성한다.
- day result 또는 transient feedback을 통해 플레이어가 변화를 볼 수 있게 한다.
- 저장/로드 후 Knowledge/Status/Journal/HUD 상태가 중복 없이 복원되어야 한다.
- 전체 스캔, Update polling, 반복 Instantiate/Destroy를 추가하지 않는다.

필수 TDD:
- activity condition과 outcome이 데이터 기반으로 평가되는 EditMode test.
- Status/Knowledge 변화가 정확히 한 번만 반영되는 test.
- reload 후 중복 progression이 없는 test.
- Journal hint stable key 중복 방지 test.

필수 PlayMode 시나리오:
- MainMenu SaveSlot flow에서 시작한다.
- Town에서 activity source까지 플레이어 이동으로 접근한다.
- visible interaction/UI 선택으로 activity를 수행한다.
- Knowledge discovery 또는 Status change feedback을 확인한다.
- Tab으로 Objective Journal을 열어 next-step hint를 확인한다.
- screenshot을 캡처한다.
- reload 후 Knowledge/Status/Journal/HUD 상태를 확인한다.

필수 검증:
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.Knowledge`
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.Status`
- `tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.KnowledgeStatusActivityChainE2EScenarioTests`
- `Get-ChildItem Builds/Logs/knowledge-status-activity-chain-e2e*.png | Select-Object Name,Length,LastWriteTime`
- 공통 게이트 전체

커밋:
`[FEATURE][TEST] Knowledge Status activity chain 추가`
```

---

## Prompt 7: Quest/Interior Reward Saved-State Regression

Prompt 6 커밋 후 붙여 넣는다.

```text
Quest 또는 Interior reward saved-state regression을 TDD로 강화해줘.

목표:
- 보상 수령, 인벤토리 수용 가능 여부, 중복 수령 방지, 저장/로드 후 재호출 멱등성을 실제 player-facing flow와 테스트로 증명한다.
- visible interior/placement 변화가 있다면 Game View screenshot으로 검증한다.

필수 작업:
- Quest reward 또는 Interior reward 중 하나만 대상으로 한다.
- reward 지급 전에 inventory capacity와 duplicate claim을 검증한다.
- 실패 시 inventory와 reward state를 변경하지 않는다.
- 성공 지급은 원자적이고 멱등이어야 한다.
- reload 후 같은 reward trigger를 재호출해도 duplicate item, partial grant, duplicate placement가 없어야 한다.
- Objective Journal ownership과 Modern UI Style2 규약을 깨지 않는다.

필수 TDD:
- capacity 부족 시 state rollback test.
- duplicate claim 시 state unchanged test.
- successful claim이 exactly-once인 test.
- reload 후 reward 재호출이 멱등인 test.

필수 PlayMode 시나리오:
- MainMenu SaveSlot flow에서 시작한다.
- Town 또는 relevant scene에서 reward trigger까지 실제 이동/상호작용한다.
- reward 수령 UI 또는 배치 결과를 확인한다.
- inventory/reward state를 검사한다.
- visible 변화가 있으면 screenshot을 캡처한다.
- reload 후 claimed state와 inventory/interior state를 확인한다.
- 같은 reward trigger 반복 시 duplicate 지급/배치가 없는지 확인한다.
- inventory full 또는 duplicate claim 실패 path가 state를 변경하지 않는지 확인한다.

필수 검증:
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.Inventory`
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.Quests`
- `tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardSavedStateE2EScenarioTests`
- `Get-ChildItem Builds/Logs/quest-interior-reward-saved-state-e2e*.png | Select-Object Name,Length,LastWriteTime`
- 공통 게이트 전체

커밋:
`[TEST][BUGFIX] Quest Interior reward saved-state 회귀 강화`
```

## 운영 메모

- Prompt 1은 현재 진행 중인 안정화 작업이므로 새 기능보다 먼저 끝내야 한다.
- Prompt 2는 구현 코드보다 문서/게이트 정합성 작업이다. 다음 기능 개발 전에 반드시 정리한다.
- Prompt 3은 다음 기능을 고르는 설계용 프롬프트다. Prompt 4~7은 Prompt 3 결과가 동일할 때의 기본 추천 순서다.
- 각 prompt는 독립 커밋 단위로 끝낸다.
