# 픽셀 키트 2단계(게임에 넣기) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 픽셀 키트의 그림을 게임이 이미 읽는 모양(PNG + 좌표표 + 가구 정의의 그림 참조, LDtk 타일셋 시트)으로 내보내는 `export`를 만들고, 기존 가구 3개의 그림을 새 그림으로 바꿔 게임 코드 수정 없이 화면에 보이는지 확인한다.

**Architecture:** 먼저 1단계 최종 검토가 미룬 것(설계서 6.7절)을 고쳐 두 번째 세트를 명세만으로 만들 수 있게 한다. `export`는 명세의 `game` 덩어리를 읽어 `art/` 아래에 PNG를, `data/atlases/` 아래에 좌표표를 쓰고, `replaces`가 가리키는 가구 정의의 그림 참조 두 값만 고친다. 건물·타일은 덧붙이기만 하는 묶음 시트와 자리 표로 내고, 시험용 LDtk 레벨로 본다.

**Tech Stack:** Python 3.11 + Pillow(도구), 게임 쪽은 C#/MonoGame(코드를 고치지 않는다 — 테스트 하나만 더한다), bash(배포 목록).

**Spec:** `docs/superpowers/specs/2026-11-13-pixel-kit-design.md` (5.3절 종류와 내보내는 모양, 6.7절 2단계 전에 할 일, 10절 2단계, 15절 답 1·2·4·5·6·7)

**게임 쪽 사실(2026-11-13 조사, 코드에서 확인한 것):**
- 가구 그림 참조는 `data/interiors/FurnitureDefinition.json`의 `tileParts[].tile = {"sprite": "<저장소 뿌리 기준 PNG 경로>", "name": "<좌표표 칸 이름>"}`. 대부분 `tileParts` 1개(`localCell` (0,0)), 큰 그림 한 장이 점유 칸 전체를 덮는다.
- 좌표표는 `data/atlases/<PNG 경로>.atlas.json`: `{"image", "width", "height", "pixelsPerUnit", "origin": "top-left", "sprites": {"<칸>": {"x","y","w","h","pivot":{"x","y"}}}}`. `width`·`height`는 PNG 실제 크기와 같아야 한다(`FileReferenceTests`).
- 그리는 크기는 `그림 픽셀 / pixelsPerUnit` 칸. 기준점(pivot)이 `localCell` 칸의 가운데에 놓인다. 지금 그림은 48px 칸(`pixelsPerUnit` 48)이고 2x1 가구는 96x144에 pivot (0.25, 0.8333)이다 → **키트 그림은 `pixelsPerUnit` 32로 내면 크기가 맞는다.**
- `RealDataTests`가 가구 정의 수 1503을 고정한다. 새 가구 id를 더하면 깨진다(더하는 것은 기획이라 이 계획에서 하지 않는다).
- 가구 정의 JSON을 고치면 데이터 지문이 달라진다(이전 배포와 같은 방에 못 들어간다). PNG·좌표표는 지문에 들지 않는다.
- 배포 목록(`Tools/publish/content.txt`, `publish.sh`)은 JSON 안의 `Assets/….png` 문자열만 따라간다. `art/` 아래 그림은 지금 배포에서 빠진다.
- LDtk: 타일셋은 `relPath`(`.ldtk` 폴더 기준)로 저장소 안 PNG를 가리키고 `pxWid`·`pxHei`가 PNG 실제 크기와 같아야 한다. 엔티티 그림은 `art`·`ysort` 태그 층의 엔티티 `__tile`에서 온다. 다른 맵 목록은 `--maps <파일>`로 준다. 화면 한 장은 `--world <맵> --frames N --screenshot <PNG>`.
- 가구를 화면에서 보는 길: Host 테스트 `FurniturePlacementHostTests`(P 키 → 검색 칸에 id → 놓기 → `PlayerDriver.Screenshot`). 근거 종류는 "장치 이벤트 주입, OS 입력 계층 미확인".

## Global Constraints

