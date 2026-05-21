# 추천 순서별 Goal 프롬프트: 현재 안정화 우선 순서

작성일: 2026-05-21

이 문서는 현재 ROOTBORN 작업 흐름에서 다음에 실행할 goal 프롬프트를 추천 순서대로 정리한다. 우선순위는 기반 안정화, 회귀 방지, 실제 PlayMode 검증, 그 다음 플레이어-facing 기능 확장 순서다.

## 추천 순서

1. Objective Journal Legacy HUD Final Ownership Audit
2. Dedicated Core Gameplay Matrix
3. House Upgrade Interior Loop QA Runner
4. Daily Plan Result Feedback and Route Completion Slice

Prompt 2 이후는 Prompt 1이 완료, 커밋, 또는 명확한 blocker로 중단 기록된 뒤 진행한다. 각 prompt는 한 사이클 단위로 끝내고, 테스트와 실제 플레이 검증을 통과한 뒤 별도 커밋한다.

## 공통 필수 조건

- 공유 작업 트리로 취급하고, 관련 없는 dirty/untracked 파일은 되돌리거나 삭제하지 않는다.
- `Assets/**/*.cs`는 shell/apply_patch로 직접 쓰지 않는다. Unity MCP script tool을 사용한다.
- 신규 동작, 회귀 수정, 저장/로드 변경은 실패 테스트를 먼저 작성한다.
- 게임 엔티티와 진행 조건은 ScriptableObject 데이터와 전략 배열 기반으로 유지한다.
- production C#에 quest, route, activity, location, NPC, reward, furniture, career, job, room, upgrade 같은 엔티티 ID별 분기를 추가하지 않는다.
- 목표성 UI는 `ObjectiveJournalPanel`이 소유하고, 상시 HUD는 `TrackedObjectiveHud` 하나만 허용한다.
- visible/player-facing 변경은 실제 PlayMode 사용자 흐름, Game View 또는 Camera screenshot, runtime state, 저장/로드 검증으로 확인한다.
- `Assets/AddressableAssetsData/link.xml` 및 `.meta` 보존을 확인한다.
- 완료 전 `check-addressables-linkxml-preserved.ps1`, `git diff --check`, `check-no-entity-id-branching.sh`, `git status --short`를 실행한다.

---

## Prompt 1: Objective Journal Legacy HUD Final Ownership Audit

### Objective

목표, 퀘스트, 캠페인, 진로 힌트, 관계, 하우징, route guidance 같은 persistent objective-like UI가 `ObjectiveJournalPanel`과 정확히 하나의 `TrackedObjectiveHud`를 통해서만 표시되도록 최종 소유권 감사를 수행한다.

### Why First

Daily Plan, Relationship, Career/Job, House loop가 Objective Journal에 연결된 상태다. 다음 기능을 더 얹기 전에 legacy HUD나 별도 Canvas가 다시 생기는 회귀를 차단해야 한다.

### Start From

```text
docs/superpowers/goals/2026-05-21-objective-journal-legacy-hud-final-ownership-audit-goal.md
docs/superpowers/specs/2026-05-19-objective-journal-legacy-hud-migration-design.md
docs/superpowers/specs/2026-05-19-objective-journal-integrated-tracking-design.md
```

### Required Work

- runtime installer, factory, panel 생성 경로에서 persistent objective-like HUD 생성을 감사한다.
- 허용 대상은 `ObjectiveJournalPanel`과 하나의 `TrackedObjectiveHud`로 제한한다.
- transient result, reward, dialogue, detail panel은 persistent objective guidance가 아닐 때만 허용한다.
- Objective Journal source stable key 중복 방지 테스트를 강화한다.
- Tab으로 Journal을 열고 track/reload까지 확인하는 E2E를 추가하거나 강화한다.

### Required TDD

RED:

- forbidden legacy HUD/widget이 runtime path에서 생성되면 실패하는 테스트.
- player-facing scene에 `TrackedObjectiveHud`가 2개 이상 있으면 실패하는 테스트.
- Journal source stable key가 중복되면 실패하는 테스트.

