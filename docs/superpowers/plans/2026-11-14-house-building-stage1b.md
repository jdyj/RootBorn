# 집 짓기와 꾸미기 1단계 (나) — 방마다 벽지·바닥 고르기 Implementation Plan

> **구현 끝(2026-11-14, 브랜치 room-builder). 다음 계획 1c를 쓴 뒤 이 파일을 지운다 — 1c가 기대는 약속은 맨 끝 절.**

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 플레이어가 가구 배치 화면(P)의 "꾸미기" 분류에서 벽지·바닥(과 벽 윗면 띠 색)을 고르고, 방 채우기·칸 붓으로 돈을 내고 바꾼다. 바뀐 것은 세이브에 남고 게임이 바로 다시 그린다. 곁가지: 잠긴 꾸밈의 가게 진열, 인테리어 기술, 새 게임의 처음 배치 가구, 앞 계획이 미룬 작은 것들.

**Architecture:** Core `Rootborn.Core.Building`이 꾸밈 상태(바닥 칸 → (벽지, 바닥), 집의 띠 색), 방 채우기 규칙, 미리 보기(바뀔 칸·비용·안 되는 이유)를 낸다. `GameSession`이 명령·조회·비용(`ItemInventory.TryExchange`)·저장 묶음(`house-build.json`)을 잇는다. World `ComposedMapTiles`가 칸마다의 꾸밈으로 조합하고, Host는 꾸밈 알림이 올 때와 미리 보기 칸 묶음이 바뀔 때만 다시 조합한다. 화면은 `GameSession` 조회·명령만 부른다.

**Tech Stack:** C# .NET 8 + NUnit(Core/Data/World/Game/Screens/Host), MonoGame 3.8.6.1(Host만), Python 3.11 + Pillow + unittest(키트, 작업 5만).

**Spec:** `docs/superpowers/specs/2026-11-14-house-building-design.md` 12.1절 1-6·1-7(권위). 결정 대기: `docs/superpowers/specs/2026-11-14-house-building-open-decisions.md`(지금 값이 기준, 이 계획이 23~32번을 더함).

**계획을 둘로 나눈다.** 이 파일은 혼자 하는 집에서 꾸미기가 끝까지 되는 것(규칙·세이브·비용·다시 그리기·화면)과 곁가지다. 1-8 같이 하기는 `docs/superpowers/plans/2026-11-14-house-building-stage1c.md`(이 계획이 합쳐진 뒤 쓴다)로 넘긴다. 이유 한 줄: 같이 하기는 Net·Server·방 조각·지갑 잡기까지 닿아 작업이 5개 넘게 늘고, 혼자 하기의 규칙과 세이브가 굳기 전에 방 쪽 판정을 만들면 두 번 고친다. 다음 계획이 기댈 약속은 맨 끝 "다음 계획(1c)이 기대는 것".

## 출발점 — 앞 계획(`…-stage1.md`, 끝나서 지웠다)에서 받는 것

앞 계획 맨 끝 "다음 계획이 기대는 것"을 그대로 쓴다(이름을 바꾸지 않는다): `RoomTilePlan.Build(RoomGrid, faceHeight, band, wallDoors)`, `CellTiles.Parts`의 `TilePart`(`Source`·`Shade`·`Piece`·`Owner`·`HasOwner`·`Rects`), `RoomCells`, `RoomFinishDefinition`(`Id`·`Slot`·`NameKey`·`Sheet`·`Art`·`Cost` 칸당·`UnlockToken`·`IsValid`), `RoomFinishSlots`, World `RoomFinishArt`(`TryPiece`·`TryCommon`·`TryBand`·`BandOf`), `MapEntry.Build`(`MapBuildSettings` 8필드), `LoadedMap.Tiles`, `RoomTileCompositor.Compose(plan, wallAt, floorAt, sheetForBand, bandColorsId)`, `ComposedMapTiles.Compose(map, compositor, finish)`(`Ok`·`Draws`·`InsertAt`·`FallbackUnder`·`TextureKey`), `WorldScreen.RefreshComposedTiles(mapId)`(지금 부르는 곳은 Host 테스트 하나뿐), `HouseFurnitureLayout.Stored`·`TryPlaceStored`, `GameSession.StoredFurniture()`·`PlaceStoredFurniture`(같이 쓰는 집에서 false), 분류 줄 `furniture.category.furniture`·`furniture.category.stored`.

앞 계획의 작업 이름 → 이 계획:

| 앞 계획 | 이 계획 |
|---|---|
| B1 Core 꾸밈 상태·방 채우기·미리 보기 | 작업 1 |
| B2 `GameSession` 명령·`house-build.json`·HOUSE-001 | 작업 2(명령·비용·저장 묶음) + 작업 4(하위·상위호환, 고정 폴더) |
| B3 꾸미기 분류 화면 + Host 입력, 띠 색 | 작업 3(다시 그리기), 작업 6(화면), 작업 7(띠 색) |
| B4 가게 진열과 해금 | 작업 8 |
| B5 인테리어 기술 | 작업 9 |
| B6 새 게임 처음 배치 | 작업 10 |
| B7 같이 하기 + HOUSE-004, 같이 쓰는 집의 보관 꺼내기(결정 16), 옮긴 자리를 방 기록에 굳히기 | `…-stage1c.md` |
| 결정 22 첫 입장 지연(미리 읽기) | 작업 3 |
| 결정 18 보관 줄 구별 | 작업 11 |
| 최종 검토 Minor: T자 바닥 그림자 8px | 작업 5 |

미뤄 둔 지적(`.superpowers/sdd/2026-11-14-house-building-stage1/progress.md`) 중 받는 것: 그림자 8px·시험 표의 `faceHeight`·`band` 다른 값·문지방 조각 전부·띠 색을 벽 기본과 달리 고르는 시험(작업 5·7), `TryPiece` 문자열 이어 붙이기·조합 텍스처를 예산에·`Load`의 사전 조회·첫 입장 지연(작업 3), 영어 "1 pieces"·보관 줄 글자 매 프레임·같은 이름 줄 구별·Host 시험 조건(작업 11), `IsValid`가 `nameKey`를 안 봄(작업 7), 맵 검사가 `faceHeight`와 조각을 안 맞춰 봄(작업 5). **받지 않는 것과 이유:**
- 조합 실패가 `Console.Error`에만: 맵 문제(`ReportProblems`)도 `Console.Error`뿐이라 모을 곳이 없다. 대신 `MapContractTests`가 정의·그림을 넘겨 같은 문제를 테스트에서 잡는다(작업 5에서 넓힘).
- 없는 시트와 없는 조각이 구별 안 됨, `Read` 예외 감싸기: 조합기가 시트·조각 이름을 문제 줄에 적고(`IsAtlasReadError`로 이미 모은다) 플레이에 영향이 없다.
- `ShadeRange`가 `0-32`를 받음, `Shade`·`ShadeInside` 중복, `FaceCode` 한도, 시험 표 이중 장부·import 15초·Pillow 주석: 이 기능의 동작과 무관한 내부 정리다.
- `PreservesAccess` 매번 BFS: 측정(200개 87ms)이 집을 열 때 한 번이라 문제가 없다.
- 열린 채 기록 번호가 바뀌어 같은 종류의 다른 기록을 가리킴(물건 든 상자 대 빈 상자): 작업 11의 줄 그림이 상자 상태를 보이지만 번호 재매김 자체는 1c의 방 기록 굳히기와 함께 본다.
- 전후 스크린샷 23장 비교를 테스트로: 조합 = 구운 그림 테스트(World·Host)가 같은 것을 지킨다.

## 시작 전 확인 다섯 가지 (이 계획을 쓰며 직접 확인한 사실)

- **(가) 시트**: 꾸밈 10종 모두 `sheet` = `art/world/finishes/finishes.png`(`data/building/RoomFinishDefinition.json`). 조합기는 띠 색과 공용 조각을 `sheetForBand`(지금 기본 벽 꾸밈의 `Sheet`) 하나에서, 벽지·바닥 조각은 각 꾸밈의 `Sheet`에서 찾는다(`RoomTileCompositor.Job`). 꾸밈이 같은 시트라 지금 어긋남은 없다. 작업 7이 "띠 꾸밈의 시트 = 기본 벽 꾸밈의 시트"를 맵 검사로 지킨다.
- **(나) 문지방에 띠 색이 구워졌는가: 아니다.** `common/sill:*` 9장과 `common/door`는 나무 줄(`SILL_WOOD = "wood.school"`) + 외곽선 한 줄(`style.outline`)뿐이다(`Tools/pixel-kit/pixelkit/room_tiles.py` `_draw_sill`·`_door_face`, `output/pixel-kit/house/finishes-common_x4.png`를 직접 열어 봄). 외곽선 색 `#3A384E`는 좌표표 `bands` 다섯 줄의 `o`와 모두 같다. 문지방 양 끝의 문설주는 이웃 띠 칸의 띠 끝 외곽선이라 띠 색을 따라 바뀐다. 그래서 띠 색을 바꿔도 문지방은 나무로 남고 어긋나지 않는다 — **키트·시험 표·C#을 고칠 일이 없고, 띠 색 고르기는 이 계획 안(작업 7)에서 한다.**
- **(다) `RefreshComposedTiles`를 부르는 곳**: `game/tests/Rootborn.Host.Tests/HouseTilesHostTests.cs` 하나뿐이다. 작업 3이 새 주제 `GameChangeTopics.HouseBuild`를 `HostGame`의 `session.Changed` 처리(지금 `Furniture`·`World`에서 `RefreshFurniture`를 부르는 줄, `game/Rootborn.Host/HostGame.cs` 596줄 근처)에 더해 부르게 한다.
- **(라) 맵 검사는 `faceHeight`와 조각을 맞춰 보지 않는다**: `MapChecks.CheckBuildArt`는 기본 꾸밈·띠 색·띠 두께만 본다. 시트에는 `face:1`·`face:2`만 있어 `faceHeight` 3인 맵은 조합이 투명 픽셀로 무너진다. 작업 5가 "출처표가 내는 모든 조각을 모든 벽·바닥 꾸밈이 갖는가"를 검사로 더한다.
- **(마) T자 바닥 그림자**: 새 집 (13,3)처럼 가로 벽 정면이 세로 벽 띠 칸 안으로 8px 늘어난 자리 바로 아래 칸에서, 띠 옆으로 비치는 바닥(주인 `[-1,0]`·`[1,0]`)의 위 두 줄에 그림자가 없다(`output/pixel-kit/house/vectors-house-two-floors_x4.png`에서 왼쪽 방 그림자가 띠 8px 앞에서 끊기고 오른쪽 방은 8px 늦게 시작). 규칙이 Python과 C# 두 곳 + 시험 표에 있다. 작업 5가 받는다.

## 방 채우기 규칙 — 새 집에 미리 돌린 결과

설계서 4.2절의 "양옆이 벽인 바닥 칸"은 새 집 (13,14)·(13,15)(1칸 세로 벽에 난 폭 2 틈)를 못 잡아 왼쪽 아래·오른쪽 아래 방이 한 덩어리가 되고, 시험 방(`art/kit/rooms/test.two-rooms.json`, 커밋된 내용)에서는 두꺼운 벽 아래 1줄 띠가 문간으로 잘못 잡힌다. 그래서 규칙을 다듬었다(Core `RoomFill`, 결정 대기 23번):

