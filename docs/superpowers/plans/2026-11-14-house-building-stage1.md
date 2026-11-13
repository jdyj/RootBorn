# 집 짓기와 꾸미기 1단계 (가) — 새 집과 조합 그리기 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 지금 집 맵을 키트로 만든 새 집(벽 정면 2칸 + 16px 띠, `Build` 층)으로 바꾸고, 옛 세이브의 가구를 옮기거나 보관하며, 게임이 방 격자에서 칸마다 벽·바닥 그림을 조합해 그린다(꾸밈은 기본값만).

**Architecture:** 키트(Python)가 새 집 LDtk와 게임용 조각 시트·시험 표를 내보낸다. Core `Rootborn.Core.Building`이 격자 → 칸마다 픽셀 출처표(`RoomTilePlan`)를 내고, World가 출처표 + 꾸밈 → 픽셀(엔진 없음)을, Host는 그 픽셀을 텍스처로 올려 맵의 `Wall`·`Ground` 층 대신 그린다. 고르기 규칙은 Python과 C# 두 곳에 있고 같은 시험 표(`room-vectors.json`)로 묶는다.

**Tech Stack:** C# .NET 8 + NUnit(Core/Data/World/Game/Screens/Host), MonoGame 3.8.6.1(Host만), Python 3.11 + Pillow + unittest(키트).

**Spec:** `docs/superpowers/specs/2026-11-14-house-building-design.md`(12.1절 1-1 ~ 1-5). 결정 대기: `docs/superpowers/specs/2026-11-14-house-building-open-decisions.md`.

**계획을 둘로 나눈다.** 이 파일은 1-1 ~ 1-5(새 집이 보이고, 옛 가구가 옮겨지고, 게임이 조합해 그린다)와 1-2에 딸린 보관 분류다. 1-6 ~ 1-8(꾸밈 규칙·세이브·비용, 꾸미기 화면, 같이 하기)과 결정 대기의 새 게임 처음 배치·가게 진열·띠 색 고르기·인테리어 기술은 다음 계획 `docs/superpowers/plans/2026-11-14-house-building-stage1b.md`(이 계획이 합쳐진 뒤 쓴다)로 넘긴다. 이유: 1-6 이후는 1-5가 실제로 그린 화면을 사용자가 본 뒤에야 화면 구성을 정할 수 있고, 한 계획이 10개 작업을 넘으면 검토자가 한 번에 볼 수 없다. 다음 계획이 기댈 약속은 맨 끝 "다음 계획이 기대는 것".

## Global Constraints

- 규칙은 Core(`game/Rootborn.Core/src`)에만. Core는 `System.IO`·`System.Text.Json`·엔진을 참조하지 않는다(CI `check-core-references.sh`). 화면은 `GameSession` 명령·조회만 부른다. MonoGame 참조는 `Rootborn.Host`·`Rootborn.Host.Tests`에만.
- 엔티티 id로 분기하지 않는다 — 맵 id(`"House"`)·꾸밈 id·가구 id 포함. 짓는 집은 맵 항목의 `build` 성질로 고른다(`check-no-entity-id-branching.sh`).
- 사람이 읽는 문구는 현지화 키로, `game/localization/ko.json`·`en.json` 둘 다에. 값이 드는 문구는 `{0}` 자리 표시 + `GameText.Format`. 글자 크기 14 미만 금지.
- 그림 파일 경로·좌표·색을 코드에 적지 않는다. 꾸밈 그림은 정의의 `sheet`·`art` + 좌표표(`data/atlases/<PNG 경로>.atlas.json`), 맵은 `game/world/maps.json`.
- 칸 좌표: 맵 칸 `CellPos`(y 아래로 +), 가구 칸 `GridPoint`(y 위로 +). 바꾸는 곳은 `FurnitureRoom.ToPlacement/ToMap`(Screens)과 `HouseGrids.FromMap`(Server)뿐.
- 지급·옮김은 전부 아니면 전무. 실패하면 배치·가방·돈이 그대로. 같은 요청 두 번은 상태로 한 번만.
- 세이브: 하위호환(옛 세이브가 열리고 진행이 남음)과 상위호환(새 세이브가 옛 빌드에서도 플레이됨)을 테스트로 확인한 뒤에만 끝. 있는 고정 폴더(`game/tests/Rootborn.Data.Tests/Fixtures/saves/*`)는 고치지 않는다. 데이터에 없는 가구 id의 기록은 버리지 않는다. 읽기만으로 세이브 파일을 쓰지 않는다. 스킬 `save-compat-change`.
- 테스트는 임시 폴더와 고정 파일만 쓴다. 사용자 세이브 폴더(`AppData/LocalLow`)를 읽거나 쓰지 않는다.
- 매 프레임 다시 계산하지 않는다: 출처표는 맵을 읽을 때 한 번, 조합은 맵에 처음 들어설 때 한 번(다음 계획에서는 꾸밈 알림이 올 때만). 그리기 루프에서 새 객체·LINQ·문자열 이어 붙이기 금지.
- 실패 테스트 먼저. 테스트는 그림의 특정 픽셀 색을 단언하지 않는다(구조·출처·두 경로가 같은지는 된다).
- 근거 종류를 나눠 쓴다: 자동 테스트 / 장치 이벤트 주입(OS 입력 계층 미확인) / 직접 본 화면(스크린샷 경로). 화면을 바꾼 작업은 스크린샷을 직접 연 뒤에 끝났다고 한다.
- 커밋: 한글 제목·본문, prefix(`[FEATURE]` `[ASSET]` `[CHORE]` `[TEST]` `[DOCS]` `[META]` `[UI]`), 끝줄은 그 커밋을 만든 모델의 `Co-Authored-By: <모델 이름> <noreply@anthropic.com>`. 브랜치 `room-builder`에만, 푸시하지 않는다.
- 게임 수치(꾸밈 비용 벽지 2·바닥 1, 넣을 종류 10개, 처음부터 열린 6개)는 **시험용**이다 — 사용자가 값을 정하지 않았다(결정 대기 문서 1·7·9번).
- 사용자의 미커밋 파일 `art/world/rooms/test.two-rooms/{test.two-rooms.ldtk,tiles.png,tiles.slots.json}`를 건드리지 않는다(`git add` 하지 않고, 키트 명령으로 다시 쓰지 않는다). 커밋은 파일을 하나씩 골라 `git add <경로>`.
- dotnet은 이 PC에서 한 번에 한 프로젝트씩: `export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"` 뒤 `dotnet test game/tests/<프로젝트>`. Host는 `--filter`로 좁힌다. 합치기 전 전체는 `bash Scripts/remote-test/run.sh <커밋>`. 도구 테스트는 `python -m unittest discover -s Tools/pixel-kit/tests -t Tools/pixel-kit`(지금 488건 — 줄지 않는다). 프로젝트별 건수가 줄지 않았는지 본다.

## Review Focus

1. **현관 문이 바깥 벽 칸으로 옮겨진다.** 옛 문 엔티티는 (450,543) 29x21로 칸 (14,16)·(14,17)에 걸쳤다. 새 문은 벽 칸 (14,17)에 32x32로 놓이고, 안쪽 바닥 (14,16)에서 몸이 닿아야 열린다(`DoorWatch` 접촉 여유 0.02칸, `MapReach.Touches` 1px). 나가기·들어오기(도착 지점 `house-entry`), 마을의 `house` 문, NPC가 집에 드나드는 길, 배포 목록에 새 집 LDtk가 드는지 — Task 1·3의 테스트.
2. **옮기기가 아무것도 잃지 않는다.** 데이터에 없는 가구, 다른 면의 기록은 옮기지 않고 그대로 보존. 물건이 든 상자는 `Storage`째. 결과는 결정적이고, 읽기만으로 배치 파일을 쓰지 않으며, 같이 쓰는 집의 방(서버)과 손님이 같은 자리를 낸다 — Task 4.
3. **두 방 사이 세로 벽의 띠 칸**: 띠 왼쪽 8px과 오른쪽 8px의 바닥 주인이 서로 다른 칸이다(Python `Ground` 한 장 규칙과 다름). 출처표의 `own`이 둘로 갈리는지 — Task 6·7·9.
4. **두 언어의 규칙이 어긋나면 깨지는가.** Python만 고치고 시험 표를 다시 만들지 않으면 Python 테스트가, 시험 표만 바뀌면 C# 테스트가 깨진다. 지금 CI는 키트 테스트를 돌리지 않는다 — Task 6이 CI job을 더한다.
5. **조합 텍스처**: 매 프레임 만들지 않는다, 같은 조합은 한 번만, 맵을 떠나면 버린다, 상한(4,096칸)을 넘거나 조각이 없으면 문제를 알리고 LDtk `Wall`·`Ground` 층으로 그린다 — Task 9.

---

### Task 1: 키트 — 바깥 벽의 문과 `Build` 층

모델: 중간. 멈춤 없음.

**Files:**
- Modify: `Tools/pixel-kit/pixelkit/room_plan.py`(`Room`, `load_room`, `check_grid`), `Tools/pixel-kit/pixelkit/room_ldtk.py`(`LAYER_ORDER`, `LAYER_UIDS`, 층 정의, 엔티티, 방 폴더의 `maps.json`, `check_room`), `Tools/pixel-kit/pixelkit/cli.py`(`cmd_room`이 새 인자를 넘긴다)
- Test: `Tools/pixel-kit/tests/test_room_plan.py`, `Tools/pixel-kit/tests/test_room_ldtk.py`
- Modify: `Tools/pixel-kit/README.md` "방 만들기 — 평면에서 LDtk로", "검사 코드"

확인한 사실(15절 첫 줄이 풀림): 지금 `doors` 글자 칸은 바닥이 되므로 맨 아래 줄에 두면 `room-open-edge`(바닥이 맵 가장자리에 닿음)에 걸린다. 그래서 벽 칸에 문을 다는 새 키를 더한다(`doors`의 뜻은 그대로).

