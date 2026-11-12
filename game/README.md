# game/ — ROOTBORN MonoGame 판

엔진 이전 규칙은 `.claude/rules/engine-migration.md`, 전체 설계는 `docs/superpowers/specs/2026-11-11-unity-removal-design.md`.
지금 있는 것은 실행 프로그램 뼈대와 UI 토대(프로젝트 3b), 데이터 로더와 세이브 저장소, 월드 화면(LDtk 맵을 그리고 걸어서 문으로 오감),
화면 토대(프로젝트 4 묶음 0: 화면 쌓기, 키·상호작용 등록 표, 현지화, 도시형 몰딩 스킨, 설정 화면)다.

| 폴더 | 내용 | MonoGame 참조 |
|---|---|---|
| `Rootborn.Core` | 게임 규칙, 진행 상태, 세이브 모양, 월드 판정. 소스는 `Rootborn.Core/src/<영역>`. 엔진·파일·JSON 라이브러리를 참조하지 않는다 | 없음 |
| `Rootborn.Data` | `data/` JSON으로 Core 정의 만들기(`GameDataLoader.Load(dir, RootbornBuilders.Register)`), 세이브 파일 저장소(`FileSaveStore`) | 없음 |
| `Rootborn.Game` | 게임 한 판(`GameSession.StartFromData(data, 저장소, 슬롯, 플레이어)`, 또는 `Start(정의, GameStartSettings.FromDefinitions(정의), …)`). 화면이 부를 명령과 조회(가구는 `EnterHouse(방 칸, "house")` 뒤에). 규칙은 Core가 판단하고 여기서는 영역을 잇고 저장만 한다 | 없음 |
| `Rootborn.World` | LDtk 파일(.ldtk·.ldtkl)을 그대로 읽고, 맵 목록 `game/world/maps.json`으로 Core 월드(충돌·문·도착 지점)와 그릴 목록을 만든다. 장면 목록·낮밤 조명은 정의 로더가 만든 것을 받는다(`WorldTravelData.From`, `DayNightLighting.From`). 새 맵 검사 `MapChecks`. 맵을 그리는 사람의 약속은 `game/world/README.md` | 없음 |
| `Rootborn.Ui` | 배치, 포인터 판정, 클릭, 포커스 이동, 9조각 계산과 부품(패널, 글자, 여러 줄 글자, 그림, 버튼, 칸, 글자 칸, 스크롤 목록, 탭 줄, 게이지, 켬/끔, 미끄럼 막대, 카드 줄·격자). 그리기 명령 목록만 낸다 | 없음 |
| `Rootborn.Screens` | 화면들, 화면 쌓기(`ScreenStack`, `GameSession.Changed`를 받아 열린 화면에 넘긴다), 등록 표(`ScreenCatalog`), 현지화 문구(`GameText`), 언어 고르기(`LanguageChoice`), 설정 저장(`SettingsStore`) | 없음 |
| `Rootborn.Net` | 멀티플레이 바탕: 전송 인터페이스(`ITransport`)와 같은 프로세스 전송(`LoopbackNetwork`, 지연·손실·순서 바뀜)·UDP 전송(`UdpTransport`, LiteNetLib 1.3.5 고정), 메시지 종류 등록 표(`RootbornMessages`, 종류 번호 + 형식 버전 + 이진 몸통), 접속 세션(`SessionServer`·`SessionClient`: 승인·신원·위치·겉모습·공용 시간). 각자 자기 판을 돌리는 모델이라 진행은 보내지 않는다. 규칙(정원, 도착 자리, 공용 시간, 사이 메우기)은 Core `Rootborn.Core.Network` | 없음 |
| `Rootborn.Host` | 창, 화면 배율, 입력 장치 계층, 한글 조합 입력, TTF 글자, 그리기, 화면 저장, 견본 화면, 방 접속(`Net/RoomConnection`)과 다른 참가자 그리기(`World/RemoteAvatars`) | 3.8.6.1 고정 |
| `Rootborn.Host` | 창, 화면 배율, 입력 장치 계층, 한글 조합 입력, TTF 글자, 그리기, 화면 저장, 견본 화면 | 3.8.6.1 고정 |
| `Rootborn.Server` | 화면 없는 데디케이티드 서버(방 하나): 인자 읽기 → 데이터 읽기 → 슬롯 열기 → UDP로 듣기 → 고정 간격 루프 → 끝낼 때 저장하고 닫기. 방의 논리는 `Rootborn.Net/Room`(`RoomServer`). 인자는 아래 "서버" | 없음 |
| `tests/Rootborn.Core.Tests` | Core 규칙 단위(생성자로 만든 정의, 메모리 저장소). 엔진 없이 몇 초에 돈다 | 없음 |
| `tests/Rootborn.Data.Tests` | 정의 로더, 실제 `data/` 전체 읽기, 세이브 저장소, Unity 판 QA 세이브 고정 파일 | 없음 |
| `tests/Rootborn.World.Tests` | 모든 맵에 도는 새 맵 검사(`MapContractTests`, 지금 맵의 알려진 문제 고정), 새 게임 자리에서 메인 체인 지점까지 걷기(`MainChainWalkTests`), 실제 LDtk로 맵 만들기, Unity 씬에 구워진 충돌과 칸 단위 대조, 도착 지점·문 목적지, 크기가 다른 집, 장면 목록·고정 세이브, 캐릭터 그림, 낮밤, 상호작용 데이터 | 없음 |
| `tests/Rootborn.Game.Tests` | 실제 `data/`와 임시 폴더 저장소로 새 게임 초기값, 메인 체인 끝까지(화면 없이, 가구 방 칸은 실제 집 맵), 성인기, 보상 한 번만, Unity QA 세이브·스냅샷 이전 슬롯 이어 하기 | 없음 |
| `tests/Rootborn.Net.Tests` | 같은 프로세스 전송으로 서버 1 + 클라이언트 여럿(승인·정원 초과, 서로 보임, 맵 옮김, 하루 끝 준비, 늦게 들어온 사람, 끊김·재접속·시간 초과, 손실·지연·순서 바뀜, 깨진 메시지), 메시지 형식, UDP 루프백(포트는 운영체제가 준다) | 없음 |
| `tests/Rootborn.Ui.Tests` | 화면 없이 도는 테스트 | 없음 |
| `tests/Rootborn.Server.Tests` | 서버 인자(`ServerOptionsTests`), 데이터 지문, 방 루프·방 서버(메모리 저장소), 빌드한 서버를 별도 프로세스로 띄워 루프백 UDP로 들어가기(`ServerProcessTests`, 두 기계·Steam 아님) | 없음 |
| `tests/Rootborn.Simulation.Tests` | 대본 없이 정책이 화면에 보이는 것만 보고 여러 날을 플레이(입력은 화면 포인터 판정·키 배정을 거친다): 걸음마다 불변식, 날마다 저장 뒤 다시 읽기, 같은 씨앗은 같은 플레이·기록 다시 재생. 씨앗·날 수는 `ROOTBORN_SIM_SEEDS`·`ROOTBORN_SIM_DAYS`, 기록은 `output/simulation/`. 알려진 결함 재현은 이유와 함께 건너뛴다. 사람의 실제 플레이를 대신하지 않는다 | 없음 |
| `tests/Rootborn.Screens.Tests` | 창 없이 도는 화면 테스트: 화면 쌓기의 층·배타·이동 막음·입력 넘기기, 실제 `ko.json`·`en.json`, 설정·HUD·대화·하루 이벤트·가방·상태 화면을 실제 `data/`와 `GameSession`으로 눌러 보기(알려진 빠진 현지화 키는 `KnownMissingKeys`) | 없음 |
| `tests/Rootborn.Host.Tests` | 실제 창과 게임 루프를 한 프레임씩 돌리는 테스트(새 게임 → 집 → 마을 → 가게 → 이어 하기 `NewGameJourneyTests`, 메인 체인 처음부터 끝까지 `MainChainHostTests`, 영어로 모든 화면 찍기 `EnglishScreensTests`, `--frames`·`--screenshot` 프로세스 시험 `FramesScreenshotTests`, 같은 프로세스 안 방에 UDP 루프백으로 들어가 다른 참가자 보기·거절·끊김 `MultiplayerJoinHostTests` 포함) | 있음 |

