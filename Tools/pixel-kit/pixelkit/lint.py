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
    out: List[Problem] = []
    for x, y, c in _opaque(cv):
        if c not in style.palette:
            out.append(Problem("off-palette", f"팔레트 밖 색 {c}", x, y))
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
    out: List[Problem] = []
    for x, y, c in _opaque(cv):
        if c == ol:
            continue
        # 캔버스 가장자리는 overflow가 맡는다: 안쪽 이웃만 본다.
        for dx, dy in N4:
            nx, ny = x + dx, y + dy
            if 0 <= nx < cv.w and 0 <= ny < cv.h and not cv.get(nx, ny)[3]:
                out.append(Problem("outline-open", "외곽선이 끊겼다", x, y))
                break
    out += _palette_and_count(cv, recipe, ctx)
    for x, y, c in _opaque(cv):
        if c != ol and not any(cv.get(x + dx, y + dy) == c for dx, dy in N8):
            out.append(Problem("orphan-pixel", "같은 색 이웃이 없는 외톨이 점", x, y))
    if not is_part:
        out += _cells(cv, ctx)
        for x, y, c in _opaque(cv):
            if c != ol and (x in (0, cv.w - 1) or y in (0, cv.h - 1)):
                out.append(Problem("overflow", "내용이 캔버스 가장자리에 닿았다", x, y))
        bb = cv.bbox()
        if bb is not None:
            x0, _, x1, y1 = bb
            if y1 != cv.h or abs(x0 - (cv.w - x1)) > 1:
                out.append(Problem("not-bottom-center", f"아래 가운데가 아니다(bbox {bb})"))
    return [p for p in out if p.code not in allow]


def _runs(line: list) -> list:
    runs = []
    for c in line:
        if runs and runs[-1][0] == c:
            runs[-1][1] += 1
        else:
            runs.append([c, 1])
    return runs


def _line_broken(line: list) -> bool:
    """한 줄을 이어 붙일 때 이음매에서 무늬 주기가 끊기는가.
    이음매를 건너는 같은 색 덩어리(양 끝 합)가 안쪽 덩어리 중 가장 긴 것보다 길거나,
    이음매가 색 경계인데 끝 덩어리가 안쪽 덩어리 중 가장 짧은 것보다 짧으면 끊긴 것."""
    runs = _runs(line)
    if len(runs) <= 2:
        return False
    inner = [n for _, n in runs[1:-1]]
    if runs[0][0] == runs[-1][0]:
        return runs[0][1] + runs[-1][1] > max(inner)
    return runs[0][1] < min(inner) or runs[-1][1] < min(inner)


def _seam(cv: Canvas) -> List[Problem]:
    out = []
    rows = [[cv.get(x, y) for x in range(cv.w)] for y in range(cv.h)]
    cols = [[cv.get(x, y) for y in range(cv.h)] for x in range(cv.w)]
    if sum(_line_broken(r) for r in rows) * 2 > cv.h:
        out.append(Problem("tile-seam", "가로로 깔면 이음매에서 무늬 주기가 끊긴다"))
    if sum(_line_broken(c) for c in cols) * 2 > cv.w:
        out.append(Problem("tile-seam", "세로로 깔면 이음매에서 무늬 주기가 끊긴다"))
    return out


def lint_tile(cv: Canvas, recipe: dict, ctx, allow: FrozenSet[str] = frozenset()) -> List[Problem]:
    """타일: 외곽선이 없고 한 점 얼룩이 있으므로 외곽선·외톨이 검사는 하지 않는다."""
    out = _palette_and_count(cv, recipe, ctx) + _cells(cv, ctx) + _seam(cv)
    return [p for p in out if p.code not in allow]
