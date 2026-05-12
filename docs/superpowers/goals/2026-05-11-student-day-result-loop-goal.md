# Student Day Result Loop Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "학생이 하루 동안 실제 이동과 상호작용으로 학습/활동/퀘스트를 수행하고, 하루 종료 시 결과 요약을 확인한 뒤 다음 날 같은 진행 상태에서 이어지는 초반 하루 루프"를 설계/구현/검증해줘.

최우선 검증 원칙:
이 goal은 "직접 유저가 플레이하듯 검증"하지 않으면 완료로 인정하지 않는다. 내부 API 직접 호출, 상태 강제 세팅, 씬 강제 로드만으로 하루 진행/종료/다음 날 시작을 통과시키면 실패로 본다.

금지:
- 하루 종료를 내부 메서드 호출만으로 처리하고 완료 보고 금지.
- StudentLifeProgress, QuestLog, Inventory, SaveService, DayProgress 같은 도메인 객체 상태를 테스트에서 직접 세팅해서 성공 처리 금지.
- 플레이어 위치를 목적지에 강제 세팅한 뒤 "이동했다"고 보고 금지.
- UI 결과 패널을 직접 생성/바인딩만 하고 실제 활동 완료/하루 종료 상호작용을 생략 금지.
- 포탈 이동, 저장/로드, 퀘스트 수락/보상 수령을 SceneManager.LoadScene 또는 내부 API 직접 호출로 대체 금지.
- 기존 dirty 변경 되돌리기 금지.

필수:
- 플레이어 GameObject를 실제 키보드/입력 경로로 이동시킨다.
- 학습 오브젝트, NPC, 자원, 포탈, 하루 종료 지점은 실제 Collider/Interaction/Prompt/Input/Button 경로로 상호작용한다.
- 하루 종료는 침대/집/귀가 지점/하루 마감 게시판 같은 월드 오브젝트에 접근해 실제 상호작용 입력으로 실행한다.
- 하루 결과 UI는 실제 활동 결과를 기반으로 열려야 하며, 테스트에서 패널만 강제로 띄우면 안 된다.
- 다음 날 시작은 결과 UI의 버튼 또는 실제 저장 슬롯 재진입 흐름으로 진행한다.
- 핵심 완료 조건은 도메인 상태와 유저가 보는 UI 상태를 둘 다 확인한다.

핵심 문제:
1. 현재 학생 성장, 퀘스트, 인벤토리, 포탈, 저장/로드 E2E는 동작하지만 "하루가 지나갔다"는 플레이 감각이 약하다.
2. 플레이어가 오늘 무엇을 했고, 무엇이 증가했고, 다음 날 어떤 상태로 이어지는지가 UI와 저장 파일 양쪽에 명확히 남아야 한다.
3. 에너지/집중도/시간이 활동 후 변하지만 하루 종료와 다음 날 회복/정산 규칙이 플레이 루프로 연결되어 있지 않다.
4. 학생 생활 루프는 반복 가능한 하루 단위 구조가 있어야 이후 진로, 직업, 관계, 가족, 상태이상, 네트워크 확장에 안전하게 연결된다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- activityId, questId, traitId, skillId, careerId, itemId, sceneName별 `if/switch` 분기, enum 기반 엔티티 분기, 엔티티별 C# 클래스 생성은 금지한다.
- 하루 종료/회복/결과 규칙도 하드코딩 분기가 아니라 데이터/전략 구조로 표현한다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 보상/인벤토리/퀘스트 완료는 원자적이고 멱등이어야 한다. 저장/로드 후 재호출해도 중복 지급이나 진행도 중복 증가가 없어야 한다.

