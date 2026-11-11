# 엔진 이전 전수조사 — Unity → MonoGame 진행 상태 (2026-11-12)

- **조사한 커밋: `0f1b5c2f5`** (브랜치 `jdyj/progress-audit`, 조사 시작 때 원격 main과 같았다).
- 조사 도중 main이 `d90f073df`로 두 커밋 나아갔다(`8370a48bf` 규칙 문서 정리, `d90f073df` Unity 제거 준비 조사). 아래 내용은 이 둘을 반영하지 않았다. 다만 그 내용과 겹치는 곳에는 따로 적었다.
- 방법: 문서와 세션 보고를 믿지 않고, 코드를 읽고 테스트를 직접 돌려 대조했다. 영역 대조와 리터럴 세기의 일부는 읽기 전용 조사 도우미 4개에 나눠 맡겼다. 그 결과 중 중요한 것(이야기 깃발 `null`, 퀘스트 기록 파일 이름, 하루 이벤트 재선택, 테마 누락 등)은 작성자가 코드를 열어 다시 확인했다.
- 수치는 모두 직접 센 것이다. 추정은 "추정"이라고 적었다.

---

## 0. 요약

### 프로젝트 0~7

| # | 프로젝트 | 상태 | 한 줄 근거 |
|---|---|---|---|
| 0 | 스파이크 | 끝남 | 설계 문서 6절 결과표. 이번에는 다시 돌리지 않았다 |
| 1 | 규칙 옮기기 | **대부분 끝남. 빈 곳 있음** | Core 테스트 856건 통과. 데이터 종류 189종은 모두 분류됐다. 그러나 데이터가 없는 규칙 코드(이야기 깃발 모음, ModernSociety, VerticalSlice)는 이유도 없이 빠졌다. 그중 이야기 깃발은 실제 버그다(4.1). 설계 문서가 요구한 "옮기지 않은 테스트 목록"도 없다 |
| 2 | 데이터 내보내기 | 끝남 | 189종 = 등록 163 + 규칙 아님 19 + 게임이 안 씀 7 + 아직 안 옮김 0. 변환기를 다시 돌려도 결과가 같다(`.gitattributes`만 다름). 못 푼 참조 12건은 기록되어 있다 |
| 3 | 월드 | **일부** | 맵 10개, 걷기·충돌·문·NPC 자리·낮밤이 있다. 임시 연결이 16개이고 알려진 맵 문제가 7건이다. 가로등 조명과 씬별 카메라 경계는 없다 |
| 3b | 실행 프로그램·UI 토대 | 끝남 | Ui 114건, Host 96건 통과 |
| 4 | 화면 옮기기 | **끝 기준은 통과. 빈 곳 있음** | 메인 체인 Host 테스트 12건이 실제 입력으로 통과했다. Unity 화면 31개 중 25개를 옮겼고 6개는 이유를 적고 뺐으며 빠진 것은 0이다. 그러나 화면 5개는 실제 맵에서 여는 길이 없다 |
| 5 | 멀티플레이·Steam | **시작 전** (main 기준) | main에는 `game/Rootborn.Net`과 Steam 코드가 없다. 조사 문서만 있다. 브랜치 `jdyj/net-base`에 합치지 않은 커밋 2개가 있다(UDP·루프백 전송, Steam 없음) |
| 6 | 테스트 이식 | **일부** | `SCENARIOS.md` 대응표가 있다. KNOW-001/002, TOOL-001, HEIR-001, NET-001/002는 대응이 없고 STATUS-001은 일부만 있다 |
| 7 | Unity 제거 | 시작 전 | `Assets/`, `ProjectSettings/`가 그대로 있다. CI는 이미 Unity를 부르지 않는다. 계획 문서는 main `d90f073df`에만 있다 |

### 지금 이 게임으로 할 수 있는 것 (근거는 자동 테스트와 장치 이벤트 주입 테스트. 사람이 직접 해 본 것은 아니다)

- 실행 → 슬롯 선택 → 새 게임 → 캐릭터 만들기 → 엄마 대화 → 일과 → 학교 → 마을 일터 네 곳 방문 → 가게 알바(순서 외우기 미니게임) → 장래희망 고르기 → 도서관 → 가구 사기 → 집에 가구 놓기 → 메인 체인 완료. 중간에 끄고 이어 하기도 된다.
- 맵 10개를 걸어 다닐 수 있다: 마을, 해변, 학교, 집, 실내 6개. 문 통과, NPC 대화, 마을 낮밤도 된다.
- 가방, 상태, 설정(언어 바꾸기), 목표 수첩과 추적 HUD, 도감, 옷장, 마을 기록, 하루 이벤트, 하루 결과를 쓸 수 있다.

### 아직 못 하는 것

- 멀티플레이, Steam 친구 방(main에 코드가 없다).
- 실제 맵에서 탐색 선택, 성인 시설, 진로 연습 미니게임을 할 수 없다. 화면은 있지만 `interactions.json`에 그런 지점이 없어서 시험용 파일로만 열린다.
- 나무 퀘스트를 끝낼 수 없다. 채집 명령이 없고, 이야기 깃발이 `null`이라 보상도 받을 수 없다.
- 발견 지점, 장소 기록 화면을 여는 길도 없다.
- 배포 폴더로 실행: 코드로 보면 시작하자마자 멈춘다(테마 폴더가 빠짐, 4.4). 실행해서 확인하지는 않았다.
- 가로등 조명, 소리(Unity 판에도 없음), NPC 걷기(Unity 판에도 없음).

---

## 1. 테스트와 검사 (직접 실행)

`export PATH="$HOME/.dotnet:$PATH"`, dotnet 8.0.422, Windows 11.

