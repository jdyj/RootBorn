# 앉기 메모 — 의자 그림이 준비한 것과 게임에 필요한 것

2026-11-13, 픽셀 키트 2단계 작업 12. 사용자 말: "실제로 앉을수도 있게 할 예정이라 잘 만들어줘".
**설계가 아니라 메모다.** 아무것도 정하지 않았다. 앉기 기능을 설계할 때 출발점으로 쓴다.

## 1. 지금 게임에 들어간 것

- 의자·책상 9종의 그림을 픽셀 키트 그림으로 바꿨다(가구 정의의 `sprite`·`name` 두 값만). 가구 정의가 가리키는 것은 **통짜 그림(기본 칸) 하나**다.

| 가구 id | 그림(PNG / 기본 칸) | 막는가 | 새 슬롯에서 놓을 수 있나 |
|---|---|---|---|
| `chair.black` | `art/props/home/home.chair.png` / `home.chair`(남향) | 아니오 | 예 |
| `chair.quest-reward` | `art/props/home/home.chair-reward.png` / `home.chair-reward`(남향) | 아니오 | 아니오(`unlock.furniture.quest.library-chair`) |
| `school.chair.south` | `art/props/school/school.chair-south.png` / `school.chair-south` | 예 | 예 |
| `school.chair.north` | `art/props/school/school.chair-north.png` / `school.chair-north` | 예 | 예 |
| 책상 5종 | `art/props/{home,school}/*.png`(64x64, 2칸 폭) | 예 | `story.study-desk`만 잠김 |

- 그림이 방향에 따라 바뀌지 않는다(가구는 돌려도 차지 칸만 돈다). 그래서 `chair.black`은 어느 방향으로 놓아도 **남향(우리를 보는) 의자**로 보인다 — 책상 앞 칸에 놓으면 책상을 등지고 앉는 모양이다(스크린샷 `output/pixel-kit/game/chairs/pixel-kit-chairs-02-…`). 학교 의자는 방향마다 다른 가구라 북향이 따로 있다.

## 2. 그림이 준비한 것

### 칸 이름 (좌표표 `data/atlases/art/props/<세트>/<명세 id>.png.atlas.json`)

의자 PNG는 기본 그림과 상태를 왼쪽부터 가로로 붙인 한 장이다(칸마다 32x64).

| 명세 | 칸 |
|---|---|
| `home.chair`, `home.chair-reward` | `<id>`(남향 통짜), `<id>@back`, `<id>@front`, `<id>@north`, `<id>@north-back`, `<id>@north-front`, `<id>@west`, `<id>@west-back`, `<id>@west-front`, `<id>@east`, `<id>@east-back`, `<id>@east-front` |
| `school.chair-south`, `school.chair-north` | `<id>`(그 방향 통짜), `<id>@back`, `<id>@front` |

- `back` = 앉은 사람 **뒤에** 그릴 조각, `front` = 앉은 사람 **앞에** 그릴 조각. `back` → 앉은 사람 → `front` 순서로 같은 자리에 그리면 된다. 사람 없이 겹치면 통짜와 바이트가 같다(도구 테스트가 본다).
- 남향 `front` = 좌판 앞 테두리 + 앞다리. 북향 `front` = 보이는 것 전부(등받이가 앉은 사람의 등·허리를 가린다; 북향 `back`은 다리 두 토막뿐). 옆모습 `front` = 우리 쪽 옆 테두리 + 가까운 다리.
- 좌판 밑 다리 줄은 두 층이 함께 갖는다(그래서 층마다 혼자서도 바닥에 선 그림).
- 이번에 게임에 넣지 않은 것: 층 칸과 방향 칸. 칸은 PNG·좌표표에 이미 있으니 게임이 칸 이름만 고르면 쓸 수 있다.

### 치수 (그림 픽셀 = 게임 픽셀, 칸 32px. 바닥 = 캔버스 아랫변 = 점유 칸 아랫변)

| 값 | 남향·옆 | 북향 |
|---|---|---|
| 의자 상자(`_layout.size`, 방향 공통) | 폭 20, 높이 27(외곽선 포함 28줄) | 같음 |
| 좌판 윗면 | 바닥에서 14px | 14px |
| 등받이 꼭대기 | 바닥에서 28px | **25px** — 북향 그림은 위 3줄이 빈다(`back_top` 3). 높이는 `floor - back_top`로 읽는다 |
| `sit_point`(엉덩이 점 = 좌판 윗면 사각형 가운데) | 좌판 가운데, 바닥에서 약 12px | 같음 |
| 옆모습 좌판 | 폭 17(서: 왼쪽, 동: 오른쪽 3px 비움) | — |

- 책상(남향, 64x64): 윗면 앞 모서리 높이(`work_height`) 바닥에서 25px = 좌판 + 11px. 무릎 자리(`kneehole`, 투명)는 서랍 반대쪽 32px 폭(서랍 없으면 50px, 양쪽 서랍이면 가운데 20px), 높이 17px.
- 캐릭터 프레임 32x59(시트에 따라 53), 발 = 몸 상자(1칸) 아랫변 가운데. 서 있는 캐릭터에게 좌판은 무릎, 등받이 꼭대기는 허리 조금 위다(스크린샷에서 확인 — 아래 4절).
- 처음 계획의 등받이 34~38px는 작업 11에서 그림을 보고 28px(북향 25px)로 낮췄다(36px는 선 캐릭터 턱까지 오고 앉은 머리를 뒤에서 가렸다).

