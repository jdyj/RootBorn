# Student Progression Save E2E Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "학생으로 시작해 학습/활동/선택을 통해 성향, 특성, 스킬, 진로 힌트가 누적되고, 그 진행도가 저장/로드 후에도 끊기지 않는 실제 플레이 가능한 초반 스토리 진행 루프"를 설계/구현/검증해줘.

최우선 검증 원칙:
이 goal은 "직접 유저가 플레이하듯 검증"하지 않으면 완료로 인정하지 않는다.

금지:
- 포탈 이동을 코드에서 SceneManager.LoadScene, 내부 메서드 호출, 상태 직접 주입만으로 대체 금지.
- QuestLog.Accept, QuestLog.RecordEvent, SaveService.Save/Load, Inventory.Add 같은 내부 API 직접 호출만으로 플레이 성공 처리 금지.
- 테스트 편의를 위해 플레이어 위치/진행도/보상을 코드로 바로 세팅하고 "검증했다"고 보고 금지.
- UI 패널이나 도메인 객체 상태만 확인하고 실제 이동/상호작용을 생략하는 것 금지.
- 내부 API 직접 호출 테스트만 통과시키고 실제 PlayMode E2E 검증 없이 완료 보고 금지.

필수:
- 플레이어 GameObject를 실제로 이동시킨다.
- NPC, 게시판, 학습 오브젝트, 자원, 포탈은 실제 Collider/Interaction/Prompt/Input/Button 경로로 상호작용한다.
- 포탈 이동은 플레이어를 포탈 위치까지 이동시킨 뒤 실제 상호작용 입력으로 진행한다.
- 퀘스트 수락/진행/완료/보상 수령은 실제 대화 UI, 버튼 클릭, 월드 상호작용을 통해 진행한다.
- 학습/수업/실습도 실제 UI 버튼 또는 월드 상호작용을 통해 실행한다.
- 저장/로드 검증은 실제 저장된 슬롯을 메인 메뉴 또는 저장 UI 흐름에서 다시 로드하는 방식으로 검증한다.
- 모든 핵심 완료 조건은 도메인 상태와 유저가 보는 UI 상태를 둘 다 확인한다.

핵심 문제:
1. 현재 학생 생활, 퀘스트, 인벤토리, 저장/로드 기능이 일부 만들어지고 있지만 실제 플레이 느낌상 매번 같은 자리만 도는 것처럼 보인다.
2. 플레이어가 무엇을 했고, 어디까지 진행했으며, 어떤 선택/학습/퀘스트/보상을 완료했는지가 저장 파일에 명확히 남아야 한다.
3. 저장/로드 후에도 인벤토리, 퀘스트 진행도, 학생 성장 진행도, 스토리 플래그, 해금 상태가 이어져야 한다.
4. 테스트는 내부 API 직접 호출만으로 인정하지 않는다. 반드시 실제 유저가 플레이하듯 씬 로드, UI 버튼, 상호작용, 이동, 포탈, 저장, 로드, 재진입까지 검증해야 한다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- 모든 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- activityId, questId, traitId, skillId, careerId, itemId, sceneName별 `if/switch` 분기, enum 기반 엔티티 분기, 엔티티별 C# 클래스 생성은 금지한다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 보상/인벤토리/퀘스트 완료는 원자적이고 멱등이어야 한다. 저장/로드 후 재호출해도 중복 지급이나 진행도 중복 증가가 없어야 한다.
- 기존 dirty 변경은 되돌리지 않는다.

목표 플레이 흐름:
1. 새 게임을 시작하면 플레이어는 학생 상태로 시작한다.
2. 첫날 또는 초반 튜토리얼 흐름에서 "수업/학습/실습/탐색" 중 하나 이상을 실제 상호작용으로 수행할 수 있다.
3. 학습은 한 가지를 깊게 파는 방식과 여러 분야를 얕게 경험하는 방식이 모두 가능해야 한다.
4. 선택 결과는 정답/오답이 아니라 성향과 성장 방향의 차이로 표현한다.
   - 예: 성실함, 호기심, 집중력, 창의성, 공감, 책임감
   - 예: 기초학습, 개발, 디자인, 발표, 서비스, 자기관리