목표 플레이 흐름:
1. 새 게임 또는 기존 저장 슬롯 로드로 Town에 진입한다.
2. 플레이어가 실제 이동으로 학습/활동 오브젝트에 접근한다.
3. 실제 상호작용 입력으로 학생 활동을 수행한다.
4. 활동 결과로 성향/스킬/진로 힌트/튜토리얼 단계/오늘 활동 로그 중 하나 이상이 갱신된다.
5. 필요하면 NPC/퀘스트/자원 상호작용을 실제 UI와 월드 입력으로 수행한다.
6. 플레이어가 실제 이동으로 하루 종료 지점에 접근한다.
7. 실제 상호작용 입력으로 하루 종료를 실행한다.
8. 하루 결과 UI가 열린다.
9. 결과 UI에는 오늘 완료한 활동, 증가한 성향/스킬, 해금된 진로 힌트, 퀘스트/보상 변화, 다음 날 안내가 표시된다.
10. 결과 UI 버튼 또는 저장 슬롯 재진입 흐름으로 다음 날을 시작한다.
11. 다음 날에는 날짜/일차가 증가하고, 오늘 로그가 새로 시작되며, 누적 성장/퀘스트/인벤토리는 유지된다.
12. 저장 후 메인 메뉴에서 같은 슬롯을 다시 로드해도 일차, 누적 성장, 오늘/이전 로그, 다음 날 시작 상태가 유지된다.

필수 저장 대상:
- saveSlot
- player identity
- 현재 일차 또는 날짜
- 현재 하루 상태(진행 중/결과 표시/다음 날 시작 가능)
- 오늘 완료한 학생 활동 목록
- 오늘 활동 결과 로그
- 마지막 활동 id
- 튜토리얼/스토리 단계
- 성향 값
- 스킬 진행도
- 진로 힌트 해금 상태
- 퀘스트 수락/진행/완료/보상 수령 상태
- 인벤토리 수량
- 씬 전환 후 복원에 필요한 현재 위치 또는 진입 지점
- 하루 종료 후 다음 날 시작 위치 또는 진입 지점

권장 구조:
- 기존 `StudentLifeProgress`, `StudentLifeProgressPersistence`, `SaveService`, `ActiveSaveContext`, `PlayerIdentity`, `WorldPortal`, `QuestLogPanel`, `SaveSlotSelectPanel` 구조를 먼저 읽고 재사용한다.
- 하루 진행 데이터는 saveSlot/player identity 기준으로 복원한다.
- `StudentDayProgress` 또는 유사한 작은 도메인 모델을 두되, 엔티티별 분기 없이 일반적인 하루 상태/로그를 저장한다.
- 하루 종료 규칙은 SO 데이터 또는 전략 배열로 표현한다.
  - 예: `DayEndRuleDefinition`
  - 예: `EnergyRestoreRule`
  - 예: `FocusRestoreRule`
  - 예: `DailyLogSummaryRule`
  - 예: `TutorialStageAdvanceRule`
- 결과 UI는 도메인 상태에서 다시 바인딩 가능해야 하며 씬 오브젝트 생명주기에 묶지 않는다.
- 결과 요약 UI는 Modern UI Style2 공통 패널 규약을 지킨다.

