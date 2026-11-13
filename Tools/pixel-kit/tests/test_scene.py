import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.parts import load_parts
from pixelkit.recipes import Ctx, RecipeError, load_recipes, render
from pixelkit.scene import build_scene, scene_items
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE = Style.load(str(ROOT / "art" / "kit" / "style.json"))
PARTS = load_parts(str(ROOT / "art" / "kit" / "parts"), STYLE)
RECIPES = load_recipes(str(ROOT / "art" / "kit" / "recipes" / "bakery"))


def ctx():
    return Ctx(STYLE, PARTS, [])


def floor_scene(w, h, place=(), hang=(), extra=None):
    s = {"id": "t.scene", "kind": "scene", "size": [w, h],
         "fill": [{"tile": "bakery.tile.floor", "rect": [0, 0, w, h]}],
         "place": list(place), "hang": list(hang)}
    s.update(extra or {})
    return s


def opaque(cv):
    return [(x, y) for y in range(cv.h) for x in range(cv.w) if cv.get(x, y)[3]]


class SceneTests(unittest.TestCase):
    def test_scene_canvas_is_cell_sized_and_places_by_cell(self):
        shelf = render(RECIPES["bakery.shelf"], ctx())
        self.assertEqual((shelf.w, shelf.h), (64, 96))
        scene = build_scene(floor_scene(4, 5, [{"id": "bakery.shelf", "cell": [1, 3]}]), RECIPES, ctx())
        self.assertEqual((scene.w, scene.h), (4 * 32, 5 * 32))
        # 왼쪽 아래 = (32, 4*32-1) → 왼쪽 위 = (32, 32)
        for x, y in opaque(shelf):
            self.assertEqual(scene.get(32 + x, 32 + y), shelf.get(x, y))

    def test_dx_dy_shift_in_pixels_and_state_is_used(self):
        empty = render(RECIPES["bakery.shelf"], ctx(), "empty")
        scene = build_scene(floor_scene(4, 5, [{"id": "bakery.shelf", "state": "empty", "cell": [1, 3],
                                                "dx": 3, "dy": -2}]), RECIPES, ctx())
        for x, y in opaque(empty):
            self.assertEqual(scene.get(35 + x, 30 + y), empty.get(x, y))

    def test_part_bottom_left_goes_to_the_cell_bottom_left(self):
        plant = PARTS["prop.plant"]
        scene = build_scene(floor_scene(3, 3, [{"part": "prop.plant", "cell": [1, 1], "dx": 2}]), RECIPES, ctx())
        x0, y0 = 32 + 2, 2 * 32 - plant.h
        for x, y in opaque(plant):
            self.assertEqual(scene.get(x0 + x, y0 + y), plant.get(x, y))

    def test_fill_repeats_the_tile_over_the_rect(self):
        tile = render(RECIPES["bakery.tile.floor"], ctx())
        scene = build_scene(floor_scene(3, 2), RECIPES, ctx())
        for cx in range(3):
            for cy in range(2):
                self.assertEqual(scene.get(cx * 32 + 5, cy * 32 + 7), tile.get(5, 7))

    def test_fill_rect_must_hold_whole_tiles(self):
        s = floor_scene(2, 3)
        s["fill"] = [{"tile": "bakery.tile.wall", "rect": [0, 0, 2, 3]}]       # 64px 벽 타일이 3칸에 안 맞는다
        with self.assertRaises(RecipeError) as e:
            build_scene(s, RECIPES, ctx())
        self.assertTrue(str(e.exception).startswith("t.scene"))

    def test_fill_wall_must_be_a_bool(self):
        s = floor_scene(2, 2)
        s["fill"] = [{"tile": "bakery.tile.floor", "rect": [0, 0, 2, 2], "wall": "yes"}]
        with self.assertRaises(RecipeError) as e:
            build_scene(s, RECIPES, ctx())
        self.assertTrue(str(e.exception).startswith("t.scene"))

    def test_wall_line_gets_two_darker_rows(self):
        s = floor_scene(2, 4)
        s["fill"] = [{"tile": "bakery.tile.wall", "rect": [0, 0, 2, 2], "wall": True},
                     {"tile": "bakery.tile.floor", "rect": [0, 2, 2, 2]}]
        scene = build_scene(s, RECIPES, ctx())
        floor = render(RECIPES["bakery.tile.floor"], ctx())
        for y in (64, 65):
            self.assertLess(sum(scene.get(5, y)[:3]), sum(floor.get(5, y - 64)[:3]))
        self.assertEqual(scene.get(5, 66), floor.get(5, 2))

    def test_scene_with_unknown_recipe_id_raises_recipe_error_naming_it(self):
        with self.assertRaises(RecipeError) as e:
            build_scene(floor_scene(3, 3, [{"id": "bakery.nope", "cell": [0, 2]}]), RECIPES, ctx())
        self.assertTrue(str(e.exception).startswith("t.scene"))
        self.assertIn("bakery.nope", str(e.exception))

    def test_unknown_part_and_unknown_tile_raise_naming_them(self):
        for s, name in ((floor_scene(3, 3, [{"part": "prop.nope", "cell": [0, 2]}]), "prop.nope"),
                        (floor_scene(3, 3, hang=[{"part": "prop.nope2", "at": [2, 2]}]), "prop.nope2"),
                        (floor_scene(3, 3, extra={"fill": [{"tile": "bakery.tile.nope", "rect": [0, 0, 3, 3]}]}),
                         "bakery.tile.nope")):
            with self.subTest(name), self.assertRaises(RecipeError) as e:
                build_scene(s, RECIPES, ctx())
            self.assertTrue(str(e.exception).startswith("t.scene"))
            self.assertIn(name, str(e.exception))

    def test_a_scene_cannot_be_placed_inside_a_scene(self):
        with self.assertRaises(RecipeError) as e:
            build_scene(floor_scene(3, 3, [{"id": "bakery.scene.interior", "cell": [0, 2]}]), RECIPES, ctx())
        self.assertIn("bakery.scene.interior", str(e.exception))

    def test_place_entry_needs_exactly_one_of_id_and_part(self):
        for entry in ({"cell": [0, 2]}, {"id": "bakery.shelf", "part": "prop.plant", "cell": [0, 2]}):
            with self.subTest(entry), self.assertRaises(RecipeError):
                build_scene(floor_scene(3, 3, [entry]), RECIPES, ctx())

    def test_things_further_down_are_drawn_over_things_behind(self):
        # 수레(아랫줄 95)가 진열대(아랫줄 63) 앞에 겹치게 놓으면 겹친 자리는 수레 색이다(JSON 순서와 무관).
        place = [{"id": "bakery.trolley", "cell": [0, 2]}, {"id": "bakery.shelf", "cell": [0, 1], "dy": -32}]
        scene = build_scene(floor_scene(3, 3, place), RECIPES, ctx())
        trolley = render(RECIPES["bakery.trolley"], ctx())
        for x, y in opaque(trolley):
            self.assertEqual(scene.get(x, y), trolley.get(x, y))

    def test_hung_part_is_placed_by_its_top_left_pixel_with_a_shadow_under_it(self):
        clock = PARTS["prop.clock"]
        x0, y0, x1, y1 = clock.bbox()
        s = floor_scene(3, 3, hang=[{"part": "prop.clock", "at": [20, 10]}])
        scene = build_scene(s, RECIPES, ctx())
        plain = build_scene(floor_scene(3, 3), RECIPES, ctx())
        for x, y in opaque(clock):
            self.assertEqual(scene.get(20 - x0 + x, 10 - y0 + y), clock.get(x, y))
        mid = 20 + (x1 - x0) // 2
        bottom = 10 + (y1 - y0)                                  # 시계 바로 밑 줄
        self.assertLess(sum(scene.get(mid, bottom)[:3]), sum(plain.get(mid, bottom)[:3]))

    def test_flat_thing_gets_no_shadow_and_lies_under_standing_things(self):
        """바닥에 깔린 것(깔개)은 그림자가 없고, 위에 선 것보다 먼저 그린다(JSON 순서·아랫줄과 무관)."""
        rug = render(RECIPES["bakery.rug"], ctx())
        x0, y0, x1, y1 = rug.bbox()
        s = floor_scene(4, 4, [{"id": "bakery.rug", "cell": [0, 3], "flat": True}])
        scene = build_scene(s, RECIPES, ctx())
        plain = build_scene(floor_scene(4, 4), RECIPES, ctx())
        top = 4 * 32 - rug.h
        for y in range(top + y1, 4 * 32):                       # 깔개 밑은 바닥 그대로
            for x in range(x0, x1):
                self.assertEqual(scene.get(x, y), plain.get(x, y), (x, y))
        plant = PARTS["prop.plant"]
        place = [{"part": "prop.plant", "cell": [0, 2], "dx": 10, "dy": 20},
                 {"id": "bakery.rug", "cell": [0, 3], "flat": True}]
        scene = build_scene(floor_scene(4, 4, place), RECIPES, ctx())
        px0, py0 = 10, 3 * 32 - plant.h + 20
        for x, y in opaque(plant):
            self.assertEqual(scene.get(px0 + x, py0 + y), plant.get(x, y))
        with self.assertRaises(RecipeError):
            build_scene(floor_scene(4, 4, [{"id": "bakery.rug", "cell": [0, 3], "flat": "yes"}]), RECIPES, ctx())

    def test_scene_render_is_deterministic(self):
        for sid in ("bakery.scene.interior", "bakery.scene.street"):
            with self.subTest(sid):
                a = build_scene(RECIPES[sid], RECIPES, ctx()).to_image().tobytes()
                b = build_scene(RECIPES[sid], RECIPES, ctx()).to_image().tobytes()
                self.assertEqual(a, b)


