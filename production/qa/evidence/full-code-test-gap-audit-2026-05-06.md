# 전체 코드 테스트 갭 감사 — 2026-05-06

## 1. 감사 요약
- 총평: NEEDS TESTS
- EditMode 결과: PASS 144/144. 기존 131개에 테스트 갭 보강 13개가 추가되어 Unity Test Runner 전체 통과.
- PlayMode 결과: PASS 12/12.
- 가장 큰 리스크 3개:
  1. Network 세션 lifecycle/approval/host-client-server 실제 경로가 `ArgsParserTests` 외에는 자동화되지 않아 `NET-002`가 여전히 PENDING이다.
  2. Host/Client/DedicatedServer의 NGO Start/Stop, callback 해제, approval full rejection은 아직 실제 `NetworkManager` harness로 검증하지 못한다.
  3. HUD 일부(`StatusHud`, `TimeHud`, `ToolUnlockToast`, `GenerationTransitionPanel`, `ModeSelectPanel`)와 Editor build/data tool의 직접 테스트가 부족하다.

## 2. 코드 인벤토리
| 도메인 | 주요 파일 | 책임 | 주요 상태 전이/동작 | 테스트 존재 여부 |
|---|---|---|---|---|
| Bootstrap/Config | `Assets/Scripts/Game/Bootstrap/AppConfig.cs`, `ArgsParser.cs`, `GameBootstrap.cs`, `SceneDiagnostics.cs`, `FarmAutoFiller.cs` | CLI 인자 파싱, 부트스트랩, 씬 전환, Farm 자동 채움 | `-mode/-port/-maxPlayers/-saveSlot/-joinIp` 파싱, Managers bootstrap, Farm 씬 자동 생성/보강 | PARTIAL: `ArgsParserTests` 7개, `BootSmokeTest`, `FarmFlowTests`. `FarmAutoFiller` fallback/에러 경로는 제한적 |
| Managers/Data/Addressables | `Managers.cs`, `ResourceManager.cs`, `DataManager.cs`, `GameDataRegistry.cs`, `AddressableManifest.cs` | Addressables 초기화, Registry 로드, sprite preload/cache, 데이터 dictionary 구성 | Addressables 우선 로드, Resources fallback, UI/player sprite preload | PARTIAL: `UISpriteAddressesTests`, PlayMode bootstrap 간접 검증. `ResourceManager` 실패/Release/cache 경로 직접 테스트 없음 |
| Generation/Lineage | `GenerationManager.cs`, `GenerationProfile.cs`, `LineageBook.cs`, `AncestorRecord.cs` | 세대 진행, lifetime/game-day 전환, 조상 기록 | lifetime 경과 시 next generation, lineage 기록, day length 변화 보존 | COVERED: `GenerationManagerTests` 3개, `GenerationSimulatorTests` 일부 |
| Heir | `HeirGenerator.cs`, `HeirData.cs`, `HeirTrait.cs` | seed 기반 후계자 생성, 상속/랜덤 특성 | 동일 seed 결정론, 비상속 trait 제외, random bonus | COVERED: `HeirGeneratorTests` 3개, `GenerationSimulatorTests` 1000회 결정론 |
| Knowledge | `KnowledgeNode.cs`, `KnowledgeProgress.cs`, `KnowledgeTriggerBase.cs`, `Triggers/*.cs` | 행동 카운트 누적, trigger AND 평가, unlock 1회 발화 | 맨손/자원/표면/반복 횟수 조건, 중복 unlock 방지 | COVERED: `KnowledgeTriggerTests` 3개, `KnowledgeProgressTests`, `FarmFlowTests` |
| Tools/Effects | `ToolDefinition.cs`, `ToolEffectBase.cs`, `Tools/Effects/*.cs` | SO 전략 배열 기반 도구 효과, 농사 액션 | 효과 배열 순회, till/plant/harvest/water/fertilize, 자원-도구 power matching | COVERED: `ToolEffectTests` 7개, `ToolResourceMatchingTests` 10개, `ToolDefinitionStrategyTests` 3개 |
| Resources/Gathering | `ResourceNodeDefinition.cs`, `ResourceNode.cs`, `ResourceDrop.cs`, `GatherInteractor.cs`, `PlayerInventory` | 자원 노드 hit/break/drop, inventory stack/equip, 입력 action | preferred tool full power, mismatch/bare hand penalty, drop inventory, E/Space/Ctrl/좌클릭 | COVERED/PARTIAL: `GatherInteractorTests`, `GatherInteractorInputBindingTests`, `ToolResourceMatchingTests`, `FarmFlowTests`. drop RNG 범위/결정론은 약함 |
| Farming/Crops | `FarmGrid.cs`, `TileSoilState.cs`, `CropPlot.cs`, `CropDefinition.cs`, `GrowthBehaviorBase.cs`, crop behaviors | 토양, 물, 비료, 파종, 성장, 수확 | till/plant/harvest, daily water reset, fertilizer expiry, crop stage tick | COVERED: `CropGrowthTests`, `FarmGridTests`, `CropPlotWaterTests`, `ToolEffectTests`, PlayMode `FarmingScenarioTests` |
| Status/Clock | `StatusValue.cs`, `StatusEffectDefinition.cs`, `PlayerStatus.cs`, `GameClock.cs`, `TimeDefinition.cs` | 상태 누적/회복/페널티, 게임 일자 진행 | max clamp, floor, penalty, day rollover, `TimeDefinition` 적용 | PARTIAL: `StatusValueTests`, `GameClockTests`. `PlayerStatus` MonoBehaviour lifecycle 및 UI 연동은 직접 테스트 부족 |
| Save | `SaveService.cs`, `SaveSlotMetadata.cs`, `ActiveSaveContext`, `SaveSlotSelectPanel.cs` | 세이브 슬롯 metadata 생성/저장/삭제, character/seed 선택 | 3개 UI 슬롯, CLI slot 허용, unsafe slot rejection, preview UI | COVERED/PARTIAL: `SaveSlotServiceTests`, `SaveSlotSelectPanelTests`, PlayMode `SaveSlotUiFlowTests`. 실제 디스크 손상/동시성은 미검증 |
| UI/HUD/Inventory | `StatusHud.cs` 내부 `StatusHud`, `ResourceHud`, `InventoryView`, `FarmHudController`; `TimeHud.cs`; `ToolUnlockToast.cs`; `GenerationTransitionPanel.cs`; `ModeSelectPanel.cs` | HUD, 자원/도구 표시, 인벤토리/장비 UI, toast/transition/menu | inventory toggle/drag/equip, HUD refresh, book pages, button actions | PARTIAL: inventory/equipment 흐름은 강함. `StatusHud`, `TimeHud`, `ToolUnlockToast`, `GenerationTransitionPanel`, `ModeSelectPanel` button/Update/lifecycle 직접 테스트 부족 |
| Network | `Assets/Scripts/Network/Session/*.cs` | single/host/client/server session 생성과 NGO start/stop | `NetworkSessionFactory.Create`, `NetworkBootstrap` event, StartHost/StartClient/StartServer, approval maxPlayers | PARTIAL: `ArgsParserTests`, `NetworkSessionTests`가 factory/single events 검증. NGO lifecycle/approval/loopback PlayMode 없음 |
| WorldGeneration | `SeededWorldGenerator.cs`, `SeededFarmWorldApplier.cs`, terrain/tile/spawn SO | seed 기반 tile/prop 생성, Farm 씬 적용 | seed 결정론, reserved/start-safe area, density/cluster, ActiveSave seed 반영 | COVERED/PARTIAL: `SeededWorldGeneratorTests` 11개, `SeededFarmGenerationFlowTests`. `SeededFarmWorldApplier` fallback/empty scene failure 경로는 약함 |
| Crafting | `RecipeDefinition.cs`, `CraftStepBase.cs`, `GatherResourceStep.cs`, `WaitOnSurfaceStep.cs` | 단계 분해 제작 조건 평가 | 모든 step 만족, null step 실패, gather count, surface/wait time | COVERED: `CraftingRecipeTests` 5개 |
| Editor/Build/Data | `BuildScript.cs`, `GameDataRegistryValidator.cs`, `Editor/Tools/*.cs`, asmdef | 빌드, 데이터 생성/와이어링, 씬/플레이어/Addressables setup | client/server build options, registry validation, setup idempotency | MISSING/PARTIAL: `UISpriteAddressesTests`가 AddressablesSetup 일부 커버. BuildScript/validator/one-click setup 직접 테스트 없음 |