5. 특정 성향/스킬 조건을 만족하면 진로 힌트나 다음 활동이 열린다.
6. 플레이어가 활동을 마치면 "무엇을 완료했는지", "무엇이 증가했는지", "다음에 무엇을 할 수 있는지"가 UI와 저장 데이터 양쪽에 반영되어야 한다.
7. 저장 후 메인 메뉴 또는 다른 씬으로 나갔다가 다시 로드하면 같은 진행도에서 이어져야 한다.

필수 저장 대상:
- saveSlot
- player identity
- 현재 스토리 단계 또는 튜토리얼 단계
- 완료한 학생 활동 목록
- 마지막/현재 진행 중인 활동
- 성향 값
- 스킬 진행도
- 진로 힌트 해금 상태
- 퀘스트 수락/진행/완료/보상 수령 상태
- 인벤토리 수량
- 이미 지급된 보상 기록
- 씬 전환 후 복원에 필요한 현재 위치 또는 진입 지점
- 활동 로그 또는 진행 로그

권장 구조:
- 기존 StudentLife, Quest, Inventory, SaveService, ActiveSaveContext, GameDataRegistry 구조를 먼저 읽고 재사용한다.
- 학생 활동은 `LifeActivityDefinition` 또는 기존 StudentLife 활동 정의를 사용한다.
- 선택지/활동 결과는 SO 전략 배열로 처리한다.
  - 예: TraitDeltaEffect
  - 예: SkillProgressEffect
  - 예: CareerHintUnlockEffect
  - 예: QuestEventEmitEffect
  - 예: StoryFlagSetEffect
  - 예: ActivityLogEffect
- 저장 데이터는 씬 오브젝트 생명주기에 묶지 말고 saveSlot/player identity 기준으로 복원한다.
- UI는 새로 생성되더라도 저장된 도메인 상태를 다시 바인딩해서 보여줘야 한다.

