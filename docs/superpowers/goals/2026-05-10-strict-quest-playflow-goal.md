# Strict Quest Playflow Goal Prompt

아래 프롬프트를 새 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 NPC 대화 기반 퀘스트 수락, 나무 채집, 퀘스트 완료, 보상 수령까지의 실제 플레이 가능 여부를 엄격하게 검증하고 고쳐줘.

문제 인식:
현재 퀘스트 수주, 완료, 보상 수령 테스트가 있다고 했지만 실제 플레이 시 하나도 동작하지 않는다. 코드상 QuestLog.Accept, Complete, ClaimReward를 직접 호출하는 테스트만으로는 완료로 인정하지 않는다. 반드시 실제 유저가 플레이하듯 씬에서 NPC와 대화하고, 버튼/입력으로 퀘스트를 수락하고, 월드에서 나무를 캐고, GUI에서 진행도를 확인하고, 다시 NPC에게 돌아가 완료/보상 수령까지 가능해야 한다.

절대 규칙:
- AGENTS.md와 .claude/rules/*를 따른다.
- Assets/**/*.cs는 외부 디스크 직접 쓰기 금지. 신규/수정 C#은 반드시 Unity MCP script-update-or-create 또는 Unity Editor 안전 경로를 사용한다.
- TDD로 진행한다. 먼저 실패하는 PlayMode 시나리오 테스트를 작성하고 실패를 확인한 뒤 구현/수정한다.
- NPC, Dialogue, Quest, Reward, Resource, Tool, Trait 등 게임 엔티티는 100% ScriptableObject 데이터 기반이어야 한다.
- questId/npcId/resourceId/toolId/traitId 같은 문자열 또는 enum별 if/switch 분기 로직을 만들지 않는다.
- 보상 지급은 지급 전 인벤토리 수용 가능 여부와 중복 수령 여부를 검증한다. 실패 시 인벤토리와 퀘스트 상태가 바뀌면 안 된다.
- 기존 사용자 변경, dirty 파일, 생성된 에셋을 되돌리지 않는다.

핵심 목표:
1. Town 또는 현재 기본 플레이 씬에서 실제 NPC가 존재한다.
2. 플레이어가 실제 이동/상호작용 입력으로 NPC에게 접근해 대화할 수 있다.
3. DialoguePanel 또는 실제 GUI 선택지를 통해 "나무를 얻는 퀘스트"를 수락할 수 있다.
4. 퀘스트 수락 후 GUI에서 내가 어떤 퀘스트를 받았는지 확인할 수 있다.
5. 플레이어가 실제 월드 상호작용으로 나무/목재 자원을 획득할 수 있다.
6. 나무 획득 수량이 퀘스트 진행도 GUI에 "현재 개수 / 필요 개수" 형태로 보인다.
7. 필요 개수만큼 나무를 얻으면 퀘스트 완료 조건이 GUI와 QuestLog 상태 모두에 반영된다.
8. 플레이어가 다시 NPC에게 가서 대화하고 완료/보상 수령 선택지를 실제 GUI로 누를 수 있다.
9. 보상 수령 후 어떤 보상을 받았는지 GUI에서 확인할 수 있다.
10. 보상으로 아이템/자원/특성이 오르는 경우, 코드 상태뿐 아니라 유저가 볼 수 있는 GUI 경로에서 확인 가능해야 한다.
11. 저장/로드 후에도 완료 및 보상 수령 상태가 유지되고, 재수령으로 중복 보상이 발생하지 않는다.

필수 실제 플레이 시나리오:
QUEST-PLAY-WOOD-001:
- Town 씬을 로드한다.
- 플레이어가 GuideNpc 또는 데이터 기반 NPC 근처로 이동한다.
- 실제 상호작용 입력을 실행한다.
- DialoguePanel이 열린 것을 확인한다.
- "나무 수집 퀘스트" 수락 선택지를 실제 Button click 또는 유저 입력 경로로 실행한다.
- QuestLog GUI에 퀘스트 제목, 목표 설명, 진행도 0/N이 표시되는지 확인한다.

QUEST-PLAY-WOOD-002:
- 퀘스트 수락 후 플레이어가 나무 또는 채집 가능한 월드 오브젝트로 이동한다.
- 실제 상호작용 입력으로 나무를 캔다.
- PlayerInventory에 목재/나무 자원이 증가한다.
- QuestLog GUI의 진행도가 1/N, 2/N처럼 즉시 갱신된다.
- 이 검증은 QuestLog.RecordEvent 같은 내부 API 직접 호출만으로 대체하면 안 된다.

QUEST-PLAY-WOOD-003:
- 필요한 개수만큼 직접 나무를 캔다.
- QuestLog 상태가 Completed가 된다.
- GUI에서도 완료 가능 상태가 표시된다.
- 완료 전에는 보상 수령 선택지가 나오면 안 되고, 완료 후에만 보상 수령 선택지가 나와야 한다.

