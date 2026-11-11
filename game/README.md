# game/ — ROOTBORN MonoGame 판

엔진 이전 규칙은 `.claude/rules/engine-migration.md`, 전체 설계는 `docs/superpowers/specs/2026-11-11-unity-removal-design.md`.
지금 있는 것은 실행 프로그램 뼈대와 UI 토대(프로젝트 3b), 데이터 로더와 세이브 저장소, 월드 화면(LDtk 맵을 그리고 걸어서 문으로 오감),
화면 토대(프로젝트 4 묶음 0: 화면 쌓기, 키·상호작용 등록 표, 현지화, 도시형 몰딩 스킨, 설정 화면)다.

| 폴더 | 내용 | MonoGame 참조 |
|---|---|---|
| `Rootborn.Core` | `Packages/com.rootborn.core/Runtime`의 게임 규칙을 그대로 묶는 프로젝트(코드 없음, LangVersion 9) | 없음 |
| `Rootborn.Data` | `data/` JSON으로 Core 정의 만들기(`GameDataLoader.Load(dir, RootbornBuilders.Register)`), 세이브 파일 저장소(`FileSaveStore`) | 없음 |
| `Rootborn.Game` | 게임 한 판(`GameSession.StartFromData(data, 저장소, 슬롯, 플레이어)`, 또는 `Start(정의, GameStartSettings.FromDefinitions(정의), …)`). 화면이 부를 명령과 조회(가구는 `EnterHouse(방 칸, "house")` 뒤에). 규칙은 Core가 판단하고 여기서는 영역을 잇고 저장만 한다 | 없음 |
| `Rootborn.World` | LDtk 파일(.ldtk·.ldtkl)을 그대로 읽고, 맵 목록 `game/world/maps.json`으로 Core 월드(충돌·문·도착 지점)와 그릴 목록을 만든다. 장면 목록·낮밤 조명은 정의 로더가 만든 것을 받는다(`WorldTravelData.From`, `DayNightLighting.From`). 새 맵 검사 `MapChecks`. 맵을 그리는 사람의 약속은 `game/world/README.md` | 없음 |
| `Rootborn.Ui` | 배치, 포인터 판정, 클릭, 포커스 이동, 9조각 계산과 부품(패널, 글자, 여러 줄 글자, 그림, 버튼, 칸, 글자 칸, 스크롤 목록, 탭 줄, 게이지, 켬/끔, 미끄럼 막대, 카드 줄·격자). 그리기 명령 목록만 낸다 | 없음 |
| `Rootborn.Screens` | 화면들, 화면 쌓기(`ScreenStack`), 등록 표(`ScreenCatalog`), 현지화 문구(`GameText`), 게임 명령 감싸개(`SessionCommands`) | 없음 |
| `Rootborn.Net` | 멀티플레이 바탕: 전송 인터페이스(`ITransport`)와 같은 프로세스 전송(`LoopbackNetwork`, 지연·손실·순서 바뀜)·UDP 전송(`UdpTransport`, LiteNetLib 1.3.5 고정), 메시지 종류 등록 표(`RootbornMessages`, 종류 번호 + 형식 버전 + 이진 몸통), 접속 세션(`SessionServer`·`SessionClient`: 승인·신원·위치·겉모습·공용 시간). 각자 자기 판을 돌리는 모델이라 진행은 보내지 않는다. 규칙(정원, 도착 자리, 공용 시간, 사이 메우기)은 Core `Rootborn.Core.Network` | 없음 |
| `Rootborn.Host` | 창, 화면 배율, 입력 장치 계층, 한글 조합 입력, TTF 글자, 그리기, 화면 저장, 견본 화면 | 3.8.6.1 고정 |
| `tests/Rootborn.Data.Tests` | 정의 로더, 실제 `data/` 전체 읽기, 세이브 저장소, Unity 판 QA 세이브 고정 파일 | 없음 |
| `tests/Rootborn.World.Tests` | 모든 맵에 도는 새 맵 검사(`MapContractTests`, 지금 맵의 알려진 문제 고정), 새 게임 자리에서 메인 체인 지점까지 걷기(`MainChainWalkTests`), 실제 LDtk로 맵 만들기, Unity 씬에 구워진 충돌과 칸 단위 대조, 도착 지점·문 목적지, 크기가 다른 집, 장면 목록·고정 세이브, 캐릭터 그림, 낮밤, 상호작용 데이터 | 없음 |
| `tests/Rootborn.Game.Tests` | 실제 `data/`와 임시 폴더 저장소로 새 게임 초기값, 메인 체인 끝까지(화면 없이, 가구 방 칸은 실제 집 맵), 성인기, 보상 한 번만, Unity QA 세이브·스냅샷 이전 슬롯 이어 하기 | 없음 |
| `tests/Rootborn.Net.Tests` | 같은 프로세스 전송으로 서버 1 + 클라이언트 여럿(승인·정원 초과, 서로 보임, 맵 옮김, 하루 끝 준비, 늦게 들어온 사람, 끊김·재접속·시간 초과, 손실·지연·순서 바뀜, 깨진 메시지), 메시지 형식, UDP 루프백(포트는 운영체제가 준다) | 없음 |
| `tests/Rootborn.Ui.Tests` | 화면 없이 도는 테스트 | 없음 |
| `tests/Rootborn.Screens.Tests` | 창 없이 도는 화면 테스트: 화면 쌓기의 층·배타·이동 막음·입력 넘기기, 실제 `ko.json`·`en.json`, 설정·HUD·대화·하루 이벤트·가방·상태 화면을 실제 `data/`와 `GameSession`으로 눌러 보기(알려진 빠진 현지화 키는 `KnownMissingKeys`) | 없음 |
| `tests/Rootborn.Host.Tests` | 실제 창과 게임 루프를 한 프레임씩 돌리는 테스트(새 게임 → 집 → 마을 → 가게 → 이어 하기 시나리오 `NewGameJourneyTests` 포함) | 있음 |

