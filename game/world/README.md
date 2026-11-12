# 맵 약속 — LDtk로 맵을 그리는 사람이 지킬 것

이 문서는 MonoGame 판(`game/`)이 맵에서 **실제로 읽는 것**만 적는다. 코드는 특정 맵의 이름, 크기, 좌표를 모른다.
맵을 바꾸는 일은 LDtk 파일, 타일셋 PNG, 이 폴더의 `maps.json`·`interactions.json`·`buildings.json`을 바꾸는 일이다.

읽는 코드: `game/Rootborn.World`(LDtk 읽기 `Ldtk/LdtkFile.cs`, 맵 만들기 `WorldMapLoader.cs`, 맵 목록 `WorldCatalog.cs`,
그리기 순서 `MapPicture.cs`, 상호작용 `WorldInteractionData.cs`, 검사 `MapChecks.cs`, 건물 목록 `BuildingCatalog.cs`, 단계 종류 `BuildingLevels.cs`),
규칙은 Core `game/Rootborn.Core/src/World`(건물 놓기 `BuildingLots.cs`).

## 1. 파일 두는 곳

- LDtk 프로젝트(`.ldtk`)와 따로 저장한 레벨(`.ldtkl`)은 저장소 안 아무 곳에나 둔다. 지금 맵은 `Assets/LDtk/` 아래에 있다.
- 타일셋 PNG는 LDtk 프로젝트에서 상대 경로(`relPath`)로 가리킨다. **그 PNG가 저장소 안에 있어야 그려진다.** 없으면 그 층은 그리지 않고
  문제로 알린다(검사 테스트가 잡는다). PNG는 그대로 읽는다(변환 단계 없음).
- 맵 하나를 더하려면 `maps.json`의 `maps`에 한 덩어리를 더한다. 맵 크기는 적지 않는다(레벨에서 읽는다).

```jsonc
{
  "id": "Library",                       // 맵 id. 세이브의 현재 장면 이름과 같다. data/locations 장면 목록에도 있어야 이어 하기가 된다
  "ldtk": "Assets/LDtk/Town/library.ldtk", // 저장소 뿌리 기준 경로
  "level": "Library_1",                  // LDtk 레벨 이름
  "viewCells": 13,                       // 화면에 보일 높이(칸)
  "collision": { "rule": "interior-auto" },
  "doors": {                             // 문 엔티티의 값 → 목적지 맵과 도착 지점
    "town": { "map": "Town", "spawn": "town-library-entry" }
  },
  "spawns": {                            // 도착 지점(레벨 픽셀). 처음 적은 것이 이 맵의 시작 도착 지점
    "library-entry": [ 304, 176 ]
  }
}
```

있으면 읽는 값(없으면 기본값):

| 값 | 뜻 | 없을 때 |
|---|---|---|
| `doorEntity` | 문 엔티티 이름 | `Door` |
| `doorField` | 문 엔티티에서 목적지 값을 읽는 칸 | `Where` |
| `spawnField` | 문 엔티티에서 도착 지점을 따로 정하는 칸(값이 있으면 `doors` 표의 도착 지점 대신 쓴다) | `Position` |
| `doorOpenSeconds` | 문에 들어선 뒤 넘어갈 때까지(초) | 0.35 |
| `areas` | LDtk에 없는 이동 구역 목록(3절) | 없음 |
| `dayNight` | 시각에 따라 화면 색을 바꾸는가(바깥 맵) | `false` |
| `entityRowShift` | 문 엔티티와 칠한 Collision 층을 그림보다 몇 줄 아래로 읽을지 | 0 |
| `cameraBounds` | 카메라가 비출 맵 안의 부분 `[x, y, 너비, 높이]`(레벨 픽셀, 그림 기준, 줄 밀기 없음). 카메라는 이 사각형 밖을 비추지 않는다(`CameraFrame.cs`). 지금은 이 값을 쓰는 맵이 없다 | 맵 전체 |

`entityRowShift`는 Unity 판에서 가져온 지금 맵의 어긋남(문이 그림보다 한 줄 아래)을 재현하는 값이다. **새 맵에는 적지 않는다.**

## 2. 칸, 좌표, 층

