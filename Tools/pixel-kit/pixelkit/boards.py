"""견본판: 그림 여러 장을 번호·id 라벨과 함께 한 장에 늘어놓는다(보기용 PNG, 게임에 안 들어간다).

확대는 정수 배 최근접만. 라벨(id)은 Pillow 기본 글꼴. 한글 이름(title)과 컨셉(note)은 한글 글꼴이 있을 때만 그린다 —
글꼴 파일은 환경 변수 ROOTBORN_FONT가 먼저, 다음은 글꼴 이름 후보(Pillow가 시스템 글꼴 폴더에서 이름으로 찾는다)이고,
못 찾으면 title·note를 그리지 않는다(글꼴 경로를 코드에 적지 않는다).
PNG에는 시각 같은 메타데이터를 넣지 않아 같은 입력이 같은 바이트가 된다.
"""
import os
from dataclasses import dataclass
from functools import lru_cache
from typing import List, Optional

from PIL import Image, ImageDraw, ImageFont

from .canvas import Canvas

BACKGROUND = (176, 176, 176)
LABEL_COLOR = (20, 20, 20)
RED_FRAME = (220, 30, 30)
PAD = 8              # 칸 안쪽 여백(px)
FRAME = 3            # 빨간 테두리 두께(px)
LABEL_H = 18         # 라벨 줄 높이(px)
TITLE_H = 22         # 한글 이름 줄 높이(px)
NOTE_H = 17          # 컨셉 줄 높이(px)
TITLE_SIZE = 16
NOTE_SIZE = 14
TEXT_GAP = 5         # 한글 글자가 있을 때 글자와 그림 사이 틈(px)
MIN_TEXT_W = 200     # 컨셉을 줄바꿈하는 최소 폭(px) — 그림이 이보다 좁아도 글자는 이만큼 쓴다
TITLE_COLOR = (10, 10, 10)
NOTE_COLOR = (60, 60, 60)
FONT_ENV = "ROOTBORN_FONT"
FONT_NAMES = ("malgun.ttf", "NanumGothic.ttf", "NotoSansCJK-Regular.ttc", "NotoSansCJKkr-Regular.otf",
              "NotoSansKR-Regular.ttf", "AppleGothic.ttf")
MAX_WIDTH = 1600     # 이보다 넓어지면 줄바꿈


@dataclass
class Entry:
    label: str       # 그대로 찍는다(번호와 id는 부르는 쪽이 정한다)
    canvas: Canvas
    bad: bool = False
    title: str = ""  # 한글 이름(한글 글꼴이 있을 때만 그린다)
    note: str = ""   # 컨셉 한 줄(그림 폭에 맞게 줄바꿈, 한글 글꼴이 있을 때만 그린다)


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


def find_korean_font(size: int) -> Optional[ImageFont.FreeTypeFont]:
    """한글을 그릴 글꼴(size px). 환경 변수 ROOTBORN_FONT의 파일이 먼저, 다음은 FONT_NAMES를 이름으로. 없으면 None."""
    return _load_font(os.environ.get(FONT_ENV, ""), size)


@lru_cache(maxsize=None)
def _load_font(env_path: str, size: int):
    for name in ([env_path] if env_path else []) + list(FONT_NAMES):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return None


def _wrap(text: str, font, width: int, probe) -> List[str]:
    """공백에서 끊고, 한 낱말이 폭보다 길면 글자 단위로 끊는다(localization.md의 줄바꿈 규칙과 같다)."""
    lines: List[str] = []
    cur = ""
    for word in text.split(" "):
        trial = word if not cur else cur + " " + word
        if probe.textlength(trial, font=font) <= width:
            cur = trial
            continue
        if cur:
            lines.append(cur)
            cur = ""
        for ch in word:
            if cur and probe.textlength(cur + ch, font=font) > width:
                lines.append(cur)
                cur = ""
            cur += ch
    if cur:
        lines.append(cur)
    return lines


def board_image(entries: List[Entry], scale: int) -> Image.Image:
    """왼쪽에서 오른쪽으로 놓다가 MAX_WIDTH를 넘으면 줄바꿈. 칸 너비는 그림·글자 중 넓은 쪽,
    줄 높이는 그 줄에서 가장 큰 것. 글자(id, 한글 이름, 컨셉)는 그림 바로 위, 그림은 줄 바닥에 맞춘다."""
    font = _font()
    title_font = find_korean_font(TITLE_SIZE) if any(e.title for e in entries) else None
    note_font = find_korean_font(NOTE_SIZE) if any(e.note for e in entries) else None
    probe = ImageDraw.Draw(Image.new("RGB", (1, 1)))
    cells = []
    blocks = []      # 항목마다 [(글, 글꼴, 색, 줄 높이)]
    for e in entries:
        w, h = e.canvas.w * scale, e.canvas.h * scale
        text_w = max(w, MIN_TEXT_W)
        lines = [(e.label, font, LABEL_COLOR, LABEL_H)]
        if title_font and e.title:
            lines.append((e.title, title_font, TITLE_COLOR, TITLE_H))
        if note_font and e.note:
            lines += [(t, note_font, NOTE_COLOR, NOTE_H) for t in _wrap(e.note, note_font, text_w, probe)]
        if len(lines) > 1:
            lines.append(("", font, LABEL_COLOR, TEXT_GAP))
        blocks.append(lines)
        tw = max(int(probe.textlength(t, font=f)) for t, f, _, _ in lines)
        if len(lines) > 1:
            tw = max(tw, text_w)
        cells.append((max(w, tw) + PAD * 2, h + sum(l[3] for l in lines)))
    rows: List[List[int]] = [[]]
    x = 0
    for i, (cw, _) in enumerate(cells):
        if rows[-1] and x + cw > MAX_WIDTH:
            rows.append([])
            x = 0
        rows[-1].append(i)
        x += cw
    row_w = [sum(cells[i][0] for i in r) for r in rows]
    row_h = [max([cells[i][1] for i in r] + [1]) + PAD * 2 for r in rows]
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
            ty = py - sum(l[3] for l in blocks[i])
            for text, f, color, lh in blocks[i]:
                draw.text((ox + PAD, ty), text, fill=color, font=f)
                ty += lh
            img.paste(big, (ox + (cw - big.width) // 2, py), big)
            ox += cw
        oy += rh
    return img
