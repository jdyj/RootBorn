"""장면에 깔리는 그림자(설계서 6.6절). 색을 곱하는 곳은 여기(Canvas.darken)뿐이다.

그림자는 물체 바로 밑의 짧은 띠 하나다. 열마다 그 열에서 물체가 끝나는 곳 바로 아래에서 시작하므로,
다리·바퀴·발로 떠 있는 몸통 밑(다리 사이)이 가장 어둡다. 그림자는 이미 장면에 있는 것만 어둡게 하고,
물체가 차지하는 열 밖으로 나가지 않는다. 그림자를 먼저 깔고 그 위에 물체를 붙인다.
"""
from typing import List, Optional

from .canvas import Canvas
from .style import Style

FEET_ROWS = 4      # 물체의 맨 아래 몇 줄에 칸이 있는 열을 "발이 닿는 범위"로 보는가


def _lowest(sprite: Canvas, x: int) -> Optional[int]:
    """그 열의 가장 아래 불투명 칸의 줄. 비었으면 None."""
    for y in range(sprite.h - 1, -1, -1):
        if sprite.get(x, y)[3]:
            return y
    return None


def _feet(sprite: Canvas) -> List[int]:
    return [x for x in range(sprite.w)
            if any(sprite.get(x, y)[3] for y in range(max(0, sprite.h - FEET_ROWS), sprite.h))]


def under_shadow(scene: Canvas, sprite: Canvas, x: int, y: int, style: Style, rows: int = 3) -> None:
    """바닥에 선 물체(sprite를 scene의 x, y에 붙일 것)의 그림자.

    발 범위(맨 아래 FEET_ROWS줄에 칸이 있는 첫 열 ~ 끝 열) 안에서만 깐다.
    - 안쪽 열: 그 열에서 물체가 끝나는 곳 바로 아래 ~ 그림의 맨 아랫줄까지 shadow.under,
      그 밑으로 rows - 1줄 shadow.front.
    - 양 끝 열: 그림 밑으로 rows - 2줄 shadow.edge(한 단계 옅고 한 줄 짧다).
    """
    feet = _feet(sprite)
    if not feet:
        return
    first, last = feet[0], feet[-1]
    sh = style.shadow
    bottom = sprite.h                         # 그림 바로 밑 줄(그림 좌표)
    for i in range(first + 1, last):
        low = _lowest(sprite, i)
        if low is None:
            continue
        if low < bottom - 1:
            scene.darken(x + i, y + low + 1, 1, bottom - 1 - low, sh["under"])
        scene.darken(x + i, y + bottom, 1, rows - 1, sh["front"])
    for i in {first, last}:
        scene.darken(x + i, y + bottom, 1, rows - 2, sh["edge"])


def hung_shadow(scene: Canvas, sprite: Canvas, x: int, y: int, style: Style) -> None:
    """벽에 걸린 것의 그림자: 열마다 그 열의 가장 아래 칸 바로 밑 2줄 shadow.hung(벽 밑 줄보다 짙다)."""
    for i in range(sprite.w):
        low = _lowest(sprite, i)
        if low is not None:
            scene.darken(x + i, y + low + 1, 1, 2, style.shadow["hung"])
