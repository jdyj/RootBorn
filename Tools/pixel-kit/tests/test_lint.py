import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.lint import Problem, lint_sprite, lint_tile
from pixelkit.parts import load_part_allowances, load_parts
from pixelkit.recipes import Ctx
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE = Style.load(str(ROOT / "art" / "kit" / "style.json"))
PARTS_DIR = ROOT / "art" / "kit" / "parts"
OL = STYLE.outline
A = STYLE.color("bread.1")
B = STYLE.color("bread.2")
C = STYLE.color("bread.0")
CTX = Ctx(STYLE, {})
REC = {"id": "t", "kind": "furniture"}


def good_sprite():
    """32x32, 안쪽 6x6 상자에 외곽선, 아래 가운데."""
    cv = Canvas(32, 32)
    cv.rect(13, 25, 6, 6, A)
    cv.rect(14, 26, 4, 4, B)
    cv.outline(OL)
    return cv


def codes(problems):
    return sorted({p.code for p in problems})


class SpriteLintTests(unittest.TestCase):
    def test_clean(self):
        self.assertEqual(lint_sprite(good_sprite(), REC, CTX), [])

    def test_outline_open(self):
        cv = good_sprite()
        cv.px(16, 24, (0, 0, 0, 0))  # 위 외곽선 한 칸 뚫기
        ps = lint_sprite(cv, REC, CTX)
        self.assertEqual(codes(ps), ["outline-open"])
        self.assertIsInstance(ps[0], Problem)
        self.assertEqual((ps[0].x, ps[0].y), (16, 25))

    def test_off_palette(self):
        cv = good_sprite()
        cv.px(15, 27, (1, 2, 3, 255))
        cv.px(16, 27, (1, 2, 3, 255))
        self.assertEqual(codes(lint_sprite(cv, REC, CTX)), ["off-palette"])

    def test_too_many_colors(self):
        cv = Canvas(32, 32)
        keys = [k for k in STYLE._colors][:30]
        for i, k in enumerate(keys):
            cv.px(13 + i % 6, 26 + i // 6, STYLE.color(k))
        cv.outline(OL)
        ps = lint_sprite(cv, REC, CTX, allow=frozenset({"orphan-pixel"}))
        self.assertEqual(codes(ps), ["too-many-colors"])

    def test_orphan_pixel(self):
        cv = good_sprite()
        cv.px(15, 27, C)
        self.assertEqual(codes(lint_sprite(cv, REC, CTX)), ["orphan-pixel"])

    def test_allow_skips_code(self):
        cv = good_sprite()
        cv.px(15, 27, C)
        self.assertEqual(lint_sprite(cv, REC, CTX, allow=frozenset({"orphan-pixel"})), [])

    def test_not_cell_multiple(self):
        cv = Canvas(30, 32)
        cv.rect(12, 25, 6, 6, A)
        cv.rect(13, 26, 4, 4, B)
        cv.outline(OL)
        self.assertEqual(codes(lint_sprite(cv, REC, CTX)), ["not-cell-multiple"])

    def test_overflow(self):
        cv = good_sprite()
        cv.rect(0, 20, 1, 3, A)  # 왼쪽 가장자리에 외곽선 아닌 색
        cv.rect(1, 20, 1, 3, A)
        cv.outline(OL)
        self.assertIn("overflow", codes(lint_sprite(cv, REC, CTX)))

    def test_not_bottom_center_up(self):
        cv = Canvas(32, 32)
        cv.rect(13, 10, 6, 6, A)
        cv.rect(14, 11, 4, 4, B)
        cv.outline(OL)
        self.assertEqual(codes(lint_sprite(cv, REC, CTX)), ["not-bottom-center"])

    def test_not_bottom_center_side(self):
        cv = Canvas(32, 32)
        cv.rect(4, 25, 6, 6, A)
        cv.rect(5, 26, 4, 4, B)
        cv.outline(OL)
        self.assertEqual(codes(lint_sprite(cv, REC, CTX)), ["not-bottom-center"])

    def test_part_kind_uses_part_limit_and_skips_canvas_checks(self):
        cv = Canvas(8, 8)
        cv.rect(1, 1, 3, 3, A)
        cv.outline(OL)
        self.assertEqual(lint_sprite(cv, {"id": "p", "kind": "part"}, CTX), [])


def stripes(period, vertical=True):
    cv = Canvas(32, 32)
    for y in range(32):
        for x in range(32):
            t = (x if vertical else y) % period
            cv.px(x, y, A if t < period // 2 else B)
    return cv


TILE = {"id": "tile", "kind": "tile"}


class TileLintTests(unittest.TestCase):
    def test_seam_broken_10px_period(self):
        self.assertEqual(codes(lint_tile(stripes(10), TILE, CTX)), ["tile-seam"])
        self.assertEqual(codes(lint_tile(stripes(10, False), TILE, CTX)), ["tile-seam"])

    def test_seam_ok_16px_period(self):
        self.assertEqual(lint_tile(stripes(16), TILE, CTX), [])
        self.assertEqual(lint_tile(stripes(16, False), TILE, CTX), [])

    def test_flat_tile_ok(self):
        cv = Canvas(32, 32)
        cv.rect(0, 0, 32, 32, A)
        self.assertEqual(lint_tile(cv, TILE, CTX), [])

    def test_tile_speckles_allowed(self):
        cv = Canvas(32, 32)
        cv.rect(0, 0, 32, 32, A)
        cv.px(5, 5, B)
        cv.px(20, 9, B)
        self.assertEqual(lint_tile(cv, TILE, CTX), [])

    def test_tile_off_palette_and_cells(self):
        cv = Canvas(30, 32)
        cv.rect(0, 0, 30, 32, (9, 9, 9, 255))
        self.assertEqual(codes(lint_tile(cv, TILE, CTX)), ["not-cell-multiple", "off-palette"])


class RealPartsLintTests(unittest.TestCase):
    def test_all_real_parts_pass(self):
        parts = load_parts(str(PARTS_DIR), STYLE)
        allow = load_part_allowances(str(PARTS_DIR))
        ctx = Ctx(STYLE, parts)
        for pid, cv in parts.items():
            ps = lint_sprite(cv, {"id": pid, "kind": "part"}, ctx, allow=allow.get(pid, frozenset()))
            self.assertEqual(ps, [], f"{pid}: {[(p.code, p.x, p.y) for p in ps]}")


if __name__ == "__main__":
    unittest.main()