## 3. 게임에 필요한 작업 (정하지 않음 — 할 일 목록)

1. **어느 가구가 의자인지 알려 주는 데이터 표.** 선례는 상자: `data/interiors/FurnitureStorageDefinition.json`(가구 id → 칸 수) + Core `FurnitureStorageDefinition` + Host가 놓인 상자마다 상호작용 후보 `"storage"`를 둔다(`WorldScreen`). 같은 모양으로 "앉는 가구" 표(가구 id, 앉는 점, 앉은 방향 또는 "가구 방향을 따름", 층 칸 이름)와 상호작용 종류(예: `"sit"`)를 더하는 길이 있다. 엔티티 id 분기 없이 표로.
2. **방향에 따라 그림 칸 고르기.** 지금 가구 그림은 방향과 무관하다. 고르는 길: (가) 가구 정의에 방향별 칸 이름(예: `tileParts`를 방향마다), (나) 좌표표 칸 이름 규칙(`<기본>@<방향>`)을 Host가 따른다, (다) 지금처럼 방향마다 다른 가구 정의(학교 의자 방식). (가)·(나)는 세이브의 방향 값(이미 저장됨)을 그대로 쓴다.
3. **앉은 자세.** 캐릭터 시트에 앉은 프레임이 없다. (가) 앉은 프레임을 그린다(방향 4개 × 몸·머리·옷 층 전부), (나) 서 있는 프레임을 좌판 높이만큼 올리고 다리를 `front` 조각이나 잘라 내기로 가린다. 작업 11의 흉내(`output/pixel-kit/task11/c2_sit_raised_x4.png`, `c2_sit_hips_x4.png`)에서 (나)는 다리가 서 있는 그대로라 의자 위에 선 것처럼 보였다.
4. **층을 캐릭터 앞에 그리기.** 지금 규칙: 놓인 가구는 그림 조각(`tileParts`)마다 그 칸 가운데 Y로, 캐릭터는 몸 상자 가운데 Y로 정렬하고 **같으면 캐릭터가 앞**이다(`WorldScreen.ActorIndex`: `SortY <= 캐릭터` 이면 먼저 그림). 의자 칸에 선 캐릭터는 의자와 정렬 값이 같아 의자 전체 위에 그려진다(스크린샷 `pixel-kit-chairs-03`·`04`: 좌판이 캐릭터에 가린다). 그래서 **조각 둘(back·front)을 같은 칸에 두는 것만으로는 front가 앞에 오지 않는다.** 앉은 동안 front를 캐릭터 다음에 그리는 규칙(조각의 정렬 덧값, 또는 앉은 캐릭터의 정렬 값을 의자 back과 front 사이에 두기)이 필요하다 — Host 그리기 한 곳의 변경.
5. **막는 의자(학교 의자 둘, `blocksMovement` 1).** 지금은 들어갈 수 없다. 앉기를 상호작용(앞에서 누르면 앉는다)으로 하면 막는 채로도 되고, 걸어 들어가는 방식이면 막지 않게 바꿔야 한다. 막는지는 기획(가구 규칙) — 사용자가 정한다.
6. **집 맵에 그려진 의자·책상·컴퓨터**(`Assets/LDtk/…house.ldtk`의 `Furniture` 층, `Assets/Art/HousePixels/Comfy31/House31_*_1x1.png`)는 놓인 가구가 아니라 맵 엔티티라 이번에 안 바뀌었다. **이 그림들은 맵을 고쳐야 바뀐다**(맵은 새로 그릴 예정이라 고치지 않았다).

## 4. 정해야 할 것 (사용자)

- 앉기를 어떻게 시작하나: 의자 칸에 걸어 들어가기 / 앞에서 상호작용 키 / 둘 다.
- 앉으면 무엇이 되나: 그림만인가, 활동(공부·일)과 이어지나, 쉬면 회복되나.
- 앉은 자세를 새로 그릴지(시트 전부) 아니면 가리는 방식으로 갈지.
- 학교 의자를 막는 채로 둘지.
- 방향에 따라 그림을 고르는 방식(위 3-2의 가·나·다).
- `chair.black`을 책상 앞에 놓으면 지금은 책상을 등진 모양이다 — 방향 그림이 들어올 때까지 이대로 둘지.

## 5. 근거 (작업 12)

- 장치 이벤트 주입, OS 입력 계층 미확인: Host `PixelKitFurnitureHostTests.PIXEL_KIT_002` — 책상을 놓고 그 앞 칸에 의자, 막지 않는 의자 칸에 걸어 들어가 섬(캐릭터 칸 = 의자 칸 단언), 책상 뒤, 혼자 놓은 학교 의자(남향)의 앞 칸·뒤 칸.
- 직접 본 화면: `output/pixel-kit/game/chairs/`(새 그림), `chairs-before/`(옛 그림), 확대 `output/pixel-kit/game/crop-chairs-*.png`.
- 잠긴 두 가구(`story.study-desk`, `chair.quest-reward`)는 새 슬롯에서 놓을 수 없어 게임 화면에서는 안 봤다(PNG만 직접 봄: `output/pixel-kit/game/locked-two-x5.png`).
