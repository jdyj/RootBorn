# Open Ended Milestone Growth Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "학생이 학교, 마을 도움 행동, 독학, 알바, 탐험 중 원하는 경로를 선택해도 장기 목표/마일스톤 진행도가 쌓이고, 하루 결과 UI와 HUD가 다음 목표를 보여주며, 저장/로드 후 진행도가 유지되는 자유 경로 목표 루프"를 설계/구현/검증해줘.

전제:
- 학교 밖 성장 기반(`2026-05-12-outside-school-growth-foundation-goal.md`)과 도움 행동/독학/알바/탐험 루트가 존재하거나, 이 goal에서 최소 샘플 대체 경로 2개를 통해 검증 가능해야 한다.
- 자유 경로 성장 원칙에 따라 학교 수업은 성장 경로 중 하나일 뿐 유일한 정답이 아니다.
- 하루 결과 UI는 학교 활동과 학교 밖 활동 결과를 표시할 수 있다.

핵심 의도:
1. 플레이어에게 "오늘 무엇을 왜 해야 하는가"를 보여주는 장기 목표 구조를 만든다.
2. 같은 마일스톤은 학교 루트와 학교 밖 루트 중 여러 방식으로 진행될 수 있어야 한다.
3. 마일스톤은 특성·성향·스킬·진로 힌트·관계·컨디션·퀘스트·인벤토리·지식·탐험 발견 같은 기존 시스템을 하나의 목표로 묶는다.
4. 하루 결과 UI와 HUD가 현재 목표, 진행도, 다음 추천 행동을 명확히 보여줘야 한다.

최우선 검증 원칙:
- 이 goal은 실제 유저가 플레이하듯 검증하지 않으면 완료로 인정하지 않는다.
- 최소 1개 이상의 PlayMode E2E는 저장 슬롯 UI, 실제 키보드 이동, 실제 상호작용, 하루 종료, 결과 UI, 저장/로드 재진입을 거쳐야 한다.
- 최소 1개 이상의 PlayMode E2E는 학교 수업이 아닌 경로로 마일스톤 진행도를 올려야 한다.
- 가능하면 같은 마일스톤을 학교 루트와 학교 밖 루트 두 방식으로 각각 진행하는 테스트를 포함한다.
- 테스트에서 MilestoneProgress, Trait/Skill/Quest/Inventory/Relationship/Status 상태를 직접 세팅해서 성공 처리하면 실패다.
- 플레이어 Transform 직접 이동, 내부 메서드 직접 호출, 씬 강제 로드, 저장 데이터 직접 주입으로 실제 플레이 경로를 대체하면 완료로 보고하지 않는다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- milestoneId, objectiveId, routeId, activityId, questId, traitId, skillId, locationId별 `if/switch` 분기, enum 기반 엔티티 분기, 엔티티별 C# 클래스 생성은 금지한다.
- 마일스톤, 목표 조건, 진행도 계산, 보상, 추천 행동은 SO 데이터와 전략 배열로 표현한다.
- 동일 마일스톤 목표에는 가능한 한 2개 이상의 대체 진행 경로를 제공한다.
- 보상 지급은 인벤토리 수용 가능 여부와 중복 수령 여부를 검증하고 원자적으로 처리한다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

권장 구조:
- `MilestoneDefinition`
  - 표시명, 설명, 활성 조건, 목표 배열, 보상 배열, 추천 행동 배열을 정의한다.
- `MilestoneObjectiveBase[]`
  - 특성 수치, 스킬 진행도, 관계 수치, 컨디션 유지, 퀘스트 상태, 인벤토리 수량, 지식 해금, 발견 상태, 학교 밖 활동 로그 등을 일반 조건으로 평가한다.
- `MilestoneRouteDefinition`
  - 같은 목표를 학교/도움 행동/독학/알바/탐험 같은 여러 경로로 진행할 수 있음을 데이터로 표현한다.
  - C# enum 대신 SO 정의를 사용한다.