- **좌표**: 모든 좌표는 LDtk 레벨 픽셀이다. 왼쪽 위가 (0, 0)이고 아래로 갈수록 y가 커진다(LDtk와 같다).
  칸 (c, r)의 가운데 픽셀은 ((c + 0.5) × 칸, (r + 0.5) × 칸).
- **칸 크기**: `Collision` IntGrid 층이 있으면 그 격자 크기, 없으면 타일이 가장 많은 타일 층의 격자 크기다.
  레벨 픽셀 크기는 칸 크기로 나누어떨어져야 한다(아니면 문제로 알린다). 칸 크기가 다른 타일 층은 충돌 판정에서 빠진다.
- **걷는 몸**: 1칸 크기 상자, 초당 4칸(`maps.json`의 `walker`). 칸 단위라 칸 크기가 다른 맵에서도 같다.
- **카메라·문 연출**: 카메라는 캐릭터를 부드럽게 따라가고(맵 경계 안), 자동문(문 엔티티)에 들어서면 문 위에 넓어지는 사각형,
  이동 구역(`maps.json` `areas`)에 들어서면 화면 덮개를 보인다. 수치는 `presentation.json`(맵마다 다르지 않다). 문 사각형은 문 상자 가운데 기준이라
  새 문 그림에서도 문 상자를 문 그림 가운데에 두면 맞는다.
- **그리기 순서** (`MapPicture.cs`):
  - 보이는(`visible`) 타일 층은 모두 캐릭터 **아래**에 LDtk 순서대로 깐다.
  - LDtk 층 정의에 태그(`uiFilterTags`) `ysort`를 단 층의 그림만 캐릭터와 발 위치로 앞뒤를 가린다(아래쪽에 선 것이 앞). 가구가 이런 층이다.
  - 엔티티는 태그 `art` 또는 `ysort`가 붙은 층의 것만 그린다. 문 같은 표시용 엔티티는 그리지 않는다.
  - 엔티티 그림은 엔티티의 타일(`__tile`)에서 오고, 발 위치는 엔티티의 기준점(pivot) y다.

## 3. 충돌(걸을 수 있는 칸)

`collision.rule`로 규칙을 고른다. 규칙 뒤에 칠한 `Collision` 층이 언제나 덮는다.

### `interior-auto` — 층 이름으로 자동 판정(실내에 쓴다)

- 이름에 **`Ground`**가 들어간 층은 바닥이다.
- 이름에 **`Wall`** 또는 **`Border`**가 들어간 층은 벽이다(막음).
- 이름이 **`Prop`**으로 시작하는 층은 가구다(막음). 단, 문 바로 둘레 8칸의 바닥은 가구가 있어도 연다(문 앞을 가구가 막지 않게).
- 타일이 있는 범위에 한 칸 테두리를 더한 안에서, 벽·가구가 없는 바닥과 문 칸만 열리고 나머지는 막힌다. 맵 밖은 언제나 막힌다.
- 이름 규칙은 맵마다 바꿀 수 있다: `"collision": { "rule": "interior-auto", "groundKeys": [...], "solidKeys": [...], "propPrefixes": [...] }`.
  지금 실내 맵은 Unity 판 층 이름(Police·Kitchen·Shop·Shooting·School·House1·House2)을 `propPrefixes`로 적어 둔다.
  이름에 `Ground`가 들어가기만 하면 바닥이라, `ForeGround` 같은 이름도 바닥으로 친다.

### `collider-layers` — 정해 둔 층의 타일이 칸 전체를 막음(바깥 맵에 쓴다)

`"collision": { "rule": "collider-layers", "layers": [ "Building", "Fence" ] }`. 그 층에 타일이 있는 칸은 막히고, 나머지는 열린다.

### 칠해서 덮기 — `Collision` IntGrid 층

레벨에 이름이 `Collision`인 IntGrid 층을 두고 **1은 막음, 2는 열림**으로 칠한다. 0이나 다른 값은 규칙의 결과를 둔다.
격자 크기가 맵 칸과 다르면 쓰지 않고 문제로 알린다. 규칙만으로 안 맞는 칸은 이 층으로 고친다(`openCells`보다 이쪽을 쓴다).
`collision.openCells: [[x, y], ...]`는 지금 학교 맵을 Unity 판 충돌에 맞추려고 둔 값이다.

## 4. 문과 이동