1. **좁은 칸**: 바닥 칸 중 가로 바닥 줄 길이 ≤ 2 또는 세로 바닥 줄 길이 ≤ 2(그 칸을 지나는, 벽·밖에서 끊기는 연속 바닥 칸 수).
2. **방 바탕**: 좁지 않은 바닥 칸의 4방향 덩어리.
3. 좁은 칸의 4방향 덩어리마다: 맞닿은 방 바탕이 하나면 그 방에 든다, 둘 이상이면 **문간**, 없으면 혼자 한 방.
4. 방 채우기(시작 칸이 방): 그 방 + 그 방에 맞닿은 문간 칸. 시작 칸이 문간: 그 문간 덩어리만.

이 계획을 쓰며 Python(임시 스크립트, 커밋 안 함)으로 돌린 결과(자동 계산): 새 집 바닥 283칸 → 방 넷 84(x1~12, y3~9)·52(x14~26, y3~6)·91(x14~26, y10~16)·48(x1~12, y13~16), 문간 셋 (6,10~12)·(20,7~9)·(13,14~15), 합 283. 채우기 결과는 87·55·96·53칸. 시험 방 바닥 141칸 → 방 둘 65·75 + 문간 (9,6). 아래 줄 띠 (1~5,12)·(10~14,12)는 각 방에 든다. 작업 1의 테스트가 이 숫자를 격자에서 다시 낸다.

## Global Constraints

- 규칙은 Core(`game/Rootborn.Core/src`)에만. Core는 `System.IO`·`System.Text.Json`·엔진을 참조하지 않는다(CI `check-core-references.sh`). 화면은 `GameSession` 명령·조회만 부르고 판정을 다시 하지 않는다. MonoGame 참조는 `Rootborn.Host`·`Rootborn.Host.Tests`에만.
- 엔티티 id로 분기하지 않는다 — 맵 id·꾸밈 id·가구 id·면 id 포함. 짓는 집은 맵 성질 `build`로, 꾸밈 자리는 정의의 `Slot` 글로 고른다(`check-no-entity-id-branching.sh`). 종류(도구, 안 되는 이유)를 enum으로 닫지 않는다 — 글 상수와 문구 키.
- 사람이 읽는 문구는 현지화 키로 `game/localization/ko.json`·`en.json` 둘 다에. 값은 `{0}` + `GameText.Format`, 문장을 코드에서 잇지 않는다. 수를 보이는 영어 문구는 `.one` 키를 나눈다. 글자 14 이상, 2배 길이 문구에도 패널 밖으로 안 나감.
- 그림 파일 경로·좌표·색을 코드에 적지 않는다. 꾸밈 그림은 정의의 `Sheet`·`Art` + 좌표표, 카드 그림 id는 `<Sheet>#<Art>/<조각>`(Host `ImageCatalog`가 좌표표로 푼다).
- 칸 좌표: 맵 칸 `CellPos`(y 아래로 +)로 저장·판정, 가구 칸 `GridPoint`(y 위로 +)는 화면에서만. 바꾸기는 `FurnitureRoom.ToPlacement/ToMap` 한 곳.
- **지급·지불은 전부 아니면 전무, 같은 요청은 한 번만.** 바꾸기 전에 계산하고, 돈을 빼는 것과 꾸밈을 바꾸는 것은 한 `TryExchange`(커밋은 메모리만, I/O·예외 없음) 안에서, 저장은 그 뒤 한 `Save()` 묶음. 이미 그 꾸밈인 칸은 세지 않으므로 같은 요청 두 번째는 바뀌는 칸 0 → 비용 0 → 무변경.
- **세이브 하위호환·상위호환**(스킬 `save-compat-change`): 있는 고정 폴더를 고치지 않는다, 데이터에 없는 꾸밈 id의 기록을 버리지 않는다, 읽기만으로 파일을 쓰지 않는다, `ReadableFrom`을 올리지 않는다(결정 5), `Require`에 넣지 않는다.
- 테스트는 임시 폴더와 고정 파일만 쓴다. 사용자 세이브 폴더(`AppData/LocalLow`)를 읽거나 쓰지 않는다.
- 매 프레임 다시 계산하지 않는다: 조합은 꾸밈 알림이 올 때만, 미리 보기 조합은 미리 보기의 칸 묶음·꾸밈이 바뀔 때만, 미리 보기 계산은 포인터 칸이 바뀔 때만. 그리기 루프에 새 객체·LINQ·문자열 이어 붙이기 금지.
- 실패 테스트 먼저. 테스트는 그림의 특정 픽셀 색을 단언하지 않는다(출처·두 경로가 같은지는 된다).
- 근거 종류를 나눠 쓴다: 자동 테스트 / 장치 이벤트 주입(OS 입력 계층 미확인) / 직접 본 화면(스크린샷 경로). Host 테스트에서 돈을 주거나 명령을 직접 부른 것은 "준비 단계, 실제 플레이 아님"이라고 보고에 적는다. 화면을 바꾼 작업은 스크린샷을 직접 연 뒤에 끝.
- 벽 그림 규칙을 고치면 Python(`room_tiles.py`) → `room vectors` → C#(`RoomTilePlan`) → 두 쪽 테스트를 함께(작업 5).
- 커밋: 한글 제목·본문, prefix(`[FEATURE]` `[META]` `[UI]` `[ASSET]` `[BALANCE]` `[TEST]` `[DOCS]` `[CHORE]`), 끝줄은 그 커밋을 만든 모델의 `Co-Authored-By: <모델 이름> <noreply@anthropic.com>`. 브랜치 `room-builder`에만, 푸시하지 않는다. 파일을 하나씩 골라 `git add <경로>`.
- **게임 수치·목록은 시험용**이고 결정 대기 문서에 있다(꾸밈 비용 벽지 2·바닥 1, 띠 색 무료, 가게 값 15, 처음 배치 세 가구). 사용자가 값을 정하지 않았다.
- 사용자의 미커밋 파일 `art/world/rooms/test.two-rooms/{test.two-rooms.ldtk,tiles.png,tiles.slots.json}`을 건드리지 않는다(`room build test.two-rooms`·`repaint`를 돌리지 않는다, `git add` 하지 않는다).
- dotnet은 이 PC에서 한 번에 한 프로젝트씩: `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"` 뒤 `dotnet test game/tests/<프로젝트>`, Host는 `--filter`로 좁힌다. 합치기 전 전체는 `bash Scripts/remote-test/run.sh <커밋>`. 지금 건수 Core 1278, Data 451, World 166, Game 895, Screens 559, Server 124, Simulation 107, 도구 585(`python -m unittest discover -s Tools/pixel-kit/tests -t Tools/pixel-kit`) — 줄지 않는다.

## Review Focus

1. **방 채우기가 문간 너머로 번지거나 한 방이 둘로 갈린다.** 새 집과 시험 방의 숫자(위 절), 문간에서 시작한 채우기, 폭 3 이상의 넓은 틈(한 방이 된다 — 맞다) — 작업 1의 테스트.
2. **돈과 꾸밈이 어긋난다.** 돈은 나가고 꾸밈은 저장 안 됨(또는 반대): 커밋은 메모리만, 파일은 스냅샷(지갑)과 `house-build.json`이 한 묶음. 묶음이 실패하면 둘 다 옛것 — 작업 2의 `SaveFailure_*`.
3. **데이터에서 지운 꾸밈·잠긴 꾸밈을 이미 쓴 칸.** 지운 id의 기록은 남고 그 칸은 기본으로 그린다. 잠긴 꾸밈을 이미 쓴 칸(가게에서 산 뒤 해금 파일이 옛 것이거나 1c의 남이 쓴 것)은 그대로 보이고, 다시 고를 때만 잠김 — 작업 4.
4. **"기본"의 표현.** 기록의 빈 글 = 기본. 기본 꾸밈을 고르면 그 자리를 빈 글로, 두 자리가 다 비면 기록을 지운다(맵 기본이 바뀌면 따라간다). 바뀌는 칸은 보이는 값(실제 꾸밈 id)으로 견준다 — 데이터에 없는 id가 든 칸에 기본을 고르면 "이미 이 꾸밈"이고 기록은 남는다 — 작업 1·4.
5. **꾸밈을 바꾼 뒤 한 프레임 어긋남.** 명령이 저장 → `Changed(HouseBuild)` → `RefreshComposedTiles`와 미리 보기 지우기가 같은 업데이트 안에 일어나 다음 그리기는 새 꾸밈이다. 옛 미리 보기·옛 조합이 한 프레임 보이지 않는지 — 작업 3·6의 Host 테스트(누른 뒤 첫 프레임의 `ComposeCount`·미리 보기 없음).

---

### Task 1: Core 방 채우기와 꾸밈 상태

모델: 높음. 멈춤 없음.

**Files:**
- Create: `game/Rootborn.Core/src/Building/RoomFill.cs`, `HouseFinishes.cs`, `HouseBuildSave.cs`
- Test: `game/tests/Rootborn.Core.Tests/Building/RoomFillTests.cs`, `HouseFinishesTests.cs`

