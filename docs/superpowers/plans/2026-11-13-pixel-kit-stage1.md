# 픽셀 키트 1단계(바탕 + 빵집 정식판) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 부품 데이터와 규칙으로 픽셀아트를 그리는 도구(`Tools/pixel-kit`)를 만들고, 그 도구로 빵집 한 벌(외관 2종, 가구 4종, 타일 3종, 실내·거리 장면)을 32px 칸에 맞춰 그려 사용자가 승인할 화풍 기준판을 낸다.

**Architecture:** 화풍은 `art/kit/style.json` 한 곳(팔레트 키, 칸 크기, 외곽선 색, 그림자 단계). 점 부품은 JSON(픽셀 행 + 팔레트 키), 만드는 부품은 Python 함수 하나 + 등록 한 줄. 조립 명세(JSON)를 렌더러가 칸 배수 캔버스에 아래 가운데로 그리고 외곽선을 마지막에 입힌다. 검사(`lint`)를 통과하지 못한 그림은 판에 "문제 있음"으로 표시된다.

**Tech Stack:** Python 3.11, Pillow 12. 테스트는 표준 `unittest`(새 의존성 없음). 이 PC에서 돈다.

**Spec:** `docs/superpowers/specs/2026-11-13-pixel-kit-design.md` (5절 구성, 6절 화풍 규칙, 7절 명령, 8절 스킬, 10절 순서의 1단계, 15절 답)

**참고 그림(이 PC에만 있다, git이 무시한다):** `output/art-spike-bakery/`의 시험 스크립트와 결과. 사용자가 승인한 판은 외관 `spike5.py`+`spike6.py`(`v6_flat.png`, `v6_pitched.png`), 실내 `spike8.py`(`v8b_interior_x5.png`), 빵 픽셀 행 `spike3.py`의 `SPR`. 만드는 부품의 모양과 비율은 이 스크립트에서 옮긴다. 시험 코드를 그대로 가져오지 않는다(색을 곱해서 만든 곳을 팔레트 키로 바꾸고, 칸에 맞춘다).

## Global Constraints

- 칸은 32px, 그림은 게임 픽셀 1:1. 확대는 보기용 판에서만 한다(정수배, 최근접).
- **모든 그림 캔버스는 32의 배수**(`art_cells` × 32). 그림은 캔버스 **아래 가운데**에 붙이고 남는 곳은 투명.
- 부품과 명세에는 hex 색을 적지 않는다. **팔레트 키**(`재질.단계`, 예 `wood.warm.3`)만 쓴다.
- 물체(타일·가구·건물·부품)의 색은 전부 팔레트 안이다. 그늘은 색을 곱하지 않고 팔레트의 다른 단계를 쓴다. 예외는 **장면에 깔리는 그림자**(바닥·벽 위)뿐이고 `style.json`의 그림자 단계 값을 쓴다.
- 외곽선은 렌더러가 마지막에 입힌다: 바깥 1px, 색은 `style.json`의 한 가지(`#3a384e`), 안쪽 칸 나눔 선도 같은 색.
- **건물의 옆면(옆벽, 지붕 옆 비탈, 차양 옆면)을 그리지 않는다.** 정면 + 내려다보이는 지붕만.
- 간판·메뉴판에 글자를 그리지 않는다(그림과 줄만).
- LimeZu 등 남의 그림 조각을 결과물에 넣지 않는다. 비교판(`output/` 아래, 커밋하지 않음)에만 나란히 놓는다.
- 렌더는 결정적이다: 같은 명세 + 같은 `style.json` → 바이트까지 같은 PNG. 시각·난수를 쓰지 않는다. 변화는 명세의 `seed`에서만.
- 1단계는 `data/`, `game/`, LDtk 파일을 건드리지 않는다(`export`는 2단계).
- 견본판·작업물은 `output/pixel-kit/`(git 무시)에 쓴다. 경로는 인자로 받고 기본값은 저장소 안 상대 위치.
- 커밋 메시지는 한글, 접두사는 `.claude/rules/commit-conventions.md`. 도구는 `[CHORE]`, 그림 데이터는 `[ASSET]`, 테스트만이면 `[TEST]`, 문서·스킬은 `[DOCS]`.
- 보고의 근거 종류: 도구 테스트는 "자동 테스트", 견본판은 "도구가 낸 그림을 직접 봄". 게임 안에서 보인다고 쓰지 않는다(1단계는 게임에 넣지 않는다).

## Review Focus

1. **명세가 없는 부품·팔레트 키를 가리킨다** → 조용히 빈 그림이 아니라 명세 id와 빠진 이름을 적은 오류. (Task 2, 3의 테스트)
2. **칸에 넣은 물건이 칸보다 크다 / 캔버스가 내용보다 작다** → 조용히 잘리지 않고 검사 문제(`slot-clipped`, `overflow`). (Task 4, 6)
3. **빈 칸·빈 층**(`tiers: [[], ["bread.loaf"]]`, 물건 0개) → 예외 없이 빈 선반으로 그려진다. 층 수가 달라도 층 높이가 남거나 모자라지 않는다(시험에서 빈 층이 한 칸 남았다). (Task 6)
4. **다리·바퀴로 떠 있는 물체** → 몸통 밑이 그림자로 채워진다. 그림자가 물체 폭 밖으로 나가지 않는다(사용자가 직접 지적한 결함). (Task 7)
5. **판 PNG가 다른 프로그램에서 열려 있어 저장이 거절된다**(오늘 실제로 났다) → 스택 트레이스 대신 어느 파일을 못 썼는지 알리고 나머지 그림은 저장한 뒤 0이 아닌 코드로 끝난다. (Task 9)