GREEN:

- persistent objective guidance는 Journal과 tracked HUD 하나만 소유한다.
- 기존 transient feedback panel은 유지된다.
- reload 후 Journal row, stable key, HUD가 중복되지 않는다.

### Required PlayMode Scenario

```text
Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalOwnershipE2EScenarioTests.OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud
```

시나리오는 MainMenu SaveSlot flow에서 시작해 Town 진입, 최소 2개 guidance source 상태 생성, Tab으로 Objective Journal 열기, item track, progress action, screenshot, 같은 save slot reload, duplicate HUD/row/stable key 없음까지 검증한다.

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

### Commit

```text
[REFACTOR][UI][TEST] Objective Journal HUD ownership 최종 감사
```

---

## Prompt 2: Dedicated Core Gameplay Matrix

### Objective

dedicated server/client 검증을 route/relationship 기반에서 core gameplay matrix로 확장한다. route/relationship, career/job route, quest/interior reward 또는 House loop, Objective Journal tracking, screenshot, reload, cleanup evidence를 포함한다.

### Why Second

Editor PlayMode가 통과해도 build artifact, dedicated process, server/client log, reload persistence에서 별도 문제가 생길 수 있다. 소유권 게이트를 먼저 고정한 뒤 네트워크 검증 범위를 넓힌다.

### Start From

```text
docs/superpowers/goals/2026-05-21-dedicated-core-gameplay-matrix-goal.md
docs/superpowers/goals/2026-05-21-dedicated-client-autoplay-evidence-contract-goal.md
Scripts/qa/
```

### Required Work

- opt-in runner를 추가하거나 확장한다: `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1`.
- 각 slice는 isolated run name, save slot, port, screenshot, server/client logs를 사용한다.
- route/relationship, career/job route, quest/interior reward 또는 House loop를 최소 3개 slice로 검증한다.
- 각 slice는 action pass와 reload pass를 분리하고 saved/restored-state marker를 남긴다.
- 실패 시 server/client process cleanup을 검증한다.

### Required TDD

RED:

- matrix slice가 readiness, screenshot, completion marker, saved-state, restored-state, cleanup evidence 중 하나라도 빠지면 실패하는 contract test.
- 실패한 cycle 뒤 launched process가 남으면 실패하는 cleanup test.
- duplicate Journal/HUD/route/reward/placement marker가 있으면 실패하는 parser test.

GREEN:

- 모든 slice가 readiness, action, screenshot, save, reload, cleanup evidence를 남긴다.
- missing screenshot 또는 missing marker는 fail closed 처리된다.
- cleanup 후 server/client process leak이 없다.

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

### Commit

```text
[NETWORK][TEST] Dedicated core gameplay matrix 추가
```

---

## Prompt 3: House Upgrade Interior Loop QA Runner

### Objective

House upgrade/interior E2E를 반복 실행 가능한 QA runner로 고정한다. stage-1 upgrade, room preset, furniture select/rotate/place/move/save, screenshot, serialized state, reload, overlay cleanup을 한 명령으로 검증한다.

### Why Third

House loop는 tilemap, placement, UI, save/load, screenshot이 모두 걸린 고위험 시각 검증 지점이다. 이미 E2E가 있다면 runner contract를 강화해 이후 주기마다 쉽게 재검증하도록 만든다.

### Start From

```text
docs/superpowers/goals/2026-05-21-house-upgrade-interior-loop-e2e-hardening-goal.md
docs/superpowers/goals/2026-05-21-house-upgrade-interior-loop-qa-runner-goal.md
Assets/Tests/PlayMode/EndToEnd/HouseUpgradeInteriorLoopE2EScenarioTests.cs
Scripts/qa/run-house-upgrade-interior-loop.ps1
```

### Required Work

- `Scripts/qa/run-house-upgrade-interior-loop.ps1`가 strict E2E method를 실행하고 evidence marker를 검사하도록 한다.
- screenshot path, byte length, stage, preset, furniture layout, runtime tilemap, overlay cleanup, Addressables marker를 출력한다.
- script contract test가 있으면 runner가 target method, screenshot, saved-state marker, tilemap marker, Addressables preservation을 요구하는지 검증한다.
- QA runner는 missing screenshot, zero-byte screenshot, missing marker를 fail closed 처리한다.

