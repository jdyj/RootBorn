---
name: pixel-kit-room
description: "ROOTBORN 실내 방·집 구조를 칸마다 벽/바닥/밖만 정해 만들면 벽 그림(32x32)과 걸을 수 있는 칸(Collision)이 함께 든 LDtk 방이 나오는 절차. '실내 방 만들기', '집 구조 만들어 줘', '벽', '방 구조', '걸을 수 있는 칸', '벽이라서 이동하면 안 되는 곳', 'LDtk 방', 'Collision 칠하기', 'room build', 'room repaint', 'room check', '집 맵 고치기', '벽 문', 'room vectors', 'room export-finishes' 요청에 사용. 가구·소품·건물 외관 그림은 pixel-kit, 바깥 맵은 이 스킬이 아니다."
---

# 방 구조 만들기 — 칠하면 벽 그림과 충돌이 같이 나온다

도구는 `Tools/pixel-kit/`(명령·검사 코드·파일 형식은 `Tools/pixel-kit/README.md`의 "방 만들기" 세 절), 설계는 `docs/superpowers/specs/2026-11-13-room-builder-design.md`, 게임이 읽는 층·충돌 약속은 `game/world/README.md` 3절. 이 스킬은 순서와 판단만 적는다. 명령 옵션과 형식은 README를 읽는다.

게임의 집(`House`)도 이 도구가 만든 방이다(`art/kit/rooms/house.json` → `art/world/rooms/house/`, 게임 맵 목록 `game/world/maps.json`) — 시험 방(`art/world/rooms/test.two-rooms/`)만이 아니다. 맵 쪽 약속(`build` 값, `Build` 층, 벽 문)은 `game/world/README.md` 6절.

핵심: 칸 하나(1 벽, 2 바닥, 0 밖)가 **그림과 충돌의 하나뿐인 원본**이다. 같은 격자에서 `Wall`·`Ground` 타일과 `Collision`이 함께 나오므로 둘이 어긋나지 않는다.

## 쓰지 않을 때

- 가구·소품·건물 외관·타일 그림: `pixel-kit`. UI 테두리: `ui-nine-slice-skin`.
- 집 맵의 크기·도착 지점·고정 지점을 바꾸는 일: 이 스킬만으로 하지 않는다(아래 "집 맵을 바꿀 때", "하지 않는다").

## 순서

