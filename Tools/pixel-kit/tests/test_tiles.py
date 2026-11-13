import unittest
from pathlib import Path

from pixelkit.lint import _period, lint_tile
from pixelkit.recipes import Ctx, load_recipes, render
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE = Style.load(str(ROOT / "art" / "kit" / "style.json"))
RECIPES = load_recipes(str(ROOT / "art" / "kit" / "recipes" / "bakery"))
CTX = Ctx(STYLE, {})


def ramp(style, prefix):
    out, i = [], 0
    while f"{prefix}.{i}" in style._colors:
        out.append(style.color(f"{prefix}.{i}"))
        i += 1
    return out

SIZES = {"bakery.tile.floor": (32, 32), "bakery.tile.wall": (32, 64), "bakery.tile.sidewalk": (32, 32),
         "bakery.tile.grass": (32, 32)}


class TileTests(unittest.TestCase):
    def test_tiles(self):
        for rid, size in SIZES.items():
            with self.subTest(rid):
                cv = render(RECIPES[rid], CTX)
                self.assertEqual((cv.w, cv.h), size)
                self.assertEqual(lint_tile(cv, RECIPES[rid], CTX), [])
                transparent = [1 for y in range(cv.h) for x in range(cv.w) if cv.get(x, y)[3] == 0]
                self.assertEqual(len(transparent), 0)
                self.assertLessEqual(len(cv.colors() - {STYLE.outline}), 12)

    def test_deterministic(self):
        for rid in SIZES:
            a = render(RECIPES[rid], CTX).to_image().tobytes()
            b = render(RECIPES[rid], CTX).to_image().tobytes()
            self.assertEqual(a, b)

    def test_floor_checker_takes_one_ramp_per_cell_kind(self):
        """밝은 칸과 어두운 칸이 각자의 줄(바탕 = 단계 2)을 쓴다. 같은 줄을 두 번 쓰지 않는다."""
        spec = {"id": "t.floor", "kind": "tile", "part": "tile.floor-checker", "art_cells": [1, 1],
                "colors": ["floor.tile.light", "floor.tile.warm"]}
        cv = render(spec, CTX)
        light, warm = (ramp(STYLE, p) for p in spec["colors"])
        self.assertEqual(cv.get(8, 8), light[2])
        self.assertEqual(cv.get(24, 24), light[2])
        self.assertEqual(cv.get(24, 8), warm[2])
        self.assertEqual(cv.get(8, 24), warm[2])

    def test_every_checker_cell_has_a_lit_top_left_edge_and_a_dark_joint(self):
        spec = RECIPES["bakery.tile.floor"]
        cv = render(spec, CTX)
        ramps = [ramp(STYLE, p) for p in spec["colors"]]
        for ty in range(2):
            for tx in range(2):
                r = ramps[(tx + ty) % 2]
                ox, oy = tx * 16, ty * 16
                with self.subTest(cell=(tx, ty)):
                    self.assertEqual(cv.get(ox + 5, oy), r[3])          # 윗모서리 빛
                    self.assertEqual(cv.get(ox, oy + 5), r[3])          # 왼모서리 빛
                    self.assertEqual(cv.get(ox + 5, oy + 15), r[0])     # 줄눈
                    self.assertEqual(cv.get(ox + 15, oy + 5), r[0])

    def test_floor_speckles_do_not_sit_at_the_same_spot_in_every_cell(self):
        """잔 점이 칸마다 같은 자리면 깔았을 때 점선처럼 읽힌다."""
        spec = RECIPES["bakery.tile.floor"]
        cv = render(spec, CTX)
        ramps = [ramp(STYLE, p) for p in spec["colors"]]
        spots = []
        for ty in range(2):
            for tx in range(2):
                speck = ramps[(tx + ty) % 2][1]
                ox, oy = tx * 16, ty * 16
                spots.append(frozenset((x - ox, y - oy) for y in range(oy, oy + 16) for x in range(ox, ox + 16)
                                       if cv.get(x, y) == speck))
        self.assertTrue(all(spots))
        self.assertEqual(len(set(spots)), 4)

    def test_grass_does_not_repeat_every_16px(self):
        """16px 무늬를 네 번 되풀이하면 칸 격자가 보인다. 풀밭은 32px 안에서 되풀이하지 않는다."""
        cv = render(RECIPES["bakery.tile.grass"], CTX)
        self.assertIsNone(_period(cv, True))
        self.assertIsNone(_period(cv, False))

    def test_wall_cap_then_outline_line(self):
        cv = render(RECIPES["bakery.tile.wall"], CTX)
        self.assertEqual(cv.get(5, 6), STYLE.outline)
        self.assertNotEqual(cv.get(5, 5), STYLE.outline)


if __name__ == "__main__":
    unittest.main()