## 데이터·그림·맵 파일의 자리

- **게임 데이터는 `data/`가 원본이다.** 사람이 직접 고친다(`data/README.md`). Unity 에셋에서 바꾸던 변환기는 Unity를 지울 때 지웠다(태그 `unity-playable-20261111`에 있다).
- **그림(PNG)과 맵(LDtk)은 아직 `Assets/` 아래에 있다.** Unity가 없는데 폴더 이름이 `Assets`인 것은 Unity 시절 이름이다.
  게임이 읽는 파일만 남겼고(`Tools/publish/content.txt`가 고르는 것), 새 그림·맵이 오면 새 자리로 가면서 사라진다.
  옮기지 않은 이유: 그림·맵을 전부 새로 그릴 예정이라 지금 옮기면 같은 경로 수천 곳을 두 번 고친다(`docs/superpowers/specs/2026-11-12-unity-removal-plan.md` 4절).
- 저장소 루트는 `data/registry.json`이 있는 폴더다(실행 프로그램·서버·테스트 도우미가 이것으로 찾는다).

## 명령

```
dotnet test game/Rootborn.sln
dotnet test game/tests/Rootborn.Core.Tests
dotnet run --project game/Rootborn.Host -- --sample --size 1920x1080
dotnet run --project game/Rootborn.Host -- --world Shop --size 1920x1080
dotnet run --project game/Rootborn.Host -- --save-root <세이브 폴더>
dotnet run --project game/Rootborn.Host -- --save-root <세이브 폴더> --slot slot-0
```

인자와 환경 변수(인자가 먼저다):