| 명령 / 프로젝트 | 건수 | 실패 | 건너뜀 | 걸린 시간 |
|---|---|---|---|---|
| `dotnet test Tools/Rootborn.Core.Tests` | 856 | 0 | 0 | 실행 0.8초, 명령 5초 |
| `dotnet test Tools/Rootborn.DataExport.Tests` | 24 | 0 | 0 | 1초, 명령 6초 |
| `dotnet test game/Rootborn.sln` 합계 | **847** | **2** | 0 (+Explicit 1) | 명령 363초 |
| └ Ui.Tests | 114 | 0 | 0 | 0.3초 |
| └ World.Tests | 50 | 0 | 0 | 10초 |
| └ Data.Tests | 187 | **1** | 0 | 21초 |
| └ Game.Tests | 99 | 0 | 0 | 35초 |
| └ Screens.Tests | 301 | **1** | 0 | 2분 54초 |
| └ Host.Tests (실제 창) | 96 | 0 | 0 | 5분 45초 |
| `bash Scripts/ci/run-all.sh --checks-only` | 검사 6개 | 0 | — | 106초 |

**실패 2건은 모두 시간 기준 테스트였다.**

- `RealDataTests.LoadingRealData_TakesUnderOneSecond`: 2380ms가 나왔고 기준은 1000ms 미만이다.
- `FurniturePlacementScreenTests.OpeningAndPaging_With1500Pieces_IsCheap`: 28.3ms가 나왔고 기준은 16.6ms 미만이다.
- 각 프로젝트를 혼자 다시 돌리면 둘 다 통과한다. Data는 187/187, Screens는 301/301이다.
- 그러나 `LoadingRealData_TakesUnderOneSecond`만 3번 돌리면 2번 실패한다(1029ms, 1931ms). 혼자 돌려도 흔들리는 테스트다.
- `game-e2e` 세션 보고도 같은 실패(1158ms)를 "부하 때문"으로 넘겼다. **`run-all.sh` 전체가 지금 확실히 통과한다고 말할 수 없다.**

**건너뜀**

- 코드에 `Assert.Ignore`가 7곳 있다. 모두 데이터 조건에 따른 것이다: `ExplorationScreenTests:76`, `DailyPlanScreenTests:71,105`, `DayResultScreenTests:96`, `LocationScreenTests:103,116,119`.
- 이번 실행에서는 한 번도 걸리지 않았다(건너뜀 0).
- `[Explicit]`은 `ThemeConversionWriter`(`UiSkinDataTests.cs:253`, 테마 다시 쓰기)이고, 손으로 돌리는 도구다.
- `[Ignore]`와 `Assert.Inconclusive`는 0개다.

**CI 검사 6개는 모두 통과했다.**

- Core 참조 없음: 145개 파일
- MonoGame 참조 위치
- 엔티티 id 분기 없음: 301개 파일, 허용 목록 0
- 화면 한글 문구 없음: 68개 파일
- 현지화 키 일치: en·ko 각 1221개
- `.meta`: 167개

테스트가 띄운 Host 프로세스는 끝난 뒤 남지 않았다(`tasklist | grep -i rootborn` 결과 없음). 테스트가 작업 폴더 `output/host-screens-evidence` 등에 스크린샷 187개를 썼고, `.gitignore` 대상이다.

---

## 2. 규칙 (프로젝트 1)

**Core 엔진 독립성**

- `Rootborn.Core.asmdef`는 `noEngineReferences: true`이고 references가 비어 있다.
- `UnityEngine|UnityEditor|Mathf|Debug.Log|SerializeField|MonoBehaviour|ScriptableObject`를 grep하면 주석 1줄만 나온다.
- LangVersion 9.0이다(`game/Rootborn.Core/Rootborn.Core.csproj:6`, `Tools/Rootborn.Core.Tests/...csproj:7`).

**영역 대조**

- `Assets/Scripts/Game`의 34개 영역에 대해, Unity 쪽 형식 이름이 Core·game에 같은 이름으로 있는지 셌다. 이름만 비교했으므로 근사치다.

| 영역 (Unity .cs 수) | Core·game 대응 | 안 옮긴 이유가 적힌 곳 |
|---|---|---|
| StudentLife (163) | 있음. 같은 이름 302/360 | 빠진 것은 아래 "StudentLife 안" 참고 |
| Quests (46), DiscoveryClues (31), WorldState (22), Careers (18), Dialogue (12), Minigames (10) | 있음 | — |
| Placement (11), Housing (1), Encyclopedia (4), Save (2), Time (3), Resources (3) | 있음 | — |
| Crafting (5) | 일부 | GatherResourceStep: `NotUsedByGame.cs:26`. **WaitOnSurfaceStep: 이유 없음** |
| Characters (19) | 일부 (Wearable*) | `NotRules.cs:18-19` (겉모습) |
| Lighting (3) | 일부 (DayNightLightingProfile) | 설계 문서 134줄 (지역 조명) |
| Player (10), World (14) | 일부 | 설계 문서 54줄 (프로젝트 3) |
| Localization (2) | game `GameText` | — |
| Common (20) | 일부 | 따로 없음. 나머지는 그림 주소·UI 입력이다 |
| Bootstrap (8), Interiors (5) | 없음 | 설계 문서 54줄, `NotRules.cs:25-26`, SCENARIOS.md:110 |
| WorldGeneration (7), Tiles (1) | 없음 | `NotRules.cs:24-32` |
| Knowledge (6), Tools (4), Status (3) | 없음 | `NotUsedByGame.cs:12-25` |
| Heir (3), Lineage (2), Family (9) | 없음 | `NotUsedByGame.cs:31`, `NotRules.cs:20-21` |
| Managers (3) | 없음 | 따로 없음 (Addressables 기반 시설) |
| **ModernSociety (5)** | **없음** | **어디에도 없음.** Unity 안에서도 쓰는 곳이 없다 |
| **VerticalSlice (2)** | **없음** | 설계 문서 54줄이 "보고 정한다"고만 적었고 결론이 없다 |
| **Story (2)** | **일부.** 정의만 있고 플래그 모음 구현이 없다 | 정의만 `NotRules.cs:13` |

