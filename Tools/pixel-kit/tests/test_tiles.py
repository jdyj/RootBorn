import unittest
from pathlib import Path

from pixelkit.lint import lint_tile
from pixelkit.recipes import Ctx, load_recipes, render
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE = Style.load(str(ROOT / "art" / "kit" / "style.json"))
RECIPES = load_recipes(str(ROOT / "art" / "kit" / "recipes" / "bakery"))
CTX = Ctx(STYLE, {})

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

    def test_wall_cap_then_outline_line(self):
        cv = render(RECIPES["bakery.tile.wall"], CTX)
        self.assertEqual(cv.get(5, 6), STYLE.outline)
        self.assertNotEqual(cv.get(5, 5), STYLE.outline)


if __name__ == "__main__":
    unittest.main()
