# Recommended Goal Prompts Priority Order After Core Matrix

작성일: 2026-05-21

이 문서는 현재 `Dedicated route relationship long-run`, `Dedicated core gameplay matrix freshness guard`, `core matrix evidence refresh`, `scenario catalog evidence refresh`가 완료된 뒤 이어서 실행할 goal 프롬프트를 추천 순서대로 정리한다.

추천 기준은 다음과 같다.

- 1순위: 이미 발견된 QA runner freshness 불일치를 먼저 정리해서 검증 기반을 안정화한다.
- 2순위: 전용 dedicated smoke/long-run 검증망을 실제 기능 개발 전에 신뢰 가능한 상태로 맞춘다.
- 3순위: 그 다음 player-facing Student Life 기능을 작은 vertical slice 단위로 확장한다.
- 4순위: 모든 visible/player-facing 변경은 PlayMode 사용자 흐름, Game View 또는 Camera screenshot, runtime state, saved/reload state까지 검증한다.

## 공통 실행 규칙

- 공유 워크트리로 취급한다. 관련 없는 dirty/untracked 파일은 stage, commit, delete, restore, move 하지 않는다.
- `Assets/**/*.cs`는 shell/apply_patch로 직접 쓰지 않는다. Unity C# 파일은 MCP `script-update-or-create` 또는 Unity Editor 경유로만 수정한다.
- 신규/변경 코드는 실패 테스트를 먼저 작성한다.
- production C#에 activity, route, location, NPC, reward, career, job, resource, recipe ID별 분기를 추가하지 않는다.
- 엔티티/기능 튜닝은 ScriptableObject 데이터와 strategy array 기반으로 유지한다.
- 목표성 UI는 `ObjectiveJournalPanel`과 `TrackedObjectiveHud` 소유권을 지킨다.
- Addressables generated churn이 생기면 완료 전 복구한다.

공통 마감 게이트:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

---

## 1. Dedicated Smoke Freshness Source Contract Normalization

가장 먼저 실행할 프롬프트:

```text
docs/superpowers/goals/2026-05-21-dedicated-smoke-freshness-source-contract-normalization-goal.md 진행.

목표:
Discovery Route Unlock smoke runner와 Resource/Recipe Activity smoke runner의 freshness guard를 정규화한다. managed DLL timestamp는 컴파일되는 autoplay C# source와만 비교하고, runner ps1 파일명은 실패/복구 메시지에만 남긴다.

이유:
long-run runner에서 이미 같은 계열의 문제를 수정했다. smoke runner 일부가 자기 자신의 ps1 timestamp를 DLL freshness 기준으로 삼으면, QA runner 문서/스크립트만 고쳐도 빌드 산출물이 stale로 오판될 수 있다. 기능 개발 전 smoke 검증망을 먼저 안정화한다.

필수 작업:
- `DedicatedDiscoveryRouteUnlockAutoplayContractTests.cs`에 RED contract를 추가한다.
- `DedicatedResourceRecipeActivityAutoplayContractTests.cs`에 RED contract를 추가한다.
- 두 테스트는 `$requiredSources` 블록에 runner ps1 파일명이 들어 있으면 실패해야 한다.
- runner ps1 파일명은 failure/remediation message 안에는 계속 존재해야 한다.
- 다음 script의 `$requiredSources`에서 자기 자신 ps1 항목만 제거한다.
  - `Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1`
  - `Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1`
- gameplay, scene, SO data, UI, save format, autoplay behavior는 변경하지 않는다.

필수 검증:
```powershell
tests_run EditMode testClass DedicatedDiscoveryRouteUnlockAutoplayContractTests
tests_run EditMode testClass DedicatedResourceRecipeActivityAutoplayContractTests
powershell.exe -NoProfile -Command "`$errors = `$null; `$tokens = `$null; `$null = [System.Management.Automation.Language.Parser]::ParseFile('Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1', [ref]`$tokens, [ref]`$errors); if (`$errors -and `$errors.Count -gt 0) { `$errors | Format-List; exit 1 }"
powershell.exe -NoProfile -Command "`$errors = `$null; `$tokens = `$null; `$null = [System.Management.Automation.Language.Parser]::ParseFile('Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1', [ref]`$tokens, [ref]`$errors); if (`$errors -and `$errors.Count -gt 0) { `$errors | Format-List; exit 1 }"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

완료 기준:
- 두 smoke runner가 DLL freshness를 컴파일 C# source 기준으로만 판단한다.
- runner 이름은 error/remediation output에 남아 있다.
- 관련 EditMode contract와 PowerShell parser check가 통과한다.

