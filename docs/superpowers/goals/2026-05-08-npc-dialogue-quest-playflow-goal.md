# NPC Dialogue Quest Playflow Goal Prompt

아래 프롬프트를 새 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 기존 NPC/대화/퀘스트 구현을 확인하고, 실제 플레이 흐름에서 NPC와 대화한 뒤 퀘스트를 수령할 수 있도록 완성/검증해줘.

핵심 목표:
1. 플레이 가능한 씬에 NPC가 존재해야 한다.
2. 플레이어가 NPC와 대화할 수 있어야 한다.
3. NPC와 대화한 뒤 퀘스트를 수령할 수 있어야 한다.
4. 테스트를 만들고 Unity Test Runner로 직접 실행해서 결과를 보고해야 한다.
5. 퀘스트 진행, 대화 상태, 보상 수령 상태는 세이브 파일마다 분리되어야 한다. saveSlot A에서 수락/완료/보상 수령한 상태가 saveSlot B에 섞이면 안 된다.

절대 규칙:
- AGENTS.md와 .claude/rules/*를 따른다.
- Assets/**/*.cs는 외부 디스크 직접 쓰기 금지. 신규/수정 C#은 반드시 Unity MCP script-update-or-create 또는 Unity Editor 안전 경로를 사용한다.
- TDD로 진행한다. 먼저 실패 테스트를 작성하고 실패를 확인한 뒤 최소 구현/수정을 한다.
- NPC, Dialogue, Quest, Reward, StoryFlag, Knowledge 등 게임 엔티티는 ScriptableObject 데이터 기반이어야 한다.
- questId/npcId/saveSlot/resourceId/toolId/cropId 같은 문자열/enum별 if/switch 분기 로직을 만들지 않는다.
- 보상 지급은 지급 전 인벤토리 수용 가능 여부와 중복 수령 여부를 검증하고, 실패 시 상태를 변경하지 않는다.
- 기존 사용자 변경, dirty 파일, 생성된 에셋을 되돌리지 않는다. 작업 범위와 충돌하는 변경만 읽고 맞춰 수정한다.

먼저 확인할 기존 구현:
- Assets/Scripts/Game/Dialogue/NpcDefinition.cs
- Assets/Scripts/Game/Dialogue/NpcInteractor.cs
- Assets/Scripts/Game/Dialogue/DialogueDefinition.cs
- Assets/Scripts/Game/Dialogue/DialogueChoiceDefinition.cs
- Assets/Scripts/Game/Dialogue/DialogueSession.cs
- Assets/Scripts/Game/Dialogue/QuestProvider.cs
- Assets/Scripts/Game/World/WorldNpcAutoFiller.cs
- Assets/Scripts/UI/Quests/DialoguePanel.cs
- Assets/Scripts/UI/Quests/QuestHudAutoFiller.cs
- Assets/Scripts/Game/Quests/QuestLog.cs
- Assets/Scripts/Game/Quests/QuestSaveData.cs
- Assets/Data/NPCs/
- Assets/Data/Dialogue/
- Assets/Data/Quests/
- Assets/Data/Registry/GameDataRegistry.asset
- Assets/Tests/EditMode/Dialogue/
- Assets/Tests/EditMode/Quests/
- Assets/Tests/PlayMode/Quests/
- Assets/Tests/PlayMode/EndToEnd/
- Assets/Tests/PlayMode/TownConcept/

기존 확인 결과를 전제로 삼되, 실제 코드를 다시 읽고 현재 상태를 검증한다:
- NpcDefinition, NpcInteractor, DialogueDefinition, DialogueChoiceDefinition, DialogueSession, QuestProvider는 이미 존재할 가능성이 높다.
- QuestDefinition, QuestLog, QuestProgress, QuestSaveData, reward preflight/idempotency 구현은 이미 존재할 가능성이 높다.
- WorldNpcAutoFiller는 Town/Farm 씬에 GuideNpc를 자동 배치하고 NpcInteractor/QuestProvider를 붙이는 구조일 가능성이 높다.
- QuestHudAutoFiller는 DialoguePanel과 씬 NPC를 연결하는 binder를 포함할 가능성이 높다.
- 기존 QUEST_001, QUEST_002, QUEST_003, QUEST_015 및 E2E 테스트가 일부 존재할 가능성이 높다.

