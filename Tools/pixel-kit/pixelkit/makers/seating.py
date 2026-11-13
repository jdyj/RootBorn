"""의자와 책상. 나중에 캐릭터가 앉을 것을 염두에 둔다.

치수는 게임 캐릭터(프레임 32x59, 발이 칸 아랫변, 어른의 다리 약 24px)에서 얻었다: 좌판 윗면은 무릎 높이(바닥에서
14px), 책상 윗면 앞 모서리는 좌판보다 11px 높다(서 있을 때 허리 아래, 앉으면 가슴 아래).
높이는 모두 "바닥에서 몇 px"로 적는다. 바닥 = 캔버스 아랫변 = 그린 그림(외곽선 전) 좌표의 y = floor(그림 높이 + 1:
그림 밑 한 줄은 렌더러가 치는 외곽선). 그래서 줄 y의 높이는 floor - y다.

면 나누기는 가구(furniture.py)와 같다: 넓고 밝은 윗면 / 한 단계 어두운 앞면 / 튀어나온 것 밑 짙은 줄 하나.
막대(기둥·다리·등받이 살)는 왼쪽 열이 밝고 오른쪽 열이 어둡다(빛은 왼쪽 위). 동쪽 그림은 서쪽을 뒤집은 자리에
같은 빛 규칙으로 다시 칠한다(_Paint.mirror).

앉기를 위한 층(layer): back = 앉은 사람 뒤에 그릴 조각, front = 앉은 사람 앞에 그릴 조각, all = 둘을 합친 그림.
- 나누는 기준은 "앉은 사람의 몸이 그 조각보다 뒤에 있는가". 그리는 동안 칠한 칸마다 어느 조각인지 적는다.
- 좌판 밑(다리 줄, LEG_TOP 아래)은 앉은 사람과 겹치지 않으므로 두 층이 함께 갖는다. 그래서 층마다 혼자서도
  바닥에 선 그림이다(검사의 아래 가운데 맞춤을 통과한다).
- back 위에 front를 같은 자리에 겹치면 all과 바이트가 같다. 그러려면 앞 조각이 뒤 조각과 맞닿는 곳은 외곽선 색
  줄(이음 줄)이어야 한다 — 렌더러가 조각마다 외곽선을 다시 치기 때문이다. 이음 줄은 좌판 앞 테두리 위의 짙은 줄이다.
"""
from typing import Callable, Dict, Set, Tuple

from ..canvas import CLEAR, Canvas
from . import RecipeError, register
from .common import art_cells_in, local, merge, part, ramp_of, report, rid


# ---------- 칠하기 ----------

class _Paint:
    """칠하면서 칸마다 앞 조각인지 적는다(front가 참일 때 칠한 칸). mirror면 x를 좌우로 뒤집어 놓되,
    막대의 밝은 열·어두운 열(cols)은 화면 왼쪽부터 칠한다 — 빛은 늘 왼쪽 위다."""

    def __init__(self, w: int, h: int, mirror: bool = False):
        self.cv = Canvas(w, h)
        self.mirror = mirror
        self.front = False
        self.front_px: Set[Tuple[int, int]] = set()

    def _put(self, x: int, y: int, c) -> None:
        if 0 <= x < self.cv.w and 0 <= y < self.cv.h:
            self.cv.px(x, y, c)
            if self.front and c != CLEAR:
                self.front_px.add((x, y))
            else:
                self.front_px.discard((x, y))

    def _sx(self, x: int, w: int) -> int:
        return self.cv.w - x - w if self.mirror else x

    def rect(self, x: int, y: int, w: int, h: int, c) -> None:
        sx = self._sx(x, w)
        for yy in range(y, y + h):
            for xx in range(sx, sx + w):
                self._put(xx, yy, c)

    def hline(self, x: int, y: int, w: int, c) -> None:
        self.rect(x, y, w, 1, c)

    def px(self, x: int, y: int, c) -> None:
        self.rect(x, y, 1, 1, c)

    def bar(self, x: int, y: int, h: int, cols) -> None:
        """세로 막대. cols는 화면 왼쪽 열부터의 색(폭 = len(cols))."""
        sx = self._sx(x, len(cols))
        for i, c in enumerate(cols):
            for yy in range(y, y + h):
                self._put(sx + i, yy, c)

    def end_col(self, x: int, w: int, y: int, h: int, c) -> None:
        """가로로 긴 판의 화면 오른쪽 끝 열(빛 반대쪽)."""
        sx = self._sx(x, w)
        for yy in range(y, y + h):
            self._put(sx + w - 1, yy, c)

    def layer(self, name: str, shared_from: int) -> Canvas:
        """all이면 그림 그대로. front는 앞 조각, back은 나머지 + shared_from 줄부터 아래(두 층이 함께 갖는 줄)."""
        if name == "all":
            return self.cv
        out = Canvas(self.cv.w, self.cv.h)
        for y in range(self.cv.h):
            for x in range(self.cv.w):
                c = self.cv.get(x, y)
                if not c[3]:
                    continue
                f = (x, y) in self.front_px
                if f if name == "front" else (not f or y >= shared_from):
                    out.px(x, y, c)
        return out