- **문 엔티티**: 이름 `Door`(또는 `doorEntity`). 필드 `Where`(또는 `doorField`)에 목적지 값을 적는다. 값은 앞뒤 공백을 지우고 소문자로,
  `-`는 공백으로 바꿔 `doors` 표와 비교한다(`Shopping-Center` = `shopping center`). **표에 없는 값의 문은 열리지 않는다**(검사가 잡는다).
- 필드 `Position`(또는 `spawnField`)에 값이 있으면 그 도착 지점으로 간다. 비어 있으면 표의 도착 지점.
- **문 상자**: 엔티티의 사각형. 가운데는 그대로 두고 너비 0.75칸, 높이 1칸보다 작으면 넓힌다.
  몸이 문 상자에 새로 겹치면 열리기 시작해 `doorOpenSeconds` 뒤에 넘어간다. 도착했을 때 이미 겹친 문은 한 번 나갔다 들어와야 열린다.
- **문을 그림에 맞게 놓기**: 문 엔티티를 문 그림 타일 위에 **그 칸 그대로** 놓는다. 새 맵은 `entityRowShift`를 적지 않으므로 엔티티 자리가 곧 게임의 문 자리다.
  문 상자는 걸어서 들어갈 수 있는 칸에 걸쳐야 한다(벽 안에만 있으면 닿지 않는다 — `interior-auto`는 문 칸을 자동으로 연다).
- **맵 가장자리 이동 구역**: LDtk 엔티티 없이 `areas`에 적는다.
  `{ "name": "beach", "rect": [x, y, 너비, 높이], "to": { "map": "Beach", "spawn": "beach-top-entry" }, "openSeconds": 0.25 }`.
  `rect`는 레벨 픽셀이고 맵 밖으로 나가도 된다(맵 밖으로 나간 칸은 걸을 수 있게 열린다). 줄 밀기를 받지 않는다.
- **도착 지점**: `spawns`의 이름 → 레벨 픽셀 [x, y](몸 가운데). 걸을 수 있는 칸 위에 둔다. 실내는 출구 상자에서 0.9칸 넘게 떨어뜨리고
  그 자리에서 모든 출구까지 걸어갈 수 있어야 한다(Unity 판 저장 검사와 같다). 도착 지점 이름은 맵 사이에서 겹치지 않게 짓는다(`<맵>-<어디>-entry`).
  다른 맵의 문이 이 이름을 가리키므로 이름을 바꾸면 그 문들의 `doors`·`Position`도 바꾼다.

## 4-1. 건물 부지(`lots`)와 건물 목록(`buildings.json`)

건물은 LDtk에 칠하지 않고 데이터로 둔다. 게임을 진행하며 건물이 커질 수 있어서다(단계마다 그림·차지하는 칸이 다르다). 맵에는 **부지**만 둔다.
**지금 맵에는 부지가 없다**(지금 맵의 건물은 LDtk에 칠해져 있다). 새 맵부터 쓴다. 설계: `docs/superpowers/specs/2026-11-12-building-lots-doors-design.md`.

- **기준점은 문이다.** 건물은 그 단계의 문 칸이 부지의 `doorCell`에 오도록 놓인다. 건물이 커져도 문 자리, 문 앞 길, 도착 지점은 그대로다.
  건물이 좌우 대칭일 필요는 없다(문 칸이 건물 가운데가 아니어도 된다).
- 부지(`maps.json` 맵의 `lots`):

```jsonc
"lots": [
  {
    "name": "bakery-lot",                       // 부지 이름(문 이름이 된다 — 도착 기록에 남는다)
    "building": "bakery",                       // buildings.json의 건물 id
    "doorCell": [12, 20],                       // 맵 칸. 건물의 문 칸이 여기 온다
    "cells": [9, 14, 7, 7],                     // 예약한 칸 [x, y, 너비, 높이]. 어느 단계의 건물도 이 안에 들어가야 한다
    "level": { "kind": "fixed", "value": 1 },   // 단계를 어디서 읽는지(아래 표)
    "to": { "map": "BakeryInterior", "spawn": "bakery-entry" },  // 문이 이어지는 곳. 없으면 열리지 않는 배경 건물(문 칸도 막는다)
    "openSeconds": 0.6                          // 없으면 맵의 doorOpenSeconds, 그것도 없으면 0.35초
  }
]
```

