"""사물함·칸 사물함(makers/storage.py)과 사진 게시판·훈련 타이어·구급 상자(makers/objects.py).

그림의 특정 픽셀보다 구성(크기, 쓰는 색 줄, 문·사진 수, 검사 통과, 잘못된 명세는 RecipeError)을 본다."""
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


def ramp_colors(prefix):
    return {STYLE.color(k) for k in STYLE._colors if k.rpartition(".")[0] == prefix}


def spec(part, art_cells, **kw):
    r = {"id": "t." + part, "kind": "furniture", "part": part, "cells": [art_cells[0], 1], "art_cells": art_cells}
    r.update(kw)
    return r


def clean(test, r):
    c = ctx()
    cv = render(r, c)
    body = render(r, ghost_ctx(c))
    test.assertEqual(lint_sprite(cv, r, c, body=body), [])
    test.assertEqual(c.problems, [])
    test.assertEqual((cv.w, cv.h), (r["art_cells"][0] * 32, r["art_cells"][1] * 32))
    return cv


class LockerTests(unittest.TestCase):
    def test_door_count_comes_from_width(self):
        for cells, doors in (([1, 3], 1), ([2, 3], 3), ([3, 3], 4)):
            with self.subTest(cells=cells):
                r = spec("furniture.locker", cells, material="wood.school")
                _, s = render_with_spec(r, ctx())
                self.assertEqual(len(s["_layout"]["doors"]), doors)
                clean(self, r)

    def test_material_comes_from_the_recipe(self):
        cv = clean(self, spec("furniture.locker", [2, 3], material="wood.school"))
        self.assertTrue(cv.colors() & ramp_colors("wood.school"))
        self.assertEqual(cv.colors() & ramp_colors("wood.warm"), set())

    def test_metal_locker_has_streaks(self):
        r = spec("furniture.locker", [2, 3], material="locker.steel")
        a = render(r, ctx())
        b = render(dict(r, streaks=False), ctx())
        self.assertNotEqual(a.to_image().tobytes(), b.to_image().tobytes())

    def test_bad_size_raises_with_id(self):
        with self.assertRaises(RecipeError) as e:
            render(spec("furniture.locker", [2, 2]), ctx())
        self.assertTrue(str(e.exception).startswith("t.furniture.locker:"))


class CubbyTests(unittest.TestCase):
    def test_grid_sets_door_count_and_is_clean(self):
        for grid, n in (([3, 2], 6), ([2, 2], 4), ([3, 1], 3)):
            with self.subTest(grid=grid):
                r = spec("furniture.cubby", [2, 2], grid=grid, material="wood.school")
                _, s = render_with_spec(r, ctx())
                self.assertEqual(len(s["_layout"]["doors"]), n)
                clean(self, r)

    def test_bad_grid_raises(self):
        for g in ([0, 2], [7, 2], "3x2", [3]):
            with self.subTest(grid=g):
                with self.assertRaises(RecipeError):
                    render(spec("furniture.cubby", [2, 2], grid=g), ctx())


class PhotoBoardTests(unittest.TestCase):
    PHOTOS = [["glass.window.2", "leaf.1"], ["glass.window.1", "red.1"],
              ["yellow.1", "grass.2"], ["glass.window.2", "roof.teal.3"]]

    def test_photo_count_and_colors_come_from_the_recipe(self):
        r = spec("furniture.photo-board", [1, 2], photos=self.PHOTOS)
        _, s = render_with_spec(r, ctx())
        self.assertEqual(len(s["_layout"]["photos"]), 4)
        cv = clean(self, r)
        for k in ("leaf.1", "red.1", "roof.teal.3"):
            self.assertIn(STYLE.color(k), cv.colors())

    def test_bad_photos_raise(self):
        for p in ([], [["leaf.1"]], [["nope.1", "leaf.1"]], self.PHOTOS + [["leaf.1", "red.1"]]):
            with self.subTest(photos=p):
                with self.assertRaises(RecipeError):
                    render(spec("furniture.photo-board", [1, 2], photos=p), ctx())


class TireTests(unittest.TestCase):
    def test_tire_is_clean_has_a_see_through_hole_and_uses_its_rubber(self):
        r = spec("furniture.tire", [1, 1], rubber="metal.dark")
        cv, s = render_with_spec(r, ctx())
        clean(self, r)
        hx, hy = s["_layout"]["hole"]
        ox = (cv.w - (s["_layout"]["size"][0] + 2)) // 2 + 1
        oy = cv.h - (s["_layout"]["size"][1] + 2) + 1
        self.assertEqual(cv.get(hx + ox, hy + oy)[3], 0)          # 구멍 아래쪽으로 바닥이 보인다
        self.assertTrue(cv.colors() & ramp_colors("metal.dark"))


class FirstAidKitTests(unittest.TestCase):
    def test_kit_is_clean_and_has_its_cross(self):
        r = spec("furniture.first-aid-kit", [1, 1], case="ceramic", cross="red")
        cv = clean(self, r)
        self.assertIn(STYLE.color("red.1"), cv.colors())
        self.assertTrue(cv.colors() & ramp_colors("ceramic"))


if __name__ == "__main__":
    unittest.main()
