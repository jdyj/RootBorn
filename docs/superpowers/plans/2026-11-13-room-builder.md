# 방 만들기 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 칸마다 벽/바닥/밖만 정하면 32x32 벽·바닥 타일 그림과 걸을 수 있는 칸이 함께 든 LDtk 방이 나오고, LDtk에서 다시 칠한 뒤 명령 하나로 그림을 맞춘다.

**Architecture:** 픽셀 키트(`Tools/pixel-kit/pixelkit`)에 모듈 셋을 더한다 — `room_tiles.py`(둘레 칸 → 타일 그림), `room_plan.py`(평면·격자·검사), `room_ldtk.py`(LDtk·시트·맵 목록 쓰기와 다시 칠하기). 격자 하나(값 0 밖, 1 벽, 2 바닥)가 그림과 충돌의 유일한 원본이다. CLI는 `kit.py room build|repaint|check`.

**Tech Stack:** Python 3 + Pillow(키트와 같음), unittest. 게임 쪽 확인은 C#(`Rootborn.World.Tests`, `Rootborn.Host.Tests`).

**Spec:** `docs/superpowers/specs/2026-11-13-room-builder-design.md` (화풍은 `docs/superpowers/specs/2026-11-13-pixel-kit-design.md` 6.4~6.6절)

## Global Constraints

- 타일은 전부 32x32. 칸 값: 0 밖, 1 벽(막음), 2 바닥(열림) — 게임의 `Collision` IntGrid 약속(`game/world/README.md` 3절)과 같다.
- 벽의 옆으로 기운 면을 그리지 않는다. 정면은 남쪽을 보는 면만, 기본 높이 `face_height` 2칸.
- 색은 `art/kit/style.json` 팔레트만(새 줄이 필요하면 style.json에 더하고 보고), 외곽선 색 하나, 타일 색 한도(`max_colors.tile` 12)는 타일 한 장마다.
- 종류(벽 모양)는 "이름 → 함수" 등록으로 더한다. if 사슬·enum으로 닫지 않는다. 쓰이지 않는 확장점을 만들지 않는다.
- 문제가 하나라도 있으면 아무것도 쓰지 않는다. 쓰기는 임시 파일 뒤 `os.replace`(`export.py`의 방식을 다시 쓴다). 같은 입력이면 같은 바이트. `art/` 아래 텍스트는 LF.
- `repaint`는 `Wall`·`Ground` 층의 타일과 `tiles.png`·자리 표만 바꾼다. 다른 층, 엔티티, 필드, 모르는 키는 그대로 다시 쓴다.
- 지금 맵(`Assets/LDtk/**`, `game/world/maps.json`)과 게임 코드(`game/`, tests 제외)는 고치지 않는다. 세이브에 닿는 것을 고치지 않는다.
- 실패 테스트 먼저. 테스트는 임시 폴더만 쓰고 그림의 특정 픽셀 색에 기대지 않는다(구조: 크기, 투명 여부, 외곽선이 어느 변에 있는지, 타일이 서로 같은지 다른지).
- 기존 키트 그림과 게임 데이터는 바이트 단위로 그대로. 기존 도구 테스트 322건이 줄지 않는다.
- 커밋: 한글, prefix(`[CHORE]` 도구, `[ASSET]` 그림·맵, `[TEST]`, `[DOCS]`), 끝줄에 만든 모델의 `Co-Authored-By`. 푸시하지 않는다.
- 보고의 근거 종류를 나눈다(자동 테스트 / 장치 이벤트 주입, OS 입력 계층 미확인 / 직접 본 화면).

## Review Focus

- LDtk 편집기가 저장한 파일(키 순서·머리말·`autoLayerTiles` 빈 배열·모르는 키가 다름)을 `repaint`가 받아 다른 것을 지우지 않는가.
- 평면 줄 길이가 들쭉날쭉하거나 모르는 글자가 있으면 분명한 문제로 알리는가(조용히 채우지 않는가).
- 벽이 맵 가장자리에 닿거나 `face_height`보다 얇을 때(정면만 남음, 맵 밖을 내려다봄) 깨진 타일 없이 그려지는가.
- 닿을 수 없는 바닥, 가장자리가 열린 바닥, 벽 안의 도착 지점·문을 `check`가 잡는가.
- 시트에 타일이 늘 때 이미 있는 타일의 자리(LDtk의 `t`)가 바뀌지 않는가(덧붙이기만).