## 명령

```
dotnet test game/Rootborn.sln
dotnet test Tools/Rootborn.Core.Tests
dotnet run --project game/Rootborn.Host -- --sample --size 1920x1080
dotnet run --project game/Rootborn.Host -- --world Shop --size 1920x1080
dotnet run --project game/Rootborn.Host -- --save-root <세이브 폴더>
dotnet run --project game/Rootborn.Host -- --save-root <세이브 폴더> --slot slot-0
```

인자와 환경 변수(인자가 먼저다):

| 인자 | 환경 변수 | 없을 때 |
|---|---|---|
| `--root <저장소>` | `ROOTBORN_REPO_ROOT` | 실행 파일 위치에서 위로 올라가며 찾는다 |
| `--font <TTF>` | `ROOTBORN_FONT` | 운영체제 한글 글꼴을 찾는다. 못 찾으면 오류 |
| `--evidence <폴더>` | `ROOTBORN_EVIDENCE_DIR` | `<저장소>/output/host-ui-evidence` |
| `--size 1280x720` | | 1280x720 |
| `--sample` | | UI 견본 화면 |
| `--world <맵>` | | 월드 화면. 맵 id(Unity 씬 이름) 또는 LDtk 레벨 이름(예: Shop, Town, House) |
| `--spawn <도착 지점>` | | 맵 목록에서 그 맵의 첫 도착 지점 |
| `--maps <맵 목록>` | `ROOTBORN_WORLD_MAPS` | `<저장소>/game/world/maps.json` |
| `--interactions <파일>` | | `<저장소>/game/world/interactions.json` |
| `--hour <시각>` | | 8(Unity 판 학생 생활 하루 시작). `--world`일 때만(슬롯은 학생 시각을 따른다) |
| `--time-scale <배속>` | | 1. 하루 길이는 data/time의 TimeDefinition(600초). `--world`일 때만 |
| `--save-root <세이브 폴더>` | `ROOTBORN_SAVE_ROOT` | 없음. `--slot`을 쓰면 반드시 준다. `--slot`·`--world`·`--sample` 없이 주면 슬롯 선택 화면부터 시작한다 |
| `--lang <언어>` | | ko. `data/localization`의 파일 이름 |
| `--ui-theme <테마>` | `ROOTBORN_UI_THEME` | urban(Unity 판의 도시형 몰딩). `game/themes/<이름>`의 이름이나 테마 폴더 경로. 빠진 스킨·아이콘이 있으면 목록을 보이고 멈춘다 |
| `--slot <슬롯 id>` | | 슬롯으로 월드를 시작한다. `metadata.json`이 없으면 새 게임(장면 목록 데이터의 새 게임 자리), 있으면 `world-progress.json`의 자리에서 이어 한다. `--world`와 함께 줄 수 없다 |

