# Student Relationship Condition Day Loop Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "학생이 하루 동안 실제 이동과 NPC 상호작용/도움 행동/퀘스트를 수행하면 관계와 컨디션이 변하고, 하루 결과 UI에 관계/컨디션 변화가 표시되며, 다음 날 NPC 대사나 가능 행동이 달라지는 생활 루프"를 설계/구현/검증해줘.

전제:
- 학생 하루 루프는 실제 이동, 활동, 하루 종료, 결과 UI, 다음 날 시작, 저장/로드 복원이 동작한다.
- 하루 종료 규칙은 GameDataRegistry 기반 ScriptableObject 데이터로 와이어링되어 있다.
- 하루 결과 UI는 학생 활동, 퀘스트, 보상, 인벤토리 변화를 표시할 수 있다.
- 튜토리얼/스토리 단계는 하루 종료와 다음 날 시작에 연결되어 있다.

핵심 의도:
1. 하루 루프를 단순 성장/보상 정산에서 "생활 관계와 몸 상태가 누적되는 루프"로 확장한다.
2. NPC와의 실제 상호작용이 다음 날 대사, 선택지, 가능 행동에 영향을 줘야 한다.
3. 무리한 활동, 도움 행동, 퀘스트 수행이 피로/집중/기분/컨디션 상태로 이어져야 한다.
4. 관계/컨디션은 저장/로드 후 유지되고, 같은 하루 결과를 재확인해도 중복 적용되지 않아야 한다.

최우선 검증 원칙:
- 이 goal은 실제 유저가 플레이하듯 검증하지 않으면 완료로 인정하지 않는다.
- NPC 관계 변화, 컨디션 변화, 다음 날 대사/행동 변화는 내부 API 직접 호출만으로 검증하면 실패다.
- 플레이어가 실제 키보드/입력으로 이동하고, 실제 Collider/Interaction/Prompt/UI Button 경로로 NPC/퀘스트/하루 종료/다음 날 시작을 수행해야 한다.
- 테스트에서 RelationshipProgress, StatusProgress, StudentLifeProgress, QuestLog, Inventory를 직접 세팅해서 성공 처리하면 실패다.
- EditMode나 도메인 단위 테스트는 보조 검증으로만 인정한다. 최종 완료 근거는 반드시 PlayMode에서 저장 슬롯 UI, 실제 이동 입력, 실제 상호작용 입력, 실제 결과 UI 버튼을 거치는 E2E 테스트여야 한다.
- 테스트 자동화가 어렵다는 이유로 플레이어 위치 강제 세팅, 컴포넌트 메서드 직접 호출, 씬 강제 로드, 저장 데이터 직접 주입으로 실제 플레이 경로를 대체하면 완료로 보고하지 않는다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- npcId, relationshipId, statusId, activityId, questId, itemId, tutorialStageId별 `if/switch` 분기, enum 기반 엔티티 분기, 엔티티별 C# 클래스 생성은 금지한다.
- 관계 변화, 컨디션 변화, 대사/선택지 조건, 하루 결과 요약은 SO 데이터와 전략 배열로 표현한다.
- 보상/인벤토리 트랜잭션 원칙을 유지한다. 관계/상태 보상도 저장/로드 후 재호출해도 중복 적용되면 안 된다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

권장 도메인 구조:
- `RelationshipDefinition`
  - NPC 또는 관계 대상과의 관계 축을 정의한다.
  - 예: 신뢰, 친밀, 존중 같은 값은 엔티티별 C# 분기가 아니라 SO 데이터로 표현한다.
- `RelationshipDeltaEffect`
  - 퀘스트 완료, 도움 행동, 대화 선택지, 학생 활동 결과에서 관계 값을 변경하는 일반 전략이다.
- `StatusDefinition`
  - 피로, 집중 저하, 기분, 활력 같은 컨디션 상태를 정의한다.
- `StatusDeltaEffect`
  - 활동/퀘스트/하루 종료 결과로 상태 값을 변경하는 일반 전략이다.
- `DialogueCondition` 또는 기존 조건 전략 확장
  - 현재 관계/컨디션/튜토리얼 단계에 따라 NPC 대사 또는 선택지를 데이터 기반으로 노출한다.
