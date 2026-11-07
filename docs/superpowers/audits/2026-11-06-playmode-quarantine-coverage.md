# PlayMode 격리 테스트 커버리지 기록 (2026-11-06)

`[Ignore(LegacyPremise.*)]`로 격리한 PlayMode 테스트가 무엇을 검증했는지, 저작 월드에서는 어디서 대체할 수 있는지 정리한다.
코드 읽기로만 만든 기록이다. 이 문서 작성 중 Unity를 실행하지 않았다.

## 숫자

| 전제 | 개수 | 뜻 |
|---|---|---|
| AuthoredTown | 67 | 저작 Town에서는 만들지 않는 프로토타입 오브젝트 이름을 기다림 |
| AuthoredHouse | 15 | 옛 생성형 집(방 생성·프리셋·업그레이드 포털) 흐름 |
| Farm | 14 | Town에서 더 이상 이어지지 않는 Farm 씬과 옛 HUD |
| CharacterParts | 5 | 성장 캐릭터로 바뀐 옛 부위 조합 캐릭터 |
| ObjectiveJournal | 3 | 목표 표시가 Tab 목표 저널로 옮겨 숨긴 옛 패널 |
| 합계 | 104 | |

활성 E2E 파일은 22개다. 부트·메뉴·생성 화면, 발견·단서, 도감, 지식·상태 활동, 자원·레시피, 목표 저널 회귀, 경찰서 실내, 집 배치 저장이 여기에 들어 있다.

## 핵심: 기능은 저작 월드에 있고, 테스트가 옛 이름을 찾는다

저작 씬 YAML에서 확인한 실제 배치는 아래와 같다.

| 옛 테스트가 기다리는 것 | 저작 월드의 대응 | 씬 |
|---|---|---|
| `StudyBasicsActivity` (Town 공부 보드) | `SchoolClassActivity` (`LocationActivityInteractor`, `LocationIdentity_School`) | School |
| `StudentDayEndBoard` (Town) | `StudentDayEndBoard` (`StudentDayEndInteractor`, `DayEndRule_StudentDefault`) | **House** |
| `GuideNpc` | `TownNpc_npc_first_guide` (`Npc_FirstGuide`) | Town |
| 알바 작업 지점 | `PartTimeWork_work_*` 9곳 (`PartTimeWorkInteractor`) | Town |
| 학교 밖 활동 | `OutsideActivity_outside_self_study_library`, `OutsideActivity_outside_help_neighbor` | Town |
| 장소 활동·방문 | `LocationVisit_location_*`, `FacilityActivities_location_*` | Town |
| 발견 지점 | `TownDiscoveryPoint` (`Discovery_ClocktowerNotice`) | Town |
| 교사 NPC | `TownNpc_npc_teacher` (Town에서는 비활성으로 저장, School에도 있음) | Town, School |

런타임 설치기(`TownStudentLifeRuntimeInstaller`, `PartTimeWorkRuntimeInstaller`, `OutsideSchoolRuntimeInstaller`,
`TownHelpActionRuntimeInstaller`, `StudentDayRuntimeInstaller`의 보드 생성)는 저작 씬에서 일부러 아무것도 만들지 않는다.
그래서 이 테스트들은 기능이 사라져서가 아니라 오브젝트 이름과 위치가 바뀌어서 멈춘다.

## 시나리오 ID별 공백

| 시나리오 | 저작 월드 활성 E2E | 상태 |
|---|---|---|
| TOWN-001 진입 | BootToGame 7건, RegistryProviderUnifiedFlow | 유지 |
| LIFE-001/002 활동 결과·멱등 | KnowledgeStatusActivityChain, TownResourceRecipeActivity, FREE_PATH_001(학교 수업) | 유지 |
| CAREER-001 진로 해금 | `CareerCandidateDeepeningLoopE2EScenarioTests.CAREER_001_AuthoredTown_*` | 2026-11-06 복구 |
| REL-001 관계 | `FirstGuideRelationshipMilestoneE2EScenarioTests.REL_001_AuthoredTown_*` | 2026-11-06 복구 |
| JOB-001 알바 | `PartTimeWorkGrowthE2EScenarioTests.JOB_001_AuthoredTown_*` | 2026-11-06 복구: 걷기 → E → 미니게임을 화면 버튼으로 진행 → 불러오기 후 한 번만 반영 |
| KNOW/STATUS | KnowledgeStatusActivityChain | 유지 |
| 하루 끝 결과·다음 날 | `StudentDayResultLoopE2EScenarioTests.DAY_END_001_AuthoredHouse_*` | 2026-11-06 복구: Town → 집 문 → House 보드 → 결과 → 불러오기 후 같은 결과(재정산 없음) → 다음 날 |
| 자유 경로(학교 밖 성장) | `SelfStudyLibraryGrowthE2EScenarioTests.FREE_PATH_001_*` | 2026-11-06 복구: Town 도서관 독학과 School 수업이 둘 다 trait.focus를 올림. 2026-11-07: `TownHelpActionGrowthE2EScenarioTests.HELP_001_AuthoredTown_*`로 엄마 심부름(걷기 → E)이 책임감·신뢰를 올리고, 불러오기 후 한 번만 반영되며, 다시 도우면 한 번 더 오르는 것을 확인 |

