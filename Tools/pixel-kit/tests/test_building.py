import copy
import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.lint import lint_sprite
from pixelkit.makers.building import ROOF_ITEMS, _Pal
from pixelkit.parts import load_parts
from pixelkit.recipes import Ctx, RecipeError, ghost_ctx, load_recipes, render, render_with_spec
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE = Style.load(str(ROOT / "art" / "kit" / "style.json"))
PARTS = load_parts(str(ROOT / "art" / "kit" / "parts"), STYLE)
RECIPES = load_recipes(str(ROOT / "art" / "kit" / "recipes" / "bakery"))
ALL_RECIPES = load_recipes(str(ROOT / "art" / "kit" / "recipes"))
# 옆면 금지는 건물 명세 전부에 적용한다(2단계 건물이 늘어도 자동으로 걸린다).
EXTERIORS = tuple(rid for rid, r in ALL_RECIPES.items() if r.get("part") == "building.shop")


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


def sub(rid, key, **kw):
    d = copy.deepcopy(RECIPES[rid][key])
    d.update(kw)
    return with_values(rid, **{key: d})


def problems_of(spec, code):
    c = ctx()
    render(spec, c)
    return [p for p in c.problems if p.code == code]


class FacadeLayoutTests(unittest.TestCase):
    FLAT, PITCHED = "bakery.exterior.flat", "bakery.exterior.pitched"

    def test_real_bakery_recipes_have_no_facade_problems(self):
        for rid in EXTERIORS:
            with self.subTest(rid):
                c = ctx()
                render(RECIPES[rid], c)
                self.assertEqual([p.code for p in c.problems if p.code.startswith("facade-")], [])

    def test_the_five_reproduced_cases_each_report_a_problem(self):
        cases = {
            "narrow canvas": (with_values(self.FLAT, pixels=[128, 192]), "facade-out", "창"),
            "door on window": (sub(self.FLAT, "door", at_x=85), "facade-overlap", "문틀"),
            "sign off right": (sub(self.FLAT, "sign", at_x=140), "facade-out", "간판"),
            "lamp on door": (sub(self.FLAT, "lamp", at_x=20), "facade-overlap", "벽등"),
            "door off left": (sub(self.FLAT, "door", at_x=-30), "facade-out", "문틀"),
        }
        for name, (spec, code, who) in cases.items():
            with self.subTest(name):
                hits = problems_of(spec, code)
                self.assertEqual(len(hits), 1)
                self.assertTrue(hits[0].message.startswith(spec["id"]), hits[0].message)
                self.assertIn(who, hits[0].message)
                self.assertIsNone(hits[0].x)

    def test_out_message_says_how_far_and_names_every_offender(self):
        spec = sub(self.FLAT, "door", at_x=-30)
        spec["sign"] = dict(spec["sign"], at_x=140)
        hits = problems_of(spec, "facade-out")
        self.assertEqual(len(hits), 1)
        m = hits[0].message
        self.assertIn("문틀", m)
        self.assertIn("간판", m)
        self.assertEqual(m.count("33px"), 2)   # 문틀 왼쪽 끝 -30 ↔ 벽 3, 간판 오른쪽 끝 188 ↔ 벽 155

    def test_overlap_message_names_each_pair(self):
        spec = sub(self.FLAT, "door", at_x=85)
        spec["lamp"] = dict(spec["lamp"], at_x=90)
        m = problems_of(spec, "facade-overlap")[0].message
        self.assertIn("문틀과 창", m)
        self.assertIn("문틀과 벽등", m)

    def test_no_lamp_means_no_lamp_checks(self):
        spec = copy.deepcopy(RECIPES[self.FLAT])
        del spec["lamp"]
        c = ctx()
        render(spec, c)
        self.assertEqual(c.problems, [])
        spec["door"]["at_x"] = 60
        self.assertTrue(problems_of(spec, "facade-out") == [] or "벽등" not in problems_of(spec, "facade-out")[0].message)

    def test_checks_use_the_rectangles_the_drawing_reports(self):
        from pixelkit.makers.building import ROOF_H, _extents, _values
        for rid in EXTERIORS:
            with self.subTest(rid):
                _, spec = render_with_spec(RECIPES[rid], ctx())
                lay = spec["_layout"]
                ext = _extents(_values(spec, ctx()), ROOF_H)
                self.assertEqual(ext["문틀"][0], lay["door"])
                self.assertEqual(ext["창"][0], lay["window"])
                self.assertEqual(ext["간판"][0], lay["sign"])
                self.assertEqual(ext["벽등"][0], lay["lamp"])

    def test_drawing_still_happens_when_a_problem_is_reported(self):
        c = ctx()
        cv = render(sub(self.FLAT, "door", at_x=85), c)
        self.assertEqual((cv.w, cv.h), (160, 192))
        self.assertTrue(c.problems)

    def test_roof_item_under_the_sign_reports_roof_overlap(self):
        roof = {"type": "pitched", "items": [{"part": "roof.chimney", "at": [20, 60]}]}
        hits = problems_of(with_values(self.PITCHED, roof=roof), "roof-overlap")
        self.assertEqual(len(hits), 1)
        self.assertIn("간판", hits[0].message)
        self.assertTrue(hits[0].message.startswith(self.PITCHED))