- 건물(`buildings.json`): `id`와 `levels`(배열 순서가 단계 1, 2, 3 …). 단계마다 `texture`(저장소 뿌리 기준 PNG), `src`(그림 안 사각형, 너비·높이는 칸 크기의 배수),
  `solid`(막는 칸, 그림 왼쪽 위 칸 기준), `door.cell`(문 칸, `solid` 안), `door.frames`(문 프레임 닫힘 → 열림, 없어도 된다), `door.at`(프레임을 그릴 자리,
  그림 왼쪽 위 기준 픽셀). 형식은 파일 머리 주석에 있다.
- 좌표는 모두 그림 기준 칸(`entityRowShift`와 무관). 그림 좌표(`src`, `frames`, `at`)는 PNG를 직접 재서 정한다.
- 문 상자는 문 칸 한 칸 그대로다. 문 칸은 막지 않는다(걸어 들어가야 열린다). 문 칸 바로 아래 칸이 문 앞이다 — 그 칸이 걸을 수 있어야 한다.
- 건물 그림은 캐릭터와 발 위치로 앞뒤를 가린다(발 위치 = 건물 아래 끝). 캐릭터가 건물 뒤(위쪽)로 가면 가려진다. `solid`에 넣지 않은 지붕 위쪽 줄은 지나갈 수 있다.
- 문이 열리는 동안 문 프레임을 차례로 그린다(진행도를 프레임 수로 나눈다). 프레임이 없으면 `presentation.json` `door`의 사각형을 그린다.
  닫혀 있을 때는 프레임을 그리지 않는다 — 건물 그림에 닫힌 문을 그려 둔다.
- 단계 값이 건물의 단계 수보다 크면 가장 높은 단계를, 1보다 작으면 1을 쓴다. 단계가 바뀌면 맵을 다시 만들고, 그 맵에 서 있으면 다시 읽는다
  (몸이 새로 막힌 칸에 있으면 가장 가까운 열린 칸으로 옮긴다).
- 단계 종류(`level.kind`). 새 종류는 클래스 하나와 등록 한 줄이다:

| kind | 단계 | 어디 |
|---|---|---|
| `fixed` | `value` 그대로 | World `FixedLevel` |
| `house-stage` | 집 단계 + 1(게임 한 판이 없으면 1) | Host `HostBuildingLevels` |
| `facility-level` | 성인 시설 레벨. `key`가 시설 id이고, 지금 맡은 시설이 그 id가 아니면 1 | Host `HostBuildingLevels` |

- 검사(문제로 알린다, 조용히 넘기지 않는다): 없는 건물 id, 모르는 단계 종류, 단계가 없는 건물, 맵 밖 부지, 예약한 칸 밖의 `doorCell`, 서로 겹치는 부지,
  부지 밖으로 나가는 건물, 칸 크기의 배수가 아닌 `src`, `solid` 밖의 문 칸, 저장소에 없는 그림, 가구 방(`placementSurface`)에 둔 부지(지원하지 않는다).
  부지의 문도 4절의 문 검사(목적지, 걸어서 닿음)를 받는다.
- 하지 않는 것: 단계마다 실내 맵 바꾸기, 재건축 규칙(비용·조건 — 기획), 층 단위 조립식 건물.

## 5. 상호작용 지점

LDtk 엔티티가 아니라 **`interactions.json`**에 맵 id별로 적는다(LDtk에는 상호작용 엔티티를 두지 않는다).

```jsonc
"Library": {
  "npcAnchors": { "location.library": [ [ 144, 80 ] ] },   // 장소 id → NPC가 설 자리 목록(레벨 픽셀)
  "things": [
    { "kind": "location-activity", "id": "location.library", "at": [ 80, 208 ], "priority": 45 }
  ]
}
```

- `at`은 물건의 가운데 레벨 픽셀이다. 플레이어가 `radiusCells`(1.75칸) 안에 들어오면 대상이 되고, 여럿이면 `priority`가 큰 것.
  지점은 걸을 수 있는 칸이거나 그 옆 칸에 둔다(검사가 잡는다). 지점 자체는 길을 막지 않는다.
- NPC는 일정(data/npcs)이 정한 장소의 자리에 선다. 같은 장소에 NPC가 여럿이면 data/registry.json의 NPC 순서대로 1번째, 2번째 자리.
  자리가 모자라면 그 NPC는 그 맵에 서지 않는다.