먼저 확인할 파일:
- `docs/superpowers/specs/2026-05-08-student-career-life-design.md`
- `docs/superpowers/goals/2026-05-08-student-trait-class-practice-goal.md`
- `docs/superpowers/goals/2026-05-10-town-portal-inventory-quest-persistence-goal.md`
- `docs/superpowers/goals/2026-05-10-strict-quest-playflow-goal.md`
- `Assets/Scripts/Game/StudentLife/`
- `Assets/Scripts/Game/Save/`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/Game/Player/PlayerInventory.cs`
- `Assets/Scripts/Game/Bootstrap/TownStudentLifeRuntimeInstaller.cs`
- `Assets/Scripts/UI/Quests/`
- `Assets/Tests/EditMode/StudentLife/`
- `Assets/Tests/PlayMode/TownConcept/`
- `Assets/Tests/PlayMode/EndToEnd/`

필수 실제 플레이 E2E 시나리오:
- STUDENT-E2E-001: 새 슬롯을 실제 UI로 생성하고 Town에 진입한다.
- STUDENT-E2E-002: 플레이어를 직접 이동시켜 학생 학습/수업/실습 상호작용 위치까지 간다.
- STUDENT-E2E-003: 실제 상호작용 입력 또는 UI 버튼 클릭으로 학습 활동을 수행한다.
- STUDENT-E2E-004: 활동 결과로 성향/스킬/진로 힌트/스토리 단계가 UI와 도메인 상태 양쪽에 반영되는지 확인한다.
- STUDENT-E2E-005: 실제 이동으로 NPC 또는 퀘스트 오브젝트에 접근하고, 대화 UI/버튼을 통해 퀘스트를 수락한다.
- STUDENT-E2E-006: 실제 이동으로 자원 또는 목표 오브젝트에 접근하고, 실제 상호작용으로 퀘스트 진행도를 올린다.
- STUDENT-E2E-007: QuestLog UI의 진행도와 도메인 QuestLog 상태가 함께 갱신되는지 확인한다.
- STUDENT-E2E-008: 실제 포탈 위치까지 이동한 뒤 포탈 상호작용 입력으로 Town -> Farm 또는 지정 씬 전환을 수행한다.
- STUDENT-E2E-009: 전환된 씬에서 인벤토리, 퀘스트, 학생 진행도, UI 표시가 유지되는지 확인한다.
- STUDENT-E2E-010: 실제 저장 슬롯 흐름으로 저장하고 메인 메뉴 또는 재시작 흐름을 거쳐 같은 슬롯을 다시 로드한다.
- STUDENT-E2E-011: 로드 후 같은 시작점 반복이 아니라 이전 진행 단계, 인벤토리, 퀘스트, 성향/스킬 상태에서 이어지는지 확인한다.
- STUDENT-E2E-012: 완료 보상은 저장/로드 후 다시 상호작용해도 중복 지급되지 않는지 확인한다.

테스트 작성 기준:
- PlayMode 테스트는 실제 GameObject, Collider2D, PlayerController 또는 이동 시스템, PlayerInteractionRouter, NpcInteractor, DialoguePanel, QuestLogPanel, PlayerInventory, SaveSlotSelectPanel, WorldPortal을 사용한다.
- 포탈 테스트는 SceneManager.LoadScene 직접 호출만으로 끝내지 않는다. 최소 1개 이상은 실제 포탈까지 이동하고 상호작용 입력으로 씬 전환한다.
- 퀘스트 테스트는 QuestLog API 직접 호출만으로 상태를 만들지 않는다. 실제 NPC 대화, 버튼 클릭, 월드 상호작용으로 상태를 만든다.
- 학습/학생 성장 테스트는 StudentLifeProgress에 값을 직접 넣지 않는다. 실제 학습/수업/실습 상호작용으로 상태를 만든다.
- 저장/로드 테스트는 저장 데이터 객체를 직접 조작하는 보조 검증은 허용하되, 최종 E2E 완료 조건은 실제 UI/슬롯/씬 재진입 흐름으로 확인한다.
- 임의 WaitForSeconds에 의존하지 말고 Player, UI, QuestLog, Inventory, Portal, SaveSlot 준비 조건을 명확히 기다린다.
- 실패 메시지는 "실제 이동 실패", "상호작용 프롬프트 미표시", "UI 미갱신", "도메인 상태 미갱신", "저장 복원 실패", "포탈 직접 상호작용 실패"처럼 어느 단계가 끊겼는지 알 수 있게 작성한다.

완료 조건:
- 학생 시작 루프가 실제 플레이 가능한 초반 스토리 진행으로 동작한다.
- 플레이어가 학습/활동을 수행하면 성향, 스킬, 진로 힌트 또는 스토리 단계 중 하나 이상이 눈에 보이게 변한다.
- 저장 파일에 "어디까지 진행했는지"가 명확히 남는다.
- 저장/로드 후 동일한 자리에서 반복 시작하지 않고 이전 진행 상태에서 이어진다.
- 인벤토리, 퀘스트, 보상, 학생 성장 진행도가 저장/로드/씬 전환 후에도 유지된다.
- 포탈 이동은 실제 플레이어 이동과 상호작용으로 검증된다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 실행한 테스트, 통과/실패 결과, 실제 플레이 검증 흐름, 남은 리스크를 포함한다.
```

## Constitutional Addition

아래 문구를 AGENTS.md 또는 `.claude/rules/testing-discipline.md`에 추가하는 것을 권장한다.

```md
## 실제 플레이 검증 절대 원칙

진행도, 저장/로드, 퀘스트, 인벤토리, 포탈 이동, 학생 성장 루프는 내부 API 직접 호출만으로 검증 완료 처리할 수 없다. 핵심 시나리오는 반드시 PlayMode에서 유저처럼 직접 이동하고, Collider/Interaction/Prompt/Input/Button/UI를 통해 상호작용하며, 포탈도 코드 이동이 아니라 실제 위치 접근과 상호작용으로 넘어가야 한다. 도메인 상태와 UI 표시를 함께 검증하지 않은 기능은 완료로 인정하지 않는다.
```
