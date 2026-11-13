import copy
import re
import unittest
from pathlib import Path

from pixelkit.lint import lint_tile
from pixelkit.parts import load_parts
from pixelkit.rand import rnd
from pixelkit.recipes import Ctx, load_recipes, render
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
KIT = ROOT / "art" / "kit"
STYLE = Style.load(str(KIT / "style.json"))
PARTS = load_parts(str(KIT / "parts"), STYLE)
RECIPES = load_recipes(str(KIT / "recipes" / "bakery"))
MAKERS = ROOT / "Tools" / "pixel-kit" / "pixelkit" / "makers"


def ctx():
    return Ctx(STYLE, PARTS, [])


class SharedHelperTests(unittest.TestCase):
    def test_no_maker_imports_private_names_from_another_maker(self):
        for name in ("building.py", "furniture.py", "tiles.py"):
            src = (MAKERS / name).read_text(encoding="utf-8")
            self.assertIsNone(re.search(r"from \.(furniture|building|tiles) import [^\n]*\b_\w", src), name)

    def test_common_exposes_public_helpers(self):
        from pixelkit.makers import common
        for n in ("rid", "col", "part", "local", "merge", "report", "pile", "streaks", "ramp", "ramp_steps"):
            self.assertTrue(callable(getattr(common, n)), n)


class SeedTests(unittest.TestCase):
    def test_seed_zero_is_todays_value(self):
        for k in range(16):
            for salt in range(16):
                self.assertEqual(rnd(k, salt, 100), rnd(k, salt, 100, seed=0))

    def test_seed_one_changes_some_values(self):
        diff = [1 for i in range(256) if rnd(i, 3, 1000) != rnd(i, 3, 1000, seed=1)]
        self.assertTrue(diff)

    def test_building_seed_changes_wall_and_is_deterministic(self):
        base = RECIPES["bakery.exterior.flat"]
        seeded = copy.deepcopy(base)
        seeded["seed"] = 1
        a = render(base, ctx()).to_image().tobytes()
        b = render(seeded, ctx()).to_image().tobytes()
        c = render(copy.deepcopy(seeded), ctx()).to_image().tobytes()
        self.assertNotEqual(a, b)
        self.assertEqual(b, c)

    def test_seed_that_is_not_an_int_raises_recipe_error_with_the_id(self):
        from pixelkit.recipes import RecipeError
        for rid in ("bakery.exterior.flat", "bakery.tile.grass", "bakery.shelf"):
            for bad in ("1", 1.5, True, None):
                with self.subTest(rid=rid, bad=bad), self.assertRaises(RecipeError) as e:
                    spec = copy.deepcopy(RECIPES[rid])
                    spec["seed"] = bad
                    render(spec, ctx())
                self.assertTrue(str(e.exception).startswith(f"{rid}:"), str(e.exception))


class TileSizeTests(unittest.TestCase):
    def test_floor_checker_fills_art_cells(self):
        spec = copy.deepcopy(RECIPES["bakery.tile.floor"])
        spec["art_cells"] = [2, 2]
        c = ctx()
        cv = render(spec, c)
        self.assertEqual((cv.w, cv.h), (64, 64))
        self.assertEqual(sum(1 for y in range(64) for x in range(64) if cv.get(x, y)[3] == 0), 0)
        self.assertEqual(lint_tile(cv, spec, c), [])

    def test_transparent_tile_is_reported_once(self):
        from pixelkit.canvas import Canvas
        cv = Canvas(32, 32)
        cv.rect(0, 0, 32, 31, STYLE.color("floor.tile.light.2"))
        found = [p for p in lint_tile(cv, {"id": "t", "kind": "tile"}, ctx()) if p.code == "tile-transparent"]
        self.assertEqual(len(found), 1)
        self.assertEqual((found[0].x, found[0].y), (0, 31))
        self.assertIn("32", found[0].message)


if __name__ == "__main__":
    unittest.main()