QUEST-PLAY-WOOD-004:
- 완료 후 플레이어가 다시 NPC에게 접근한다.
- 실제 상호작용 입력으로 대화를 연다.
- 완료/보상 수령 선택지를 실제 GUI Button click 또는 유저 입력 경로로 실행한다.
- 보상 지급 후 QuestLog 상태가 RewardClaimed가 된다.
- 보상 아이템/자원/특성 증가가 Inventory/Trait/Quest GUI에서 유저가 확인 가능한 텍스트 또는 UI로 표시된다.

QUEST-PLAY-WOOD-005:
- 보상 수령 후 같은 NPC와 다시 대화한다.
- 같은 보상이 두 번 지급되지 않는다.
- GUI가 이미 완료/수령된 퀘스트 상태를 올바르게 보여준다.
- 저장/로드 후에도 재수령이 불가능해야 한다.

테스트 기준:
- 반드시 PlayMode 테스트로 실제 씬을 로드한다.
- 실제 GameObject, Collider2D, PlayerInteractionRouter, GatherInteractor, NpcInteractor, DialoguePanel, QuestLogPanel, PlayerInventory, Trait UI를 사용한다.
- 내부 도메인 API 직접 호출 테스트는 보조 테스트로만 인정한다.
- "유저가 보는 GUI"를 검증해야 한다. 텍스트, 버튼 활성 여부, 패널 표시 여부, 진행도 표시, 보상 표시, 특성 증가 표시를 Assert한다.
- 버튼 클릭은 Unity EventSystem/ExecuteEvents 또는 실제 입력 경로로 실행한다.
- 나무 채집은 QuestLog에 이벤트를 직접 넣지 말고, 실제 월드 채집 상호작용을 통해 발생시킨다.
- 임의 WaitForSeconds에 의존하지 말고 오브젝트 생성, 바인딩, UI 갱신 완료 조건을 명확히 기다린다.
- 테스트 이름에는 위 시나리오 ID를 포함한다.

먼저 확인할 기존 파일:
- Assets/Scripts/Game/Dialogue/
- Assets/Scripts/Game/Quests/
- Assets/Scripts/Game/Quests/Objectives/
- Assets/Scripts/Game/Quests/Rewards/
- Assets/Scripts/Game/Player/
- Assets/Scripts/Game/World/
- Assets/Scripts/UI/Quests/
- Assets/Scripts/UI/Modern/
- Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs
- Assets/Scripts/Game/Bootstrap/TownStudentLifeRuntimeInstaller.cs
- Assets/Data/Registry/GameDataRegistry.asset
- Assets/Data/Quests/
- Assets/Data/NPCs/
- Assets/Data/Dialogue/
- Assets/Data/Resources/
- Assets/Data/Tools/
- Assets/Data/Traits/
- Assets/Tests/PlayMode/Quests/
- Assets/Tests/PlayMode/World/
- Assets/Tests/PlayMode/TownConcept/
- Assets/Tests/EditMode/Quests/

작업 순서:
1. git status로 현재 dirty 상태를 확인하고 기존 변경은 되돌리지 않는다.
2. 기존 퀘스트/대화/월드 채집/인벤토리/UI 테스트가 실제 플레이 경로를 검증하는지 확인한다.
3. 실제 플레이와 동떨어진 테스트, 즉 내부 API만 직접 호출하는 테스트와 누락된 GUI 검증을 식별한다.
4. 위 QUEST-PLAY-WOOD-001~005 PlayMode 실패 테스트를 먼저 추가한다.
5. 실패를 직접 확인한다.
6. 최소 구현/수정으로 실제 씬 플레이 경로를 연결한다.
7. 필요한 경우 데이터 기반 SO 인스턴스와 GameDataRegistry 등록을 보강한다.
8. GUI에 퀘스트 제목, 목표, 진행도, 완료 상태, 보상, 특성 증가 확인 경로가 없으면 추가한다.
9. 관련 EditMode 테스트를 실행한다.
10. 관련 PlayMode 테스트를 실행한다.
11. entity ID 분기 금지 CI를 실행한다: Scripts/ci/check-no-entity-id-branching.sh

완료 조건:
- 실제 플레이 씬에서 NPC 대화 -> 퀘스트 수락 -> 나무 채집 -> 진행도 확인 -> 완료 -> NPC 재대화 -> 보상 수령 -> 보상/특성 GUI 확인까지 전부 가능하다.
- 이 흐름을 자동화한 PlayMode 테스트가 존재하고 통과한다.
- QuestLog API 직접 호출만으로 통과하는 테스트는 완료 근거로 쓰지 않는다.
- 유저가 직접 확인 가능한 GUI 표시가 검증된다.
- 보상 중복 수령, 저장/로드 후 재수령, 인벤토리 수용 실패 시 상태 오염이 방지된다.
- 최종 보고에는 수정 파일, 추가/수정 테스트, 실행한 테스트 명령, 통과/실패 결과, 남은 리스크를 포함한다.
```