def _choice(spec: dict, key: str, table: dict, default: str) -> str:
    v = spec.get(key, default)
    if v not in table:
        raise RecipeError(f"{rid(spec)}: {key} {v!r}는 모르는 값. 아는 값: {', '.join(table)}")
    return v


# ---------- 의자 ----------

CHAIR_W = 20          # 의자 폭(외곽선 전). 캐릭터 몸통(팔 포함 약 21px)과 비슷하다
SEAT_TOP = 14         # 좌판 윗면(먼 쪽 가장자리)의 높이. 어른 다리 24px의 무릎 높이
SEAT_ROWS = 4         # 위에서 비스듬히 보이는 좌판 윗면 줄 수(14~11)
SEAM = 10             # 좌판 앞 테두리 위 이음 줄(외곽선 색)의 높이 — 앞 조각과 뒤 조각의 경계
EDGE_ROWS = 2         # 좌판 앞 테두리(9~8)
LEG_TOP = 7           # 다리가 시작하는 높이(좌판 밑). 이 줄부터 아래는 두 층이 함께 갖는다
LEG_FOOT = 2          # 다리 맨 아랫줄의 높이(그 밑은 외곽선)
POST = 3              # 기둥·다리 폭
RAIL_ROWS = 4         # 등받이 윗판 줄 수(앞·뒤에서)
BACK_RISE = 14        # 등받이가 딛고 선 좌판 가장자리 줄 위로 솟는 높이 = 좌판 높이(실제 의자처럼 1:1).
                      # 캐릭터 다리로 잰 척도(다리 24px ≈ 80cm)에서 등받이 꼭대기는 서 있는 캐릭터의 허리 조금 위,
                      # 엉덩이를 좌판에 둔 캐릭터의 턱(바닥에서 약 22px)과 비슷하다 — 뒤에서 봐도 머리가 가려지지 않는다
BACK_TOP = {          # 등받이 꼭대기 높이. 남쪽은 먼 가장자리(14) 위, 북쪽은 가까운 가장자리(11) 위,
    "south": SEAT_TOP + BACK_RISE,                     # 옆모습은 앞뒤로 긴 윗판의 윗면이 보여 먼 쪽 끝까지
    "north": SEAT_TOP - SEAT_ROWS + 1 + BACK_RISE,
    "side": SEAT_TOP + BACK_RISE,
}
SLATS = (6, 12)       # 앞·뒤에서 본 등받이 살 두 개의 x(폭 2)
CHAIR_LAYERS = ("all", "back", "front")
CHAIR_H = max(BACK_TOP.values()) - 1     # 그린 그림 높이: 맨 윗줄의 높이가 가장 높은 등받이
FLOOR = CHAIR_H + 1


def _y(h: int) -> int:
    """높이 h(바닥에서 px)인 줄의 y(그린 그림 좌표)."""
    return FLOOR - h


