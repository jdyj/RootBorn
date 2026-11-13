"""검사: 내보내기 전에 나쁜 그림을 막는다. 문제 하나당 코드 하나."""
from dataclasses import dataclass
from typing import FrozenSet, List, Optional

from .canvas import Canvas

N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))
N8 = tuple((dx, dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy)


@dataclass
class Problem:
    code: str
    message: str
    x: Optional[int] = None
    y: Optional[int] = None


def _group(code: str, text: str, hits: list) -> List[Problem]:
    """칸마다 나온 결함을 그림당 하나로 묶는다. 위치는 읽는 순서로 첫 칸, 메시지에 나머지 칸 수."""
    if not hits:
        return []
    hits = sorted(hits, key=lambda h: (h[1], h[0]))
    x, y = hits[0]
    rest = f" 외 {len(hits) - 1}칸" if len(hits) > 1 else ""
    return [Problem(code, f"{text}{rest}", x, y)]


def _limit(style, kind: str) -> int:
    return style.max_colors.get(kind, style.max_colors["furniture"])


def _opaque(cv: Canvas):
    for y in range(cv.h):
        for x in range(cv.w):
            c = cv.get(x, y)
            if c[3]:
                yield x, y, c


def _palette_and_count(cv, recipe, ctx) -> List[Problem]:
    style = ctx.style
    out = _group("off-palette", "팔레트 밖 색", [(x, y) for x, y, c in _opaque(cv) if c not in style.palette])
    used = {c for c in cv.colors() if c != style.outline}
    limit = _limit(style, recipe.get("kind", "furniture"))
    if len(used) > limit:
        out.append(Problem("too-many-colors", f"색 {len(used)}개(최대 {limit}, 외곽선 제외)"))
    return out


def _cells(cv, ctx) -> List[Problem]:
    c = ctx.style.cell
    if cv.w % c or cv.h % c:
        return [Problem("not-cell-multiple", f"캔버스 {cv.w}x{cv.h}는 {c}의 배수가 아니다")]
    return []


def lint_sprite(cv: Canvas, recipe: dict, ctx, allow: FrozenSet[str] = frozenset()) -> List[Problem]:
    """외곽선이 입혀진 그림(건물·가구·소품·점 부품). 점 부품은 kind가 "part"."""
    ol = ctx.style.outline
    is_part = recipe.get("kind") == "part"
    open_hits = []
    for x, y, c in _opaque(cv):
        if c == ol:
            continue
        # 캔버스 가장자리는 overflow가 맡는다: 안쪽 이웃만 본다.
        for dx, dy in N4:
            nx, ny = x + dx, y + dy
            if 0 <= nx < cv.w and 0 <= ny < cv.h and not cv.get(nx, ny)[3]:
                open_hits.append((x, y))
                break
    out: List[Problem] = _group("outline-open", "외곽선이 끊겼다", open_hits)
    out += _palette_and_count(cv, recipe, ctx)
    # 외톨이: 이웃 8칸에 외곽선 아닌 불투명 칸이 하나도 없는 한 점 섬.
    # 모양 안의 한 점 무늬(씨앗, 손잡이)는 이웃이 있으므로 걸리지 않는다.
    out += _group("orphan-pixel", "떨어진 외톨이 점", [
        (x, y) for x, y, c in _opaque(cv)
        if c != ol and not any(cv.get(x + dx, y + dy)[3] and cv.get(x + dx, y + dy) != ol for dx, dy in N8)])
    if not is_part:
        out += _cells(cv, ctx)
        out += _group("overflow", "내용이 캔버스 가장자리에 닿았다", [
            (x, y) for x, y, c in _opaque(cv) if c != ol and (x in (0, cv.w - 1) or y in (0, cv.h - 1))])
        bb = cv.bbox()
        if bb is not None:
            x0, _, x1, y1 = bb
            if y1 != cv.h or abs(x0 - (cv.w - x1)) > 1:
                out.append(Problem("not-bottom-center", f"아래 가운데가 아니다(bbox {bb})"))
    return [p for p in out if p.code not in allow]


def _period(cv: Canvas, horizontal: bool) -> Optional[int]:
    """그 축으로 그림이 자기 자신과 겹치는 부분이 같아지는 가장 작은 밀기 p(1..크기/2). 없으면 None.
    p가 절반을 넘으면 겹치는 부분이 너무 좁아 아무 그림이나 우연히 같아지므로(한두 점 얼룩) 주기로 치지 않는다."""
    n = cv.w if horizontal else cv.h
    for p in range(1, n // 2 + 1):
        if horizontal:
            same = all(cv.get(x, y) == cv.get(x + p, y) for y in range(cv.h) for x in range(n - p))
        else:
            same = all(cv.get(x, y) == cv.get(x, y + p) for x in range(cv.w) for y in range(n - p))
        if same:
            return p
    return None


def _seam(cv: Canvas) -> List[Problem]:
    """축마다 그림이 자기 자신을 p칸 민 것과 겹친 곳이 모두 같아지는 가장 작은 p(무늬 주기, 크기의 절반까지)를 찾는다.
    p가 있는데 그 축의 길이가 p로 나누어떨어지지 않으면 이음매에서 무늬가 잘리므로 tile-seam 하나를 낸다(주기가 없으면 통과)."""
    bad = []
    for horizontal, name in ((True, "가로"), (False, "세로")):
        p = _period(cv, horizontal)
        n = cv.w if horizontal else cv.h
        if p is not None and n % p:
            bad.append(f"{name} 무늬 주기 {p}px이 {n}px에 나누어떨어지지 않는다")
    return [Problem("tile-seam", ", ".join(bad))] if bad else []


def lint_tile(cv: Canvas, recipe: dict, ctx, allow: FrozenSet[str] = frozenset()) -> List[Problem]:
    """타일: 외곽선이 없고 한 점 얼룩이 있으므로 외곽선·외톨이 검사는 하지 않는다."""
    out = _palette_and_count(cv, recipe, ctx) + _cells(cv, ctx) + _seam(cv)
    return [p for p in out if p.code not in allow]