먼저 확인할 파일:
- `docs/superpowers/goals/2026-05-10-student-progression-save-e2e-goal.md`
- `docs/superpowers/specs/2026-05-08-student-career-life-design.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/Game/World/WorldPortal.cs`
- `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`
- `Assets/Scripts/UI/Quests/`
- `Assets/Scripts/UI/Modern/`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/PlayMode/EndToEnd/StudentFullProgressionE2EScenarioTests.cs`
- `Assets/Tests/PlayMode/TownConcept/`

필수 실제 플레이 E2E 시나리오:
- STUDENT-DAY-E2E-001: 저장 슬롯 UI로 새 게임을 만들고 Town에 진입한다.
- STUDENT-DAY-E2E-002: 플레이어를 키보드 입력으로 학생 활동 오브젝트까지 이동시킨다.
- STUDENT-DAY-E2E-003: 실제 상호작용 입력으로 학습/활동을 수행한다.
- STUDENT-DAY-E2E-004: 활동 결과가 도메인 상태와 UI에 함께 반영되는지 확인한다.
- STUDENT-DAY-E2E-005: 플레이어를 키보드 입력으로 하루 종료 지점까지 이동시킨다.
- STUDENT-DAY-E2E-006: 실제 상호작용 입력으로 하루 종료를 실행한다.
- STUDENT-DAY-E2E-007: 하루 결과 UI가 오늘 활동, 증가한 성향/스킬, 해금 정보, 다음 안내를 표시하는지 확인한다.
- STUDENT-DAY-E2E-008: 결과 UI의 실제 버튼 입력으로 다음 날을 시작한다.
- STUDENT-DAY-E2E-009: 다음 날 일차가 증가하고 오늘 로그가 초기화되며 누적 성장/퀘스트/인벤토리는 유지되는지 확인한다.
- STUDENT-DAY-E2E-010: 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 일차와 누적 상태가 복원되는지 확인한다.
- STUDENT-DAY-E2E-011: 하루 종료 버튼/상호작용을 저장/로드 후 반복해도 같은 날 결과가 중복 정산되지 않는지 확인한다.
- STUDENT-DAY-E2E-012: 하루 종료와 다음 날 시작 흐름 중 포탈/퀘스트/인벤토리 기존 E2E가 깨지지 않는지 확인한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, UI Button/EventSystem, SaveSlotSelectPanel을 사용한다.
- 하루 종료 테스트는 내부 `EndDay()` 직접 호출만으로 완료 처리하지 않는다. 최소 1개 이상은 실제 하루 종료 오브젝트까지 이동하고 상호작용 입력으로 결과 UI를 열어야 한다.
- 결과 UI 테스트는 도메인 상태만 보지 말고 화면 텍스트/버튼 상태를 같이 확인한다.
- 저장/로드 테스트는 저장 데이터 객체를 직접 조작하는 보조 검증은 허용하되, 최종 E2E 완료 조건은 실제 UI/슬롯/씬 재진입 흐름으로 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, DayProgress, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "실제 이동 실패", "하루 종료 프롬프트 미표시", "결과 UI 미표시", "일차 저장 실패", "다음 날 복원 실패", "중복 정산 발생"처럼 어느 단계가 끊겼는지 알 수 있게 작성한다.

완료 조건:
- 학생이 실제 플레이로 하루 활동을 수행하고 하루 종료까지 진행할 수 있다.
- 하루 결과 UI가 오늘 무엇을 했고 무엇이 증가했는지 명확히 보여준다.
- 다음 날 시작 시 일차/하루 상태가 갱신되고 누적 성장/퀘스트/인벤토리는 유지된다.
- 저장 파일에 일차, 오늘 로그, 마지막 활동, 튜토리얼/스토리 단계, 다음 날 시작 상태가 명확히 남는다.
- 저장/로드 후 같은 슬롯에서 이전 진행 상태로 이어진다.
- 하루 종료/결과 정산은 저장/로드 후 반복해도 중복 적용되지 않는다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 실행한 테스트, 통과/실패 결과, 실제 플레이 검증 흐름, 남은 리스크를 포함한다.
```

## Constitutional Addition

아래 문구를 `.claude/rules/testing-discipline.md`에 추가하는 것을 권장한다.

```md
## 하루 루프 실제 플레이 검증 원칙

하루 진행, 하루 종료, 결과 정산, 다음 날 시작, 저장/로드 복원은 내부 API 직접 호출만으로 검증 완료 처리할 수 없다. 핵심 시나리오는 반드시 PlayMode에서 유저처럼 이동하고, 하루 종료 오브젝트의 Collider/Interaction/Prompt/Input을 거쳐 결과 UI를 열며, 결과 UI 버튼 또는 저장 슬롯 UI 흐름으로 다음 날 상태를 검증해야 한다. 도메인 상태와 UI 표시를 함께 검증하지 않은 하루 루프 기능은 완료로 인정하지 않는다.
```
