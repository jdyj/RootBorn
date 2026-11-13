"""건물: 가게 정면 + 위에서 내려다보이는 큰 지붕(설계서 6.4·6.5절, 사용자가 승인한 판 v6).

옆면(옆벽, 지붕 옆 비탈, 차양 옆면)은 그리지 않는다(사용자 결정). 입체감은 정면에 얹은 것과 지붕 면적으로 낸다.
그늘과 그림자는 색을 곱하지 않고 그 자리 재질의 어두운 단계다(_Pal.down). 지붕 종류와 지붕 위 물건은 등록 표
(ROOFS, ROOF_ITEMS)에서 고른다. 자리는 spec["_layout"]에 적는다(테스트가 읽는다). 좌표는 이 그림(외곽선 전)
기준이고 사각형은 [x, y, w, h]다.
"""
from dataclasses import dataclass
from typing import Callable, Dict, List, Tuple

from ..canvas import Canvas
from . import RecipeError, register
from .furniture import _col, _local, _merge, _part, _report, _rid, pile

ROOF_H = 104          # 지붕 줄 수(그림 높이 186의 절반 넘게)
WALL_H = 82           # 벽 윗줄부터 그림 맨 아랫줄까지(앞으로 나온 창 받침 포함)
WALL_BODY = 76        # 벽 윗줄 ~ 벽이 땅에 닿는 줄
OUT = 6               # 진열창이 벽보다 앞으로(화면 아래로) 나온 만큼
DOOR_W = 44
SIGN_W, SIGN_H = 48, 22
LAMP_DROP = 30        # 벽 윗줄에서 벽등까지(차양 밑 그늘보다 아래, 문 머리 높이)
FRONT_EDGE = 16       # 지붕 맨 아랫줄 위로 몇 줄: 평지붕 앞 난간 윗줄이자 간판 윗줄(간판이 처마 위로 솟는 만큼)
AWNING_SLOPE, AWNING_VALANCE = 12, 6
SHADE_SIDE, SHADE_BELOW = 3, 2   # 지붕 위 물건 그림자: 오른쪽 변 3열, 아랫변 2줄의 짧은 띠(사용자: 굴뚝 그림자가 너무 크다)


def _rnd(k: int, salt: int, n: int) -> int:
    """결정적인 작은 난수(씨앗 k, 소금 salt) → 0..n-1."""
    v = (k * 2654435761 + salt * 40503 + 12345) & 0xFFFFFFFF
    v ^= v >> 13
    v = (v * 2246822519) & 0xFFFFFFFF
    v ^= v >> 16
    return v % max(1, n)


def _ramp(spec: dict, ctx, prefix: str, need: int) -> list:
    out = []
    while True:
        try:
            out.append(ctx.style.color(f"{prefix}.{len(out)}"))
        except KeyError:
            break
    if len(out) < need:
        raise RecipeError(f"{_rid(spec)}: 팔레트 줄 {prefix}는 단계가 {need}개 이상이어야 한다(지금 {len(out)}개)")
    return out


def _steps(ramps) -> dict:
    """색 → (줄, 단계). 같은 색이 여러 줄에 있으면 먼저 준 줄."""
    where = {}
    for r in ramps:
        for i, c in enumerate(r):
            where.setdefault(c, (r, i))
    return where


def _shift(cv: Canvas, x: int, y: int, w: int, h: int, where: dict, n: int) -> None:
    """사각형 안의 색을 자기 줄에서 n단계 어둡게(n<0이면 밝게). 줄 밖의 색(외곽선 등)은 그대로."""
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            hit = where.get(cv.get(xx, yy))
            if hit:
                r, i = hit
                cv.px(xx, yy, r[min(len(r) - 1, max(0, i - n))])


def _bevel(cv: Canvas, x: int, y: int, w: int, h: int, lit, dark) -> None:
    """틀의 두께: 위·왼쪽 밝게, 아래·오른쪽 어둡게."""
    cv.hline(x, y, w, lit)
    cv.vline(x, y, h, lit)
    cv.hline(x, y + h - 1, w, dark)
    cv.vline(x + w - 1, y, h, dark)