**Interfaces:**
- Consumes: `RoomGrid`, `RoomCells`, `RoomTilePlan`·`CellTiles`·`TilePart`(`Owner`, `HasOwner`, `Source`), `TileSources`, `RoomFinishDefinition`, `RoomFinishSlots`, `Rootborn.Core.Placement.FurnitureUnlockProgress.IsUnlocked`, `Rootborn.Core.Inventory.ItemAmount<string>`, `Rootborn.Core.Save.IVersionedSave`.
- Produces(모두 `namespace Rootborn.Core.Building`, `sealed`):
  - `RoomFill`: `public const int NarrowRun = 2; public static RoomFill Build(RoomGrid grid); public bool IsDoorway(CellPos cell); public IReadOnlyList<CellPos> RoomOf(CellPos floorCell); public int RoomCount { get; }` — 규칙은 위 "방 채우기 규칙" 1~4. `RoomOf`는 바닥이 아니면 빈 목록, 같은 방이면 **같은 목록 인스턴스**를 돌려준다(Build 때 방마다 한 번 만든다 — 미리 보기가 칸 묶음이 같은지 참조로 본다). 목록은 y 다음 x 순.
  - `HouseBuildFrame`: `public HouseBuildFrame(string frameId, RoomTilePlan tiles, string defaultWall, string defaultFloor, string defaultBand)`, 같은 이름의 읽기 속성. `defaultBand`는 띠 색 줄 이름(맵 `build.bandColors`).
  - `public static class HouseFinishTools { public const string Room = "room", Brush = "brush"; }`
  - `public static class HouseFinishReasons` — 문구 키 상수: `NoTarget = "ui.house-build.reason.no-target"`, `UnknownFinish = "…unknown-finish"`, `Locked = "…locked"`, `NothingToChange = "…nothing-to-change"`, `NotEnoughMoney = "…not-enough-money"`, `NotInHouse = "…not-in-house"`, `SharedHouse = "…shared-house"`.
  - `HouseFinishPreview`: `string FinishId, Slot, Tool; bool HasTarget; CellPos Target; IReadOnlyList<CellPos> Cells; IReadOnlyList<CellPos> ChangedCells; IReadOnlyList<ItemAmount<string>> Cost; string ReasonKey; int StateVersion; bool CanApply => ReasonKey == null; bool SameAs(HouseFinishPreview other); public static HouseFinishPreview Refused(string finishId, string slot, string tool, string reasonKey)` — `SameAs`는 FinishId·Slot·ReasonKey·StateVersion이 같고 `Cells`가 같은 인스턴스이거나 칸이 같다(칸 붓 1칸). 그 밖의 생성자는 `internal`(`Refused`는 `GameSession`이 같이 쓰는 집·집 밖 거절에 쓴다).
  - `HouseFinishes`: `public HouseFinishes(); public void Load(HouseBuildSaveData data); public void Attach(HouseBuildFrame frame, Func<string, RoomFinishDefinition> find); public bool Attached { get; } public HouseBuildFrame Frame { get; } public string WallAt(CellPos floorCell); public string FloorAt(CellPos floorCell); public string Band { get; } public int Version { get; } public HouseFinishPreview Preview(string finishId, string tool, CellPos pointer, FurnitureUnlockProgress unlocks, Func<IReadOnlyList<ItemAmount<string>>, bool> canPay); public bool Apply(HouseFinishPreview preview); public HouseBuildSaveData ToSaveData()`.
  - `HouseBuildSaveData : IVersionedSave { public const int KnownVersion = 1; public int Version; public string FrameId = ""; public string Band = ""; public HouseFinishCellSaveData[] Finishes = Array.Empty<…>(); }`, `HouseFinishCellSaveData { public int X; public int Y; public string Wall = ""; public string Floor = ""; }`(`[Serializable]`, 공개 필드, `HouseStateSaveData`와 같은 꼴).
- 뜻(테스트가 고정한다):
  - **가리킨 칸 → 대상 바닥 칸**: 바닥 칸이면 그 칸. 벽 칸이면 출처표 그 칸의 `face`·`narrow` 부분 중 첫 것의 주인 칸(`cell + Owner`). 그 밖(띠·밖)은 대상 없음 → `NoTarget`.
  - **덮는 칸**(`Cells`): 도구 `room` → `RoomOf(대상)`, `brush` → `[대상]`. 모르는 도구는 `NoTarget`.
  - **벽지의 자리**: 벽지는 "정면 주인 칸"(출처표의 모든 `face`·`narrow` 부분의 주인 칸 집합, `Attach` 때 한 번 셈)에만 든다. 벽지 요청의 `ChangedCells`·비용은 덮는 칸 ∩ 정면 주인 칸 중 보이는 값이 다른 칸. 바닥 요청은 덮는 칸 중 보이는 값이 다른 칸.
  - **보이는 값**: 기록의 그 자리가 비었거나, `find`가 null이거나, 정의의 `Slot`이 그 자리가 아니면 프레임 기본. 아니면 기록의 id.
  - **판정 순서**(첫 실패가 이유): 붙지 않음 → `NotInHouse`; 정의 없음·자리가 `wall`/`floor`가 아님 → `UnknownFinish`; 대상 없음 → `NoTarget`; 잠김(`UnlockToken`이 있고 `unlocks.IsUnlocked`가 false) → `Locked`; 바뀌는 칸 0 → `NothingToChange`; `canPay(Cost)` false → `NotEnoughMoney`. 이유가 있어도 `Cells`·`ChangedCells`·`Cost`는 채운다(화면이 보인다).
  - **비용**: 정의의 칸당 `Cost` × 바뀌는 칸 수, 아이템별로 합친다(정의 순서 유지).
  - **적용**: `Apply`는 `CanApply`이고 `StateVersion == Version`일 때만, `ChangedCells`의 그 자리를 새 id로(프레임 기본이면 빈 글), 두 자리가 다 빈 기록은 지우고, `Version++`. 메모리만, 던지지 않는다. 아니면 false·무변경.
  - **기록 보존**: `Load`의 모든 기록(데이터에 없는 id, 지금 바닥이 아닌 칸·격자 밖 칸 포함)을 그대로 들고 `ToSaveData`에 다시 쓴다. 정렬은 y 다음 x. `FrameId`는 붙은 뒤면 프레임의 것, 아니면 읽은 값. `Band`는 그대로(띠 색 명령은 작업 7).
- [ ] **Step 1: 실패하는 테스트**
  - `RoomFillTests`(격자는 테스트 안의 글자 평면 — 새 집 평면과 시험 방 평면을 위 절의 글 그대로 복사, `#` 벽 `.` 바닥 `D` 벽): `NewHouse_HasFourRoomsAndThreeDoorways`(방 바탕 칸 수 84·52·91·48, 문간 칸 8), `NewHouse_FillFromEachRoom_StopsAtDoorways`(87·55·96·53, 다른 방 칸 없음), `FillFromDoorway_IsThatDoorwayOnly`, `TwoRooms_StripUnderThickWall_JoinsItsRoom`(65·75), `GapOfThree_IsOneRoom`(폭 3 틈), `NarrowDeadEnd_JoinsItsRoom`(한 방에만 닿은 좁은 칸), `SameRoom_SameListInstance`, `NotFloor_Empty`.
  - `HouseFinishesTests`(작은 격자 + `RoomTilePlan.Build(…, 2, 16, null)`, 꾸밈은 생성자로): `Floor_RoomFill_ChangesOnlyDifferentCells_CostPerCell`, `Wallpaper_CountsOnlyFaceOwnerCells`, `PointingAtWallFace_TargetsItsOwnerFloor`, `PointingAtBand_NoTarget`, `Locked_Unknown_NotAttached_Reasons_InOrder`, `NotEnoughMoney_StillListsCellsAndCost`, `Apply_SecondTimeNothingToChange`, `Apply_StalePreview_Refused`, `ChoosingDefault_ClearsTheSlot_AndEmptyRecordIsRemoved`, `UnknownIdRecord_LooksDefault_ChoosingDefaultKeepsRecord`, `RecordsOnWallsOrOutside_KeptAndWrittenBack`, `OtherFrameId_SameCellsApply`, `SaveData_SortedAndRoundTrips`.
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Core.Tests --filter "FullyQualifiedName~RoomFill|FullyQualifiedName~HouseFinishes"`. 기대: 컴파일 오류(타입 없음).
- [ ] **Step 3: 구현** — 위 시그니처와 뜻. `RoomFill.Build`와 정면 주인 칸 집합은 한 번만 만든다. `Preview`의 새 목록은 포인터 칸이 바뀔 때만 생긴다(매 프레임 부르지 않는다).
- [ ] **Step 4: 통과를 본다** — Core 전부(1278 + 새 것), `bash Scripts/ci/run-all.sh --checks-only`(Core 참조·id 분기).
- [ ] **Step 5: 커밋** — `[FEATURE] Core 꾸밈 상태와 방 채우기 — 바뀌는 칸만 세는 미리 보기`.

### Task 2: `GameSession` 꾸밈 명령·비용·저장 묶음 (HOUSE-001)

모델: 높음(세이브·지불). 멈춤 없음.

**Files:**
- Create: `game/Rootborn.Game/GameSession.HouseBuild.cs`
- Modify: `game/Rootborn.Core/src/Save/SaveFileNames.cs`(`public const string HouseBuild = "house-build.json";`), `game/Rootborn.Game/GameSession.Changes.cs`(`public const string HouseBuild = "house-build";`), `game/Rootborn.Screens/GameScreen.cs`(`ChangeTopics.HouseBuild = GameChangeTopics.HouseBuild`), `game/Rootborn.Game/GameSession.cs`(`Load`이 `StartReads.TryRead<HouseBuildSaveData>`로 읽음, `Save()` 묶음에 `SaveHouseBuildIfDirty(store)` — `SaveHouseLayoutIfDirty` 바로 다음, `SlotSharedFiles()`에 `SaveFileNames.HouseBuild`), `game/tests/Rootborn.Data.Tests/SaveShapeCoverage.cs`(`("house-build.json", typeof(Rootborn.Core.Building.HouseBuildSaveData))`)
- Test: `game/tests/Rootborn.Game.Tests/HouseFinishTests.cs`
- Modify: `game/tests/Rootborn.Game.Tests/SCENARIOS.md`, `.claude/rules/testing-discipline.md` 시나리오 표 — `HOUSE-001 | 방 꾸밈을 돈을 내고 바꾸고 같은 요청은 한 번만 | Core HouseFinishesTests, Game HouseFinishTests`(작업 6이 Screens·Host를 더한다)

**Interfaces:**
- Consumes: 작업 1 전부, `ItemInventory.TryExchange(IReadOnlyList<InventoryGrant>, IReadOnlyList<InventoryGrant>, Action)`·`CanExchange`, `GameSession.Shared<T>(Func<T>, Func<T,bool>)`, `Saved<T>`, `FindItem`, `House`, `ActiveWorkspace`, `_roomHouse`.
- Produces(`GameSession`):
  - `public void EnterHouseBuild(HouseBuildFrame frame)` — 짓는 집에 들어설 때 Host가 부른다(같은 프레임이면 다시 붙이지 않는다). 붙인 뒤 `Notify(null, GameChangeTopics.HouseBuild)`(저장 없음).
  - 조회: `public bool HouseBuildOpen`(붙었고, `House != null`, 일터가 아님), `public string HouseWallAt(CellPos floorCell)`, `HouseFloorAt(CellPos)`, `public string HouseBandColors()`(작업 7 전에는 프레임 기본), `public int HouseBuildVersion`, `public IReadOnlyList<RoomFinishDefinition> HouseFinishOptions(string slot)`(데이터 순서, 잠긴 것 포함), `public bool IsFinishUnlocked(RoomFinishDefinition finish)`.
  - `public HouseFinishPreview PreviewHouseFinish(string finishId, string tool, CellPos pointer)` — 같이 쓰는 집이면 이유 `SharedHouse`(1c가 연다), 집이 아니면 `NotInHouse`. `canPay`는 `Inventory.CanExchange(아이템으로 바꾼 비용, null)`.
  - `public bool ApplyHouseFinish(string finishId, string tool, CellPos pointer)` — 미리 보기를 새로 내고, `CanApply`가 아니면 false. 맞으면 가구 사기(`PurchaseFurniture`)와 같은 꼴: `Shared(…)` 안에서 `Inventory.TryExchange(비용을 InventoryGrant로, null, 커밋)` — 커밋은 `_houseFinishes.Apply(preview)`와 더러움 표시뿐(메모리, I/O 없음) — 적용됐으면 `Saved(…)`가 저장하고 `HouseBuild`·`Inventory`를 알린다. 비용이 빈 꾸밈(무료)도 같은 길이다.  - `private void SaveHouseBuildIfDirty(ISaveStore batch)` — 붙었든 아니든 `_houseFinishes.ToSaveData()`를 그 묶음에 쓴다(더럽혀졌을 때만). `Save()` 끝에서 더러움 표시를 지운다.
- [ ] **Step 1: 실패하는 테스트** — `HouseFinishTests`(실제 `data/`, 임시 폴더 저장소, 새 집 출처표는 World 로더로 만들거나 `art/kit/rooms/house.json` 평면 → `RoomGrid`로; 돈은 `Inventory.Add(Item("item.allowance"), n)` 준비):
  - `HOUSE_001_RoomFill_PaysPerChangedCell_AndChangesAllAtOnce`(왼쪽 위 방 바닥 → 돈이 87 × 칸당 값만큼 줄고, 그 방 칸이 모두 새 바닥, 파일에 기록 87개)
  - `NotEnoughMoney_ChangesNothing`(지갑·`HouseFloorAt`·파일 바이트 그대로, 알림 없음)
  - `Locked_And_NothingToChange_ChangeNothingAndCostNothing`
  - `SameRequestTwice_SecondCostsNothing`
  - `SaveAndNewSession_SameRequestCostsNothing`(저장 → 새 `GameSession` → `EnterHouseBuild` → 같은 꾸밈으로 보이고 같은 요청은 `NothingToChange`)
  - `SaveFailure_LeavesFilesAsBefore`(묶음 커밋에서 던지는 저장소 감싸개 — 있는 도우미를 `git grep -n "Commit" game/tests/Rootborn.Game.Tests`로 찾고 없으면 테스트 안에 — 스냅샷 지갑과 `house-build.json` 둘 다 옛 바이트)
  - `SharedHouse_RefusedWithReason`, `NotInHouse_RefusedWithReason`
  - `WithoutAttach_SaveKeepsLoadedRecords`(붙기 전 저장해도 읽은 기록이 그대로)
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Game.Tests --filter "FullyQualifiedName~HouseFinishTests"`. 기대: 컴파일 오류.
- [ ] **Step 3: 구현.**
- [ ] **Step 4: 통과를 본다** — Game 전부(`SavedFilesAreRegisteredTests`, `NewerSlotIsPlayableTests`, `DeletedSaveFileTests` 포함), Data(`SaveShapeRegistrationTests`), Core.
- [ ] **Step 5: 커밋 둘** — `[META] 짓는 집 꾸밈 세이브 house-build.json — Save 묶음, Require에 넣지 않음`, `[FEATURE] GameSession 꾸밈 명령 — 칸당 비용, 같은 요청은 한 번만(HOUSE-001)`.