| 인자 | 환경 변수 | 없을 때 |
|---|---|---|
| `--root <저장소>` | `ROOTBORN_REPO_ROOT` | 실행 파일 위치에서 위로 올라가며 `data/registry.json`이 있는 폴더를 찾는다(서버와 같다) |
| `--font <TTF>` | `ROOTBORN_FONT` | 운영체제 한글 글꼴을 찾는다. 못 찾으면 오류 |
| `--evidence <폴더>` | `ROOTBORN_EVIDENCE_DIR` | `<저장소>/output/host-ui-evidence` |
| `--size 1280x720` | | 1280x720 |
| `--fullscreen` / `--windowed` | | 설정 파일에 저장된 표시 모드(F11이나 설정 화면의 표시 줄로 바꾸면 바로 적용되고 저장된다), 그것도 없으면 테두리 없는 전체 화면. `--size`를 주면 창 모드. 테스트 모드는 늘 창 모드. 창은 1280x720(그보다 작은 `--size`면 그 크기)보다 작게 줄지 않는다 |
| `--sample` | | UI 견본 화면 |
| `--world <맵>` | | 월드 화면. 맵 id(Unity 씬 이름) 또는 LDtk 레벨 이름(예: Shop, Town, House) |
| `--spawn <도착 지점>` | | 맵 목록에서 그 맵의 첫 도착 지점 |
| `--maps <맵 목록>` | `ROOTBORN_WORLD_MAPS` | `<저장소>/game/world/maps.json` |
| `--interactions <파일>` | | `<저장소>/game/world/interactions.json` |
| `--hour <시각>` | | 8. `--world`일 때만(슬롯은 저장된 학생 시각에서 이어진다) |
| `--time-scale <배속>` | | 1. 하루 길이는 data/time의 TimeDefinition(600초). `--world`면 시험 화면의 시계, 슬롯이면 학생의 흐르는 시계에 곱한다(0이면 시계가 서 있다) |
| `--save-root <세이브 폴더>` | `ROOTBORN_SAVE_ROOT` | 없음. `--slot`을 쓰면 반드시 준다. `--slot`·`--world`·`--sample` 없이 주면 슬롯 선택 화면부터 시작한다 |
| `--lang <언어>` | | 저장된 설정(세이브 폴더의 설정 파일) → 운영체제 언어 → 기준 언어(ko) 순서. 언어 이름은 `data/localization`·`game/localization`의 파일 이름. 설정 화면의 언어 줄에서 바꾸면 저장된다 |
| `--ui-theme <테마>` | `ROOTBORN_UI_THEME` | urban(Unity 판의 도시형 몰딩). `game/themes/<이름>`의 이름이나 테마 폴더 경로. 빠진 스킨·아이콘이 있으면 목록을 보이고 멈춘다 |
| `--slot <슬롯 id>` | | 슬롯으로 월드를 시작한다. `metadata.json`이 없으면 새 게임(장면 목록 데이터의 새 게임 자리), 있으면 `world-progress.json`의 자리에서 이어 한다. `--world`와 함께 줄 수 없다 |
| `--join <호스트:포트>` | `ROOTBORN_JOIN` | 없음(주소 기본값이 없다). 서버 주소로 방에 들어간다. `--save-root`가 필요하다. 시작 화면에서 들어가기 창을 그 주소로 열고, `--slot`도 주면 그 슬롯의 인물로 바로 접속을 시작해 방이 받아들이면 그 슬롯으로 월드를 띄운다. `--world`·`--sample`과 함께 줄 수 없다 |
| `--frames <N>` | | 없음(끝내지 않는다). N프레임 그린 뒤 끝낸다. 배포 폴더에서 창을 띄워 화면을 확인할 때 쓴다 |
| `--screenshot <PNG>` | | 없음. `--frames`와 함께 준다: 끝내기 직전 화면을 그 파일로 저장한다(없는 폴더는 만든다) |
| `--day-copies <N>` | `ROOTBORN_DAY_COPIES` | 3(`GameSession.DefaultDayCopiesToKeep`). 다음 날을 시작할 때 슬롯 폴더의 `backups/<날>/`에 남길 사본 수(최근 것부터). 0이면 남기지 않는다. 사본을 못 쓰면(디스크가 참 등) 그날 저장은 끝난 채로 두고 오류 출력에 `[세이브]` 한 줄을 남긴다 |
| `--host <IP 주소:포트>` | `ROOTBORN_HOST_ROOM` | 없음(주소 기본값이 없다). 내 PC에서 방을 연다. 이 PC에서 들을 IPv4 주소와 포트(0이면 운영체제가 빈 포트를 준다). `--save-root`가 필요하다. 시작 화면에서 방 열기 창을 그 주소로 열고, `--slot`도 주면 그 슬롯으로 바로 방을 열고 들어가 월드를 띄운다. `--join`·`--world`·`--sample`과 함께 줄 수 없다 |
| `--max-players <정원>` | `ROOTBORN_MAX_PLAYERS` | 4(방 설정 기본값). 1~8. `--host`와 함께만 준다(환경 변수는 `--host`가 없으면 쓰지 않는다 — 서버와 같은 이름). 방을 처음 만들 때만 쓰고 방 설정 파일에 남는다(이미 있는 방은 저장된 정원) |
| `--share <조각,...>` | `ROOTBORN_ROOM_SHARE` | 없음(방 열기 창에서 고른다). 서버의 `--share`와 같은 이름·규칙(`Rootborn.Net/Room/RoomShareArgument`): `wallet`(돈), `house`(집과 가구), `town`(마을 상태)을 쉼표로, `none`이면 시간만. 방 열기 창에 처음 켜 둘 것이고 `--slot`과 함께면 그대로 연다. `--host`와 함께만 준다. 방을 처음 만들 때만 쓴다 |

