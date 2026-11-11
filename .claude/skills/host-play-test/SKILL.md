---
name: host-play-test
description: "ROOTBORN MonoGame 판(game/)의 화면을 실제 게임 루프와 입력으로 검증하는 테스트를 쓰고 돌린다. 'MonoGame 판 테스트', '화면 테스트', '실제 입력 테스트', 'E2E', 스크린샷 검증, game/ 아래 화면을 만들거나 고친 뒤 확인할 때 사용."
---

# MonoGame 판 화면을 실제 루프로 테스트하기

안내: `game/README.md`. 보고 규칙: `.claude/rules/core-logic-and-play-evidence.md` 2절.

## 명령

```
C:\Users\jdyj\.dotnet\dotnet.exe test game/Rootborn.sln
C:\Users\jdyj\.dotnet\dotnet.exe run --project game/Rootborn.Host -- --sample --size 1920x1080
```

## 테스트 층

| 층 | 어디에 | 무엇을 | 보고에서의 이름 |
|---|---|---|---|
| 화면 없음 | `game/tests/Rootborn.Core.Tests`·`Data.Tests`·`World.Tests`·`Game.Tests`·`Ui.Tests` | 규칙 계산, 데이터 읽기, 칸 단위 걷기, `GameSession` 흐름, 배치·판정 | 자동 테스트 |
| 창 없는 화면 | `game/tests/Rootborn.Screens.Tests` | 화면을 눌러 보기, 화면 쌓기, 모든 언어의 문구 키 | 자동 테스트(화면 근거 아님) |
| 실제 루프 | `game/tests/Rootborn.Host.Tests` | 창을 띄우고 한 프레임씩 진행하며 입력을 넣음 | 장치 이벤트 주입, OS 입력 계층 미확인 |
| OS 입력 | FEVM의 Xvfb + xdotool, 또는 사람이 직접 | 실제 마우스, 키보드, 입력기 | 실제 입력(OS) |

## 무거운 테스트는 원격 Linux에서

이 PC에서 세션 여럿이 Host 테스트를 함께 돌리면 메모리가 모자란다. 전체 테스트나 Host 테스트 묶음은 `bash Scripts/remote-test/run.sh [<커밋>] [-- <dotnet test 인자>]`로 Strix Halo의 가상 화면(Xvfb)에서 돌린다. 접속 정보는 환경 변수 `ROOTBORN_REMOTE_HOST`·`ROOTBORN_REMOTE_USER`·`ROOTBORN_REMOTE_KEY`(값은 사용자 전역 지침에, 저장소에는 적지 않는다). 커밋된 것만 가고, 결과(프로젝트별 건수, 실패 목록, 스크린샷)는 `output/remote-test/<실행 id>/`에 온다. 원격 실행은 연결에서 떼어 돌기 때문에 이 PC 쪽이 끊기거나 꺼져도 끝까지 간다 — 시작하면 바로 찍히는 실행 id로 `run.sh --resume [<실행 id>]`(id가 없으면 이 작업 폴더의 가장 최근 안 받은 실행)를 하면 이어서 기다리거나 받는다. 오래 걸리는 전체 실행은 `run.sh --no-wait HEAD`로 시작만 하고 나중에 `--resume`해도 된다. 같은 커밋·인자의 실행이 있으면 거기에 붙고(`--force`로 새로), `--status`는 원격에서 도는 실행과 안 받아 간 실행을, `--cancel <실행 id>`는 그 실행만 멈춘다. 근거는 여전히 "장치 이벤트 주입(OS 입력 계층 미확인)"이고 그리기는 소프트웨어 GL이다. 쓰는 법과 원격 준비는 `Scripts/remote-test/README.md`.

## 실제 루프 테스트 쓰는 법

