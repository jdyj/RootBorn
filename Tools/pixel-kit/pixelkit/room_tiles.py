"""방 만들기 — 칸마다 벽/바닥/밖만 정한 격자에서 둘레 칸을 보고 32x32 타일을 고르고 그린다.

- classify: 격자 → 칸의 종류(바닥 "floor"/"floor:shade", 정면 "face:<k>[:<표시>]", 윗면 "cap", 밖 None). 평면 검사가 쓴다.
- plan_tiles: 격자 → 칸마다 Wall 층 타일 열쇠와 Ground 층 타일 열쇠(RoomTiles). 열쇠의 꼴은 README "방 만들기 — 벽 타일".
- draw_tile: 열쇠 하나 → 32x32 타일. 벽 모양은 @wall_style 등록 표에서, 바닥은 키트의 바닥 타일 명세를 그대로 그린다.
- lint_room_tile: 타일 한 장 검사(팔레트, 색 한도, 투명, 이음매).

벽 윗면은 칸을 꽉 채우지 않고 두께 band(벽 모양의 값, 기본 16px)의 띠만 그린다(사용자 결정 2026-11-14, 견본 A).
띠·정면이 아닌 자리는 벽 타일에서 투명하다. 그 자리가 바닥 쪽이면 같은 칸의 Ground 층에 바닥 타일이 깔려 비치고,
맵 밖 쪽이면 아무것도 없다. 바닥을 벽 타일에 굽지 않으므로 벽 타일 열쇠는 바닥 종류와 무관하다.
충돌은 칸 단위 그대로다(벽 칸은 띠 옆의 바닥처럼 보이는 자리까지 통째로 막힌다).

벽의 옆으로 기운 면은 그리지 않는다. 정면은 남쪽을 보는 면뿐이고, 정면이 아닌 벽 칸은 모두 윗면이다.
타일은 스스로 가장자리 선을 그린다(렌더러가 외곽선을 입히지 않는다).
"""
import re
from dataclasses import dataclass
from typing import Dict, FrozenSet, List, Optional, Tuple

from .canvas import Canvas
from .lint import Problem, _palette_and_count, _seam, _transparent
from .makers import RecipeError
from .recipes import Ctx, render
from .style import Style

OUT, WALL, FLOOR = 0, 1, 2
CELL = 32
PAD = 2               # 띠를 칠할 때 보는 반경(외곽선 1, 안쪽 빛·그늘 2). 열쇠는 칸 둘레 이만큼 이웃 칸의 띠를 담는다.
MIN_BAND = 4          # 띠 두께 하한(외곽선 둘 + 면). 상한은 칸 크기 미만

FACE_FLAGS = "lrct"   # l 왼쪽 끝, r 오른쪽 끝, c 위가 윗면(천장 밑 그늘), t 위가 열림(벽이 아님)
CAP = "cap"


# ---------------------------------------------------------------- 칸의 종류

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
    """격자(grid[y][x], 값 OUT/WALL/FLOOR) → 칸의 종류. 격자 밖은 OUT으로 본다.

    벽 칸: 아래로 face_height칸 안에 바닥이 나오고 그 사이가 모두 벽이면 정면 "face:<k>[:<flags>]"
    (k는 바닥에서 위로 몇 번째, 1이 바닥 바로 위), 아니면 윗면 "cap". 바닥: "floor" 또는 바로 위가 정면이면
    "floor:shade". 밖: None. 그릴 타일 열쇠(윗면의 띠 모양 등)는 plan_tiles가 낸다."""
    _check_face_height(face_height)
    kinds = _kinds(grid, face_height)

    def kind(x, y):
        if 0 <= y < len(kinds) and 0 <= x < len(kinds[y]):
            return kinds[y][x]
        return None

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
                row.append(CAP)
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


def _check_face_height(face_height) -> None:
    if not isinstance(face_height, int) or isinstance(face_height, bool) or face_height < 1:
        raise RecipeError(f"face_height는 1 이상의 정수여야 한다(지금 {face_height!r})")


# ---------------------------------------------------------------- 벽 모양 등록

_WALLS: Dict[str, object] = {}