- `kind`는 그대로 이벤트로 나가는 문자열이고, 화면 등록 표(`Rootborn.Screens/ScreenCatalog.cs`)가 종류로 화면을 연다. 지금 종류:

| kind | 무엇 | 우선순위(Unity 판) | 지금 여는 화면(2026-11-11 메인 체인 Host 시나리오로 확인) |
|---|---|---|---|
| `npc` | NPC(자동, `npcAnchors`에서) | 30 | 대화 |
| `daily-plan-board` | 하루 계획판 | 80 | 일과 |
| `student-day-end` | 하루 마감 | 80 | 하루 결과(다음 날) |
| `location-visit` | 장소 방문 | 35 | 없음(같은 자리의 `location-activity`가 우선순위로 먼저 대상이 된다) |
| `location-activity` | 장소 활동 | 45 | 장소(열 때 방문 기록), 닫으면 그 장소의 하루 이벤트 |
| `part-time-work` | 알바 자리(`id`는 data의 알바 id) | 45 | 미니게임(알바를 예약하면 열린다) |
| `career-practice` | 진로 연습 자리(`id`는 data의 진로 연습 id) | 45(Unity 판에 놓인 곳이 없어 알바와 같게) | 미니게임(연습을 예약하면 열린다) |
| `career-facility` | 성인 시설(`id`는 data의 시설 id) | 55 | 성인 시설 관리 |
| `exploration` | 탐색 지점(`id`는 data의 탐색 id) | 50 | 탐색 선택 |
| `discovery` | 발견 지점(`id`는 data의 발견 id) | 15 | 발견 알림(알림 층, 이동을 막지 않음) |
| `door` | 자동문(자동, 목적지 있는 문) | 110 | 없음 — 걸어 들어가야 열린다 |

  새 종류는 화면 쪽에 등록하면 되고, 이 파일에는 문자열만 적는다. 장소 기록은 지점이 아니라 L 키와 HUD 버튼으로 연다.
- 대상 고르기는 가까운 것이 먼저다(Core `InteractionTargetRule`: 거리 차가 0.5칸 안일 때만 우선순위로 가른다).
  자리가 다른 지점끼리는 상호작용 범위(`radiusCells`)보다 멀리 두면 한 지점 칸에 섰을 때 다른 지점이 범위 밖이라 서로 가리지 않는다
  (임시 지점은 `TemporaryPointsTests`가 확인한다). 같은 자리에 겹쳐 둔 장소 방문 + 장소 활동은 우선순위로 고르도록 일부러 둔 것이다.
- **맵 성질**: 화면은 맵 id가 아니라 맵 성질을 본다. 지금 성질은 하나 — `placementSurface`가 있는 맵은 "가구를 놓는 면이 있다"
  (`MapFeatures.FurniturePlacement`)이고, Host가 맵에 들어갈 때 `ScreenStack.EnterMap`으로 넘긴다(가구 배치 HUD 단추가 이 성질을 본다).
- 검사(`game/tests/Rootborn.World.Tests/WorldEntryPointsTests.cs`): 모든 지점이 새 게임 자리에서 걸어서 닿고, 그 자리에 서면 대상으로 골라지고
  (같은 자리의 더 높은 우선순위 지점에 가려진 것은 빼고), `id`가 data/의 정의다(`daily-plan-board`·`student-day-end`의 `id`는 지점 이름일 뿐이다).
  data/의 탐색·성인 시설·진로 연습 정의는 하나도 빠짐없이 지점을 가진다. 화면을 여는 종류마다 지점이 있는지는
  `game/tests/Rootborn.Screens.Tests/WorldEntryPointKindsTests.cs`가 화면 등록 표로 본다.

## 6. 집

- 집 맵의 크기는 자유다(지금 28×18칸). 코드는 집 크기를 맵 파일에서 읽는다(`HouseSizeTests`가 크기가 다른 집으로 확인한다).
- 가구를 놓을 수 있는 칸: 지금 정한 방식(`game/tests/Rootborn.Game.Tests/HouseRoom.cs`, Host 연결은 아직 전)은
  **맵 안의 모든 칸이 바닥, 막힌 칸은 고정 칸, 도착 지점이 있는 칸은 들어오는 칸(문 앞)**이다. 그래서 벽·고정 가구는 충돌로 막아 두면 된다.
  가구 그림은 `ysort` 태그 층의 엔티티로 그린다.