- `StudentDayRelationshipConditionSummary`
  - 하루 결과 UI에 표시할 관계/컨디션 변화 요약 DTO다.

목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임 또는 기존 슬롯을 로드해 Town에 진입한다.
2. 1일차 Town에서 NPC의 기본 대사와 가능 행동을 확인한다.
3. 플레이어가 실제 이동으로 NPC 또는 도움 행동 오브젝트에 접근한다.
4. 실제 상호작용 입력과 UI 버튼으로 대화/도움/퀘스트 중 하나 이상을 수행한다.
5. 수행 결과로 관계 값 또는 컨디션 상태가 변경된다.
6. 플레이어가 실제 이동으로 하루 종료 지점에 접근한다.
7. 실제 상호작용 입력으로 하루를 종료한다.
8. 하루 결과 UI에 오늘의 관계 변화와 컨디션 변화가 표시된다.
9. 결과 UI 버튼으로 다음 날을 시작한다.
10. 2일차 Town에서 같은 NPC의 대사, 선택지, 가능 행동 중 하나 이상이 관계/컨디션/튜토리얼 단계 기준으로 달라진다.
11. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 관계/컨디션/2일차 대사 상태가 유지된다.
12. 같은 날 결과를 다시 열거나 저장/로드 후 재상호작용해도 관계/컨디션 변화가 중복 적용되지 않는다.

결과 UI 표시 요구:
- 오늘 관계 변화
  - 대상 표시명
  - 변화한 관계 축
  - 이전 값/이후 값 또는 변화량
- 오늘 컨디션 변화
  - 상태 표시명
  - 이전 값/이후 값 또는 변화량
  - 다음 날 시작 시 유지/회복 여부
- 다음 날 영향
  - 변경된 NPC 안내, 새 선택지, 제한된 활동, 권장 회복 행동 중 하나 이상
- UI는 Modern UI Style2 공통 패널 규약을 유지한다.

저장 대상:
- saveSlot
- player identity
- 현재 일차와 하루 상태
- 관계 값 또는 관계 진행도
- 컨디션/상태 값
- 오늘 관계 변화 로그
- 오늘 컨디션 변화 로그
- 이전 날 관계/컨디션 결과 요약
- 다음 날 NPC 대사/행동 조건 평가에 필요한 튜토리얼/스토리 단계
- 퀘스트 수락/진행/완료/보상 수령 상태
- 인벤토리 수량
- 다음 날 시작 위치 또는 진입 지점

먼저 확인할 파일:
- `docs/superpowers/goals/2026-05-11-student-day-result-loop-goal.md`
- `docs/superpowers/goals/2026-05-11-day-end-rule-registry-goal.md`
- `docs/superpowers/goals/2026-05-11-student-day-result-quest-inventory-summary-goal.md`
- `docs/superpowers/goals/2026-05-11-student-day-tutorial-story-loop-goal.md`
- `docs/superpowers/specs/2026-05-08-student-career-life-design.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/World/`
- `Assets/Scripts/UI/StudentLife/`
- `Assets/Scripts/UI/Quests/`
- `Assets/Data/StudentLife/`
- `Assets/Data/Relationships/`
- `Assets/Data/Status/`
- `Assets/Data/NPCs/`
- `Assets/Data/Dialogue/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/PlayMode/EndToEnd/`

필수 EditMode 테스트:
- RELCOND-EDIT-001: RelationshipDefinition/StatusDefinition이 GameDataRegistry 또는 지정 Registry 경로에서 로드된다.
- RELCOND-EDIT-002: 관계 변화 전략은 특정 npcId/relationshipId 분기 없이 일반 RelationshipDefinition 참조로 값을 변경한다.
- RELCOND-EDIT-003: 컨디션 변화 전략은 특정 statusId 분기 없이 일반 StatusDefinition 참조로 값을 변경한다.
- RELCOND-EDIT-004: 하루 결과 요약은 관계/컨디션 변화의 before/after 또는 delta를 일반 DTO로 만든다.
- RELCOND-EDIT-005: 같은 하루 결과를 재적용해도 관계/컨디션 변화가 중복 적용되지 않는다.
- RELCOND-EDIT-006: 대사/선택지 조건은 관계/컨디션/튜토리얼 단계를 데이터 기반 조건으로 평가한다.

