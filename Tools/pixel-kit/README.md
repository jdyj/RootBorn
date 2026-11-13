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

공통 인자: `--kit 폴더`(기본 `art/kit`), `--out 폴더`(기본 `output/pixel-kit`), `--scale N`(보기용 확대 배율,
정수·최근접. 기본 render 6, sheet·scene 4, parts 8).

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
| 방 평면 파일 | `art/kit/rooms/<id>.json` |
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
- `--out`에 이미 있는 `maps.json`이 이 도구가 쓴 것(맵이 `PixelKitTest` 하나)이 아니거나 `.ldtk`가 이 도구가 만든 것(헤더 `app: pixel-kit`)이 아니면 덮어쓰지 않고 끝 코드 2(예: `--out game/world`는 거절된다).
- 같은 내용이면 쓰지 않는다. 시트가 없거나 `npcWalker`가 원본에 없으면 끝 코드 2.

## 방 만들기 — 벽 타일

`pixelkit/room_tiles.py`. 칸마다 벽/바닥/밖만 정한 격자(`grid[y][x]`, 값 `OUT, WALL, FLOOR = 0, 1, 2`, 격자 밖은 `OUT`)에서
둘레 칸을 보고 32x32 타일을 고르고 그린다. 설계는 `docs/superpowers/specs/2026-11-13-room-builder-design.md` 4.2절.
평면 파일 읽기와 LDtk 쓰기는 다음 절이다.

- `classify(grid, face_height=2)` → 칸마다 타일 열쇠(아래 표) 또는 `None`(밖).
- `draw_tile(style, key, wall, floor_spec)` → 32x32 한 장. `wall`은 벽 모양 이름, `floor_spec`은 키트의 바닥 타일 명세
  (예: `bakery.tile.floor`, 32x32 — 바닥은 그 명세를 그대로 그린다). 모르는 벽 모양·열쇠는 `RecipeError`(있는 모양을 적는다).
- `lint_room_tile(canvas, key, style)` → 문제 목록: `room-tile-size`(32x32가 아님), `off-palette`, `too-many-colors`(타일 한도 12),
  `tile-transparent`, 정면·바닥 열쇠는 가로 이음매 `tile-seam`(가로로만 이어 붙이므로 세로는 보지 않는다).
- 벽 모양은 `@wall_style("이름")`을 붙인 클래스 하나로 더한다: `cap`(윗면 팔레트 줄, 단계 0 안쪽 그늘 / 1 면 / 2 안쪽 빛)과
  `face(style, k, ceiling) -> Canvas`(32x32, 끝·위 외곽선 없이. `k == 1`이 바닥 바로 위 타일, `ceiling`이면 천장 밑 그늘 2줄).
  `wall_styles()`가 이름 목록. 지금 `paper`(줄무늬 벽지 `wall.cream` + 징두리 판 `wood.archive` + 걸레받이, 윗면 `wall.top.cream`),
  `plaster`(회반죽 `wall.plaster` + 낮은 걸레받이 `wood.school`, 윗면 `wall.plaster.top`).

| 열쇠 | 뜻 |
|---|---|
| `floor` | 바닥 |
| `floor:shade` | 바로 위 칸이 정면인 바닥. 맨 위 2줄이 그 팔레트 줄의 가장 어두운 단계(벽 밑 그림자) |
| `face:<k>` | 정면. 아래로 `face_height`칸 안에 바닥이 나오고 그 사이가 모두 벽인 벽 칸. `k`는 바닥에서 위로 몇 번째(1이 바닥 바로 위) |
| `face:<k>:<표시>` | 표시는 `l` `r` `c` `t` 중 붙는 것만 이 순서로. `l`/`r` 왼쪽/오른쪽 끝(그쪽 이웃이 같은 높이 정면도 윗면도 아님 → 그 변 열이 외곽선), `c` 위 칸이 윗면(천장 밑 그늘), `t` 위 칸이 벽이 아님(얇은 벽·맵 가장자리 → 맨 윗줄이 외곽선). 예: `face:2:c`, `face:2:lc`, `face:1:lrt` |
| `cap:<mask>` | 윗면(정면이 아닌 벽 칸). `mask`는 둘레 8칸 중 윗면인 칸의 비트 합: 북 1, 동 2, 남 4, 서 8, 북동 16, 남동 32, 남서 64, 북서 128. 대각 비트는 양옆 두 변이 모두 윗면일 때만 남겨 47가지 |