## 3. 테스트 인벤토리
| 테스트 계층 | 테스트 클래스 | 테스트 수 | 커버 대상 | 한계 |
|---|---|---:|---|---|
| EditMode | `ArgsParserTests` | 7 | CLI config 파싱, Unity 예약 인자 무시 | Network session start/approval 미검증 |
| EditMode | `CropGrowthTests` | 1 | crop stage tick | 단일 happy path 중심 |
| EditMode | `Farming/CropPlotWaterTests` | 4 | 물/비료 기반 crop growth | renderer/lifecycle 경로 제한 |
| EditMode | `Farming/FarmGridTests` | 8 | till/plant/harvest/water/fertilizer/day roll | Tilemap 시각/scene wiring 간접 |
| EditMode | `Farming/ToolEffectTests` | 7 | 5종 `ToolEffectBase` 구현 | `ToolDefinition.ApplyEffects` 순서/널 스킵 없음 |
| EditMode | `ToolDefinitionStrategyTests` | 3 | `TOOL-001`: strategy 배열 순서, null skip, context 전달 | concrete effect 조합은 `ToolEffectTests`에 의존 |
| EditMode | `GameClockTests` | 4 | day progress, `TimeDefinition`, rollover event | `TimeHud` 연동 없음 |
| EditMode | `GatherInteractorInputBindingTests` | 2 | Ctrl/E/Space binding reflection 검증 | 실제 입력 이벤트/좌클릭 통합은 제한 |
| EditMode | `GatherInteractorTests` | 11 | knowledge record, ResourceNode, inventory stack/remove, walkable wiring | drop RNG/nearby target selection 경계 부족 |
| EditMode | `GenerationManagerTests` | 3 | generation transition, lineage, game-day lifetime | UI transition 연동 없음 |
| EditMode | `GenerationSimulatorTests` | 3 | 5세대/30일 시뮬, heir seed 1000회 | PlayMode full journey는 아님 |
| EditMode | `HeirGeneratorTests` | 3 | seed determinism, inheritable/random trait | trait modifier gameplay 적용 미검증 |
| EditMode | `InventoryEquipmentRegressionCatalogTests` | 4 | regression test 존재 가드 | 실제 동작 검증이 아닌 메타 테스트 |
| EditMode | `InventoryEquipmentScenarioTests` | 19 | inventory toggle/slots/equip/drag/stack | UI 일부는 reflection/수동 구성 기반 |
| EditMode | `InventoryUiTests` | 5 | description field, sprite metadata, selected item API | 구조 가드 중심 |
| EditMode | `KnowledgeProgressTests` | 1 | repeated bare-hand hit unlock | 단일 unlock path 중심 |
| EditMode | `KnowledgeTriggerTests` | 3 | trigger 조건/AND | SO asset wiring 전체 검증은 제한 |
| EditMode | `PlayerControllerTests` | 3 | bind/default facing/basic lookup | 실제 input movement/animation transition 부족 |
| EditMode | `PlayerToolSpriteMetadataTests` | 1 | item tool sprite prefix | sprite sheet 존재/animation fallback 부족 |
| EditMode | `Save/SaveSlotSelectPanelTests` | 5 | character selection/preview controls | scene/menu button full navigation 부족 |
| EditMode | `Save/SaveSlotServiceTests` | 6 | slot list/save/load/delete/unsafe id | corrupted JSON, concurrent write 미검증 |
| EditMode | `StatusValueTests` | 3 | tick/restore/penalty | `PlayerStatus` lifecycle/decay per status 미검증 |
| EditMode | `ToolResourceMatchingTests` | 10 | pickaxe/axe/resource preferredTool, registry wiring, penalty | runtime animation/visual feedback 미검증 |
| EditMode | `UISpriteAddressesTests` | 6 | UI Addressables setup consistency | non-UI Addressables registry coverage 제한 |
| EditMode | `WorldGeneration/SeededWorldGeneratorTests` | 11 | deterministic terrain/props/registry terrain asset | scene application fallback 제한 |
| EditMode | `NetworkSessionTests` | 3 | `NetworkSessionFactory` mode mapping, single-player local events | Host/Client/Server NGO lifecycle 미검증 |
| EditMode | `StaticRuntimeUsageGateTests` | 2 | 엔티티 ID 분기 금지, 런타임 `Resources.Load`/Find 허용 목록 게이트 | 현재 허용 목록 기반. 사용 제거 자체는 아님 |
| EditMode | `Crafting/CraftingRecipeTests` | 5 | recipe step/null/empty/gather/surface/time 조건 | 실제 craft UI/인벤토리 소비는 후속 |
| PlayMode | `BootSmokeTest` | 1 | Boot bootstrap config | only smoke |
| PlayMode | `FarmFlowTests` | 4 | Farm autofill, movement persistence, knowledge unlock, resource break | network/client-server path 없음 |
| PlayMode | `Farming/FarmingScenarioTests` | 1 | till-plant-wait-harvest user flow | one crop/tool path |
| PlayMode | `InventoryEquipmentUiScenarioTests` | 4 | inventory UI/equip/drag in PlayMode | keyboard/mouse visual regression 제한 |
| PlayMode | `SaveSlotUiFlowTests` | 1 | save slot cards displayed | button navigation and persistence path 부족 |
| PlayMode | `SeededFarmGenerationFlowTests` | 1 | ActiveSave seed affects Farm layout | only resource layout difference |
| PlayMode | `Scenarios/ScenarioId` | 0 | scenario ID constants | `NET-002~NET-005`, `TOOL-001` pending marker only |

