"""furniture.prop: 점 부품 하나(몸)를 그대로 세운 가구 + 그 위에 얹는 부품(선택)."""
import unittest
from pathlib import Path

from pixelkit.lint import lint_sprite
from pixelkit.parts import load_parts
from pixelkit.recipes import Ctx, RecipeError, ghost_ctx, render, render_with_spec
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE = Style.load(str(ROOT / "art" / "kit" / "style.json"))
PARTS = load_parts(str(ROOT / "art" / "kit" / "parts"), STYLE)


def ctx():
    return Ctx(STYLE, PARTS, [])


def recipe(**kw):
    r = {"id": "t.prop", "kind": "furniture", "part": "furniture.prop", "cells": [1, 1], "art_cells": [1, 1],
         "body": "prop.basket"}
    r.update(kw)
    return r


class PropFurnitureTests(unittest.TestCase):
    def test_body_part_stands_bottom_center_on_a_cell_canvas_and_is_lint_clean(self):
        c = ctx()
        r = recipe()
        cv = render(r, c)
        self.assertEqual((cv.w, cv.h), (32, 32))
        part = PARTS["prop.basket"]
        self.assertEqual(cv.bbox()[3] - cv.bbox()[1], part.bbox()[3] - part.bbox()[1])
        self.assertEqual(lint_sprite(cv, r, c, body=render(r, ghost_ctx(c))), [])
        self.assertEqual(c.problems, [])

    def test_items_are_pasted_at_their_place_on_the_body(self):
        plain = render(recipe(body="kitchen.plates", art_cells=[1, 1]), ctx())
        topped = render(recipe(body="kitchen.plates", items=[{"part": "kitchen.bowl", "x": 0, "y": -6}]), ctx())
        self.assertNotEqual(plain.to_image().tobytes(), topped.to_image().tobytes())
        self.assertIn(STYLE.color("leaf.1"), topped.colors())

    def test_item_outside_the_canvas_reports_slot_clipped(self):
        c = ctx()
        render(recipe(items=[{"part": "kitchen.bowl", "x": 30, "y": 0}]), c)
        self.assertEqual([p.code for p in c.problems], ["slot-clipped"])

    def test_missing_or_unknown_body_and_bad_items_raise(self):
        for bad in (recipe(body=None), recipe(body="no.such"), recipe(items=[{"part": "prop.clock"}]),
                    recipe(items="x")):
            with self.subTest(bad):
                with self.assertRaises(RecipeError):
                    render(bad, ctx())

    def test_layout_names_the_body_size(self):
        _, spec = render_with_spec(recipe(), ctx())
        part = PARTS["prop.basket"]
        self.assertEqual(spec["_layout"]["size"], [part.w - 2, part.h - 2])


if __name__ == "__main__":
    unittest.main()