### Task 3: 게임이 고른 꾸밈으로 다시 그린다

모델: 높음. **멈춤: 게임에서 방 하나의 바닥이 다른 종류로 바뀐 스크린샷을 사용자에게 보인다.**

**Files:**
- Create: `game/Rootborn.World/HouseFinishLookup.cs`(엔진 없음)
- Modify: `game/Rootborn.World/RoomTileCompositor.cs`(`ComposedMapTiles.Compose` 오버로드), `game/Rootborn.World/RoomFinishArt.cs`(조각 찾기 캐시), `game/Rootborn.Host/World/WorldScreen.cs`, `game/Rootborn.Host/World/ComposedRoomTextures.cs`, `game/Rootborn.Host/World/FurnitureView.cs`(`HostPlacementView`), `game/Rootborn.Host/HostGame.cs`(`SyncHouse`가 `EnterHouseBuild`, `session.Changed`에 `HouseBuild`), `game/Rootborn.Screens/Furniture/FurnitureWidgets.cs`(`IPlacementView`에 기본 구현 메서드 하나)
- Test: `game/tests/Rootborn.World.Tests/RoomTileCompositorTests.cs`(더함), `game/tests/Rootborn.Host.Tests/HouseFinishHostTests.cs`(새)

**Interfaces:**
- Consumes: 작업 2의 조회·`ApplyHouseFinish`·`HouseBuildVersion`, `GameChangeTopics.HouseBuild`.
- Produces:
  - World `public interface IHouseFinishLookup { string WallAt(CellPos floorCell); string FloorAt(CellPos floorCell); string BandColors { get; } }`, `public sealed class HouseFinishPreviewLookup : IHouseFinishLookup { public HouseFinishPreviewLookup(IHouseFinishLookup inner, HouseFinishPreview preview); }`(미리 보기의 `ChangedCells`·`Slot` 자리만 `FinishId`로 덮는다), `public static HouseBuildFrame HouseBuildFrames.From(LoadedMap map)`(짓는 집이 아니면 null).
  - `ComposedMapTiles.Compose(LoadedMap map, RoomTileCompositor compositor, Func<string, RoomFinishDefinition> finish, IHouseFinishLookup lookup)` — lookup이 null이면 지금과 같다(기존 오버로드가 이것을 부른다). 칸의 id가 비었거나 정의가 없으면 기본 꾸밈(문제 줄 없음 — 지운 꾸밈은 기본으로 그린다). 띠 색은 `lookup.BandColors`(비면 `build.BandColors`).
  - `IPlacementView`: `void ShowFinishPreview(HouseFinishPreview preview) { }`(기본 구현 — Screens 시험 뷰·Simulation 뷰는 고치지 않는다). null = 지움.
  - `WorldScreen`: `public IHouseFinishLookup HouseFinishes { get; set; }`(HostGame이 `GameSession` 어댑터를 넣는다), `public void ShowFinishPreview(HouseFinishPreview preview)`(같은 것(`SameAs`)이면 아무것도 안 함; 바뀌면 다음 그리기에서 미리 보기 조합 하나를 만들고 그동안 그것을 그린다; null이면 버린다), `public int PreviewComposeCount { get; }`, 지금의 `RefreshComposedTiles`·`ComposeCount`·`ComposedTextureBytes`.
- 정할 것(작업 9의 측정으로 판단): **꾸밈이 바뀌면 집 전체를 다시 조합한다**(504칸 조합 0.4ms, 텍스처 올리기 포함 1~5ms — 바뀐 칸만 고르는 복잡함보다 싸다). 묶음 크기가 같으면 텍스처를 다시 만들지 않고 `SetData`. 미리 보기도 같은 길(칸 묶음·꾸밈이 바뀔 때만).
- 함께 고친다: (1) 조합 칸은 들고 있는 텍스처로 바로 그리고 `_runtime` 표와 `Load`의 첫 줄을 없앤다(프레임당 약 1000번의 사전 조회), (2) 그림 캐시 손질의 한도 계산에 조합·미리 보기 텍스처 바이트를 더한다, (3) `WorldScreen` 생성자에서 데이터의 모든 `RoomFinishDefinition.Sheet`(서로 다른 것)를 미리 읽는다(첫 입장 23.8ms → 목표 5ms 아래, 결정 22), (4) `RoomFinishArt.TryPiece`·`TryCommon`이 (시트, 이름)을 한 번만 이어 붙이게 캐시.
- [ ] **Step 1: 실패하는 테스트**
  - World `RoomTileCompositorTests`: `Lookup_PerCellFloor_OnlyThatRoomChanges`(실제 집, 왼쪽 위 방 칸만 다른 바닥 — 그 방 칸과 띠 옆 비침만 다른 조각, 다른 방은 기본 조합과 픽셀이 같다), `Lookup_UnknownId_DrawsDefault_NoProblem`, `PreviewLookup_OverridesOnlyChangedCells`.
  - Host `HouseFinishHostTests.HOUSE_BUILD_001_ChangedFloor_IsRedrawnOnce_AndAfterContinue`: 새 게임(입력, `HouseMapHostTests`처럼) → 집으로 `WalkTo` → 준비 단계: 돈을 주고 `Session.ApplyHouseFinish("finish.floor.carpet", "room", 왼쪽 위 방 칸)` → 다음 프레임에 `ComposeCount`가 하나 늘고 그 방 칸의 조합 칸 그림이 카펫 조각에서 왔다(GPU `GetData`와 `RoomTileCompositor` 결과가 같다 — 색 단언 아님) → 스크린샷 → 끄고 이어 하기(입력) → 같은 그림 → 스크린샷. `output/house-building/stage1b/house-finish-NN-*.png`.
  - Host `HOUSE_BUILD_002_FirstEntryUnder5ms_AndTextureCounted`: 들어설 때 `LastComposeMilliseconds`를 출력하고 50ms 아래를 단언(느슨한 지킴이), `CachedTextureBytes` 손질 계산에 조합 바이트가 든다.
- [ ] **Step 2: 실패를 본다** — World `--filter "FullyQualifiedName~RoomTileCompositor"`, Host `--filter "FullyQualifiedName~HouseFinishHost"`.
- [ ] **Step 3: 구현** — `HostGame.SyncHouse`: 맵이 짓는 집이면 `Session.EnterHouseBuild(HouseBuildFrames.From(map))`. `session.Changed`: `HouseBuild`면 `World.RefreshComposedTiles(World.Session.Map.Id)`. `HostPlacementView.ShowFinishPreview` → `WorldScreen.ShowFinishPreview`.
- [ ] **Step 4: 통과를 본다** — World 전부, Host `--filter "FullyQualifiedName~HouseFinishHost|FullyQualifiedName~HouseTiles|FullyQualifiedName~HouseMap|FullyQualifiedName~WorldFrameCost|FullyQualifiedName~FurniturePlacementHost"`(`WORLD_FRAME_001` 할당 0 유지), `run-all.sh --checks-only`.
- [ ] **Step 5: 직접 본다** — 스크린샷을 열어 바뀐 방, 띠 양옆 두 바닥, 문간, 이음매. **사용자에게 경로와 첫 입장 시간·텍스처 바이트를 보이고 멈춘다.**
- [ ] **Step 6: 커밋 둘** — `[FEATURE] 짓는 집을 칸마다의 꾸밈으로 다시 조합 — 꾸밈 알림에서만`, `[REFACTOR] 조합 텍스처를 사전 조회 없이 그리고 예산에 넣고 시트를 미리 읽는다`.

### Task 4: 꾸밈 세이브의 하위호환·상위호환

모델: 높음(세이브). 멈춤 없음.

**Files:**
- Test: `game/tests/Rootborn.Data.Tests/HouseBuildSaveTests.cs`(새), `game/tests/Rootborn.Data.Tests/NewerSaveInAnOlderProgramTests.cs`(사례 하나), `game/tests/Rootborn.Game.Tests/HouseBuildCompatTests.cs`(새), `game/tests/Rootborn.Game.Tests/SlotCheckpoints`가 든 파일(단계 하나 더함 — `git grep -n "furniture-placed" game/tests`로 찾는다)
- Create(명령의 결과, 커밋): `game/tests/Rootborn.Data.Tests/Fixtures/saves/monogame-20261114/…`; Modify: 그 폴더 위 `README.md` 표에 한 줄

스킬 `save-compat-change`의 순서를 단계로 푼다. 세이브 모양은 작업 2에서 처음 생겼으므로(`KnownVersion` 1) 고정 폴더는 "꾸밈이 든 슬롯"으로 **더한다** — 있는 폴더(`editor-20261105`, `exe-20261105`, `monogame-20261112`)는 고치지 않는다.

