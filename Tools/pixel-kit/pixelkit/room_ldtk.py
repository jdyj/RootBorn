"""방 만들기 — 평면에서 게임이 그대로 읽는 LDtk 방, 타일 시트, 맵 목록 쓰기(`kit.py room build`).

결과(기본 `art/world/rooms/<id>/`): `<id>.ldtk`, `tiles.png`(쓰인 타일만 모은 시트, 덧붙이기만), `tiles.slots.json`, `maps.json`.
LDtk 층(위에서 아래): Collision(IntGrid 32, 1 벽 / 2 바닥), Door(Entities), Furniture(Entities, 태그 ysort, 비어 있음), Wall(Tiles), Ground(Tiles).
쓰는 것은 게임 읽기 코드(game/Rootborn.World/Ldtk/LdtkFile.cs, WorldCatalog.cs)가 읽는 필드와 LDtk 편집기가 파일을 열 때 필요한 값이다.
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
from .ldtk_common import dump, walkers, write_all
from .lint import Problem
from .room_plan import Room, RoomError, cells, check_grid, is_warning, load_room
from .room_tiles import FLOOR, OUT, WALL, classify, draw_tile, lint_room_tile

DEFAULT_ROOT = "art/world/rooms"           # 저장소 뿌리 기준. 방마다 <id> 폴더
DEFAULT_ROOMS_DIR = "art/kit/rooms"
TILES_NAME, SLOTS_NAME, MAPS_NAME = "tiles.png", "tiles.slots.json", "maps.json"
MAX_VIEW_CELLS = 13                        # 화면에 보일 높이(칸). 레벨이 더 낮으면 레벨 높이
LDTK_VERSION = "1.5.3"
LAYER_ORDER = ("Collision", "Door", "Furniture", "Wall", "Ground")      # 파일 안 순서: 위 층이 먼저
INT_VALUES = ((WALL, "wall", "#8a6f5a"), (FLOOR, "floor", "#c9b99a"))   # Collision 값 약속(game/world/README.md 3절)
TILESET_UID, DOOR_DEF_UID, WHERE_FIELD_UID = 1, 20, 21
_NAMESPACE = uuid.UUID("5d9b2a0e-6a3c-4a53-9d5e-2b0b6a6f3c11")      # iid를 결정적으로 만든다


class RoomBuildError(Exception):
    """평면이나 그림에 쓰기를 막는 문제가 있다. problems가 그 목록(알림은 들어 있지 않다)."""

    def __init__(self, problems: List[Problem]):
        super().__init__("; ".join(f"{p.code} {p.message}" for p in problems))
        self.problems = problems


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
    """(자리 표, 그림). 둘 다 없으면 ({}, None). 하나만 있거나 못 읽거나 이 도구의 시트가 아니면 RoomError."""
    table_file, png_file = out / SLOTS_NAME, out / TILES_NAME
    if not table_file.exists() and not png_file.exists():
        return {}, None
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
        with Image.open(png_file) as im:
            img = im.convert("RGBA")
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as e:
        raise RoomError(f"{out}의 {SLOTS_NAME}·{TILES_NAME}를 못 읽었다({e})") from None
    if img.width != CELL * SHEET_WIDTH_CELLS or any(s["x"] + s["w"] > img.width or s["y"] + s["h"] > img.height for s in slots.values()):
        raise RoomError(f"{out}: {TILES_NAME} 크기 {img.size}가 {SLOTS_NAME}와 맞지 않는다")
    return slots, img


def _sheet(out: Path, wanted: List[str], tiles: Dict[str, object]) -> Tuple[bytes, bytes, Dict[str, dict], int]:
    """(PNG, 자리 표, 자리, 시트 높이). 이미 있는 시트는 자리를 지키고 지금 쓰는 타일만 다시 그린다."""
    old_slots, old_img = _read_sheet(out)
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
    table = _json_bytes({"cell": CELL, "width_cells": SHEET_WIDTH_CELLS, "slots": slots})
    return png, table, slots, height


# ---------------------------------------------------------------- LDtk

def _layer_defs() -> List[dict]:
    base = {"gridSize": CELL, "displayOpacity": 1, "pxOffsetX": 0, "pxOffsetY": 0}
    return [
        {**base, "identifier": "Collision", "type": "IntGrid", "uid": 1, "uiFilterTags": [],
         "intGridValues": [{"value": v, "identifier": name, "color": color} for v, name, color in INT_VALUES]},
        {**base, "identifier": "Door", "type": "Entities", "uid": 2, "uiFilterTags": []},
        {**base, "identifier": "Furniture", "type": "Entities", "uid": 3, "uiFilterTags": ["ysort"]},
        {**base, "identifier": "Wall", "type": "Tiles", "uid": 4, "uiFilterTags": [], "tilesetDefUid": TILESET_UID},
        {**base, "identifier": "Ground", "type": "Tiles", "uid": 5, "uiFilterTags": [], "tilesetDefUid": TILESET_UID},
    ]


def _entity_defs() -> List[dict]:
    return [{"identifier": "Door", "uid": DOOR_DEF_UID, "width": CELL, "height": CELL, "pivotX": 0, "pivotY": 0, "tags": [],
             "fieldDefs": [{"identifier": "Where", "uid": WHERE_FIELD_UID, "__type": "String", "type": "F_String",
                            "isArray": False, "canBeNull": True, "defaultOverride": None}]}]


def _ldtk(room: Room, grid: List[List[int]], keys: List[List[Optional[str]]], slots: Dict[str, dict],
          sheet_height: int, level_name: str) -> dict:
    cols, rows = len(grid[0]), len(grid)
    uids = {d["identifier"]: d["uid"] for d in _layer_defs()}

    def layer(name, kind, **extra):
        return {"__identifier": name, "__type": kind, "__cWid": cols, "__cHei": rows, "__gridSize": CELL,
                "__opacity": 1, "__pxTotalOffsetX": 0, "__pxTotalOffsetY": 0, "iid": _iid(room.id, "layer", name),
                "levelId": 1, "layerDefUid": uids[name], "pxOffsetX": 0, "pxOffsetY": 0, "visible": True, **extra}

    def tiles(want: int):
        out = []
        for y, row in enumerate(grid):
            for x, v in enumerate(row):
                if v != want:
                    continue
                s = slots[keys[y][x]]
                out.append({"px": [x * CELL, y * CELL], "src": [s["x"], s["y"]], "f": 0,
                            "t": _slot_index(s), "d": [y * cols + x], "a": 1})
        return out

    door_entities = []
    for (x, y), dest in sorted(room.doors.items(), key=lambda kv: (kv[0][1], kv[0][0])):
        door_entities.append({
            "__identifier": "Door", "__grid": [x, y], "__pivot": [0, 0], "__tags": [], "iid": _iid(room.id, "door", x, y),
            "width": CELL, "height": CELL, "defUid": DOOR_DEF_UID, "px": [x * CELL, y * CELL],
            "fieldInstances": [{"__identifier": "Where", "__type": "String", "__value": dest, "defUid": WHERE_FIELD_UID,
                                "realEditorValues": []}]})
    tile_extra = {"__tilesetDefUid": TILESET_UID, "__tilesetRelPath": TILES_NAME, "overrideTilesetUid": None,
                  "intGridCsv": [], "autoLayerTiles": [], "seed": 0, "optionalRules": []}
    instances = {
        "Collision": layer("Collision", "IntGrid", intGridCsv=[v for row in grid for v in row], autoLayerTiles=[],
                           gridTiles=[], entityInstances=[], optionalRules=[], seed=0, overrideTilesetUid=None),
        "Door": layer("Door", "Entities", entityInstances=door_entities, gridTiles=[], autoLayerTiles=[], intGridCsv=[],
                      optionalRules=[], seed=0, overrideTilesetUid=None),
        "Furniture": layer("Furniture", "Entities", entityInstances=[], gridTiles=[], autoLayerTiles=[], intGridCsv=[],
                           optionalRules=[], seed=0, overrideTilesetUid=None),
        "Wall": layer("Wall", "Tiles", gridTiles=tiles(WALL), entityInstances=[], **tile_extra),
        "Ground": layer("Ground", "Tiles", gridTiles=tiles(FLOOR), entityInstances=[], **tile_extra),
    }
    return {
        "__header__": {"fileType": "LDtk Project JSON", "app": "pixel-kit",
                       "doc": "픽셀 키트가 평면에서 만든 방(kit.py room build). LDtk에서 Collision 칸을 다시 칠해도 된다"},
        "iid": _iid(room.id, "project"), "jsonVersion": LDTK_VERSION, "appBuildId": 0, "nextUid": 100,
        "worldLayout": "Free", "worldGridWidth": 256, "worldGridHeight": 256,
        "defaultGridSize": CELL, "defaultPivotX": 0, "defaultPivotY": 0, "defaultLevelWidth": cols * CELL,
        "defaultLevelHeight": rows * CELL, "bgColor": "#40465B", "externalLevels": False,
        "defs": {"layers": _layer_defs(), "entities": _entity_defs(), "enums": [], "externalEnums": [], "levelFields": [],
                 "tilesets": [{"uid": TILESET_UID, "identifier": "RoomTiles", "relPath": TILES_NAME,
                               "pxWid": CELL * SHEET_WIDTH_CELLS, "pxHei": sheet_height, "tileGridSize": CELL,
                               "spacing": 0, "padding": 0, "tags": [], "customData": [], "enumTags": []}]},
        "levels": [{"identifier": level_name, "iid": _iid(room.id, "level"), "uid": 1, "worldX": 0, "worldY": 0, "worldDepth": 0,
                    "pxWid": cols * CELL, "pxHei": rows * CELL, "__bgColor": "#40465B", "bgColor": None, "useAutoIdentifier": False,
                    "fieldInstances": [], "layerInstances": [instances[n] for n in LAYER_ORDER]}],
    }


def _maps(room: Room, root: Path, ldtk_rel: str, level_name: str, rows: int) -> dict:
    sx, sy = room.spawn
    return {**walkers(root, RoomError), "maps": [{
        "id": level_name, "ldtk": ldtk_rel, "level": level_name, "viewCells": min(rows, MAX_VIEW_CELLS),
        "collision": {"rule": "interior-auto"}, "doors": {},
        "spawns": {"spawn": [sx * CELL + CELL // 2, sy * CELL + CELL // 2]}}]}


# ---------------------------------------------------------------- build

def build_room(repo_root, room_id: str, out_dir=None, force: bool = False, dry_run: bool = False,
               kit: Optional[Kit] = None, rooms_dir=None) -> List[str]:
    """평면 `<rooms_dir>/<room_id>.json`에서 방을 쓴다. 쓴(dry_run이면 쓸) 파일의 저장소 뿌리 기준 경로를 돌려준다.

    평면이나 그림에 문제가 있으면 RoomBuildError, 읽을 수 없거나 덮어쓰게 되면(force 없이) 저장소 밖이면 RoomError.
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
    keys = classify(grid, room.face_height)
    wanted: List[str] = []
    for row in keys:
        for k in row:
            if k is not None and k not in wanted:
                wanted.append(k)
    tiles = {k: draw_tile(kit.style, k, room.wall, floor_spec) for k in wanted}      # 모르는 벽 모양은 RecipeError
    found = [Problem(p.code, f"{k}: {p.message}", p.x, p.y) for k, cv in tiles.items() for p in lint_room_tile(cv, k, kit.style)]
    if found:
        raise RoomBuildError(found)

    names = {"ldtk": f"{room.id}.ldtk", "png": TILES_NAME, "slots": SLOTS_NAME, "maps": MAPS_NAME}
    existing = [n for n in names.values() if (out / n).exists()]
    if existing and not force:
        raise RoomError(f"{out}에 이미 {', '.join(existing)}가 있다(덮어쓰려면 --force)")
    png, table, slots, height = _sheet(out, wanted, tiles)

    level_name = map_id(room.id)
    ldtk_rel = (out / names["ldtk"]).relative_to(root).as_posix()
    ldtk = dump(_ldtk(room, grid, keys, slots, height, level_name))
    maps = dump(_maps(room, root, ldtk_rel, level_name, len(grid)))
    files = [(out / names["ldtk"], ldtk), (out / TILES_NAME, png), (out / SLOTS_NAME, table), (out / MAPS_NAME, maps)]
    if dry_run:
        return [p.relative_to(root).as_posix() for p, data in files if not _same(p, data)]
    return write_all(root, files)


def _same(path: Path, data: bytes) -> bool:
    try:
        return path.read_bytes() == data
    except OSError:
        return False
