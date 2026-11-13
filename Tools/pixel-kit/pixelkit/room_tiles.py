"""방 만들기 — 칸마다 벽/바닥/밖만 정한 격자에서 둘레 칸을 보고 32x32 타일을 고르고 그린다.

- classify: 격자 → 칸마다 타일 열쇠(문자열). 열쇠의 꼴은 README "방 만들기 — 벽 타일".
- draw_tile: 열쇠 하나 → 32x32 타일. 벽 모양은 @wall_style 등록 표에서, 바닥은 키트의 바닥 타일 명세를 그대로 그린다.
- lint_room_tile: 타일 한 장 검사(팔레트, 색 한도, 투명, 정면·바닥의 가로 이음매).

벽의 옆으로 기운 면은 그리지 않는다. 정면은 남쪽을 보는 면뿐이고, 정면이 아닌 벽 칸은 모두 윗면이다.
타일은 스스로 가장자리 선을 그린다(렌더러가 외곽선을 입히지 않는다).
"""
from typing import Dict, List, Optional

from .canvas import Canvas
from .lint import Problem, _palette_and_count, _seam, _transparent
from .makers import RecipeError
from .recipes import Ctx, render
from .style import Style

OUT, WALL, FLOOR = 0, 1, 2
CELL = 32

# 윗면 둘레 비트. 대각은 양옆 두 변이 모두 윗면일 때만 남긴다(안쪽 모서리) → 47가지.
N, E, S, W, NE, SE, SW, NW = 1, 2, 4, 8, 16, 32, 64, 128
_DIRS = ((N, 0, -1), (E, 1, 0), (S, 0, 1), (W, -1, 0), (NE, 1, -1), (SE, 1, 1), (SW, -1, 1), (NW, -1, -1))
_DIAG_NEEDS = ((NE, N | E), (SE, S | E), (SW, S | W), (NW, N | W))

FACE_FLAGS = "lrct"   # l 왼쪽 끝, r 오른쪽 끝, c 위가 윗면(천장 밑 그늘), t 위가 열림(벽이 아님)


def normalize_mask(mask: int) -> int:
    for diag, need in _DIAG_NEEDS:
        if mask & diag and (mask & need) != need:
            mask &= ~diag
    return mask


# ---------------------------------------------------------------- 고르기

def _cell(grid, x: int, y: int) -> int:
    if 0 <= y < len(grid) and 0 <= x < len(grid[y]):
        return grid[y][x]
    return OUT


def _kinds(grid, face_height: int):
    """칸마다 ("cap",) / ("face", k) / ("floor",) / None."""
    out = []
    for y, row in enumerate(grid):
        line = []
        for x, v in enumerate(row):
            if v not in (OUT, WALL, FLOOR) or isinstance(v, bool):
                raise RecipeError(f"격자 ({x}, {y})의 값 {v!r}은 0(밖)·1(벽)·2(바닥)이 아니다")
            if v == FLOOR:
                line.append(("floor",))
            elif v == WALL:
                kind = ("cap",)
                for d in range(1, face_height + 1):
                    below = _cell(grid, x, y + d)
                    if below == FLOOR:
                        kind = ("face", d)
                        break
                    if below != WALL:
                        break
                line.append(kind)
            else:
                line.append(None)
        out.append(line)
    return out


