# 추천 순서별 Goal 프롬프트

Date: 2026-05-21

이 문서는 현재 워크스페이스 상태를 기준으로 다음 goal 세션에 순서대로 붙여 넣을 프롬프트를 정리한다. 우선순위는 `진행 중인 작업 안전 마무리 -> dedicated/저장상태 회귀 하드닝 -> 다음 기능 슬라이스`다.

## 현재 전제

- 현재 브랜치: `town-playable-baseline-recovery`
- 현재 미커밋 WIP는 Career/Job Daily Plan route 확장 작업이다.
- 관련 목표 문서: `docs/superpowers/goals/2026-05-21-career-job-daily-plan-route-expansion-goal.md`
- 현재 WIP를 커밋하기 전에는 새 기능 구현으로 넘어가지 않는다.
- `Assets/**/*.cs`는 외부 파일 쓰기 금지. Unity MCP `script-update-or-create`를 사용한다.
- visible/player-facing 변경은 PlayMode 실제 사용자 흐름, Game View/Camera screenshot, save/reload 검증 전에는 완료로 보지 않는다.
- 목표성 UI는 `ObjectiveJournalPanel`이 소유하며, 상시 HUD는 `TrackedObjectiveHud` 하나만 허용한다.
- gameplay/entity 로직은 ScriptableObject 데이터와 전략 배열로 표현하고, production C#에 entity ID 분기를 추가하지 않는다.

---

## 1순위: 현재 Career/Job Daily Plan Route WIP 마무리

```text
docs/superpowers/goals/2026-05-21-career-job-daily-plan-route-expansion-goal.md 진행을 마무리해줘.

현재 워크스페이스에 남아 있는 Career/Job Daily Plan route 확장 WIP만 대상으로 한다. 새 기능으로 확장하지 말고, 이미 구현된 WIP의 검증, Addressables link.xml 보존, diff 확인, no entity ID branching 게이트, 선택적 상태 점검, 커밋까지 완료한다.

필수 확인:
- git status --short로 의도한 파일만 남았는지 확인
- CareerJobRoute PlayMode E2E screenshot `Builds/Logs/career-job-route-e2e.png`가 존재하고 non-zero인지 확인
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife`
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives`
- `tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerJobRouteE2EScenarioTests.CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload`
- 기존 보정 테스트도 깨지지 않는지 확인:
  - `DailyPlanRouteSelectionE2EScenarioTests.DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload`
  - `PartTimeWorkGrowthE2EScenarioTests.WORK_E2E_001_006_PlayPartTimeWorkResultReloadAndDedupe`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1`
- `git diff --check`
- `& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh`
- `git status --short`

완료 기준:
- `WorkshopShiftRoute.asset`가 Corner Store part-time work 완료 후 열리는 데이터 기반 requirement를 갖는다.
- 실제 PlayMode에서 work -> Daily Plan route -> Objective Journal -> Tracked HUD -> screenshot -> reload 흐름이 검증된다.
- 반복 work/route 선택/reload가 result log, work log, Journal row, HUD를 중복 생성하지 않는다.
- 관련 파일만 stage하고 커밋한다.

커밋 메시지:
`[FEATURE][ASSET][TEST] Career job Daily Plan route 확장`
```

---

## 2순위: Dedicated Career Daily Plan Route Smoke

```text
docs/superpowers/goals/2026-05-21-dedicated-career-daily-plan-route-smoke-goal.md 진행해줘.

Career/Job route가 Editor PlayMode에서 통과한 뒤, dedicated server/client 프로세스에서도 player-facing 입력/이동/UI 선택으로 career route unlock과 reload persistence가 검증되도록 smoke runner를 추가한다.

필수 조건:
- host-mode나 내부 domain mutation으로 대체하지 않는다.
- dedicated autoplay는 player movement, interact input, visible UI click을 사용한다.
- action pass와 observe-only reload pass를 분리한다.
- screenshot, completion marker, career snapshot, DailyPlan snapshot이 없으면 실패한다.
- `Assets/AddressableAssetsData/link.xml` 삭제/변형을 보존 게이트로 확인한다.