`--sample`도 `--world`도 `--slot`도 없으면 슬롯 선택 화면(슬롯 3개: 새 이야기 → 캐릭터 만들기, 이어서 시작, 삭제)부터 띄운다. 세이브 폴더가 없으면 안내 후 끝난다.
캐릭터 만들기의 시작하기는 슬롯 정보(이름, 고른 겉모습, 시각으로 만든 시드)를 쓰고 그 슬롯으로 `--slot`과 같게 시작한다. 슬롯으로 시작하면 맵에 들어갈 때마다(처음 들어갈 때 포함) `world-progress.json`에 쓴다.
읽을 수 없는 세이브 파일은 덮어쓰지 않고 시작을 거절한다.
슬롯이 손상돼(이 플레이어의 다른 파일은 있는데 스냅샷이 없음, 파일이 깨짐) 시작을 거절하면 슬롯 선택 위에 손상 알림(`save-slot-damaged`)이 뜬다: 문제 파일마다 한 줄(없는 파일·읽을 수 없는 파일), 단추는 돌아가기뿐이다. `--slot`으로 띄웠을 때도 슬롯 선택과 그 알림을 띄운다(예외로 끝나지 않는다). 새 게임으로 덮기·사본에서 되살리기는 아직 없다(기획 결정 대기). 슬롯 선택 칸에서는 손상을 미리 구별하지 않는다(슬롯 정보만 읽고, 손상은 시작할 때 모든 파일을 읽어야 알 수 있다).
세이브 폴더 배치: `<세이브 폴더>/<슬롯>/`에 영역마다 JSON 파일 하나. 한 명령의 저장은 묶음 하나로 `<슬롯>/.save-batch/`에 다 쓴 뒤 바꿔 넣는다(도중에 꺼지면 다음에 읽을 때 마저 넣거나 버려서 전부 옛것 또는 전부 새것). 다음 날을 시작할 때 `<슬롯>/backups/<날 번호>/`에 슬롯 파일 사본을 남긴다(`--day-copies`, 맨 위 칸 파일만, `.`으로 시작하는 임시 파일 빼고. 임시 폴더에 다 복사한 뒤 이름을 바꿔 넣어 반쯤 찬 사본은 남지 않는다).
되살리는 명령은 없다: 사본 폴더의 파일을 슬롯 폴더(같은 슬롯이든 다른 이름의 슬롯이든)에 복사해 넣고 그 슬롯으로 이어 한다. 슬롯 폴더를 다른 이름으로 복사해도 같다 — 세이브 안의 슬롯 이름이 옛 이름이어도 지금 슬롯으로 읽고 저장한다(Game `CopiedSlotTests`).
캐릭터는 슬롯의 옷 선택(`wearables-local-player.json`, 없으면 칸 기본값)으로 성장 단계 몸에 옷·머리를 겹쳐 그린다.
낮밤은 맵 목록의 `dayNight`가 켜진 맵(마을)에만 입힌다. 슬롯으로 시작하면 낮밤·NPC 자리는 학생 날·시각(`GameSession.StudentDay`·`StudentHour`)을, 캐릭터 몸은 `GrowthStageId()`를 따른다. `interactions.json`에서 `placementSurface`가 있는 맵(집)에 들어가면 그 맵에서 방 칸을 만들어 `GameSession.EnterHouse`를 부른다. NPC는 일정(data/npcs)과 시간대로 `interactions.json`의 장소 자리에 선다.
흐르는 시간(설계 `docs/superpowers/specs/2026-11-12-flowing-time-design.md`): 하루 시작·취침 시각·밤 체력 감소·늦잠 회복 감소는 `game/time.json`(하루 길이는 data/time), 시계는 `GameSession.AdvanceTime(지난 초, 메뉴 열림)`으로 가고 `Sleep()`으로 잔다(규칙은 Core `TimeRules`·`StudentLifeProgress.Clock`, 화면·Host 연결은 다음 단계).
슬롯으로 시작하면 같은 정의로 `GameSession`도 시작하고, 월드 위에 화면 쌓기를 올린다(`--world`도 화면 쌓기는 올린다).
E·Space·Ctrl(Unity 판과 같은 상호작용 키)을 누르면 앞의 대상과 상호작용한 이벤트가 나오고(`[상호작용] 맵:종류/id`), 그 종류에 등록된 화면이 열린다.
Esc는 설정, I는 가방, C는 상태, Tab은 목표 수첩(등록된 화면만). HUD(시계, 체력·집중, 추적 목표, HUD 버튼, 상호작용 안내 글자)는 월드를 띄우면 늘 떠 있다. NPC 앞에서 E는 대화, 장소 지점(location-activity)에서 E를 누르면 그 장소의 하루 이벤트가 열린다. 이동을 막지 않는 화면의 버튼은 Shift+방향 키로 옮기고 Enter로 누른다. 이동을 막는 화면이 떠 있거나 글자를 치는 동안은 걷지 않는다. 월드 화면에서는 화살표나 WASD를 누르고 있는 동안 걷고, 문에 들어서면 0.35초 뒤 목적지 맵으로 간다. 증거 폴더 기본값은 `<저장소>/output/host-world-evidence`.

