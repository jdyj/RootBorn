import copy
import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.lint import lint_sprite
from pixelkit.makers.furniture import pile
from pixelkit.parts import load_parts
from pixelkit.recipes import Ctx, RecipeError, load_recipes, render, render_with_spec
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE = Style.load(str(ROOT / "art" / "kit" / "style.json"))
PARTS = load_parts(str(ROOT / "art" / "kit" / "parts"), STYLE)
RECIPES = load_recipes(str(ROOT / "art" / "kit" / "recipes" / "bakery"))
FURNITURE = ("bakery.shelf", "bakery.counter", "bakery.glass-case", "bakery.trolley")


def ctx():
    return Ctx(STYLE, PARTS, [])


def with_values(rid, **kw):
    r = copy.deepcopy(RECIPES[rid])
    r.update(kw)
    return r


def make_shelf(tiers, c=None):
    """그린 캔버스와 layout(층마다 [y0, y1), 그린 그림 좌표)."""
    cv, spec = render_with_spec(with_values("bakery.shelf", tiers=tiers), c or ctx())
    return cv, spec["_layout"]


def offset_y(cv, layout):
    """그린 그림 좌표 → 렌더된 캔버스 좌표(사방 1px 외곽선 여유, 아래 맞춤)."""
    return cv.h - (layout["size"][1] + 2) + 1


def heights(tiers):
    return [y1 - y0 for y0, y1 in tiers]


def codes(problems):
    return [p.code for p in problems]


class PileTests(unittest.TestCase):
    def test_empty_list_draws_nothing(self):
        cv = Canvas(60, 20)
        c = ctx()
        pile(cv, [], 2, 57, 18, c)
        self.assertIsNone(cv.bbox())
        self.assertEqual(c.problems, [])

    def test_items_stand_on_base_row_inside_span(self):
        cv = Canvas(60, 20)
        c = ctx()
        pile(cv, ["bread.loaf"] * 3, 2, 57, 18, c)
        x0, y0, x1, y1 = cv.bbox()
        self.assertEqual(y1, 19)            # 바닥 줄 18이 마지막 줄
        self.assertGreaterEqual(x0, 2)
        self.assertLessEqual(x1, 58)
        self.assertEqual(c.problems, [])

    def test_overlap_beyond_third_reports_once(self):
        cv = Canvas(60, 20)
        c = ctx()
        pile(cv, ["bread.loaf"] * 9, 2, 57, 18, c)
        self.assertEqual(codes(c.problems), ["slot-overlap"])

    def test_tall_item_reports_slot_clipped_with_top(self):
        cv = Canvas(60, 20)
        c = ctx()
        pile(cv, ["bread.toast"], 2, 57, 18, c, top=10)
        self.assertEqual(codes(c.problems), ["slot-clipped"])


class ShelfTests(unittest.TestCase):
    def test_shelf_uses_all_inner_height_for_any_tier_count(self):
        for n in (1, 2, 3, 4):
            with self.subTest(n=n):
                cv, layout = make_shelf([["bread.loaf"]] * n)
                tiers = layout["tiers"]
                self.assertEqual(len(tiers), n)
                self.assertEqual(tiers[0][0], layout["inner"][0])     # 빈 띠가 남지 않는다
                self.assertEqual(tiers[-1][1], layout["inner"][1])
                for a, b in zip(tiers, tiers[1:]):
                    self.assertEqual(a[1], b[0])                       # 층 사이에 틈이 없다
                self.assertLessEqual(max(heights(tiers)) - min(heights(tiers)), 1)

    def test_empty_tiers_and_empty_state_render_without_items_or_errors(self):
        c = ctx()
        full, layout = make_shelf([["bread.loaf"]] * 4, c)
        empty, _ = make_shelf([[], [], [], []], c)
        state = render(RECIPES["bakery.shelf"], c, "empty")
        self.assertEqual(c.problems, [])
        self.assertEqual(empty.to_image().tobytes(), state.to_image().tobytes())
        loaf_colors = PARTS["bread.loaf"].colors() - {STYLE.outline}
        shelf_only = empty.colors()
        self.assertTrue(loaf_colors - shelf_only)       # 빵 색이 빈 선반에는 없다
        self.assertNotEqual(full.to_image().tobytes(), empty.to_image().tobytes())
        self.assertEqual(lint_sprite(empty, RECIPES["bakery.shelf"], c), [])

    def test_item_taller_than_tier_reports_slot_clipped(self):
        c = ctx()
        make_shelf([["bread.toast"], [], [], []], c)       # 4층 선반에 높이 12 부품
        self.assertEqual(codes(c.problems), ["slot-clipped"])

    def test_loaf_fits_a_four_tier_shelf(self):
        c = ctx()
        make_shelf([["bread.loaf", "bread.pretzel", "bread.cake"]] * 4, c)
        self.assertEqual(c.problems, [])

    def test_too_many_items_reports_slot_overlap(self):
        c = ctx()
        make_shelf([["bread.loaf"] * 9, ["bread.loaf"] * 9, [], []], c)   # 한 층에 9개
        self.assertEqual(codes(c.problems), ["slot-overlap"])           # 그림당 하나

    def test_changing_one_tier_changes_only_that_tiers_rows(self):
        a, layout = make_shelf([["bread.loaf"] * 3, ["bread.bun"] * 3, ["bread.roll"] * 3, ["bread.croissant"] * 3])
        b, _ = make_shelf([["bread.loaf"] * 3, ["bread.baguette"] * 2, ["bread.roll"] * 3, ["bread.croissant"] * 3])
        oy = offset_y(a, layout)
        y0, y1 = layout["tiers"][1]
        changed = {y for y in range(a.h) for x in range(a.w) if a.get(x, y) != b.get(x, y)}
        self.assertTrue(changed)
        self.assertTrue(all(oy + y0 <= y < oy + y1 for y in changed), sorted(changed))

    def test_bad_tiers_raise(self):
        for bad in ([], [[]] * 5, "bread.loaf", [["nope.part"]]):
            with self.subTest(bad=bad), self.assertRaises(RecipeError):
                make_shelf(bad)