요구 동작:
1. Town 또는 현재 기본 플레이 씬에서 GuideNpc 또는 데이터 기반 NPC가 생성/배치된다.
2. NPC는 NpcDefinition을 참조한다.
3. NPC는 DialogueDefinition을 통해 대화를 제공한다.
4. NPC 또는 연결된 QuestProvider는 GameDataRegistry에 등록된 QuestDefinition을 제공한다.
5. NPC 상호작용은 DialogueSession을 열고 DialoguePanel을 표시한다.
6. DialoguePanel의 퀘스트 수락 선택지를 실행하면 DialogueChoiceDefinition이 QuestLog.Accept를 호출한다.
7. 수락 성공 후 해당 QuestDefinition의 상태는 Active가 된다.
8. 이미 수락한 퀘스트는 중복 수락되지 않는다.
9. 완료 후 보상 수령은 기존 QuestLog.CanClaimReward/ClaimReward preflight와 idempotency 규칙을 유지한다.
10. 저장/로드 경로가 있다면 QuestLogSaveData는 선택된 saveSlot에 종속되어 저장/복원된다. saveSlot 간 진행도, 보상 수령 상태, 대화/스토리 진행 상태가 공유되지 않도록 검증한다.

필수 테스트:
- NPC-QUEST-001: 플레이 씬 로드 후 NPC가 존재하고 SpriteRenderer, Collider2D, NpcInteractor, QuestProvider를 가진다.
- NPC-QUEST-002: NPC와 상호작용하면 DialoguePanel이 열린다.
- NPC-QUEST-003: DialoguePanel에서 퀘스트 수락 선택지를 실행하면 QuestLog 상태가 Active가 된다.
- NPC-QUEST-004: 같은 퀘스트 수락 선택지를 반복 실행해도 중복 수락이나 상태 오염이 발생하지 않는다.
- NPC-QUEST-005: GameDataRegistry에 NpcDefinition, DialogueDefinition, QuestDefinition이 등록되어 있고 null 참조가 없다.
- NPC-QUEST-006: saveSlot A에서 퀘스트를 수락/진행/보상 수령해도 saveSlot B의 QuestLog/저장 데이터는 NotStarted 또는 독립 상태를 유지한다.
- NPC-QUEST-007: 저장/로드 후 같은 saveSlot에서는 Active/Completed/RewardClaimed 상태와 objective 진행도가 유지된다.
- NPC-QUEST-008: 기존 QUEST_001~QUEST_015 테스트와 NPC/Dialogue/Quest 관련 E2E 테스트가 계속 통과한다.

테스트 작성 기준:
- 가능하면 기존 테스트 구조를 확장한다.
- 실제 씬 동작은 PlayMode 테스트로 검증한다.
- 순수 QuestLog, DialogueChoiceDefinition, saveSlot 분리는 EditMode 테스트로 먼저 검증한다.
- 테스트 이름에는 위 시나리오 ID를 포함한다.
- 테스트가 임의 대기 시간에 의존하지 않도록 씬 로드/오브젝트 생성/바인딩 완료 조건을 명확히 기다린다.

작업 순서:
1. git status로 dirty 상태를 확인하고, 기존 변경을 되돌리지 않는다.
2. 위 기존 구현 파일과 테스트를 읽고 현재 NPC 대화 퀘스트 흐름을 요약한다.
3. NPC/대화/퀘스트/세이브 슬롯 분리에서 누락된 지점을 하나씩 식별한다.
4. 실패 테스트를 먼저 추가한다.
5. 테스트 실패를 확인한다.
6. 최소 구현/수정으로 테스트를 통과시킨다.
7. 관련 EditMode 테스트를 실행한다.
8. 관련 PlayMode 테스트를 실행한다.
9. 가능하면 전체 Quest/NPC/Dialogue 관련 테스트 묶음을 실행한다.
10. entity ID 분기 금지 CI도 실행한다: Scripts/ci/check-no-entity-id-branching.sh

완료 조건:
- 실제 플레이 씬에서 NPC가 존재한다.
- NPC와 대화할 수 있다.
- NPC 대화 선택지를 통해 퀘스트를 수령할 수 있다.
- 수령한 퀘스트 상태가 Active로 전환된다.
- 같은 퀘스트가 중복 수락되지 않는다.
- 세이브 파일별로 퀘스트/대화/보상 상태가 분리된다.
- 기존 보상 원자성/idempotency 테스트가 깨지지 않는다.
- 관련 EditMode/PlayMode 테스트 결과를 직접 실행 로그 기준으로 보고한다.
- 최종 보고에는 수정 파일, 추가/수정 테스트, 실행한 테스트 명령, 통과/실패 결과, 남은 리스크를 포함한다.
```

## Context Summary

2026-05-08 조사 기준으로 NPC/대화/퀘스트 기반 구현은 이미 상당 부분 존재한다. 이번 goal은 새 시스템 재구현이 아니라 실제 플레이 경로와 세이브 슬롯 분리를 검증하고 누락된 연결만 보강하는 복구형 작업이다.

특히 `WorldNpcAutoFiller`, `QuestHudAutoFiller`, `DialoguePanel`, `DialogueChoiceDefinition`, `QuestLog`의 연결이 실제 PlayMode에서 이어지는지 확인해야 한다. 단위 테스트만 통과하고 씬 상호작용이 끊겨 있으면 미완료로 본다.
