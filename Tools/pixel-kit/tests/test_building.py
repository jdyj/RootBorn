import copy
import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.lint import lint_sprite
from pixelkit.makers.building import ROOF_ITEMS, _Pal
from pixelkit.parts import load_parts
from pixelkit.recipes import Ctx, RecipeError, load_recipes, render, render_with_spec
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE = Style.load(str(ROOT / "art" / "kit" / "style.json"))
PARTS = load_parts(str(ROOT / "art" / "kit" / "parts"), STYLE)
RECIPES = load_recipes(str(ROOT / "art" / "kit" / "recipes" / "bakery"))
EXTERIORS = ("bakery.exterior.flat", "bakery.exterior.pitched")


def ctx():
    return Ctx(STYLE, PARTS, [])


def with_values(rid, **kw):
    r = copy.deepcopy(RECIPES[rid])
    r.update(kw)
    return r


def offset(cv, layout):
    """그린 그림 좌표 → 렌더된 캔버스 좌표(사방 1px 외곽선 여유, 아래 가운데)."""
    w, h = layout["size"]
    return (cv.w - (w + 2)) // 2 + 1, cv.h - (h + 2) + 1


def codes(problems):
    return [p.code for p in problems]


def gap(a, b):
    """[x, y, w, h] 두 사각형 사이의 빈 칸 수(가로·세로 중 큰 쪽). 겹치면 음수 또는 0 이하."""
    ax0, ay0, ax1, ay1 = a[0], a[1], a[0] + a[2], a[1] + a[3]
    bx0, by0, bx1, by1 = b[0], b[1], b[0] + b[2], b[1] + b[3]
    return max(bx0 - ax1, ax0 - bx1, by0 - ay1, ay0 - by1)