**대응도 이유도 없는 것**

1. **이야기 깃발 모음 — 실제 버그다.** 작성자가 직접 확인했다.
   - `game/Rootborn.Game/GameSession.cs:120`의 `Rewards()`가 `new RewardRuntimeContext(QuestLog, Inventory, null, null, Student, ...)`로 깃발 자리에 `null`을 넘긴다.
   - 보상을 받으려면 완료 효과까지 함께 검사를 통과해야 하는데, `SetStoryFlagCompletionEffect.CanApply`는 깃발이 `null`이면 false다. 그래서 `quest.gatherWood`는 보상을 받을 수 없다.
   - 수정은 합치지 않은 브랜치 `jdyj/game-needs-2`의 `e3cc5fbf1`에 있다.
2. ModernSociety 5개, VerticalSlice 2개: 결론이 기록되지 않았다.
3. StudentLife 안의 `LifeChoiceDefinition`, `Trait/SkillThresholdActivityRequirement`, `StudentLifeDeltaSnapshot`, `CareerPracticeBoard`, `LifeActivityBoard`: 데이터가 0건이라 검사에 걸리지 않고, 이유도 없다.

**구조적 맹점**

- "분류 안 된 종류 0" 검사(`RealDataTests.EveryTypeInData_IsClassified`)와 비어 있는 `NotPortedYet`은 **`data/` JSON 종류만** 본다.
- 데이터가 없는 규칙 코드(깃발 모음, HeirGenerator, KnowledgeProgress 등)는 이 검사 밖에 있다. 따라서 "NotPortedYet이 비었다"를 "규칙을 다 옮겼다"는 근거로 쓸 수 없다.

**원본 EditMode 테스트 대조**

- EditMode `.cs`는 415개, Core 테스트 `.cs`는 79개다.
- 머리 주석에 `// 옮겨 옴: Assets/Tests/EditMode/...`가 있는 Core 테스트는 53개다. 나머지 26개에는 없다.
- Tools·game 어디에서든 파일 이름이 언급된 EditMode 파일은 75개, **언급되지 않은 것은 340개**다.
- 340개를 키워드로 나눈 추정: 에셋·씬 검사가 약 255개, 규칙 테스트가 약 85개다.
- 언급되지 않은 규칙성 테스트의 대표 (괄호는 테스트 수):
  - StudentLifeTimePointTests (17), StudentLifeTraitActivityTests (7)
  - AdultCareerSnapshotTests (14), PlayerLifeAggregateTests (8), AdultCareerCommandCoordinatorTests (6)
  - Save/SaveSlotServiceTests (11), World/WorldTransferLeaseTests (11)
  - InteriorGenerationTests (10), CraftingRecipeTests (5), GameTextCatalogTests (7)
  - Quests/QuestNetworkPlayerIsolationTests
  - 이유가 있어 옮기지 않은 것: Knowledge*, StatusValue*, ToolDefinitionStrategy*
- 설계 문서 85줄은 "옮기지 않은 테스트는 이유와 함께 목록으로 남긴다"고 요구한다. **그런 목록은 없다.**

---

## 3. 데이터 (프로젝트 2)

- 종류는 엔티티의 `"type"` 값으로 셌다. `data/*/*.json` 202개 파일(좌표표와 현지화 제외)에서 엔티티 2289개, 서로 다른 type **189**종이다. `export-report.json`의 `typeCounts`와 같다.

| 분류 | 로더 등록 수 | data에 있는 종류 |
|---|---|---|
| 등록 (`Add`) | 174 | 163 |
| 규칙 아님 (`NotARule`) | 19 | 19 |
| 게임이 쓰지 않음 (`NotUsedByGame`) | 7 | 7 |
| 아직 안 옮김 (`NotPortedYet`) | 0 | 0 |

- 합계 189 = 163+19+7+0이다. 어느 목록에도 없는 종류는 0이다. 테스트 `EveryTypeInData_IsClassified`, `NothingIsStillWaitingToBePorted`가 이를 고정한다.
- 변환기 재현: 미리 빌드된 변환기로 스크래치 폴더에 다시 냈다. `diff -rq` 결과 차이는 `.gitattributes`뿐이었다.

**못 푼 참조 12건** (모두 `guidNotFound`)

| 대상 | 건수 | 종류의 분류 | 처리 |
|---|---|---|---|
| `Furniture_Desk_Basic` / `Desk_Large`의 타일 조각 | 1 + 2 | **규칙(등록)** | 빌더가 그 칸을 버린다(`PlacementBuilders.cs:37`). basic만 테스트로 고정됐다 |
| `TileSetDefinition_NewPaletteOffice` | 7 | 규칙 아님 | 만들지 않는다 |
| `Town2LdtkMapDefinition` | 2 | 규칙 아님 | 만들지 않는다 |

- 로더 설계 문서 141줄의 "12건은 규칙 종류에 걸리지 않는다"는 틀렸다. 3건이 가구에 걸린다.

---

## 4. 월드 (프로젝트 3)

**맵 대조**

