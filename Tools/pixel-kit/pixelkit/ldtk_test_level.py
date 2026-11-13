"""시험용 LDtk 레벨 쓰기: 내보낸 묶음 시트(art/tiles, art/world의 *.slots.json)가 게임에서 보이는지 한 눈에 보는 작은 맵.

LDtk 파일은 편집기(1.5.3)가 저장하는 꼴 그대로 쓴다(ldtk_common의 편집기 꼴 함수 — 공식 스키마의 필수 필드를 테스트가 지킨다).
게임 읽기 코드(game/Rootborn.World/Ldtk/LdtkFile.cs, WorldCatalog.cs)가 읽는 값은 그 위에 얹는다. 어디서 어느 필드를 읽는지는
README "시험용 맵"에 있다. 그림 엔티티마다 같은 이름의 엔티티 정의(그 자리의 타일로 그림)를 둔다 — 편집기는 엔티티의 __tile을
정의에서 다시 계산한다.

맵은 `pixel-kit-test.ldtk`(레벨 PixelKitTest) 하나와 그 맵 하나만 가진 `maps.json`이다. `--maps`로 게임에 준다.
"""
import json
import os
import uuid
from pathlib import Path
from typing import Dict, List, Optional, Tuple

from . import ldtk_common as ldtk
from .ldtk_common import SOURCE_MAPS, dump as _dump, loads_with_comments as _loads_with_comments, walkers

CELL = 32
LDTK_NAME = "pixel-kit-test.ldtk"
MAPS_NAME = "maps.json"
MAP_ID = "PixelKitTest"
DEFAULT_OUT = "art/world/test"            # 저장소 뿌리 기준
MAX_VIEW_CELLS = 13                       # 화면에 보일 높이(칸). 레벨이 더 낮으면 레벨 높이
TILES_DIR, WORLD_DIR = "art/tiles", "art/world"
LAYERS = (("Art", "Entities"), ("Ground", "Tiles"))   # 파일 안 순서(위 층이 먼저). 이 도구가 쓴 시험 맵이라는 표시(머리말로 판별하지 않는다)
BG_COLOR = "#40465B"
ART_COLOR = "#94D9B3"
_NAMESPACE = uuid.UUID("0c3f6a52-9b7e-4d1a-8f20-7a1d5e9b4c33")      # iid를 결정적으로 만든다

class TestLevelError(Exception):
    __test__ = False    # unittest가 테스트 클래스로 모으지 않게


def _read_slots(root: Path, folder: str) -> List[Tuple[str, Dict[str, dict]]]:
    """(시트 PNG 경로, 자리 표) 목록. 파일 이름순."""
    out = []
    for table in sorted((root / folder).glob("*.slots.json")):
        png = f"{folder}/{table.name[:-len('.slots.json')]}"
        try:
            slots = json.loads(table.read_text(encoding="utf-8"))["slots"]
            for s in slots.values():
                assert all(type(s[k]) is int for k in "xywh")
        except (OSError, ValueError, KeyError, TypeError, AssertionError) as e:
            raise TestLevelError(f"{table}를 못 읽었다({e})") from None
        if not (root / png).exists():
            raise TestLevelError(f"{png}가 없다(자리 표만 있다)")
        out.append((png, slots))
    return out


def _png_size(path: Path) -> Tuple[int, int]:
    from PIL import Image
    with Image.open(path) as im:
        return im.size


def _walkers(root: Path) -> dict:
    return walkers(root, TestLevelError)


