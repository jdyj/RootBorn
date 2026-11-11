# Unity 시나리오 → MonoGame 판 대응

2026-11-11 기준. 이 폴더(`Rootborn.Game.Tests`)는 화면 없이 `GameSession` 명령만으로 **규칙과 저장이 흐름대로 진행되는지** 본다(실제 `data/`, 임시 폴더 저장소, 단계마다 새 `GameSession`으로 다시 읽기). 근거 종류는 모두 **자동 테스트**다. 화면·입력은 `Rootborn.Screens.Tests`(자동)와 `Rootborn.Host.Tests`(장치 이벤트 주입)가 맡는다.

열 뜻:
- **옮김**: 이미 옮긴 테스트(파일.테스트 또는 파일). `—`는 아직 없음.
- **옮길 수 있나**: 예 = 규칙·저장 흐름이다. 아니오 = 순수 화면·씬·입력 장치·Unity 수명 주기·네트워크 프로세스이고, 그 이유를 적는다. 부분 = 규칙 부분만 옮길 수 있다.

## 1. `Assets/Tests/PlayMode` 시나리오 목록

### EndToEnd

| 테스트 | 확인하는 것 | 옮김 | 옮길 수 있나 |
|---|---|---|---|
| MAIN_E2E_000 SchoolOpeningChainFromNewGameToMyRoom | 새 게임에서 메인 체인 끝(내 방)까지 | MainChainEndTests.MainChain_RunsToCompletion_TheSameWhenEveryCommandIsReadAgain | 예 |
| MAIN_E2E_001 BackToSchool_MomStartsChainThenPlanAndTeacherCount | 엄마 대화로 체인 시작, 일과·선생님 목표 | MainChainTests.Step001_… | 예 |
| MAIN_E2E_002 SleepingIntoDayTwoOpensTheTour | 자고 2일째 둘러보기 단계 | MainChainTests.MainChain_RunsFromTalkingToMomToMyOwnSpace | 예 |
| MAIN_E2E_003 VisitingTheFourCareerPlaces | 일터 네 곳 방문 | MainChainTests(같은 흐름) | 예 |
| MAIN_E2E_004 FirstPartTimeJob_AShiftCounts | 가게 알바 한 번 | MainChainTests(같은 흐름) | 예 |
| MAIN_E2E_005 DayThreeOpensMyDream | 3일째 장래희망 단계 | MainChainTests(같은 흐름) | 예 |
| MAIN_E2E_006 ReadingJobBooksAtTheLibrary | 도서관 직업 책 | MainChainTests(같은 흐름) | 예 |
| MAIN_E2E_007 PlacingRoomFurnitureCompletesTheChain | 집에 가구 놓아 체인 완료 | MainChainEndTests.Step007_… | 예 |
| MAIN_E2E_008 BuyingFurnitureWithShiftAllowance | 알바 용돈으로 가구 사기 | MainChainEndTests.Step008And010_… | 예 |
| MAIN_E2E_009 ChoosingADreamShowsItInTheJournal | 장래희망 고르기(수첩 표시는 화면) | MainChainEndTests.Step009_… | 부분(수첩 표시는 화면) |
| MAIN_E2E_010 PlacingBoughtFurnitureCompletesTheChain | 산 가구로 완료 | MainChainEndTests.Step008And010_… | 예 |
| MAIN_E2E_011 ASecondDreamChangeTheSameDayIsRefused… | 같은 날 두 번째 바꾸기 거절, 이어 하기 뒤에도 | MainChainEndTests.Step011_… | 예 |
| ADULT_E2E_001 ReadyStudentChoosesTheChefRoute… | 시설 고르기로 성인 1단계 | AdultCareerTests.Adult001_… | 예 |
| ADULT_E2E_002 AdultPlaysTheFirstWorkFromThePanel | 일 예약·정산 | AdultCareerTests.Adult002_… | 예 |
| ADULT_E2E_003 UpgradeToLevelThree…AndWorkGoesOn | 3단계 올리기·경로 끝, 그 뒤에도 일이 셈 | AdultCareerTests.Adult003_…, AdultAutoCompleteTests.Adult003_AfterTheRouteEnds_… | 예(업적 표시는 화면) |
| ADULT_E2E_004 LastPassedWorkUnlocksAutoComplete… | 다섯 번째 통과로 자동 완료 열림, 만점 보상 한 번, 시간 다 씀 | AdultAutoCompleteTests.Adult004_… | 예(단추 활성은 화면) |
| BOOT_E2E_002 ReenteringBootKeepsSingleManager… | 부트 씬 재진입 시 매니저 하나 | — | 아니오: Unity 싱글톤·씬 수명 주기 |
| SAVE_E2E_001 SaveSlotUiUsesInjectedTestRoot… | 슬롯 화면이 테스트 저장 폴더를 쓰고 메타데이터 보존 | — | 아니오: 슬롯 화면. 저장 쪽은 Data.Tests의 저장소 테스트 |
| BOOT_FLOW_PM_001 NewGameFromBootCanEnterPoliceInteriorDoor | 새 게임 → 경찰서 문 | — | 아니오: 맵 이동(World.Tests·Host.Tests 몫) |
| CAREER_001 SquareHelpGivesServiceCareerHint… | 광장 도움으로 서비스 진로 힌트, 다시 읽어도 | FreePathTests.SquareHelpRequest_… | 예 |
| ENCYCLOPEDIA_E2E_001_008 SaveSlotDiscoveryUiReloadAndDedupe | 발견 지점 → 도감 해금·새 표시, 도감 화면, 다시 읽기, 중복 없음 | ExplorationAndEncyclopediaTests.Encyclopedia_… (탐색 선택으로 해금) | 부분: 발견 지점(Discovery) 명령과 본 표시 지우기 명령이 GameSession에 없다(아래 필요한 변경). 화면 부분은 아니오 |
| EXPLORATION_CHOICE_E2E_005_011 OpenChoosePersistAndDedupe… | 합성 탐색 지점의 선택·결과·저장·중복 | ExplorationAndEncyclopediaTests.Exploration001_… (실제 data의 지점으로) | 예(합성 정의 대신 실제 data) |
| REL_001 TalkToMomAtHomeThenSquareHelpRaisesTrust… | 엄마 대화 뒤 광장 도움으로 신뢰, 한 번씩 | HelpAndTrustTests.Rel001_… | 예 |
| QUEST_DIALOGUE_E2E_001 ClickMomsAcceptChoiceThenJournal… | 2일째 엄마 대화로 퀘스트 받기, 바로 저장, 다시 권하지 않음 | MomQuestTests.QuestDialogue001_… | 예(수첩·씬 넘나들기는 화면) |
| QUEST_WOOD_E2E_001 AcceptFromMomGatherTownTrees… | 나무 퀘스트 받기 → 나무 베기 → 보상 한 번 | MomQuestTests.QuestDialogue001_…(받기까지) | 부분: 자원 채집 명령이 GameSession에 없다 |
| QUEST_DECORATE_E2E_001 PlaceThreeFurniture…ClaimOnce | 가구 세 개로 완료, 보상 한 번, 다시 읽어도 | MomQuestTests.QuestDecorate001_… | 예 |
| HOUSE_FLOW_001_003 NewGamePlaceSaveExitLoadRestores… | 놓은 가구가 저장되고 다시 들어오면 돌아옴 | FurnitureTests.PlacedFurniture_IsSavedToTheStageFile_… | 예(그림 증거는 화면) |
| KNOWLEDGE_STATUS_E2E_001 ActivityChainHintReloadNoDuplicate | 작업장 일로 피로 +1·기록 한 번, 새 요청은 한 번 더 | ActivityStatusAndRecipeTests.KnowledgeStatus001_… | 예(수첩 줄·HUD는 화면) |
| BOOT_E2E_003 MainMenuSinglePlayButtonRespondsToRealPointerClick | 메인 메뉴 단추 실제 포인터 | — | 아니오: 입력 장치 |
| OBJECTIVE_JOURNAL_SOURCE_COLLISION_PM_001 MultiSourceReload… | 수첩 출처별 줄 키가 겹치지 않음, 추적 지정이 저장됨 | — | 아니오: 수첩 화면. 추적 지정 저장은 Core에 세이브 모양이 없다(game/README.md) |
| JOB_001 CornerStoreShiftThroughMinigameSurvivesReloadOnce | 가게 알바 성장 한 번, 기록 한 번 | PartTimeWorkGrowthTests.Job001_… | 예 |
| JOB_002 EveryJobPassesFromTheScreen (9개 알바) | 알바마다 성장 한 번, 이어진 진로 후보만 이해도 | PartTimeWorkGrowthTests.Job002_… (9건) | 예(미니게임 판 플레이는 화면) |
| REGISTRY_PROVIDER_002 ManagersDestroyedMidLoad… | Unity 매니저가 로드 중 사라져도 다시 읽음 | — | 아니오: Unity Addressables·매니저 |
| REGISTRY_PROVIDER_E2E_001 MainMenuNewGameEntersTown… | 새 게임 → 마을, 데이터 공급자 설치 | NewGameTests(시작 값) | 부분: 설치 순서는 Unity 전용 |
| FREE_PATH_001 FocusGrowsFromLibrarySelfStudyAndFromSchoolClass | 같은 집중이 자습과 수업 두 길로 | SelfStudyAndClassTests.FreePath001_… | 예 |
| FREE_PATH_004 LibraryRefusedWhenSpent… | 시간 다 쓰면 자습 거절, 아무것도 안 바뀜 | SelfStudyAndClassTests.FreePath004_… | 예(거절 안내 글자는 화면) |
| CREATOR_001~005 (캐릭터 만들기 5건) | 만들기 화면 열기·단계 화살표·확정 저장·무작위·뒤로 | — | 아니오: 화면. 옷 저장 규칙은 Core WearableSaveTests. 이름·시작 옷 명령은 GameSession에 없다 |
| DAY_END_001 EndDayReloadShowsSameResultThenNextDay | 하루 끝 → 다시 읽어도 결과 대기 → 다음 날 | DayEndLoopTests.DayEnd001_… | 예 |
| TOWN_PAINTED_COLLISION_001 | 그린 막힘 칸과 빈 칸 | World.Tests(충돌 대조) | 아니오: 월드 충돌 |
| TOWN_AUTOBOOT_001 NpcAndLocationVisitSpawnAutomatically_EOpensDialogue | 마을 NPC·방문 지점 생성, E로 대화 | — | 아니오: 씬 설치·입력 |
| TOWN_POINT_001/002 EveryPlacedPointIsReachable… | 지점마다 닿고 안내가 뜸 | — | 아니오: 월드 이동·안내 |
| EXPLORATION_CHOICE_E2E_001_011 SaveSlotTownPlacedPoint… | 마을 탐색 지점: 잠긴 선택, 고르기, 저장 파일에 id, 다시 고르면 거절 | ExplorationAndEncyclopediaTests.Exploration001_…, Exploration_SelfStudyOutsideSchool_OpensTheCarefulChoice | 예 |
| HELP_001 ErrandForMomGrowsResponsibilityAndTrust… | 심부름으로 책임감·신뢰, 한 번씩, 두 번째는 한 번 더 | HelpAndTrustTests.Help001_… | 예 |
| TOWN_RESOURCE_RECIPE_E2E_001 ActivityRewardHintReloadNoDuplicate | 광장 도움 레시피 공개 한 번, 반복해도 기록 없음 | RecipeRevealTests, ActivityStatusAndRecipeTests.TownResourceRecipe001_… | 예(수첩 줄은 화면) |
| TRAVEL_E2E_001 HouseTownSchoolFacilityThenContinue… | 맵을 옮겨 다니고 이어 하면 마지막 자리 | — | 아니오: 월드 진행(world-progress.json)은 Host 몫. Host NewGameJourneyTests가 비슷 |