- Unity 씬은 13개다. `maps.json`에 10개가 있다: Town, Beach, School, House, ShopInterior, HospitalInterior, PoliceInterior, PoliceShootingInterior, KitchenInterior, Kitchen2Interior.
- 대응이 없는 Unity 씬 Boot, MainMenu, HostLobby는 월드가 아니다. HostLobby는 멀티플레이와 함께 빠져 있다.
- 맵 크기는 LDtk의 `pxWid/pxHei`에서 읽는다(`LdtkFile.cs:174`, `WorldMapLoader.cs:71`).
- 테스트 아닌 코드에서 맵 이름이나 크기 리터럴을 grep하면 주석 1줄만 나온다. 가구 버튼은 `MapFeatures`로 고른다.

**임시 연결 16개**

- 주석을 빼고 JSON을 파싱해 셌다. 단순 `grep`으로 세면 주석 1줄이 섞여 1개 더 나온다.
- `maps.json` 3개: 마을 북쪽 문 → 학교, 그 도착 지점, 학교 아래 가장자리 → 마을.
- `interactions.json` 13개: 마을 북쪽 광장의 일터 네 곳·도서관·가게 방문/활동 12개, 가게 알바 1개.

**알려진 맵 문제 7건** (`MapContractTests.cs:113 KnownMapProblems`)

- 마을 병원 문이 계단 타일에 막혀 닿지 않는다.
- 값이 빈 문이 5개다(마을 2, 경찰서 3).
- `hospital_2` 문의 대상 장면이 없다.

**상호작용 지점 종류** (합계 18)

- `location-activity` 7, `location-visit` 7, `daily-plan-board` 2, `student-day-end` 1, `part-time-work` 1.
- 화면이 듣는 `career-practice`, `exploration`, 시설 지점은 0개다.
- NPC 자리는 House 1, School 1, Town 0이다.

**Unity에 있고 MonoGame에 없는 월드 기능**

| 기능 | MonoGame 판 |
|---|---|
| 가로등 등 국소 조명 (`LocalLightController`, Town에 1개) | **없음.** 화면 전체 색 곱만 한다 |
| 씬별 카메라 경계 상자 (`CameraBounds`) | **일부.** 맵 전체 크기로만 막는다 |
| 바닥 종류 구역 (`SurfaceTagZone`) | 없음 |
| 낚시 애니메이션 | 없음 (`NotUsedByGame`) |
| 낮밤 실내 처리 | 둘 다 실내는 늘 밝다 (같음) |
| 애니메이션 타일, NPC 걷기, 소리 | Unity 판에도 없다 |

**문서끼리 충돌한다.** 설계 문서 6절은 "어긋난 곳은 LDtk 원본을 고친다", `engine-migration.md`는 "LDtk에서 고치지 않는다(맵을 새로 그린다)"라고 한다. 뒤의 것이 나중 결정(11-12)이다.

### 4.4 배포 빌드

- `Tools/publish/content.txt`에 `game/themes`가 없다(grep 결과 `game/world`와 `game/localization`만 있다).
- Host는 테마 폴더가 없으면 `HostOptions.cs:140`에서 예외를 던진다.
- 따라서 **배포 폴더는 시작하지 못할 것으로 본다.** 코드를 읽고 내린 판단이고, 실행하지는 않았다.
- `build-publish` 세션의 "출력 폴더 복사본에서 Host 테스트 59건 통과"는 테마 폴더가 생기기 전의 결과다.
- 테마 PNG는 아직 모두 `repo:Assets/...`(얼린 Unity 폴더)를 가리킨다.

---

## 5. 화면 (프로젝트 4)

**Unity 화면 31개 대조** (빌더·행·설치 코드는 뺐다)

| 상태 | 수 | 화면 |
|---|---|---|
| 옮김 | 25 | 슬롯 선택, 캐릭터 만들기, HUD 시계·생활 수치·추적 목표, 대화, 하루 이벤트, 일과, 장소, 하루 결과, 미니게임, 목표 수첩, 체인 기록, 가구 배치, 장래희망, 진로 후보, 탐색, 가방, 상태, 설정, 도감, 옷장, 마을 기록, 장소 기록, 성인 시설 |
| 일부러 안 옮김 | 6 | ToolUnlockToast, DayNightDebugCycleButton, JoinGamePanel, ModeSelectPanel(`screens-port-design.md:81`), WorldStateUsageLogPanel, LocationStatePanel(`core-rules-extraction-design.md:155`) |
| 빠짐 | 0 | |

- `ScreenCatalog`에는 `r.Add` 28개와 리스너 3개가 있다.
- 옮긴 25개는 모두 Host 시나리오가 있고, `output/host-screens-evidence`에 스크린샷이 있다(D: 190개, 작업 폴더 187개).
- D: 쪽에만 있는 `hud-03-tracked-after-mom*`, `main-chain-14-complete.png`는 지금 테스트가 만들지 않는 낡은 파일이다.
- **실제 게임에서 여는 길이 없는 화면은 5개다.**
  - 탐색, 성인 시설, 진로 연습 미니게임: 테스트가 시험용 `--interactions` 파일로만 연다.
  - 장소 기록: 직접 열기만 있다.
  - 체인 기록: 직접 열기만 있다. Unity 판도 같았다.

**메인 체인 시나리오 (`MainChainHostTests`, 12건)**

- 작성자가 테스트 코드와 `PlayerDriver.cs` 전체를 읽었다.
- **입력**
  - 키와 글자는 `Sdl2Native.PushKey`로 SDL 이벤트 큐에 넣는다.
  - 포인터는 `ScriptedPointer`(게임이 읽는 `IPointerSource`)로 넣는다.
  - `PressButton`은 `Ui.HitTest`로 맨 위 요소를 확인한 뒤 포인터로 누른다.
  - 걷기(`WalkTo`/`WalkThroughDoor`)는 Core `GridPath`로 경로만 정하고, 실제 이동은 방향 키를 누르고 떼서 만든다. 매 프레임 벽에 끼었는지도 센다.