발견 지점(`discovery`)에서 E를 누르면 알림 층에 발견 알림(`discovery-notice`)이 뜬다: 화면이 `GameSession.Discover(지점 id)`를 부르고 발견 이름, 결과(새로 알게 됨·이미 살펴봄·아직 알 수 없음), 바뀐 성장 줄을 보인 뒤 4초 뒤 저절로 닫힌다(이동을 막지 않고 포인터도 가져가지 않는다). 한 번만 적용되는지와 저장은 Core·`GameSession`이 정한다.
F1은 단축키 안내(모달, 이동을 막음, 시작 화면에서도 열림, 설정의 "단축키" 단추로도). 줄은 등록 표의 키 배정(`ScreenRegistry.KeyBindings`, 하는 일 문구 키 `ui.keys.<화면 id>`), 화면 쌓기의 버튼 옮기기, Host 이름표(`Input/Devices.cs`의 `KeyGuideNames`)에서 만든다 — 키 배정을 바꾸면 이름표에도 한 줄.
서버 주소로 들어가기(멀티플레이, 설계 `docs/superpowers/specs/2026-11-12-multiplayer-design.md` 6절): 슬롯 선택 아래 "서버 주소로 들어가기" → 주소(`호스트:포트`)와 들어갈 인물(이 PC의 이야기가 있는 슬롯)을 고르고 들어가기.
접속 중·실패·거절 이유가 창에 뜬다(이유 코드 → 문구 키 `ui.join.reason.<코드>`). 방이 받아들이면 혼자 하기와 같은 화면으로 그 슬롯을 시작하고, 같은 맵의 다른 참가자를 그들의 겉모습으로 그린다(이름표 없음 — Unity 판과 같다, 몸은 걷기를 막지 않는다).
내 위치·겉모습은 Net 설정의 횟수로 보낸다. 서버가 닫히거나 응답이 끊기면 시작 화면으로 돌아가 알림을 보이고, 게임을 끄면 서버에 나간다고 알린다.
손님 저장(설계 3.1절): 방(데디케이티드 서버, 남이 내 PC에서 연 방)에 들어가면 방이 내려 준 그 인물의 세이브로 게임 한 판을 시작하고(처음 온 인물은 새 게임 —
들어간 직후 알림에 "새 캐릭터로 시작해요" 또는 "이어 해요"),
저장할 때마다 바뀐 파일을 방으로 보낸다(`Rootborn.Net/Room/RoomGuestLink`, 나눠 보내기와 크기 한도는 `RoomTransfer` 한 곳). 이 PC의 슬롯에는 진행을 남기지 않는다.
방이 받았다고 답하지 않은 저장은 들고 있다가 다시 들어갈 때 다시 보낸다. 내 PC에서 연 방은 손님 진행을 내 슬롯 폴더 안 `guests/<플레이어>/`에 받는다
(데디케이티드 서버와 같은 `Rootborn.Net/Room/GuestSaveFolder` — 같은 자리·같은 검사). 방을 연 나 자신은 손님 저장을 받지 않는 사람이라 그 슬롯 그대로 돈다
(그 폴더는 슬롯 맨 위 파일만 읽는 혼자 하기에 끼어들지 않는다). 예전에 자기 PC 슬롯으로 들어갔던 진행(임시 방식)은 옮기지 않는다. 어느 쪽인지는 `Rootborn.Host/Net/GuestProgress.cs` 한 곳이다.
같이 하는 방의 시간(흐르는 시간 설계 `docs/superpowers/specs/2026-11-12-flowing-time-design.md` 3.1·3.3절): 방의 시계(날, 분)는 방을 가진 쪽(서버·내 PC의 방)에서 흐르고 멈추지 않는다(아무도 없을 때만 멈춘다).
게임 한 판은 손님 저장 여부와 상관없이 방의 시각을 따라간다(`Rootborn.Net/Session/RemoteRoomTime` — 서버는 바뀔 때와 5초마다 보내고 사이는 같은 속도로 메운다).
같이 할 때 활동은 시계를 건너뛰지 않고 끝나는 시각까지 "하는 중"이다(HUD에 끝나는 시각, 다른 활동은 거절 `ui.prompt.busy`). 잠자리에서 자면 "자는 중" 화면(아직 깨어 있는 사람)이
뜨고 07:00까지 깨어날 수 없다. 모두 잠들면 곧바로, 아니면 07:00에 모두에게 다음 날이 온다(깨어 있던 사람은 자지 않은 날 — 회복 없음). 방의 "때"와 넘기기는 없다.
아직 이 PC에 남는 것: 맵 자리(`world-progress.json`)와 옷 선택. 돈·집과 가구·마을 상태를 같이 쓰는 방이면 게임 한 판의 지갑·집 배치·월드 상태 깃발이 방의 것을 따른다(`GameSession.Shares`).
손님 저장을 받지 않는 사람(내 PC에서 방을 연 나)의 공유 조각 요청은 방 접속이 프레임마다 방으로 보낸다(`RoomGuestLink.FlushRequests`, 메시지 `RoomRequestsMessage`).
손님의 요청은 저장과 함께 가므로 방은 손님이 보낸 이 메시지를 버린다.
같이 쓰는 집(`house`): 방이 놓인 가구 목록의 주인이다. 놓기·옮기기·치우기는 요청 → 방이 Core 배치 규칙으로 판정(같은 칸은 먼저 온 요청만) → 허락된 상태로 모두의 집을 다시 채운다.
방의 판정 칸은 가구 방 맵에서 만든다(내 PC의 방: 화면과 같은 `RoomAwareMaps.GridForSurface`, 서버: `Rootborn.Server/HouseGrids`). 가구 배치 화면은 기다리는 동안
"방의 허락을 기다리는 중", 거절되면 "다른 사람이 먼저 그 자리를 썼어요"를 보인다. 해금·구매는 각자이고 놓아도 줄지 않아 누가 치워도 놓은 사람은 그 가구를 그대로 가진다.
메시지 하나(4KB)에 들어가지 않는 조각 상태(가구가 많은 집)는 나눠 보낸다(`RoomPieceStates`, 메시지 31).
돈을 같이 쓰는 방이면 HUD 시계 칸의 잔액이 "방 지갑", 가방의 용돈 카드 상세 이름이 "방 지갑", 가게 화면의 잔액 줄도 방 지갑이다. 다른 사람이 벌거나 쓰면 알림으로 바뀐다.
돈 쓰는 명령(가게의 가구 구매, 성인 시설)이 방의 허락을 기다리는 동안은 기다림 문구를 보이고 다시 누르지 않으며, 거절되면 거절 문구를 보인다(`Screens/Hud/RoomMoneyText`).
설정 화면(Esc)의 방 줄 아래 줄에 그 방이 같이 쓰는 것을 보인다(남의 방에 들어간 사람도).
방에서의 신원은 슬롯 정보(만든 시각·시드)로 만든 그 인물의 고정 id다(슬롯의 플레이어 id는 모든 슬롯이 `local-player`라 두 PC가 서로를 밀어낸다). 방에 들어가지 않으면 Net 코드는 프레임마다 돌지 않는다.
게임 버전은 `1.0.0+<커밋>`이다(저장소 루트 `Directory.Build.props`, git이 없으면 `-p:ROOTBORN_COMMIT=<커밋>` 또는 `unknown-commit`). 데이터 지문은 `Rootborn.Data.DataFingerprint`(서버와 같은 셈법).
내 PC에서 방 열기(설계 6절 "내 PC에서 열기"): 슬롯 선택 아래 "방 열기" → 이 PC에서 들을 주소(`IP 주소:포트`), 정원, 방을 열 인물(이야기가 있는 슬롯),
같이 쓸 것(공유 조각마다 켬/끔 — 고를 수 없는 줄 "시간은 항상 같이, 가방·개인 성장은 항상 각자")을 고르고 열기(또는 `--host`).
고른 슬롯에 이미 방이 있으면(방 설정 파일 `room-settings.json`, 또는 그 파일보다 먼저 만든 방) 그 방을 만들 때 정한 같이 쓸 것·정원을 보이고 잠근다(정원을 적지 않은 옛 방이면 정원은 고른다).
같은 프로세스 안에 데디케이티드 서버와 같은 방(`Rootborn.Net/Room/HostedRoom` — `RoomServer`, 같은 프로세스 안 전송과 UDP를 묶어 듣는다)이 돌고, 내 게임은 들어가기와 같은 방 접속(`Net/RoomConnection`)으로 같은 프로세스 안 전송을 타고 들어간다. 다른 사람은 그 주소로 들어오기(UDP)로 온다.
방의 틱은 Host 게임 루프가 프레임마다 돌린다(`Net/RoomHosting.Update`, 고정 간격, 시계는 Host의 네트워크 시계 — 들어가기와 같은 실제 시간). 방을 열지 않으면 방 코드는 프레임마다 돌지 않는다.
방의 상태(방의 시계 `room-clock.json`)는 고른 슬롯 폴더에 저장되고, 같은 슬롯으로 다시 열면 잇는다. 그 슬롯은 혼자 이어 하기로도 그대로 읽힌다(혼자 하기는 방의 파일을 읽지도 지우지도 않는다).
방이 열려 있으면 설정 화면(Esc)의 아이콘 묶음 자리에 "방 열림 · 사람 수/정원 · 주소"와 방 닫기가 보인다. 방을 닫거나 게임을 끄면 들어와 있던 사람에게 닫혔다고 알리고(`server shutdown`) 방을 저장한 뒤, 닫기면 시작 화면으로 돌아간다(Unity 판에는 이때의 화면 처리가 없었다).
임시: 내 게임 한 판은 아직 방의 시계를 따르지 않는다(들어가기와 같다 — 시간 공유를 잇는 일은 남았다).
테스트: Net `HostedRoomTests`(같은 프로세스 + UDP), Screens `HostRoomScreenTests`(창 없이), Host `MultiplayerHostModeHostTests`(실제 창, 장치 이벤트 주입, 친구는 루프백 UDP의 `SessionClient`).