필수 검증:
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.Network`
- `tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1 -RunName network-dedicated-career-daily-plan-route-e2e -Port 7893 -WaitSeconds 100`
- `Get-ChildItem Builds/Logs/network-dedicated-career-daily-plan-route-e2e -File | Select-Object Name,Length,LastWriteTime`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1`
- `git diff --check`
- `& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh`
- `git status --short`

커밋 메시지:
`[NETWORK][TEST] Dedicated career Daily Plan route smoke 추가`
```

---

## 3순위: Career/Job Daily Plan Saved-State Regression Hardening

```text
docs/superpowers/goals/2026-05-21-career-job-daily-plan-saved-state-regression-goal.md 진행해줘.

Career/Job Daily Plan 기능의 저장상태 회귀를 강화한다. 새 기능을 넓히지 말고, career hint, workshop/job route, selected route, Objective Journal row, Tracked HUD, save/reload exact-once 보장을 하드닝한다.

필수 조건:
- RED 테스트를 먼저 작성하거나 기존 테스트를 실패하도록 강화한다.
- route/hint/workshop/job 상태는 SO 데이터와 requirement/outcome으로 유지한다.
- Objective Journal rows가 Hints와 Goals에 중복 stable key로 나타나지 않아야 한다.
- reload 후 career/job progress, DailyPlan route, tracked state가 정확히 한 번만 복원되어야 한다.

필수 검증:
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife`
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives`
- `tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerDailyPlanRouteE2EScenarioTests.CAREER_DAILY_PLAN_E2E_001_SelectInterestUnlockPracticeRouteReload`
- `Get-ChildItem Builds/Logs/career-daily-plan-route-e2e.png | Select-Object Name,Length,LastWriteTime`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1`
- `git diff --check`
- `& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh`
- `git status --short`

커밋 메시지:
`[TEST] Career job Daily Plan 저장상태 회귀 강화`
```

---

## 4순위: Second NPC Relationship Saved-State Regression Hardening

```text
docs/superpowers/goals/2026-05-21-second-npc-relationship-saved-state-regression-goal.md 진행해줘.

기존 Teacher/Second NPC relationship milestone slice를 새 기능 확장 전에 회귀 하드닝한다. 목표는 relationship progress, milestone completion, Objective Journal guidance, Tracked HUD, day-result replay, save/reload exact-once 보장이다.

필수 조건:
- Teacher NPC/help action은 실제 movement, interact key, visible UI 흐름으로 검증한다.
- relationship/milestone 저장 파일은 containment가 아니라 exact-once로 검증한다.
- 반복 day-result interaction이 milestone save data나 relationship value를 재적용하지 않아야 한다.
- Objective Journal relationship/milestone guidance는 중복 row/HUD를 만들지 않는다.

필수 검증:
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife`
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives`
- `tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.SecondNpcRelationshipMilestoneE2EScenarioTests.NPC_RELATIONSHIP_E2E_002_SecondNpcMilestoneReload`
- `Get-ChildItem Builds/Logs/npc-relationship-second-slice-e2e.png | Select-Object Name,Length,LastWriteTime`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1`
- `git diff --check`
- `& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh`
- `git status --short`

커밋 메시지:
`[TEST] Second NPC relationship 저장상태 회귀 강화`
```

---

## 5순위: Quest/Interior Reward Saved-State Regression Hardening

```text
docs/superpowers/goals/2026-05-21-quest-interior-reward-saved-state-regression-goal.md 진행해줘.

Quest -> Interior reward slice의 보상 트랜잭션과 저장상태를 하드닝한다. duplicate reward claim, inventory/unlock token, furniture placement, occupancy cell, saved layout record가 reload/replay 후에도 exact-once인지 검증한다.

필수 조건:
- reward 지급 전 inventory 수용 가능 여부와 duplicate claim을 검증한다.
- 실패 시 inventory와 reward state를 변경하지 않는다.
- 성공 지급은 atomic하고 save/load 후 재호출해도 멱등이어야 한다.
- visible House/interior 결과는 PlayMode Game View screenshot과 runtime tilemap/occupancy/save file 검증을 모두 포함한다.