## 4. 시나리오 커버리지 매핑
| 시나리오 ID/요구사항 | 기대 동작 | 실제 테스트 | 커버 상태 | 증거 |
|---|---|---|---|---|
| GEN-001 | lifetime 경과 → 2세대 전환 + Lineage 기록 | `GenerationManagerTests.Tick_LifetimeElapsed_AdvancesGenerationAndRecordsAncestor`, `GenerationSimulatorTests.Simulate_5_Generations_Records_All_Ancestors` | COVERED | Unity EditMode PASS 131/131 |
| HEIR-001 | 동일 seed 동일 후계자, 비계승 trait 제외 | `HeirGeneratorTests.*`, `GenerationSimulatorTests.HeirGenerator_SeedDeterminism_1000_Iterations` | COVERED | 테스트명과 assertion 확인 |
| KNOW-001 | 맨손+돌+땅 반복 N회 → StoneTool trigger | `KnowledgeTriggerTests.Trigger_RequiresAllConditionsAndRepeatThreshold`, `FarmFlowTests.Hit_Rock_Repeatedly_Unlocks_StoneTool_Knowledge` | COVERED | EditMode/PlayMode PASS |
| KNOW-002 | threshold 도달 시 unlock 1회, 이후 중복 없음 | `KnowledgeProgressTests.Repeated_HitGround_With_BareHand_Unlocks_StoneTool`, `FarmFlowTests` unlock count assert | COVERED | `Assert.AreEqual(1, unlocks/unlockedCount)` |
| TOOL-001 | `ToolDefinition.ApplyEffects`가 SO 전략 배열을 순서대로 호출 | `ToolDefinitionStrategyTests.TOOL_001_ApplyEffects_InvokesStrategyArrayInSerializedOrder`, `TOOL_002`, `TOOL_003` | COVERED | EditMode PASS 144/144 |
| CROP-001 | `CropPlot.Tick` stage duration 결정론 전환 | `CropGrowthTests.Tick_TransitionsStages`, water/fertilizer crop tests | COVERED | EditMode PASS |
| STATUS-001 | `StatusValue.Tick`, max clamp, Restore floor, Penalty | `StatusValueTests` 3개 | COVERED | EditMode PASS |
| STATUS-002 | `GameClock.Tick` day/progress 결정론 | `GameClockTests` 4개 | COVERED | EditMode PASS |
| NET-001 | ArgsParser mode/port/maxPlayers/saveSlot/joinIp 파싱 | `ArgsParserTests` 7개 | COVERED | EditMode PASS |
| NET-002 | dedicated server 빌드 → 클라 loopback 접속 → player spawn | 없음 | MISSING | `ScenarioId.cs` 주석: `[PENDING] NET-002 ~ NET-005` |
| Network session factory/lifecycle | mode별 session 생성, Start/Stop callback 해제, approval maxPlayers | `NetworkSessionTests`가 factory/single-player event만 검증 | PARTIAL | Host/Client/Server NGO Start/Stop/approval은 남음 |
| Crafting recipe steps | gather/wait/surface/all steps/null step 조건 | `CraftingRecipeTests` 5개 | COVERED | EditMode PASS 144/144 |
| SO Registry wiring | 신규 tool/item/resource/terrain registry에 등록 | `ToolResourceMatchingTests`, `SeededWorldGeneratorTests` | PARTIAL | core assets 일부만 검증, 모든 SO 범용 registry validator 테스트 없음 |
| UI inventory/equipment | panel open/close, selected item, equip, drag, stack | `InventoryEquipmentScenarioTests`, `InventoryEquipmentUiScenarioTests` | COVERED | EditMode/PlayMode PASS |
| UI status/time/toast/generation/menu | status/time label, toast duration, generation continue, mode buttons | 일부 구조/간접 테스트만 있음 | PARTIAL | 관련 클래스 검색 대비 직접 테스트 부족 |
| Addressables UI sprite consistency | UI 주소 상수와 AddressablesSetup 동기화 | `UISpriteAddressesTests` | COVERED | 6개 EditMode PASS |
| Runtime Resources fallback policy | Addressables 우선, fallback 허용 위치 제한, 신규 `Resources.Load` 차단 | `StaticRuntimeUsageGateTests.STATIC_002_RuntimeResourcesAndFindUsages_StayInsideReviewedAllowlist` | PARTIAL | 신규 사용은 차단, 기존 허용 목록 제거는 별도 리팩터 필요 |
| Editor build/data tools | client/server build method, registry validator, one-click setup idempotency | 없음 또는 간접 | MISSING | `BuildScript`, `GameDataRegistryValidator`, `Editor/Tools` 전용 테스트 없음 |

