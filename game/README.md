# game/ — ROOTBORN MonoGame 판

엔진 이전 규칙은 `.claude/rules/engine-migration.md`, 전체 설계는 `docs/superpowers/specs/2026-11-11-unity-removal-design.md`.
지금 있는 것은 실행 프로그램 뼈대와 UI 토대(프로젝트 3b), 데이터 로더와 세이브 저장소다. 게임 화면은 아직 옮기지 않았다.

| 폴더 | 내용 | MonoGame 참조 |
|---|---|---|
| `Rootborn.Core` | `Packages/com.rootborn.core/Runtime`의 게임 규칙을 그대로 묶는 프로젝트(코드 없음, LangVersion 9) | 없음 |
| `Rootborn.Data` | `data/` JSON으로 Core 정의 만들기(`GameDataLoader.Load(dir, RootbornBuilders.Register)`), 세이브 파일 저장소(`FileSaveStore`) | 없음 |
| `Rootborn.Ui` | 배치, 포인터 판정, 클릭, 포커스 이동, 스크롤 목록, 글자 칸, 9조각 계산. 그리기 명령 목록만 낸다 | 없음 |
| `Rootborn.Host` | 창, 화면 배율, 입력 장치 계층, 한글 조합 입력, TTF 글자, 그리기, 화면 저장, 견본 화면 | 3.8.6.1 고정 |
| `tests/Rootborn.Data.Tests` | 정의 로더, 실제 `data/` 전체 읽기, 세이브 저장소, Unity 판 QA 세이브 고정 파일 | 없음 |
| `tests/Rootborn.Ui.Tests` | 화면 없이 도는 테스트 | 없음 |
| `tests/Rootborn.Host.Tests` | 실제 창과 게임 루프를 한 프레임씩 돌리는 테스트 | 있음 |

## 명령

```
dotnet test game/Rootborn.sln
dotnet test Tools/Rootborn.Core.Tests
dotnet run --project game/Rootborn.Host -- --sample --size 1920x1080
```

인자와 환경 변수(인자가 먼저다):

| 인자 | 환경 변수 | 없을 때 |
|---|---|---|
| `--root <저장소>` | `ROOTBORN_REPO_ROOT` | 실행 파일 위치에서 위로 올라가며 찾는다 |
| `--font <TTF>` | `ROOTBORN_FONT` | 운영체제 한글 글꼴을 찾는다. 못 찾으면 오류 |
| `--evidence <폴더>` | `ROOTBORN_EVIDENCE_DIR` | `<저장소>/output/host-ui-evidence` |
| `--size 1280x720` | | 1280x720 |
| `--sample` | | 견본 화면이 없으면 띄울 화면이 없어 안내 후 끝난다 |

견본 화면에서 F12는 지금 화면을 증거 폴더에 저장한다. Esc는 포커스를 풀고, 포커스가 없으면 끝낸다.

## 한 곳에만 두는 것

- SDL2 직접 호출: `Rootborn.Host/Input/Sdl2Native.cs`
- MonoGame 내부 `SdlRunLoop` 리플렉션 호출: `tests/Rootborn.Host.Tests/PlayerDriver.cs`의 `FrameStepper`
- 키보드·포인터·글자 입력 읽기: `Rootborn.Host/Input/Devices.cs`의 `KeyboardDevice`, `OsPointer`, `TextInputDevice`
- 패널 그림 좌표(PNG 실측값): `Rootborn.Host/Rendering/UiRenderer.cs`의 `UiSkins`

MonoGame 버전을 올릴 때는 앞의 두 곳을 먼저 확인하고 `Rootborn.Host.Tests`를 돌린다.

## 테스트 근거의 종류

- `Rootborn.Data.Tests`: 자동 테스트. 종류를 더하면 `Rootborn.Data/Builders`에 등록하거나 `NotRules`·`NotPortedYet`에 적어야 `RealDataTests`가 통과한다.
- `Rootborn.Ui.Tests`: 자동 테스트. 화면 근거가 아니다.
- `Rootborn.Host.Tests`: 장치 이벤트 주입. 키와 글자는 SDL 이벤트 큐에, 포인터는 게임의 포인터 장치 계층에 넣는다.
  클릭은 UI 포인터 판정을 거친다. OS 입력 계층은 거치지 않으므로 보고할 때 "OS 입력 계층 미확인"으로 적는다.
- 스크린샷은 그려진 화면 버퍼에서 저장한다. 보고 전에 직접 열어 본다.
