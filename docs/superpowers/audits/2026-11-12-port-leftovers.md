# 옮기지 않은 것의 목록 — 프로젝트 1(규칙 옮기기) 마무리 근거 (2026-11-12)

- **조사한 커밋: `4f72c7bae`** (브랜치 `jdyj/port-leftovers`). 보고 전에 main `95c080c51`(화면 결함 정리) 위로 옮겼고, 이 문서가 인용한 game/ 줄 번호를 그 위에서 다시 확인했다. 코드는 고치지 않았다. 고친 것은 `Tools/unity-removal/list.sh` 한 곳뿐이다(5절).
- 목적: Unity 판(`Assets/`)을 지우기 전에(`docs/superpowers/specs/2026-11-12-unity-removal-plan.md`) 무엇을 일부러 버리는지 근거를 남긴다. 전수조사(`docs/superpowers/audits/2026-11-12-migration-status.md` 2절)가 "데이터가 없는 규칙 코드와 옮기지 않은 EditMode 테스트의 목록이 없다"고 지적한 빈 곳을 채운다.
- 근거의 종류: 이 문서의 모든 판정은 **코드·에셋을 grep하고 읽은 것**이다. 테스트를 돌려 확인한 것이 아니고, 화면을 본 것도 아니다. 숫자는 모두 직접 셌고 세는 방법을 6절에 적었다. 자동 분류만 한 줄에는 "(분류 규칙)"을, 내용 대조 없이 이름만 맞춘 줄에는 "추정"을 붙였다.

---

## 0. 요약

| 무엇 | 수 | 결론 |
|---|---|---|
| `Assets/Scripts/Game` 파일 | 459 | 옮김 320(같은 이름 227, 데이터 종류 54, 이름 다름 39) · 데이터 종류 분류에 이유가 있음 25 · Unity 컴포넌트 52 · 다른 것으로 대체 16 · 게임이 쓰지 않음 12 · 쓰이지 않음 10 · 규칙 아님 14 · Unity 전용 8 · 데이터 없음 1 · 빈 파일 1. **이유가 없는 파일 0** |
| `Assets/Tests/EditMode` 파일 | 415 | 대응 있음 77 · 대응 일부 9 · 다른 것으로 대체 1 · 옮겨야 할 것 0(5개를 2026-11-12에 옮김, 아래) · 옮기지 않음(작성자 판정) 16 · 옮기지 않음(분류 규칙) 218 · 대응 추정(분류 규칙) 94 |
| `Assets/Tests/PlayMode` 파일 | 76 | `SCENARIOS.md`에 있음 68 · 도우미 파일(테스트 0) 7 · 없음 1(테스트 도우미 계약 테스트, Unity 전용) |
| Unity에 있고 MonoGame에 없는 런타임 동작 | 13 + 안 쓰이던 것 4 | 문 열림·구역 페이드 연출, 카메라 부드러운 추적, 마우스 클릭 행동, 상호작용 실패 안내가 크다(3절) |

**전수조사가 걱정한 세 가지의 결론**

- **ModernSociety(5파일)**: Unity 안에서도 쓰이지 않는다. 폴더 밖 참조는 자기 테스트(`ModernSocietyLoopTests`)뿐이고, 씬·프리팹·`.asset`이 0, `data/`도 0이다. 버린다.
- **VerticalSlice(2파일)**: Unity 안에서도 쓰이지 않는다. UI에 받는 자리(`ObjectiveJournalPanel.SetVerticalSliceSummary`, `StudentDayResultPanel.AppendVerticalSliceGuide`)는 있지만 부르는 곳이 0이다. 문구도 영어로 코드에 적혀 있다. 버린다.
- **이야기 깃발(Story)**: 전수조사 4.1의 `null` 버그는 main에서 이미 고쳐졌다. `GameSession.cs:122-125`가 Unity `new StoryFlagSet()`처럼 저장하지 않는 메모리 묶음(`UnsavedStoryFlags`)을 넘긴다.
- 그 밖에 전수조사가 "이유 없음"으로 짚은 것도 판정했다: `WaitOnSurfaceStep`(데이터 0, Unity 안에서 테스트 말고 부르는 곳 0), `LifeChoiceDefinition`·`Trait/SkillThresholdActivityRequirement`(Core에 다른 이름으로 옮겨져 있고 data/에 쓰는 곳이 0이라 데이터 종류로만 등록하지 않음), `CareerPracticeBoard`·`LifeActivityBoard`(파일 이름과 다른 MonoBehaviour라 씬에 붙일 수 없고 `AddComponent`하는 곳도 0 — Unity에서도 안 쓰임), `StudentLifeDeltaSnapshot`(`StudentLifeCore` 안의 private 형).

### 옮겨야 할 것 — 옮김 (2026-11-12, 브랜치 `jdyj/missing-tests`)

조사 때 "옮겨야 할 것"이던 5개의 빠진 경우를 dotnet 테스트로 옮겼다. 근거는 모두 **자동 테스트**다(화면 근거 아님).
테스트를 먼저 쓰고 돌려서, 지금 코드가 지키는지 보았다. 3·4·5에서 **잃은 보호**가 나와 `GameSession`을 고쳤다.

| # | Unity 파일 | 옮긴 곳 | 결과 |
|---|---|---|---|
| 1 | `StudentLife/StudentLifeTimePointTests` | Core `StudentLifeTimePointBudgetTests` 13건 | 처음부터 통과. 세 번째 유료 행동 무변경 거절, 같은 요청은 포인트를 더 쓰지 않음, 학교는 평일 하루 한 번 무료이고 학교 밖 성장을 막지 않음(다시 읽은 뒤에도 한 번), 주말 3회, 세이브 왕복·달력 다시 묶기로 예산이 차지 않음, 정산한 날 유료·무료·정책 없음 모두 거절, 시간 필드 없는 옛 세이브의 예산, 장소 활동·학교 밖·도움·알바가 한 예산을 씀. 게임 진행에서의 "다시 시작해도 예산이 차지 않음"은 이미 Game `SelfStudyAndClassTests.FreePath004`가 지킨다 |
| 2 | `Save/SaveSlotServiceTests` | Data `SaveSlotMetadataFileTests` 3건 | 처음부터 통과. 씨앗·몸 부위·방향, 겉모습 부위 id·만들기 화면 스냅샷 왕복, 겉모습 없는 Unity 슬롯 정보 읽기. 읽을 수 없는 슬롯 정보 위의 새 이야기가 남은 파일을 지우는 것과 읽히는 이야기를 덮지 않는 것은 Screens `CharacterCreatorScreenTests`(`NewStoryInAnUnreadableSlot_…`, `ASlotThatGotAStoryMeanwhile_…`)에 이미 있었다(조사 때 못 찾음). 4절 2번 |
| 3 | `Careers/AdultCareerSnapshotTests` | Game `SnapshotOwnerTests` | **실패 → 고침.** 다른 플레이어 id가 든 스냅샷을 거절하지 않고 학생 id를 지금 플레이어로 덮어 읽었다(성인·월드는 다른 id를 그대로 둠). 데이터에 없는 아이템은 빼고 읽어 다음 저장이 지웠다. 장비 선택은 저장할 때마다 빈 값으로 덮였다 |
| 4 | `Careers/PlayerLifeAggregateTests` | Game `SnapshotOwnerTests` | **실패 → 고침.** 체인·가구 해금의 다른 플레이어 id를 지금 플레이어로 덮어 읽었다. 버전 1 스냅샷(체인·가구 없음)을 이 플레이어의 빈 상태로 읽는 것은 처음부터 통과 |
| 5 | `StudentLife/DailyPlanSchedulePersistenceTests` | Game `SnapshotOwnerTests` | Core 규칙(`DailyPlanProgress.FromSaveData`가 다른 플레이어 일과를 버림)은 이미 Core `DailyPlanRouteTests.WrongEmbeddedIdentity_…`·`StudentNextDay_ArchivesOwnedScheduleBeforeSnapshotCapture`에 있었다(조사 때 못 찾음). 그러나 **`GameSession.Load`가 읽기 전에 일과·진로 후보 id를 지금 플레이어로 덮어 그 보호를 건너뛰었다 → 고침** |

고친 것(`game/Rootborn.Game/GameSession.SnapshotOwner.cs` 새 파일, `GameSession.cs`·`GameSession.StartReads.cs` 몇 줄): 스냅샷의 성인·학생·일과·진로 후보·월드·체인·가구 해금 중 하나라도 비어 있지 않은 다른 플레이어 id를 가졌거나, 가방·장비에 데이터에 없는 아이템이 있으면 `SaveSlotDamagedException`(문제 종류 `Unreadable`)으로 시작을 거절한다. 슬롯 파일은 바꾸지 않는다. 빈 id는 이 플레이어로 읽고, 슬롯 이름이 다른 것은 지금처럼 복사한 슬롯으로 읽는다. 장비 선택은 읽은 값을 그대로 다시 쓴다.

**"대응 추정"에서 더 열어 본 것**(세이브·보상·플레이어 구분 우선): `Interiors/QuestFurniturePlacementEventTests` → Core `FurniturePlacementEventTests` 3건(대응 있음), `StudentLife/QuestGameplayEventAdapterTests` → Game `RefusedActionQuestTests` 4건(대응 일부 — 거절·중복은 옮겼고, 성공 때 정확한 이벤트는 메인 체인 테스트가 간접으로만 본다), `StudentLife/OutsideSchoolInteractorRequestPersistenceTests` → Core `OutsideSchoolActivityTests`에 같은 이름의 테스트 4개와 Game `FreePath004`(대응 있음). 셋 다 처음부터 통과.
테스트 이름만 보고 판정을 바꾸지 않은 것: `Careers/AdultCareerRuntimeTests`(소유자·라우터·관찰자), `StudentLife/QuestChainOwnerLeaseTests`, `StudentLife/QuestChainSessionEventTests`, `World/AuthoredHousePlacementOwnerTests`, `World/WorldTravelStudentRestoreTests` — Unity 컴포넌트 여럿이 한 세이브를 나눠 쓰던 소유권 문제라 `GameSession` 하나가 모든 영역을 갖고 명령마다 묶음 저장하는 MonoGame 판에는 같은 위험이 없어 보인다(Game `PartialWriteTests`·`TwoPlayersInterleavedTests`·`CorruptSaveStartTests`·`SnapshotOwnerTests`). 내용 대조는 하지 않았다.

**아직 판정하지 못한 것**: "대응 추정(분류 규칙)" 94개. 대부분 Unity 컴포넌트·UI를 거치는 테스트다. 부록 B에서 판정 열이 "대응 추정"인 줄.

---

## 1. 규칙 클래스 (`Assets/Scripts/Game` 459파일)

### 1.1 영역별 요약

판정 뜻:

- **같은 이름으로 옮김**: 파일이 선언한 형 이름이 `Packages/com.rootborn.core/Runtime`이나 `game/`(테스트 제외)에 같은 이름으로 있다. 이름 대조라서 내용이 같은지는 보지 않았다.
- **데이터 종류로 옮김**: 형 이름이 `data/`의 `type`이고 `game/Rootborn.Data/Builders`의 `b.Add("…")`로 Core 형을 만든다(Unity `LocationDefinition` → Core `Location` 같은 것).
- **옮김(이름 다름)**: 작성자가 열어서 Core·game의 대응 형을 찾은 것.
- **NotRules/NotUsedByGame**: 데이터 종류이고 `NotRules.cs`·`NotUsedByGame.cs`에 이유가 적혀 있다.
- **Unity 컴포넌트**: `MonoBehaviour`. 씬·조작·그리기 연결이다. 규칙은 Core에, 연결은 World·Host·Screens에 다시 만들었다. 런타임 동작은 3절.
- **다른 것으로 대체**: Unity 구조(데이터 공급자, 명령 조정자, 입력 잠금 등)를 MonoGame 판의 다른 구조가 맡는다.
- **게임이 쓰지 않음**: 규칙이지만 지금 게임이 부르지 않는다(지식·도구·세대·레거시 상태·레시피 단계). `NotUsedByGame.cs`의 같은 묶음.
- **쓰이지 않음**: Unity 판 안에서도 그 파일 밖 참조가 자기 테스트뿐이거나 0이고, 씬·프리팹·`.asset` 0이다.
- **규칙 아님**: 그림 주소, 겉모습, 맵 생성.

| 영역 | 파일 | 같은 이름으로 옮김 | 데이터 종류로 옮김 | 옮김(이름 다름) | NotRules/NotUsedByGame | Unity 컴포넌트 | 다른 것으로 대체 | 게임이 쓰지 않음 | 쓰이지 않음 | 규칙 아님 | Unity 전용 | 데이터 없음 | 빈 파일 | 확인 안 함 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Bootstrap | 8 |  |  |  |  | 4 |  |  | 2 |  | 2 |  |  |  |
| Careers | 18 | 11 | 3 | 2 | 1 |  | 1 |  |  |  |  |  |  |  |
| Characters | 19 | 1 | 3 | 4 | 2 | 4 | 1 |  |  | 2 | 2 |  |  |  |
| Common | 20 | 4 |  |  |  | 1 | 5 |  | 1 | 7 | 2 |  |  |  |
| Crafting | 5 | 2 |  |  | 1 |  |  | 1 |  |  |  | 1 |  |  |
| Dialogue | 12 | 9 | 1 |  |  | 2 |  |  |  |  |  |  |  |  |
| DiscoveryClues | 31 | 21 | 4 | 4 |  |  | 2 |  |  |  |  |  |  |  |
| Encyclopedia | 4 | 3 | 1 |  |  |  |  |  |  |  |  |  |  |  |
| Family | 9 |  |  | 1 | 3 | 3 |  |  |  | 2 |  |  |  |  |
| Heir | 3 |  |  |  | 1 |  |  | 2 |  |  |  |  |  |  |
| Housing | 1 | 1 |  |  |  |  |  |  |  |  |  |  |  |  |
| Interiors | 5 |  |  |  | 2 | 2 | 1 |  |  |  |  |  |  |  |
| Knowledge | 6 |  |  |  | 2 |  |  | 4 |  |  |  |  |  |  |
| Lighting | 3 | 1 |  |  |  | 2 |  |  |  |  |  |  |  |  |
| Lineage | 2 |  |  |  |  |  |  | 2 |  |  |  |  |  |  |
| Localization | 2 | 2 |  |  |  |  |  |  |  |  |  |  |  |  |
| Managers | 3 |  |  |  |  | 1 |  |  |  |  | 2 |  |  |  |
| Minigames | 10 | 8 | 1 | 1 |  |  |  |  |  |  |  |  |  |  |
| ModernSociety | 5 |  |  |  |  |  |  |  | 5 |  |  |  |  |  |
| Placement | 11 | 6 |  | 2 |  | 2 | 1 |  |  |  |  |  |  |  |
| Player | 10 | 3 |  | 1 |  | 6 |  |  |  |  |  |  |  |  |
| Quests | 46 | 40 |  | 6 |  |  |  |  |  |  |  |  |  |  |
| Resources | 3 | 2 |  |  |  | 1 |  |  |  |  |  |  |  |  |
| Save | 2 | 1 |  | 1 |  |  |  |  |  |  |  |  |  |  |
| Status | 3 |  |  |  | 1 | 1 |  | 1 |  |  |  |  |  |  |
| Story | 2 |  |  | 1 | 1 |  |  |  |  |  |  |  |  |  |
| StudentLife | 163 | 97 | 35 | 13 | 3 | 11 | 3 |  |  |  |  |  | 1 |  |
| Tiles | 1 |  |  |  | 1 |  |  |  |  |  |  |  |  |  |
| Time | 3 |  | 1 |  |  | 2 |  |  |  |  |  |  |  |  |
| Tools | 4 |  |  |  | 2 |  |  | 2 |  |  |  |  |  |  |
| VerticalSlice | 2 |  |  |  |  |  |  |  | 2 |  |  |  |  |  |
| World | 14 | 2 | 2 |  | 1 | 9 |  |  |  |  |  |  |  |  |
| WorldGeneration | 7 |  |  |  | 4 |  |  |  |  | 3 |  |  |  |  |
| WorldState | 22 | 13 | 3 | 3 |  | 1 | 2 |  |  |  |  |  |  |  |
| **합계** | **459** | **227** | **54** | **39** | **25** | **52** | **16** | **12** | **10** | **14** | **8** | **1** | **1** | **0** |

파일별 표는 부록 A.

### 1.2 Network·UI·Editor 폴더

| 폴더 | 파일 | 판정 | 근거 |
|---|---|---|---|
| `Assets/Scripts/Network` | 21 | 옮기지 않음. 프로젝트 5에서 새로 만든다 | Unity Netcode·Steam 세션. Core로 옮긴 규칙은 `Runtime/Network`의 `SessionCapacity`, `SharedWorldTime`, `SteamSessionRules`, `SpawnSpread`, `PoseInterpolation`, `WorldTimeHours`. `game/Rootborn.Net`은 별도 바탕(Messages·Session·Transport). 같은 이름으로 옮겨진 형은 `NetworkWorldTimeState.cs`의 2개뿐 |
| `Assets/Scripts/UI` | 110 | 화면 옮기기(프로젝트 4)에서 대조됨 | Unity 화면 31개 중 25개 옮김, 6개는 이유를 적고 뺌(전수조사 0절, `docs/superpowers/specs/2026-11-12-screens-port-design.md`). 규칙이 든 것은 없다고 보았다(화면이 `GameSession` 명령만 부르게 옮겼으므로). 월드에 직접 그리는 두 가지는 3절 |
| `Assets/Scripts/Editor` | 29 | Unity 전용 | 편집기 메뉴·임포터. 데이터 변환은 `Tools/Rootborn.DataExport`가 맡는다 |

---

## 2. 테스트 (`Assets/Tests`)

### 2.1 EditMode (415파일)

판정을 정한 순서(위에서 먼저 맞는 것):

1. 작성자가 열어 보고 정한 것 — 33파일.
2. dotnet 테스트 파일 머리에 `옮겨 옴: Assets/Tests/EditMode/…`로 그 파일 경로가 적혀 있음 → 대응 있음.
3. 같은 이름의 테스트 클래스가 `Tools/Rootborn.Core.Tests`·`game/tests`에 있음 → 대응 있음.
4. 그 파일의 시나리오 id(`CLUE_INTERPRET_EDIT_009` 같은 것)가 dotnet 테스트에도 있음 → 전부면 대응 있음, 일부면 대응 일부.
5. Netcode를 씀 / Unity 소스 문자열을 읽음 / 에셋·씬·주소를 읽음 / Addressables·`Managers`를 씀 → 옮기지 않음(분류 규칙).
6. 옮긴 규칙 형을 하나도 부르지 않음 → 옮기지 않음(분류 규칙).
7. 그 밖 → 대응 추정(분류 규칙).

| 판정 | 파일 수 |
|---|---|
| 옮기지 않음(분류 규칙) | 218 |
| 대응 추정(분류 규칙) | 94 |
| 대응 있음 | 77 |
| 옮기지 않음 | 16 |
| 대응 있음(일부) | 9 |
| 옮겨야 할 것(부분) | 0 (5개를 옮김, 0절) |
| 다른 것으로 대체 | 1 |

