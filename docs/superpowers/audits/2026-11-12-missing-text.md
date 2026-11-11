# 빠진 문구 목록 — 원본(Unity 판)에도 없는 것 (2026-11-12)

문구를 정하는 것은 사용자 결정이다. 이 문서는 **정할 거리**만 모은다. 문구를 지어 넣지 않았다.

## 조사 결과

- `LocalizationKeyTests`가 고정한 빠진 키 83개를 Unity 판에서 모두 찾아봤다. **83개 모두 원본에 문구가 없다.**
  - `Assets/Localization/{ko,en}.json`(421개)에도, `Assets/Localization/GameTextCatalog.asset`(1,096개, json 421개를 포함)에도 없다.
  - 정의 에셋(`Assets/Data/**`)은 그 키를 필드 값으로 가리킬 뿐 이름·설명 문구를 따로 갖지 않는다(한글·영어 이름 필드 없음). 코드(`Assets/Scripts`)에도 이 키의 문자열이 없다.
  - Unity 판의 `GameTextCatalog.Get`은 빠진 키를 **키 그대로 돌려줬다**(`Assets/Scripts/Game/Localization/GameTextCatalog.cs:29`). 그래서 Unity 판에서도 이 83개는 화면에 키 문자열(영어 낱말이나 `action.help-npc` 같은 키)이 그대로 보였다. 한국어·영어 모두.
- 따라서 `game/localization/*.json`에 넣은 것은 없고 고정 파일(`Fixtures/known-missing-localization-keys.json`)도 그대로다.
- **변환기는 빠뜨린 것이 없다.** `data/localization/{ko,en}.json` 1,096개 = Unity 카탈로그 1,096개(Unity json 421개 포함)와 같은 키 집합. 에셋에는 있는데 `data/localization`에 없는 키는 0개다. `Tools/Rootborn.DataExport`를 고쳐 다시 내보낼 이유가 없다.
- 아래 "보이는 곳"은 필드 이름에서 읽은 것이다. 그 정의가 지금 게임 흐름에서 실제로 화면에 닿는지는 확인하지 않았다(대부분 시험용·레거시 정의로 보인다).

사용자가 정하면 할 일: ① 문구를 정한다 → ② `game/localization/{ko,en}.json` 끝에 키와 문구를 더한다 → ③ 고정 파일에서 그 키를 지운다(`LocalizationKeyTests`가 지워야 할 키를 알려 준다). **키가 영어 문구인 항목**은 키를 `{영역}.{이름}` 모양으로 바꾸려면 `data/`가 아니라 변환 원본(Unity 에셋)이나 변환기를 고쳐야 하는데, 지금은 `data/`를 손으로 고치지 않기로 했으므로 "키 이름은 그대로 두고 문구만 더하기"가 가장 싸다(키가 영어 낱말이라도 표에 넣으면 된다).

## A. 사용자 결정 대기 (1)

| 키 | 쓰는 정의 | 보이는 곳 |
|---|---|---|
| `dialogue.firstNpc.greeting` | Dialogue/Dialogue_FirstNpc | 엄마의 첫 대사(메인 체인 시작) |

## B. 키 모양인데 문구가 없다 (25) — 대부분 시험용·레거시 정의