**Interfaces:**
- Consumes: `room_plan.load_room`, `check_grid(grid, spawn, doors, face_height)`, `room_ldtk.build_room(repo_root, room_id, out_dir=None, force=False, dry_run=False, kit=None, rooms_dir=None)`.
- Produces:
  - 평면 JSON 새 키 `"wallDoors": {"<글자>": "<Where 값>"}` — 그 칸은 벽(`#`)이고 Door 엔티티가 그 칸 `(x*32, y*32)`, 32x32에 놓인다. `"build": ["<0·1 글자 줄>", …]` — 평면과 같은 크기, `1` 지을 수 있음, `0` 고정.
  - `Room.wall_doors: Dict[Tuple[int, int], str]`, `Room.build: Optional[List[str]]`(둘 다 기본값 있음 — 있는 평면 파일은 그대로 읽힌다).
  - `check_grid(grid, spawn, doors, face_height, wall_doors=None, build=None) -> List[Problem]`, 새 코드: `room-door-no-floor`(벽 문의 상하좌우 넷 중 도착 지점에서 걸어 닿는 바닥이 없다), `room-build-ragged`(줄 수·길이가 평면과 다름), `room-build-char`(`0`·`1`이 아닌 글자), `room-build-fixed`(밖 칸, 도착 지점 칸, 문 칸, 벽 문에 닿은 바닥 칸이 `1`). 모두 막는 문제(경고 아님).
  - LDtk: IntGrid 층 `Build`(uid 7, 값 1 이름 `buildable`, 32px, 파일 안 순서는 `Collision` 바로 다음, 편집기 불투명도 0.25). 값 0은 IntGrid의 빈 칸 = 고정. `build`가 없으면 층을 만들지 않는다.
  - 방 폴더 `maps.json`의 맵 항목: `build`가 있으면 `"build": { "frameLayer": "Build" }`.
  - `check_room(ldtk_path)`: `Build` 층이 있으면 크기가 `Collision`과 같은지, 값 ∈ {0,1}, `Collision` 0(밖) 칸이 0인지 — 같은 코드로. `repaint_room`은 `Build` 층을 그대로 둔다.
- [ ] **Step 1: 실패하는 테스트**
  - `test_room_plan.py`: `test_wall_door_cell_stays_wall_and_is_listed`(글자 칸이 `#`, `wall_doors[(x,y)]`), `test_wall_door_on_bottom_edge_is_not_open_edge`(맨 아랫줄 벽 문 + 안쪽 바닥 → 문제 0), `test_wall_door_without_reachable_floor_is_problem`(→ `room-door-no-floor`), `test_build_rows_must_match_plan`(→ `room-build-ragged`), `test_build_unknown_char`(→ `room-build-char`), `test_build_marks_spawn_or_door_buildable_is_problem`(도착 지점·벽 문·그 안쪽 바닥 칸 각각 → `room-build-fixed`), `test_old_plan_without_new_keys_reads_as_before`(`art/kit/rooms/test.two-rooms.json` 읽기 결과가 같다).
  - `test_room_ldtk.py`: `test_build_layer_written_after_collision_with_plan_values`(층 순서, `intGridCsv` = build 줄), `test_no_build_key_no_build_layer`, `test_wall_door_entity_at_wall_cell`(`px` = (x*32, y*32), `Where`), `test_maps_json_has_build_frame_layer`, `test_repaint_keeps_build_layer`(JSON 값 비교), `test_check_room_build_layer_problems`(임시 파일의 `Build`를 밖 칸 1로 고치면 `room-build-fixed`).
- [ ] **Step 2: 실패를 본다** — `python -m unittest discover -s Tools/pixel-kit/tests -t Tools/pixel-kit -p "test_room_*.py"`. 기대: `TypeError`(모르는 인자)·`AttributeError`(`wall_doors`)·단언 실패.
- [ ] **Step 3: 구현** — 위 시그니처대로. `cmd_room`의 `check`·`build`가 `room.wall_doors`·`room.build`를 넘긴다. 벽 문도 `door_problems`(Where가 doors 표에 없음 알림)를 탄다.
- [ ] **Step 4: 통과를 본다** — 도구 테스트 전체(488 + 새 것) + `python Tools/pixel-kit/kit.py lint` 0 + `python Tools/pixel-kit/kit.py room check test.two-rooms` 문제 없음(**파일을 쓰지 않는 명령만** — 사용자의 미커밋 방을 다시 쓰지 않는다).
- [ ] **Step 5: 커밋** — `[CHORE] 픽셀 키트 방 — 바깥 벽의 문(wallDoors)과 Build 층`.

### Task 2: 새 집 평면과 LDtk

모델: 높은(평면 검산·그림 판단). 멈춤 없음(게임 화면은 Task 3에서).

**Files:**
- Create: `art/kit/rooms/house.json`
- Create(명령의 결과, 커밋): `art/world/rooms/house/{house.ldtk,tiles.png,tiles.slots.json,maps.json}`

**평면 검산 결과**(이 계획을 쓰며 `game/world/interactions.json`·`maps.json`·`Assets/LDtk/House/house.ldtk`에서 다시 읽고 `room_plan.check_grid`·`room_tiles.plan_tiles`로 돌림, 자동 계산): 설계서 11절 평면은 맞다. 도착 지점 `house-entry` (464,496) → 칸 (14,15), 일과 판 (560,368) → (17,11), 하루 끝 (720,496) → (22,15), NPC `location.home` (624,432) → (19,13) — 모두 바닥. 옛 문 엔티티 (450,543) 29x21은 (14,16)·(14,17)에 걸쳤다 → 새 문은 벽 칸 (14,17), 안쪽 바닥 (14,16). 바닥 283칸 모두 도착 지점에서 닿는다(`check_grid` 문제 0), 설계서의 `=`(정면) 표시와 `classify`의 정면 칸이 모두 같다, 벽 열쇠 31·바닥 열쇠 3(`floor`, `floor:shade`, `floor:shade:8-24`), 좁은 정면 칸 2, Python 고르기 0.22초. 설계서와 다른 점은 문 칸이 `doors`(바닥)가 아니라 `wallDoors`(벽)라는 것뿐이다.

`house.json`(이대로 쓴다; `Build` 0은 바깥 테두리, 바깥 북쪽 벽의 정면 1·2줄(가로 13열 칸막이 제외), 현관 안쪽 (14,16), 도착 지점·일과 판·하루 끝·NPC 칸과 그 상하좌우 바닥 칸):

```json
{
  "id": "house", "wall": "paper", "floor": "floor.plank", "top": "wall.top.cream", "face_height": 2,
  "spawn": [14, 15],
  "wallDoors": { "D": "town" },
  "plan": [
    "############################", "############################", "############################",
    "#............#.............#", "#............#.............#", "#............#.............#",
    "#............#.............#", "#............#######.#######", "#............#######.#######",
    "#............#######.#######", "######.#######.............#", "######.#######.............#",
    "######.#######.............#", "#............#.............#", "#..........................#",
    "#..........................#", "#............#.............#", "##############D#############"
  ],
  "build": [
    "0000000000000000000000000000", "0000000000000100000000000000", "0000000000000100000000000000",
    "0111111111111111111111111110", "0111111111111111111111111110", "0111111111111111111111111110",
    "0111111111111111111111111110", "0111111111111111111111111110", "0111111111111111111111111110",
    "0111111111111111111111111110", "0111111111111111101111111110", "0111111111111111000111111110",
    "0111111111111111101011111110", "0111111111111111110001111110", "0111111111111101111011011110",
    "0111111111111000111110001110", "0111111111111101111111011110", "0000000000000000000000000000"
  ]
}
```

- [ ] **Step 1: 실패하는 테스트** — `Tools/pixel-kit/tests/test_room_plan.py`에 `test_house_plan_is_clean_and_keeps_todays_points`: `load_room(art/kit/rooms/house.json)`, `check_grid(...)` 문제 0, 28x18, 위 네 지점 칸과 (14,16)이 바닥이고 `build` 0, (14,17)이 `wall_doors`. (좌표를 적는 것은 이 시험뿐 — "지금 집과 같은 칸"이 이 평면의 약속이다.)
- [ ] **Step 2: 실패를 본다** — 같은 명령, 기대: 파일 없음.
- [ ] **Step 3:** `house.json`을 쓰고 `python Tools/pixel-kit/kit.py room build house`(기본 출력 `art/world/rooms/house/`).
- [ ] **Step 4:** `python Tools/pixel-kit/kit.py room check art/world/rooms/house/house.ldtk` 문제 0. 도구 테스트 전체. 생성된 `maps.json`의 맵 id가 `House`, 레벨 `House`, 도착 지점 id `house-entry`인지 본다.
- [ ] **Step 5:** 미리 보기 그림을 `output/pixel-kit/room/house-preview-x2.png`(커밋 안 함, `tiles.png`·자리 표로 칸을 깐 스크립트)로 만들어 직접 연다: 정면 2칸이 서 보이는지, 세로 벽 x=13의 좁은 정면, 문간 셋, 이음매·끊긴 외곽선. 현관 칸(14,17)은 다른 바깥 벽과 똑같이 보인다(문 그림 없음 — 결정 대기 13번).
- [ ] **Step 6: 커밋** — `[ASSET] 새 집 방 house — 28x18, 정면 2칸 벽, Build 층, 현관은 바깥 벽 칸`.

### Task 3: 게임이 새 집을 쓴다

모델: 중간(여러 파일 통합). **멈춤: 새 집을 걷는 스크린샷을 사용자에게 보이고 기다린다.**

