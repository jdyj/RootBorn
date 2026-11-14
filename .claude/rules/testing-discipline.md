# 테스트 규칙

모든 코드에 적용한다. 보고할 때 근거를 어떻게 쓰는지는 `rules/core-logic-and-play-evidence.md`.

## 원칙

1. **실패 테스트 먼저, 구현은 다음.** AI가 쓴 코드도 같다. 테스트 없이 끝난 작업은 끝나지 않은 것이다. 왜: 테스트가 먼저 실패하는 것을 봐야 그 테스트가 무엇을 지키는지 안다.
2. **보상은 한 번만.** 지급 코드의 테스트는 정상 지급, 공간 부족, 일부 지급 없음, 같은 요청 두 번, 저장·다시 읽기 뒤 다시 요청을 모두 본다. 거절되면 인벤토리와 진행이 그대로인지도 본다(`rules/data-driven.md` "보상과 인벤토리").
3. **플레이어처럼 테스트한다.** 사용자 흐름(걷기, 문 지나기, 상호작용, 버튼 누르기, 화면 고르기, 이어 하기)이 끝났다고 말하려면 그 흐름을 입력으로 지나는 테스트가 있어야 한다. 내부 명령 직접 호출, 상태 강제 설정, 순간이동은 준비 단계로만 쓴다.
4. **사용자가 정한 흐름은 그대로 재현한다.** "문으로 걸어가서", "버튼을 눌러서", "NPC와 이야기해서"라고 했으면 테스트도 그 길로 간다. 다른 길로 같은 결과를 만들어 통과시키지 않는다. 자동화가 어려우면 먼저 그렇다고 알린다.
5. **보이는 것은 직접 본다.** 화면·그림·맵·배치를 바꾼 일은 스크린샷을 직접 열어 겹친 글자, 잘린 패널, 빈 그림, 맵 밖 노출, 남은 디버그 표시가 없는지 본 뒤에 끝났다고 한다. 테스트 통과만으로 "화면에 보인다"고 하지 않는다.
6. **다음 판으로 한 번 더 돈다.** 저장되는 것은 새 `GameSession`(또는 새 실행)으로 다시 읽어 같은지 본다. 왜: 메모리에서만 맞고 세이브에서 빠지는 일을 잡는다(2026-11-11 예: 수첩 추적 지정이 메모리에만 있어 이어 하면 추적 목표 HUD가 사라졌다).

## 어디에 무엇을

| 프로젝트 | 무엇을 | 근거의 종류 |
|---|---|---|
| `game/tests/Rootborn.Core.Tests` | Core 규칙 단위(생성자로 만든 정의, 메모리 저장소) | 자동 테스트 |
| `game/tests/Rootborn.Data.Tests` | 정의 로더, 실제 `data/` 전체 읽기(`RealDataTests`), 세이브 저장소, 옛 세이브 고정 파일 | 자동 테스트 |
| `game/tests/Rootborn.World.Tests` | 맵 만들기, 새 맵 검사(`MapContractTests`), 메인 체인 지점까지 칸 단위 걷기(`MainChainWalkTests`) | 자동 테스트(화면 근거 아님) |
| `game/tests/Rootborn.Game.Tests` | `GameSession` 명령만으로 흐름(메인 체인, 성인기, 보상 한 번, 이어 하기). 실제 `data/`, 임시 폴더 저장소 | 자동 테스트(화면 근거 아님) |
| `game/tests/Rootborn.Balance.Tests` | 수치 균형: 전략대로 `GameSession` 명령으로 여러 날 돌려 수입을 잰다(빠른 검사 + 손으로 도는 전체 보고 `output/balance/`, 모든 경우 훑기 `output/balance/sweep/`). 가정을 넣은 계산 | 자동 테스트(실제 플레이 아님) |
| `Tools/pixel-kit/tests` (Python) | 픽셀 키트: 부품·명세·방 평면·LDtk 쓰기·벽 그림 고르기, 시험 표 `tests/fixtures/room-vectors.json`(키트가 내고 Core `Rootborn.Core.Building` 테스트도 읽는다), 조각 시트. `python -m unittest discover -s Tools/pixel-kit/tests -t Tools/pixel-kit`, CI job `pixel-kit-tests` | 자동 테스트(그림 모양을 사람이 직접 보는 일은 따로) |
| `game/tests/Rootborn.Ui.Tests` | 배치, 포인터 판정, 포커스, 부품 | 자동 테스트 |
| `game/tests/Rootborn.Screens.Tests` | 창 없이 화면을 눌러 보기, 화면 쌓기, 모든 언어의 문구 키 | 자동 테스트(화면 근거 아님) |
| `game/tests/Rootborn.Host.Tests` | 실제 창과 게임 루프를 한 프레임씩 돌리며 `PlayerDriver`로 키·포인터를 넣고 스크린샷 | 장치 이벤트 주입(OS 입력 계층 미확인) |