- 1단계의 Global Constraints가 그대로 적용된다(`docs/superpowers/plans/2026-11-13-pixel-kit-stage1.md`): 칸 32px, 캔버스는 32의 배수, 팔레트 키만, 외곽선은 렌더러가, 건물 옆면 금지, 글자 금지, 결정적 렌더, 남의 그림 조각 금지.
- **게임 코드(`game/Rootborn.*`의 실행 코드)를 고치지 않는다.** 고치는 것은 도구(`Tools/pixel-kit`, `Tools/publish`), 그림 데이터(`art/`), 좌표표(`data/atlases/art/**`), 가구 정의의 그림 참조 두 값, 테스트뿐이다.
- 가구의 `id`, `stableId`, `footprintCells`, `blocksMovement`, `category`, `allowedSurfaceIds`, 이름 문구를 바꾸지 않는다. 가구 정의를 더하거나 지우지 않는다.
- `data/interiors/FurnitureDefinition.json`은 6만 줄이 넘는다. **그 항목의 `sprite`·`name` 두 값만 문자열 치환으로 고친다.** JSON을 읽어 통째로 다시 쓰지 않는다(전체 파일 diff 금지).
- 새 그림은 `art/` 아래에 둔다(`Assets/` 아님): 가구 `art/props/<세트>/<명세 id>.png`, 타일 `art/tiles/<세트>.png`, 건물·바깥 소품 `art/world/<세트>.png`. 묶음 시트의 이미 있는 자리는 바꾸지 않는다(덧붙이기만).
- 1단계 그림은 바이트까지 그대로다: 새 값(seed, 팔레트 키, 벽등)의 기본값은 지금 그림을 낸다. 기본값으로 다시 그린 빵집 그림 전부가 변경 전과 바이트가 같은지 작업마다 확인한다(Task 1~4).
- 경로는 인자로 받고 기본값은 저장소 안 상대 위치. 테스트는 임시 폴더에 내보낸다. 실제 `data/`·`art/`에 쓰는 것은 Task 8뿐이다.
- 근거 종류: 도구 테스트·Data 테스트는 "자동 테스트", Host 테스트는 "장치 이벤트 주입(OS 입력 계층 미확인)", 스크린샷은 직접 열어 본 것만 경로와 함께.
- 커밋은 한글, 접두사는 `.claude/rules/commit-conventions.md`. 푸시하지 않는다.

## Review Focus

1. **`replaces`가 가리키는 가구 id가 없거나, 명세의 `cells`가 그 가구의 `footprintCells`와 다르다** → 아무것도 쓰지 않고 명세 id와 가구 id를 적은 오류. (Task 5)
2. **내보내기를 두 번 돌린다** → 두 번째는 파일을 하나도 바꾸지 않는다(같은 바이트). 묶음 시트의 자리가 움직이지 않는다. (Task 5, 6)
3. **묶음 시트에 이미 자리가 있는 그림의 크기가 바뀌었다** → 덮어쓰지 않고 오류(맵에 놓인 그림이 깨진다). (Task 6)
4. **검사 문제가 있는 그림을 내보내려 한다** → 그 그림은 내보내지 않고 끝 코드 1, 다른 그림도 쓰지 않는다(일부만 바뀐 상태를 남기지 않는다). (Task 5)
5. **배포 폴더에 `art/` 그림이 빠진다** → 배포 목록 테스트가 잡는다. (Task 7)

---

## File Structure

```
Tools/pixel-kit/pixelkit/makers/common.py     만드는 부품이 함께 쓰는 도우미(팔레트 읽기, 칸 문제 모으기, pile)
Tools/pixel-kit/pixelkit/export.py            내보내기: 가구, 묶음 시트, 좌표표, 가구 정의 치환
Tools/pixel-kit/pixelkit/ldtk_test_level.py   시험용 LDtk 레벨 쓰기
Tools/pixel-kit/tests/test_export*.py, fixtures/export/…
art/kit/parts/books.json, kitchen.json        새 점 부품
art/kit/recipes/school/*.json, kitchen/*.json 새 세트 명세
art/props/<세트>/<id>.png                     내보낸 가구 그림
art/tiles/bakery.png, art/world/bakery.png    내보낸 묶음 시트, art/**/<시트>.slots.json 자리 표
art/world/test/pixel-kit-test.ldtk, maps.json 시험용 맵
data/atlases/art/**.atlas.json                좌표표
game/tests/Rootborn.Host.Tests/PixelKitFurnitureHostTests.cs   새 그림을 놓아 스크린샷
```

---

### Task 0: 브랜치

- [ ] 작업 폴더 `D:/User/farmer/.worktrees/pixel-kit`, `main`(8f7df0a8a)에서 브랜치 `pixel-kit-stage2`. 이 계획서를 커밋한다(`[DOCS] 픽셀 키트 2단계 계획`).

### Task 1: 공용 도우미, seed, 타일 크기 (설계서 6.7절)

**Files:** Create `pixelkit/makers/common.py`, `tests/test_common.py`; Modify `makers/furniture.py`, `makers/building.py`, `makers/tiles.py`, `rand.py`, 관련 테스트

**Interfaces — Produces:**
- `makers/common.py`: `building.py`가 `furniture.py`에서 가져다 쓰던 비공개 도우미를 공개 이름으로 옮긴다 — `rid(spec) -> str`, `local(ctx) -> Ctx`, `merge(ctx, local, spec) -> None`, `report(problems, code, message) -> None`, `pile(cv, items, x0, x1, base_y, ctx, top) -> None`, `streaks(...)`, 팔레트 줄 읽기 `ramp(ctx, spec, prefix, steps) -> list[RGBA]`. `furniture.py`·`building.py`는 여기서 가져온다. 다른 만드는 부품 모듈의 `_비공개` 이름을 가져오는 곳이 없어야 한다.
- `rand.rnd(k, salt, n, seed=0)`: `seed`가 0이면 지금과 같은 값. 만드는 부품은 `spec.get("seed", 0)`을 모든 `rnd` 호출에 넘긴다. `tiles.py`의 두 번째 해시 `_hash`를 없애고 `rnd`를 쓴다(보도의 점 자리가 바뀌면 안 된다 — 같은 자리가 나오게 옮기거나, 안 되면 보고).
- 타일 만드는 부품은 `art_cells`만큼 그린다: `art_cells [2, 2]`의 바닥 타일은 64x64를 무늬로 채운다(지금은 32x32만 그려 투명 여백이 남는다). `lint_tile`에 투명 칸이 있으면 문제 `tile-transparent`.