- **세어 본 결과**
  - 테스트 본문의 `Session.` 사용 24종은 모두 조회(Assert나 대상 고르기)다. **`GameSession` 명령 호출은 0이다.**
  - `Screens.Open` 직접 호출, 순간이동, 위치 대입, 리플렉션 상태 설정도 0이다.
  - 옵션은 `--save-root --size 1920x1080 --evidence`뿐이다. `--hour`, `--time-scale`, `--spawn`, `--slot`은 쓰지 않는다.
- **남는 점**
  - 미니게임 답(순서)은 화면 모델 `PreviewSequence`에서 읽는다. 사람이 미리 보기를 보고 외우는 것을 대신하는 것이다.
  - `TestMode`는 고정 프레임 시간을 쓰고 수직 동기를 끈다.
  - 대상을 고를 때 맵 데이터와 화면 모델을 읽는다. 머리 주석에 적힌 그대로다.
  - **판정: 주장대로 실제 입력 경로로 간다. 다만 OS 입력 계층은 거치지 않는다.**
- **다른 Host 테스트는 준비 단계로 상태를 강제로 만든다**(메인 체인과 구분해 둔다).
  - `CareerHostTests`: `ApplyHint`로 진로 힌트를 직접 넣는다.
  - `FacilityHostTests`: 저장된 스냅샷의 날짜와 후보를 손으로 고친다.
  - `RecordsHostTests`: `EndDay/StartNextDay`를 직접 부른다.
  - `--hour`/`--time-scale`은 5개 파일, `--spawn`은 1개 파일이 쓴다.
  - 이것들은 "실제 플레이로 확인"이 아니라 준비 단계 + 실제 입력이다.

**화면이 GameSession을 거치지 않는 곳:** `EncyclopediaScreen.cs:228`이 `Services.Session.Encyclopedia.MarkSeen`을 직접 부르고, 저장하지 않는다. 주석에 "필요한 변경"으로 인정되어 있다.

---

## 6. 게임 흐름 테스트 (프로젝트 6)

- `game/tests/Rootborn.Game.Tests/SCENARIOS.md`(120줄)에 Unity PlayMode 시나리오별 대응표가 있다. Game.Tests는 지금 `[Test]` 86개이고, `TestCase`를 포함해 실행하면 99건이다.
- `testing-discipline.md` id 대응:
  - KNOW-001/002, TOOL-001: 대응 없음. 게임이 쓰지 않는다는 이유가 있다.
  - HEIR-001, NET-001/002: 대응 없음. 옮기지 않았다.
  - STATUS-001: 일부만 있다. StatusValue는 옮기지 않았다.
  - TOWN-001의 "farm/crop 용어 노출 검사"는 없다.
- SCENARIOS.md 3절의 "필요한 변경" 5건은 **조사 커밋에서 모두 그대로였다.** 작성자가 코드로 확인했다.
  1. `GameSession.cs:152`가 모든 플레이어에게 `SaveFileNames.QuestLog` 하나를 쓴다.
  2. `DailyEventRunner.TryChoose`(Core `StudentLife/DailyEventRunner.cs:13-52`)는 요청 id 중복만 본다.
  3. `Discover` 명령이 없다.
  4. `MarkSeen`은 화면이 직접 부른다.
  5. 채집 명령이 없다.
- 이 5건과 이야기 깃발 수정은 **합치지 않은 브랜치 `jdyj/game-needs-2`(main보다 10커밋 앞섬)** 에 있다: `939e88698`, `01f46c616`, `6fc9d6756`, `e3cc5fbf1` 등.

---

## 7. 멀티플레이 (프로젝트 5), Unity 제거 (프로젝트 7)

**멀티플레이**

- main과 조사 커밋에는 `game/Rootborn.Net`이 없고, `game/`에 Steam 코드나 네이티브 라이브러리도 없다.
- `2026-11-12-multiplayer-research.md`(205줄)는 "코드는 쓰지 않았다"고 적고, 사용자가 정할 것 6개를 남긴 조사 문서다.
- 브랜치 `jdyj/net-base`(분기점 `d90f073`)에 합치지 않은 커밋 2개가 있다.
  - `aaa7014d4`: Rootborn.Net. 파일 11개, 1,743줄. 루프백과 UDP(LiteNetLib 1.3.5) 전송, 메시지 표, 접속 세션. Net 테스트 41개.
  - `c6b596cc3`: Core에 남의 몸 사이 메우기와 공용 시간 규칙. 테스트 24개.
  - Steam 전송은 없다.

**Unity 제거**

- `Assets/`, `ProjectSettings/`, `Packages/manifest.json`이 그대로 있다.
- `.github/workflows/ci.yml` 하나만 있고 Unity를 부르지 않는다.
- 태그 `unity-playable-20261111`이 있다(`454ca4a09`).
- 계획 문서 `2026-11-12-unity-removal-plan.md`는 main `d90f073df`에만 있고 조사 커밋에는 없다. 아무것도 지우지 않은 계획 단계다.
- 동결 뒤 `Assets/`에서 바뀐 것은 `Assets/LDtk/House/house.ldtk` 하나다(28x18, 설계 11.7 승인).

---

## 8. 그림·다국어 방침

**그림**