- `MilestoneRewardBase[]`
  - 아이템/재화, 진로 힌트, 지식, 관계, 특성/스킬 보상, 퀘스트 해금을 일반 전략으로 지급한다.
- `MilestoneProgress`
  - 플레이어별 진행 상태, 완료 여부, 보상 수령 여부, 마지막 진행 로그를 저장한다.
- `MilestoneProgressPersistence`
  - saveSlot + player identity 기준으로 저장/로드한다.
- `MilestoneHudPanel`
  - 현재 추적 중인 마일스톤, 진행도, 다음 추천 행동을 보여준다.
- `StudentDayResultPanel` 연동
  - 오늘 마일스톤 진행 변화와 완료/보상 수령 가능 상태를 하루 결과 UI에 표시한다.

목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임 또는 기존 슬롯을 로드해 Town에 진입한다.
2. HUD에 현재 초반 마일스톤과 진행도가 표시된다.
3. 플레이어가 학교 수업 루트 또는 학교 밖 루트 중 하나를 선택한다.
4. 실제 이동/상호작용으로 활동, 도움 행동, 독학, 알바, 탐험, 퀘스트 중 하나를 수행한다.
5. 수행 결과가 마일스톤 목표 조건 중 하나에 반영된다.
6. HUD 또는 즉시 피드백 UI에 마일스톤 진행도가 갱신된다.
7. 플레이어가 실제 이동으로 하루 종료 지점에 접근한다.
8. 실제 상호작용 입력으로 하루를 종료한다.
9. 하루 결과 UI에 오늘 마일스톤 진행 변화, 완료 여부, 다음 추천 행동이 표시된다.
10. 결과 UI 버튼으로 다음 날을 시작한다.
11. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 마일스톤 진행도, 완료 여부, 보상 수령 여부가 유지된다.
12. 같은 활동 결과 또는 같은 하루 결과를 재호출해도 마일스톤 진행도/보상이 중복 적용되지 않는다.

자유 경로 검증 요구:
- 최소 하나의 마일스톤은 학교 수업 없이도 진행 가능해야 한다.
- 최소 하나의 마일스톤은 2개 이상의 경로로 진행 가능해야 한다.
  - 예: "성실함 5 달성"은 수업 참여, 도서관 독학, NPC 도움 행동 중 2개 이상으로 진행 가능.
  - 예: "첫 진로 힌트 발견"은 학교 활동, 알바, 탐험 발견 중 2개 이상으로 진행 가능.
- 테스트는 학교 루트와 학교 밖 루트가 같은 마일스톤을 진행할 수 있음을 실제 플레이 또는 최소 PlayMode 상호작용 경로로 확인해야 한다.

초반 마일스톤 기획 예시:
- `마을에 적응하기`
  - 목적: 학교 밖 자유 경로를 소개한다.
  - 진행 조건: 아래 조건 중 2개 이상 달성.
    - NPC 도움 행동 1회
    - 독학 또는 학교 수업 1회
    - 새 장소 1곳 발견
    - 아무 주민과 대화 2회
  - 보상:
    - 진로 힌트 1개
    - 사교성 또는 성실함 소폭 증가
    - 다음 날 신규 대사 또는 퀘스트 해금
- `나만의 학습 방식 찾기`
  - 목적: 학교 수업만 정답이 아님을 보여준다.
  - 진행 조건: 아래 조건 중 2개 이상 달성.
    - 학교 수업 1회
    - 도서관 독학 1회
    - 집에서 독학 1회
    - 지식 노드 1개 해금
  - 보상:
    - 집중 회복 보너스
    - 기초 학습 스킬 증가
    - 진로 힌트 `학습형 루트` 해금
- `하루 균형 잡기`
  - 목적: 컨디션/피로 관리 루프를 소개한다.
  - 진행 조건:
    - 하루 동안 활동 2회 이상 수행
    - 하루 종료 시 피로가 기준치 이하
    - 휴식 또는 가벼운 활동 1회 수행
  - 보상:
    - 컨디션 관리 관련 지식
    - 다음 날 피로 회복량 증가
    - 꾸준함 계열 성향 증가