- [ ] **Step 1: 변경 전 그림 고정** — 변경 전에 모든 명세(상태 포함)와 장면 둘을 임시 폴더에 그려 SHA-256 목록을 만든다.
- [ ] **Step 2: 실패 테스트** — `test_common.py`: `building.py`·`furniture.py` 소스에 `from .furniture import _`·`from .building import _` 같은 비공개 가져오기가 없다(소스 문자열 검사); `rnd(k, s, n)` == `rnd(k, s, n, seed=0)`이고 `seed=1`이면 256개 입력 중 다른 값이 있다; 같은 건물 명세에 `seed: 1`을 주면 벽 그림이 달라지고 `seed`가 같으면 바이트가 같다; `tile.floor-checker`에 `art_cells [2, 2]` → 64x64, 투명 칸 0, `lint_tile` 빈 목록; 투명 칸이 든 타일 → `tile-transparent`.
- [ ] **Step 3–4:** 실패 확인 → 구현 → 통과(전체 테스트).
- [ ] **Step 5: 바이트 비교** — Step 1의 목록과 다시 그린 것이 전부 같다.
- [ ] **Step 6:** 커밋 `[REFACTOR] 픽셀 키트 만드는 부품 공용 도우미, 명세의 seed, 타일은 art_cells만큼`.

### Task 2: 팔레트와 크기를 명세에서 (설계서 6.7절)

**Files:** Modify `makers/furniture.py`, `makers/building.py`, 테스트, `Tools/pixel-kit/README.md`(만드는 부품 표)

**Interfaces — Produces:** 가구·건물 만드는 부품이 지금 코드에 적어 둔 팔레트 줄을 명세의 선택 값으로 받는다. 기본값은 지금 값(그림 불변).

| 만드는 부품 | 새 선택 값(기본값) |
|---|---|
| `furniture.shelf` | `material` (`wood.warm`), `back` (`wood.back`), `tray` (`cream`) |
| `furniture.counter` | `material` (`wood.warm`) |
| `furniture.glass-case`, `furniture.trolley` | `frame` (`metal`), 수레 `inside` (`metal.dark`) |
| `building.shop` | `trim` (`trim`), `plinth` (`plinth`), `frame` (`wood.warm`), `lamp: {at_x, part}` — `part` 기본 `prop.wall-lamp`, `lamp` 자체가 없으면 벽등을 그리지 않는다 |

- 팔레트 줄이 필요한 단계 수보다 짧으면 `RecipeError`(명세 id, 줄 이름, 필요한 단계 수).
- 가구의 폭은 `art_cells[0] * 32 - 2`에서 얻는다: `furniture.shelf`는 `art_cells [1..3, 2..3]`, `furniture.counter`는 `art_cells [2..4, 2]`를 받아 그 폭으로 그린다(앞판 수는 폭에서 계산). 지금 크기(`[2, 3]`, `[3, 2]`)의 그림은 불변.

- [ ] **Step 1:** 변경 전 그림 SHA-256 목록(Task 1과 같은 방법).
- [ ] **Step 2: 실패 테스트** — 값마다: 기본값이면 지금 그림과 같다(목록 비교로 대신); `material: "metal"`을 준 선반에 `wood.warm` 색이 하나도 없다; 짧은 줄 → `RecipeError`; `lamp` 없는 건물 → `_layout["lamp"]`가 없고 벽등 부품 색이 없다, 검사 통과; `furniture.shelf` `art_cells [1, 3]` → 30px 폭 그림, `[3, 3]` → 94px, 둘 다 검사 통과·빈 띠 없음; `furniture.counter` `art_cells [2, 2]` → 62px 폭, 앞판 수가 줄고 검사 통과; 범위 밖 `art_cells` → `RecipeError`.
- [ ] **Step 3–5:** 실패 확인 → 구현 → 통과 → 바이트 비교.
- [ ] **Step 6:** 새 크기·색의 그림 몇 장(1칸 선반, 3칸 선반, 2칸 계산대, 금속 선반, 벽등 없는 가게)을 6배로 `output/pixel-kit/`에 내 **직접 열어 본다**(찌그러진 비율, 빈 띠, 어색한 판 나눔).
- [ ] **Step 7:** README 만드는 부품 표 갱신. 커밋 `[CHORE] 픽셀 키트 만드는 부품 — 팔레트 줄과 폭을 명세에서, 벽등은 선택`.