---

## File Structure

```
art/kit/style.json                     화풍 규칙 한 곳
art/kit/parts/bread.json, props.json   점 부품
art/kit/recipes/bakery/*.json          빵집 조립 명세
Tools/pixel-kit/kit.py                 명령줄 진입점(인자 풀이만)
Tools/pixel-kit/pixelkit/style.py      style.json 읽기, 팔레트 키 → 색
Tools/pixel-kit/pixelkit/canvas.py     Canvas(점·사각형·붙이기·외곽선·저장)
Tools/pixel-kit/pixelkit/parts.py      점 부품 읽기
Tools/pixel-kit/pixelkit/makers/__init__.py   만드는 부품 등록 표(register, Ctx)
Tools/pixel-kit/pixelkit/makers/tiles.py      바닥·실내 벽·보도
Tools/pixel-kit/pixelkit/makers/furniture.py  진열대·계산대·유리 진열대·빵 수레, 무더기 놓기
Tools/pixel-kit/pixelkit/makers/building.py   평지붕·기와지붕·정면(문, 내민 창, 차양, 간판, 벽등)
Tools/pixel-kit/pixelkit/recipes.py    명세 읽기, 칸 맞춤 캔버스에 그리기
Tools/pixel-kit/pixelkit/shadow.py     장면의 그림자 규칙
Tools/pixel-kit/pixelkit/scene.py      장면 조립
Tools/pixel-kit/pixelkit/lint.py       검사
Tools/pixel-kit/pixelkit/boards.py     견본판
Tools/pixel-kit/tests/                 unittest + 고정 파일(tests/fixtures/)
Tools/pixel-kit/README.md
.claude/skills/pixel-kit/SKILL.md, .agents/skills/pixel-kit/SKILL.md
```

테스트 실행: `python -m unittest discover -s Tools/pixel-kit/tests -t Tools/pixel-kit -v`

---

### Task 0: 브랜치와 설계 문서

**Files:** Create(커밋): `docs/superpowers/specs/2026-11-13-pixel-kit-design.md`, `docs/superpowers/plans/2026-11-13-pixel-kit-stage1.md`

- [ ] **Step 1:** `git status`로 미커밋 파일을 확인한다. 지금 체크아웃(`balance-simulator`)에는 이 일과 무관한 미커밋 문서가 있다 — 건드리지 않는다.
- [ ] **Step 2:** `main`에서 브랜치 `pixel-kit`을 만든다(작업 폴더를 나누면 `superpowers:using-git-worktrees`). 설계서와 이 계획서는 아직 커밋되지 않았으므로 새 작업 폴더로 따라가지 않는다 — 두 파일만 새 브랜치로 복사해 커밋한다.
- [ ] **Step 3:** 커밋 `[DOCS] 픽셀 키트 설계와 1단계 계획`. 푸시하지 않는다.

### Task 1: 화풍 파일, Canvas, 외곽선 규칙

**Files:** Create `art/kit/style.json`, `pixelkit/style.py`, `pixelkit/canvas.py`, `tests/test_style.py`, `tests/test_canvas.py`

**Interfaces — Produces:**
- `Style.load(path: str) -> Style`; `style.cell: int`(32); `style.outline: tuple[int,int,int,int]`; `style.color(key: str) -> RGBA`(없는 키는 `KeyError`에 키 이름); `style.palette: frozenset[RGBA]`(외곽선 색 포함); `style.shadow: dict[str, float]`(`under`, `front`, `edge`, `wall`); `style.max_colors: dict[str, int]`
- `Canvas(w: int, h: int)`; `.w .h`; `px(x, y, rgba)`(밖이면 무시); `get(x, y) -> RGBA`(밖이면 투명); `rect(x, y, w, h, rgba)`; `hline`, `vline`; `paste(other: Canvas, x, y)`(알파 합성); `darken(x, y, w, h, k: float)`(장면 그림자 전용); `outline(rgba) -> Canvas`; `bbox() -> tuple[x0, y0, x1, y1] | None`; `colors() -> set[RGBA]`; `save(path)`; `to_image() -> PIL.Image`; `Canvas.from_image(img) -> Canvas`