class BuildingTests(unittest.TestCase):
    def test_both_bakery_exteriors_are_160x192_and_lint_clean(self):
        for rid in EXTERIORS:
            with self.subTest(rid):
                c = ctx()
                cv = render(RECIPES[rid], c)
                self.assertEqual((cv.w, cv.h), (160, 192))
                body = render(RECIPES[rid], ghost_ctx(c))
                self.assertEqual(lint_sprite(cv, RECIPES[rid], c, body=body), [])
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

    def test_door_step_is_drawn_in_the_ramp_the_recipe_names(self):
        """계단 색은 명세의 door.step 줄(윗면 단계 2, 앞면 단계 1). 승인한 판은 따뜻한 회색 돌이었다."""
        for rid in EXTERIORS:
            with self.subTest(rid):
                step = RECIPES[rid]["door"]["step"]
                cv, spec = render_with_spec(RECIPES[rid], ctx())
                lay = spec["_layout"]
                ox, oy = offset(cv, lay)
                dx, dy, dw, dh = lay["door"]
                base = dy + dh
                self.assertEqual(cv.get(ox + dx - 2, oy + base - 2), STYLE.color(f"{step}.2"))
                self.assertEqual(cv.get(ox + dx - 2, oy + base + 1), STYLE.color(f"{step}.1"))
        door = dict(RECIPES["bakery.exterior.flat"]["door"], step="no.such.ramp")
        with self.assertRaises(RecipeError):
            render(with_values("bakery.exterior.flat", door=door), ctx())

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

    def test_roof_item_shadow_is_two_steps_darker_than_the_roof_under_it(self):
        """한 단계 그늘은 평지붕의 줄눈과 같은 색이라 천창 그림자가 묻혔다. 그림자는 그 자리 지붕의 두 단계 아래."""
        spec = {"id": "t.item"}
        P = _Pal(spec, ctx(), "roof.teal", "brick", "roof.slate")
        plain, want = STYLE.color("roof.teal.2"), STYLE.color("roof.teal.0")
        for name, it in sorted(ROOF_ITEMS.items()):
            with self.subTest(name):
                alone = Canvas(it.w + 8, it.h + 8)
                it.draw(alone, P, 4, 4)
                roof = Canvas(it.w + 8, it.h + 8)
                roof.rect(0, 0, roof.w, roof.h, plain)
                it.draw(roof, P, 4, 4)
                y = 4 + it.front[0] + it.front[1] - 1                # 앞면 맨 아랫줄
                x = max(x for x in range(alone.w) if alone.get(x, y)[3]) + 1
                self.assertEqual(roof.get(x, y), want)


    def test_every_roof_item_shadow_is_a_short_band(self):
        """사용자(2026-11-13): "굴뚝 그림자가 … 너무 큰거같아". 오른쪽 띠는 앞면 줄마다 4열 이하, 아래 띠는 2줄 이하."""
        spec = {"id": "t.item"}
        P = _Pal(spec, ctx(), "roof.teal", "brick", "roof.slate")
        plain = STYLE.color("roof.teal.2")
        for name, it in sorted(ROOF_ITEMS.items()):
            with self.subTest(name):
                alone = Canvas(it.w + 8, it.h + 8)
                it.draw(alone, P, 4, 4)
                roof = Canvas(it.w + 8, it.h + 8)
                roof.rect(0, 0, roof.w, roof.h, plain)
                it.draw(roof, P, 4, 4)
                dark = lambda x, y: not alone.get(x, y)[3] and roof.get(x, y) != plain   # noqa: E731
                y0, n = it.front
                for y in range(4 + y0, 4 + y0 + n):
                    x = max(x for x in range(alone.w) if alone.get(x, y)[3]) + 1
                    run = 0
                    while dark(x + run, y):
                        run += 1
                    self.assertLessEqual(run, 4, (name, y))
                bottom = 4 + y0 + n - 1                             # 앞면 맨 아랫줄
                for x in range(alone.w):
                    ys = [y for y in range(alone.h) if alone.get(x, y)[3]]
                    if not ys or max(ys) < bottom:                  # 밑변까지 오지 않는 열(내민 처마)은 오른쪽 띠가 맡는다
                        continue
                    run = 0
                    while dark(x, max(ys) + 1 + run):
                        run += 1
                    self.assertLessEqual(run, 2, (name, x))