class _Chair:
    def __init__(self, wood, cushion, ol):
        self.wood = wood          # 0 가장 어둡게 … 4 가장 밝게
        self.cushion = cushion    # 0 … 2, 없으면 None
        self.ol = ol              # 외곽선 색(이음 줄)

    def post(self, p: _Paint, x: int, top: int, foot: int) -> None:
        """기둥이나 다리: 높이 top부터 foot까지, 맨 아랫줄은 한 단계 어둡게."""
        w = self.wood
        p.bar(x, _y(top), top - foot, (w[3], w[2], w[1]))
        p.bar(x, _y(foot), 1, (w[2], w[1], w[0]))

    def rail(self, p: _Paint, x: int, top: int, w_: int, rows: int) -> None:
        """등받이 윗판(앞이나 뒤에서): 밝은 윗면 한 줄, 앞면, 아랫줄 어둡게, 오른쪽 끝 어둡게, 위 모서리 둥글게."""
        w = self.wood
        p.rect(x, _y(top), w_, rows, w[3])
        p.hline(x, _y(top), w_, w[4])
        p.hline(x, _y(top - rows + 1), w_, w[2])
        p.end_col(x, w_, _y(top - 1), rows - 1, w[2])
        p.px(x, _y(top), CLEAR)
        p.px(x + w_ - 1, _y(top), CLEAR)

    def seat_top(self, p: _Paint, x: int, w_: int, open_left: bool = True, open_right: bool = True) -> None:
        """좌판 윗면(높이 14~11). 방석이 있으면 나무 테 안에 방석, 없으면 나뭇결."""
        w, c = self.wood, self.cushion
        y0 = _y(SEAT_TOP)
        p.rect(x, y0, w_, SEAT_ROWS, w[4])
        p.end_col(x, w_, y0, SEAT_ROWS, w[3])
        if c is None:
            p.hline(x + 3, y0 + 1, max(1, w_ // 3), w[3])                 # 나뭇결
            p.hline(x + w_ // 2, y0 + 2, max(1, w_ // 3), w[3])
            return
        cx0 = x + (1 if open_left else 0)
        cw = w_ - (1 if open_left else 0) - (1 if open_right else 0)
        p.rect(cx0, y0, cw, SEAT_ROWS - 1, c[2])                          # 방석 윗면
        p.hline(cx0, y0 + SEAT_ROWS - 1, cw, c[1])                        # 앞으로 불룩한 줄
        p.end_col(cx0, cw, y0 + 1, SEAT_ROWS - 1, c[0])                   # 오른쪽 끝(빛 반대쪽)
        p.end_col(cx0, cw, y0, 1, c[1])
        p.px(cx0, y0, w[4])                                               # 먼 쪽 모서리 둥글게
        p.px(cx0 + cw - 1, y0, w[4])

    def seat_edge(self, p: _Paint, x: int, w_: int) -> None:
        """이음 줄 + 좌판 앞 테두리(높이 10~8). 앞 조각."""
        w = self.wood
        p.hline(x, _y(SEAM), w_, self.ol)
        p.rect(x, _y(SEAM - 1), w_, EDGE_ROWS, w[2])
        p.hline(x, _y(SEAM - 1), w_, w[3])
        p.end_col(x, w_, _y(SEAM - 1), EDGE_ROWS, w[1])


def _chair_south(p: _Paint, ch: _Chair) -> dict:
    """의자가 우리를 본다: 등받이가 뒤(위), 좌판, 앞 테두리와 앞다리. 뒷다리는 앞다리에 가려 안 보인다."""
    W, w = CHAIR_W, ch.wood
    top = BACK_TOP["south"]
    ch.rail(p, 0, top, W, RAIL_ROWS)
    post_top, rail_low = top - RAIL_ROWS, SEAT_TOP + 2         # 기둥 24~15, 아래 가로대 16~15
    for x in (0, W - POST):
        ch.post(p, x, post_top, SEAT_TOP + 1)
    for x in SLATS:
        p.bar(x, _y(post_top), post_top - rail_low, (w[3], w[1]))
    p.rect(POST, _y(rail_low), W - 2 * POST, 2, w[2])
    p.hline(POST, _y(rail_low), W - 2 * POST, w[3])
    ch.seat_top(p, 0, W)
    p.front = True                                              # 앞 조각: 이음 줄, 앞 테두리, 앞다리
    ch.seat_edge(p, 0, W)
    for x in (0, W - POST):
        ch.post(p, x, LEG_TOP, LEG_FOOT)
    return {"seat": [0, _y(SEAT_TOP), W, SEAT_ROWS], "back_top": _y(top)}


def _chair_north(p: _Paint, ch: _Chair) -> dict:
    """등받이 뒷면이 우리 쪽: 좌판은 등받이에 가려진다. 기둥이 그대로 뒷다리가 된다. 전부 앞 조각이다."""
    W, w = CHAIR_W, ch.wood
    top = BACK_TOP["north"]
    p.front = True
    ch.rail(p, 0, top, W, RAIL_ROWS)
    post_top = top - RAIL_ROWS
    for x in (0, W - POST):
        ch.post(p, x, post_top, LEG_FOOT)
    for x in SLATS:
        p.bar(x, _y(post_top), post_top - SEAT_TOP, (w[3], w[1]))
    low = SEAT_TOP                                              # 아래 가로대 14~11: 좌판 윗면 줄을 가린다
    p.rect(POST, _y(low), W - 2 * POST, SEAT_ROWS, w[2])
    p.hline(POST, _y(low), W - 2 * POST, w[3])
    p.hline(POST, _y(low - SEAT_ROWS + 1), W - 2 * POST, w[1])
    ch.seat_edge(p, POST, W - 2 * POST)                         # 좌판 뒤 테두리
    return {"seat": [0, _y(SEAT_TOP), W, SEAT_ROWS], "back_top": _y(top)}


SIDE_SEAT_W = CHAIR_W - POST      # 옆모습 좌판 깊이(17). 등받이 기둥이 오른쪽(서쪽을 볼 때) 3열


def _chair_side(p: _Paint, ch: _Chair) -> dict:
    """옆모습(서쪽을 본다): 등받이가 오른쪽에 서고, 좌판 윗면과 우리 쪽 옆 테두리, 가까운 쪽 다리 둘."""
    w = ch.wood
    top = BACK_TOP["side"]
    bx = SIDE_SEAT_W
    p.bar(bx, _y(top), 3, (w[4], w[4], w[3]))                  # 등받이 윗판의 윗면(앞뒤로 길다)
    p.bar(bx, _y(top - 3), top - 3 - SEAT_TOP + SEAT_ROWS, (w[3], w[2], w[1]))   # 등받이 기둥(좌판 옆까지)
    ch.seat_top(p, 0, SIDE_SEAT_W, open_right=False)
    p.front = True                                              # 앞 조각: 이음 줄, 옆 테두리, 가까운 다리 둘
    ch.seat_edge(p, 0, CHAIR_W)
    p.bar(bx, _y(SEAM - 1), SEAM - 1 - LEG_FOOT, (w[3], w[2], w[1]))   # 뒷다리: 등받이 기둥이 이어진다
    p.bar(bx, _y(LEG_FOOT), 1, (w[2], w[1], w[0]))
    ch.post(p, 0, LEG_TOP, LEG_FOOT)                            # 앞다리
    return {"seat": [0, _y(SEAT_TOP), SIDE_SEAT_W, SEAT_ROWS], "back_top": _y(top)}


CHAIR_VIEWS: Dict[str, Tuple[Callable, bool]] = {             # 방향 → (그리는 함수, 좌우 뒤집기)
    "south": (_chair_south, False),
    "north": (_chair_north, False),
    "west": (_chair_side, False),
    "east": (_chair_side, True),
}


@register("furniture.chair")
def chair(spec: dict, ctx) -> Canvas:
    """의자 한 개(art_cells [1, 2]). facing 방향, layer 층, material 나무 줄(5), cushion 방석 줄(3, 선택)."""
    art_cells_in(spec, (1, 1), (2, 2))
    facing = _choice(spec, "facing", CHAIR_VIEWS, "south")
    layer = _choice(spec, "layer", dict.fromkeys(CHAIR_LAYERS), "all")
    wood = ramp_of(spec, ctx, "material", "wood.warm", 5)
    cushion = ramp_of(spec, ctx, "cushion", "", 3) if "cushion" in spec else None
    draw, mirror = CHAIR_VIEWS[facing]
    p = _Paint(CHAIR_W, CHAIR_H, mirror)
    lay = draw(p, _Chair(wood, cushion, ctx.style.outline))
    sx, sy, sw, sh = lay["seat"]
    if mirror:
        lay["seat"] = [CHAIR_W - sx - sw, sy, sw, sh]
        sx = lay["seat"][0]
    lay.update({"size": [CHAIR_W, CHAIR_H], "floor": FLOOR, "sit_point": [sx + (sw - 1) // 2, sy + sh // 2],
                "shared_from": _y(LEG_TOP), "facing": facing, "layer": layer})
    spec["_layout"] = lay
    return p.layer(layer, _y(LEG_TOP))


# ---------- 책상 ----------

DESK_H = 61               # 그린 그림 높이(위쪽은 윗면에 놓는 물건 자리)
DESK_FLOOR = DESK_H + 1
WORK_HEIGHT = 25          # 윗면 앞 모서리 줄의 높이 = 좌판 + 11
DESK_DEPTH_ROWS = 12      # 앞에서 본 책상: 윗면 줄 수(깊이 한 칸)
DESK_LENGTH_ROWS = 24     # 옆에서 본 책상: 윗면 줄 수(길이 두 칸)
APRON_ROWS = 3            # 윗판 밑 앞판(무릎 자리 위)
LEG_W = 4
PEDESTAL_W = {1: 22, 2: 19}   # 서랍장 폭: 한쪽만이면 22, 양쪽이면 19
DESK_ITEM_HEIGHT = 30     # 윗면 물건의 마지막 줄 높이(윗면 뒤쪽 절반에 선다)
DRAWER_SIDES = {          # 서랍 → (왼쪽 서랍장, 오른쪽 서랍장). 앉은 사람 기준 오른쪽 = 남쪽을 볼 때 화면 오른쪽
    "right": (False, True),
    "left": (True, False),
    "none": (False, False),
    "both": (True, True),
}


def _dy(h: int) -> int:
    return DESK_FLOOR - h


def _top_items(spec: dict, ctx):
    v = spec.get("top_items", [])
    ok = isinstance(v, list) and all(
        isinstance(t, dict) and isinstance(t.get("part"), str)
        and isinstance(t.get("x"), int) and not isinstance(t.get("x"), bool) for t in v)
    if not ok:
        raise RecipeError(f"{rid(spec)}: top_items는 {{part, x(정수 px)}} 목록이어야 한다")
    return [(part(spec, ctx, t["part"]), t["part"], t["x"]) for t in v]


class _Desk:
    def __init__(self, wood, top, ol):
        self.wood = wood
        self.top = top            # (윗면, 나뭇결)
        self.ol = ol              # 외곽선 색(서랍 사이 줄)

    def top_face(self, p: _Paint, w_: int, rows: int, vertical_grain: bool) -> None:
        """넓고 밝은 윗면 + 앞 모서리 두 줄 + 그 밑 짙은 줄(외곽선 색)."""
        w = self.wood
        fill, grain = self.top
        y0 = _dy(WORK_HEIGHT + rows - 1)
        p.rect(0, y0, w_, rows, fill)
        if vertical_grain:
            for gx, gy, gl in ((5, 3, 9), (14, 10, 11), (22, 5, 7)):
                p.rect(gx * w_ // 30, y0 + gy, 1, gl, grain)
        else:
            for gx, gy, gl in ((6, 2, 14), (30, 5, 18), (14, 8, 12), (44, 9, 10)):
                p.hline(gx * w_ // 62, y0 + gy, gl * w_ // 62, grain)
        p.hline(0, _dy(WORK_HEIGHT - 1), w_, w[3])            # 윗판 앞 모서리
        p.hline(0, _dy(WORK_HEIGHT - 2), w_, w[2])
        p.end_col(0, w_, _dy(WORK_HEIGHT - 1), 2, w[1])

    def leg(self, p: _Paint, x: int) -> None:
        w = self.wood
        top = WORK_HEIGHT - 3
        p.bar(x, _dy(top), top - LEG_FOOT, (w[3], w[2], w[2], w[1]))
        p.bar(x, _dy(top), 1, (w[1], w[1], w[1], w[0]))          # 윗판 밑 짙은 줄
        p.bar(x, _dy(LEG_FOOT), 1, (w[2], w[1], w[1], w[0]))

    def box(self, p: _Paint, x: int, w_: int) -> Tuple[int, int]:
        """윗판 밑부터 바닥까지 선 상자(서랍장·옆판). 안쪽 [y0, y1)를 돌려준다. 밑은 한 칸 들어간 받침."""
        w = self.wood
        top = WORK_HEIGHT - 3
        y0, y1 = _dy(top), _dy(LEG_FOOT + 1)
        p.rect(x, y0, w_, y1 - y0, w[2])
        p.bar(x, y0, y1 - y0, (w[3],))
        p.end_col(x, w_, y0, y1 - y0, w[1])
        p.hline(x, y0, w_, w[1])                                  # 윗판 밑 짙은 줄
        p.rect(x + 1, y1, w_ - 2, 2, w[0])                        # 받침
        return y0 + 1, y1

    def drawers(self, p: _Paint, x: int, w_: int) -> None:
        """서랍장: 서랍 셋, 사이는 외곽선 색 줄, 가운데 손잡이."""
        w = self.wood
        y0, y1 = self.box(p, x, w_)
        n, gap = 3, 1
        rows = (y1 - y0 - (n - 1) * gap) // n
        y = y0 + (y1 - y0 - (n * rows + (n - 1) * gap)) // 2
        for k in range(n):
            px, pw = x + 2, w_ - 4
            p.rect(px, y, pw, rows, w[3])
            p.hline(px, y, pw, w[4])
            p.hline(px, y + rows - 1, pw, w[1])
            p.end_col(px, pw, y, rows, w[1])
            p.hline(x + w_ // 2 - 1, y + rows // 2, 2, w[0])     # 손잡이
            if k < n - 1:
                p.hline(x + 1, y + rows, w_ - 2, self.ol)
            y += rows + gap

    def end_panel(self, p: _Paint, x: int, w_: int) -> None:
        """옆에서 본 서랍장 옆판: 판 안에 들어간 네모(위·왼쪽 그늘, 아래·오른쪽 빛)."""
        w = self.wood
        y0, y1 = self.box(p, x, w_)
        ix0, ix1, iy0, iy1 = x + 3, x + w_ - 4, y0 + 2, y1 - 3
        p.rect(ix0, iy0, ix1 - ix0 + 1, iy1 - iy0 + 1, w[2])
        p.hline(ix0, iy0, ix1 - ix0 + 1, w[1])
        p.rect(ix0, iy0, 1, iy1 - iy0 + 1, w[1])
        p.hline(ix0, iy1, ix1 - ix0 + 1, w[3])
        p.rect(ix1, iy0 + 1, 1, iy1 - iy0, w[3])

    def apron(self, p: _Paint, x0: int, x1: int) -> None:
        """무릎 자리 위 앞판 [x0, x1]."""
        w = self.wood
        top = WORK_HEIGHT - 3
        p.rect(x0, _dy(top), x1 - x0 + 1, APRON_ROWS, w[2])
        p.hline(x0, _dy(top), x1 - x0 + 1, w[1])


def _open_under(left: int, right: int) -> list:
    """다리·서랍장 사이 [left, right] 열이 비었을 때, 외곽선 뒤에도 투명한 무릎 자리 사각형."""
    top = WORK_HEIGHT - 3 - APRON_ROWS          # 앞판 밑 줄(외곽선이 들어간다)
    y = _dy(top) + 1
    return [left + 1, y, right - left - 1, DESK_H - y]


def _desk_south(p: _Paint, d: _Desk, sides) -> dict:
    """앞에서 본 책상(폭 두 칸): 넓은 윗면, 서랍장과 다리, 그 사이 뚫린 무릎 자리."""
    W = p.cv.w
    d.top_face(p, W, DESK_DEPTH_ROWS, False)
    lw = PEDESTAL_W[max(1, sum(sides))]
    inner = []
    if sides[0]:
        d.drawers(p, 1, lw)
        inner.append(1 + lw)
    else:
        d.leg(p, 1)
        inner.append(1 + LEG_W)
    if sides[1]:
        d.drawers(p, W - 1 - lw, lw)
        inner.append(W - 1 - lw - 1)
    else:
        d.leg(p, W - 1 - LEG_W)
        inner.append(W - 1 - LEG_W - 1)
    d.apron(p, inner[0], inner[1])
    return {"top": [0, _dy(WORK_HEIGHT + DESK_DEPTH_ROWS - 1), W, DESK_DEPTH_ROWS],
            "kneehole": _open_under(inner[0], inner[1])}


def _desk_side(p: _Paint, d: _Desk, sides) -> dict:
    """옆에서 본 책상(책상의 끝): 앞뒤로 긴 윗면과 가까운 끝. 가까운 끝에 서랍장이 있으면 옆판, 없으면 다리 둘."""
    W = p.cv.w
    d.top_face(p, W, DESK_LENGTH_ROWS, True)
    near = sides[1] if not p.mirror else sides[0]      # 서쪽을 볼 때 앉은 사람의 오른쪽 = 남쪽(가까운 끝)
    out = {"top": [0, _dy(WORK_HEIGHT + DESK_LENGTH_ROWS - 1), W, DESK_LENGTH_ROWS], "kneehole": None}
    if near:
        d.end_panel(p, 1, W - 2)
        return out
    d.leg(p, 1)
    d.leg(p, W - 1 - LEG_W)
    d.apron(p, 1 + LEG_W, W - 2 - LEG_W)
    out["kneehole"] = _open_under(LEG_W, W - 2 - LEG_W)
    return out


DESK_VIEWS: Dict[str, Tuple[Callable, Tuple[int, int], int, bool]] = {   # 방향 → (그리는 함수, art_cells, 폭, 뒤집기)
    "south": (_desk_south, (2, 2), 62, False),
    "west": (_desk_side, (1, 2), 30, False),
    "east": (_desk_side, (1, 2), 30, True),
}


@register("furniture.desk")
def desk(spec: dict, ctx) -> Canvas:
    """책상. facing(south 두 칸 폭 | west·east 옆면 한 칸 폭), material 나무 줄(5), top 윗면 줄(2 이상, 선택),
    drawers 서랍 쪽, top_items 윗면 물건 {part, x}."""
    facing = _choice(spec, "facing", DESK_VIEWS, "south")
    draw, cells, width, mirror = DESK_VIEWS[facing]
    art_cells_in(spec, (cells[0], cells[0]), (cells[1], cells[1]))
    sides = DRAWER_SIDES[_choice(spec, "drawers", DRAWER_SIDES, "right")]
    wood = ramp_of(spec, ctx, "material", "wood.warm", 5)
    top = ramp_of(spec, ctx, "top", "", 2)[-2:] if "top" in spec else [wood[3], wood[4]]
    items = _top_items(spec, ctx)
    p = _Paint(width, DESK_H, mirror)
    lay = draw(p, _Desk(wood, (top[1], top[0]), ctx.style.outline), sides)
    lc = local(ctx)
    base = _dy(DESK_ITEM_HEIGHT)
    for pc, pid, x in items:
        y = base - pc.h + 1
        if y < 0 or x < 0 or x + pc.w > width:
            report(lc.problems, "slot-clipped", f"윗면 물건이 책상 밖으로 나간다({pid} x={x})")
        p.cv.paste(pc, x, y)
    lay.update({"size": [width, DESK_H], "floor": DESK_FLOOR, "work_height": WORK_HEIGHT, "facing": facing})
    spec["_layout"] = lay
    merge(spec, ctx, lc)
    return p.cv