---

### Task 1: 벽·바닥 타일 그리기 (`room_tiles.py`)

**Files:**
- Create: `Tools/pixel-kit/pixelkit/room_tiles.py`, `Tools/pixel-kit/tests/test_room_tiles.py`
- Modify: `art/kit/style.json`(필요한 색 줄만), `Tools/pixel-kit/README.md`(절 "방 만들기 — 벽 타일")

**Interfaces:**
- Produces:
  - `OUT, WALL, FLOOR = 0, 1, 2`
  - `classify(grid: list[list[int]], face_height: int = 2) -> list[list[str | None]]` — 칸마다 타일 열쇠. 벽 칸: `"face:<k>"`(k = 바닥에서 위로 몇 번째, 1이 바닥 바로 위, `face_height`까지) + 좌우 끝 표시, 또는 `"cap:<mask>"`(둘레 8칸 중 윗면인 칸의 비트 — 필요 없는 대각 비트는 정리해 47가지 이내). 바닥 칸: `"floor"` 또는 `"floor:shade"`(바로 위 칸이 정면). 밖: `None`. 열쇠 문자열의 정확한 꼴은 이 작업이 정하고 README에 적는다. 격자 밖은 0으로 본다.
  - `@wall_style("paper")`로 등록한 벽 모양과 `wall_styles() -> list[str]`. 기본 모양 `paper`(줄무늬 벽지 + 나무 허리판 + 걸레받이), 두 번째 `plaster`(단색 + 낮은 걸레받이).
  - `draw_tile(style: Style, key: str, wall: str, floor_spec: dict) -> Canvas` — 32x32 한 장. 바닥은 키트의 기존 바닥 타일 만드는 부품을 불러 그린다(새로 그리지 않는다).
  - 타일 한 장을 검사하는 `lint_room_tile(canvas, key) -> list[Problem]`: 팔레트 밖 색, 색 한도, 벽·바닥 타일에 투명 픽셀 없음, 정면·바닥 타일은 좌우로 이어 붙여 이음매 없음(기존 `tile-seam` 규칙 재사용).

- [ ] **Step 1: 실패하는 테스트** — `test_room_tiles.py`:
  - `test_wall_within_face_height_above_floor_is_face`: 3줄 벽 위에 바닥이 있는 격자에서 바닥 바로 위 줄 `face:1`, 그 위 `face:2`, 그 위 `cap:…`.
  - `test_vertical_wall_is_cap_and_its_south_end_is_face`.
  - `test_cap_mask_differs_at_corner_tee_end_and_run`: 모서리·T자·끝·가운데의 열쇠가 서로 다르다. 같은 둘레면 같은 열쇠.
  - `test_cap_keys_are_at_most_47`: 3x3 둘레의 모든 256가지에서 나오는 `cap` 열쇠 종류 ≤ 47.
  - `test_floor_under_face_gets_shade`, `test_out_cells_have_no_tile`.
  - `test_thin_horizontal_wall_is_all_face_no_crash`, `test_wall_at_map_edge_treats_outside_as_out`.
  - `test_every_key_draws_32x32_opaque_and_passes_lint`(두 벽 모양 모두, 시험 격자에서 나온 모든 열쇠).
  - `test_cap_outline_is_on_the_sides_without_cap_neighbour`: 왼쪽 이웃이 윗면이 아니면 왼쪽 변 열이 외곽선 색, 이웃이 윗면이면 아니다(네 변).
  - `test_face_tiles_tile_horizontally`, `test_unknown_wall_style_is_recipe_error_listing_known`.
