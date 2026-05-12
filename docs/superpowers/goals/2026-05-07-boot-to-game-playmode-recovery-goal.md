# Boot부터 실제 게임 PlayMode 복구 goal

아래 프롬프트를 새 goal 세션에 그대로 사용한다.

```text
ROOTBORN Unity 프로젝트에서 지금까지 구현/테스트된 기능들이 실제 게임 실행 흐름에서는 대다수 동작하지 않는 문제를 Boot 씬부터 시작하는 PlayMode E2E 테스트로 재현하고 복구해줘.

사용자가 실제 플레이에서 확인한 문제:
1. 포탈을 어디서 사용하는지 알 수 없고, 포탈 이동도 되지 않는다.
2. 맵 이동이 안 되고, 실제 씬에 NPC가 하나도 보이지 않는다.
3. 퀘스트를 어디서 확인하고 어디서 수령하는지 알 수 없다.
4. 저장 후 로드하면 기존 데이터가 유지되지 않고 delete가 되거나 새 게임이 시작된다.

절대 규칙:
- AGENTS.md 헌법을 지킨다.
- Assets/**/*.cs는 디스크 직접 쓰기 금지. C# 신규/수정은 반드시 Unity MCP script-update-or-create 또는 Unity Editor 안전 경로를 사용한다.
- 반드시 실패 PlayMode 테스트를 먼저 작성하고, 구현은 그 다음에 한다.
- 엔티티 데이터-드리븐 원칙을 지킨다. NPC, 포탈, 퀘스트, 맵, 아이템, 보상, 저장 슬롯은 ScriptableObject/씬 와이어링/데이터 참조로 연결하고, npcId/questId/sceneId/itemId별 if/switch/enum 특수 처리는 금지한다.
- 기존 사용자 변경분을 되돌리지 않는다.
- 수동 QA나 눈검사만으로 완료 처리하지 않는다. Boot부터 시작하는 자동화 PlayMode 테스트가 통과해야 한다.
- 기존 EditMode/PlayMode 회귀 테스트가 깨지면 완료로 인정하지 않는다.

목표:
1. Boot 씬에서 시작해 실제 사용자 흐름을 타는 PlayMode E2E 테스트 스위트를 만든다.
2. Boot -> MainMenu -> SaveSlot 선택/생성 -> Farm 진입 -> HUD/Quest UI 확인 -> NPC 상호작용 -> 퀘스트 수락 -> 포탈 발견 -> 맵 이동 -> 저장 -> 로드 흐름을 자동화한다.
3. 실제 씬에 플레이어가 알아볼 수 있는 포탈, NPC, 퀘스트 진입점, 퀘스트 로그 UI, 세이브/로드 경로가 존재해야 한다.
4. 단위 테스트에서만 통과하는 가짜 오브젝트 테스트로 끝내지 않는다. 최종 E2E는 실제 프로젝트 씬과 실제 런타임 부트스트랩을 사용한다.
5. 테스트 중 필요한 최소 샘플 데이터는 ScriptableObject로 생성하고 GameDataRegistry에 등록한다.
6. 저장/로드 테스트는 임시 save root를 주입하거나 테스트 전용 슬롯을 사용해 사용자 실제 저장 데이터를 손상시키지 않는다.

권장 접근 방식:

접근 A. 권장: Boot E2E 실패 스위트를 먼저 만들고 원인군별로 복구한다.
- 장점: 사용자가 겪은 “실제 게임에서는 안 됨” 문제를 직접 잠근다.
- 단점: 처음에는 실패가 여러 개 쏟아질 수 있으므로 원인군 분류가 필요하다.

접근 B. 기존 단위 테스트를 보강한 뒤 E2E를 나중에 붙인다.
- 장점: 작은 테스트는 빠르게 안정화할 수 있다.
- 단점: 이미 현재 문제가 “단위 테스트는 있는데 실제 게임이 안 됨”이므로 같은 실패를 반복할 위험이 크다.

접근 C. 수동 플레이 체크리스트를 먼저 만들고 자동화한다.
- 장점: 체감 동선을 문장으로 정리하기 쉽다.
- 단점: 헌법의 TDD/자동화 원칙과 맞지 않고 완료 기준이 흐려진다.

이번 goal은 접근 A를 따른다.

1차 시나리오 카탈로그:

BOOT-E2E-001:
- Boot 씬에서 시작하면 MainMenu 또는 설정된 모드의 첫 씬으로 정상 전환된다.
- GameBootstrap.Config, Managers, GameDataRegistry가 null이 아니다.
- 콘솔에 scene load/registry/addressable 관련 Error/Exception이 없어야 한다.

SAVE-E2E-001:
- MainMenu에서 새 게임 슬롯을 생성하면 slot metadata가 저장된다.
- 같은 슬롯을 다시 로드하면 동일한 SlotId, WorldSeed, TileSeed, Character metadata가 유지된다.
- Load 동작 중 SaveService.DeleteSlot 또는 슬롯 디렉터리 삭제가 호출되면 실패해야 한다.

SAVE-E2E-002:
- 저장된 슬롯으로 Farm에 진입한 뒤 다시 MainMenu 또는 Boot 흐름을 거쳐 같은 슬롯을 로드해도 새 게임 생성 흐름으로 빠지지 않는다.
- 기존 metadata의 CreatedAtUtcTicks는 유지되고 UpdatedAtUtcTicks만 저장 시점에 맞게 바뀐다.

MAP-E2E-001:
- Farm 씬에는 플레이어가 식별 가능한 포탈 진입점이 존재한다.
- 포탈 오브젝트는 상호작용 가능하거나 Trigger로 진입 가능해야 하며, 테스트에서 위치/표시 이름/목적지를 확인할 수 있어야 한다.

MAP-E2E-002:
- Farm에서 포탈을 사용하면 다른 맵 씬으로 이동한다.
- 현재 프로젝트에 Town 씬이 없으면 먼저 실제 이동 목적지 씬을 명확히 정하고 생성/등록한다. 단, 씬 이름 하드코딩 분기로 해결하지 말고 포탈/맵 전환 데이터로 연결한다.

MAP-E2E-003:
- 맵 이동 후 플레이어는 목적지 SpawnPoint 근처에 재배치된다.
- 플레이어 인스턴스가 중복 생성되지 않는다.
- 카메라가 이동 후에도 플레이어를 추적한다.

NPC-E2E-001:
- Farm 또는 첫 이동 목적지 씬에 최소 1명의 실제 NPC가 배치되어 있다.
- NPC는 NpcDefinition/DialogueDefinition/QuestProvider 데이터와 연결되어 있다.
- NPC 오브젝트는 테스트에서 찾을 수 있는 안정적인 컴포넌트 또는 태그/마커를 가진다.

QUEST-E2E-001:
- NPC와 상호작용하면 Dialogue UI가 열린다.
- Dialogue UI에는 퀘스트 수락 선택지가 보인다.
- 선택지를 실행하면 QuestLog에서 해당 QuestDefinition 상태가 Active가 된다.

QUEST-E2E-002:
- QuestLog UI 또는 HUD 진입점이 실제 씬에서 열리고 닫힌다.
- 활성 퀘스트 이름, 목표 진행도, 완료/보상 상태를 사용자가 확인할 수 있다.
- 퀘스트 UI가 없거나 열 방법이 없으면 실패한다.

QUEST-E2E-003:
- 퀘스트 완료 후 보상 수령은 1회만 성공한다.
- 저장/로드 후 RewardClaimed 상태가 유지되고 이중 지급되지 않는다.

PLAYABLE-E2E-001:
- Boot부터 시작한 전체 흐름에서 이동 입력이 동작한다.
- Dialogue/QuestLog/SaveSlot UI를 열고 닫은 뒤 이동 잠금이 해제된다.
- 포탈 이동 직후에도 HUD와 입력이 정상이다.

필수 테스트 작성 원칙:
1. E2E 테스트 파일은 `Assets/Tests/PlayMode/EndToEnd/` 아래에 둔다.
2. 테스트명은 위 시나리오 ID를 포함한다.
3. 기존 `BootSmokeTest`처럼 GameBootstrap만 직접 붙이는 smoke 수준으로 끝내지 않는다.
4. 적어도 하나의 테스트는 실제 `Assets/Scenes/Boot.unity`를 SceneManager로 로드해서 시작한다.
5. 테스트는 실제 씬 오브젝트와 실제 UI 컴포넌트를 찾고 검증한다.
6. 테스트가 너무 길어지면 테스트 헬퍼를 만들되, 검증 자체를 숨기지 않는다.
7. 모든 테스트는 실행 전후에 DontDestroyOnLoad 잔재, 테스트 슬롯, 테스트 씬 상태를 정리한다.
8. 사용자 실제 저장 데이터를 삭제하지 않는다.

권장 작업 순서:
1. 현재 씬/BuildSettings/registry/기본 데이터 인벤토리를 작성한다.
   - Boot, MainMenu, Farm, HostLobby, 존재하는 모든 맵 씬
   - 포탈 오브젝트와 WorldPortal/WorldSpawnPoint 현황
   - NpcInteractor/QuestProvider/DialoguePanel/QuestLogPanel 현황
   - SaveService/DeleteSlot 호출 경로
2. Boot부터 시작하는 가장 작은 실패 PlayMode 테스트를 작성한다.
3. 실패를 실제로 실행해 확인한다.
4. 실패를 아래 원인군 중 하나로 분류한다.
   - Boot/Scene
   - SaveSlot/Load
   - Map/Portal/Spawn
   - NPC/Dialogue
   - Quest/UI
   - Player/Input/Camera/HUD
   - Test Harness
5. 한 반복에서 하나의 원인군만 고친다.
6. C# 수정은 Unity MCP script-update-or-create로만 한다.
7. 씬/프리팹/SO/Registry 수정은 Unity MCP asset/gameobject/scene 도구 또는 Editor 안전 경로를 사용한다.
8. 수정 후 해당 단일 PlayMode 테스트를 다시 실행한다.
9. 관련 기존 단위/통합 테스트를 실행한다.
10. 다음 실패 시나리오로 넘어간다.

구현 방향:
- 포탈:
  - WorldPortal은 목적지 씬/SpawnPoint를 데이터 또는 직렬화 필드로 갖는다.
  - 포탈에는 사용자가 알아볼 수 있는 시각 표시 또는 상호작용 프롬프트가 있어야 한다.
  - 포탈 위치, 목적지, SpawnId는 테스트에서 검증 가능해야 한다.
- 맵:
  - Farm 외 목적지 씬이 없다면 최소 목적지 씬을 생성하고 BuildSettings에 포함한다.
  - 복귀 포탈도 함께 배치한다.
- NPC:
  - 최소 NPC 1명은 실제 씬에 배치한다.
  - NPCDefinition, DialogueDefinition, QuestProvider, QuestDefinition은 GameDataRegistry에 등록한다.
  - NPC별 C# 클래스나 npcId 분기는 금지한다.
- 퀘스트:
  - 퀘스트 로그 UI를 실제 HUD 또는 키/버튼 진입점에서 열 수 있어야 한다.
  - QuestLogPanel은 활성 퀘스트와 진행도를 표시해야 한다.
  - Dialogue 선택지를 통해 퀘스트 수락/완료/보상 수령이 가능해야 한다.
- 저장/로드:
  - Load는 절대 DeleteSlot을 호출하지 않는다.
  - Delete는 명시적 Delete 버튼/확인 흐름에서만 호출된다.
  - 저장 슬롯 metadata와 실제 플레이 진행 상태가 같은 SlotId 아래에 보존된다.
  - 새 게임 생성과 기존 게임 로드는 분리된 코드 경로로 테스트한다.

필수 실행 테스트:
1. 새로 만든 EndToEnd PlayMode 테스트 전체
2. 기존 PlayMode:
   - BootSmokeTest
   - SaveSlotUiFlowTests
   - FarmFlowTests
   - SeededFarmGenerationFlowTests
   - QuestDialogueScenarioTests
   - QuestProviderScenarioTests
   - FarmingScenarioTests
3. 기존 EditMode:
   - SaveSlotServiceTests
   - SaveSlotSelectPanelTests
   - QuestProgressTests
   - QuestSaveTests
   - QuestRewardTests
   - QuestRegistryTests
   - QuestUiTests
   - WorldSceneDirectorTests
   - PlayerControllerTests
4. 전체 EditMode 회귀가 가능하면 마지막에 실행한다.
5. 엔티티 ID 분기 CI 게이트:
   - `bash Scripts/ci/check-no-entity-id-branching.sh`

완료 조건:
- BOOT-E2E/SAVE-E2E/MAP-E2E/NPC-E2E/QUEST-E2E/PLAYABLE-E2E 시나리오 테스트가 존재하고 통과한다.
- Boot부터 시작한 실제 게임 흐름에서 새 게임, 로드, Farm 진입, NPC 대화, 퀘스트 수락, 퀘스트 로그 확인, 포탈 이동, 저장/로드 재진입이 자동화 테스트로 검증된다.
- 포탈, NPC, 퀘스트 UI는 실제 플레이어가 발견 가능한 형태로 씬에 배치되어 있다.
- 저장 후 로드가 새 게임 생성이나 DeleteSlot으로 이어지지 않는다.
- 사용자 저장 데이터를 건드리지 않는 테스트 격리가 되어 있다.
- 콘솔 Error/Exception이 없다.
- 기존 관련 회귀 테스트가 통과한다.
- 마지막 보고에는 아래를 포함한다.
  - 추가한 E2E 시나리오 ID
  - 수정한 C# 파일, 씬, SO, Registry asset
  - 실행한 테스트와 결과
  - 저장 데이터 격리 방식
  - 남은 리스크와 다음 우선순위
```

## Scope Note

이번 goal은 “기능별 단위 구현 흔적”을 늘리는 작업이 아니라, 사용자가 실제 게임에서 겪는 첫 플레이 동선을 Boot부터 PlayMode로 잠그는 복구 작업이다. 단위 테스트는 보조 수단이며 완료 기준은 실제 씬 기반 E2E 통과다.
