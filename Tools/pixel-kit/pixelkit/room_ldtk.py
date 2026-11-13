"""방 만들기 — 평면에서 게임이 그대로 읽는 LDtk 방, 타일 시트, 맵 목록 쓰기(`kit.py room build`)와
LDtk에서 다시 칠한 Collision에 벽 그림을 맞추기(`room repaint`), LDtk 방 검사(`room check <ldtk 경로>`).

결과(기본 `art/world/rooms/<id>/`): `<id>.ldtk`, `tiles.png`(쓰인 타일만 모은 시트, 덧붙이기만), `tiles.slots.json`
(자리 표 + 그 시트를 그린 벽 모양 `wall`·바닥 `floor`·`face_height`·윗면 띠 두께 `band` — repaint가 여기서 읽는다), `maps.json`.
LDtk 층(위에서 아래): Collision(IntGrid 32, 1 벽 / 2 바닥, 편집기에서 반투명), Door(Entities), Furniture(Entities, 태그 ysort, 비어 있음),
Wall(Tiles — 벽 칸마다, 띠·정면이 아닌 자리는 투명), Ground(Tiles — 바닥 칸마다 + 띠 옆으로 바닥이 비치는 벽 칸마다).
LDtk 파일은 편집기(1.5.3)가 저장하는 꼴 그대로 쓴다(ldtk_common의 편집기 꼴 함수 — 공식 스키마의 필수 필드를 테스트가 지킨다).
게임 읽기 코드(game/Rootborn.World/Ldtk/LdtkFile.cs, WorldCatalog.cs)가 읽는 값은 그 위에 얹는다.
문제가 하나라도 있으면 아무것도 쓰지 않는다. 쓸 때는 모든 파일을 임시 파일에 먼저 쓴 뒤 바꿔친다(ldtk_common.write_all).
"""
import io
import json
import re
import uuid
from pathlib import Path
from typing import Dict, List, Optional, Tuple

from PIL import Image

from .cli import Kit, load_kit
from .export import CELL, SHEET_WIDTH_CELLS, _json_bytes
from . import ldtk_common as ldtk
from .ldtk_common import dump, loads_with_comments, walkers, write_all
from .lint import Problem
from .makers import RecipeError
from .room_plan import Room, RoomError, cells, check_grid, is_warning, load_room
from .room_tiles import FLOOR, WALL, RoomTiles, draw_tile, lint_room_tile, plan_tiles, wall_band

DEFAULT_ROOT = "art/world/rooms"           # 저장소 뿌리 기준. 방마다 <id> 폴더
DEFAULT_ROOMS_DIR = "art/kit/rooms"
TILES_NAME, SLOTS_NAME, MAPS_NAME = "tiles.png", "tiles.slots.json", "maps.json"
MAX_VIEW_CELLS = 13                        # 화면에 보일 높이(칸). 레벨이 더 낮으면 레벨 높이
LAYER_ORDER = ("Collision", "Door", "Furniture", "Wall", "Ground")      # 파일 안 순서: 위 층이 먼저
INT_VALUES = ((WALL, "wall", "#8a6f5a"), (FLOOR, "floor", "#c9b99a"))   # Collision 값 약속(game/world/README.md 3절)
# uid는 LDtk에서 정의·레벨이 한 번호 공간을 쓴다(겹치지 않게). 타일셋 1은 게임이 읽는 __tilesetDefUid라 예전 값 그대로 둔다.
TILESET_UID, LEVEL_UID, DOOR_DEF_UID, WHERE_FIELD_UID, NEXT_UID = 1, 0, 20, 21, 22
LAYER_UIDS = {"Collision": 2, "Door": 3, "Furniture": 4, "Wall": 5, "Ground": 6}
BG_COLOR = "#40465B"                       # 레벨 바탕(__bgColor) = 프로젝트 defaultLevelBgColor
ROOM_LAYERS = (("Collision", "IntGrid"), ("Wall", "Tiles"), ("Ground", "Tiles"))   # 이 도구가 만든 방이라는 표시(머리말로 판별하지 않는다)
ART_LAYERS = ("Wall", "Ground")                                                   # repaint가 다시 쓰는 층(RoomTiles의 wall·ground)
ROOM_VALUES = ("wall", "floor", "face_height", "band")                            # 자리 표에 남기는 방 값
SHEET_VALUES = ("wall", "floor", "band")                                          # 시트의 그림을 정하는 값 — 다르면 한 시트에 섞지 않는다
COLLISION_OPACITY = 0.35                   # 편집기에서 Collision 색 칸이 벽·바닥 그림을 덮지 않고 비쳐 보이게(게임은 이 값을 읽지 않는다)
_NAMESPACE = uuid.UUID("5d9b2a0e-6a3c-4a53-9d5e-2b0b6a6f3c11")      # iid를 결정적으로 만든다