class BuildingTests(unittest.TestCase):
    def test_both_bakery_exteriors_are_160x192_and_lint_clean(self):
        for rid in EXTERIORS:
            with self.subTest(rid):
                c = ctx()
                cv = render(RECIPES[rid], c)
                self.assertEqual((cv.w, cv.h), (160, 192))
                self.assertEqual(lint_sprite(cv, RECIPES[rid], c), [])
                self.assertEqual(c.problems, [])

    def test_roof_is_at_least_half_the_picture_height(self):
        for rid in EXTERIORS:
            with self.subTest(rid):
                _, spec = render_with_spec(RECIPES[rid], ctx())
                self.assertGreaterEqual(spec["_layout"]["roof_bottom"], 96)

    def test_content_spans_the_full_width_with_no_side_face(self):
        for rid in EXTERIORS:
            with self.subTest(rid):
                cv, spec = render_with_spec(RECIPES[rid], ctx())
                lay = spec["_layout"]
                self.assertFalse([k for k in lay if "side" in k])
                x0, _, x1, _ = cv.bbox()
                self.assertEqual((x0, x1), (0, cv.w))           # 지붕이 폭을 다 쓴다(외곽선 포함)
                ox, _ = offset(cv, lay)
                left = lay["wall"][0] + ox
                right = cv.w - 1 - (lay["wall"][1] + ox)
                self.assertLessEqual(abs(left - right), 1)

    def test_roof_items_do_not_overlap_each_other_or_the_parapet(self):
        for rid in EXTERIORS:
            with self.subTest(rid, case="as given"):
                c = ctx()
                _, spec = render_with_spec(RECIPES[rid], c)
                rects = spec["_layout"]["roof_items"]
                self.assertGreaterEqual(len(rects), 2)
                for i, a in enumerate(rects):
                    for b in rects[i + 1:]:
                        self.assertGreater(gap(a, b), 0, (a, b))
                self.assertNotIn("roof-overlap", codes(c.problems))
        items = RECIPES["bakery.exterior.flat"]["roof"]["items"]
        on_top = copy.deepcopy(RECIPES["bakery.exterior.flat"]["roof"])
        on_top["items"] = [items[0], {"part": items[1]["part"], "at": list(items[0]["at"])}]
        on_rim = copy.deepcopy(RECIPES["bakery.exterior.flat"]["roof"])
        on_rim["items"] = [{"part": items[0]["part"], "at": [1, 1]}]
        for name, roof in (("overlap", on_top), ("parapet", on_rim)):
            with self.subTest(name):
                c = ctx()
                render(with_values("bakery.exterior.flat", roof=roof), c)
                hits = [p for p in c.problems if p.code == "roof-overlap"]
                self.assertEqual(len(hits), 1)
                self.assertTrue(hits[0].message.startswith("bakery.exterior.flat"))

    def test_window_slots_fill_both_sides_of_the_mullion_without_crossing_it(self):
        rid = "bakery.exterior.flat"
        c = ctx()
        full, spec = render_with_spec(RECIPES[rid], c)
        window = copy.deepcopy(RECIPES[rid]["window"])
        window["slots"] = [[], []]
        empty = render(with_values(rid, window=window), ctx())
        lay = spec["_layout"]
        ox, oy = offset(full, lay)
        mx = lay["mullion"][0] + ox
        mw = lay["mullion"][1]
        changed = [(x, y) for y in range(full.h) for x in range(full.w) if full.get(x, y) != empty.get(x, y)]
        self.assertTrue([p for p in changed if p[0] < mx])
        self.assertTrue([p for p in changed if p[0] >= mx + mw])
        self.assertFalse([p for p in changed if mx <= p[0] < mx + mw])
        wx, wy, ww, wh = lay["window"]
        for x, y in changed:
            self.assertTrue(wx + ox <= x < wx + ox + ww and wy + oy <= y < wy + oy + wh, (x, y))
        self.assertEqual([p for p in c.problems if p.code.startswith("slot-")], [])
        crowded = copy.deepcopy(RECIPES[rid]["window"])
        crowded["slots"] = [["bread.baguette"] * 6, []]
        c2 = ctx()
        render(with_values(rid, window=crowded), c2)
        self.assertTrue([p for p in c2.problems if p.code.startswith("slot-") and p.message.startswith(rid)])

    def test_unknown_roof_type_raises_recipe_error_listing_known_types(self):
        roof = copy.deepcopy(RECIPES["bakery.exterior.flat"]["roof"])
        roof["type"] = "dome"
        with self.assertRaises(RecipeError) as e:
            render(with_values("bakery.exterior.flat", roof=roof), ctx())
        msg = str(e.exception)
        self.assertTrue(msg.startswith("bakery.exterior.flat"))
        for known in ("flat", "pitched", "dome"):
            self.assertIn(known, msg)
        roof["type"] = "flat"
        roof["items"] = [{"part": "roof.ufo", "at": [20, 20]}]
        with self.assertRaises(RecipeError) as e:
            render(with_values("bakery.exterior.flat", roof=roof), ctx())
        self.assertIn("roof.chimney", str(e.exception))

    def test_lamp_is_at_least_4px_from_the_awning(self):
        for rid in EXTERIORS:
            with self.subTest(rid):
                _, spec = render_with_spec(RECIPES[rid], ctx())
                lay = spec["_layout"]
                self.assertGreaterEqual(gap(lay["lamp"], lay["awning"]), 4)
                self.assertLess(lay["sign"][1], lay["roof_bottom"])       # 간판이 처마 위로 솟는다
                self.assertGreater(lay["sign"][1] + lay["sign"][3], lay["roof_bottom"])

    def test_building_render_is_deterministic(self):
        for rid in EXTERIORS:
            with self.subTest(rid):
                a = render(RECIPES[rid], ctx())
                b = render(RECIPES[rid], ctx())
                self.assertEqual(a.to_image().tobytes(), b.to_image().tobytes())


