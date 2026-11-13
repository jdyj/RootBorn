"""가구: 진열대·계산대·유리 진열대·빵 수레.

설계서 6.6절의 면 나누기를 따른다: 넓고 밝은 윗면 / 한 단계 어두운 앞면 / 튀어나온 것(윗판, 선반 판) 바로 밑에
짙은 줄 하나. 그늘은 색을 곱하지 않고 팔레트의 다른 단계다. 안쪽 칸 나눔은 외곽선 색 선이다.
칸에 넣는 물건(점 부품)은 pile()이 바닥 줄에 촘촘히 놓는다. 층·면의 자리는 spec["_layout"]에 적는다
(테스트가 읽는다, 그리기에는 쓰지 않는다). 좌표는 이 그림(외곽선 전) 기준이다.
"""
from typing import List

from ..canvas import Canvas
from ..lint import Problem
from . import RecipeError, register

MAX_GAP = 2          # 물건이 적을 때 물건 사이 틈의 최대(px). 그보다 넓으면 무더기를 가운데로 모은다.


# ---------- 공통 ----------

def _rid(spec: dict) -> str:
    return spec.get("id", "?")


def _col(spec: dict, ctx, key: str):
    try:
        return ctx.style.color(key)
    except KeyError as e:
        raise RecipeError(f"{_rid(spec)}: {e.args[0]}") from None


def _report(problems: list, code: str, message: str) -> None:
    """같은 코드는 한 번만(그림당 하나)."""
    if all(p.code != code for p in problems):
        problems.append(Problem(code, message))


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
        raise RecipeError(f"{_rid(spec)}: {name}는 부품 id 목록 {lo}~{hi}개여야 한다")
    for r in v:
        for i in r:
            _part(spec, ctx, i)
    return v


def _part(spec: dict, ctx, pid: str) -> Canvas:
    try:
        return ctx.part(pid)
    except RecipeError as e:
        raise RecipeError(f"{_rid(spec)}: {e}") from None


def _local(ctx):
    """그림 하나 동안 문제를 모을 ctx. 끝나면 _merge로 원래 ctx에 코드마다 하나씩 넘긴다."""
    return type(ctx)(ctx.style, ctx.parts, [])


def _merge(spec: dict, ctx, local) -> None:
    """local은 이 그림 하나의 문제(코드마다 이미 하나)다. 다른 그림의 같은 코드와 합치지 않고 그대로 붙인다."""
    for p in local.problems:
        ctx.problems.append(Problem(p.code, f"{_rid(spec)}: {p.message}", p.x, p.y))


# ---------- 칸 채우기 ----------