필수 PlayMode E2E 시나리오:
- RELCOND-E2E-001: 저장 슬롯 UI로 새 게임을 만들고 Town에 진입한다.
- RELCOND-E2E-002: 플레이어를 키보드 입력으로 NPC 또는 도움 행동 지점까지 이동시킨다.
- RELCOND-E2E-003: 실제 상호작용 입력과 UI 경로로 대화/도움/퀘스트 행동을 수행한다.
- RELCOND-E2E-004: 관계 또는 컨디션 변화가 도메인 상태와 유저가 보는 UI 상태에 함께 반영되는지 확인한다.
- RELCOND-E2E-005: 플레이어를 키보드 입력으로 하루 종료 지점까지 이동시킨다.
- RELCOND-E2E-006: 실제 상호작용 입력으로 하루 종료를 실행한다.
- RELCOND-E2E-007: 하루 결과 UI가 관계 변화와 컨디션 변화를 표시하는지 확인한다.
- RELCOND-E2E-008: 결과 UI 버튼으로 다음 날을 시작한다.
- RELCOND-E2E-009: 2일차 Town에서 NPC 대사, 선택지, 가능 행동 중 하나 이상이 변경되었는지 확인한다.
- RELCOND-E2E-010: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 관계/컨디션/2일차 대사 상태가 유지되는지 확인한다.
- RELCOND-E2E-011: 저장/로드 후 같은 날 결과를 다시 열어도 관계/컨디션 변화가 중복 적용되지 않는지 확인한다.
- RELCOND-E2E-012: 기존 학생 하루 루프, 포탈/퀘스트/인벤토리 E2E가 계속 통과하는지 확인한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, NpcInteractor, DialoguePanel, QuestLogPanel, StudentDayResultPanel, SaveSlotSelectPanel, EventSystem, UI Button을 사용한다.
- PlayMode 테스트는 저장 슬롯 UI에서 시작하고, 플레이어 이동은 키보드/입력 시스템 경로로 수행한다. 목적지 근처로 Transform을 직접 이동시킨 뒤 상호작용만 누르는 방식은 완료 근거로 쓰지 않는다.
- NPC 대화, 도움 행동, 퀘스트 수락/완료/보상 수령, 하루 종료, 다음 날 시작은 각각 실제 Prompt/Input/Button 경로를 거쳐야 한다.
- NPC 대사 변화는 도메인 상태만 보지 말고 화면 텍스트 또는 선택지 버튼 상태로 확인한다.
- 컨디션 변화는 도메인 값과 유저가 보는 UI 또는 하루 결과 텍스트 양쪽에서 확인한다.
- 관계 변화는 도메인 값과 하루 결과 UI 또는 NPC 반응 변화 양쪽에서 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, RelationshipProgress, StatusProgress, DayProgress, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "NPC 접근 실패", "관계 변화 미기록", "컨디션 변화 미표시", "하루 결과 관계 요약 누락", "2일차 대사 변화 없음", "저장 후 관계 복원 실패", "중복 관계 정산 발생"처럼 어느 단계가 끊겼는지 알 수 있게 작성한다.

완료 조건:
- 최소 1개 이상의 PlayMode E2E가 실제 저장 슬롯 UI, 실제 이동 입력, 실제 NPC/하루 종료 상호작용 입력, 실제 결과 UI 버튼 경로로 전체 루프를 검증한다.
- 실제 플레이 경로로 NPC 대화/도움/퀘스트 행동을 수행하면 관계 또는 컨디션이 변한다.
- 하루 결과 UI가 관계/컨디션 변화와 다음 날 영향을 명확히 보여준다.
- 다음 날 NPC 대사, 선택지, 가능 행동 중 하나 이상이 관계/컨디션/튜토리얼 단계 기준으로 달라진다.
- 저장/로드 후 관계/컨디션/대사 상태가 유지된다.
- 같은 하루 결과를 다시 열거나 재호출해도 관계/컨디션 변화가 중복 적용되지 않는다.
- 관계/컨디션/대사 조건은 ScriptableObject 데이터와 전략 배열 기반이며 엔티티 ID별 분기가 없다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 SO 에셋, 실행한 테스트, 실제 플레이 검증 흐름, 통과/실패 결과, 남은 리스크를 포함한다.
```
