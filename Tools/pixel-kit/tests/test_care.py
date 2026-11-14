"""돌봄 소품(병원 등): 침대(furniture.bed), 칸막이 커튼(prop.screen), 점 부품 쌓기(prop.stack)."""
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

BED = {"id": "t.bed", "kind": "prop", "part": "furniture.bed", "art_cells": [1, 3]}
SCREEN = {"id": "t.screen", "kind": "prop", "part": "prop.screen", "art_cells": [1, 2]}
STACK = {"id": "t.stack", "kind": "prop", "part": "prop.stack", "art_cells": [1, 2],
         "stack": ["prop.plant", "prop.clock"]}


def ctx():
    return Ctx(STYLE, PARTS, [])


def spec(base, **kw):
    r = copy.deepcopy(base)
    r.update(kw)
    return r


def draw(recipe):
    c = ctx()
    cv, s = render_with_spec(recipe, c)
    body, _ = render_with_spec(recipe, ghost_ctx(c))
    return cv, s, list(c.problems) + lint_sprite(cv, s, c, layout=s.get("_layout"), body=body)


def used(cv):
    return {cv.get(x, y) for y in range(cv.h) for x in range(cv.w) if cv.get(x, y)[3]}


class BedTests(unittest.TestCase):
    def test_default_bed_is_clean_and_fills_two_floor_cells(self):
        cv, s, problems = draw(BED)
        self.assertEqual([p.code for p in problems], [])
        self.assertEqual((cv.w, cv.h), (32, 96))
        x0, y0, x1, y1 = cv.bbox()
        self.assertEqual(y1, 96)
        self.assertGreater(y1 - y0, 64)            # 바닥 두 칸(64px) + 머리판이 위 칸으로 솟는다
        self.assertEqual(s["_layout"]["floor_cells"], 2)

    def test_wide_bed(self):
        cv, s, problems = draw(spec(BED, art_cells=[2, 3]))
        self.assertEqual([p.code for p in problems], [])
        self.assertEqual(s["_layout"]["size"][0], 62)

    def test_ramps_come_from_spec_keys(self):
        cv, _, _ = draw(spec(BED, blanket="box.kraft"))
        self.assertIn(STYLE.color("box.kraft.2"), used(cv))
        cv, _, _ = draw(BED)
        self.assertIn(STYLE.color("metal.1"), used(cv))     # 기본 틀은 metal

    def test_out_of_range_size_is_rejected(self):
        for cells in ([3, 3], [1, 2], [1, 4]):
            with self.subTest(cells=cells), self.assertRaises(RecipeError):
                draw(spec(BED, art_cells=cells))

    def test_short_ramp_is_rejected(self):
        with self.assertRaises(RecipeError):
            draw(spec(BED, blanket="glass.streak"))         # 단계 하나뿐


class ScreenTests(unittest.TestCase):
    def test_default_screen_is_clean_and_bottom_aligned(self):
        cv, s, problems = draw(SCREEN)
        self.assertEqual([p.code for p in problems], [])
        self.assertEqual((cv.w, cv.h), (32, 64))
        self.assertEqual(cv.bbox()[3], 64)

    def test_panel_count_grows_with_width(self):
        _, one, _ = draw(SCREEN)
        _, three, problems = draw(spec(SCREEN, art_cells=[3, 2]))
        self.assertEqual([p.code for p in problems], [])
        self.assertGreater(len(three["_layout"]["panels"]), len(one["_layout"]["panels"]))

    def test_fabric_key(self):
        cv, _, _ = draw(spec(SCREEN, fabric="book.teal"))
        self.assertIn(STYLE.color("book.teal.1"), used(cv))

    def test_out_of_range_size_is_rejected(self):
        for cells in ([4, 2], [1, 1], [1, 3]):
            with self.subTest(cells=cells), self.assertRaises(RecipeError):
                draw(spec(SCREEN, art_cells=cells))


class StackTests(unittest.TestCase):
    def test_parts_are_stacked_bottom_first_and_centered(self):
        cv, s, problems = draw(STACK)
        self.assertEqual([p.code for p in problems], [])
        plant, clock = PARTS["prop.plant"], PARTS["prop.clock"]
        self.assertEqual(s["_layout"]["size"][1], plant.h + clock.h - 4)   # 부품 외곽선(위·아래 1px)을 걷어 맞붙인다

    def test_unknown_part_and_empty_stack_are_rejected(self):
        for bad in ([], ["no.such-part"], "prop.plant", [3]):
            with self.subTest(stack=bad), self.assertRaises(RecipeError):
                draw(spec(STACK, stack=bad))


if __name__ == "__main__":
    unittest.main()