## 5. 테스트 부족 영역
| 우선순위 | 영역 | 누락된 테스트 | 왜 위험한가 | 권장 테스트 종류 |
|---|---|---|---|---|
| P0 | Network `NET-002` | dedicated server + client loopback + player spawn PlayMode/CI 시나리오 | MVP 범위에 dedicated server 접속이 명시되어 있으나 현재 loopback 경로가 없다 | PlayMode integration, 가능하면 headless/loopback scenario |
| P0 | Network session lifecycle | `NetworkBootstrap.HandleBootstrapped`, host/client/server Start/Stop, approval full rejection | factory/single-player 단위 테스트는 생겼지만 NGO callback wiring과 approval은 아직 잡지 못한다 | PlayMode NGO harness |
| P1 | Runtime `Resources.Load`/Find fallback | `DataManager` Addressables 실패 fallback, `FarmAutoFiller` registry/sprite fallback, `SeededFarmWorldApplier` fallback 실패 경로 | 신규 사용 게이트는 생겼지만 기존 fallback 동작/실패 경로와 제거 계획은 남아 있다 | PlayMode fallback/error-path tests + 리팩터 |
| P1 | HUD non-inventory UI | `StatusHud`, `TimeHud`, `ToolUnlockToast`, `GenerationTransitionPanel`, `ModeSelectPanel`의 button/update/event lifecycle | UI가 빌드에서 보이지 않거나 버튼이 안 눌려도 현재 inventory 중심 테스트만으로 놓칠 수 있다 | EditMode component tests + PlayMode UI flow |
| P1 | Registry validator/build tools | `GameDataRegistryValidator`, `BuildScript` build options/scenes, setup idempotency | 데이터 누락과 빌드 설정 회귀는 테스트보다 수동 setup에 의존한다 | Editor tests |
| P2 | Save robustness | corrupted metadata JSON, partial write, duplicate/concurrent write, directory missing | 현재 happy/error 일부만 검증되어 저장 파일 손상 회귀에 약하다 | EditMode filesystem edge tests |
| P2 | Resource drops and RNG | min/max drop bounds, no item when broken twice, deterministic option or seeded harness | 채집 보상 수량이 랜덤이라 밸런스/회귀 재현성이 낮다 | EditMode seeded adapter or repeated bounds test |
| P2 | PlayerStatus lifecycle | `StatusEffectDefinition` 배열 적용, Update tick, restore/penalty와 HUD 연결 | `StatusValue`는 검증됐지만 실제 MonoBehaviour 상태 루프는 약하다 | EditMode/PlayMode component tests |
| P2 | World scene application failures | no tilemap/root/resources, missing registry, duplicate applier | generator pure logic은 강하지만 scene application failure path가 약하다 | PlayMode scene harness |
| P3 | Test count/coverage gate | `ScenarioId`와 테스트 클래스/메서드 자동 동기화 | pending scenario가 주석으로만 남아도 CI가 차단하지 못한다 | EditMode meta-test or CI script |

