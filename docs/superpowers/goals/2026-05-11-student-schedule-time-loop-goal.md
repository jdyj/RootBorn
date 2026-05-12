# Student Schedule Time Loop Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "학생 싱글플레이에서 일차/요일/시간대에 따라 NPC 위치, 대사, 가능 활동, 퀘스트 노출이 데이터 기반으로 달라지고, 실제 플레이어가 하루를 넘기면 다음 스케줄 상태가 저장/로드 후에도 유지되는 생활 스케줄 루프"를 설계/구현/검증해줘.

이번 goal은 싱글플레이 완성도를 우선하지만, 스케줄/시간대/요일/일차 상태는 멀티플레이에서 서버 권한 공유 월드 상태로 승격 가능해야 한다. 개인 활동 로그, 관계, 컨디션, 퀘스트 진행은 플레이어별 상태로 분리하고, NPC 위치/활동 가능 여부/대사 조건은 공유 스케줄 데이터와 개인 조건을 함께 평가할 수 있는 구조로 설계한다.

전제:
- 학생 하루 루프는 실제 이동, 활동, 하루 종료, 결과 UI, 다음 날 시작, 저장/로드 복원이 동작한다.
- 하루 결과 UI는 학생 활동, 퀘스트, 보상, 인벤토리, 관계, 컨디션 변화를 표시할 수 있다.
- 튜토리얼/스토리 단계는 하루 종료와 다음 날 시작에 연결되어 있다.
- 멀티플레이 직접 검증은 이번 goal의 필수 완료 조건은 아니지만, 데이터/도메인 구조가 멀티플레이 확장을 막으면 안 된다.

핵심 의도:
1. "다음 날"이 숫자 증가에 그치지 않고 실제 월드 상태 변화로 느껴져야 한다.
2. 일차/요일/시간대에 따라 NPC 위치, 대사, 선택지, 활동 가능 여부, 퀘스트 노출이 달라져야 한다.
3. 플레이어는 오늘 무엇을 언제 할지 선택하고, 하루 종료 후 다음 스케줄로 이어지는 감각을 얻어야 한다.
4. 싱글플레이에서 먼저 완성하되, 추후 멀티플레이에서는 시간/스케줄이 서버 권한 공유 상태가 될 수 있어야 한다.

최우선 검증 원칙:
- 이 goal은 실제 유저가 플레이하듯 검증하지 않으면 완료로 인정하지 않는다.
- 내부 API 직접 호출, 상태 강제 세팅, 씬 강제 로드만으로 스케줄 변화가 검증되었다고 보고하면 실패다.
- 최소 1개 이상의 PlayMode E2E는 저장 슬롯 UI, 실제 이동 입력, 실제 NPC/활동/퀘스트 상호작용, 실제 하루 종료 입력, 실제 결과 UI 버튼을 거쳐야 한다.
- EditMode나 도메인 단위 테스트는 보조 검증으로만 인정한다.

금지:
- dayId, weekdayId, timeSlotId, npcId, activityId, questId별 `if/switch` 분기 금지.
- 특정 NPC/장소/활동별 C# 클래스 생성 금지.
- 테스트에서 현재 시간대나 스케줄 상태를 직접 세팅한 뒤 완료 보고 금지.
- 플레이어 Transform을 목적지로 직접 이동시킨 뒤 "실제 이동"으로 보고 금지.
- 멀티플레이 확장을 고려한다는 이유로 싱글플레이 실제 플레이 검증을 생략 금지.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- 스케줄, 시간대, NPC 배치, 대사 조건, 활동 가능 조건, 퀘스트 노출 조건은 ScriptableObject 데이터와 전략 배열로 표현한다.
- 보상/인벤토리/퀘스트/관계/컨디션 변화는 저장/로드 후 재호출해도 중복 적용되면 안 된다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

권장 도메인 구조:
- `WorldTimeState`
  - 현재 일차, 요일, 시간대, 스케줄 단계 같은 공유 월드 시간 상태를 표현한다.
  - 싱글플레이에서는 saveSlot 기준으로 저장하고, 멀티플레이에서는 서버 권한 공유 상태로 승격 가능해야 한다.
- `TimeSlotDefinition`
  - 아침/오후/저녁/밤 또는 프로젝트에 맞는 시간대를 SO로 정의한다.
- `ScheduleDefinition`
  - 특정 일차/요일/시간대에 적용되는 NPC 위치, 가능 활동, 퀘스트 노출, 대사 조건 묶음이다.