`style.json` 값(설계서 6.1·6.6절, 시험 스크립트의 색): `cell` 32, `outline` `#3a384e`, `shadow` `{"under": 0.56, "front": 0.72, "edge": 0.82, "wall": 0.8}`, `max_colors` `{"furniture": 16, "tile": 12, "building": 32, "part": 10}`. 팔레트는 재질마다 어두운 것부터 0번: `wood.warm` 5단계 + 윗면 `wood.top` 2단계, `wood.back` 2, `bread` 4, `icing.pink` 2, `cream` 3, `choco` 2, `leaf` 3, `red` 2, `yellow` 2, `terracotta` 2, `metal` 3, `metal.dark` 2, `glass.top` 2, `glass.front` 2, `glass.streak` 1, `wall.cream` 3, `floor.tile.light` 3, `floor.tile.warm` 3, `paver` 3, `brick` 3, `plinth` 3, `roof.teal` 4(평지붕 난간·바닥), `roof.slate` 4(기와), `door.teal` 3, `awning.red` 2, `awning.cream` 2, `stone` 3, `board.green` 2, `white` 1. 색 값은 `spike3.py`의 `P`, `spike.py`의 `WOOD`·`BACK`, `spike5.py`·`spike6.py`·`spike8.py`의 hex에서 옮기되 단계가 비는 곳만 사이 색을 더한다.

- [ ] **Step 1: 실패 테스트** — `test_style.py`: `cell == 32`; `color("wood.warm.0")`가 RGBA 4개; 없는 키는 `KeyError`이고 메시지에 그 키; 팔레트의 모든 값이 `#rrggbb`. `test_canvas.py`:

```python
def test_outline_fills_exactly_the_4_neighbours():
    cv = Canvas(8, 8); cv.rect(3, 3, 2, 2, RED)
    cv.outline(OUT)
    ring = {(x, y) for y in range(8) for x in range(8) if cv.get(x, y) == OUT}
    assert ring == {(3,2),(4,2),(2,3),(5,3),(2,4),(5,4),(3,5),(4,5)}   # 대각선 칸은 채우지 않는다
    assert cv.get(3, 3) == RED

def test_shape_touching_the_canvas_edge_keeps_its_pixels_and_does_not_raise(): ...
def test_paste_is_alpha_composite_and_clips_outside(): ...
def test_save_then_from_image_roundtrips_bytes(): ...   # 같은 Canvas를 두 번 저장하면 파일 바이트가 같다
```

- [ ] **Step 2:** 실행 → 모듈 없음으로 실패.
- [ ] **Step 3:** 구현. `outline`은 "투명이고 4방향 이웃 중 불투명이 있는 칸"을 먼저 모은 뒤 한꺼번에 칠한다(칠한 칸이 다시 이웃으로 번지지 않게). `save`는 PNG 메타데이터(시각 등)를 넣지 않는다.
- [ ] **Step 4:** 실행 → 통과.
- [ ] **Step 5:** 커밋 `[CHORE] 픽셀 키트 화풍 파일과 Canvas — 외곽선은 규칙이 입힌다`.

### Task 2: 점 부품

**Files:** Create `pixelkit/parts.py`, `art/kit/parts/bread.json`, `art/kit/parts/props.json`, `tests/test_parts.py`, `tests/fixtures/parts_bad_key.json`, `tests/fixtures/parts_ragged.json`

**Interfaces — Consumes:** `Style`, `Canvas`. **Produces:** `load_parts(directory: str, style: Style) -> dict[str, Canvas]`(외곽선까지 입힌 그림, 사방 1px 여유); `PartError(Exception)`(메시지에 파일과 부품 id).

부품 JSON 모양은 설계서 5.1절(`id`, `rows`, `keys`, `anchor`). `.`은 투명. `bread.json` 12종: `spike3.py` `SPR`의 `loaf`, `baguette`, `croissant`, `donut`, `muffin`, `cake`, `sandwich`, `pretzel` 8종의 행을 옮기고(id는 `bread.<이름>`), 새로 4종 `bread.bun`, `bread.toast`, `bread.roll`, `bread.pie`를 9~12px 폭으로 그린다 — 서로 실루엣이 달라야 한다. `props.json`: `prop.basket`, `prop.plant`, `prop.wall-lamp`, `prop.register`(시험보다 크게, 폭 14px 안팎), `prop.clock`.

- [ ] **Step 1: 실패 테스트** — 실제 `art/kit/parts`를 읽어: 부품 17개 id가 모두 있다; 모든 부품의 `colors()`가 `style.palette`의 부분집합이다; 빵 12종의 실루엣(불투명 마스크)이 서로 다르다; 빵은 폭 16 이하·높이 12 이하. 고정 파일: 없는 팔레트 키 → `PartError`에 부품 id와 키; 줄 길이가 다른 `rows` → 짧은 줄은 투명으로 채워 읽힌다; id가 겹치면 `PartError`.
- [ ] **Step 2:** 실행 → 실패. **Step 3:** 구현. **Step 4:** 통과.
- [ ] **Step 5:** `python`으로 부품을 8배로 모은 그림을 `output/pixel-kit/`에 임시로 내 **직접 열어 본다**. 새로 그린 4종이 무엇인지 알아볼 수 없으면 행을 고친다.
- [ ] **Step 6:** 커밋 `[ASSET] 픽셀 키트 점 부품 — 빵 12종과 소품 5종`.

### Task 3: 조립 명세와 칸 맞춤 캔버스, 만드는 부품 등록 표

**Files:** Create `pixelkit/makers/__init__.py`, `pixelkit/recipes.py`, `tests/test_recipes.py`, `tests/fixtures/recipes/*.json`

