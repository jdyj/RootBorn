# Objective Journal Legacy HUD Migration Design

## Objective

Objective Journal UI ownership 규약을 코드베이스 전체에 실제로 적용한다. 목표, 퀘스트, 캠페인, 마일스톤, 진로 힌트처럼 "플레이어가 지금 무엇을 하면 되는가"를 설명하는 상시 UI는 `ObjectiveJournalPanel`과 `TrackedObjectiveHud`로 수렴시키고, 기존 레거시 목표성 HUD는 단계적으로 제거한다.

## Current State

현재 Objective Journal은 Quest, Campaign, Milestone source를 표시하고 row 선택과 tracking을 지원한다. 하지만 레거시 목표성 UI가 아직 남아 있다.

- `CampaignHudPanel`
- `MilestoneHudPanel`
- `QuestChainHudWidget`
- `CareerInterestHudWidget`

이들은 각자 `EnsureInScene`, Canvas 부착, 고정 위치, 상시 표시 흐름을 갖고 있어 Objective Journal ownership 규약과 충돌한다. 직전 작업에서 추가된 `ObjectiveJournalUiOwnershipGateTests`는 이 레거시 항목을 allowlist로 허용하고 있으므로, 이번 이관의 최종 목표는 allowlist를 줄이는 것이다.

## Non-Goals

- 대화창, 결과창, 보상 실패 알림, day result summary 같은 즉시 피드백 UI는 제거하지 않는다.
- `SaveSlotSelectPanel`, `TownTimeHud`, inventory/status/settings 같은 비목표성 UI는 이 작업 범위가 아니다.
- 모든 Objective Journal UX 개선을 한 번에 하지 않는다. 선택 하이라이트, 검색, 정렬 UI 같은 polish는 후속 작업으로 둔다.

## Migration Strategy

레거시 HUD를 즉시 삭제하지 않는다. 각 HUD마다 아래 순서를 따른다.

1. 기존 HUD가 표시하던 정보와 E2E 기대값을 테스트로 고정한다.
2. 동일 정보를 `ObjectiveJournalRuntimeProvider` 또는 분리된 objective source가 제공하도록 연결한다.
3. 기존 PlayMode/E2E가 레거시 HUD 대신 Objective Journal category, row, detail, tracked HUD를 검증하도록 바꾼다.
4. 레거시 HUD 생성 경로와 installer 의존성을 제거한다.
5. `ObjectiveJournalUiOwnershipGateTests` allowlist에서 해당 legacy type/factory를 제거한다.

이 방식은 user-facing PlayMode 흐름을 유지하면서도, 각 단계에서 실제 UI가 깨졌는지 확인할 수 있다.

## Migration Order

### 1. Campaign HUD

`CampaignHudPanel`은 이미 Campaign source와 정보가 많이 겹친다. 먼저 이관한다.

Required behavior:

- Campaign category row가 현재 day, selected route, completed objective, next guide를 표시한다.
- Track 버튼으로 campaign objective를 추적하면 `TrackedObjectiveHud`에 campaign 진행이 표시된다.
- `FirstThreeDaysCampaignE2EScenarioTests`는 `CampaignHudPanel` 탐색 대신 Objective Journal 열기, Campaign category 선택, row click, Track click, reload 후 restore를 검증한다.
- `CampaignRuntimeInstaller`는 campaign progress 유지와 provider refresh만 담당하고 별도 campaign HUD를 만들지 않는다.
- ownership gate allowlist에서 `CampaignHudPanel` type/factory를 제거한다.

### 2. Milestone HUD

`MilestoneHudPanel`은 Objective Journal Hints category의 Milestone source로 이관한다.

Required behavior:

- Hints category row가 milestone별 objective progress와 `Active`, `Completed`, `RewardClaimed` 상태를 표시한다.
- milestone reward claim 이후에도 completed history가 Journal에 남는다.
- `OpenEndedMilestoneGrowthE2EScenarioTests`는 `MilestoneHudPanel` 탐색 대신 Objective Journal Hints category와 `TrackedObjectiveHud`를 검증한다.
- `StudentDayRuntimeInstaller`와 `StudentDayEndInteractor`는 레거시 milestone HUD refresh를 호출하지 않는다.
- ownership gate allowlist에서 `MilestoneHudPanel` type/factory를 제거한다.

### 3. Quest Chain HUD Widget

`QuestChainHudWidget`은 quest-chain 진행 정보를 Objective Journal Quests category로 이관한다.

Required behavior:

- Quest chain step 상태가 Quests category row/detail에 표시된다.
- active step, failed/expired reason, reward state는 Objective Journal detail에 들어간다.
- `QuestChainRuntimeInstaller`는 `QuestChainLogPanel`과 `TrackedObjectiveHud`를 중복 상시 HUD로 만들지 않는다.
- 관련 QuestChain E2E는 Objective Journal row click과 Track click으로 검증한다.
- ownership gate allowlist에서 `QuestChainHudWidget` type/factory를 제거한다.

`QuestChainLogPanel`은 상세 로그/선택형 패널 성격이 남아 있으므로 별도 판단한다. 상시 목표성 UI로 사용 중이면 Objective Journal로 흡수하고, 일회성/상세 modal 성격이면 예외로 남길 수 있다.

### 4. Career Interest HUD Widget

