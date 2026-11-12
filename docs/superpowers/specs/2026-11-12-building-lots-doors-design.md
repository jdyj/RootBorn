# 건물 부지와 열리는 문 — 설계 (2026-11-12)

사용자 결정(2026-11-12): 건물에 들어갈 때 문이 열렸으면 좋겠다. 게임을 진행하며 건물을 재건축해 커질 수 있고 그에 맞춰 맵도 조금 바뀐다. 코드는 그것을 감안해야 한다. 건물이 좌우 대칭일 필요는 없다.

## 1. 지금

- 건물 겉모습은 LDtk 맵에 타일로 칠해져 있다. 단계마다 갈아 끼울 수 없다.
- 문에 들어서면 `doorOpenSeconds` 뒤에 넘어가고, 그동안 문 위에 넓어지는 사각형만 그린다(`presentation.json` `door`). 열린 문 그림은 없다.
- 세이브에 집 단계(`GameSession.HouseStage`)와 시설 레벨(`AdultCareerProgress.FacilityLevel`)은 있지만, 단계에 따라 맵이 달라지는 곳은 없다.

## 2. 하려는 것

- 건물을 맵에서 떼어 **데이터**로 둔다: 단계마다 그림, 차지하는 칸, 막는 칸, 문 칸, 문 프레임.
- 맵에는 **부지**만 둔다: 어느 건물, 문이 놓일 칸(기준점), 예약한 칸 범위, 단계를 어디서 읽는지, 문이 이어지는 곳.
- **기준점은 문이다.** 건물은 문 칸이 부지의 문 칸에 오도록 놓인다. 건물이 커져도 문 앞 길과 도착 지점은 그대로다.
- 문이 열리는 동안 문 프레임을 차례로 그린다. 프레임이 없는 건물은 지금의 사각형 연출을 쓴다.
- 지금 LDtk 맵의 건물은 건드리지 않는다. 지금 `maps.json`에는 부지를 더하지 않는다(새 맵부터 쓴다). 기능은 시험용 고정 파일로 확인한다.

하지 않는 것: 실내 맵을 단계마다 바꾸기(부지의 `to`가 단계마다 다를 수 있게만 한다), 재건축 규칙(비용·조건 — 기획), 건물 그림 만들기, 조립식(층 단위) 건물.

## 3. 데이터

### 3.1 `game/world/buildings.json` — 건물 겉모습

```jsonc
{
  "buildings": [
    {
      "id": "bakery",
      "levels": [                                  // 배열 순서가 단계 1, 2, 3 …
        {
          "texture": "art/buildings/bakery-1.png", // 저장소 뿌리 기준
          "src": [0, 0, 160, 192],                 // 그림 안의 사각형(PNG 왼쪽 위 기준 픽셀). 너비·높이는 칸 크기의 배수
          "solid": [0, 2, 5, 4],                   // 막는 칸 [x, y, 너비, 높이] — 그림 왼쪽 위 칸 기준. 지붕 위쪽은 걸어 지나갈 수 없으니 보통 전부
          "door": {
            "cell": [2, 5],                        // 문 칸 — 그림 왼쪽 위 칸 기준. 이 칸이 부지의 doorCell에 온다
            "frames": [ [160, 0, 32, 64], [192, 0, 32, 64], [224, 0, 32, 64] ],   // 닫힘 → 열림 순. 같은 texture 안의 사각형. 없어도 된다
            "at": [64, 128]                        // 프레임을 그릴 자리 — 그림 왼쪽 위 기준 픽셀
          }
        }
      ]
    }
  ]
}
```

### 3.2 `maps.json` 맵의 `lots` — 부지

```jsonc
"lots": [
  {
    "name": "bakery-lot",
    "building": "bakery",
    "doorCell": [12, 20],                // 맵 칸. 건물의 문 칸이 여기 온다
    "cells": [9, 14, 7, 7],              // 예약한 칸 [x, y, 너비, 높이]. 어느 단계의 건물도 이 안에 들어가야 한다
    "level": { "kind": "fixed", "value": 1 },          // 단계를 어디서 읽는지. kind는 종류 문자열(아래 5절)
    "to": { "map": "BakeryInterior", "spawn": "bakery-entry" },   // 문이 이어지는 곳. 없으면 열리지 않는 건물(배경)
    "openSeconds": 0.6                   // 문에 들어선 뒤 넘어갈 때까지. 없으면 맵의 doorOpenSeconds, 그것도 없으면 Core 기본값
  }
]
```

- 좌표는 모두 그림 기준 칸(줄 밀기 `entityRowShift`와 무관). 새 맵은 `entityRowShift`를 적지 않는다.
- 부지 밖으로 나가는 건물, 맵 밖 부지, 서로 겹치는 부지, 문 칸이 `solid` 밖인 건물, 칸 크기의 배수가 아닌 `src`, 없는 건물 id, 없는 단계는 **문제로 알린다**(맵 검사가 잡는다). 조용히 넘기지 않는다.
- 단계 값이 건물의 단계 수보다 크면 가장 높은 단계를, 1보다 작으면 1을 쓴다(세이브가 데이터보다 앞서 있어도 게임이 멈추지 않게).

## 4. 판정은 Core에

