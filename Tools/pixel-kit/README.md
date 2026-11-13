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

공통 인자: `--kit 폴더`(기본 `art/kit`), `--out 폴더`(기본 `output/pixel-kit`), `--scale N`(보기용 확대 배율,
정수·최근접. 기본 render 6, sheet·scene 4, parts 8).

끝 코드: 0 깨끗, 1 검사 문제, 2 사용·데이터 오류(없는 id, 못 읽는 키트, 저장 거절).
`render`와 `lint`는 문제가 있으면 1이다. `scene`과 `sheet`는 문제를 찍거나 빨간 테두리를 그려도 항상 0이다(사용·데이터 오류와 저장 거절만 2) — 끝 코드로 거르려면 `lint`를 쓴다.

## 파일이 있는 곳

| 무엇 | 어디 |
|---|---|
| 화풍(팔레트, 외곽선 색, 그림자 세기, 색 수 한도, 칸 크기) | `art/kit/style.json` |
| 점 부품(빵, 소품) | `art/kit/parts/*.json` |
| 조립 명세(빵집) | `art/kit/recipes/bakery/*.json` |
| 만드는 부품(코드) | `Tools/pixel-kit/pixelkit/makers/{tiles,furniture,building}.py` |
| 장면·그림자 | `Tools/pixel-kit/pixelkit/{scene,shadow}.py` |
| 검사 | `Tools/pixel-kit/pixelkit/lint.py` |
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

## 같이 바뀌는 색

`style.json`에는 일부러 **같은 hex를 갖게 한 줄 세 쌍**이 있다. 하나를 바꿀 때는 짝도 같은 값으로 바꾼다
(`tests/test_style.py`의 `test_paired_colors_stay_equal`이 지킨다).

| 짝 | 왜 합쳤나 |
|---|---|
| `red` 줄 전체 = `awning.red` 줄 | 케이크 체리·샌드위치 토마토의 빨강을 차양의 빨강과 같게 |
| `paper.1` = `trim.2` | 머핀 종이컵의 밝은 면을 창틀 크림과 같게 |
| `bread.3` = `wood.warm.4` | 빵의 가장 밝은 단계를 나무의 가장 밝은 단계와 같게 |

- **왜**: 외관은 창·간판에 넣은 빵의 색까지 센다. 문 앞 계단을 따뜻한 돌(`stone`, 3색)로 바꾸면서 평지붕 외관이
  한도 40을 넘어, 거의 같던 이 세 쌍을 합쳐 3색을 비웠다. 지금 `bakery.exterior.flat`은 **40/40**이라 짝 중 하나만
  바꾸면 바로 `too-many-colors`가 난다.
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
| `too-many-colors` | 그림 하나의 색이 한도(`max_colors`: 가구 24, 건물 40, 타일 12, 부품 10, 외곽선 제외)를 넘는다 |
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

- **정면만.** 건물의 옆면(옆벽, 지붕 옆 비탈, 차양 옆면)을 그리지 않는다. 입체감은 정면에 얹은 것과 지붕 면적으로 낸다.
- **팔레트 키만.** 부품·명세·코드에 hex 색을 적지 않는다. 색은 `style.json`에만 있고 `재질.단계`로 부른다.
  그늘은 색을 곱하지 않고 같은 줄의 다른 단계다(장면에 깔리는 그림자만 예외).
- **그림자는 물체가 끝나는 곳에서 시작하는 짧은 띠.** 바닥 물건은 열마다 물체 바로 밑에서, 지붕 위 물건은 오른쪽
  3열·아래 2줄. 옆으로 눕힌 긴 그림자나 물체 밖으로 삐져나온 네모를 만들지 않는다.
- **캔버스는 32의 배수.** 그림은 아래 가운데, 나머지는 투명. 확대는 보기용(정수배, 최근접)만.
- 외곽선은 렌더러가 마지막에 입히는 한 색(`outline`). 사람이 그리지 않는다.
- 간판·메뉴판에 글자를 쓰지 않는다(다국어). 남의 그림(LimeZu 등) 조각을 넣지 않는다.
- 렌더는 결정적이다. 시각·난수를 쓰지 않고 변화는 좌표 해시나 명세의 값에서 온다.