EditMode 단위 테스트는 위 시스템을 계속 다룬다. 공백은 "실제 입력으로 저작 월드를 걸어 다니며 끝까지 가는" PlayMode 증명이다.

## 옮기는 순서 (제안)

한 번에 67개를 고치지 않는다. 시나리오 ID 하나당 대표 테스트 하나를 저작 월드 경로로 다시 쓰고, 나머지는 그 뒤에 지운다.

1. **공용 도우미 먼저.** `PlayerFlowE2EDriver`에 다음을 둔다.
   - 저작 오브젝트를 이름 대신 컴포넌트와 데이터로 찾는 함수 (`LocationActivityInteractor`의 identity, `PartTimeWorkInteractor`의 work 등)
   - 문으로 씬을 건너는 함수: 문 트리거까지 걷고, 씬 전환과 도착 지점을 기다림
2. **JOB-001:** Town 시작 → `PartTimeWork_work_shift_corner_store`까지 걷기 → E → 미니게임 → 시간·체력·돈 변화.
   알바 9종이 모두 미니게임(`MinigamePanel`: 선택·탭·판 배치·차선 이동)을 연다. 옛 테스트의 "E 한 번에 즉시 적용" 전제는 더는 맞지 않는다.
   먼저 미니게임 종류별로 화면 버튼을 눌러 끝내는 테스트 도우미가 필요하다. 저장 위치도 `student-life-progress.json`이 아니라
   `adult-career-snapshot-*.json`이다(활성 테스트 `KnowledgeStatusActivityChainE2EScenarioTests` 기준).
3. **하루 끝:** Town → 집 문 → House → `StudentDayEndBoard` → 결과 창 → 저장/불러오기 후 다음 날.
4. **LIFE(공부):** Town → 학교 문 → School → `SchoolClassActivity` → 특성·기술 변화.
5. **자유 경로:** `OutsideActivity_outside_self_study_library`로 같은 성장 목표가 학교 밖에서도 오르는지.
6. **REL-001:** `TownNpc_npc_first_guide` 대화 → 관계 수치.
7. **CAREER-001:** 2~6의 누적으로 진로 힌트 해금.

각 단계는 실행해서 통과를 본 뒤에만 해당 옛 테스트를 지운다.

## 같이 발견한 것

- 새 게임 버튼 전에 `PlayModeScenarioStart.WaitForGameData()`를 부르지 않는 E2E가 40개 가까이 있다.
  실행 첫 순서가 되면 레지스트리가 아직 없어 진입이 실패하고 생성 화면이 남는다. 도감 E2E의 실행 첫 순서 실패가 이 경우로 보인다(2026-11-06 수정, 미실행).
- 공용 도우미를 만들 때 새 게임 클릭 함수 안에서 이 대기를 하면 개별 테스트가 빠뜨릴 수 없다.

## 진행 (2026-11-06)

- `PlayerFlowE2EDriver.PlayMinigameToEnd`: 순서(미리보기를 기억했다가 누름), 선택(힌트 표시), 타이밍(커서가 박자 위일 때 Tap)을 화면 버튼으로 진행한다. 2026-11-07: 판 배치(맞는 배치를 먼저 계산해 조각·회전·칸 버튼을 누름)와 피하기(다음 장애물 차선에서 비켜서고 표적을 조준 영역에서 클릭)도 지원. `JOB_002_AuthoredTown_EveryJobPassesFromTheScreen`이 알바 9개 전부를 화면 입력으로 통과시키고 성장이 한 번만 반영되는지 본다(2026-11-07 실행 10/10 통과).
- JOB-001 복구. 옛 `WORK_E2E_001_006`은 즉시 적용 전제라 격리 유지.
- 공용 도우미 추가: `EnterWorldFromSaveSlot`, `WaitForPlayerReady`, `WalkThroughDoor`(문까지 걷고 필요하면 E).
- 하루 끝(DAY_END_001) 복구.
- 자유 경로(FREE_PATH_001) 복구. 그 과정에서 도착 처리 중 상호작용이 바인딩에 덮여 사라지는 게임 버그를 찾아 고침.
- 남은 공백: CAREER-001, REL-001, TownHelpAction.