def classify(grid: List[List[int]], face_height: int = 2) -> List[List[Optional[str]]]:
    """격자(grid[y][x], 값 OUT/WALL/FLOOR) → 칸마다 타일 열쇠. 격자 밖은 OUT으로 본다.

    벽 칸: 아래로 face_height칸 안에 바닥이 나오고 그 사이가 모두 벽이면 정면 "face:<k>[:<flags>]"
    (k는 바닥에서 위로 몇 번째, 1이 바닥 바로 위), 아니면 윗면 "cap:<mask>". 바닥: "floor" 또는 바로 위가 정면이면
    "floor:shade". 밖: None."""
    if not isinstance(face_height, int) or face_height < 1:
        raise RecipeError(f"face_height는 1 이상의 정수여야 한다(지금 {face_height!r})")
    kinds = _kinds(grid, face_height)

    def kind(x, y):
        if 0 <= y < len(kinds) and 0 <= x < len(kinds[y]):
            return kinds[y][x]
        return None

    def is_cap(x, y):
        return kind(x, y) == ("cap",)

    keys: List[List[Optional[str]]] = []
    for y, line in enumerate(kinds):
        row: List[Optional[str]] = []
        for x, k in enumerate(line):
            if k is None:
                row.append(None)
            elif k[0] == "floor":
                above = kind(x, y - 1)
                row.append("floor:shade" if above is not None and above[0] == "face" else "floor")
            elif k[0] == "cap":
                mask = sum(bit for bit, dx, dy in _DIRS if is_cap(x + dx, y + dy))
                row.append(f"cap:{normalize_mask(mask)}")
            else:
                flags = ""
                for flag, dx in (("l", -1), ("r", 1)):
                    side = kind(x + dx, y)
                    if side != k and side != ("cap",):      # 같은 높이 정면이면 이어지고, 윗면이면 그쪽이 선을 친다
                        flags += flag
                above = kind(x, y - 1)
                if above == ("cap",):
                    flags += "c"
                elif above is None or above[0] != "face":
                    flags += "t"
                row.append(f"face:{k[1]}" + (f":{flags}" if flags else ""))
        keys.append(row)
    return keys


# ---------------------------------------------------------------- 벽 모양 등록

_WALLS: Dict[str, object] = {}


def wall_style(name: str):
    """벽 모양 클래스를 name으로 등록한다. 클래스는 cap(윗면 팔레트 줄, 단계 0 안쪽 어두운 줄 / 1 면 / 2 안쪽 밝은 줄)과
    face(style, k, ceiling) -> Canvas(32x32, 끝·위 선 없이)를 갖는다. k == 1이 바닥 바로 위 타일."""
    def deco(cls):
        if name in _WALLS:
            raise RecipeError(f"{name}: 벽 모양 이름이 겹친다")
        _WALLS[name] = cls()
        return cls
    return deco


def wall_styles() -> List[str]:
    return sorted(_WALLS)


def _look(wall: str):
    try:
        return _WALLS[wall]
    except KeyError:
        raise RecipeError(f"{wall}: 등록되지 않은 벽 모양. 있는 것: {', '.join(wall_styles())}") from None


def _ramp(style: Style, prefix: str, n: int) -> list:
    try:
        return [style.color(f"{prefix}.{i}") for i in range(n)]
    except KeyError as e:
        raise RecipeError(f"벽 모양: {e.args[0]}") from None


# 정면 아래 타일(k=1)의 줄 자리. 위 타일의 벽지가 이 타일 위 PAPER_TOP줄까지 이어진다.
PAPER_TOP = 4          # 벽지 0..3
MOLDING = (4, 5, 6)    # 허리 몰딩(빛, 면, 밑 그늘)
WAINSCOT = (7, 25)     # 징두리 판 [7, 25)
BASEBOARD = (25, 31)   # 걸레받이 [25, 31), 31은 바닥과 만나는 외곽선