1. **평면을 쓴다**(`art/kit/rooms/<방 id>.json`, 본보기 `test.two-rooms.json`). 글자 줄 `plan`(`#` 벽, `.` 바닥, 빈칸 밖), `wall`(벽 모양), `floor`(바닥 — 키트의 한 칸 타일 명세 id), 선택 `top`(띠 색 줄), `spawn`([x, y] 칸). 지을 수 있는 칸(`build`)을 적으면 `frameId`(테두리의 판 이름, 예 `"house.v2"`)도 적는다 — 없으면 `room-build-no-frame-id`로 거절되고, 방 폴더 `maps.json`의 `build`에 그대로 들어가 게임이 그 파일을 읽는다(게임 맵 목록의 짓는 집과 같은 값). 벽·바닥·띠 색은 아래 "벽·바닥 고르기"대로 사용자가 고른다(본보기 평면은 `paper` + `bakery.tile.floor`, `top` 없음). 방 사이는 벽 줄을 끊은 문간(바닥 칸)으로 잇는다. 벽 칸은 한 칸(32px)이고, 윗면은 그 칸 안에 **16px 띠**로 그려진다(얇은 벽이 기본 — 아래 "얇은 벽"). 가로 벽이 서 보이려면 3칸(윗면 1 + 정면 2)이어야 하고 더 얇으면 정면만 남는다(`room-wall-thin` 알림). 세로 벽은 1칸이면 된다. 문 글자(`doors`)는 써도 되지만 아래 "하지 않는다"의 문 표를 본다. 바깥 벽에 다는 현관 문은 `wallDoors`(`{글자: Where 값}`)로 쓴다: 그 글자 칸은 벽(`#`)으로 남고 32x32 `Door`가 놓이며 그림은 문지방이다(그 문 둘레에 도착 지점에서 닿는 바닥이 있어야 한다 — `room-door-no-floor`). `build`는 평면과 같은 크기의 `0`·`1` 줄이다(1 지을 수 있음, 0 고정 — 밖 칸, 도착 지점, 문 칸, 벽 문과 그 둘레 바닥은 0). 형식·검사 코드는 README "방 만들기 — 평면에서 LDtk로".
2. **`python Tools/pixel-kit/kit.py room check <방 id>`**로 평면부터 검사한다(쓰지 않는다). 문제(열린 가장자리, 닿지 못하는 바닥, 벽 안의 도착 지점)는 평면을 고쳐서 푼다.
3. **`room build <방 id>`**(`--dry-run`으로 쓸 파일 먼저). 결과는 `art/world/rooms/<방 id>/`의 `<방 id>.ldtk`, `tiles.png`, `tiles.slots.json`, `maps.json`. 맵 id는 방 id를 PascalCase로(`test.two-rooms` → `TestTwoRooms`), 도착 지점은 `<맵 id 소문자>-entry`.
4. **게임에서 본다**: `dotnet run --project game/Rootborn.Host -- --world <맵 id> --maps art/world/rooms/<방 id>/maps.json --size 1920x1080`. 벽에 막히는지, 문간을 지나는지 걷고, F12 스크린샷을 **직접 열어** 겹친 그림·잘린 타일·빈 칸을 본다(스킬 `host-play-test`로 장치 이벤트 주입도 된다).
5. **LDtk에서 고친 뒤**: 편집기에서 `Collision` 층만 칠한다(1 벽, 2 바닥). `Collision` 층은 반투명(`displayOpacity` 0.35)으로 만들어 두어 벽·바닥 그림 위에 칠한 칸이 비쳐 보이게 했다. 저장한 뒤(편집기에 파일이 열린 채로 `repaint`하면 편집기가 나중에 저장할 때 옛 벽 그림으로 덮는다 — 편집기에서 저장하고 닫은 뒤, 또는 `repaint` 뒤 편집기에서 다시 불러온 뒤 쓴다) `room check <.ldtk 경로>`(어긋난 칸 수가 나온다) → `room repaint <.ldtk 경로>`(`--dry-run` 먼저)로 벽·바닥 그림을 칠한 칸에 맞춘다. `repaint`는 `Wall`·`Ground` 타일과 시트만 바꾸고 `Collision`·`Door`·`Furniture`와 엔티티는 그대로 둔다. 다시 `room check`, 4번.
6. **가구는 사람이 `Furniture` 층에 놓는다**(태그 `ysort`, 비어 있게 만들어진다). 도구는 가구를 놓지도 지우지도 않는다.

## 벽·바닥 고르기

사용자가 벽 8가지·바닥 9가지 견본을 보고 "오 좋아 이거 말고도 다른것도 계속 만들어줘 유저가 자유롭게 만들 수 있도록"이라고 했다(2026-11-14). 그래서 벽 모양 14가지, 정식 바닥 14가지(세트 `floor`), 띠 색 5가지가 도구에 있다(표는 README "방 만들기 — 벽 모양·띠 색·바닥").

1. **`python Tools/pixel-kit/kit.py room styles --list`**로 종류마다 한글 이름과 컨셉(생김·느낌·어울리는 곳·약점)을 읽고, 사용자의 요청에 맞는 후보를 추린다. 이어 **`room styles`**로 견본판 두 장을 뽑는다(`output/pixel-kit/room/styles-walls_x2.png`, `styles-floors_x2.png` — 이름표에 id·한글 이름·컨셉이 같이 찍힌다. 한글 글꼴을 못 찾으면 id만 찍히고 알림이 나온다: `ROOTBORN_FONT`로 글꼴 파일을 준다). 직접 열어 본 뒤 그 경로(`D:\…` 전체 경로)를 사용자에게 주고 **사용자가 고르게 한다**.
2. 평면에 적는다: `"wall": "<벽 모양>"`, `"floor": "<바닥 명세 id>"`, 띠 색을 바꾸려면 `"top": "<띠 색 줄>"`(적지 않으면 벽 모양의 기본). 모르는 이름은 `room check <방 id>`가 아는 이름 목록과 함께 끝 코드 2로 알린다.
3. 이미 만든 방의 벽·바닥·띠 색을 바꾸려면 시트가 섞이지 않게 그 방 폴더의 `tiles.png`·`tiles.slots.json`을 지우고 `room build --force`(LDtk에서 칠한 것이 있으면 사라진다 — 사용자에게 먼저 묻는다). `--force`만으로는 거절된다.
4. `top`이 없는 옛 자리 표(2026-11-14 전에 만든 방)는 그 벽 모양의 기본 띠 색으로 다시 그려진다 — 거절하지 않고 표에 `top`을 더하지도 않는다.