### Task 3: 건물 배치 검사 (설계서 6.7절)

**Files:** Modify `makers/building.py`, `tests/test_building.py`, README 검사 코드 표

**Interfaces — Produces:** `building.shop`이 그리기 전에 배치를 확인해 그림마다 문제를 낸다(`ctx.problems`, 코드마다 하나).
- `facade-out`: 문틀, 창(양옆 볼과 벽 그림자 포함), 간판, 벽등 중 하나가 벽(`_layout["wall"]`) 밖이나 캔버스 밖으로 나간다.
- `facade-overlap`: 문틀·창·벽등의 사각형이 서로 겹친다(간판은 처마 위로 솟으므로 문·창과의 세로 겹침만 본다).
- `roof-overlap`(기존)에 더함: 지붕 위 물건(그림자 포함)이 간판 사각형과 겹친다.
- `pixels`의 폭이 정면에 필요한 최소 폭보다 작으면 `RecipeError`가 아니라 위 문제로 드러난다.

- [ ] **Step 1: 실패 테스트** — 최종 검토가 재현한 다섯 경우 각각이 문제 하나 이상을 낸다: `pixels: [128, 192]`(창이 캔버스 밖), 문 `at_x: 85`(문이 창 위), 간판 `at_x: 140`(오른쪽 밖), 벽등 `at_x: 20`(문 위), 문 `at_x: -30`; 기와지붕에서 간판 밑에 놓은 굴뚝 → `roof-overlap`; 실제 빵집 두 명세는 문제 없음; 문제 메시지가 명세 id로 시작하고 어느 것이 어디로 나갔는지 적는다.
- [ ] **Step 2–4:** 실패 확인 → 구현 → 통과. 빵집 그림 바이트 불변.
- [ ] **Step 5:** 커밋 `[CHORE] 픽셀 키트 건물 — 정면 배치가 벽 밖으로 나가거나 겹치면 문제로`.

### Task 4: 색 수는 몸통만 센다 (설계서 6.7절 — 작성자 판단, 사용자가 다르게 정하면 고친다)

**Files:** Modify `pixelkit/recipes.py`(또는 `makers/__init__.py`의 `Ctx`), `pixelkit/lint.py`, `makers/common.py`, `art/kit/style.json`, 테스트, 설계서 6.1절, README

**Interfaces — Produces:**
- `Ctx.part(id)`가 쓴 부품 id를 `ctx.used_parts: set[str]`에 적는다(그림마다 새 Ctx).
- `lint_sprite(cv, recipe, ctx, allow=…, layout=…)`의 `too-many-colors`는 **그림의 색에서, 쓴 점 부품들의 색을 뺀 수**를 한도와 견준다(외곽선 색 제외는 그대로). 점 부품은 각자 `part` 한도(10)로 따로 검사된다.
- 한도 값: `furniture` 16, `building` 32로 되돌린다(1단계에서 붙인 부품 때문에 24·40으로 올렸던 것). 지금 그림이 새 규칙으로 넘으면 한도를 올리지 말고 넘은 수를 보고한다.
- README "같이 바뀌는 색": 세 쌍을 다시 나눠도 되는지 확인한다(나누면 그림이 바뀌므로 **이 작업에서는 나누지 않는다** — 문구만 "한도 때문이 아니라 색 수를 줄이려고 합친 것"으로 고친다).

- [ ] **Step 1: 실패 테스트** — 몸통 3색 + 서로 다른 색의 부품 5개(합쳐 30색 넘음)를 붙인 그림이 한도 16에서 통과한다; 몸통만 17색이면 `too-many-colors`이고 메시지에 몸통 색 수가 적힌다; 부품을 쓰지 않은 그림은 지금과 같은 수; 그림 둘을 이어 그려도 `used_parts`가 새지 않는다; 실제 빵집 명세 전부가 16·32에서 통과한다(넘으면 실패시킨 채 보고).
- [ ] **Step 2–4:** 실패 확인 → 구현 → 통과. 빵집 그림 바이트 불변.
- [ ] **Step 5:** 설계서 6.1절과 6.7절(해당 줄을 "했다"로), README, 스킬("색 한도는 몸통만 센다")을 고친다. 커밋 `[CHORE] 픽셀 키트 검사 — 색 수는 몸통만 센다(붙인 부품은 따로), 한도 16·32로`.

### Task 5: 가구 내보내기

**Files:** Create `pixelkit/export.py`, `tests/test_export_furniture.py`, `tests/fixtures/export/**`(작은 가구 정의 JSON, 작은 키트); Modify `pixelkit/cli.py`, `kit.py`, README

