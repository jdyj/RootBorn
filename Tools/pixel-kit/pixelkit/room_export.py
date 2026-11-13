"""방 만들기 — 꾸밈(벽지·바닥) 정의에서 게임용 조각 시트와 좌표표를 낸다(`room export-finishes`).

게임은 칸마다 픽셀 출처표(room_tiles.plan_sources)대로 조각 그림을 붙여 벽·바닥을 그린다. 이 모듈이 그 조각을 만든다.
조각은 키트의 그리기(room_tiles.draw_tile)에서 나온다 — 새로 그리지 않는다. 그래서 "조각 + 출처표 = 지금 그림"이 성립한다
(test_room_export가 내보낸 시트에서 다시 잘라 조합해 확인한다).

입력은 정의 파일(data/building/RoomFinishDefinition.json)의 `sheet`·`slot`·`art`뿐이다(어느 꾸밈을 내보낼지). 한 시트 안의 조각 자리는
이름 순으로 한 줄 16칸이고 자리는 좌표표가 정한다(코드는 자리를 모른다). 문제가 있으면 아무것도 쓰지 않는다.
"""
import io
import json
from pathlib import Path
from typing import Dict, List, Tuple

from PIL import Image

from .canvas import Canvas
from .cli import Kit
from .export import ATLAS_ROOT, CELL, PIXELS_PER_UNIT, SHEET_WIDTH_CELLS, ExportPlan, apply, pending_writes
from .room_ldtk import floor_ids
from .room_tiles import DOOR_RECT, draw_tile, wall_band, wall_styles, wall_top, wall_tops  # noqa: F401 (wall_top: 시험이 쓴다)

DEFINITIONS = "data/building/RoomFinishDefinition.json"       # 저장소 뿌리 기준
TYPE = "RoomFinishDefinition"
BAND = 16                          # 조각 시트가 받는 띠 두께. 이 값이 아닌 벽 모양은 거절한다(좌표표 `band`로 게임이 견준다)
COMMON = "common"                  # 꾸밈과 무관한 조각(문지방·문짝)의 이름 앞
FACE_ENDS = ("", "l", "r", "lr")
FACE_TOPS = ("", "c", "t")         # 위 칸이 윗면(c), 위가 벽이 아님(t)
SIDES = "new"                      # 문지방이 바라보는 쪽(바닥이 있는 쪽): 위, 오른쪽, 왼쪽


class RoomExportError(Exception):
    def __init__(self, code: str, message: str):
        super().__init__(f"{code} {message}")
        self.code = code


# ---------------------------------------------------------------- 조각 이름