`--sample`도 `--world`도 `--slot`도 없으면 슬롯 선택 화면(슬롯 3개: 새 이야기 → 캐릭터 만들기, 이어서 시작, 삭제)부터 띄운다. 세이브 폴더가 없으면 안내 후 끝난다.
캐릭터 만들기의 시작하기는 슬롯 정보(이름, 고른 겉모습, 시각으로 만든 시드)를 쓰고 그 슬롯으로 `--slot`과 같게 시작한다. 슬롯으로 시작하면 맵에 들어갈 때마다(처음 들어갈 때 포함) `world-progress.json`에 쓴다.
읽을 수 없는 세이브 파일은 덮어쓰지 않고 시작을 거절한다.
캐릭터는 슬롯의 옷 선택(`wearables-local-player.json`, 없으면 칸 기본값)으로 성장 단계 몸에 옷·머리를 겹쳐 그린다.
낮밤은 맵 목록의 `dayNight`가 켜진 맵(마을)에만 입힌다. 슬롯으로 시작하면 낮밤·NPC 자리는 학생 날·시각(`GameSession.StudentDay`·`StudentHour`)을, 캐릭터 몸은 `GrowthStageId()`를 따른다. `interactions.json`에서 `placementSurface`가 있는 맵(집)에 들어가면 그 맵에서 방 칸을 만들어 `GameSession.EnterHouse`를 부른다. NPC는 일정(data/npcs)과 시간대로 `interactions.json`의 장소 자리에 선다.
슬롯으로 시작하면 같은 정의로 `GameSession`도 시작하고, 월드 위에 화면 쌓기를 올린다(`--world`도 화면 쌓기는 올린다).
E·Space·Ctrl(Unity 판과 같은 상호작용 키)을 누르면 앞의 대상과 상호작용한 이벤트가 나오고(`[상호작용] 맵:종류/id`), 그 종류에 등록된 화면이 열린다.
Esc는 설정, I는 가방, C는 상태, Tab은 목표 수첩(등록된 화면만). HUD(시계, 체력·집중, 추적 목표, HUD 버튼, 상호작용 안내 글자)는 월드를 띄우면 늘 떠 있다. NPC 앞에서 E는 대화, 장소 지점(location-activity)에서 E를 누르면 그 장소의 하루 이벤트가 열린다. 이동을 막지 않는 화면의 버튼은 Shift+방향 키로 옮기고 Enter로 누른다. 이동을 막는 화면이 떠 있거나 글자를 치는 동안은 걷지 않는다. 월드 화면에서는 화살표나 WASD를 누르고 있는 동안 걷고, 문에 들어서면 0.35초 뒤 목적지 맵으로 간다. 증거 폴더 기본값은 `<저장소>/output/host-world-evidence`.

미니게임은 알바·연습·시설 일이 예약되면 저절로 열리고(누가 예약했든 "게임 진행이 바뀜" 알림으로 본다), 일터 지점(`part-time-work`)·연습 지점(`career-practice`)에서 E를 누르면 예약한다. Space 박자, R 돌리기, A/D·←/→ 줄 옮기기, 포인터로 단추·칸·조준 판. 시간은 Host가 넘기는 프레임 dt(`ScreenStack.Tick`)로만 흐르고, 끝나면 한 번 정산하고 결과를 보인 채 둔다. Esc·닫기는 끝나지 않은 판을 포기로 정산하고 닫는다. 도전 종류마다 그리는 부분은 `Minigames/ChallengeViewCatalog`에 한 줄로 등록한다.
목표 수첩(Tab, 1440x840, 걸으면서 볼 수 있음)은 분류 줄 + 목록(보이는 줄만 그림) + 상세 + 추적·행동 단추다. 항목은 `Objectives/ObjectiveJournalSourceCatalog`에 한 줄씩 등록한 모으는 쪽이 `GameSession` 조회로 만들고, 글자는 키와 값(`JournalText`)으로만 넘긴다. 추적 목표 HUD는 수첩에서 "추적"한 항목 하나만 보인다(추적하지 않았으면 숨음). 추적 지정은 지금 메모리에만 있다(세이브 모양이 Core에 없다).

견본·월드 화면에서 F12는 지금 화면을 증거 폴더에 저장한다. 견본 화면에서 Esc는 포커스를 풀고, 포커스가 없으면 끝낸다.

## 화면 더하기

1. `Rootborn.Screens/<영역>/`에 표시 모델과 `GameScreen`을 상속한 화면 클래스를 둔다. `Build()`에서 `UiNode` 나무를 한 번만 만들고,
   `OnOpened`·`OnChanged(topic)`에서 값만 고친다. 매 프레임 상태를 읽지 않는다. Unity 수치는 `UnityLayout.Centered(x, y, w, h)`로 옮긴다.
