# Recommended Goal Prompts: After Relationship Commitment Route

Date: 2026-05-21

이 문서는 현재 커밋 상태 기준으로 다음 goal 세션에 붙여 넣을 프롬프트를 추천 순서대로 정리한다. 이미 완료된 Resource/Recipe activity slice, dedicated smoke, core gameplay matrix 확장, Career/Job Daily Plan route 확장, Relationship activity commitment route는 제외한다.

## Current State

최근 완료된 기반:

- `2ec5d8675 [FEATURE][ASSET][TEST] Town resource recipe activity slice`
- `77e8b510f [NETWORK][TEST] Resource recipe activity dedicated smoke`
- `60ddcd872 [NETWORK][TEST] Core gameplay matrix coverage expansion`
- `2ea72aae6 [FEATURE][ASSET][TEST] Career job Daily Plan route 확장`
- `524d96ed3 [FEATURE][TEST] Relationship activity commitment route 추가`

현재 추천 순서:

1. Knowledge/Status Activity Chain
2. House Upgrade/Interior Construction Next Slice
3. Objective Journal Legacy HUD Final Ownership Audit
4. Dedicated Core Gameplay Matrix Follow-up
5. Quest/Interior Reward Transaction Regression Sweep

Prompt 2 이후는 Prompt 1이 커밋되거나, 명시적인 blocker와 함께 중단된 뒤 진행한다.

## Common Non-Negotiable Constraints

- 공유 워크트리로 취급한다. 관련 없는 dirty/untracked 파일을 되돌리거나 삭제하지 않는다.
- `Assets/**/*.cs`는 쉘/apply_patch로 직접 쓰지 않는다. Unity C# 파일은 MCP `script-update-or-create`를 사용한다.
- 모든 신규 동작, 회귀 게이트, QA 스크립트 변경은 실패 테스트를 먼저 만든다.
- 게임 엔티티와 진행 조건은 ScriptableObject 데이터와 전략 배열로 표현한다.
- production C#에 quest, route, activity, location, NPC, reward, crop, tool, furniture, career, job, room, upgrade, clue, world-state, recipe, resource, knowledge, status ID 분기를 추가하지 않는다.
- 목표성 UI는 `ObjectiveJournalPanel`이 소유한다. 상시 HUD는 `TrackedObjectiveHud` 하나만 허용한다.
- visible/player-facing 변경은 PlayMode에서 실제 플레이 흐름, Game View/Camera 스크린샷, 런타임 상태, 저장/로드 상태로 검증한다.
- Runtime-only 검증은 scene, prefab, ScriptableObject, palette, tilemap, serialized save state 검증을 대체하지 못한다.
- `Assets/AddressableAssetsData/link.xml`과 `.meta` 삭제/변형을 보존 게이트로 확인한다.
- 완료 전에는 `check-addressables-linkxml-preserved.ps1`, `git diff --check`, `check-no-entity-id-branching.sh`, `git status --short`를 실행한다.

---

## Prompt 1: Knowledge/Status Activity Chain

아래 프롬프트를 가장 먼저 붙여 넣는다.

### Objective

Knowledge 또는 Status progression 중 하나를 Town activity chain으로 연결한다. 플레이어가 Town에서 활동을 선택하면 지식 발견 또는 상태 변화가 발생하고, 다음 행동이 Objective Journal에 표시되며, day result와 reload에서 중복 없이 유지되어야 한다.

### Why First

Career/Job route와 Relationship commitment route가 들어갔으므로 다음 player-facing 가치는 자유 경로 성장이다. Knowledge/Status는 학교 수업 하나만 정답인 구조를 피하고, 활동/탐험/독학/휴식 같은 대체 성장 경로를 넓히는 기반이다.

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Knowledge 또는 Status 중 하나만 선택한다. 둘 다 동시에 구현하지 않는다.
- 한 개의 Town activity chain만 만든다.
- progression condition/outcome은 ScriptableObject 전략 배열로 작성한다.
- 새 데이터는 관련 `Assets/Data/...` 경로에 두고 `Assets/Data/Registry/GameDataRegistry.asset`에 등록한다.
- Objective Journal에는 next-step hint 또는 goal만 추가한다. 새 상시 HUD를 만들지 않는다.
- save slot reload 후 Knowledge/Status, Journal row, tracked HUD가 중복 없이 복원되게 한다.
- performance risk를 점검한다: `Update()` polling, 매 프레임 전체 스캔, 반복 Instantiate/Destroy를 추가하지 않는다.