class _Pal:
    """한 건물이 쓰는 색 줄과 '그 자리 재질의 어두운 단계' 그늘."""

    def __init__(self, spec: dict, ctx, roof: str, wall: str, door: str):
        self.roof = _ramp(spec, ctx, roof, 5)
        self.wall = _ramp(spec, ctx, wall, 4)
        self.door = _ramp(spec, ctx, door, 4)
        self.trim = _ramp(spec, ctx, "trim", 4)
        self.wood = _ramp(spec, ctx, "wood.warm", 5)
        self.glass = _ramp(spec, ctx, "glass.window", 3)
        self.plinth = _ramp(spec, ctx, "plinth", 2)
        self.metal = _ramp(spec, ctx, "metal", 3)
        self.bread = _ramp(spec, ctx, "bread", 4)
        self.yellow = _col(spec, ctx, "yellow.1")
        self._where = _steps([self.roof, self.wall, self.door, self.trim, self.wood, self.glass,
                              self.plinth, self.metal, self.bread])
        self._wall_only = _steps([self.wall])

    def down(self, cv: Canvas, x: int, y: int, w: int, h: int, n: int = 1) -> None:
        _shift(cv, x, y, w, h, self._where, n)

    def glow(self, cv: Canvas, cx: int, cy: int, r2: int) -> None:
        """벽등 둘레의 벽만 한 단계 밝게(원 안)."""
        for yy in range(cy - 8, cy + 9):
            for xx in range(cx - 9, cx + 10):
                if (xx - cx) ** 2 + (yy - cy) ** 2 <= r2:
                    _shift(cv, xx, yy, 1, 1, self._wall_only, -1)


# ---------- 지붕 위 물건 ----------

@dataclass(frozen=True)
class RoofItem:
    w: int                                                # 차지하는 폭·높이(자기 그림자 포함)
    h: int
    front: Tuple[int, int]                                # 앞면 줄(차지하는 자리 윗줄에서 몇 줄 아래, 줄 수). 그림자 검사가 읽는다
    draw: Callable[[Canvas, _Pal, int, int], None]        # (그림, 색, 차지하는 자리의 왼쪽 위 x, y)


ROOF_ITEMS: Dict[str, RoofItem] = {}


def roof_item(name: str, w: int, h: int, front: Tuple[int, int]):
    def deco(fn):
        if name in ROOF_ITEMS:
            raise RecipeError(f"{name}: 지붕 위 물건 이름이 겹친다")
        ROOF_ITEMS[name] = RoofItem(w, h, front, fn)
        return fn
    return deco


def _box(cv: Canvas, P: _Pal, x: int, y: int, w: int, top_h: int, front_h: int, top, front) -> None:
    """높이가 있는 상자: 윗면 + 앞면, 그림자는 오른쪽 변과 아랫변에 붙은 짧은 띠(물체가 끝나는 곳에서 시작).
    차지하는 자리는 (w + SHADE_SIDE) x (top_h + front_h + SHADE_BELOW).
    top = (밝은, 몸, 어두운), front = (밝은, 몸, 어두운, 윗면 밑 짙은)."""
    hh = top_h + front_h
    P.down(cv, x + w, y + 3, SHADE_SIDE, hh - 3)           # 오른쪽 띠
    P.down(cv, x + 3, y + hh, w + SHADE_SIDE - 3, SHADE_BELOW)   # 아랫변 띠
    cv.rect(x, y, w, top_h, top[1])
    _bevel(cv, x, y, w, top_h, top[0], top[2])
    cv.rect(x, y + top_h, w, front_h, front[1])
    cv.hline(x, y + top_h, w, front[3])                    # 윗면 모서리 밑
    cv.hline(x, y + top_h + 1, w, front[2])
    cv.vline(x, y + top_h + 2, front_h - 2, front[0])
    cv.vline(x + w - 1, y + top_h + 2, front_h - 2, front[2])


def _trim_top(P: _Pal):
    return (P.trim[3], P.trim[2], P.trim[1])


def _wall_front(P: _Pal):
    return (P.wall[3], P.wall[2], P.wall[1], P.wall[0])


@roof_item("roof.stair-house", 37, 34, (12, 20))
def stair_house(cv: Canvas, P: _Pal, x: int, y: int) -> None:
    """옥탑(계단실): 윗면 + 벽돌 앞면 + 문."""
    _box(cv, P, x, y, 34, 12, 20, _trim_top(P), _wall_front(P))
    d = P.door
    cv.rect(x + 12, y + 18, 11, 14, d[2])
    _bevel(cv, x + 12, y + 18, 11, 14, d[3], d[1])
    cv.px(x + 20, y + 25, P.yellow)


@roof_item("roof.ac-unit", 23, 20, (9, 9))
def ac_unit(cv: Canvas, P: _Pal, x: int, y: int) -> None:
    """실외기: 금속 윗면의 팬 + 앞면 살."""
    m = P.metal
    _box(cv, P, x, y, 20, 9, 9, (m[2], m[2], m[1]), (m[2], m[1], m[0], m[0]))
    cv.rect(x + 5, y + 2, 10, 5, m[1])
    cv.rect(x + 7, y + 3, 6, 3, m[0])
    for xx in range(x + 3, x + 18, 3):
        cv.vline(xx, y + 12, 5, m[0])