2. 문구는 `T(key)`로만 받는다(코드에 한글을 적지 않는다). 상태를 바꿀 때는 `Services.Commands.Run(s => s.명령(...), ChangeTopics.무엇)`.
3. `ScreenCatalog.Register`에 한 줄: `r.Add(id, 층, blocksMovement, s => new 화면(s)).OpenedBy(키).OpenedByInteraction("상호작용 종류")`.
   이동을 막는지는 Unity 판과 같게(설계 `docs/superpowers/specs/2026-11-12-screens-port-design.md` 3.1절).
4. 테스트: `Rootborn.Screens.Tests`에서 화면을 눌러 보고 `TextKeys`가 두 언어에 다 있는지, `Rootborn.Host.Tests`에서 `PlayerDriver`로
   열고(`OpenScreenWithKey`·`WaitForScreen`) 누르고(`PressButton`) 찍는다(`ScreenshotScreen`).

## 새 UI 그림을 넣는 법

1. `game/themes/urban`을 `game/themes/<새 이름>`으로 복사한다.
2. 폴더 안 PNG를 새 그림으로 바꾼다(크기는 자유).
3. `theme.json`의 그림 파일, 칸(`rect`), 9조각 테두리(`border`)를 고친다. 형식과 필요한 스킨 5개·아이콘 18개는 `game/themes/README.md`.
4. `--ui-theme <새 이름>`(또는 폴더 경로)으로 띄워 본다. 빠진 것이 있으면 시작할 때 목록이 나온다.
5. 검사 테스트: `dotnet test game/tests/Rootborn.Host.Tests --filter "FullyQualifiedName~TestThemeScreensTests|FullyQualifiedName~UiSkinDataTests"`
   (테두리·크기가 다른 시험용 테마로 등록된 모든 화면을 그려 예외·빠진 아이콘·패널 밖 글자를 본다. 보고서는 `output/host-theme-evidence/theme-report.md`).

## 한 곳에만 두는 것

- SDL2 직접 호출: `Rootborn.Host/Input/Sdl2Native.cs`
- MonoGame 내부 `SdlRunLoop` 리플렉션 호출: `tests/Rootborn.Host.Tests/PlayerDriver.cs`의 `FrameStepper`
- 키보드·포인터·글자 입력 읽기: `Rootborn.Host/Input/Devices.cs`의 `KeyboardDevice`, `OsPointer`, `TextInputDevice`
- UI 그림(스킨·아이콘·글자 색): 테마 폴더 `game/themes/<이름>/theme.json`(형식과 필요한 그림 목록은 `game/themes/README.md`). 읽기·검사는
  `Rootborn.Host/Rendering/ThemeFolder.cs`, 모든 테마가 가져야 할 스킨·아이콘은 `ThemeRequirements.cs`, 그림 id(`icon:<키>`, `item:<id>`, `<PNG>#<이름>`) 풀기는 `UiSkins.cs`의 `ImageCatalog`
- 키 → 화면, 상호작용 종류 → 화면: `Rootborn.Screens/ScreenCatalog.cs`
- 맵 목록(맵 id, LDtk 레벨, 문 목적지, 도착 지점, 충돌 규칙, 보이는 높이, 낮밤): `game/world/maps.json`. 맵 크기는 적지 않는다(레벨에서 읽는다)
- 맵별 상호작용 물건과 NPC 자리, 반경·우선순위: `game/world/interactions.json`

MonoGame 버전을 올릴 때는 앞의 두 곳을 먼저 확인하고 `Rootborn.Host.Tests`를 돌린다.

## 테스트 근거의 종류

- `Rootborn.Data.Tests`: 자동 테스트. 종류를 더하면 `Rootborn.Data/Builders`에 등록하거나 `NotRules`·`NotPortedYet`에 적어야 `RealDataTests`가 통과한다.
- `Rootborn.World.Tests`: 자동 테스트. 화면 근거가 아니다.
- `Rootborn.Net.Tests`: 자동 테스트. UDP는 같은 기계의 루프백 소켓 둘이고, 두 프로세스·두 기계·Steam 근거가 아니다.
- `Rootborn.Ui.Tests`, `Rootborn.Screens.Tests`: 자동 테스트. 화면 근거가 아니다.
- `Rootborn.Host.Tests`: 장치 이벤트 주입. 키와 글자는 SDL 이벤트 큐에, 포인터는 게임의 포인터 장치 계층에 넣는다.
  클릭은 UI 포인터 판정을 거친다. OS 입력 계층은 거치지 않으므로 보고할 때 "OS 입력 계층 미확인"으로 적는다.
- 스크린샷은 그려진 화면 버퍼에서 저장한다. 보고 전에 직접 열어 본다.