필수 검증:
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors`
- `tests_run EditMode testNamespace Rootborn.Tests.EditMode.Quests`
- `tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardE2EScenarioTests.QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload`
- `Get-ChildItem Builds/Logs/quest-interior-reward-e2e.png | Select-Object Name,Length,LastWriteTime`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1`
- `git diff --check`
- `& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh`
- `git status --short`

커밋 메시지:
`[TEST] Quest interior reward 저장상태 회귀 강화`
```

---

## 6순위: Relationship Activity Reward and Commitment Route

```text
Relationship activity reward and commitment route goal을 새로 작성하고 진행해줘.

목표:
기존 relationship/milestone 저장상태 하드닝이 끝난 뒤, NPC relationship activity 하나를 reward 또는 commitment route와 연결하는 작은 player-facing slice를 추가한다. Town에서 실제 NPC 접근, visible interaction/dialogue/UI 선택, relationship progress 또는 commitment hint 변화, Objective Journal guidance, save/reload, duplicate prevention까지 검증한다.

필수 설계/구현 조건:
- 대상 NPC/relationship definition은 하나만 고른다.
- relationship activity outcome은 SO 데이터와 전략 배열로 표현한다.
- production C#에 NPC ID, relationship ID, milestone ID, route ID 분기를 추가하지 않는다.
- Objective Journal guidance는 `ObjectiveJournalPanel`이 소유하고, 상시 HUD는 `TrackedObjectiveHud` 하나만 허용한다.
- reward가 있다면 지급 전 capacity/duplicate 검증, 실패 rollback, 성공 atomic/idempotent 보장을 테스트한다.

필수 PlayMode 시나리오:
1. MainMenu SaveSlot flow에서 시작
2. Town 진입
3. 지정 NPC까지 player movement로 접근
4. visible interaction/dialogue/UI 선택으로 relationship activity 수행
5. reward 또는 commitment guidance 표시 확인
6. `Tab`으로 Objective Journal 열기
7. relationship guidance row와 tracked HUD exact-one 확인
8. screenshot 캡처
9. 같은 save slot reload
10. relationship/progress/Journal/HUD 상태 exact-once 확인
11. 같은 interaction 반복 후 duplicate reward/milestone/Journal row/HUD 없음 확인

필수 산출물:
- `docs/superpowers/goals/YYYY-MM-DD-relationship-activity-commitment-route-goal.md`
- RED/GREEN EditMode tests
- PlayMode E2E test
- screenshot evidence

커밋 메시지:
`[FEATURE][TEST] Relationship activity commitment route 추가`
```

---

## 7순위: Knowledge/Status Activity Chain

```text
Knowledge/Status activity chain goal을 새로 작성하고 진행해줘.

목표:
자유 경로 성장 원칙에 맞춰 Knowledge 또는 Status progression 하나를 Town activity chain과 연결한다. 활동 선택이 지식 발견 또는 상태 변화를 만들고, 다음 행동이 Objective Journal에 표시되며, day result와 reload에서 중복 없이 유지되도록 한다.

필수 설계/구현 조건:
- Knowledge 또는 Status 중 하나만 고른다.
- activity chain은 하나만 추가한다.
- progression condition/outcome은 SO 데이터와 전략 배열로 작성한다.
- school-only 선형 성장으로 만들지 말고, 마을 활동/탐험/도움/독학/휴식 등 대체 성장 경로의 기반으로 확장 가능해야 한다.
- Update polling, 전체 스캔, 반복 Instantiate/Destroy 같은 성능 리스크를 추가하지 않는다.

필수 PlayMode 시나리오:
1. MainMenu SaveSlot flow에서 시작
2. Town 진입
3. activity source까지 player movement로 이동
4. visible interaction/UI 선택으로 activity 수행
5. Knowledge discovery 또는 Status change 확인
6. day result 또는 transient feedback 확인
7. `Tab`으로 Objective Journal 열기
8. next-step hint 확인
9. screenshot 캡처
10. reload 후 Knowledge/Status/Journal/HUD 상태 exact-once 확인

필수 산출물:
- `docs/superpowers/goals/YYYY-MM-DD-knowledge-status-activity-chain-goal.md`
- RED/GREEN EditMode tests
- PlayMode E2E test
- screenshot evidence

커밋 메시지:
`[FEATURE][TEST] Knowledge status activity chain 추가`
```