**Interfaces — Produces:**
- 명세의 `game` 덩어리(가구): `"game": {"replaces": "<가구 id>"}`. `replaces`가 없으면 그림과 좌표표만 내고 가구 정의는 건드리지 않는다.
- `export_furniture(recipe, kit, repo_root) -> ExportPlan` — 쓰지 않고 계획만 만든다: `files: dict[경로, bytes]`, `replace: list[(가구 id, 새 sprite, 새 name)]`, `problems: list[Problem]`.
  - PNG: `art/props/<세트>/<명세 id>.png` — 기본 상태와 모든 상태를 왼쪽부터 가로로 나란히 붙인 한 장(칸마다 `art_cells * 32`). 상태 순서는 기본, 그다음 `states`에 적힌 순서.
  - 좌표표: `data/atlases/art/props/<세트>/<명세 id>.png.atlas.json` — `pixelsPerUnit` 32, `origin` `"top-left"`, `width`·`height`는 PNG에서 잰 값, 칸 이름 `<명세 id>`(기본)와 `<명세 id>@<상태>`, `pivot`은 `{"x": 0.5 / art_w_cells, "y": (art_h_cells - 0.5) / art_h_cells}`(점유 칸 줄의 왼쪽 아래 칸 가운데 — 지금 가구 좌표표의 관례와 같다).
  - `replaces`: 그 가구 항목의 `tileParts[0].tile.sprite`와 `.name`을 새 PNG 경로와 기본 칸 이름으로.
- `apply(plan, repo_root)`: 문제가 하나라도 있으면 아무것도 쓰지 않는다. 가구 정의는 **그 항목 안의 `sprite`·`name` 두 문자열만** 치환한다(파일의 다른 바이트 불변, 줄바꿈 방식 유지).
- 검사: `replaces`의 가구 id가 정의에 없으면 문제 `export-unknown-furniture`; 그 가구의 `tileParts`가 1개가 아니거나 `localCell`이 (0,0)이 아니면 `export-unsupported-furniture`; 명세의 `cells [w, 1]`이 가구의 `footprintCells`(가로 한 줄 w칸)와 다르면 `export-footprint-mismatch`; 그림의 lint·만드는 부품 문제가 있으면 그대로 문제.
- 명령: `kit.py export <id…> [--dry-run] [--repo <뿌리>]` — `--dry-run`은 쓸 파일과 바꿀 가구를 출력만 한다. 끝 코드 0(썼다/쓸 것 없음), 1(문제), 2(사용법·저장 거절). 장면·타일·건물 id를 주면 Task 6의 내보내기로 넘긴다(이 작업에서는 "아직 없다" 오류).

- [ ] **Step 1: 실패 테스트** (모두 임시 폴더의 가짜 저장소에서) —

```python
def test_export_writes_png_atlas_and_replaces_two_strings_only():
    # 가짜 FurnitureDefinition.json(항목 3개, CRLF 줄바꿈)에서 가운데 항목만 replaces
    before = read_bytes(defs)
    apply(export_furniture(recipe, kit, root), root)
    after = read_bytes(defs)
    assert changed_line_count(before, after) == 2           # sprite 줄, name 줄
    assert b"\r\n" in after and after.count(b"\n") == before.count(b"\n")
    atlas = json.load(open(atlas_path))
    assert atlas["pixelsPerUnit"] == 32 and atlas["origin"] == "top-left"
    assert (atlas["width"], atlas["height"]) == Image.open(png_path).size
    assert atlas["sprites"]["test.shelf"]["pivot"] == {"x": 0.25, "y": 5 / 6}   # art_cells [2, 3]

def test_states_are_extra_cells_in_the_same_png(): ...        # test.shelf@empty 칸이 x=64에서 시작
def test_second_export_changes_nothing(): ...                  # 파일 바이트·수정 시각 불변, "바꿀 것 없음" 출력
def test_unknown_furniture_or_footprint_mismatch_writes_nothing(): ...   # 문제 코드, 폴더에 새 파일 0개
def test_lint_problem_blocks_every_file_of_the_run(): ...      # 명세 둘 중 하나가 문제면 둘 다 안 쓴다
def test_dry_run_writes_nothing_and_lists_the_plan(): ...
def test_without_replaces_definitions_are_untouched(): ...
```

- [ ] **Step 2–4:** 실패 확인 → 구현 → 통과.
- [ ] **Step 5:** 실제 저장소에 `kit.py export bakery.shelf --dry-run`을 돌려 출력이 맞는지 본다(`game` 덩어리가 없으므로 PNG·좌표표 계획만 나오고 가구 정의는 "바꿀 것 없음"). 아무것도 쓰지 않는다.
- [ ] **Step 6:** README에 내보내기 절과 `game` 덩어리, 좌표표 관례. 커밋 `[CHORE] 픽셀 키트 export — 가구 그림·좌표표를 내고 가구 정의의 그림 참조만 바꾼다`.

### Task 6: 묶음 시트와 시험용 맵