지금 없는 것(있는 것처럼 말하지 않는다): 키트가 맵 하나에 방마다 다른 벽·바닥을 **굽는** 것(키트는 맵 하나에 벽 한 종류·바닥 한 종류. 게임이 짓는 집을 조합해 그리지만 지금은 맵의 기본 꾸밈 하나만 그린다), 플레이어가 게임 안에서 꾸밈을 고르는 것(규칙·세이브·비용·화면·같이 하기 — 다음 계획), 안쪽 벽 세우기(2단계), 사용자가 직접 그린 PNG를 벽·바닥 타일로 넣는 것(그 뒤 계획).

**새 벽 모양·바닥을 더할 때**(도구 일): 실패 테스트 먼저 → 벽은 `room_tiles.py`에 `_Wall` 클래스 하나 + `@wall_style("이름", label=…, concept=…)`, 바닥은 무늬가 새로우면 `makers/tiles.py`에 `@register("tile.floor-이름")` 하나 + `art/kit/recipes/floor/tiles.json`에 명세 한 덩어리(`label`·`concept` 포함), 띠 색은 `room_tiles.TOPS`에 이름·컨셉과 함께. **`label`(한글 이름)과 `concept`(한 줄)은 반드시 쓴다**(비면 테스트가 실패한다) — 그림을 직접 본 뒤에 있는 그대로 쓰고, 약점이 있으면 한 구절 적는다. 이름(id)은 `모양-특징`꼴로 뜻이 드러나게(`wallpaper-sprig`, `panel-pine`, `floor.stone-slab`), 색은 `style.json`에 뜻이 보이는 새 줄(있는 줄은 바꾸지 않고, 방 재료 줄은 다른 줄과 hex를 나누지 않는다). LimeZu Room Builder 시트는 종류·명도·무늬 밀도만 보고 **픽셀을 베끼지 않는다**. 화풍은 면 음영(넓은 면 + 한 단계 어두운 면 + 돌출부 밑 짙은 줄 하나), 그라데이션·디더링·글자·옆으로 기운 면 없음. `room styles`와 캐릭터·가구를 올린 방 예시를 **직접 열어 보고**(띠와 정면이 만나는 곳, 이음매, 시끄러운 무늬, 벽과 바닥이 구별되는지, 1배 크기) `kit.py lint` 0과 도구 테스트를 통과시킨다. `paper`와 `bakery.tile.floor`의 타일은 바이트까지 그대로여야 한다(이미 칠한 방이 있다).

## 얇은 벽

사용자가 꽉 찬 32px 윗면을 보고 "벽이 너무 두꺼운데 … 얇게"라고 했고, 견본 셋 중 A를 골랐다(2026-11-14 "A로 가보자"). 그래서 지금 도구의 벽은 이렇다(규칙 전부는 README "방 만들기 — 벽 타일", 설계서 4.2절):

- 윗면 칸에는 16px 띠만 그린다. 바깥 벽은 방 쪽에 붙고, 양쪽이 방인 세로 벽은 가운데, 정면 위 가로 윗면은 정면 쪽(아래)에 붙는다. 세로 벽 남쪽 끝 정면도 띠 폭으로 좁다.
- 띠 옆 남는 자리는 벽 타일에서 투명하다: 바닥 쪽이면 그 칸 `Ground` 층에 바닥 타일이 깔려 비치고(벽 칸에도 `Ground` 타일이 있을 수 있다), 맵 밖 쪽이면 비어 있다. 정면 쪽이면 정면이 옆으로 늘어난다.
- **충돌은 칸 단위 그대로다.** 벽 칸은 통째로 막히므로 띠 옆 8px, 가로 띠 위 16px는 바닥처럼 보여도 들어가지 못한다. 그림을 보고 "여기 걸을 수 있겠다"고 판단하지 않는다 — 걷는 칸은 `Collision`이 정한다.
- 띠 두께(`band`)는 벽 모양의 값이고, 띠 색은 벽 모양의 기본을 평면의 `top`으로 바꾼다(위 "벽·바닥 고르기"). 방의 `tiles.slots.json`에 `band`(그리고 적었으면 `top`)가 남는다.
- 얇은 벽 전(윗면 32px)에 만든 방은 자리 표에 `band`가 없다. `repaint`·`room check <.ldtk>`가 거절한다(끝 코드 2) — LDtk에서 칠한 것을 지키려면 그 자리 표에 `"band": 16`을 적고 `repaint`, 평면에서 다시 만들어도 되면 `room build <id> --force`.
- 알려진 약점: 세로 벽이 정면 위로 이어지는 두꺼운 벽 덩어리(예: 3x4)에는 한 단 꺾인 자국이 남는다.

