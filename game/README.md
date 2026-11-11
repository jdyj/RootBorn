# game/ — ROOTBORN MonoGame 판

엔진 이전 규칙은 `.claude/rules/engine-migration.md`, 전체 설계는 `docs/superpowers/specs/2026-11-11-unity-removal-design.md`.
지금 있는 것은 실행 프로그램 뼈대와 UI 토대(프로젝트 3b), 데이터 로더와 세이브 저장소, 월드 화면(LDtk 맵을 그리고 걸어서 문으로 오감)이다.

| 폴더 | 내용 | MonoGame 참조 |
|---|---|---|
| `Rootborn.Core` | `Packages/com.rootborn.core/Runtime`의 게임 규칙을 그대로 묶는 프로젝트(코드 없음, LangVersion 9) | 없음 |
| `Rootborn.Data` | `data/` JSON으로 Core 정의 만들기(`GameDataLoader.Load(dir, RootbornBuilders.Register)`), 세이브 파일 저장소(`FileSaveStore`) | 없음 |
| `Rootborn.World` | LDtk 파일(.ldtk·.ldtkl)을 그대로 읽고, 맵 목록 `game/world/maps.json`으로 Core 월드(충돌·문·도착 지점)와 그릴 목록을 만든다 | 없음 |
| `Rootborn.Ui` | 배치, 포인터 판정, 클릭, 포커스 이동, 스크롤 목록, 글자 칸, 9조각 계산. 그리기 명령 목록만 낸다 | 없음 |
| `Rootborn.Host` | 창, 화면 배율, 입력 장치 계층, 한글 조합 입력, TTF 글자, 그리기, 화면 저장, 견본 화면 | 3.8.6.1 고정 |
| `tests/Rootborn.Data.Tests` | 정의 로더, 실제 `data/` 전체 읽기, 세이브 저장소, Unity 판 QA 세이브 고정 파일 | 없음 |
| `tests/Rootborn.World.Tests` | 실제 LDtk로 맵 만들기, Unity 씬에 구워진 충돌과 칸 단위 대조, 도착 지점·문 목적지, 크기가 다른 집 | 없음 |
| `tests/Rootborn.Ui.Tests` | 화면 없이 도는 테스트 | 없음 |
| `tests/Rootborn.Host.Tests` | 실제 창과 게임 루프를 한 프레임씩 돌리는 테스트 | 있음 |

## 명령

```
dotnet test game/Rootborn.sln
dotnet test Tools/Rootborn.Core.Tests
dotnet run --project game/Rootborn.Host -- --sample --size 1920x1080
dotnet run --project game/Rootborn.Host -- --world Shop --size 1920x1080
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

`--sample`도 `--world`도 없으면 안내 후 끝난다. 월드 화면에서는 화살표나 WASD를 누르고 있는 동안 걷고, 문에 들어서면 0.35초 뒤 목적지 맵으로 간다. 증거 폴더 기본값은 `<저장소>/output/host-world-evidence`.

견본·월드 화면에서 F12는 지금 화면을 증거 폴더에 저장한다. Esc는 포커스를 풀고, 포커스가 없으면 끝낸다.

## 한 곳에만 두는 것

- SDL2 직접 호출: `Rootborn.Host/Input/Sdl2Native.cs`
- MonoGame 내부 `SdlRunLoop` 리플렉션 호출: `tests/Rootborn.Host.Tests/PlayerDriver.cs`의 `FrameStepper`
- 키보드·포인터·글자 입력 읽기: `Rootborn.Host/Input/Devices.cs`의 `KeyboardDevice`, `OsPointer`, `TextInputDevice`
- 패널 그림 좌표(PNG 실측값): `Rootborn.Host/Rendering/UiRenderer.cs`의 `UiSkins`
- 맵 목록(맵 id, LDtk 레벨, 문 목적지, 도착 지점, 충돌 규칙, 보이는 높이): `game/world/maps.json`. 맵 크기는 적지 않는다(레벨에서 읽는다)

MonoGame 버전을 올릴 때는 앞의 두 곳을 먼저 확인하고 `Rootborn.Host.Tests`를 돌린다.

## 테스트 근거의 종류

- `Rootborn.Data.Tests`: 자동 테스트. 종류를 더하면 `Rootborn.Data/Builders`에 등록하거나 `NotRules`·`NotPortedYet`에 적어야 `RealDataTests`가 통과한다.
- `Rootborn.World.Tests`: 자동 테스트. 화면 근거가 아니다.
- `Rootborn.Ui.Tests`: 자동 테스트. 화면 근거가 아니다.
- `Rootborn.Host.Tests`: 장치 이벤트 주입. 키와 글자는 SDL 이벤트 큐에, 포인터는 게임의 포인터 장치 계층에 넣는다.
  클릭은 UI 포인터 판정을 거친다. OS 입력 계층은 거치지 않으므로 보고할 때 "OS 입력 계층 미확인"으로 적는다.
- 스크린샷은 그려진 화면 버퍼에서 저장한다. 보고 전에 직접 열어 본다.