### Required TDD

RED:

- Registry test가 새 Knowledge/Status activity chain data 누락으로 실패한다.
- Outcome/condition test가 activity 수행 전 false, 수행 후 true 또는 changed state를 검증하지 못해 실패한다.
- Persistence test가 save slot reload 후 Knowledge/Status state를 복원하지 못해 실패한다.
- Objective Journal source test가 next-step guidance를 stable key로 emit하지 못해 실패한다.
- PlayMode E2E가 visible Town activity path 또는 screenshot evidence 부재로 실패한다.

GREEN:

- Activity outcome이 데이터 참조만으로 Knowledge/Status progression을 적용한다.
- 같은 activity 반복 실행이 duplicate discovery, duplicate status delta, duplicate Journal row를 만들지 않는다.
- Save/load 후 state와 Journal/HUD가 exact-once로 복원된다.

### Required PlayMode Scenario

Create:

```text
KNOWLEDGE_STATUS_E2E_001_ActivityChainHintReloadNoDuplicate
```

The scenario must:

1. MainMenu SaveSlot flow에서 시작한다.
2. Town에 진입한다.
3. activity source까지 player movement로 이동한다.
4. visible interaction/UI 선택으로 activity를 수행한다.
5. Knowledge discovery 또는 Status change가 보이는지 확인한다.
6. day result 또는 transient feedback을 확인한다.
7. `Tab`으로 Objective Journal을 열어 next-step hint를 확인한다.
8. 필요한 경우 한 row를 track하고 exactly one `TrackedObjectiveHud`를 확인한다.
9. Game View 또는 Camera screenshot을 캡처한다:

```text
Builds/Logs/knowledge-status-activity-chain-e2e.png
```

10. 같은 save slot을 reload한다.
11. Knowledge/Status/Journal/HUD 상태가 중복 없이 복원되는지 확인한다.
12. 같은 activity를 반복해 duplicate progress, duplicate status delta, duplicate Journal row가 없는지 확인한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Knowledge
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Status
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.KnowledgeStatusActivityChainE2EScenarioTests.KNOWLEDGE_STATUS_E2E_001_ActivityChainHintReloadNoDuplicate
Get-ChildItem Builds/Logs/knowledge-status-activity-chain-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If the exact EditMode namespace differs, use the nearest existing Knowledge/Status test namespace and record the substitution in the final report.

### Acceptance Criteria

- One Knowledge/Status progression route is fully data-driven.
- The route adds one alternative growth path outside school-only progression.
- Objective Journal owns the next-step guidance.
- PlayMode screenshot, reload, duplicate-prevention, and runtime-state checks pass.
- No production entity-ID branching is introduced.

### Commit

```text
[FEATURE][ASSET][TEST] Knowledge status activity chain 추가
```

---

## Prompt 2: House Upgrade/Interior Construction Next Slice

Prompt 1이 커밋된 뒤 붙여 넣는다.

### Objective

One small House upgrade 또는 interior construction slice를 구현한다. progression requirement, upgrade availability, visible House/Interior result, Objective Journal guidance, save/load persistence가 하나의 player-facing flow로 증명되어야 한다.

### Why Second

Quest/interior reward와 placement 회귀망은 이미 존재한다. 다음 housing 가치는 넓은 인테리어 콘텐츠 추가가 아니라, 작은 upgrade/construction path를 실제 플레이 흐름으로 증명하는 것이다.

### Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- 기존 설계 문서를 먼저 확인한다:

```text
docs/superpowers/specs/2026-05-17-house-upgrade-and-interior-construction-design.md
docs/superpowers/specs/2026-05-19-house-expansion-interior-loop-design.md
```