## 집 맵을 바꿀 때

먼저 무엇을 바꾸는지 가른다.

- **크기, 도착 지점, 고정 지점(일과 판, 하루 끝, NPC 자리, 현관) 칸을 바꾼다**: 옛 세이브의 가구 자리와 `game/world/interactions.json` 좌표가 걸린다. 스킬 `save-compat-change`를 먼저 따르고 **사용자에게 묻는다**. 집 단계가 올라 테두리를 넓힐 때는 칸 좌표가 그대로이게 오른쪽·아래로만 키운다(설계서 6.2절).
- **벽·바닥 칸이나 `Build` 값만 바꾼다**: 아래 순서.

1. 평면 `art/kit/rooms/house.json`을 고친다 → `python Tools/pixel-kit/kit.py room check house`.
   `room check house`에 알림 `room-door-unregistered (14, 17)` 하나가 나오는 것은 정상이다(문 목적지는 게임 `game/world/maps.json`의 `House` 항목이 가진다).
2. `room build house --force --dry-run`으로 쓸 파일을 보고 `room build house --force`. **LDtk에서 손으로 고친 것(칠한 `Collision`·`Build`, 놓은 가구, 고친 문)은 지워진다.** 손 고친 것이 있으면 `--force` 대신 `repaint`(위 5번).
3. 평면의 `frameId`를 올렸으면 방 폴더 `maps.json`과 `game/world/maps.json`의 `House` `build`도 같은 값으로 맞춘다(키트 시험 `HouseRoomTests`가 견준다).
4. 테스트와 화면: 도구 테스트 전체, World `HouseMapTests`·`MapContractTests`, Host `HouseMapHostTests`·`HouseTilesHostTests`. 게임 화면 스크린샷을 **직접 열어** 본다(스킬 `host-play-test`).
5. 벽 그림을 고르는 규칙을 고쳐야 하면 아래 "게임 쪽과 나누는 것"으로 간다.

## 게임 쪽과 나누는 것