- 새 테스트는 그 일을 맡은 프로젝트에 둔다. 같은 동작을 여러 층에서 중복 검증하지 않는다. 규칙은 Core 테스트, 흐름은 Game 테스트, 화면은 Screens·Host 테스트.
- Host 테스트는 `PlayerDriver`의 동사(키, 포인터, 글자, 걷기, 스크린샷)만 쓴다. 대상은 화면 좌표가 아니라 요소 id·데이터 id로 찾는다. 쓰는 법은 `host-play-test` 스킬.
- 화면 테스트는 특정 그림의 픽셀에 기대지 않는다. 구성과 동작을 본다(그림은 전부 바뀔 예정).
- 시간·난수는 고정한다. Host 테스트는 고정 프레임 간격으로 돌고 실시간을 기다리지 않는다.

## 명령

```bash
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
dotnet test game/Rootborn.sln                 # 전부(Core 포함)
dotnet test game/tests/Rootborn.Core.Tests    # 규칙만
bash Scripts/ci/run-all.sh --checks-only   # 정적 검사(Scripts/ci/README.md)
bash Scripts/ci/run-all.sh                 # 자체 시험 + 검사 + dotnet 테스트 전부
```

- 평소에는 고친 프로젝트의 테스트만 돌리고, 커밋·합치기 전에는 전부 돌린다.
- **새 게임의 모습이나 여러 프로젝트가 쓰는 것(처음 배치 가구, 새 세이브 파일, 공유 정의, 화면 구성)을 바꾼 작업은 고친 프로젝트만이 아니라 Screens·Simulation·Server 테스트도 돌린다.** 왜: 2026-11-14 새 게임에 처음 배치 가구를 넣은 작업이 Game·Core·Data만 돌려, Screens 11건·Host 3건·Server 1건·Simulation 1건의 실패가 뒤 작업에서야 드러났다(새 게임은 빈 집이라는 전제가 여러 프로젝트의 테스트에 있었다).
- 전체 실행(특히 Host)은 원격 Linux 장치에서 돌릴 수 있다: `bash Scripts/remote-test/run.sh [<커밋>]`(`Scripts/remote-test/README.md`). 커밋된 것만 가고, 접속 값은 환경 변수로 받는다. 이 PC의 메모리가 모자라거나 세션 여럿이 동시에 돌 때는 이쪽을 쓴다. 원격 실행은 연결이 끊겨도 끝까지 돈다 — 이 PC 쪽이 꺼졌으면 다시 시작하지 말고 `run.sh --resume`으로 이어 받는다(`--status`로 도는 실행을 본다). 근거 종류는 같다(장치 이벤트 주입, OS 입력 계층 미확인).
- **통과 줄만 보지 말고 프로젝트별 건수가 줄지 않았는지 본다.** 왜: 건수가 줄었으면 테스트가 빠진 것이고, 통과 줄은 그것을 알려 주지 않는다.
- CI(`.github/workflows/ci.yml`)는 정적 검사, 키트 도구 테스트(Python, job `pixel-kit-tests` — GitHub에서 도는 것은 아직 확인하지 못했다), Host를 뺀 dotnet 테스트로 막는다. Host 테스트는 아직 `continue-on-error`다.

## 시나리오 id

시나리오 id는 유지하고, 각 id가 어느 테스트에 대응하는지는 `game/tests/Rootborn.Game.Tests/SCENARIOS.md`(Unity PlayMode 테스트 전체의 대응표 포함)가 기준이다.