def wall_style(name: str):
    """벽 모양 클래스를 name으로 등록한다. 클래스는
    - cap: 윗면 띠의 팔레트 줄(단계 0 안쪽 어두운 줄 / 1 면 / 2 안쪽 밝은 줄). 띠 색을 바꾸는 곳은 여기 한 곳이다.
    - band: 윗면 띠 두께(px). 방의 자리 표에 남는다.
    - paper·wood: 정면 벽지·나무의 팔레트 줄(좁은 정면도 이 줄로 그린다).
    - face(style, k, ceiling) -> Canvas(32x32, 끝·위 선 없이). k == 1이 바닥 바로 위 타일."""
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


def wall_band(wall: str) -> int:
    """벽 모양의 윗면 띠 두께. 모르는 벽 모양은 RecipeError."""
    return _look(wall).band


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
NARROW_WOOD = 19       # 좁은 정면 아래 타일: 이 줄이 나무 윗면 빛, 그 아래가 나무 면(무늬 없음)


@wall_style("paper")
class PaperWall:
    """줄무늬 벽지 + 나무 징두리 판 + 걸레받이."""
    cap = "wall.top.cream"
    band = 16
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
    band = 16
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


# ---------------------------------------------------------------- 띠 자리(견본 A)

def _strip_lr(face_key: str) -> str:
    """정면 열쇠에서 끝 표시(l, r)를 뺀 것 — 옆으로 늘여 그릴 때 쓴다."""
    parts = face_key.split(":")
    flags = parts[2].replace("l", "").replace("r", "") if len(parts) == 3 else ""
    return f"face:{parts[1]}" + (f":{flags}" if flags else "")