**Files:**
- Modify: `game/world/maps.json` 집 항목 — `"ldtk": "art/world/rooms/house/house.ldtk"`, `"level": "House"`, `"collision": { "rule": "interior-auto" }`, `"build": { "frameLayer": "Build", "frameId": "house.v2" }`. `id`, `viewCells` 10, `doorField` `Where`, `spawnField` `Position`, `doors`, `spawns`는 그대로. 위 주석에 "집은 키트 방(art/kit/rooms/house.json)" 한 줄.
- Create: `game/Rootborn.World/MapBuildSettings.cs`; Modify: `game/Rootborn.World/WorldCatalog.cs`(`MapEntry.Build`와 읽기), `game/Rootborn.World/MapChecks.cs`(`CheckMap`에 짓는 집 검사)
- Modify: `Tools/publish/content.txt`(`@ldtk art/world/rooms/house/house.ldtk` 한 줄), `Tools/publish/content-server.txt`(`art/world/rooms/house/*.ldtk` 한 줄)
- Create: `Tools/pixel-kit/tests/fixtures/ldtk-1.5.3/house.ldtk`(옛 `Assets/LDtk/House/house.ldtk`를 바이트 그대로 복사 — 편집기 저장 견본); Modify: `Tools/pixel-kit/tests/test_ldtk_editor_shape.py`(37줄), `Tools/pixel-kit/tests/test_room_ldtk.py`(346~355줄), `Tools/pixel-kit/pixelkit/ldtk_common.py` 머리 주석, `Tools/pixel-kit/tests/fixtures/ldtk-1.5.3/README.md`·`editor-keys.json`의 `source` 글, `Tools/pixel-kit/README.md` 408·412·448줄 — 모두 새 견본 경로로
- Delete: `Assets/LDtk/House/house.ldtk`(폴더째. 타일셋 PNG는 가구 좌표표가 쓰므로 지우지 않는다)
- Modify(옛 집에 기대던 테스트): `game/tests/Rootborn.World.Tests/HouseSizeTests.cs`, `game/tests/Rootborn.Host.Tests/HouseSizeScreenTests.cs`
- Test: `game/tests/Rootborn.World.Tests/HouseMapTests.cs`(새), `game/tests/Rootborn.World.Tests/MapContractTests.cs`(검사 하나 더), `game/tests/Rootborn.Host.Tests/HouseMapHostTests.cs`(새)

**Interfaces:**
- Consumes: Task 2의 파일들.
- Produces:
  - `public sealed class MapBuildSettings { public string FrameLayer; public string FrameId; }`(이 작업은 두 필드만 — Task 9가 더한다), `MapEntry.Build`(없으면 null). 읽기: `m.TryGetProperty("build", …)`; `frameLayer`·`frameId`가 비면 `WorldCatalog`가 오류를 낸다(빠진 값은 분명한 오류).
  - `MapChecks` 새 문제(짓는 집만): `"짓는 집: Build 층 <이름>이 없다"`, `"짓는 집: Build 층 크기가 맵과 다르다"`, `"짓는 집: Build 값은 0·1이어야 한다 <칸>"`, `"짓는 집: <도착 지점·상호작용·NPC 자리·문 앞> <칸>이 Build 1이다"`, `"짓는 집: … <칸>이 바닥이 아니다"`. "문 앞"은 문 상자 둘레에서 걸어 닿는 바닥 칸.
- [ ] **Step 1: 실패하는 테스트**
  - World `HouseMapTests`(자동 테스트, 좌표는 맵·평면에서 읽는다): `HouseIsTheKitRoom_CollisionEqualsThePlan`(`art/kit/rooms/house.json`의 `#`/`.`과 `Collision`이 같다), `TheOnlyDoorLeadsToTown_AndTouchesTheFloorInside`(목적지 `Town/town-home-entry`, 문 상자가 도착 지점에서 닿는 칸에 맞닿음), `TodaysPointsAreFloor_AndFixedInBuild`(도착 지점·`things`·`npcAnchors` 칸이 막히지 않고 `Build` 0), `EveryFloorIsReachableFromTheEntry`.
  - `MapContractTests.Checks_CatchEachBrokenPromise`에 짓는 집 사례 하나(임시 맵 목록에 `build`를 단 맵, 도착 지점 칸을 `Build` 1로 고친 LDtk 사본 → 문제가 나온다).
  - Host `HouseMapHostTests.HOUSE_MAP_001_NewGameWalksEveryRoomOfTheNewHouse_AndOutTheDoor`: 새 게임(`NewGameJourneyTests`처럼 슬롯 선택부터 입력으로) → 집 → 바닥을 채워 찾은 방 넷 각각의 가운데로 `WalkTo` → 방마다 스크린샷 → 일과 판·하루 끝 앞까지 `WalkTo` → 현관 안쪽에서 아래로 눌러 마을로 나가고 마을의 `house` 문으로 다시 들어온다(`player.World.Map.Id`가 `Town` → `House`). 스크린샷 `output/house-building/stage1/house-map-NN-*.png`. 그림 픽셀은 단언하지 않는다.
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.World.Tests --filter "FullyQualifiedName~HouseMapTests|FullyQualifiedName~MapContractTests"`. 기대: Collision 불일치(아직 옛 집), `MapEntry.Build` 없음.
- [ ] **Step 3: 구현** — maps.json, `MapBuildSettings`, `MapChecks`, 배포 목록, 키트 견본 옮기기, 옛 집 지우기. 옛 집에 기대던 테스트:
  - `HouseSizeTests.BiggerHouse`·`TwoHouseSizes_…`: 원본 경로를 `Repo.Loader().Catalog.Find("House").Ldtk`에서 얻는다.
  - `HouseSizeTests.House_MatchesTheUnitySceneLayout`: 지운다(Unity 씬의 집 그림 수를 세는 테스트 — 그 맵이 없다). 출구 x(464±0.6)는 `HouseMapTests`가 맡는다.
  - `HouseSizeTests.ActorIndex_PutsTheCharacter…`: 새 집에는 정렬 그림이 없다 → `new MapPicture()`에 `AddSorted`로 정렬값 다섯을 넣어 같은 단언을 한다.
  - `HouseSizeScreenTests`: 경로를 맵 목록에서, `WalkTo(new CellPos(26, 9))`는 "원래 너비 안에서 도착 지점이 닿는 가장 오른쪽 바닥 칸"을 `Collision`에서 찾아 쓴다.
  - 그 밖은 찾아서 고친다: `git grep -nE "Assets/LDtk/House|house\.ldtk" -- game Tools Scripts` 0건이 될 때까지, `git grep -n "\"House\"" -- game/tests`의 테스트는 Step 4에서 돌려 본다.
- [ ] **Step 4: 통과를 본다** — 한 프로젝트씩: `Rootborn.World.Tests`(전부 — `MainChainWalkTests`, `NpcRouteContractTests`, `InteractionDataTests`, `NpcStageTests`, `WorldSlotDataTests` 포함), `Rootborn.Game.Tests`(`FurnitureTests`, `HouseRoom`을 쓰는 것, `MainChainTests`), `Rootborn.Screens.Tests`, `Rootborn.Server.Tests`(`HouseGridsTests`, `PublishListStartupTests` — 새 경로가 배포 목록에 없으면 여기서 실패해야 한다), `Rootborn.Simulation.Tests`, Host는 `--filter "FullyQualifiedName~HouseMap|FullyQualifiedName~HouseSize|FullyQualifiedName~FurniturePlacementHost|FullyQualifiedName~NpcWalkHost|FullyQualifiedName~StorageChestHost"`. 도구 테스트 전체. `bash Scripts/ci/run-all.sh --checks-only`. 메인 체인 Host(`MainChainHostTests`, 약 8분)는 원격 `bash Scripts/remote-test/run.sh <커밋>`.
- [ ] **Step 5: 직접 본다** — `output/house-building/stage1/house-map-*.png`를 열어 벽이 서 보이는지, 캐릭터가 정면 앞에서 멈추는지, 맵 밖 노출·잘린 타일이 없는지. **사용자에게 스크린샷 경로를 보이고 멈춘다**(새 게임의 집에는 가구가 없다 — 처음 배치는 다음 계획 B6).
- [ ] **Step 6: 커밋 셋** — `[ASSET] 집 맵을 키트 방으로 교체 — 옛 house.ldtk는 도구 견본으로만 남김`(maps.json, 배포 목록, 옛 파일 삭제, 견본 복사), `[FEATURE] 맵 build 성질과 짓는 집 검사`(MapBuildSettings, MapChecks), `[TEST] 새 집 맵 계약과 걷기, 옛 집 테스트 정리`.

### Task 4: 옛 가구를 가까운 자리로 옮기고 못 놓은 것은 보관 (HOUSE-002)

모델: 높은(세이브). 멈춤 없음.

**Files:**
- Modify: `game/Rootborn.Core/src/Placement/HousePlacementModel.cs`(`IPlacementGrid.FloorCells`), `game/Rootborn.Core/src/Placement/FurnitureReplay.cs`, `game/Rootborn.Core/src/Placement/HouseFurnitureLayout.cs`, `game/Rootborn.Core/src/Room/RoomHouse.cs`(`RoomHouseRules`의 `layout.Restore`, 65줄 근처), `game/Rootborn.Game/GameSession.Furniture.cs`(`LoadHouse`, 새 명령·조회), `game/Rootborn.Game/GameSession.SharedHouse.cs`(`SharedHouseLayout`)
- Test: `game/tests/Rootborn.Core.Tests/FurnitureRelocationTests.cs`(새), `game/tests/Rootborn.Game.Tests/HouseMoveTests.cs`(새)
- Modify: `game/tests/Rootborn.Game.Tests/SCENARIOS.md`, `.claude/rules/testing-discipline.md` 시나리오 표 — `HOUSE-002 | 집 맵 교체 뒤 옛 가구가 옮겨지거나 보관되고 사라지지 않음 | Core FurnitureRelocationTests, Game HouseMoveTests, Host HouseMoveHostTests`

세이브 모양은 바뀌지 않는다(새 파일·필드 없음). 바뀌는 것은 "옛 좌표의 기록을 새 집이 어떻게 읽는가"라서 `save-compat-change`의 하위·상위 테스트를 둔다.

**Interfaces:**
- Consumes: `FurnitureReplay.Plan(model, surfaceId, records, catalog, acceptedSurfaceIds)`, `HouseFurnitureLayout.Restore(...)`(**동작을 바꾸지 않는다** — 일터와 기존 테스트가 쓴다).
- Produces:
  - `IPlacementGrid.IReadOnlyCollection<GridPoint> FloorCells { get; }`(구현은 `PlacementGrid` 하나).
  - `FurnitureReplayEntry.bool Moved { get; }`(생성자에 선택 인자 `bool moved = false`).
  - `public static FurnitureReplayPlan FurnitureReplay.PlanRelocating(HousePlacementModel model, string surfaceId, IReadOnlyList<FurniturePlacementSaveData> records, IEnumerable<FurnitureDefinition> catalog, IReadOnlyList<string> acceptedSurfaceIds = null)` — 알고리즘: (1) `Plan`과 같이 제자리 기록을 받는다. (2) 이유가 `Blocked`·`DuplicateFootprint`인 못 놓은 기록만, 원래 기록 순서대로, `FloorCells`를 원래 기준 칸에서 맨해튼 거리 오름차순, 같으면 가구 칸 Y 내림차순(화면 위), 같으면 X 오름차순으로 훑어 **같은 방향**으로 처음 들어가는 칸에 놓는다. "들어간다" = `CanOccupy` + 앞에서 받은 가구와 차지 칸·그림 칸(`TileCells`)이 겹치지 않음 + 이동을 막는 가구면 `PreservesAccess`. 거리 상한 없음. 옮긴 기록은 사본(앵커만 바뀜, `Storage`·`StateJson`·`TileName` 그대로), `Moved = true`, `Accepted`에 기록 순서로 끼운다(`SourceIndex` 유지). 나머지 이유(`MissingDefinition`·`WrongSurface`·`AmbiguousDefinition`·`InvalidDirection`)는 옮기지 않는다.
  - `public sealed class HouseFurnitureRelocation { public int Moved { get; } public int Stored { get; } }`
  - `HouseFurnitureLayout`: `public HouseFurnitureRelocation RestoreRelocating(IReadOnlyList<FurniturePlacementSaveData> records, IEnumerable<FurnitureDefinition> catalog, IReadOnlyList<string> instanceIds = null)`(저장하지 않는다); `public IReadOnlyList<StoredFurniture> Stored { get; }`(못 놓은 기록 중 정의가 있고 이유가 `Blocked`·`DuplicateFootprint`인 것, 기록 순서); `public bool CanPlaceStored(int index, GridPoint anchor, FurniturePlacementDirection direction)`; `public bool TryPlaceStored(int index, GridPoint anchor, FurniturePlacementDirection direction)`(기록을 못 놓은 목록에서 빼 새 앵커·방향으로 `_placed` 끝에 넣고 전체를 한 번 commit, 실패면 무변경, `PlacementCommitted`는 내지 않는다).
  - `public sealed class StoredFurniture { public int Index { get; } public FurnitureDefinition Definition { get; } public FurniturePlacementDirection Direction { get; } public bool HoldsItems { get; } }`
  - `GameSession`: `public IReadOnlyList<StoredFurniture> StoredFurniture()`(집 밖이면 빈 목록), `public bool CanPlaceStoredFurniture(int index, GridPoint anchor, FurniturePlacementDirection direction)`, `public bool PlaceStoredFurniture(int index, GridPoint anchor, FurniturePlacementDirection direction)`(같이 쓰는 집·일터에서는 false — 같이 쓰는 집은 다음 계획 B7; 성공하면 `GameChangeTopics.Furniture`), `public bool TryTakeHouseRelocationNotice(out int moved, out int stored)`(집을 연 뒤 이 판에서 처음 한 번만, 둘 다 0이면 false — 메모리만, 저장하지 않는다).
  - `LoadHouse`(혼자)와 `SharedHouseLayout`·`RoomHouseRules`(같이 쓰는 집 손님과 방)는 `RestoreRelocating`을 쓴다. 일터(`GameSession.Workspace`)는 `Restore` 그대로.
- [ ] **Step 1: 실패하는 테스트**
  - Core `FurnitureRelocationTests`(작은 `PlacementGrid`로): `BlockedRecord_MovesToTheNearestFittingCell_SameDirection`, `TiesGoUpThenLeft`, `RecordsThatFitStay_AndAreNotDisplacedByMovedOnes`(제자리 먼저), `MovedFurniture_DoesNotOverlapCellsOrTiles_AndKeepsAccess`, `ChestWithItems_MovesWithItsStorage`, `UnknownOrWrongSurfaceRecords_AreKeptUnmoved_AndNotInStored`, `NoRoomAnywhere_GoesToStored_AndIsSavedBack`(`BuildSaveRecords`에 원래 앵커로 남음), `SameInput_SameResult`(두 번 돌려 같은 기록 열), `Restore_IsUnchanged`(같은 입력에서 `Restore`는 여전히 못 놓음), `PlaceStored_FitsOrChangesNothing`(commit false면 무변경, 맞지 않는 칸이면 false), `RelocationCounts`(Moved, Stored).
  - Game `HouseMoveTests`(실제 `data/`, 임시 폴더 저장소, `TestWorld`·`HouseRoom`):
    - `HOUSE_002_OldLayoutOnNewWalls_IsMovedOrStored_NothingLost`: 임시 슬롯에 지금 모양의 배치 파일을 쓴다 — 새 집 벽 칸(맵 `Collision` 1인 안쪽 칸을 찾아 쓴다) 위의 책상, 물건이 든 상자(데이터가 상자라고 한 가구, `Storage` 하나), 데이터에 없는 가구 id 하나. `EnterHouse` 뒤 책상·상자는 `Placed`(상자 내용물 그대로), 모르는 기록은 `BuildSaveRecords`에 그대로, 개수 합 = 처음 기록 수.
    - `EditorFixtureSlot_DeskFromUnityCoordinates_IsMovedIntoTheHouse`: `saves.CopyFixture("editor-20261105/slot-1", "slot-1")`(UnitySaveTests 방식) → 집에 들어가면 `story.study-desk`가 놓인다.
    - `ReadingDoesNotWriteTheLayoutFile`: 들어가기 전후 배치 파일 바이트가 같다.
    - `AfterAChange_TheFileHoldsMovedPositions_OldShapeReadsIt`(상위호환): 가구 하나를 옮긴 뒤 파일을 `FurniturePlacementLayoutSaveData`로 읽어 모든 기록(옮긴 것 새 자리, 보관·모르는 것 원래 자리)이 있다 — 옛 빌드는 같은 모양을 읽고, 새 벽 칸은 옛 집에서 바닥이라 그대로 놓인다.
    - `NextSession_SamePlacement`: 저장 → 새 `GameSession` → 같은 `Placed`·`Stored`.
    - `PlaceStored_PlacesTheRecord_AndSecondRequestDoesNothing`: 보관된 것 하나를 놓고 같은 index·칸으로 다시 부르면 false, 목록·파일 그대로. `TryTakeHouseRelocationNotice`는 첫 번만 true.
    - `SharedHouse_GuestAndRoomPlaceTheSame`(같이 쓰는 집이 같은 코드): `RoomHouseRules`로 판정한 칸과 손님 `House.Placed`가 같다.
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Core.Tests --filter "FullyQualifiedName~FurnitureRelocation"`(기대: 컴파일 오류 — `PlanRelocating` 없음), 구현 뒤 Game도.
- [ ] **Step 3: 구현** — 위 시그니처와 알고리즘. 상자의 "물건이 든 상자는 치우지 않는다" 규칙은 보관된 상자에도 그대로(`HoldsItems`).
- [ ] **Step 4: 통과를 본다** — Core 전부, Game 전부(`SavedFilesAreRegisteredTests`, `NewerSlotIsPlayableTests`, `UnitySaveTests`, `WorkspaceTests` 포함), Data(`OldSaveTests`), Server(`HouseGridsTests`), Net.
- [ ] **Step 5: 커밋** — `[FEATURE] 집 가구 — 못 놓은 기록을 가까운 자리로 옮기고 나머지는 보관(HOUSE-002)`. 본문: 세이브 모양 그대로, 읽기만으로 쓰지 않음, 하위·상위 테스트 이름.