| id | 시나리오 | 대응 |
|---|---|---|
| TOWN-001 | 새 게임이 마을 생활로 시작(새 게임의 집에만 처음 배치 가구) | Game `NewGameTests`·`HouseStarterTests`, Host `NewGameJourneyTests`·`HouseMapHostTests` |
| LIFE-001 | 활동 실행 → 비용 확인 → 결과가 한꺼번에 반영, 비용이 모자라면 무변경 | Core `StudentLifeActivityTests`, Game `SelfStudyAndClassTests` 등 |
| LIFE-002 | 같은 활동 요청을 다시 해도 한 번만 반영 | Core `StudentLifeActivityTests`, Game `RewardOnceTests` |
| CAREER-001 | 활동·선택이 쌓여 진로 힌트·연습이 열림, 학교 밖 길로도 | Core `CareerPracticeTests`·`CareerCandidateTests`, Game `FreePathTests`·`AdultCareerTests` |
| REL-001 | 관계 활동으로 관계 값이 바뀌고 같은 요청은 한 번만 | Game `HelpAndTrustTests` |
| JOB-001 | 알바로 시간·체력·돈·성장이 데이터대로 바뀜 | Game `PartTimeWorkGrowthTests`, Core `PartTimeWorkTests` |
| STATUS-002 | 하루 시계의 날·시각이 결정적 | Core `DayClockTests` |
| WORKSPACE-001 | 일터를 꾸미면 시설 일의 보상이 늘고, 꾸미지 않아도 손해가 없다 | Game `WorkspaceTests`, Host `WorkspaceHostTests` |
| WORKSPACE-002 | 일터 템플릿은 미리 확인하고 한 번만 적용, 안 되면 무변경 | Game `WorkspaceTests`, Screens `WorkspaceTemplateScreenTests` |
| SKILL-001 | 가구를 놓거나 방을 꾸미면 인테리어 기술이(같은 하루 한도), 요리 일을 하면 요리 기술이 자란다(하루 한도) | Game `WorkspaceTests`, `HouseFinishTests` |
| MAIN_E2E_000~011 | 메인 체인을 실행부터 끝까지 실제 입력으로(이어 하기 포함) | Host `MainChainHostTests`, 화면 없이는 Game `MainChainTests`·`MainChainEndTests` |
| NPC-001 | NPC가 일과표대로 다음 자리까지 걸어 도착 | Core `NpcDayPlanTests`, World `NpcRouteContractTests`, Simulation `NpcWalkingTests`, Host `NpcWalkHostTests` |
| NPC-002 | 자리에 없는 NPC는 만날 수 없다(걷는 중에는 말을 건다) | World `NpcStageTests`, Core `NpcDayPlanTests`, Host `NpcWalkHostTests` |
| NPC-003 | 같은 시각이면 같은 자리 — 이어 하기·같이 하기 | Core `NpcStayTests`·`NpcDayPlanTests`, World `NpcStageTests`, Simulation `NpcWalkingTests`, Game `NpcTimeTests`, Host `TwoGamesHostTests.TWO_GAMES_NPC_001` |
| HOUSE-001 | 방 꾸밈을 돈을 내고 바꾸고 같은 요청은 한 번만, 바뀐 꾸밈으로 집을 다시 그림 | Core `HouseFinishesTests`·`RoomFillTests`, Game `HouseFinishTests`·`HouseBuildCompatTests`(세이브 하위·상위호환), World `RoomTileCompositorTests`(Lookup·PreviewLookup), Screens `FinishPaletteScreenTests`, Host `HouseFinishHostTests` |
| HOUSE-002 | 집 맵 교체 뒤 옛 가구가 옮겨지거나 보관되고 사라지지 않음 | Core `FurnitureRelocationTests`, Game `HouseMoveTests`, Screens `StoredFurnitureScreenTests`·`HouseMovedNoticeTests`, Host `HouseMoveHostTests` |

- 지금 대응이 없는 id(KNOW-001·002, TOOL-001, NET-001·002, HEIR-001, STATUS-001의 일부)는 게임이 그 영역을 쓰지 않거나 MonoGame 판에 아직 없다. 이유는 `SCENARIOS.md` 2절. 그 영역을 옮기거나 만들 때 테스트를 더하고 이 표에 올린다.
- 새 메카닉(활동, 선택지, 진로, 관계, 알바, 퀘스트 종류)을 더하면 시나리오 테스트를 하나 이상 더한다. 새 보상 지급 경로는 원칙 2의 다섯 경우를 더한다.
- 학교 밖 성장 길을 하나 이상 테스트한다(`rules/game-design.md` "자유 경로 성장").
