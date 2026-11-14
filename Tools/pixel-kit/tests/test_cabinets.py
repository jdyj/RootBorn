"""서랍장(furniture.drawers)과 강의대(furniture.lectern): 크기, 면, 검사 통과, 잘못된 명세."""
import copy
import unittest
from pathlib import Path

from pixelkit.lint import lint_sprite
from pixelkit.parts import load_parts
from pixelkit.recipes import Ctx, RecipeError, ghost_ctx, render_with_spec
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
KIT = ROOT / "art" / "kit"
STYLE = Style.load(str(KIT / "style.json"))
PARTS = load_parts(str(KIT / "parts"), STYLE)
DRAWERS = {"id": "t.drawers", "kind": "furniture", "part": "furniture.drawers", "cells": [2, 1], "art_cells": [2, 2],
           "material": "wood.warm"}
LECTERN = {"id": "t.lectern", "kind": "furniture", "part": "furniture.lectern", "cells": [1, 1], "art_cells": [1, 2],
           "material": "wood.warm"}


def spec(base, **kw):
    r = copy.deepcopy(base)
    r.update(kw)
    return r


def draw(recipe):
    c = Ctx(STYLE, PARTS, [])
    cv, s = render_with_spec(recipe, c)
    body, _ = render_with_spec(recipe, ghost_ctx(c))
    return cv, s["_layout"], list(c.problems) + lint_sprite(cv, s, c, layout=s["_layout"], body=body)


class DrawersTests(unittest.TestCase):
    def test_sizes_and_lint_clean(self):
        for cells, size in (([1, 2], (32, 64)), ([2, 2], (64, 64)), ([3, 2], (96, 64))):
            for rows in (2, 3, 4):
                with self.subTest(cells=cells, rows=rows):
                    cv, lay, problems = draw(spec(DRAWERS, art_cells=cells, rows=rows))
                    self.assertEqual((cv.w, cv.h), size)
                    self.assertEqual([(p.code, p.message) for p in problems], [])
                    self.assertEqual(lay["size"][0], cells[0] * 32 - 2)

    def test_drawer_grid_follows_rows_and_width(self):
        _, lay, _ = draw(spec(DRAWERS, rows=3))
        self.assertEqual(len(lay["drawers"]), 3 * 2)          # 두 칸 폭이면 두 줄기
        _, lay1, _ = draw(spec(DRAWERS, art_cells=[1, 2], rows=4))
        self.assertEqual(len(lay1["drawers"]), 4)
        for x, y, w, h in lay["drawers"]:
            self.assertGreaterEqual(w, 10)
            self.assertGreaterEqual(h, 4)
        tops = sorted({d[1] for d in lay["drawers"]})
        self.assertGreater(tops[0], lay["top_face"][1])        # 서랍은 윗면 밑 앞면에

    def test_material_ramp_comes_from_the_recipe(self):
        cv, _, _ = draw(spec(DRAWERS, material="wood.school"))
        self.assertIn(STYLE.color("wood.school.4"), cv.colors())
        self.assertNotIn(STYLE.color("wood.warm.4"), cv.colors())

    def test_bad_values_raise(self):
        for kw in ({"rows": 1}, {"rows": 6}, {"rows": "3"}, {"art_cells": [4, 2]}, {"art_cells": [2, 3]}):
            with self.subTest(kw=kw):
                with self.assertRaises(RecipeError) as e:
                    draw(spec(DRAWERS, **kw))
                self.assertTrue(str(e.exception).startswith("t.drawers"))


class LecternTests(unittest.TestCase):
    def test_size_and_lint_clean(self):
        cv, lay, problems = draw(LECTERN)
        self.assertEqual((cv.w, cv.h), (32, 64))
        self.assertEqual([(p.code, p.message) for p in problems], [])

    def test_reading_board_is_higher_than_a_desk_and_wider_than_the_post(self):
        _, lay, _ = draw(LECTERN)
        self.assertGreater(lay["board_height"], 25 + 8)         # 서서 쓰는 높이: 책상 윗면(25)보다 높다
        bx, by, bw, bh = lay["board"]
        px, pw = lay["post"]
        self.assertGreater(bw, pw)
        self.assertGreaterEqual(lay["base"][1], pw)

    def test_top_items_sit_on_the_board_and_off_board_is_clipped(self):
        cv, _, problems = draw(spec(LECTERN, top_items=[{"part": "desk.book-open", "x": 4}]))
        self.assertEqual([(p.code, p.message) for p in problems], [])
        c = Ctx(STYLE, PARTS, [])
        render_with_spec(spec(LECTERN, top_items=[{"part": "desk.book-open", "x": 20}]), c)
        self.assertIn("slot-clipped", [p.code for p in c.problems])

    def test_bad_values_raise(self):
        for kw in ({"art_cells": [2, 2]}, {"top_items": [{"part": "desk.book-open"}]}):
            with self.subTest(kw=kw):
                with self.assertRaises(RecipeError):
                    draw(spec(LECTERN, **kw))


if __name__ == "__main__":
    unittest.main()