- `ScheduleRuleBase`
  - 일차/요일/시간대/튜토리얼 단계/관계/컨디션/퀘스트 상태를 평가하는 일반 조건 전략이다.
- `NpcScheduleEntry`
  - NPC가 어느 시간대에 어느 위치/대사 세트/선택지를 사용하는지 정의한다.
- `ActivityAvailabilityEntry`
  - 활동 오브젝트가 어느 시간대/조건에서 활성화되는지 정의한다.
- `QuestScheduleEntry`
  - 퀘스트가 어느 스케줄 조건에서 노출되거나 숨겨지는지 정의한다.

싱글플레이 목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임 또는 기존 슬롯을 로드해 Town에 진입한다.
2. 1일차 아침 또는 기본 시간대의 NPC 위치, 대사, 가능 활동, 퀘스트 노출을 확인한다.
3. 플레이어가 실제 이동으로 NPC 또는 활동 오브젝트에 접근한다.
4. 실제 상호작용 입력과 UI 버튼으로 대화/활동/퀘스트 중 하나 이상을 수행한다.
5. 활동 결과가 학생 성장, 퀘스트, 관계, 컨디션, 오늘 로그 중 하나 이상에 반영된다.
6. 플레이어가 실제 이동으로 하루 종료 지점에 접근한다.
7. 실제 상호작용 입력으로 하루를 종료한다.
8. 하루 결과 UI에서 오늘 결과와 다음 스케줄 안내를 확인한다.
9. 결과 UI 버튼으로 다음 날을 시작한다.
10. 2일차 또는 다음 시간대 Town에서 NPC 위치, 대사, 가능 활동, 퀘스트 노출 중 하나 이상이 달라진다.
11. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 현재 일차/요일/시간대와 스케줄 상태가 유지된다.
12. 같은 하루 결과를 다시 열거나 재호출해도 시간/스케줄 전환이 중복 적용되지 않는다.

멀티플레이 확장 고려사항:
- WorldTimeState는 플레이어별 개인 상태가 아니라 공유 월드 상태로 승격 가능해야 한다.
- 서버 권한 구조에서는 Host/Server가 현재 일차/요일/시간대를 확정하고 모든 클라이언트가 같은 스케줄 상태를 본다.
- 개인 활동 로그, 개인 하루 결과, 개인 관계/컨디션/퀘스트 진행은 플레이어별 상태로 유지한다.
- NPC 위치, 공용 활동 노출, 공용 퀘스트 노출은 공유 스케줄 데이터에서 평가한다.
- 대사/선택지는 공유 스케줄 조건과 개인 조건을 함께 평가할 수 있어야 한다.
- 이번 goal에서 멀티플레이 직접 실행 검증이 범위 밖이면, 후속 검증 항목과 구조상 남은 리스크를 최종 보고에 명시한다.

결과 UI 요구:
- 오늘 시간대 또는 일차/요일 표시
- 오늘 수행한 활동/퀘스트/관계/컨디션 변화 요약
- 다음 날 또는 다음 시간대 안내
- 새로 열린 NPC/활동/퀘스트가 있다면 표시
- UI는 Modern UI Style2 공통 패널 규약을 유지한다.

저장 대상:
- saveSlot
- player identity
- 현재 일차
- 현재 요일 또는 요일 계산에 필요한 값
- 현재 시간대 또는 스케줄 단계
- 오늘 완료한 활동 목록
- 오늘 결과 로그
- 이전 날 결과 요약
- 현재 튜토리얼/스토리 단계
- 관계/컨디션/퀘스트/인벤토리 상태
- 다음 날 시작 위치 또는 진입 지점

먼저 확인할 파일:
- `docs/superpowers/goals/2026-05-11-student-day-result-loop-goal.md`
- `docs/superpowers/goals/2026-05-11-student-relationship-condition-day-loop-goal.md`
- `docs/superpowers/specs/2026-05-08-student-career-life-design.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/World/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/UI/StudentLife/`
- `Assets/Scripts/UI/Quests/`
- `Assets/Data/StudentLife/`
- `Assets/Data/Locations/`
- `Assets/Data/NPCs/`
- `Assets/Data/Dialogue/`
- `Assets/Data/Quests/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/PlayMode/EndToEnd/`