- [ ] **Step 2:** 돌려서 실패를 본다 — `python -m unittest Tools.pixel-kit...`가 아니라 `python -m unittest discover -s Tools/pixel-kit/tests -t Tools/pixel-kit -p "test_room_tiles.py"`. 기대: ImportError.
- [ ] **Step 3:** 구현. 치수의 출발점(견본에서 본 것 — 그림을 보고 고쳐도 된다): 정면 위 타일 = 천장 밑 그늘 2px + 벽지; 정면 아래 타일 = 몰딩 3px + 허리판 + 걸레받이 6px + 맨 아랫줄 외곽선; 윗면 = 밝은 면 + 가장자리 외곽선 1px + 남쪽 변 안쪽에 한 단계 어두운 줄 1px. 윗면 색은 벽지보다 한 단계 진하게(견본에서 둘이 비슷해 벽이 덜 서 보였다).
- [ ] **Step 4:** 테스트 통과 + 전체 도구 테스트(322 + 새 것) + `kit.py lint` 0.
- [ ] **Step 5:** 눈으로 확인 — 스크립트(`output/pixel-kit/room/` 아래, 커밋 안 함)로 방 둘 + 세로 벽 + T자 + 문간이 든 격자를 타일로 깔아 2배·5배 그림을 만들고 직접 연다. 캐릭터(32x59)와 키트 책장을 붙여 크기를 본다. 이음매, 끊긴 외곽선, 벽이 서 보이는지.
- [ ] **Step 6:** 커밋 `[CHORE] 픽셀 키트 방 벽·바닥 타일 — 둘레 칸으로 정면·윗면 고르기`.

### Task 2: 평면, 검사, LDtk 쓰기 (`room_plan.py`, `room_ldtk.py`, CLI `room build|check`)

**Files:**
- Create: `Tools/pixel-kit/pixelkit/room_plan.py`, `Tools/pixel-kit/pixelkit/room_ldtk.py`, `Tools/pixel-kit/tests/test_room_plan.py`, `Tools/pixel-kit/tests/test_room_ldtk.py`, `art/kit/rooms/test.two-rooms.json`
- Modify: `Tools/pixel-kit/pixelkit/cli.py`(하위 명령 `room`), `Tools/pixel-kit/kit.py`(필요하면), `Tools/pixel-kit/README.md`
- Create(명령의 결과, 커밋한다): `art/world/rooms/test.two-rooms/{test.two-rooms.ldtk,tiles.png,tiles.slots.json,maps.json}`

**Interfaces:**
- Consumes: Task 1의 `classify`, `draw_tile`, `lint_room_tile`, `OUT/WALL/FLOOR`. `export.py`의 스테이징 쓰기, `ldtk_test_level.py`의 LDtk 뼈대·`_loads_with_comments`·`_walkers`(같이 쓰게 공용 자리로 옮겨도 된다 — 동작은 그대로, 기존 테스트 통과).
- Produces:
  - `load_room(path) -> Room`(`id`, `wall`, `floor`, `grid`, `spawn`, `doors: dict[(x, y), str]`, `face_height`), 평면 글자: `#` 벽, `.` 바닥, 빈칸 밖, `doors`에 적은 글자는 바닥 + 문.
  - `check_grid(grid, spawn, doors, face_height) -> list[Problem]` — 코드: `room-ragged-plan`, `room-unknown-char`, `room-open-edge`(바닥이 맵 가장자리나 밖 칸에 닿음), `room-unreachable-floor`, `room-spawn-in-wall`, `room-door-in-wall`, `room-wall-thin`(알림이지만 쓰기를 막지 않는다 — 심각도 구분이 키트에 없으면 이 코드만 "경고"로 출력하고 종료 코드는 0).
  - `build_room(repo_root, room_id, out_dir=None, force=False, dry_run=False) -> list[str]`(쓴 파일들).
  - LDtk 모양: 층 정의와 인스턴스 `Collision`(IntGrid 32, 값 1 "wall", 2 "floor"), `Door`(Entities, 엔티티 정의 `Door`에 문자열 필드 `Where`), `Furniture`(Entities, `uiFilterTags` `["ysort"]`), `Wall`(Tiles), `Ground`(Tiles); 타일셋 하나(`tiles.png`, 32). `maps.json`: 맵 id = 방 id를 PascalCase로 바꾼 것(예: `TestTwoRooms`), `collision.rule` `interior-auto`, `spawns`에 `spawn`, `viewCells`는 레벨 높이와 13 중 작은 것, `walker`·`npcWalker`는 `game/world/maps.json`에서 옮긴다.
  - 시트: 쓰인 타일만, 열쇠 → 자리(`tiles.slots.json`), 덧붙이기만, 한 줄 16칸.
