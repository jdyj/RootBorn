"""RGBA 픽셀 캔버스. 외곽선은 outline()이 마지막에 한 번 입힌다."""
from typing import Optional, Set, Tuple

from PIL import Image

RGBA = Tuple[int, int, int, int]
CLEAR: RGBA = (0, 0, 0, 0)


class Canvas:
    def __init__(self, w: int, h: int):
        self.w = w
        self.h = h
        self._p = [CLEAR] * (w * h)

    def px(self, x: int, y: int, rgba: RGBA) -> None:
        if 0 <= x < self.w and 0 <= y < self.h:
            self._p[y * self.w + x] = tuple(rgba)

    def get(self, x: int, y: int) -> RGBA:
        if 0 <= x < self.w and 0 <= y < self.h:
            return self._p[y * self.w + x]
        return CLEAR

    def rect(self, x: int, y: int, w: int, h: int, rgba: RGBA) -> None:
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self.px(xx, yy, rgba)

    def hline(self, x: int, y: int, length: int, rgba: RGBA) -> None:
        self.rect(x, y, length, 1, rgba)

    def vline(self, x: int, y: int, length: int, rgba: RGBA) -> None:
        self.rect(x, y, 1, length, rgba)

    def paste(self, other: "Canvas", x: int, y: int) -> None:
        for oy in range(other.h):
            for ox in range(other.w):
                sr, sg, sb, sa = other._p[oy * other.w + ox]
                if sa == 0:
                    continue
                tx, ty = x + ox, y + oy
                if not (0 <= tx < self.w and 0 <= ty < self.h):
                    continue
                if sa == 255:
                    self._p[ty * self.w + tx] = (sr, sg, sb, 255)
                    continue
                dr, dg, db, da = self._p[ty * self.w + tx]
                oa = sa + da * (255 - sa) // 255
                if oa == 0:
                    continue
                def mix(s, d):
                    return (s * sa + d * da * (255 - sa) // 255) // oa
                self._p[ty * self.w + tx] = (mix(sr, dr), mix(sg, dg), mix(sb, db), oa)

    def darken(self, x: int, y: int, w: int, h: int, k: float) -> None:
        """장면에 깔리는 그림자 전용. 색을 k배로 곱한다."""
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                r, g, b, a = self.get(xx, yy)
                if a:
                    self.px(xx, yy, (int(r * k), int(g * k), int(b * k), a))

    def outline(self, rgba: RGBA) -> "Canvas":
        """투명이고 4방향 이웃 중 외곽선색이 아닌 불투명이 있는 칸을 모아 한꺼번에 칠한다(대각선은 채우지 않는다).
        이미 외곽선색인 칸 옆에는 두 번째 고리를 만들지 않는다(미리 외곽선을 입힌 부품을 붙여도 1px)."""
        todo = []
        for y in range(self.h):
            for x in range(self.w):
                if self.get(x, y)[3]:
                    continue
                if any(self.get(x + dx, y + dy)[3] and self.get(x + dx, y + dy) != rgba for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    todo.append((x, y))
        for x, y in todo:
            self.px(x, y, rgba)
        return self

    def bbox(self) -> Optional[Tuple[int, int, int, int]]:
        xs = [i % self.w for i, p in enumerate(self._p) if p[3]]
        if not xs:
            return None
        ys = [i // self.w for i, p in enumerate(self._p) if p[3]]
        return (min(xs), min(ys), max(xs) + 1, max(ys) + 1)

    def colors(self) -> Set[RGBA]:
        return {p for p in self._p if p[3]}

    def to_image(self) -> Image.Image:
        img = Image.new("RGBA", (self.w, self.h))
        img.putdata(self._p)
        return img

    @staticmethod
    def from_image(img: Image.Image) -> "Canvas":
        img = img.convert("RGBA")
        cv = Canvas(img.width, img.height)
        cv._p = [tuple(p) for p in img.getdata()]
        return cv

    def save(self, path: str) -> None:
        # PNG 메타데이터(시각 등)를 넣지 않아 같은 입력이 같은 바이트가 되게 한다.
        self.to_image().save(path, format="PNG", optimize=False)