**Interfaces — Produces:**
- `Ctx`(dataclass): `style: Style`, `parts: dict[str, Canvas]`; `ctx.part(id: str) -> Canvas`(없으면 `RecipeError`)
- `register(name: str)` 데코레이터; 만드는 부품의 꼴은 `def maker(spec: dict, ctx: Ctx) -> Canvas`(외곽선 **전**의 그림, 크기 자유); `get_maker(name) -> Callable`(없으면 `RecipeError`에 등록된 이름 목록)
- `load_recipes(directory: str) -> dict[str, dict]`(하위 폴더 포함, id 겹치면 `RecipeError`)
- `render(recipe: dict, ctx: Ctx) -> Canvas`: `recipe["part"]`의 만드는 부품을 불러 그리고, 외곽선을 입히고, `art_cells`(건물은 `pixels`를 칸 배수로 적는다) × 32 캔버스의 아래 가운데에 붙인다. `kind == "tile"`은 외곽선을 입히지 않고 그대로.
- `RecipeError(Exception)`: 메시지는 항상 명세 `id`로 시작.

명세 공통 값: `id`, `kind`(`tile`|`furniture`|`building`|`prop`|`item`), `part`, `art_cells: [w, h]`, 가구는 `cells: [w, h]`, 건물은 `site: [w, h]`. 부품 자리는 **그림 왼쪽 위 기준 정수 픽셀**(설계서 15절 4번). `states`는 `{이름: 덮어쓸 값}`이고 `render(recipe, ctx, state="empty")`가 얕게 덮어 그린다.

- [ ] **Step 1: 실패 테스트** — 테스트용 만드는 부품 `test.box`(20×30 단색)를 등록해: 결과 캔버스가 `art_cells [1, 2]` → 32×64; 내용의 bbox가 아래 줄에 닿고 좌우 여백 차가 1 이하; 없는 `part` → `RecipeError`가 명세 id와 부품 이름을 담는다; `art_cells`가 없거나 정수가 아니면 `RecipeError`; `state="empty"`가 값을 덮는다, 없는 상태 이름은 `RecipeError`; 같은 명세를 두 번 그리면 `to_image().tobytes()`가 같다.
- [ ] **Step 2–4:** 실패 확인 → 구현 → 통과.
- [ ] **Step 5:** 커밋 `[CHORE] 픽셀 키트 조립 명세 — 칸 배수 캔버스에 아래 가운데로`.

### Task 4: 검사(lint)

**Files:** Create `pixelkit/lint.py`, `tests/test_lint.py`, `tests/fixtures/lint/*.png`(결함 그림과 깨끗한 그림을 테스트 코드가 Canvas로 만들어도 된다)

**Interfaces — Produces:** `Problem`(dataclass: `code: str`, `message: str`, `x: int | None`, `y: int | None`); `lint_sprite(cv: Canvas, recipe: dict, ctx: Ctx) -> list[Problem]`; `lint_tile(cv, recipe, ctx) -> list[Problem]`. 검사 코드:

| code | 조건 |
|---|---|
| `outline-open` | 불투명 칸 중 투명(또는 캔버스 밖)과 4방향으로 맞닿은 칸이 외곽선 색이 아니다 |
| `off-palette` | `style.palette`에 없는 색 |
| `too-many-colors` | 색 수 > `style.max_colors[kind]` |
| `orphan-pixel` | 8방향 이웃에 같은 색이 하나도 없는 불투명 칸(외곽선 색 제외) |
| `not-cell-multiple` | 캔버스 폭·높이가 32의 배수가 아니다 |
| `overflow` | 내용이 캔버스 가장자리에 닿아 외곽선이 잘렸다(맨 아래 줄 제외) |
| `not-bottom-center` | bbox가 아래 줄에 닿지 않거나 좌우 여백 차 > 1 |
| `tile-seam` | 타일을 3×3으로 깔았을 때 이음매 양쪽 줄의 무늬 주기가 끊긴다: 왼쪽 끝 열 = 오른쪽 끝 열 다음에 와야 할 열(구현: 타일을 가로·세로로 반 칸 민 그림에 가운데 십자를 따라 "한 줄만 있는 색 경계"가 없다) |

`slot-overlap`·`slot-clipped`는 만드는 부품이 그리면서 `ctx.problems`에 더한다(Task 6) — `Ctx`에 `problems: list[Problem]`을 더한다.

- [ ] **Step 1: 실패 테스트** — 코드마다 "그 결함만 든 그림 → 그 코드 하나가 나온다"와 "깨끗한 그림 → 빈 목록". `tile-seam`: 가로 10px 주기 무늬를 32px에 깐 타일(끊김) → 문제, 16px 주기 → 없음. Task 2의 실제 부품 전부가 `outline-open`·`off-palette`·`orphan-pixel` 없이 통과한다(걸리면 부품 그림을 고친다 — 검사를 느슨하게 하지 않는다. 한 점짜리 눈·손잡이처럼 의도한 외톨이 점은 부품 JSON의 `"allow": ["orphan-pixel"]`로만 푼다).
- [ ] **Step 2–4:** 실패 확인 → 구현 → 통과.
- [ ] **Step 5:** 커밋 `[CHORE] 픽셀 키트 검사 — 끊긴 외곽선·팔레트 밖 색·칸 어긋남·타일 이음`.