**Interfaces:** Consumes 작업 1·2. Produces 없음(테스트·고정 파일).
- [ ] **Step 1: 하위호환 실패 테스트** — `HouseBuildCompatTests`: `OldSlotWithoutFile_Opens_DefaultFinishes_ReadingWritesNothing`(`monogame-20261112/slot-robust` 사본, 집에 들어가 `EnterHouseBuild` → 기본 꾸밈, 슬롯 파일 목록·바이트 그대로), `UnknownFinishId_KeptAndDrawnAsDefault`(저장 → 새 판 → 기록 그대로), `LockedFinishAlreadyUsed_StaysVisible_ButCannotBeChosenAgain`, `RecordOnWallOrOutside_KeptNotApplied`, `OtherFrameId_SameCellsApply_RecordsKept`, `NewPlayerInSlotWithOnlyHouseBuild_IsNewGameNotDamaged`.
- [ ] **Step 2: 상위호환 실패 테스트** — Data `NewerSaveInAnOlderProgramTests`에 `HouseBuildSaveData` 사례(맨 위 모르는 필드 `WallsAdded`·`WallsRemoved`가 읽고 다시 쓴 뒤 남는다 — 설계서 7.1절이 2단계 벽을 맨 위 필드로 둔 까닭), Data `HouseBuildSaveTests.FileStoreRoundTrip_AndVersionWritten`, Game `HouseBuildCompatTests.OlderBuildVisit_FileUntouched_FurnitureStillPlays`(꾸밈 파일을 모르는 흐름 — `GameSession`이 `EnterHouseBuild` 없이 하루를 보내고 저장해도 `house-build.json` 바이트 그대로; 옛 빌드는 이 파일을 모른다).
- [ ] **Step 3: 실패를 본다** — `dotnet test game/tests/Rootborn.Game.Tests --filter "FullyQualifiedName~HouseBuildCompat"`, Data도. 작업 2가 맞게 했으면 일부는 처음부터 통과한다 — 그럼 Core `HouseFinishes`의 보존 코드를 일부러 망가뜨려(기록 버리기) 실패하는 것까지 보고 되돌린다(보고에 적는다).
- [ ] **Step 4: 고친다**(필요하면). 고정 파일·기대값을 고쳐 통과시키지 않는다.
- [ ] **Step 5: 새 날짜 고정 폴더** — 슬롯 단계에 "꾸밈 하나 바꿈"(`house-finished`, 가구 단계 다음)을 더하고 `ROOTBORN_FIXTURE_OUT=<저장소>/game/tests/Rootborn.Data.Tests/Fixtures/saves/monogame-20261114 dotnet test game/tests/Rootborn.Game.Tests --filter "Name~WriteCurrentSlotFixture"`. README 표에 한 줄. `OldSaveTests`가 새 폴더를 읽는지 본다(읽힌다, 버리는 필드 없음, 읽고 다시 써도 같다, 하루를 넘긴다).
- [ ] **Step 6: 통과를 본다** — Core, Data(`OldSaveTests`, `SaveShapeRegistrationTests`, `NewerSaveInAnOlderProgramTests`), Game(`SavedFilesAreRegisteredTests`, `DeletedSaveFileTests`, `NewerSlotIsPlayableTests`, 새 것).
- [ ] **Step 7: 커밋** — `[TEST] 꾸밈 세이브 하위·상위호환과 새 고정 폴더 monogame-20261114`. 본문에 "하위호환 확인"·"상위호환 확인" 테스트 이름을 나눠 적고, 실제 옛 빌드 실행 파일로 연 것은 아니라고 쓴다.

### Task 5: 키트·시험 표 다지기 — 그림자 8px, 다른 `faceHeight`·`band`, 문지방 전부, 맵 검사

모델: 높음(두 언어 규칙). 멈춤 없음.

**Files:**
- Modify: `Tools/pixel-kit/pixelkit/room_tiles.py`(바닥 그림자 범위), `Tools/pixel-kit/pixelkit/room_vectors.py`(사례), `Tools/pixel-kit/tests/test_room_vectors.py`·`test_room_tiles.py`(있는 파일에), `Tools/pixel-kit/tests/fixtures/room-vectors.json`(명령의 결과)
- Modify: `game/Rootborn.Core/src/Building/RoomTilePlan.cs`, `game/tests/Rootborn.Core.Tests/Building/RoomTileVectorTests.cs`
- Modify: `game/Rootborn.World/MapChecks.cs`(`CheckBuildArt`), `game/tests/Rootborn.World.Tests/MapContractTests.cs`, `game/tests/Rootborn.World.Tests/RoomTileCompositorTests.cs`(조각 시트 시험을 `faceHeight` 2·`band` 16 사례로 좁힘)
- Create(명령의 결과, 커밋): `art/world/rooms/house/{house.ldtk,tiles.png,tiles.slots.json,maps.json}` 중 바뀐 것(`room build house --force`) — **`test.two-rooms`는 다시 만들지 않는다**