@roof_item("roof.skylight", 41, 30, (22, 6))
def skylight(cv: Canvas, P: _Pal, x: int, y: int) -> None:
    """솟은 천창: 금속 틀 윗면의 유리 + 앞면."""
    m, g = P.metal, P.glass
    _box(cv, P, x, y, 38, 22, 6, (P.trim[3], m[2], m[0]), (m[1], m[1], m[0], m[0]))
    cv.rect(x + 3, y + 3, 32, 16, g[1])
    _glass_streaks(cv, P, x + 3, y + 3, 32, 16)
    cv.vline(x + 18, y + 3, 16, m[2])
    cv.hline(x + 3, y + 10, 32, m[2])


@roof_item("roof.chimney", 19, 32, (8, 22))
def chimney(cv: Canvas, P: _Pal, x: int, y: int) -> None:
    """굴뚝: 윗면의 구멍 + 벽돌 앞면(줄눈)."""
    _box(cv, P, x, y, 16, 8, 22, _trim_top(P), _wall_front(P))
    cv.rect(x + 3, y + 2, 10, 4, P.wall[0])
    for yy in range(y + 13, y + 30, 5):
        cv.hline(x + 1, yy, 14, P.wall[1])


@roof_item("roof.dormer", 49, 42, (16, 22))
def dormer(cv: Canvas, P: _Pal, x: int, y: int) -> None:
    """박공 지붕창: 세모 박공(지붕 끝 두 줄) + 벽 + 창 + 창턱. 처마가 양옆으로 3px 나온다."""
    s, t, wd, g = P.roof, P.trim, P.wood, P.glass
    dx, dy, dw, half = x + 3, y + 2, 40, 20
    P.down(cv, dx + dw + 3, dy + 12, SHADE_SIDE, 5)        # 오른쪽 띠: 처마 줄은 처마 끝 바로 옆부터
    P.down(cv, dx + dw, dy + 17, SHADE_SIDE, 21)           # 벽 줄은 벽이 끝나는 열 바로 옆부터(붙은 그림자)
    P.down(cv, dx + 3, dy + 38, dw, SHADE_BELOW)           # 아랫변 띠
    for j in range(half):                                  # 세모 박공
        xl, xr = dx + half - 1 - j, dx + half + j
        cv.rect(xl, dy + j * 14 // half, xr - xl + 1, 14 - j * 14 // half, t[2])
    for j in range(half + 3):                              # 지붕 끝: 왼쪽 밝게, 오른쪽 어둡게
        yy = dy - 2 + j * 14 // half
        for k in range(4):
            cv.px(dx + half - 1 - j, yy + k, s[3] if k < 3 else s[1])
            cv.px(dx + half + j, yy + k, s[1] if k < 3 else s[0])
    cv.rect(dx, dy + 14, dw, 22, t[2])
    cv.vline(dx, dy + 14, 22, t[3])
    cv.vline(dx + dw - 1, dy + 14, 22, t[1])
    P.down(cv, dx + 1, dy + 14, dw - 2, 2)                 # 박공 밑 그늘
    cv.rect(dx + 9, dy + 12, 22, 20, wd[1])
    _bevel(cv, dx + 9, dy + 12, 22, 20, wd[3], wd[0])
    cv.rect(dx + 12, dy + 15, 16, 14, g[1])
    _glass_streaks(cv, P, dx + 12, dy + 15, 16, 14)
    cv.vline(dx + 19, dy + 15, 14, wd[1])
    cv.hline(dx + 12, dy + 21, 16, wd[1])
    P.down(cv, dx + 12, dy + 15, 16, 2)
    cv.rect(dx - 1, dy + 36, dw + 2, 2, t[1])
    cv.hline(dx - 1, dy + 37, dw + 2, P.plinth[0])


def _glass_streaks(cv: Canvas, P: _Pal, x: int, y: int, w: int, h: int, period: int = 22) -> None:
    """유리 몸 색 위의 사선 반사 줄(/ 방향, 밝은 단계)."""
    for j in range(h):
        for i in range(w):
            if (i + j) % period in (5, 6, 7, 10) and cv.get(x + i, y + j) == P.glass[1]:
                cv.px(x + i, y + j, P.glass[2])


# ---------- 지붕 ----------

ROOFS: Dict[str, Callable[[Canvas, dict, object, int, int], None]] = {}


def roof(name: str, palette: str):
    """지붕 종류 등록. 지붕 함수는 0..rh-1 줄을 그리고 spec["_layout"]["roof_area"](물건을 놓을 수 있는 곳)를 적는다."""
    def deco(fn):
        if name in ROOFS:
            raise RecipeError(f"{name}: 지붕 종류 이름이 겹친다")
        fn.palette = palette
        ROOFS[name] = fn
        return fn
    return deco


@roof("flat", "roof.teal")
def roof_flat(cv: Canvas, spec: dict, ctx, w: int, rh: int) -> None:
    """평지붕: 갓돌(난간 윗면), 뒤 난간의 안쪽 벽, 뒤가 어둡고 앞이 밝은 바닥, 네 모서리 기둥, 앞 난간 윗면 + 앞면."""
    t = _ramp(spec, ctx, "roof.teal", 5)
    side, back, in_h = 8, 7, 9
    fx, fy = side, back + in_h
    fw, fh = w - 2 * side, rh - FRONT_EDGE - fy
    ty = fy + fh                                           # 앞 난간 윗줄
    cv.rect(0, 0, w, rh, t[3])
    cv.rect(fx, back, fw, in_h, t[1])                      # 뒤 난간 안쪽 벽(우리 쪽을 보는 면)
    cv.hline(fx, back, fw, t[0])                           # 갓돌 밑 그늘
    for x in range(fx + 15, fx + fw, 16):
        cv.vline(x, back + 2, in_h - 2, t[0])
    stains = set()
    for k in range(7):                                     # 바닥 얼룩(짧은 줄)
        sx, sy, n = 8 + _rnd(k, 1, fw - 26), 10 + _rnd(k, 2, fh - 18), 5 + _rnd(k, 3, 6)
        stains.update((sx + i, sy) for i in range(n))
    for j in range(fh):                                    # 바닥: 띠 두 개(뒤 어둡게, 앞 밝게) + 줄눈 + 난간 그림자
        band = 1 if j < fh * 2 // 5 else 2
        back_sh = 2 if j < 3 else (1 if j < 6 else 0)      # 뒤 난간이 드리운 그림자(위)
        for i in range(fw):
            joint = (i + 1) % 26 == 0 or (j + 1) % 20 == 0 or (i, j) in stains
            idx = band - joint - max(back_sh, 1 if i < 4 else 0)   # 왼 난간 그림자는 한 띠
            cv.px(fx + i, fy + j, t[max(0, idx)])
    cv.rect(0, 0, w, 2, t[4])                              # 갓돌: 바깥 위·왼쪽은 밝게, 안쪽 모서리는 어둡게
    cv.rect(0, 0, 2, rh - 6, t[4])
    cv.hline(fx, back - 1, fw, t[2])
    cv.vline(fx - 1, back - 1, fh + in_h + 1, t[2])
    cv.vline(fx + fw, back, fh + in_h, t[4])
    cv.rect(w - 3, 2, 2, rh - 8, t[2])
    for y in range(21, rh - 20, 20):
        cv.hline(1, y, side - 2, t[2])
        cv.hline(w - side, y, side - 2, t[2])
    for x in range(21, w - 12, 20):
        cv.vline(x, 1, back - 2, t[2])
    cv.rect(0, ty, w, 8, t[3])                             # 앞 난간 윗면
    cv.hline(fx, ty, fw, t[4])
    cv.hline(0, ty + 7, w, t[2])
    for x in range(11, w - 6, 20):
        cv.vline(x, ty + 1, 6, t[2])
    cv.rect(0, ty + 8, w, rh - ty - 8, t[1])               # 앞 난간 앞면
    cv.hline(0, ty + 8, w, t[0])
    cv.hline(0, rh - 1, w, t[0])
    for x in range(11, w - 6, 20):
        cv.vline(x, ty + 9, rh - ty - 10, t[0])
    for cx, cy in ((0, 0), (w - 10, 0), (0, ty - 1), (w - 10, ty - 1)):   # 모서리 기둥
        cv.rect(cx, cy, 10, 10, t[4])
        cv.hline(cx, cy + 9, 10, t[2])
        cv.vline(cx + 9, cy, 10, t[2])
    spec["_layout"]["roof_area"] = [fx, fy, fw, fh]


ROW_HEIGHTS = (5, 6, 6, 7, 7, 8, 8, 9, 9)                 # 앞 비탈 기와 줄: 아래로 갈수록 크다


@roof("pitched", "roof.slate")
def roof_pitched(cv: Canvas, spec: dict, ctx, w: int, rh: int) -> None:
    """기와지붕: 어두운 좁은 뒤 비탈, 솟은 용마루와 그 그늘, 겹쳐 얹힌 기와(아래로 갈수록 크다), 박공널, 처마."""
    s = _ramp(spec, ctx, "roof.slate", 5)
    where = _steps([s])
    ridge_y, ridge_h = 23, 6
    for y in range(ridge_y):                               # 뒤 비탈(넘어가는 면)
        row, ry = divmod(y, 4)
        for x in range(w):
            rx = (x + (5 if row % 2 else 0)) % 10
            cv.px(x, y, s[0] if (ry == 0 or rx == 9) else s[1])
    y = ridge_y + ridge_h
    for row, rowh in enumerate(ROW_HEIGHTS):
        tw = 10 + row // 2 * 2                             # 기와 폭도 아래로 갈수록 넓다
        for ry in range(rowh):
            for x in range(w):
                rx = (x + (tw // 2 if row % 2 else 0)) % tw
                if ry == 0:
                    c = s[0]                               # 위 줄이 드리운 그늘
                elif ry == 1:
                    c = s[1]
                elif ry == rowh - 1:
                    c = s[3] if rx not in (0, tw - 1) else s[2]   # 밝은 끝단(둥근 모서리)
                elif rx == tw - 1:
                    c = s[1]
                elif rx == 0:
                    c = s[3]
                else:
                    c = s[2]
                cv.px(x, y + ry, c)
        y += rowh
    eave = y
    cv.rect(0, eave, w, rh - eave, s[1])                   # 처마 두께
    cv.hline(0, eave, w, s[4])
    cv.hline(0, eave + 1, w, s[2])
    cv.hline(0, rh - 1, w, s[0])
    cv.rect(0, ridge_y, w, ridge_h, s[3])                  # 솟은 용마루
    cv.rect(0, ridge_y, w, 2, s[4])
    cv.hline(0, ridge_y + ridge_h - 1, w, s[1])
    for x in range(7, w - 1, 12):
        cv.vline(x, ridge_y + 2, ridge_h - 3, s[2])
    _shift(cv, 0, ridge_y + ridge_h, w, 2, where, 2)       # 용마루 그늘
    _shift(cv, 0, ridge_y + ridge_h + 2, w, 2, where, 1)
    cv.rect(0, 0, 5, eave, s[3])                           # 박공널: 왼쪽 밝게, 오른쪽 어둡게
    cv.vline(0, 0, eave, s[4])
    cv.vline(4, 0, eave, s[1])
    _shift(cv, 5, ridge_y + ridge_h, 2, eave - ridge_y - ridge_h, where, 1)
    cv.rect(w - 5, 0, 5, eave, s[1])
    cv.vline(w - 6, 0, eave, s[0])
    spec["_layout"]["roof_area"] = [5, 0, w - 11, eave]


# ---------- 정면 ----------

def _facade(cv: Canvas, spec: dict, ctx, P: _Pal, w: int, rh: int, v: dict, local) -> dict:
    lay = {}
    b, t, wd, pl, g, d = P.wall, P.trim, P.wood, P.plinth, P.glass, P.door
    wt = rh
    base = wt + WALL_BODY                                  # 벽이 땅에 닿는 줄
    left, right = 3, w - 4
    ww = right - left + 1
    lay["wall"] = [left, right]

    cv.rect(left, wt, ww, base - wt, b[2])                 # 넓은 색 면
    for k in range(26):                                    # 벽돌은 낱장 몇 장만
        bx, by = left + 10 + _rnd(k, 5, ww - 28), wt + 8 + _rnd(k, 8, (base - wt - 22) // 5) * 5
        c = b[3] if k % 3 else b[1]
        cv.rect(bx, by, 9, 4, c)
        _shift(cv, bx, by + 3, 9, 1, _steps([b]), 1)
    for px0, body, lit, dark in ((left, b[3], b[3], b[1]), (right - 8, b[1], b[2], b[0])):   # 양 끝 벽기둥
        cv.rect(px0, wt + 5, 9, base - wt - 5, body)
        cv.vline(px0, wt + 5, base - wt - 5, lit)
        cv.vline(px0 + 8, wt + 5, base - wt - 5, dark)
    P.down(cv, left + 9, wt + 5, 2, base - wt - 5)         # 왼 벽기둥이 벽에 드리운 그늘
    cv.rect(left - 1, wt, ww + 2, 5, t[2])                 # 처마 밑 띠
    cv.hline(left - 1, wt, ww + 2, t[3])
    cv.hline(left - 1, wt + 4, ww + 2, t[1])
    P.down(cv, left, wt + 5, ww, 3, 2)
    P.down(cv, left, wt + 8, ww, 2, 1)
    cv.rect(left - 2, base - 12, ww + 4, 3, pl[1])         # 받침: 벽보다 앞으로 나와 윗면이 보인다
    cv.hline(left - 2, base - 12, ww + 4, wd[3])
    cv.rect(left - 2, base - 9, ww + 4, 9, pl[0])
    for x in range(left + 4, right, 14):
        cv.vline(x, base - 9, 9, wd[0])
    cv.hline(left - 2, base - 5, ww + 4, wd[0])
    cv.hline(left - 2, base - 1, ww + 4, wd[0])

    # --- 깊이 들어간 문 ---
    fx, fy = v["door_x"], wt + 9
    fw, fh = DOOR_W, base - fy
    cv.rect(fx, fy, fw, fh, wd[1])                         # 두꺼운 문틀
    _bevel(cv, fx, fy, fw, fh, wd[3], wd[0])
    cv.rect(fx, fy, fw, 3, wd[3])
    cv.hline(fx, fy, fw, wd[4])
    cv.rect(fx + 6, fy + 8, fw - 12, fh - 8, b[0])         # 파인 자리
    cv.rect(fx + fw - 9, fy + 8, 3, fh - 8, wd[2])         # 오른쪽 안벽은 빛
    ix, iy, iw = fx + 9, fy + 12, fw - 20
    ih = base - iy
    cv.rect(ix, iy, iw, ih, d[2])
    _bevel(cv, ix, iy, iw, ih, d[3], d[1])
    cv.rect(ix + 4, iy + 4, iw - 8, 20, g[1])
    _glass_streaks(cv, P, ix + 4, iy + 4, iw - 8, 20)
    _bevel(cv, ix + 3, iy + 3, iw - 6, 22, d[1], d[3])
    cv.rect(ix + 7, iy + 9, 10, 8, t[2])                   # 걸린 팻말(글자 없음)
    _bevel(cv, ix + 7, iy + 9, 10, 8, t[3], t[1])
    cv.hline(ix + 9, iy + 12, 6, b[2])
    cv.hline(ix + 9, iy + 14, 4, b[2])
    cv.vline(ix + 12, iy + 5, 4, wd[0])
    cv.rect(ix + 4, iy + 28, iw - 8, ih - 32, d[2])
    _bevel(cv, ix + 4, iy + 28, iw - 8, ih - 32, d[3], d[1])
    cv.rect(ix + iw - 5, iy + 24, 3, 4, P.yellow)
    cv.px(ix + iw - 5, iy + 24, t[3])
    P.down(cv, fx + 6, fy + 8, fw - 15, 4, 2)              # 문틀이 문에 드리운 깊은 그늘(위·왼쪽)
    P.down(cv, fx + 6, fy + 12, fw - 15, 3, 1)
    P.down(cv, fx + 6, fy + 15, 4, fh - 15, 1)
    st = P.metal                                           # 회색 돌 계단(색 한도 안에서 이미 쓰는 회색 줄): 윗면 밝게, 앞면 어둡게
    cv.rect(fx - 4, base - 3, fw + 8, 3, st[2])
    cv.rect(fx - 4, base, fw + 8, 4, st[1])
    cv.hline(fx - 4, base + 3, fw + 8, st[0])
    lay["door"] = [fx, fy, fw, fh]

    # --- 벽등 ---
    lamp = _part(spec, ctx, "prop.wall-lamp")
    lx, ly = v["lamp_x"], wt + LAMP_DROP
    P.glow(cv, lx + lamp.w // 2, ly + lamp.h // 2, 52)
    cv.paste(lamp, lx, ly)
    lay["lamp"] = [lx, ly, lamp.w, lamp.h]

    # --- 앞으로 꺾여 나온 진열창 ---
    bx, bw = v["window_x"], v["window_w"]
    by0 = wt + 14                                          # 몸통 윗줄(앞면)
    bb = base + OUT                                        # 몸통 밑줄(앞면)
    P.down(cv, bx + bw + 5, by0 - 2, 5, bb - by0 - 4)      # 몸통이 벽에 드리운 그림자(오른쪽 볼에 붙어서)
    for c in range(5):                                     # 양옆 볼: 왼쪽 밝게, 오른쪽 어둡게
        off = round(OUT * (c + 1) / 5)
        for xx, lit in ((bx - 5 + c, True), (bx + bw + 4 - c, False)):
            top, bot = by0 - OUT + off, base + off
            cv.rect(xx, top, 1, bot - top, t[2] if lit else t[0])
            cv.rect(xx, bot - 12, 1, 3, pl[1] if lit else pl[0])
            cv.rect(xx, bot - 9, 1, 9, pl[0] if lit else wd[0])
            cv.px(xx, top, t[3] if lit else t[0])
    cv.rect(bx, by0, bw, 6, t[2])                          # 윗띠
    cv.hline(bx, by0, bw, t[3])
    cv.hline(bx, by0 + 5, bw, t[1])
    wy0 = by0 + 6
    wh = bb - 15 - wy0
    cv.rect(bx, wy0, bw, wh, wd[1])
    _bevel(cv, bx, wy0, bw, wh, wd[3], wd[0])
    gx, gy, gw, gh = bx + 3, wy0 + 3, bw - 6, wh - 6
    cv.rect(gx, gy, gw, gh, g[1])
    _glass_streaks(cv, P, gx, gy, gw, gh)
    shelf = gy + gh
    cv.rect(gx, shelf - 6, gw, 6, wd[2])                   # 진열 판
    cv.rect(gx, shelf - 6, gw, 2, wd[4])
    half = gw // 2
    mx = gx + half - 1                                     # 창살 [mx, mx + 2)
    pile(cv, v["slots"][0], gx + 1, mx - 1, shelf - 4, local, top=gy + 4)
    pile(cv, v["slots"][1], mx + 2, gx + gw - 2, shelf - 4, local, top=gy + 4)
    P.down(cv, gx, gy, gw, 3)                              # 들어간 유리: 위·왼쪽 안쪽 그늘
    P.down(cv, gx, gy + 3, 2, gh - 3)
    cv.rect(mx, gy, 2, gh, wd[1])
    cv.vline(mx, gy, gh, wd[3])
    P.down(cv, mx + 2, gy, 1, gh)
    sy = bb - 15
    cv.rect(bx - 1, sy, bw + 2, 3, pl[1])                  # 창 받침 윗면
    cv.hline(bx - 1, sy, bw + 2, wd[3])
    cv.rect(bx, sy + 3, bw, 12, pl[0])
    for x in range(bx + 6, bx + bw, 14):
        cv.vline(x, sy + 3, 12, wd[0])
    cv.hline(bx, sy + 8, bw, wd[0])
    cv.hline(bx, bb - 1, bw, wd[0])
    lay["window"] = [bx - 5, by0 - OUT, bw + 10, bb - by0 + OUT]
    lay["glass"] = [gx, gy, gw, gh]
    lay["mullion"] = [mx, 2]

    # --- 차양: 창 몸통 위로 (옆면 없음) ---
    ax0, ax1, ay = bx - 7, bx + bw + 6, wt + 2
    slope, val = AWNING_SLOPE, AWNING_VALANCE
    P.down(cv, ax0 + 2, ay + slope + val, ax1 - ax0 - 3, 3, 2)
    P.down(cv, ax0 + 2, ay + slope + val + 3, ax1 - ax0 - 3, 2, 1)
    stripes = v["awning"]
    for x in range(ax0, ax1 + 1):
        i = x - ax0
        r = stripes[(i // 6) % 2]
        cv.vline(x, ay, slope, r[1])                       # 비탈
        cv.vline(x, ay + slope, val, r[0])                 # 드림(더 어둡게)
        if i % 6 in (1, 2, 3, 4):
            cv.px(x, ay + slope + val, r[0])
    cv.hline(ax0, ay, ax1 - ax0 + 1, t[3])
    cv.hline(ax0, ay + slope, ax1 - ax0 + 1, t[3])
    lay["awning"] = [ax0, ay, ax1 - ax0 + 1, slope + val + 1]

    # --- 간판: 처마 위로 솟는다 ---
    sx, sy0 = v["sign_x"], rh - FRONT_EDGE
    P.down(cv, sx + SIGN_W, sy0 + 3, 3, SIGN_H - 3)       # 오른쪽 변과 아랫변에 붙은 그림자
    P.down(cv, sx + 3, sy0 + SIGN_H, SIGN_W, 3)
    cv.rect(sx, sy0, SIGN_W, SIGN_H, wd[1])
    _bevel(cv, sx, sy0, SIGN_W, SIGN_H, wd[3], wd[0])
    ix0, iy0, iw0, ih0 = sx + 3, sy0 + 3, SIGN_W - 6, SIGN_H - 6
    cv.rect(ix0, iy0, iw0, ih0, t[2])
    cv.hline(ix0, iy0, iw0, t[1])
    cv.vline(ix0, iy0, ih0, t[1])
    icons = v["icons"]
    for k, (pid, p) in enumerate(icons):                   # 아이콘은 판을 고르게 나눈 자리의 가운데
        cx = ix0 + iw0 * (2 * k + 1) // (2 * len(icons))
        px0, py0 = cx - p.w // 2, iy0 + ih0 // 2 - p.h // 2
        if px0 < ix0 + 1 or px0 + p.w > ix0 + iw0 - 1 or py0 < iy0 + 1 or py0 + p.h > iy0 + ih0:
            _report(local.problems, "slot-clipped", f"간판 아이콘이 판보다 크다({pid} {p.w}x{p.h})")
        cv.paste(p, px0, py0)
    lay["sign"] = [sx, sy0, SIGN_W, SIGN_H]
    return lay


# ---------- 명세 읽기 ----------

def _int(v) -> bool:
    return isinstance(v, int) and not isinstance(v, bool)


def _need(spec: dict, ok: bool, what: str) -> None:
    if not ok:
        raise RecipeError(f"{_rid(spec)}: {what}")


def _values(spec: dict, ctx) -> dict:
    get = lambda k: spec.get(k) if isinstance(spec.get(k), dict) else {}   # noqa: E731
    wall, door, win, aw, sign, lamp = (get(k) for k in ("wall", "door", "window", "awning", "sign", "lamp"))
    _need(spec, isinstance(wall.get("material"), str), "wall는 {material: 팔레트 줄}이어야 한다")
    _need(spec, _int(door.get("at_x")) and isinstance(door.get("color"), str),
          "door는 {at_x: px, color: 팔레트 줄}이어야 한다")
    slots = win.get("slots")
    _need(spec, _int(win.get("at_x")) and _int(win.get("width")) and isinstance(slots, list) and len(slots) == 2
          and all(isinstance(s, list) and all(isinstance(i, str) for i in s) for s in slots),
          "window는 {at_x, width(px), slots: [[부품 id], [부품 id]]}이어야 한다(창살 양쪽)")
    for s in slots:
        for i in s:
            _part(spec, ctx, i)
    colors = aw.get("colors")
    _need(spec, isinstance(colors, list) and len(colors) == 2 and all(isinstance(c, str) for c in colors),
          "awning은 {colors: [팔레트 줄 2개]}이어야 한다")
    icons = sign.get("icons")
    _need(spec, _int(sign.get("at_x")) and isinstance(icons, list) and icons and all(isinstance(i, str) for i in icons),
          "sign은 {at_x: px, icons: [부품 id]}이어야 한다")
    _need(spec, _int(lamp.get("at_x")), "lamp는 {at_x: px}이어야 한다")
    return {"wall": wall["material"], "door": door["color"], "door_x": door["at_x"],
            "window_x": win["at_x"], "window_w": win["width"], "slots": slots,
            "awning": [_ramp(spec, ctx, c, 2) for c in colors],
            "sign_x": sign["at_x"], "icons": [(i, _part(spec, ctx, i)) for i in icons], "lamp_x": lamp["at_x"]}


def _roof_spec(spec: dict) -> Tuple[str, List[Tuple[str, int, int]]]:
    r = spec.get("roof")
    _need(spec, isinstance(r, dict), "roof는 {type, items}이어야 한다")
    kind = r.get("type")
    if kind not in ROOFS:
        raise RecipeError(f"{_rid(spec)}: 없는 지붕 종류 {kind}. 있는 것: {', '.join(sorted(ROOFS))}")
    items = r.get("items", [])
    _need(spec, isinstance(items, list), "roof.items는 [{part, at: [px x, y]}] 목록이어야 한다")
    out = []
    for it in items:
        _need(spec, isinstance(it, dict) and isinstance(it.get("at"), list) and len(it["at"]) == 2
              and all(_int(n) for n in it["at"]), f"roof.items의 줄마다 {{part, at: [px x, y]}}: {it}")
        if it.get("part") not in ROOF_ITEMS:
            raise RecipeError(f"{_rid(spec)}: 없는 지붕 위 물건 {it.get('part')}. 있는 것: {', '.join(sorted(ROOF_ITEMS))}")
        out.append((it["part"], it["at"][0], it["at"][1]))
    return kind, out


def _overlaps(a, b) -> bool:
    return a[0] < b[0] + b[2] and b[0] < a[0] + a[2] and a[1] < b[1] + b[3] and b[1] < a[1] + a[3]


def _inside(a, area) -> bool:
    return area[0] <= a[0] and area[1] <= a[1] and a[0] + a[2] <= area[0] + area[2] and a[1] + a[3] <= area[1] + area[3]


@register("building.shop")
def shop(spec: dict, ctx) -> Canvas:
    """가게 건물. 그림 폭은 pixels 폭 - 2(외곽선 자리), 높이는 지붕 ROOF_H + 벽 WALL_H."""
    kind, items = _roof_spec(spec)
    v = _values(spec, ctx)
    px = spec.get("pixels")
    _need(spec, isinstance(px, list) and len(px) == 2, "건물은 pixels: [w, h]로 크기를 적는다")
    w, h = px[0] - 2, ROOF_H + WALL_H
    _need(spec, h <= px[1] - 2, f"pixels 높이 {px[1]}에 지붕 {ROOF_H} + 벽 {WALL_H}과 외곽선이 들어가지 않는다")
    roof_fn = ROOFS[kind]
    P = _Pal(spec, ctx, roof_fn.palette, v["wall"], v["door"])
    cv = Canvas(w, h)
    local = _local(ctx)
    spec["_layout"] = {"size": [w, h], "roof_bottom": ROOF_H - 1}
    roof_fn(cv, spec, ctx, w, ROOF_H)
    area = spec["_layout"]["roof_area"]
    rects = []
    for pid, x, y in items:
        it = ROOF_ITEMS[pid]
        rect = [x, y, it.w, it.h]
        if not _inside(rect, area):
            _report(local.problems, "roof-overlap", f"지붕 위 물건이 난간·지붕 끝에 걸친다({pid} {rect}, 놓을 곳 {area})")
        for other in rects:
            if _overlaps(rect, other):
                _report(local.problems, "roof-overlap", f"지붕 위 물건끼리 겹친다({pid} {rect} · {other})")
        rects.append(rect)
    for (pid, x, y), rect in sorted(zip(items, rects), key=lambda z: (z[1][1], z[1][0])):   # 뒤에서 앞으로
        ROOF_ITEMS[pid].draw(cv, P, x, y)
    spec["_layout"]["roof_items"] = rects
    spec["_layout"].update(_facade(cv, spec, ctx, P, w, ROOF_H, v, local))
    _merge(spec, ctx, local)
    return cv