## 6. 정적 분석 결과
- 엔티티 ID 분기: PASS. `rg -n -g '*.cs' 'crop(Id|\.Id|\.id)\s*==|tool(Id|\.Id|\.id)\s*==|switch\s*\(\s*(crop|tool|knowledge).*\\)|enum\s+(CropId|ToolId|KnowledgeId)|class\s+(Wheat|Carrot|StonePickaxe|StoneAxe|StoneHoe)' Assets/Scripts/Game Assets/Scripts/UI Assets/Scripts/Network` 결과 없음. 원 요구 명령의 `toolId` hit(`KnowledgeProgress.cs:54`)는 문자열 key 생성 변수명이며 엔티티별 분기가 아니다.
- Resources.Load 신규 사용: PARTIAL/RISK. 런타임 C# hit:
  - `Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs:52` `UnityEngine.Resources.Load<GameDataRegistry>("GameDataRegistry")`
  - `Assets/Scripts/Game/Managers/DataManager.cs:45` `UnityEngine.Resources.Load<GameDataRegistry>("GameDataRegistry")` (주석상 Addressables 실패 fallback)
  - `Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs:47` `Resources.Load<GameDataRegistry>("GameDataRegistry")`
  - `Assets/Scripts/UI/HUD/StatusHud.cs:25` `Resources.Load<Font>("Fonts/VaultUI")`
  - Editor-only `SceneSetup.cs:197` font fallback도 존재
