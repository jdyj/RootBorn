# Town Portal Inventory Quest Persistence Goal Prompt

아래 프롬프트를 새 goal 세션에 그대로 붙여 넣고 진행한다.

```text
ROOTBORN Unity 프로젝트에서 "Town 씬에서 획득한 인벤토리와 퀘스트 진행 상태가 포탈을 통한 씬 이동 후 Farm 씬에서도 유지되는지"를 실제 플레이 기준의 닫힌 테스트 루프로 검증하고, 유지되지 않는 원인을 TDD로 수정해줘.

핵심 문제:
1. 현재 포탈을 타고 씬을 이동할 때 인벤토리가 유지되지 않는 것으로 보인다.
2. Town 씬에서 나무를 획득한 뒤 포탈을 타고 Farm으로 이동하면, 획득한 나무 수량과 관련 퀘스트 상태가 Farm에서도 그대로 보여야 한다.
3. 이 검증은 내부 API 직접 호출이 아니라 유저가 직접 플레이하는 흐름처럼 진행해야 한다.

절대 규칙:
- AGENTS.md와 `.claude/rules/*`를 따른다.
- `Assets/**/*.cs`는 외부 디스크 직접 쓰기 금지. 신규/수정 C#은 반드시 Unity MCP `script-update-or-create` 또는 Unity Editor 안전 경로를 사용한다.
- TDD로 진행한다. 먼저 실패하는 PlayMode 시나리오 테스트를 작성하고 실패를 확인한 뒤 구현한다.
- 인벤토리, 퀘스트, 보상, 저장/로드 상태는 플레이어/세이브 슬롯 기준으로 격리되어야 한다.
- questId/itemId/sceneName/playerId별 `if/switch` 분기나 엔티티 ID 하드코딩으로 해결하지 않는다.
- 기존 사용자 변경이나 dirty 파일을 되돌리지 않는다.
- 보상/인벤토리 트랜잭션 원칙을 지킨다. 중복 지급, 부분 지급, 씬 전환 중 상태 유실이 발생하면 안 된다.

먼저 확인할 기존 파일:
- `Assets/Scripts/Game/Player/PlayerInventory.cs`
- `Assets/Scripts/Game/Player/GatherInteractor.cs`
- `Assets/Scripts/Game/World/WorldPortal.cs`
- `Assets/Scripts/Game/Save/SaveService.cs`
- `Assets/Scripts/Game/Save/SaveSlotMetadata.cs`
- `Assets/Scripts/Game/Quests/QuestLog.cs`
- `Assets/Scripts/Game/Quests/QuestSaveData.cs`
- `Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs`
- `Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs`
- `Assets/Scripts/UI/Quests/QuestLogPanel.cs`
- `Assets/Scripts/UI/Modern/ModernUiInventoryPanel.cs`
- `Assets/Data/Items/Item_Wood.asset`
- `Assets/Data/Quests/Objectives/Objective_GatherWood.asset`
- `Assets/Data/Registry/GameDataRegistry.asset`
- `Assets/Tests/PlayMode/Quests/`
- `Assets/Tests/PlayMode/EndToEnd/`
- `Assets/Tests/PlayMode/TownConcept/`

필수 원인 분석:
1. PlayerInventory가 씬 전환 시 새 Player 생성과 함께 초기화되는지 확인한다.
2. QuestLogPanel/QuestLog가 씬마다 새로 만들어질 때 기존 ActiveSaveContext 또는 플레이어 상태에서 복원되는지 확인한다.
3. Town -> Farm 포탈 이동 시 PlayerGlobalState, ActiveSaveContext, SaveSlotMetadata, QuestLogSaveData, Inventory 저장 데이터 중 어느 계층이 상태를 보존해야 하는지 확인한다.
4. Town 런타임과 Farm 런타임이 서로 다른 자동 설치기에서 동일한 저장/복원 경로를 사용하는지 확인한다.
5. "화면 UI만 초기화된 문제"인지, "도메인 상태 자체가 사라진 문제"인지 분리해서 증명한다.

필수 실제 플레이 시나리오:
- PORTAL-PERSIST-001: 새 세이브 슬롯 또는 테스트 ActiveSaveContext로 Town 씬을 로드한다.
- PORTAL-PERSIST-002: Town에서 Player, Inventory UI, QuestLog UI, 상호작용 라우터, 포탈이 준비될 때까지 명확한 조건으로 대기한다.
- PORTAL-PERSIST-003: 플레이어를 나무 획득 가능한 오브젝트 또는 나무 지급 상호작용 위치까지 실제 이동/상호작용 경로로 보낸다.
- PORTAL-PERSIST-004: 실제 입력 또는 UI/상호작용 경로로 나무를 1개 이상 획득한다.
- PORTAL-PERSIST-005: Town Inventory UI와 PlayerInventory 도메인 상태에서 나무 수량이 증가했는지 확인한다.
- PORTAL-PERSIST-006: 나무 획득 관련 퀘스트가 Accepted/Completed/Progressed 중 기대 상태로 갱신되는지 QuestLog UI와 도메인 상태 양쪽에서 확인한다.
- PORTAL-PERSIST-007: 플레이어를 Town 포탈 근처로 이동시키고, 상호작용 프롬프트를 확인한 뒤 실제 E 상호작용 경로로 Farm 씬으로 이동한다.
- PORTAL-PERSIST-008: Farm 씬 로드 후 Player, Inventory UI, QuestLog UI가 다시 준비될 때까지 대기한다.
- PORTAL-PERSIST-009: Farm의 PlayerInventory에서 Town에서 얻은 나무 수량이 유지되는지 확인한다.
- PORTAL-PERSIST-010: Farm의 Inventory UI에서도 동일한 나무 수량이 보이는지 확인한다.
- PORTAL-PERSIST-011: Farm의 QuestLog와 QuestLog UI에서도 Town에서 진행한 퀘스트 상태/진행도가 유지되는지 확인한다.
- PORTAL-PERSIST-012: Farm에서 추가로 저장/로드 또는 Farm -> Town 재전환을 수행해도 나무 수량과 퀘스트 상태가 중복 지급 없이 유지되는지 확인한다.

권장 테스트 이름:
- `PORTAL_PERSIST_PM_001_TownWoodInventoryAndQuestProgressSurvivePortalTravelToFarm`
- `PORTAL_PERSIST_PM_002_PortalTravelDoesNotRecreateEmptyInventoryForActiveSaveSlot`
- `PORTAL_PERSIST_PM_003_QuestLogProgressSurvivesTownFarmSceneRuntimeReinstall`

테스트 작성 기준:
- 반드시 PlayMode 테스트로 실제 `Town`과 `Farm` 씬을 로드한다.
- `SceneManager.LoadScene` 직접 호출만으로 끝내지 말고, 최소 1개 시나리오는 실제 `WorldPortal` 상호작용 경로를 사용한다.
- `PlayerInteractionRouter.RefreshPromptNow`, `GatherInteractor.TriggerInteract`, 버튼 클릭, 키 입력 같은 유저 경로를 우선 사용한다.
- 내부 저장 API를 직접 호출해서 상태를 만들어내는 테스트는 보조 검증으로만 둔다.
- Inventory UI 텍스트/슬롯과 PlayerInventory 도메인 수량을 둘 다 검증한다.
- QuestLog UI 텍스트/상태와 QuestLog 도메인 상태를 둘 다 검증한다.
- 임의 `WaitForSeconds`에 의존하지 말고 Player, UI, QuestLog, Inventory, Portal 준비 조건을 명확히 기다린다.
- 테스트 실패 메시지는 "도메인 상태 유실", "UI 재바인딩 누락", "저장/복원 누락", "포탈 이동 경로 누락" 중 어디가 깨졌는지 알 수 있게 작성한다.

권장 구현 방향:
1. 테스트가 먼저 실패하도록 현재 Town -> Farm 포탈 이동 시 인벤토리/퀘스트 상태 유실을 재현한다.
2. PlayerInventory와 QuestLog의 소유권을 정리한다.
   - 씬 GameObject 생명주기에만 묶여 있으면 씬 이동 시 유실된다.
   - ActiveSaveContext 또는 PlayerGlobalState 같은 플레이어 저장 상태에서 복원되어야 한다.
3. Town/Farm 런타임 설치기가 모두 같은 저장/복원 서비스 또는 같은 플레이어 상태 소스를 사용하게 한다.
4. 포탈 이동 직전 필요한 상태를 저장하거나, 상태 변경 시 즉시 저장되는 구조를 사용한다.
5. 씬 로드 후 새 Player/새 UI가 생성되면 저장된 Inventory/QuestLog 상태를 다시 바인딩한다.
6. QuestLogPanel과 InventoryPanel은 새 인스턴스여도 기존 상태를 화면에 반영해야 한다.
7. 저장/로드 반복과 재포탈 이동에서 중복 지급이나 진행도 중복 증가가 없어야 한다.

완료 조건:
- Town에서 실제 플레이 경로로 나무 획득 후 Inventory UI와 PlayerInventory 수량이 증가한다.
- 같은 흐름에서 관련 QuestLog 진행도 또는 상태가 갱신된다.
- 포탈 상호작용으로 Farm 씬에 이동한 뒤에도 나무 수량과 퀘스트 상태가 유지된다.
- Farm의 Inventory UI와 QuestLog UI가 복원된 상태를 보여준다.
- Farm -> Town 재전환 또는 씬 재로드 후에도 상태가 유지된다.
- 중복 지급/중복 진행도 증가가 없다.
- 관련 PlayMode/EditMode 테스트가 통과한다.
- `Scripts/ci/check-no-entity-id-branching.sh`가 통과한다.
- 최종 보고에는 원인, 수정 파일, 통과한 테스트, 남은 리스크를 포함한다.
```

## Design Notes

이번 goal은 새 콘텐츠를 늘리는 작업이 아니라, 이미 만들어진 Town/Farm/Quest/Inventory 루프가 씬 전환을 지나도 끊기지 않는지 검증하는 안정화 작업이다.

핵심은 `PlayerInventory`와 `QuestLog`가 씬 오브젝트의 생명주기에 종속되어 초기화되는지, 또는 활성 세이브 슬롯/플레이어 상태에서 재구성되는지를 분리해서 보는 것이다. 테스트는 도메인 객체만 검사하지 말고, 유저가 보는 Inventory UI와 QuestLog UI까지 확인해야 한다.

우선 대상 아이템은 기존 `Item_Wood.asset`를 사용한다. 퀘스트는 기존 나무 수집 목표가 있으면 재사용하고, 없거나 Town에서 접근 불가능하면 데이터 기반으로 Town에서 수행 가능한 수집/지급 루프를 추가한다. 단, itemId/questId별 하드코딩 분기는 만들지 않는다.
