"""수납 가구: 키 큰 사물함과 칸 사물함.

진열대와 같은 면 나누기(설계서 6.6절): 넓고 밝은 윗면 / 한 단계 어두운 앞면 / 튀어나온 윗판 밑에 짙은 줄 하나.
문과 문 사이는 외곽선 색 선이다. 이름표 자리는 밝은 판일 뿐 글자를 쓰지 않는다(다국어).
좌표는 이 그림(외곽선 전) 기준이고 문 자리는 spec["_layout"]에 적는다(테스트가 읽는다).
"""
from ..canvas import Canvas
from . import RecipeError, register
from .common import art_cells_in, is_int_pair, ramp_of, rid, streaks

TOP = 6            # 윗면 줄 수(마지막 줄이 앞 모서리, 그 밑에 외곽선 한 줄)


def _top(cv: Canvas, w: int, m, ol) -> int:
    """넓고 밝은 윗면 + 앞 모서리 + 외곽선. 앞면이 시작하는 줄을 돌려준다."""
    cv.rect(0, 0, w, TOP, m[4])
    cv.hline(0, TOP - 1, w, m[3])
    cv.hline(0, TOP, w, ol)
    return TOP + 1


def _door(cv: Canvas, x: int, y: int, w: int, h: int, m) -> None:
    """앞면 문 판: 위·왼쪽 밝게, 아래·오른쪽 어둡게, 위에 짙은 줄(윗판·위 칸의 그늘)."""
    cv.rect(x, y, w, h, m[2])
    cv.vline(x, y, h, m[3])
    cv.vline(x + w - 1, y, h, m[1])
    cv.hline(x, y + h - 1, w, m[1])
    cv.rect(x, y, w, 2, m[1])


def _plate(cv: Canvas, cx: int, y: int, plate) -> None:
    """이름표 자리(글자 없음): 밝은 판, 아랫줄 한 단계 어둡게."""
    cv.rect(cx - 3, y, 6, 3, plate[2])
    cv.hline(cx - 3, y + 2, 6, plate[1])


# ---------- 키 큰 사물함 ----------

LOCKER_CELLS = ((1, 3), (3, 3))
LOCKER_DOOR = 21           # 문 하나의 목표 폭(나눔 선 포함). 문 수 = (폭 + 1) // 이 값
LOCKER_H = 84              # 그림 높이(진열대와 같다: 96 - 위 빈 줄 10 - 외곽선 2)
LOCKER_BASE = 5            # 걸레받이 줄 수