- 조작은 `PlayerDriver`의 동사만 쓴다(키 누르기와 떼기, 포인터 이동·누름·뗌·휠, 글자 입력, 스크린샷). 게임 내부 메서드를 직접 불러 상태를 만들지 않는다. 필요한 동사가 없으면 드라이버에 더한다.
- 키와 글자는 SDL 이벤트 큐에, 포인터는 게임의 포인터 장치 계층에 넣는다(MonoGame이 마우스를 OS에서 직접 읽어서다). 클릭은 UI 포인터 판정을 거쳐야 한다.
- 프레임 진행은 `FrameStepper` 한 곳만 쓴다(MonoGame 내부 `SdlRunLoop` 호출). 다른 곳에서 리플렉션으로 MonoGame 내부를 부르지 않는다.
- 테스트 모드는 고정 시간 간격으로 돈다. 실시간을 기다리지 않는다.
- 학생 시계가 흐른다(흐르는 시간): 게임 루프가 프레임마다 `GameSession.AdvanceTime(1/60초 × --time-scale, …)`을 넘긴다. 하루 600초라 배속 1이면 게임 1분 ≈ 25프레임. 시각에 기대는 테스트는 `--time-scale`(슬롯에서도 받는다, 0이면 시계가 서 있다)로 정하고, 시각을 맞추려면 프레임을 보낸다(상태를 직접 바꾸지 않는다). 혼자 하기에서 패널·모달·대화가 떠 있으면 시계가 멈춘다(창 활성 여부는 테스트 모드에서 보지 않는다).
- 대상은 화면 좌표가 아니라 요소 id로 찾는다. 화면은 `OpenScreenWithKey`·`WaitForScreen`으로 열고 `PressButton`으로 누르고 `ScreenshotScreen`으로 찍는다(`game/README.md` "화면 더하기").
- 테스트가 띄운 창과 프로세스는 반드시 끝낸다. 사용자의 마우스와 키보드를 OS 수준으로 조작하지 않는다.

## 스스로 플레이하는 시뮬레이션

대본 없이 정책(`random`, `main-chain`, `off-school`, `novelty`, `wander`, `stay-up`)이 화면에 보이는 것(누를 수 있는 요소, 걸어 닿는 지점과 문, 여는 키, 추적 목표, HUD 시계의 시각·경고)만 보고 골라 여러 날을 플레이하며 불변식(예외, 막힘, 하루 넘김, 시계가 거꾸로 가지 않음·지친 동안 활동 없음·자면 07:00·안 자면 회복 없음·메뉴가 열리면 시계 멈춤, 수치 범위, 보상 두 번, 화면 쌓임, 날마다 저장 → 새 `GameSession`으로 다시 읽기, 빠진 문구 키, 걸음 프레임·프레임당 할당)을 본다. 판단 코드는 `game/tests/Rootborn.Simulation.Tests/Play/`에 하나다. 하루는 걸음을 다 썼거나, HUD가 잘 시간이 지났다·지쳤다고 보이거나, 40걸음 동안 아무것도 바뀌지 않으면(할 것이 없다) 집으로 걸어가 자며 끝난다(`stay-up`은 자지 않고 서서 07:00을 기다린다 — 실제 창에서는 배속 10). 실제 10분 하루의 흐름 표: `--filter "FullyQualifiedName~DayFlowTests"` → `output/simulation/day-flow-<정책>/day-flow.md`. 창 없는 층은 `dotnet test game/tests/Rootborn.Simulation.Tests`(자동 테스트, 30초쯤), 실제 창 층은 `dotnet test game/tests/Rootborn.Host.Tests --filter "FullyQualifiedName~Simulation"`(장치 이벤트 주입, OS 입력 계층 미확인). 길게 돌리려면 `ROOTBORN_SIM_SEEDS`·`ROOTBORN_SIM_DAYS`(창 없음), `ROOTBORN_SIM_HOST_SEEDS`·`ROOTBORN_SIM_HOST_DAYS`(창). 판마다 `output/simulation/<이름>/`에 `play.txt`(정책·씨앗·행동 이름), `summary.md`(지나간 화면·상호작용·맵, 끝낸 퀘스트, 열지 못한 화면), 실패하면 `failure.md`(화면 쌓임·맵·자리·최근 행동)가 남는다. 재현: 같은 정책과 씨앗은 같은 판이다(결정적 — 창 없는 층). 남은 기록을 그대로 다시 하려면 `ROOTBORN_SIM_REPLAY=output/simulation/<이름>/play.txt dotnet test game/tests/Rootborn.Simulation.Tests --filter "Name=ReplayFromEnvironment"`. 새 요소 종류는 `UiTargets.Register`, 줄·카드의 데이터 id는 `ScreenSubjects.Register`, 정책은 `PlayPolicyCatalog.Register`, 목표 종류 힌트는 `ObjectiveHints.Register`에 한 줄씩 더한다. 미니게임은 도전 종류마다 풀이 하나(`IChallengeSolver`)가 미니게임 화면에서 보이는 것(`MinigameSight`: 요소 사각형·글자·색, 안내 글)만 보고 푼다 — 새 도전 종류는 `ChallengeSolvers.Register`에 한 줄(없으면 `ChallengeSolverTests`가 실패한다). 정답(외울 순서는 미리 보기 글로, 고르기의 답은 힌트 표시로만)을 Core에서 읽지 않는다. 찾은 결함 중 범위 밖은 `KnownDefects`와 지금 동작을 고정하는 재현 테스트(건너뜀 없음)에 두고, 고친 것은 `FoundDefectRegressionTests`에 통과하는 재현으로 남긴다.