- `첫 관계 만들기`
  - 목적: 관계 시스템을 소개한다.
  - 진행 조건: 아래 조건 중 2개 이상 달성.
    - 같은 NPC와 대화 2회
    - NPC 도움 행동 1회
    - 관계 수치 기준치 이상 달성
    - 관계 기반 선택지 1회 선택
  - 보상:
    - 관계 대사 해금
    - 해당 NPC의 작은 퀘스트 해금
    - 사교성 증가
- `진로의 단서`
  - 목적: 진로 힌트 시스템을 소개한다.
  - 진행 조건: 아래 조건 중 3개 이상 달성.
    - 학교 활동 1회
    - 학교 밖 활동 1회
    - 탐험 발견 1회
    - 알바 또는 도움 행동 1회
    - 지식 또는 진로 힌트 1개 해금
  - 보상:
    - 첫 진로 후보 표시
    - 목표 HUD에 진로 관련 장기 목표 추가

마일스톤 기획 원칙:
- 위 예시는 구현 시 그대로 고정하지 말고, 현재 데이터 구조와 테스트 가능 범위에 맞춰 SO 데이터로 조정한다.
- 마일스톤은 단일 루트 완료형보다 "조건 중 N개 달성" 방식을 우선한다.
- 각 마일스톤은 학교 루트와 학교 밖 루트가 함께 진행 조건에 들어가야 한다.
- 보상은 `MilestoneRewardBase[]` 같은 전략 배열로 지급하고, 특정 마일스톤 ID별 C# 분기로 처리하지 않는다.

멀티플레이 확장 고려:
- 마일스톤 진행은 기본적으로 플레이어별 개인 상태로 저장한다.
- 공유 월드 마일스톤이 필요한 경우 개인 마일스톤과 별도 정의/저장 경로를 사용한다.
- 멀티플레이에서는 서버 권한으로 진행도 증가와 보상 수령을 확정할 수 있어야 한다.
- 월드 시간/스케줄은 공유 상태로, 개인 마일스톤 진행은 플레이어별 상태로 분리한다.
- 이번 goal에서 멀티플레이 직접 검증이 범위 밖이면 최종 보고에 후속 검증 항목과 리스크를 명시한다.

필수 저장 대상:
- saveSlot
- player identity
- 활성 마일스톤 id 목록
- 마일스톤별 목표 진행도
- 마일스톤 완료 여부
- 마일스톤 보상 수령 여부
- 마지막 진행 로그
- 오늘 마일스톤 진행 변화 요약
- 추천 다음 행동 표시 상태