- 화면 코드의 PNG 경로 리터럴: Screens 0, Ui 0, Host 1(수동 스크린샷 파일 이름이라 그림이 아니다).
- 스프라이트 좌표 리터럴: 0.
- 테마 폴더(`game/themes/{urban,style2}`)에는 `theme.json`만 있고 PNG는 모두 `Assets/`를 가리킨다.
- **패널 안 여백은 테마에서 받지 않는다.** 화면이 고정 Padding을 쓰고 `SkinPadding`이 없다. `engine-migration.md`의 "패널 안 여백은 테마의 테두리 두께에서 얻는다"를 지키지 않는다.

**사람이 읽는 문구 리터럴** (주석과 테스트 제외)

- Screens: 한글 22건, 모두 예외 메시지.
- Host: 사용자에게 보이는 한글 38건. `SampleScreen` 16건(`--sample`에서만 보임), 테마 오류 22건. 이 밖에 예외 30건, 로그 14건.
- 영어 문장 리터럴: Screens·Ui·Host·Game·World 모두 0건.
- CI `check-no-hardcoded-text.sh`가 놓치는 것:
  - `game/Rootborn.Screens`만 보고 한글만 찾는다.
  - Ui·Host는 검사 대상이 아니고, 영어 문장은 어디서든 못 잡는다.
  - 지금 실제로 빠져나간 영어 문장은 0건이다.
- 문장을 `+`로 이어 붙인 곳: `SampleScreen.cs:39,86`(`"확인 버튼을 " + n + "번 눌렀습니다."`)만 해당한다. 나머지는 줄바꿈이나 부호 붙이기다.

**규칙을 어기는 곳**

1. `GameScreen.Shown(keyOrText)`(`GameScreen.cs:140`)
   - 키가 없으면 받은 값을 그대로 보인다. 기준 언어로 대신하지도 않고 `MissingKeys`에 남기지도 않는다.
   - 15개 파일에서 36번 부른다.
2. 가구 이름
   - `FurniturePlacementScreen.cs:143`이 데이터의 `displayName`을 그대로 보인다.
   - 1,503개 중 한글은 30개이고, 나머지 1,473개는 `"Bathroom Singles Shadowless 48x48 30"` 같은 그림 시트 이름이다. 이름 키는 0개다.
3. `KnownMissingKeys`
   - `dialogue.firstNpc.greeting` 1개가 있는데, 키마다 이유가 적혀 있지 않다.
   - 메인 체인 스크린샷에서 이 키가 그대로 보인다(`main-chain-play` 보고).

**현지화 파일**

- `game/localization`은 ko 125, en 125, `data/localization`은 ko 1096, en 1096이다. 한쪽에만 있는 키는 0, 두 폴더에 겹치는 키도 0이다.
- data 쪽 키 22개는 키 자체가 영어 문장이다(예: `"Locked"`).
- 맵 이름이나 크기에 기대는 화면 코드는 0이다.

---

## 9. "Unity 판과 달라진 동작" 목록 (설계 9절)

- 항목은 **69줄**이다. 시드 20261112로 10줄을 뽑았다: 15, 16, 27, 28, 32, 49, 57, 65, 66, 69.

| 줄 | 판정 |
|---|---|
| 15 장소 상태 숨김 | 있음·맞음 (`LocationStateTests.ChangedBehavior_HiddenState…`) |
| 16 퀘스트 받기 조건 | 있음·맞음 |
| 27 빈 기록을 세이브에 남기지 않음 | 일부 다름. 월드 상태 쪽 테스트는 `UsedCount==0`만 봐서 기록이 남아도 통과한다 |
| 28 id 순서, 도감 발견 시각 | 일부 다름. 시각 인자 기본값이 0이면 여전히 `DateTime.UtcNow`를 읽는다 |
| 32 맵 밖 막힘, 0.1초 상한, 0.001px | 일부 다름. 출구 칸은 맵 밖이라도 열리고, 0.1초 상한은 단언하지 않는다 |
| 49 기본 언어 ko, Esc로 설정 | 낡음. 67줄이 대신하는데 지워지지 않았다 |
| 57 가방 Shift+방향 키 | 있음·맞음 |
| 65 성인 시설 거절 이유 | 있음·맞음 |
| 66 같은 날 미니게임 예약 이어 하기 | 있음·맞음 |
| 69 낮밤·NPC가 학생 시각을 따름 | 있음·맞음 |

- 결과: 맞음 6, 일부 다름이나 낡음 4, 없음 0.
- 표본 밖에서도 **56줄**("남은 예약은 시작할 때 열지 않는다")과 66줄·실제 코드("같은 날 예약은 연다")가 서로 맞지 않는다.

---

## 10. 어긋난 주장