`Rootborn.Core.World`에 그림을 모르는 판정만 둔다(`game/Rootborn.Core/src/World/BuildingLots.cs`).

- `BuildingFootprint`: 칸 크기(너비·높이), 막는 칸 사각형, 문 칸. 생성자가 값을 확인한다.
- `BuildingLot`: 이름, 예약한 칸 사각형, 문 칸.
- `BuildingPlacement.Place(lot, footprint)` → 건물 왼쪽 위 칸, 막는 칸 목록, 문 칸. 부지를 벗어나면 문제 문자열을 낸다(예외가 아니라 결과에 담는다 — 맵 검사가 모아 보여 준다).
- 문 상자: 문 칸 한 칸 그대로(`AreaRect`). 문 칸은 막지 않는다(걸어 들어가야 열린다). 문 칸 바로 아래 칸이 문 앞이다.
- `DoorWatch`/`WorldSession`에 열림 진행도(0~1)를 더한다: `WorldSession.OpeningProgress`. 문 프레임 고르기가 이 값을 쓴다(지금 `WorldTransition`이 따로 세는 시간과 같은 값이어야 한다 — 한 곳에서 센다).

## 5. 단계는 종류로 읽는다

- `IBuildingLevels`(World): `int LevelOf(string kind, string key)`.
- 종류는 문자열 + 클래스 하나 + 등록 한 줄이다. enum·switch로 닫지 않는다. 엔티티 id로 분기하지 않는다.
  - `fixed`: `value`를 그대로.
  - `house-stage`: 집 단계(`GameSession.HouseStage` + 1).
  - `facility-level`: 성인 시설 레벨. `key`가 시설 id이고, 지금 맡은 시설이 그 id가 아니면 1.
- `fixed`는 World에, 나머지 둘은 `GameSession`을 아는 층(Host 또는 Screens — 참조 방향에 맞는 곳)에 둔다.

## 6. 맵에 얹기 (World)

- `BuildingCatalog`(`buildings.json` 읽기)와 `MapEntry.Lots`(`maps.json` 읽기).
- `WorldMapLoader`가 맵을 만들 때 부지를 얹는다: 막는 칸을 충돌에 더하고, 문을 `DoorArea`로 더하고, 건물 그림을 `MapPicture.Sorted`에 더한다(발 위치 = 건물 아래 끝. 캐릭터가 건물 뒤로 가면 가려진다).
- 문 프레임은 `LoadedMap`에 `BuildingDoorArt`(어느 `DoorArea`, 프레임 `SpriteDraw` 목록) 목록으로 둔다. 진행도 → 프레임 번호는 순수 함수 하나(`BuildingDoorArt.FrameAt(progress)`).
- 맵은 기억해 두되, **부지의 단계가 바뀌었으면 다시 만든다**: `LoadMap`이 지금 단계들의 서명(부지 순서대로 단계 값)을 기억한 것과 비교한다. 부지가 없는 맵은 지금과 똑같이 한 번만 만든다.
- 서 있는 맵의 건물이 바뀌는 경우: `WorldSession.Reload()` — 지금 맵을 다시 읽고, 몸이 막힌 칸에 있으면 가장 가까운 열린 칸으로 옮긴다. 문 지켜보기는 새 문으로 다시 시작한다.
- `RoomAwareMaps`(가구 방)와 같이 써도 깨지지 않아야 한다(가구 방이 `LoadMap`의 결과를 기억한다 — 부지가 있는 가구 방은 지금 없으므로, 그런 맵이 오면 문제로 알리는 정도로 둔다).

## 7. 그리기 (Host)

- 건물 그림은 `MapPicture.Sorted`에 들어 있으므로 따로 그릴 것이 없다. 텍스처 읽기 목록(`MapPicture.Textures`)에 건물 그림을 더한다.
- 문 프레임: 열리는 문이 건물 문이고 프레임이 있으면 그 프레임을 건물 그림 바로 위에 그리고, 사각형 덮개는 그리지 않는다. 열리지 않을 때는 첫 프레임(닫힘)을 그리지 않는다 — 건물 그림에 닫힌 문이 이미 있다.
- 매 프레임 새 객체를 만들지 않는다.

## 8. 검사와 테스트

- Core: 놓기(문 칸 기준, 부지 밖 문제, 막는 칸에서 문 칸 빠짐), 진행도.
- World: 고정 파일(`tests/Rootborn.World.Tests/Fixtures`)의 작은 LDtk 맵 + 건물 2단계로 — 단계 1과 2에서 문 자리가 같고 막는 칸만 달라짐, 문으로 걸어 들어가면 목적지에 도착, 단계가 바뀌면 다시 만들어짐, `Reload` 뒤 몸이 열린 칸에 있음, 잘못된 데이터가 문제로 나옴.
- `MapContractTests`: 실제 `maps.json`의 부지(지금은 없음)가 건물 목록과 맞는지.
- Host: 고정 맵에서 문으로 걸어 들어가는 동안 문 프레임 번호가 0 → 마지막으로 올라가는지(장치 이벤트 주입). 그림의 픽셀에는 기대지 않는다.
- 문서: `game/world/README.md`에 부지와 건물 데이터의 약속을 적는다.