### Task 5: 가구 배치 화면의 보관 분류와 첫 입장 알림

모델: 중간. **멈춤: 옮겨진 옛 가구와 보관 목록 스크린샷을 사용자에게 보인다.**

**Files:**
- Modify: `game/Rootborn.Screens/Furniture/FurniturePlacementScreen.cs`(분류 줄 가구/보관, 보관 목록 노드, `Metrics`에 분류 줄·목록 수치)
- Create: `game/Rootborn.Screens/Furniture/StoredFurnitureModel.cs`, `game/Rootborn.Screens/Furniture/HouseMovedNoticeScreen.cs`(`StayedUpNoticeScreen`과 같은 틀 + 여는 쪽 `HouseMovedTrigger`)
- Modify: `game/Rootborn.Screens/ScreenCatalog.cs`(`HouseMovedNotice` 등록 + `AddListener`), `game/localization/ko.json`, `en.json`
- Test: `game/tests/Rootborn.Screens.Tests/Furniture/StoredFurnitureScreenTests.cs`, `game/tests/Rootborn.Screens.Tests/Furniture/HouseMovedNoticeTests.cs`, `game/tests/Rootborn.Host.Tests/HouseMoveHostTests.cs`

**Interfaces:**
- Consumes: Task 4의 `GameSession.StoredFurniture()`, `CanPlaceStoredFurniture`, `PlaceStoredFurniture`, `TryTakeHouseRelocationNotice`.
- Produces:
  - 요소 id: `furniture.category.furniture`, `furniture.category.stored`(분류 줄 단추 — 다음 계획이 `furniture.category.finish`를 같은 줄에 더한다), `furniture.stored.list`, `furniture.stored.row.<n>`, `furniture.stored.empty`, `furniture.stored.reason`; 알림 `house-moved-notice`, `house-moved-notice.message`.
  - 문구 키: `ui.furniture.category.furniture`(가구), `ui.furniture.category.stored`(보관), `ui.furniture.stored.empty`(보관한 가구가 없어요), `ui.furniture.stored.reason.shared`(같이 쓰는 집에서는 아직 꺼내 놓을 수 없어요), `ui.notice.house-moved`(새 집으로 가구 {0}개를 옮겼고 {1}개는 보관했어요. 가구 배치(P)의 보관에서 꺼내 놓을 수 있어요.) — en도.
  - 분류마다 따로 노드를 두고 켜고 끈다. 고른 분류는 밝기 + 튀어나옴. 보관 줄은 보이는 줄만(`UiScrollList`). 줄을 고르면 가구 고를 때와 같은 미리 보기(판정은 `CanPlaceStoredFurniture`), 칸을 누르면 `PlaceStoredFurniture`.
  - 알림: 알림 층, 이동을 막지 않음, 5초 뒤 닫힘. 여는 쪽은 `Furniture` 알림을 받을 때 `TryTakeHouseRelocationNotice`가 true면 연다(규칙 판단 없음).