### 그 밖

| 테스트 | 확인하는 것 | 옮김 | 옮길 수 있나 |
|---|---|---|---|
| BootSmokeTest.Bootstrap_PopulatesConfig | 부트 설정 | — | 아니오: Unity 부트 |
| FACILITY_WORLD_PLAY_001 | 시설 월드 그림 주소 해제 한 번 | — | 아니오: Addressables 수명 주기 |
| GROW_PERF_001 | 걷는 동안 할당 없음 | — | 아니오: Unity 성능 |
| GROW_PLAY_001/002 | 걷기 행 바꾸기, 단계마다 모습 | — | 아니오: 그림 |
| GROW_PLAY_003/004 | 옷 고르기와 다시 읽기 | — | 부분: 옷 저장 규칙은 Core WearableSaveTests. GameSession에 옷 명령 없음 |
| GROW_PLAY_005 CommittingAChefCareerGrows…ChefLook | 셰프가 되면 성인 단계 | StudentClockAndGrowthTests.TheGrowthStage_FollowsTheStudentDay_AndAdulthood | 부분(그림은 화면) |
| VISUAL_DAYNIGHT_001~003 | 낮밤 조명 | — | 아니오: 그림. 시간 규칙은 Core DayPartsTests |
| ENCYCLOPEDIA_E2E_003_004_005_006_009 | 도감 화면 분류·잠긴 줄·상세 | — | 아니오: 화면 |
| GLOBAL_UI_PM_001 | Esc로 설정 | — | 아니오: 화면·입력 |
| HouseScene_* (2건) | 집 씬 바탕, 카메라 경계 | — | 아니오: 씬·카메라 |
| Interiors/* (37건: 가구 배치 화면·카메라·오버레이·검색 칸 포커스·교실 걷기·그림 주소) | 팔레트 클릭, 미리보기, 회전·옮기기·지우기, 오버레이 정리, 카메라, 교실 가림 | FurnitureTests(배치·옮기기·지우기·저장 규칙) | 부분: 규칙(놓기·옮기기·지우기·막힘)은 옮김, 나머지는 화면·입력 |
| EQP_002, DRG_001 | 장비 단추, 가방 칸 끌기 | — | 아니오: 화면. 가방 칸 규칙은 Core ItemSlotsTests |
| LifeTimeOwnership* (3건) | 시간 주인 컴포넌트 고르기 | — | 아니오: Unity 컴포넌트 |
| ModernUiPanelAutoInstaller, ModernUiReconstruction*(2), TownStyle2*(5), UrbanInventoryBrowseScrollbar, UrbanUiModalLifecycle | Unity UI 설치·그림 | — | 아니오: Unity UI |
| DEDICATED_HARNESS_PM_001~009 | 전용 서버 프로세스 실행기 | — | 아니오: 네트워크 프로세스 |
| NET_JOIN_001/002 | 친구 방 패널, Steam 없을 때 안내 | — | 아니오: 화면·Steam |
| MULTIPLAYER_PM_001 | 호스트 세션 시작·정리 | — | 아니오: 네트워크. 규칙 일부는 Core SteamSessionRulesTests·SessionCapacityTests |
| OBJECTIVE_JOURNAL_PM_001/002 | Tab으로 수첩, 줄 클릭·추적 | — | 아니오: 화면(Host ObjectiveJournalHostTests) |
| NonHouseTilePlacementSurface_… | 집 밖 면에서도 같은 배치 흐름 | — | 부분: 면 id 규칙은 Core FurniturePlacementTests. 집 밖 방 칸을 넘길 명령이 없다(EnterHouse만) |
| PLAYER_STATE_NET_PM_001 TwoRuntimePlayersKeepSeparateInventories… | 한 슬롯 두 플레이어 인벤토리 따로 | TwoPlayersOneSlotTests.PlayerStateNet001_…, …KeepSeparateQuestChains | 예 |
| PLAYER_STATE_NET_PM_002 TwoRuntimePlayersKeepSeparateQuestProgress… | 한 슬롯 두 플레이어 일반 퀘스트 따로 | — | 예, 그러나 GameSession이 막는다: 모든 플레이어가 `quest-log.json` 하나를 쓴다(아래 필요한 변경) |
| QUEST_001 NpcInteraction_OpensAndClosesDialogue | NPC 대화 열고 닫기 | SideDomainTests.DialogueChoice_… | 부분(창은 화면) |
| QUEST_015 NpcQuest_FullFlow_ClaimReward_SetsStoryFlag | 합성 퀘스트 받기→채집→보상·깃발, 두 번 안 줌 | Core QuestLogTests·QuestConditionRewardTests(합성 정의) | 예(규칙은 Core에 있음). 실제 data로는 채집 명령이 없어 못 함 |
| QUEST_003 InteractableObject_ProvidesQuestDefinition | 물건이 퀘스트를 줌 | — | 아니오: Unity 컴포넌트 |
| SaveSlotSelectPanel_Show_DisplaysThreeSlotCards | 슬롯 카드 셋 | — | 아니오: 화면 |
| TOWN_UI_PM_002 | 이벤트 시스템 중복 제거 | — | 아니오: Unity |
| TOWN_DOOR_PM_001~013 (13건), TOWN_LEVEL_PM_001 | 문·레벨 이동 | World.Tests(문 목적지·도착 지점), Host PATH_PLAY_001 | 아니오: 월드 |
| INTERACT_PM_001/002/005 | 가까운 대상 안내·고르기 | Core InteractionTargetsTests, World.Tests InteractionDataTests | 아니오: 입력·안내(규칙은 Core에 있음) |
| WorldTravelRecovery* (7건) | Unity 씬 이동 실패 복구 | — | 아니오: Unity 씬 로더 |

## 2. `testing-discipline.md` "ROOTBORN 도시 생활 시나리오" id 대응

| id | 대응 테스트 | 비고 |
|---|---|---|
| TOWN-001 | Game NewGameTests.NewGame_StartsWithTheUnityStartingValues, Host NewGameJourneyTests.JOURNEY_001_… | farm/crop 용어 노출 검사는 없음 |
| LIFE-001 | Core StudentLifeActivityTests(LIFE_STUDENT_002~006), Game SelfStudyAndClassTests, HelpAndTrustTests, ActivityStatusAndRecipeTests | 비용 거절 시 무변경: SelfStudyAndClassTests.FreePath004_… |
| LIFE-002 | Core StudentLifeActivityTests.LIFE_STUDENT_007_…, Game RewardOnceTests.TheSameLocationActivityRequestTwice_GrowsOnce, HelpAndTrustTests(같은 요청 거절) | |
| CAREER-001 | Core CareerPracticeTests·CareerCandidateTests, Game FreePathTests, PartTimeWorkGrowthTests.Job002_…, AdultCareerTests | |
| REL-001 | Game HelpAndTrustTests.Rel001_…, Help001_… | |
| JOB-001 | Game PartTimeWorkGrowthTests.Job001_…, RewardOnceTests, Core PartTimeWorkTests | |
| KNOW-001 | 없음 | 지식 노드는 게임에서 쓰이지 않아 옮기지 않았다(`Rootborn.Data/Builders/NotUsedByGame.cs`: 유일한 트리거가 어느 씬에도 없는 바위 노드) |
| KNOW-002 | 없음 | KNOW-001과 같은 이유 |
| TOOL-001 | 없음 | ToolDefinition도 NotUsedByGame(장착할 길이 없음) |
| STATUS-001 | 부분: Game ActivityStatusAndRecipeTests.KnowledgeStatus001_…(활동으로 상태 값 +1·저장) | Unity StatusValue(Tick 누적·최대 클램프·0 하한·페널티 곡선)는 Core로 옮기지 않았다 |
| STATUS-002 | Core DayClockTests | |
| NET-001 | 없음 | Unity ArgsParser(-mode/-port/-joinIp)는 옮기지 않았다. MonoGame Host는 다른 인자(`HostOptions`, game/README.md)이고 네트워크 실행 모드가 없다 |
| NET-002 | 없음 | 전용 서버·네트워크 동기화가 MonoGame 판에 없다. 규칙 일부만 Core SteamSessionRulesTests·SessionCapacityTests |
| HEIR-001 | 없음 | HeirGenerator는 Unity(`Assets/Scripts/Game/Heir`)에만 있고 Core로 옮기지 않았다 |

## 3. 옮기다 찾은 필요한 변경 (여기서는 고치지 않음)

1. **일반 퀘스트 기록이 플레이어마다 나뉘지 않는다.** `GameSession`은 `SaveFileNames.QuestLog`(`quest-log.json`)를 모든 플레이어에게 쓴다. 한 슬롯에서 player-1이 받은 퀘스트가 player-2에게도 받은 상태로 읽힌다. Unity 판(`PlayerQuestRuntime.LogFileName`)은 기본 플레이어가 아니면 `quest-log-<플레이어>.json`을 썼다. 필요한 변경: Core `SaveFileNames`에 플레이어별 퀘스트 기록 이름을 더하고 `GameSession.Load/Save`가 그것을 쓴다(기본 플레이어는 `quest-log.json` 그대로).
2. **답한 하루 이벤트를 새 요청 id로 다시 고를 수 있다.** Core `DailyEventRunner.TryChoose`는 요청 id 중복만 보고 기록 상태(완료·거절)를 보지 않는다. 되풀이 안 되는 광장 이벤트를 거절한 뒤 같은 날 새 요청으로 "지금 돕기"를 고르면 적용된다(목록 `AvailableDailyEvents`에는 안 나오므로 화면으로는 못 고르지만, 명령은 막지 않는다). 필요한 변경: `TryChoose`가 `GetAvailableEvents`와 같은 상태 판정(되풀이 안 되는 완료·거절·만료, 쿨다운, 미룬 날)을 하고 거절한다.
3. **발견 지점 명령이 없다.** Unity `DiscoveryPointInteractor`(Core `DiscoveryRunner.TryDiscover` + 도감 해금 평가)에 해당하는 `GameSession` 명령이 없다. 필요한 변경: `Discover(discoveryId)`(실행 → 저장 → 도감 해금 조건 평가).
4. **도감 본 표시를 지우는 명령이 없다.** Core `EncyclopediaProgress.MarkSeen`이 있지만 `GameSession`이 내주지 않는다. 필요한 변경: `MarkEncyclopediaEntrySeen(entryId)`(적용되면 저장).
5. **자원 채집 명령이 없다.** 나무 모으기 퀘스트(`quest.gatherWood`)를 실제 data로 끝까지 진행할 수 없다. 필요한 변경: 자원 노드 치기·채집 명령(채집 이벤트를 퀘스트에 알리고 저장).