### Task 5: 타일

**Files:** Create `pixelkit/makers/tiles.py`, `art/kit/recipes/bakery/tiles.json`, `tests/test_tiles.py`

**Interfaces — Produces(등록 이름):** `tile.floor-checker`(값: `colors: [키 접두사 2개]`), `tile.wall-interior`(32×64: 벽 윗면(두께) 6px + 벽지 + 허리 몰딩 + 징두리 판 + 걸레받이, `spike8.py` `wall_cap`과 `spike.py` `tile_wall_interior`), `tile.paver`. 명세 id: `bakery.tile.floor`, `bakery.tile.wall`, `bakery.tile.sidewalk`.

- [ ] **Step 1: 실패 테스트** — 세 명세를 그려: 크기 32×32, 32×64, 32×32; `lint_tile`이 빈 목록; 투명 칸 0개; 색 수 ≤ 12.
- [ ] **Step 2–4:** 실패 확인 → 구현(무늬 주기는 32의 약수만 쓴다) → 통과.
- [ ] **Step 5:** 3×3으로 깐 그림을 4배로 `output/pixel-kit/`에 내 **직접 열어** 이음매를 본다.
- [ ] **Step 6:** 커밋 `[ASSET] 픽셀 키트 타일 — 실내 바닥·벽, 보도`.

### Task 6: 가구(칸 채우기)

**Files:** Create `pixelkit/makers/furniture.py`, `art/kit/recipes/bakery/furniture.json`, `tests/test_furniture.py`

**Interfaces — Produces:**
- `pile(cv: Canvas, items: list[str], x0: int, x1: int, base_y: int, ctx: Ctx) -> None`: 부품을 `x0..x1`에 바닥 줄 `base_y`로 촘촘히 놓는다. 겹침은 물건 폭의 1/3까지만 허용하고 넘으면 `ctx.problems`에 `slot-overlap`. 물건이 칸 높이를 넘으면 `slot-clipped`. 빈 목록이면 아무것도 그리지 않는다.
- 등록 이름과 명세(설계서 6.6절 면 나누기: 넓고 밝은 윗면 / 한 단계 어두운 앞면 / 튀어나온 것 밑 짙은 줄 / 안쪽 칸 나눔은 외곽선 색):

| 등록 이름 | 명세 id | `cells` | `art_cells` | 값 |
|---|---|---|---|---|
| `furniture.shelf` | `bakery.shelf` | [2, 1] | [2, 3] | `tiers: list[list[부품 id]]`(1~4층). **층 높이는 안쪽 높이를 층 수로 나눠 남김없이 쓴다** |
| `furniture.counter` | `bakery.counter` | [3, 1] | [3, 2] | `top: [{part, x}]`(윗면에 놓는 것, x는 정수 px). 윗면이 높이의 절반쯤 |
| `furniture.glass-case` | `bakery.glass-case` | [2, 1] | [2, 2] | `rows: list[list[부품 id]]`, 유리 윗면·앞면에 사선 반사 줄(`glass.streak`) |
| `furniture.trolley` | `bakery.trolley` | [1, 1] | [1, 3] | `rows`, 폭 30px 이하(1칸 안), 안쪽은 `metal.dark` 밝은 단계(시험은 너무 어두웠다), 기둥에 반사 줄, 바퀴 |

  `bakery.shelf`와 `bakery.glass-case`, `bakery.trolley`에 `states: {"empty": ...}`.

- [ ] **Step 1: 실패 테스트** —

```python
def test_shelf_uses_all_inner_height_for_any_tier_count():
    for n in (1, 2, 3, 4):
        cv, layout = make_shelf(tiers=[["bread.loaf"]] * n)        # layout: 층마다 (y0, y1)
        assert layout[0][0] == INNER_TOP and layout[-1][1] == INNER_BOTTOM   # 빈 띠가 남지 않는다
        assert max(h for h in heights(layout)) - min(heights(layout)) <= 1

def test_empty_tiers_and_empty_state_render_without_items_or_errors(): ...
def test_item_taller_than_tier_reports_slot_clipped(): ...            # 4층 선반에 높이 12 부품
def test_too_many_items_reports_slot_overlap(): ...                   # 한 층에 9개
def test_changing_one_tier_changes_only_that_tiers_rows(): ...        # 다른 층의 픽셀은 그대로
def test_every_bakery_furniture_recipe_is_cell_sized_and_lint_clean(): ...   # 4개 명세 × 상태, ctx.problems도 빈 목록
def test_counter_top_face_is_at_least_40_percent_of_body_height(): ...
def test_trolley_content_width_is_at_most_30(): ...
```

  만드는 부품은 층·면의 자리를 테스트가 볼 수 있게 `spec["_layout"]`에 적는다(그리기에는 쓰지 않는다).
- [ ] **Step 2–4:** 실패 확인 → 구현(`spike8.py`의 `shelf`·`counter`·`glass_case`·`trolley`·`streaks`에서 모양을 옮기되 `shade()` 곱셈을 팔레트 단계로 바꾼다) → 통과.
- [ ] **Step 5:** 네 가구 × 상태를 6배로 모은 그림을 **직접 열어 본다.** 볼 것: 빈 층, 뭉개진 무더기, 알아볼 수 없는 물건, 너무 어두운 안쪽. 고친 뒤 테스트를 다시 돌린다.
- [ ] **Step 6:** 커밋 `[ASSET] 픽셀 키트 가구 — 진열대·계산대·유리 진열대·빵 수레, 칸 채우기`.