- 런타임 GameObject.Find/FindObjectOfType: RISK. 런타임 C# hit:
  - `Managers.cs:28` `GameObject.Find("@Managers")`
  - `StatusHud.cs:418` `GameObject.Find("Player")`
  - `SeededFarmWorldApplier.cs:105` `GameObject.Find("[Resources]")`
  - `FarmGrid.cs:68`, `SaveSlotSelectPanel.cs:20/234/252`, `FarmAutoFiller.cs:446`, `SeededFarmWorldApplier.cs:21/79`, `GatherInteractor.cs:233` 등 `FindFirstObjectByType`/`FindObjectsByType` 사용. 일부는 bootstrap/setup 성격이나 정적 게이트상 예외 기준이 필요하다.
- 런타임 UnityEditor 노출: PASS. `Assets/Scripts/Game`, `Assets/Scripts/UI`, `Assets/Scripts/Network`에서 `using UnityEditor;`/`UnityEditor.` 검색 결과 없음.
- 환경값 하드코딩: PARTIAL/RISK.
  - `Assets/Scripts/Game/Bootstrap/AppConfig.cs:15` default port `7777`
  - `Assets/Scripts/Game/Bootstrap/AppConfig.cs:18` default join IP `"127.0.0.1"`
  - `Assets/Scripts/Network/Session/HostSession.cs:31`, `DedicatedServerSession.cs:37` bind address `"0.0.0.0"`
  - 이 값들은 CLI default/config로 쓰이지만 `.claude/rules/environment-config.md` 관점에서는 후속 설정 데이터화 검토 대상이다.

