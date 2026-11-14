"""가구: 진열대·계산대·유리 진열대·빵 수레.

설계서 6.6절의 면 나누기를 따른다: 넓고 밝은 윗면 / 한 단계 어두운 앞면 / 튀어나온 것(윗판, 선반 판) 바로 밑에
짙은 줄 하나. 그늘은 색을 곱하지 않고 팔레트의 다른 단계다. 안쪽 칸 나눔은 외곽선 색 선이다.
칸에 넣는 물건(점 부품)은 pile()이 바닥 줄에 촘촘히 놓는다. 층·면의 자리는 spec["_layout"]에 적는다
(테스트가 읽는다, 그리기에는 쓰지 않는다). 좌표는 이 그림(외곽선 전) 기준이다.
"""
from typing import List

from ..canvas import Canvas
from . import RecipeError, register
from .common import art_cells_in, col, local, merge, part, pile, ramp_of, report, rid, streaks


# ---------- 공통 ----------

def _split(y0: int, y1: int, n: int) -> List[List[int]]:
    """[y0, y1)을 n칸으로 남김없이 나눈다. 칸 높이 차이는 1 이하, 남는 줄은 아래 칸들이 하나씩 갖는다."""
    base, rem = divmod(y1 - y0, n)
    out, y = [], y0
    for i in range(n):
        h = base + (1 if i >= n - rem else 0)
        out.append([y, y + h])
        y += h
    return out


def _rows(spec: dict, ctx, name: str, lo: int, hi: int) -> List[List[str]]:
    v = spec.get(name)
    if (not isinstance(v, list) or not lo <= len(v) <= hi
            or not all(isinstance(r, list) and all(isinstance(i, str) for i in r) for r in v)):
        raise RecipeError(f"{rid(spec)}: {name}는 부품 id 목록 {lo}~{hi}개여야 한다")
    for r in v:
        for i in r:
            part(spec, ctx, i)
    return v


# ---------- 진열대 ----------

SHELF_CELLS = ((1, 3), (2, 3))   # 받는 art_cells [폭, 높이] 범위. 2칸 높이는 낮은 진열대, 3칸은 키 큰 진열대
SHELF_TOP = 9              # 윗면 줄 수(그 밑에 외곽선 한 줄)
SHELF_INNER_TOP = 10       # 층들이 나눠 쓰는 안쪽의 윗줄(윗면 + 외곽선 밑)
SHELF_HEADROOM = 10        # 캔버스 위쪽에 남기는 빈 줄(외곽선 제외). 키 큰 진열대(3칸)는 그림 84줄 + 외곽선 2 = 96 - 10
SHELF_BASE = 6             # 안쪽 밑에서 그림 끝까지: 밑판 4줄 + 발 2줄
SHELF_MIN_TIER = 17        # 층 하나의 최소 높이(키 큰 진열대 4층의 층 높이. 물건 11px까지)
SHELF_POST = 4             # 양옆 기둥 폭(안쪽 칸 나눔 선은 그 안쪽)


def shelf_inner(cells_h: int, cell: int = 32):
    """art_cells 높이 → 안쪽 [y0, y1). 3칸이면 (10, 78), 2칸이면 (10, 46)."""
    return SHELF_INNER_TOP, cells_h * cell - 2 - SHELF_HEADROOM - SHELF_BASE