| 출처 | 주장 | 실제 |
|---|---|---|
| 커밋 `93aa78f3d`, `NotPortedYet.cs` 주석 | 남은 것을 마지막으로 옮겼다 | 데이터 종류 기준으로만 사실이다. 데이터 없는 규칙 코드(이야기 깃발 모음 등)는 검사 밖이고, 그중 하나가 버그다 |
| 로더 설계 문서 74줄 | 분류는 셋 | 코드는 넷이다(`NotUsedByGame`) |
| 로더 설계 문서 141줄 | 못 푼 12건은 규칙 종류에 걸리지 않는다 | 3건이 가구(규칙)에 걸린다 |
| 규칙 옮기기 설계 문서 85줄 | 옮기지 않은 테스트를 이유와 함께 목록으로 남긴다 | 목록이 없다. 언급 없는 EditMode 파일이 340개다 |
| 규칙 옮기기 설계 9절 49줄·56줄 | 언어 기본값, 예약 처리 | 낡았거나 다른 줄과 모순된다 |
| `build-publish` 보고 | 출력 폴더 복사본에서 Host 59건 통과 | 그때는 맞았다. 지금은 `content.txt`에 `game/themes`가 없어 배포 폴더가 시작하지 못할 것으로 본다(4.4) |
| `game-e2e` 보고 | 시간 테스트 1건 실패는 부하 때문이고 마지막에는 통과 | 혼자 돌려도 3번 중 2번 실패한다. 흔들리는 테스트다 |
| `screens-furniture` 보고 | 가구 이름은 데이터의 한국어 이름 | 1,503개 중 1,473개가 영어 그림 시트 이름이다 |
| `screens-records` 보고 | 기록 화면 테스트 48건 | 지금 47개다 |
| `game-needs` 보고 | 현지화 키 1,101개 | 지금 1,221개다(그 뒤 키가 더해짐. 거짓은 아니고 낡음) |
| `screens-wire` 보고·커밋 `020fd0b14` | 화면이 직접 하던 것을 GameSession 명령으로 | 대부분 맞다. `EncyclopediaScreen`의 `MarkSeen` 직접 호출 1곳이 남았다(본인이 주석으로 인정) |
| `rules-cleanup` 보고 | `assets-data.md` 삭제, `data-driven.md` 신설 | 조사 커밋에는 없다. main `8370a48bf`에서 합쳐졌다 |
| `game/README.md` | `SessionCommands`, `Services.Commands.Run(...)` | 그런 클래스가 없다. 화면은 `Services.Session.명령()`을 직접 부른다 |
| `game/README.md` | `--lang`이 없으면 ko | 인자 → 저장된 설정 → OS 언어 → ko 순서다(`LanguageChoice`) |
| `game/README.md` | 미니게임은 `career-practice` 지점에서 E | 그런 지점이 데이터에 없다 |
| `game/README.md` 64줄 | 추적 지정은 메모리에만 있다 | `main-chain-play`에서 저장하도록 고쳤다 |
| `game/world/README.md` 6절 | 가구 방 칸의 Host 연결은 아직 전 | 이미 연결됐다(`HostGame.cs:260-296`) |
| 설계 6절 / `engine-migration.md` | LDtk를 고친다 / 고치지 않는다 | 서로 충돌한다. 나중 것이 우선이다 |
| `game/README.md` | (빠짐) | Tools/publish, 테마 `style2`가 적혀 있지 않다. `Program.cs` 도움말에 `--ui-theme`, `--lang`, `--interactions`, `--hour`, `--time-scale`이 없다 |

세션 보고에서 맞다고 확인한 주요 주장:

- 메인 체인 12건이 실제 입력으로 간다.
- 화면이 쓰는 키가 en·ko에 모두 있다.
- 마을에 시설·탐색 지점이 없다(보고 스스로 인정).
- 장래희망 `linkedQuests` 4건이 비어 있다.
- 월드가 정의 로더에서 받는다.

---

## 11. 빠진 것과 위험 (중요한 순서)

1. **합치지 않은 고침이 main 밖에 있다.** `jdyj/game-needs-2`의 10커밋에 이야기 깃발 `null`(나무 퀘스트 보상 불가), 플레이어별 퀘스트 기록, 하루 이벤트 재선택(보상 두 번 받기), 발견·도감·채집 명령 수정이 들어 있다. 합치기 전까지 main에는 보상·멱등성 원칙을 어기는 결함이 남는다.
2. **배포 폴더가 시작하지 못할 가능성이 크다**(테마 폴더 누락, 코드 판단). 지금 아무도 배포 폴더를 띄워 화면을 본 적이 없다. Host에 자동 종료나 스크린샷 인자가 없어 확인 수단도 없다.
3. **실제 맵에서 갈 수 없는 기능**: 탐색, 성인 시설, 진로 연습, 발견, 장소 기록. 테스트는 시험용 상호작용 파일로 통과하므로, 테스트 통과가 플레이 가능하다는 뜻이 아니다. 성인기 진로는 메인 체인 뒤의 핵심 흐름이다.
4. **현지화 대체 규칙 위반과 가구 이름 1,473개.** 영어든 한국어든 그림 시트 이름이 화면에 나온다. `Shown()`은 빠진 키를 숨긴다.
5. **흔들리는 시간 기준 테스트 2건.** 전체 실행에서 실패하므로 CI(ubuntu)에서도 흔들릴 수 있다. CI가 GitHub에서 한 번도 돈 적이 없어 알 수 없다.
6. **"규칙 다 옮김" 검사의 맹점.** 데이터 없는 규칙 코드와 원본 규칙 테스트 약 85개(추정)는 대응 기록이 없다.
7. **멀티플레이**는 main에 코드가 없다. Steam 전송과 친구 방은 브랜치에도 없다.
8. **월드 빈 곳**: 임시 연결 16개, 알려진 맵 문제 7건, 가로등 조명과 씬별 카메라 경계 없음. 맵은 새로 그리기로 했으므로 임시 연결을 지울 시점이 정해지지 않았다.
9. **테마 그림이 얼린 `Assets/`를 가리킨다.** Unity 제거(프로젝트 7) 전에 옮겨야 한다.
10. **낡은 문서**: game/README, world/README 6절, 설계 9절 49·56줄, 로더 설계 74·141줄. 다음 세션이 이것을 믿으면 잘못 작업한다.

---

## 12. 출시까지 남은 일 (작성자 판단)