F1은 단축키 안내(모달, 이동을 막음, 시작 화면에서도 열림, 설정의 "단축키" 단추로도). 줄은 등록 표의 키 배정(`ScreenRegistry.KeyBindings`, 하는 일 문구 키 `ui.keys.<화면 id>`), 화면 쌓기의 버튼 옮기기, 화면 안에서만 듣는 키를 그 화면이 등록 때 넘긴 줄(`.GuidesKeys(...)`, 지금은 미니게임), Host 이름표(`Input/Devices.cs`의 `KeyGuideNames`: 걷기·상호작용·F11·F12·가구 배치 카메라 F·=·-)에서 만든다 — 키 배정을 바꾸면 이름표에도 한 줄.
미니게임은 알바·연습·시설 일이 예약되면 저절로 열리고(누가 예약했든 "게임 진행이 바뀜" 알림으로 본다), 일터 지점(`part-time-work`)·연습 지점(`career-practice`)에서 E를 누르면 예약한다. Space 박자, R 돌리기, A/D·←/→ 줄 옮기기, 포인터로 단추·칸·조준 판. 시간은 Host가 넘기는 프레임 dt(`ScreenStack.Tick`)로만 흐르고, 끝나면 한 번 정산하고 결과를 보인 채 둔다. Esc·닫기는 끝나지 않은 판을 포기로 정산하고 닫는다. 도전 종류마다 그리는 부분은 `Minigames/ChallengeViewCatalog`에 한 줄로 등록한다.
목표 수첩(Tab, 1440x840, 걸으면서 볼 수 있음)은 분류 줄 + 목록(보이는 줄만 그림, 긴 글은 말줄임) + 상세 + 추적·행동 단추다. 항목은 `Objectives/ObjectiveJournalSourceCatalog`에 한 줄씩 등록한 모으는 쪽이 `GameSession` 조회로 만들고, 글자는 키와 값(`JournalText`)으로만 넘긴다. 추적 목표 HUD는 수첩에서 "추적"한 항목 하나만 보인다(추적하지 않았으면 숨음). 추적 지정은 `GameSession.TrackObjective`로 세이브에 저장되어 이어 해도 남는다.