### Required TDD

RED:

- runner가 없거나 strict E2E method를 참조하지 않으면 실패하는 contract test.
- screenshot evidence 또는 Addressables preservation check가 빠지면 실패하는 test.
- saved-state/runtime tilemap/overlay cleanup marker가 빠지면 실패하는 test.

GREEN:

- runner가 strict House E2E를 실행하고 evidence summary를 출력한다.
- runner와 underlying E2E가 모두 통과한다.
- Addressables `link.xml`과 `.meta`가 보존된다.

### Required Verification

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-house-upgrade-interior-loop.ps1 -RunName house-upgrade-interior-loop
Get-ChildItem Builds/Logs/house-upgrade-interior-loop-e2e.png | Select-Object Name,Length,LastWriteTime
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Housing
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeInteriorLoopE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Commit

```text
[TEST][CHORE] House upgrade interior QA runner 강화
```

---

## Prompt 4: Daily Plan Result Feedback and Route Completion Slice

### Objective

Daily Plan route completion 결과가 플레이어에게 명확히 보이고, Objective Journal row와 tracked HUD가 갱신되며, selected/completed route state가 저장/로드 후 중복 없이 복원되도록 작은 vertical slice를 구현한다.

### Why Fourth

기반 ownership, dedicated matrix, House QA runner를 고정한 뒤에는 플레이어 체감 기능을 작게 확장하는 것이 적절하다. Daily Plan 완료 피드백은 기존 route, relationship, career/job, Objective Journal 기반 위에서 가치가 크고 범위를 작게 유지할 수 있다.

### Start From

```text
docs/superpowers/goals/2026-05-21-daily-plan-route-completion-result-feedback-goal.md
docs/superpowers/specs/2026-05-20-daily-plan-route-selection-design.md
Assets/Scripts/Game/StudentLife/
Assets/Scripts/UI/Objectives/
Assets/Tests/EditMode/StudentLife/
Assets/Tests/PlayMode/EndToEnd/
```

### Required Work

- route completion 결과를 기존 transient result panel 또는 기존 UI pattern으로 표시한다.
- 별도 persistent route HUD를 만들지 않는다.
- Objective Journal source가 selected, completed, next recommendation 상태를 소유한다.
- completion state는 정확히 한 번 저장되고 reload 후 reward/delta가 재적용되지 않아야 한다.
- route 동작은 기존 ScriptableObject 정의와 strategy array 기반으로 유지한다.

### Required TDD

RED:

- completed route state가 정확히 한 번 저장되지 않으면 실패하는 test.
- repeated result interaction이 route outcome을 재적용하면 실패하는 test.
- route completion guidance가 `ObjectiveJournalPanel` 또는 `TrackedObjectiveHud` 밖에 persistent로 표시되면 실패하는 test.
- E2E에 visible result, Journal update, screenshot, saved-state, reload assertion이 빠지면 실패하는 contract/source test.

GREEN:

- completion feedback이 transient UI로 보인다.
- Objective Journal row와 tracked HUD가 owned surface에서 갱신된다.
- reload 후 selected/completed route state가 중복 없이 복원된다.

### Required PlayMode Scenario

```text
Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteCompletionE2EScenarioTests.DAILY_PLAN_ROUTE_E2E_001_CompleteResultReload
```

시나리오는 MainMenu SaveSlot flow, Town 진입, Daily Plan route 선택, player-facing completion action, transient result 확인, Tab으로 Journal 열기, screenshot, serialized state inspect, same save slot reload, duplicate row/HUD/outcome 없음까지 검증한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteCompletionE2EScenarioTests.DAILY_PLAN_ROUTE_E2E_001_CompleteResultReload
Get-ChildItem Builds/Logs/daily-plan-route-completion-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Commit

```text
[FEATURE][UI][TEST] Daily Plan route result feedback 추가
```