- 집 단계: 세이브의 집 상태(`HouseStage`, 0부터)와 단계마다 따로 저장하는 가구 배치가 있다. 단계마다 다른 집 맵으로 바꾸는 표는 아직 없다
  (지금 `maps.json`의 집은 하나). 새 집 그림이 단계별로 오면 그때 맵 목록에 단계 → 맵을 더한다.

### 일터 면

시설(성인 진로)의 일터는 집과 같은 방식의 가구 방이다. 면 id만 다르다.

- `interactions.json`에서 맵의 `placementSurface`를 **일터의 면 id**(data/workspaces `WorkspaceDefinition`의 `surfaceId`, 예: `workspace.chef`)로 두면
  그 맵이 그 일터다. Host는 맵 id도 일터 id도 모르고 이 면 id만 `GameSession.EnterPlacementSurface(방 칸, 면 id)`에 넘긴다.
  그 면의 일터가 데이터에 있으면 일터로, 없으면 집으로 열린다. 방 칸 만드는 법(바닥, 고정 칸, 들어오는 칸)은 집과 같다.
- 일터 면 하나에 맵 하나를 둔다. 일터마다 면을 가진 맵이 있어야 한다.
- 시설을 맡은 뒤 처음 들어오면 그 일터의 기본 템플릿이 깔린다. **템플릿 칸(`data/workspaces/WorkspaceTemplate.json`의 `cell`)은 가구 칸 좌표**
  (x는 맵 칸과 같고 y는 위로 커진다: 가구 칸 y = 맵 줄 수 − 1 − 맵 칸 y)이고, 그 맵의 막히지 않은 칸에 놓여 들어오는 칸에서 걸어 닿는 길을
  막지 않아야 한다. 맵을 새로 그리면 템플릿 칸도 그 맵에 맞춘다. `WorkspaceTemplateMapTests.EveryWorkspaceTemplate_FitsItsMap`이 확인한다.
- 시설의 일 지점(`career-facility`)은 일터 맵이 아니어도 된다(지금은 마을 임시 지점). 일의 보상은 저장된 일터 배치로 낸다.
- 지금 요리사 일터는 주방 실내(`KitchenInterior`)를 임시로 쓴다(8절).

## 7. 메인 체인이 요구하는 곳

새 게임 시작 자리(data/locations 장면 목록의 새 게임 자리, 지금 `House/house-entry`)에서 **문을 지나 걸어서** 아래에 모두 닿고,
닿은 모든 곳에서 집으로 걸어서 돌아와야 한다. `MainChainWalkTests`가 확인한다(맵 이름이 아니라 종류와 id로 찾는다).

| 단계(Unity E2E) | 지점 |
|---|---|
| 001 엄마와 이야기 | NPC 자리 `location.home` |
| 001 하루 계획 | `daily-plan-board` |
| 001 학교 선생님 | NPC 자리 `location.school` |
| 002·005 하루 마감 | `student-day-end` |
| 003 일터 둘러보기 | `location.restaurant`, `location.medical-clinic`, `location.design-studio`, `location.training-ground` (`location-visit`·`location-activity`) |
| 004 가게 알바 | `part-time-work` / `work.shift.corner-store` |
| 006 도서관 | `location.library` |
| 008~009 가구 사기 | `location.corner-store` |

## 8. 임시 연결 목록

새 맵이 오기 전까지 메인 체인을 걸어서 확인하려고 데이터에만 둔 연결이다. 항목마다 `"temporary": true`와 `"reason"`이 있다
(이유가 없으면 읽기를 거절한다). 게임은 다른 값과 똑같이 쓰고, 화면에서 구별하지 않는다. **맵을 새로 그리면 지운다.**
자리가 있는 항목은 `이름` (x, y)로 적는다. 이 목록과 데이터가 어긋나면 `TemporaryPointsTests.TheReadmeTemporaryList_MatchesTheTemporaryMarksInTheData`가 실패한다.
마을 임시 지점은 북쪽 광장(칸 32~52열, 0~11행)에 두 줄 이상 간격으로 둔다(칸 (c, r)의 가운데 = ((c+0.5)×32, (r+0.5)×32)).