- 5단계의 "에셋·씬·주소 검사"는 Unity 에셋 연결(SO 와이어링, Addressables 주소, 스프라이트 자르기, 씬 구성)을 지키던 것이다. MonoGame 판은 그런 연결이 없고, 데이터 쪽은 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`: 그림·맵 파일, 엔티티 참조, 문구 키)가 맡는다.
- 6단계의 상당수는 Unity UI 부품(패널 배치, 9-slice, 스크롤, 글자 크기) 테스트다. MonoGame 판 화면은 Screens·Host 테스트가 새로 지킨다(전수조사 1절: Screens 301건, Host).
- 파일별 표는 부록 B.

### 2.2 PlayMode (76파일)

- 대응은 `game/tests/Rootborn.Game.Tests/SCENARIOS.md` 1절이 기준이다. 여기에 다시 쓰지 않는다.
- 파일마다 그 안의 시나리오 id나 묶음 행(예: "Interiors/* (37건)")이 `SCENARIOS.md`에 있는지 확인했다. 68파일 있음, 테스트가 없는 도우미 파일 7개.
- **`SCENARIOS.md`에 없는 것은 1파일**: `EndToEnd/Support/E2EDriverContractTests.cs`(5건). Unity PlayMode 테스트 도우미(`PlayerFlowE2EDriver`)가 버튼을 제대로 찾는지 보는 계약 테스트다. 게임 규칙과 무관하고, MonoGame 판은 Host `PlayerDriver`가 같은 역할이다. `SCENARIOS.md` "그 밖" 표에 한 줄 더하면 된다(필요한 변경 1).
- `[Test]`·`[UnityTest]` 표시 수: 168(정규식이 `[TestCase]`까지 센 수. `git grep -cE '\[(Test|UnityTest)\b'`로는 167). `SCENARIOS.md`의 "Interiors/* 37건"은 `PlayMode/Interiors` 아래 테스트 수 37과 같다.
- 파일별 표는 부록 C.

---

## 3. Unity 판에만 있는 런타임 동작 (규칙이 아닌 것)

`Assets/Scripts/Game`의 규칙 아닌 MonoBehaviour와 `UI/Lighting`, `UI/WorldState`를 보았다. "쓰임"은 스크립트 `.cs.meta` guid를 `*.unity`·`*.prefab`에서 찾은 결과와 `AddComponent<>` 호출 위치다. 영향이 큰 순이다. 일부는 읽기 전용 조사 도우미가 찾았고, 작성자가 1·3·5·8번의 핵심 근거(game/에 `OpeningDoor` 읽는 곳 0, `CameraFollow`의 `SmoothDamp`와 `WorldScreen.cs:272` 바로 맞춤, `ui.prompt.no-time` 0건, LDtk `ysort` 층 수)를 다시 grep했다. "추정"은 추정이다.

| # | 동작 | Unity 위치 | Unity 판에서 쓰임 | MonoGame 판 | 플레이에 주는 영향 | 옮기는 데 필요한 것 |
|---|---|---|---|---|---|---|
| 1 | 자동문 열림 연출(문 그림이 0.25→1 폭으로 넓어지고 알파 0.55→1, 그 뒤 이동) | `Game/World/TownAutoDoorPortal.cs` | Town 씬 5곳과 실내 6씬. `InteriorDoorPortalRuntimeInstaller`가 AddComponent | 일부: 기다리는 시간만(`Runtime/World/Doors.cs:134`, `maps.json` `doorOpenSeconds`). Core `WorldSession.OpeningDoor`를 game/에서 읽는 곳 0 | 문에 들어서고 0.35~0.8초 동안 아무 표시 없이 멈춘 듯 보임 | WorldScreen이 열리는 문과 경과 비율을 받아 문 위에 넓어지는 사각형을 그림(경과 비율 조회 필요) |
| 2 | 이동 구역 페이드(검정 82% 덮개 0.25초, 0.75초 재진입 금지) | `Game/World/TownLevelTransitionTrigger.cs` | Town 1, Beach 1 | 일부: 0.25초 지연만(`WorldMapLoader.cs:107`). 재진입은 "나갔다 다시 들어와야 반응"으로 대신 | 마을↔해변 전환이 페이드 없이 툭 바뀜 | 지연 동안 화면 전체 검정 덮개 그리기 |
| 3 | 카메라 부드러운 추적(SmoothDamp 0.15초) | `Game/Player/CameraFollow.cs` | 실내 5씬, 마을은 설치기가 AddComponent | 다름: 매 프레임 바로 맞춤(`Host/World/WorldScreen.cs:272`). 맵 끝 고정·정수 배율은 있음 | 카메라가 딱딱하게 붙어 움직임 | 카메라 위치 상태와 SmoothDamp를 WorldScreen에, 맵에 들어설 때는 바로 맞춤 |
| 4 | 마우스 왼클릭 행동(0.3초 행동 칸, 마우스 쪽 바라보기, E와 같은 상호작용) | `Game/Player/PlayerController.cs`, `Game/Characters/Growing/GrowingCharacterVisualView.cs` | Player 프리팹, 캐릭터 그림 프리팹 | 없음: 걷기·서기만(`World/CharacterArt.cs:192-222`), 월드 클릭 처리 없음 | 클릭해도 반응이 없고 마우스로 상호작용할 수 없음 | 행동 타이머(열 3~4) + Host 월드 클릭 → 방향·행동·상호작용 |
| 5 | 상호작용 실패 안내(이유를 2.5초, 예: `ui.prompt.no-time`) | `Game/Player/PlayerInteractionRouter.cs:89-93` | Town 씬(학교 밖 활동·알바·도움) | 없음: 그 키가 game/에 0건. 활동 화면 안의 거절 이유는 있음(`Screens/Day/LocationModel.cs:103`) | 시간이 없을 때 알바·도움 지점을 눌러도 아무 말이 없음 | 안내 화면에 실패 문구 상태, 상호작용 결과에 실패 이유 |
| 6 | 안내 글자를 화면 안에 맞추고 HUD 피하기 | `PlayerInteractionRouter.cs:136-200`, `Game/Player/InteractionPromptScreenReservations.cs` | 씬 4개 + 마을 설치기 | 없음: 대상 위에 그대로(`Screens/Hud/InteractionPromptScreen.cs:117-118`) | 화면 가장자리·HUD 아래 대상이면 글자가 잘리거나 가려짐 | 글자 상자를 화면·HUD 밖으로 옮기는 배치 |
| 7 | 대상별 안내 글자 색 | `UI/StudentLife/DailyPlanBoardInteractor.cs:22` | School 씬 1곳 | 다름: 색 하나(`InteractionPromptScreen.cs:41`) | 작음 | 안내 사양에 색 하나 |
| 8 | 학교 책상·의자·칠판의 앞뒤 가림(y-sort) | `Game/Interiors/GroundDepthSort.cs` | School 씬 13개(마을 32개는 그림 없는 지점) | 일부: 장치는 있음(`World/MapPicture.cs:23`). LDtk `ysort` 층이 house 1, interiors 0, town 0 | 학교에서 캐릭터가 책상 뒤로 가려지지 않음(추정) | 맵 데이터 작업(학교 가구 층에 `ysort`). 맵은 새로 그릴 예정 |
| 9 | 마을 북쪽 구역 그림과 그 카메라 경계 | Town 씬 `CameraBounds`, 씬에서 칠한 땅 | Town 씬 | 없음: LDtk Level_0만. `interactions.json` 머리에 "북쪽 구역 아직 없음" | 학교 입구·시설이 있던 구역이 없어 임시 연결로 대신 | 맵 작업(새 맵) |
| 10 | 가로등 지역 조명(주황 점광원, 반경 4, 낮 0.1 / 저녁 0.55 / 밤 0.85) | `Game/Lighting/LocalLightController.cs` | Town 씬 1개뿐, 자리가 마을 왼쪽 위 모서리 | 없음: 전역 낮밤 색조만(`World/DayNightLighting.cs:95`) | 작음(자리로 보아, 추정) | 점광원 목록 데이터 + 가산 혼합 빛 그림 |
| 11 | 월드 상태 변화 표시물(바뀐 장소에 노란 사각형과 글자) | `UI/WorldState/WorldStateLogRuntimeInstaller.cs:275-303` | 씬에 없고 코드로 모든 플레이 씬에 생성. 깃발이 켜지고 장소가 있을 때만 | 없음: 기록 화면만(`Screens/Records/WorldStateLogScreen.cs`) | 바뀐 장소가 월드에 표시되지 않음 | 켜진 깃발 → 장소 좌표 → WorldScreen에 그리기 |
| 12 | 낮밤 디버그 버튼 | `UI/Lighting/DayNightDebugCycleButton.cs` | Town 씬. 시간 HUD가 있으면 숨으므로 실제 플레이에서는 안 보였을 것(추정) | 없음. `HostOptions.StartHour`로 대신 | 없음 | 필요 없음 |
| 13 | 나무 자원 지점 3개(치면 꺼짐, 보이지 않는 판정 상자) | `Game/Resources/ResourceNode.cs`, `Game/Player/GatherInteractor.cs:162` | Town 씬 3개 | 없음: `interactions.json`에 자원 종류 없음. 명령 `GameSession.GatherResource`는 있음 | 나무 퀘스트를 실제 맵에서 끝낼 수 없음(전수조사 0절) | 상호작용 종류 하나 + 지점 데이터 |

**Unity 판에서도 쓰이지 않던 것**: 도구 휘두르기 그림(도구를 장착할 길이 없음), 낚시 연출(낚싯대를 장착할 수 없음), 부위 조립 캐릭터와 Pixelwood·NPC 그림 어댑터(씬·프리팹 0, AddComponent 0).

**Unity 판에도 없던 것**: 소리, 파티클, 그림자, 커서 모양, 타일 애니메이션, NPC 걷기, 말풍선, 이름표. 씬·프리팹에 AudioSource(`!u!82`)·ParticleSystem(`!u!198`)이 0건이다.

**이미 있는 것(목록에서 뺌)**: 낮밤 색조(`World/DayNightLighting.cs`), 걷기 속도·대각선·방향(`Runtime/World/Walker.cs:61-80`), 걷기 칸 8fps(`CharacterArt.cs`), 정수 배율 화면 맞춤(`WorldRules.cs:82`), 캐릭터·NPC·집 가구 y-sort(`WorldScreen.cs:280-290`), 기본 안내 "[E] …", 가구 배치 카메라(`Screens/Furniture/PlacementCamera.cs`).

---

## 4. 필요한 변경 (이 작업 범위 밖이라 고치지 않음)

1. `game/tests/Rootborn.Game.Tests/SCENARIOS.md` 1절 "그 밖" 표에 `E2EDriverContractTests`(5건, Unity 테스트 도우미 계약, 옮기지 않음 — Host `PlayerDriver`가 같은 역할) 한 줄.
2. ~~"옮겨야 할 것" 5개 옮기기~~ — 2026-11-12에 했다(0절). 남은 것: Screens `CharacterCreatorScreenTests.ASlotThatGotAStoryMeanwhile_…`는 슬롯 정보만 그대로인지 보고, 진행 파일(예: 스냅샷)이 그대로인지는 보지 않는다. `SaveSlots.StartNewSlot`이 지우기 전에 돌아가므로 지금은 안전하지만, Screens를 맡은 세션이 진행 파일 확인 한 줄을 더하면 Unity `StartNewSlot_NeverOverwritesAStoryThatStillLoads`와 같아진다. 슬롯 정보 파일이 아예 없는(깨진 것이 아닌) 슬롯의 남은 파일 지우기도 같은 테스트 파일에 한 경우로 더할 수 있다.
3. `docs/superpowers/specs/2026-11-12-unity-removal-plan.md` 112줄·127줄은 "`MissingPathsTests.School_…`가 `School.unity`를 직접 읽는다"고 적고 있다. 지금은 고정 파일 `Fixtures/school-structure-colliders.json`을 읽으므로 낡았다(5절). 단계 1의 (다)는 끝났다고 적어야 한다.
4. 전수조사 문서 2절의 이야기 깃발 버그(4.1)는 main에서 고쳐졌다(`GameSession.cs:122-125`). 그 문서를 갱신할 때 반영한다.

## Unity 판과 달라진 동작

조사 자체는 코드를 바꾸지 않았다. 2026-11-12 옮기기(0절)에서: 스냅샷의 슬롯 이름이 다르면 Unity 판은 거절했지만 MonoGame 판은 복사한 슬롯으로 읽는다(그 전부터의 결정, `OlderSlotTests`·`SnapshotOwnerTests.ASnapshotWithAnotherSlotName_…`). 거절할 때 Unity 판은 "복구 필요"로 멈췄고 MonoGame 판은 `SaveSlotDamagedException`으로 시작을 거절한다.

---

## 5. `Tools/unity-removal/list.sh` — 낡은 `School.unity` 분류 고침

- 고친 것: 분류 함수에서 `Assets/Scenes/School.unity`를 "나 테스트가 읽는 Unity 씬"으로 따로 빼던 줄을 지우고, 머리 주석의 "나" 설명을 "테스트가 직접 여는 Unity 씬은 없다(학교 충돌 대조는 고정 파일 `school-structure-colliders.json`을 읽는다)"로 고쳤다.
- 근거: `game/tests/Rootborn.World.Tests/MissingPathsTests.cs:48-66`이 `Fixtures/school-structure-colliders.json`을 읽는다. `git grep -n "Assets/Scenes\|\.unity\b" -- game Tools`에서 `.unity`를 여는 테스트 코드는 0이고 주석·고정 파일 설명만 나온다.
- 다시 돌린 결과(`bash Tools/unity-removal/list.sh`, 커밋 기준 HEAD `4f72c7bae`):

| 분류 | 고치기 전 | 고친 뒤 |
|---|---|---|
| 나 테스트가 읽는 Unity 씬 | 1파일 (477,515바이트) | (없어짐) |
| 나 테스트 고정 파일 (game/tests) | 24 | 24 |
| 라 Unity 전용: Assets 의 코드·씬·설정·에셋 | 2,428파일, 40,192,969바이트 (38.3MB) | 2,429파일, 40,670,484바이트 (38.8MB) |
| `delete-unity-only.txt` (지워도 되는 Unity 전용 목록) | 74,538줄 | 74,539줄 |
| `tests-read.txt` | 25줄 | 24줄 |
| 합계 | 121,198파일, 474.3MB | 같음 |

- main `95c080c51` 위로 옮긴 뒤 다시 돌려도 Unity 쪽 숫자는 같다(라 2,429파일, `delete-unity-only.txt` 74,539줄). 합계만 121,206파일로 늘었다(main의 새 파일과 이 문서).
- 뜻: `School.unity`는 이제 지워도 되는 Unity 전용 파일이다. 테스트가 읽는 Unity 파일은 0이 됐다.

---

## 6. 세는 방법

모두 작업 폴더 루트에서 Git Bash로 돌렸다. 중간 결과는 저장소 밖 임시 폴더에 두었다.

```bash
# 규칙 파일 수 (영역별)
for d in Assets/Scripts/Game/*/; do echo "$d $(find "$d" -name '*.cs' | wc -l)"; done   # 합 459
find Assets/Scripts/Network -name '*.cs' | wc -l; find Assets/Scripts/UI -name '*.cs' | wc -l; find Assets/Scripts/Editor -name '*.cs' | wc -l   # 21, 110, 29

# 새 코드에 선언된 형 이름 (같은 이름 대조용)
git grep -hoE '\b(class|struct|interface|enum)\s+[A-Za-z_][A-Za-z0-9_]*' -- 'Packages/com.rootborn.core/Runtime/*.cs' 'game/*.cs' ':!game/tests' | awk '{print $2}' | sort -u | wc -l   # 1150

# 데이터 종류 등록·분류
git grep -hoE 'b\.Add(<[^>]+>)?\("[A-Za-z_]+"' -- game/Rootborn.Data | sort -u | wc -l           # 174
git grep -hoE 'b\.(NotARule|NotUsedByGame)\("[A-Za-z_]+"' -- game/Rootborn.Data | sort -u | wc -l # 26

# 스크립트가 씬·프리팹·에셋에 붙었는지: .cs.meta 의 guid 를 *.unity, *.prefab, Assets/Data 등의 *.asset 에서
#   'm_Script: {fileID: 11500000, guid: <guid>' 로 찾았다(파이썬으로 한 번에 읽음). 스크립트 619개 중 247개 guid 가 쓰임.

# 테스트 파일과 테스트 수
git ls-files 'Assets/Tests/EditMode/*.cs' | wc -l; git ls-files 'Assets/Tests/PlayMode/*.cs' | wc -l   # 415, 76
git grep -cE '\[(Test|UnityTest)\b' -- 'Assets/Tests/PlayMode/*.cs' | awk -F: '{s+=$2} END{print s}'   # 167

# dotnet 테스트 머리의 "옮겨 옴" 경로
git grep -oE 'Assets/Tests/EditMode/[A-Za-z0-9_/]+\.cs' -- Tools/Rootborn.Core.Tests game/tests Tools/Rootborn.DataExport.Tests | sort -u | wc -l   # 59줄, Unity 파일 51개

# 쓰이지 않음 확인 (예)
git grep -lw ModernSocietyStats -- 'Assets/Scripts/*.cs' 'Assets/Tests/*.cs'
git grep -n "SetVerticalSliceSummary\|AppendVerticalSliceGuide" -- Assets/Scripts | grep -v "public void"   # 0줄
```

부록 표는 위 결과를 파이썬으로 합쳐 만든 것이고, 판정 이유 중 손으로 쓴 것(규칙 101파일, 테스트 33파일)은 작성자가 해당 파일을 열거나 grep한 결과다.

---

## 부록 A. `Assets/Scripts/Game` 파일별 대조 (459개)

열: 파일 / 판정 / 대응 또는 이유. "옮김(같은 이름)"은 그 파일이 선언한 형 이름이 `Packages/com.rootborn.core/Runtime`·`game/`(테스트 제외)에 같은 이름으로 있는 것이다(이름 대조라 내용 동일성까지는 보지 않았다). "씬·프리팹에 N번 붙음"은 그 스크립트 `.cs.meta` guid가 `.unity`·`.prefab` 안에 나온 횟수다.


### Bootstrap (8)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `AppConfig.cs` | Unity 전용 | Unity 실행 인자(-mode/-port/-joinIp) 설정. Host는 `HostOptions`(game/README.md 인자 표). 네트워크 실행 모드는 프로젝트 5 |
| `ArgsParser.cs` | Unity 전용 | 위와 같음. SCENARIOS.md 2절 NET-001 |
| `GameBootstrap.cs` | Unity 컴포넌트 | Unity 부트·씬 설치. MonoGame 판은 Host가 시작하고 `GameSession`을 만든다. 씬·프리팹에 2번 붙음 |
| `SceneDiagnostics.cs` | Unity 컴포넌트 | Unity 부트·씬 설치. MonoGame 판은 Host가 시작하고 `GameSession`을 만든다. Unity 판에서도 안 쓰임(씬·프리팹 0, 참조 0) |
| `TownPlayableBaselineRuntimeInstaller.cs` | Unity 컴포넌트 | Unity 부트·씬 설치. MonoGame 판은 Host가 시작하고 `GameSession`을 만든다. 씬 0, 코드에서 AddComponent·참조 21파일 |
| `TownSinglePlayerFallbackInstaller.cs` | 쓰이지 않음 | 이 파일 밖 참조 0, 씬·프리팹 0 |
| `TownStudentLifeRuntimeInstaller.cs` | Unity 컴포넌트 | Unity 부트·씬 설치. MonoGame 판은 Host가 시작하고 `GameSession`을 만든다. 씬 0, 코드에서 AddComponent·참조 20파일 |
| `TownTilemapFallbackInstaller.cs` | 쓰이지 않음 | 이 파일 밖 참조 0, 씬·프리팹 0 |

### Careers (18)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `AdultCareerCommandCoordinator.cs` | 다른 것으로 대체 | 명령 → 세이브 → 알림 순서를 맞추던 Unity 조정자. MonoGame 판은 `GameSession`이 명령마다 저장하고 `Changed`를 낸다 |
| `AdultCareerPersistenceRouter.cs` | 옮김(같은 이름) | `Entry` |
| `AdultCareerProgress.cs` | 옮김(같은 이름) | `PendingFacilityWorkSaveData`, `FacilityWorkProgressSaveData`, `AdultCareerProgressSaveData`, `AdultCareerProgress` |
| `AdultCareerService.cs` | 옮김(같은 이름) | `AdultCareerResultKind`, `AdultCareerResult`, `AdultCareerService` |
| `AdultCareerSnapshot.cs` | 옮김(이름 다름) | Core `AdultCareerSnapshotSaveData`, 파일 이름은 `SaveFileNames` |
| `AdultCareerSnapshotPersistence.cs` | 옮김(이름 다름) | Core `SaveFileNames` + Data `FileSaveStore` |
| `AutoSkipRequirementBase.cs` | 옮김(같은 이름) | `AutoSkipContext` |
| `CandidateReadyTransitionRequirement.cs` | 옮김(같은 이름) | `CandidateReadyTransitionRequirement` |
| `CareerHintTransitionRequirement.cs` | 옮김(같은 이름) | `CareerHintTransitionRequirement` |
| `FacilityDefinition.cs` | 옮김(데이터 종류) | `FacilityDefinition` → Core `Facility` (AdultCareerBuilders.cs) |
| `FacilityEquipmentAutoSkipRequirement.cs` | 옮김(같은 이름) | `FacilityEquipmentAutoSkipRequirement` |
| `FacilityLevelDefinition.cs` | 옮김(데이터 종류) | `FacilityLevelDefinition` → Core `FacilityLevel` (AdultCareerBuilders.cs) |
| `FacilityTransitionRequirementBase.cs` | 옮김(같은 이름) | `FacilityTransitionContext` |
| `FacilityVisualDefinition.cs` | 분류됨 | `FacilityVisualDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `FacilityWorkDefinition.cs` | 옮김(데이터 종류) | `FacilityWorkDefinition` → Core `FacilityWork` (AdultCareerBuilders.cs) |
| `MasteryAutoSkipRequirement.cs` | 옮김(같은 이름) | `MasteryAutoSkipRequirement` |
| `MinimumDayTransitionRequirement.cs` | 옮김(같은 이름) | `MinimumDayTransitionRequirement` |
| `SuccessCountAutoSkipRequirement.cs` | 옮김(같은 이름) | `SuccessCountAutoSkipRequirement` |

### Characters (19)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `CharacterAppearanceDefinition.cs` | 규칙 아님 | 캐릭터 겉모습(`NotRules` Look 묶음) |
| `CharacterAppearanceSnapshot.cs` | 옮김(이름 다름) | Core `SaveSlotMetadata`의 겉모습 필드 |
| `CharacterGrowthStageDefinition.cs` | 옮김(데이터 종류) | `CharacterGrowthStageDefinition` → Core `GrowthStageRule` (CharacterBuilders.cs) |
| `CharacterVisualAction.cs` | 규칙 아님 | 그리기 동작 이름(걷기·도구). Host 캐릭터 그리기 몫 |
| `GrowingCharacterAppearanceDefinition.cs` | 분류됨 | `GrowingCharacterAppearanceDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `GrowingCharacterRuntimeInstaller.cs` | Unity 전용 | 씬에 캐릭터 그림 컴포넌트를 붙이는 설치기 |
| `GrowingCharacterSheetDefinition.cs` | 분류됨 | `GrowingCharacterSheetDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `GrowingCharacterStageSelector.cs` | 옮김(이름 다름) | World `PlayerWearables`, Screens `CreatorLooks` |
| `GrowingCharacterVisualView.cs` | Unity 컴포넌트 | 캐릭터 그림 컴포넌트. Host 캐릭터 그리기·World `PlayerWearables`. 씬·프리팹에 1번 붙음 |
| `GrowingWardrobeStore.cs` | 옮김(같은 이름) | `Entry` |
| `ICharacterVisualView.cs` | Unity 전용 | Unity 그림 컴포넌트 인터페이스 |
| `NpcCharacterVisualAdapter.cs` | Unity 컴포넌트 | 캐릭터 그림 컴포넌트. Host 캐릭터 그리기·World `PlayerWearables`. Unity 판에서도 안 쓰임(씬·프리팹 0, 참조 0) |
| `PixelwoodCharacterVisualView.cs` | Unity 컴포넌트 | 캐릭터 그림 컴포넌트. Host 캐릭터 그리기·World `PlayerWearables`. Unity 판에서도 안 쓰임(씬·프리팹 0, 참조 0) |
| `PlayerCharacterVisualAdapter.cs` | Unity 컴포넌트 | 캐릭터 그림 컴포넌트. Host 캐릭터 그리기·World `PlayerWearables`. 씬 0, 코드에서 AddComponent·참조 2파일 |
| `WearableCatalogFactory.cs` | 다른 것으로 대체 | 옷 목록은 `GameDefinitions`(Core Wearable*) |
| `WearableDefinition.cs` | 옮김(데이터 종류) | `WearableDefinition` → Core `?` (CharacterBuilders.cs) |
| `WearableLook.cs` | 옮김(이름 다름) | `GameSession.Queries` 겉모습 조회, Host `WorldScreen` |
| `WearableSlotDefinition.cs` | 옮김(데이터 종류) | `WearableSlotDefinition` → Core `WearableSlotRule` (CharacterBuilders.cs) |
| `WearableWardrobeStore.cs` | 옮김(이름 다름) | Core `SaveFileNames`(옷장 파일) + World `PlayerWearables` |

### Common (20)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `AddressableManifest.cs` | Unity 전용 | Addressables 목록. 참조 0 |
| `EventSystemRuntimeDeduplicator.cs` | Unity 컴포넌트 | Unity UI 이벤트 시스템. Unity 판에서도 안 쓰임(씬·프리팹 0, 참조 0) |
| `GameDataLookupCache.cs` | 다른 것으로 대체 | `GameDefinitions` 표 조회 |
| `GameDataRegistry.cs` | 옮김(같은 이름) | `ItemCategory`, `Slot` |
| `GameDataRegistryProvider.cs` | 다른 것으로 대체 | Unity 데이터 공급자. `GameDefinitions`를 생성자로 받는다 |
| `GameDataRegistryToolVisualMappingExtensions.cs` | 쓰이지 않음 | 참조 0. 도구 그림(도구는 NotUsedByGame) |
| `IGameDataRegistryProvider.cs` | 다른 것으로 대체 | 위 공급자와 같음 |
| `ItemDefinition.cs` | 옮김(같은 이름) | `ItemDefinition` |
| `ModernFarmSpriteAddresses.cs` | 규칙 아님 | 그림 주소. MonoGame 판은 테마·좌표표 데이터(game/themes, data/atlases) |
| `ModernHudSpriteKeys.cs` | 규칙 아님 | 그림 주소(위와 같음) |
| `ModernInteriorsSpriteAddresses.cs` | 규칙 아님 | 그림 주소(위와 같음) |
| `ModernUISpriteAddresses.cs` | 규칙 아님 | 그림 주소(위와 같음) |
| `ModernUiCommonPanel48Sprites.cs` | 규칙 아님 | 그림 주소(위와 같음) |
| `ModernUiSpriteKey.cs` | 규칙 아님 | 그림 주소(위와 같음) |
| `ModernUiStyle2Icon.cs` | 규칙 아님 | 그림 주소(위와 같음) |
| `ModernUiStyle2IconCatalog.cs` | 옮김(같은 이름) | `Entry` |
| `ModernUiStyle2Sprites.cs` | 옮김(같은 이름) | `Slot` |
| `PassiveInputModule.cs` | Unity 전용 | Unity EventSystem 입력 모듈. Host `Input/Devices.cs` |
| `UiGameplayInputLock.cs` | 다른 것으로 대체 | 이동을 막는지는 `ScreenCatalog` 등록의 `blocksMovement` |
| `UiTextInputFocus.cs` | 다른 것으로 대체 | 글자 칸 포커스는 Rootborn.Ui 포커스 |

### Crafting (5)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `CraftStepBase.cs` | 게임이 쓰지 않음 | 레시피 만들기 단계의 바탕. `NotUsedByGame` GatherResourceStep과 같은 이유 |
| `GatherResourceStep.cs` | 분류됨 | `GatherResourceStep` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `RecipeDefinition.cs` | 옮김(같은 이름) | `RecipeDefinition` |
| `ResourceRecipeProgress.cs` | 옮김(같은 이름) | `ResourceRecipeProgressSaveData`, `ResourceRecipeProgress` |
| `WaitOnSurfaceStep.cs` | 데이터 없음 + 쓰이지 않음 | data/에 0건, Unity 안에서도 `CraftingRecipeTests` 말고 부르는 곳 0 (전수조사가 '이유 없음'으로 지적한 것) |

### Dialogue (12)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `DialogueChoiceDefinition.cs` | 옮김(같은 이름) | `DialogueQuestAction`, `DialogueChoiceContext` |
| `DialogueCondition.cs` | 옮김(같은 이름) | `DialogueCondition` |
| `DialogueDefinition.cs` | 옮김(같은 이름) | `DialogueDefinition` |
| `DialogueSession.cs` | 옮김(같은 이름) | `DialogueSession` |
| `NpcDefinition.cs` | 옮김(같은 이름) | `NpcDefinition` |
| `NpcInteractor.cs` | Unity 컴포넌트 | NPC 상호작용 컴포넌트. 대화 규칙은 Core Dialogue, 지점은 `game/world`. 씬·프리팹에 7번 붙음 |
| `NpcRoleDefinition.cs` | 옮김(데이터 종류) | `NpcRoleDefinition` → Core `NpcRole` (DialogueBuilders.cs) |
| `NpcScheduleDefinition.cs` | 옮김(같은 이름) | `NpcScheduleEntry`, `NpcScheduleContext` |
| `NpcScheduleProgress.cs` | 옮김(같은 이름) | `NpcScheduleProgressResultKind`, `NpcScheduleProgressResult`, `NpcScheduleProgress` |
| `NpcScheduleResolver.cs` | 옮김(같은 이름) | `NpcScheduleResult`, `NpcScheduleResolver` |
| `QuestProvider.cs` | Unity 컴포넌트 | NPC 상호작용 컴포넌트. 대화 규칙은 Core Dialogue, 지점은 `game/world`. 씬 0, 코드에서 AddComponent·참조 1파일 |
| `TimeSlotScheduleRule.cs` | 옮김(같은 이름) | `TimeSlotScheduleRule` |

### DiscoveryClues (31)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `ClueInterpretationCareerHintOutcome.cs` | 옮김(같은 이름) | `ClueInterpretationCareerHintOutcome` |
| `ClueInterpretationClueSeenCondition.cs` | 옮김(같은 이름) | `ClueInterpretationClueSeenCondition` |
| `ClueInterpretationCore.cs` | 옮김(같은 이름) | `ClueInterpretationSourceKind`, `ClueInterpretationSummarySurface`, `ClueInterpretationPolicyKind`, `ClueInterpretationResultKind` |
| `ClueInterpretationDefinition.cs` | 옮김(데이터 종류) | `ClueInterpretationDefinition` → Core `ClueInterpretation` (DiscoveryClueBuilders.cs) |
| `ClueInterpretationEncyclopediaOutcome.cs` | 옮김(같은 이름) | `ClueInterpretationEncyclopediaOutcome` |
| `ClueInterpretationPolicyDefinition.cs` | 옮김(데이터 종류) | `ClueInterpretationPolicyDefinition` → Core `ClueInterpretationPolicy` (DiscoveryClueBuilders.cs) |
| `ClueInterpretationProgress.cs` | 옮김(같은 이름) | `ClueInterpretationProgressSaveData`, `ClueInterpretationRecordSaveData`, `ClueInterpretationRecord`, `ClueInterpretationTodaySummaryEntry` |
| `ClueInterpretationProgressPersistence.cs` | 옮김(이름 다름) | Core `ClueInterpretationProgress` |
| `ClueInterpretationQuestUnlockOutcome.cs` | 옮김(같은 이름) | `ClueInterpretationQuestUnlockOutcome` |
| `ClueInterpretationRelationshipDeltaOutcome.cs` | 옮김(같은 이름) | `ClueInterpretationRelationshipDeltaOutcome` |
| `ClueInterpretationRelationshipThresholdCondition.cs` | 옮김(같은 이름) | `ClueInterpretationRelationshipThresholdCondition` |
| `ClueInterpretationSourceDefinition.cs` | 옮김(데이터 종류) | `ClueInterpretationSourceDefinition` → Core `ClueInterpretationSource` (DiscoveryClueBuilders.cs) |
| `ClueInterpretationStatusDeltaOutcome.cs` | 옮김(같은 이름) | `ClueInterpretationStatusDeltaOutcome` |
| `ClueInterpretationStrategies.cs` | 옮김(이름 다름) | 결과 id 상수 묶음. 전략 종류는 데이터 종류로 등록 |
| `ClueInterpretationSummaryBuilder.cs` | 옮김(같은 이름) | `ClueInterpretationSummaryModel`, `ClueInterpretationSummaryBuilder`, `ClueInterpretationLookupCache` |
| `ClueInterpretationWorldStateActiveCondition.cs` | 옮김(같은 이름) | `ClueInterpretationWorldStateActiveCondition` |
| `ClueInterpretationWorldStateOutcome.cs` | 옮김(같은 이름) | `ClueInterpretationWorldStateOutcome` |
| `DiscoveryClueCareerHintOutcome.cs` | 옮김(같은 이름) | `DiscoveryClueCareerHintOutcome` |
| `DiscoveryClueCore.cs` | 옮김(같은 이름) | `DiscoveryClueSourceKind`, `DiscoveryClueSummarySurface`, `DiscoveryClueCompletionKind`, `DiscoveryClueResultKind` |
| `DiscoveryClueEncyclopediaOutcome.cs` | 옮김(같은 이름) | `DiscoveryClueEncyclopediaOutcome` |
| `DiscoveryClueLocationVisitCompletion.cs` | 옮김(같은 이름) | `DiscoveryClueLocationVisitCompletion` |
| `DiscoveryClueProgress.cs` | 옮김(같은 이름) | `DiscoveryClueProgressSaveData`, `DiscoveryClueRecordSaveData`, `DiscoveryClueRecord`, `DiscoveryClueTodaySummaryEntry` |
| `DiscoveryClueProgressPersistence.cs` | 옮김(이름 다름) | Core `DiscoveryClueSummary` |
| `DiscoveryClueQuestUnlockOutcome.cs` | 옮김(같은 이름) | `DiscoveryClueQuestUnlockOutcome` |
| `DiscoveryClueSourceDefinition.cs` | 옮김(데이터 종류) | `DiscoveryClueSourceDefinition` → Core `DiscoveryClueSource` (DiscoveryClueBuilders.cs) |
| `DiscoveryClueStrategies.cs` | 옮김(이름 다름) | 결과 id 상수 묶음. 전략 종류는 데이터 종류로 등록 |
| `DiscoveryClueSummaryBuilder.cs` | 옮김(같은 이름) | `DiscoveryClueSummaryModel`, `DiscoveryClueSummaryBuilder`, `DiscoveryClueLookupCache` |
| `DiscoveryClueWorldStateActiveCondition.cs` | 옮김(같은 이름) | `DiscoveryClueWorldStateActiveCondition` |
| `DiscoveryClueWorldStateRevealOutcome.cs` | 옮김(같은 이름) | `DiscoveryClueWorldStateRevealOutcome` |
| `GameDataRegistryClueInterpretationExtensions.cs` | 다른 것으로 대체 | `GameDefinitions` 표 조회. 참조 0 |
| `GameDataRegistryDiscoveryClueExtensions.cs` | 다른 것으로 대체 | `GameDefinitions` 표 조회. 참조 0 |

### Encyclopedia (4)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `EncyclopediaDefinitions.cs` | 옮김(같은 이름) | `EncyclopediaUnlockContext`, `EncyclopediaLocationUnlockedCondition`, `EncyclopediaNpcMetCondition`, `EncyclopediaActivityCompletedCondition` |
| `EncyclopediaEntryDefinition.cs` | 옮김(데이터 종류) | `EncyclopediaEntryDefinition` → Core `EncyclopediaEntry` (EncyclopediaBuilders.cs) |
| `EncyclopediaProgress.cs` | 옮김(같은 이름) | `EncyclopediaProgressSaveData`, `EncyclopediaProgressRecord`, `EncyclopediaProgress` |
| `EncyclopediaRuntime.cs` | 옮김(같은 이름) | `EncyclopediaDiscoveryCompletedCondition` |

### Family (9)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `CharacterAppearance.cs` | 옮김(이름 다름) | Core `SaveSlotMetadata` |
| `CharacterPartAnimationClipDefinition.cs` | 분류됨 | `CharacterPartAnimationClipDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `CharacterPartAnimator.cs` | Unity 컴포넌트 | 레거시 부위 그림·낚시 연출. 씬 0, 코드에서 AddComponent·참조 3파일 |
| `CharacterPartComposer.cs` | Unity 컴포넌트 | 레거시 부위 그림·낚시 연출. 씬 0, 코드에서 AddComponent·참조 3파일 |
| `CharacterPartDefinition.cs` | 규칙 아님 | 레거시 부위 그림(Look 묶음) |
| `CharacterPartLayer.cs` | 규칙 아님 | 레거시 부위 그림(Look 묶음) |
| `CharacterPartMotionDefinition.cs` | 분류됨 | `CharacterPartMotionDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `FishingAnimationController.cs` | Unity 컴포넌트 | 레거시 부위 그림·낚시 연출. 씬·프리팹에 1번 붙음 |
| `FishingAnimationDefinition.cs` | 분류됨 | `FishingAnimationDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |

### Heir (3)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `HeirData.cs` | 게임이 쓰지 않음 | 레거시 세대(`NotUsedByGame` HeirTrait과 같은 이유) |
| `HeirGenerator.cs` | 게임이 쓰지 않음 | 참조 0(테스트 `HeirGeneratorTests`만). HEIR-001 |
| `HeirTrait.cs` | 분류됨 | `HeirTrait` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |

### Housing (1)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `HouseStatePersistence.cs` | 옮김(같은 이름) | `HouseStateSaveData` |

### Interiors (5)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `GroundDepthSort.cs` | Unity 컴포넌트 | 그리기 정렬·실내 타일 칠하기. 씬·프리팹에 50번 붙음 |
| `InteriorGeneration.cs` | 분류됨 | `InteriorGenerationProfile` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `InteriorPlacementAvailability.cs` | 다른 것으로 대체 | 배치 미리보기의 놓을 수 있는 칸 셈. MonoGame 판은 화면이 Core `House.CanPlace`를 부른다(`FurniturePlacementScreen.cs:20`) |
| `InteriorTileSetDefinition.cs` | 분류됨 | `InteriorTileSetDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `InteriorTilemapApplier.cs` | Unity 컴포넌트 | 그리기 정렬·실내 타일 칠하기. 씬·프리팹에 1번 붙음 |

### Knowledge (6)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `GatherCountTrigger.cs` | 게임이 쓰지 않음 | 지식. 데이터 0건, 참조 0 |
| `HitGroundWithRockTrigger.cs` | 분류됨 | `HitGroundWithRockTrigger` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `KnowledgeContext.cs` | 게임이 쓰지 않음 | 지식(`NotUsedByGame` Knowledge) |
| `KnowledgeNode.cs` | 분류됨 | `KnowledgeNode` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `KnowledgeProgress.cs` | 게임이 쓰지 않음 | 지식. Core 퀘스트 문맥의 지식 자리는 `GameSession`이 null로 넘긴다(`GameSession.cs:122`) |
| `KnowledgeTriggerBase.cs` | 게임이 쓰지 않음 | 지식 |

### Lighting (3)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `DayNightLightingProfile.cs` | 옮김(같은 이름) | `DayNightLightingProfile` |
| `LightingManager.cs` | Unity 컴포넌트 | 조명(런타임 동작 표 참고). 씬·프리팹에 1번 붙음 |
| `LocalLightController.cs` | Unity 컴포넌트 | 조명(런타임 동작 표 참고). 씬·프리팹에 1번 붙음 |

### Lineage (2)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `AncestorRecord.cs` | 게임이 쓰지 않음 | 레거시 세대. Lineage 폴더 밖 참조 0 |
| `LineageBook.cs` | 게임이 쓰지 않음 | 참조 0, 테스트도 없음 |

### Localization (2)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `GameText.cs` | 옮김(같은 이름) | `GameText` |
| `GameTextCatalog.cs` | 옮김(같은 이름) | `Entry` |

### Managers (3)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `DataManager.cs` | Unity 전용 | Addressables 데이터 적재. Data `GameDataLoader` |
| `Managers.cs` | Unity 컴포넌트 | Addressables 매니저 싱글톤. 씬 0, 코드에서 AddComponent·참조 29파일 |
| `ResourceManager.cs` | Unity 전용 | Addressables 그림 적재. Host 그림 읽기 |

### Minigames (10)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `ChallengeDefinitionBase.cs` | 옮김(같은 이름) | `MinigameMechanic`, `MinigameTuning`, `ChallengeSession` |
| `ChoiceChallengeDefinition.cs` | 옮김(같은 이름) | `ChoiceSession` |
| `GrowthMinigameBonusRule.cs` | 옮김(같은 이름) | `MinigameBonusTarget`, `GrowthMinigameBonusRule` |
| `MinigameBonusRuleBase.cs` | 옮김(이름 다름) | Core 미니게임 보너스 규칙 |
| `MinigameDefinition.cs` | 옮김(데이터 종류) | `MinigameDefinition` → Core `Minigame` (MinigameBuilders.cs) |
| `MinigameSession.cs` | 옮김(같은 이름) | `MinigameSession` |
| `PlacementChallengeDefinition.cs` | 옮김(같은 이름) | `PlacementSession` |
| `SequenceChallengeDefinition.cs` | 옮김(같은 이름) | `SequenceSession` |
| `TargetDodgeChallengeDefinition.cs` | 옮김(같은 이름) | `TargetSession` |
| `TimingChallengeDefinition.cs` | 옮김(같은 이름) | `TimingSession` |

### ModernSociety (5)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `ModernActivityDefinition.cs` | 쓰이지 않음 | ModernSociety 폴더와 그 테스트(`ModernSocietyLoopTests`) 밖 참조 0, `.asset` 0, data/ 0 |
| `ModernActivityKind.cs` | 쓰이지 않음 | 위와 같음 |
| `ModernRoutineFlagRule.cs` | 쓰이지 않음 | 위와 같음 |
| `ModernSocietyDay.cs` | 쓰이지 않음 | 위와 같음 |
| `ModernSocietyStats.cs` | 쓰이지 않음 | 위와 같음 |

### Placement (11)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `AuthoredHouseFurnitureReplay.cs` | 옮김(이름 다름) | Core `FurnitureReplay` |
| `AuthoredHouseFurnitureSession.cs` | Unity 컴포넌트 | Unity 가구 배치 세션·면 컴포넌트. 규칙은 Core `House`, 화면은 Screens `FurniturePlacementScreen`. 씬 0, 코드에서 AddComponent·참조 3파일 |
| `AuthoredHousePlacementModel.cs` | 옮김(이름 다름) | Core `HousePlacementModel` |
| `FurnitureDefinition.cs` | 옮김(같은 이름) | `FurnitureDefinition` |
| `FurniturePlacementLayoutPersistence.cs` | 옮김(같은 이름) | `FurniturePlacementLayoutSaveData` |
| `FurniturePlacementRegistry.cs` | 옮김(같은 이름) | `Entry` |
| `FurniturePlacementSaveData.cs` | 옮김(같은 이름) | `FurniturePlacementSaveData` |
| `FurniturePlacementService.cs` | 다른 것으로 대체 | Unity Tilemap에 칸을 칠하는 놓기. 판정은 Core `House`, 그리기는 Host |
| `FurniturePlacementTypes.cs` | 옮김(같은 이름) | `FurniturePlacementDirection`, `FurniturePlacementPreference`, `FurnitureTilePart` |
| `FurnitureUnlockProgress.cs` | 옮김(같은 이름) | `FurnitureUnlockProgressSaveData`, `FurniturePurchaseResult`, `FurniturePurchase`, `FurnitureUnlockProgress` |
| `TilePlacementSurface.cs` | Unity 컴포넌트 | Unity 가구 배치 세션·면 컴포넌트. 규칙은 Core `House`, 화면은 Screens `FurniturePlacementScreen`. 씬 0, 코드에서 AddComponent·참조 6파일 |

### Player (10)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `CameraFollow.cs` | Unity 컴포넌트 | 플레이어 이동·카메라·상호작용·인벤토리 컴포넌트. MonoGame 판은 World 걷기 + Host + Core `ItemInventory`. 씬·프리팹에 5번 붙음 |
| `CharacterCustomization.cs` | 옮김(같은 이름) | `Facing` |
| `GatherInteractor.cs` | 옮김(같은 이름) | `PlayerInventorySaveData`, `PlayerInventoryItemSaveData` |
| `InteractionPromptScreenReservations.cs` | 옮김(같은 이름) | `Registration` |
| `PlayerController.cs` | Unity 컴포넌트 | 플레이어 이동·카메라·상호작용·인벤토리 컴포넌트. MonoGame 판은 World 걷기 + Host + Core `ItemInventory`. 씬·프리팹에 5번 붙음 |
| `PlayerGlobalState.cs` | 옮김(이름 다름) | 플레이어별 퀘스트 기록: Core `SaveFileNames.PlayerQuestLog`, Game `TwoPlayersOneSlotTests` |
| `PlayerIdentity.cs` | Unity 컴포넌트 | 플레이어 이동·카메라·상호작용·인벤토리 컴포넌트. MonoGame 판은 World 걷기 + Host + Core `ItemInventory`. 씬·프리팹에 1번 붙음 |
| `PlayerInteractionRouter.cs` | Unity 컴포넌트 | 플레이어 이동·카메라·상호작용·인벤토리 컴포넌트. MonoGame 판은 World 걷기 + Host + Core `ItemInventory`. 씬·프리팹에 4번 붙음 |
| `PlayerInventory.cs` | Unity 컴포넌트 | 플레이어 이동·카메라·상호작용·인벤토리 컴포넌트. MonoGame 판은 World 걷기 + Host + Core `ItemInventory`. 씬·프리팹에 3번 붙음 |
| `ScenePlayerRenderPolicy.cs` | Unity 컴포넌트 | 플레이어 이동·카메라·상호작용·인벤토리 컴포넌트. MonoGame 판은 World 걷기 + Host + Core `ItemInventory`. 씬·프리팹에 3번 붙음 |

### Quests (46)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `AnyOfQuestObjective.cs` | 옮김(같은 이름) | `AnyOfQuestObjective` |
| `CareerInterestSelectedQuestObjective.cs` | 옮김(같은 이름) | `CareerInterestSelectedQuestObjective` |
| `CollectQuestObjective.cs` | 옮김(같은 이름) | `CollectQuestObjective` |
| `ConditionalQuestChainTransition.cs` | 옮김(같은 이름) | `ConditionalQuestChainTransition` |
| `DailyPlanSelectedQuestObjective.cs` | 옮김(같은 이름) | `DailyPlanSelectedQuestObjective` |
| `DayAtLeastCondition.cs` | 옮김(같은 이름) | `DayAtLeastCondition` |
| `DefeatQuestObjective.cs` | 옮김(같은 이름) | `DefeatQuestObjective` |
| `FurniturePlacedQuestObjective.cs` | 옮김(같은 이름) | `FurniturePlacedQuestObjective` |
| `FurnitureUnlockQuestReward.cs` | 옮김(같은 이름) | `FurnitureUnlockQuestReward` |
| `GatherQuestObjective.cs` | 옮김(같은 이름) | `GatherQuestObjective` |
| `InventoryGrant.cs` | 옮김(같은 이름) | `InventoryGrant` |
| `ItemQuestReward.cs` | 옮김(같은 이름) | `ItemQuestReward` |
| `KnowledgeUnlockedCondition.cs` | 옮김(같은 이름) | `KnowledgeUnlockedCondition` |
| `LocationActivityCompletedCondition.cs` | 옮김(같은 이름) | `LocationActivityCompletedCondition` |
| `LocationActivityQuestObjective.cs` | 옮김(같은 이름) | `LocationActivityQuestObjective` |
| `LocationVisitQuestObjective.cs` | 옮김(같은 이름) | `LocationVisitQuestObjective` |
| `PartTimeWorkQuestObjective.cs` | 옮김(같은 이름) | `PartTimeWorkQuestObjective` |
| `QuestChainCore.cs` | 옮김(같은 이름) | `QuestChainState`, `QuestChainLogSaveData`, `QuestChainProgressSaveData`, `QuestChainProgress` |
| `QuestChainDefinition.cs` | 옮김(같은 이름) | `QuestChainDefinition` |
| `QuestChainProgressStore.cs` | 옮김(같은 이름) | `QuestChainProgressStore` |
| `QuestChainTransitionBase.cs` | 옮김(이름 다름) | ScriptableObject 바탕 → Core 추상 클래스(Base를 뺀 이름) |
| `QuestCompletionEffectBase.cs` | 옮김(이름 다름) | Core `QuestCompletionEffect` |
| `QuestConditionBase.cs` | 옮김(이름 다름) | Core `QuestCondition` |
| `QuestDefinition.cs` | 옮김(같은 이름) | `QuestDefinition` |
| `QuestEvent.cs` | 옮김(같은 이름) | `QuestEvent` |
| `QuestEventKind.cs` | 옮김(같은 이름) | `QuestEventKind` |
| `QuestEventSink.cs` | 옮김(같은 이름) | `IQuestEventSink` |
| `QuestGameplayEventBridge.cs` | 옮김(같은 이름) | `QuestGameplayEventBridge` |
| `QuestLog.cs` | 옮김(같은 이름) | `QuestStateChangeKind`, `QuestStateChange`, `QuestLog` |
| `QuestObjectiveBase.cs` | 옮김(이름 다름) | Core `QuestObjective` |
| `QuestProgress.cs` | 옮김(같은 이름) | `QuestProgress` |
| `QuestRewardBase.cs` | 옮김(이름 다름) | Core `QuestReward` |
| `QuestRewardBatch.cs` | 옮김(같은 이름) | `QuestRewardBatch`, `RewardStep`, `EffectStep` |
| `QuestRuntimeContext.cs` | 옮김(같은 이름) | `QuestRuntimeContext` |
| `QuestSaveData.cs` | 옮김(같은 이름) | `QuestLogSaveData`, `QuestProgressSaveData` |
| `QuestState.cs` | 옮김(이름 다름) | Core `QuestTracker` |
| `QuestStateCondition.cs` | 옮김(같은 이름) | `QuestStateCondition` |
| `QuestStepDefinition.cs` | 옮김(같은 이름) | `QuestStepDefinition` |
| `RewardRuntimeContext.cs` | 옮김(같은 이름) | `RewardRuntimeContext` |
| `SetStoryFlagCompletionEffect.cs` | 옮김(같은 이름) | `SetStoryFlagCompletionEffect` |
| `StoryFlagCondition.cs` | 옮김(같은 이름) | `StoryFlagCondition` |
| `TalkQuestObjective.cs` | 옮김(같은 이름) | `TalkQuestObjective` |
| `TilePlacedQuestObjective.cs` | 옮김(같은 이름) | `TilePlacedQuestObjective` |
| `TraitDeltaCompletionEffect.cs` | 옮김(같은 이름) | `TraitDeltaCompletionEffect` |
| `UnlockKnowledgeCompletionEffect.cs` | 옮김(같은 이름) | `UnlockKnowledgeCompletionEffect` |
| `WorldStateChangeQuestReward.cs` | 옮김(같은 이름) | `WorldStateChangeQuestReward` |

### Resources (3)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `ResourceDrop.cs` | 옮김(같은 이름) | `ResourceDrop` |
| `ResourceNode.cs` | Unity 컴포넌트 | 자원 노드 컴포넌트. 규칙은 Core Gathering, 명령은 `GameSession.GatherResource`. 씬·프리팹에 3번 붙음 |
| `ResourceNodeDefinition.cs` | 옮김(같은 이름) | `ResourceNodeDefinition` |

### Save (2)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `SaveService.cs` | 옮김(이름 다름) | Core `SaveFileNames`·`SaveSlotMetadata` + Data `FileSaveStore` |
| `SaveSlotMetadata.cs` | 옮김(같은 이름) | `SaveSlotMetadata`, `SaveSlotSummary` |

### Status (3)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `PlayerStatus.cs` | Unity 컴포넌트 | 레거시 상태 컴포넌트. Unity 판에서도 안 쓰임(씬·프리팹 0, 참조 0) |
| `StatusEffectDefinition.cs` | 분류됨 | `StatusEffectDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `StatusValue.cs` | 게임이 쓰지 않음 | 쓰는 곳은 `PlayerStatus`뿐이고 그것은 씬 0·참조 0(`NotUsedByGame` StatusEffectDefinition). STATUS-001 일부 |

### Story (2)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `StoryFlagDefinition.cs` | 분류됨 | `StoryFlagDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `StoryFlagSet.cs` | 옮김(이름 다름) | `GameSession.cs:122-125` `UnsavedStoryFlags` — Unity처럼 메모리 전용. 전수조사 4.1의 null 버그는 main에서 고쳐졌다 |

### StudentLife (163)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `ActivityTimePolicyDefinition.cs` | 옮김(데이터 종류) | `ActivityTimePolicyDefinition` → Core `ActivityTimePolicy` (StudentLifeBuilders.cs) |
| `CampaignActivityLogObjective.cs` | 옮김(같은 이름) | `CampaignActivityLogObjective` |
| `CampaignCareerHintObjective.cs` | 옮김(같은 이름) | `CampaignCareerHintObjective` |
| `CampaignCareerHintReward.cs` | 옮김(같은 이름) | `CampaignCareerHintReward` |
| `CampaignCore.cs` | 옮김(같은 이름) | `CampaignEvaluationContext`, `CampaignObjectiveEvaluation`, `CampaignProgressSaveData`, `SelectedRouteSaveData` |
| `CampaignDayDefinition.cs` | 옮김(데이터 종류) | `CampaignDayDefinition` → Core `CampaignDay` (CampaignBuilders.cs) |
| `CampaignDefinition.cs` | 옮김(데이터 종류) | `CampaignDefinition` → Core `Campaign` (CampaignBuilders.cs) |
| `CampaignLocationVisitObjective.cs` | 옮김(같은 이름) | `CampaignLocationVisitObjective` |
| `CampaignObjectiveBase.cs` | 옮김(이름 다름) | Core 캠페인 목표 전략 |
| `CampaignRewardBase.cs` | 옮김(이름 다름) | Core 캠페인 보상 전략 |
| `CampaignRouteDefinition.cs` | 옮김(데이터 종류) | `CampaignRouteDefinition` → Core `CampaignRoute` (CampaignBuilders.cs) |
| `CareerCandidateCore.cs` | 옮김(같은 이름) | `CareerCandidateState`, `CareerCandidateEvaluationContext`, `CareerCandidateHintCountRequirement`, `CareerCandidateSkillRequirement` |
| `CareerCandidateProgressComponent.cs` | Unity 컴포넌트 | 학생 생활 상호작용·진행 컴포넌트. 규칙은 Core StudentLife, 지점은 `game/world/interactions.json`, 화면은 Screens. 씬 0, 코드에서 AddComponent·참조 6파일 |
| `CareerCandidateRouteDefinition.cs` | 옮김(데이터 종류) | `CareerCandidateRouteDefinition` → Core `CareerCandidateRoute` (CareerBuilders.cs) |
| `CareerDefinition.cs` | 옮김(데이터 종류) | `CareerDefinition` → Core `Career` (CareerBuilders.cs) |
| `CareerHintDefinition.cs` | 옮김(데이터 종류) | `CareerHintDefinition` → Core `?` (CareerBuilders.cs) |
| `CareerHintUnlockActivityEffect.cs` | 옮김(데이터 종류) | `CareerHintUnlockActivityEffect` → Core `CareerHintUnlockEffect` (StudentLifeBuilders.cs) |
| `CareerInterestCandidateStateRequirement.cs` | 옮김(같은 이름) | `CareerInterestCandidateStateRequirement` |
| `CareerInterestCore.cs` | 옮김(같은 이름) | `CareerInterestSelectionState`, `CareerInterestEvaluationContext`, `CareerInterestSelectionEvaluation`, `CareerInterestSelectionResult` |
| `CareerInterestDailyChangeLimitRule.cs` | 옮김(같은 이름) | `CareerInterestDailyChangeLimitRule` |
| `CareerInterestFocusReward.cs` | 옮김(같은 이름) | `CareerInterestFocusReward` |
| `CareerInterestProgressComponent.cs` | Unity 컴포넌트 | 학생 생활 상호작용·진행 컴포넌트. 규칙은 Core StudentLife, 지점은 `game/world/interactions.json`, 화면은 Screens. 씬 0, 코드에서 AddComponent·참조 5파일 |
| `CareerInterestStaticRecommendation.cs` | 옮김(같은 이름) | `CareerInterestStaticRecommendation` |
| `CareerPracticeDefinition.cs` | 옮김(데이터 종류) | `CareerPracticeDefinition` → Core `CareerPractice` (CareerBuilders.cs) |
| `CareerResultLogHintSource.cs` | 옮김(같은 이름) | `CareerResultLogHintSource` |
| `DailyEventCareerHintOutcome.cs` | 옮김(같은 이름) | `DailyEventCareerHintOutcome` |
| `DailyEventChoiceDefinition.cs` | 옮김(데이터 종류) | `DailyEventChoiceDefinition` → Core `DailyEventChoice` (DailyEventBuilders.cs) |
| `DailyEventDefinition.cs` | 옮김(같은 이름) | `DailyEventStates`, `DailyEventContext` |
| `DailyEventKindDefinition.cs` | 옮김(데이터 종류) | `DailyEventKindDefinition` → Core `DailyEventKind` (DailyEventBuilders.cs) |
| `DailyEventLocationAvailabilityRule.cs` | 옮김(같은 이름) | `DailyEventLocationAvailabilityRule` |
| `DailyEventOutcomes.cs` | 빈 파일 | 형식이 없다(주석 한 줄). 결과 종류는 각자 파일에 있고 옮겨졌다 |
| `DailyEventProgress.cs` | 옮김(같은 이름) | `DailyEventProgressSaveData`, `Record`, `DailyEventRecord`, `DailyEventProgress` |
| `DailyEventProgressComponent.cs` | Unity 컴포넌트 | 학생 생활 상호작용·진행 컴포넌트. 규칙은 Core StudentLife, 지점은 `game/world/interactions.json`, 화면은 Screens. 씬 0, 코드에서 AddComponent·참조 2파일 |
| `DailyEventRelationshipDeltaOutcome.cs` | 옮김(같은 이름) | `DailyEventRelationshipDeltaOutcome` |
| `DailyEventRelationshipThresholdRule.cs` | 옮김(같은 이름) | `DailyEventRelationshipThresholdRule` |
| `DailyEventResultSummary.cs` | 옮김(같은 이름) | `DailyEventResultSummary` |
| `DailyEventRunner.cs` | 옮김(같은 이름) | `DailyEventRunner`, `DailyEventResolver`, `DailyEventLogCodec` |
| `DailyEventSkillDeltaOutcome.cs` | 옮김(같은 이름) | `DailyEventSkillDeltaOutcome` |
| `DailyEventStatusDeltaOutcome.cs` | 옮김(같은 이름) | `DailyEventStatusDeltaOutcome` |
| `DailyEventTraitDeltaOutcome.cs` | 옮김(같은 이름) | `DailyEventTraitDeltaOutcome` |
| `DailyPlanCareerHintRequirement.cs` | 옮김(같은 이름) | `DailyPlanCareerHintRequirement` |
| `DailyPlanPartTimeWorkRequirement.cs` | 옮김(같은 이름) | `DailyPlanPartTimeWorkRequirement` |
| `DailyPlanProgress.cs` | 옮김(같은 이름) | `DailyPlanProgressSaveData`, `DailyPlanProgress`, `DailyPlanCompletionResult`, `DailyPlanRouteRunner` |
| `DailyPlanProgressComponent.cs` | Unity 컴포넌트 | 학생 생활 상호작용·진행 컴포넌트. 규칙은 Core StudentLife, 지점은 `game/world/interactions.json`, 화면은 Screens. 씬 0, 코드에서 AddComponent·참조 9파일 |
| `DailyPlanRouteContextBuilder.cs` | 다른 것으로 대체 | Unity 컴포넌트에서 문맥을 모으던 것. `GameSession`이 Core 일과 문맥을 만든다 |
| `DailyPlanRouteDefinition.cs` | 옮김(같은 이름) | `DailyPlanRouteContext` |
| `DailyPlanRouteSetDefinition.cs` | 옮김(데이터 종류) | `DailyPlanRouteSetDefinition` → Core `DailyPlanRouteSet` (DailyPlanBuilders.cs) |
| `DailyPlanSchedule.cs` | 옮김(같은 이름) | `DailyPlanActivityKind`, `DailyPlanSlotSaveData`, `DailyPlanDaySaveData`, `DailyPlanSlotRecord` |
| `DailyPlanWorldStateRequirement.cs` | 옮김(같은 이름) | `DailyPlanWorldStateRequirement` |
| `DayEndRuleBase.cs` | 옮김(이름 다름) | Core 하루 끝 규칙 전략 |
| `DayEndRuleDefinition.cs` | 옮김(데이터 종류) | `DayEndRuleDefinition` → Core `DayEndRuleSet` (StudentLifeBuilders.cs) |
| `DiscoveryDefinition.cs` | 옮김(같은 이름) | `DiscoveryScope` |
| `DiscoveryOutcomes.cs` | 옮김(같은 이름) | `DiscoveryTraitDeltaOutcome`, `DiscoverySkillDeltaOutcome`, `DiscoveryStatusDeltaOutcome`, `DiscoveryCareerHintOutcome` |
| `DiscoveryPointInteractor.cs` | Unity 컴포넌트 | 학생 생활 상호작용·진행 컴포넌트. 규칙은 Core StudentLife, 지점은 `game/world/interactions.json`, 화면은 Screens. 씬·프리팹에 1번 붙음 |
| `DiscoveryRequirements.cs` | 옮김(같은 이름) | `PreviousDiscoveryRequirement`, `DiscoveryTraitThresholdRequirement`, `DiscoveryCareerHintRequirement` |
| `DiscoveryRunner.cs` | 옮김(같은 이름) | `DiscoveryResult`, `DiscoveryRunner`, `DiscoveryDayLog`, `DiscoveryGrowthSummaryEntry` |
| `EnergyRestoreDayEndRule.cs` | 옮김(데이터 종류) | `EnergyRestoreDayEndRule` → Core `EnergyRestoreRule` (StudentLifeBuilders.cs) |
| `ExplorationCareerHintOutcome.cs` | 옮김(같은 이름) | `ExplorationCareerHintOutcome` |
| `ExplorationChoiceDefinition.cs` | 옮김(데이터 종류) | `ExplorationChoiceDefinition` → Core `?` (ExplorationBuilders.cs) |
| `ExplorationClueOutcome.cs` | 옮김(같은 이름) | `ExplorationClueOutcome` |
| `ExplorationEncyclopediaOutcome.cs` | 옮김(같은 이름) | `ExplorationEncyclopediaOutcome` |
| `ExplorationFollowUpQuestOutcome.cs` | 옮김(같은 이름) | `ExplorationFollowUpQuestOutcome` |
| `ExplorationInteractionConditions.cs` | 옮김(이름 다름) | internal 상수 묶음. 조건 종류는 데이터 종류로 등록 |
| `ExplorationInteractionDefinition.cs` | 옮김(같은 이름) | `ExplorationInteractionContext` |
| `ExplorationInteractionOutcomes.cs` | 옮김(이름 다름) | internal 상수 묶음. 결과 종류는 데이터 종류로 등록 |
| `ExplorationInteractionRunner.cs` | 옮김(같은 이름) | `ExplorationChoiceResultKind`, `ExplorationOutcomeCollector`, `ExplorationChoiceResult`, `ExplorationInteractionRunner` |
| `ExplorationItemGrantOutcome.cs` | 옮김(같은 이름) | `ExplorationItemGrantOutcome` |
| `ExplorationItemHeldCondition.cs` | 옮김(같은 이름) | `ExplorationItemHeldCondition` |
| `ExplorationItemSpendOutcome.cs` | 옮김(같은 이름) | `ExplorationItemSpendOutcome` |
| `ExplorationPreviousChoiceCondition.cs` | 옮김(같은 이름) | `ExplorationPreviousChoiceCondition` |
| `ExplorationProgress.cs` | 옮김(같은 이름) | `ExplorationProgressSaveData`, `ExplorationProgressRecordSaveData`, `ExplorationProgressRecord`, `ExplorationProgress` |
| `ExplorationRiskPolicyDefinition.cs` | 옮김(데이터 종류) | `ExplorationRiskPolicyDefinition` → Core `ExplorationRiskPolicy` (ExplorationBuilders.cs) |
| `ExplorationSkillDeltaOutcome.cs` | 옮김(같은 이름) | `ExplorationSkillDeltaOutcome` |
| `ExplorationStatusDeltaOutcome.cs` | 옮김(같은 이름) | `ExplorationStatusDeltaOutcome` |
| `ExplorationSummaryBuilder.cs` | 옮김(같은 이름) | `ExplorationLookupCache`, `ExplorationChoiceSummaryModel`, `ExplorationSummaryModel`, `ExplorationSummaryBuilder` |
| `ExplorationTraitDeltaOutcome.cs` | 옮김(같은 이름) | `ExplorationTraitDeltaOutcome` |
| `ExplorationTraitThresholdCondition.cs` | 옮김(같은 이름) | `ExplorationTraitThresholdCondition` |
| `FocusRestoreDayEndRule.cs` | 옮김(데이터 종류) | `FocusRestoreDayEndRule` → Core `FocusRestoreRule` (StudentLifeBuilders.cs) |
| `GameDataRegistryExplorationExtensions.cs` | 다른 것으로 대체 | `GameDefinitions` 표 조회. 참조 0 |
| `GameDataRegistryLocationStateExtensions.cs` | 다른 것으로 대체 | `GameDefinitions` 표 조회 |
| `HelpActionRelationshipDeltaOutcome.cs` | 옮김(같은 이름) | `HelpActionRelationshipDeltaOutcome` |
| `LifeActivityDefinition.cs` | 옮김(데이터 종류) | `LifeActivityDefinition` → Core `LifeActivity` (StudentLifeBuilders.cs) |
| `LifeActivityRunner.Reserved.cs` | 옮김(같은 이름) | `LifeActivityRunner` |
| `LifeCalendarDefinition.cs` | 옮김(데이터 종류) | `LifeCalendarDefinition` → Core `LifeCalendar` (StudentLifeBuilders.cs) |
| `LifeChoiceDefinition.cs` | 옮김(이름 다름), 데이터 없음 | Core `LifeChoice`(`LifeActivity.cs:16`). data/ 활동 4개 모두 `choices: []`라 데이터 종류로는 등록하지 않았다 |
| `LocationActivityDefinition.cs` | 옮김(같은 이름) | `LocationGrowthRoute` |
| `LocationActivityInteractor.cs` | Unity 컴포넌트 | 학생 생활 상호작용·진행 컴포넌트. 규칙은 Core StudentLife, 지점은 `game/world/interactions.json`, 화면은 Screens. 씬·프리팹에 9번 붙음 |
| `LocationActivityOutcomes.cs` | 옮김(같은 이름) | `LocationActivityTraitDeltaOutcome`, `LocationActivitySkillDeltaOutcome`, `LocationActivityRelationshipDeltaOutcome`, `LocationActivityStatusDeltaOutcome` |
| `LocationActivityProgressDeltaOutcome.cs` | 옮김(데이터 종류) | `LocationActivityProgressDeltaOutcome` → Core `?` (LocationBuilders.cs) |
| `LocationActivityResourceRecipeOutcome.cs` | 옮김(같은 이름) | `LocationActivityResourceRecipeOutcome` |
| `LocationActivityRunner.cs` | 옮김(같은 이름) | `LocationActivityResult`, `LocationActivityRunner`, `LocationActivityGrowthSummaryEntry`, `LocationActivityGrowthSummary` |
| `LocationCategoryDefinition.cs` | 옮김(데이터 종류) | `LocationCategoryDefinition` → Core `LocationCategory` (LocationBuilders.cs) |
| `LocationDefinition.cs` | 옮김(데이터 종류) | `LocationDefinition` → Core `Location` (LocationBuilders.cs) |
| `LocationIdentityDefinition.cs` | 옮김(같은 이름) | `LocationHintEntry` |
| `LocationStateActivityLogCondition.cs` | 옮김(같은 이름) | `LocationStateActivityLogCondition` |
| `LocationStateAvailabilityEffect.cs` | 옮김(같은 이름) | `LocationStateAvailabilityEffect` |
| `LocationStateConditionBase.cs` | 옮김(이름 다름) | Core 장소 상태 조건 전략 |
| `LocationStateConflictPolicyDefinition.cs` | 옮김(데이터 종류) | `LocationStateConflictPolicyDefinition` → Core `LocationStateConflictPolicy` (LocationBuilders.cs) |
| `LocationStateCore.cs` | 옮김(같은 이름) | `LocationStateContext`, `LocationStateConflictMode`, `LocationStateLookupCache`, `LocationStateResolver` |
| `LocationStateDefinition.cs` | 옮김(데이터 종류) | `LocationStateDefinition` → Core `LocationState` (LocationBuilders.cs) |
| `LocationStateEffectBase.cs` | 옮김(이름 다름) | Core 장소 상태 효과 전략 |
| `LocationStateNpcScheduleCondition.cs` | 옮김(같은 이름) | `LocationStateNpcScheduleCondition` |
| `LocationStateProgressPersistence.cs` | 옮김(이름 다름) | Core LocationState 세이브 |
| `LocationStateQuestStateCondition.cs` | 옮김(같은 이름) | `LocationStateQuestStateCondition` |
| `LocationStateRelationshipCondition.cs` | 옮김(같은 이름) | `LocationStateRelationshipCondition` |
| `LocationStateStatusCondition.cs` | 옮김(같은 이름) | `LocationStateStatusCondition` |
| `LocationStateTimeSlotCondition.cs` | 옮김(같은 이름) | `LocationStateTimeSlotCondition` |
| `LocationStateWorldStateCondition.cs` | 옮김(같은 이름) | `LocationStateWorldStateCondition` |
| `LocationVisitInteractor.cs` | Unity 컴포넌트 | 학생 생활 상호작용·진행 컴포넌트. 규칙은 Core StudentLife, 지점은 `game/world/interactions.json`, 화면은 Screens. 씬·프리팹에 14번 붙음 |
| `LocationVisitProgress.cs` | 옮김(같은 이름) | `LocationVisitResultKind`, `LocationVisitResult`, `LocationVisitProgress` |
| `MilestoneCoreTypes.cs` | 옮김(같은 이름) | `MilestoneEvaluationContext`, `MilestoneObjectiveEvaluation` |
| `MilestoneDefinition.cs` | 옮김(데이터 종류) | `MilestoneDefinition` → Core `?` (MilestoneBuilders.cs) |
| `MilestoneGrowth.cs` | 옮김(같은 이름) | `MilestoneProgressChange`, `MilestoneApplyResult`, `MilestoneProgressSaveData`, `MilestoneRecordSaveData` |
| `MilestoneObjectiveBase.cs` | 옮김(같은 이름) | `TraitMilestoneObjective`, `SkillMilestoneObjective`, `StatusMilestoneObjective` |
| `MilestoneRewardBase.cs` | 옮김(같은 이름) | `ItemMilestoneReward` |
| `MilestoneRouteDefinition.cs` | 옮김(데이터 종류) | `MilestoneRouteDefinition` → Core `MilestoneRoute` (MilestoneBuilders.cs) |
| `OutsideSchoolActivityCategoryDefinition.cs` | 분류됨 | `OutsideSchoolActivityCategoryDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `OutsideSchoolActivityDefinition.cs` | 옮김(데이터 종류) | `OutsideSchoolActivityDefinition` → Core `OutsideSchoolActivity` (TownLifeBuilders.cs) |
| `OutsideSchoolActivityInteractor.cs` | Unity 컴포넌트 | 학생 생활 상호작용·진행 컴포넌트. 규칙은 Core StudentLife, 지점은 `game/world/interactions.json`, 화면은 Screens. 씬·프리팹에 2번 붙음 |
| `OutsideSchoolActivityRunner.cs` | 옮김(같은 이름) | `OutsideSchoolActivityResult`, `OutsideSchoolActivityRunner`, `OutsideSchoolDayLog`, `OutsideSchoolGrowthSummaryEntry` |
| `OutsideSchoolLogMilestoneObjective.cs` | 옮김(같은 이름) | `OutsideSchoolLogMilestoneObjective` |
| `OutsideSchoolOutcomes.cs` | 옮김(같은 이름) | `OutsideSchoolTraitDeltaOutcome`, `OutsideSchoolRelationshipDeltaOutcome`, `OutsideSchoolStatusDeltaOutcome` |
| `OutsideSchoolRequirements.cs` | 옮김(같은 이름) | `OutsideSchoolTraitThresholdRequirement`, `OutsideSchoolSkillThresholdRequirement` |
| `OutsideSchoolSkillDeltaOutcome.cs` | 옮김(같은 이름) | `OutsideSchoolSkillDeltaOutcome` |
| `PartTimeWorkDefinition.cs` | 옮김(데이터 종류) | `PartTimeWorkDefinition` → Core `PartTimeWork` (TownLifeBuilders.cs) |
| `PartTimeWorkInteractor.cs` | Unity 컴포넌트 | 학생 생활 상호작용·진행 컴포넌트. 규칙은 Core StudentLife, 지점은 `game/world/interactions.json`, 화면은 Screens. 씬·프리팹에 9번 붙음 |
| `PartTimeWorkOutcomes.cs` | 옮김(같은 이름) | `WorkTraitDeltaOutcome`, `WorkSkillDeltaOutcome`, `WorkRelationshipDeltaOutcome`, `WorkStatusDeltaOutcome` |
| `PartTimeWorkRequirements.cs` | 옮김(같은 이름) | `WorkTraitThresholdRequirement`, `WorkSkillThresholdRequirement`, `WorkRelationshipThresholdRequirement`, `WorkStatusThresholdRequirement` |
| `PartTimeWorkRunner.Reserved.cs` | 옮김(같은 이름) | `PartTimeWorkRunner` |
| `PartTimeWorkRunner.cs` | 옮김(같은 이름) | `PartTimeWorkResult`, `PartTimeWorkRunner`, `PartTimeWorkDayLog`, `PartTimeWorkGrowthSummaryEntry` |
| `PracticeStepDefinition.cs` | 옮김(데이터 종류) | `PracticeStepDefinition` → Core `PracticeStep` (CareerBuilders.cs) |
| `RelationshipDefinition.cs` | 옮김(같은 이름) | `RelationshipDefinition` |
| `RelationshipDeltaEffect.cs` | 옮김(같은 이름) | `RelationshipDeltaEffect` |
| `RelationshipMilestoneObjective.cs` | 옮김(같은 이름) | `RelationshipMilestoneObjective` |
| `SkillDefinition.cs` | 분류됨 | `SkillDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `SkillProgressActivityEffect.cs` | 옮김(데이터 종류) | `SkillProgressActivityEffect` → Core `SkillProgressEffect` (StudentLifeBuilders.cs) |
| `SkillThresholdActivityRequirement.cs` | 옮김(이름 다름), 데이터 없음 | Core `SkillThresholdRequirement`(`LifeActivityStrategies.cs:52`). data/의 `requirements`가 모두 빈 배열 |
| `SkillThresholdCareerRequirement.cs` | 옮김(같은 이름) | `SkillThresholdCareerRequirement` |
| `StatusDefinition.cs` | 옮김(같은 이름) | `StatusDefinition` |
| `StatusDeltaEffect.cs` | 옮김(같은 이름) | `StatusDeltaEffect` |
| `StudentActivityRefusalText.cs` | 옮김(이름 다름) | Core `StudentActivityRefusal`(거절 이유 → 현지화 키) |
| `StudentDailyPlanProgress.cs` | 옮김(같은 이름) | `StudentLifeProgress` |
| `StudentDayQuestInventorySummary.cs` | 옮김(같은 이름) | `QuestSummaryEntry`, `InventoryDeltaEntry`, `RewardSummaryEntry`, `StudentDayQuestInventorySummary` |
| `StudentDayRelationshipConditionSummary.cs` | 옮김(같은 이름) | `RelationshipDeltaEntry`, `StatusDeltaEntry`, `StudentDayRelationshipConditionSummary`, `StudentDayRelationshipConditionSummaryBuilder` |
| `StudentLifeCore.cs` | 옮김(같은 이름) | `LifeActivityCategory`, `LifeActivityResultKind`, `StudentDayState`, `LifeActivityResult` |
| `StudentLifeMinigameProgress.cs` | 옮김(같은 이름) | `StudentMinigameKind`, `PendingStudentMinigameSaveData`, `StudentLifeProgress` |
| `StudentLifeSceneInteraction.cs` | Unity 컴포넌트 | 학생 생활 상호작용·진행 컴포넌트. 규칙은 Core StudentLife, 지점은 `game/world/interactions.json`, 화면은 Screens. 씬 0, 코드에서 AddComponent·참조 48파일 |
| `StudentLifeTimeBudget.cs` | 옮김(같은 이름) | `StudentLifeProgress` |
| `StudentMinigameRequestBridge.cs` | 옮김(이름 다름) | Core 학생 미니게임 서비스(`StudentMinigameServiceTests`) |
| `StudentMinigameService.cs` | 옮김(같은 이름) | `StudentMinigameService` |
| `TimeSlotDefinition.cs` | 옮김(같은 이름) | `TimeSlotDefinition` |
| `TownHelpActionDefinition.cs` | 옮김(데이터 종류) | `TownHelpActionDefinition` → Core `TownHelpAction` (TownLifeBuilders.cs) |
| `TownHelpActionInteractor.cs` | Unity 컴포넌트 | 학생 생활 상호작용·진행 컴포넌트. 규칙은 Core StudentLife, 지점은 `game/world/interactions.json`, 화면은 Screens. 씬·프리팹에 2번 붙음 |
| `TownHelpActionOutcomes.cs` | 옮김(같은 이름) | `HelpActionTraitDeltaOutcome`, `HelpActionSkillDeltaOutcome`, `HelpActionStatusDeltaOutcome`, `HelpActionCareerHintOutcome` |
| `TownHelpActionRequirements.cs` | 옮김(같은 이름) | `HelpActionTraitThresholdRequirement`, `HelpActionSkillThresholdRequirement`, `HelpActionRelationshipThresholdRequirement`, `HelpActionStatusThresholdRequirement` |
| `TownHelpActionRunner.cs` | 옮김(같은 이름) | `TownHelpActionResult`, `TownHelpActionRunner`, `TownHelpActionDayLog`, `TownHelpActionGrowthSummaryEntry` |
| `TraitDefinition.cs` | 분류됨 | `TraitDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `TraitDeltaActivityEffect.cs` | 옮김(데이터 종류) | `TraitDeltaActivityEffect` → Core `TraitDeltaEffect` (StudentLifeBuilders.cs) |
| `TraitThresholdActivityRequirement.cs` | 옮김(이름 다름), 데이터 없음 | Core `TraitThresholdRequirement`(`LifeActivityStrategies.cs:44`). 데이터 0건 |
| `TraitThresholdCareerRequirement.cs` | 옮김(같은 이름) | `TraitThresholdCareerRequirement` |
| `TutorialStageAdvanceDayEndRule.cs` | 옮김(데이터 종류) | `TutorialStageAdvanceDayEndRule` → Core `TutorialStageAdvanceRule` (StudentLifeBuilders.cs) |
| `TutorialStageDefinition.cs` | 옮김(데이터 종류) | `TutorialStageDefinition` → Core `TutorialStage` (StudentLifeBuilders.cs) |
| `WorkplaceDefinition.cs` | 옮김(데이터 종류) | `WorkplaceDefinition` → Core `Workplace` (TownLifeBuilders.cs) |

### Tiles (1)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `PlaceableTileDefinition.cs` | 분류됨 | `PlaceableTileDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |

### Time (3)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `GameClock.cs` | Unity 컴포넌트 | 시계 컴포넌트. Core `DayClock`·`GameSession`. 씬·프리팹에 1번 붙음 |
| `LifeTimePresentation.cs` | Unity 컴포넌트 | 시계 컴포넌트. Core `DayClock`·`GameSession`. 씬 0, 코드에서 AddComponent·참조 6파일 |
| `TimeDefinition.cs` | 옮김(데이터 종류) | `TimeDefinition` → Core `DayClock` (TimeBuilders.cs) |

### Tools (4)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `FishingAnimationEffect.cs` | 분류됨 | `FishingAnimationEffect` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `ToolDefinition.cs` | 분류됨 | `ToolDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `ToolEffectBase.cs` | 게임이 쓰지 않음 | 도구(`NotUsedByGame` ToolDefinition) |
| `ToolVisualMappingDefinition.cs` | 게임이 쓰지 않음 | 도구 그림(위와 같음) |

### VerticalSlice (2)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `VerticalSliceSummary.cs` | 쓰이지 않음 | UI의 `SetVerticalSliceSummary`·`AppendVerticalSliceGuide`를 부르는 곳이 0(정의만 있다). 문구도 영어로 코드에 적혀 있다 |
| `VerticalSliceSummaryBuilder.cs` | 쓰이지 않음 | 참조 0(테스트 `VerticalSliceSummaryTests`만) |

### World (14)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `AuthoredWorldScene.cs` | Unity 컴포넌트 | 씬 이동·문·스폰 컴포넌트. MonoGame 판은 Rootborn.World(LDtk 문·스폰, `maps.json`). 씬·프리팹에 10번 붙음 |
| `InteriorDoorPortalRuntimeInstaller.cs` | Unity 컴포넌트 | 씬 이동·문·스폰 컴포넌트. MonoGame 판은 Rootborn.World(LDtk 문·스폰, `maps.json`). 씬 0, 코드에서 AddComponent·참조 1파일 |
| `LdtkTownMapDefinition.cs` | 분류됨 | `LdtkTownMapDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `LdtkTownMapRuntimeBinding.cs` | Unity 컴포넌트 | 씬 이동·문·스폰 컴포넌트. MonoGame 판은 Rootborn.World(LDtk 문·스폰, `maps.json`). Unity 판에서도 안 쓰임(씬·프리팹 0, 참조 0) |
| `SurfaceTagZone.cs` | Unity 컴포넌트 | 씬 이동·문·스폰 컴포넌트. MonoGame 판은 Rootborn.World(LDtk 문·스폰, `maps.json`). 씬 0, 코드에서 AddComponent·참조 1파일 |
| `TownAutoDoorPortal.cs` | Unity 컴포넌트 | 씬 이동·문·스폰 컴포넌트. MonoGame 판은 Rootborn.World(LDtk 문·스폰, `maps.json`). 씬·프리팹에 13번 붙음 |
| `TownLevelTransitionTrigger.cs` | Unity 컴포넌트 | 씬 이동·문·스폰 컴포넌트. MonoGame 판은 Rootborn.World(LDtk 문·스폰, `maps.json`). 씬·프리팹에 2번 붙음 |
| `TownReturnViewCleanup.cs` | Unity 컴포넌트 | 씬 이동·문·스폰 컴포넌트. MonoGame 판은 Rootborn.World(LDtk 문·스폰, `maps.json`). 씬 0, 코드에서 AddComponent·참조 21파일 |
| `WorldDestinationDefinition.cs` | 옮김(데이터 종류) | `WorldDestinationDefinition` → Core `WorldDestination` (WorldBuilders.cs) |
| `WorldEntryService.cs` | 옮김(같은 이름) | `WorldEntryPoint` |
| `WorldPortal.cs` | 옮김(같은 이름) | `WorldProgressSaveData` |
| `WorldSpawnPoint.cs` | Unity 컴포넌트 | 씬 이동·문·스폰 컴포넌트. MonoGame 판은 Rootborn.World(LDtk 문·스폰, `maps.json`). 씬·프리팹에 18번 붙음 |
| `WorldTravelCatalogDefinition.cs` | 옮김(데이터 종류) | `WorldTravelCatalogDefinition` → Core `WorldTravelCatalog` (WorldBuilders.cs) |
| `WorldTravelRuntime.cs` | Unity 컴포넌트 | 씬 이동·문·스폰 컴포넌트. MonoGame 판은 Rootborn.World(LDtk 문·스폰, `maps.json`). 씬 0, 코드에서 AddComponent·참조 5파일 |

### WorldGeneration (7)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `NaturalPropSpawnDefinition.cs` | 분류됨 | `NaturalPropSpawnDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `SeededWorldGenerator.cs` | 규칙 아님 + 쓰이지 않음 | 맵 생성(`NotRules` MapGeneration). 참조 0(테스트만) |
| `TerrainGenerationDefinition.cs` | 분류됨 | `TerrainGenerationDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `TerrainReservedArea.cs` | 규칙 아님 | 맵 생성 |
| `TilePatternDefinition.cs` | 분류됨 | `TilePatternDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `TileVariantSetDefinition.cs` | 분류됨 | `TileVariantSetDefinition` — `game/Rootborn.Data/Builders/NotRules.cs`·`NotUsedByGame.cs`에 이유 |
| `WeightedTileVariant.cs` | 규칙 아님 | 맵 생성 |

### WorldState (22)

| 파일 | 판정 | 대응 / 이유 |
|---|---|---|
| `GameDataRegistryWorldStateExtensions.cs` | 다른 것으로 대체 | `GameDefinitions` 표 조회 |
| `GameDataRegistryWorldStateUsageExtensions.cs` | 다른 것으로 대체 | `GameDefinitions` 표 조회. 참조 0 |
| `WorldStateConditionBase.cs` | 옮김(같은 이름) | `WorldStateConditionContext`, `WorldStateFlagActiveCondition` |
| `WorldStateEffectBase.cs` | 옮김(같은 이름) | `WorldStateEffectContext`, `WorldStateSetObjectActiveEffect` |
| `WorldStateFlagDefinition.cs` | 옮김(데이터 종류) | `WorldStateFlagDefinition` → Core `WorldStateFlag` (WorldStateBuilders.cs) |
| `WorldStateProgress.cs` | 옮김(같은 이름) | `WorldStateProgressSaveData`, `WorldStateFlagRecordSaveData`, `WorldStateFlagRecord`, `TodayWorldChangeSummaryEntry` |
| `WorldStateProgressPersistence.cs` | 옮김(이름 다름) | Core `WorldStateSaveFiles` |
| `WorldStateScopeDefinition.cs` | 옮김(데이터 종류) | `WorldStateScopeDefinition` → Core `WorldStateScope` (WorldStateBuilders.cs) |
| `WorldStateSummaryBuilder.cs` | 옮김(같은 이름) | `WorldStateSummaryBuilder`, `WorldStateFlagLookupCache` |
| `WorldStateTypes.cs` | 옮김(같은 이름) | `WorldStateScopeKind`, `WorldStateChangeKind`, `WorldStateSummarySurface`, `WorldStateBadgeKind` |
| `WorldStateUsageActivityUnlockOutcome.cs` | 옮김(같은 이름) | `WorldStateUsageActivityUnlockOutcome` |
| `WorldStateUsageCareerHintOutcome.cs` | 옮김(같은 이름) | `WorldStateUsageCareerHintOutcome` |
| `WorldStateUsageCore.cs` | 옮김(같은 이름) | `WorldStateUsageRepeatMode`, `WorldStateUsageResultKind`, `WorldStateUsageSummarySurface`, `WorldStateUsageContext` |
| `WorldStateUsageEncyclopediaOutcome.cs` | 옮김(같은 이름) | `WorldStateUsageEncyclopediaOutcome` |
| `WorldStateUsageFlagActiveCondition.cs` | 옮김(같은 이름) | `WorldStateUsageFlagActiveCondition` |
| `WorldStateUsageInteractor.cs` | Unity 컴포넌트 | 월드 상태 사용 상호작용. Unity 판에서도 안 쓰임(씬·프리팹 0, 참조 0) |
| `WorldStateUsageOutcomes.cs` | 옮김(이름 다름) | 결과 id 상수 묶음 |
| `WorldStateUsageProgress.cs` | 옮김(같은 이름) | `WorldStateUsageProgressSaveData`, `WorldStateUsageRecordSaveData`, `WorldStateUsageRecord`, `WorldStateUsageTodaySummaryEntry` |
| `WorldStateUsageProgressPersistence.cs` | 옮김(이름 다름) | Core `WorldStateUsageSummary` |
| `WorldStateUsageQuestUnlockOutcome.cs` | 옮김(같은 이름) | `WorldStateUsageQuestUnlockOutcome` |
| `WorldStateUsageRepeatPolicyDefinition.cs` | 옮김(데이터 종류) | `WorldStateUsageRepeatPolicyDefinition` → Core `WorldStateUsageRepeatPolicy` (WorldStateBuilders.cs) |
| `WorldStateUsageSummary.cs` | 옮김(같은 이름) | `WorldStateUsageSummaryModel`, `WorldStateUsageLookupCache`, `WorldStateUsageSummaryBuilder` |

---

## 부록 B. `Assets/Tests/EditMode` 파일별 대응 (415개)

열: 파일 / 테스트 수(`[Test]`·`[UnityTest]`·`[TestCase]` 표시 수) / 판정 / 대응 또는 이유. 판정에 "(분류 규칙)"이 붙은 것은 아래 규칙으로 2.1절의 순서로 자동 분류한 것이고 작성자가 하나씩 열어 보지 않았다.

| 파일 | 수 | 판정 | 대응 / 이유 |
|---|---|---|---|
| `Addressables/AddressablesLinkXmlPreservationTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Addressables/ClientBuildAddressablesLoopGuardTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ArgsParserTests.cs` | 7 | 옮기지 않음 | Unity 실행 인자. NET-001(SCENARIOS.md 2절) |
| `Careers/AdultCareerCommandCoordinatorTests.cs` | 6 | 다른 것으로 대체 | Unity 조정자 테스트. MonoGame 판은 `GameSession`이 명령마다 저장한다: Game `PartialWriteTests`, `RewardOnceTests` |
| `Careers/AdultCareerFoundationTests.cs` | 20 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/AdultCareerTests.cs` |
| `Careers/AdultCareerRuntimeTests.cs` | 21 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `AdultCareerPersistenceRouter`, `AdultCareerProgress`, `AdultCareerProgressSaveData`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Careers/AdultCareerSnapshotTests.cs` | 14 | 대응 있음 | Game `SnapshotOwnerTests`(다른 플레이어·모르는 아이템·장비 선택, 2026-11-12 옮기며 `GameSession` 고침), `LeftoverTempFileTests`, Data `FileSaveStoreTests` |
| `Careers/CareerAchievementArtAssetTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Careers/CareerFacilityCanvasSelectionTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Careers/CenteredFacilityEntranceAssetTests.cs` | 9 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Careers/ClinicLevel1ExteriorArtTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Careers/FacilityManagementPanelTests.cs` | 26 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Careers/FacilityVisualTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Careers/FacilityWorldPresenterTests.cs` | 13 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `AdultCareerProgress`, `AdultCareerProgressSaveData`, `AdultCareerSnapshotPersistence`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Careers/InteriorWallpaperShopArtTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Careers/InventoryExchangeTests.cs` | 9 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/ItemInventoryTests.cs` |
| `Careers/MinigamePresentationBridgeTests.cs` | 6 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `ActivityTimePolicyDefinition`, `AdultCareerResult`, `AppConfig`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Careers/PartTimeWorkRegistryIdentityTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Careers/PendingFacilityWorkTests.cs` | 16 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/AdultCareerTests.cs` |
| `Careers/PlayerLifeAggregateTests.cs` | 8 | 대응 있음 | Game `SnapshotOwnerTests`(체인·가구의 다른 플레이어 거절, 버전 1 스냅샷, 2026-11-12), `PartialWriteTests` |
| `Careers/StudentMinigameReservationTests.cs` | 19 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/PartTimeWorkTests.cs`, `Tools/Rootborn.Core.Tests/StudentMinigameServiceTests.cs` |
| `Careers/StudioLevel2ExteriorArtTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Characters/GrowingCharacterInstallTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Characters/GrowingCharacterIntegrationTests.cs` | 9 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Characters/GrowingCharacterStageTests.cs` | 9 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Characters/GrowingCharacterVisualViewTests.cs` | 15 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Characters/GrowingWardrobeTests.cs` | 11 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `CharacterAppearanceDefinition`, `GameDataRegistry`, `GrowingCharacterAppearanceDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Characters/Spum/CharacterVisualViewBoundaryTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `Characters/Spum/SpumResourcesUsageGateTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Characters/Spum/SpumToolVisualMappingTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `Characters/WearableDataWiringTests.cs` | 10 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Characters/WearableLookTests.cs` | 7 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Crafting/CraftingRecipeTests.cs` | 5 | 옮기지 않음 | 레시피 만들기 단계는 게임이 쓰지 않음(`NotUsedByGame` GatherResourceStep). 레시피 공개는 Game `RecipeRevealTests` |
| `Crafting/ResourceRecipeActivitySliceTests.cs` | 6 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/ResourceGatheringTests.cs` |
| `DataManagerEditorFastPathSourceTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `DataManagerPackedRegistryTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `DayNightLightingAssetTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `DayNightLightingTests.cs` | 6 | 대응 있음 | 같은 이름: `game/tests/Rootborn.World.Tests/DayNightLightingTests.cs` |
| `Dialogue/DialogueQuestTests.cs` | 4 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/DialogueRulesTests.cs` |
| `Dialogue/NpcDialogueAuthorityTests.cs` | 5 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `AdultCareerPersistenceRouter`, `DialogueChoiceDefinition`, `DialogueCondition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Dialogue/NpcDialogueContextTests.cs` | 7 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `AppConfig`, `AuthoredWorldScene`, `DialogueChoiceContext`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Dialogue/NpcDialogueRecoveryTests.cs` | 2 | 대응 있음(일부) | 깨진 세이브로 새 상태를 만들지 않음: Game `CorruptSaveStartTests`(시작 거절, 덮어쓰지 않음). Unity 씬 소유 바인더 부분은 Unity 전용 |
| `Dialogue/NpcInteractorDialogueSelectionTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`DialogueDefinition`, `NpcDefinition`, `NpcInteractor`, `StudentLifeProgress`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Dialogue/NpcPublishedMeetingTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`AdultCareerSnapshotPersistence`, `DialogueDefinition`, `GameDataRegistry`, `LocationDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `DiscoveryClues/ClueInterpretationLoopTests.cs` | 2 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/ClueInterpretationTests.cs` |
| `DiscoveryClues/ClueInterpretationPersistenceTests.cs` | 1 | 대응 있음 | 시나리오 id 1/1개가 dotnet 테스트에 있음: `ClueInterpretationTests.cs` |
| `DiscoveryClues/ClueInterpretationRegistryTests.cs` | 1 | 대응 있음 | 시나리오 id 1/1개가 dotnet 테스트에 있음: `ClueInterpretationTests.cs` |
| `DiscoveryClues/DiscoveryClueLoopTests.cs` | 3 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/DiscoveryClueTests.cs` |
| `DiscoveryClues/DiscoveryClueRegistryTests.cs` | 2 | 대응 있음(일부) | 시나리오 id 1/2개가 dotnet 테스트에 있음: `DiscoveryClueTests.cs` |
| `Encyclopedia/EncyclopediaCoreTests.cs` | 6 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/EncyclopediaCoreTests.cs` |
| `Family/CharacterAppearanceTests.cs` | 1 | 옮기지 않음(분류 규칙) | 옮긴 규칙 형을 쓰지 않음(NotRules·NotUsedByGame 영역 또는 Unity 전용) |
| `Family/CharacterPartAnimationIntegrationSourceTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `Family/CharacterPartAnimatorTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Family/CharacterPartAssetWiringTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Family/CharacterPartComposerTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Family/CharacterPartDefinitionTests.cs` | 2 | 옮기지 않음(분류 규칙) | 옮긴 규칙 형을 쓰지 않음(NotRules·NotUsedByGame 영역 또는 Unity 전용) |
| `Family/CharacterPartMotionDefinitionTests.cs` | 20 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Family/CharacterPartRegistryTests.cs` | 2 | 대응 추정(분류 규칙) | 옮긴 규칙(`CharacterPartAnimationClipDefinition`, `DataManager`, `GameDataRegistry`, `Managers`)을 부르는 순수 테스트. 같은 이름의 dotnet 테스트는 없다(내용 대조 안 함) |
| `Family/CharacterPartResourceManagerTests.cs` | 1 | 대응 추정(분류 규칙) | 옮긴 규칙(`CommonPanel`, `Managers`, `ModernUiStyle2Sprites`, `ResourceManager`)을 부르는 순수 테스트. 같은 이름의 dotnet 테스트는 없다(내용 대조 안 함) |
| `Family/FishingAnimationAssetWiringTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Family/FishingAnimationControllerTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Family/FishingAnimationRegistryTests.cs` | 2 | 대응 추정(분류 규칙) | 옮긴 규칙(`DataManager`, `FishingAnimationDefinition`, `GameDataRegistry`, `Managers`)을 부르는 순수 테스트. 같은 이름의 dotnet 테스트는 없다(내용 대조 안 함) |
| `FishingAnimationEffectTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `FishingToolAssetWiringTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `GameClockTests.cs` | 4 | 대응 있음 | Core `DayClockTests`(STATUS-002) |
| `GatherInteractorInputBindingTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`GatherInteractor`, `PlayerController`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `GatherInteractorTests.cs` | 11 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/ResourceGatheringTests.cs` |
| `GatherInteractorToolEffectRoutingTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`GatherInteractor`, `SurfaceTagZone`, `ToolDefinition`, `ToolEffectBase`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `HeirGeneratorTests.cs` | 3 | 옮기지 않음 | HEIR-001. 게임이 쓰지 않음 |
| `House/HouseDailyPlanPromptPlacementTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `House/SchoolPrepComfyAssetTests.cs` | 8 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `House/StoryPhotoComfyAssetTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/FurniturePlacementLabelReadabilityTests.cs` | 6 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `Hud`, `Panel`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Interiors/HospitalSceneIntegrityTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/HouseComfyDecorationAssetTests.cs` | 14 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/HouseInteriorTileSetAssetTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/HouseMaterialComfyAssetTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/HousePersistentTileArtTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/HousePlacementToggleRaycastTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Interiors/InteriorDirectionalTileSetTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/InteriorFurniturePalettePagingTests.cs` | 3 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `InteriorObjectKind`, `InteriorPlacementPreference`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Interiors/InteriorFurniturePlacementUiModelTests.cs` | 4 | 옮기지 않음(분류 규칙) | 옮긴 규칙 형을 쓰지 않음(NotRules·NotUsedByGame 영역 또는 Unity 전용) |
| `Interiors/InteriorFurnitureRuntimeCatalogTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/InteriorGenerationTests.cs` | 10 | 옮기지 않음 | 실내 생성(`NotRules` MapGeneration) |
| `Interiors/InteriorLdtkMapDefinitionTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/InteriorPlacementAvailabilityTests.cs` | 3 | 옮기지 않음(분류 규칙) | 옮긴 규칙 형을 쓰지 않음(NotRules·NotUsedByGame 영역 또는 Unity 전용) |
| `Interiors/InteriorPlacementAvailabilityTextTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`FurnitureDefinition`, `FurniturePlacementDirection`, `FurniturePlacementPreference`, `FurnitureTilePart`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Interiors/InteriorPlacementFurnitureFootprintOverlayTests.cs` | 5 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`FurnitureDefinition`, `FurniturePlacementDirection`, `FurniturePlacementPreference`, `FurnitureTilePart`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Interiors/InteriorPlacementRequestTests.cs` | 4 | 옮기지 않음(분류 규칙) | 옮긴 규칙 형을 쓰지 않음(NotRules·NotUsedByGame 영역 또는 Unity 전용) |
| `Interiors/ModernInteriorsShadowlessFurnitureImporterTests.cs` | 11 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/ModernOfficeChairSampleAssetTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/ModernOfficeFurniturePainterEditorTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/ModernOfficePaletteAssetTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/PlacedFurnitureDepthPresentationTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Interiors/PlacementCameraControllerTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Interiors/QuestFurniturePlacementEventTests.cs` | 3 | 대응 있음 | Core `FurniturePlacementEventTests`(2026-11-12) |
| `Interiors/QuestInteriorUnlockRewardTests.cs` | 9 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/FurnitureUnlockTests.cs`, `Tools/Rootborn.Core.Tests/QuestConditionRewardTests.cs` |
| `Interiors/ScenePlayerRenderPolicyTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Interiors/SchoolDesignComfyAssetTests.cs` | 12 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/SchoolFloorArtAssetTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/StudentActivityArtAssetTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/TownPlayerGroundDepthSortTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Interiors/UrbanGroundArtAssetTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/UrbanStreetArtAssetTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Interiors/WheatPlanterArtTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `InventoryEquipmentRegressionCatalogTests.cs` | 3 | 대응 추정(분류 규칙) | 옮긴 규칙(`Inventory`)을 부르는 순수 테스트. 같은 이름의 dotnet 테스트는 없다(내용 대조 안 함) |
| `InventoryEquipmentScenarioTests.cs` | 10 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/ItemInventoryTests.cs` |
| `InventoryUiTests.cs` | 4 | 대응 추정(분류 규칙) | 옮긴 규칙(`ItemDefinition`, `PlayerController`, `PlayerInventory`, `PlayerToolSpriteAddresses`)을 부르는 순수 테스트. 같은 이름의 dotnet 테스트는 없다(내용 대조 안 함) |
| `KnowledgeProgressTests.cs` | 1 | 옮기지 않음 | KNOW-001. `NotUsedByGame` Knowledge |
| `KnowledgeTriggerTests.cs` | 3 | 옮기지 않음 | KNOW-002. `NotUsedByGame` Knowledge |
| `LDtk/LdtkImportValidatorTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `LifeTimeHudTests.cs` | 4 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`GameDataRegistry`, `GameDataRegistryProvider`, `GameText`, `GameTextCatalog`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `LifeTimeLightingTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`DayNightLightingProfile`, `GameClock`, `LifeTimePresentation`, `LightingManager`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `LifeTimeOwnershipTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `ManagersBootstrapConcurrencySourceTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `Minigames/CareSequenceCardSymbolTests.cs` | 8 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Minigames/CareerMinigameAssetTests.cs` | 8 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Minigames/MinigamePanelTests.cs` | 21 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `ChallengeDefinitionBase`, `ChoiceChallengeDefinition`, `MinigameDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Minigames/MinigamePropAssetTests.cs` | 9 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Minigames/MinigameSessionTests.cs` | 18 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/MinigameSessionTests.cs` |
| `Minigames/StoreOrderSymbolAssetTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ModernArtAuditTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ModernDataManagerSourceTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `ModernEditorToolSourceTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `ModernFarmAddressablesSetupTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ModernFarmDataWiringTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ModernFarmSliceSetupTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ModernHudSpriteKeysTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `ModernInteriorsAddressablesSetupTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ModernInteriorsSliceSetupTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ModernRuntimeSourceTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `ModernSociety/ModernSocietyLoopTests.cs` | 1 | 옮기지 않음 | ModernSociety는 Unity 안에서도 쓰이지 않음(1절) |
| `ModernUiAddressablesSetupTests.cs` | 7 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ModernUiInventoryPanelTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `Inventory`, `ItemCategory`, `ItemDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `ModernUiPanelAutoInstallerSourceTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `ModernUiPanelInputRouterTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `ModernUiPanelReconstructionManifestTests.cs` | 3 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Panel`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `ModernUiPanelReconstructionRuntimeTests.cs` | 1 | 대응 추정(분류 규칙) | 옮긴 규칙(`Panel`)을 부르는 순수 테스트. 같은 이름의 dotnet 테스트는 없다(내용 대조 안 함) |
| `ModernUiRecipeManifestTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ModernUiRecipeRuntimeTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `ModernUiSliceSetupTests.cs` | 7 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ModernUiSourceAuditTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `ModernUiStatusPanelTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `ModernUiTileImageTests.cs` | 8 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `CommonPanel`, `ModernUiSpriteKey`, `Panel`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Network/NetworkLocalPlayerViewSourceTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `Network/NetworkStudentLifeInteractionSourceTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `Network/NetworkTownMultiplayerInstallerSourceTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `Network/NetworkWorldTimeHudSourceTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `Network/NetworkWorldTimeStateSourceTests.cs` | 5 | 대응 있음(일부) | 시나리오 id 2/5개가 dotnet 테스트에 있음: `SharedWorldTimeTests.cs` |
| `NetworkSessionTests.cs` | 3 | 옮기지 않음(분류 규칙) | 옮긴 규칙 형을 쓰지 않음(NotRules·NotUsedByGame 영역 또는 Unity 전용) |
| `PerFrameLookupSourceTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `Placement/FurnitureCatalogRegistryTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `Placement/FurniturePlacementDomainTests.cs` | 4 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/FurniturePlacementTests.cs` |
| `Placement/FurniturePlacementRegistryTests.cs` | 4 | 대응 있음 | Core `FurniturePlacementTests`, Game `FurnitureTests` |
| `Placement/FurniturePlacementSaveTests.cs` | 6 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/FurnitureUnlockTests.cs` |
| `Placement/FurniturePlacementServiceTests.cs` | 5 | 대응 있음 | 놓기 판정은 Core `FurniturePlacementTests`, 흐름은 Game `FurnitureTests`. Tilemap 칠하기는 Unity 전용 |
| `Placement/FurnitureSpriteAutoImporterTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Placement/GrowthRoomArtAssetTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Placement/SchoolPixelAssetIntegrationTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Placement/StudentLifePixelAssetIntegrationTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Player/HarvestedWheatComfyAssetTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Player/InteractionPriorityTests.cs` | 5 | 대응 있음 | Core `InteractionTargetsTests`·`InteractionTargetRuleTests`(앞의 대상 우선, 거리) |
| `Player/InteractionPromptScreenReservationsTests.cs` | 16 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`InteractionPromptScreenReservations`, `PlayerInteractionRouter`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Player/InteractionPromptSizingTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Player/PlayerInventoryScriptAssetTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Player/PlayerRigidbodyLateBindingTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `Player/SchoolPromptAppearanceTests.cs` | 11 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `PlayerControllerTests.cs` | 5 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`CharacterPartComposer`, `CharacterPartDefinition`, `Facing`, `PlayerController`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `PlayerToolSpriteMetadataTests.cs` | 1 | 대응 추정(분류 규칙) | 옮긴 규칙(`ItemDefinition`)을 부르는 순수 테스트. 같은 이름의 dotnet 테스트는 없다(내용 대조 안 함) |
| `Portability/RegistryOnlyRuntimeProviderAssetTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Portability/RegistryOnlyRuntimeProviderBehaviorTests.cs` | 5 | 대응 추정(분류 규칙) | 옮긴 규칙(`ClueInterpretationConditionBase`, `ClueInterpretationDefinition`, `ClueInterpretationOutcomeBase`, `ClueInterpretationPolicyDefinition`)을 부르는 순수 테스트. 같은 이름의 dotnet 테스트는 없다(내용 대조 안 함) |
| `Portability/RegistryOnlyRuntimeProviderSourceAuditTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Quests/NpcQuestNetworkSyncTests.cs` | 5 | 옮기지 않음 | Netcode 동기화(프로젝트 5) |
| `Quests/PlayerGlobalStateMultiplayerTests.cs` | 13 | 대응 있음(일부) | 한 슬롯 두 플레이어: Game `TwoPlayersOneSlotTests`·`TwoPlayersInterleavedTests` |
| `Quests/QuestConditionTests.cs` | 3 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestConditionRewardTests.cs` |
| `Quests/QuestEventAdapterTests.cs` | 1 | 대응 있음 | 시나리오 id 1/1개가 dotnet 테스트에 있음: `QuestLogTests.cs`, `QuestObjectiveTests.cs` |
| `Quests/QuestNetworkPlayerIsolationTests.cs` | 1 | 대응 있음(일부) | 플레이어별 퀘스트 기록: Game `TwoPlayersOneSlotTests`. 네트워크 동기화 부분은 프로젝트 5 |
| `Quests/QuestObjectiveTests.cs` | 16 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestChainTests.cs`, `Tools/Rootborn.Core.Tests/QuestLogTests.cs`, `Tools/Rootborn.Core.Tests/QuestObjectiveTests.cs` |
| `Quests/QuestProgressTests.cs` | 5 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestLogTests.cs` |
| `Quests/QuestRegistryTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Quests/QuestRewardTests.cs` | 11 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestChainTests.cs`, `Tools/Rootborn.Core.Tests/QuestConditionRewardTests.cs`, `Tools/Rootborn.Core.Tests/QuestLogTests.cs` |
| `Quests/QuestSaveTests.cs` | 2 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestLogTests.cs` |
| `Quests/QuestUiTests.cs` | 3 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `DialogueChoiceDefinition`, `DialogueDefinition`, `DialoguePanel`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Registry/GameDataRegistryBrokenReferenceTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Registry/GameDataRegistryProviderTests.cs` | 5 | 옮기지 않음(분류 규칙) | Addressables·`Managers` 수명 주기 |
| `Save/SaveSlotSelectPanelTests.cs` | 6 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`CharacterCustomization`, `Facing`, `SaveSlotMetadata`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `Save/SaveSlotServiceTests.cs` | 11 | 대응 있음 | Data `SaveSlotMetadataFileTests`(2026-11-12), `FileSaveStoreTests`, Screens `CharacterCreatorScreenTests`·`SaveSlotScreenTests`, Core `WorldSlotTests`. 4절 2번 |
| `Scenarios/ScenarioCoverageMappingTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `Scenarios/StudentLifeScenarioRegistryCoverageGateTests.cs` | 8 | 대응 있음(일부) | 시나리오 id 1/12개가 dotnet 테스트에 있음: `ActivityStatusAndRecipeTests.cs` |
| `SessionApprovalTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity Netcode. 멀티플레이는 프로젝트 5에서 새로 만든다 |
| `StaticRuntimeUsageGateTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StatusValueTests.cs` | 3 | 옮기지 않음 | STATUS-001 일부. `NotUsedByGame` StatusEffectDefinition |
| `SteamPlatformTests.cs` | 7 | 옮기지 않음(분류 규칙) | 옮긴 규칙 형을 쓰지 않음(NotRules·NotUsedByGame 영역 또는 Unity 전용) |
| `StudentLife/CampaignLoopTests.cs` | 2 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/CampaignTests.cs` |
| `StudentLife/CareerCandidateDeepeningLoopTests.cs` | 9 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/CareerCandidateTests.cs` |
| `StudentLife/CareerCandidateRegistryAssetTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/CareerHintDailyResetTests.cs` | 3 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/CareerCandidateTests.cs` |
| `StudentLife/CareerInterestPersistenceComponentTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`CareerInterestProgressComponent`, `CareerInterestProgressSaveData`, `Data`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `StudentLife/CareerInterestQuestLinkPersistenceTests.cs` | 1 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestLogTests.cs` |
| `StudentLife/CareerInterestRegistryAssetTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/CareerInterestSelectionLoopTests.cs` | 6 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/CareerInterestTests.cs` |
| `StudentLife/CareerPracticeDailyPlanRouteAssetWiringTests.cs` | 8 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/DailyEventLoopTests.cs` | 4 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/DailyEventTests.cs` |
| `StudentLife/DailyEventRegistryTests.cs` | 2 | 대응 있음(일부) | 시나리오 id 1/2개가 dotnet 테스트에 있음: `DailyEventTests.cs` |
| `StudentLife/DailyEventResultSummaryTests.cs` | 1 | 대응 있음 | 시나리오 id 1/1개가 dotnet 테스트에 있음: `DailyEventTests.cs` |
| `StudentLife/DailyPlanFacilityRouteAssetTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/DailyPlanProgressComponentTests.cs` | 3 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`DailyPlanProgress`, `DailyPlanProgressComponent`, `DailyPlanProgressPersistence`, `DailyPlanRouteDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `StudentLife/DailyPlanRegistryTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/DailyPlanRouteContextTests.cs` | 6 | 대응 있음 | Core `DailyPlanRouteTests`·`UserDecisions20261111Tests`(월드 상태 조건) |
| `StudentLife/DailyPlanRouteRunnerTests.cs` | 10 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/DailyPlanRouteTests.cs` |
| `StudentLife/DailyPlanRouteUnlockAssetWiringTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/DailyPlanRouteUnlockSecondSliceAssetWiringTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/DailyPlanScheduleIntegrationTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/DailyPlanSchedulePersistenceTests.cs` | 11 | 대응 있음 | Core `DailyPlanRouteTests`(`WrongEmbeddedIdentity_…`, `StudentNextDay_…`), `DailyPlanScheduleTests`, Game `SnapshotOwnerTests`(게임 진행이 다른 플레이어 일과를 덮어 읽던 것 고침, 2026-11-12), `TwoPlayersInterleavedTests`. 조정자·컴포넌트 묶기는 `GameSession`이 대신함 |
| `StudentLife/DailyPlanScheduleTests.cs` | 15 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/DailyPlanScheduleTests.cs` |
| `StudentLife/DailyPlanTypedCompletionTests.cs` | 7 | 대응 있음 | Core `DailyPlanRouteTests`(Typed 완료 기록) |
| `StudentLife/ExplorationDismissChoiceTests.cs` | 10 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/ExplorationInteractionChoiceTests.cs` | 5 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/ExplorationTests.cs` |
| `StudentLife/FirstGuideRelationshipMilestoneSliceTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/GameTextCatalogTests.cs` | 7 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/LocationActivityVisitQuestEventTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`LocationActivityInteractor`, `LocationDefinition`, `LocationIdentityDefinition`, `QuestEvent`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `StudentLife/LocationIdentityGameplayTests.cs` | 4 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/LocationActivityTests.cs` |
| `StudentLife/LocationStateConditionStrategyTests.cs` | 1 | 대응 있음 | 시나리오 id 1/1개가 dotnet 테스트에 있음: `LocationStateTests.cs` |
| `StudentLife/LocationStateSystemTests.cs` | 4 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/LocationStateTests.cs` |
| `StudentLife/MainStoryOpeningAssetTests.cs` | 74 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/NpcScheduleLifePatternTests.cs` | 4 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/NpcScheduleTests.cs` |
| `StudentLife/OpenEndedMilestoneGrowthTests.cs` | 9 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/MilestoneTests.cs` |
| `StudentLife/OpeningStoryAddressableTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/OutsideSchoolGrowthFoundationTests.cs` | 8 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/OutsideSchoolActivityTests.cs` |
| `StudentLife/OutsideSchoolInteractorRequestPersistenceTests.cs` | 6 | 대응 있음 | Core `OutsideSchoolActivityTests`(같은 이름 4개), Game `SelfStudyAndClassTests.FreePath004` |
| `StudentLife/PartTimeWorkGrowthTests.cs` | 7 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/PartTimeWorkTests.cs` |
| `StudentLife/QuestChainBranchingTests.cs` | 15 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestChainTests.cs` |
| `StudentLife/QuestChainDailyPlanRouteRewardAssetWiringTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `StudentLife/QuestChainOwnedStoreTests.cs` | 2 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestChainProgressStoreTests.cs` |
| `StudentLife/QuestChainOwnerLeaseTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `AppConfig`, `GameBootstrap`, `GameDataRegistry`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `StudentLife/QuestChainProgressStoreTests.cs` | 6 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestChainProgressStoreTests.cs` |
| `StudentLife/QuestChainRegistryAssetTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/QuestChainSessionEventTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`AdultCareerCommandCoordinator`, `AdultCareerProgress`, `AdultCareerResult`, `AdultCareerResultKind`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `StudentLife/QuestChainStateTests.cs` | 8 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestChainTests.cs` |
| `StudentLife/QuestGameplayEventAdapterTests.cs` | 6 | 대응 있음(일부) | Game `RefusedActionQuestTests`(거절·중복은 퀘스트를 진행시키지 않고 쓰지 않음, 2026-11-12). 성공 때의 정확한 이벤트는 메인 체인 테스트가 간접으로만 본다 |
| `StudentLife/QuestGameplayObjectiveTests.cs` | 5 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestObjectiveTests.cs` |
| `StudentLife/RegistryTestBinder.cs` | 0 | 옮기지 않음(분류 규칙) | 옮긴 규칙 형을 쓰지 않음(NotRules·NotUsedByGame 영역 또는 Unity 전용) |
| `StudentLife/SecondNpcRelationshipRouteTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/SelfStudyLibraryGrowthTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/StatusActivityChainTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/StudentActivityRefusalTextTests.cs` | 1 | 대응 있음 | Core `StudentDaySummaryAndRefusalTests` |
| `StudentLife/StudentDayProgressTests.cs` | 7 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/StudentDayProgressTests.cs` |
| `StudentLife/StudentDayQuestInventorySummaryTests.cs` | 2 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/StudentDayQuestInventorySummaryTests.cs` |
| `StudentLife/StudentLifeActivityTests.cs` | 8 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/CareerPracticeTests.cs`, `Tools/Rootborn.Core.Tests/StudentLifeActivityTests.cs` |
| `StudentLife/StudentLifeCalendarAssetTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/StudentLifeCareerPracticeNetworkTests.cs` | 3 | 옮기지 않음 | Netcode 동기화(프로젝트 5) |
| `StudentLife/StudentLifeCareerPracticeTests.cs` | 8 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/CareerPracticeTests.cs` |
| `StudentLife/StudentLifeNetworkTests.cs` | 6 | 옮기지 않음 | Netcode 동기화(프로젝트 5) |
| `StudentLife/StudentLifeProgressIdentityTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `StudentLife/StudentLifeRegistryTests.cs` | 1 | 대응 추정(분류 규칙) | 옮긴 규칙(`GameDataRegistry`)을 부르는 순수 테스트. 같은 이름의 dotnet 테스트는 없다(내용 대조 안 함) |
| `StudentLife/StudentLifeTimeBudgetUiTests.cs` | 5 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `ActivityTimePolicyDefinition`, `Button`, `GameDataRegistry`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `StudentLife/StudentLifeTimePointTests.cs` | 17 | 대응 있음 | Core `StudentLifeTimePointBudgetTests`(2026-11-12), `StudentLifeActivityTests`, Game `SelfStudyAndClassTests.FreePath004` |
| `StudentLife/StudentLifeTraitActivityNetworkTests.cs` | 2 | 옮기지 않음 | Netcode 동기화(프로젝트 5) |
| `StudentLife/StudentLifeTraitActivitySourceAuditTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `StudentLife/StudentLifeTraitActivityTests.cs` | 7 | 옮기지 않음 | 합성 정의(`CreateLifestyleSet`)로 선택지 성장을 본다. 같은 규칙은 Core `StudentLifeActivityTests.Choice_…`·`LIFE_STUDENT_007_…`. data/의 활동은 선택지가 0개 |
| `StudentLife/StudentRelationshipConditionTests.cs` | 6 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/StudentDaySummaryAndRefusalTests.cs` |
| `StudentLife/StudentTutorialStoryRegistryTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/StudentTutorialStoryStageTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `StudentLife/TownExplorationDiscoveryTests.cs` | 6 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/DiscoveryTests.cs` |
| `StudentLife/TownHelpActionGrowthTests.cs` | 7 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/TownHelpActionTests.cs` |
| `StudentLife/TownHelpRequestIdentityTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `StudentLife/TownLocationNpcFoundationTests.cs` | 4 | 대응 있음(일부) | 시나리오 id 2/4개가 dotnet 테스트에 있음: `LocationActivityTests.cs` |
| `StudentLife/WorkPointRolePixelArtTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `ToolDefinitionStrategyTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `ToolResourceMatchingTests.cs` | 10 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/ResourceGatheringTests.cs` |
| `TownConcept/ModernUiStyle2IconCatalogTests.cs` | 5 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`CommonPanel`, `Entry`, `ModernUISpriteAddresses`, `ModernUiStyle2Icon`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `TownConcept/ModernUiStyle2RecipeTests.cs` | 10 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `TownConcept/ModernUiStyle2RuntimeSourceAuditTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `TownConcept/TownConceptDomainMappingTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `TownConcept/TownConceptSourceAuditTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `TownConcept/TownDefaultFlowSourceAuditTests.cs` | 8 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `TownConcept/TownSavedTilemapSceneTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/CareerCandidatePanelTests.cs` | 1 | 대응 있음 | 시나리오 id 1/1개가 dotnet 테스트에 있음: `CareerCandidateTests.cs` |
| `UI/CareerInterestDayResultPanelTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`CareerCandidateDefinition`, `CareerCandidateEvaluationContext`, `CareerCandidateProgress`, `CareerCandidateRequirementBase`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/CareerInterestPanelTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `CareerCandidateDefinition`, `CareerCandidateEvaluationContext`, `CareerCandidateProgress`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/CareerPanelPolishTests.cs` | 5 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `CareerCandidateDefinition`, `CareerCandidateProgress`, `CareerInterestDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/CareerShortcutPresentationTests.cs` | 4 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `GameText`, `Runner`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/LocationStateRecordPresentationTests.cs` | 8 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`DiscoveryDefinition`, `GameDataRegistry`, `GameDataRegistryProvider`, `GameText`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/LocationStateUiSurfaceTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`GameDataRegistry`, `GameDataRegistryProvider`, `LocationStateTodaySummarySaveData`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/MainMenu/UrbanMenuPresentationTests.cs` | 3 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `CharacterCustomization`, `SaveService`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/Modern/DialoguePanelRecipeTests.cs` | 4 | 대응 추정(분류 규칙) | 옮긴 규칙(`DialoguePanel`, `ModernUiStyle2Sprites`)을 부르는 순수 테스트. 같은 이름의 dotnet 테스트는 없다(내용 대조 안 함) |
| `UI/Modern/EncyclopediaReferenceBookTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/FacilityUpgradeBlueprintIconTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/InventoryUiFilterTests.cs` | 10 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanDailyPlannerArtTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanInventoryBackpackTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanInventoryBrowseBindingTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `Inventory`, `ItemDefinition`, `PlayerGlobalState`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/Modern/UrbanInventoryBrowseTests.cs` | 13 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanInventoryCategoryArtTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanInventoryControlArtTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanInventoryDetailCloseTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanInventoryIconFallbackTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanInventoryItemPresentationTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanInventoryLayoutTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Inventory`, `Scrollbar`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/Modern/UrbanInventoryStackCountLayoutTests.cs` | 3 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Inventory`, `ItemDefinition`, `PlayerInventory`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/Modern/UrbanLifeIconAssetsTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanMoldingAssetTests.cs` | 7 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanStudentStatusArtTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanStudentStatusPanelTests.cs` | 9 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanUiIconTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Modern/UrbanUiSurfaceTests.cs` | 4 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `DialogueChoiceDefinition`, `DialogueDefinition`, `DialoguePanel`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/Modern/UrbanUiThemeLifecycleTests.cs` | 2 | 옮기지 않음(분류 규칙) | Addressables·`Managers` 수명 주기 |
| `UI/Modern/UrbanUiThemeRenderingTests.cs` | 8 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `SlotPanel`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/Objectives/DailyPlanObjectiveSourceTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `UI/Objectives/DailyPlanRouteHintObjectiveSourceTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `UI/Objectives/DailyPlanScheduleJournalTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Objectives/ObjectiveJournalDailyPlanIntegrationHardeningTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Objectives/ObjectiveJournalPanelStrictScalingTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/Objectives/ObjectiveJournalPanelTests.cs` | 14 | 대응 있음 | 시나리오 id 1/1개가 dotnet 테스트에 있음: `MomQuestTests.cs` |
| `UI/Objectives/ObjectiveJournalProviderTests.cs` | 7 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `Button`, `SaveService`, `SaveSlotMetadata`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/Objectives/ObjectiveJournalRuntimeProviderTests.cs` | 13 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Objectives/ObjectiveJournalSourceCatalogTests.cs` | 4 | 옮기지 않음(분류 규칙) | 옮긴 규칙 형을 쓰지 않음(NotRules·NotUsedByGame 영역 또는 Unity 전용) |
| `UI/Objectives/ObjectiveJournalSourceCollisionContractTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Objectives/ObjectiveJournalSourceRegressionMatrixTests.cs` | 1 | 옮기지 않음(분류 규칙) | 옮긴 규칙 형을 쓰지 않음(NotRules·NotUsedByGame 영역 또는 Unity 전용) |
| `UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Objectives/StatusActivityObjectiveSourceTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 소스 문자열 검사(코드 모양을 지킴). Unity 코드와 함께 사라진다 |
| `UI/Objectives/StudentDayGuidePresentationTests.cs` | 6 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`AdultCareerProgress`, `AdultCareerProgressSaveData`, `CareerDefinition`, `GameDataRegistry`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/Objectives/TrackedObjectiveModalVisibilityTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `UI/OpeningStoryLocalizedUiTests.cs` | 4 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`DialogueDefinition`, `DialoguePanel`, `GameDataRegistry`, `GameDataRegistryProvider`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/QuestChainCurrentStepUiTests.cs` | 7 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/QuestChainLogPanelTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `QuestChainDefinition`, `QuestChainLog`, `QuestChainState`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/QuestChainOwnedPanelCommandTests.cs` | 3 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`QuestChainDefinition`, `QuestChainLog`, `QuestChainState`, `QuestStepDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/SchoolSupportPixelImportTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/Spum/SaveSlotCreatorModalTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `SaveService`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/Spum/SpumCharacterCreatorPanelTests.cs` | 8 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `CharacterAppearanceDefinition`, `CharacterAppearancePartSelection`, `CharacterAppearanceSnapshot`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/AlleyExplorationCratePixelArtTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/CampaignDefaultSelectionTests.cs` | 2 | 대응 추정(분류 규칙) | 옮긴 규칙(`CampaignDefinition`, `GameDataRegistry`, `GameDataRegistryProvider`)을 부르는 순수 테스트. 같은 이름의 dotnet 테스트는 없다(내용 대조 안 함) |
| `UI/StudentLife/CareerPracticeResultVocabularyTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/DailyEventLocationQueueTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`DailyEventAvailabilityRuleBase`, `DailyEventChoiceDefinition`, `DailyEventDefinition`, `DailyEventProgressComponent`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/DailyEventPresentationTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/DailyPlanDayEndInteractorTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/DailyPlanDayResultPanelTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`GameText`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/DailyPlanLocationIdentityPanelTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `DailyPlanProgressComponent`, `DailyPlanRouteDefinition`, `DailyPlanRouteOutcomeBase`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/DailyPlanPanelTests.cs` | 5 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`DailyPlanProgressComponent`, `DailyPlanRouteDefinition`, `DailyPlanRouteOutcomeBase`, `DailyPlanRouteRequirementBase`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/DailyPlanRuntimeInstallerTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/DailyPlanScheduleUiTests.cs` | 7 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `DailyPlanProgressComponent`, `DailyPlanRouteDefinition`, `DailyPlanRouteSetDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/ExplorationAndStoryTextPresentationTests.cs` | 25 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/ExplorationChoicePanelOwnershipTests.cs` | 8 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/ExplorationTextLocalizationTests.cs` | 13 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/ExplorationVisualLayoutTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/FirstGuideHelpStandV2PixelArtTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/HouseAndWorkshopComfyArtTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/LibraryBookStationArtTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/LibraryStudyDeskArtTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/LocationActivityModalPresentationTests.cs` | 9 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `DiscoveryDefinition`, `LocationActivityDefinition`, `LocationDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/LocationActivityNarrativeResultTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`GameText`, `LocationActivityDefinition`, `LocationDefinition`, `LocationGrowthRoute`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/NeighborRequestBoxArtTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/OutsideSchoolPromptReadabilityTests.cs` | 10 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/QuestLocationActivityBridgeTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`LocationActivityDefinition`, `LocationDefinition`, `LocationGrowthRoute`, `LocationIdentityDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/SchoolChalkboardMoldingArtTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/StudentDayEndPromptLocalizationTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`GameDataRegistry`, `GameDataRegistryProvider`, `GameText`, `GameTextCatalog`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/StudentDayResultLocalizationTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`GameDataRegistry`, `GameDataRegistryProvider`, `GameText`, `GameTextCatalog`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/StudentDayResultPresentationTests.cs` | 5 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `StudentDaySummary`, `UiGameplayInputLock`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/StudentLife/TownDailyPlanBoardArtTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/TownSquareCivicBoardArtTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/StudentLife/UrbanInterestEntryTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `UI/TownReturnUtilityEntryTests.cs` | 2 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Tab`, `TownReturnViewCleanup`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/TownUiObstructionTests.cs` | 3 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `GameDataRegistry`, `Panel`, `PlayerGlobalState`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/UrbanGameplayHudLayoutTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `UI/UrbanGrowthPresentationTests.cs` | 7 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `CareerInterestDefinition`, `CareerInterestProgress`, `DailyPlanRouteDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/UrbanHudHostLayoutTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `UI/UrbanHudPresentationTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `UI/UrbanLifeVitalsHudTests.cs` | 3 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`LifeTimePresentation`, `StudentLifeProgress`, `StudentLifeProgressComponent`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/UrbanUtilityMenuPresentationTests.cs` | 4 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `GameText`, `UiGameplayInputLock`, `WorldStateSummaryModel`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/UtilityModalReadabilityTests.cs` | 10 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`Button`, `EncyclopediaCategoryDefinition`, `EncyclopediaEntryDefinition`, `EncyclopediaIndex`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/UtilityTextDataAssetTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `UI/WorldStateChangeCardTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`GameDataRegistry`, `GameDataRegistryProvider`, `GameText`, `WorldStateBadgeKind`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/WorldStateUiSurfaceTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`WorldStateBadgeKind`, `WorldStateChangeKind`, `WorldStateScopeKind`, `WorldStateSummaryModel`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UI/WorldStateUsageUiSurfaceTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`WorldStateUsageSummaryModel`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `UISpriteAddressesTests.cs` | 3 | 옮기지 않음(분류 규칙) | Addressables·`Managers` 수명 주기 |
| `VerticalSlice/VerticalSliceSummaryTests.cs` | 6 | 옮기지 않음 | VerticalSlice 요약을 화면에 넘기는 곳이 0(1절) |
| `Visual/GifWorldStyleCuratedAssetsTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Visual/GifWorldStyleMainSceneExpansionTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Visual/TownGifWorldStyleFullGroundTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `Visual/TownGifWorldStyleSliceTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `World/AuthoredBootstrapSafetyTests.cs` | 3 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`AuthoredWorldScene`, `Panel`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `World/AuthoredGenerationPolicyTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `World/AuthoredHouseFurnitureReplayTests.cs` | 8 | 대응 있음 | Core `FurniturePlacementTests`(FurnitureReplay를 부름). Unity 씬 소유 부분은 Unity 전용 |
| `World/AuthoredHousePlacementCaptureTests.cs` | 7 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/FurniturePlacementTests.cs` |
| `World/AuthoredHousePlacementIntegrationTests.cs` | 14 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`AuthoredHouseFurnitureSession`, `AuthoredHousePlacementModel`, `AuthoredWorldScene`, `FurnitureDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `World/AuthoredHousePlacementOwnerTests.cs` | 5 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`AdultCareerPersistenceRouter`, `AuthoredFurnitureReplayEntry`, `AuthoredHouseFurnitureReplay`, `AuthoredHouseFurnitureSession`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `World/AuthoredPlacementCameraTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `World/AuthoredSceneUiEligibilityTests.cs` | 12 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `World/AuthoredWorldIntegrationTests.cs` | 12 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`AuthoredWorldScene`, `DiscoveryDefinition`, `GameDataRegistry`, `LocationDefinition`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `World/AuthoredWorldTimeHudTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `World/CareerDistrictAssetTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `World/HouseSceneAssetTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `World/LegacyDestinationPlaceholderTests.cs` | 6 | 옮기지 않음(분류 규칙) | Unity Netcode. 멀티플레이는 프로젝트 5에서 새로 만든다 |
| `World/LegacyPlayerIdentityReinforcementTests.cs` | 2 | 옮기지 않음(분류 규칙) | Unity 컴포넌트·UI만 다룸(옮긴 규칙 형을 쓰지 않음). 화면은 Screens·Host 테스트가 따로 맡는다 |
| `World/SaveSlotWorldEntryTests.cs` | 6 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `GameDataRegistry`, `GameDataRegistryProvider`, `SaveService`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `World/SchoolSceneAssetTests.cs` | 5 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `World/TownAuthoredBindingsAssetTests.cs` | 4 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `World/TownHomeReturnClearanceTests.cs` | 13 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `World/TownLdtkMapDefinitionTests.cs` | 7 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `World/WorldAuthoredOwnerEntryTests.cs` | 4 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `AuthoredWorldScene`, `GameDataRegistry`, `GameDataRegistryProvider`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `World/WorldBaselineEntryTests.cs` | 6 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `AuthoredWorldScene`, `CameraFollow`, `GameDataRegistry`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `World/WorldDestinationSceneSetupTests.cs` | 1 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `World/WorldEntryRoutingTests.cs` | 12 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`CameraFollow`, `GameDataRegistry`, `GameDataRegistryProvider`, `PlayerIdentity`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `World/WorldLevelTransitionRoutingTests.cs` | 5 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`GameDataRegistry`, `GameDataRegistryProvider`, `PlayerController`, `PlayerIdentity`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `World/WorldTransferLeaseTests.cs` | 11 | 옮기지 않음 | Unity 씬 이동 중 플레이어 객체 빌려주기. MonoGame 판은 씬이 없고 World가 맵을 바꾼다 |
| `World/WorldTravelCatalogAssetTests.cs` | 3 | 옮기지 않음(분류 규칙) | Unity 에셋·씬·주소 검사. MonoGame 판의 데이터 검사는 Data `RealDataTests`와 데이터 온전성 검사(`4f72c7bae`) |
| `World/WorldTravelOwnerAdapterTests.cs` | 5 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `AdultCareerSnapshotPersistence`, `AppConfig`, `GameBootstrap`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `World/WorldTravelRuntimeTests.cs` | 8 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `CameraFollow`, `GameDataRegistry`, `GameDataRegistryProvider`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `World/WorldTravelStudentRestoreTests.cs` | 1 | 대응 추정(분류 규칙) | Unity 컴포넌트·UI를 거쳐 옮긴 규칙(`ActiveSaveContext`, `AppConfig`, `GameBootstrap`, `GameDataRegistry`)을 부른다. 규칙 자체는 Core·Game 테스트가 맡는다고 보았다(내용 대조 안 함) |
| `WorldGeneration/SeededWorldGeneratorTests.cs` | 11 | 옮기지 않음 | 맵 생성은 규칙이 아님. 맵은 LDtk로 새로 그림 |
| `WorldState/WorldStateCoreTests.cs` | 5 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/QuestConditionRewardTests.cs`, `Tools/Rootborn.Core.Tests/WorldStateCoreTests.cs` |
| `WorldState/WorldStatePersistenceTests.cs` | 1 | 대응 있음 | 시나리오 id 1/1개가 dotnet 테스트에 있음: `WorldStateCoreTests.cs` |
| `WorldState/WorldStateRegistryTests.cs` | 2 | 대응 있음 | 시나리오 id 1/1개가 dotnet 테스트에 있음: `WorldStateCoreTests.cs` |
| `WorldState/WorldStateUsageLoopTests.cs` | 5 | 대응 있음 | 옮겨 온 곳: `Tools/Rootborn.Core.Tests/WorldStateUsageTests.cs` |
| `WorldState/WorldStateUsageRegistryAssetTests.cs` | 1 | 대응 있음 | 시나리오 id 1/1개가 dotnet 테스트에 있음: `WorldStateUsageTests.cs` |

---

## 부록 C. `Assets/Tests/PlayMode` 파일별 (76개)

대응은 `game/tests/Rootborn.Game.Tests/SCENARIOS.md` 1절에 있다(여기에 다시 쓰지 않는다). 열: 파일 / 테스트 수 / SCENARIOS.md에서 찾은 시나리오 id 또는 묶음 행.

| 파일 | 수 | SCENARIOS.md 행 |
|---|---|---|
| `BootSmokeTest.cs` | 1 | 파일 이름으로 있음 |
| `Careers/FacilityWorldPresenterLifecycleTests.cs` | 1 | FACILITY_WORLD_PLAY_001 |
| `Characters/GrowingCharacterPerformanceTests.cs` | 1 | GROW_PERF_001 |
| `Characters/GrowingCharacterPlayModeTests.cs` | 2 | GROW_PLAY_001 |
| `Characters/GrowingWardrobeMouseTests.cs` | 3 | GROW_PLAY_003, GROW_PLAY_005 |
| `DayNightLightingPlayModeTests.cs` | 3 | VISUAL_DAYNIGHT_001 |
| `Encyclopedia/EncyclopediaPanelPlayModeTests.cs` | 1 | ENCYCLOPEDIA_E2E_003_004_005_006_009 |
| `EndToEnd/AdultCareerE2EScenarioTests.cs` | 4 | ADULT_E2E_001, ADULT_E2E_002, ADULT_E2E_003, ADULT_E2E_004 |
| `EndToEnd/BootToGameEndToEndTests.cs` | 2 | BOOT_E2E_002, SAVE_E2E_001 |
| `EndToEnd/BootToPoliceInteriorE2ETests.cs` | 1 | BOOT_FLOW_PM_001 |
| `EndToEnd/CareerCandidateDeepeningLoopE2EScenarioTests.cs` | 1 | CAREER_001 |
| `EndToEnd/EncyclopediaDiscoveryUiE2EScenarioTests.cs` | 1 | ENCYCLOPEDIA_E2E_001_008 |
| `EndToEnd/ExplorationInteractionChoiceE2EScenarioTests.cs` | 1 | EXPLORATION_CHOICE_E2E_005_011 |
| `EndToEnd/FirstGuideRelationshipMilestoneE2EScenarioTests.cs` | 4 | QUEST_DECORATE_E2E_001, QUEST_DIALOGUE_E2E_001, QUEST_WOOD_E2E_001, REL_001 |
| `EndToEnd/HousePlacementSaveLoadE2EScenarioTests.cs` | 1 | HOUSE_FLOW_001, HOUSE_FLOW_001_003 |
| `EndToEnd/KnowledgeStatusActivityChainE2EScenarioTests.cs` | 1 | KNOWLEDGE_STATUS_E2E_001 |
| `EndToEnd/MainChainOpeningE2EScenarioTests.cs` | 12 | MAIN_E2E_000, MAIN_E2E_001, MAIN_E2E_002, MAIN_E2E_003, MAIN_E2E_004, MAIN_E2E_005, MAIN_E2E_006, MAIN_E2E_007, MAIN_E2E_008, MAIN_E2E_009, MAIN_E2E_010, MAIN_E2E_011 |
| `EndToEnd/MainMenuPointerInputE2ETests.cs` | 1 | BOOT_E2E_003 |
| `EndToEnd/ObjectiveJournalRegressionE2EScenarioTests.cs` | 1 | OBJECTIVE_JOURNAL_SOURCE_COLLISION_PM_001 |
| `EndToEnd/PartTimeWorkGrowthE2EScenarioTests.cs` | 2 | JOB_001, JOB_002 |
| `EndToEnd/RegistryProviderUnifiedFlowE2ETests.cs` | 2 | REGISTRY_PROVIDER_002, REGISTRY_PROVIDER_E2E_001 |
| `EndToEnd/SelfStudyLibraryGrowthE2EScenarioTests.cs` | 2 | FREE_PATH_001, FREE_PATH_004 |
| `EndToEnd/SpumCharacterCreatorNewGameE2ETests.cs` | 5 | CREATOR_001 |
| `EndToEnd/StudentDayResultLoopE2EScenarioTests.cs` | 1 | DAY_END_001 |
| `EndToEnd/Support/E2EDriverContractTests.cs` | 5 | **없음** — PlayMode 테스트 도우미(`PlayerFlowE2EDriver`)의 계약 테스트. Unity 전용이고 MonoGame 판은 Host `PlayerDriver`가 같은 일을 한다 |
| `EndToEnd/Support/ObjectiveJournalE2EDriver.cs` | 0 | — (테스트 없음: 도우미 파일) |
| `EndToEnd/Support/PlayerFlowE2EDriver.cs` | 0 | — (테스트 없음: 도우미 파일) |
| `EndToEnd/Support/ScreenshotEvidence.cs` | 0 | — (테스트 없음: 도우미 파일) |
| `EndToEnd/TownAutoBootstrapInteractionRegressionTests.cs` | 2 | TOWN_AUTOBOOT_001, TOWN_PAINTED_COLLISION_001 |
| `EndToEnd/TownExplorationDiscoveryE2EScenarioTests.cs` | 2 | TOWN_POINT_001 |
| `EndToEnd/TownExplorationInteractionChoiceE2EScenarioTests.cs` | 1 | EXPLORATION_CHOICE_E2E_001_011 |
| `EndToEnd/TownHelpActionGrowthE2EScenarioTests.cs` | 1 | HELP_001 |
| `EndToEnd/TownResourceRecipeActivityE2EScenarioTests.cs` | 1 | TOWN_RESOURCE_RECIPE_E2E_001 |
| `EndToEnd/WorldTravelContinueE2EScenarioTests.cs` | 1 | TRAVEL_E2E_001 |
| `GlobalSceneUiPlayModeTests.cs` | 1 | GLOBAL_UI_PM_001 |
| `HouseSceneBootTests.cs` | 2 | 묶음 행: HouseScene_* (2건) |
| `InputSystemUiModulePlayModeTestGuard.cs` | 0 | — (테스트 없음: 도우미 파일) |
| `Interiors/FurniturePlacementMouseFlowPlayModeTests.cs` | 3 | 묶음 행: Interiors/* (37건) |
| `Interiors/GroundDepthSceneScopePlayModeTests.cs` | 2 | 묶음 행: Interiors/* (37건) |
| `Interiors/HouseFurniturePlacementVisualCleanupPlayModeTests.cs` | 1 | 묶음 행: Interiors/* (37건) |
| `Interiors/HouseInteriorFurniturePlacementPlayModeTests.cs` | 9 | 묶음 행: Interiors/* (37건) |
| `Interiors/HouseInteriorPlacementAvailabilityPlayModeTests.cs` | 3 | 묶음 행: Interiors/* (37건) |
| `Interiors/HouseInteriorPlacementUiPlayModeTests.cs` | 1 | 묶음 행: Interiors/* (37건) |
| `Interiors/HousePlacementCameraControlPlayModeTests.cs` | 3 | 묶음 행: Interiors/* (37건) |
| `Interiors/HousePlacementModeTogglePlayModeTests.cs` | 1 | 묶음 행: Interiors/* (37건) |
| `Interiors/HouseUtilityMenuVisibilityPlayModeTests.cs` | 2 | 묶음 행: Interiors/* (37건) |
| `Interiors/ModernInteriorsShadowlessHousePlacementPlayModeTests.cs` | 4 | 묶음 행: Interiors/* (37건) |
| `Interiors/SchoolPixelPlayModeTests.cs` | 3 | 묶음 행: Interiors/* (37건) |
| `Interiors/StudentLifePixelPlayModeTests.cs` | 1 | 묶음 행: Interiors/* (37건) |
| `Interiors/TextInputGameplayFocusPlayModeTests.cs` | 4 | 묶음 행: Interiors/* (37건) |
| `InventoryEquipmentUiScenarioTests.cs` | 2 | DRG_001, EQP_002 |
| `LifeTimeOwnershipPlayModeTests.cs` | 3 | 묶음 행: LifeTimeOwnership* (3건) |
| `ModernUiPanelAutoInstallerPlayModeTests.cs` | 1 | 묶음 행: ModernUiPanelAutoInstaller, ModernUiReconstruction* |
| `ModernUiReconstructionScenarioTests.cs` | 2 | 묶음 행: ModernUiPanelAutoInstaller, ModernUiReconstruction* |
| `Network/DedicatedServerClientProcessHarnessTests.cs` | 9 | DEDICATED_HARNESS_PM_001 |
| `Network/JoinGamePanelPlayModeTests.cs` | 2 | NET_JOIN_001 |
| `Network/NetworkSessionHostPlayModeTests.cs` | 1 | MULTIPLAYER_PM_001 |
| `Network/Support/DedicatedServerClientProcessHarness.cs` | 0 | — (테스트 없음: 도우미 파일) |
| `Objectives/ObjectiveJournalInputPlayModeTests.cs` | 2 | OBJECTIVE_JOURNAL_PM_001 |
| `Placement/NonHouseFurnitureSurfacePlayModeTests.cs` | 1 | 묶음 행: NonHouseTilePlacementSurface_… |
| `PlayModeInputSystemGuardSetUp.cs` | 0 | — (테스트 없음: 도우미 파일) |
| `Quests/PlayerGlobalStateMultiplayerPlayModeTests.cs` | 2 | PLAYER_STATE_NET_PM_001, PLAYER_STATE_NET_PM_002 |
| `Quests/QuestDialogueScenarioTests.cs` | 2 | QUEST_001, QUEST_015 |
| `Quests/QuestProviderScenarioTests.cs` | 1 | QUEST_003 |
| `SaveSlotUiFlowTests.cs` | 1 | 묶음 행: SaveSlotSelectPanel_Show_… |
| `Scenarios/ScenarioId.cs` | 0 | — (테스트 없음: 도우미 파일) |
| `TownConcept/TownStyle2SpriteExhaustivePlayModeTests.cs` | 2 | 묶음 행: TownStyle2* |
| `TownConcept/TownStyle2UiSmokeTests.cs` | 3 | 묶음 행: TownStyle2* |
| `UI/Modern/UrbanInventoryBrowseScrollbarPlayModeTests.cs` | 1 | 묶음 행: UrbanInventoryBrowseScrollbar, UrbanUiModalLifecycle |
| `UI/UrbanUiModalLifecyclePlayModeTests.cs` | 1 | 묶음 행: UrbanInventoryBrowseScrollbar, UrbanUiModalLifecycle |
| `World/EventSystemRuntimeDedupPlayModeTests.cs` | 1 | TOWN_UI_PM_002 |
| `World/InteriorReturnDoorPortalPlayModeTests.cs` | 7 | 묶음 행: TOWN_DOOR_PM_001~013 |
| `World/PlayerWorldInteractionScenarioTests.cs` | 3 | INTERACT_PM_001 |
| `World/TownDoorPortalPlayModeTests.cs` | 6 | TOWN_DOOR_PM_001 |
| `World/TownLevelTransitionPlayModeTests.cs` | 1 | TOWN_LEVEL_PM_001 |
| `World/WorldTravelRecoveryPlayModeTests.cs` | 7 | 묶음 행: WorldTravelRecovery* (7건) |