### Task 7: 그림자 규칙과 장면

**Files:** Create `pixelkit/shadow.py`, `pixelkit/scene.py`, `art/kit/recipes/bakery/scenes.json`, `tests/test_shadow.py`, `tests/test_scene.py`

**Interfaces — Produces:**
- `under_shadow(scene: Canvas, sprite: Canvas, x: int, y: int, style: Style, rows: int = 3) -> None` — 설계서 6.6절: 열마다 그 열에서 물체가 끝나는 곳 바로 아래부터 물체의 맨 아랫줄까지 `shadow.under`, 그 아래 `rows - 1`줄 `shadow.front`, 양 끝 열은 `shadow.edge`로 한 줄 짧게. 물체의 맨 아래 4줄에 불투명 칸이 있는 열의 범위(발 사이) 안에서만.
- `hung_shadow(scene, sprite, x, y, style)` — 벽에 걸린 것: 바로 밑 2줄 `shadow.wall`.
- `build_scene(recipe: dict, recipes: dict[str, dict], ctx: Ctx) -> Canvas` — 값: `size: [칸 w, 칸 h]`, `fill: [{tile, rect: [칸 x, y, w, h]}]`, `place: [{id, state?, cell: [x, y], dx?, dy?}]`(`cell`은 그림 캔버스의 왼쪽 아래 칸, `dx`·`dy`는 정수 px 미세 이동), `hang: [{id | part, at: [px x, y]}]`. 바닥에 선 것은 아랫줄이 큰 순서로 뒤에서 앞으로 놓고 `under_shadow`를 먼저 깐다. 벽과 바닥이 만나는 줄에 `shadow.wall` 2줄.
- 명세 id: `bakery.scene.interior`(9×5칸), `bakery.scene.street`(7×8칸, Task 8 뒤에 건물을 넣는다 — 이 task에서는 보도와 풀만).

- [ ] **Step 1: 실패 테스트** —

```python
def test_floating_body_gets_shadow_under_the_body_between_the_legs():
    spr = legs_sprite(body_bottom=20, leg_bottom=25, legs_at=(2, 17), width=20)   # 다리 둘로 5px 뜬 상자
    scene = flat(40, 40, FLOOR); under_shadow(scene, spr, 10, 5, style)
    assert all(scene.get(10 + i, 5 + 22) != FLOOR for i in range(4, 16))          # 몸통 밑이 어둡다
    assert scene.get(9, 5 + 26) == FLOOR and scene.get(31, 5 + 26) == FLOOR       # 물체 폭 밖은 그대로

def test_shadow_under_body_is_darker_than_the_front_strip(): ...
def test_sprite_with_nothing_in_its_bottom_rows_casts_no_shadow_and_does_not_raise(): ...
def test_scene_canvas_is_cell_sized_and_places_by_cell(): ...       # cell [1, 3]에 놓은 64×96 그림의 왼쪽 아래 = (32, 4*32-1)
def test_scene_with_unknown_recipe_id_raises_recipe_error_naming_it(): ...
def test_scene_render_is_deterministic(): ...
```

- [ ] **Step 2–4:** 실패 확인 → 구현 → 통과.
- [ ] **Step 5:** `bakery.scene.interior`를 5배로 내 **직접 열어 본다**(진열대 2, 수레, 유리 진열대, 계산대, 바구니, 화분, 메뉴판, 시계). 볼 것: 떠 있는 물체 밑의 빈 틈, 화면 끝에 붙은 물체, 가구끼리 겹침. 메뉴판은 만드는 부품 `prop.menu-board`(글자 없는 줄)로 `furniture.py`에 더한다.
- [ ] **Step 6:** 커밋 `[CHORE] 픽셀 키트 장면과 그림자 — 물체가 끝나는 곳에서 시작하는 짧은 띠`.

### Task 8: 건물

**Files:** Create `pixelkit/makers/building.py`, `art/kit/recipes/bakery/buildings.json`, `tests/test_building.py`; Modify `art/kit/recipes/bakery/scenes.json`(거리 장면에 건물·화분·입간판), `pixelkit/makers/furniture.py`(바깥 소품 `prop.a-board`)