### 새 맵을 그릴 사람을 위한 한 장 요약 (2026-11-12 데이터 기준)

임시 표시는 모두 36개다: `interactions.json` 상호작용 지점 31개와 일터 면 표시 1개, `lights.json` 빛 지점 1개, `maps.json` 연결 3개(문 1, 도착 지점 1, 이동 구역 1).
일터 면 표시 말고는 전부 **LDtk 마을 그림에 Unity 판의 북쪽 구역(학교 입구, 가게·식당 등 장소, 성인 시설, 알바 자리)이 없어서** 생겼다. 새 맵에 아래 자리를 그리면
그 항목의 `temporary`·`reason`을 지우고 자리를 새 맵 좌표로 옮긴다(목록 표의 같은 줄도 지운다 — 어긋나면 테스트가 알려 준다).

| 새 맵에 있어야 할 것 | 맵 | 지금 임시로 둔 것 | 개수 |
|---|---|---|---|
| 마을 ↔ 학교 출입구(마을 쪽 문과 학교 앞 도착 지점, 학교 쪽 출구) | Town, School | 문 `north`, 도착 지점 `town-school-entry`, School 아래 가장자리 이동 구역 `town` | 3 |
| 장소 6곳의 입구(식당, 의원, 디자인 스튜디오, 훈련장, 도서관, 가게) — 같은 자리에 장소 활동·장소 방문 둘 | Town | `location-activity`·`location-visit` 쌍 | 12 |
| 성인 시설 4곳의 문(요리, 응급, 인테리어, 군인) | Town | `career-facility` | 4 |
| 진로 연습 4곳(Unity 판에도 자리가 없었다 — 어디에 둘지는 맵 그리는 사람이 정한다) | Town | `career-practice` | 4 |
| 알바 자리 9곳(가게 알바 + 도우미 8곳) | Town | `part-time-work` | 9 |
| 탐색 1곳, 발견 1곳(골목 상자, 시계탑 안내문) | Town | `exploration`, `discovery` | 2 |
| 밤에 학교 앞을 비추는 가로등 | Town | 빛 지점 `town-night-guide` | 1 |
| 요리사 일터(식당 안) — 새 맵을 그리면 면 id를 그 맵에 옮기고 템플릿 칸을 맞춘다(6절 "일터 면") | KitchenInterior | 일터 면 표시 `workspace.chef` | 1 |

| 파일 | 맵 | 항목 | 이유 |
|---|---|---|---|
| maps.json | Town | 문 `north` → School/school-entry | LDtk 마을 그림 밖 북쪽 구역(학교 입구가 있던 곳)이 LDtk에 없다 |
| maps.json | Town | 도착 지점 `town-school-entry` (1168, 144) | 학교 앞 도착 지점이 북쪽 구역에 있었다. 문 `north` 아래 북쪽 광장 |
| maps.json | School | 이동 구역 `town`(아래 가장자리 한 줄) → Town/town-school-entry | School_1에 문 엔티티가 없고 학교 출구도 LDtk에 없다 |
| interactions.json | Town | 장소 6곳, `location-activity`·`location-visit`를 같은 자리에 하나씩(12개): `location.restaurant` (1072, 176), `location.medical-clinic` (1200, 176), `location.design-studio` (1328, 176), `location.training-ground` (1456, 176), `location.library` (1072, 304), `location.corner-store` (1264, 304) | 이 지점들이 북쪽 구역에 있었다 |
| interactions.json | Town | 성인 시설 4곳 `career-facility`: `facility.chef` (1296, 112), `facility.emergency-care` (1392, 112), `facility.interior` (1488, 112), `facility.soldier` (1584, 112) | Unity 판 성인 시설(Town.unity `Facility_*`)이 북쪽 구역에 있었다 |
| interactions.json | Town | 탐색 1곳 `exploration`: `exploration.test.alley-crate` (1584, 176) | Unity 판 탐색 지점(`TownExplorationInteractionPoint`)이 북쪽 구역에 있었다 |
| interactions.json | Town | 진로 연습 4곳 `career-practice`: `practice.chef` (1168, 368), `practice.emergency-care` (1360, 368), `practice.interior` (1552, 368), `practice.soldier` (1648, 368) | 작성자 판단: Unity 판에는 진로 연습 지점이 어느 씬에도 없었다. 일터 지점이 있는 북쪽 광장에 둔다 |
| interactions.json | Town | 알바 9곳 `part-time-work` — 가게 알바 `work.shift.corner-store` (1456, 304)와 알바 8곳(7행 2칸 간격): `work.workshop-helper` (1104, 240), `work.restaurant-helper` (1168, 240), `work.studio-helper` (1232, 240), `work.supply-helper` (1296, 240), `work.training-helper` (1360, 240), `work.clinic-helper` (1424, 240), `work.fitness-helper` (1488, 240), `work.errand-helper` (1552, 240) | Unity 판 알바 자리(Town.unity `PartTimeWorkInteractor`)가 북쪽 구역에 있었다 |
| interactions.json | Town | 발견 1곳 `discovery`: `discovery.clocktower-notice` (1360, 304) — 9행 도서관과 가게 알바 사이 | Unity 판 발견 지점(`TownDiscoveryPoint`)이 북쪽 구역에 있었다 |
| interactions.json | KitchenInterior | 일터 면 표시 `workspace.chef`(맵 전체, 자리 없음) | 새 맵 전까지 요리사 일터로 쓴다. 주방 둘 중 마을 문에서 바로 들어가고 빈 바닥이 더 넓은 쪽 |
| lights.json | Town | 빛 지점 `town-night-guide` (1072, 176) | Unity 판 유일한 빛은 씬 원점(마을 모서리)에 남아 거의 보이지 않았다. Unity 판 가로등 그림 자리(학교 앞 도착 지점에서 (-3, -1)유닛)를 임시 학교 앞 도착 지점에 맞춰 옮겼다 |