**Files:** Modify `pixelkit/export.py`, `pixelkit/cli.py`; Create `pixelkit/ldtk_test_level.py`, `tests/test_export_sheets.py`, `tests/test_ldtk_test_level.py`

**Interfaces — Produces:**
- `export_sheet(recipes, kind, set_name, kit, repo_root) -> ExportPlan`:
  - 타일(`kind: tile`) → `art/tiles/<세트>.png`, 건물·바깥 소품(`building`, `prop`) → `art/world/<세트>.png`. 32px 격자, 시트 폭은 16칸(512px) 고정, 그림은 칸 단위 사각형으로 위에서부터 줄 맞춰 놓는다(가장 단순한 선반 쌓기).
  - 자리 표 `<시트>.slots.json`: `{"cell": 32, "width_cells": 16, "slots": {"<명세 id>": {"x", "y", "w", "h"}}}`(픽셀). **이미 자리가 있는 id는 그 자리에 다시 그린다. 크기가 달라졌으면 문제 `export-slot-size-changed`이고 아무것도 쓰지 않는다.** 새 id는 마지막 줄 뒤에 덧붙인다. 시트 높이는 필요한 만큼 늘린다(32의 배수).
  - 좌표표 `data/atlases/<시트 경로>.atlas.json`: `pixelsPerUnit` 32, 칸마다 자리와 `pivot`(건물·소품은 `{"x": 0.5, "y": 1.0}` 발 위치, 타일은 `{"x": 0.5, "y": 0.5}`).
  - 32x64 실내 벽은 시트에서 위아래 두 칸을 차지한다(자리 하나, h 64).
- `write_test_level(sheets, placements, out_dir, repo_root)`: 시험용 `pixel-kit-test.ldtk`와 `maps.json`을 쓴다. 타일 층 `Ground`(바닥 타일을 전부 깐다) 하나와 `art` 태그 엔티티 층 하나(건물·소품을 발 위치 기준으로), 도착 지점 하나(열린 칸). 타일셋 `relPath`는 `.ldtk` 폴더 기준 상대 경로, `pxWid`·`pxHei`는 PNG에서 잰 값. `maps.json`은 `game/world/maps.json`의 `walker`·`npcWalker`를 읽어 옮기고 맵 하나(`PixelKitTest`)만 둔다. 필요한 JSON 필드는 `game/Rootborn.World/Ldtk/LdtkFile.cs`가 읽는 것에 맞춘다(작은 본보기: `game/tests/Rootborn.World.Tests/Fixtures/buildings/lot.ldtk`).
- 명령: `kit.py export <타일·건물·소품 id…>`는 세트·종류별로 시트에 넣는다. `kit.py test-level [--out art/world/test]`는 지금 시트로 시험용 맵을 쓴다.

- [ ] **Step 1: 실패 테스트** — 새 시트: 그림 둘을 넣으면 자리가 겹치지 않고 32 격자에 맞고 PNG 크기가 32의 배수; 같은 명세로 다시 → 바이트 불변; 세 번째 그림을 더하면 앞 두 자리와 그 픽셀이 그대로다; 크기가 바뀐 그림 → `export-slot-size-changed`, 파일 불변; 좌표표의 `width`·`height` == PNG 크기, 칸이 그림 안; 시험 레벨: 쓴 `.ldtk`가 JSON으로 읽히고 타일셋마다 `relPath`의 PNG가 있고 `pxWid`·`pxHei`가 맞다, `gridTiles`의 `src`가 자리 표의 타일 자리와 같다, 엔티티 `__tile`이 자리 표의 건물 자리와 같다, `maps.json`의 도착 지점이 레벨 안이다.
- [ ] **Step 2–4:** 실패 확인 → 구현 → 통과.
- [ ] **Step 5:** `dotnet test game/tests/Rootborn.World.Tests`가 쓰는 읽기 코드를 테스트에서 직접 부를 수는 없으므로(파이썬), 시험 레벨이 실제로 읽히는지는 Task 8에서 게임을 띄워 본다 — 여기서는 그렇다고 보고서에 적는다.
- [ ] **Step 6:** README 갱신. 커밋 `[CHORE] 픽셀 키트 export — 덧붙이기만 하는 묶음 시트와 자리 표, 시험용 LDtk 레벨`.

### Task 7: 배포 목록

**Files:** Modify `Tools/publish/content.txt`, `Tools/publish/publish.sh`(필요하면), `Tools/publish/README.md`, 배포 목록을 보는 테스트(`game/tests/Rootborn.Server.Tests`의 `PublishListStartupTests` 또는 그 옆)