| 키 | 쓰는 정의 | 보이는 곳 |
|---|---|---|
| `action.help-npc` | Careers/Candidates/Candidate_Service, Routes/Route_Candidate_Service | 진로 후보의 추천 행동 이름 |
| `action.visit-store` | Careers/Candidates/Candidate_Service | 진로 후보의 추천 행동 이름 |
| `action.investigate-town` | Candidate_Investigation, Route_Candidate_Investigation | 진로 후보의 추천 행동 이름 |
| `action.try-workshop` | Candidate_Technical, Route_Candidate_Workshop | 진로 후보의 추천 행동 이름 |
| `choice.test.careful.desc` | StudentLife/ExplorationInteractions/Choices/Choice_Exploration_TestCareful | 탐색 선택지 설명(시험용) |
| `choice.test.discovery.desc` | …/Choice_Exploration_TestDiscovery | 탐색 선택지 설명(시험용) |
| `choice.test.inspect.desc` | …/Choice_Exploration_TestInspect | 탐색 선택지 설명(시험용) |
| `choice.test.leave.desc` | …/Choice_Exploration_TestLeave | 탐색 선택지 설명(시험용) |
| `dialogue.careerMentor.greeting` | Quests/CareerRoutes/Dialogue/Dialogue_CareerMentor | 대사 첫 줄 |
| `dialogue.questProvider.greeting` | Quests/CareerRoutes/Dialogue/Dialogue_OrderedQuestProvider | 대사 첫 줄 |
| `knowledge.stoneTool` | Knowledge/Knowledge_StoneTool | 지식 이름(쓰지 않는 레거시) |
| `trait.greenThumb` | Traits/Trait_GreenThumb | 특성 이름(쓰지 않는 레거시) |
| `trait.quickLearner` | Traits/Trait_QuickLearner | 특성 이름(쓰지 않는 레거시) |
| `story.firstQuestComplete` | Story/StoryFlag_FirstQuestComplete | 이야기 깃발 이름 |
| `questchain.test.career-interest.library` | StudentLife/QuestChains/QuestChain_Test_CareerInterest_Library | 퀘스트 체인 기록의 체인 이름(시험용) |
| `questchain.test.career-interest.library.desc` | 같은 정의 | 체인 설명 |
| `questchain.test.career-interest.blocked-library-access` | …/QuestChain_Test_CareerInterest_BlockedLibraryAccess | 체인 이름(시험용) |
| `questchain.test.career-interest.blocked-library-access.desc` | 같은 정의 | 체인 설명 |
| `step.questchain.test.library.start` | …/Steps/Step_QC_Test_LibraryStart | 체인 단계 이름 |
| `step.questchain.test.library.start.desc` | 같은 정의 | 단계 설명 |
| `step.questchain.test.blocked.library.access` | …/Steps/Step_QC_Test_BlockedLibraryAccess | 단계 이름 |
| `step.questchain.test.blocked.library.access.desc` | 같은 정의 | 단계 설명 |
| `objective.questchain.library.activity` | …/Objectives/Objective_QC_Test_LibraryActivity | 목표 이름 |
| `objective.questchain.library.followup` | …/Objectives/Objective_QC_Test_SecondActivity | 목표 이름 |
| `objective.questchain.blocked.library.access` | …/Objectives/Objective_QC_Test_BlockedLibraryAccess | 목표 이름 |

→ `questchain.test.*`·`step.questchain.test.*`·`objective.questchain.*`는 이름에 `test`가 들어 있다. 퀘스트 체인 기록에서 이름 없이 id로 보이는 항목이 이것들이다. 문구를 붙일지, 시험용이라 기록에서 숨길지 정해야 한다.

## C. 키 자리에 영어 문구가 적혀 있다 (57) — Unity 판에서도 영어 그대로 보였다

문구를 넣는다면 `ko`와 `en` 둘 다 필요하다(`en`은 키와 같게 두면 Unity 판과 같다).

### C1. 첫 3일 캠페인·경로

| 키 | 쓰는 정의 · 필드 | 보이는 곳 |
|---|---|---|
| `First three days` | Campaigns/Campaign_FirstThreeDays · 이름 | 캠페인 이름 |
| `Open first campaign` | 같은 정의 · 설명 | 캠페인 설명 |
| `Town adaptation` | Campaigns/Days/Day_01_TownAdaptation · 이름, 주제 | 1일차 이름·주제 |
| `Find growth` | Day_02_FindGrowth · 이름, 주제 / Day_01 · 다음 안내 | 2일차 이름·주제, 1일차 다음 안내 |
| `Connect future` | Day_03_ConnectFuture · 이름, 주제 / Day_02 · 다음 안내 | 3일차 이름·주제, 2일차 다음 안내 |
| `School path` | Campaigns/Routes/Route_SchoolPath · 이름 | 경로 이름 |
| `A school route is available` | 같은 정의 · 설명 | 경로 설명 |
| `Library path` | Route_LibraryPath · 이름 | 경로 이름 |
| `Study outside class` | 같은 정의 · 설명 | 경로 설명 |
| `Event path` | Route_EventPath · 이름 | 경로 이름 |
| `Follow a town event` | 같은 정의 · 설명 | 경로 설명 |
| `Town walk` | Route_TownWalk · 이름 | 경로 이름 |
| `Visit town spaces` | 같은 정의 · 설명 | 경로 설명 |