- 한 upgrade 또는 construction action만 선택한다.
- requirement와 outcome은 ScriptableObject data 또는 기존 strategy arrays로 표현한다.
- 새 data assets는 `GameDataRegistry.asset`에 등록한다.
- housing guidance는 Objective Journal에만 표시한다.
- visible result는 runtime state와 serialized save state가 reload 후 일치해야 한다.
- 기존 sample/debug tilemap, placeholder object, old/new overlap이 보이면 실패로 처리한다.

### Required TDD

RED:

- Upgrade availability가 requirement 전 false임을 검증하지 못해 실패한다.
- Registry wiring test가 upgrade/requirement/outcome data 누락으로 실패한다.
- Save/load test가 upgrade result를 복원하지 못해 실패한다.
- Objective Journal ownership test가 housing guidance source 누락 또는 duplicate stable key로 실패한다.

GREEN:

- Player-facing construction action이 requirement를 validate 또는 consume하고 exact-once로 결과를 적용한다.
- Visible runtime result와 serialized state가 reload 후 일치한다.
- Objective Journal guidance와 tracked HUD가 중복되지 않는다.

### Required PlayMode Scenario

Create:

```text
HOUSE_UPGRADE_E2E_001_UnlockConstructReload
```

The scenario must:

1. MainMenu SaveSlot flow에서 시작한다.
2. Town/House flow에 player-facing movement 또는 UI로 진입한다.
3. requirement 전 upgrade가 unavailable임을 확인한다.
4. required progress path를 player-facing action으로 완료한다.
5. visible UI로 construction 또는 upgrade를 실행한다.
6. Game View에서 House/Interior 변화가 보이는지 확인한다.
7. runtime scene, tilemap, prefab, renderer, selected asset, save-state reference를 inspect한다.
8. `Tab`으로 Objective Journal을 열어 housing guidance를 확인한다.
9. screenshot을 캡처한다:

```text
Builds/Logs/house-upgrade-e2e.png
```

10. 같은 save slot을 reload한다.
11. upgraded visual state, serialized state, Objective Journal state, duplicate old/new object absence를 확인한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UnlockConstructReload
Get-ChildItem Builds/Logs/house-upgrade-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- One House upgrade/construction slice is fully player-facing.
- Requirements, outcomes, and registry wiring are data-driven.
- Visible runtime state and serialized saved state agree after reload.
- Objective Journal owns guidance and no new permanent objective HUD is introduced.
- No stale sample/debug tilemap, placeholder object, or overlapping old/new visual remains visible.

### Commit

```text
[FEATURE][ASSET][TEST] House upgrade construction slice 추가
```

---

## Prompt 3: Objective Journal Legacy HUD Final Ownership Audit

Prompt 2 완료 뒤 붙여 넣는다. 새 기능 도중 별도 상시 goal HUD가 생기려는 징후가 있으면 이 prompt를 먼저 진행한다.

### Objective

Legacy objective-like HUD ownership을 최종 점검한다. goal, quest, campaign, career, milestone, hint, housing guidance를 설명하는 persistent UI는 `ObjectiveJournalPanel`과 exactly one `TrackedObjectiveHud`만 소유해야 한다.

### Why Third

Daily Plan, Career/Job, Relationship, Resource/Recipe, Knowledge/Status, Housing guidance가 Objective Journal로 모이고 있다. 이 시점에서 scattered permanent HUD가 다시 생기지 않도록 ownership gate를 고정한다.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- production code에서 legacy HUD/widget factories와 always-visible objective panels를 audit한다.
- persistent objective guidance는 `ObjectiveJournalPanel`과 one `TrackedObjectiveHud`만 허용한다.
- modal/result feedback panel은 persistent goal guidance가 아니면 유지할 수 있다.
- source ownership tests를 강화해 forbidden HUD creation과 duplicate stable key를 차단한다.
- 관련 PlayMode E2E는 `Tab`으로 Objective Journal을 열고 one tracked item만 검증하도록 정리한다.

### Required TDD

RED:

- Runtime installer 또는 factory가 forbidden HUD/widget type을 참조하면 실패한다.
- player-facing scene에서 `TrackedObjectiveHud`가 2개 이상이면 실패한다.
- Goals, Quests, Hints, Campaign, Career, Relationship, Housing, Resource/Recipe, Knowledge/Status source 간 stable key collision이 있으면 실패한다.

GREEN:

- Persistent objective guidance owner가 Objective Journal과 one tracked HUD로 제한된다.
- Existing modal/result panels는 transient feedback으로 유지된다.
- Save/load 후 Journal rows와 tracked HUD가 중복되지 않는다.

### Required PlayMode Scenario

Create or strengthen:

```text
OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud
```

The scenario must:

1. MainMenu SaveSlot flow에서 시작한다.
2. Town에 진입한다.
3. 최소 두 개 이상의 guidance source state를 player-facing flow로 만든다.
4. `Tab`으로 Objective Journal을 연다.
5. quest, route, relationship, career, resource/recipe, knowledge/status, housing guidance가 가능한 owned category에만 나타나는지 확인한다.
6. 한 item을 track한다.
7. exactly one `TrackedObjectiveHud`를 확인한다.
8. 관련 progress action 하나를 수행한다.
9. Journal과 tracked HUD가 update되는지 확인한다.
10. screenshot을 캡처한다:

```text
Builds/Logs/objective-journal-ownership-e2e.png
```

11. 같은 save slot을 reload하고 duplicate HUD/row가 없는지 확인한다.

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

- Persistent objective-like UI is owned only by Objective Journal and one tracked HUD.
- No legacy goal HUD factory or always-visible objective panel remains active.
- Existing player-facing guidance still appears through Objective Journal.
- Reload does not duplicate rows or HUDs.

### Commit

```text
[REFACTOR][UI][TEST] Objective Journal HUD ownership 최종 점검
```

---

## Prompt 4: Dedicated Core Gameplay Matrix Follow-up

Prompt 1부터 3까지 완료한 뒤 붙여 넣는다. Multiplayer validation이 release blocker가 되면 더 앞당길 수 있다.

### Objective

Dedicated core gameplay matrix를 현재 player-facing slice까지 확장한다. Knowledge/Status activity chain, Relationship commitment route, Career/Job route, Resource/Recipe, route/relationship persistence, Objective Journal reload behavior가 dedicated server/client process 조건에서 깨지지 않아야 한다.

### Why Fourth