- [ ] **Step 1: 실패하는 테스트**
  - Screens `StoredFurnitureScreenTests`: 분류를 바꾸면 노드가 켜지고 꺼짐, 보관 둘이면 줄 둘(이름 키), 줄 → 칸 누르면 놓이고 줄이 사라짐, 비면 빈 문구, 같이 쓰는 집에서는 이유 문구와 놓기 거절, 긴 문구(2배)에서 줄 글자가 패널 밖으로 안 나감.
  - Screens `HouseMovedNoticeTests`: 옮긴 슬롯으로 집에 들어서면 한 번 열림, 다시 들어서도 안 열림, 5초 뒤 닫힘, 언어 바꾸면 글자 바뀜, 0·0이면 안 열림.
  - 모든 언어 문구 키와 시험 테마는 기존 테스트(`LocalizationKeyTests`류, Host `TestThemeScreensTests`)가 본다 — 새 화면이 그 목록에 저절로 드는지 확인한다.
  - Host `HouseMoveHostTests.HOUSE_MOVE_001_OldFurnitureIsMoved_StoredOneIsPlacedFromTheStoredTab`: 임시 `--save-root`에 Task 4의 시험 배치(벽 칸 위 책상 + 아무 데도 안 들어가는 기록 하나 — 가장 큰 차지 칸의 가구를 데이터에서 골라 방이 들어갈 칸이 없게 앞 기록으로 채운다)를 쓴 슬롯 → 슬롯 선택에서 이어 하기(입력) → 알림 스크린샷 → P → 보관 분류 단추 → 줄 → 빈 바닥 칸 → 놓임 스크린샷. `output/house-building/stage1/house-move-NN-*.png`.
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Screens.Tests --filter "FullyQualifiedName~StoredFurniture|FullyQualifiedName~HouseMovedNotice"`.
- [ ] **Step 3: 구현.**
- [ ] **Step 4: 통과를 본다** — Screens 전부, Host `--filter "FullyQualifiedName~HouseMoveHost|FullyQualifiedName~FurniturePlacementHost|FullyQualifiedName~TestThemeScreens|FullyQualifiedName~KeyGuide"`, `bash Scripts/ci/run-all.sh --checks-only`(한글 문구·현지화 키).
- [ ] **Step 5: 직접 본다** — 스크린샷을 열어 겹친 글자, 잘린 패널, 분류 표시. **사용자에게 경로를 보이고 멈춘다.**
- [ ] **Step 6: 커밋** — `[UI] 가구 배치에 보관 분류와 집 옮김 알림`.

### Task 6: 키트 — 픽셀 출처표와 시험 표 `room-vectors.json`

모델: 높은. 멈춤 없음.

**Files:**
- Modify: `Tools/pixel-kit/pixelkit/room_tiles.py`(`plan_tiles`의 칸 안 픽셀 분류가 출처를 들고 가게 바꾸고, 같은 분류에서 `plan_sources`를 낸다)
- Create: `Tools/pixel-kit/pixelkit/room_vectors.py`, `Tools/pixel-kit/tests/test_room_vectors.py`, `Tools/pixel-kit/tests/fixtures/room-vectors.json`(명령의 결과, 커밋)
- Modify: `Tools/pixel-kit/pixelkit/cli.py`(`room vectors [--check]`), `.github/workflows/ci.yml`(job `pixel-kit-tests`: `actions/setup-python` 3.11, `pip install pillow`, 도구 테스트 전체), `Tools/pixel-kit/README.md`(새 절 "방 만들기 — 시험 표")

**시험 표의 꼴**(설계서가 맡긴 것을 여기서 정한다 — Task 7이 이대로 읽는다):

```json
{
  "about": "방 격자 → 칸마다 열쇠와 픽셀 출처. 만든 곳: python Tools/pixel-kit/kit.py room vectors. 손으로 고치지 않는다.",
  "version": 1, "cell": 32,
  "cases": [
    { "id": "two-rooms-wall", "faceHeight": 2, "band": 16,
      "grid": ["#####", "#.#.#", "#.#.#", "#####"],
      "cells": [
        { "x": 2, "y": 1, "kind": "cap", "wall": "cap:…", "ground": "floor",
          "parts": [
            { "src": "band", "shade": "f", "px": "10,0,22,32" },
            { "src": "band", "shade": "o", "px": "8,0,9,32+23,0,24,32" },
            { "src": "floor", "shade": "floor", "own": [-1, 0], "px": "0,0,8,32" },
            { "src": "floor", "shade": "floor", "own": [1, 0], "px": "24,0,32,32" } ] } ] } ] }
```

- `grid`: `#` 벽, `.` 바닥, 빈칸 밖. `cells`: 밖이 아닌 칸만, 줄 순서(y, x). `kind` = `classify`의 값. `wall`·`ground` = `plan_tiles`의 열쇠(없으면 `null`) — 사람이 읽고 Python 쪽만 비교한다.
- `parts`: 칸 안 32x32 픽셀의 출처. 덮지 않은 픽셀은 투명. 서로 겹치지 않는다. `px`는 `_encode(pixels, 0, 32)`의 정규형 글.
  - `band` + `shade`(`o` 외곽선, `d` 그늘, `f` 면, `l` 빛): 띠. `tile_layout(wall 열쇠)`의 `_shades(band, padded)`.
  - `face` + `piece`(`face:<k>[:<lrct>]`, 늘인 정면은 l·r 없는 꼴) + `own`: 벽지 정면 조각, 같은 자리 픽셀을 옮긴다. 
  - `narrow` + `piece`(`face:<k>:lr[c]@<x0>-<x1>`) + `own`: 좁은 정면 조각.
  - `floor` + `shade`(그 칸의 `ground` 열쇠: `floor`, `floor:shade`, `floor:shade:<x0>-<x1>`) + `own`: 바닥.
  - `own` = `[dx, dy]`, 그 픽셀의 꾸밈 주인 바닥 칸(맵 칸, y 아래로 +)의 상대 위치. 바닥 칸 자신은 `[0,0]`, 띠 옆으로 비치는 바닥은 그 픽셀이 바라보는 이웃 바닥 칸, 정면 `face:<k>`는 `[0,k]`(아래로 k칸이 주인 바닥 — 설계서 4.2절), 이웃 정면에서 늘인 정면은 `[dx, dy + k]`. 주인은 늘 바닥 칸이다.
  - 순서: `(src, shade 또는 piece, own, px)` 글자 순.
- 사례(`room_vectors.py` 안에 글자로 둔다 — 방 평면 파일이 바뀌어도 흔들리지 않게): 새 집 평면 사본, 시험 방 사본(`art/kit/rooms/test.two-rooms.json`의 `plan`을 지금 커밋된 내용으로 복사), 두 방 사이 세로 벽, T자, 십자, 벽 끝, 3x3 덩어리, 문간, 얇은 가로 벽(1·2줄), 가장자리에 닿은 벽, 무작위 8개(씨앗 고정 `random.Random(20261114)`, 14x10 바깥 벽 안에 길이 2~6의 가로·세로 벽 1~5개). `faceHeight` 2, `band` 16. 사례마다 같은 바이트(LF, `ensure_ascii=False`, `indent=1`).

**Interfaces:**
- Produces: `@dataclass(frozen=True) class SourcePart: src: str; shade: Optional[str]; piece: Optional[str]; own: Optional[Tuple[int, int]]; px: str`; `plan_sources(grid, face_height=2, band=16) -> List[List[Optional[List[SourcePart]]]]`; `room_vectors.build_vectors() -> bytes`, `room_vectors.VECTORS_PATH`; CLI `room vectors`(쓴다), `room vectors --check`(다르면 끝 코드 1).
- [ ] **Step 1: 실패하는 테스트** — `test_room_vectors.py`: `test_vectors_file_is_up_to_date`(`build_vectors()` == 파일 바이트), `test_plan_tiles_unchanged_on_every_case`(바꾸기 전 `plan_tiles` 결과를 이 테스트가 먼저 저장해 둔 것과 같다 — 바꾸기 전 커밋에서 한 번 뽑아 시험 안에 해시로), `test_parts_cover_disjoint_and_owners_are_floor`, `test_vertical_wall_band_has_two_floor_owners`(두 방 사이 세로 벽 칸에 `own` `[-1,0]`과 `[1,0]`), `test_face_owner_is_k_cells_below`, `test_band_shades_match_tile_layout`, `test_cli_check_fails_on_stale_file`(임시 사본).
- [ ] **Step 2: 실패를 본다** — `python -m unittest discover -s Tools/pixel-kit/tests -t Tools/pixel-kit -p "test_room_vectors.py"`, 기대: ImportError.
- [ ] **Step 3: 구현** — `plan_tiles`의 칸 안 분류 값(`"F"`, 정면 열쇠)이 어느 이웃에서 왔는지(`around`의 방향, 좁은 정면의 `x - 1`/`x + 1`)를 함께 들게 하고, 열쇠는 출처를 버린 값으로 지금처럼 만든다. `V`→`B` 메우기, 좁은 정면 판정, 바닥 그림자 범위는 한 곳에서 두 결과에 쓰인다. 고르기 규칙은 바꾸지 않는다.
- [ ] **Step 4: 통과를 본다** — `python Tools/pixel-kit/kit.py room vectors`, 도구 테스트 전체, 파일 크기를 적는다(목표 1.5MB 아래 — 넘으면 무작위 사례 수를 줄이고 보고).
- [ ] **Step 5: 커밋 둘** — `[CHORE] 픽셀 키트 방 — 픽셀 출처표와 시험 표 room-vectors`, `[CHORE] CI에 픽셀 키트 도구 테스트`.

### Task 7: Core 고르기 규칙 (`Rootborn.Core.Building`)

모델: 높은. 멈춤 없음.

**Files:**
- Create: `game/Rootborn.Core/src/Building/RoomGrid.cs`, `RoomTileKinds.cs`, `RoomBandLayout.cs`, `PixelRects.cs`, `TilePart.cs`, `RoomTilePlan.cs`
- Test: `game/tests/Rootborn.Core.Tests/Building/RoomTileVectorTests.cs`, `game/tests/Rootborn.Core.Tests/Building/RoomTilePlanTests.cs`; Modify: `game/tests/Rootborn.Core.Tests/Rootborn.Core.Tests.csproj`(`<None Include="../../../Tools/pixel-kit/tests/fixtures/room-vectors.json" Link="Fixtures/room-vectors.json" CopyToOutputDirectory="PreserveNewest" />` — Core는 파일을 읽지 않는다, 테스트가 읽는다)