class NoSideFaceTests(unittest.TestCase):
    """옆면 금지(사용자: 절대 하지 마)를 그린 그림의 픽셀로 본다."""

    def test_real_exteriors_have_no_side_face_problem(self):
        for rid in EXTERIORS:
            with self.subTest(rid):
                c = ctx()
                cv, spec = render_with_spec(ALL_RECIPES[rid], c)
                ps = lint_sprite(cv, spec, c, layout=spec["_layout"])
                self.assertEqual([p for p in ps if p.code == "side-face"], [])

    def test_every_building_recipe_is_covered(self):
        self.assertGreaterEqual(len(EXTERIORS), 2)

    def render(self, rid, recipe=None):
        cv, spec = render_with_spec(recipe or ALL_RECIPES[rid], ctx())
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


def ramp_colors(prefix):
    return {STYLE.color(k) for k in STYLE._colors if k.rpartition(".")[0] == prefix}


def only_in(prefix):
    """그 줄에만 있는 색(다른 줄과 hex가 같은 색은 뺀다 — 같이 바뀌는 색)."""
    others = {STYLE.color(k) for k in STYLE._colors if k.rpartition(".")[0] != prefix}
    return ramp_colors(prefix) - others


class ShopPaletteTests(unittest.TestCase):
    def test_trim_plinth_and_frame_come_from_the_recipe(self):
        for key, old, new in (("trim", "trim", "grass"), ("plinth", "plinth", "choco"), ("frame", "wood.warm", "roof.teal")):
            with self.subTest(key):
                self.assertTrue(only_in(old))
                cv = render(with_values("bakery.exterior.pitched", **{key: new}), ctx())
                self.assertEqual(cv.colors() & only_in(old), set())
                self.assertTrue(cv.colors() & ramp_colors(new))

    def test_defaults_written_out_give_the_same_bytes(self):
        for rid in EXTERIORS:
            with self.subTest(rid):
                r = with_values(rid, trim="trim", plinth="plinth", frame="wood.warm")
                r["lamp"] = dict(RECIPES[rid]["lamp"], part="prop.wall-lamp")
                a = render(RECIPES[rid], ctx()).to_image().tobytes()
                self.assertEqual(a, render(r, ctx()).to_image().tobytes())

    def test_short_ramp_raises_with_id_ramp_and_steps(self):
        for key, ramp, need in (("trim", "plinth", 4), ("plinth", "white", 2), ("frame", "trim", 5)):
            with self.subTest(key), self.assertRaises(RecipeError) as e:
                render(with_values("bakery.exterior.flat", **{key: ramp}), ctx())
            msg = str(e.exception)
            self.assertTrue(msg.startswith("bakery.exterior.flat:"), msg)
            self.assertIn(ramp, msg)
            self.assertIn(str(need), msg)


class ShopLampTests(unittest.TestCase):
    def test_no_lamp_key_draws_no_lamp_and_no_glow(self):
        lamp_colors = PARTS["prop.wall-lamp"].colors() - {STYLE.outline}
        for rid in EXTERIORS:
            with self.subTest(rid):
                r = with_values(rid)
                del r["lamp"]
                c = ctx()
                bare, spec = render_with_spec(r, c)
                lit, lit_spec = render_with_spec(RECIPES[rid], ctx())
                self.assertNotIn("lamp", spec["_layout"])
                body = render(r, ghost_ctx(c))
                self.assertEqual(lint_sprite(bare, r, c, layout=spec["_layout"], body=body), [])
                self.assertEqual(c.problems, [])
                ox, oy = offset(bare, spec["_layout"])
                lx, ly, lw, lh = lit_spec["_layout"]["lamp"]
                region = {bare.get(ox + x, oy + y) for y in range(ly, ly + lh) for x in range(lx, lx + lw)}
                self.assertEqual(region & lamp_colors, set())
                cx, cy = lx + lw // 2, ly + lh // 2                 # 다른 곳은 벽등과 그 둘레 빛 말고 그대로
                diff = [(x - ox, y - oy) for y in range(bare.h) for x in range(bare.w) if bare.get(x, y) != lit.get(x, y)]
                self.assertTrue(diff)
                self.assertTrue(all(abs(x - cx) <= 9 and abs(y - cy) <= 8 for x, y in diff), diff[:5])

    def test_lamp_part_comes_from_the_recipe(self):
        r = with_values("bakery.exterior.flat")
        r["lamp"] = {"at_x": r["lamp"]["at_x"], "part": "prop.clock"}
        _, spec = render_with_spec(r, ctx())
        self.assertEqual(spec["_layout"]["lamp"][2:], [PARTS["prop.clock"].w, PARTS["prop.clock"].h])

    def test_bad_lamp_raises(self):
        for bad in ({"part": "prop.wall-lamp"}, {"at_x": 20, "part": "nope.part"}, {"at_x": 20, "part": 3}, "x"):
            with self.subTest(bad=bad), self.assertRaises(RecipeError) as e:
                render(with_values("bakery.exterior.flat", lamp=bad), ctx())
            self.assertTrue(str(e.exception).startswith("bakery.exterior.flat:"))


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
