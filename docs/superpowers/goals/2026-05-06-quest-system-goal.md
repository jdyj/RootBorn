# ROOTBORN Quest System Goal Prompt

아래 프롬프트를 새 goal 세션에 그대로 사용한다.

```text
ROOTBORN Unity 프로젝트에 NPC 대화 기반 퀘스트 시스템을 정식 설계/구현해줘.

절대 규칙:
- AGENTS.md 헌법을 지킨다.
- Assets/**/*.cs는 디스크 직접 쓰기 금지. Unity MCP script-update-or-create 또는 Editor 안전 경로만 사용한다.
- 반드시 실패 테스트 먼저 작성하고, 구현은 그 다음에 한다.
- 퀘스트, NPC, 대화, 보상, 스토리 진행 조건은 모두 ScriptableObject 데이터로 정의한다.
- questId/npcId/resourceId/toolId/cropId 등 엔티티 ID별 if/switch/enum 분기는 금지한다.
- 기존 사용자 변경분을 되돌리지 않는다.
- 기존 퀘스트 외 시스템 테스트가 깨지면 완료로 인정하지 않는다.

목표:
1. 플레이어가 NPC와 대화할 수 있어야 한다.
2. NPC 또는 다른 상호작용 가능한 오브젝트를 통해 퀘스트를 받을 수 있어야 한다.
3. 현재 퀘스트 기능이 없다면 Rootborn.Game.Quests 도메인을 새로 설계/구현한다.
4. 퀘스트 완료 시 보상이 지급되어야 한다.
5. 퀘스트 완료/수락/진행 상태가 스토리 진행 트리거로 확장 가능해야 한다.
6. 스토리 내용은 아직 확정하지 않는다. 대신 특정 NPC 퀘스트 완료가 다음 대화, 다음 퀘스트, KnowledgeNode, StoryFlag 같은 데이터 기반 후속 상태를 열 수 있는 구조를 만든다.
7. 나무 채집, 동물 처치, 아이템 수집, 작물 수확처럼 카운팅형 목표는 실제 게임 이벤트를 통해 정확히 누적되어야 한다.
8. 카운팅은 중복 지급, 중복 이벤트, 완료 후 추가 누적, 저장/로드 후 재집계 오류가 없도록 방어한다.
9. 퀘스트 UI는 최소한 활성 퀘스트, 목표 진행도, 완료 가능 여부, 보상 수령 상태를 확인할 수 있어야 한다.
10. 모든 퀘스트 데이터는 Assets/Data/Quests/에 두고, 필요하면 NPC/Dialogue/Story/Rewards 데이터 폴더도 추가한다.
11. 모든 신규 SO는 Assets/Data/Registry/GameDataRegistry.asset에 등록한다.

권장 설계 방향:
- QuestDefinition SO:
  - 표시 이름/설명 localization key
  - 제공자 조건
  - 선행 조건
  - QuestObjectiveBase[]
  - QuestRewardBase[]
  - QuestCompletionEffectBase[]
- QuestObjectiveBase 전략 SO:
  - Gather/Defeat/Collect/Harvest/Talk/Reach/UseTool 등 목표 유형을 데이터로 표현
  - 목표별 대상은 SO 참조로 연결하고 ID 분기 금지
- QuestProgress 런타임 모델:
  - NotStarted, Active, Completed, RewardClaimed 상태
  - objective별 current/required 카운트
  - 동일 이벤트 중복 처리 방어
- QuestEventBus 또는 기존 액션 이벤트 연동:
  - 채집, 처치, 수집, 수확, 대화 완료 등 게임 이벤트를 퀘스트 목표에 전달
  - 이벤트는 테스트에서 결정론적으로 주입 가능해야 한다.
- QuestRewardBase 전략 SO:
  - 아이템, 지식, 상태, 스토리 플래그, 도구 해금 등 보상을 데이터로 지급
- QuestCompletionEffectBase 전략 SO:
  - 다음 퀘스트 해금
  - NPC 대화 상태 변경
  - StoryFlag 설정
  - KnowledgeNode 해금 연동
- DialogueDefinition 또는 기존 대화 구조:
  - NPC 대화에서 퀘스트 수락/완료/보상 수령 선택지가 노출되도록 한다.
  - 대화 문구는 localization key 기반으로 둔다.

필수 테스트:
- QUEST-001: NPC와 상호작용하면 대화가 열리고 닫을 수 있다.
- QUEST-002: NPC 대화 선택지를 통해 퀘스트를 수락하면 상태가 Active가 된다.
- QUEST-003: 상호작용 오브젝트도 QuestDefinition을 제공할 수 있다.
- QUEST-004: 퀘스트 목표가 모두 충족되기 전에는 완료 처리되지 않는다.
- QUEST-005: 퀘스트 완료 후 보상이 정확히 1회만 지급된다.
- QUEST-006: 보상 수령을 반복 호출해도 이중 지급되지 않는다.
- QUEST-007: Gather objective는 실제 채집 이벤트 횟수만큼 정확히 카운팅된다.
- QUEST-008: Defeat objective는 실제 처치 이벤트 횟수만큼 정확히 카운팅된다.
- QUEST-009: Collect objective는 인벤토리 증가 또는 아이템 획득 이벤트와 일관되게 카운팅된다.
- QUEST-010: 완료된 퀘스트의 CompletionEffect가 다음 퀘스트 또는 StoryFlag를 데이터 기반으로 해금한다.
- QUEST-011: 선행 퀘스트/스토리 조건을 만족하지 않으면 후속 퀘스트가 제공되지 않는다.
- QUEST-012: 저장/로드 후 Active 퀘스트의 objective 진행도가 유지된다.
- QUEST-013: 저장/로드 후 RewardClaimed 퀘스트가 다시 보상을 지급하지 않는다.
- QUEST-014: QuestDefinition, Objective, Reward, CompletionEffect가 GameDataRegistry에 등록되어 있다.
- QUEST-015: 퀘스트 구현 후 기존 KNOW/CROP/TOOL/GEN/STATUS/NET 관련 테스트가 깨지지 않는다.

테스트 범위:
1. EditMode 단위 테스트:
   - QuestProgress 상태 전이
   - Objective 카운팅
   - Reward idempotency
   - CompletionEffect 실행
   - 선행 조건 평가
2. EditMode 통합 테스트:
   - QuestDefinition + Objective + Reward 조합
   - GameDataRegistry 와이어링
   - 인벤토리/Knowledge/StoryFlag 연동
3. PlayMode 시나리오 테스트:
   - NPC 대화 -> 퀘스트 수락 -> 목표 진행 -> 완료 -> 보상 -> 후속 스토리 트리거
   - 채집/처치/수집 카운팅이 실제 플레이 이벤트와 연결되는지 검증
4. 회귀 테스트:
   - 기존 FarmingScenarioTests
   - KnowledgeProgressTests / KnowledgeTriggerTests
   - Inventory 관련 테스트
   - GameDataRegistryValidator 관련 테스트

권장 작업 순서:
1. 기존 Dialogue, Interaction, Inventory, Knowledge, Gather/Resource/Combat 이벤트 구조를 먼저 읽고 현재 동작을 정리한다.
2. 퀘스트 도메인 설계를 짧은 spec으로 작성한다.
3. QUEST-* 시나리오 ID를 테스트 규약과 ScenarioId에 추가한다.
4. 가장 작은 실패 테스트부터 작성한다.
5. 실패를 확인한다.
6. QuestDefinition / QuestProgress / QuestObjectiveBase 최소 구현을 추가한다.
7. Gather/Defeat/Collect objective를 실제 이벤트와 연결한다.
8. Reward와 CompletionEffect를 데이터 기반 전략 SO로 구현한다.
9. NPC/오브젝트에서 퀘스트 제공이 가능하도록 연결한다.
10. 저장/로드 상태 유지 테스트를 추가한다.
11. PlayMode 시나리오를 추가한다.
12. 관련 기존 테스트를 실행해 회귀를 확인한다.

완료 조건:
- QUEST-001~QUEST-015 테스트가 존재하고 통과한다.
- 퀘스트 수락, 진행, 완료, 보상, 스토리 트리거가 모두 데이터 기반이다.
- 퀘스트 관련 엔티티 ID 분기, enum 기반 특수 처리, 하드코딩된 NPC/퀘스트 로직이 없다.
- 기존 KNOW/CROP/TOOL/GEN/STATUS/NET 및 인벤토리 관련 테스트가 깨지지 않는다.
- 마지막 보고에는 수정 파일, 추가 SO, 추가 테스트, 실행한 테스트, 통과/실패 결과, 남은 리스크를 적는다.
```

## Scope Note

이번 goal은 스토리 내용을 확정하는 작업이 아니라, 퀘스트 완료가 스토리 라인 진행을 트리거할 수 있는 확장 가능한 구조를 만드는 작업이다.