## 7. 실행한 검증 명령
| 명령/도구 | 결과 |
|---|---|
| `Get-Content AGENTS.md` 및 6개 `.claude/rules/*` 문서 | 감사 기준 확인 완료 |
| `rg --files Assets/Scripts Assets/Tests` | 대상 경로 파일 목록 수집. 코드 파일: Game 64, UI 6, Network 7, Editor 10 |
| `rg -n "\[Test\]|\[UnityTest\]|class .*Tests" Assets/Tests` | 테스트 클래스/메서드 위치 수집. 프로젝트 테스트: EditMode `[Test]` 130개, PlayMode `[UnityTest]` 12개 |
| `rg -n "Resources\.Load|GameObject\.Find|FindObjectOfType|FindObjectsOfType|switch\s*\(|cropId|toolId|enum\s+.*Id" Assets/Scripts` | 원 요구 정적 분석 실행. `switch(arg)`, runtime/editor `Resources.Load`/Find, `toolId` 변수명 hit 확인 |
| `rg -n -g '*.cs' ... Assets/Scripts` 보정 검색 | `.meta` 노이즈 제거 후 C# 정적 분석 판단 |
| MCP `scene-list-opened` | `Farm` 씬 loaded, dirty=false. 테스트 실행 전 저장 블로커 없음 |
| MCP `tests-run` EditMode | 테스트 추가 전 PASS 131/131. 테스트 추가 후 PASS 144/144, 실패 0, skipped 0, duration 약 4.6초 |
| MCP `tests-run` PlayMode | PASS 12/12, 실패 0, skipped 0, duration 약 7.3초 |
| MCP `tests-run` EditMode `includePassingTests=true` | Addressables 패키지 스텁 1개 포함 확인 |
| `git status --short Assets production` | 기존 변경/미추적 파일 다수 존재. 본 감사는 코드 수정 없이 보고서만 추가 |

## 8. 권장 후속 작업
1. `[TEST] NET-002 loopback PlayMode 시나리오 추가`
   - dedicated server/host + client 접속 + player spawn까지 최소 경로 검증.
2. `[TEST] Host/Client/DedicatedServer lifecycle PlayMode harness 추가`
   - `StartAsync`/`StopAsync`, callback 해제, dedicated approval full rejection 검증.
3. `[TEST] Runtime Resources/Find fallback 실패 경로 테스트 추가`
   - 정적 게이트 외에 Addressables 실패, registry 부재, scene root 부재 때의 로그/무해성 검증.
4. `[TEST] HUD 상태/시간/toast/세대/menu UI 컴포넌트 테스트 추가`
   - `StatusHud`, `TimeHud`, `ToolUnlockToast`, `GenerationTransitionPanel`, `ModeSelectPanel`의 이벤트 구독/버튼/label update 검증.
5. `[TEST] Editor build/data validator 테스트 추가`
   - `BuildScript` scene list/options, `GameDataRegistryValidator`, `AddressablesSetup` idempotency를 Editor test로 검증.

## 완료 감사 체크
- 모든 명시 대상 경로 검색: 완료 (`Assets/Scripts/Game`, `UI`, `Network`, `Editor`, `Assets/Tests/EditMode`, `PlayMode` 포함).
- 코드 인벤토리와 테스트 인벤토리 매핑: 완료.
- 테스트 통과만으로 커버 판단하지 않고 assertion/테스트명/정적 검색 결과와 대조: 완료.
- MISSING/PARTIAL 항목 공개: 완료 (`NET-002`, Network NGO lifecycle/approval, UI 일부, Editor tools 등).
- EditMode/PlayMode 결과 MCP 출력과 일치: 완료.
- 보고서 파일 생성: 완료.