먼저 확인할 파일:
- `docs/superpowers/goals/2026-05-12-outside-school-growth-foundation-goal.md`
- `docs/superpowers/goals/2026-05-11-town-help-action-growth-goal.md`
- `docs/superpowers/goals/2026-05-11-self-study-library-growth-goal.md`
- `docs/superpowers/goals/2026-05-11-part-time-work-growth-goal.md`
- `docs/superpowers/goals/2026-05-11-town-exploration-discovery-goal.md`
- `.claude/rules/game-design.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/UI/StudentLife/`
- `Assets/Scripts/UI/Quests/`
- `Assets/Data/StudentLife/`
- `Assets/Data/Quests/`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/PlayMode/EndToEnd/`

필수 EditMode 테스트:
- MILESTONE-EDIT-001: MilestoneDefinition과 목표/보상/경로 정의가 Registry 또는 지정 데이터 경로에서 로드된다.
- MILESTONE-EDIT-002: 목표 조건은 엔티티 ID별 분기 없이 trait/skill/relationship/status/quest/inventory/knowledge/discovery/outside-school-log 상태를 평가한다.
- MILESTONE-EDIT-003: 같은 마일스톤 목표가 2개 이상의 route definition을 통해 진행 가능하다.
- MILESTONE-EDIT-004: 학교 밖 활동 로그만으로도 최소 하나의 마일스톤 진행도가 증가한다.
- MILESTONE-EDIT-005: 완료 보상 지급 전 중복 수령 여부와 인벤토리 수용 가능 여부를 검증한다.
- MILESTONE-EDIT-006: 저장/로드 후 진행도, 완료 여부, 보상 수령 여부가 복원된다.
- MILESTONE-EDIT-007: 같은 request id 또는 같은 하루 결과를 재적용해도 진행도/보상이 중복 적용되지 않는다.
- MILESTONE-EDIT-008: 하루 결과 summary에 오늘 마일스톤 진행 변화와 다음 추천 행동이 포함된다.

필수 PlayMode E2E 시나리오:
- MILESTONE-E2E-001: 저장 슬롯 UI로 Town에 진입한다.
- MILESTONE-E2E-002: HUD에 활성 마일스톤과 진행도가 표시된다.
- MILESTONE-E2E-003: 플레이어가 학교 수업 없이 실제 이동으로 학교 밖 활동 지점에 접근한다.
- MILESTONE-E2E-004: 실제 Prompt/Input/Button 경로로 학교 밖 활동을 수행한다.
- MILESTONE-E2E-005: 학교 밖 활동 결과로 마일스톤 진행도가 UI와 도메인 상태에 함께 반영된다.
- MILESTONE-E2E-006: 하루 종료 결과 UI에 오늘 마일스톤 진행 변화와 다음 추천 행동이 표시된다.
- MILESTONE-E2E-007: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 마일스톤 진행도가 유지된다.
- MILESTONE-E2E-008: 같은 결과를 재확인해도 마일스톤 진행도/보상이 중복 적용되지 않는다.
- MILESTONE-E2E-009: 가능하면 같은 마일스톤을 학교 루트와 학교 밖 루트 두 방식으로 각각 진행 가능함을 확인한다.
- MILESTONE-E2E-010: 기존 학생 하루 루프와 학교 밖 성장 E2E가 계속 통과한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, MilestoneHudPanel, StudentDayResultPanel, SaveSlotSelectPanel, EventSystem, UI Button을 사용한다.
- 플레이어 이동은 키보드/입력 시스템 경로로 수행한다. Transform 직접 이동은 완료 근거로 쓰지 않는다.
- 마일스톤 진행은 도메인 상태만 보지 말고 HUD 텍스트, 하루 결과 UI 텍스트, 보상 버튼 상태 중 하나 이상으로 함께 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, MilestoneProgress, DayProgress, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "마일스톤 HUD 미표시", "학교 밖 경로 진행도 미반영", "대체 경로 불가", "하루 결과 마일스톤 요약 누락", "저장 후 진행도 복원 실패", "중복 보상 지급"처럼 어느 단계가 끊겼는지 알 수 있게 작성한다.

완료 조건:
- 장기 목표/마일스톤이 데이터 기반으로 정의되고 Registry에서 로드된다.
- 최소 하나의 마일스톤이 학교 수업 없이 학교 밖 경로로 진행된다.
- 최소 하나의 마일스톤이 2개 이상의 대체 경로로 진행 가능하다.
- HUD와 하루 결과 UI가 마일스톤 진행도, 완료 여부, 다음 추천 행동을 보여준다.
- 저장/로드 후 마일스톤 진행도, 완료 여부, 보상 수령 여부가 유지된다.
- 같은 활동 결과 또는 같은 하루 결과를 재호출해도 진행도/보상이 중복 적용되지 않는다.
- 보상/인벤토리 트랜잭션 원칙을 위반하지 않는다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 생성/수정한 SO 에셋, 실행한 테스트, 실제 플레이 검증 흐름, 자유 경로 검증 결과, 통과/실패 결과, 멀티플레이 확장 리스크를 포함한다.
```