필수 EditMode 테스트:
- SCHEDULE-EDIT-001: TimeSlotDefinition/ScheduleDefinition이 Registry 또는 지정 데이터 경로에서 로드된다.
- SCHEDULE-EDIT-002: WorldTimeState가 하루 종료 후 다음 일차/요일/시간대로 전환된다.
- SCHEDULE-EDIT-003: 같은 하루 결과를 재적용해도 WorldTimeState가 중복 전환되지 않는다.
- SCHEDULE-EDIT-004: NPC 스케줄 조건은 일차/요일/시간대/개인 조건을 엔티티별 분기 없이 평가한다.
- SCHEDULE-EDIT-005: 활동 가능 조건과 퀘스트 노출 조건은 SO 전략 배열로 평가된다.
- SCHEDULE-EDIT-006: 저장/로드 데이터에 현재 일차/요일/시간대/스케줄 단계가 포함된다.
- SCHEDULE-EDIT-007: WorldTimeState는 추후 멀티플레이 서버 권한 공유 상태로 감쌀 수 있도록 플레이어 개인 진행 데이터와 분리되어 있다.

필수 PlayMode E2E 시나리오:
- SCHEDULE-E2E-001: 저장 슬롯 UI로 새 게임을 만들고 Town에 진입한다.
- SCHEDULE-E2E-002: 1일차 기본 시간대에서 NPC 위치, 대사, 가능 활동, 퀘스트 노출 중 하나 이상을 화면에서 확인한다.
- SCHEDULE-E2E-003: 플레이어를 키보드 입력으로 NPC 또는 활동 지점까지 이동시킨다.
- SCHEDULE-E2E-004: 실제 상호작용 입력과 UI 경로로 대화/활동/퀘스트 중 하나 이상을 수행한다.
- SCHEDULE-E2E-005: 플레이어를 키보드 입력으로 하루 종료 지점까지 이동시킨다.
- SCHEDULE-E2E-006: 실제 상호작용 입력으로 하루 종료를 실행한다.
- SCHEDULE-E2E-007: 하루 결과 UI가 오늘 결과와 다음 스케줄 안내를 표시한다.
- SCHEDULE-E2E-008: 결과 UI 버튼으로 다음 날을 시작한다.
- SCHEDULE-E2E-009: 2일차 또는 다음 시간대 Town에서 NPC 위치, 대사, 가능 활동, 퀘스트 노출 중 하나 이상이 변경되었는지 화면에서 확인한다.
- SCHEDULE-E2E-010: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 현재 일차/요일/시간대와 스케줄 상태가 유지되는지 확인한다.
- SCHEDULE-E2E-011: 저장/로드 후 같은 날 결과를 다시 열어도 시간/스케줄 전환이 중복 적용되지 않는지 확인한다.
- SCHEDULE-E2E-012: 기존 학생 하루 루프, 퀘스트/인벤토리/관계/컨디션 E2E가 계속 통과하는지 확인한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, NpcInteractor, DialoguePanel, QuestLogPanel, StudentDayResultPanel, SaveSlotSelectPanel, EventSystem, UI Button을 사용한다.
- 플레이어 이동은 키보드/입력 시스템 경로로 수행한다. Transform 직접 이동은 완료 근거로 쓰지 않는다.
- NPC 위치/대사/활동/퀘스트 변화는 도메인 상태만 보지 말고 화면 텍스트, 버튼 상태, 오브젝트 활성 상태, 프롬프트 표시로 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, WorldTimeState, ScheduleResolver, DayProgress, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "1일차 스케줄 미표시", "NPC 위치 변경 없음", "대사 변경 없음", "활동 가능 조건 미적용", "스케줄 저장 실패", "시간 중복 전환 발생"처럼 어느 단계가 끊겼는지 알 수 있게 작성한다.

완료 조건:
- 실제 플레이 경로로 하루를 넘기면 일차/요일/시간대 또는 스케줄 단계가 전환된다.
- 다음 날 또는 다음 시간대에 NPC 위치, 대사, 가능 활동, 퀘스트 노출 중 하나 이상이 실제 화면에서 달라진다.
- 저장/로드 후 현재 일차/요일/시간대와 스케줄 상태가 유지된다.
- 같은 하루 결과를 다시 열거나 재호출해도 시간/스케줄 전환이 중복 적용되지 않는다.
- 스케줄/시간대/조건 평가는 ScriptableObject 데이터와 전략 배열 기반이며 엔티티 ID별 분기가 없다.
- 싱글플레이 구현이 멀티플레이의 서버 권한 공유 월드 시간 구조로 확장 가능하다는 점을 코드 구조와 최종 보고에서 설명한다.
- 멀티플레이 직접 검증을 하지 않았다면 범위 밖임을 명시하고, 후속 검증 항목과 리스크를 남긴다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 SO 에셋, 실행한 테스트, 실제 플레이 검증 흐름, 통과/실패 결과, 멀티플레이 확장 리스크를 포함한다.
```