@wall_style("paper")
class PaperWall:
    """줄무늬 벽지 + 나무 징두리 판 + 걸레받이."""
    cap = "wall.top.cream"
    paper = "wall.cream"     # 0 천장 밑 그늘, 1 줄무늬, 2 바탕
    wood = "wood.archive"    # 0..4

    def face(self, style: Style, k: int, ceiling: bool) -> Canvas:
        p = _ramp(style, self.paper, 3)
        cv = Canvas(CELL, CELL)
        cv.rect(0, 0, CELL, CELL, p[2])
        for x in range(0, CELL, 8):
            cv.rect(x + 2, 0, 3, CELL, p[1])        # 줄무늬(주기 8)
        if ceiling:
            cv.rect(0, 0, CELL, 2, p[0])
        if k > 1:
            return cv
        w = _ramp(style, self.wood, 5)
        cv.hline(0, MOLDING[0], CELL, w[4])
        cv.hline(0, MOLDING[1], CELL, w[3])
        cv.hline(0, MOLDING[2], CELL, w[1])
        cv.rect(0, WAINSCOT[0], CELL, WAINSCOT[1] - WAINSCOT[0], w[2])
        for x in (0, 16):                            # 판 둘(주기 16): 위·왼쪽 안쪽 그늘, 아래 빛
            top, h = WAINSCOT[0] + 3, WAINSCOT[1] - WAINSCOT[0] - 6
            cv.rect(x + 2, top, 12, h, w[3])
            cv.hline(x + 2, top, 12, w[1])
            cv.vline(x + 2, top, h, w[1])
            cv.hline(x + 2, top + h - 1, 12, w[4])
        cv.hline(0, BASEBOARD[0], CELL, w[3])        # 걸레받이 윗면
        cv.rect(0, BASEBOARD[0] + 1, CELL, BASEBOARD[1] - BASEBOARD[0] - 1, w[1])
        cv.hline(0, CELL - 1, CELL, style.outline)    # 바닥과 만나는 줄
        return cv


@wall_style("plaster")
class PlasterWall:
    """단색 회반죽 + 아래 한 단계 + 낮은 걸레받이."""
    cap = "wall.plaster.top"
    paper = "wall.plaster"   # 0 천장 밑 그늘, 1 아래쪽 한 단계, 2 넓은 면
    wood = "wood.school"

    def face(self, style: Style, k: int, ceiling: bool) -> Canvas:
        p = _ramp(style, self.paper, 3)
        cv = Canvas(CELL, CELL)
        cv.rect(0, 0, CELL, CELL, p[2])
        if ceiling:
            cv.rect(0, 0, CELL, 2, p[0])
        if k > 1:
            return cv
        w = _ramp(style, self.wood, 5)
        cv.rect(0, 20, CELL, 3, p[1])
        cv.hline(0, 23, CELL, w[4])                   # 걸레받이 윗면
        cv.rect(0, 24, CELL, 6, w[2])
        cv.hline(0, 30, CELL, w[1])
        cv.hline(0, CELL - 1, CELL, style.outline)
        return cv


# ---------------------------------------------------------------- 그리기

def _ascii_number(text: str) -> bool:
    """0-9만으로 된 수인가(isdigit은 '²'를 통과시켜 int()가 ValueError를 낸다)."""
    return text.isascii() and text.isdecimal()


def _parse_face(key: str):
    parts = key.split(":")
    if len(parts) not in (2, 3) or not _ascii_number(parts[1]) or int(parts[1]) < 1:
        return None
    flags = parts[2] if len(parts) == 3 else ""
    if len(parts) == 3 and not flags:
        return None
    order = [FACE_FLAGS.index(f) for f in flags if f in FACE_FLAGS]
    if len(order) != len(flags) or order != sorted(set(order)) or ("c" in flags and "t" in flags):
        return None
    return int(parts[1]), flags