구현자가 읽을 Python(`Tools/pixel-kit/pixelkit/room_tiles.py`): 상수 `CELL`·`PAD`·`MIN_BAND`·`FACE_FLAGS`, `_cell`, `_kinds`, `classify`, `_check_face_height`, `_strip_lr`, `_band_layout`, `_encode`(정규형 글), `_WIN`·`_RING1`·`_RING2_UP`·`_RING2_LOW`·`_shades`, `plan_tiles`(칸 안 분류, 좁은 정면, `floor_key`, 벽 칸 밑 바닥 그림자 범위), 그리고 Task 6의 `plan_sources`. `_near`·`_forced`·`_rim`·`_decode`·`tile_layout`은 열쇠 글을 만들고 읽는 데만 쓰이므로 옮기지 않는다 — C#은 띠 칠을 **집 전체 띠 픽셀 지도**에서 바로 센다(같은 결과인지는 시험 표가 본다). 열쇠 글은 C#이 만들지 않는다(설계서 4.3절은 열쇠도 견주자고 했지만, 게임이 쓰지 않는 정규형 코드를 옮기는 대신 픽셀마다의 출처를 견준다 — 더 엄격한 증거다).

**Interfaces:**
- Produces(모두 `namespace Rootborn.Core.Building`, `sealed`):
  - `public static class RoomCells { public const int Out = 0, Wall = 1, Floor = 2; }`
  - `public sealed class RoomGrid { public RoomGrid(int columns, int rows, IReadOnlyList<int> cells); public int Columns { get; } public int Rows { get; } public int At(int x, int y); }` — 줄 순서(y 아래로 +), 밖은 `Out`, 0·1·2가 아니면 `ArgumentException`.
  - `public readonly struct PixelRect { public PixelRect(int x0, int y0, int x1, int y1); public int X0, Y0, X1, Y1; }`(끝 제외) / `public static class PixelRects { public static string Encode(IEnumerable<(int X, int Y)> pixels); public static IReadOnlyList<PixelRect> Rects(IEnumerable<(int X, int Y)> pixels); }` — `Encode`는 Python `_encode(…, 0, 32)`와 같은 글.
  - `public static class TileSources { public const string Band = "band", Face = "face", Narrow = "narrow", Floor = "floor"; }`
  - `public sealed class TilePart { public string Source { get; } public string Shade { get; } public string Piece { get; } public CellPos Owner { get; } public IReadOnlyList<PixelRect> Rects { get; } public string Pixels { get; } }` — `Owner`는 상대 위치(`Rootborn.Core.World.CellPos`), 띠는 (0,0), 해당 없는 글은 null.
  - `public sealed class CellTiles { public CellPos Cell { get; } public string Kind { get; } public IReadOnlyList<TilePart> Parts { get; } }` — 부분 순서는 시험 표와 같다.
  - `public sealed class RoomTilePlan { public static RoomTilePlan Build(RoomGrid grid, int faceHeight, int band); public RoomGrid Grid { get; } public int FaceHeight { get; } public int Band { get; } public CellTiles At(int x, int y); public IReadOnlyList<CellTiles> Cells { get; } }` — `At`은 밖이면 null. `band`가 4 이상 32 미만이 아니거나 `faceHeight` < 1이면 `ArgumentOutOfRangeException`.
- [ ] **Step 1: 실패하는 테스트**
  - `RoomTileVectorTests.EveryCase_MatchesThePythonTable`(사례마다 `TestCaseSource`): `grid`로 `RoomGrid`, `Build(…, faceHeight, band)`; 칸 집합이 같고 칸마다 `Kind`와 부분 목록(src, shade, piece, own, `Pixels`)이 같다. 틀리면 사례 id·칸·첫 다른 부분을 메시지에.
  - `RoomTilePlanTests`: `OwnersAreAlwaysFloorCells`(시험 표 전부), `PartsAreDisjointAndInsideTheCell`, `BadBandOrFaceHeight_Throws`, `UnknownCellValue_Throws`, `HouseSizedPlan_TakesUnder16ms`(새 집 사례로 20번, 가운데 값을 `TestContext.Out`에 적고 100ms 아래를 단언 — 느슨한 지킴이, 목표 16ms는 보고에 숫자로).
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Core.Tests --filter "FullyQualifiedName~Building"`, 기대: 컴파일 오류.
- [ ] **Step 3: 구현.**
- [ ] **Step 4: 통과를 본다** — Core 전부, `bash Scripts/ci/run-all.sh --checks-only`(Core 참조 검사).
- [ ] **Step 5: 커밋** — `[FEATURE] Core Building — 방 격자에서 칸마다 픽셀 출처표(키트 시험 표와 같음)`. 본문에 잰 시간.

### Task 8: 꾸밈 정의와 게임용 조각 시트

모델: 중간. 멈춤 없음.

**Files:**
- Create: `game/Rootborn.Core/src/Building/RoomFinishDefinition.cs`, `game/Rootborn.Data/Builders/BuildingBuilders.cs`, `data/building/RoomFinishDefinition.json`; Modify: `game/Rootborn.Data/Builders/RootbornBuilders.cs`(한 줄 `BuildingBuilders.Register(b);`), `game/localization/ko.json`, `en.json`
- Create: `Tools/pixel-kit/pixelkit/room_export.py`, `Tools/pixel-kit/tests/test_room_export.py`; Modify: `cli.py`(`room export-finishes [--dry-run]`)
- Create(명령의 결과, 커밋): `art/world/finishes/finishes.png`, `data/atlases/art/world/finishes/finishes.png.atlas.json`
- Create: `game/Rootborn.World/RoomFinishArt.cs`; Test: `game/tests/Rootborn.Data.Tests/RoomFinishDefinitionTests.cs`, `game/tests/Rootborn.World.Tests/RoomFinishArtTests.cs`

데이터(설계서 6.1절과 다른 점: 비용은 이 저장소의 관례 `costItems`/`costCounts` + `BuilderHelpers.StrictItemAmounts`(일터 템플릿과 같음), 그림 키는 `art` 하나가 키트 id를 겸한다 — `kit`는 같은 값이라 두지 않는다, PNG 경로 `sheet`를 정의에 둔다 — 배포 목록의 `@refs data/**/*.json`이 시트와 좌표표를 저절로 넣는다):

```json
{ "type": "RoomFinishDefinition", "id": "finish.wall.paper", "slot": "wall", "nameKey": "finish.wall.paper.name",
  "sheet": "art/world/finishes/finishes.png", "art": "paper",
  "costItems": [ { "ref": "item.allowance", "type": "ItemDefinition" } ], "costCounts": [ 2 ], "unlockToken": "" }