def _ranges(band: int) -> List[Tuple[int, int]]:
    """띠가 칸 안에서 놓이는 범위: 왼쪽(위) 끝, 가운데, 오른쪽(아래) 끝."""
    return [(a, a + band) for a in (0, (CELL - band) // 2, CELL - band)]


def face_pieces() -> List[str]:
    """꽉 찬 정면 24가지: 바닥에서 몇 번째 k(1·2) × 끝 표시 4 × 위 표시 3."""
    return [f"face:{k}" + (":" + ends + top if ends + top else "") for k in (1, 2) for ends in FACE_ENDS for top in FACE_TOPS]


def narrow_pieces(band: int = BAND) -> List[str]:
    """좁은 정면 12가지: k(1·2) × 띠 범위 3 × 천장 그늘(c) 있음·없음."""
    return [f"face:{k}:lr{c}@{a}-{b}" for k in (1, 2) for (a, b) in _ranges(band) for c in ("", "c")]


def sill_pieces(band: int = BAND) -> List[str]:
    """문지방 9가지: 바닥이 있는 쪽 3 × 띠 범위 3."""
    return [f"sill:{s}:{a}-{b}" for s in SIDES for (a, b) in _ranges(band)]


# ---------------------------------------------------------------- 정의 읽기

def read_definitions(root: Path) -> List[dict]:
    path = root / DEFINITIONS
    try:
        entities = json.loads(path.read_text(encoding="utf-8-sig"))
    except OSError:
        raise RoomExportError("room-finish-defs", f"{DEFINITIONS}를 못 읽었다") from None
    except json.JSONDecodeError as e:
        raise RoomExportError("room-finish-defs", f"{DEFINITIONS}가 JSON이 아니다: {e}") from None
    if not isinstance(entities, list):
        raise RoomExportError("room-finish-defs", f"{DEFINITIONS}는 엔티티 배열이어야 한다")
    out, ids = [], set()
    for e in entities:
        if not isinstance(e, dict) or e.get("type") != TYPE:
            continue
        for field in ("id", "slot", "sheet", "art"):
            if not isinstance(e.get(field), str) or not e[field].strip():
                raise RoomExportError("room-finish-sheet" if field == "sheet" else f"room-finish-{field}",
                                      f"{e.get('id', '?')}: {field}가 비었다")
        if e["id"] in ids:
            raise RoomExportError("room-finish-duplicate", f"{e['id']}가 두 번 나온다")
        ids.add(e["id"])
        out.append(e)
    return out


# ---------------------------------------------------------------- 조각 그리기

def _check(defn: dict, kit: Kit) -> None:
    art, slot = defn["art"], defn["slot"]
    if slot == "wall":
        if art not in wall_styles():
            raise RoomExportError("room-finish-art", f"{defn['id']}: 벽 모양 {art!r}가 없다. 있는 것: {', '.join(wall_styles())}")
        if wall_band(art) != BAND:
            raise RoomExportError("room-finish-band", f"{defn['id']}: 벽 모양 {art}의 띠 두께 {wall_band(art)}px는 조각 시트가 받지 않는다({BAND}px만)")
    elif slot == "floor":
        if art not in floor_ids(kit):
            raise RoomExportError("room-finish-art", f"{defn['id']}: 바닥 {art!r}가 키트의 한 칸 타일 명세에 없다. 있는 것: {', '.join(floor_ids(kit))}")
    else:
        raise RoomExportError("room-finish-slot", f"{defn['id']}: 모르는 slot {slot!r}(wall·floor)")
    sheet = defn["sheet"]
    if not sheet.endswith(".png"):
        raise RoomExportError("room-finish-sheet", f"{defn['id']}: sheet {sheet!r}는 .png 경로여야 한다")


def _hex(rgba) -> str:
    return "#%02X%02X%02X" % tuple(rgba[:3])


def _tile(kit: Kit, key: str, wall: str, floor: dict) -> Canvas:
    return draw_tile(kit.style, key, wall, floor)


def _door_piece(kit: Kit) -> Canvas:
    """정면 문 타일에서 문이 칠하는 자리(DOOR_RECT)만. 문 밖은 벽지가 낼 것이라 비운다 — 벽지·바닥과 무관한 조각."""
    full = _tile(kit, "door:face:1", wall_styles()[0], {})
    out = Canvas(CELL, CELL)
    x0, y0, x1, y1 = DOOR_RECT
    for y in range(y0, y1):
        for x in range(x0, x1):
            out.px(x, y, full.get(x, y))
    return out


def _pieces_of(defn: dict, kit: Kit) -> Dict[str, Canvas]:
    art = defn["art"]
    if defn["slot"] == "wall":
        return {f"{art}/{k}": _tile(kit, k, art, {}) for k in face_pieces() + narrow_pieces()}
    spec = kit.recipes[art]
    wall = wall_styles()[0]
    return {f"{art}/floor": _tile(kit, "floor", wall, spec), f"{art}/floor:shade": _tile(kit, "floor:shade", wall, spec)}


def _common_pieces(kit: Kit) -> Dict[str, Canvas]:
    wall = wall_styles()[0]
    pieces = {f"{COMMON}/{k}": _tile(kit, k, wall, {}) for k in sill_pieces()}
    pieces[f"{COMMON}/door"] = _door_piece(kit)
    return pieces


def _bands(kit: Kit) -> Dict[str, Dict[str, str]]:
    return {top: {"o": _hex(kit.style.outline), "d": _hex(kit.style.color(f"{top}.0")),
                  "f": _hex(kit.style.color(f"{top}.1")), "l": _hex(kit.style.color(f"{top}.2"))} for top in sorted(wall_tops())}


def _sheet(pieces: Dict[str, Canvas], kit: Kit, sheet_path: str) -> Tuple[bytes, bytes]:
    names = sorted(pieces)
    rows = (len(names) + SHEET_WIDTH_CELLS - 1) // SHEET_WIDTH_CELLS
    img = Image.new("RGBA", (CELL * SHEET_WIDTH_CELLS, CELL * rows), (0, 0, 0, 0))
    sprites = {}
    for i, name in enumerate(names):
        x, y = i % SHEET_WIDTH_CELLS * CELL, i // SHEET_WIDTH_CELLS * CELL
        img.paste(pieces[name].to_image().convert("RGBA"), (x, y))
        sprites[name] = {"x": x, "y": y, "w": CELL, "h": CELL}
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=False)
    atlas = {"image": sheet_path, "width": img.width, "height": img.height, "pixelsPerUnit": PIXELS_PER_UNIT,
             "origin": "top-left", "band": BAND, "sprites": sprites, "bands": _bands(kit)}
    return buf.getvalue(), (json.dumps(atlas, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


# ---------------------------------------------------------------- 계획과 쓰기

def plan_finishes(repo_root, kit: Kit) -> ExportPlan:
    """정의 파일의 꾸밈마다 조각을 그려 시트별 PNG와 좌표표를 계획에 담는다(쓰지 않는다). 문제는 RoomExportError."""
    defs = read_definitions(Path(repo_root))
    by_sheet: Dict[str, List[dict]] = {}
    for d in defs:
        _check(d, kit)
        by_sheet.setdefault(d["sheet"], []).append(d)
    plan = ExportPlan()
    for sheet, group in by_sheet.items():
        pieces = _common_pieces(kit)
        done = set()
        for d in group:
            if d["art"] not in done:                       # 같은 그림을 두 꾸밈이 써도 조각은 한 벌
                done.add(d["art"])
                pieces.update(_pieces_of(d, kit))
        png, atlas = _sheet(pieces, kit, sheet)
        plan.files[sheet] = png
        plan.files[f"{ATLAS_ROOT}/{sheet}.atlas.json"] = atlas
    return plan


def export_finishes(repo_root, kit: Kit, dry_run: bool = False) -> List[str]:
    """꾸밈 조각 시트와 좌표표를 쓴다. 바뀐 파일(뿌리 기준 경로)을 돌려준다. dry_run이면 쓰지 않고 쓸 파일만."""
    plan = plan_finishes(repo_root, kit)
    if dry_run:
        return pending_writes(plan, repo_root)
    return apply(plan, repo_root)