class RoofItemShadowTests(unittest.TestCase):
    def test_every_roof_item_shadow_starts_right_where_its_front_face_ends(self):
        """물건 하나를 맨 지붕(한 색)에 그리고, 앞면의 줄마다 물체 바로 오른쪽 칸이 어두워졌는지 본다(떨어진 그림자 금지)."""
        spec = {"id": "t.item"}
        c = ctx()
        P = _Pal(spec, c, "roof.teal", "brick", "roof.slate")
        plain = STYLE.color("roof.teal.2")
        for name, it in sorted(ROOF_ITEMS.items()):
            with self.subTest(name):
                alone = Canvas(it.w + 8, it.h + 8)                  # 투명 위: 물체만 남는다(그림자는 색 줄에만 든다)
                it.draw(alone, P, 4, 4)
                roof = Canvas(it.w + 8, it.h + 8)
                roof.rect(0, 0, roof.w, roof.h, plain)
                it.draw(roof, P, 4, 4)
                y0, n = it.front
                for y in range(4 + y0, 4 + y0 + n):
                    xs = [x for x in range(alone.w) if alone.get(x, y)[3]]
                    self.assertTrue(xs, (name, y))
                    self.assertNotEqual(roof.get(max(xs) + 1, y), plain, (name, y, max(xs) + 1))


class NoSideFaceTests(unittest.TestCase):
    """옆면 금지(사용자: 절대 하지 마)를 그린 그림의 픽셀로 본다."""

    def render(self, rid, recipe=None):
        cv, spec = render_with_spec(recipe or RECIPES[rid], ctx())
        lay = spec["_layout"]
        ox, oy = offset(cv, lay)
        return cv, lay, ox, oy

    def test_roof_rows_are_opaque_across_the_full_width(self):
        for rid in EXTERIORS:
            with self.subTest(rid):
                cv, lay, ox, oy = self.render(rid)
                w = lay["size"][0]
                holes = [(x, y) for y in range(oy, oy + lay["roof_bottom"] + 1)
                         for x in range(ox, ox + w) if not cv.get(x, y)[3]]
                self.assertEqual(holes, [])

    def test_wall_rows_above_the_step_are_symmetric(self):
        for rid in EXTERIORS:
            with self.subTest(rid):
                cv, lay, ox, oy = self.render(rid)
                dx, dy, dw, dh = lay["door"]
                for y in range(oy + lay["roof_bottom"] + 1, oy + dy + dh - 3):
                    xs = [x for x in range(cv.w) if cv.get(x, y)[3]]
                    self.assertLessEqual(abs(min(xs) - (cv.w - 1 - max(xs))), 1, y)

    def test_awning_slope_has_no_side_triangle(self):
        names = [f"awning.red.{i}" for i in range(2)] + [f"awning.cream.{i}" for i in range(2)] + ["trim.3"]
        awning = {STYLE.color(k) for k in names}
        for rid in EXTERIORS:
            with self.subTest(rid):
                cv, lay, ox, oy = self.render(rid)
                ax, ay, aw, ah = lay["awning"]
                ends = set()
                for y in range(oy + ay + 4, oy + ay + 12):          # 비탈 줄(처마 밑 띠 아래부터)
                    xs = [x for x in range(cv.w) if cv.get(x, y) in awning]
                    ends.add((min(xs), max(xs)))
                self.assertEqual(ends, {(ax + ox, ax + ox + aw - 1)})


class ABoardTests(unittest.TestCase):
    def test_a_board_is_lint_clean(self):
        c = ctx()
        r = RECIPES["bakery.a-board"]
        cv = render(r, c)
        self.assertEqual(lint_sprite(cv, r, c), [])


class StreetSceneTests(unittest.TestCase):
    def test_street_has_the_building_two_plants_and_an_a_board(self):
        s = RECIPES["bakery.scene.street"]
        names = [p.get("id") or p.get("part") for p in s["place"]]
        self.assertEqual(names.count("bakery.exterior.flat"), 1)
        self.assertEqual(names.count("prop.plant"), 2)
        self.assertEqual(names.count("bakery.a-board"), 1)


if __name__ == "__main__":
    unittest.main()