커밋:
`[NETWORK][TEST] Dedicated smoke freshness source contract 정규화`
```

---

## 2. Dedicated Core Gameplay Matrix Smoke Evidence Re-run

1번 완료 후 붙여 넣을 프롬프트:

```text
Dedicated core gameplay matrix smoke evidence를 최신 runner contract 기준으로 다시 실행하고 audit을 보강해줘.

목표:
Discovery/Resource smoke runner freshness 정규화 이후, core gameplay matrix가 최신 guard와 smoke runner 기준에서도 정상 통과하는지 재검증한다.

필수 작업:
- 최신 server/client build artifact와 source timestamp 상태를 확인한다.
- 필요하면 Unity build method로 Windows server/client artifact를 갱신한다.
- `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1`를 실행한다.
- route-relationship, career-daily-plan-route, daily-plan-completion, discovery-route-unlock, resource-recipe-activity 5개 slice가 모두 통과하는지 확인한다.
- 각 slice의 server/client log, screenshot, completion marker, reload marker를 확인한다.
- audit 문서를 새로 작성한다.
  - `docs/superpowers/audits/2026-05-21-dedicated-core-gameplay-matrix-smoke-rerun-after-freshness-normalization.md`
- scenario catalog가 최신 evidence를 참조해야 하면 별도 TDD cycle로 catalog test와 문서를 갱신한다.

필수 검증:
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 -RunName network-dedicated-core-gameplay-matrix-after-smoke-freshness-normalization -StartPort 8021 -WaitSeconds 80
Get-ChildItem Builds/Logs/network-dedicated-core-gameplay-matrix-after-smoke-freshness-normalization -Recurse -File | Select-Object FullName,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-scenario-registry-sync.ps1
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-scenario-registry-sync.sh
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

완료 기준:
- core matrix 5개 slice가 최신 runner 기준으로 모두 통과한다.
- 각 slice에 screenshot 2개와 완료 marker가 존재한다.
- audit에 실제 evidence path, marker, screenshot size, 실행 포트, WaitSeconds를 기록한다.

커밋:
`[DOCS][NETWORK] Dedicated core gameplay matrix smoke 증거 재검증`
```

---

## 3. Career/Job Daily Plan Route Expansion Hardening

2번 완료 후 붙여 넣을 프롬프트:

```text
Career/Job Daily Plan route expansion hardening을 진행해줘.

목표:
Career/Job Daily Plan route가 player-facing flow, Objective Journal guidance, tracked HUD, save/reload, duplicate prevention까지 안정적으로 동작하도록 기존 slice를 보강한다.

필수 작업:
- 기존 Career/Job route 관련 EditMode/PlayMode 테스트와 SO data wiring을 조사한다.
- 기능이 이미 구현되어 있으면 새 기능을 넓히기보다 failure path, duplicate prevention, reload persistence, Journal/HUD ownership을 강화한다.
- 부족한 데이터는 ScriptableObject asset과 `GameDataRegistry.asset` wiring으로 추가한다.
- career/job/route/activity ID별 production C# 분기는 금지한다.
- 보상 또는 progress claim이 있다면 인벤토리 수용 가능 여부, 중복 수령, 실패 시 rollback을 검증한다.

필수 TDD:
- RED: route outcome 중복 적용 시 progress/reward가 중복되면 실패하는 테스트.
- RED: reload 후 selected/completed/progress/Journal/HUD state가 유지되지 않으면 실패하는 테스트.
- RED: Objective Journal stable key가 중복되면 실패하는 테스트.
- GREEN: SO data와 generic strategy path만으로 통과시킨다.

필수 PlayMode 시나리오:
- MainMenu save slot flow에서 시작한다.
- Town에 진입한다.
- 실제 UI/input으로 Daily Plan route를 선택한다.
- 플레이어 이동, 상호작용, UI 선택으로 career/job 관련 route action을 수행한다.
- Tab으로 Objective Journal을 열어 다음 행동 guidance를 확인한다.
- Tracked HUD가 추적 목표 1개만 표시하는지 확인한다.
- Game View 또는 Camera screenshot을 캡처한다.
- 같은 save slot reload 후 selected/completed/progress/Journal/HUD state를 확인한다.
- 같은 action 반복 후 duplicate progress/reward/Journal row가 없는지 확인한다.

필수 검증:
```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Careers
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testClass Rootborn.Tests.PlayMode.EndToEnd.CareerJobRouteE2EScenarioTests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

완료 기준:
- 실제 player flow로 route 선택, 진행, reload가 검증된다.
- Objective Journal과 tracked HUD 소유권이 유지된다.
- duplicate progress/reward/Journal row가 없다.
- visible/UI 변경이 있으면 screenshot과 runtime/saved-state 검증이 남아 있다.

커밋:
`[FEATURE][TEST] Career Job Daily Plan route 안정화`
```

---

## 4. Relationship Activity Reward And Commitment Route