### C2. 하루 이벤트 종류·상태·관계·월드

| 키 | 쓰는 정의 · 필드 | 보이는 곳 | 참고 |
|---|---|---|---|
| `Optional` | DailyEvents/Kinds/DailyEventKind_Optional · 이름 | 하루 이벤트 종류 이름 | |
| `Deferrable` | DailyEventKind_Deferrable · 이름 | 하루 이벤트 종류 이름 | |
| `Fatigue` | Status/Status_Student_Fatigue · 이름 | 상태 이름 | 같은 뜻의 `status.fatigue`(ko "피로")가 이미 있다. 정의가 그 키를 가리키게 고치는 쪽이 문구 중복이 없다(변환 원본 수정 필요) |
| `Guide Trust` | Relationships/Relationship_FirstGuide_Trust · 이름 | 관계 이름 | |
| `Teacher Support` | Relationships/Relationship_Teacher_Support · 이름 | 관계 이름 | |
| `Shared World` | WorldState/Scopes/Scope_SharedWorld · 이름 | 월드 상태 범위 이름 | |
| `Librarian` | NPCs/Roles/NpcRole_Librarian · 이름, InterpretationSource_Librarian · 이름 | NPC 역할 이름 | |
| `Town Square` | InterpretationSource_Location · 이름 | 단서 해석 출처 이름 | `location.identity.square.name`(en "Town Square")이 이미 있다 |

### C3. 진로 후보 잠금 안내

| 키 | 쓰는 정의 · 필드 | 보이는 곳 |
|---|---|---|
| `???` | Careers/Candidates/Candidate_* 4개 · 잠금 안내(13곳이 같은 키를 쓴다) | 잠긴 진로 후보의 안내 줄 |

### C4. 발견 단서 묶음(PlayLoop·서재·작업대) — 이름에 시험용 낌새가 짙다

| 키 | 쓰는 정의 · 필드 |
|---|---|
| `Play clue` / `PlayMode clue` | DiscoveryClue_PlayLoop · 이름 / 설명 |
| `The trace is nearby.` / `??? trace` | 같은 정의 · 공개 문구 / 가려진 문구 |
| `Play clue board` / `A board hint points to a trace.` | Source_PlayLoopBoard · 이름 / 힌트 |
| `Play clue NPC` / `A villager repeats the board rumor.` | Source_PlayLoopNpc · 이름 / 힌트 |
| `Play clue map hint` / `The map shades a nearby trace area.` | Source_PlayLoopMap · 이름 / 힌트 |
| `Play clue trace` / `A scuffed trace waits nearby.` | Source_PlayLoopTrace · 이름 / 힌트 |
| `Play clue unknown entry` / `??? trace entry` | Source_PlayLoopEncyclopedia · 이름 / 힌트 |
| `Play clue follow-up` / `Follow the play clue.` | Quest_DiscoveryClue_PlayLoop · 이름 / 설명 |
| `Play clue world state` / `Play clue revealed a trace.` / `Follow clue` | WorldState_DiscoveryClue_PlayLoop · 이름 / 설명 / 다음 행동 |
| `Play Research` | Career_DiscoveryClue_PlayLoop · 이름 |
| `Librarian`(C2와 같은 키) / `Ask about the clue` | InterpretationSource_Librarian · 이름 / 힌트 |
| `Workbench` / `Restore at the workbench` | InterpretationSource_Workbench · 이름 / 힌트 |
| `Investigate the place` | InterpretationSource_Location · 힌트 |
| `Investigate Location` / `Investigate` / `Investigate the clue location.` / `The location changes your condition.` | Interpretation_InvestigateLocation · 이름 / 방법 / 설명 / 공개 문구 |
| `Restore at Workbench` / `Restore` / `Restore the clue with tools.` / `The fragment points to follow-up work.` | Interpretation_RestoreWorkbench · 이름 / 방법 / 설명 / 공개 문구 |
| `Inspect workshop repair ledger` / `Inspect ledger` / `??? repair ledger` | Interpretation_WorkshopRepairLedger · 이름 / 방법 / 가려진 문구 |