- [ ] **Step 1:** `Tools/publish/publish.sh`와 `content.txt`의 `@refs` 처리를 읽는다. `@refs`가 JSON 안의 `Assets/….png`만 찾는다면 `art/….png`도 찾게 한다(정규식 한 곳). 좌표표 자동 따라오기는 PNG 경로 기준이라 그대로 된다. 시험용 맵(`art/world/test/**`)은 배포에 넣지 않는다.
- [ ] **Step 2: 실패 테스트** — 임시 저장소(작은 `data/` JSON 하나가 `art/props/x/y.png`를 가리키고 그 PNG와 좌표표가 있다)에서 배포 목록을 만들면 PNG와 좌표표가 목록에 있다. 지금 구현으로 실패하는 것을 본다. 테스트를 어디에 둘지는 기존 배포 목록 테스트가 있는 프로젝트를 따른다(없으면 `Tools/publish`의 `verify.sh`가 하는 검사에 한 줄 더하고 그 실행 결과를 근거로 적는다).
- [ ] **Step 3–4:** 구현 → 통과. `bash Scripts/ci/run-all.sh --checks-only` 통과.
- [ ] **Step 5:** 커밋 `[CHORE] 배포 목록 — art/ 아래 그림도 데이터가 가리키면 따라온다`.

### Task 8: 두 번째 세트 — 책장 둘과 조리대

**Files:** Create `art/kit/parts/books.json`, `art/kit/parts/kitchen.json`, `art/kit/recipes/school/furniture.json`, `art/kit/recipes/kitchen/furniture.json`, 장면 명세 하나(미리 보기)

바꿀 가구(조사에서 고른 후보 — 퀘스트·가게·시작 배치·테스트가 가리키지 않는다. **이 작업을 시작할 때 `data/`와 `game/`에서 세 id를 다시 grep해 확인하고, 가리키는 곳이 있으면 멈추고 보고한다**):

| 가구 id | 이름 | 점유 칸 | 지금 그림 | 새 명세 |
|---|---|---|---|---|
| `school.bookcase` | 학교 책장 | 2x1 | `Assets/Art/SchoolPixels/Furniture48/Storage/School_bookcase_2x1.png` | `school.bookcase` — `furniture.shelf`, `art_cells [2, 3]`, 책이 꽂힌 4층 |
| `school.archive30.archive.bookshelf` | 기록실 책장 | 2x1 | `Assets/Art/SchoolPixels/Archive30/Archive30_archive_bookshelf_2x1.png` | `school.archive-bookshelf` — 같은 부품, 어두운 나무(`material` 다른 줄), 서류철·상자 |
| `comfy31.kitchen.counter.front` | 주방 조리대(정면) | 2x1 | `Assets/Art/CareerFurniture/Comfy31/Comfy31_kitchen_counter_front_2x1.png` | `kitchen.counter-front` — `furniture.counter`, `art_cells [2, 2]`, 윗면에 도마·냄비 |

- [ ] **Step 1:** 스킬 `pixel-kit`(`.claude/skills/pixel-kit/SKILL.md`)의 순서를 그대로 따른다. 지금 그림 세 장을 직접 열어 무엇을 그린 것인지 본다(같은 물건을 새 화풍으로 그리는 것이지 베끼는 것이 아니다).
- [ ] **Step 2:** 점 부품: 책(세워 꽂은 책 묶음 3~4종, 눕힌 책 더미), 서류철·보관 상자, 도마·냄비·그릇. 필요한 팔레트 줄을 `style.json`에 더한다(어두운 나무, 책 표지 색 3~4개).
- [ ] **Step 3:** 명세 셋과 미리 보기 장면(교실 벽·바닥은 빵집 타일을 임시로 쓴다). 각 명세에 `"game": {"replaces": "<가구 id>"}`, `cells [2, 1]`.
- [ ] **Step 4:** `render` → `lint` → **직접 열어 보기** → 고치기. 볼 것: 책장이 책장으로, 조리대가 조리대로 읽히는가; 층이 비어 보이지 않는가; 지금 그림과 같은 물건으로 받아들여지는가.
- [ ] **Step 5:** `kit.py lint` 0건, 전체 테스트 통과. 커밋 `[ASSET] 픽셀 키트 두 번째 세트 — 학교 책장 둘, 주방 조리대`.

### Task 9: 게임에 넣어 확인 (여기서 멈춘다)

**Files:** Create(내보내기 결과) `art/props/school/*.png`, `art/props/kitchen/*.png`, `data/atlases/art/props/**.atlas.json`, `art/tiles/bakery.png`, `art/world/bakery.png`, 자리 표, `art/world/test/*`; Modify `data/interiors/FurnitureDefinition.json`(세 항목의 두 값씩); Create `game/tests/Rootborn.Host.Tests/PixelKitFurnitureHostTests.cs`