1. `jdyj/game-needs-2`와 `jdyj/net-base`를 검토해 합치기.
2. 배포: `content.txt`에 `game/themes` 넣기, Host에 `--frames N --screenshot` 추가, 배포 폴더를 띄워 스크린샷 보기(Windows·Linux).
3. 실제 맵에 탐색·시설·진로 연습·발견 지점 놓기. 맵을 새로 그리면 그 맵에. 장소 기록 여는 길 만들기.
4. 현지화: `Shown()`을 기준 언어로 대신하고 빠진 키를 기록하게, 가구 이름 키, `KnownMissingKeys`에 이유 적기, CI 문구 검사를 Ui·Host와 영어로 넓히기.
5. 흔들리는 시간 테스트 고치기(기준 넓히기 또는 반복 측정). GitHub에서 CI 한 번 돌리기. xvfb Host 테스트 확인.
6. 프로젝트 1 마무리: ModernSociety·VerticalSlice·WaitOnSurfaceStep 결론 적기, 옮기지 않은 EditMode 테스트 목록, StudentLifeTimePoint·AdultCareerSnapshot·SaveSlotService 등 규칙 테스트 대조.
7. 멀티플레이: 사용자 결정 6개 → Steam 전송 → 친구 방 → 2인 실제 확인(HostLobby 화면 포함).
8. 월드: 새 맵, 임시 연결 제거, 가로등 조명, 카메라 경계, 맵 문제 7건 해소.
9. 사람이 실제 키보드·마우스로 처음부터 끝까지 한 번 플레이하기. OS 입력, 키보드만 쓰는 조작, 다른 해상도 포함.
10. 테마 PNG를 `Assets/` 밖으로 옮긴 뒤 Unity 제거 계획(8단계) 실행. 문서 정리.
11. 그림 전면 교체(사용자 계획). 이것은 이전 작업과 별개다.

---

## 13. 확인되지 않은 것 (자동 테스트가 어디까지인지)

| 항목 | 상태 |
|---|---|
| 규칙·데이터·흐름 | 자동 테스트(dotnet). 화면 근거 아님 |
| 화면을 거친 플레이 | Host 테스트 96건. 장치 이벤트 주입이고 **OS 입력 계층은 미확인** |
| OS 입력(실제 키보드·마우스) | MonoGame 판 게임으로는 **아무도 하지 않았다.** 스파이크에서만 했다(FEVM xdotool, 한글 조합은 사용자가 확인) |
| 사람이 실제로 해 본 플레이 | 기록 없음 |
| Linux 실행 | 게임 판으로는 안 해 봤다. 스파이크만 FEVM에서 했다 |
| 해상도 | 테스트는 1920x1080(12곳), 1280x720(5곳)만 쓴다. 21:9, 4K, 창 크기 바꾸기는 미확인 |
| GitHub CI | 한 번도 돌지 않았다. Host job은 `continue-on-error: true`라 실패해도 막지 않는다 |
| Steam | 스파이크의 독립 실행판으로 로비 만들기만 했다. 게임 판에는 없다 |
| 배포 폴더 화면 | 아무도 보지 않았다. 지금은 시작하지 못할 것으로 추정 |
| 기존 Unity 세이브 읽기 | `UnitySaveTests`, `OlderSlotTests`의 고정용 세이브로만 확인. 사용자 실제 세이브는 읽지 않았다(지시) |
| 스크린샷 | 테스트가 187~190장을 만든다. 이번 조사에서 사람 눈으로 다시 보지는 않았다 |

## 14. 이 조사가 확인하지 못한 것

- 스파이크(프로젝트 0) 결과는 다시 돌리지 않았다.
- 배포 폴더는 실제로 만들어 띄우지 않았다(4.4는 코드 판단).
- 영역 대응은 형식 이름이 같은지로만 봤다. 이름이 바뀐 클래스는 "없음"으로 셌을 수 있다. 규칙 테스트 85개와 에셋 검사 255개는 키워드로 나눈 추정이다.
- 스크린샷 내용(겹친 글자, 잘린 패널)은 열어 보지 않았다.
- `jdyj/game-needs-2`, `jdyj/net-base`의 테스트는 돌리지 않았다.
- main `8370a48bf`, `d90f073df`의 내용은 문서 존재만 봤다.

## 15. 저장소 상태 (조사 시점)

- 작업 폴더 7개(`git worktree list`):
  - `D:/User/farmer` [main `d90f073df`]
  - `progress-audit` [`0f1b5c2f5`]
  - `screens-polish` [`0f1b5c2f5`, main보다 2 뒤]
  - `game-needs-2` [`a60fc772b`, main보다 10 앞, 2 뒤]
  - `net-base` [`306e1ba1f`, 작업 폴더 HEAD 기준. 브랜치 커밋은 위 7절]
  - `C:/Users/jdyj/farmer/.worktrees/house-upgrade-interior-construction` [2026-05-17, 1 앞, 539 뒤]
  - `D:/User/farmer-dev` [`dev/no-unity-20261106`, 247 뒤]
- 조사 도중 `rules-cleanup`과 `unity-removal-plan` 작업 폴더와 브랜치가 사라졌다. main에 합쳐진 것으로 보인다.
- 로컬 브랜치 `town-playable-baseline-recovery`가 남아 있다(310 뒤).
- 푸시되지 않은 커밋: main은 origin/main과 같다(앞 0, 뒤 0).
- 사용자의 커밋하지 않은 변경(`D:/User/farmer`, 21줄, 손대지 않음):
  - 수정: `.claude/constitution.md`, `.claude/rules/changelog.md`, `AGENTS.md`, `Assets/AddressableAssetsData/Windows/addressables_content_state.bin`, `Assets/Gif/.../SuperRetro_LegacytilePalette.prefab`, `docs/art/art-requests.md`
  - 새 파일: `.claude/rules/strix-verification.md`, `Assets/InitTestScene*.unity`(+meta) 4쌍, `docs/story/rootborn-*.md` 3개, `docs/superpowers/audits/2026-11-04-full-design-status.md`
  - 이름이 이상한 파일 `-`와 `x.path)')`가 있다. 셸 명령 실수로 생긴 것으로 보인다(추정).