(`Ask Librarian`은 카탈로그에 있어 빠진 목록에 없다.)

## D. 가구 이름 1,473개는 원본에도 없음 — 이름을 붙일 방식 결정 필요

- Unity 판의 가구 이름은 **그림 시트 파일 이름에서 자동으로 만든 영어 이름**이었다. 가구를 만드는 편집기 도구가 `ToDisplayName(baseName)`으로 파일 이름의 낱말을 띄어 쓰고 첫 글자를 대문자로 바꿔 `_displayName`에 넣었다(`Assets/Scripts/Editor/Interiors/ModernInteriorsShadowlessFurnitureImporter.cs:229,352`, `FurnitureSpriteAutoImporter.cs:150,359`). 가구 배치 화면은 그 값을 그대로 보였다(`Assets/Scripts/UI/Interiors/InteriorFurnitureCatalog.cs:160`, `InteriorFurniturePlacementUi.cs:474`).
- 이름 키(`displayNameKey`)가 있는 가구는 2개뿐이다(`story.photo-board`, `story.study-desk` — 둘 다 `dream.furniture.*.name` 키가 문구 표에 있다). 따로 붙인 한국어 이름은 30개(그 2개 포함)다.
- 결정할 것: 나머지 1,473개에 이름을 붙이는 방식 — (a) 분류 이름 + 번호(예: "침실 가구 12"), (b) 사람이 하나씩, (c) 생성(예: 그림을 보고 만든 이름을 사람이 검수). 정해지면 이름 키 규칙(`furniture.<id>.name`)과 `ko`/`en` 표를 함께 만든다.

### 분류(`category`)별 개수

한글 이름이 있는 것 / 시트 이름뿐인 것 (총 1,503 = 30 + 1,473)

| 분류 | 개수 | 한글 이름 있음 | 영어 시트 이름뿐 | 시트 이름 예 |
|---|---:|---:|---:|---|
| Bedroom | 555 | 0 | 555 | Bedroom Singles Shadowless 48x48 1 |
| Kitchen | 408 | 0 | 408 | Kitchen Singles Shadowless 48x48 1 |
| Bathroom | 158 | 0 | 158 | Bathroom Singles Shadowless 48x48 1 |
| Japanese | 131 | 0 | 131 | Japanese Interiors Singles Shadowless 48x48 1 |
| LivingRoom | 122 | 0 | 122 | Living Room Singles Shadowless 48x48 1 |
| Condominium | 86 | 0 | 86 | Condominium Singles Shadowless 48x48 1 |
| Activity31 | 9 | 9 | 0 | |
| Archive30 | 6 | 0 | 6 | Archive30 Archive Bookshelf |
| Desk | 4 | 4 | 0 | |
| Outdoor | 4 | 4 | 0 | |
| Comfy31 | 3 | 0 | 3 | Comfy31 Kitchen Counter Front |
| desk(소문자) | 3 | 1 | 2 | Basic Desk |
| Storage | 3 | 3 | 0 | |
| chair(소문자) | 2 | 0 | 2 | Black Chair |
| Growth31 | 2 | 2 | 0 | |
| Chair | 2 | 2 | 0 | |
| Decor | 2 | 2 | 0 | |
| Furniture48 | 2 | 2 | 0 | |
| Plant | 1 | 1 | 0 | |
| **합계** | **1,503** | **30** | **1,473** | |

- 분류 이름에 대소문자만 다른 `Desk`/`desk`, `Chair`/`chair`가 있다. 분류별 이름을 붙이기 전에 합쳐야 한다(데이터 원본 수정 필요).
