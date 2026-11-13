import os
import tempfile
import unittest

from pixelkit.canvas import Canvas

RED = (200, 40, 40, 255)
BLUE = (40, 40, 200, 255)
OUT = (58, 56, 78, 255)
CLEAR = (0, 0, 0, 0)


class CanvasTests(unittest.TestCase):
    def test_outline_fills_exactly_the_4_neighbours(self):
        cv = Canvas(8, 8)
        cv.rect(3, 3, 2, 2, RED)
        cv.outline(OUT)
        ring = {(x, y) for y in range(8) for x in range(8) if cv.get(x, y) == OUT}
        self.assertEqual(ring, {(3, 2), (4, 2), (2, 3), (5, 3), (2, 4), (5, 4), (3, 5), (4, 5)})
        self.assertEqual(cv.get(3, 3), RED)

    def test_shape_touching_the_canvas_edge_keeps_its_pixels_and_does_not_raise(self):
        cv = Canvas(4, 4)
        cv.rect(0, 0, 2, 2, RED)
        cv.outline(OUT)
        self.assertEqual(cv.get(0, 0), RED)
        self.assertEqual(cv.get(1, 1), RED)
        self.assertEqual(cv.get(2, 0), OUT)
        self.assertEqual(cv.get(0, 2), OUT)
        self.assertEqual(cv.get(2, 2), CLEAR)

    def test_px_and_get_outside_are_safe(self):
        cv = Canvas(2, 2)
        cv.px(-1, 0, RED)
        cv.px(5, 5, RED)
        self.assertEqual(cv.get(-1, 0), CLEAR)
        self.assertEqual(cv.get(9, 9), CLEAR)
        self.assertIsNone(cv.bbox())

    def test_paste_is_alpha_composite_and_clips_outside(self):
        base = Canvas(4, 4)
        base.rect(0, 0, 4, 4, RED)
        top = Canvas(2, 2)
        top.rect(0, 0, 2, 2, BLUE)
        top.px(1, 1, (0, 0, 0, 0))
        base.paste(top, 3, 3)       # 대부분 밖
        self.assertEqual(base.get(3, 3), BLUE)
        base.paste(top, 0, 0)
        self.assertEqual(base.get(1, 1), RED)   # 투명 칸은 아래가 보인다
        self.assertEqual(base.get(0, 0), BLUE)
        half = Canvas(1, 1)
        half.px(0, 0, (0, 0, 0, 128))
        b2 = Canvas(1, 1)
        b2.px(0, 0, (255, 255, 255, 255))
        b2.paste(half, 0, 0)
        r, g, b, a = b2.get(0, 0)
        self.assertEqual(a, 255)
        self.assertTrue(120 <= r <= 135)

    def test_hline_vline_bbox_colors(self):
        cv = Canvas(6, 6)
        cv.hline(1, 2, 3, RED)
        cv.vline(5, 0, 6, BLUE)
        self.assertEqual(cv.bbox(), (1, 0, 6, 6))
        self.assertEqual(cv.colors(), {RED, BLUE})

    def test_darken_changes_only_opaque_pixels_in_area(self):
        cv = Canvas(2, 1)
        cv.px(0, 0, (100, 100, 100, 255))
        cv.darken(0, 0, 2, 1, 0.5)
        self.assertEqual(cv.get(0, 0), (50, 50, 50, 255))
        self.assertEqual(cv.get(1, 0), CLEAR)

    def test_save_then_from_image_roundtrips_bytes(self):
        cv = Canvas(8, 8)
        cv.rect(2, 2, 3, 3, RED)
        cv.outline(OUT)
        with tempfile.TemporaryDirectory() as d:
            a, b, c = (os.path.join(d, n) for n in ("a.png", "b.png", "c.png"))
            cv.save(a)
            cv.save(b)
            with open(a, "rb") as fa, open(b, "rb") as fb:
                self.assertEqual(fa.read(), fb.read())
            from PIL import Image
            back = Canvas.from_image(Image.open(a))
            self.assertEqual(back.colors(), cv.colors())
            back.save(c)
            with open(a, "rb") as fa, open(c, "rb") as fc:
                self.assertEqual(fa.read(), fc.read())


if __name__ == "__main__":
    unittest.main()