def build_level(root: Path, out: Path) -> Tuple[bytes, bytes]:
    """(.ldtk 내용, maps.json 내용). 쓰지 않는다."""
    tiles = _read_slots(root, TILES_DIR)
    worlds = [(png, slots) for png, slots in _read_slots(root, WORLD_DIR) if slots]
    # 바닥 규칙: 첫 타일 시트(파일 이름순)에서 32x32 자리 가운데 맨 처음 것. 이름이나 종류를 가리지 않는다.
    ground = next(((png, name, s) for png, slots in tiles for name, s in slots.items() if (s["w"], s["h"]) == (CELL, CELL)), None)
    if ground is None:
        raise TestLevelError(f"{TILES_DIR}에 32x32 타일 자리가 없다(타일을 먼저 내보낸다)")
    if not worlds:
        raise TestLevelError(f"{WORLD_DIR}에 건물·소품 자리가 없다(건물이나 소품을 먼저 내보낸다)")
    walkers = _walkers(root)

    sheets = [ground[0]] + [png for png, _ in worlds]
    # uid는 정의·레벨이 한 번호 공간을 쓴다. 타일셋 1..n은 게임이 읽는 값(__tile.tilesetUid)이라 예전 그대로, 레벨 0, 그 뒤로 층·엔티티.
    tileset_uid = {png: i + 1 for i, png in enumerate(sheets)}
    tilesets = []
    for png in sheets:
        w, h = _png_size(root / png)
        tilesets.append(ldtk.tileset_def(Path(png).parent.name + "_" + Path(png).stem, tileset_uid[png],
                                         os.path.relpath(root / png, out).replace("\\", "/"), w, h, CELL))
    tileset_by_png = dict(zip(sheets, tilesets))

    # 가로: 왼쪽 한 칸 여백, 그림마다 폭 + 한 칸 틈. 세로: 위 한 칸 여백 + 가장 큰 그림 높이 + 아래 세 칸(앞마당).
    placed = []     # (이름, 시트, 자리, 시작 칸)
    col = 1
    for png, slots in worlds:
        for name, s in slots.items():
            cells = -(-s["w"] // CELL)
            placed.append((name, png, s, col))
            col += cells + 1
    tall = max(-(-s["h"] // CELL) for _, _, s, _ in placed)
    cols, rows = col, 1 + tall + 3
    base_y = (1 + tall) * CELL

    ground_png, _, g = ground
    ground_ts = tileset_by_png[ground_png]
    level_uid, uid = 0, len(sheets) + 1
    mark = "픽셀 키트 시험용 맵(kit.py test-level이 쓴다). 손으로 고치지 않는다 — 다시 쓰면 덮인다."
    art_def = ldtk.layer_def("Entities", "Art", uid, CELL, tags=["art"], doc=mark + " 내보낸 건물·소품 그림(태그 art).")
    ground_def = ldtk.layer_def("Tiles", "Ground", uid + 1, CELL, tileset_uid=ground_ts["uid"], doc=mark + " 바닥 타일.")
    uid += 2
    entity_defs: Dict[str, dict] = {}
    entities = []
    for name, png, s, start in placed:
        rect = ldtk.tile_rect(tileset_uid[png], s["x"], s["y"], s["w"], s["h"])
        if name not in entity_defs:
            entity_defs[name] = ldtk.entity_def(name, uid, s["w"], s["h"], color=ART_COLOR, pivot=(0.5, 1), tile=rect,
                                                resizable=False, show_name=False)
            uid += 1
        elif entity_defs[name]["tileRect"] != rect:
            raise TestLevelError(f"그림 이름 {name!r}이 두 시트에 다른 그림으로 있다(LDtk 엔티티 정의는 이름마다 하나다)")
        entities.append(ldtk.entity_instance(entity_defs[name], _iid("entity", png, name),
                                             (start * CELL + s["w"] // 2, base_y), CELL))
    gtiles = [ldtk.grid_tile((c * CELL, r * CELL), (g["x"], g["y"]), ground_ts["__cWid"], CELL, cols, CELL)
              for r in range(rows) for c in range(cols)]
    layer_defs = {"Art": art_def, "Ground": ground_def}
    instances = {
        "Art": ldtk.layer_instance(art_def, _iid("layer", "Art"), level_uid, (cols, rows), entities=entities),
        "Ground": ldtk.layer_instance(ground_def, _iid("layer", "Ground"), level_uid, (cols, rows), tileset=ground_ts,
                                      grid_tiles=gtiles),
    }
    doc = ldtk.project(
        _iid("project"), _iid("dummy-world"), next_uid=uid, grid=CELL, level_size=(cols * CELL, rows * CELL),
        bg_color=BG_COLOR, level_bg_color=BG_COLOR,
        defs=ldtk.definitions([layer_defs[n] for n, _ in LAYERS], list(entity_defs.values()), tilesets),
        # 파일에서는 위 층이 먼저다(읽을 때 뒤집어 아래 층부터 그린다).
        levels=[ldtk.level(MAP_ID, _iid("level"), level_uid, cols * CELL, rows * CELL, BG_COLOR,
                           [instances[n] for n, _ in LAYERS])])

    first_name, _, first, first_col = placed[0]
    spawn_x = (first_col + (-(-first["w"] // CELL)) // 2) * CELL + CELL // 2
    spawn_y = (1 + tall + 1) * CELL + CELL // 2          # 첫 그림의 발 바로 아래 둘째 줄
    # 충돌: collider-layers에 빈 층 목록 → 어떤 층의 타일도 막지 않아 레벨 전체가 열린다(WorldMapLoader의 칸 크기는
    # 타일이 있는 Ground 층의 격자 32에서 온다). interior-auto는 Ground/Wall/Prop 이름 규칙이 필요해 쓰지 않았다.
    maps = {**walkers, "maps": [{
        "id": MAP_ID, "ldtk": f"{out.relative_to(root).as_posix()}/{LDTK_NAME}", "level": MAP_ID,
        "viewCells": min(rows, MAX_VIEW_CELLS),
        "collision": {"rule": "collider-layers", "layers": []},
        "doors": {}, "spawns": {"front": [spawn_x, spawn_y]}}]}
    return _dump(doc), _dump(maps)


def _check_overwritable(out: Path) -> None:
    """덮어쓸 파일이 이미 있으면 이 도구가 쓴 것인지 본다. 아니면 거절한다(게임의 실제 맵 목록을 지키려는 것)."""
    maps_file, ldtk_file = out / MAPS_NAME, out / LDTK_NAME
    if maps_file.exists():
        try:
            ids = [m.get("id") for m in _loads_with_comments(maps_file.read_text(encoding="utf-8"))["maps"]]
        except (OSError, ValueError, KeyError, TypeError, AttributeError):
            ids = None
        if ids != [MAP_ID]:
            raise TestLevelError(f"{maps_file}가 이 도구가 쓴 시험 맵 목록(맵이 {MAP_ID} 하나)이 아니라 덮어쓰지 않는다")
    if ldtk_file.exists() and not _is_test_level(ldtk_file):
        raise TestLevelError(f"{ldtk_file}가 이 도구가 만든 시험 맵(레벨 {MAP_ID} 하나, 층 Art·Ground)이 아니라 덮어쓰지 않는다")


def _is_test_level(path: Path) -> bool:
    """레벨이 MAP_ID 하나이고 층이 Art(Entities)·Ground(Tiles)인가. 머리말로 판별하지 않는다(편집기가 저장하면 자기 것으로 바꾼다)."""
    try:
        levels = json.loads(path.read_text(encoding="utf-8-sig"))["levels"]
        if len(levels) != 1 or levels[0]["identifier"] != MAP_ID:
            return False
        return sorted((li["__identifier"], li["__type"]) for li in levels[0]["layerInstances"]) == sorted(LAYERS)
    except (OSError, ValueError, KeyError, TypeError, AttributeError):
        return False


def _iid(*parts) -> str:
    return str(uuid.uuid5(_NAMESPACE, "/".join(str(p) for p in parts)))


def write_test_level(repo_root, out_dir=None) -> List[str]:
    """시험용 레벨을 쓴다. 바뀐 파일(뿌리 기준 경로)을 돌려주고, 같은 내용이면 쓰지 않는다."""
    root = Path(repo_root).resolve()
    out = (Path(out_dir) if out_dir is not None else root / DEFAULT_OUT).resolve()
    try:
        out.relative_to(root)
    except ValueError:
        raise TestLevelError(f"출력 폴더 {out}가 저장소 {root} 밖이다(maps.json의 ldtk는 저장소 뿌리 기준 경로라야 한다)") from None
    ldtk, maps = build_level(root, out)
    _check_overwritable(out)
    written = []
    for name, data in ((LDTK_NAME, ldtk), (MAPS_NAME, maps)):
        path = out / name
        try:
            same = path.read_bytes() == data
        except OSError:
            same = False
        if same:
            continue
        out.mkdir(parents=True, exist_ok=True)
        tmp = path.with_name(path.name + ".tmp")
        tmp.write_bytes(data)
        os.replace(tmp, path)
        written.append(path.relative_to(root).as_posix())
    return written