**Interfaces — Produces(등록 이름):**
- `building.shop` — 값: `site: [칸 w, h]`, `pixels: [w, h]`(칸 배수), `roof: {type: "flat" | "pitched", items: [{part, at: [px x, y]}]}`, `wall: {material: 팔레트 접두사}`, `door: {at_x: px, color: 팔레트 접두사}`, `window: {at_x: px, width: px, slots: [[부품 id], [부품 id]]}`(창살 양쪽), `awning: {colors: [2개]}`, `sign: {at_x: px, icons: [부품 id]}`, `lamp: {at_x: px}`.
- 지붕 종류는 `ROOFS: dict[str, Callable[[Canvas, dict, Ctx, int, int], None]]`에 등록한다(`if type ==`로 닫지 않는다). 지붕 위 물건(`roof.stair-house`, `roof.chimney`, `roof.ac-unit`, `roof.skylight`, `roof.dormer`)은 윗면 + 앞면으로 그리고 자기 그림자를 같이 낸다(`spike6.py` `tall_box`; 그림자 모양은 물체의 오른쪽 변과 아랫변에 붙은 띠, 지붕 바닥 색의 어두운 단계 — 곱셈이 아니다).
- 명세 id: `bakery.exterior.flat`, `bakery.exterior.pitched` — `site: [5, 3]`, `pixels: [160, 192]`(6칸; 시험판 184px에서 8px 늘린다 — 지붕에 4, 벽에 4).

규칙(설계서 6.5절, 모양은 `spike5.py` `facade`와 `spike6.py` `roof_flat`·`roof_pitched`): 지붕 높이 ≥ 그림 높이의 절반; 벽은 넓은 색 면 + 양 끝 벽기둥 + 처마 밑 띠; 창은 받침과 함께 6px 앞으로 꺾여 나옴(양옆 볼: 왼쪽 밝은 단계, 오른쪽 어두운 단계); 문은 두꺼운 틀 안으로 들어감; 간판은 처마 위로 솟음; 벽등은 차양과 4px 이상 떨어진다(시험에서 붙어 있었다); 옆면 없음.

- [ ] **Step 1: 실패 테스트** —

```python
def test_both_bakery_exteriors_are_160x192_and_lint_clean(): ...
def test_roof_is_at_least_half_the_picture_height(): ...            # layout["roof_bottom"] >= 96
def test_content_spans_the_full_width_with_no_side_face(): ...      # layout에 "side" 영역이 없고, 벽 왼쪽·오른쪽 끝이 캔버스 폭에서 대칭(차 ≤ 1)
def test_roof_items_do_not_overlap_each_other_or_the_parapet(): ...  # 겹치게 준 명세 → ctx.problems에 "roof-overlap"
def test_window_slots_fill_both_sides_of_the_mullion_without_crossing_it(): ...
def test_unknown_roof_type_raises_recipe_error_listing_known_types(): ...
def test_lamp_is_at_least_4px_from_the_awning(): ...
```

- [ ] **Step 2–4:** 실패 확인 → 구현 → 통과.
- [ ] **Step 5:** 두 건물을 6배로, 거리 장면을 4배로 내 **직접 열어 본다.** `output/art-spike-bakery/v6_flat_x6.png`·`v6_pitched_x6.png`(승인된 판)와 나란히 놓고 비율이 틀어졌는지, 각진 그림자·겹친 물건·세로줄 경계가 남았는지 본다.
- [ ] **Step 6:** 커밋 `[ASSET] 픽셀 키트 건물 — 빵집 외관(평지붕·기와지붕), 정면만`.

### Task 9: 견본판과 명령

**Files:** Create `pixelkit/boards.py`, `Tools/pixel-kit/kit.py`, `tests/test_cli.py`, `tests/test_boards.py`

**Interfaces — Produces:** `kit.py <명령> [id…] [--kit art/kit] [--out output/pixel-kit] [--scale N]`

| 명령 | 내는 것 | 끝 코드 |
|---|---|---|
| `render <id…>` | `<out>/render/<id>[@상태].png`(1:1)와 `<id>_x<N>.png` | 검사 문제가 있으면 1 |
| `lint [id…]`(없으면 전부) | 문제를 `id: code (x, y) message` 줄로 출력 | 문제가 있으면 1 |
| `sheet <세트>` | `<out>/sheet/<세트>.png`: 세트(명세 id의 첫 마디)의 모든 명세를 번호·id와 함께 한 장에. 검사 문제가 있는 칸은 빨간 테두리 | 0 |
| `scene <id>` | `<out>/scene/<id>.png`, `_x<N>.png` | 0 |
| `parts` | `<out>/parts.png`: 점 부품 전부를 id와 함께 | 0 |

판의 글자(번호, id)는 Pillow 기본 글꼴로 쓴다(글꼴 파일 경로를 적지 않는다). 저장이 거절되면(`OSError`) `못 썼다: <경로> (다른 프로그램에서 열려 있는지 확인)`을 stderr에 적고 나머지를 계속 저장한 뒤 2로 끝난다.

- [ ] **Step 1: 실패 테스트** — `subprocess`로 고정 명세 폴더(`tests/fixtures/kit_ok`, `kit_bad`)에: `render` → 파일이 생기고 1:1 크기가 칸 배수, 확대본은 정확히 N배; `kit_bad`의 `lint` → 끝 코드 1, 출력에 명세 id와 코드; 없는 id → 끝 코드 2와 알려진 id 목록; `--out`에 준 폴더에만 쓴다(저장소의 다른 곳에 파일이 생기지 않는다); 저장 거절(쓰기 금지 파일을 미리 만들어 둔다) → 끝 코드 2, 다른 그림은 저장됨; `sheet`를 두 번 돌리면 바이트가 같다.
- [ ] **Step 2–4:** 실패 확인 → 구현 → 통과.
- [ ] **Step 5:** 커밋 `[CHORE] 픽셀 키트 명령 — render·lint·sheet·scene·parts`.