@register("furniture.locker")
def locker(spec: dict, ctx) -> Canvas:
    """복도 사물함. 폭은 art_cells 폭 × 32 - 2, 문 수는 폭에서. 문마다 위·아래 환기 구멍, 이름표 자리, 손잡이.
    재질이 금속이면(streaks, 기본 true) 문 앞면에 사선 반사 줄."""
    cw, _ = art_cells_in(spec, *LOCKER_CELLS)
    w = cw * ctx.style.cell - 2
    m = ramp_of(spec, ctx, "material", "locker.steel", 5)     # 0 걸레받이 그늘 … 4 윗면
    handle = ramp_of(spec, ctx, "handle", "metal.dark", 3)
    plate = ramp_of(spec, ctx, "plate", "cream", 3)
    shine = spec.get("streaks", True)
    if not isinstance(shine, bool):
        raise RecipeError(f"{rid(spec)}: streaks는 true/false여야 한다(지금 {shine!r})")
    ol = ctx.style.outline
    cv = Canvas(w, LOCKER_H)
    y0 = _top(cv, w, m, ol)
    y1 = LOCKER_H - LOCKER_BASE - 1                            # 문 [y0, y1), 그 밑 외곽선 한 줄
    n = max(1, (w + 1) // LOCKER_DOOR)
    pitch, extra = divmod(w + 1, n)                            # 문 폭 + 나눔 선 1
    doors, x = [], 0
    for k in range(n):
        dw = pitch - 1 + (1 if k < extra else 0)
        doors.append([x, dw])
        x += dw + 1
    for k, (dx, dw) in enumerate(doors):
        _door(cv, dx, y0, dw, y1 - y0, m)
        if k:
            cv.vline(dx - 1, y0, y1 - y0, ol)                 # 문 나눔
        for vy in (y0 + 5, y0 + 51):                           # 환기 구멍: 짙은 줄 셋, 밑에 밝은 턱
            for i in range(3):
                cv.hline(dx + 3, vy + 3 * i, dw - 6, m[0])
                cv.hline(dx + 3, vy + 3 * i + 1, dw - 6, m[3])
        _plate(cv, dx + dw // 2, y0 + 16, plate)
        hx = dx + dw - 5                                       # 손잡이: 문마다 오른쪽
        cv.rect(hx, y0 + 30, 2, 7, handle[1])
        cv.vline(hx, y0 + 30, 7, handle[2])
        cv.hline(hx, y0 + 36, 2, handle[0])
    if shine:
        streaks(cv, 0, y0 + 2, w, y1 - y0 - 3, ctx, 30)
    cv.hline(0, y1, w, ol)
    cv.rect(0, y1 + 1, w, LOCKER_BASE, m[1])                   # 걸레받이
    cv.hline(0, y1 + 1, w, m[2])
    cv.hline(0, LOCKER_H - 1, w, m[0])
    spec["_layout"] = {"size": [cv.w, cv.h], "doors": doors, "front": [y0, y1]}
    return cv


# ---------- 칸 사물함 ----------

CUBBY_CELLS = ((1, 3), (2, 3))
CUBBY_POST = 3             # 양옆 기둥 폭
CUBBY_BASE = 6             # 밑판 4줄 + 발 2줄(진열대와 같다)
CUBBY_MIN = 9              # 문 한 칸의 최소 폭·높이


@register("furniture.cubby")
def cubby(spec: dict, ctx) -> Canvas:
    """교실 뒤 나무 칸 사물함. grid [열, 행](기본 [3, 2])만큼 작은 문, 문마다 이름표 자리와 손잡이.
    그림 높이는 art_cells 높이 × 32 - 12(진열대와 같은 위 빈 줄)."""
    cw, ch = art_cells_in(spec, *CUBBY_CELLS)
    w = cw * ctx.style.cell - 2
    h = ch * ctx.style.cell - 12
    grid = spec.get("grid", [3, 2])
    m = ramp_of(spec, ctx, "material", "wood.warm", 5)
    plate = ramp_of(spec, ctx, "plate", "cream", 3)
    knob = ramp_of(spec, ctx, "handle", "metal.dark", 3)
    ol = ctx.style.outline
    iy0 = TOP + 1
    iy1 = h - CUBBY_BASE                                       # 문들 [iy0, iy1)
    ix0, ix1 = CUBBY_POST, w - CUBBY_POST                      # 문들 [ix0, ix1)
    if not (is_int_pair(grid) and 1 <= grid[0] <= 6 and 1 <= grid[1] <= 6
            and (ix1 - ix0 + 1) // grid[0] - 1 >= CUBBY_MIN and (iy1 - iy0 + 1) // grid[1] - 1 >= CUBBY_MIN):
        raise RecipeError(f"{rid(spec)}: grid는 [열, 행](각 1~6, 문 한 칸이 {CUBBY_MIN}px 이상)이어야 한다(지금 {grid!r})")
    cols, rows = grid
    cv = Canvas(w, h)
    _top(cv, w, m, ol)
    cv.rect(0, iy0, w, iy1 - iy0, m[2])                        # 앞 틀
    cv.vline(0, iy0, iy1 - iy0, m[3])
    cv.vline(w - 1, iy0, iy1 - iy0, m[1])
    cv.rect(0, iy0, w, 2, m[1])                                # 윗판 밑 짙은 줄

    def split(a, b, k):
        """[a, b)를 k칸(나눔 선 1px씩)으로. 칸 [시작, 폭] 목록, 남는 열은 앞 칸들이 하나씩."""
        base, extra = divmod(b - a + 1, k)
        out, p = [], a
        for i in range(k):
            s = base - 1 + (1 if i < extra else 0)
            out.append([p, s])
            p += s + 1
        return out

    xs, ys = split(ix0 + 1, ix1 - 1, cols), split(iy0 + 2, iy1 - 1, rows)
    doors = []
    for (dy, dh) in ys:
        cv.hline(ix0, dy - 1, ix1 - ix0, ol)                   # 칸 나눔(가로)
        for (dx, dw) in xs:
            cv.vline(dx - 1, dy - 1, dh + 2, ol)               # 칸 나눔(세로)
            cv.vline(dx + dw, dy - 1, dh + 2, ol)
            _door(cv, dx, dy, dw, dh, m)
            cv.rect(dx, dy, dw, 2, m[3])                       # 문은 틀보다 밝다: 위 짙은 줄 대신 밝은 턱
            cv.hline(dx, dy, dw, m[4])
            _plate(cv, dx + dw // 2, dy + 3, plate)
            cv.rect(dx + dw - 3, dy + dh - 4, 2, 2, knob[1])   # 손잡이
            cv.px(dx + dw - 3, dy + dh - 4, knob[2])
            doors.append([dx, dy, dw, dh])
    cv.hline(ix0, iy1 - 1, ix1 - ix0, ol)
    cv.rect(0, iy1, w, 4, m[1])                                # 밑판
    cv.hline(0, iy1, w, m[2])
    cv.rect(2, iy1 + 4, 6, 2, m[0])                            # 발
    cv.rect(w - 8, iy1 + 4, 6, 2, m[0])
    spec["_layout"] = {"size": [cv.w, cv.h], "doors": doors, "front": [iy0, iy1]}
    return cv