def _cap(style: Style, look, mask: int) -> Canvas:
    """윗면 한 장. 둘레 3x3 칸 중 윗면인 칸을 픽셀 지도로 펴고, 윗면이 아닌 픽셀에 8방향으로 닿는 픽셀은 외곽선,
    그 안쪽 한 줄은 면 음영(먼 쪽(북쪽) 가장자리는 빛, 나머지 가장자리는 그늘)으로 칠한다. 이웃 타일과 같은 규칙이라
    모서리·T자·끝에서 선이 끊기지 않는다."""
    dark, face, light = _ramp(style, look.cap, 3)
    ol = style.outline
    filled = {(0, 0)} | {(dx, dy) for bit, dx, dy in _DIRS if mask & bit}

    def solid(px: int, py: int) -> bool:
        return (px // CELL, py // CELL) in filled      # 둘레 칸은 -1..1, 내림 나눗셈이라 음수도 맞다

    def touches(px: int, py: int, r: int):
        """반경 r(체스판 거리) 안의 윗면이 아닌 픽셀이 위쪽에만 있으면 {"n"}, 다른 쪽에도 있으면 "other"를 담는다."""
        sides = set()
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if (dx or dy) and not solid(px + dx, py + dy):
                    sides.add("n" if dy < 0 else "other")
        return sides

    cv = Canvas(CELL, CELL)
    for py in range(CELL):
        for px in range(CELL):
            if touches(px, py, 1):
                cv.px(px, py, ol)
                continue
            near = touches(px, py, 2)
            if not near:
                cv.px(px, py, face)
            elif near == {"n"}:
                cv.px(px, py, light)                  # 먼 가장자리 안쪽 빛
            else:
                cv.px(px, py, dark)                   # 정면 쪽·옆 가장자리 안쪽 그늘
    return cv


def _floor(style: Style, floor_spec: dict, shade: bool) -> Canvas:
    cv = render(floor_spec, Ctx(style, {}))
    if (cv.w, cv.h) != (CELL, CELL):
        raise RecipeError(f"{floor_spec.get('id', '?')}: 방 바닥 타일은 {CELL}x{CELL}이어야 한다(지금 {cv.w}x{cv.h})")
    if shade:
        # 정면 밑 짧은 그림자 2줄: 색마다 그 팔레트 줄의 가장 어두운 단계로 옮긴다
        # (색을 곱하지 않으므로 새 색이 생기지 않는다). 8은 줄의 단계 수보다 큰 상한(같은 hex가 여러 줄에 있어도 끝난다).
        for y in (0, 1):
            for x in range(CELL):
                c = cv.get(x, y)
                for _ in range(8):
                    d = style.darker(c)
                    if d is None:
                        break
                    c = d
                cv.px(x, y, c)
    return cv


def draw_tile(style: Style, key: str, wall: str, floor_spec: dict) -> Canvas:
    """열쇠 하나 → 32x32 타일. wall은 등록된 벽 모양 이름, floor_spec은 키트의 바닥 타일 명세(32x32)."""
    look = _look(wall)
    if key == "floor":
        return _floor(style, floor_spec, False)
    if key == "floor:shade":
        return _floor(style, floor_spec, True)
    if isinstance(key, str) and key.startswith("cap:"):
        v = key[4:]
        if _ascii_number(v) and int(v) < 256 and normalize_mask(int(v)) == int(v):
            return _cap(style, look, int(v))
    elif isinstance(key, str) and key.startswith("face:"):
        parsed = _parse_face(key)
        if parsed is not None:
            k, flags = parsed
            cv = look.face(style, k, "c" in flags)
            ol = style.outline
            if "t" in flags:
                cv.hline(0, 0, CELL, ol)
            if "l" in flags:
                cv.vline(0, 0, CELL, ol)
            if "r" in flags:
                cv.vline(CELL - 1, 0, CELL, ol)
            return cv
    raise RecipeError(f"{key!r}: 모르는 방 타일 열쇠(floor, floor:shade, cap:<mask>, face:<k>[:<{FACE_FLAGS}>])")


def lint_room_tile(canvas: Canvas, key: str, style: Style) -> List[Problem]:
    """타일 한 장: 크기, 팔레트 밖 색, 색 한도(타일), 투명 픽셀, 정면·바닥은 가로로 이어 붙였을 때 이음매."""
    out: List[Problem] = []
    if (canvas.w, canvas.h) != (CELL, CELL):
        out.append(Problem("room-tile-size", f"방 타일은 {CELL}x{CELL}이어야 한다(지금 {canvas.w}x{canvas.h})"))
    out += _palette_and_count(canvas, {"kind": "tile"}, Ctx(style, {}))
    out += _transparent(canvas)
    if key.startswith("face:") or key.startswith("floor"):
        out += _seam(canvas, axes=((True, "가로"),))
    return out