```

시험용 10종(결정 대기 7·9번 — 값은 시험용): 벽 `paper`·`wallpaper-sprig`·`paint-two-tone`(열림), `panel-plank`·`paint-mint`(`unlock.finish.<art>`로 잠금); 바닥 `floor.plank`·`floor.checker-soft`·`floor.carpet`(열림), `floor.tatami`·`floor.carpet-diamond`(잠금). id는 `finish.wall.<art>`, `finish.floor.<art에서 floor. 뺀 것>`. 벽 비용 2, 바닥 1(칸당). 이름(ko는 키트 `label`): 줄무늬 벽지 벽/Striped wallpaper, 연녹 잔무늬 벽지/Green sprig wallpaper, 위아래 투톤 벽/Two-tone paint, 짙은 가로 판자벽/Dark plank panelling, 민트 칠 벽/Mint paint, 꿀색 마루/Honey plank floor, 조용한 체크 바닥/Soft checker floor, 남색 카펫/Navy carpet, 돗자리 바닥/Tatami mat, 장밋빛 마름모 카펫/Rose diamond carpet.

해금 판단(결정 7번): 가구 해금 `FurnitureUnlockProgress`는 아무 토큰 글을 담는 집합이라 꾸밈도 같은 진행에 `unlock.finish.*` 토큰으로 든다(세이브 모양이 늘지 않고, 옛 빌드도 모르는 토큰을 그대로 들고 간다 — 다음 계획 B4에서 테스트로 확인). 가게 진열은 가구의 `IsForSale`·`PurchaseLocationId`·`Price`와 같은 필드와 장소 화면(`LocationModel`의 살 목록) 줄이 필요해 다음 계획 B4로 넘긴다. 그래서 잠긴 4종은 이 계획이 끝나도 얻을 길이 없다(보이지 않을 뿐이다). 인테리어 기술·집 단계로 여는 조건은 `data/`에 범용 기술 문턱 조건이 없음을 확인했다(조건 종류는 탐색·단서·장소·세계 상태뿐) — 넣지 않는다.

좌표표 꼴(키트가 쓴다, `FurnitureArt`와 같은 `origin`·`pixelsPerUnit`·`sprites`에 두 키를 더함):
`{"origin":"top-left","pixelsPerUnit":32,"band":16,"sprites":{"paper/face:1:lc":{"x":0,"y":0,"w":32,"h":32}, "paper/face:2:lrc@8-24":{…}, "floor.plank/floor":{…}, "floor.plank/floor:shade":{…}},"bands":{"wall.top.cream":{"o":"#RRGGBB","d":"…","f":"…","l":"…"}}}`. 벽 모양마다 꽉 찬 정면 24장(k 1·2 × 끝 없음·l·r·lr × 위 없음·c·t)과 좁은 정면 12장(k 1·2 × 범위 `0-16`·`8-24`·`16-32` × c 있음·없음 — 띠 16 기준), 바닥마다 2장. 띠 색은 `room styles --list`의 5개 전부(`_ramp(style, top, 3)` + `style.outline`). 시트 자리는 이름 순으로 한 줄 16칸, 같은 입력이면 같은 바이트.

**Interfaces:**
- Produces:
  - Core `public sealed class RoomFinishDefinition { public RoomFinishDefinition(string id, string slot, string nameKey, string sheet, string art, IReadOnlyList<ItemAmount<string>> cost, string unlockToken); public string Id, Slot, NameKey, Sheet, Art, UnlockToken { get; } public IReadOnlyList<ItemAmount<string>> Cost { get; } public bool IsValid { get; } }` + `public static class RoomFinishSlots { public const string Wall = "wall", Floor = "floor"; }`(데이터 값 이름일 뿐 — 분기는 "이 칸의 벽지인가 바닥인가"를 고르는 데만 쓴다).
  - Data `b.Add("RoomFinishDefinition", …)` — 잘못되면(id·slot·sheet·art 빔, 비용 0 이하) 읽기 오류. 등록부(`data/registry.json`)에는 넣지 않는다(일터 종류와 같다).
  - World `public sealed class RoomFinishArt { public RoomFinishArt(string dataDirectory); public bool TryPiece(RoomFinishDefinition finish, string piece, out FinishPiece sprite); public bool TryBand(string sheet, string bandId, out BandColors colors); public int BandOf(string sheet); }`, `public readonly record struct FinishPiece(string Texture, int X, int Y)`(32x32), `public readonly record struct BandColors(uint Outline, uint Dark, uint Face, uint Light)` — 색은 MonoGame `Color.PackedValue`와 같은 순서(가장 낮은 바이트가 R, 그다음 G, B, 가장 높은 바이트가 A=255).
  - Python `room_export.export_finishes(repo_root, kit, dry_run=False) -> List[str]` — 정의 파일의 `art`를 읽어 그 모양만 내보낸다. 띠 두께가 16이 아닌 벽 모양이면 거절(`room-finish-band`).
- [ ] **Step 1: 실패하는 테스트**
  - Data `RoomFinishDefinitionTests`: 실제 데이터 10종이 읽힘, slot은 wall·floor 뿐, 기본 6종은 토큰 빔, 비용 아이템이 데이터에 있음, 잘못된 정의(비용 0, slot 빔)는 읽기 오류. `RealDataTests`는 종류가 등록됐으니 그대로 통과해야 한다.
  - World `RoomFinishArtTests`: 모든 벽 꾸밈의 36조각·바닥 꾸밈의 2조각이 풀림, 5개 띠 색이 풀림, `BandOf` = 16, 없는 조각은 false.
  - Python `test_room_export.py`: 조각 수, 같은 바이트 다시 쓰기, 띠 16 아닌 모양 거절, **다시 조합하면 키트 타일과 같다**(`room-vectors.json`의 모든 사례·칸마다 시험 표의 부분 + 조각 + 띠 색으로 32x32를 만들고, 같은 칸의 `draw_tile(wall 열쇠)`를 `draw_tile(ground 열쇠)` 위에 얹은 것과 픽셀이 같다 — 바닥 주인이 다른 칸은 두 주인이 같은 꾸밈일 때만 견준다).
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.Data.Tests --filter "FullyQualifiedName~RoomFinish"`; `python -m unittest … -p "test_room_export.py"`.
- [ ] **Step 3: 구현** → `python Tools/pixel-kit/kit.py room export-finishes`.
- [ ] **Step 4: 통과를 본다** — Data 전부(`RealDataTests`, 지문 `DataFingerprintTests` — 새 배열 파일은 지문에 든다, 서버와 같은 지문이면 된다), World 전부, Server(`PublishListStartupTests`), 도구 전부, `run-all.sh --checks-only`(현지화 키). 시트를 2배로 열어 조각을 직접 본다.
- [ ] **Step 5: 커밋 셋** — `[FEATURE] 꾸밈 정의 RoomFinishDefinition과 그림 풀이`, `[CHORE] 픽셀 키트 room export-finishes — 게임용 조각 시트`, `[ASSET] 시험용 꾸밈 10종 조각 시트와 좌표표`.

### Task 9: 게임이 조합해 그린다 (기본 꾸밈만)

모델: 높은. **멈춤: 조합으로 그린 새 집 스크린샷을 사용자에게 보인다.**

**Files:**
- Modify: `game/Rootborn.World/MapBuildSettings.cs`(필드 더함), `game/Rootborn.World/WorldCatalog.cs`, `game/world/maps.json` 집 `build` 완성:
  `"build": { "frameLayer": "Build", "frameId": "house.v2", "faceHeight": 2, "band": 16, "bandColors": "wall.top.cream", "defaultWall": "finish.wall.paper", "defaultFloor": "finish.floor.plank", "replacesLayers": [ "Wall", "Ground" ] }`
- Modify: `game/Rootborn.World/MapPicture.cs`(`From(LdtkLevel level, IReadOnlyCollection<string> skipTileLayers = null)`, `public int ReplacedAt { get; }` — 건너뛴 층의 첫 타일이 `Under`에 들어갈 자리, 없으면 -1), `game/Rootborn.World/WorldMapLoader.cs`(`LoadedMap.Tiles`), `game/Rootborn.World/MapChecks.cs`(짓는 집 그림 검사)
- Create: `game/Rootborn.World/RoomTileCompositor.cs`(엔진 없음), `game/Rootborn.Host/World/ComposedRoomTextures.cs`; Modify: `game/Rootborn.Host/World/WorldScreen.cs`(`Draw`의 `Under` 그리기, `Load`가 실행 중 텍스처를 먼저 찾음, `Dispose`)
- Test: `game/tests/Rootborn.World.Tests/RoomTileCompositorTests.cs`, `game/tests/Rootborn.World.Tests/HouseMapTests.cs`(더함), `game/tests/Rootborn.Host.Tests/HouseTilesHostTests.cs`

이 작업이 푸는 15절: `MapPicture`는 층 이름 없이 `Under` 한 목록으로 펴므로 "층 하나만 바꿔 그리기"는 읽을 때 건너뛰고 자리(`ReplacedAt`)를 기억해야 한다(가구처럼 따로 목록을 두고 그 자리에 끼운다).

**Interfaces:**
- Consumes: Task 7 `RoomTilePlan`, Task 8 `RoomFinishDefinition`·`RoomFinishArt`.
- Produces:
  - `MapBuildSettings`: `int FaceHeight`, `int Band`, `string BandColors`, `string DefaultWall`, `string DefaultFloor`, `IReadOnlyList<string> ReplacesLayers`(모두 필수 — 빠지면 `WorldCatalog` 오류).
  - `LoadedMap.RoomTilePlan Tiles`(짓는 집만; 격자는 `Collision` IntGrid 값 그대로, 크기는 레벨에서) — 맵을 읽을 때 한 번.
  - `MapChecks`(짓는 집): `ReplacesLayers` 층이 레벨에 있음, `DefaultWall`이 slot wall인 꾸밈·`DefaultFloor`가 floor인 꾸밈으로 데이터에 있음, `BandColors`가 그 시트 좌표표에 있음, 좌표표 `band` = `Band`. 이 검사는 정의와 좌표표가 필요하므로 `MapChecks.Check(loader, interactions, GameDefinitions definitions = null, RoomFinishArt art = null)` — null이면 그 검사를 건너뛴다(기존 호출은 그대로), `MapContractTests`는 둘을 넘긴다.
  - `public sealed class PieceImage { public PieceImage(int width, int height, uint[] pixels); }`
  - `public sealed class RoomTileCompositor { public RoomTileCompositor(Func<string, PieceImage> sheet, RoomFinishArt art); public ComposedRoom Compose(RoomTilePlan plan, Func<CellPos, RoomFinishDefinition> wallAt, Func<CellPos, RoomFinishDefinition> floorAt, string sheetForBand, string bandColorsId); }` — 칸마다: 띠는 색 넷, `face`·`narrow`는 `wallAt(cell + Owner)`의 조각에서 같은 자리 픽셀, `floor`는 `floorAt(cell + Owner)`의 `floor` 조각(그림자 범위의 위 두 줄은 `floor:shade` 조각). 같은 (칸의 부분 목록, 쓰인 꾸밈 id들) 조합은 한 칸만 만든다. 조각이 없으면 그 픽셀은 투명 + 문제 한 줄.
  - `public sealed class ComposedRoom { public int Width { get; } public int Height { get; } public uint[] Pixels { get; } public IReadOnlyList<(CellPos Cell, int SrcX, int SrcY)> Cells { get; } public int UniqueTiles { get; } public IReadOnlyList<string> Problems { get; } public bool TooLarge { get; } }` — 묶음 너비 1024px(한 줄 32칸), 상한 4,096칸(16MB); 넘으면 `TooLarge` 그리고 `Pixels` 비움.
  - Host `ComposedRoomTextures`: `Texture2D Upload(GraphicsDevice device, ComposedRoom room)`, 시트 PNG → `PieceImage`(`Texture2D.GetData<Color>` 한 번, 시트마다 캐시). `WorldScreen`: 짓는 집에 처음 들어선 그리기에서 한 번 조합(꾸밈은 `build.defaultWall`·`defaultFloor`로), 텍스처 키 `"composed:" + mapId`를 `Load`가 실행 중 표에서 먼저 찾는다(손질 대상 아님), `Under[0..ReplacedAt)` → 조합 칸 → 나머지 순으로 그린다. 맵을 떠나면 그 텍스처를 버린다. 문제가 있거나 `TooLarge`면 문제를 맵 문제 보고(`ReportProblems`)에 더하고 LDtk 층을 그대로 그린다(그때는 `skipTileLayers`로 뺀 그림이 필요하므로 `LoadedMap`이 건너뛰지 않은 `MapPicture`를 따로 들지 말고 `WorldScreen`이 그 맵만 `MapPicture.From(level)`로 다시 만든다 — 드문 길).
  - `public void WorldScreen.RefreshComposedTiles(string mapId)`(그 맵의 조합을 버려 다음 그리기에서 다시 만든다 — 다음 계획이 꾸밈 알림에서 부른다), `public int ComposeCount { get; }`, `public long ComposedTextureBytes { get; }`(시험·진단용).