새 gameplay slice가 늘어난 뒤에는 Editor PlayMode만으로는 부족하다. core matrix는 built dedicated server/client에서 player-facing loop와 persisted state가 유지되는지 확인하는 opt-in QA gate여야 한다.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1` 또는 새 opt-in matrix runner를 확장한다.
- 최소 세 slice를 포함한다:
  - route/relationship persistence,
  - career/job route,
  - resource/recipe,
  - Knowledge/Status activity chain 또는 House upgrade 중 최근 구현 slice.
- 각 slice는 action pass와 observe-only reload pass를 분리한다.
- 각 client cycle은 screenshot, completion marker, runtime snapshot, saved-state evidence를 남긴다.
- cleanup 후 server/client process leak이 없어야 한다.
- matrix runtime이 길면 standard PlayMode gate가 아니라 명시적 QA command로 유지한다.

### Required TDD

RED:

- Matrix contract test가 최근 구현 slice 누락으로 실패한다.
- Log parser test가 server readiness, client readiness, action marker, observe-only marker, screenshot, saved-state evidence 중 하나가 없으면 실패한다.
- Cleanup test가 launched process leak을 발견하면 실패한다.
- Duplicate-state parser가 reload 후 duplicate Journal row, HUD, route state, relationship delta, recipe progress, knowledge/status state를 발견하면 실패한다.

GREEN:

- Matrix runner가 각 slice의 action/reload evidence를 생성한다.
- Matrix logs가 fatal client/server errors 없이 cleanup을 증명한다.

### Required Process Scenario

Create or extend:

```text
NETWORK_DEDICATED_MATRIX_001_CoreGameplayReload
```

The scenario must:

1. Fresh client/server artifacts를 사용한다.
2. dedicated server/client cycle for route/relationship을 실행한다.
3. dedicated server/client cycle for career/job route를 실행한다.
4. dedicated server/client cycle for resource/recipe를 실행한다.
5. dedicated server/client cycle for Knowledge/Status 또는 House upgrade를 실행한다.
6. 각 client cycle마다 screenshot을 캡처한다.
7. 모든 slice에서 persisted state reload를 검증한다.
8. cleanup 후 launched process가 남지 않았는지 확인한다.

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

- Dedicated server/client validation covers multiple current core gameplay slices.
- Screenshots, logs, runtime state, and saved state agree for every slice.
- Reload does not duplicate Journal/HUD/gameplay state.
- No launched server/client process remains after cleanup.
- Failures distinguish build artifact problems, network process problems, and gameplay saved-state problems.

### Commit

```text
[NETWORK][TEST] Dedicated core gameplay matrix follow-up 추가
```

---

## Prompt 5: Quest/Interior Reward Transaction Regression Sweep

Prompt 4 이후 붙여 넣는다. Quest/interior reward나 inventory transaction regression이 보이면 더 앞당긴다.

### Objective

Quest/Interior reward transaction과 saved-state 회귀를 한 번 더 sweep한다. 보상 지급 전 capacity와 duplicate claim을 검증하고, 실패 시 state를 변경하지 않으며, 성공 지급은 atomic/idempotent여야 한다.

### Why Fifth

기능이 늘어날수록 reward, inventory, furniture unlock, placement save가 조용히 깨질 위험이 커진다. 이 prompt는 새 기능 추가보다 보상/배치/저장 안정성 확인에 집중한다.

### Required Skills

- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

### Required Work

- Quest reward 또는 Interior reward 중 가장 위험한 한 path만 대상으로 한다.
- reward 지급 전 inventory capacity와 duplicate claim을 검증한다.
- 실패 시 inventory, reward state, unlock state, placement state가 변경되지 않아야 한다.
- 성공 지급은 item copy/delete, partial grant, double grant 없이 atomic 처리한다.
- reload 후 같은 reward trigger를 다시 호출해도 idempotent여야 한다.
- visible placement/interior change가 있으면 Game View screenshot과 saved layout inspection을 포함한다.

### Required TDD

RED:

- Capacity failure가 state를 변경하면 실패한다.
- Duplicate claim이 inventory/unlock/layout state를 변경하면 실패한다.
- Save/load 후 reward 재호출이 item 또는 placement를 중복 생성하면 실패한다.
- PlayMode visual check가 placed object 또는 tilemap saved state 불일치로 실패한다.

GREEN:

- Reward transaction은 validate-before-mutate 구조를 지킨다.
- 실패 path는 rollback이 아니라 no-mutation으로 끝난다.
- Success path와 reload path 모두 exact-once이다.

### Required PlayMode Scenario

Create or strengthen:

```text
QUEST_INTERIOR_REWARD_REGRESSION_E2E_001_RewardTransactionReloadNoDuplicate
```

The scenario must:

1. MainMenu SaveSlot flow에서 시작한다.
2. Town 또는 relevant scene에 진입한다.
3. reward trigger를 visible interaction/UI로 수행한다.
4. reward 수령 UI 또는 placement result를 확인한다.
5. inventory/reward/unlock/layout state를 검사한다.
6. screenshot을 캡처한다:

```text
Builds/Logs/quest-interior-reward-regression-e2e.png
```

7. 같은 save slot을 reload한다.
8. reward claimed state와 inventory/interior state가 유지되는지 확인한다.
9. 같은 reward trigger를 반복해 duplicate item, partial grant, duplicate placement가 없는지 확인한다.
10. inventory full 또는 duplicate claim failure path가 state를 변경하지 않는지 확인한다.

### Required Verification

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Quests
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardRegressionE2EScenarioTests.QUEST_INTERIOR_REWARD_REGRESSION_E2E_001_RewardTransactionReloadNoDuplicate
Get-ChildItem Builds/Logs/quest-interior-reward-regression-e2e.png | Select-Object Name,Length,LastWriteTime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

### Acceptance Criteria

- Reward transaction은 capacity, duplicate claim, failure no-mutation을 검증한다.
- Save/load 후 reward 재호출이 멱등이다.
- Visible interior/placement 변경이 있으면 실제 Game View screenshot과 serialized state로 검증된다.
- Objective Journal ownership과 Modern UI Style2 규약을 깨지 않는다.

### Commit

```text
[TEST][BUGFIX] Quest interior reward transaction 회귀 강화
```