class TrolleyShadowTests(unittest.TestCase):
    """사용자 지적(6.6절): 수레 몸통 밑이 밝고 바퀴 앞에만 그림자 — "말이 안 되잖아". 다시 나오지 않게 고정한다."""

    def test_floor_under_a_body_on_wheels_or_feet_is_dark_between_them(self):
        for rid in ("bakery.trolley", "bakery.glass-case", "bakery.shelf"):
            with self.subTest(rid):
                spr = render(RECIPES[rid], ctx())
                place = [{"id": rid, "cell": [0, 2]}]
                scene = build_scene(floor_scene(3, 4, place), RECIPES, ctx())
                plain = build_scene(floor_scene(3, 4), RECIPES, ctx())
                top = 3 * 32 - spr.h
                feet = [x for x in range(spr.w) if any(spr.get(x, y)[3] for y in range(spr.h - 4, spr.h))]
                gaps = 0
                for x in range(feet[0] + 1, feet[-1]):
                    col = [y for y in range(spr.h) if spr.get(x, y)[3]]
                    for y in range(col[-1] + 1, spr.h):            # 그 열에서 물체가 끝난 곳 밑 ~ 맨 아랫줄
                        gaps += 1
                        self.assertLess(sum(scene.get(x, top + y)[:3]), sum(plain.get(x, top + y)[:3]), (x, y))
                self.assertGreater(gaps, 0)                          # 정말 떠 있는 물체를 시험했다