**Interfaces:**
- Consumes: `plan_tiles`·`plan_sources`·`_plan`(Python), `RoomTilePlan.Build`(C#), `RoomFinishArt.TryPiece`.
- Produces: 시험 표 새 사례 — `face-height-1`·`face-height-3`(각각 새 집 평면 사본), `band-8`·`band-24`(시험 방 사본), `sills-all`(벽 문 9종 `sill:<n|e|w>:<0-16|8-24|16-32>`이 모두 나오는 격자 — 띠 16에서는 범위가 하나라 n·e·w 셋 + 다른 `band` 사례에서 나머지; 사례 머리에 "문지방 열쇠 목록"을 적어 테스트가 9종(시트의 `common/sill:*`) 전부 나왔는지 본다). `MapChecks` 새 문제: `"짓는 집: 꾸밈 <id>에 출처표가 쓰는 조각 <이름>이 없다"`(slot wall·floor인 **모든** 꾸밈 × 그 맵 출처표의 `face`·`narrow` 조각 이름·`floor`·`floor:shade` — 맵마다 한 번).
- 그림자 규칙의 고침(뜻): 세로 벽 띠 칸에서 띠 옆으로 비치는 바닥 픽셀 바로 위 칸의 같은 열이 정면(늘인 정면 포함)이면, 그 픽셀은 그 칸 바닥 열쇠의 그림자 범위에 든다 — 지금은 늘인 정면의 열이 빠져 8px 일찍 끊긴다(새 집 (13,3)).
- [ ] **Step 1: 실패하는 테스트** — Python `test_shadow_continues_under_stretched_face`(새 집 사례 (13,3)의 바닥 부분 열쇠가 띠 양옆 비침 열을 그림자 범위에 넣는다), `test_every_sill_piece_is_in_the_table`, `test_vectors_file_is_up_to_date`(다시 만들기 전 실패). World `MapContractTests.Checks_CatchFinishMissingAPiece`(좌표표 사본에서 한 꾸밈의 `face:2` 하나를 지운 맵 → 문제). C# `RoomTileVectorTests`는 시험 표를 다시 만들면 새 사례로 실패해야 한다.
- [ ] **Step 2: 실패를 본다** — `python -m unittest discover -s Tools/pixel-kit/tests -t Tools/pixel-kit -p "test_room_*.py"`.
- [ ] **Step 3: 구현** — Python 규칙 → `python Tools/pixel-kit/kit.py room vectors` → `test_plan_tiles_unchanged_on_every_case`의 해시는 **새 집 둘레 바뀐 칸만** 다르다는 것을 확인한 뒤 새로 적는다(바뀐 사례·칸을 커밋 본문에) → C# 같은 고침 → `MapChecks` → `python Tools/pixel-kit/kit.py room build house --force`(방 폴더 `tiles.png`가 바뀐다 — Host `HOUSE_TILES_001`의 "조합 = 구운 그림"을 지키려고).
- [ ] **Step 4: 통과를 본다** — 도구 전부(585 + 새 것), `kit.py lint` 0, `kit.py room vectors --check`, `kit.py room check art/world/rooms/house/house.ldtk`, Core 전부, World 전부, Host `--filter "FullyQualifiedName~HouseTiles"`. 바뀐 칸을 확대해 직접 본다(`output/pixel-kit/house/…_x4.png`).
- [ ] **Step 5: 커밋 넷** — `[CHORE] 픽셀 키트 방 — 늘인 정면 아래 바닥 그림자, 시험 표에 다른 정면 높이·띠 두께·문지방 전부`, `[FEATURE] Core 고르기 규칙에 같은 그림자 고침`, `[ASSET] 새 집 방 폴더 다시 만듦 — 늘인 정면 아래 그림자`, `[FEATURE] 맵 검사 — 모든 꾸밈이 출처표의 조각을 갖는가`.

### Task 6: 가구 배치 화면의 꾸미기 분류 (HOUSE-001 화면)

모델: 중간(여러 파일 통합). **멈춤: P → 꾸미기 → 카드 고르고 방을 눌러 바꾸는 스크린샷을 사용자에게 보인다.**

**Files:**
- Create: `game/Rootborn.Screens/Furniture/FinishPaletteModel.cs`(표시 모델), `game/Rootborn.Screens/Furniture/FinishPalette.cs`(꾸미기 분류의 노드 묶음과 그 조작 — `FurniturePlacementScreen`이 52KB라 따로 둔다)
- Modify: `game/Rootborn.Screens/Furniture/FurniturePlacementScreen.cs`(분류 줄 셋, `Metrics`, 포인터·상호작용 키를 꾸미기 분류일 때 `FinishPalette`로), `game/localization/ko.json`, `en.json`
- Test: `game/tests/Rootborn.Screens.Tests/Furniture/FinishPaletteScreenTests.cs`, `game/tests/Rootborn.Host.Tests/HouseFinishHostTests.cs`(더함)
- Modify: `SCENARIOS.md`·`testing-discipline.md`의 HOUSE-001 줄에 `Screens FinishPaletteScreenTests, Host HouseFinishHostTests`

**화면 구성**(작성자 추천, 결정 대기 27번 — 지금 화면의 관례를 따른다):
- 분류 줄: 가구 / 보관 / 꾸미기 — 단추 셋을 같은 줄에(`CategoryW` 120, `CategoryPitch` 128 — 팔레트 안쪽 368px에 맞춤). 분류마다 따로 노드(`furniture.group.finish`)를 켜고 끈다. 고른 분류는 지금처럼 밝기 + 안쪽 테두리.
- 꾸미기 묶음 위에서 아래로: 자리 탭 줄 벽지 / 바닥(작업 7이 "벽 윗면 색"을 셋째로), 도구 줄 방 채우기 / 칸 붓(고른 것 표시는 같은 `FurnitureCategoryButton`), 카드 격자(가구 칸과 같은 `UiSlot` 5열, 넘치면 같은 쪽 넘김 — 종류가 늘어도 보이는 쪽만 그린다). 벽 카드는 그림 둘을 위아래로(`face:2` 위, `face:1` 아래), 바닥 카드는 `floor` 하나 — 그림 id는 `<Sheet>#<Art>/<조각>`, 조각 이름은 Core `RoomFinishPieces`(새 상수 묶음: `FaceUpper = "face:2"`, `FaceLower = "face:1"`, `Floor = "floor"`). 잠긴 카드는 흐리게(`IsActive = false`와 같은 칠) — 새 아이콘 키를 만들지 않는다.
- 오른쪽 패널(`furniture.settings`)은 꾸미기 분류일 때 가구 정보 노드를 숨기고 꾸밈 정보 노드(`furniture.finish.info`)를 켠다: 이름, 칸당 값(또는 무료), 바뀌는 칸 수, 비용, 가진 돈, 잠김 안내 또는 안 되는 이유.
- 월드 위: 포인터 칸이 바뀔 때만 `PreviewHouseFinish`를 부르고, 결과가 앞의 것과 `SameAs`가 아니면 바꿔 들고 `_view.ShowFinishPreview(preview)`. 바뀌는 칸은 `Metrics.ValidFootprint`(안 되면 `InvalidFootprint`)로 밝힌다 — 칸 사각형 목록은 미리 보기가 바뀔 때 한 번 만든다. 누르면 `ApplyHouseFinish` 하나. 상호작용 키는 바라보는 칸을 누른 것과 같다(지금 `OnKey`처럼).

**Interfaces:**
- Consumes: 작업 2의 `HouseFinishOptions`·`IsFinishUnlocked`·`PreviewHouseFinish`·`ApplyHouseFinish`·`HouseBuildOpen`, 작업 3의 `IPlacementView.ShowFinishPreview`, `FurnitureRoom.ToMap`.
- Produces: 요소 id `furniture.category.finish`, `furniture.group.finish`, `furniture.finish.tab.wall`·`.tab.floor`, `furniture.finish.tool.room`·`.tool.brush`, `furniture.finish.card.<n>`(+ `.icon`, `.icon2`), `furniture.finish.info.name`·`.price`·`.cells`·`.cost`·`.money`·`.reason`. `public bool FinishShown`, `public FinishPaletteModel Finishes`, `public HouseFinishPreview FinishPreview`(시험용 조회). `FurniturePlacementScreen.ShowCategory(bool stored)`는 그대로 두고 `public void ShowCategory(string category)`(`FurniturePlacementScreen.Categories.Furniture`·`Stored`·`Finish` 글 상수)를 더해 옛 것이 그것을 부른다.
- 문구 키(ko / en, 그대로 쓴다): `ui.furniture.category.finish` 꾸미기 / Decorate; `ui.house-build.tab.wall` 벽지 / Wallpaper; `ui.house-build.tab.floor` 바닥 / Floor; `ui.house-build.tool.room` 방 채우기 / Fill room; `ui.house-build.tool.brush` 칸 붓 / One tile; `ui.house-build.pick` 꾸밈을 고르세요 / Choose a finish; `ui.house-build.price` 칸당 {0} {1} / {0} {1} per tile; `ui.house-build.price.one` 칸당 1 {1} / 1 {1} per tile; `ui.house-build.free` 무료 / Free; `ui.house-build.cells` 바뀌는 칸 {0}칸 / {0} tiles will change; `ui.house-build.cells.one` 바뀌는 칸 1칸 / 1 tile will change; `ui.house-build.cost` 비용 {0} {1} / Cost: {0} {1}; `ui.house-build.money` 가진 {1} {0} / You have {0} {1}; `ui.house-build.reason.no-target` 바닥이나 벽 정면을 가리키세요 / Point at a floor or a wall face; `…reason.unknown-finish` 쓸 수 없는 꾸밈이에요 / This finish is not available; `…reason.locked` 잠겨 있어요. 가게에서 살 수 있어요 / Locked. You can buy it at a shop; `…reason.nothing-to-change` 이미 이 꾸밈이에요 / It already has this finish; `…reason.not-enough-money` 돈이 모자라요 / Not enough money; `…reason.not-in-house` 집에서만 꾸밀 수 있어요 / You can decorate only at home; `…reason.shared-house` 같이 쓰는 집에서는 아직 꾸밀 수 없어요 / You cannot decorate a shared house yet. `.one`은 수가 1일 때 고르는 키(`{1}` 아이템 이름은 아이템 정의의 이름 키).
- [ ] **Step 1: 실패하는 테스트**
  - Screens `FinishPaletteScreenTests`(실제 데이터, 시험 뷰): `Categories_ToggleTheirOwnNodes`(셋), `WallTab_ShowsWallFinishes_FloorTab_FloorFinishes`(데이터 수와 같다), `LockedCard_IsDimmed_AndPickingShowsLockedText`, `HoverRoom_ShowsCellsCostMoney`, `Click_Applies_ThenNothingToChange`, `BrushTool_OneCell`, `PreviewChangesOnlyWhenCellsChange`(같은 방 안에서 칸을 옮기면 같은 미리 보기 인스턴스, 다른 방이면 바뀜 — 시험 뷰가 받은 `ShowFinishPreview` 횟수), `SharedHouse_ShowsReason`, `LongText_StaysInsidePanels`(2배 길이), `LanguageSwitch_UpdatesTexts`.
  - Host `HOUSE_BUILD_010_DecorateFromPlacementScreen`: 새 게임(입력) → 집 → 준비: 돈 → P → 꾸미기 단추 → 바닥 탭 → 카펫 카드 → 왼쪽 위 방 칸에 포인터(`View(p).CellRect` + `ClickAt`) → 미리 보기 스크린샷 → 누름 → 첫 프레임에 미리 보기 없음·새 조합(Review Focus 5) → 스크린샷 → 벽지 탭·잔무늬 카드 → 북쪽 벽 정면을 누름 → 스크린샷 → 끄고 이어 하기 → 같은 그림 스크린샷. `output/house-building/stage1b/house-decorate-NN-*.png`.
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Screens.Tests --filter "FullyQualifiedName~FinishPalette"`.
- [ ] **Step 3: 구현.** 보관 분류·가구 분류의 기존 동작과 테스트는 그대로(`StoredFurnitureScreenTests`, `FurniturePlacementScreenTests`).
- [ ] **Step 4: 통과를 본다** — Screens 전부, Host `--filter "FullyQualifiedName~HouseFinishHost|FullyQualifiedName~FurniturePlacementHost|FullyQualifiedName~HouseMoveHost|FullyQualifiedName~TestThemeScreens|FullyQualifiedName~KeyGuide"`, `run-all.sh --checks-only`(현지화 키 두 언어).
- [ ] **Step 5: 직접 본다** — 스크린샷을 열어 분류 셋, 카드 그림, 잠긴 카드, 오른쪽 패널 글자, 미리 보기 칸, 겹친 글자·잘린 패널. **사용자에게 경로를 보이고 멈춘다.**
- [ ] **Step 6: 커밋** — `[UI] 가구 배치에 꾸미기 분류 — 벽지·바닥 카드, 방 채우기·칸 붓, 미리 보기`.

### Task 7: 집 하나의 띠 색 고르기

모델: 중간. 멈춤 없음.

**Files:**
- Modify: `data/building/RoomFinishDefinition.json`(띠 꾸밈 다섯 줄), `game/Rootborn.Core/src/Building/RoomFinishDefinition.cs`(`RoomFinishSlots.Band = "band"`, `IsValid`가 `NameKey`도 본다), `HouseFinishes.cs`(띠 미리 보기·적용), `game/Rootborn.Game/GameSession.HouseBuild.cs`, `game/Rootborn.World/MapChecks.cs`, `game/Rootborn.Screens/Furniture/FinishPalette.cs`, `game/localization/ko.json`, `en.json`
- Test: Core `HouseFinishesTests`(더함), Data `RoomFinishDefinitionTests`(더함), World `RoomTileCompositorTests`·`MapContractTests`(더함), Screens `FinishPaletteScreenTests`(더함), Game `HouseFinishTests`(더함)

데이터(값은 시험용, 결정 대기 26번): `{ "type": "RoomFinishDefinition", "id": "finish.band.cream", "slot": "band", "nameKey": "finish.band.cream.name", "sheet": "art/world/finishes/finishes.png", "art": "wall.top.cream", "costItems": [], "costCounts": [], "unlockToken": "" }` — `cream`·`brown`·`light`·`slate`·`plaster`(art = 좌표표 `bands`의 `wall.top.cream`·`wall.top.brown`·`wall.top.light`·`wall.top.slate`·`wall.plaster.top`). 이름: 크림색 / Cream, 갈색 / Brown, 밝은 회색 / Light gray, 짙은 남회색 / Slate, 회반죽색 / Plaster. 무료(빈 비용)·처음부터 열림.

**Interfaces:**
- Produces: `HouseFinishes.PreviewBand(string finishId) : HouseFinishPreview`(Slot `band`, `Cells`·`ChangedCells` 빈 목록, 이유: 정의 없음·자리가 band 아님 → `UnknownFinish`, 잠김 → `Locked`, 보이는 띠 색 줄(`find(Band)?.Art`, 없으면 프레임 기본)과 정의의 `Art`가 같으면 `NothingToChange`, 비용은 정의의 `Cost` 한 번 — 지금 데이터는 빈 목록), `Apply`가 band 미리 보기도 받는다(`Band` = 그 id, 프레임 기본의 `Art`와 같으면 빈 글), `public string BandColors`(보이는 띠 색 줄). `GameSession.PreviewHouseBand(string finishId)`, `ApplyHouseBand(string finishId)`(같은 지불 길, 알림 `HouseBuild`), `HouseBandColors()`가 고른 색. 화면: 탭 `furniture.finish.tab.band`(`ui.house-build.tab.band` 벽 윗면 색 / Wall top color), 띠 탭은 카드 대신 `UiScrollList` 줄(이름, 지금 색이면 `ui.house-build.band.current` "{0} (지금)" / "{0} (current)"), 줄을 누르면 바로 `ApplyHouseBand`(무료 — 미리 보기 없이).
- 맵 검사: 모든 band 꾸밈의 `Art`가 그 `Sheet` 좌표표 `bands`에 있다, band 꾸밈의 `Sheet` = 맵 기본 벽 꾸밈의 `Sheet`(조합기는 띠 색·공용 조각을 한 시트에서 찾는다 — 위 (가)).
- [ ] **Step 1: 실패하는 테스트** — Core `Band_Preview_Apply_SecondTimeNothing`, `Band_ChoosingMapDefault_ClearsField`; Data `BandFinishes_AreFree_AndResolve`; World `Compose_BandOtherThanMapDefault_PaintsChosenBand_SillAndDoorUnchanged`(띠 픽셀은 고른 줄의 네 색 중 그 칠, 문지방·문짝 픽셀은 기본 띠 조합과 같다 — 위 (나)의 사실을 테스트로); Game `ApplyHouseBand_SavedAndReadBack`; Screens `BandTab_RowsAndApply`.
- [ ] **Step 2: 실패를 본다.** — Core·Data·World 필터.
- [ ] **Step 3: 구현** — Host는 작업 3의 `IHouseFinishLookup.BandColors`로 이미 받는다(어댑터만 `HouseBandColors()`로).
- [ ] **Step 4: 통과를 본다** — Core, Data(`RealDataTests` — slot band도 등록된 종류), World, Game, Screens 전부, Host `--filter "FullyQualifiedName~HouseFinishHost"`(띠 색을 바꾸는 스크린샷 하나 더 — `house-decorate-NN-band.png`), `run-all.sh --checks-only`. 스크린샷을 직접 연다.
- [ ] **Step 5: 커밋 둘** — `[FEATURE] 집 하나의 벽 윗면 띠 색 고르기 — 무료, 다섯 색`, `[UI] 꾸미기 분류에 벽 윗면 색 탭`.

### Task 8: 잠긴 꾸밈의 가게 진열과 해금

모델: 중간. 멈춤 없음.

**Files:**
- Modify: `game/Rootborn.Core/src/Building/RoomFinishDefinition.cs`(판매 필드), `game/Rootborn.Data/Builders/BuildingBuilders.cs`(`BuilderHelpers.AddedLater`로 `priceItem`·`price`·`purchaseLocationId` — 가구와 같은 꼴), `data/building/RoomFinishDefinition.json`(잠긴 4종에 판매 필드), `game/Rootborn.Game/GameSession.HouseBuild.cs`, `game/Rootborn.Screens/Day/LocationModel.cs`, `game/Rootborn.Screens/Day/LocationScreen.cs`
- Create: `game/Rootborn.Core/src/Building/RoomFinishPurchase.cs`
- Test: Core `RoomFinishPurchaseTests.cs`, Game `HouseFinishShopTests.cs`, Screens 장소 화면 테스트 파일(있는 것에 — `git grep -n "FurnitureForSaleAt\|Offer" game/tests/Rootborn.Screens.Tests`)

가장 작은 길: 가구의 판매 구조(`IsForSale`·`PurchaseLocationId`·장소 화면의 살 목록 줄 `LocationRowKind.Offer`)를 그대로 꾸밈에 붙인다. 해금은 같은 `FurnitureUnlockProgress`에 `unlock.finish.*` 토큰 — 세이브 모양이 늘지 않는다. 값은 시험용(결정 대기 28번): 잠긴 4종 `price` 15, `priceItem` `item.allowance`, `purchaseLocationId` `location.corner-store`.

**Interfaces:**
- Produces: `RoomFinishDefinition`에 `ItemDefinition`가 아니라 id 글 `PriceItemId`, `int Price`, `string PurchaseLocationId`, `bool IsForSale => Price > 0 && PriceItemId.Length > 0 && UnlockToken.Length > 0 && PurchaseLocationId.Length > 0`(생성자 선택 인자 — 있는 호출은 그대로). `public static class RoomFinishPurchase { public static FurniturePurchaseResult Try(RoomFinishDefinition finish, ItemDefinition priceItem, ItemInventory inventory, FurnitureUnlockProgress unlocks); }`(가구 `FurniturePurchase.Try`와 같은 뜻 — 결과 enum을 다시 쓴다). `GameSession.FinishesForSaleAt(string locationId)`, `GameSession.PurchaseFinish(string finishId)`(`Shared` + `Saved`, 알림 `HouseBuild`·`Furniture`·`Inventory`). `LocationModel`: 꾸밈 판매도 `LocationRowKind.Offer` 줄(이름 키 = `NameKey`). `LocationScreen.Purchase`: 줄 id가 꾸밈 정의면(`Definitions.TryGet<RoomFinishDefinition>`) `PurchaseFinish`, 아니면 `PurchaseFurniture` — 종류로 고르는 것이지 id 분기가 아니다. 결과 문구는 지금 `dream.shop.*`.
- [ ] **Step 1: 실패하는 테스트** — Core 지불 다섯 경우(`Purchased`, `NotEnoughMoney` 무변경, `AlreadyOwned` 무비용, 같은 요청 두 번 → 두 번째 `AlreadyOwned`, 판매 아님); Game `ShopListsLockedFinishes_BuyUnlocks_CardBecomesUsable`, `SaveAndNewSession_StillUnlocked_SecondBuyAlreadyOwned`, `UnlockTokenSurvivesOlderShape`(스냅샷의 `FurnitureUnlocks.UnlockTokens`를 옛 모양 그대로 읽고 다시 써도 `unlock.finish.*`가 남는다 — 옛 빌드는 그 토큰을 쓰는 가구가 없어 아무 것도 열리지 않는다); Screens 장소 화면에 꾸밈 줄 넷, 사면 "샀어요".
- [ ] **Step 2: 실패를 본다.**
- [ ] **Step 3: 구현.** `EntityReferenceTests`의 참조 표에 `RoomFinishDefinition.purchaseLocationId → LocationDefinition` 한 줄.
- [ ] **Step 4: 통과를 본다** — Core, Data(`RealDataTests`, `EntityReferenceTests`, `DataFingerprintTests`), Game, Screens 전부, Host `--filter "FullyQualifiedName~Location"`.
- [ ] **Step 5: 커밋 둘** — `[FEATURE] 잠긴 꾸밈을 가게에서 산다 — 가구와 같은 해금 토큰`, `[BALANCE] 시험용 꾸밈 가게 값 15`.

### Task 9: 꾸미면 인테리어 기술이 자란다 (가구 놓기와 같은 하루 한도)

모델: 낮음. 멈춤 없음.

**Files:** Modify `game/Rootborn.Game/GameSession.HouseBuild.cs`; Test `game/tests/Rootborn.Game.Tests/HouseFinishTests.cs`(더함); Modify `SCENARIOS.md`·`testing-discipline.md`의 SKILL-001 줄("가구를 놓거나 방을 꾸미면").

가장 작은 변경(결정 10): 데이터는 그대로 — `ApplyHouseFinish`가 적용됐으면 가구 놓기와 같은 `GrowByPlacing(House.SurfaceId)`를 부른다(`data/workspaces/PlacementSkillGrowth.json`의 `house` 줄, 하루 3번 +1, 같은 하루 셈 `WorkspaceProgress`). 자랐으면 알림에 `Workspace`도. 띠 색(작업 7)은 칸이 바뀌지 않으므로 자라지 않는다.

**Interfaces:** Consumes `GrowByPlacing(string surfaceId)`. Produces 없음.
- [ ] **Step 1: 실패하는 테스트** — `Finish_GrowsInterior`, `FurnitureAndFinish_ShareTheDailyLimit`(가구 둘 + 꾸밈 둘 → +3), `NothingToChange_DoesNotGrow`, `Band_DoesNotGrow`, `NextDay_GrowsAgain`.
- [ ] **Step 2: 실패를 본다.** **Step 3: 구현.** **Step 4: 통과를 본다** — Game 전부(`WorkspaceTests` 포함).
- [ ] **Step 5: 커밋** — `[FEATURE] 방을 꾸미면 인테리어 기술 — 가구 놓기와 같은 하루 한도`.

### Task 10: 새 게임의 처음 배치 가구

모델: 중간. 멈춤 없음.

**Files:**
- Create: `game/Rootborn.Core/src/Placement/HouseStarterLayout.cs`, `data/building/HouseStarterLayout.json`
- Modify: `game/Rootborn.Data/Builders/BuildingBuilders.cs`(`b.Add("HouseStarterLayout", …)`, 놓을 줄은 `WorkspaceTemplate`의 `placements` 읽기와 같은 꼴), `game/Rootborn.Game/GameSession.cs`(`Load`의 새 게임 판단), `game/Rootborn.Game/GameSession.Furniture.cs`(`LoadHouse`), `game/Rootborn.Game/GameSession.Storage.cs`(`SaveHouseLayoutIfDirty` 옆)
- Test: Core `HouseStarterLayoutTests.cs`, Game `HouseStarterTests.cs`, Data `RealDataTests`(종류가 등록됐는지는 저절로)
- Modify: `SCENARIOS.md`·`testing-discipline.md` TOWN-001 줄에 `Game HouseStarterTests`

데이터(시험용, 결정 대기 29번 — 사용자가 고르지 않았다; 왼쪽 위 방, 가구 칸 좌표 = `(x, 17 - 맵 y)`):
`{ "type": "HouseStarterLayout", "id": "house-starter.default", "surfaceId": "house", "placements": [ { "furniture": "school.desk", "cell": { "x": 2, "y": 13 }, "direction": 0 }, { "furniture": "school.chair.north", "cell": { "x": 2, "y": 12 }, "direction": 0 }, { "furniture": "school.bookcase", "cell": { "x": 9, "y": 14 }, "direction": 0 } ] }` — 맵 칸 (2,4)~(3,4) 책상, (2,5) 의자, (9,3)~(10,3) 책장.

"새 게임"의 판단(세이브 모양을 늘리지 않는다 — 받음 기록 대신 파일 자체가 기록이다): `Load`에서 학생을 `NewStudent()`로 만들었고(스냅샷도 옛 영역 파일도 없음) 슬롯에 집 배치 파일(지금 단계 파일, 단계 전 파일)이 없고 같이 쓰는 집이 아니면 `_houseStarterPending = true`(메모리). 그러면 `LoadHouse`가 빈 기록 대신 처음 배치 기록으로 `RestoreRelocating`을 부르고 배치를 더럽힘으로 표시해 다음 `Save()` 묶음이 쓴다. 집을 열기 전에 저장되면 `Save()`가 처음 배치 기록을 그대로 쓴다. 쓰고 나면 표시를 지운다. 명령 없이 끝낸 새 게임은 아무것도 쓰지 않는다(다음에도 새 게임이라 같은 결과). 옛 세이브·다른 플레이어가 논 슬롯에는 주지 않는다.

**Interfaces:**
- Produces: `public sealed class HouseStarterLayout { public HouseStarterLayout(string id, string surfaceId, TemplatePlacement[] placements); public string Id, SurfaceId; public IReadOnlyList<TemplatePlacement> Placements; public bool IsValid; public FurniturePlacementSaveData[] Records(); }`(기록은 `HouseFurnitureLayout.BuildSaveRecords`가 쓰는 필드 꼴 그대로 — 가구 id, 기준 칸, 방향, 면; 인스턴스 id는 비운다 — 읽을 때 매긴다). 같은 면의 처음 배치가 둘이면 데이터 읽기 오류.
- [ ] **Step 1: 실패하는 테스트** — Core `Records_HaveFurnitureAnchorDirection`, `Invalid_WhenEmptyIdsOrBadDirection`; Game `NewGame_EnterHouse_StarterFurnitureIsPlaced`(셋, 칸이 데이터대로 — 막혔으면 옮겨짐도 받아들임), `NewGame_NoCommand_WritesNothing`, `NewGame_FirstSave_WritesStarter_NextSessionSame`, `OldFixtureSlots_GetNoStarter`(`monogame-20261112/slot-robust`, `editor-20261105/slot-1`), `SecondPlayerInPlayedSlot_GetsNoStarter`, `SharedHouse_GetsNoStarter`, `StarterFitsTheRealHouse`(모든 줄이 실제 새 집에서 `CanPlace` — 데이터를 바꾸면 여기서 걸린다).
- [ ] **Step 2: 실패를 본다.** **Step 3: 구현.**
- [ ] **Step 4: 통과를 본다** — Core, Data, Game 전부(`NewGameTests`, `SavedFilesAreRegisteredTests`, `UnitySaveTests`), Host `--filter "FullyQualifiedName~HouseMap|FullyQualifiedName~NewGameJourney"`(새 게임 집에 가구가 보이는 스크린샷 하나를 직접 연다).
- [ ] **Step 5: 커밋** — `[FEATURE] 새 게임의 집에만 처음 배치 가구 — 시험용 책상·의자·책장`.

### Task 11: 보관 목록 다듬기와 남은 작은 것

모델: 중간. 멈춤 없음(스크린샷은 직접 연다).

**Files:**
- Modify: `game/Rootborn.Ui/`의 `UiScrollList`(줄 그림 하나를 선택적으로: `Func<int, string> RowIcon` — 보이는 줄만 묻는다), `game/Rootborn.Screens/Furniture/StoredFurnitureModel.cs`, `FurniturePlacementScreen.cs`(보관 줄 글자를 `RefreshStored`·언어 바뀜 때 한 번 만들어 들고 있다 — 지금은 `StoredRowText`가 그리기마다 `F(…)`), `game/localization/en.json`(집 옮김 알림 영어를 수 꼴에서 벗어나게), `ko.json`(새 키)
- Test: `game/tests/Rootborn.Ui.Tests/`(`UiScrollList` 줄 그림), Screens `StoredFurnitureScreenTests`(더함), Host `HouseMoveHostTests`(사례 하나 더함)

정할 것(결정 18 작성자 추천, 답 대기): 보관 줄 = 가구 대표 그림(`FurnitureArt.TryPrimary` → `FurnitureArt.ImageId`) + 이름 + 방향(`ui.furniture.stored.row` "{0} · {1}" / "{0} · {1}", 방향 이름은 지금 오른쪽 패널의 방향 문구 키) + 물건이 든 상자 표시(지금 키). 영어 알림(결정 31번): `ui.notice.house-moved` "Furniture moved to the new house: {0}. Stored: {1}. Take them out from Stored in Furniture Placement (P).", `.only-moved` "Furniture moved to the new house: {0}.", `.only-stored` "Furniture stored for lack of room: {1}. Take them out from Stored in Furniture Placement (P)." — 수 앞에 명사를 두어 단수·복수가 없다(ko는 그대로).

**Interfaces:** Produces `UiScrollList.RowIcon`(null이면 지금과 같다), 키 `ui.furniture.stored.row`.
- [ ] **Step 1: 실패하는 테스트** — Ui `RowIcon_AskedOnlyForVisibleRows`; Screens `StoredRows_ShowIconAndDirection`, `StoredRowText_BuiltOnceNotPerFrame`(그리기 100번 동안 문구 묻기 횟수가 줄 수와 같다), `LanguageSwitch_RebuildsRows`; Host `HOUSE_MOVE_003_RealisticOldSave_MultiCellDesksOnWalls`(옛 모양 배치 파일에 2칸 책상·책장 다섯이 새 벽에 걸침 → 모두 옮겨지고 "옮김만" 알림, 보관 없음 — 스크린샷: 알림, 집 전체 보기).
- [ ] **Step 2: 실패를 본다.** **Step 3: 구현.**
- [ ] **Step 4: 통과를 본다** — Ui, Screens 전부, Host `--filter "FullyQualifiedName~HouseMoveHost|FullyQualifiedName~TestThemeScreens"`, `run-all.sh --checks-only`. 스크린샷을 직접 연다.
- [ ] **Step 5: 커밋 둘** — `[UI] 보관 목록 줄에 가구 그림과 방향, 줄 글자는 바뀔 때만`, `[TEST] 집 옮김 Host 시험을 실제 옛 세이브처럼 — 벽에 걸친 여러 칸 가구`.

### Task 12: 문서·스킬·끝난 계획 정리

모델: 낮음. 멈춤 없음.

**Files:** Modify `game/README.md`(맵 목록 `build`의 쓰임에 "꾸밈 세이브는 `house-build.json`", 키 표는 그대로), `game/world/README.md` 6절(띠 색은 데이터의 band 꾸밈, `bandColors`는 기본값, 모든 꾸밈이 출처표의 조각을 가져야 한다는 검사), `data/README.md` "집 짓기 `building/`"(slot `band`, 판매 필드, `HouseStarterLayout`, 값은 시험용), `.claude/skills/pixel-kit-room/SKILL.md`와 `.agents/skills/pixel-kit-room/SKILL.md`(바이트 같게: "꾸밈을 더하는 순서" — 정의 한 줄 → `room export-finishes` → 문구 ko·en → 띠 색이면 slot band와 좌표표 `bands` → World `MapContractTests`·Data `RoomFinishDefinitionTests` → 게임 스크린샷), `.claude/rules/changelog.md`(항목 하나: 시나리오 표에 HOUSE-001, SKILL-001·TOWN-001 줄 고침 — 일반화 "지불은 바뀌는 것만 세어 상태로 한 번만, 받음 기록이 필요 없게"), 설계서 12.1절 표(1-6·1-7 끝, 1-8은 `…-stage1c.md`)·12.1.1절(이 계획에서 달라진 것: 방 채우기 규칙 다듬음, 벽지는 정면 주인 칸만, 띠 색은 band 꾸밈, 같이 하기 요청은 칸 목록 대신 (도구, 가리킨 칸))·15절(풀린 것·남은 것), 결정 대기 문서(이 계획이 끝난 사실로 고친다), **끝난 앞 계획 `…-stage1.md`를 지운다**(이 계획 머리말이 "다음 계획이 기대는 것"을 옮겨 받았다) — 그 경로를 가리키던 곳을 이 계획 또는 설계서로 바꾼다(`.superpowers/sdd/…`의 작업 기록은 고치지 않는다).
- [ ] **Step 1:** 쓴다. 새 스킬은 만들지 않는다.
- [ ] **Step 2:** 문서에 적은 명령 중 읽기만 하는 것(`--check`, `--dry-run`)을 실제로 돌린다. `cmp .claude/skills/pixel-kit-room/SKILL.md .agents/skills/pixel-kit-room/SKILL.md`.
- [ ] **Step 3:** 도구 테스트 전부, `run-all.sh --checks-only`, 위 `git grep`이 0건(작업 기록 제외). 커밋 `[DOCS] 집 꾸미기 1단계(나) — 꾸밈 세이브·띠 색·가게·처음 배치, 끝난 계획 정리`.

---

## 다음 계획(1c, 같이 하기)이 기대는 것

`docs/superpowers/plans/2026-11-14-house-building-stage1c.md`(아직 쓰지 않았다)는 아래 이름을 그대로 쓴다. 바꾸면 그 계획을 먼저 고친다. 아래는 **구현이 끝난 지금 코드의 이름**이다(2026-11-14).

- Core 판정은 엔진·파일 없이 방(서버)에서도 돈다: `HouseFinishes`(`Load(HouseBuildSaveData)`·`Attach(HouseBuildFrame, find)`·`Preview(finishId, tool, pointer, unlocks, canPay)`·`PreviewBand(finishId, unlocks, canPay)`·`Apply(preview)`(띠 미리 보기도 이것이 받는다)·`ToSaveData()`·`Version`), 정적 `HouseFinishes.IsUnlocked(finish, unlocks)`(해금 판단 하나), `RoomFill`, `HouseBuildFrame`, `HouseFinishTools`, `HouseFinishReasons`(`SharedHouse`는 1c가 쓰지 않게 된다), `HouseFinishPreview`(바뀌는 칸 `ChangedCells`와 밝힐 칸 `HighlightCells`가 다르다 — 벽지는 정면 벽 칸을 밝힌다), `HouseBuildSaveData`(같은 모양을 방 조각 `SaveFileNames.RoomPiece("house-build")`에도 쓴다).
- **띠 색의 이름**: `HouseFinishes.Band`는 **기록 그대로의 꾸밈 id**(빈 글이면 프레임 기본, 데이터에 없는 id도 그대로)이고, 보이는 띠 색 줄 이름은 `HouseFinishes.BandColors`다(그 꾸밈의 `Art`, 아니면 프레임 기본 `HouseBuildFrame.DefaultBand`). 그림 쪽 조회 `IHouseFinishLookup.BandColors`가 이 줄 이름을 받는다.
- **요청은 칸 목록이 아니라 (꾸밈 id, 도구, 가리킨 칸)**: 방이 같은 `RoomFill`·출처표로 같은 칸을 다시 낸다(결정적). 설계서 8.1절의 "칸 목록 글"과 다르다 — 요청이 작고, 손님이 보낸 칸을 믿지 않는다. 띠 색은 (띠 꾸밈 id).
- 비용 다섯 경우와 "바뀌는 칸 0 → 비용 0"이 상태로 한 번만을 만든다(동시 요청은 뒤 요청이 바뀐 상태로 다시 판정되어 0이 된다). 받음 기록은 따로 두지 않는다.
- `GameSession.PreviewHouseFinish`·`ApplyHouseFinish`·`PreviewHouseBand`·`ApplyHouseBand`의 같이 쓰는 집 갈래(지금 `SharedHouse` 이유로 거절)를 방 요청으로 바꾼다 — 돈 공유 켬은 `Shared(…)`, 끔은 상자의 잡기 → 쓰기/풀기(`RoomChests`의 `Use`·`Release` 틀). **같이 쓰는 집에서는 지금 그림 조회가 프레임 기본이다**(`GameSession.HouseWallAt`·`HouseFloorAt`·`HouseBandColors`가 `_roomHouse != null`이면 `HouseBuildFrame`의 기본 꾸밈을 낸다 — 손님이 자기 세이브 꾸밈으로 주인 집을 그리지 않게). 1c가 방이 알린 꾸밈으로 바꾸면 이 갈래를 지운다.
- 가게 구매(`GameSession.PurchaseFinish`, Core `RoomFinishPurchase.Try`)는 해금만 바꾸고 `FurnitureUnlockProgress`에 든다 — 같이 쓰는 집과 무관하게 개인 진행이다.
- 그림 쪽: 조합기는 `IHouseFinishLookup`(World, `WallAt`·`FloorAt`·`BandColors`)만 안다. Host는 그 어댑터(`HostGame`의 `SessionHouseFinishes`)만 바꾸면 된다(방이 알린 꾸밈), 알림 주제 `GameChangeTopics.HouseBuild`(`"house-build"`).
- 1c가 받을 남은 일: 조각 `house-build`(`house`를 따름 — `RoomPieceCatalog.Follow`), 서버의 짓는 집 프레임(`game/Rootborn.Server/HouseGrids.cs`가 LDtk에서), 데이터 지문(`game/Rootborn.Data/DataFingerprint.cs`)에 `build`가 있는 맵의 LDtk, 같이 쓰는 집의 보관 꺼내기(결정 16, `GameSession.PlaceStoredFurniture`가 지금 false — `MayPlaceStored`가 `_roomHouse == null`을 요구한다), 같이 쓰는 집에서 옮긴 가구 자리를 방 기록에 굳히기(앞 계획 작업 4 걱정), HOUSE-004, Host 두 창(`TwoGamesHostTests`). **처음 배치 가구(`HouseStarterLayout`)는 이미 같이 쓰는 집에 주지 않는다**(`GameSession.WantsHouseStarter`가 `_roomHouse == null`을 요구 — 1c가 할 일이 아니다).
