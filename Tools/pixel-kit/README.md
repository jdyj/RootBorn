# 픽셀 키트

부품과 규칙으로 32px 칸의 픽셀 그림(건물 외관, 가구, 타일, 소품)을 그리는 도구다. 그림은 손으로 칠하지 않고
**데이터**(팔레트, 점 부품, 조립 명세)로 적고, 렌더러가 외곽선·그늘·그림자를 같은 규칙으로 입힌다.
설계는 `docs/superpowers/specs/2026-11-13-pixel-kit-design.md`(화풍은 6절).

Python 3.11 + Pillow. 다른 패키지는 쓰지 않는다.

## 명령

저장소 루트에서 돌린다. 결과는 `output/pixel-kit/`(git 무시, 커밋하지 않는다)에 쓴다.

| 명령 | 하는 일 | 결과 |
|---|---|---|
| `python Tools/pixel-kit/kit.py lint [id…]` | 검사. id가 없으면 장면이 아닌 명세 전부(모든 상태 포함)와 점 부품 전부 | 문제를 `id: 코드 (x, y) 설명`으로 찍는다 |
| `python Tools/pixel-kit/kit.py render <id[@상태]…>` | 명세를 그리고 검사한다. 상태를 안 적으면 기본 그림과 모든 상태 | `render/<id>.png`(1:1), `render/<id>_x6.png` |
| `python Tools/pixel-kit/kit.py sheet <세트>` | 세트(명세 id의 첫 마디, 예: `bakery`)의 모든 명세를 번호와 함께 한 장에. 검사에 걸린 것은 표시된다 | `sheet/<세트>.png` |
| `python Tools/pixel-kit/kit.py scene <장면 id…>` | 방·거리 미리 보기 | `scene/<id>.png`, `scene/<id>_x4.png` |
| `python Tools/pixel-kit/kit.py parts` | 점 부품 목록 판(명세를 쓸 때 다시 쓸 부품을 고른다) | `parts.png` |
| `python Tools/pixel-kit/kit.py export <가구 id…> [--dry-run] [--repo 뿌리]` | 가구 그림을 게임이 읽는 PNG + 좌표표로 내고, `game.replaces`가 있으면 가구 정의의 그림 참조를 바꾼다(아래 "내보내기") | 저장소 뿌리의 `art/props/`, `data/atlases/` |
| `python Tools/pixel-kit/kit.py export <타일·건물·소품 id…> [--dry-run] [--repo 뿌리]` | 타일은 `art/tiles/<세트>.png`, 건물·바깥 소품은 `art/world/<세트>.png` 묶음 시트에 덧붙이고 자리 표·좌표표를 쓴다(아래 "묶음 시트") | 저장소 뿌리의 `art/tiles/`, `art/world/`, `data/atlases/` |
| `python Tools/pixel-kit/kit.py test-level [--out 폴더] [--repo 뿌리]` | 지금 시트들로 시험용 맵을 쓴다(아래 "시험용 맵") | `<뿌리>/art/world/test/pixel-kit-test.ldtk`, `maps.json` |
| `python Tools/pixel-kit/kit.py room build <방 id> [--force] [--dry-run] [--out 폴더] [--repo 뿌리]` | 평면 `art/kit/rooms/<id>.json`에서 게임이 읽는 LDtk 방·타일 시트·맵 목록을 쓴다(아래 "방 만들기 — 평면에서 LDtk로") | `<뿌리>/art/world/rooms/<id>/` |
| `python Tools/pixel-kit/kit.py room check <방 id>` | 평면만 검사한다(쓰지 않는다) | — |
| `python Tools/pixel-kit/kit.py room repaint <.ldtk 경로> [--dry-run]` | LDtk에서 다시 칠한 `Collision`에 맞춰 `Wall`·`Ground` 타일과 시트만 다시 쓴다(아래 "방 만들기 — LDtk에서 칠한 뒤") | 그 `.ldtk`가 든 폴더 |
| `python Tools/pixel-kit/kit.py room check <.ldtk 경로>` | LDtk 방을 검사한다(쓰지 않는다): 그림과 `Collision`이 어긋난 칸, 평면 검사, 표에 없는 문 | — |
| `python Tools/pixel-kit/kit.py room styles [--out 폴더] [--scale N]` | 등록된 벽 모양 전부(윗면 띠 + 정면 2칸 + 바닥 한 줄 + 문간 + 방 안에서 끝나는 세로 벽), 띠 색 전부(`paper` 상자 방), 방 바닥으로 받는 명세 전부(4x3칸)를 이름표(id, 한글 이름, 컨셉 한 줄)와 함께 견본판 두 장으로. 한글 이름·컨셉은 한글 글꼴이 있을 때만 그려진다: 환경 변수 `ROOTBORN_FONT`의 파일이 먼저이고(값을 줬는데 못 열면 알림 한 줄 뒤 넘어간다), 없으면 시스템 글꼴 이름 후보(`malgun.ttf` 등)에서 찾는다(보기용 도구라 글꼴 이름 후보를 코드에 둔 예외). 못 찾으면 id만 그리고 알림 한 줄(실패하지 않는다). 사용자가 이것을 보고 고른다 | `<--out, 기본 output/pixel-kit/room>/styles-walls.png`, `styles-floors.png`(1:1)와 `_x2`(기본 배율 2) |
| `python Tools/pixel-kit/kit.py room styles --list` | 그리지 않고 벽 모양·띠 색·바닥을 종류마다 `이름(id) — 한글 이름 — 컨셉`으로 글만 찍는다(벽은 기본 띠 색도). 고르기 전에 컨셉을 읽는 데 쓴다 | — |
| `python Tools/pixel-kit/kit.py room vectors [--check]` | 게임(C#)과 나누는 시험 표 `Tools/pixel-kit/tests/fixtures/room-vectors.json`을 지금 규칙으로 다시 쓴다. `--check`면 쓰지 않고 다르면 끝 코드 1(아래 "방 만들기 — 시험 표") | `Tools/pixel-kit/tests/fixtures/room-vectors.json` |
| `python Tools/pixel-kit/kit.py room export-finishes [--dry-run]` | 꾸밈 정의(`data/building/RoomFinishDefinition.json`)의 벽지·바닥을 게임용 조각 시트 PNG와 좌표표로 낸다(아래 "방 만들기 — 꾸밈 조각 시트"). 같은 입력은 같은 바이트, 문제가 있으면 아무것도 쓰지 않는다 | `art/world/finishes/finishes.png`, `data/atlases/art/world/finishes/finishes.png.atlas.json` |

공통 인자: `--kit 폴더`(기본 `art/kit`), `--out 폴더`(기본 `output/pixel-kit`), `--scale N`(보기용 확대 배율,
정수·최근접. 기본 render 6, sheet·scene 4, parts 8, room styles 2).

끝 코드: 0 깨끗, 1 검사 문제, 2 사용·데이터 오류(없는 id, 못 읽는 키트, 저장 거절).
`render`와 `lint`는 문제가 있으면 1이다. `scene`과 `sheet`는 문제를 찍거나 빨간 테두리를 그려도 항상 0이다(사용·데이터 오류와 저장 거절만 2) — 끝 코드로 거르려면 `lint`를 쓴다.

## 파일이 있는 곳

| 무엇 | 어디 |
|---|---|
| 화풍(팔레트, 외곽선 색, 그림자 세기, 색 수 한도, 칸 크기) | `art/kit/style.json` |
| 점 부품(빵, 소품) | `art/kit/parts/*.json` |
| 조립 명세(세트마다 폴더: 빵집, 학교, 부엌, 집) | `art/kit/recipes/<세트>/*.json` |
| 만드는 부품(코드) | `Tools/pixel-kit/pixelkit/makers/{tiles,furniture,building,seating}.py` |
| 장면·그림자 | `Tools/pixel-kit/pixelkit/{scene,shadow}.py` |
| 검사 | `Tools/pixel-kit/pixelkit/lint.py` |
| 방 벽·바닥 타일(둘레 칸으로 고르기) | `Tools/pixel-kit/pixelkit/room_tiles.py` |
| 방 평면 읽기·검사 / LDtk 방 쓰기 | `Tools/pixel-kit/pixelkit/room_plan.py` / `room_ldtk.py`(공용 도우미 `ldtk_common.py`) |
| 방 평면 파일 | `art/kit/rooms/<id>.json` (게임의 집은 `house.json`) |
| 키트가 만든 방 | `art/world/rooms/<id>/` (게임의 집은 `house/`, 시험 방은 `test.two-rooms/`) |
| 픽셀 출처표 / 시험 표 / 꾸밈 조각 시트 | `pixelkit/room_tiles.py`의 `plan_sources` / `tests/fixtures/room-vectors.json`(`room_vectors.py`) / `art/world/finishes/`(`room_export.py`) |
| 방 바닥 명세(세트 `floor`) | `art/kit/recipes/floor/tiles.json` |
| 테스트 | `Tools/pixel-kit/tests/` |

## 점 부품 더하기

`art/kit/parts/` 아래 아무 `*.json`(부품 배열)에 한 덩어리를 더한다.

```json
{ "id": "bread.bagel", "anchor": "bottom-center",
  "rows": [
    "..LLLL..",
    ".LHC.CL.",
    ".CL..LC.",
    "..CCCC.."
  ],
  "keys": { "H": "bread.3", "L": "bread.2", "C": "bread.1" } }
```

- 글자 하나 = 팔레트 키 하나(`재질.단계`), `.`은 투명. 외곽선은 그리지 않는다(읽을 때 바깥 1px을 입힌다).
- 부품 하나의 색은 10개까지. 일부러 둔 외톨이 점(손잡이, 씨앗)만 `"allow": ["orphan-pixel"]`로 푼다.
- `python Tools/pixel-kit/kit.py parts`로 열어 보고, `lint`가 0인지 본다.

## 만드는 부품 더하기

모양을 규칙으로 그려야 하는 것(크기가 바뀌는 진열대, 지붕, 타일)은 코드다. 새 종류는 **함수 하나 + 등록 한 줄**이고
if/switch로 종류를 닫지 않는다.

1. 실패하는 테스트를 먼저 `Tools/pixel-kit/tests/`에 쓴다(크기, 쓰는 색 줄, 검사 통과, 잘못된 명세는 `RecipeError`).
2. 알맞은 모듈(`makers/tiles.py`, `furniture.py`, `building.py`)에 함수를 쓰고 `@register("종류.이름")`을 붙인다.
   새 모듈이면 `makers/__init__.py` 끝에 `from . import 모듈` 한 줄.
3. 함수는 `(spec, ctx) -> Canvas`. 외곽선 전 그림을 크기 자유로 돌려준다(렌더러가 외곽선을 입히고 칸 배수 캔버스의
   아래 가운데에 붙인다). 색은 `ctx.style.color("재질.단계")`로만 얻고 명세의 값(줄 이름, 크기)을 읽는다.
   잘못된 값은 `RecipeError(f"{spec['id']}: …")`. 바꿀 수 있는 재질은 `common.ramp_of(spec, ctx, 키, 기본 줄, 필요한 단계 수)`로
   명세 키에서 읽고, 크기를 칸 수에서 얻으면 `common.art_cells_in(spec, 폭 범위, 높이 범위)`로 범위를 확인한다.
4. 칸 안에 물건을 늘어놓을 때는 `common.pile()`을 쓴다(겹침·잘림 문제를 알린다).
   지붕 종류는 `@roof(...)`, 지붕 위 물건은 `@roof_item(...)` 표에 등록한다(`building.py`).

## 조립 명세 더하기

`art/kit/recipes/<세트>/*.json`(명세 배열)에 한 덩어리.

```json
{"id": "bakery.shelf", "kind": "furniture", "part": "furniture.shelf", "cells": [2, 1], "art_cells": [2, 3],
 "tiers": [["bread.loaf", "bread.loaf"], ["bread.baguette"]],
 "states": {"empty": {"tiers": [[], []]}}}
```

- `kind`: `building` · `furniture` · `prop` · `tile` · `scene`. 색 수 한도와 검사 종류가 이것으로 정해진다.
- `part`: 만드는 부품 이름. 나머지 키는 그 부품이 읽는 값이다(아래 표).
- 팔레트 키는 `art/kit/style.json`의 `palette`에 있는 줄 이름 + `.단계`(예: `wood.warm.3`)다. 단계 0이 가장 어둡고 번호가 클수록 밝다. 없는 단계가 필요하면 그 줄에 어두운 것부터 순서대로 더한다.
- `cells`(차지 칸)와 `site`(건물 터)는 기획 값이고 그림에는 쓰이지 않는다.
- 크기는 `art_cells: [w, h]`(칸) 또는 `pixels: [w, h]`(32의 배수) 중 하나. 그림은 그 캔버스의 아래 가운데.
- `seed`(정수, 기본 0): 명세 하나의 변주. 흩어지는 자리(벽돌 낱장, 얼룩, 풀잎, 잔 점)가 바뀌고, 0이면 지금 그림과 같다. 같은 seed는 같은 바이트. 정수가 아니면 `RecipeError`.
- `states`: 이름 → 덮어쓸 값. 같은 가구의 다른 모습(빈 진열대, 다른 빵)은 새 명세가 아니라 상태로 둔다.
  `render id@상태`, 장면에서는 `"state": "이름"`.

## 만드는 부품이 읽는 키

코드(`pixelkit/makers/`)와 `art/kit/recipes/bakery/`에서 뽑았다. 크기(`art_cells` 또는 `pixels`)는 모든 명세에 있다.
"줄"은 팔레트 줄 이름(접두사), "부품 id"는 `parts` 판에 있는 점 부품이다. 줄 키의 값은 줄 이름 하나(예: `"material": "wood.warm"`)이고,
그 줄의 단계가 괄호 안 수보다 적으면 `RecipeError`(명세 id, 키, 줄, 필요한 단계 수). 줄 키를 적지 않으면 기본값이고 기본값이면 1단계 그림과 바이트까지 같다.
진열대·계산대는 그림 폭이 `art_cells` 폭 × 32 - 2이고(`pixels`로는 적지 않는다), 범위 밖 `art_cells`는 `RecipeError`(받는 범위를 적는다). 유리 진열대·수레는 폭이 부품에 고정이고 명세 크기는 캔버스일 뿐이다.

| 부품 | 필수 | 선택 |
|---|---|---|
| `tile.floor-checker` | `colors`: 줄 2개(밝은 칸, 어두운 칸. 줄마다 단계 0~3) | |
| `tile.wall-interior` | `paper`(벽지 줄, 단계 0~2), `cap`(벽 윗면 줄, 0~1), `wood`(징두리·몰딩 줄, 0~4) | |
| `tile.grass` | `colors`: 줄 1개(단계 0~3) | |
| `tile.paver` | `colors`: 줄 1개(단계 0~2) | |
| `tile.floor-plank` | `colors`: 줄 1개(0 이음 / 1 결 / 2 바탕 / 3 빛 모서리) | `board`(판 폭 `8` 기본 · `16` — 16은 끝 이음 없는 긴 판), `vertical`(`false` 기본 · `true`: 가로 판을 돌린 세로 판) |
| `tile.floor-herringbone` | `colors`: 줄 1개(0 이음 / 2 세로 판 / 3 가로 판, 판 16x8) | |
| `tile.floor-basket` | `colors`: 줄 1개(0 이음 / 2 세로 판 / 3 가로 판, 16px 칸마다 판 둘) | |
| `tile.floor-slab` | `colors`: 줄 1개(0 줄눈 / 1 결 / 2 바탕 / 3 빛, 32px 판 하나) | |
| `tile.floor-concrete` | `colors`: 줄 1개(0 줄눈 / 1 잔 점 / 2 바탕 / 3 얼룩) | |
| `tile.floor-brick` | `colors`: 줄 1개(1 줄눈 / 2 벽돌 / 3 밝은 벽돌 몇 장, 16x8 엇갈림) | `seed`(밝은 벽돌 자리) |
| `tile.floor-mosaic` | `colors`: 줄 1개(1 줄눈 / 2 짙은 알 / 3 밝은 알, 알 7px) | |
| `tile.floor-linoleum` | `colors`: 줄 1개(1 잔 점 / 2 바탕) | `seed`(잔 점 자리) |
| `tile.floor-carpet` | `colors`: 줄 1개(1 점 / 2 바탕) | |
| `tile.floor-carpet-diamond` | `colors`: 줄 1개(1 마름모 선·점 / 2 바탕, 주기 16) | |
| `tile.floor-tatami` | `colors`: 줄 2개(몸: 2 바탕 / 3 결, 테: 1이 아래·오른쪽 테) | |

방 바닥 부품(`tile.floor-*`)의 줄 단계 0은 대개 벽 밑 그림자 자리다 — 방의 `floor:shade`가 색마다 그 줄의 가장 어두운 단계로 옮기므로,
바탕보다 어두운 단계 0이 있어야 정면 밑 그림자가 보인다.
| `furniture.shelf` | `art_cells` `[1~3, 2~3]`(높이 3칸은 키 큰 진열대, 2칸은 낮은 진열대), `tiers`: 층마다 부품 id 목록(안쪽 높이를 층 수로 나눈다. 3칸 높이 1~4층, 2칸 높이 1~2층 — 층 높이 17px 이상) | `material` 줄(5, 기본 `wood.warm`: 윗면·기둥·선반 판·밑판·발), `back` 줄(2, 기본 `wood.back`: 0 판 밑 짙은 줄, 1 뒤판), `tray` 줄(3, 기본 `cream`: 단계 2가 쟁반). `states`로 층 내용을 바꾼다 |
| `furniture.glass-case` | `rows`: 줄마다 부품 id 목록, 1~3줄 | `frame` 줄(3, 기본 `metal`: 틀과 받침) |
| `furniture.trolley` | `rows`: 줄마다 부품 id 목록, 1~4줄 | `frame` 줄(3, 기본 `metal`: 윗면·기둥·살), `inside` 줄(3, 기본 `metal.dark`: 안쪽과 바퀴) |
| `furniture.counter` | `art_cells` `[2~4, 2]` | `material` 줄(5, 기본 `wood.warm`), `top`: `{part, x}` 목록(윗면에 놓는 물건, x는 px), `tray`: `{x, w, items}`(윗면 쟁반). 앞판 수는 폭에서(앞면 폭 / 23 반올림: 2칸 3장, 3칸 4장, 4칸 5장), 판 폭은 모두 같고 남는 열은 양 끝에 반씩 |
| `furniture.chair` | `art_cells` `[1, 2]` | `facing`(`south` 기본 · `north` · `west` · `east`), `layer`(`all` 기본 · `back` · `front`, 아래 "앉기를 위한 층"), `material` 줄(5, 기본 `wood.warm`), `cushion` 줄(3: 0 오른쪽 끝, 1 앞 불룩한 줄, 2 윗면. 없으면 방석 없는 나무 의자). 치수는 캐릭터에서: 좌판 윗면 바닥에서 14px, 등받이 꼭대기 28px(북쪽 25px), 폭 20px. `east`는 `west`를 뒤집은 자리에 왼쪽이 밝게 다시 칠한다. `_layout`: `floor`(바닥 줄의 y), `seat`(좌판 윗면 사각형), `back_top`, `sit_point`(엉덩이가 올 점), `shared_from` |
| `furniture.desk` | `art_cells` `[2, 2]`(`south`) 또는 `[1, 2]`(`west`·`east`, 책상의 끝) | `facing`(`south` 기본 · `west` · `east`), `material` 줄(5), `top` 줄(2 이상: 마지막 단계가 윗면, 그 앞이 나뭇결. 없으면 `material`의 4·3), `drawers`(`right` 기본 · `left` · `none` · `both`: 앉은 사람 기준. 남쪽은 화면 오른쪽이 오른쪽, 옆모습은 가까운 끝에 서랍장이 오면 옆판, 아니면 다리 둘), `top_items`: `{part, x}` 목록(윗면 뒤쪽 절반에 선다, 밖이면 `slot-clipped`). 윗면 앞 모서리는 바닥에서 25px(좌판 + 11). 서랍이 없는 쪽 앞면은 다리와 그 사이 뚫린 무릎 자리(투명). `_layout`: `floor`, `top`(윗면 사각형), `kneehole`(외곽선 뒤에도 투명한 사각형, 옆모습 옆판이면 null), `work_height` |
| `prop.menu-board`, `prop.a-board` | 없음(고정 그림, 글자 없음) | |
| `prop.rug` | `size`: `[w, h]`px, `colors`: `[몸 줄(단계 0 테두리, 1 바탕), 바느질 줄]`, `stitch_step`: 바느질 줄의 단계 | |
| `building.shop` | `pixels`(높이는 192 이상: 지붕 104 + 벽 82 + 외곽선), `roof`: `{type, items}`, `wall`: `{material}`(벽 줄), `door`: `{at_x, color, step}`(문 줄, 계단 줄), `window`: `{at_x, width, slots}`(slots는 창살 양쪽 `[[부품 id], [부품 id]]`), `awning`: `{colors}`(줄 2개), `sign`: `{at_x, icons}`(부품 id 목록, 글자 없음) | `roof.items`: `{part, at: [x, y]}` 목록, `lamp`: `{at_x, part}`(벽등. `part` 기본 `prop.wall-lamp`. `lamp`를 적지 않으면 벽등도 둘레 빛도 없다), `trim` 줄(4, 기본 `trim`: 창틀·처마 밑 띠·간판 판), `plinth` 줄(2, 기본 `plinth`: 벽·창 받침), `frame` 줄(5, 기본 `wood.warm`: 문틀·창틀·간판 틀·받침 줄) |

`roof.type`(등록된 지붕): `flat`(평지붕, 줄 `roof.teal`), `pitched`(기와지붕, 줄 `roof.slate`).
`roof.items[].part`(등록된 지붕 위 물건, 그림자 띠까지 차지하는 w×h px): `roof.stair-house` 37×34, `roof.ac-unit` 23×20,
`roof.skylight` 41×30, `roof.chimney` 19×32, `roof.dormer` 49×42. `at`은 그 사각형의 왼쪽 위다. 지붕 안(`roof_area`)에 들어가고 서로 겹치지 않아야 한다(`roof-overlap`).

아직 고정으로 읽는 것(명세 키가 아니라 코드에 박혀 있다):

- `building.shop`: 팔레트 줄 `glass.window`(3단계), `metal`(3, 지붕 위 물건), `bread`(4)와 색 `yellow.1`. 지붕 줄(`roof.teal`·`roof.slate` 5단계)은 지붕 종류에서 온다.
- 가구: `furniture.counter`의 쟁반 `cream.2`·`wall.cream.0`. `furniture.glass-case`의 `glass.front.0·1`, `glass.streak.0`. `prop.menu-board`는 `wood.warm`(5), `board.green`(2), `white.0`. `prop.a-board`는 `wood.warm`(4), `board.green`(2), `white.0`. 반사 줄은 `glass.streak.0`.
- 지금 팔레트에서 `metal`은 3단계라 진열대·계산대의 `material`(5단계)로는 쓸 수 없다(`RecipeError`). 금속 진열대가 필요하면 `style.json`에 5단계 줄을 더한다.
- `prop.rug`의 `size`는 w ≥ 12, h ≥ 7px이다(테두리 2px 둘레 + 바느질 자리).

선반·진열대의 물건 높이 안내(점 부품 크기): 키 큰 진열대(높이 3칸) 4층은 층마다 물건 높이 11px까지 들어가고(3층은 맨 위 16, 나머지 둘 17, 2층 28, 1층 62 — 안쪽 높이 68px을 층 수로 나눈 층 높이에서 6줄을 뺀 값),
낮은 진열대(높이 2칸, 안쪽 36px)는 2층이면 12px, 1층이면 30px까지.
넘으면 `slot-clipped`다. 한 층에는 물건이 폭에 촘촘히 놓이는데, 너무 작으면 층이 비어 보인다.

## 앉기를 위한 층

의자는 앉은 사람의 앞뒤로 나눠 그릴 수 있다(`layer`). 게임에서 이 층을 쓰는 코드는 아직 없다(앉기는 나중 작업). 게임은 지금 가구의 방향과 무관하게 기본(남향) 그림만 그린다. 층·방향 칸은 PNG와 좌표표에만 있다. 게임에 필요한 것은 `docs/superpowers/specs/2026-11-13-sitting-notes.md`.

- `back` = 앉은 사람 **뒤에** 그릴 조각, `front` = 앉은 사람 **앞에** 그릴 조각, `all` = 둘을 합친 그림(기본).
  `back` → 앉은 사람 → `front` 순서로 같은 자리에 그린다. 앉은 사람 없이 `back` 위에 `front`를 겹치면 `all`과 바이트가 같다.
- 나누는 기준은 "앉은 사람의 몸이 그 조각보다 뒤에 있는가": `south`의 `front`는 좌판 앞 테두리와 앞다리, `north`의 `front`는
  등받이 전체(좌판은 등받이에 가려져 안 보이므로 보이는 것 전부), `west`·`east`의 `front`는 우리 쪽 옆 테두리와 가까운 다리 둘.
- `_layout.size`는 방향 공통 상자(폭 20, 높이 27)다. 북향은 그림 위 3줄이 비므로 등받이 높이는 `size`가 아니라 `floor - back_top`으로 읽는다.
- 좌판 밑(다리 줄, `_layout.shared_from`부터 아래)은 앉은 사람과 겹치지 않으므로 **두 층이 함께 갖는다.** 그래서 층마다
  혼자서도 바닥에 선 그림이고 검사(아래 가운데)를 통과한다. 상태로는 `back`·`front`(남쪽), `<방향>-back`·`<방향>-front`.
- 렌더러가 조각마다 외곽선을 다시 치므로, 앞 조각이 뒤 조각과 맞닿는 곳은 외곽선 색 줄(이음 줄: 좌판 앞 테두리 위)이어야
  겹친 그림이 `all`과 같다. 새 방향·모양을 그릴 때도 이것을 지킨다(테스트 `test_seating.py`가 바이트로 본다).

## 장면 더하기

```json
{"id": "bakery.scene.interior", "kind": "scene", "size": [9, 5],
 "fill":  [{"tile": "bakery.tile.wall", "rect": [0, 0, 9, 2], "wall": true}],
 "hang":  [{"part": "prop.clock", "at": [176, 14]}],
 "place": [{"id": "bakery.shelf", "state": "sweets", "cell": [2, 2], "dx": 8},
           {"id": "bakery.rug", "cell": [1, 4], "flat": true}]}
```

- `fill`: 칸 사각형을 타일로 채운다. `wall: true`면 그 아랫변(벽과 바닥이 만나는 줄)에 그림자 줄.
- `hang`: 벽에 걸린 것. `at`은 보이는 왼쪽 위 칸(px). 바로 밑에 짧은 그림자(`shadow.hung`).
- `place`: 바닥에 선 것. `cell`은 그림 캔버스의 왼쪽 아래 칸, `dx`·`dy`는 px 미세 이동. 아랫줄이 위인 것부터 그린다.
  `flat: true`는 바닥에 깔린 것(깔개): 그림자 없이 선 것보다 먼저 그린다.
- 장면은 견본 PNG일 뿐 게임에 들어가지 않는다. 물건끼리 겹치거나 장면 가장자리에 붙지 않게 둔다(테스트가 본다).

## 내보내기 (export, 가구)

명세에 `game` 덩어리를 두면 가구 정의의 그림 참조까지 바꾼다. 없으면 그림과 좌표표만 내고 가구 정의는 건드리지 않는다.

```json
"game": {"replaces": "school.bookcase"}
```

- PNG: `art/props/<세트>/<명세 id>.png`(세트 = id의 첫 마디). 기본 그림과 모든 상태를 **왼쪽부터 가로로** 붙인 한 장이고 상태 순서는 기본, 그다음 명세 `states`에 적힌 순서. 칸마다 `art_cells * 32`px.
- 좌표표: `data/atlases/art/props/<세트>/<명세 id>.png.atlas.json`. `pixelsPerUnit` 32, `origin` `"top-left"`, `width`·`height`는 PNG에서 잰 값. 칸 이름은 `<명세 id>`(기본)와 `<명세 id>@<상태>`. `pivot`은 점유 칸 줄의 왼쪽 아래 칸 가운데(`x = 0.5/art_w`, `y = (art_h-0.5)/art_h`). 키 순서·숫자 모양은 늘 같다.
- `replaces`는 그 가구 항목의 `tileParts[0].tile`의 `sprite`와 `name` **두 문자열만** 새 PNG 경로와 기본 칸 이름으로 바꾼다. 파일의 나머지 바이트(줄바꿈, BOM, 끝 줄바꿈)는 그대로이고, 바꾼 뒤 JSON을 다시 읽어 그 두 값 밖이 같은지 확인한다. 하나라도 다르면 아무것도 쓰지 않는다.
- 이 작업이 지원하는 가구: `tileParts`가 하나이고 `localCell`이 (0,0), 명세 `cells`가 `[w, 1]`이고 가구 `footprintCells`가 가로 한 줄 w칸과 같을 때.
- 문제 코드: `export-unknown-furniture`(정의에 없는 id), `export-unsupported-furniture`(위 조건 밖), `export-footprint-mismatch`(칸 수가 다름), `export-replace-failed`(치환 검증 실패), `export-duplicate-target`(한 번에 준 명세 둘이 같은 가구를 가리킴), `export-unreadable-definitions`(정의 파일을 못 읽거나 UTF-8·JSON이 아님), `export-state-size-mismatch`(상태 그림 크기가 기본 그림과 다름 — 한 시트의 칸은 크기가 같아야 해서 쓰지 않는다). 가구 정의의 `tileParts[0].tile`이나 `footprintCells`의 `x`·`y`가 없으면 `export-unsupported-furniture`다. `replaces`가 있는 명세는 `art_cells` 가로 칸 수가 `cells[0]`과 같아야 한다(다르면 `export-unsupported-furniture`, pivot이 어긋난다). 그림의 검사·만드는 부품 문제도 그대로 문제다. 문제가 하나라도 있으면(여러 id를 한 번에 줬어도) 전부 안 쓴다. 끝 코드 1.
- 쓰기는 같은 폴더의 `<이름>.tmp`에 먼저 다 쓴 뒤 PNG, 좌표표, 가구 정의 순서로 바꿔친다. 중간에 실패하면 임시 파일을 지우고, 바꿔치기 도중이면 이미 바뀐 파일을 알리며 끝 코드 2. 바꿔치기 도중 쓰기가 실패하면(`ExportWriteError`) PNG와 좌표표가 어긋날 수 있다. 같은 명령을 다시 돌리면 고쳐진다.
- 같은 내용이면 쓰지 않고 "바꿀 것 없음"이라고 한다(수정 시각도 그대로). `--dry-run`은 쓸 파일과 바꿀 가구만 찍는다.
- `--repo`의 기본은 이 도구가 든 저장소의 뿌리다. 테스트는 임시 폴더의 가짜 저장소만 쓴다.
- 장면 id를 주면 아직 없다는 오류(끝 코드 2). 타일·건물·소품은 아래 묶음 시트다.

## 묶음 시트 (export, 타일·건물·바깥 소품)

- 세트 = 명세 id의 첫 마디. 타일(`kind: tile`)은 `art/tiles/<세트>.png`, 건물·바깥 소품(`building`, `prop`)은 한 시트 `art/world/<세트>.png`에 같이 든다(새 종류는 `export.SHEET_KINDS` 한 줄). 한 번에 여러 id를 주면 시트별로 묶어 처리한다. 각 명세의 **기본 그림만** 낸다(상태는 아직 안 낸다).
- 시트는 32px 격자, **폭 16칸(512px) 고정**, 높이는 필요한 만큼(32의 배수). 폭이 넘는 그림은 `export-too-wide`.
- 자리 표 `<시트>.slots.json`: `{"cell": 32, "width_cells": 16, "slots": {"<명세 id>": {"x", "y", "w", "h"}}}`(px). 32x64 실내 벽은 자리 하나(h 64).
- **덧붙이기만 한다.** 이미 자리가 있는 id는 그 자리에 다시 그리고, 시트의 나머지 픽셀은 그대로 둔다. 크기가 달라졌으면 `export-slot-size-changed`이고 아무것도 쓰지 않는다(맵에 놓인 그림이 깨진다). 새 id는 **마지막으로 쓴 줄 뒤**에 명령줄 인자 순서대로 왼쪽부터 놓고, 폭이 모자라면 다음 줄로 간다(줄 높이는 그 줄에서 가장 큰 그림). 빈 틈을 메우지 않는다.
- 좌표표 `data/atlases/<시트 경로>.atlas.json`: `pixelsPerUnit` 32, 칸 이름은 명세 id(자리 표에 있는 것 전부), `pivot`은 타일 `{0.5, 0.5}`, 건물·소품 `{0.5, 1.0}`(발 위치). `width`·`height`는 PNG에서 잰 값.
- 쓰기는 가구와 같다(임시 파일에 다 쓴 뒤 바꿔치기, 같은 내용이면 쓰지 않는다, 문제가 하나라도 있으면 전부 안 쓴다, `--dry-run`).
- 문제 코드: `export-too-wide`, `export-slot-size-changed`, `export-kind-mismatch`(명세 종류가 그 시트에 안 맞음), `export-slots-unreadable`(자리 표가 JSON·모양이 아님), `export-sheet-inconsistent`(PNG와 자리 표 중 하나만 있거나 서로 안 맞음). 그림의 검사 문제도 그대로 문제다.

## 시험용 맵 (test-level)

`kit.py test-level`은 `--repo`의 `art/tiles/*.slots.json`, `art/world/*.slots.json`을 읽어 `--out`(기본 `<뿌리>/art/world/test`, 저장소 안이어야 한다)에
`pixel-kit-test.ldtk`(레벨 `PixelKitTest`)와 `maps.json`을 쓴다. 게임에서는 `--maps <그 maps.json>`으로 본다(`game/world/README.md`).

- 층 `Ground`(타일): 첫 타일 시트(파일 이름순)의 32x32 자리 중 맨 처음 것으로 레벨 전체를 깐다. 층 `Art`(엔티티, 층 정의 태그 `art`): 건물·소품 자리를 하나씩, 왼쪽에서 오른쪽으로 한 칸 틈을 두고 같은 땅 줄에 **발(아래 가운데) 기준**으로 놓는다.
- 레벨 크기는 내용에서 계산한다(항상 32의 배수). 도착 지점 `front` 하나: 첫 그림의 발 아래 둘째 줄.
- 충돌은 `collider-layers`에 빈 층 목록: 어떤 층의 타일도 막지 않아 레벨 전체가 열린다.
- `relPath`는 `.ldtk` 폴더 기준 상대 경로(슬래시)이고 `pxWid`·`pxHei`는 PNG에서 잰 값이다. `maps.json`은 `game/world/maps.json`의 `walker`·`npcWalker`를 옮기고 맵 하나만 둔다(`ldtk`는 저장소 뿌리 기준).
- `--out`에 이미 있는 `maps.json`이 이 도구가 쓴 것(맵이 `PixelKitTest` 하나)이 아니거나 `.ldtk`가 이 도구가 만든 모양(레벨 `PixelKitTest` 하나, 층 `Art`·`Ground`)이 아니면 덮어쓰지 않고 끝 코드 2(예: `--out game/world`는 거절된다). 머리말로는 판별하지 않는다(편집기 꼴로 `app: LDtk`를 쓰고, 편집기도 저장할 때 자기 것으로 바꾼다).
- `.ldtk`는 `room build`와 같이 LDtk 1.5.3 편집기 꼴로 쓴다(아래 "방 만들기 — 평면에서 LDtk로"). 그림 엔티티마다 같은 이름의 엔티티 정의를 두고 그 정의의 `tileRect`가 그 자리다(편집기는 엔티티의 `__tile`을 정의에서 다시 계산한다). 이름이 같은데 그림이 다른 자리가 두 시트에 있으면 끝 코드 2. 엔티티 이름(`bakery.exterior.flat` 같은 자리 이름)에는 점·하이픈이 들어 있어 편집기의 이름 규칙에 맞지 않을 수 있다(열기를 막는지는 확인 대기).
- 같은 내용이면 쓰지 않는다. 시트가 없거나 `npcWalker`가 원본에 없으면 끝 코드 2.

## 방 만들기 — 벽 타일

`pixelkit/room_tiles.py`. 칸마다 벽/바닥/밖만 정한 격자(`grid[y][x]`, 값 `OUT, WALL, FLOOR = 0, 1, 2`, 격자 밖은 `OUT`)에서
둘레 칸을 보고 32x32 타일을 고르고 그린다. 설계는 `docs/superpowers/specs/2026-11-13-room-builder-design.md` 4.2절.
평면 파일 읽기와 LDtk 쓰기는 다음 절이다.

**벽 윗면은 얇은 띠다**(사용자 결정 2026-11-14, 견본 A). 타일 격자와 충돌은 32px 칸 그대로이고, 윗면 칸에는 두께 `band`(기본 16px)의
띠만 그린다. 띠·정면이 아닌 자리는 벽 타일에서 **투명**하다 — 바닥 쪽이면 같은 칸의 `Ground` 층에 바닥 타일이 깔려 비치고,
맵 밖 쪽이면 아무것도 없다(게임은 투명 픽셀을 섞어 그린다). 바닥을 벽 타일에 굽지 않으므로 벽 타일 열쇠는 바닥 종류와 무관하다.

- `classify(grid, face_height=2)` → 칸의 종류: `floor`, `floor:shade`, 정면 열쇠(아래 표의 `face:…`), 윗면 `cap`, 밖 `None`. 평면 검사가 쓴다.
- `plan_tiles(grid, face_height=2, band=16, wall_doors=None)` → `RoomTiles(wall, ground)`: 칸마다 `Wall` 층 열쇠(벽 칸)와 `Ground` 층 열쇠(바닥 칸,
  그리고 띠 옆으로 바닥이 비치는 벽 칸) 또는 `None`. 둘레 칸뿐 아니라 이웃 칸의 띠 자리까지 계산해 열쇠에 담는다.
  `wall_doors`는 벽 칸의 문 칸들(평면의 `wallDoors`, LDtk에서는 `Collision`이 벽인 칸에 놓인 `Door`) — 아래 "벽 칸의 문". `build`·`check`·`repaint`가 같은 규칙으로 쓴다.
- `plan_sources(grid, face_height=2, band=16, wall_doors=None)` → 칸마다 픽셀 출처표(같은 계산에서 나온다 — 아래 "방 만들기 — 시험 표"). 게임이 벽지·바닥을 바꿔 그릴 때 쓰는 꼴이다.
- `tile_layout(key)` → 띠·좁은 정면 열쇠를 읽은 것(`band` 칸 안 띠 픽셀, `padded` 칠할 때 보는 이웃 칸 픽셀까지, `fills` 늘인 정면,
  `narrow` 좁은 정면 범위). 바닥·꽉 찬 정면 열쇠는 `None`, 틀린 열쇠는 `RecipeError`.
- `draw_tile(style, key, wall, floor_spec)` → 32x32 한 장. `wall`은 벽 모양 이름, `floor_spec`은 키트의 바닥 타일 명세
  (예: `bakery.tile.floor`, 32x32 — 바닥은 그 명세를 그대로 그린다). 모르는 벽 모양·열쇠는 `RecipeError`(있는 모양을 적는다).
  열쇠 하나만 보고 그린다 — 이웃 타일을 몰라도 칸 경계에서 띠의 선이 이어진다.
- `lint_room_tile(canvas, key, style)` → 문제 목록: `room-tile-size`(32x32가 아님), `off-palette`, `too-many-colors`(타일 한도 12),
  바닥·꽉 찬 정면은 `tile-transparent`(투명 픽셀)와 가로 이음매 `tile-seam`(가로로만 이어 붙이므로 세로는 보지 않는다).
  띠·좁은 정면 타일은 열쇠가 정한 자리로 본다: `tile-transparent`(띠·정면 자리에 투명 픽셀), `room-tile-leftover`(남는 자리에
  칠한 픽셀 — 바닥을 구웠다), `room-tile-empty`(완전히 투명), `tile-seam`(띠가 칸 경계를 넘어 이어지는 변에 외곽선이 그어졌다).
- `draw_tile(style, key, wall, floor_spec, top=None)`의 `top`은 띠 색 줄(아래 "띠 색"). 띠 픽셀만 바뀌고 정면·바닥은 그대로다.
- 벽 모양·띠 색·바닥 목록과 더하는 법은 아래 "방 만들기 — 벽 모양·띠 색·바닥". `wall_styles()`가 이름 목록, `wall_band(이름)`이 띠 두께,
  `wall_top(이름)`이 기본 띠 색, `wall_tops()`가 고를 수 있는 띠 색.

| 열쇠 | 층 | 뜻 |
|---|---|---|
| `floor` | Ground | 바닥 |
| `floor:shade` | Ground | 바로 위 칸이 꽉 찬 정면인 바닥. 맨 위 2줄이 그 팔레트 줄의 가장 어두운 단계(벽 밑 그림자) |
| `floor:shade:<x0>-<x1>` | Ground | 그림자를 x `[x0, x1)`에만(좁은 정면 밑). 칸 전체(`0-32`)는 `floor:shade`로 쓴다 |
| `face:<k>` | Wall | 꽉 찬 정면. 아래로 `face_height`칸 안에 바닥이 나오고 그 사이가 모두 벽인 벽 칸. `k`는 바닥에서 위로 몇 번째(1이 바닥 바로 위) |
| `face:<k>:<표시>` | Wall | 표시는 `l` `r` `c` `t` 중 붙는 것만 이 순서로. `l`/`r` 왼쪽/오른쪽 끝(그쪽 이웃이 같은 높이 정면도 윗면도 아님 → 그 변 열이 외곽선), `c` 위 칸이 윗면(천장 밑 그늘), `t` 위 칸이 벽이 아님(얇은 벽·맵 가장자리 → 맨 윗줄이 외곽선). 예: `face:2:c`, `face:2:lc`, `face:1:lrt` |
| `face:<k>:lr[c]@<x0>-<x1>[;<늘인 정면>…]` | Wall | 좁은 정면: 세로 벽 남쪽 끝의 정면(양쪽 끝이고 위로 정면을 따라가면 윗면이 나온다)을 그 윗면 띠의 가로 범위 `[x0, x1)`로. 무늬 없는 면(위 칸 벽지 한 면, 아래 칸 벽지 + 나무 두 면), 양옆 열과 아래 칸 맨 아랫줄이 외곽선. 나머지는 투명(옆이 다른 높이 정면이면 그 정면을 늘인다) |
| `cap:<띠>[;<늘인 정면>…]` | Wall | 윗면(정면이 아닌 벽 칸). `<띠>`는 띠 픽셀을 사각형으로 적은 것 — 칸 둘레 2px(`PAD`)까지의 이웃 칸 띠 중 칠을 정하는 픽셀만 담는다(그래서 그림이 같으면 열쇠가 같다) |
| `sill:<쪽>:<a>-<b>` | Wall | 윗면 칸의 문(문지방). `<쪽>`은 바닥이 있는 쪽(`n` 위 — 남쪽 바깥 벽의 문, `e` 오른쪽, `w` 왼쪽; `s`는 받기만 한다), `[a, b)`는 띠가 지나던 범위(`n`이면 줄, `e`·`w`면 열)이고 칸 끝에서 끝까지 덮는다. 두께 `MIN_BAND`(4) 이상. 나머지는 투명 |
| `door:face:1[:<표시>]` | Wall | 바닥 바로 위 정면 칸의 문(북쪽 벽처럼 정면이 보이는 벽): 그 정면 타일에 문틀 + 문짝을 박는다. 칸 전체 불투명 |

- 사각형 글: `x0,y0,x1,y1`(끝 제외)을 `+`로 잇는다. 좌표는 칸 왼쪽 위 기준이고 띠는 `-2..34`, 늘인 정면은 `0..32`. **정규형만 받는다**:
  같은 가로 구간들이 이어지는 줄을 한 사각형 띠로 묶고 위에서 아래, 왼쪽에서 오른쪽 순서. 같은 픽셀을 다르게 적은 열쇠, 0이 앞에 붙은 수,
  칠을 바꾸지 않는 이웃 픽셀을 적은 열쇠, 칸 안에 띠가 없는 열쇠, 띠·정면이 겹치는 열쇠는 `RecipeError`.
- 늘인 정면: `;<정면 열쇠>@<사각형 글>`. 정면 열쇠는 끝 표시 없이(`face:2:c`, `face:1`, `face:1:t`), 열쇠 순서로 한 번씩. 그 픽셀은 그 정면 타일의 같은 자리 픽셀이다.
  예: 방 둘 사이 세로 벽이 가로 벽 정면을 지나는 칸 `cap:8,-2,24,34;face:2:c@0,0,8,32+24,0,32,32`.
- 띠 자리(견본 A의 규칙, 설계서 4.2절): 가로 자리는 양옆이 방 쪽(바닥·정면)이면 가운데(`band` 16이면 8~24), 한쪽만 방이면 그쪽에 붙고,
  아니면 위아래로 이어진 윗면에서 받는다. 세로 자리는 아래가 정면이면 아래, 위만 바닥이면 위, 아니면 옆으로 이어진 윗면에서 받는다.
  한쪽만 방인 칸이 양쪽이 방인 벽에 이어지면 그 벽의 자리를 따른다. 띠 = 가운데 사각형 + 이웃 윗면 쪽 팔, 두 칸이 나누는 변의 팔 범위는
  "양쪽 방"으로 정해진 쪽 것.
- 남는 자리: 바닥 쪽 → 투명 + 그 칸 `Ground`에 바닥 타일, 밖 쪽 → 투명(`Ground` 없음), 정면 쪽 → 그 정면을 옆으로 늘인다, 둘레가 모두 윗면 → 띠로 채운다.
  한 칸에 바닥 쪽과 밖 쪽이 함께 남으면 밖 쪽을 띠로 채운다(바닥이 방 밖으로 새지 않게).
- **벽 칸의 문**(2026-11-14, 검토 지적 "현관이 다른 벽과 똑같아 어디로 나가는지 알 수 없다"): 문 칸은 여전히 벽(`Collision` 1)이고 그림만 바뀐다.
  윗면 칸이고 위·오른쪽·왼쪽(이 순서) 중 바닥이 붙어 있으면 그 칸의 띠 대신 문지방(`sill:…`, 나무 줄 `wood.school` — 바닥 쪽 가장자리 한 줄 빛,
  바깥 쪽 가장자리 외곽선, 가운데 판 이음)을 깐다. 바닥 쪽 가장자리는 띠 범위 `[a, b)` 안에서 `n`이면 맨 위 줄, `e`면 오른쪽 끝 열, `w`면 왼쪽 끝 열이다. 그 칸의 띠는 이웃 칸의 띠 계산에서 빠지므로 이웃 띠의 끝이 외곽선으로 닫힌다(문설주).
  바닥 바로 위 정면 칸이면 `door:face:1…`(문틀·문짝). 바닥이 붙지 않은 문 칸(`room-door-no-floor`)과 위가 막힌 정면 칸은 문 없이 그린다.
  바뀌는 것은 문 칸과 그 둘레 칸의 `Wall` 열쇠뿐이다(문이 없는 방은 그대로). 약점: 정면 문은 아래 타일 한 장(32px)에만 박혀 2칸 높이 벽에서 작아 보인다.
- 벽 칸 밑 `Ground`의 그림자는 그 칸의 실제 이웃으로 정한다(위가 정면이면 그 정면이 덮는 열 중 이 칸에서 바닥이 비치는 열에만 — 대개 `floor`).
- 띠 그림: 띠가 아닌 픽셀에 8방향으로 닿는 띠 픽셀은 외곽선, 그 안쪽 한 줄은 북쪽(먼 쪽) 가장자리만 빛, 나머지 가장자리는 그늘.
  이웃 칸의 띠까지 보고 칠하므로 모서리·T자·끝·십자에서 선이 끊기거나 두 겹이 되지 않는다.
- 정면 옆이 윗면이면 그 변의 외곽선은 윗면 쪽(띠 가장자리)이 친다(정면은 `l`/`r`을 받지 않는다) — 선이 두 겹이 되지 않는다.
- 세로 벽은 윗면만이고 그 남쪽 끝 `face_height`칸이 정면(좁은 정면)이다. 가로 벽이 `face_height + 1`칸보다 얇으면 윗면 없이 정면만 남는다(`t`).
- 문간은 벽 줄의 바닥 칸이다. 따로 그리는 것은 없고 양옆 정면이 `l`/`r` 끝이 된다.
- 충돌은 칸 단위다: 벽 칸은 띠 옆의 8px, 가로 띠 위의 16px처럼 바닥으로 보이는 자리까지 통째로 막힌다. 가로 벽이 서 보이려면
  3칸(윗면 1 + 정면 2), 세로 벽은 1칸이다.
- 알려진 약점: 세로 벽이 정면 위 윗면으로 이어지는 두꺼운 덩어리(예: 3x4)에는 한 단 꺾인 자국이 남는다(정면 위 띠가 정면 너비를 덮어야 해서).
  가로 윗면 바로 위가 바닥 쪽이면 바닥처럼 보이는 16px가 남는다(사용자 답 대기라 그대로).
- 알려진 약점: 띠 자리는 칸 둘레만이 아니라 이어진 윗면을 따라 방 전체에서 정해져서, 두꺼운 벽 덩어리에서는 좌우 거울 대칭이 아닐 수 있다. 벽은 한 칸 두께로 칠해야 얇게 보인다(두 칸 이상이면 안쪽이 띠로 채워져 넓은 덩어리가 된다).

## 방 만들기 — 벽 모양·띠 색·바닥

2026-11-14 사용자 요청("오 좋아 이거 말고도 다른것도 계속 만들어줘 유저가 자유롭게 만들 수 있도록")으로 견본 W3~W8·F2~F9를 정식으로 옮기고
더 그렸다. 지금은 **맵(방 평면) 하나에 벽 한 종류·바닥 한 종류·띠 색 하나**다. 고를 때는 `room styles`로 견본판을 뽑아 본다
(`room styles --list`는 글로 이름·컨셉).

### 벽 모양 (평면의 `wall`)

한글 이름과 컨셉은 코드의 등록 값(`@wall_style(이름, label=…, concept=…)`)이 원본이고 `room styles --list`와 견본판에 같은 문구가 찍힌다.
표는 읽기 쉽게 옮긴 것이다(문구가 조금 달라도 이름 집합은 테스트가 맞춘다).

| 이름(id) | 한글 이름 | 컨셉 | 기본 띠 색 |
|---|---|---|---|
| `paper` | 줄무늬 벽지 벽 | 크림색 세로줄 벽지 아래 갈색 나무 징두리 판. 포근하고 무난한 집 벽(본보기 방). 판 때문에 벽 아래가 묵직하다 | `wall.top.cream` |
| `plaster` | 회반죽 벽 | 연한 회백색 단색 면에 낮은 갈색 걸레받이. 가장 조용해서 어디든 어울리지만 무늬가 없어 밋밋하다 | `wall.plaster.top` |
| `wallpaper-sprig` | 연녹 잔무늬 벽지 | 연녹색 바탕에 작은 분홍 십자 잔무늬, 아래는 밝은 크림 걸레받이. 부드럽고 무난한 집 벽. 1배에서는 무늬가 점으로 보인다 | `wall.top.brown` |
| `wallpaper-floral` | 분홍 꽃무늬 벽지 | 연분홍 바탕에 드문드문 작은 꽃(꽃잎 넷 + 노란 꽃술), 아래는 갈색 걸레받이. 아기자기한 침실·아이 방. 1배에서는 꽃이 점으로 보인다 | `wall.top.brown` |
| `paint-mint` | 민트 칠 벽 | 민트색 단색에 흰 허리 몰딩, 아래 판에 짧은 테(32px마다)가 되풀이. 산뜻한 아이 방. 테는 1배에서 가는 줄로 보인다 | `wall.top.light` |
| `paint-green` | 짙은 녹색 칠 벽 | 짙은 녹색 벽(같은 계열 가는 세로줄)에 높고 밝은 걸레받이. 차분하고 가구가 도드라진다. 서재·사무실 | `wall.top.slate` |
| `paint-two-tone` | 위아래 투톤 벽 | 위는 크림, 아래는 청회색 칠(벽 높이 4할)에 흰 허리 몰딩. 깔끔한 거실·복도 | `wall.top.brown` |
| `panel-pine` | 소나무 세로 판벽 | 밝은 소나무 세로 판(벽 전체, 판 8px)을 벽 전체에, 같은 나무 걸레받이. 따뜻한 오두막·부엌·가게 | `wall.top.brown` |
| `panel-plank` | 짙은 가로 판자벽 | 짙은 갈색 가로 판자를 겹쳐 댄 벽. 오두막·창고 느낌. 나무 바닥과 깔면 갈색 일색이 된다 — 돌·타일 바닥과 | `wall.top.brown` |
| `tile-white` | 흰 타일 벽 | 흰 벽 타일(16x8 엇갈림 쌓기)에 청록 띠와 타일 걸레받이. 깨끗한 부엌·욕실 | `wall.top.light` |
| `tile-blue` | 하늘색 타일 벽 | 하늘색 정사각 타일(16px), 아래에 짙은 타일 한 줄. 시원한 욕실. 아래 짙은 줄은 옅어서 잘 안 보인다 | `wall.top.light` |
| `brick-cream` | 크림 벽돌 벽 | 크림색 벽돌(16x8)에 밝은 벽돌 몇 장, 짙은 나무 걸레받이. 벽돌 중 가장 조용하다. 가게·부엌 | `wall.top.brown` |
| `brick-red` | 붉은 벽돌 벽 | 붉은 벽돌에 짙은 줄눈, 밝은 벽돌 몇 장. 묵직하고 개성이 강하다. 가게 뒤·창고 | `wall.top.slate` |
| `concrete` | 콘크리트 벽 | 회색 노출 콘크리트에 거푸집 이음(32x16)과 폼타이 구멍. 차갑고 투박한 작업실·창고. 아래 때 탄 띠가 약하다 | `wall.top.slate` |

타일 한 장의 색은 띠·외곽선을 합쳐 12 이하다(검사가 지킨다). 정면 무늬의 가로 주기는 32의 약수이거나 한 장(32)이다.

### 띠 색 (평면의 `top`, 선택)

고를 수 있는 줄은 아래 다섯이다. 적지 않으면 그 벽 모양의 기본이다(위 벽 표의 마지막 열). 띠 픽셀만 바뀐다(정면·바닥·열쇠는 같다).
짙은 색은 벽이 또렷하지만 바깥 어둠과 붙어 보인다 — 사용자는 아직 고르지 않았다(지금 기본은 위 표). 원본은 `room_tiles.TOPS`.

| 이름(id) | 한글 이름 | 컨셉 |
|---|---|---|
| `wall.top.cream` | 황토 띠 | 따뜻한 황토색 윗면. 크림 벽지 벽과 한 덩어리로 포근하고, 바깥 어둠과는 또렷이 구별된다 |
| `wall.top.brown` | 갈색 띠 | 차분한 갈색 윗면. 나무·벽지 벽에 두루 어울려 가장 무난하다 |
| `wall.top.slate` | 짙은 남회색 띠 | 벽이 또렷해지고 방 안이 도드라진다. 바깥 어둠과는 붙어 보인다 |
| `wall.top.light` | 밝은 회색 띠 | 밝고 가벼운 윗면. 타일·파스텔 벽에 어울리고 벽이 가벼워 보인다 |
| `wall.plaster.top` | 회백색 띠 | 회반죽 벽의 기본 윗면. 연한 회갈색이라 가장 조용하고, 다른 벽에도 고를 수 있다(다른 줄과 이름 꼴이 다른 것은 plaster의 기본색으로 먼저 생겨서다) |

- 평면의 `top`은 자리 표(`tiles.slots.json`)에 방 값 `top`으로 남고 `repaint`가 읽는다. **`top`이 없는 자리 표는 그 벽 모양의 기본색으로 본다**
  (top은 2026-11-14에 생겼다 — 그 전에 만들어 LDtk에서 칠한 방도 그대로 다시 그려지고 표에 `top`을 더하지 않는다. `band`가 없는 옛 방 거절과 다르다).
- 시트가 다른 띠 색으로 그려져 있으면(표의 `top`, 없으면 기본) `--force`도 거절한다 — 한 시트에 두 색이 섞이지 않게.
- 띠 색을 더하려면 `style.json`에 `wall.top.<이름>` 3단계 줄(어두운 것부터: 0 안쪽 그늘 / 1 면 / 2 안쪽 빛)과 `room_tiles.TOPS`에 한 줄(한글 이름·컨셉 포함).

### 바닥 (평면의 `floor`)

키트의 한 칸(32x32) 타일 명세면 무엇이든 받는다(`room styles --list`가 받는 것 전부를 찍는다). 정식 방 바닥은 세트 `floor`
(`art/kit/recipes/floor/tiles.json`)다. 캐릭터·가구가 위에서 잘 보이게 조용히 그렸다. 한글 이름·컨셉은 명세의 `label`·`concept` 필드(비어 있지 않은
문자열)가 원본이다.

| 명세 id | 한글 이름 | 컨셉 |
|---|---|---|
| `floor.checker-soft` | 조용한 체크 바닥 | 크림·모래색이 비슷한 옅은 체크(16px). 가구와 캐릭터가 잘 보이는 무난한 바닥. 어느 방이든 |
| `floor.plank` | 꿀색 마루 | 꿀색 가로 판자(8px) 마루. 따뜻한 거실·침실. 판 끝 이음 표시가 곳곳에 보인다 |
| `floor.plank-vertical` | 밝은 세로 마루 | 밝은 참나무색 세로 판자(8px). 산뜻하고 가벼운 방·카페·가게 |
| `floor.plank-wide` | 넓은 회갈색 마루 | 회갈색 넓은 판자(16px)에 끝 이음이 없어 조용하다. 차분한 거실·사무실. 돌판처럼 읽힐 수 있다 |
| `floor.herringbone` | 헤링본 마루 | 호두색 헤링본 쪽매(판 16x8). 클래식하고 고급스럽다. 넓게 깔면 무늬가 많아 보인다 |
| `floor.parquet-basket` | 바구니 짜임 마루 | 밤색 바구니 짜임 쪽매(16px 칸마다 판 둘, 가로·세로 엇갈림). 클래식한 서재·응접실. 넓게 깔면 무늬가 복잡하다 |
| `floor.stone-slab` | 돌판 바닥 | 베이지 회색 돌판을 크게(32px) 깐 바닥. 조용하고 서늘하다. 현관·가게·복도 |
| `floor.concrete` | 콘크리트 바닥 | 회색 콘크리트 판(32px)에 옅은 얼룩과 잔 점. 투박하고 단단하다. 작업실·창고 |
| `floor.brick` | 벽돌 바닥 | 붉은 벽돌을 엇갈려(16x8) 깐 바닥. 투박하고 따뜻하다. 가게 뒤·창고·주방 |
| `floor.mosaic` | 하늘빛 모자이크 바닥 | 하늘빛 작은 타일(알 7px) 모자이크, 짙은 알이 대각선으로 고르게. 깨끗하고 시원하다. 욕실. 알이 작아 바쁘게 보인다 |
| `floor.linoleum` | 연녹회색 리놀륨 바닥 | 연녹회색 단색에 잔 점. 가장 조용한 바닥 중 하나. 부엌·가게 |
| `floor.carpet` | 청회색 카펫 | 청회색 단색 카펫에 아주 옅은 점. 차분하고 포근하다. 침실·서재 |
| `floor.carpet-diamond` | 장밋빛 마름모 카펫 | 장밋빛 카펫에 옅은 마름모 격자(테두리 없음). 아늑한 침실·아이 방 |
| `floor.tatami` | 돗자리 바닥 | 연두 황토빛 정사각 돗자리(16px, 결 엇갈림, 테 한 줄). 일본식 방 느낌. 넓게 깔면 무늬가 많아 보인다 |

`bakery.tile.floor`(진한 체크, 본보기 방)·`bakery.tile.sidewalk`·`bakery.tile.grass`도 한 칸 타일이라 받는다(한글 이름·컨셉이 있다 — `--list`에 나온다).

### 새 벽 모양·바닥을 더하는 법

- **벽 모양**: `room_tiles.py`에 `_Wall`을 이어받은 클래스 하나 + `@wall_style("이름", label="한글 이름", concept="컨셉 한 줄")` 한 줄. 값: `cap`(기본 띠 색 줄), `band`(띠 두께, 기본 16),
  `face(style, k, ceiling) -> Canvas`(32x32, 끝·위 외곽선 없이 — `k == 1`이 바닥 바로 위 타일, `ceiling`이면 맨 위 2줄만 천장 밑 그늘),
  좁은 정면(세로 벽 남쪽 끝)의 색 `narrow(style) -> (천장 밑 그늘, 면, 아래 면 윗줄, 아래 면)`과 아래 면이 시작하는 줄 `narrow_base`
  (`_Wall`의 기본은 `paper`·`wood` 줄에서 고른다). `label`·`concept`은 비워 둘 수 없고(테스트가 지킨다) 견본판과 `--list`에 찍히므로, 그림을 직접 본 뒤 생김·느낌·어울리는 곳·약점을 한 줄로 쓴다. 걸레받이·허리 몰딩·천장 밑 그늘은 도우미
  `_skirting`·`_rail`·`_ceiling`, 두 칸을 잇는 세로 주기 64 무늬는 `_wall_y`. 등록하면 테스트가 모든 열쇠를 그려 검사하고
  다른 모양과 그림이 다른지 본다(`tests/test_room_styles.py`).
- **바닥**: 무늬가 새로우면 `makers/tiles.py`에 `@register("tile.floor-이름")` 함수 하나(위 "만드는 부품이 읽는 키" 표에 한 줄),
  색만 다르면 부품은 그대로. 그리고 `art/kit/recipes/floor/tiles.json`에 명세 한 덩어리
  `{"id": "floor.<이름>", "kind": "tile", "part": …, "art_cells": [1, 1], "colors": [...], "label": "한글 이름", "concept": "컨셉 한 줄"}`
  (`label`·`concept`은 정식 방 바닥 `floor.*`에서 비워 둘 수 없다 — 테스트가 지킨다).
- **팔레트**: 필요한 줄을 `style.json`에 뜻이 보이는 이름으로 덧붙인다(어두운 것부터). 있는 줄은 바꾸지 않는다. 방 재료 줄(`wall.`·`floor.`·`brick.`)은
  다른 줄과 hex를 나누지 않는다 — 같은 hex가 두 줄에 있으면 `floor:shade`가 어두워지는 줄이 이름순 첫 줄로 바뀔 수 있다(테스트가 지킨다).
- 그림은 LimeZu Room Builder 시트(`Assets/moderninteriors-win/1_Interiors/32x32/Room_Bulder_subfiles_32x32/`)에서 종류·명도·무늬 밀도만 보고
  픽셀을 베끼지 않는다. `room styles`와 방 예시를 직접 열어 보고, `kit.py lint`(0)와 테스트를 통과시킨다.

## 방 만들기 — 평면에서 LDtk로

`pixelkit/room_plan.py`(평면 읽기·검사)와 `pixelkit/room_ldtk.py`(쓰기). 설계는 같은 설계서 4.1·4.3·4.4절.

평면 파일 `art/kit/rooms/<id>.json`(파일 이름 = `id`):

```json
{"id": "test.two-rooms", "wall": "paper", "floor": "bakery.tile.floor", "spawn": [3, 4],
 "plan": ["####…", "#..D…"], "doors": {"D": "town"}, "face_height": 2}
```

바깥 벽에 현관 문을 달고 지을 수 있는 칸을 정하는 평면(집 짓기용)은 키 셋을 더한다 — 모두 선택이고(`frameId`는 `build`와 함께만), 없는 평면은 지금과 똑같이 읽힌다:

```json
{"id": "house.main", "wall": "paper", "floor": "bakery.tile.floor", "spawn": [3, 4],
 "plan": ["########", "########", "########", "#......#", "#......#", "#......#", "###W####"],
 "wallDoors": {"W": "town"},
 "build": ["00000000", "00000000", "00000000", "01111110", "01011110", "01101110", "00000000"],
 "frameId": "house.main.v1"}
```

- `plan`: 글자 줄. `#` 벽, `.` 바닥, 빈칸 밖. 줄 길이가 다르거나 모르는 글자가 있으면 문제다(조용히 채우지 않는다). `doors`(선택)에 적은 글자는 바닥 + 문이다 — 그 칸에 `Door` 엔티티가 놓이고 필드 `Where`에 값이 들어간다. `face_height`(선택, 기본 2). `spawn`은 칸 [x, y].
- `wallDoors`(선택): `{글자: Where 값}`. 그 글자 칸은 **벽(`#`)으로 남고** 그 칸 `(x*32, y*32)`에 32x32 `Door` 엔티티가 놓인다 — 바깥 벽에 다는 현관 문이다(`doors`의 글자는 바닥이 되므로 맨 아랫줄에 두면 `room-open-edge`에 걸린다). 글자는 `doors`의 글자와 겹치지 않는다. 그 문의 상하좌우 넷 중 도착 지점에서 걸어 닿는 바닥이 있어야 한다.
- `build`(선택): 평면과 같은 크기의 `0`·`1` 줄. `1` 지을 수 있음, `0` 고정. 있으면 LDtk에 `Build` 층이 생기고 `maps.json`의 맵 항목에 `build`(아래 `maps.json` 줄)가 든다. 밖 칸, 도착 지점 칸, 문 칸, 벽 문과 그 둘레 바닥 칸은 `0`이어야 한다. 벽·그 밖의 바닥 칸은 자유.
- `frameId`(`build`가 있으면 필수, 없으면 쓰지 않는다): 테두리의 판 이름(글). 맵을 다시 그려 테두리가 바뀌면 올리는 값이고 `maps.json`의 `build.frameId`에 그대로 든다 — 게임(`WorldCatalog`)은 이 값이 없는 `build`를 거절하고 기본값으로 메우지 않는다. 게임 맵 목록(`game/world/maps.json`)의 짓는 집 항목도 같은 값이어야 한다(`test_room_ldtk.HouseRoomTests`가 본다 — 하나를 올리면 둘 다 고친다).
- `wall`은 벽 모양 이름, `floor`는 키트의 한 칸 타일 명세 id(`kind: tile`이고 `art_cells` `[1, 1]`), `top`(선택)은 띠 색 줄 이름(위 절).
  모르는 이름은 아는 이름 목록과 함께 끝 코드 2. 본보기 평면은 `paper` + `bakery.tile.floor`, `top` 없음.

`room check <id>`가 보는 것(코드): `room-ragged-plan`, `room-unknown-char`, `room-open-edge`(바닥이 맵 가장자리나 밖 칸에 닿음),
`room-unreachable-floor`(도착 지점에서 걸어 닿지 못하는 바닥, 덩어리마다 하나), `room-spawn-in-wall`, `room-door-in-wall`,
`room-door-no-floor`(벽 문 둘레에 도착 지점에서 닿는 바닥이 없음), `room-build-ragged`(`build` 줄 수·길이가 평면과 다름),
`room-build-char`(`build`에 `0`·`1`이 아닌 글자), `room-build-fixed`(고정이어야 할 칸이 `1`),
`room-build-no-frame-id`(`build`가 있는데 `frameId`가 없음), `room-frame-id-without-build`(`build` 없이 `frameId`만 있음),
`room-wall-thin`(**알림**: 정면 `face_height`칸 + 윗면 1칸보다 얇은 가로 벽. 찍기만 하고 끝 코드는 0이며 쓰기를 막지 않는다).
알림이 아닌 문제가 있으면 끝 코드 1이고 `build`는 아무것도 쓰지 않는다. 평면에 쓰인 벽 모양·바닥 명세가 키트에 없으면 끝 코드 2.

`room build <id>`는 `art/world/rooms/<id>/`(`--out`으로 바꿀 수 있다, 저장소 안이어야 한다)에 넷을 쓴다:

| 파일 | 내용 |
|---|---|
| `<id>.ldtk` | 레벨 `<맵 id>`(32px 칸). 층은 위에서 아래: `Collision`(IntGrid, 1 `wall` / 2 `floor` / 0 밖, 층 정의 `displayOpacity` 0.35 — LDtk 편집기에서 색 칸이 벽·바닥 그림을 덮지 않고 비쳐 보인다. 게임은 이 값을 읽지 않는다), `Build`(IntGrid, 평면에 `build`가 있을 때만 `Collision` 바로 다음, 1 `buildable` / 0 고정(칠하지 않은 칸), `displayOpacity` 0.25, 32px), `Door`(Entities, 엔티티 정의 `Door` + 문자열 필드 `Where` — 바닥 칸의 문과 벽 칸의 문(`wallDoors`) 모두), `Furniture`(Entities, 층 정의 태그 `ysort`, 비어 있음), `Wall`(Tiles, 벽 칸마다), `Ground`(Tiles, 바닥 칸마다 + 띠 옆으로 바닥이 비치는 벽 칸마다). 타일셋 하나(`tiles.png`, 32). 타일 인스턴스는 `px`, `src`, `f`, `t`(시트의 타일 번호, 행 우선 16칸), `d`(칸 번호), `a` |
| `tiles.png`, `tiles.slots.json` | 쓰인 타일만 모은 시트와 열쇠 → 자리 표(`room_tiles`의 열쇠). **덧붙이기만**: 한 줄 16칸 폭 고정, 새 열쇠는 마지막 번호 다음부터. 이미 있는 타일의 자리(`t`, `src`)는 시트가 커져도 바뀌지 않는다. 자리 표에는 그 시트를 그린 `wall`·`floor`·`face_height`·`band`(윗면 띠 두께, 벽 모양의 값)와 평면에 적었을 때만 `top`(띠 색)도 적는다(`repaint`가 여기서 읽는다 — LDtk 편집기는 이 파일을 건드리지 않는다). 시트가 다른 `wall`·`floor`·`band`·띠 색으로 그려져 있으면 `--force`도 거절한다(한 시트에 두 모양이 섞이지 않게). `band`가 없는 옛 표(윗면 32px 시절)에는 덧붙인다 — 옛 윗면 열쇠 `cap:<수>`는 다시 나오지 않아 쓰이지 않고 남는다(깨끗한 시트를 원하면 `tiles.png`·`tiles.slots.json`을 지우고 만든다) |
| `maps.json` | 이 맵 하나. `collision.rule`은 `interior-auto`, `spawns`는 도착 지점 하나 `<맵 id 소문자>-entry`(예: `testtworooms-entry`, 맵 사이에서 이름이 겹치지 않게)에 도착 칸 가운데 픽셀, `viewCells`는 레벨 높이(칸)와 13 중 작은 것, `build`는 평면에 `build`가 있을 때만 `{"frameLayer": "Build", "frameId": <평면의 frameId>, "faceHeight": <face_height>, "band": <벽 모양의 띠 두께>, "bandColors": <top, 없으면 벽 모양의 기본 띠 색>, "defaultWall": <slot wall·art = wall인 꾸밈 id>, "defaultFloor": <slot floor·art = floor인 꾸밈 id>, "replacesLayers": ["Wall", "Ground"]}` — 게임(`WorldCatalog`)이 전부 필수로 읽고 기본 꾸밈으로 벽·바닥을 조합해 `Wall`·`Ground` 대신 그린다. 기본 꾸밈은 `data/building/RoomFinishDefinition.json`에서 찾고(여럿이면 파일의 첫 것) 없으면 `room-build-no-finish`로 아무것도 쓰지 않는다(조합한 그림이 구운 그림과 달라지므로), `doors`는 비어 있다(문의 목적지 표는 손으로 채운다). `walker`·`npcWalker`는 `game/world/maps.json`에서 옮긴다. 게임에는 `--maps`로 준다 |

- 맵 id = 방 id에서 점·하이픈을 지우고 낱말 첫 글자를 대문자로(`test.two-rooms` → `TestTwoRooms`).
- 칸 → 층: 벽(1)은 `Wall` 층 타일(띠·정면이 아닌 자리는 투명), 바닥(2)은 `Ground` 층 타일, 띠 옆으로 바닥이 비치는 벽 칸은 `Ground` 층에도 바닥 타일, 밖(0)은 타일 없음(`room_tiles.plan_tiles`). 같은 격자가 `Collision`의 `intGridCsv`(행 우선, 왼쪽 위부터)다. 그림과 충돌이 한 격자에서 나온다. 벽 칸에 `Ground` 타일이 있어도 `Wall` 타일이 함께 있으므로 `interior-auto`가 막고, 칠한 `Collision` 1이 언제나 막는다(`game/world/README.md` 3절).
- 이미 있는 방 파일(넷 중 하나라도)이 있으면 `--force` 없이는 거절(끝 코드 2). `--force`는 같은 평면이면 같은 바이트를 쓴다. `--dry-run`은 쓸 파일만 찍는다.
- **`--force`여도 남의 파일은 덮지 않는다**(끝 코드 2, 아무것도 쓰지 않음): 있는 `maps.json`이 맵 하나이고 그 `ldtk`가 이 방의 `<id>.ldtk`가 아니거나(예: `--out game/world` — 게임 전체의 맵 목록), 있는 `<id>.ldtk`가 방 모양(`Collision`·`Wall`·`Ground` 층)이 아니거나(예: 방 모양이 아닌 편집기 맵 `house.ldtk`가 있는 폴더에 방 id `house` — 시험은 옛 집 맵 견본 `tests/fixtures/ldtk-1.5.3/house.ldtk`로 본다), `tiles.png`·`tiles.slots.json` 중 하나만 있으면 거절한다. 자기 방을 `--force`로 다시 만드는 것은 된다.
- **`--force`는 평면에서 통째로 다시 만든다.** LDtk에서 손으로 고친 것(다시 칠한 `Collision` 칸, 놓은 가구, 고친 문)은 지워진다(덮을 때 한 줄 알림을 찍는다). LDtk에서 손을 댄 뒤에는 `--force`가 아니라 `room repaint`를 쓴다.
- 평면에 문 글자가 있으면 알림 `room-door-unregistered`를 찍는다(쓰기는 막지 않는다): 만든 `maps.json`의 `doors`가 비어 있어 게임에서 그 문은 열리지 않는다. `doors`에 그 `Where` 값(소문자, `-`는 공백)의 목적지 맵·도착 지점을 손으로 적는다.
- 쓰기는 모든 파일을 임시 파일에 먼저 쓴 뒤 바꿔친다. 같은 입력이면 같은 바이트, LF.
- `.ldtk`는 LDtk 1.5.3 편집기가 저장하는 꼴 그대로 쓴다: 머리말(`app: LDtk`), 키와 키 순서, 기본값을 편집기가 저장한 파일(옛 집 맵의 견본 `tests/fixtures/ldtk-1.5.3/house.ldtk` — 2026-11-14까지 게임의 집 맵이던 `Assets/LDtk/House/house.ldtk`를 바이트 그대로 옮긴 것)에서
  가져왔다(`ldtk_common.py`의 편집기 꼴 함수). 빠지면 안 되는 키는 공식 스키마의 `required`로 테스트가 지킨다(`tests/test_ldtk_editor_shape.py`,
  스키마와 출처는 `tests/fixtures/ldtk-1.5.3/README.md`). 이 도구가 만든 방이라는 표시는 머리말이 아니라 층 정의의 `doc` 문자열과 방 모양이다
  (편집기는 저장할 때 머리말을 자기 것으로 바꾼다). 문의 `Where` 값은 `__value`와 편집기가 읽는 `realEditorValues`에 함께 둔다.
- 2026-11-14 전에 이 도구가 쓴 `.ldtk`는 게임이 읽는 키만 있어 편집기에서 열리지 않았다(아래 "편집기에서 확인된 것"). 그런 파일은
  `room check <.ldtk>`가 `room-ldtk-incomplete`로 알리고 `room repaint`가 거절한다(끝 코드 1, 채워 쓰지 않는다) — 평면에서 `room build <id> --force`로 다시 만든다.

## 방 만들기 — LDtk에서 칠한 뒤 (`room repaint`, `room check <.ldtk>`)

`room build`로 만든 방을 LDtk 편집기에서 열어 `Collision` 층(1 벽, 2 바닥, 0 밖)을 다시 칠하고 저장한 뒤
`room repaint <그 .ldtk 경로>`를 부르면 벽 그림이 칠한 칸을 따른다.
**편집기에 파일이 열린 채로 `repaint`하지 않는다** — 편집기가 나중에 저장할 때 옛 벽 그림으로 덮는다. 편집기에서 저장하고 닫은 뒤 부르거나, 부른 뒤 편집기에서 파일을 다시 불러온다.

- **바꾸는 것**: 레벨의 `Wall`·`Ground` 층 인스턴스의 `gridTiles`(칠한 칸에서 `room_tiles.plan_tiles`로 다시 고른다), 타일셋 정의의
  `pxWid`·`pxHei`와 편집기가 저장한 문서에 있으면 `__cWid`·`__cHei`(시트가 커졌을 때), `tiles.png`·`tiles.slots.json`(새 열쇠만 덧붙인다 — 이미 있는 타일의 자리 `t`·`src`는 그대로).
- **그대로 두는 것**: 그 밖의 전부 — 머리말(편집기가 쓴 `app: LDtk` 등), 모르는 키, 키 순서, 다른 층(`Collision`, `Build`, `Door`, `Furniture`)과
  엔티티·필드, 층 인스턴스의 다른 키(`autoLayerTiles`, `seed` …), 타일셋의 다른 키, `maps.json`. JSON 값으로 그대로 다시 쓴다
  (들여쓰기·줄바꿈만 이 도구의 모양이 된다). 타일 목록이 순서만 다르면 바뀐 것으로 치지 않는다.
- 레벨 크기는 `Collision`의 `__cWid`·`__cHei`를 따른다(편집기에서 레벨을 키워도 된다).
- 벽 모양·바닥·`face_height`·`band`·`top`(없으면 벽 모양의 기본 띠 색)은 같은 폴더의 `tiles.slots.json`에서, 도착 지점은 `maps.json`(레벨 이름이 같은 맵의 첫 도착 지점)에서,
  문은 `Door` 엔티티(`maps.json`의 `doorEntity`·`doorField`, 기본 `Door`·`Where`)에서 읽는다.
- **거절(아무것도 쓰지 않는다)**: 레벨이 하나가 아니거나 `Collision`(IntGrid)·`Wall`(Tiles)·`Ground`(Tiles) 층이 없거나 종류가 다르거나
  칸 크기가 32가 아니거나 `Wall`·`Ground` 층이나 레벨(`pxWid`·`pxHei`)의 크기가 `Collision`과 다르거나 `Wall`·`Ground`가 `tiles.png` 타일셋을 쓰지 않으면 `room-not-a-room`(끝 코드 1). 머리말로는 판별하지 않는다.
  편집기 꼴이 아닌 예전 파일(편집기가 늘 쓰는 최상위 키가 빠졌다)이면 `room-ldtk-incomplete`(끝 코드 1).
  칠한 칸에 막는 문제(닿지 못하는 바닥, 열린 가장자리, 모르는 값, 벽 안의 도착 지점·문)가 있으면 그 문제(끝 코드 1).
  벽 칸에 놓인 `Door`는 바깥 벽 문으로 본다(둘레에 닿는 바닥이 있어야 한다). `Build` 층이 있으면 그 칸 검사에 함께 든다(크기·값·고정 칸 — 막는 문제는 `repaint`도 거절하고, `repaint`는 `Build` 층 자체를 고치지 않는다).
  자리 표에 `wall`·`floor`·`face_height`가 없거나(자리 표에 방 값이 없는 옛 방) `maps.json`이 없으면 끝 코드 2 — `room build --force`로 다시 만든다.
  자리 표에 `band`가 없으면(윗면이 32px이던 때 만든 방) 끝 코드 2로 거절한다 — 옛 두께로 조용히 그리지 않는다. LDtk에서 칠한 것을 지키려면
  자리 표에 `"band": 16`을 적고 다시 `repaint`한다(옛 윗면 타일은 시트에 남지만 쓰이지 않는다). 평면에서 다시 만들어도 되면 `room build <id> --force`(칠한 것은 사라진다). `room check <.ldtk>`도 같은 이유로 끝 코드 2.
- 방 폴더에 커밋하지 않은 변경이 있으면 사용자가 LDtk 편집기에서 칠한 것일 수 있다 — `git checkout`으로 되돌리거나 `room build --force`로 덮기 전에 사용자에게 묻는다.
- 두 번 부르면 두 번째는 "바꿀 것 없음". `--dry-run`은 쓸 파일만 찍는다. 쓴 뒤에 알림(얇은 벽, 표에 없는 문)을 찍는다.

`room check <.ldtk 경로>`는 쓰지 않고 본다: `room-not-a-room`, `room-ldtk-incomplete`, `room-slots-value`(자리 표의 `wall`·`floor`·`top` 값을 받을 수 없다 — `repaint`가 거절하는 값을 같은 검사로 미리 알린다), 평면 검사와 같은 코드(위 절), `room-door-unregistered`(**알림**: `Door`
엔티티의 `Where`가 `maps.json`의 `doors`에 없다 — 게임에서 그 문이 열리지 않는다), `room-art-mismatch`(`Wall`·`Ground` 타일이 `Collision`과
어긋난 칸 수와 첫 칸 — 칸마다 `plan_tiles`가 내는 `Wall`·`Ground` 열쇠의 자리와 실제 타일을 견준다: 와야 할 타일이 없거나, 다른 자리이거나, 오면 안 되는 층에 타일이 있다. 벽 칸의 `Ground` 타일은 띠 옆으로 바닥이 비치는 칸에서만 맞다. `room repaint`로 맞춘다).

테스트 고정 파일 둘: `tests/fixtures/room-editor-saved/`는 **사용자가 LDtk 1.5.3 편집기에서 `Collision`을 칠해 저장한 실제 방**(2026-11-14)이다 —
편집기가 저장한 뒤 그때의 `repaint`가 32px 윗면으로 `Wall`·`Ground`를 다시 쓴 파일이라 들여쓰기·줄바꿈은 이 도구의 것이고 `cachedPixelData` 같은 값은 편집기의 것이다.
자리 표에만 고정 파일용으로 `"band": 16`을 적어 넣었다. `tests/fixtures/room-saved-by-editor.ldtk`는 옛 집 맵 견본 `tests/fixtures/ldtk-1.5.3/house.ldtk`의 꼴(탭 들여쓰기·키 순서)을 흉내 낸 것이다.

### 게임에서 보기와 처음 확인할 것

- 게임에서 본다: `dotnet run --project game/Rootborn.Host -- --world <맵 id> --maps art/world/rooms/<방 id>/maps.json --size 1920x1080`(인자 표는 `game/README.md`). 도착 지점은 `maps.json`의 첫 것이다.
- LDtk 편집기에서 레벨을 따로 저장(`externalLevels`)하게 하면 `repaint`가 `room-not-a-room`으로 거절한다(`.ldtk`만 다룬다).
- 문 글자(`doors`)가 있는 방은 `maps.json`의 `doors` 표를 손으로 채우기 전에는 게임에서 그 문이 열리지 않는다(알림 `room-door-unregistered`).
- **편집기에서 확인된 것**(2026-11-14, 사용자가 LDtk 1.5.3으로): 처음에는 `Cannot read properties of null (reading 'externalLevels') (TypeError)`로 열리지 않았다 —
  게임이 읽는 키만 쓰고 편집기가 기대하는 키를 많이 빠뜨렸다. 공식 스키마(1.5.3)의 필수 키와 편집기 저장 파일에 늘 있는 키를 채운 뒤
  (가) 도구가 쓴 파일이 **편집기에서 열렸고**, (나) 편집기에서 `Collision`을 칠해 저장한 파일을 **`repaint`가 받아 벽 그림을 맞췄다**(32px 윗면 때 한 번,
  얇은 벽(16px 띠)으로 바뀐 뒤에도 한 번 — 어긋난 칸 26개가 `repaint` 뒤 문제 없음), (다) `repaint`가 다시 쓴 파일을 편집기가 **다시 열었다** — 32px 윗면 때의 파일로,
  그리고 **얇은 벽으로 `repaint`가 다시 쓴 파일도 다시 열려 얇은 띠로 보였다**(2026-11-14 사용자 확인).
  편집기에서 `Collision` 층이 불투명하면 벽·바닥 그림을 가려 색 칸만 보인다 — 눈 아이콘을 끄면 그림이 보인다(그래서 반투명으로 바꿨다).
  **2026-11-14 사용자가 더 확인한 것**: 반투명(`displayOpacity` 0.35) `Collision` 층을 켠 채로도 그 아래로 그림이 비친다. 편집기 쪽 확인은 모두 끝났다.
- 벽 모양·띠 색·바닥은 위 "방 만들기 — 벽 모양·띠 색·바닥". 아직 없는 것: 한 맵 안에서 방마다 다른 벽·바닥(지금은 맵 하나에 한 종류씩),
  사용자가 직접 그린 PNG를 벽·바닥 타일로 넣기(다음 작업), 게임 안에서 플레이어가 벽·바닥을 고르기(게임 기능 — 설계 전).
  걸어서 지나는 벽에 박힌 문과 벽의 옆으로 기운 면은 없다.
- 절차는 스킬 `pixel-kit-room`.

## 방 만들기 — 시험 표 (`room vectors`, 픽셀 출처표)

벽 그림을 고르는 규칙은 키트(Python, 위 "방 만들기 — 벽 타일")와 게임(C#, `Rootborn.Core.Building`) 두 곳에 있다. 게임이
플레이어가 고른 벽지·바닥으로 집을 다시 그리려면 칸마다 "이 칸의 어느 픽셀이 무엇에서 오는가"를 알아야 하고, 띠 타일을 종류마다
미리 그려 둘 수는 없다(띠 모양이 끝없이 늘어난다). 그래서 규칙은 칸마다 **픽셀 출처표**를 내고, 게임은 조각 그림(벽지 정면 조각,
좁은 정면 조각, 띠 칠 네 색, 바닥 조각, 문지방·문짝)을 그 표대로 붙인다. 두 언어의 규칙은 같은 **시험 표**로 묶는다:
`Tools/pixel-kit/tests/fixtures/room-vectors.json`(저장소 뿌리 기준, LF 고정 — 그 폴더의 `.gitattributes`).

- `plan_sources(grid, face_height=2, band=16, wall_doors=None)` → 칸마다 `SourcePart` 목록(밖 칸은 `None`). `plan_tiles`와 같은 계산
  (`room_tiles._plan`)에서 나온다 — 고르기 규칙은 한 곳이고, 열쇠는 출처를 버린 값, 출처표는 출처를 든 값이다. `plan_room`은 둘을 한 번에.
- `python Tools/pixel-kit/kit.py room vectors`: 시험 표를 지금 규칙으로 다시 쓴다(`room_vectors.build_vectors()`, 사례는 `room_vectors.py`
  안의 글 — 방 평면 파일이 바뀌어도 흔들리지 않게). `room vectors --check`: 쓰지 않고 견주기만, 다르면 끝 코드 1. **손으로 고치지 않는다.**
- 규칙(`room_tiles.py`의 고르기·출처표)을 고치면 시험 표를 다시 만들어 함께 커밋한다. 열쇠가 일부러 바뀌었으면 `test_room_vectors.py`의 `PLAN_TILES_BEFORE`(바꾸기 전 커밋의 열쇠 해시 표)도 고친다. 안 하면 `test_room_vectors`
  (`test_vectors_file_is_up_to_date`)가, 시험 표만 바뀌면 C# 쪽 시험이 깨진다. CI가 둘 다 돈다(`Scripts/ci/README.md`).
- 증명: `test_room_vectors`의 참조 조합기(`compose_cell`)가 출처표 + 조각으로 칸을 다시 만든 그림이, 같은 칸의 `draw_tile(Wall 열쇠)`를
  `draw_tile(Ground 열쇠)` 위에 얹은 그림과 **픽셀까지 같다** — 시험 표의 모든 사례 × 모든 벽 모양. 두 방의 바닥을 다르게 주면 세로 벽 띠 칸의
  왼쪽·오른쪽이 각자의 바닥으로 그려지는 것도 본다(키트의 `Ground`는 칸에 한 장이라 그 경우를 그리지 못한다 — 출처표가 더 많이 말한다).

### 파일 꼴

```text
{ "about": "…", "version": 1, "cell": 32,
  "sources": { "<src>": { "field": "shade" | "piece", "own": true | false }, … },
  "cases": [ { "id", "faceHeight", "band", "grid": ["#..", …], "doors": [[x, y], …],
               "cells": [ { "x", "y", "kind", "wall", "ground", "parts": [ { "src", "shade"?, "piece"?, "own"?, "px" }, … ] }, … ] }, … ] }
```

- `grid`: 줄마다 한 글, `#` 벽(1), `.` 바닥(2), 빈칸 밖(0). `grid[y]`의 `x`번째 글이 칸 (x, y). 줄 길이는 같지 않을 수 있다(없는 칸은 밖).
- `doors`: 벽 칸의 문 칸 (x, y)들(키트 `plan_tiles`의 `wall_doors`, LDtk에서는 벽 칸에 놓인 `Door`). 없으면 `[]`. 문 칸의 그림(문지방·문짝)은
  격자만으로 정해지지 않아서 사례마다 적는다.
- `cells`: 밖이 아닌 칸만, 줄 순서(y, 다음 x). `kind`는 `classify`의 값(정면 열쇠는 좁은 정면 옆에서 끝 표시를 떼기 **전**), `wall`·`ground`는
  `plan_tiles`의 열쇠(없으면 `null`) — 사람이 읽고 Python 쪽만 견준다. 게임은 `parts`만 쓴다. `kind`는 떼기 전 분류라서 `face` 부분의 `piece`와 같은 글이 아닐 수 있다 —
  게임은 `kind`로 `piece`를 끌어내지 않고, `piece`를 조각 시트를 찾는 불투명한 열쇠로만 쓴다.
- 모든 사례가 `faceHeight` 2, `band` 16이다(`room_vectors.py`의 `FACE_HEIGHT`·`BAND`). 시험 표는 다른 값을 지키지 않는다. 다른 값을 쓸 때는 사례를 더한다.
- `parts`: 아래 "출처 종류". 값이 없는 필드(`shade`·`piece`·`own`)는 적지 않는다(`own`이 없으면 주인이 없는 부분 — C#에서는 `(0, 0)`으로 읽어도 된다).
- 줄 나눔: 머리 값은 한 줄에 하나, 사례는 머리 한 줄 + 칸 하나가 한 줄(규칙을 고쳤을 때 바뀐 칸이 diff에서 한 줄). 키 순서는 위 그대로, `ensure_ascii=False`,
  LF, 끝 줄바꿈. 같은 코드면 같은 바이트다.

### 좌표와 사각형

- 맵 칸 좌표는 (x 오른쪽 +, y **아래로** +). 칸 안 픽셀 좌표는 그 칸의 왼쪽 위가 (0, 0), x 오른쪽 +, y 아래로 +, 0..31.
- 사각형은 **반열린 구간**: `x0,y0,x1,y1`은 `x0 ≤ x < x1`, `y0 ≤ y < y1`의 픽셀.
- `px`는 픽셀 집합의 **정규형 글**(`room_tiles._encode(픽셀, 0, 32)`, 열쇠의 사각형 글과 같은 규칙): 줄(y)마다 그 줄 픽셀을 가로로 이어진
  구간들(왼쪽부터, 끝 제외)로 나누고, 위아래로 이어진 줄들의 구간 목록이 **똑같으면** 한 띠로 묶는다. 띠를 위에서 아래로, 띠 안에서는 구간을
  왼쪽에서 오른쪽으로 `x0,y0,x1,y1`로 적고 `+`로 잇는다. 예: `"8,0,9,32+23,0,24,32"`(두 열), `"0,16,32,17+0,17,1,31+0,31,32,32"`(ㄷ자 테).
- 한 칸의 부분들은 **겹치지 않는다**. 합집합은 그 칸의 최종 그림(Wall 타일을 Ground 타일 위에 얹은 것)의 불투명 픽셀 전부이고, 어느 부분도
  덮지 않은 픽셀은 **투명**이다(비어 있음은 적지 않는다). 그래서 칠하는 순서는 상관없다.
- 부분 순서: `src`, 그다음 `shade` 또는 `piece`, 그다음 `own`(없는 것 먼저, 있으면 dx 다음 dy 수 순), 그다음 `px`. 글은 코드 값 순(C# `string.CompareOrdinal`). `src` 글의 순서는 `band`, `door`, `face`, `floor`, `narrow`, `sill`이다.

### 출처 종류 (`src`)

| `src` | 필드 | 주인 `own` | 그 픽셀 |
|---|---|---|---|
| `band` | `shade`: `o` 외곽선, `d` 안쪽 그늘, `f` 면, `l` 안쪽 빛 | 없음 | 띠 색(맵의 띠 색 줄 단계 0 = `d`, 1 = `f`, 2 = `l`, 외곽선 = 팔레트 `outline`) |
| `face` | `piece`: 꽉 찬 정면 조각 `face:<k>[:<표시>]` | 있음 | 주인의 벽지로 그린 그 조각의 **같은 자리** 픽셀 |
| `narrow` | `piece`: 좁은 정면 조각 `face:<k>:lr[c]@<x0>-<x1>` | 있음 | 주인의 벽지로 그린 그 조각의 같은 자리 픽셀 |
| `floor` | `shade`: 그 칸의 Ground 열쇠 `floor` / `floor:shade` / `floor:shade:<x0>-<x1>` | 있음 | 주인의 바닥 조각 `floor`의 같은 자리 픽셀. 단 그림자 범위(`floor:shade`면 x 0..32, `floor:shade:<x0>-<x1>`면 x `[x0, x1)`) × y 0..1 픽셀은 `floor:shade` 조각에서 |
| `sill` | `piece`: 문지방 조각 `sill:<n\|e\|w>:<a>-<b>` | 없음 | 그 조각의 같은 자리 픽셀(나무 줄 `wood.school`, 벽지와 무관) |
| `door` | `piece`: `door` | 없음 | 정면 문짝 조각의 같은 자리 픽셀(`DOOR_RECT` = x `[6, 26)` × y `[4, 31)`, 벽지와 무관) |

`sill`과 `door`는 문 칸의 그림이다(띠 칠도 벽지 조각도 아니다). 종류는 이름 → 처리 표(`room_tiles.SOURCES`, 시험 표의 `sources`)로 고른다 —
종류를 더하면 그 표에 한 줄과 처리 하나.

**조각**(게임은 조각 시트에서, 키트 시험은 `draw_tile`로 만든다 — 조각 이름이 그대로 그릴 수 있는 열쇠다). 모두 32x32 한 장이고, 출처표는 조각의
같은 자리 픽셀만 옮긴다(옮기지 않는 자리는 무엇이든 상관없다):

- 꽉 찬 정면 `face:<k>[:<표시>]`(벽 모양마다 24장: `k` 1·2 × 끝 없음·`l`·`r`·`lr` × 위 없음·`c`·`t`): 벽 모양의 정면 그림 + 표시의 선.
  `k`는 바닥에서 위로 몇 번째(1이 바닥 바로 위 — 걸레받이가 있는 아래 칸, 2가 그 위). `l` 왼쪽 끝 열이 외곽선, `r` 오른쪽 끝 열이 외곽선,
  `c` 맨 위 2줄이 천장 밑 그늘(위 칸이 윗면), `t` 맨 윗줄이 외곽선(위 칸이 벽이 아님). 늘인 정면(윗면 칸·좁은 정면 칸 옆으로 이어진 정면)은
  `l`·`r` 없는 꼴만 나온다.
- 좁은 정면 `face:<k>:lr[c]@<x0>-<x1>`: 열 `[x0, x1)`에 무늬 없는 면(벽 모양의 `narrow` 색), 양옆 열과 `k` = 1의 맨 아랫줄이 외곽선.
  띠 16이면 범위는 `0-16`·`8-24`·`16-32`, `face_height` 2이면 `face:1:lr@…`와 `face:2:lrc@…`만 나온다.
- 바닥 `floor`(바닥 명세 그대로)와 `floor:shade`(맨 위 2줄을 그 팔레트 줄의 가장 어두운 단계로).
- 문지방 `sill:<쪽>:<a>-<b>`: `<쪽>`은 바닥이 있는 쪽(`n` 위, `e` 오른쪽, `w` 왼쪽), `[a, b)`는 띠가 지나던 범위(`n`이면 줄, `e`·`w`면 열)이고 칸 끝에서
  끝까지(위 "벽 칸의 문"). 띠 16이면 범위는 `0-16`·`8-24`·`16-32`.
- 문짝 `door`: 정면 문 타일(`door:face:1…`)에서 `DOOR_RECT` 자리. 표시·벽 모양과 무관하다.
- 띠 칠 네 색: 띠 색 줄(`wall.top.*` 등, `room styles --list`)의 단계 0·1·2와 `outline`.

### 주인 칸 (`own`)

`own` = `[dx, dy]`는 그 픽셀의 **꾸밈 주인**(벽지·바닥을 정하는 바닥 칸)의 이 칸에 대한 상대 위치다(맵 칸, y 아래로 +). 주인은 **늘 바닥 칸**이다.

- 바닥 칸 자신: `[0, 0]`.
- 꽉 찬 정면 `face:<k>`: `[0, k]`(아래로 k칸이 그 벽 면이 마주 보는 바닥 — 설계서 4.2절). 좁은 정면도 같다.
- 늘인 정면: 그 픽셀이 바라본 이웃 정면 칸 (dx, dy)의 주인 = `[dx, dy + k]`(k는 그 정면의 k).
- 띠 옆으로 비치는 바닥: 그 픽셀이 바라본 이웃 바닥 칸 (dx, dy). 대각 이웃도 있다(두 변이 모두 윗면인 모서리 자리).
  그래서 **두 방 사이 세로 벽의 띠 칸은 왼쪽 비침이 `[-1, 0]`, 오른쪽 비침이 `[1, 0]`** — 서로 다른 방의 바닥이다(키트의 `Ground` 한 장 규칙과 다르다).
- 이웃 바닥에서 오지 않은 비침(벽 그림이 덮지 않는데 그 칸에 Ground가 깔려 비치는 픽셀): 문지방 칸이면 문지방이 바라보는 바닥 칸(`n` `[0, -1]`,
  `e` `[1, 0]`, `w` `[-1, 0]`) — 문지방 밖의 남은 띠 자리와, 문지방 칸이 그리지 않는 늘인 정면 자리가 이것이다. 좁은 정면 칸이면 바닥이 있는 옆
  칸(`[-1, 0]` 또는 `[1, 0]`) — 다른 옆이 밖이라 비어 있는 자리. 드물다(시험 표의 `odd-1`·`odd-2`).
- 어느 픽셀이 어느 이웃을 "바라보는지"는 `room_tiles._plan`의 칸 안 분류(띠 가운데 사각형 밖의 픽셀은 그 쪽 두 변 중 윗면이 아닌 이웃, 띠 옆
  픽셀은 옆 먼저, 둘 다 윗면이면 대각)다. 게임은 이것을 다시 계산하지 않고 시험 표와 같은 결과를 내면 된다.

### 띠 칠 (`band`의 `shade`)

맵 전체의 **띠 픽셀 지도** B(윗면 칸의 띠 자리를 모두 모은 것 — 문지방 칸은 빼고, 맵 밖은 띠가 아님)에서 띠 픽셀 p마다:

1. p의 8방향 이웃 중 하나라도 B가 아니면 `o`.
2. 아니면, p를 둘러싼 5x5에서 p와 같은 줄과 아래 두 줄(dy 0..2, dx −2..2, p 자신 제외)이 모두 B이고 위 두 줄(dy −2..−1)도 모두 B이면 `f`,
   아래쪽은 모두 B인데 위 두 줄에 B가 아닌 것이 있으면 `l`.
3. 그 밖은 `d`.

(`room_tiles._shades`. 키트는 칸마다 열쇠에 담긴 이웃 띠 픽셀로 같은 값을 센다.)

### 예

두 방 사이 세로 벽의 윗면 칸(`two-rooms-wall`의 (3, 4), 띠는 가운데 8..24):

```json
{"x": 3, "y": 4, "kind": "cap", "wall": "cap:8,-2,24,34", "ground": "floor", "parts": [
  {"src": "band", "shade": "d", "px": "9,0,10,32+22,0,23,32"},
  {"src": "band", "shade": "f", "px": "10,0,22,32"},
  {"src": "band", "shade": "o", "px": "8,0,9,32+23,0,24,32"},
  {"src": "floor", "shade": "floor", "own": [-1, 0], "px": "0,0,8,32"},
  {"src": "floor", "shade": "floor", "own": [1, 0], "px": "24,0,32,32"}]}
```

정면 위에 얹힌 가로 띠 칸(새 집 (5, 0): 바로 아래 (5, 1)이 `face:2:c`라 띠가 아래 16줄에 붙는다. 위 16줄은 맵 밖 쪽이라 투명 — 부분이 없다):

```json
{"x": 5, "y": 0, "kind": "cap", "wall": "cap:-2,16,34,32", "ground": null, "parts": [
  {"src": "band", "shade": "d", "px": "0,30,32,31"},
  {"src": "band", "shade": "f", "px": "0,18,32,30"},
  {"src": "band", "shade": "l", "px": "0,17,32,18"},
  {"src": "band", "shade": "o", "px": "0,16,32,17+0,31,32,32"}]}
```

세로 벽이 가로 벽 정면을 지나는 칸(새 집 (13, 1)): 띠 옆 8px는 양옆 정면 `face:2:c`를 늘인 것이고, 주인은 각 정면의 아래 2칸 바닥 `[-1, 2]`·`[1, 2]`.

## 방 만들기 — 꾸밈 조각 시트 (`room export-finishes`)

플레이어가 고른 벽지·바닥으로 집을 다시 그릴 때 게임(Host)이 칸마다 붙이는 **조각 그림**을 낸다(픽셀 출처표는 위 "방 만들기 — 시험 표").
입력은 `data/building/RoomFinishDefinition.json`의 `sheet`·`slot`·`art`(어느 꾸밈을 낼지)뿐이고 조각은 `draw_tile`이 그린다 — 새로 그리지 않는다.
`python Tools/pixel-kit/kit.py room export-finishes [--dry-run]`. 모듈 `pixelkit/room_export.py`, 테스트 `tests/test_room_export.py`.

- 한 시트(`sheet`) 안의 조각은 이름 순으로 **한 줄 16칸**(512px 폭), 칸은 32x32. 자리는 좌표표가 정하고 코드는 모른다 — 꾸밈을 더하면 이름 순서에 따라
  자리가 밀릴 수 있으므로 게임은 반드시 좌표표로 찾는다.
- 조각(이름은 출처표의 글과 글자 그대로 같다 — `test_room_export`와 World `RoomFinishArtTests`가 시험 표의 모든 조각 이름으로 확인한다):
  - 벽(`slot` wall) 꾸밈마다 36장 `<art>/…`: 꽉 찬 정면 24(`face:<k>[:<표시>]`, k 1·2 × 끝 없음·`l`·`r`·`lr` × 위 없음·`c`·`t`),
    좁은 정면 12(`face:<k>:lr[c]@<x0>-<x1>`, k 1·2 × 범위 `0-16`·`8-24`·`16-32` × `c` 없음·있음).
  - 바닥(`slot` floor) 꾸밈마다 2장 `<art>/floor`, `<art>/floor:shade`(맨 위 2줄이 그림자).
  - 꾸밈과 무관한 10장 `common/…`: 문지방 `sill:<n|e|w>:<a>-<b>` 9(바닥이 있는 쪽 3 × 범위 3 — 시험 표에 나오지 않는 것도 평면에 따라 나온다),
    문짝 `door`(정면 문 타일의 `DOOR_RECT` 자리만, 나머지는 투명). 나무 줄 `wood.school`이라 벽지와 무관하다.
  - 띠 칠은 그림이 아니라 색 값이다: 좌표표의 `bands`에 띠 색 줄(`wall.top.*`, `wall.plaster.top`) 5개 × `o` 외곽선·`d`·`f`·`l`(`#RRGGBB`).
- 좌표표 꼴: 가구와 같은 `image`·`width`·`height`·`pixelsPerUnit` 32·`origin` `top-left`·`sprites`(이름 → `x`·`y`·`w`·`h`)에
  `band`(시트가 받는 띠 두께 16)와 `bands`를 더했다.
- 띠 두께가 16이 아닌 벽 모양은 거절한다(`room-finish-band`). 그 밖의 거절: `room-finish-defs`(정의 파일을 못 읽음), `room-finish-slot`·`-art`·`-sheet`·`-id`
  (모르는 slot·벽 모양·바닥, 빈 값), `room-finish-duplicate`(같은 id 두 번). 거절되면 아무것도 쓰지 않고 끝 코드 1. 같은 입력은 같은 바이트이고
  바뀔 것이 없으면 쓰지 않는다. 쓸 때는 다른 export처럼 임시 파일에 먼저 쓴 뒤 바꿔친다.
- 증명: 내보낸 PNG·좌표표에서 조각을 다시 잘라 참조 조합기(`test_room_vectors.compose_cell`)에 넣어도 시험 표의 모든 사례·칸이
  `draw_tile(Wall 열쇠)`를 `draw_tile(Ground 열쇠)` 위에 얹은 그림과 픽셀까지 같다(벽 꾸밈 5종 전부, 두 방 사이 세로 벽의 두 바닥, 바닥 꾸밈 5종).

## 방 만들기 — 집 맵과 꾸밈을 고치는 순서

게임의 집은 키트 방이다(`art/kit/rooms/house.json` → `art/world/rooms/house/`, 게임 맵 목록 `game/world/maps.json`의 `House`). 아래는 세 가지 일의 순서다. 스킬 `pixel-kit-room`이 같은 순서를 적는다.

### 집 맵을 고칠 때

크기, 도착 지점, 고정 지점(일과 판, 하루 끝, NPC 자리, 현관)의 칸을 바꾸는 일이면 먼저 스킬 `save-compat-change`를 따르고 사용자에게 묻는다(옛 세이브의 가구 자리와 `game/world/interactions.json` 좌표가 칸 좌표다). 그 밖에 벽·바닥 칸이나 `Build` 값만 고치는 일은 아래 순서다.

1. **평면을 고치고 `room build`로 다시 만들 때**: `art/kit/rooms/house.json`을 고친다 → `python Tools/pixel-kit/kit.py room check house` → `room build house --force --dry-run`으로 쓸 파일 보기 → `room build house --force`. **`--force`는 LDtk에서 손으로 고친 것(칠한 `Collision`·`Build`, 놓은 가구, 고친 문)을 지운다.** 지워도 되는지 `git diff`·사용자에게 먼저 본다.
   `room check house`에 알림 `room-door-unregistered (14, 17)` 하나가 나오는 것은 정상이다(방 폴더 `maps.json`의 `doors`는 비어 있고, 문 목적지는 게임 `game/world/maps.json`의 `House` 항목이 가진다). 끝 코드는 0이다.
2. **LDtk에서 `Collision`·`Build`를 칠했을 때**: 편집기에서 저장하고 닫는다 → `room check <.ldtk>` → `room repaint <.ldtk> --dry-run` → `room repaint <.ldtk>` → `room check <.ldtk>`. `Wall`·`Ground`는 손으로 칠하지 않는다.
3. 평면의 `frameId`를 올렸으면 방 폴더 `maps.json`(`room build`가 쓴다)과 `game/world/maps.json`의 `House` `build`를 같은 값으로 맞춘다. 같은지는 키트 시험 `test_room_ldtk.HouseRoomTests`가 본다.
4. 테스트: 키트 테스트 전체(`python -m unittest discover -s Tools/pixel-kit/tests -t Tools/pixel-kit`), World `HouseMapTests`·`MapContractTests`, Host `HouseMapHostTests`·`HouseTilesHostTests`(`dotnet test game/tests/Rootborn.Host.Tests --filter "House"`). 게임 화면을 스크린샷으로 직접 열어 본다(빈 칸, 겹친 그림, 막힌 현관).
5. **확인 못 한 것**: `Build` 층과 벽 문이 든 새 집 파일을 LDtk 편집기에서 연 것은 아직 아무도 확인하지 않았다(스키마 검사 테스트만 있다). 편집기에서 확인된 것은 `Collision`을 칠한 방뿐이다(스킬 `pixel-kit-room` "편집기에서 확인된 것").

### 벽 그림 규칙을 고칠 때

벽 그림을 고르는 규칙은 Python(`pixelkit/room_tiles.py`)과 C#(`game/Rootborn.Core/src/Building/`의 `RoomTilePlan` 등) 두 곳에 있고 시험 표 `room-vectors.json`이 둘을 묶는다. **한쪽만 고치지 않는다.**

1. Python 실패 테스트를 먼저 쓴다(`test_room_tiles`, `test_room_vectors`).
2. `room_tiles.py`를 고친다.
3. `python Tools/pixel-kit/kit.py room vectors`로 시험 표를 다시 만든다(손으로 고치지 않는다). 열쇠가 일부러 바뀌었으면 `test_room_vectors.py`의 `PLAN_TILES_BEFORE`도 고친다.
4. C# `Rootborn.Core.Building`을 같이 고친다. `dotnet test game/tests/Rootborn.Core.Tests --filter "FullyQualifiedName~Building"`이 시험 표를 읽어 모든 칸의 출처가 같은지 본다(파일은 빌드할 때 `Fixtures/room-vectors.json`으로 복사된다).
5. 키트의 방 그림과 조각 시트가 달라졌을 수 있다: 만든 방(`art/world/rooms/*`)을 다시 만들 일인지 `room check`로 보고, `room export-finishes --dry-run`으로 조각 시트가 바뀌는지 본다. 바뀌면 `room export-finishes`와 함께 커밋한다.
6. 두 쪽 테스트: 키트 테스트 전체, Core, World(`RoomFinishArtTests`, 집이 키트 그림과 픽셀까지 같은 `HouseMapTests`의 조합 시험), Host `HouseTilesHostTests`. CI(`pixel-kit-tests` job과 dotnet 테스트)가 둘 다 돌지만 GitHub에서 실제로 돈 것은 아직 확인하지 못했다.

### 꾸밈을 더할 때

`data/README.md` "집 짓기 `building/`"의 순서를 따른다: 키트에 벽 모양·바닥이 있는지 → `data/building/RoomFinishDefinition.json`에 줄 → `room export-finishes`(`--dry-run` 먼저) → 문구 두 언어 → 테스트. 코드는 고치지 않는다.

## 같이 바뀌는 색

`style.json`에는 일부러 **같은 hex를 갖게 한 줄 세 쌍**이 있다(색 수를 줄이려고 합친 것이고, 색 한도 때문은 아니다). 하나를 바꿀 때는 짝도 같은 값으로 바꾼다
(`tests/test_style.py`의 `test_paired_colors_stay_equal`이 지킨다).

| 짝 | 왜 합쳤나 |
|---|---|
| `red` 줄 전체 = `awning.red` 줄 | 케이크 체리·샌드위치 토마토의 빨강을 차양의 빨강과 같게 |
| `paper.1` = `trim.2` | 머핀 종이컵의 밝은 면을 창틀 크림과 같게 |
| `bread.3` = `wood.warm.4` | 빵의 가장 밝은 단계를 나무의 가장 밝은 단계와 같게 |

- **왜**: 1단계에서 문 앞 계단을 따뜻한 돌(`stone`, 3색)로 바꾸면서 평지붕 외관이 그때의 한도(붙인 부품의 색까지
  셌다)를 넘어, 거의 같던 이 세 쌍을 합쳐 3색을 비웠다. 지금은 색 수가 몸통만 세므로 한도 때문에 합쳐 둘 이유는
  없지만, 합친 채로 둔다(가르면 그림이 바뀐다). 짝 중 하나만 바꾸면 그림의 색이 어긋나 보이므로 테스트가 막는다.
- **곁효과**: 건물의 그늘(`building.py`의 `_Pal.down`)은 색을 "그 색을 가진 첫 줄"의 어두운 단계로 옮긴다
  (`_steps`, 줄 순서는 지붕·벽·문·trim·wood.warm·유리·plinth·metal·bread·계단). 그래서 그늘진 자리(예: 진열창 유리
  윗부분)에 들어간 `bread.3` 점은 빵 줄이 아니라 **나무 줄**을 따라 어두워지고(`wood.warm.3`), `paper.1` 점은
  **trim 줄**을 따라 어두워진다(`trim.1`). 짝을 다시 가르면 이 그늘 색도 달라진다.
- 짝을 가르려면 그 그림에서 다른 색을 하나 합쳐 자리를 만든 뒤 테스트의 짝 목록과 이 표를 함께 고친다.

## 검사 코드

| 코드 | 막는 것 |
|---|---|
| `outline-open` | 외곽선이 끊겨 그림 칸이 투명과 바로 맞닿는다 |
| `off-palette` | `style.json` 팔레트에 없는 색 |
| `too-many-colors` | 그림 하나의 색이 몸통 색 수가 한도(`max_colors`: 가구 16, 건물 32, 타일 12, 부품 10)를 넘는다. 몸통 = 부품을 투명하게 두고 그린 그림(`recipes.ghost_ctx`로 같은 명세를 한 번 더 그린다. 만드는 부품이 직접 그린 접촉 그림자·쟁반·선반 판은 몸통이고 부품의 픽셀만 사라진다). 그 색에서 외곽선을 뺀 수를 센다. 메시지에 몸통 색 수와 한도가 적힌다 |
| `orphan-pixel` | 이웃과 이어지지 않은 한 점 얼룩 |
| `not-cell-multiple` | 캔버스가 32의 배수가 아니다 |
| `overflow` | 내용이 캔버스 가장자리에 닿았다(외곽선이 들어갈 자리가 없다) |
| `not-bottom-center` | 그림이 캔버스의 아래 가운데에 있지 않다 |
| `tile-seam` | 타일 무늬의 주기가 타일 크기에 나누어떨어지지 않아 이음매에서 잘린다 |
| `tile-transparent` | 타일에 투명한 픽셀이 있다(첫 픽셀 위치와 개수). 타일은 art_cells만큼 무늬로 빈틈없이 채운다. 방의 띠·좁은 정면 타일은 띠·정면 자리에 투명 픽셀이 있을 때 |
| `room-tile-leftover` | 방의 띠·좁은 정면 타일의 남는 자리(투명해야 한다 — 바닥은 `Ground` 층이 비친다)에 칠한 픽셀이 있다 |
| `room-tile-empty` | 방의 벽 타일이 완전히 투명하다 |
| `room-door-no-floor` | 방의 벽 문(`wallDoors`, LDtk에서는 벽 칸에 놓인 `Door`) 둘레에 도착 지점에서 걸어 닿는 바닥이 없다 |
| `room-build-ragged` | 방의 `build` 줄 수·길이(LDtk에서는 `Build` 층 크기·값 개수)가 평면(`Collision`)과 다르다 |
| `room-build-char` | 방의 `build`에 `0`·`1`이 아닌 글자(LDtk에서는 값)가 있다 |
| `room-build-fixed` | 밖 칸, 도착 지점, 문 칸, 벽 문과 그 둘레 바닥 칸이 지을 수 있음(`1`)이다 |
| `room-build-no-frame-id` | 방 평면에 `build`가 있는데 `frameId`(테두리의 판 이름)가 없다 — 게임이 그 방의 `maps.json`을 거절한다 |
| `room-build-no-finish` | 짓는 집(`build`)의 벽 모양·바닥에 맞는 꾸밈 정의(`data/building`)가 없다 — 게임이 기본 꾸밈으로 조합해 그리므로 `room build`가 쓰지 않는다 |
| `room-frame-id-without-build` | 방 평면에 `build` 없이 `frameId`만 있다 |
| `slot-overlap` | 칸에 넣은 물건이 서로 너무 겹친다(좁은 쪽 폭의 1/3 넘게) |
| `slot-clipped` | 칸에 넣은 물건이 칸·판·계산대보다 크다 |
| `roof-overlap` | 지붕 위 물건끼리 겹치거나 난간·지붕 끝에 걸치거나 간판과 겹친다 |
| `facade-out` | 문틀·창(양옆 볼 포함)·간판·벽등이 벽(또는 그림) 밖으로 나간다 |
| `facade-overlap` | 문틀·창(벽 그림자 포함)·벽등이 서로 겹친다(간판은 문·창과 닿을 때) |
| `side-face` | 건물의 지붕 줄이 그린 너비 전체에 차 있지 않다(잘린 모서리 = 옆면). 건물이 `_layout`을 줄 때만 검사 |

검사는 "쓸 수 있다"의 바닥이다. 보기 좋은지는 `sheet`·`scene` 판을 직접 열어 본다.

## 테스트

```bash
python -m unittest discover -s Tools/pixel-kit/tests -t Tools/pixel-kit -v
```

CI(`.github/workflows/ci.yml`의 `pixel-kit-tests`)와 `bash Scripts/ci/run-all.sh`(전체)가 이것과 `kit.py lint`를 돈다. 방 고르기 규칙을 고치면
`python Tools/pixel-kit/kit.py room vectors`로 시험 표를 다시 만들어 함께 커밋한다(아래 "방 만들기 — 시험 표").

만드는 부품의 동작을 바꾸면 실패하는 테스트부터 쓴다. 팔레트 값·부품 행·명세 내용만 바꿀 때는 새 테스트가 없어도
되지만 전체 테스트와 `kit.py lint`(끝 코드 0)를 지켜야 한다. 테스트는 그림의 특정 픽셀 모양보다 구성(크기, 쓰는 색 줄,
그림자가 시작하는 곳)을 본다.

## 깨면 안 되는 화풍 규칙

- **유리와 금속은 사선 반사 줄.** 평평한 색 면으로 두지 않고 `glass.streak`처럼 같은 줄의 밝은 단계로 비스듬한 줄을 긋는다(진열창, 유리 진열대).
- **색 한도는 몸통만 센다.** 칸에 넣은 부품의 색은 세지 않고 부품 한도(10)로 따로 본다. 몸통은 부품을 투명하게 두고 다시 그린 그림(유령 렌더)에서 센다.
- **정면만.** 건물의 옆면(옆벽, 지붕 옆 비탈, 차양 옆면)을 그리지 않는다. 입체감은 정면에 얹은 것과 지붕 면적으로 낸다.
- **팔레트 키만.** 부품·명세·코드에 hex 색을 적지 않는다. 색은 `style.json`에만 있고 `재질.단계`로 부른다.
  그늘은 색을 곱하지 않고 같은 줄의 다른 단계다(장면에 깔리는 그림자만 예외).
- **그림자는 물체가 끝나는 곳에서 시작하는 짧은 띠.** 바닥 물건은 열마다 물체 바로 밑에서, 지붕 위 물건은 오른쪽
  3열·아래 2줄. 옆으로 눕힌 긴 그림자나 물체 밖으로 삐져나온 네모를 만들지 않는다.
- **캔버스는 32의 배수.** 그림은 아래 가운데, 나머지는 투명. 확대는 보기용(정수배, 최근접)만.
- 외곽선은 렌더러가 마지막에 입히는 한 색(`outline`). 사람이 그리지 않는다.
- 간판·메뉴판에 글자를 쓰지 않는다(다국어). 남의 그림(LimeZu 등) 조각을 넣지 않는다.
- 렌더는 결정적이다. 시각·난수를 쓰지 않고 변화는 좌표 해시나 명세의 값에서 온다.