class InteriorSceneTests(unittest.TestCase):
    def test_interior_has_the_listed_things(self):
        s = RECIPES["bakery.scene.interior"]
        self.assertEqual(s["size"], [9, 5])
        names = [p.get("id") or p.get("part") for p in s["place"]] + [h.get("id") or h.get("part") for h in s["hang"]]
        for need, n in (("bakery.shelf", 2), ("bakery.trolley", 1), ("bakery.glass-case", 1), ("bakery.counter", 1),
                        ("prop.basket", 1), ("prop.plant", 1), ("bakery.menu-board", 1), ("prop.clock", 1)):
            self.assertGreaterEqual(names.count(need), n, need)

    def test_things_do_not_overlap_and_keep_off_the_scene_edges(self):
        for sid in ("bakery.scene.interior", "bakery.scene.street"):
            s = RECIPES[sid]
            w, h = s["size"][0] * 32, s["size"][1] * 32
            taken = {}
            for item in scene_items(s, RECIPES, ctx()):
                with self.subTest(sid, item=item.name):
                    pts = [(item.x + x, item.y + y) for x, y in opaque(item.sprite)]
                    self.assertTrue(pts)
                    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
                    self.assertGreaterEqual(min(xs), 2)
                    self.assertGreaterEqual(min(ys), 2)
                    self.assertLessEqual(max(xs), w - 3)
                    self.assertLessEqual(max(ys), h - 3)
                    for p in pts:
                        self.assertNotIn(p, taken, f"{item.name}가 {taken.get(p)}와 겹친다")
                    for p in pts:
                        taken[p] = item.name


if __name__ == "__main__":
    unittest.main()