- [ ] **Step 1: 실패하는 테스트** — 평면 읽기와 검사 코드 각각(위 코드마다 하나), 정상 평면은 문제 0; `build_room`을 임시 저장소에서 돌려: 파일 넷이 생김, `intGridCsv`가 격자와 같음, `Wall`·`Ground` 층의 타일 수 = 벽 칸·바닥 칸 수, 각 타일의 `src`가 자리 표와 맞음, 다시 돌리면 거절(`--force` 없이), `--force`로 다시 돌리면 같은 바이트, 문제가 있는 평면은 아무것도 안 씀, `--dry-run`은 안 씀, 문 칸에 `Door` 엔티티와 `Where` 값.
- [ ] **Step 2:** 실패를 본다.
- [ ] **Step 3:** 구현. 시험 평면 `test.two-rooms`: 20x14쯤, 방 둘이 세로 벽으로 나뉘고 문간 하나, 한 방 안에 가로 벽(정면 2칸 + 윗면)과 T자, 나가는 문은 없음(`doors` 비움), `wall: "paper"`, `floor: "bakery.tile.floor"`가 아니라 나무 바닥이 있으면 그것(없으면 Task 1에서 쓴 바닥).
- [ ] **Step 4:** `python Tools/pixel-kit/kit.py room build test.two-rooms`로 결과를 만들고, `room check`가 0, 도구 테스트 전체 통과.
- [ ] **Step 5:** 커밋 둘 — `[CHORE] 픽셀 키트 room build·check — 평면에서 LDtk 방`, `[ASSET] 시험 방 test.two-rooms`.

### Task 3: 다시 칠하기 (`room repaint`)와 게임에서 확인

**Files:**
- Modify: `Tools/pixel-kit/pixelkit/room_ldtk.py`, `cli.py`, `Tools/pixel-kit/tests/test_room_ldtk.py`, `Tools/pixel-kit/README.md`
- Create: `Tools/pixel-kit/tests/fixtures/room-saved-by-editor.ldtk`(편집기가 저장한 모양을 흉내 낸 고정 파일 — 키 순서가 다르고, 머리말 `app`이 `LDtk`, 모르는 키와 `Furniture` 엔티티 하나가 들어 있고, `Collision`이 `Wall` 타일과 어긋나 있다)
- Create: `game/tests/Rootborn.World.Tests/PixelKitRoomTests.cs`, `game/tests/Rootborn.Host.Tests/PixelKitRoomHostTests.cs`

