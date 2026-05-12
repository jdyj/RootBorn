# Student Day Result Quest Inventory Summary Goal Prompt

아래 프롬프트를 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "하루 결과 UI가 학생 활동 성장뿐 아니라 오늘의 퀘스트/보상/인벤토리 변화까지 보여주는 닫힌 하루 결과 요약"을 설계/구현/검증해줘.

배경:
- 학생 하루 루프는 실제 이동, 활동, 하루 종료, 결과 UI, 다음 날 시작, 저장/로드 복원이 동작한다.
- 현재 결과 UI는 학생 활동과 성향/스킬 성장 중심이라 하루 동안 수행한 퀘스트, 보상, 인벤토리 변화의 플레이 감각이 약하다.
- 이 goal은 기존 퀘스트/인벤토리/포탈 E2E를 유지하면서 하루 결과 패널을 "오늘 한 일의 전체 요약"으로 확장한다.

최우선 검증 원칙:
- 실제 플레이 경로로 퀘스트/자원/보상/하루 종료를 수행해야 한다.
- QuestLog, Inventory, StudentLifeProgress, SaveService 상태를 테스트에서 직접 세팅해서 결과 UI만 통과시키면 실패다.
- 보상/인벤토리 정산은 저장/로드 후 재호출해도 중복 지급되지 않아야 한다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- questId, itemId, activityId, resourceId별 `if/switch` 분기, enum 기반 엔티티 분기, 엔티티별 C# 클래스 생성은 금지한다.
- 퀘스트/인벤토리/보상 변화는 일반 이벤트/스냅샷/로그 구조로 기록한다.
- 보상 지급 전 인벤토리 수용 가능 여부와 중복 수령 여부를 검증한다.
- TDD로 진행한다. 먼저 실패하는 테스트를 만들고 실패를 확인한 뒤 구현한다.
- 기존 dirty 변경 되돌리기 금지.

목표 플레이 흐름:
1. 저장 슬롯 UI로 새 게임 또는 기존 슬롯을 로드해 Town에 진입한다.
2. 플레이어가 실제 이동으로 NPC/퀘스트 시작 지점에 접근한다.
3. 실제 상호작용 입력과 UI 경로로 퀘스트를 수락한다.
4. 플레이어가 실제 이동과 상호작용으로 자원 또는 활동 목표를 수행한다.
5. 퀘스트 진행/완료/보상 수령 상태와 인벤토리 수량이 실제 경로로 바뀐다.
6. 플레이어가 실제 이동으로 하루 종료 지점에 접근한다.
7. 실제 상호작용 입력으로 하루를 종료한다.
8. 하루 결과 UI에 오늘 활동, 성향/스킬 증가, 퀘스트 변화, 보상 변화, 인벤토리 변화, 다음 날 안내가 표시된다.
9. 결과 UI 버튼으로 다음 날을 시작한다.
10. 저장 슬롯 UI로 같은 슬롯을 다시 로드해도 결과 요약의 기준 상태와 누적 인벤토리/퀘스트 상태가 유지된다.

권장 구조:
- 하루 시작 또는 활동 전후에 QuestLog/Inventory의 일반 스냅샷을 저장하고, 하루 종료 시 차이를 요약한다.
- 엔티티별 분기 없이 `QuestSummaryEntry`, `InventoryDeltaEntry`, `RewardSummaryEntry` 같은 일반 DTO를 사용한다.
- 결과 UI는 도메인 요약 모델만 바인딩하고, 퀘스트/아이템별 표시명은 SO 데이터 또는 기존 Registry 표시 정보를 사용한다.
- 이미 존재하는 `StudentDaySummary` 또는 유사 구조를 확장하되, 패널이 특정 퀘스트/아이템을 알지 않게 한다.
- 결과 UI는 Modern UI Style2 공통 패널 규약을 유지한다.

먼저 확인할 파일:
- `Assets/Scripts/Game/StudentLife/StudentLifeCore.cs`
- `Assets/Scripts/UI/StudentLife/StudentDayResultPanel.cs`
- `Assets/Scripts/Game/Quests/`
- `Assets/Scripts/UI/Quests/`
- `Assets/Scripts/Game/Inventory/`
- `Assets/Scripts/Game/Save/`
- `Assets/Tests/PlayMode/EndToEnd/StudentFullProgressionE2EScenarioTests.cs`
- `Assets/Tests/PlayMode/EndToEnd/StudentDayResultLoopE2EScenarioTests.cs`

필수 테스트:
- EditMode: 퀘스트/인벤토리 스냅샷 차이가 엔티티별 분기 없이 요약 엔트리로 계산된다.
- EditMode: 동일 보상 수령 상태를 다시 요약해도 중복 지급이나 중복 delta가 생기지 않는다.
- PlayMode: 실제 저장 슬롯 UI → NPC 퀘스트 수락 → 실제 이동/상호작용으로 목표 수행 → 보상 수령 → 하루 종료 → 결과 UI에 퀘스트/보상/인벤토리 변화가 표시된다.
- PlayMode: 같은 슬롯 재로드 후 하루 종료 결과 재확인 또는 다음 날 시작 시 퀘스트/인벤토리 상태가 유지되고 중복 지급되지 않는다.
- PlayMode: 기존 포탈/퀘스트/인벤토리 full progression E2E가 계속 통과한다.

완료 조건:
- 하루 결과 UI가 오늘 완료한 학생 활동과 성장뿐 아니라 퀘스트/보상/인벤토리 변화를 명확히 보여준다.
- 결과 요약은 저장/로드 후에도 재구성 가능하거나 필요한 요약 데이터가 저장된다.
- 보상/인벤토리 트랜잭션 원칙을 위반하지 않는다.
- 관련 EditMode/PlayMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 수정 파일, 실행한 테스트, 실제 플레이 검증 흐름, 통과/실패 결과, 남은 리스크를 포함한다.
```

