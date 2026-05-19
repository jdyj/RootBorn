# Goal: Objective Journal Source Quality, Row Selection, and UI Ownership Gate

## Objective

Objective Journal을 단순 표시 검증 단계에서 실제 플레이 가능한 목표 관리 UI로 끌어올린다. Quest, Campaign, Milestone 항목의 표시 품질을 개선하고, objective row 선택/추적 흐름을 실제 UI 클릭으로 완성하며, 새 목표성 HUD/Canvas가 임의로 추가되지 않도록 정적 게이트를 추가한다.

## Scope

1. Quest, Campaign, Milestone 표시 품질 개선
2. Objective Journal row 선택 및 track 대상 변경 흐름 구현
3. Objective UI ownership 정적 게이트 추가

## Completion Criteria

### 1. Source Quality

- Quest 항목은 `Available`, `Active`, `Completed`, `RewardClaimed` 상태를 의도에 맞게 표시한다.
- Quest progress text는 첫 objective만이 아니라 가능한 objective 진행 상태를 명확히 요약한다.
- Campaign 항목은 현재 day, route, 완료된 objective, 다음 guide text를 실제 progress 기반으로 표시한다.
- Milestone 항목은 objective별 진행률과 completed/reward-claimed 상태를 명확히 표시한다.
- 모든 항목은 데이터와 progress state 기반으로 생성되며 entity ID별 C# 분기를 추가하지 않는다.

### 2. Row Selection and Tracking

- Objective Journal의 objective row가 실제 클릭 가능한 UI 요소다.
- row 클릭 시 detail panel이 해당 objective로 갱신된다.
- Track 버튼은 현재 선택된 row를 추적한다.
- 다른 row를 선택 후 Track하면 `TrackedObjectiveHud`가 새 objective로 교체된다.
- tracked stable key는 active save slot에 저장되고 reload 후 복원된다.
- PlayMode에서 실제 `Tab`, row click, Track click 흐름으로 검증한다.

### 3. UI Ownership Gate

- 새 goal/quest/campaign/hint/milestone 목적성 UI가 별도 상시 Canvas/HUD로 추가되지 않도록 정적 테스트 또는 CI 게이트를 추가한다.
- 허용되는 상시 목표 HUD는 `TrackedObjectiveHud` 하나뿐이다.
- 목표 목록/상세 UI는 `ObjectiveJournalPanel`이 소유한다.
- 기존 허용 UI와 즉시 피드백 패널은 명시적으로 allowlist 처리한다.
- 게이트 실패 메시지는 위반 파일, 타입, 오브젝트명을 확인 가능하게 출력한다.

## Required Tests

- EditMode: Quest source formatting tests
- EditMode: Campaign source formatting tests
- EditMode: Milestone source formatting tests
- EditMode: Objective row click updates selected detail
- EditMode or PlayMode: Track button tracks selected row and persists stable key
- PlayMode: Town E2E extension
  - MainMenu/New Game -> Town
  - `Tab`으로 Journal 열기
  - Quest/Campaign/Milestone row 클릭
  - Track 클릭
  - HUD 변경 확인
  - reload 후 tracked objective 복원
- Static gate: objective UI ownership check
- `git diff --check`
- `Scripts/ci/check-no-entity-id-branching.sh`

## Constraints

- `Assets/**/*.cs`는 Unity MCP `script-update-or-create`로만 작성/수정한다.
- visible/UI 변경은 실제 PlayMode 사용자 흐름과 Game View screenshot으로 검증한다.
- `ObjectiveJournalPanel`이 목표성 UI의 소유자다.
- `TrackedObjectiveHud` 외 상시 목표 HUD 추가 금지.
- entity ID별 C# 분기 금지.
- 대량 objective 확장을 고려해 refresh는 명시적 이벤트/Journal open 중심으로 유지하고 `Update()` polling은 추가하지 않는다.

## Execution Notes

- 먼저 source formatting을 EditMode 실패 테스트로 고정한다.
- 그다음 row selection과 tracking UX를 PlayMode 사용자 입력 흐름으로 확장한다.
- 마지막으로 UI ownership gate를 추가해 이후 기능 개발에서 같은 규약이 깨지지 않게 한다.
- 세 작업은 같은 Objective Journal 표면을 공유하므로 한 goal 안에서 묶되, 커밋은 테스트/구현/게이트 성격에 맞춰 작게 나눈다.