- 윗면 그림: 둘레 3x3 칸의 윗면을 픽셀 지도로 펴고, 윗면이 아닌 픽셀에 8방향으로 닿는 픽셀은 외곽선, 그 안쪽 한 줄은
  북쪽(먼 쪽) 가장자리만 빛, 나머지 가장자리는 그늘. 그래서 모서리·T자·끝에서 이웃 타일의 선이 이어진다.
- 정면 옆이 윗면이면 그 변의 외곽선은 윗면이 친다(정면은 `l`/`r`을 받지 않는다) — 선이 두 겹이 되지 않는다.
- 세로 벽은 윗면만이고 그 남쪽 끝 `face_height`칸이 정면이다. 가로 벽이 `face_height + 1`칸보다 얇으면 윗면 없이 정면만 남는다(`t`).
- 문간은 벽 줄의 바닥 칸이다. 따로 그리는 것은 없고 양옆 정면이 `l`/`r` 끝이 된다.

## 방 만들기 — 평면에서 LDtk로

`pixelkit/room_plan.py`(평면 읽기·검사)와 `pixelkit/room_ldtk.py`(쓰기). 설계는 같은 설계서 4.1·4.3·4.4절.

평면 파일 `art/kit/rooms/<id>.json`(파일 이름 = `id`):

```json
{"id": "test.two-rooms", "wall": "paper", "floor": "bakery.tile.floor", "spawn": [3, 4],
 "plan": ["####…", "#..D…"], "doors": {"D": "town"}, "face_height": 2}
```

- `plan`: 글자 줄. `#` 벽, `.` 바닥, 빈칸 밖. 줄 길이가 다르거나 모르는 글자가 있으면 문제다(조용히 채우지 않는다). `doors`(선택)에 적은 글자는 바닥 + 문이다 — 그 칸에 `Door` 엔티티가 놓이고 필드 `Where`에 값이 들어간다. `face_height`(선택, 기본 2). `spawn`은 칸 [x, y].
- `wall`은 벽 모양 이름, `floor`는 키트의 32x32 바닥 타일 명세 id(지금 나무 바닥 명세는 없어 `bakery.tile.floor`).

`room check <id>`가 보는 것(코드): `room-ragged-plan`, `room-unknown-char`, `room-open-edge`(바닥이 맵 가장자리나 밖 칸에 닿음),
`room-unreachable-floor`(도착 지점에서 걸어 닿지 못하는 바닥, 덩어리마다 하나), `room-spawn-in-wall`, `room-door-in-wall`,
`room-wall-thin`(**알림**: 정면 `face_height`칸 + 윗면 1칸보다 얇은 가로 벽. 찍기만 하고 끝 코드는 0이며 쓰기를 막지 않는다).
알림이 아닌 문제가 있으면 끝 코드 1이고 `build`는 아무것도 쓰지 않는다. 평면에 쓰인 벽 모양·바닥 명세가 키트에 없으면 끝 코드 2.

`room build <id>`는 `art/world/rooms/<id>/`(`--out`으로 바꿀 수 있다, 저장소 안이어야 한다)에 넷을 쓴다:

| 파일 | 내용 |
|---|---|
| `<id>.ldtk` | 레벨 `<맵 id>`(32px 칸). 층은 위에서 아래: `Collision`(IntGrid, 1 `wall` / 2 `floor` / 0 밖), `Door`(Entities, 엔티티 정의 `Door` + 문자열 필드 `Where`), `Furniture`(Entities, 층 정의 태그 `ysort`, 비어 있음), `Wall`(Tiles), `Ground`(Tiles). 타일셋 하나(`tiles.png`, 32). 타일 인스턴스는 `px`, `src`, `f`, `t`(시트의 타일 번호, 행 우선 16칸), `d`(칸 번호), `a` |
| `tiles.png`, `tiles.slots.json` | 쓰인 타일만 모은 시트와 열쇠 → 자리 표(`room_tiles`의 열쇠). **덧붙이기만**: 한 줄 16칸 폭 고정, 새 열쇠는 마지막 번호 다음부터. 이미 있는 타일의 자리(`t`, `src`)는 시트가 커져도 바뀌지 않는다. 자리 표에는 그 시트를 그린 `wall`·`floor`·`face_height`도 적는다(`repaint`가 여기서 읽는다 — LDtk 편집기는 이 파일을 건드리지 않는다). 시트가 다른 `wall`·`floor`로 그려져 있으면 `--force`도 거절한다(한 시트에 두 모양이 섞이지 않게) |
| `maps.json` | 이 맵 하나. `collision.rule`은 `interior-auto`, `spawns`는 도착 지점 하나 `<맵 id 소문자>-entry`(예: `testtworooms-entry`, 맵 사이에서 이름이 겹치지 않게)에 도착 칸 가운데 픽셀, `viewCells`는 레벨 높이(칸)와 13 중 작은 것, `doors`는 비어 있다(문의 목적지 표는 손으로 채운다). `walker`·`npcWalker`는 `game/world/maps.json`에서 옮긴다. 게임에는 `--maps`로 준다 |

- 맵 id = 방 id에서 점·하이픈을 지우고 낱말 첫 글자를 대문자로(`test.two-rooms` → `TestTwoRooms`).
- 칸 → 층: 벽(1)은 `Wall` 층 타일, 바닥(2)은 `Ground` 층 타일, 밖(0)은 타일 없음. 같은 격자가 `Collision`의 `intGridCsv`(행 우선, 왼쪽 위부터)다. 그림과 충돌이 한 격자에서 나온다.
- 이미 있는 방 파일(넷 중 하나라도)이 있으면 `--force` 없이는 거절(끝 코드 2). `--force`는 같은 평면이면 같은 바이트를 쓴다. `--dry-run`은 쓸 파일만 찍는다.
- **`--force`는 평면에서 통째로 다시 만든다.** LDtk에서 손으로 고친 것(다시 칠한 `Collision` 칸, 놓은 가구, 고친 문)은 지워진다(덮을 때 한 줄 알림을 찍는다). LDtk에서 손을 댄 뒤에는 `--force`가 아니라 `room repaint`를 쓴다.
- 평면에 문 글자가 있으면 알림 `room-door-unregistered`를 찍는다(쓰기는 막지 않는다): 만든 `maps.json`의 `doors`가 비어 있어 게임에서 그 문은 열리지 않는다. `doors`에 그 `Where` 값(소문자, `-`는 공백)의 목적지 맵·도착 지점을 손으로 적는다.
- 쓰기는 모든 파일을 임시 파일에 먼저 쓴 뒤 바꿔친다. 같은 입력이면 같은 바이트, LF.
- LDtk 편집기에서 열어 본 것은 아니다(편집기가 읽는 값은 LDtk 1.5.3 모양을 따라 적었지만 사용자가 열어 확인해야 한다).

## 방 만들기 — LDtk에서 칠한 뒤 (`room repaint`, `room check <.ldtk>`)

`room build`로 만든 방을 LDtk 편집기에서 열어 `Collision` 층(1 벽, 2 바닥, 0 밖)을 다시 칠하고 저장한 뒤
`room repaint <그 .ldtk 경로>`를 부르면 벽 그림이 칠한 칸을 따른다.

- **바꾸는 것**: 레벨의 `Wall`·`Ground` 층 인스턴스의 `gridTiles`(칠한 칸에서 `room_tiles.classify`로 다시 고른다), 타일셋 정의의
  `pxWid`·`pxHei`(시트가 커졌을 때), `tiles.png`·`tiles.slots.json`(새 열쇠만 덧붙인다 — 이미 있는 타일의 자리 `t`·`src`는 그대로).
- **그대로 두는 것**: 그 밖의 전부 — 머리말(편집기가 쓴 `app: LDtk` 등), 모르는 키, 키 순서, 다른 층(`Collision`, `Door`, `Furniture`)과
  엔티티·필드, 층 인스턴스의 다른 키(`autoLayerTiles`, `seed` …), 타일셋의 다른 키, `maps.json`. JSON 값으로 그대로 다시 쓴다
  (들여쓰기·줄바꿈만 이 도구의 모양이 된다). 타일 목록이 순서만 다르면 바뀐 것으로 치지 않는다.