3번 완료 후 붙여 넣을 프롬프트:

```text
Relationship activity reward and commitment route를 진행해줘.

목표:
Town에서 NPC와 실제 상호작용했을 때 relationship progress, reward, commitment hint 중 하나 이상이 변하고 Objective Journal이 다음 행동을 안내하는 작은 player-facing slice를 구현 또는 강화한다.

필수 작업:
- 기존 relationship milestone, route relationship, commitment 관련 테스트와 SO data를 조사한다.
- NPC, relationship, activity, outcome은 ScriptableObject와 strategy array로만 표현한다.
- reward가 있다면 지급 전 inventory capacity와 duplicate claim을 검증하고, 실패 시 상태를 변경하지 않는다.
- Journal row stable key와 tracked HUD 중복을 방지한다.
- 반복 상호작용과 save/reload를 멱등으로 만든다.

필수 TDD:
- RED: relationship outcome 조건이 data-driven으로 적용되지 않으면 실패하는 테스트.
- RED: reward capacity/duplicate failure에서 rollback이 되지 않으면 실패하는 테스트.
- RED: reload 후 milestone/progress/Journal/HUD가 중복되면 실패하는 테스트.
- GREEN: generic relationship outcome path로 통과시킨다.

필수 PlayMode 시나리오:
- MainMenu save slot flow에서 시작한다.
- Town에서 플레이어 이동으로 대상 NPC에게 접근한다.
- 실제 interaction/dialogue/UI 선택으로 relationship activity를 수행한다.
- relationship progress 또는 commitment guidance 변화를 확인한다.
- Tab으로 Objective Journal을 열어 guidance row를 확인한다.
- Game View 또는 Camera screenshot을 캡처한다.
- save slot reload 후 relationship/progress/Journal/HUD 상태를 확인한다.
- 같은 interaction 반복 후 duplicate reward/milestone/Journal row가 없는지 확인한다.

필수 검증:
```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Relationships
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.EndToEnd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

완료 기준:
- relationship slice가 실제 player flow와 screenshot으로 검증된다.
- reward/commitment/progress가 reload 후 유지된다.
- duplicate reward/progress/Journal row가 없다.

커밋:
`[FEATURE][TEST] Relationship activity commitment route 추가`
```

---

## 5. Knowledge/Status Activity Chain

4번 완료 후 붙여 넣을 프롬프트:

```text
Knowledge/Status activity chain을 진행해줘.

목표:
Town 활동 하나가 Knowledge discovery와 Status/condition 변화를 함께 만들고, Objective Journal이 다음 성장 경로를 안내하는 작은 vertical slice를 구현 또는 강화한다.

필수 작업:
- 기존 discovery route unlock, encyclopedia, status/day result E2E를 조사한다.
- 활동 결과는 `LifeActivityDefinition`, `KnowledgeNode`, Status/Trait 관련 ScriptableObject와 strategy array로 연결한다.
- 학교/수업 단일 경로가 아니라 마을 활동 기반 대체 성장 경로로 설계한다.
- Objective Journal guidance는 Journal source에서만 노출하고 별도 persistent HUD를 만들지 않는다.
- 성능 리스크를 확인한다. `Update()` polling, 반복 전체 scan, runtime 문자열 할당 폭증, 불필요한 Instantiate/Destroy loop를 피한다.

필수 TDD:
- RED: activity outcome이 Knowledge discovery를 unlock하지 못하면 실패하는 테스트.
- RED: Status/condition delta가 저장/로드 후 중복 적용되면 실패하는 테스트.
- RED: Journal guidance stable key가 중복되면 실패하는 테스트.
- GREEN: data-driven outcome strategy로 통과시킨다.

필수 PlayMode 시나리오:
- MainMenu save slot flow에서 시작한다.
- Town에서 실제 이동/상호작용/UI 선택으로 지식/상태 관련 활동을 수행한다.
- Encyclopedia 또는 관련 UI에서 discovery 상태를 확인한다.
- day result 또는 status feedback에서 condition 변화를 확인한다.
- Tab Objective Journal에서 다음 행동 guidance를 확인한다.
- Game View 또는 Camera screenshot을 캡처한다.
- save slot reload 후 Knowledge/Status/Journal/HUD 상태를 확인한다.
- 같은 활동 반복 후 duplicate discovery/status delta/Journal row가 없는지 확인한다.

필수 검증:
```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Knowledge
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Status
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.EndToEnd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

완료 기준:
- Knowledge와 Status 변화가 실제 player-facing flow로 확인된다.
- Journal/HUD ownership과 reload persistence가 유지된다.
- duplicate discovery/status/Journal row가 없다.

커밋:
`[FEATURE][TEST] Knowledge Status activity chain 추가`
```