class RoomBuildError(Exception):
    """평면이나 그림에 쓰기를 막는 문제가 있다. problems가 그 목록(알림은 들어 있지 않다)."""

    def __init__(self, problems: List[Problem]):
        super().__init__("; ".join(f"{p.code} {p.message}" for p in problems))
        self.problems = problems


def spawn_name(level_name: str) -> str:
    """도착 지점 이름: 맵 사이에서 겹치지 않게 <맵 id 소문자>-entry(game/world/README.md 4절)."""
    return level_name.lower() + "-entry"


def _normalize_where(value) -> str:
    """게임이 문 값을 doors 표와 견주는 방식(Core DoorRoutes.Normalize): 앞뒤 공백 지움, 소문자, '-' → ' '."""
    return str(value if value is not None else "").strip().lower().replace("-", " ")


def door_problems(doors: Dict[Tuple[int, int], str], doors_table: dict) -> List[Problem]:
    """문 칸 → Where 값 중 maps.json doors 표에 없는 것. 알림(room-door-unregistered)이다: 게임에서 그 문은 열리지 않는다."""
    known = {_normalize_where(k) for k in doors_table}
    return [Problem("room-door-unregistered",
                    f"문 ({x}, {y})의 Where {where!r}가 maps.json의 doors 표에 없어 게임에서 열리지 않는다 — "
                    f"그 맵의 doors에 {_normalize_where(where)!r}의 목적지 맵·도착 지점을 손으로 적는다", x, y)
            for (x, y), where in sorted(doors.items(), key=lambda kv: (kv[0][1], kv[0][0]))
            if _normalize_where(where) not in known]


def map_id(room_id: str) -> str:
    """방 id → 맵 id: 점·하이픈을 지우고 낱말 첫 글자를 대문자로(test.two-rooms → TestTwoRooms)."""
    return "".join(w[:1].upper() + w[1:] for w in re.split(r"[.\-]", room_id) if w)


def _iid(*parts) -> str:
    return str(uuid.uuid5(_NAMESPACE, "/".join(str(p) for p in parts)))


# ---------------------------------------------------------------- 타일 시트(덧붙이기만)