견본·월드 화면에서 F12는 지금 화면을 증거 폴더에 저장한다(`--frames N --screenshot <PNG>`는 N프레임 뒤 저장하고 끝낸다). 견본 화면에서 Esc는 포커스를 풀고, 포커스가 없으면 끝낸다.
포커스 테두리는 키보드(Shift+방향 키 등)로 옮긴 포커스에만 그린다. 포인터로 누른 단추·줄에는 그리지 않는다.

## 서버

```
dotnet run --project game/Rootborn.Server -- --save-root <세이브 폴더> --slot <슬롯 id> --bind <이 기계의 LAN IP> --port <포트>
```

인자와 환경 변수(인자가 먼저다, `game/Rootborn.Server/ServerOptions.cs`):

| 인자 | 환경 변수 | 없을 때 |
|---|---|---|
| `--save-root <세이브 폴더>` | `ROOTBORN_SAVE_ROOT` | 없음. 반드시 준다 |
| `--slot <슬롯 id>` | `ROOTBORN_SAVE_SLOT` | 없음. 반드시 준다. 슬롯이 없으면 새 방을 만들고, 있으면 이어서 연다 |
| `--bind <IP 주소>` | `ROOTBORN_SERVER_BIND` | 없음. 반드시 준다. 듣는 주소(이 기계의 LAN 주소 등). IP 주소만 받는다 |
| `--port <포트>` | `ROOTBORN_SERVER_PORT` | 없음. 반드시 준다. 0~65535, 0이면 운영체제가 빈 포트를 준다(로그의 `듣는 중: ` 줄에 찍힌다) |
| `--max-players <정원>` | `ROOTBORN_MAX_PLAYERS` | 4. 1~8(`RoomServer.MaxPlayersLimit`). 방을 처음 만들 때 방 설정 파일에 남고, 이미 있는 방은 저장된 정원을 쓴다(정원을 적지 않은 옛 방 설정이면 이 값. 다르면 로그에 남긴다) |
| `--share <조각,...>` | `ROOTBORN_ROOM_SHARE` | `none`(시간만 같이 쓴다). `wallet`(돈), `house`(집과 가구 — 서버가 데이터 폴더 옆 `game/world`와 집 맵(LDtk)을 읽는다), `town`(마을 상태 깃발)을 쉼표로. 방을 처음 만들 때만 쓰고 `room-settings.json`에 남는다 — 이미 있는 방은 저장된 설정을 따른다(다른 값을 주면 로그에 남기고 쓰지 않는다). 모르는 이름은 인자 오류 |
| `--day-copies <N>` | `ROOTBORN_DAY_COPIES` | 3. 방의 날이 바뀔 때 방 슬롯 파일(`<슬롯>/backups/<날>/`)과 손님 세이브마다(`<슬롯>/guests/<플레이어>/backups/<날>/`) 남길 사본 수. 0이면 안 남김 |
| `--root <저장소>` | `ROOTBORN_REPO_ROOT` | 실행 파일 위치에서 위로 올라가며 찾는다. 데이터는 `<저장소>/data` |
| `--data <데이터 폴더>` | `ROOTBORN_DATA_DIR` | `--root` 대신 데이터 폴더를 바로 준다 |
| `--help`, `-h` | | 도움말을 찍고 끝낸다 |