@register("furniture.shelf")
def shelf(spec: dict, ctx) -> Canvas:
    """벽에 붙는 진열대. 폭은 art_cells 폭 × 32 - 2, 안쪽 높이는 art_cells 높이에서.
    층 높이는 안쪽 높이를 층 수로 나눠 남김없이 쓰고, 층 수는 층 높이가 SHELF_MIN_TIER 이상인 만큼까지."""
    cw, ch = art_cells_in(spec, *SHELF_CELLS)
    w = cw * ctx.style.cell - 2
    iy0, iy1 = shelf_inner(ch, ctx.style.cell)
    tiers = _rows(spec, ctx, "tiers", 1, min(4, (iy1 - iy0) // SHELF_MIN_TIER))
    wood = ramp_of(spec, ctx, "material", "wood.warm", 5)       # 0 발 … 4 윗면
    back = ramp_of(spec, ctx, "back", "wood.back", 2)           # 0: 판 밑 짙은 줄, 1: 뒤판
    tray = ramp_of(spec, ctx, "tray", "cream", 3)[2]
    ol = ctx.style.outline
    ix0, ix1 = SHELF_POST + 1, w - SHELF_POST - 2          # 안쪽 칸(기둥 안쪽 칸 나눔 선 사이)
    cv = Canvas(w, iy1 + 6)
    lc = local(ctx)

    cv.rect(0, 0, w, SHELF_TOP, wood[4])                    # 넓고 밝은 윗면
    cv.hline(0, SHELF_TOP - 1, w, wood[3])                  # 윗판 앞 모서리
    cv.hline(0, SHELF_TOP, w, ol)

    rows = _split(iy0, iy1, len(tiers))
    for (y0, y1), items in zip(rows, tiers):
        body = y1 - y0 - 3                                   # 선반 판 2줄 + 외곽선 1줄을 뺀 높이
        cv.rect(ix0, y0, ix1 - ix0 + 1, body, back[1])       # 안쪽 뒤판
        cv.rect(ix0, y0, ix1 - ix0 + 1, 2, back[0])          # 위 판 바로 밑 짙은 줄
        cv.rect(ix0, y1 - 5, ix1 - ix0 + 1, 2, tray)         # 쟁반
        for px, lit, dark in ((0, wood[3], wood[2]), (w - SHELF_POST, wood[2], wood[1])):
            cv.rect(px, y0, SHELF_POST, body, wood[2])       # 앞면 기둥: 윗면보다 한 단계 어둡게
            cv.vline(px, y0, body, lit)
            cv.vline(px + SHELF_POST - 1, y0, body, dark)
            cv.hline(px, y0, SHELF_POST, wood[1])            # 판 밑 짙은 줄
        cv.vline(SHELF_POST, y0, body, ol)                   # 안쪽 칸 나눔
        cv.vline(w - SHELF_POST - 1, y0, body, ol)
        cv.hline(0, y1 - 3, w, wood[4])                      # 선반 판: 밝은 윗줄 + 앞면
        cv.hline(0, y1 - 2, w, wood[3])
        cv.hline(0, y1 - 1, w, ol)
        # 물건 자리: 짙은 줄 밑 한 줄 띄우고 쟁반까지(4층이면 11줄 — 빵 부품이 다 들어간다)
        pile(cv, items, ix0 + 1, ix1 - 1, y1 - 4, lc, top=y0 + 3)

    cv.rect(0, iy1, w, 4, wood[1])                           # 밑판
    cv.hline(0, iy1, w, wood[2])
    cv.rect(2, iy1 + 4, 6, 2, wood[0])                       # 발
    cv.rect(w - 8, iy1 + 4, 6, 2, wood[0])
    spec["_layout"] = {"size": [cv.w, cv.h], "inner": [iy0, iy1], "tiers": rows}
    merge(spec, ctx, lc)
    return cv


# ---------- 계산대 ----------

COUNTER_CELLS = ((2, 4), (2, 2))   # 받는 art_cells [폭, 높이] 범위
COUNTER_PANEL = 23         # 앞판 한 칸의 목표 폭(판 19 + 둘레 4). 판 수 = 앞면 폭 / 이 값(반올림), 판 폭은 모두 같다
COUNTER_GRAIN = ((8, 10, 28), (48, 15, 32), (20, 20, 24), (60, 25, 22))   # 윗면 나뭇결 (x, y, 길이): 폭 94 기준, 폭에 비례
COUNTER_GRAIN_W = 94
COUNTER_FACE = (6, 30)     # 윗면 [y0, y1). 위 6줄은 윗면에 놓인 물건이 솟을 자리
COUNTER_ITEM_BASE = 22     # 윗면에 놓인 물건의 마지막 줄
COUNTER_TRAY = (15, 25)    # 윗면 쟁반 [y0, y1)


def _tray(spec: dict, ctx):
    """윗면의 빵 쟁반(선택): {"x", "w", "items"}. 위에서 본 흰 쟁반에 물건을 한 줄로 촘촘히."""
    t = spec.get("tray")
    if t is None:
        return None
    ok = (isinstance(t, dict) and all(isinstance(t.get(k), int) and not isinstance(t.get(k), bool) for k in ("x", "w"))
          and isinstance(t.get("items"), list) and all(isinstance(i, str) for i in t["items"]))
    if not ok:
        raise RecipeError(f"{rid(spec)}: tray는 {{x, w(정수 px), items(부품 id 목록)}}이어야 한다")
    for i in t["items"]:
        part(spec, ctx, i)
    return t


def _top_items(spec: dict, ctx):
    v = spec.get("top", [])
    ok = isinstance(v, list) and all(
        isinstance(t, dict) and isinstance(t.get("part"), str)
        and isinstance(t.get("x"), int) and not isinstance(t.get("x"), bool) for t in v)
    if not ok:
        raise RecipeError(f"{rid(spec)}: top은 {{part, x(정수 px)}} 목록이어야 한다")
    return [(part(spec, ctx, t["part"]), t["part"], t["x"]) for t in v]


@register("furniture.counter")
def counter(spec: dict, ctx) -> Canvas:
    """계산대. 윗면이 높이의 절반쯤, 앞면은 같은 폭의 판들을 외곽선 색 선으로 나눈다(3칸 폭이면 넷).
    폭은 art_cells 폭 × 32 - 2."""
    cw, _ = art_cells_in(spec, *COUNTER_CELLS)
    w = cw * ctx.style.cell - 2
    items = _top_items(spec, ctx)
    tray = _tray(spec, ctx)
    wood = ramp_of(spec, ctx, "material", "wood.warm", 5)
    ol = ctx.style.outline
    f0, f1 = COUNTER_FACE
    cv = Canvas(w, 58)
    lc = local(ctx)

    cv.rect(0, f0, w, f1 - f0, wood[4])                       # 넓고 밝은 윗면
    for gx, gy, gl in COUNTER_GRAIN:
        cv.hline(gx * w // COUNTER_GRAIN_W, gy, gl * w // COUNTER_GRAIN_W, wood[3])   # 나뭇결
    cv.rect(0, f1, w, 2, wood[3])                             # 윗판 앞 모서리
    cv.hline(0, f1 + 2, w, wood[2])
    cv.hline(0, f1 + 3, w, ol)
    fy0, fy1 = f1 + 4, 56                                     # 앞면 [fy0, fy1)
    cv.rect(1, fy0, w - 2, fy1 - fy0, wood[2])                # 앞면: 윗면보다 어둡게, 윗판보다 1px 들어감
    cv.rect(1, fy0, w - 2, 2, wood[1])                        # 윗판 밑 짙은 줄
    span = w - 2                                              # 앞면 폭 [1, w - 1)
    n = max(1, round(span / COUNTER_PANEL))
    pitch = span // n
    x0 = 1 + (span - n * pitch) // 2                           # 나누고 남은 열은 양 끝에 반씩
    pw = pitch - 4
    panels = []
    for k in range(n):
        sx = x0 + k * pitch                                   # 칸 [sx, sx + pitch): 나눔 선, 1px, 판, 2px
        px = sx + 2
        cv.rect(px, fy0 + 4, pw, 14, wood[3])                 # 판: 위·왼쪽 밝게, 아래·오른쪽 어둡게
        cv.hline(px, fy0 + 4, pw, wood[4])
        cv.hline(px, fy0 + 17, pw, wood[1])
        cv.vline(px + pw - 1, fy0 + 4, 14, wood[1])
        if k:
            cv.vline(sx, fy0 + 2, fy1 - fy0 - 2, ol)          # 칸 나눔
        panels.append([px, pw])
    cv.rect(1, fy1, w - 2, 2, wood[0])                        # 걸레받이

    if tray is not None:
        tx, tw = tray["x"], tray["w"]
        if tx < 1 or tx + tw + 1 > w:     # 쟁반 테두리가 한 칸 밖에 그려진다
            report(lc.problems, "slot-clipped", f"쟁반이 계산대 밖으로 나간다(x={tx}, w={tw})")
        ty0, ty1 = COUNTER_TRAY                                # 쟁반: 흰 판, 아래·오른쪽 가장자리 한 단계 어둡게
        cv.rect(tx, ty0, tw, ty1 - ty0, col(spec, ctx, "cream.2"))
        edge = col(spec, ctx, "wall.cream.0")
        cv.hline(tx, ty1 - 1, tw, edge)
        cv.vline(tx + tw - 1, ty0, ty1 - ty0, edge)
        for yy in (ty0 - 1, ty1):                              # 쟁반 테두리: 외곽선 색
            cv.hline(tx - 1, yy, tw + 2, ol)
        for xx in (tx - 1, tx + tw):
            cv.vline(xx, ty0, ty1 - ty0, ol)
        pile(cv, tray["items"], tx + 2, tx + tw - 3, ty1 - 2, lc, 0)
    for p, pid, x in items:
        y = COUNTER_ITEM_BASE - p.h + 1
        if y < 0 or x < 0 or x + p.w > w:
            report(lc.problems, "slot-clipped", f"윗면 물건이 계산대 밖으로 나간다({pid} x={x})")
        cv.paste(p, x, y)
    spec["_layout"] = {"size": [cv.w, cv.h], "top_face": [f0, f1], "body": [f0, cv.h], "front": [fy0, fy1],
                       "panels": panels}
    merge(spec, ctx, lc)
    return cv


# ---------- 유리 진열대 ----------

CASE_W = 62
CASE_INNER = (19, 42)      # 앞 유리 안 줄들이 나눠 쓰는 [y0, y1)


@register("furniture.glass-case")
def glass_case(spec: dict, ctx) -> Canvas:
    """금속 틀의 유리 진열대. 유리 윗면 + 앞 유리 안에 줄마다 유리 선반, 유리 위로 사선 반사 줄."""
    rows_v = _rows(spec, ctx, "rows", 1, 3)
    metal = ramp_of(spec, ctx, "frame", "metal", 3)
    gdark, glight = col(spec, ctx, "glass.front.0"), col(spec, ctx, "glass.front.1")
    shine = col(spec, ctx, "glass.streak.0")
    ol = ctx.style.outline
    w, (iy0, iy1) = CASE_W, CASE_INNER
    cv = Canvas(w, 53)
    lc = local(ctx)

    cv.rect(0, 0, w, 16, metal[1])                            # 윗면 틀
    cv.hline(0, 0, w, metal[2])
    cv.hline(0, 15, w, metal[0])
    cv.rect(2, 2, w - 4, 12, glight)                          # 유리 윗면(가장 밝은 면)
    cv.hline(0, 16, w, ol)
    cv.rect(0, 17, w, iy1 - 17, metal[1])                     # 앞 틀
    cv.vline(0, 17, iy1 - 17, metal[2])
    cv.vline(w - 1, 17, iy1 - 17, metal[0])
    gx0, gx1 = 3, w - 4                                       # 앞 유리 [gx0, gx1]
    cv.rect(gx0, 17, gx1 - gx0 + 1, iy1 - 17, glight)
    cv.rect(gx0, 17, gx1 - gx0 + 1, 2, gdark)                 # 윗면 밑 짙은 줄
    rows = _split(iy0, iy1, len(rows_v))
    for (y0, y1), items in zip(rows, rows_v):
        cv.hline(gx0, y1 - 2, gx1 - gx0 + 1, shine)            # 유리 선반: 밝은 윗줄 + 짙은 앞줄
        cv.hline(gx0, y1 - 1, gx1 - gx0 + 1, gdark)
        pile(cv, items, gx0 + 1, gx1 - 1, y1 - 2, lc, top=y0)
    streaks(cv, 2, 2, w - 4, 12, ctx)
    streaks(cv, gx0, 19, gx1 - gx0 + 1, iy1 - 19, ctx)
    cv.hline(0, iy1, w, ol)
    cv.rect(0, iy1 + 1, w, 8, metal[1])                       # 받침
    cv.hline(0, iy1 + 1, w, metal[2])
    cv.rect(0, iy1 + 7, w, 2, metal[0])
    streaks(cv, 0, iy1 + 2, w, 5, ctx, 30)
    cv.rect(3, iy1 + 9, 5, 2, metal[0])                       # 발
    cv.rect(w - 8, iy1 + 9, 5, 2, metal[0])
    spec["_layout"] = {"size": [cv.w, cv.h], "inner": [iy0, iy1], "rows": rows}
    merge(spec, ctx, lc)
    return cv


# ---------- 빵 수레 ----------

TROLLEY_W = 30             # 1칸(32) 안: 바깥 외곽선 2px를 더해 32
TROLLEY_INNER = (7, 67)
TROLLEY_POST = 3


@register("furniture.trolley")
def trolley(spec: dict, ctx) -> Canvas:
    """빵 수레(기본 금속). 안쪽은 inside 줄의 밝은 단계(빵이 묻히지 않게), 기둥에 반사 줄, 바퀴."""
    rows_v = _rows(spec, ctx, "rows", 1, 4)
    metal = ramp_of(spec, ctx, "frame", "metal", 3)
    dark = ramp_of(spec, ctx, "inside", "metal.dark", 3)
    ol = ctx.style.outline
    w, (iy0, iy1), post = TROLLEY_W, TROLLEY_INNER, TROLLEY_POST
    cv = Canvas(w, iy1 + 7)
    lc = local(ctx)

    cv.rect(0, 0, w, 6, metal[2])                             # 윗면
    cv.hline(0, 5, w, metal[1])
    cv.hline(0, 6, w, ol)
    cv.rect(post, iy0, w - 2 * post, iy1 - iy0, dark[2])      # 안쪽
    cv.rect(post, iy0, w - 2 * post, 2, dark[1])              # 윗면 밑 짙은 줄
    rows = _split(iy0, iy1, len(rows_v))
    for (y0, y1), items in zip(rows, rows_v):
        cv.hline(post, y1 - 2, w - 2 * post, metal[2])        # 받침 살
        cv.hline(post, y1 - 1, w - 2 * post, ol)
        pile(cv, items, post, w - post - 1, y1 - 2, lc, top=y0 + 2)
    for px, cols in ((0, (metal[2], metal[1], metal[0])), (w - post, (metal[1], metal[1], metal[0]))):
        for i, c in enumerate(cols):                          # 기둥: 왼쪽 밝게, 오른쪽 어둡게
            cv.vline(px + i, iy0, iy1 - iy0, c)
        streaks(cv, px, iy0, post, iy1 - iy0, ctx, 12)
    cv.rect(0, iy1, w, 3, metal[1])                           # 아래 살
    cv.hline(0, iy1, w, metal[2])
    cv.hline(0, iy1 + 2, w, metal[0])
    for x in (2, w - 6):                                      # 바퀴
        cv.rect(x, iy1 + 3, 4, 4, dark[0])
        cv.px(x + 1, iy1 + 4, metal[1])
    spec["_layout"] = {"size": [cv.w, cv.h], "inner": [iy0, iy1], "rows": rows}
    merge(spec, ctx, lc)
    return cv


# ---------- 메뉴판 ----------

MENU_CHALK = ((6, 4, 9), (9, 4, 12), (12, 4, 7), (6, 18, 6), (9, 19, 5), (12, 16, 7))   # (y, x, 길이)


@register("prop.menu-board")
def menu_board(spec: dict, ctx) -> Canvas:
    """벽에 거는 칠판 메뉴판. 나무 틀(위·왼쪽 밝게, 아래·오른쪽 어둡게) 안에 분필 줄. 글자는 쓰지 않는다(다국어)."""
    wood = [col(spec, ctx, f"wood.warm.{i}") for i in range(5)]
    board = [col(spec, ctx, f"board.green.{i}") for i in range(2)]
    chalk = col(spec, ctx, "white.0")
    w, h = 30, 20
    cv = Canvas(w, h)
    cv.rect(0, 0, w, h, wood[2])                              # 틀
    cv.hline(0, 0, w, wood[3])
    cv.vline(0, 0, h, wood[3])
    cv.hline(0, h - 1, w, wood[0])
    cv.vline(w - 1, 0, h, wood[0])
    cv.rect(2, 2, w - 4, h - 4, board[1])                     # 칠판
    cv.hline(2, 2, w - 4, board[0])                           # 틀 밑 짙은 줄
    for y, x, n in MENU_CHALK:
        cv.hline(x, y, n, chalk)
    return cv


# ---------- 깔개 ----------

RUG_BORDER = 2       # 테두리 폭(px)
RUG_STITCH = (2, 2)  # 가운데 바느질 줄: (땀 길이, 땀 사이)


@register("prop.rug")
def rug(spec: dict, ctx) -> Canvas:
    """바닥에 깔린 깔개. size [w, h](px, 외곽선 전), colors [몸 줄, 바느질 줄], stitch_step(바느질 줄의 단계).
    몸 줄의 단계 0은 테두리, 1은 바탕. 가운데 가로줄에 바느질 땀. 장면에서는 flat으로 놓는다(그림자 없음)."""
    size, colors, step = spec.get("size"), spec.get("colors"), spec.get("stitch_step")
    ok = (isinstance(size, list) and len(size) == 2 and all(isinstance(n, int) and not isinstance(n, bool) for n in size)
          and size[0] >= 4 * RUG_BORDER + 4 and size[1] >= 2 * RUG_BORDER + 3
          and isinstance(colors, list) and len(colors) == 2 and all(isinstance(c, str) for c in colors)
          and isinstance(step, int) and not isinstance(step, bool))
    if not ok:
        raise RecipeError(f"{rid(spec)}: 깔개는 size [w, h](w ≥ {4 * RUG_BORDER + 4}, h ≥ {2 * RUG_BORDER + 3}), "
                          "colors [몸 줄, 바느질 줄], stitch_step(정수)이 필요하다")
    w, h = size
    edge, body = (col(spec, ctx, f"{colors[0]}.{i}") for i in range(2))
    stitch = col(spec, ctx, f"{colors[1]}.{step}")
    cv = Canvas(w, h)
    cv.rect(0, 0, w, h, edge)
    cv.rect(RUG_BORDER, RUG_BORDER, w - 2 * RUG_BORDER, h - 2 * RUG_BORDER, body)
    run, space = RUG_STITCH
    y = h // 2
    for x in range(2 * RUG_BORDER + 1, w - 2 * RUG_BORDER - run + 1, run + space):
        cv.hline(x, y, run, stitch)
    return cv


# ---------- 입간판 ----------

A_BOARD_CHALK = ((6, 4, 7), (9, 4, 8), (12, 4, 5), (12, 10, 2))   # (y, x, 길이)


@register("prop.a-board")
def a_board(spec: dict, ctx) -> Canvas:
    """가게 앞에 세우는 칠판 입간판. 나무 틀 + 분필 줄(글자 없음) + 두 다리."""
    wood = [col(spec, ctx, f"wood.warm.{i}") for i in range(4)]
    board = [col(spec, ctx, f"board.green.{i}") for i in range(2)]
    chalk = col(spec, ctx, "white.0")
    w = 16
    cv = Canvas(w, 23)
    cv.rect(6, 0, 4, 1, wood[0])                              # 손잡이
    cv.rect(0, 1, w, 17, wood[2])                             # 틀: 위·왼쪽 밝게, 아래·오른쪽 어둡게
    cv.hline(0, 1, w, wood[3])
    cv.vline(0, 1, 17, wood[3])
    cv.hline(0, 17, w, wood[1])
    cv.vline(w - 1, 1, 17, wood[1])
    cv.rect(2, 3, w - 4, 13, board[1])                        # 칠판
    cv.hline(2, 3, w - 4, board[0])                           # 틀 밑 짙은 줄
    for y, x, n in A_BOARD_CHALK:
        cv.hline(x, y, n, chalk)
    for x in (1, w - 3):                                      # 다리
        cv.rect(x, 18, 2, 5, wood[1])
        cv.vline(x, 18, 5, wood[2])
    return cv


# ---------- 점 부품 가구 ----------

@register("furniture.prop")
def prop_furniture(spec: dict, ctx) -> Canvas:
    """점 부품 하나(body)를 그대로 세운 가구(화분, 휴지통, 상자처럼 규칙보다 손 모양이 나은 것).
    items: {part, x, y} 목록(선택) — 몸 그림 좌표(외곽선 전)의 왼쪽 위에 얹는다. y가 음수면 몸 위로 솟는다.
    몸 폭 밖이나 몸 밑으로 나가면 slot-clipped. 색 한도는 부품 한도(10)를 부품마다 따로 받는다."""
    body_id = spec.get("body")
    if not isinstance(body_id, str) or not body_id:
        raise RecipeError(f"{rid(spec)}: body는 점 부품 id여야 한다")
    body = part(spec, ctx, body_id)
    raw = spec.get("items", [])
    ok = isinstance(raw, list) and all(
        isinstance(t, dict) and isinstance(t.get("part"), str)
        and all(isinstance(t.get(k), int) and not isinstance(t.get(k), bool) for k in ("x", "y")) for t in raw)
    if not ok:
        raise RecipeError(f"{rid(spec)}: items는 {{part, x, y(정수 px)}} 목록이어야 한다")
    items = [(part(spec, ctx, t["part"]), t["part"], t["x"], t["y"]) for t in raw]
    bw, bh = body.w - 2, body.h - 2                       # 부품은 사방 1px 외곽선 자리를 갖고 온다
    lc = local(ctx)
    for p, pid, x, y in items:
        if x < 0 or x + p.w - 2 > bw or y + p.h - 2 > bh:
            report(lc.problems, "slot-clipped", f"얹은 부품이 몸 밖으로 나간다({pid} x={x}, y={y})")
    off = -min([0] + [y for _, _, _, y in items])
    cv = Canvas(bw, bh + off)
    cv.paste(body, -1, off - 1)
    for p, _, x, y in items:
        cv.paste(p, x - 1, y + off - 1)
    spec["_layout"] = {"size": [bw, bh], "raised": off}
    merge(spec, ctx, lc)
    return cv