**Interfaces:**
- Consumes: Task 2의 LDtk 모양과 `check_grid`.
- Produces: `repaint_room(ldtk_path, dry_run=False) -> list[str]`, `check_room(ldtk_path) -> list[Problem]`(+ 코드 `room-art-mismatch`: `Collision`과 타일 층이 어긋난 칸 수, `room-not-a-room`: 필요한 층이 없음).
- [ ] **Step 1: 실패하는 테스트(도구)** — 고정 파일에 `repaint`: `Wall`·`Ground` 타일이 `Collision`과 맞게 됨, `Furniture` 엔티티·모르는 키·머리말·다른 층이 그대로(JSON 값 비교), `tiles.png`에 새 타일이 덧붙고 옛 자리는 그대로, 두 번 돌리면 두 번째는 바꿀 것 없음, 필요한 층이 없는 LDtk(예: 임시로 만든 다른 모양)는 `room-not-a-room`으로 거절하고 안 씀, `check`가 다시 칠하기 전에는 `room-art-mismatch`를 내고 뒤에는 0.
- [ ] **Step 2:** 실패를 본다 → **Step 3:** 구현 → **Step 4:** 도구 테스트 전체 통과.
- [ ] **Step 5: 게임 읽기 테스트(World, 자동 테스트)** — `PixelKitRoomTests`: `art/world/rooms/test.two-rooms/maps.json`을 게임의 맵 읽기(기존 World 테스트가 맵 목록을 읽는 방식 — `MapContractTests`나 작업 9의 시험 맵 읽기를 본다)로 읽어, `Collision` 1인 칸은 막히고 2인 칸은 열림, 도착 지점에서 문간을 지나 옆방 바닥까지 걸어서 닿음(`MainChainWalkTests`가 쓰는 길 찾기 도우미). 좌표를 테스트에 적지 않고 LDtk의 `Collision`에서 읽는다.
- [ ] **Step 6: 화면 테스트(Host, 장치 이벤트 주입)** — `PixelKitRoomHostTests.PIXEL_KIT_ROOM_001`: `--maps`로 시험 방을 띄워(작업 9의 `PixelKitFurnitureHostTests`와 스킬 `host-play-test`를 본다) 캐릭터가 벽 쪽으로 걸으면 벽 칸 앞에서 멈추고(`PlayerCell`이 벽 칸이 되지 않음), 문간을 지나 옆방으로 걸어간다(`WalkTo`). 스크린샷을 `output/pixel-kit/room/` 아래에 남기고 직접 연다(벽이 서 보이는지, 빈 그림·잘린 타일·이음매). 그림 픽셀을 단언하지 않는다.
- [ ] **Step 7:** dotnet은 한 번에 한 프로젝트씩: World(기존 121 + 새 것), Host는 `PixelKit` 필터. Data 438은 그대로여야 한다(데이터를 안 고쳤으므로 돌려 확인만).
- [ ] **Step 8:** 커밋 둘 — `[CHORE] 픽셀 키트 room repaint — LDtk에서 칠한 벽을 그림으로`, `[TEST] 시험 방을 게임이 읽고 벽에 막히는지`.

### Task 4: 스킬과 문서

**Files:**
- Create: `.claude/skills/pixel-kit-room/SKILL.md`, `.agents/skills/pixel-kit-room/SKILL.md`(바이트 단위로 같게)
- Modify: `.claude/rules/engine-migration.md`(스킬 목록 한 줄), `.claude/rules/changelog.md`(항목 하나), `Tools/pixel-kit/README.md`(빠진 것), `game/world/README.md`("키트로 만든 방" 한 단락: 층 이름과 `Collision` 값이 약속과 같다는 것, 도구를 가리킴)

- [ ] **Step 1:** 스킬을 쓴다. 들어갈 것: 언제 쓰는가(실내 방·집 구조를 만들거나 벽을 고칠 때, "벽", "방 구조", "걸을 수 있는 칸", "LDtk 방"), 평면 파일 쓰는 법(짧은 예 하나), 순서 `room build → room check → --maps로 게임에서 보기 → 스크린샷을 직접 연다`, LDtk 편집기에서 `Collision` 층을 칠한 뒤(1 벽, 2 바닥) `room repaint`, 가구는 `Furniture` 층에 사람이 놓는다, 하지 않는 것(지금 집 맵 `House`를 바꾸는 일은 스킬 `save-compat-change` 먼저 — 크기가 바뀌면 세이브의 가구 자리가 걸린다; 걸어서 지나는 박힌 문; 벽 옆면; 손으로 `Wall`·`Ground` 층을 칠하기 — 다시 칠하면 덮인다), 아직 확인 못 한 것(LDtk 편집기로 저장한 실제 파일은 사용자가 처음 해 볼 때 확인). README를 베끼지 않고 가리킨다.
- [ ] **Step 2:** 문서에 적은 명령을 실제로 돌려 본다(읽기만 하는 것과 `--dry-run`). `cmp`로 스킬 두 사본이 같은지.
- [ ] **Step 3:** changelog 항목(형식: 요청, 배경, 변경, 일반화 — 일반화: "그림과 충돌처럼 함께 맞아야 하는 것은 원본을 하나로 두고 둘 다 거기서 만든다").
- [ ] **Step 4:** 도구 테스트 전체, `kit.py lint`, `bash Scripts/ci/run-all.sh --checks-only`. 커밋 `[DOCS] pixel-kit-room 스킬과 문서`.