def pile(cv: Canvas, items: List[str], x0: int, x1: int, base_y: int, ctx, top: int) -> None:
    """부품을 x0..x1 안에 바닥 줄 base_y(부품의 마지막 줄)로 촘촘히 놓는다.

    맞닿은 물건은 외곽선 한 줄을 같이 쓴다(1px 겹침). 자리가 남으면 틈을 MAX_GAP까지 벌리고 가운데로 모은다.
    자리가 모자라면 겹침을 이음마다 고르게 나눈다. 겹침이 이웃한 두 물건 중 좁은 쪽 폭의 1/3을 넘으면 slot-overlap,
    top은 칸의 윗줄(필수: 높이 검사를 빼먹지 않게). 물건 윗줄이 top보다 위로 나가거나 물건이 x0..x1을 벗어나면 slot-clipped를 ctx.problems에 (코드마다 한 번) 남긴다.
    """
    if not items:
        return
    parts = [ctx.part(i) for i in items]
    ws = [p.w for p in parts]
    n = len(parts)
    span = x1 - x0 + 1
    if n == 1:
        gaps: List[int] = []
    else:
        joints = n - 1
        free = span - (sum(ws) - joints)          # 외곽선을 같이 쓰며 붙였을 때 남는 폭
        if free >= 0:
            g = min(free // joints, MAX_GAP + 1) - 1
            gaps = [g if g != 0 else -1] * joints  # 틈 0은 외곽선이 두 겹이 되므로 붙인다
        else:
            need = -free
            gaps = [-1 - (need // joints + (1 if i < need % joints else 0)) for i in range(joints)]
    total = sum(ws) + sum(gaps)
    x = x0 + max(0, (span - total) // 2)
    for i, p in enumerate(parts):
        if i:
            x += ws[i - 1] + gaps[i - 1]
            if -gaps[i - 1] > min(ws[i - 1], ws[i]) // 3:
                _report(ctx.problems, "slot-overlap",
                        f"칸 안 물건이 너무 겹친다({-gaps[i - 1]}px, 폭의 1/3까지 — {items[i - 1]}·{items[i]})")
        y = base_y - p.h + 1
        if y < top or x < x0 or x + p.w - 1 > x1:
            _report(ctx.problems, "slot-clipped", f"물건이 칸보다 크다({items[i]} {p.w}x{p.h})")
        cv.paste(p, x, y)


def streaks(cv: Canvas, x: int, y: int, w: int, h: int, ctx, period: int = 22) -> None:
    """유리·금속의 사선 반사 줄(/ 방향). 줄이 지나는 칸은 같은 줄(ramp)의 한 단계 밝은 색이 이미 그림에 있으면
    그 색, 없으면 glass.streak 색이 된다. 외곽선은 건드리지 않는다. 유리 안의 물건 위로도 지나간다.
    "이미 그림에 있는" 색은 부르는 순간의 그림 기준이다(반사 줄이 새 색을 늘리는 것은 glass.streak 하나뿐)."""
    shine = ctx.style.color("glass.streak.0")
    ol = ctx.style.outline
    present = cv.colors()
    for j in range(h):
        for i in range(w):
            if (i + j) % period not in (5, 6, 7, 10):
                continue
            c = cv.get(x + i, y + j)
            if not c[3] or c == ol:
                continue
            b = ctx.style.brighter(c)
            cv.px(x + i, y + j, b if b in present else shine)


# ---------- 진열대 ----------

SHELF_W = 62
SHELF_TOP = 9              # 윗면 줄 수(그 밑에 외곽선 한 줄)
SHELF_INNER = (10, 78)     # 층들이 나눠 쓰는 안쪽 [y0, y1)
SHELF_POST = 4             # 양옆 기둥 폭(안쪽 칸 나눔 선은 그 안쪽)


@register("furniture.shelf")
def shelf(spec: dict, ctx) -> Canvas:
    """벽에 붙는 나무 진열대. 층 높이는 안쪽 높이를 층 수로 나눠 남김없이 쓴다."""
    tiers = _rows(spec, ctx, "tiers", 1, 4)
    wood = [_col(spec, ctx, f"wood.warm.{i}") for i in range(5)]
    back = [_col(spec, ctx, f"wood.back.{i}") for i in range(2)]   # 0: 판 밑 짙은 줄, 1: 뒤판
    tray = _col(spec, ctx, "cream.2")
    ol = ctx.style.outline
    w, (iy0, iy1) = SHELF_W, SHELF_INNER
    ix0, ix1 = SHELF_POST + 1, w - SHELF_POST - 2          # 안쪽 칸(기둥 안쪽 칸 나눔 선 사이)
    cv = Canvas(w, iy1 + 6)
    local = _local(ctx)

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
        pile(cv, items, ix0 + 1, ix1 - 1, y1 - 4, local, top=y0 + 3)

    cv.rect(0, iy1, w, 4, wood[1])                           # 밑판
    cv.hline(0, iy1, w, wood[2])
    cv.rect(2, iy1 + 4, 6, 2, wood[0])                       # 발
    cv.rect(w - 8, iy1 + 4, 6, 2, wood[0])
    spec["_layout"] = {"size": [cv.w, cv.h], "inner": [iy0, iy1], "tiers": rows}
    _merge(spec, ctx, local)
    return cv


# ---------- 계산대 ----------

COUNTER_W = 94
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
        raise RecipeError(f"{_rid(spec)}: tray는 {{x, w(정수 px), items(부품 id 목록)}}이어야 한다")
    for i in t["items"]:
        _part(spec, ctx, i)
    return t


def _top_items(spec: dict, ctx):
    v = spec.get("top", [])
    ok = isinstance(v, list) and all(
        isinstance(t, dict) and isinstance(t.get("part"), str)
        and isinstance(t.get("x"), int) and not isinstance(t.get("x"), bool) for t in v)
    if not ok:
        raise RecipeError(f"{_rid(spec)}: top은 {{part, x(정수 px)}} 목록이어야 한다")
    return [(_part(spec, ctx, t["part"]), t["part"], t["x"]) for t in v]


@register("furniture.counter")
def counter(spec: dict, ctx) -> Canvas:
    """나무 계산대. 윗면이 높이의 절반쯤, 앞면은 판 넷을 외곽선 색 선으로 나눈다."""
    items = _top_items(spec, ctx)
    tray = _tray(spec, ctx)
    wood = [_col(spec, ctx, f"wood.warm.{i}") for i in range(5)]
    ol = ctx.style.outline
    w = COUNTER_W
    f0, f1 = COUNTER_FACE
    cv = Canvas(w, 58)
    local = _local(ctx)

    cv.rect(0, f0, w, f1 - f0, wood[4])                       # 넓고 밝은 윗면
    for gx, gy, gl in ((8, 10, 28), (48, 15, 32), (20, 20, 24), (60, 25, 22)):
        cv.hline(gx, gy, gl, wood[3])                         # 나뭇결
    cv.rect(0, f1, w, 2, wood[3])                             # 윗판 앞 모서리
    cv.hline(0, f1 + 2, w, wood[2])
    cv.hline(0, f1 + 3, w, ol)
    fy0, fy1 = f1 + 4, 56                                     # 앞면 [fy0, fy1)
    cv.rect(1, fy0, w - 2, fy1 - fy0, wood[2])                # 앞면: 윗면보다 어둡게, 윗판보다 1px 들어감
    cv.rect(1, fy0, w - 2, 2, wood[1])                        # 윗판 밑 짙은 줄
    for k in range(4):
        px = 3 + k * 23
        cv.rect(px, fy0 + 4, 19, 14, wood[3])                 # 판: 위·왼쪽 밝게, 아래·오른쪽 어둡게
        cv.hline(px, fy0 + 4, 19, wood[4])
        cv.hline(px, fy0 + 17, 19, wood[1])
        cv.vline(px + 18, fy0 + 4, 14, wood[1])
        if k:
            cv.vline(px - 2, fy0 + 2, fy1 - fy0 - 2, ol)      # 칸 나눔
    cv.rect(1, fy1, w - 2, 2, wood[0])                        # 걸레받이

    if tray is not None:
        tx, tw = tray["x"], tray["w"]
        if tx < 0 or tx + tw > w:
            _report(local.problems, "slot-clipped", f"쟁반이 계산대 밖으로 나간다(x={tx}, w={tw})")
        ty0, ty1 = COUNTER_TRAY                                # 쟁반: 흰 판, 아래·오른쪽 가장자리 한 단계 어둡게
        cv.rect(tx, ty0, tw, ty1 - ty0, _col(spec, ctx, "cream.2"))
        edge = _col(spec, ctx, "wall.cream.0")
        cv.hline(tx, ty1 - 1, tw, edge)
        cv.vline(tx + tw - 1, ty0, ty1 - ty0, edge)
        for yy in (ty0 - 1, ty1):                              # 쟁반 테두리: 외곽선 색
            cv.hline(tx - 1, yy, tw + 2, ol)
        for xx in (tx - 1, tx + tw):
            cv.vline(xx, ty0, ty1 - ty0, ol)
        pile(cv, tray["items"], tx + 2, tx + tw - 3, ty1 - 2, local, 0)
    for p, pid, x in items:
        y = COUNTER_ITEM_BASE - p.h + 1
        if y < 0 or x < 0 or x + p.w > w:
            _report(local.problems, "slot-clipped", f"윗면 물건이 계산대 밖으로 나간다({pid} x={x})")
        cv.paste(p, x, y)
    spec["_layout"] = {"size": [cv.w, cv.h], "top_face": [f0, f1], "body": [f0, cv.h]}
    _merge(spec, ctx, local)
    return cv


# ---------- 유리 진열대 ----------

CASE_W = 62
CASE_INNER = (19, 42)      # 앞 유리 안 줄들이 나눠 쓰는 [y0, y1)


@register("furniture.glass-case")
def glass_case(spec: dict, ctx) -> Canvas:
    """금속 틀의 유리 진열대. 유리 윗면 + 앞 유리 안에 줄마다 유리 선반, 유리 위로 사선 반사 줄."""
    rows_v = _rows(spec, ctx, "rows", 1, 3)
    metal = [_col(spec, ctx, f"metal.{i}") for i in range(3)]
    gdark, glight = _col(spec, ctx, "glass.front.0"), _col(spec, ctx, "glass.front.1")
    shine = _col(spec, ctx, "glass.streak.0")
    ol = ctx.style.outline
    w, (iy0, iy1) = CASE_W, CASE_INNER
    cv = Canvas(w, 53)
    local = _local(ctx)

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
        pile(cv, items, gx0 + 1, gx1 - 1, y1 - 2, local, top=y0)
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
    _merge(spec, ctx, local)
    return cv


# ---------- 빵 수레 ----------

TROLLEY_W = 30             # 1칸(32) 안: 바깥 외곽선 2px를 더해 32
TROLLEY_INNER = (7, 67)
TROLLEY_POST = 3


@register("furniture.trolley")
def trolley(spec: dict, ctx) -> Canvas:
    """금속 빵 수레. 안쪽은 metal.dark의 밝은 단계(빵이 묻히지 않게), 기둥에 반사 줄, 바퀴."""
    rows_v = _rows(spec, ctx, "rows", 1, 4)
    metal = [_col(spec, ctx, f"metal.{i}") for i in range(3)]
    dark = [_col(spec, ctx, f"metal.dark.{i}") for i in range(3)]
    ol = ctx.style.outline
    w, (iy0, iy1), post = TROLLEY_W, TROLLEY_INNER, TROLLEY_POST
    cv = Canvas(w, iy1 + 7)
    local = _local(ctx)

    cv.rect(0, 0, w, 6, metal[2])                             # 윗면
    cv.hline(0, 5, w, metal[1])
    cv.hline(0, 6, w, ol)
    cv.rect(post, iy0, w - 2 * post, iy1 - iy0, dark[2])      # 안쪽
    cv.rect(post, iy0, w - 2 * post, 2, dark[1])              # 윗면 밑 짙은 줄
    rows = _split(iy0, iy1, len(rows_v))
    for (y0, y1), items in zip(rows, rows_v):
        cv.hline(post, y1 - 2, w - 2 * post, metal[2])        # 받침 살
        cv.hline(post, y1 - 1, w - 2 * post, ol)
        pile(cv, items, post, w - post - 1, y1 - 2, local, top=y0 + 2)
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
    _merge(spec, ctx, local)
    return cv


# ---------- 메뉴판 ----------

MENU_CHALK = ((6, 4, 9), (9, 4, 12), (12, 4, 7), (6, 18, 6), (9, 19, 5), (12, 16, 7))   # (y, x, 길이)


@register("prop.menu-board")
def menu_board(spec: dict, ctx) -> Canvas:
    """벽에 거는 칠판 메뉴판. 나무 틀(위·왼쪽 밝게, 아래·오른쪽 어둡게) 안에 분필 줄. 글자는 쓰지 않는다(다국어)."""
    wood = [_col(spec, ctx, f"wood.warm.{i}") for i in range(5)]
    board = [_col(spec, ctx, f"board.green.{i}") for i in range(2)]
    chalk = _col(spec, ctx, "white.0")
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
