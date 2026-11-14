"""돌봄 소품: 침대, 칸막이 커튼, 점 부품 쌓기(화분·링거대처럼 점 부품 하나나 몇 개로 끝나는 소품).

가구와 같은 면 나누기(설계서 6.6절): 넓고 밝은 윗면 / 한 단계 어두운 앞면 / 튀어나온 판 밑에 짙은 줄 하나.
왼쪽이 밝고 오른쪽이 어둡다. 그늘은 팔레트의 다른 단계다. 좌표는 이 그림(외곽선 전) 기준이고
자리는 spec["_layout"]에 적는다(테스트가 읽는다).
"""
from ..canvas import Canvas
from . import RecipeError, register
from .common import art_cells_in, part, ramp_of, rid


# ---------- 침대 ----------

BED_CELLS = ((1, 2), (3, 3))   # art_cells [폭, 높이]: 바닥 두 칸 + 머리판이 위 칸으로 솟는다
BED_HEAD = 16                  # 머리판 줄 수(그 밑에 외곽선 한 줄)
BED_FLOOR_CELLS = 2            # 바닥에서 차지하는 깊이(칸)


@register("furniture.bed")
def bed(spec: dict, ctx) -> Canvas:
    """위에서 내려다본 침대. 머리판(금속 틀) → 베개 → 시트 → 접힌 이불 → 앞으로 늘어진 이불 → 앞 난간 → 바퀴.
    폭은 art_cells 폭 × 32 - 2. 바닥에서 두 칸 깊이를 덮고 머리판만 위 칸으로 솟는다."""
    cw, _ = art_cells_in(spec, *BED_CELLS)
    cell = ctx.style.cell
    frame = ramp_of(spec, ctx, "frame", "metal", 3)          # 0 짙음 1 바탕 2 밝음
    linen = ramp_of(spec, ctx, "linen", "ceramic", 3)        # 0 그늘 1 시트 2 베개·접힌 단
    blanket = ramp_of(spec, ctx, "blanket", "wall.tile.blue", 4)
    ol = ctx.style.outline
    w = cw * cell - 2
    h = BED_HEAD + 1 + BED_FLOOR_CELLS * cell - 2            # 외곽선 두 줄을 더하면 머리판 위까지 정확히
    cv = Canvas(w, h)

    # 머리판: 틀(위·왼쪽 밝게, 아래·오른쪽 어둡게) 안에 판 하나
    cv.rect(0, 0, w, BED_HEAD, frame[1])
    cv.hline(0, 0, w, frame[2])
    cv.vline(0, 0, BED_HEAD, frame[2])
    cv.vline(w - 1, 0, BED_HEAD, frame[0])
    cv.rect(3, 3, w - 6, BED_HEAD - 7, linen[1])             # 판(밝은 면)
    cv.hline(3, 3, w - 6, linen[0])                          # 틀 밑 짙은 줄
    cv.hline(0, BED_HEAD - 2, w, frame[0])                   # 머리판 아래 끝(매트리스 밑으로 들어감)
    cv.hline(0, BED_HEAD - 1, w, frame[0])
    cv.hline(0, BED_HEAD, w, ol)

    top = BED_HEAD + 1
    front = h - 12                                           # 앞면(이불 늘어진 자리)이 시작하는 줄
    cv.rect(0, top, w, front - top, linen[1])                # 시트(넓은 윗면)
    cv.vline(w - 1, top, front - top, linen[0])              # 오른쪽은 한 단계 어둡게
    # 베개: 밝은 판, 아래 짙은 줄
    px0, px1 = 3, w - 4
    cv.rect(px0, top + 2, px1 - px0 + 1, 9, linen[2])
    cv.hline(px0, top + 10, px1 - px0 + 1, linen[0])
    cv.vline(px1, top + 2, 9, linen[0])
    # 이불: 접힌 단(시트 색 띠) → 이불 면, 오른쪽 어둡게, 주름 몇 줄
    by = top + 17
    cv.rect(0, by, w, 4, linen[2])
    cv.hline(0, by + 3, w, linen[0])
    cv.rect(0, by + 4, w, front - by - 4, blanket[2])
    cv.vline(0, by + 4, front - by - 4, blanket[3])
    cv.vline(w - 1, by + 4, front - by - 4, blanket[1])
    for fy, fx, fl in ((by + 12, 4, w // 3), (by + 20, w // 2, w // 3), (by + 29, 6, w // 4)):
        if fy < front - 1:
            cv.hline(fx, fy, fl, blanket[1])
    # 앞면: 이불이 앞으로 늘어진 면(한 단계 어둡게), 그 밑 난간, 바퀴
    cv.rect(0, front, w, 5, blanket[1])
    cv.hline(0, front, w, blanket[3])                        # 매트리스 모서리
    cv.hline(0, front + 5, w, ol)
    cv.rect(0, front + 6, w, 3, frame[1])                    # 앞 난간
    cv.hline(0, front + 6, w, frame[2])
    cv.hline(0, front + 8, w, frame[0])
    for x in (1, w - 5):                                     # 바퀴(몸통 밑이 가장 어둡다 — 그림자는 장면이 깐다)
        cv.rect(x, front + 9, 4, 3, frame[0])
    spec["_layout"] = {"size": [w, h], "head": [0, BED_HEAD], "top": [top, front], "front": [front, h],
                       "floor_cells": BED_FLOOR_CELLS}
    return cv


# ---------- 칸막이 커튼 ----------

SCREEN_CELLS = ((1, 3), (2, 2))
SCREEN_H = 52              # 그림 높이(외곽선 전): 위 난간 + 천 + 아래 난간 + 다리
SCREEN_PANEL = 10          # 천 한 폭의 목표 폭(px). 폭 수 = 그림 폭 / 이 값(반올림)


@register("prop.screen")
def screen(spec: dict, ctx) -> Canvas:
    """바퀴 달린 칸막이 커튼(정면). 금속 틀 사이에 천 몇 폭, 폭마다 주름 줄. 폭은 art_cells 폭 × 32 - 2."""
    cw, _ = art_cells_in(spec, *SCREEN_CELLS)
    frame = ramp_of(spec, ctx, "frame", "metal", 3)
    fabric = ramp_of(spec, ctx, "fabric", "wall.mint", 3)    # 0 주름 그늘 1 천 2 빛
    ol = ctx.style.outline
    w, h = cw * ctx.style.cell - 2, SCREEN_H
    cv = Canvas(w, h)
    rail0, rail1 = 0, 41                                     # 위·아래 난간 줄
    cv.rect(0, rail0, w, 3, frame[1])
    cv.hline(0, rail0, w, frame[2])
    cv.hline(0, rail0 + 2, w, frame[0])
    n = max(1, round(w / SCREEN_PANEL))
    posts = [round(i * (w - 2) / n) for i in range(n + 1)]   # 기둥(2px)의 왼쪽 x
    panels = []
    for i in range(n):
        x0, x1 = posts[i] + 2, posts[i + 1] - 1              # 천 [x0, x1]
        cv.rect(x0, 3, x1 - x0 + 1, rail1 - 3, fabric[1])
        cv.hline(x0, 3, x1 - x0 + 1, fabric[0])              # 난간 밑 짙은 줄
        for x in range(x0 + 1, x1, 3):                       # 주름: 밝은 줄과 그늘 줄
            cv.vline(x, 5, rail1 - 6, fabric[2])
            if x + 1 < x1:
                cv.vline(x + 1, 5, rail1 - 6, fabric[0])
        panels.append([x0, x1])
    for i, x in enumerate(posts):                            # 기둥: 왼쪽 밝게, 오른쪽 어둡게
        cv.vline(x, rail0, rail1 + 3, frame[2] if i < n else frame[1])
        cv.vline(x + 1, rail0, rail1 + 3, frame[0])
    cv.rect(0, rail1, w, 3, frame[1])                        # 아래 난간
    cv.hline(0, rail1, w, frame[2])
    cv.hline(0, rail1 + 2, w, frame[0])
    for x in (posts[0], posts[-1]):                          # 다리와 바퀴
        cv.rect(x, rail1 + 3, 2, 5, frame[1])
        cv.rect(max(0, x - 1), h - 3, 4, 3, frame[0])
    spec["_layout"] = {"size": [w, h], "panels": panels}
    return cv


# ---------- 점 부품 쌓기 ----------

@register("prop.stack")
def stack(spec: dict, ctx) -> Canvas:
    """점 부품을 아래부터 쌓아 한 소품으로(화분 위 잎, 받침 위 기둥). 부품마다 가운데 맞춤.
    부품은 외곽선을 입은 채라 위아래로 맞닿는 외곽선 한 줄씩을 걷어 붙인다."""
    ids = spec.get("stack")
    if not isinstance(ids, list) or not ids or not all(isinstance(i, str) for i in ids):
        raise RecipeError(f"{rid(spec)}: stack은 점 부품 id 목록(아래부터, 하나 이상)이어야 한다")
    parts = [part(spec, ctx, i) for i in ids]
    w = max(p.w for p in parts) - 2
    h = sum(p.h - 2 for p in parts)
    cv = Canvas(w + 2, h + 2)                                # 부품 외곽선까지 담는 캔버스
    y = h + 2
    for p in parts:                                          # 아래 부품부터: 위 부품이 아래 부품의 윗 외곽선을 덮는다
        y -= p.h - 2
        cv.paste(p, (w + 2 - p.w) // 2, y - 1)
    out = Canvas(w, h)                                       # 렌더러가 외곽선을 다시 입힌다(이미 외곽선 색인 칸 옆에는 두 겹이 안 생긴다)
    out.paste(_crop(cv, 1, 1, w, h), 0, 0)
    spec["_layout"] = {"size": [w, h], "parts": list(ids)}
    return out


def _crop(cv: Canvas, x: int, y: int, w: int, h: int) -> Canvas:
    out = Canvas(w, h)
    for yy in range(h):
        for xx in range(w):
            out.px(xx, yy, cv.get(x + xx, y + yy))
    return out