def _slot_index(slot: dict) -> int:
    return (slot["y"] // CELL) * SHEET_WIDTH_CELLS + slot["x"] // CELL


def _allocate(old_slots: dict, wanted: List[str]) -> Dict[str, dict]:
    """있는 자리는 그대로, 새 열쇠는 마지막 번호 다음부터 번호순(행 우선, 한 줄 16칸)으로."""
    slots = {k: dict(v) for k, v in old_slots.items()}
    nxt = max((_slot_index(s) for s in slots.values()), default=-1) + 1
    for key in wanted:
        if key not in slots:
            slots[key] = {"x": (nxt % SHEET_WIDTH_CELLS) * CELL, "y": (nxt // SHEET_WIDTH_CELLS) * CELL, "w": CELL, "h": CELL}
            nxt += 1
    return slots


def _read_sheet(out: Path):
    """(자리 표, 그림, 방 값). 둘 다 없으면 ({}, None, {}). 하나만 있거나 못 읽거나 이 도구의 시트가 아니면 RoomError.
    방 값은 자리 표의 wall·floor·face_height·band 중 있는 것(그 값을 남기기 전에 만든 옛 표에는 없다)."""
    table_file, png_file = out / SLOTS_NAME, out / TILES_NAME
    if not table_file.exists() and not png_file.exists():
        return {}, None, {}
    if table_file.exists() != png_file.exists():
        raise RoomError(f"{out}: {SLOTS_NAME}와 {TILES_NAME} 중 하나만 있다(덧붙이기만 하는 시트라 어느 쪽도 믿고 고칠 수 없다)")
    try:
        doc = json.loads(table_file.read_text(encoding="utf-8"))
        if (doc["cell"], doc["width_cells"]) != (CELL, SHEET_WIDTH_CELLS):
            raise ValueError("cell·width_cells가 지금 약속(32, 16)과 다르다")
        slots = {}
        for k, v in doc["slots"].items():
            if not all(type(v[f]) is int for f in "xywh"):
                raise ValueError(f"{k}의 x·y·w·h가 정수가 아니다")
            slots[k] = {f: v[f] for f in "xywh"}
        meta = {k: doc[k] for k in ROOM_VALUES if k in doc}
        with Image.open(png_file) as im:
            img = im.convert("RGBA")
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as e:
        raise RoomError(f"{out}의 {SLOTS_NAME}·{TILES_NAME}를 못 읽었다({e})") from None
    if img.width != CELL * SHEET_WIDTH_CELLS or any(s["x"] + s["w"] > img.width or s["y"] + s["h"] > img.height for s in slots.values()):
        raise RoomError(f"{out}: {TILES_NAME} 크기 {img.size}가 {SLOTS_NAME}와 맞지 않는다")
    return slots, img, meta


def _sheet(out: Path, wanted: List[str], tiles: Dict[str, object], room_values: dict) -> Tuple[bytes, bytes, Dict[str, dict], int]:
    """(PNG, 자리 표, 자리, 시트 높이). 이미 있는 시트는 자리를 지키고 지금 쓰는 타일만 다시 그린다.
    room_values(wall·floor·face_height·band)는 자리 표에 남긴다. 시트가 다른 벽 모양·바닥·띠 두께로 그려져 있으면 RoomError
    (지금 쓰지 않는 옛 타일은 다시 그리지 않으므로 한 시트에 두 모양이 섞인다). band가 없는 옛 표(윗면 32px 시절)에는
    덧붙인다 — 옛 윗면 열쇠(cap:<수>)는 새 규칙이 다시 내지 않으므로 같은 열쇠가 다른 그림이 되지 않는다."""
    old_slots, old_img, old_values = _read_sheet(out)
    for k in SHEET_VALUES:
        if k in old_values and old_values[k] != room_values[k]:
            raise RoomError(f"{out / SLOTS_NAME}의 시트는 {k} {old_values[k]!r}로 그렸다(지금 {room_values[k]!r}). "
                            f"한 시트에 두 모양이 섞이지 않게 그 폴더의 tiles.png·tiles.slots.json을 지우고 다시 만든다")
    bad = [k for k, s in old_slots.items() if (s["w"], s["h"]) != (CELL, CELL) or s["x"] % CELL or s["y"] % CELL]
    if bad:
        raise RoomError(f"{SLOTS_NAME}의 {', '.join(bad)}가 32x32 칸 자리가 아니다(이 도구가 쓴 시트가 아니다)")
    slots = _allocate(old_slots, wanted)
    height = max([old_img.height if old_img else 0] + [s["y"] + s["h"] for s in slots.values()])
    img = Image.new("RGBA", (CELL * SHEET_WIDTH_CELLS, height), (0, 0, 0, 0))
    if old_img is not None:
        img.paste(old_img, (0, 0))
    for key in wanted:
        s = slots[key]
        img.paste(tiles[key].to_image().convert("RGBA"), (s["x"], s["y"]))
    if old_img is not None and old_img.size == img.size and old_img.tobytes() == img.tobytes():
        png = (out / TILES_NAME).read_bytes()          # 그림이 같으면 있는 바이트를 그대로 둔다
    else:
        buf = io.BytesIO()
        img.save(buf, format="PNG", optimize=False)
        png = buf.getvalue()
    table = _json_bytes({"cell": CELL, "width_cells": SHEET_WIDTH_CELLS, **{k: room_values[k] for k in ROOM_VALUES},
                         "slots": slots})
    return png, table, slots, height


# ---------------------------------------------------------------- LDtk

def _grid_tiles(keys: List[List[Optional[str]]], slots: Dict[str, dict]) -> List[dict]:
    """열쇠가 있는 칸마다 타일 인스턴스 하나(행 우선). d는 격자(Collision) 폭 기준 칸 번호."""
    cols = len(keys[0]) if keys else 0
    out = []
    for y, row in enumerate(keys):
        for x, key in enumerate(row):
            if key is None:
                continue
            s = slots[key]
            out.append({"px": [x * CELL, y * CELL], "src": [s["x"], s["y"]], "f": 0,
                        "t": _slot_index(s), "d": [y * cols + x], "a": 1})
    return out


def _layer_defs() -> Dict[str, dict]:
    """층 정의(편집기 꼴). doc 문자열이 이 도구가 만든 방이라는 표시다(편집기는 머리말을 자기 것으로 바꾸지만 doc은 지킨다)."""
    mark = "픽셀 키트 room build가 평면에서 만든 방."
    return {
        "Collision": ldtk.layer_def("IntGrid", "Collision", LAYER_UIDS["Collision"], CELL, int_grid_values=[
            ldtk.int_grid_value(v, name, color) for v, name, color in INT_VALUES], opacity=COLLISION_OPACITY,
            doc=f"{mark} 1 벽(막음) / 2 바닥(걸음) / 0 밖. 다시 칠한 뒤 kit.py room repaint로 Wall·Ground 그림을 맞춘다."),
        "Door": ldtk.layer_def("Entities", "Door", LAYER_UIDS["Door"], CELL,
                               doc=f"{mark} 문. Where 값을 maps.json의 doors 표에 적어야 게임에서 열린다."),
        "Furniture": ldtk.layer_def("Entities", "Furniture", LAYER_UIDS["Furniture"], CELL, tags=["ysort"],
                                    doc=f"{mark} 가구(태그 ysort — 캐릭터와 발 위치로 앞뒤를 가린다)."),
        "Wall": ldtk.layer_def("Tiles", "Wall", LAYER_UIDS["Wall"], CELL, tileset_uid=TILESET_UID,
                               doc=f"{mark} 벽 그림. room repaint가 Collision에서 다시 쓴다(손으로 칠하지 않는다)."),
        "Ground": ldtk.layer_def("Tiles", "Ground", LAYER_UIDS["Ground"], CELL, tileset_uid=TILESET_UID,
                                 doc=f"{mark} 바닥 그림. room repaint가 Collision에서 다시 쓴다(손으로 칠하지 않는다)."),
    }


def _door_def() -> dict:
    return ldtk.entity_def("Door", DOOR_DEF_UID, CELL, CELL, color="#BE4A2F", fields=[ldtk.field_def_string("Where", WHERE_FIELD_UID)])


def _ldtk(room: Room, grid: List[List[int]], tiles: RoomTiles, slots: Dict[str, dict],
          sheet_height: int, level_name: str) -> dict:
    cols, rows = len(grid[0]), len(grid)
    defs = _layer_defs()
    door_def = _door_def()
    where_def = door_def["fieldDefs"][0]
    tileset = ldtk.tileset_def("RoomTiles", TILESET_UID, TILES_NAME, CELL * SHEET_WIDTH_CELLS, sheet_height, CELL)

    def layer(name, **extra):
        return ldtk.layer_instance(defs[name], _iid(room.id, "layer", name), LEVEL_UID, (cols, rows), **extra)

    doors = [ldtk.entity_instance(door_def, _iid(room.id, "door", x, y), (x * CELL, y * CELL), CELL,
                                  fields=[ldtk.field_instance_string(where_def, dest)])
             for (x, y), dest in sorted(room.doors.items(), key=lambda kv: (kv[0][1], kv[0][0]))]
    instances = {
        "Collision": layer("Collision", int_grid=[v for row in grid for v in row]),
        "Door": layer("Door", entities=doors),
        "Furniture": layer("Furniture"),
        "Wall": layer("Wall", tileset=tileset, grid_tiles=_grid_tiles(tiles.wall, slots)),
        "Ground": layer("Ground", tileset=tileset, grid_tiles=_grid_tiles(tiles.ground, slots)),
    }
    return ldtk.project(
        _iid(room.id, "project"), _iid(room.id, "dummy-world"), next_uid=NEXT_UID, grid=CELL,
        level_size=(cols * CELL, rows * CELL), bg_color=BG_COLOR, level_bg_color=BG_COLOR,
        defs=ldtk.definitions([defs[n] for n in LAYER_ORDER], [door_def], [tileset]),
        levels=[ldtk.level(level_name, _iid(room.id, "level"), LEVEL_UID, cols * CELL, rows * CELL, BG_COLOR,
                           [instances[n] for n in LAYER_ORDER])])


def _maps(room: Room, root: Path, ldtk_rel: str, level_name: str, rows: int) -> dict:
    sx, sy = room.spawn
    return {**walkers(root, RoomError), "maps": [{
        "id": level_name, "ldtk": ldtk_rel, "level": level_name, "viewCells": min(rows, MAX_VIEW_CELLS),
        "collision": {"rule": "interior-auto"}, "doors": {},
        "spawns": {spawn_name(level_name): [sx * CELL + CELL // 2, sy * CELL + CELL // 2]}}]}


# ---------------------------------------------------------------- build

def existing_room_files(repo_root, room_id: str, out_dir=None) -> List[str]:
    """build가 쓸 넷 중 이미 있는 파일 이름(--force로 덮어쓰게 되는 것)."""
    root = Path(repo_root).resolve()
    out = Path(out_dir) if out_dir is not None else root / DEFAULT_ROOT / room_id
    return [n for n in (f"{room_id}.ldtk", TILES_NAME, SLOTS_NAME, MAPS_NAME) if (out / n).exists()]


def build_room(repo_root, room_id: str, out_dir=None, force: bool = False, dry_run: bool = False,
               kit: Optional[Kit] = None, rooms_dir=None) -> List[str]:
    """평면 `<rooms_dir>/<room_id>.json`에서 방을 쓴다. 쓴(dry_run이면 쓸) 파일의 저장소 뿌리 기준 경로를 돌려준다.

    평면이나 그림에 문제가 있으면 RoomBuildError, 읽을 수 없거나 덮어쓰게 되면(force 없이) 저장소 밖이면 RoomError.
    force여도 있는 파일이 이 방의 것이 아니면(게임의 maps.json, 다른 방, 방이 아닌 같은 이름 .ldtk) RoomError.
    kit이 없으면 저장소의 art/kit을 읽는다. 알림(room-wall-thin)은 막지 않는다(알리는 것은 명령이 check_grid로 한다)."""
    root = Path(repo_root).resolve()
    rooms = Path(rooms_dir) if rooms_dir is not None else root / DEFAULT_ROOMS_DIR
    room = load_room(rooms / f"{room_id}.json")
    if room.id != room_id:
        raise RoomError(f"평면의 id {room.id!r}가 요청한 {room_id!r}과 다르다")
    out = (Path(out_dir) if out_dir is not None else root / DEFAULT_ROOT / room_id).resolve()
    try:
        out.relative_to(root)
    except ValueError:
        raise RoomError(f"출력 폴더 {out}가 저장소 {root} 밖이다(maps.json의 ldtk는 저장소 뿌리 기준 경로라야 한다)") from None

    blocking = [p for p in check_grid(room.grid, room.spawn, room.doors, room.face_height) if not is_warning(p)]
    if blocking:
        raise RoomBuildError(blocking)
    kit = kit if kit is not None else load_kit(str(root / "art" / "kit"))
    floor_spec = kit.recipes.get(room.floor)
    if floor_spec is None or floor_spec.get("kind") != "tile":
        raise RoomError(f"바닥 {room.floor!r}: 키트에 그런 타일 명세가 없다")

    grid = cells(room.grid)
    band = wall_band(room.wall)                                                      # 모르는 벽 모양은 RecipeError
    plan = plan_tiles(grid, room.face_height, band)
    wanted, tiles = _draw_all(kit, plan, room.wall, floor_spec)

    names = {"ldtk": f"{room.id}.ldtk", "png": TILES_NAME, "slots": SLOTS_NAME, "maps": MAPS_NAME}
    existing = [n for n in names.values() if (out / n).exists()]
    if existing and not force:
        raise RoomError(f"{out}에 이미 {', '.join(existing)}가 있다(덮어쓰려면 --force)")
    ldtk_rel = (out / names["ldtk"]).relative_to(root).as_posix()
    _check_overwritable(out, names["ldtk"], ldtk_rel)
    png, table, slots, height = _sheet(out, wanted, tiles, {"wall": room.wall, "floor": room.floor,
                                                            "face_height": room.face_height, "band": band})

    level_name = map_id(room.id)
    ldtk = dump(_ldtk(room, grid, plan, slots, height, level_name))
    maps = dump(_maps(room, root, ldtk_rel, level_name, len(grid)))
    files = [(out / names["ldtk"], ldtk), (out / TILES_NAME, png), (out / SLOTS_NAME, table), (out / MAPS_NAME, maps)]
    if dry_run:
        return [p.relative_to(root).as_posix() for p, data in files if not _same(p, data)]
    return write_all(root, files)


def _draw_all(kit: Kit, plan: RoomTiles, wall: str, floor_spec: dict) -> Tuple[List[str], Dict[str, object]]:
    """(쓰인 열쇠 — 행 우선, 칸마다 Wall 다음 Ground, 그린 타일). 검사에 걸린 타일이 있으면 RoomBuildError."""
    wanted: List[str] = []
    for wall_row, ground_row in zip(plan.wall, plan.ground):
        for pair in zip(wall_row, ground_row):
            for k in pair:
                if k is not None and k not in wanted:
                    wanted.append(k)
    tiles = {k: draw_tile(kit.style, k, wall, floor_spec) for k in wanted}           # 모르는 벽 모양은 RecipeError
    found = [Problem(p.code, f"{k}: {p.message}", p.x, p.y) for k, cv in tiles.items() for p in lint_room_tile(cv, k, kit.style)]
    if found:
        raise RoomBuildError(found)
    return wanted, tiles


def _check_overwritable(out: Path, ldtk_name: str, ldtk_rel: str) -> None:
    """이미 있는 파일이 이 방의 것인지 본다. 아니면 --force여도 RoomError(게임의 맵 목록·다른 맵을 지키려는 것).

    maps.json은 맵이 하나이고 그 ldtk가 이 방의 <id>.ldtk라야 하고, <id>.ldtk는 방 모양(Collision·Wall·Ground 층)이라야 한다.
    tiles.png와 자리 표가 함께 있는지는 _read_sheet가 본다."""
    maps_file, ldtk_file = out / MAPS_NAME, out / ldtk_name
    if maps_file.exists():
        try:
            maps = loads_with_comments(maps_file.read_text(encoding="utf-8"))["maps"]
            ok = isinstance(maps, list) and len(maps) == 1 and maps[0].get("ldtk") == ldtk_rel
        except (OSError, ValueError, KeyError, TypeError, AttributeError):
            ok = False
        if not ok:
            raise RoomError(f"{maps_file}가 이 방의 맵 목록(맵 하나, ldtk {ldtk_rel})이 아니라 --force여도 덮어쓰지 않는다 — "
                            f"--out을 이 방만의 폴더로 준다")
    if ldtk_file.exists():
        try:
            shape = _room_shape(ldtk_file, _read_ldtk(ldtk_file))
        except RoomError:
            shape = []
        if isinstance(shape, list):
            raise RoomError(f"{ldtk_file}가 room build가 만든 방(Collision·Wall·Ground 층)이 아니라 --force여도 덮어쓰지 않는다")


def _same(path: Path, data: bytes) -> bool:
    try:
        return path.read_bytes() == data
    except OSError:
        return False



# ---------------------------------------------------------------- LDtk 방 읽기(check·repaint)

def _not_a_room(path: Path, why: str) -> List[Problem]:
    return [Problem("room-not-a-room", f"{path.name}: {why} — 픽셀 키트 room build가 만든 방(Collision·Wall·Ground 층)이 아니다")]


def _incomplete(path: Path, doc) -> List[Problem]:
    """편집기가 저장한 파일에 늘 있는 최상위 키가 빠졌으면 room-ldtk-incomplete 하나(이 도구가 2026-11-14 전에 쓴 모자란 파일)."""
    missing = ldtk.missing_project_keys(doc)
    if not missing:
        return []
    return [Problem("room-ldtk-incomplete",
                    f"{path.name}: LDtk 편집기가 기대하는 키 {len(missing)}개가 없다({', '.join(missing[:5])}{' …' if len(missing) > 5 else ''}) — "
                    f"예전 도구가 쓴 파일이라 편집기에서 열리지 않는다. 평면에서 kit.py room build <id> --force로 다시 만든다")]


def _read_ldtk(path: Path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except OSError as e:
        raise RoomError(f"{path}를 못 읽었다({e})") from None
    except ValueError as e:
        raise RoomError(f"{path}가 JSON이 아니다({e})") from None


def _room_shape(path: Path, doc):
    """(레벨, 층 이름 → 층 인스턴스, 값 격자) 또는 문제 목록(room-not-a-room 하나)."""
    if not isinstance(doc, dict) or not isinstance(doc.get("levels"), list):
        return _not_a_room(path, "levels가 없다")
    if doc.get("externalLevels") is True:
        return _not_a_room(path, "레벨을 따로 저장한 파일(externalLevels)이다")
    if len(doc["levels"]) != 1:
        return _not_a_room(path, f"레벨이 {len(doc['levels'])}개다(방은 하나)")
    level = doc["levels"][0]
    insts = level.get("layerInstances") if isinstance(level, dict) else None
    if not isinstance(insts, list):
        return _not_a_room(path, "레벨에 layerInstances가 없다")
    layers = {}
    for li in insts:
        if isinstance(li, dict) and isinstance(li.get("__identifier"), str):
            layers.setdefault(li["__identifier"], li)
    missing = [f"{name}({kind})" for name, kind in ROOM_LAYERS
               if name not in layers or layers[name].get("__type") != kind]
    if missing:
        return _not_a_room(path, "층이 없거나 종류가 다르다: " + ", ".join(missing))
    col = layers["Collision"]
    w, h, csv = col.get("__cWid"), col.get("__cHei"), col.get("intGridCsv")
    if type(w) is not int or type(h) is not int or w < 1 or h < 1 or not isinstance(csv, list) or len(csv) != w * h:
        return _not_a_room(path, "Collision의 __cWid×__cHei와 intGridCsv 길이가 맞지 않는다")
    bad = [n for n, _ in ROOM_LAYERS if layers[n].get("__gridSize") != CELL]
    if bad:
        return _not_a_room(path, f"칸 크기가 {CELL}이 아닌 층: {', '.join(bad)}")
    bad = [n for n in ART_LAYERS if (layers[n].get("__cWid"), layers[n].get("__cHei")) != (w, h)]
    if bad:
        return _not_a_room(path, f"Collision({w}x{h}칸)과 크기가 다른 층: {', '.join(bad)}")
    if (level.get("pxWid"), level.get("pxHei")) != (w * CELL, h * CELL):
        return _not_a_room(path, f"레벨 크기 {level.get('pxWid')}x{level.get('pxHei')}px가 Collision({w}x{h}칸)과 다르다")
    grid = [list(csv[y * w:(y + 1) * w]) for y in range(h)]
    return level, layers, grid


def _room_context(path: Path, level: dict, layers: dict):
    """같은 폴더의 자리 표와 maps.json에서: (방 값, 도착 칸, 문 칸 → Where, doors 표). 못 읽으면 RoomError."""
    out = path.parent
    _, _, values = _read_sheet(out)
    missing = [k for k in ROOM_VALUES if k != "band" and k not in values]
    if missing:
        raise RoomError(f"{out / SLOTS_NAME}에 {', '.join(missing)}가 없다(벽 모양을 모른다). "
                        f"평면에서 room build --force로 다시 만든 뒤 LDtk에서 칠한다")
    if "band" not in values:
        raise RoomError(f"{out / SLOTS_NAME}에 band(윗면 띠 두께)가 없다 — 윗면이 칸을 꽉 채우던(32px) 때 만든 방이라 "
                        f"옛 두께로는 다시 그리지 않는다. LDtk에서 칠한 것을 지키려면 그 자리 표에 \"band\": 16을 적고 다시 repaint한다"
                        f"(옛 윗면 타일은 시트에 남지만 쓰이지 않는다). 평면에서 다시 만들어도 되면 room build <id> --force(LDtk에서 칠한 것은 사라진다)")
    if type(values["face_height"]) is not int or values["face_height"] < 1:
        raise RoomError(f"{out / SLOTS_NAME}: face_height {values['face_height']!r}가 1 이상의 정수가 아니다")
    try:
        maps = loads_with_comments((out / MAPS_NAME).read_text(encoding="utf-8"))
        entry = next(m for m in maps["maps"] if m.get("level") == level.get("identifier"))
        sx, sy = next(iter((entry.get("spawns") or {}).values()))
        sx, sy = int(sx), int(sy)
    except (OSError, ValueError, KeyError, TypeError, AttributeError, StopIteration) as e:
        raise RoomError(f"{out / MAPS_NAME}에서 레벨 {level.get('identifier')!r}의 맵과 첫 도착 지점을 못 읽었다({e!r})") from None
    door_entity = entry.get("doorEntity", "Door")
    door_field = entry.get("doorField", "Where")
    doors: Dict[Tuple[int, int], str] = {}
    for li in layers.values():
        for e in li.get("entityInstances") or []:
            if not isinstance(e, dict) or e.get("__identifier") != door_entity:
                continue
            try:
                cell = (int(e["px"][0]) // CELL, int(e["px"][1]) // CELL)
            except (KeyError, TypeError, ValueError, IndexError):
                continue
            where = next((f.get("__value") for f in e.get("fieldInstances") or []
                          if isinstance(f, dict) and f.get("__identifier") == door_field), None)
            doors[cell] = where if where is not None else ""
    return values, (sx // CELL, sy // CELL), doors, entry.get("doors") or {}


def _art_mismatch(grid, plan: RoomTiles, slots, layers) -> List[Problem]:
    """칸마다 Wall·Ground에 와야 할 타일(plan_tiles의 열쇠가 있으면 그 자리 하나, 없으면 타일 없음)과 실제 타일을 견준다.
    벽 칸의 Ground 타일은 띠 옆으로 바닥이 비치는 칸에만 와야 한다."""
    placed = {name: {} for name in ART_LAYERS}
    for name in ART_LAYERS:
        for t in layers[name].get("gridTiles") or []:
            try:
                cell = (int(t["px"][0]) // CELL, int(t["px"][1]) // CELL)
                placed[name].setdefault(cell, []).append(list(t["src"]))
            except (KeyError, TypeError, ValueError, IndexError):
                continue
    expected = {"Wall": plan.wall, "Ground": plan.ground}
    bad = []
    for y, row in enumerate(grid):
        for x in range(len(row)):
            ok = True
            for name in ART_LAYERS:
                got = placed[name].get((x, y), [])
                key = expected[name][y][x]
                if key is not None:
                    s = slots.get(key)
                    ok = ok and s is not None and got == [[s["x"], s["y"]]]
                else:
                    ok = ok and not got
            if not ok:
                bad.append((x, y))
    if not bad:
        return []
    x, y = bad[0]
    return [Problem("room-art-mismatch",
                    f"Wall·Ground 타일이 Collision과 어긋난 칸 {len(bad)}개(첫 칸 ({x}, {y})) — room repaint로 맞춘다", x, y)]


def check_room(ldtk_path) -> List[Problem]:
    """LDtk 방 파일을 쓰지 않고 검사한다. 필요한 층이 없으면 room-not-a-room 하나. 아니면 room-ldtk-incomplete(편집기 꼴이 아닌
    예전 파일 — 막는 문제), 평면 검사(check_grid —
    도착 지점은 maps.json의 첫 도착 지점, 문은 Door 엔티티), room-door-unregistered(알림), room-art-mismatch.
    같은 폴더의 자리 표·maps.json을 못 읽으면 RoomError."""
    path = Path(ldtk_path)
    doc = _read_ldtk(path)
    shape = _room_shape(path, doc)
    if isinstance(shape, list):
        return shape
    level, layers, grid = shape
    values, spawn, doors, doors_table = _room_context(path, level, layers)
    problems = _incomplete(path, doc) + check_grid(grid, spawn, doors, values["face_height"]) + door_problems(doors, doors_table)
    if any(p.code == "room-unknown-char" for p in problems):
        return problems                            # 칸 값을 믿을 수 없으니 그림은 견주지 않는다
    slots, _, _ = _read_sheet(path.parent)
    return problems + _art_mismatch(grid, _plan(path, grid, values), slots, layers)


def _plan(path: Path, grid, values: dict) -> RoomTiles:
    try:
        return plan_tiles(grid, values["face_height"], values["band"])
    except RecipeError as e:                                  # 자리 표의 band가 받을 수 없는 값
        raise RoomError(f"{path.parent / SLOTS_NAME}: {e}") from None


def _same_tiles(old, new) -> bool:
    """타일 목록이 순서만 다르고 값이 같은가."""
    if not isinstance(old, list):
        return False
    try:
        return sorted(json.dumps(t, sort_keys=True) for t in old) == sorted(json.dumps(t, sort_keys=True) for t in new)
    except TypeError:
        return False


def _default_kit() -> Kit:
    return load_kit(str(Path(__file__).resolve().parents[3] / "art" / "kit"))     # 이 도구가 든 저장소의 키트


def repaint_room(ldtk_path, dry_run: bool = False, kit: Optional[Kit] = None) -> List[str]:
    """Collision 층을 읽어 Wall·Ground 층의 gridTiles와 tiles.png·tiles.slots.json만 다시 쓴다.

    문서의 나머지(다른 층, 엔티티, 필드, 모르는 키, 키 순서)는 읽은 그대로 다시 쓴다(들여쓰기는 이 도구의 것이 된다).
    타일셋 정의는 pxWid·pxHei(있으면 __cWid·__cHei도)만 시트에 맞춘다. 벽 모양·바닥·face_height·band는 같은 폴더의 자리 표에서 읽는다(build가 남긴다).
    band가 없는 옛 자리 표(윗면 32px 시절)는 RoomError — 옛 두께로 조용히 그리지 않는다.
    필요한 층이 없거나(room-not-a-room) 편집기 꼴이 아닌 예전 파일이거나(room-ldtk-incomplete) 칠한 칸에 막는 문제가 있으면
    RoomBuildError — 아무것도 쓰지 않는다.
    쓴(dry_run이면 쓸) 파일 이름(LDtk 파일이 든 폴더 기준)을 돌려준다. 바꿀 것이 없으면 빈 목록."""
    path = Path(ldtk_path).resolve()
    doc = _read_ldtk(path)
    shape = _room_shape(path, doc)
    if isinstance(shape, list):
        raise RoomBuildError(shape)
    if _incomplete(path, doc):
        raise RoomBuildError(_incomplete(path, doc))      # 모자란 파일을 채워 쓰지 않는다(무엇을 채울지 추측하지 않는다)
    level, layers, grid = shape
    uid = layers["Wall"].get("__tilesetDefUid")
    tileset = next((ts for ts in (doc.get("defs") or {}).get("tilesets") or []
                    if isinstance(ts, dict) and ts.get("uid") == uid), None)
    if tileset is None or tileset.get("relPath") != TILES_NAME or layers["Ground"].get("__tilesetDefUid") != uid:
        raise RoomBuildError(_not_a_room(path, f"Wall·Ground 층이 같은 타일셋 {TILES_NAME}을 쓰지 않는다"))
    values, spawn, doors, _ = _room_context(path, level, layers)
    blocking = [p for p in check_grid(grid, spawn, doors, values["face_height"]) if not is_warning(p)]
    if blocking:
        raise RoomBuildError(blocking)

    kit = kit if kit is not None else _default_kit()
    floor_spec = kit.recipes.get(values["floor"])
    if floor_spec is None or floor_spec.get("kind") != "tile":
        raise RoomError(f"바닥 {values['floor']!r}: 키트에 그런 타일 명세가 없다")
    plan = _plan(path, grid, values)
    wanted, tiles = _draw_all(kit, plan, values["wall"], floor_spec)
    out = path.parent
    png, table, slots, height = _sheet(out, wanted, tiles, values)

    changed = False
    for name, keys in (("Wall", plan.wall), ("Ground", plan.ground)):
        new = _grid_tiles(keys, slots)
        if not _same_tiles(layers[name].get("gridTiles"), new):
            layers[name]["gridTiles"] = new          # 있는 키의 값만 바꾼다(키 자리는 그대로)
            changed = True
    size = [("pxWid", CELL * SHEET_WIDTH_CELLS), ("pxHei", height)]
    size += [(k, v // CELL) for k, v in (("__cWid", CELL * SHEET_WIDTH_CELLS), ("__cHei", height)) if k in tileset]
    for k, v in size:                                    # __cWid·__cHei는 편집기가 저장한 문서에만 있다
        if tileset.get(k) != v:
            tileset[k] = v
            changed = True
    files = [(out / TILES_NAME, png), (out / SLOTS_NAME, table)]
    if changed:
        files.insert(0, (path, dump(doc)))
    if dry_run:
        return [p.relative_to(out).as_posix() for p, data in files if not _same(p, data)]
    return write_all(out, files)