- [ ] **Step 1:** `kit.py export school.bookcase school.archive-bookshelf kitchen.counter-front --dry-run` → 출력 확인 → 실제 실행. `git diff --stat`으로 가구 정의가 6줄만 바뀌었는지 본다.
- [ ] **Step 2:** `kit.py export`로 빵집 타일·건물·소품을 시트에 넣고 `kit.py test-level`.
- [ ] **Step 3: 자동 테스트** — `dotnet test game/tests/Rootborn.Data.Tests`(좌표표·그림 참조·정의 읽기, 가구 수 1503 그대로)와 `dotnet test game/tests/Rootborn.World.Tests`. 건수가 줄지 않았는지 본다.
- [ ] **Step 4: 가구를 게임 화면에서** — `PixelKitFurnitureHostTests`: `FurniturePlacementHostTests.FURNITURE_007`과 같은 길(P 키 → 검색 칸에 가구 id → 팔레트 칸 → 걸어가 E로 놓기)로 세 가구를 놓고 `PlayerDriver.Screenshot("pixel-kit-furniture-01-…")`. 가구 id는 테스트 데이터로 두고 코드가 id로 분기하지 않는다. 판정은 "놓였다(배치 수가 늘었다)"까지 — 그림 픽셀을 단언하지 않는다. 이 PC에서 필터 하나로 돌린다(`--filter "FullyQualifiedName~PixelKitFurnitureHostTests"`). 메모리가 모자라면 커밋 뒤 `bash Scripts/remote-test/run.sh`로 돌리고 그렇다고 적는다.
- [ ] **Step 5: 타일·건물을 게임 화면에서** — `dotnet run --project game/Rootborn.Host -- --maps art/world/test/maps.json --world PixelKitTest --size 1280x720 --frames 30 --screenshot output/pixel-kit/game/test-level.png`.
- [ ] **Step 6:** 스크린샷을 **직접 열어** 본다: 새 그림이 보이는가, 크기가 칸에 맞는가(옆의 옛 48px 그림과 견줘 너무 크거나 작지 않은가), 기준점이 맞는가(점유 칸 위에 서 있는가, 한 칸 밀리지 않았는가), 빈 그림·잘림이 없는가. 어긋나면 좌표표의 `pivot`·`pixelsPerUnit` 계산(Task 5)을 고치고 다시.
- [ ] **Step 7:** 커밋 둘: `[ASSET] 가구 3종(학교 책장, 기록실 책장, 주방 조리대)의 그림을 픽셀 키트 그림으로 — id·규칙·세이브 그대로`(가구 정의 + art + 좌표표), `[TEST] 픽셀 키트 그림을 게임에 놓아 보는 Host 테스트와 시험용 맵`.
- [ ] **Step 8: 사용자에게 보인다(여기서 멈춘다).** 스크린샷 경로(드라이브 문자부터), 근거 종류(Data·World 테스트 = 자동 테스트, 가구 = 장치 이벤트 주입·OS 입력 계층 미확인, 시험 맵 = 직접 본 화면), 옛 그림과 섞여 보이는 점, 데이터 지문이 달라졌다는 것(이전 배포와 같은 방에 못 들어간다), 남은 결함.

### Task 10: 문서와 스킬

**Files:** Modify `.claude/skills/pixel-kit/SKILL.md`, `.agents/skills/pixel-kit/SKILL.md`(같은 내용), `Tools/pixel-kit/README.md`, 설계서(6.7절의 끝난 줄, 10절 2단계), `.claude/rules/changelog.md`

- [ ] **Step 1:** 스킬: "게임에 넣기"가 이제 된다 — 순서에 `export --dry-run` → 확인 → `export` → Data 테스트 → 게임 스크린샷(스킬 `host-play-test`)을 더하고, "하지 않는다"에 가구 정의를 더하거나 `footprintCells`를 바꾸지 않는다, 묶음 시트의 자리를 손으로 옮기지 않는다, 가구 정의 파일을 통째로 다시 쓰지 않는다를 더한다. "쓰지 않을 때"의 "게임에 넣기는 2단계" 문장을 고친다.
- [ ] **Step 2:** 설계서 6.7절에서 끝난 것을 "했다(2단계)"로, 남은 것(양쪽에 똑같이 그린 옆면)은 그대로. 10절 표의 2단계 줄에 결과 한 줄.
- [ ] **Step 3:** `changelog.md` 항목(요청 "2단계 진행해", 변경, 일반화: 그림을 바꾸는 일은 PNG·좌표표·그림 참조 두 값을 바꾸는 일이다 — 가구 정의의 다른 값과 파일의 다른 줄은 건드리지 않는다).
- [ ] **Step 4:** `bash Scripts/ci/run-all.sh --checks-only`, 도구 테스트 전체, `cmp` 두 스킬. 커밋 `[DOCS] pixel-kit 스킬과 문서 — 게임에 넣기(export)`.

---

## 이 계획이 하지 않는 것

새 가구 정의 더하기(기획), 상태(`@empty` 등)를 게임이 쓰게 하기, 시설 겉모습을 게임이 그리게 하기, 실제 맵에 새 타일·건물 놓기(맵을 새로 그릴 때), 타일·외벽 세트(설계서 10절 3단계) 이후 단계, 어제 문서(`2026-11-12-art-production-design.md`) 정리(저장소에 없는 사용자의 미커밋 파일이다).
