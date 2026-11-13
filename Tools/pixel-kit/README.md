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
   잘못된 값은 `RecipeError(f"{spec['id']}: …")`.
4. 칸 안에 물건을 늘어놓을 때는 `furniture.pile()`을 쓴다(겹침·잘림 문제를 알린다).
   지붕 종류는 `@roof(...)`, 지붕 위 물건은 `@roof_item(...)` 표에 등록한다(`building.py`).

## 조립 명세 더하기

`art/kit/recipes/<세트>/*.json`(명세 배열)에 한 덩어리.

```json
{"id": "bakery.shelf", "kind": "furniture", "part": "furniture.shelf", "cells": [2, 1], "art_cells": [2, 3],
 "tiers": [["bread.loaf", "bread.loaf"], ["bread.baguette"]],
 "states": {"empty": {"tiers": [[], []]}}}
```

- `kind`: `building` · `furniture` · `prop` · `tile` · `scene`. 색 수 한도와 검사 종류가 이것으로 정해진다.
- `part`: 만드는 부품 이름. 나머지 키는 그 부품이 읽는 값이다(부품 함수의 설명 참고).
- 크기는 `art_cells: [w, h]`(칸) 또는 `pixels: [w, h]`(32의 배수) 중 하나. 그림은 그 캔버스의 아래 가운데.
- `states`: 이름 → 덮어쓸 값. 같은 가구의 다른 모습(빈 진열대, 다른 빵)은 새 명세가 아니라 상태로 둔다.
  `render id@상태`, 장면에서는 `"state": "이름"`.

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
| `slot-overlap` | 칸에 넣은 물건이 서로 너무 겹친다(좁은 쪽 폭의 1/3 넘게) |
| `slot-clipped` | 칸에 넣은 물건이 칸·판·계산대보다 크다 |
| `roof-overlap` | 지붕 위 물건끼리 겹치거나 난간·지붕 끝에 걸친다 |

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