### 오래 플레이했을 때(메모리·프레임 비용)

- 긴 플레이: `dotnet test game/tests/Rootborn.Host.Tests --filter "FullyQualifiedName~LongPlayHostTests"`(1분쯤). 돌아다니기 정책(`wander`, 문을 먼저)으로 실제 창에서 여러 날을 돌며 일정 걸음마다 전용 메모리·관리 힙(GC 뒤)·월드 그림 캐시·가만히 선 프레임의 할당을 재고 `output/simulation/host-longplay-wander-1/memory.md`에 표를 남긴다. 길게: `ROOTBORN_SIM_SOAK_DAYS`(기본 6), `ROOTBORN_SIM_SOAK_SAMPLE`(몇 걸음마다, 기본 10). 끝에 밤의 마을을 한 장 찍는다(`output/host-sim-evidence/longplay-end-*-night.png`).
- 무거운 화면: `--filter "FullyQualifiedName~HeavyScreensHostTests"`. 가구 팔레트·도감·수첩·가방을 되풀이해 열고 넘기며 메모리, 프레임당 할당, 그리기 명령 수(`HostGame.DrawCommandCount`), UI 그림 캐시(`UiRenderer.CachedTextureCount`)를 본다. 표는 `output/simulation/host-heavy-screens/memory.md`.
- 판정은 벽시계가 아니라 흔들리지 않는 값으로 한다: 메모리는 여러 번 잰 값의 가장 작은 것끼리 넉넉한 여유로, 프레임 비용은 `GC.GetAllocatedBytesForCurrentThread` 차이·그린 줄 수·그리기 명령 수로. 벽시계는 표에만 적는다(CI 러너는 소프트웨어 그리기라 흔들린다).
- 무엇이 할당하는지 찾을 때: 런타임 이벤트(`EventListener`로 `Microsoft-Windows-DotNETRuntime`의 GC 키워드, `GCAllocationTick`의 `TypeName`)로 형식을 보고, 게임 구성 요소의 Update·Draw 사이 할당 차이로 단계(갱신 / 그리기)를 가른다. 지역 함수·람다가 지역 변수를 잡으면 그 메서드에 들어갈 때마다 객체가 생기고, `IReadOnlyList`를 `foreach`하면 열거자가 상자에 담긴다.
- 월드 그림 캐시는 한도(`WorldScreen.TextureBudgetBytes`, 기본 256MB)를 넘으면 맵에 들어간 뒤 그 맵이 쓰지 않는 그림부터 푼다(`WorldFrameCostHostTests`).

## 스크린샷

- 그려진 화면 버퍼에서 저장한다. 위치는 `output/` 아래(git 밖).
- **보고 전에 직접 연다.** 잘림, 겹침, 어긋난 테두리, 뭉개진 글자, 맵 밖 노출, 남은 디버그 표시를 찾는다. 작은 글자는 확대 조각으로 본다.
- 그림 조각 좌표는 PNG를 직접 재서 정한다(16px 칸이라고 추정하지 않는다).
- 픽셀 비교는 GPU 렌더링끼리만 한다. 소프트웨어 렌더링은 값 1 차이가 난다.

## xdotool로 OS 입력을 넣을 때

`key`는 누름과 뗌이 한 프레임에 들어가 놓친다. `keydown`, 잠깐 대기, `keyup` 순서로 쓴다. 창 좌표는 `getwindowgeometry --shell`로 얻는데, 변수 `X`와 `Y`를 덮어쓰니 다른 변수 이름과 겹치지 않게 한다.

## 보고

확인한 것마다 근거의 종류와 테스트 이름 또는 스크린샷 경로를 붙인다. 주입으로 확인한 것을 실제 입력으로 확인했다고 쓰지 않는다. 확인하지 못한 것(OS 입력, Linux, 다른 해상도)을 따로 적는다.
