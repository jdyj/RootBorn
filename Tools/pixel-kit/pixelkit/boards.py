"""견본판: 그림 여러 장을 번호·id 라벨과 함께 한 장에 늘어놓는다(보기용 PNG, 게임에 안 들어간다).

확대는 정수 배 최근접만. 라벨은 Pillow 기본 글꼴(글꼴 파일 경로를 적지 않는다).
PNG에는 시각 같은 메타데이터를 넣지 않아 같은 입력이 같은 바이트가 된다.
"""
from dataclasses import dataclass
from typing import List

from PIL import Image, ImageDraw, ImageFont

from .canvas import Canvas

BACKGROUND = (176, 176, 176)
LABEL_COLOR = (20, 20, 20)
RED_FRAME = (220, 30, 30)
PAD = 8              # 칸 안쪽 여백(px)
FRAME = 3            # 빨간 테두리 두께(px)
LABEL_H = 18         # 라벨 줄 높이(px)
MAX_WIDTH = 1600     # 이보다 넓어지면 줄바꿈


@dataclass
class Entry:
    label: str       # 그대로 찍는다(번호와 id는 부르는 쪽이 정한다)
    canvas: Canvas
    bad: bool = False


def enlarge(img: Image.Image, n: int) -> Image.Image:
    """정수 n배 최근접 확대."""
    if n < 1:
        raise ValueError("배율은 1 이상의 정수여야 한다")
    return img.resize((img.width * n, img.height * n), Image.NEAREST)


def _font():
    try:
        return ImageFont.load_default(size=14)  # Pillow 기본 글꼴의 큰 글자(글꼴 파일 경로 없음)
    except TypeError:
        return ImageFont.load_default()


def board_image(entries: List[Entry], scale: int) -> Image.Image:
    """왼쪽에서 오른쪽으로 놓다가 MAX_WIDTH를 넘으면 줄바꿈. 칸 너비는 그림·라벨 중 넓은 쪽,
    줄 높이는 그 줄에서 가장 큰 것. 라벨은 그림 바로 위, 그림은 줄 바닥에 맞춘다."""
    font = _font()
    probe = ImageDraw.Draw(Image.new("RGB", (1, 1)))
    cells = []
    for e in entries:
        w, h = e.canvas.w * scale, e.canvas.h * scale
        lw = int(probe.textlength(e.label, font=font))
        cells.append((max(w, lw) + PAD * 2, h))
    rows: List[List[int]] = [[]]
    x = 0
    for i, (cw, _) in enumerate(cells):
        if rows[-1] and x + cw > MAX_WIDTH:
            rows.append([])
            x = 0
        rows[-1].append(i)
        x += cw
    row_w = [sum(cells[i][0] for i in r) for r in rows]
    row_h = [max([cells[i][1] for i in r] + [1]) + PAD * 2 + LABEL_H for r in rows]
    img = Image.new("RGB", (max(row_w + [1]), sum(row_h)), BACKGROUND)
    draw = ImageDraw.Draw(img)
    oy = 0
    for r, rh in zip(rows, row_h):
        ox = 0
        for i in r:
            e, (cw, h) = entries[i], cells[i]
            if e.bad:
                for k in range(FRAME):
                    draw.rectangle([ox + k, oy + k, ox + cw - 1 - k, oy + rh - 1 - k], outline=RED_FRAME)
            big = enlarge(e.canvas.to_image(), scale)
            py = oy + rh - PAD - big.height
            draw.text((ox + PAD, py - LABEL_H), e.label, fill=LABEL_COLOR, font=font)
            img.paste(big, (ox + (cw - big.width) // 2, py), big)
            ox += cw
        oy += rh
    return img
