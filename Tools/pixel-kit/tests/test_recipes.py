import shutil
import tempfile
import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.recipes import (Ctx, RecipeError, get_maker, load_recipes, register,
                              render, render_with_spec)
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE = Style.load(str(ROOT / "art" / "kit" / "style.json"))
FIX = Path(__file__).resolve().parent / "fixtures"


@register("test.box")
def _box(spec, ctx):
    cv = Canvas(20, 30)
    cv.rect(0, 0, 20, 30, ctx.style.color(spec.get("color", "bread.1")))
    spec["_layout"] = {"w": 20}
    return cv


@register("test.huge")
def _huge(spec, ctx):
    cv = Canvas(40, 70)
    cv.rect(0, 0, 40, 70, ctx.style.color("bread.1"))
    return cv


def ctx():
    return Ctx(STYLE, {})


def recipe(**kw):
    r = {"id": "t.x", "kind": "prop", "part": "test.box", "art_cells": [1, 2]}
    r.update(kw)
    return r


class RecipeTests(unittest.TestCase):
    def test_size_and_bottom_center(self):
        cv = render(recipe(), ctx())
        self.assertEqual((cv.w, cv.h), (32, 64))
        x0, y0, x1, y1 = cv.bbox()
        self.assertEqual(y1, 64)
        self.assertLessEqual(abs(x0 - (32 - x1)), 1)

    def test_outline_added_and_tile_has_none(self):
        self.assertIn(STYLE.outline, render(recipe(), ctx()).colors())
        t = render(recipe(kind="tile", art_cells=[1, 1]), ctx())
        self.assertNotIn(STYLE.outline, t.colors())

    def test_unknown_part(self):
        with self.assertRaises(RecipeError) as e:
            render(recipe(part="nope.x"), ctx())
        self.assertTrue(str(e.exception).startswith("t.x"))
        self.assertIn("nope.x", str(e.exception))
        self.assertIn("test.box", str(e.exception))
        with self.assertRaises(RecipeError):
            get_maker("nope.x")

    def test_bad_art_cells(self):
        for bad in (None, [1], [1, "2"], [0, 1], [1.5, 1]):
            r = recipe()
            if bad is None:
                del r["art_cells"]
            else:
                r["art_cells"] = bad
            with self.assertRaises(RecipeError) as e:
                render(r, ctx())
            self.assertTrue(str(e.exception).startswith("t.x"))

    def test_pixels_for_buildings(self):
        r = recipe(kind="building")
        del r["art_cells"]
        r["pixels"] = [64, 96]
        cv = render(r, ctx())
        self.assertEqual((cv.w, cv.h), (64, 96))
        r["pixels"] = [50, 96]
        with self.assertRaises(RecipeError):
            render(r, ctx())
        r["art_cells"] = [1, 1]
        r["pixels"] = [64, 96]
        with self.assertRaises(RecipeError):
            render(r, ctx())

    def test_state_override(self):
        r = recipe(color="bread.1", states={"empty": {"color": "bread.2"}})
        base = render(r, ctx())
        empty = render(r, ctx(), state="empty")
        self.assertNotEqual(base.to_image().tobytes(), empty.to_image().tobytes())
        self.assertIn(STYLE.color("bread.2"), empty.colors())
        with self.assertRaises(RecipeError) as e:
            render(r, ctx(), state="full")
        self.assertTrue(str(e.exception).startswith("t.x"))

    def test_deterministic(self):
        r = recipe()
        self.assertEqual(render(r, ctx()).to_image().tobytes(),
                         render(r, ctx()).to_image().tobytes())

    def test_original_not_mutated_and_spec_exposed(self):
        r = recipe()
        cv, spec = render_with_spec(r, ctx())
        self.assertNotIn("_layout", r)
        self.assertIn("_layout", spec)
        self.assertIsInstance(cv, Canvas)

    def test_oversize_clips_without_error(self):
        cv = render(recipe(part="test.huge"), ctx())
        self.assertEqual((cv.w, cv.h), (32, 64))

    def test_ctx_part(self):
        c = Ctx(STYLE, {"a": Canvas(1, 1)})
        self.assertEqual(c.problems, [])
        self.assertIs(c.part("a"), c.parts["a"])
        with self.assertRaises(RecipeError):
            c.part("zz")

    def test_load_recipes(self):
        rs = load_recipes(str(FIX / "recipes"))
        self.assertEqual(set(rs), {"t.box", "t.other"})
        tmp = Path(tempfile.mkdtemp())
        try:
            shutil.copytree(FIX / "recipes", tmp / "r")
            shutil.copy(FIX / "recipes_dup.json", tmp / "r" / "dup.json")
            with self.assertRaises(RecipeError) as e:
                load_recipes(str(tmp / "r"))
            self.assertIn("t.box", str(e.exception))
        finally:
            shutil.rmtree(tmp)


if __name__ == "__main__":
    unittest.main()