def _band_layout(kind, caps, T: int):
    """윗면 칸마다 (띠 사각형 목록, 가운데 사각형)과 가로 자리 X.

    X(가로 자리): 양옆이 방 쪽(바닥·정면)이면 가운데, 한쪽만 방이면 그쪽에 붙는다. Y(세로 자리): 아래가 정면이면 아래,
    위만 바닥이면 위. 한쪽만 방인 칸이 양쪽이 방인 벽에 이어지면 그 벽의 자리를 따르고(T자에서 삐져나오지 않게),
    정해지지 않은 축은 그 축을 따라 이어진 윗면에서 받는다(그래도 없으면 가운데). 띠 = 가운데 사각형 + 이웃 윗면 쪽 팔.
    두 칸이 나누는 변의 팔 범위는 "양쪽 방"으로 정해진 쪽(더 강한 쪽)의 것을 쓴다(경계에서 어긋나지 않게)."""
    def room_side(x, y):
        return kind(x, y) in ("floor", "face")

    centre = ((CELL - T) // 2, (CELL - T) // 2 + T)
    lo, hi = (0, T), (CELL - T, CELL)

    def axis(a, b):
        if a and b:
            return centre, 2
        if a:
            return lo, 1
        if b:
            return hi, 1
        return None, 0

    X, Y, RX, RY = {}, {}, {}, {}
    for x, y in caps:
        X[x, y], RX[x, y] = axis(room_side(x - 1, y), room_side(x + 1, y))
        if kind(x, y + 1) == "face":            # 정면 위 윗면은 정면 쪽(아래)에 붙는다
            Y[x, y], RY[x, y] = hi, 2
        elif room_side(x, y - 1):               # 위가 방이고 아래가 밖·윗면이면 위(방 쪽)에 붙는다
            Y[x, y], RY[x, y] = lo, 1
        else:
            Y[x, y], RY[x, y] = None, 0
    for x, y in caps:
        if RX[x, y] == 1:
            for n in ((x, y - 1), (x, y + 1)):
                if n in RX and RX[n] == 2:
                    X[x, y] = X[n]
        if RY[x, y] == 1:
            for n in ((x - 1, y), (x + 1, y)):
                if n in RY and RY[n] == 2:
                    Y[x, y] = Y[n]
    for _ in range(4):
        for x, y in caps:
            if X[x, y] is None:
                cand = [(RX[n], X[n]) for n in ((x, y - 1), (x, y + 1)) if n in X and X[n] is not None]
                if cand:
                    X[x, y] = max(cand)[1]
            if Y[x, y] is None:
                cand = [(RY[n], Y[n]) for n in ((x - 1, y), (x + 1, y)) if n in Y and Y[n] is not None]
                if cand:
                    Y[x, y] = max(cand)[1]
    for c in caps:
        X[c] = X[c] or centre
        Y[c] = Y[c] or centre

    def edge_x(a, b):
        return X[a] if RX[a] >= RX[b] else X[b]

    def edge_y(a, b):
        return Y[a] if RY[a] >= RY[b] else Y[b]

    rects = {}
    for x, y in caps:
        cx, cy = X[x, y], Y[x, y]
        rs = [(cx[0], cy[0], cx[1], cy[1])]
        if (x, y - 1) in X:
            ex = edge_x((x, y - 1), (x, y))
            rs.append((ex[0], 0, ex[1], cy[1]))
        if (x, y + 1) in X:
            ex = edge_x((x, y), (x, y + 1))
            rs.append((ex[0], cy[0], ex[1], CELL))
        if (x - 1, y) in X:
            ey = edge_y((x - 1, y), (x, y))
            rs.append((0, ey[0], cx[1], ey[1]))
        if (x + 1, y) in X:
            ey = edge_y((x, y), (x + 1, y))
            rs.append((cx[0], ey[0], CELL, ey[1]))
        rects[x, y] = (rs, (cx[0], cy[0], cx[1], cy[1]))
    return rects, X


# ---------------------------------------------------------------- 열쇠의 사각형 글

_INT = re.compile(r"-?[0-9]+\Z")
_RANGE = re.compile(r"([0-9]+)-([0-9]+)\Z")


def _encode(pixels, lo: int, hi: int) -> str:
    """픽셀 집합 → 정규형 사각형 글 "x0,y0,x1,y1+…"(끝 제외). 같은 가로 구간들이 이어지는 줄을 한 띠로 묶는다."""
    rows: Dict[int, List[int]] = {}
    for x, y in pixels:
        rows.setdefault(y, []).append(x)

    def runs(xs):
        out, start, prev = [], None, None
        for x in sorted(xs):
            if start is None:
                start = prev = x
            elif x == prev + 1:
                prev = x
            else:
                out.append((start, prev + 1))
                start = prev = x
        if start is not None:
            out.append((start, prev + 1))
        return tuple(out)

    parts, prev, top = [], (), lo
    for y in range(lo, hi + 1):
        cur = runs(rows.get(y, ())) if y < hi else ()
        if cur != prev:
            parts += [f"{a},{top},{b},{y}" for a, b in prev]
            prev, top = cur, y
    return "+".join(parts)


def _decode(text: str, lo: int, hi: int, what: str) -> FrozenSet[Tuple[int, int]]:
    pixels = set()
    for part in text.split("+"):
        nums = part.split(",")
        if len(nums) != 4 or not all(_INT.match(n) for n in nums):
            raise RecipeError(f"{what} 사각형 {part!r}는 x0,y0,x1,y1(정수)이어야 한다")
        a, b, c, d = map(int, nums)
        if not (lo <= a < c <= hi and lo <= b < d <= hi):
            raise RecipeError(f"{what} 사각형 {part!r}가 [{lo}, {hi}) 밖이거나 비었다")
        pixels |= {(x, y) for y in range(b, d) for x in range(a, c)}
    if _encode(pixels, lo, hi) != text:
        raise RecipeError(f"{what} {text!r}는 정규형이 아니다(같은 모양은 {_encode(pixels, lo, hi)!r})")
    return frozenset(pixels)


def _near(inside, pixel) -> bool:
    px, py = pixel
    return any((px + dx, py + dy) in inside for dy in range(-PAD, PAD + 1) for dx in range(-PAD, PAD + 1))


_WIN = CELL + 2 * PAD
_RING1 = [dy * _WIN + dx for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dx or dy]
_RING2_UP = [dy * _WIN + dx for dy in range(-PAD, 0) for dx in range(-PAD, PAD + 1)]            # 위쪽("n")
_RING2_LOW = [dy * _WIN + dx for dy in range(0, PAD + 1) for dx in range(-PAD, PAD + 1) if dx or dy]   # 나머지


def _shades(band, solid) -> Dict[Tuple[int, int], str]:
    """띠 픽셀마다 칠: "o" 외곽선(띠가 아닌 픽셀에 8방향으로 닿음), "l" 안쪽 빛(반경 2 안의 띠 아닌 픽셀이 위쪽에만),
    "d" 안쪽 그늘(다른 쪽에도), "f" 면. solid는 이웃 칸까지 띠인 픽셀."""
    grid = bytearray(_WIN * _WIN)                # 칸 둘레 PAD까지의 창(띠 픽셀의 이웃은 모두 이 안에 든다)
    for x, y in solid:
        if -PAD <= x < CELL + PAD and -PAD <= y < CELL + PAD:
            grid[(y + PAD) * _WIN + x + PAD] = 1
    out = {}
    for px, py in band:
        i = (py + PAD) * _WIN + px + PAD
        if not all(grid[i + o] for o in _RING1):
            out[px, py] = "o"
        elif all(grid[i + o] for o in _RING2_LOW):
            out[px, py] = "f" if all(grid[i + o] for o in _RING2_UP) else "l"
        else:
            out[px, py] = "d"
    return out


def _rim(band):
    """칸 가장자리에서 PAD 안쪽의 띠 픽셀 — 칸 밖 이웃 픽셀을 보는 것은 이것들뿐이다."""
    return {(x, y) for x, y in band if min(x, y, CELL - 1 - x, CELL - 1 - y) < PAD}


def _forced(shades) -> set:
    """칠이 그대로이려면 띠여야 하는 픽셀: 외곽선이 아닌 픽셀의 8방향 이웃, 면의 반경 2, 빛의 반경 2 중 위쪽이 아닌 것.
    열쇠에는 칸 밖 픽셀 중 이것만 적는다 — 그래서 그림이 같으면 열쇠가 같다(나머지 이웃은 칠을 바꾸지 않는다)."""
    out = set()
    for (px, py), shade in shades.items():
        if shade == "o":
            continue
        r = 1 if shade == "d" else PAD
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if shade == "l" and dy < 0 and max(abs(dx), abs(dy)) > 1:
                    continue
                out.add((px + dx, py + dy))
    return out


# ---------------------------------------------------------------- 열쇠 읽기

@dataclass(frozen=True)
class TileLayout:
    """띠·늘인 정면·좁은 정면 열쇠를 읽은 것. 칸 안 좌표(0..31), padded는 띠를 칠할 때 보는 이웃 칸 픽셀까지."""
    band: FrozenSet[Tuple[int, int]]
    padded: FrozenSet[Tuple[int, int]]
    fills: Dict[str, FrozenSet[Tuple[int, int]]]
    narrow: Optional[Tuple[int, int]]
    narrow_face: Optional[Tuple[int, str]]


def _ascii_number(text: str) -> bool:
    """0-9만으로 된 수인가(isdigit은 '²'를 통과시켜 int()가 ValueError를 낸다)."""
    return text.isascii() and text.isdecimal()


def _parse_face(key: str):
    parts = key.split(":")
    if len(parts) not in (2, 3) or not _ascii_number(parts[1]) or int(parts[1]) < 1 or str(int(parts[1])) != parts[1]:
        return None
    flags = parts[2] if len(parts) == 3 else ""
    if len(parts) == 3 and not flags:
        return None
    order = [FACE_FLAGS.index(f) for f in flags if f in FACE_FLAGS]
    if len(order) != len(flags) or order != sorted(set(order)) or ("c" in flags and "t" in flags):
        return None
    return int(parts[1]), flags


def _parse_floor(key: str):
    """바닥 열쇠 → 그늘을 넣을 칸 안 x 범위(없으면 None). 틀린 열쇠는 RecipeError."""
    if key == "floor":
        return None
    if key == "floor:shade":
        return (0, CELL)
    m = _RANGE.match(key[len("floor:shade:"):]) if key.startswith("floor:shade:") else None
    if m and all(str(int(g)) == g for g in m.groups()):
        a, b = int(m.group(1)), int(m.group(2))
        if 0 <= a < b <= CELL and (a, b) != (0, CELL):
            return (a, b)
    raise RecipeError(f"{key!r}: 바닥 열쇠는 floor, floor:shade, floor:shade:<x0>-<x1>(0 ≤ x0 < x1 ≤ {CELL}, 칸 전체가 아님)")


def tile_layout(key: str) -> Optional[TileLayout]:
    """띠(cap:…)·좁은 정면(face:…@…) 열쇠를 읽는다. 바닥과 꽉 찬 정면 열쇠는 None. 틀린 열쇠는 RecipeError."""
    if not isinstance(key, str):
        raise RecipeError(f"{key!r}: 방 타일 열쇠는 글이어야 한다")
    if key.startswith("floor"):
        _parse_floor(key)
        return None
    segs = key.split(";")
    head = segs[0]
    band = padded = frozenset()
    narrow = narrow_face = None
    if head.startswith("cap:"):
        padded = _decode(head[4:], -PAD, CELL + PAD, "띠")
        band = frozenset((x, y) for x, y in padded if 0 <= x < CELL and 0 <= y < CELL)
        if not band:
            raise RecipeError(f"{key!r}: 칸 안에 띠가 없다")
        stray = sorted(padded - band - _forced(_shades(_rim(band), padded)))
        if stray:
            raise RecipeError(f"{key!r}: 칠을 바꾸지 않는 이웃 칸 픽셀 {stray[0]}을 적었다(정규형이 아니다)")
    elif head.startswith("face:") and "@" in head:
        face_key, rng = head.split("@", 1)
        parsed = _parse_face(face_key)
        m = _RANGE.match(rng)
        if parsed is None or "l" not in parsed[1] or "r" not in parsed[1] or "t" in parsed[1]:
            raise RecipeError(f"{key!r}: 좁은 정면은 face:<k>:lr[c]@<x0>-<x1>(양쪽 끝 표시, 위가 열리지 않음)")
        if not m or any(str(int(g)) != g for g in m.groups()) or not 0 <= int(m.group(1)) < int(m.group(2)) <= CELL:
            raise RecipeError(f"{key!r}: 좁은 정면 범위 {rng!r}는 0 ≤ x0 < x1 ≤ {CELL}")
        narrow, narrow_face = (int(m.group(1)), int(m.group(2))), parsed
    else:
        if len(segs) == 1 and head.startswith("face:") and _parse_face(head) is not None:
            return None
        raise RecipeError(f"{key!r}: 모르는 방 타일 열쇠(floor, floor:shade[:<x0>-<x1>], face:<k>[:<{FACE_FLAGS}>], "
                          f"face:<k>:lr[c]@<x0>-<x1>[;…], cap:<띠>[;…])")
    fills: Dict[str, FrozenSet[Tuple[int, int]]] = {}
    taken = set(band)
    if narrow is not None:
        taken |= {(x, y) for y in range(CELL) for x in range(*narrow)}
    for seg in segs[1:]:
        face_key, _, rects = seg.partition("@")
        parsed = _parse_face(face_key)
        if parsed is None or "l" in parsed[1] or "r" in parsed[1] or not rects:
            raise RecipeError(f"{key!r}: 늘인 정면 {seg!r}는 face:<k>[:c|t]@<사각형>이어야 한다(끝 표시 없음)")
        if fills and face_key <= list(fills)[-1]:
            raise RecipeError(f"{key!r}: 늘인 정면은 열쇠 순서로 한 번씩(정규형)")
        pix = _decode(rects, 0, CELL, "늘인 정면")
        if pix & taken:
            raise RecipeError(f"{key!r}: 늘인 정면 {face_key}가 띠나 다른 정면과 겹친다")
        taken |= pix
        fills[face_key] = pix
    return TileLayout(band, padded, fills, narrow, narrow_face)


# ---------------------------------------------------------------- 고르기

@dataclass(frozen=True)
class RoomTiles:
    """칸마다 Wall 층 열쇠(벽 칸)와 Ground 층 열쇠(바닥 칸, 그리고 띠 옆으로 바닥이 비치는 벽 칸). 없으면 None."""
    wall: List[List[Optional[str]]]
    ground: List[List[Optional[str]]]


def plan_tiles(grid: List[List[int]], face_height: int = 2, band: int = 16) -> RoomTiles:
    """격자 → 칸마다 Wall·Ground 타일 열쇠. band는 윗면 띠 두께(px)."""
    if type(band) is not int or not MIN_BAND <= band < CELL:
        raise RecipeError(f"띠 두께 band는 {MIN_BAND} 이상 {CELL} 미만의 정수여야 한다(지금 {band!r})")
    keys = classify(grid, face_height)
    h = len(keys)
    w = max((len(r) for r in keys), default=0)

    def kind(x, y):
        if 0 <= y < h and 0 <= x < len(keys[y]):
            k = keys[y][x]
            if k is None:
                return "out"
            return k.split(":")[0]
        return "out"

    caps = [(x, y) for y in range(h) for x in range(len(keys[y])) if keys[y][x] == CAP]
    rects, X = _band_layout(kind, caps, band)

    narrow: Dict[Tuple[int, int], Tuple[int, int]] = {}
    for y in range(h):
        for x, k in enumerate(keys[y]):
            if k and k.startswith("face:") and "l" in k.split(":")[-1] and "r" in k.split(":")[-1] and k.count(":") == 2:
                yy = y - 1
                while kind(x, yy) == "face":
                    yy -= 1
                if kind(x, yy) == "cap":
                    narrow[x, y] = X[x, yy]

    def side(nx, ny):
        k = kind(nx, ny)
        if k == "floor":
            return "F"
        if k == "face":
            return _strip_lr(keys[ny][nx])
        return "V"

    # 칸마다 픽셀 종류: "B" 띠, "N" 좁은 정면, "F" 비침(바닥 쪽), "V" 비어 있음(밖 쪽), 정면 열쇠(늘인 정면).
    cells: Dict[Tuple[int, int], list] = {}
    for x, y in caps:
        rs, core = rects[x, y]
        # 둘레 8칸: 윗면이면 None, 아니면 그쪽에서 오는 픽셀 종류
        around = {(dx, dy): (None if kind(x + dx, y + dy) == "cap" else side(x + dx, y + dy))
                  for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dx or dy}
        cls = [None] * (CELL * CELL)
        for a, b, c, d in rs:
            for py in range(b, d):
                cls[py * CELL + a:py * CELL + c] = ["B"] * (c - a)
        for py in range(CELL):
            for px in range(CELL):
                if cls[py * CELL + px] is not None:
                    continue
                # 그 픽셀이 든 쪽의 두 변 중 윗면이 아닌 이웃(띠 옆 픽셀은 옆 이웃 먼저), 둘 다 윗면이면 대각 이웃.
                hx = -1 if px < core[0] else (1 if px >= core[2] else (-1 if px < CELL // 2 else 1))
                hy = -1 if py < core[1] else (1 if py >= core[3] else (-1 if py < CELL // 2 else 1))
                if px < core[0] or px >= core[2]:
                    cands = ((hx, 0), (0, hy))
                elif py < core[1] or py >= core[3]:
                    cands = ((0, hy), (hx, 0))
                else:
                    dxe = px + 1 if hx < 0 else CELL - px
                    dye = py + 1 if hy < 0 else CELL - py
                    cands = ((hx, 0), (0, hy)) if (dxe, (x + hx, y)) <= (dye, (x, y + hy)) else ((0, hy), (hx, 0))
                res = "B"                       # 둘레가 모두 윗면(두꺼운 덩어리 안쪽)이면 띠로 메운다
                for n in cands + ((hx, hy),):
                    if around[n] is not None:
                        res = around[n]
                        break
                cls[py * CELL + px] = res
        if "F" in cls and "V" in cls:            # 바닥이 비치는 칸의 밖 쪽 자리는 띠로 메운다(바닥이 방 밖으로 새지 않게)
            cls = ["B" if c == "V" else c for c in cls]
        cells[x, y] = cls
    for (x, y), (x0, x1) in narrow.items():
        cls = []
        for py in range(CELL):
            for px in range(CELL):
                cls.append("N" if x0 <= px < x1 else side(x - 1 if px < x0 else x + 1, y))
        cells[x, y] = cls

    pw, ph = w * CELL, h * CELL
    band_map = bytearray(pw * ph)
    for (x, y), cls in cells.items():
        for i, c in enumerate(cls):
            if c == "B":
                band_map[(y * CELL + i // CELL) * pw + x * CELL + i % CELL] = 1

    def floor_key(x, y):
        if kind(x, y - 1) != "face":
            return "floor"
        if (x, y - 1) in narrow:
            a, b = narrow[x, y - 1]
            return f"floor:shade:{a}-{b}"
        return "floor:shade"

    wall: List[List[Optional[str]]] = [[None] * len(r) for r in keys]
    ground: List[List[Optional[str]]] = [[None] * len(r) for r in keys]
    for y in range(h):
        for x, k in enumerate(keys[y]):
            if k is None:
                continue
            if k.startswith("floor"):
                ground[y][x] = floor_key(x, y)
                continue
            if (x, y) not in cells:
                wall[y][x] = k                   # 꽉 찬 정면
                continue
            cls = cells[x, y]
            fills: Dict[str, set] = {}
            for i, c in enumerate(cls):
                if c.startswith("face:"):
                    fills.setdefault(c, set()).add((i % CELL, i // CELL))
            tail = "".join(f";{fk}@{_encode(fills[fk], 0, CELL)}" for fk in sorted(fills))
            if (x, y) in narrow:
                a, b = narrow[x, y]
                wall[y][x] = f"{k}@{a}-{b}{tail}"
            else:
                inside = {(i % CELL, i // CELL) for i, c in enumerate(cls) if c == "B"}
                pad = set()
                for py in range(-PAD, CELL + PAD):
                    gy = y * CELL + py
                    if not 0 <= gy < ph:
                        continue
                    for px in range(-PAD, CELL + PAD):
                        if 0 <= px < CELL and 0 <= py < CELL:
                            continue
                        gx = x * CELL + px
                        if 0 <= gx < pw and band_map[gy * pw + gx] and _near(inside, (px, py)):
                            pad.add((px, py))
                pad &= _forced(_shades(_rim(inside), inside | pad))
                wall[y][x] = f"cap:{_encode(inside | pad, -PAD, CELL + PAD)}{tail}"
            if "F" in cls:
                # 벽 칸 밑 바닥: 위 칸 정면의 그림자는 이 칸에서 바닥이 비치는 맨 위 두 줄의 열에만 의미가 있다
                shown = {i % CELL for i in range(2 * CELL) if cls[i] == "F"}
                key = floor_key(x, y)
                if key != "floor":
                    a, b = narrow.get((x, y - 1), (0, CELL))
                    cols = sorted(shown & set(range(a, b)))
                    key = "floor" if not cols else ("floor:shade" if (cols[0], cols[-1] + 1) == (0, CELL)
                                                     else f"floor:shade:{cols[0]}-{cols[-1] + 1}")
                ground[y][x] = key
    return RoomTiles(wall, ground)


# ---------------------------------------------------------------- 그리기

def _face_tile(style: Style, look, k: int, flags: str) -> Canvas:
    cv = look.face(style, k, "c" in flags)
    ol = style.outline
    if "t" in flags:
        cv.hline(0, 0, CELL, ol)
    if "l" in flags:
        cv.vline(0, 0, CELL, ol)
    if "r" in flags:
        cv.vline(CELL - 1, 0, CELL, ol)
    return cv


def _narrow_face(style: Style, look, cv: Canvas, k: int, flags: str, x0: int, x1: int) -> None:
    """좁은 정면(세로 벽 남쪽 끝, 띠 폭): 무늬 없는 단순한 면. 위 타일은 벽지 한 면, 아래 타일은 벽지 + 나무 두 면.
    양옆 열과 아래 타일의 맨 아랫줄은 외곽선."""
    p = _ramp(style, look.paper, 3)
    w = _ramp(style, look.wood, 5)
    for y in range(CELL):
        color = p[2]
        if k == 1 and y == NARROW_WOOD:
            color = w[4]
        elif k == 1 and y > NARROW_WOOD:
            color = w[2]
        if "c" in flags and y < 2:
            color = p[0]
        for x in range(x0, x1):
            edge = x in (x0, x1 - 1) or (k == 1 and y == CELL - 1)
            cv.px(x, y, style.outline if edge else color)


def _draw_band(style: Style, look, cv: Canvas, layout: TileLayout) -> None:
    """띠의 픽셀 중 띠가 아닌 픽셀에 8방향으로 닿는 것은 외곽선, 그 안쪽 한 줄은 면 음영(먼 쪽(북쪽) 가장자리만 빛,
    나머지 가장자리는 그늘). 이웃 칸의 띠(padded)까지 보고 칠하므로 칸 경계에서 선이 끊기거나 두 겹이 되지 않는다."""
    dark, face, light = _ramp(style, look.cap, 3)
    colors = {"o": style.outline, "d": dark, "f": face, "l": light}
    for (px, py), shade in _shades(layout.band, layout.padded).items():
        cv.px(px, py, colors[shade])


def _floor(style: Style, floor_spec: dict, shade: Optional[Tuple[int, int]]) -> Canvas:
    cv = render(floor_spec, Ctx(style, {}))
    if (cv.w, cv.h) != (CELL, CELL):
        raise RecipeError(f"{floor_spec.get('id', '?')}: 방 바닥 타일은 {CELL}x{CELL}이어야 한다(지금 {cv.w}x{cv.h})")
    if shade is not None:
        # 정면 밑 짧은 그림자 2줄(정면이 있는 x 범위만): 색마다 그 팔레트 줄의 가장 어두운 단계로 옮긴다
        # (색을 곱하지 않으므로 새 색이 생기지 않는다). 8은 줄의 단계 수보다 큰 상한(같은 hex가 여러 줄에 있어도 끝난다).
        for y in (0, 1):
            for x in range(*shade):
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
    if isinstance(key, str) and key.startswith("floor"):
        return _floor(style, floor_spec, _parse_floor(key))
    layout = tile_layout(key)
    if layout is None:
        k, flags = _parse_face(key)
        return _face_tile(style, look, k, flags)
    cv = Canvas(CELL, CELL)
    for face_key, pixels in layout.fills.items():
        src = _face_tile(style, look, *_parse_face(face_key))
        for x, y in pixels:
            cv.px(x, y, src.get(x, y))
    if layout.narrow is not None:
        _narrow_face(style, look, cv, layout.narrow_face[0], layout.narrow_face[1], *layout.narrow)
    _draw_band(style, look, cv, layout)
    return cv


# ---------------------------------------------------------------- 검사

def _band_seam(canvas: Canvas, layout: TileLayout, style: Style) -> List[Problem]:
    """띠가 칸 경계를 넘어 이어지는 자리(8방향 이웃이 모두 띠)의 가장자리 픽셀에 외곽선이 있으면 이음매가 끊긴 것."""
    for x, y in sorted(layout.band):
        if x not in (0, CELL - 1) and y not in (0, CELL - 1):
            continue
        if all((x + dx, y + dy) in layout.padded for dy in (-1, 0, 1) for dx in (-1, 0, 1)) and canvas.get(x, y) == style.outline:
            return [Problem("tile-seam", f"띠가 이어지는 변의 ({x}, {y})에 외곽선이 그어졌다", x, y)]
    return []


def lint_room_tile(canvas: Canvas, key: str, style: Style) -> List[Problem]:
    """타일 한 장: 크기, 팔레트 밖 색, 색 한도(타일), 투명.

    바닥·꽉 찬 정면은 투명 픽셀이 없어야 하고 가로로 이어 붙였을 때 이음매가 없어야 한다. 띠·좁은 정면 타일은 열쇠가 정한
    띠·정면 자리만 불투명하고 나머지는 투명해야 한다(바닥은 Ground 층이 비친다 — 굽지 않는다). 완전히 투명한 타일은 안 된다.
    띠 타일의 이음매는 띠가 칸 경계를 넘어 이어지는 변에서만 본다."""
    out: List[Problem] = []
    if (canvas.w, canvas.h) != (CELL, CELL):
        return [Problem("room-tile-size", f"방 타일은 {CELL}x{CELL}이어야 한다(지금 {canvas.w}x{canvas.h})")]
    out += _palette_and_count(canvas, {"kind": "tile"}, Ctx(style, {}))
    layout = tile_layout(key)
    if layout is None:
        out += _transparent(canvas)
        out += _seam(canvas, axes=((True, "가로"),))
        return out
    want = set(layout.band)
    for pixels in layout.fills.values():
        want |= pixels
    if layout.narrow is not None:
        want |= {(x, y) for y in range(CELL) for x in range(*layout.narrow)}
    holes = [(x, y) for x, y in sorted(want, key=lambda p: (p[1], p[0])) if not canvas.get(x, y)[3]]
    extra = [(x, y) for y in range(CELL) for x in range(CELL) if (x, y) not in want and canvas.get(x, y)[3]]
    if holes:
        out.append(Problem("tile-transparent", f"띠·정면 자리에 투명한 픽셀 {len(holes)}개", *holes[0]))
    if extra:
        out.append(Problem("room-tile-leftover", f"띠·정면이 아닌 자리에 칠한 픽셀 {len(extra)}개(그 자리는 투명 — 바닥은 Ground 층이 비친다)", *extra[0]))
    if not any(canvas.get(x, y)[3] for y in range(CELL) for x in range(CELL)):
        out.append(Problem("room-tile-empty", "완전히 투명한 벽 타일"))
    out += _band_seam(canvas, layout, style)
    return out