- 레벨 크기는 `Collision`의 `__cWid`·`__cHei`를 따른다(편집기에서 레벨을 키워도 된다).
- 벽 모양·바닥·`face_height`는 같은 폴더의 `tiles.slots.json`에서, 도착 지점은 `maps.json`(레벨 이름이 같은 맵의 첫 도착 지점)에서,
  문은 `Door` 엔티티(`maps.json`의 `doorEntity`·`doorField`, 기본 `Door`·`Where`)에서 읽는다.
- **거절(아무것도 쓰지 않는다)**: 레벨이 하나가 아니거나 `Collision`(IntGrid)·`Wall`(Tiles)·`Ground`(Tiles) 층이 없거나 종류가 다르거나
  칸 크기가 32가 아니거나 `Wall`·`Ground`가 `tiles.png` 타일셋을 쓰지 않으면 `room-not-a-room`(끝 코드 1). 머리말로는 판별하지 않는다.
  칠한 칸에 막는 문제(닿지 못하는 바닥, 열린 가장자리, 모르는 값, 벽 안의 도착 지점·문)가 있으면 그 문제(끝 코드 1).
  자리 표에 `wall`·`floor`·`face_height`가 없거나(작업 2 때 만든 방) `maps.json`이 없으면 끝 코드 2 — `room build --force`로 다시 만든다.
- 두 번 부르면 두 번째는 "바꿀 것 없음". `--dry-run`은 쓸 파일만 찍는다. 쓴 뒤에 알림(얇은 벽, 표에 없는 문)을 찍는다.

`room check <.ldtk 경로>`는 쓰지 않고 본다: `room-not-a-room`, 평면 검사와 같은 코드(위 절), `room-door-unregistered`(**알림**: `Door`
엔티티의 `Where`가 `maps.json`의 `doors`에 없다 — 게임에서 그 문이 열리지 않는다), `room-art-mismatch`(`Wall`·`Ground` 타일이 `Collision`과
어긋난 칸 수와 첫 칸 — 그 칸의 값과 다른 층에 타일이 있거나, 와야 할 열쇠의 자리가 아니다. `room repaint`로 맞춘다).

편집기가 저장한 모양은 테스트 고정 파일 `tests/fixtures/room-saved-by-editor.ldtk`로 흉내 낸다(이 PC에서 편집기를 열어 본 것은 아니다 —
실제 편집기에서 칠하고 저장한 파일로 되는지는 사용자가 확인한다).

### 게임에서 보기와 처음 확인할 것

- 게임에서 본다: `dotnet run --project game/Rootborn.Host -- --world <맵 id> --maps art/world/rooms/<방 id>/maps.json --size 1920x1080`(인자 표는 `game/README.md`). 도착 지점은 `maps.json`의 첫 것이다.
- LDtk 편집기에서 레벨을 따로 저장(`externalLevels`)하게 하면 `repaint`가 `room-not-a-room`으로 거절한다(`.ldtk`만 다룬다).
- 문 글자(`doors`)가 있는 방은 `maps.json`의 `doors` 표를 손으로 채우기 전에는 게임에서 그 문이 열리지 않는다(알림 `room-door-unregistered`).
- **아직 확인하지 못한 것**: 실제 LDtk 편집기에서 만든 파일이 열리는지, 편집기가 저장한 진짜 파일을 `repaint`가 받는지, `repaint`가 다시 쓴 파일을 편집기가 다시 여는지. 사용자가 처음 해 볼 때 확인한다.
- 벽 모양은 지금 `paper`·`plaster` 둘이고 바닥은 키트의 타일 명세 id뿐이다(고르는 중인 견본은 아직 도구에 없다). 걸어서 지나는 벽에 박힌 문과 벽의 옆으로 기운 면은 없다.
- 절차는 스킬 `pixel-kit-room`.

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
| `tile-transparent` | 타일에 투명한 픽셀이 있다(첫 픽셀 위치와 개수). 타일은 art_cells만큼 무늬로 빈틈없이 채운다 |
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
