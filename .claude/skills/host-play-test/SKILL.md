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
| 화면 없음 | `game/tests/Rootborn.Ui.Tests`, `Tools/Rootborn.Core.Tests` | 배치, 판정, 규칙 계산 | 자동 테스트 |
| 실제 루프 | `game/tests/Rootborn.Host.Tests` | 창을 띄우고 한 프레임씩 진행하며 입력을 넣음 | 장치 이벤트 주입, OS 입력 계층 미확인 |
| OS 입력 | FEVM의 Xvfb + xdotool, 또는 사람이 직접 | 실제 마우스, 키보드, 입력기 | 실제 입력(OS) |

## 실제 루프 테스트 쓰는 법

- 조작은 `PlayerDriver`의 동사만 쓴다(키 누르기와 떼기, 포인터 이동·누름·뗌·휠, 글자 입력, 스크린샷). 게임 내부 메서드를 직접 불러 상태를 만들지 않는다. 필요한 동사가 없으면 드라이버에 더한다.
- 키와 글자는 SDL 이벤트 큐에, 포인터는 게임의 포인터 장치 계층에 넣는다(MonoGame이 마우스를 OS에서 직접 읽어서다). 클릭은 UI 포인터 판정을 거쳐야 한다.
- 프레임 진행은 `FrameStepper` 한 곳만 쓴다(MonoGame 내부 `SdlRunLoop` 호출). 다른 곳에서 리플렉션으로 MonoGame 내부를 부르지 않는다.
- 테스트 모드는 고정 시간 간격으로 돈다. 실시간을 기다리지 않는다.
- 대상은 화면 좌표가 아니라 요소 id로 찾는다.
- 테스트가 띄운 창과 프로세스는 반드시 끝낸다. 사용자의 마우스와 키보드를 OS 수준으로 조작하지 않는다.

## 스크린샷

- 그려진 화면 버퍼에서 저장한다. 위치는 `output/` 아래(git 밖).
- **보고 전에 직접 연다.** 잘림, 겹침, 어긋난 테두리, 뭉개진 글자, 맵 밖 노출, 남은 디버그 표시를 찾는다. 작은 글자는 확대 조각으로 본다.
- 그림 조각 좌표는 PNG를 직접 재서 정한다(16px 칸이라고 추정하지 않는다).
- 픽셀 비교는 GPU 렌더링끼리만 한다. 소프트웨어 렌더링은 값 1 차이가 난다.

## xdotool로 OS 입력을 넣을 때

`key`는 누름과 뗌이 한 프레임에 들어가 놓친다. `keydown`, 잠깐 대기, `keyup` 순서로 쓴다. 창 좌표는 `getwindowgeometry --shell`로 얻는데, 변수 `X`와 `Y`를 덮어쓰니 다른 변수 이름과 겹치지 않게 한다.

## 보고

확인한 것마다 근거의 종류와 테스트 이름 또는 스크린샷 경로를 붙인다. 주입으로 확인한 것을 실제 입력으로 확인했다고 쓰지 않는다. 확인하지 못한 것(OS 입력, Linux, 다른 해상도)을 따로 적는다.
