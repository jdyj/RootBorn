"""만드는 부품(건물·가구·타일)이 함께 쓰는 도우미. 다른 만드는 부품 모듈의 비공개 이름을 가져다 쓰지 않고 여기서 가져온다."""
from typing import List, Tuple

from ..canvas import Canvas
from ..lint import Problem
from . import RecipeError

MAX_GAP = 2          # 물건이 적을 때 물건 사이 틈의 최대(px). 그보다 넓으면 무더기를 가운데로 모은다.


# ---------- 명세·팔레트 ----------

def rid(spec: dict) -> str:
    return spec.get("id", "?")


def col(spec: dict, ctx, key: str):
    try:
        return ctx.style.color(key)
    except KeyError as e:
        raise RecipeError(f"{rid(spec)}: {e.args[0]}") from None


def report(problems: list, code: str, message: str) -> None:
    """같은 코드는 한 번만(그림당 하나)."""
    if all(p.code != code for p in problems):
        problems.append(Problem(code, message))


def part(spec: dict, ctx, pid: str) -> Canvas:
    try:
        return ctx.part(pid)
    except RecipeError as e:
        raise RecipeError(f"{rid(spec)}: {e}") from None


def local(ctx):
    """그림 하나 동안 문제를 모을 ctx. 끝나면 _merge로 원래 ctx에 코드마다 하나씩 넘긴다."""
    return type(ctx)(ctx.style, ctx.parts, [], ctx.used_parts)   # 쓴 부품은 그림 하나의 것이라 같은 집합을 나눠 쓴다


def merge(spec: dict, ctx, local) -> None:
    """local은 이 그림 하나의 문제(코드마다 이미 하나)다. 다른 그림의 같은 코드와 합치지 않고 그대로 붙인다."""
    for p in local.problems:
        ctx.problems.append(Problem(p.code, f"{rid(spec)}: {p.message}", p.x, p.y))



def ramp(spec: dict, ctx, prefix: str, need: int, key: str = "") -> list:
    """팔레트 줄 prefix의 단계 0, 1, …(어두운 것부터). need개보다 적으면 RecipeError(key는 그 줄을 준 명세 키)."""
    out = []
    while True:
        try:
            out.append(ctx.style.color(f"{prefix}.{len(out)}"))
        except KeyError:
            break
    if len(out) < need:
        what = f"{key}의 " if key else ""
        raise RecipeError(f"{rid(spec)}: {what}팔레트 줄 {prefix}는 단계가 {need}개 이상이어야 한다(지금 {len(out)}개)")
    return out


def ramp_of(spec: dict, ctx, key: str, default: str, need: int) -> list:
    """명세 키 key가 고른 팔레트 줄(키가 없으면 default)의 단계 목록. 줄 이름이 아니거나 단계가 모자라면 RecipeError."""
    name = spec.get(key, default)
    if not isinstance(name, str):
        raise RecipeError(f"{rid(spec)}: {key}는 팔레트 줄 이름이어야 한다(지금 {name!r})")
    return ramp(spec, ctx, name, need, key)


def seed(spec: dict) -> int:
    """명세의 seed(그림 전체의 변주, 기본 0). 정수가 아니면 RecipeError."""
    v = spec.get("seed", 0)
    if not isinstance(v, int) or isinstance(v, bool):
        raise RecipeError(f"{rid(spec)}: seed는 정수여야 한다(지금 {v!r})")
    return v


def art_cells_in(spec: dict, cols: Tuple[int, int], rows: Tuple[int, int]) -> Tuple[int, int]:
    """art_cells [w, h]가 받는 범위 안인지 보고 돌려준다. 그림 크기를 칸 수에서 얻는 만드는 부품이 쓴다."""
    def span(r):
        return f"{r[0]}~{r[1]}" if r[0] != r[1] else str(r[0])
    v = spec.get("art_cells")
    if not (is_int_pair(v) and cols[0] <= v[0] <= cols[1] and rows[0] <= v[1] <= rows[1]):
        raise RecipeError(f"{rid(spec)}: {spec.get('part')}는 art_cells [{span(cols)}, {span(rows)}]로 크기를 적는다"
                          f"(지금 {v if v is not None else 'art_cells 없음'})")
    return v[0], v[1]



def ramp_steps(spec: dict, ctx, prefix: str, steps):
    try:
        return [ctx.style.color(f"{prefix}.{i}") for i in steps]
    except KeyError as e:
        raise RecipeError(f"{spec.get('id', '?')}: {e.args[0]}") from None



# ---------- 그림 크기 ----------

def is_int_pair(v, minimum: int = 1) -> bool:
    return (isinstance(v, list) and len(v) == 2
            and all(isinstance(n, int) and not isinstance(n, bool) and n >= minimum for n in v))


def target_size(spec: dict, cell: int) -> Tuple[int, int]:
    rid = spec.get("id", "?")
    has_cells = "art_cells" in spec
    has_px = "pixels" in spec
    if has_cells == has_px:
        raise RecipeError(f"{rid}: art_cells와 pixels 중 정확히 하나를 적어야 한다")
    if has_cells:
        if not is_int_pair(spec["art_cells"]):
            raise RecipeError(f"{rid}: art_cells는 양의 정수 [w, h]여야 한다")
        w, h = spec["art_cells"]
        return w * cell, h * cell
    if not is_int_pair(spec["pixels"]):
        raise RecipeError(f"{rid}: pixels는 양의 정수 [w, h]여야 한다")
    w, h = spec["pixels"]
    if w % cell or h % cell:
        raise RecipeError(f"{rid}: pixels {w}x{h}는 {cell}의 배수여야 한다")
    return w, h



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
                report(ctx.problems, "slot-overlap",
                        f"칸 안 물건이 너무 겹친다({-gaps[i - 1]}px, 폭의 1/3까지 — {items[i - 1]}·{items[i]})")
        y = base_y - p.h + 1
        if y < top or x < x0 or x + p.w - 1 > x1:
            report(ctx.problems, "slot-clipped", f"물건이 칸보다 크다({items[i]} {p.w}x{p.h})")
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