끝내기: Ctrl+C, 종료 신호(SIGINT·SIGTERM·SIGQUIT), 또는 콘솔에 `stop` 입력. 방 상태를 저장하고 닫는다. 입력이 닫히면(서비스로 돌 때) 끝내지 않는다.
끝 코드: 0 정상, 2 인자 오류(빠진 값 목록과 도움말), 3 데이터 오류, 4 세이브 오류, 5 듣지 못함. 로그는 운영자가 읽는 것이라 현지화하지 않는다(UTF-8).

방의 세이브: `<세이브 폴더>/<방 슬롯>/`에 방의 시계·방 설정·요청 장부·공유 조각(`room-piece-<조각>.json`), `guests/<플레이어>/`에 손님마다 세이브.
받은 손님 저장은 옛 세이브에 겹친 것으로 게임 한 판을 시작해 보는 손상 슬롯 검사와 같은 규칙(깨진 파일, 더 새 형식 버전, 다른 플레이어의 진행)과 이름·크기 한도로 검사하고,
걸리면 쓰지 않는다(옛 세이브를 지킨다). 저장된 방 설정에 모르는 조각이 있으면(더 새 버전이 만든 방) 세이브 오류(4)로 끝난다.

## 화면 더하기

1. `Rootborn.Screens/<영역>/`에 표시 모델과 `GameScreen`을 상속한 화면 클래스를 둔다. `Build()`에서 `UiNode` 나무를 한 번만 만들고,
   `OnOpened`·`OnChanged(topic)`에서 값만 고친다. 매 프레임 상태를 읽지 않는다. Unity 수치는 `UnityLayout.Centered(x, y, w, h)`로 옮긴다.
2. 문구는 `T(key)`로만 받는다(코드에 한글을 적지 않는다). 상태를 바꿀 때는 `Services.Session.명령(...)`을 바로 부른다. 무엇이 바뀌었는지는
   `GameSession.Changed`가 알리고 화면 쌓기가 열린 화면의 `OnChanged(topic)`로 넘긴다. 데이터에 이름 키가 없는 정의는 `NameOf`(이름 키 →
   id로 만든 규칙 키 `DataTextKeys.Name(id)` = `data.<id 소문자, 밑줄은 하이픈>.name` → 데이터의 글 → id)로 이름을 받는다. 문구는 `game/localization`에 그 규칙 키로 넣는다.
3. `ScreenCatalog.Register`에 한 줄: `r.Add(id, 층, blocksMovement, s => new 화면(s)).OpenedBy(키).OpenedByInteraction("상호작용 종류")`.
   이동을 막는지는 Unity 판과 같게(설계 `docs/superpowers/specs/2026-11-12-screens-port-design.md` 3.1절).
4. 테스트: `Rootborn.Screens.Tests`에서 화면을 눌러 보고 `TextKeys`가 두 언어에 다 있는지, `Rootborn.Host.Tests`에서 `PlayerDriver`로
   열고(`OpenScreenWithKey`·`WaitForScreen`) 누르고(`PressButton`) 찍는다(`ScreenshotScreen`). 새 화면은 `ScreenOverlapTests`(HUD와 겹치지 않는지)와
   `EnglishScreensTests`(영어로 넘치지 않는지)가 등록 표에서 저절로 읽는다. 상호작용이나 예약이 있어야 열리는 화면이면 `ScreenOverlapTests.Openers`에 여는 법을 한 줄 더한다.
5. 화면끼리: 패널·모달이 덮는 HUD 요소는 화면 쌓기가 숨긴다(`ScreenStack`이 배치할 때마다 겉면 `UiSurfaces`로 정한다). HUD끼리는 배치 수치로 피한다.
   한 묶음으로 읽히는 HUD는 `CoveredAsOne`을 켠다(체력·집중). 연 화면을 끄는 HUD 단추(가구 배치 켬/끔)는 덮이지 않게 둔다.

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