## 9. 새 맵을 넣은 뒤 돌려 볼 것

```
dotnet test game/tests/Rootborn.World.Tests --filter "MapContractTests|MainChainWalkTests"
dotnet run --project game/Rootborn.Host -- --world <맵 id 또는 LDtk 레벨 이름> --size 1920x1080
```

- `MapContractTests`(새 맵 검사, `MapChecks`): `maps.json`의 모든 맵에 대해 문 값이 `doors` 표에 있다, 문의 목적지 맵과 도착 지점이 있다,
  도착 지점이 걸을 수 있는 칸이다, 모든 문이 그 맵의 시작 도착 지점에서 걸어서 닿는다, 상호작용 지점·NPC 자리가 걸어서 닿는 칸 옆에 있다,
  쓰는 타일셋 PNG가 있다, 부지와 건물 데이터가 맞다(4-1절). 지금 맵에서 걸리는 것은 테스트 옆 `KnownMapProblems`에 (맵 id, LDtk 파일, 레벨)로 묶여 있어,
  LDtk 파일이나 레벨을 바꾼 새 맵에는 적용되지 않는다.
- `MainChainWalkTests`: 7절.
- `--world`로 띄우면 맵 목록의 첫 도착 지점에 선다(`--spawn <이름>`으로 바꾼다). 화면에서 F12로 찍어 겹침·빈 그림·어긋난 문을 본다.
- 이 테스트들은 자동 테스트다(칸 단위 경로). 실제로 걸어 본 것은 Host의 장치 입력 테스트나 직접 본 화면으로만 말한다.

## 10. 빛(`lights.json`)

- `ambient`: 때(`time.morning`·`time.afternoon`·`time.evening`·`time.night`)마다 바깥 밝기. 맵 목록에서 `dayNight`가 켜진 맵에만 입힌다.
- `maps`: 맵 id → 빛 지점 목록. `at`은 레벨 픽셀(왼쪽 위 원점), `radius`는 빛이 닿는 끝, `innerRadius`까지 최대 밝기, `intensityByTime`은 때별 세기.
  읽기는 `Rootborn.World` `WorldLighting.cs`, 그리기는 Host `WorldScreen`(화면 안에 들어온 빛만).
- 빛 지점은 화면에 보이는 자리에 둔다: 빛이 닿는 원 전체가 맵 안이고 가운데가 걸어서 닿는 칸 옆이어야 한다
  (`WorldAmbienceTests.Lights_EveryLightCircleIsInsideItsMap_NextToWalkableCells`). 새 맵에서는 가로등 그림 위에 둔다.