`CareerInterestHudWidget`은 career interest와 recommendation을 Objective Journal Hints 또는 Career-related category로 이관한다.

Required behavior:

- 현재 career interest, candidate, recommendation, linked quest 상태가 Objective Journal에 표시된다.
- Track 버튼으로 career/candidate objective를 추적할 수 있다.
- `CareerInterestSelectionLoopE2EScenarioTests`는 legacy widget 대신 Objective Journal + `TrackedObjectiveHud`를 검증한다.
- `CareerInterestRuntimeInstaller`는 panel/logic은 유지하더라도 별도 상시 career HUD를 만들지 않는다.
- ownership gate allowlist에서 `CareerInterestHudWidget` type/factory를 제거한다.

## Architecture

### Objective Sources

레거시 HUD별 표시 로직을 `ObjectiveJournalRuntimeProvider` 내부에 계속 누적하지 않는다. 이관 중 파일 크기와 테스트 복잡도가 커지면 다음 source 클래스로 분리한다.

- `QuestObjectiveJournalSource`
- `CampaignObjectiveJournalSource`
- `MilestoneObjectiveJournalSource`
- `QuestChainObjectiveJournalSource`
- `CareerInterestObjectiveJournalSource`

각 source는 ScriptableObject와 progress state를 `ObjectiveJournalItem`으로 변환한다. entity ID별 C# 분기, hardcoded quest/campaign/milestone ID 조건, `Update()` polling은 추가하지 않는다.

### Refresh Flow

Refresh는 다음 시점 중심으로 유지한다.

- Objective Journal open
- 관련 runtime installer가 progress state를 갱신한 직후
- day result, quest reward claim, campaign progress apply, milestone reward claim 같은 명시적 event 이후

대량 objective 확장을 고려해 매 frame 전체 source를 scan하지 않는다.

### Tracking Flow

각 category row는 stable key를 갖는다.

- Campaign: `campaign.{campaignId}.day.{dayNumber}` 또는 더 세분화된 objective key
- Milestone: `milestone.{milestoneId}`
- Quest chain: `quest-chain.{chainId}.step.{stepId}`
- Career interest: `career-interest.{interestId}` 또는 `career-candidate.{candidateId}`

`Track`은 선택된 row의 stable key를 저장하고 `TrackedObjectiveHud` 하나만 갱신한다. reload 후 provider refresh가 saved stable key를 찾아 동일 objective를 복원한다.

## Tests

### EditMode

- Campaign source가 legacy HUD와 동등한 campaign progress summary를 만든다.
- Milestone source가 objective별 progress, completed, reward-claimed state를 만든다.
- Quest chain source가 active/blocked/failed/expired/reward state를 만든다.
- Career interest source가 current interest, recommendation, linked quest state를 만든다.
- `ObjectiveJournalUiOwnershipGateTests` allowlist가 각 migration 후 줄어든다.
- source tests는 entity ID별 분기 없이 data/progress 기반으로 검증한다.

### PlayMode

- Objective Journal을 실제 `Tab`으로 연다.
- category button을 실제 click한다.
- migrated row를 실제 click한다.
- `Track` button을 실제 click한다.
- `TrackedObjectiveHud`가 선택 row로 갱신된다.
- reload 후 tracked objective가 복원된다.

### E2E

다음 기존 E2E는 legacy HUD 탐색을 제거하고 Objective Journal flow를 검증하도록 바꾼다.

- `FirstThreeDaysCampaignE2EScenarioTests`
- `OpenEndedMilestoneGrowthE2EScenarioTests`
- QuestChain 관련 E2E
- `CareerInterestSelectionLoopE2EScenarioTests`

각 visible/UI 변경은 Game View screenshot을 남긴다.

## Static Gate

`ObjectiveJournalUiOwnershipGateTests`는 migration마다 더 엄격해져야 한다.

1. migration 전: legacy type/factory가 allowlist에 있음
2. migration 구현: E2E가 Objective Journal flow로 변경됨
3. legacy 제거: 해당 type/factory 삭제
4. allowlist 제거: 새 상시 목표성 HUD 추가 시 테스트 실패

게이트 실패 메시지는 위반 파일, 라인, type/factory 이름을 출력해야 한다.

## Risks

- 레거시 E2E가 특정 HUD 컴포넌트 존재를 직접 assert하고 있어 테스트 수정 범위가 크다.
- 일부 legacy HUD는 정보 표시뿐 아니라 refresh trigger 역할도 겸할 수 있다. 제거 전에 installer가 progress update와 provider refresh를 계속 수행하는지 확인해야 한다.
- `QuestChainLogPanel`과 `CareerInterestPanel`은 상시 HUD인지 modal/detail panel인지 경계가 애매하다. 제거 전에 실제 사용자 flow 기준으로 분류한다.

## Completion Criteria

- `CampaignHudPanel`, `MilestoneHudPanel`, `QuestChainHudWidget`, `CareerInterestHudWidget`가 더 이상 상시 목표성 HUD로 생성되지 않는다.
- Objective Journal category/detail/tracking이 각 legacy HUD의 기존 user-facing 정보를 대체한다.
- `TrackedObjectiveHud`만 상시 목표 HUD로 남는다.
- ownership gate allowlist에서 이관 완료된 legacy type/factory가 제거된다.
- 관련 EditMode, PlayMode, E2E가 통과한다.
- `git diff --check`가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