### Task 10: 빵집 기준판

**Files:** Modify `art/kit/recipes/bakery/*.json`, `art/kit/parts/*.json`, 필요한 만드는 부품(결함 고치기); Create `Tools/pixel-kit/README.md`

- [ ] **Step 1:** `python Tools/pixel-kit/kit.py lint` → 문제 0. `sheet bakery`, `scene bakery.scene.interior`, `scene bakery.scene.street`, `parts`를 낸다.
- [ ] **Step 2:** 네 장을 **직접 열어** 설계서에 적힌 결함 목록(6.5절·6.6절 끝)을 하나씩 대조한다: 빈 층, 뭉개진 쟁반, 작은 금전 등록기, 어두운 수레 안쪽, 화면 끝에 붙은 화분, 각진 그림자, 지붕 위 물건 겹침, 벽등과 차양. 남은 것은 고치고 1단계로 돌아간다.
- [ ] **Step 3:** 색을 LimeZu 쪽으로 차분하게(설계서 6.6절 끝: 채도가 높고 따뜻했다) — `style.json`의 값만 고친다. 비교판을 만든다: LimeZu 가게(`Assets/Modern_Exteriors_32x32/ME_Theme_Sorter_32x32/9_Shopping_Center_and_Markets_32x32.png` (0,800)-(330,1065))와 빵집 소품(`Assets/moderninteriors-win/1_Interiors/32x32/Theme_Sorter_Black_Shadow_32x32/16_Grocery_store_Black_Shadow_32x32.png` (224,1085)-(512,1290))을 같은 배율로 옆에 놓은 `output/pixel-kit/compare-limezu.png`(일회용 스크립트, 커밋하지 않는다). 직접 열어 본다.
- [ ] **Step 4:** `README.md`: 명령 표, 점 부품·만드는 부품·명세를 더하는 법, 검사 코드 표, 테스트 명령.
- [ ] **Step 5:** 전체 테스트 통과와 건수를 적는다. 커밋 `[ASSET] 픽셀 키트 빵집 기준판 — 칸 맞춤, 검사 통과`.
- [ ] **Step 6: 사용자에게 보인다(여기서 멈춘다).** 판 네 장과 비교판의 전체 경로(드라이브 문자부터), 스스로 찾은 남은 결함, 시험판과 달라진 비율(건물 184 → 192px 등)을 보고한다. 사용자가 승인하면 `sheet bakery` 결과를 `art/kit/anchor.png`로 복사해 커밋한다(`[ASSET] 픽셀 키트 화풍 기준판`). 고칠 곳을 말하면 명세·부품·`style.json`을 고치고 다시 보인다.

### Task 11: 스킬과 규칙 문서

**Files:** Create `.claude/skills/pixel-kit/SKILL.md`, `.agents/skills/pixel-kit/SKILL.md`(같은 내용); Modify `.claude/rules/engine-migration.md`("지금 있는 것" 스킬 목록에 한 줄), `.claude/rules/changelog.md`(맨 위에 항목)

- [ ] **Step 1:** `superpowers:writing-skills`를 불러 형식을 따른다. 내용은 설계서 8절의 순서 7개와 "하지 않는다" 목록, Task 9의 명령, 설계서 6.4~6.6절의 규칙 요약(옆면 금지, 면 나누기, 그림자는 물체가 끝나는 곳에서, 어색하면 이론을 더하기 전에 LimeZu와 같은 배율로 나란히 본다), `export`는 아직 없다는 것. 설명(description)의 부르는 말: "건물/외관/타일/가구/방 만들어 줘", "픽셀 키트", "pixel-kit", "빵집 진열대에 …".
- [ ] **Step 2:** 스킬만 읽은 새 에이전트가 "약국 진열대(약병 6개) 명세를 써서 견본판까지" 할 수 있는지 하위 에이전트 하나로 시험한다(결과물은 `output/`에만, 커밋하지 않는다). 막힌 곳을 스킬에 고친다.
- [ ] **Step 3:** `engine-migration.md` 스킬 목록에 `pixel-kit`(건물·타일·가구 그림을 부품과 규칙으로 그리기) 한 줄. `changelog.md`에 2026-11-13 항목(요청 원문, 배경: 확산 모델 결과의 마감 불만 → 부품과 규칙으로 그리는 방식, 변경, 일반화: 마감은 사람이나 후처리가 아니라 규칙과 검사가 맡는다).
- [ ] **Step 4:** `bash Scripts/ci/run-all.sh --checks-only` 통과(정적 검사가 새 폴더에 걸리지 않는지). 커밋 `[DOCS] pixel-kit 스킬 — Claude와 Codex 양쪽`.

---

## 이 계획이 하지 않는 것 (다음 단계)

`export`(게임 형식으로 내보내기)와 게임에 한 번 넣기(설계서 10절 2단계), 타일·외벽 세트(3단계), 건물 전부(4단계), 실내 세트(5단계), 아이콘(6단계). 앞 설계서(`2026-11-12-art-production-design.md`)를 "대체됨"으로 정리하는 일은 2단계에서 그 문서의 7.3절을 옮긴 뒤에 한다.
