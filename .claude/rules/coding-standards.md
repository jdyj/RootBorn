# 코딩 표준 — MonoGame 판 C#

모든 C# 코드에 적용한다. 데이터 쪽 규칙은 `rules/data-driven.md`, 화면은 `rules/ui-standards.md`, 환경 의존 값은 `rules/environment-config.md`.
Unity 판(`Assets/`)은 얼렸으므로 고치지 않는다(`rules/engine-migration.md`).

## 프로젝트와 참조 방향

| 프로젝트 | 하는 일 | 참조할 수 있는 것 |
|---|---|---|
| `Rootborn.Core` (`Packages/com.rootborn.core/Runtime`) | 게임 규칙, 진행 상태, 세이브 모양, 월드 판정 | 없음 |
| `game/Rootborn.Data` | `data/` JSON으로 Core 정의 만들기, 세이브 파일 저장소 | Core |
| `game/Rootborn.Game` | 게임 한 판(`GameSession`): 영역을 잇고 저장한다 | Core, Data |
| `game/Rootborn.World` | LDtk와 `game/world/*.json`으로 Core 월드와 그릴 목록 만들기 | Core, Data |
| `game/Rootborn.Ui` | 배치, 포인터 판정, 포커스, 부품. 그리기 명령 목록만 낸다 | 없음 |
| `game/Rootborn.Screens` | 화면, `ScreenStack`, `ScreenCatalog`, `GameText` | Ui, Game, Core |
| `game/Rootborn.Host` | 창, 입력 장치, 글자, 그리기, 화면 저장 | 위 전부 + MonoGame |

- 화살표를 거꾸로 두지 않는다. 왜: 규칙과 화면을 엔진 없이 `dotnet test`로 돌리려면 아래층이 위층을 몰라야 한다.
- **MonoGame 참조는 `Rootborn.Host`와 `Rootborn.Host.Tests`에만.** 왜: 엔진을 바꿔도 Host만 바꾸면 되게. CI `check-monogame-references.sh`가 막는다.
- **규칙 판단은 Core에만.** Game은 잇고 저장만, 화면은 `GameSession` 명령·조회만 부르고, Host는 상태를 그리고 입력을 명령으로 넘긴다. 왜: 같은 규칙이 두 곳에 있으면 한쪽만 고쳐진다.

## Core 제약

- `UnityEngine`, `System.Text.Json`, `System.IO`를 참조하지 않는다. asmdef `noEngineReferences: true`. 왜: Core는 엔진·파일·JSON 라이브러리와 무관해야 어디서든 돈다. CI `check-core-references.sh`가 막는다.
- LangVersion 9: 파일 범위 네임스페이스, `record`, `global using`을 쓰지 않는다. 왜: Unity 판이 같은 폴더를 패키지로 읽는다.
- 새 `.cs`와 새 폴더마다 `.meta`(`fileFormatVersion: 2`, 새 32자리 guid)를 함께 커밋한다. CI `check-meta-files.sh`.
- 값은 생성자로 받는다. 엔진·전역 상태(시계, 레지스트리, 저장 서비스)에 닿던 자리는 값이나 인터페이스로 받는다. 왜: 테스트가 생성자만으로 규칙을 만들 수 있게.
- 다른 엔티티는 객체가 아니라 **id 문자열**로 가리킨다. 엔티티 id로 분기하지 않는다(`rules/data-driven.md`).
- 세이브 모양 클래스는 필드 이름과 형을 바꾸지 않는다. 왜: 옛 세이브를 계속 읽어야 한다. 바꿀 일이 생기면 형식 버전(`Version`)으로 다룬다.

## 공통

- 클래스는 `sealed`가 기본이다. 상속이 필요할 때만 푼다.
- 종류를 `enum`·`switch`로 닫지 않는다. 새 종류는 클래스 하나와 등록 한 줄로 더한다. 왜: 데이터·전략·화면·도전 종류가 계속 는다.
- 쓰이지 않는 확장점을 미리 만들지 않는다.
- 경로·주소·포트·글꼴 파일을 코드에 적지 않는다. 실행 인자나 환경 변수로 받는다(`game/README.md` 인자 표).
- 사람이 읽는 문구를 코드에 적지 않는다. 현지화 키로 받는다(`rules/localization.md`).
- 매직 넘버를 흩지 않는다. 규칙 수치는 데이터에, 화면 배치 수치는 그 화면의 배치 한 곳(예: `HudLayout`)에 둔다.

## 성능

- 매 프레임 도는 코드(Host의 그리기·걷기, `ScreenStack.Tick`)에서 새 객체, LINQ, 문자열 이어 붙이기를 만들지 않는다. 왜: 프레임마다 쓰레기가 쌓여 끊긴다.
- 화면은 매 프레임 상태를 읽지 않는다. 노드 나무는 한 번 만들고, 바뀌었다는 알림(`OnChanged(topic)`)이 올 때 값만 고친다.
- 긴 목록은 보이는 줄만 그린다(`UiScrollList`, 카드 격자).
- 정의는 시작할 때 한 번 읽고(`GameDefinitions`), 게임 중에는 표 조회만 한다. 매번 전체 목록을 훑지 않는다.
- 컬렉션은 크기를 알면 용량을 정해 만든다.