- [ ] **Step 1: 실패하는 테스트**
  - World `RoomTileCompositorTests`(조각은 시험이 만든 단색 버퍼 — 조각마다 다른 값, 그림 픽셀 아님): 띠 픽셀은 띠 색 넷 중 그 칠, 정면 픽셀은 주인의 벽 꾸밈 조각 값, 세로 벽 칸의 왼쪽·오른쪽 비침이 서로 다른 바닥 꾸밈을 따름(왼쪽 방·오른쪽 방 바닥을 다르게), 그림자 범위 위 두 줄만 `floor:shade` 조각, 같은 조합 한 칸(`UniqueTiles`), 없는 조각은 투명 + 문제, 4,096칸 넘으면 `TooLarge`.
  - World `HouseMapTests`: `BuildSettingsResolve`(`MapChecks`에 정의·그림 넘겨 집 문제 0), `LoadedHouseHasTilePlan_AndPictureSkipsReplacedLayers`(`Tiles` 있음, `Under`에 `Wall`·`Ground` 타일셋 그림 없음, `ReplacedAt` ≥ 0).
  - Host `HouseTilesHostTests.HOUSE_TILES_001_ComposedHouseMatchesTheKitTiles_AndComposesOnce`: `--world House`로 띄워 60프레임 돌린 뒤 `ComposeCount` = 1, `ComposedTextureBytes` 기록; 조합 칸마다 키트가 구운 `art/world/rooms/house/tiles.png`의 같은 칸(LDtk `Ground` 위 `Wall`)과 픽셀이 같다(전제: `build.defaultWall`/`defaultFloor`/`bandColors`의 `art`가 방 평면의 `wall`/`floor`/`top`과 같음 — 다르면 전제를 메시지로 실패. 그림 색을 단언하는 것이 아니라 두 경로가 같은 그림을 내는지 본다). 스크린샷 넷: 도착 지점, 세로 벽 x=13의 좁은 정면 앞, 두 방 사이 세로 벽(문간 (20,7~9) 옆), 가로 벽 정면 앞 — `output/house-building/stage1/house-tiles-NN-*.png`.
  - Host `HOUSE_TILES_002_LeavingTheHouseReleasesTheComposedTexture`: 마을로 나가면 `ComposedTextureBytes` 0, 다시 들어오면 `ComposeCount` 2.
- [ ] **Step 2: 실패를 본다** — `dotnet test game/tests/Rootborn.World.Tests --filter "FullyQualifiedName~RoomTileCompositor|FullyQualifiedName~HouseMap"`.
- [ ] **Step 3: 구현.** 그리기 루프에 새 목록·LINQ 없음(`ComposeCount`로 한 번만인지 본다).
- [ ] **Step 4: 통과를 본다** — World, Host `--filter "FullyQualifiedName~HouseTiles|FullyQualifiedName~HouseMap|FullyQualifiedName~WorldFrameCost|FullyQualifiedName~WorldScreen"`, `run-all.sh --checks-only`. 합치기 전 원격 전체 `bash Scripts/remote-test/run.sh <커밋>`(Host 묶음 rest·main-chain 포함).
- [ ] **Step 5: 직접 본다** — 스크린샷 넷을 열어 띠·정면·좁은 정면·두 방 사이 세로 벽 칸, 이음매, 빈 칸. **사용자에게 경로와 `ComposedTextureBytes`·고르기 시간(Task 7)을 보이고 멈춘다.**
- [ ] **Step 6: 커밋 둘** — `[FEATURE] 짓는 집을 칸마다 조합해 그린다 — 출처표와 꾸밈 조각`, `[TEST] 조합 그림이 키트 타일과 같고 한 번만 만든다`.

### Task 10: 문서와 스킬

모델: 낮은. 멈춤 없음.

**Files:**
- Modify: `game/world/README.md`("키트로 만든 방"에 `wallDoors`·`Build` 층, 새 절 "짓는 집(`build`)": 필드 뜻, 테두리는 넓어지는 쪽(오른쪽·아래)으로만 키운다, `frameId`는 맵을 새로 그리면 올린다), `game/README.md`(맵 목록 필드 표에 `build`), `data/README.md`(`data/building` 영역, `RoomFinishDefinition` 필드, 시험용 값이라는 것), `Tools/pixel-kit/README.md`(빠진 것), `.claude/skills/pixel-kit-room/SKILL.md`와 `.agents/skills/pixel-kit-room/SKILL.md`(바이트 같게: 바깥 벽 문, `build` 줄, `room vectors`와 `--check`, `room export-finishes`, "집 맵을 바꿀 때" 절차 — 평면 고침 → `room build house --force` → `room vectors` → `room export-finishes` → World `HouseMapTests`·Host `HouseTilesHostTests` → 스크린샷 직접 열기, 고르기 규칙을 고치면 C# `Rootborn.Core.Building`도 함께), `.claude/rules/changelog.md`(항목 하나: `testing-discipline.md` 시나리오 표에 HOUSE-002, CI에 키트 테스트 — 일반화 "같은 규칙이 두 언어에 있으면 한 시험 표를 두 쪽 테스트가 읽고, 두 쪽 모두 CI가 돈다"), 설계서 15절(풀린 것은 지우고 사실로: 문은 `wallDoors`, `Under` 자리 기억, CI가 키트를 안 돌리던 것을 바로잡음, 잰 시간·메모리 숫자; 남은 것: 띠 열쇠 이론 상한, 개인 지갑 칸 잡기, 범용 기술 조건 없음, 넓어지는 쪽 약속), 설계서 12.1절에 "1-1 ~ 1-5 끝, 1-6 이후 다음 계획" 한 줄
- [ ] **Step 1:** 쓴다. 새 스킬은 만들지 않는다(집 맵은 `pixel-kit-room`의 한 절로 충분하다).
- [ ] **Step 2:** 문서에 적은 명령을 실제로 돌린다(읽기만 하는 것, `--check`, `--dry-run`). `cmp .claude/skills/pixel-kit-room/SKILL.md .agents/skills/pixel-kit-room/SKILL.md`.
- [ ] **Step 3:** 도구 테스트 전체, `run-all.sh --checks-only`. 커밋 `[DOCS] 짓는 집 1단계(가) — 맵 build, 꾸밈 데이터, 키트 절차와 스킬`.

---

## 다음 계획이 기대는 것

다음 계획(`2026-11-14-house-building-stage1b.md`)은 아래 이름을 그대로 쓴다. 바꾸면 그 계획을 먼저 고친다.

- `Rootborn.Core.Building.RoomTilePlan.Build(RoomGrid, int faceHeight, int band, IReadOnlyCollection<CellPos> wallDoors)`(벽 문 칸은 `Collision`이 벽인 칸의 `Door`), `CellTiles.Parts`의 `TilePart`(`Source` 출처 종류 이름(`TileSources`): `band`·`face`·`narrow`·`floor`·`sill`·`door`, `HasOwner`, 꾸밈 주인 바닥 칸 = 칸 + `Owner`), `RoomCells`(작업 7).
- `RoomFinishDefinition`(`Id`, `Slot`, `NameKey`, `Sheet`, `Art`, `Cost` 칸당, `UnlockToken`), `RoomFinishSlots`(`wall`·`floor`), World `RoomFinishArt`(조각·공용 조각·띠 색을 좌표표에서 푼다). 정의는 `data/building/RoomFinishDefinition.json`. 해금은 `FurnitureUnlockProgress`의 `unlock.finish.*` 토큰이 되도록 정의에 자리만 있고 게임은 아직 쓰지 않는다.
- `MapEntry.Build`(`MapBuildSettings`: `FrameLayer`, `FrameId`, `FaceHeight`, `Band`, `BandColors`, `DefaultWall`, `DefaultFloor`, `ReplacesLayers` — 모두 필수), `LoadedMap.Tiles`(칸마다 출처표, 맵을 읽을 때 한 번 `TilePlanOf`).
- `RoomTileCompositor.Compose(plan, wallAt, floorAt, sheetForBand, bandColorsId)` → `ComposedRoom`, 맵 한 장 분은 `ComposedMapTiles.Compose(map, compositor, finish)`(지금 `wallAt`·`floorAt`을 기본 꾸밈 `_ => wall`, `_ => floor`로 부른다 — 다음 계획은 `Func<CellPos, RoomFinishDefinition>` 둘을 받는 오버로드를 더하고 빈 칸은 기본 꾸밈으로). 두 함수는 주인 칸(`cell + Owner`, face는 `(dx, dy + level)`)으로 불린다. `sheetForBand`는 벽 꾸밈의 `Sheet`, `bandColorsId`는 맵의 `build.bandColors`. 다음 계획은 꾸밈 조회를 `GameSession`에서, 띠 색을 집의 고른 색으로 넘긴다. `WorldScreen.RefreshComposedTiles(mapId)`를 새 알림 주제(`GameChangeTopics`에 하나)에서 부른다. 구조(`Collision`)가 바뀌면 `LoadedMap.Tiles`도 다시 만들어야 한다(`RefreshComposedTiles`는 꾸밈만 다시 조합한다).
- `HouseFurnitureLayout.Stored`, `TryPlaceStored`, `GameSession.PlaceStoredFurniture` — 같이 쓰는 집에서는 지금 false다(다음 계획 B7이 방 요청으로 연다).
- 가구 배치 화면의 분류 줄(`ui.furniture.category.*`, 지금 `furniture`·`stored`) — 다음 계획이 `ui.furniture.category.finish`(꾸미기)를 더한다.
- 다음 계획의 작업(결정 대기 문서의 값으로): B1 Core 꾸밈 상태(바닥 칸마다 (벽지, 바닥), 방 채우기의 문간 규칙, 미리 보기·바뀌는 칸만 센 비용), B2 `GameSession` 명령·조회 + `house-build.json`(`HouseBuildSaveData` v1: `FrameId`, 맨 위 `Band`(결정 8), `Finishes`; `Save()` 묶음, `Require`에 넣지 않음, `SaveShapeCoverage`, 새 날짜 고정 폴더, 하위·상위 테스트) + HOUSE-001(비용 다섯 경우), B3 꾸미기 분류 화면 + Host 입력으로 벽지 바꾸기, B4 가게 진열과 해금, B5 인테리어 기술(`PlacementSkillGrowth`와 같은 하루 한도를 같이 씀, 결정 10), B6 새 게임 처음 배치(결정 6 — 새 게임에만), B7 같이 하기(`house-build` 조각, 서버 판정, 지문에 짓는 집 LDtk, 같이 쓰는 집의 보관 꺼내기) + HOUSE-004.