class AllFurnitureTests(unittest.TestCase):
    def test_every_bakery_furniture_recipe_is_cell_sized_and_lint_clean(self):
        for rid in FURNITURE:
            r = RECIPES[rid]
            for state in [None] + sorted(r.get("states", {})):
                with self.subTest(rid=rid, state=state):
                    c = ctx()
                    cv = render(r, c, state)
                    self.assertEqual((cv.w, cv.h), (r["art_cells"][0] * 32, r["art_cells"][1] * 32))
                    self.assertEqual(lint_sprite(cv, r, c), [])
                    self.assertEqual(c.problems, [])

    def test_recipes_table(self):
        expect = {
            "bakery.shelf": ("furniture.shelf", [2, 1], [2, 3], True),
            "bakery.counter": ("furniture.counter", [3, 1], [3, 2], False),
            "bakery.glass-case": ("furniture.glass-case", [2, 1], [2, 2], True),
            "bakery.trolley": ("furniture.trolley", [1, 1], [1, 3], True),
        }
        for rid, (part, cells, art, has_empty) in expect.items():
            with self.subTest(rid):
                r = RECIPES[rid]
                self.assertEqual((r["part"], r["cells"], r["art_cells"]), (part, cells, art))
                self.assertEqual("empty" in r.get("states", {}), has_empty)

    def test_deterministic(self):
        for rid in FURNITURE:
            a = render(RECIPES[rid], ctx()).to_image().tobytes()
            b = render(RECIPES[rid], ctx()).to_image().tobytes()
            self.assertEqual(a, b)

    def test_counter_top_face_is_at_least_40_percent_of_body_height(self):
        _, spec = render_with_spec(RECIPES["bakery.counter"], ctx())
        t0, t1 = spec["_layout"]["top_face"]
        b0, b1 = spec["_layout"]["body"]
        self.assertGreaterEqual((t1 - t0) / (b1 - b0), 0.4)

    def test_counter_item_off_the_top_reports_slot_clipped(self):
        c = ctx()
        render(with_values("bakery.counter", top=[{"part": "prop.register", "x": 90}]), c)
        self.assertEqual(codes(c.problems), ["slot-clipped"])

    def test_counter_bad_top_raises(self):
        for bad in ("prop.register", [{"part": "prop.register"}], [{"part": "prop.register", "x": 1.5}]):
            with self.subTest(bad=bad), self.assertRaises(RecipeError):
                render(with_values("bakery.counter", top=bad), ctx())

    def test_trolley_content_width_is_at_most_30(self):
        cv = render(RECIPES["bakery.trolley"], ctx())
        xs = [x for y in range(cv.h) for x in range(cv.w) if cv.get(x, y)[3] and cv.get(x, y) != STYLE.outline]
        self.assertLessEqual(max(xs) - min(xs) + 1, 30)

    def test_glass_has_streaks(self):
        self.assertIn(STYLE.color("glass.streak.0"), render(RECIPES["bakery.glass-case"], ctx()).colors())

    def test_streaks_change_glass_and_items_rows(self):
        # 사선 반사 줄은 유리 안의 물건 위로도 지나간다: 같은 물건을 반사 줄 없이 놓은 것과 다르다.
        from pixelkit.makers.furniture import streaks
        c = ctx()
        cv = Canvas(30, 20)
        cv.rect(0, 0, 30, 20, STYLE.color("glass.front.1"))
        cv.paste(PARTS["bread.cake"], 8, 5)
        before = [cv.get(x, y) for y in range(20) for x in range(30)]
        streaks(cv, 0, 0, 30, 20, c)
        after = [cv.get(x, y) for y in range(20) for x in range(30)]
        self.assertNotEqual(before, after)
        self.assertLessEqual(cv.colors(), STYLE.palette)
        # 새 색은 반사 줄 색 하나뿐(물건은 이미 그림에 있는 밝은 단계로만 바뀐다).
        self.assertLessEqual(cv.colors(), set(before) | {STYLE.color("glass.streak.0")})


if __name__ == "__main__":
    unittest.main()