- **벽 그림 규칙은 두 언어에 있다**(Python `pixelkit/room_tiles.py`, C# `game/Rootborn.Core/src/Building/`). 시험 표 `Tools/pixel-kit/tests/fixtures/room-vectors.json`이 둘을 묶는다. 한쪽만 고치지 않는다: Python을 고친다 → `room vectors`로 시험 표를 다시 만든다(`--check`는 쓰지 않고 견주기만) → C#을 같이 고친다 → 두 쪽 테스트. 자세한 순서는 README "방 만들기 — 집 맵과 꾸밈을 고치는 순서".
- **게임용 조각 시트**: `room export-finishes`(`--dry-run` 먼저)가 꾸밈 정의(`data/building/RoomFinishDefinition.json`)로 `art/world/finishes/`의 시트 PNG와 좌표표를 낸다. 손으로 고치지 않는다. 꾸밈을 더하는 순서는 `data/README.md` "집 짓기 `building/`".
- 게임은 짓는 집의 벽·바닥을 칸마다 출처표대로 조각을 붙여 그린다. 조합이 키트가 구운 그림과 픽셀까지 같다는 것은 자동 테스트(World `HouseMapTests`)가 본다 — 화면에 잘 보인다는 근거는 아니다.

## 하지 않는다

- `Wall`·`Ground` 층을 LDtk에서 손으로 칠하기. 다음 `repaint`가 `Collision`대로 덮는다. 구조는 `Collision`으로만 고친다.
- LDtk에서 손으로 고친 뒤에 `room build --force`. 평면에서 통째로 다시 만들어 다시 칠한 칸·놓은 가구·고친 문을 지운다. 손 고친 뒤의 길은 `repaint`다.
- 집 평면의 크기·도착 지점·고정 지점 칸을 사용자에게 묻지 않고 바꾸기. 옛 세이브의 가구 자리와 `interactions.json` 좌표가 걸린다. 스킬 `save-compat-change`를 먼저 따른다. `room build house --force`는 LDtk에서 칠한 것을 지운다(위 "집 맵을 바꿀 때").
- 벽의 옆으로 기운 면 그리기. 방 사이를 벽에 박힌 문으로 잇기(방 사이는 문간 — 바닥 칸. 벽 문 `wallDoors`는 바깥 벽의 현관이 쓰임이다).
- 벽 그림 규칙을 Python이나 C# 한쪽만 고치기, 시험 표를 손으로 고치기, 조각 시트·좌표표를 손으로 고치기.
- 평면에 문 글자를 쓰고 끝내기. `Door` 엔티티는 놓이지만 만들어진 `maps.json`의 `doors` 표가 비어 있어 손으로 목적지를 채우기 전에는 게임에서 그 문이 열리지 않는다(알림 `room-door-unregistered`).
- LDtk 편집기에서 레벨을 따로 저장(`externalLevels`)하게 하기. `repaint`가 거절한다.
- 벽·바닥 종류나 띠 색을 묻지 않고 정하기. `room styles` 견본판을 보이고 사용자가 고른다. 어느 띠 색을 기본으로 둘지도 사용자 답 대기다.
- 가로 윗면 바로 위 16px 틈(바닥 쪽일 때 바닥처럼 보이지만 못 들어감)을 없애려고 띠 규칙을 바꾸기. 사용자 답 대기다.
- 검사를 끄거나 `--force`로 문제를 덮기. 도구 코드를 이 스킬 안에서 고치기(도구는 별도 일이다).

## 편집기에서 확인된 것

**확인된 것(2026-11-14, 사용자가 LDtk 1.5.3 편집기로)**: 처음에는 LDtk가 `room build`의 파일을 `Cannot read properties of null (reading 'externalLevels') (TypeError)`로 열지 못했다 — 도구가 게임이 읽는 키만 써서다. 공식 스키마(1.5.3, `Tools/pixel-kit/tests/fixtures/ldtk-1.5.3/`)의 필수 키와 편집기 저장 파일에 늘 있는 키를 채운 뒤(`tests/test_ldtk_editor_shape.py`) (가) 도구가 쓴 파일이 **편집기에서 열렸고**, (나) 편집기에서 `Collision`을 칠해 저장한 파일을 **`repaint`가 받아 벽 그림을 맞췄다** — 32px 윗면 때 한 번, 얇은 벽(16px 띠)으로 바뀐 뒤에도 한 번(어긋난 칸 26개 → `repaint` 뒤 문제 없음). (다) `repaint`가 다시 쓴 파일을 편집기가 **다시 열었다** — 32px 윗면 때의 파일로 확인했다. **2026-11-14 사용자가 확인: 얇은 벽으로 `repaint`가 다시 쓴 파일을 편집기가 다시 열고 얇은 띠로 보인다.** 편집기에서 `Collision` 층이 불투명하면 벽·바닥 그림을 가려 색 칸만 보인다 — 그 층의 눈 아이콘을 끄면 그림이 보인다(그래서 반투명으로 바꿨다). 32px 윗면 때 저장한 파일이 테스트 고정 파일 `Tools/pixel-kit/tests/fixtures/room-editor-saved/`다. 그 전에 만든 방 파일은 `room check`가 `room-ldtk-incomplete`로 알리고 `repaint`가 거절한다 — `room build <id> --force`로 다시 만든다.

**2026-11-14 사용자가 더 확인한 것**: 반투명 `Collision` 층을 켠 채로도 색 칸 아래로 벽·바닥 그림이 비친다(눈 아이콘을 끄지 않아도 된다). 이로써 편집기 쪽에서 확인이 필요하던 것은 모두 확인됐다. 방 폴더(예: `art/world/rooms/test.two-rooms/`)에 커밋하지 않은 변경이 있으면 사용자가 편집기에서 칠한 것일 수 있다 — 되돌리거나 `room build --force`로 덮기 전에 사용자에게 묻는다.

**아직 확인하지 못한 것**: `Build` 층과 벽 문이 든 새 집 파일을 LDtk 편집기에서 연 것은 아직 아무도 확인하지 않았다(스키마 검사 테스트만 있다). 위에서 확인된 것은 `Build` 층이 없는 방이다.

벽을 얇게 보이게 하려면 **한 칸 두께**로 칠한다. 두 칸 이상 두께로 칠하면 안쪽이 띠로 채워져 넓은 덩어리로 보인다(사용자가 2칸 폭 벽을 칠했을 때 그렇게 나왔다. 알려진 약점).

## 보고

근거 종류를 나눠 쓴다(`.claude/rules/core-logic-and-play-evidence.md`). 도구 테스트·`room check`는 "자동 테스트", 게임에서 걸은 것은 입력 방식을 붙여 "장치 이벤트 주입", 스크린샷은 직접 연 경로(`D:\…` 전체 경로). 편집기에서 열거나 칠한 것은 사용자만 확인할 수 있다.
