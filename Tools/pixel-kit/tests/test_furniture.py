import copy
import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.lint import lint_sprite
from pixelkit.makers.common import pile
from pixelkit.parts import load_parts
from pixelkit.recipes import Ctx, RecipeError, ghost_ctx, load_recipes, render, render_with_spec
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


class TrayEdgeTests(unittest.TestCase):
    def test_tray_whose_border_would_leave_the_counter_is_reported(self):
        for x, w in ((0, 30), (64, 30)):                     # 왼쪽 테두리가 x=-1, 오른쪽 테두리가 x=94
            with self.subTest(x=x):
                c = ctx()
                r = with_values("bakery.counter")
                r["tray"] = {"x": x, "w": w, "items": r["tray"]["items"]}
                render(r, c)
                self.assertIn("slot-clipped", codes(c.problems))


class PileTests(unittest.TestCase):
    def test_empty_list_draws_nothing(self):
        cv = Canvas(60, 20)
        c = ctx()
        pile(cv, [], 2, 57, 18, c, 0)
        self.assertIsNone(cv.bbox())
        self.assertEqual(c.problems, [])

    def test_items_stand_on_base_row_inside_span(self):
        cv = Canvas(60, 20)
        c = ctx()
        pile(cv, ["bread.loaf"] * 3, 2, 57, 18, c, 0)
        x0, y0, x1, y1 = cv.bbox()
        self.assertEqual(y1, 19)            # 바닥 줄 18이 마지막 줄
        self.assertGreaterEqual(x0, 2)
        self.assertLessEqual(x1, 58)
        self.assertEqual(c.problems, [])

    def test_overlap_beyond_third_reports_once(self):
        cv = Canvas(60, 20)
        c = ctx()
        pile(cv, ["bread.loaf"] * 9, 2, 57, 18, c, 0)
        self.assertEqual(codes(c.problems), ["slot-overlap"])

    def test_top_is_required(self):
        with self.assertRaises(TypeError):
            pile(Canvas(60, 20), ["bread.loaf"], 2, 57, 18, ctx())

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

    def test_painted_shelf_has_no_leftover_band(self):
        # 그린 그림에서 확인한다: 윗면 밑 외곽선 줄부터 밑판까지 안쪽 가운데 열의 모든 줄이
        # 뒤판·짙은 줄·쟁반·선반 판·외곽선 중 하나이고, 선반 판 외곽선 줄이 층 수만큼, 마지막 판 바로 밑이 밑판이다.
        inside = {STYLE.color(k) for k in ("wood.back.0", "wood.back.1", "cream.2", "wood.warm.3", "wood.warm.4")}
        for n in (1, 2, 3, 4):
            with self.subTest(n=n):
                cv, _ = make_shelf([[]] * n)
                x = cv.w // 2
                col = [cv.get(x, y) for y in range(cv.h)]
                y = next(i for i, c in enumerate(col) if c[3] and c != STYLE.outline)   # 윗면 첫 줄
                top_ol = col.index(STYLE.outline, y)                                     # 윗면 밑 외곽선
                y = top_ol + 1
                boards = 0
                while col[y] in inside or col[y] == STYLE.outline:
                    if col[y] == STYLE.outline:
                        boards += 1
                    y += 1
                self.assertEqual(boards, n)
                self.assertEqual(col[y - 1], STYLE.outline)                  # 마지막 판 외곽선 바로 밑이
                self.assertEqual(col[y], STYLE.color("wood.warm.2"))         # 밑판 윗줄

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


FRAME_RAMPS = ("wood.", "metal", "glass.", "cream.")


def bread_only_colors():
    """빵 부품에만 나오고 가구 틀 재질(나무·금속·유리·쟁반 크림)에는 없는 색."""
    frame = {STYLE.color(k) for k in STYLE._colors if k.startswith(FRAME_RAMPS)}
    breads = set()
    for pid, cv in PARTS.items():
        if pid.startswith("bread."):
            breads |= cv.colors()
    return breads - frame - {STYLE.outline}


class AllFurnitureTests(unittest.TestCase):
    def test_empty_states_have_no_bread_colors(self):
        only = bread_only_colors()
        self.assertTrue(only)
        for rid in ("bakery.shelf", "bakery.glass-case", "bakery.trolley"):
            with self.subTest(rid):
                cv = render(RECIPES[rid], ctx(), "empty")
                self.assertEqual(cv.colors() & only, set())

    def test_slot_problems_are_one_per_code_per_picture(self):
        c = ctx()
        render(with_values("bakery.shelf", tiers=[["bread.loaf"] * 9, [], [], []]), c)
        render(with_values("bakery.glass-case", rows=[["bread.cake"] * 12, []]), c)
        self.assertEqual(codes(c.problems), ["slot-overlap", "slot-overlap"])
        self.assertTrue(c.problems[0].message.startswith("bakery.shelf:"))
        self.assertTrue(c.problems[1].message.startswith("bakery.glass-case:"))


    def test_every_bakery_furniture_recipe_is_cell_sized_and_lint_clean(self):
        for rid in FURNITURE:
            r = RECIPES[rid]
            for state in [None] + sorted(r.get("states", {})):
                with self.subTest(rid=rid, state=state):
                    c = ctx()
                    cv = render(r, c, state)
                    self.assertEqual((cv.w, cv.h), (r["art_cells"][0] * 32, r["art_cells"][1] * 32))
                    body = render(r, ghost_ctx(c), state)
                    self.assertEqual(lint_sprite(cv, r, c, body=body), [])
                    self.assertEqual(c.problems, [])

    def test_menu_board_is_lint_clean_and_has_chalk_but_no_letters_of_its_own(self):
        r = RECIPES["bakery.menu-board"]
        c = ctx()
        cv = render(r, c)
        self.assertEqual((cv.w, cv.h), (32, 32))
        self.assertEqual(lint_sprite(cv, r, c), [])
        self.assertIn(STYLE.color("white.0"), cv.colors())
        self.assertIn(STYLE.color("board.green.1"), cv.colors())

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

    def test_rug_is_the_size_the_recipe_names_lint_clean_in_its_ramp_with_stitches(self):
        r = RECIPES["bakery.rug"]
        c = ctx()
        cv, spec = render_with_spec(r, c)
        self.assertEqual(lint_sprite(cv, r, c), [])
        x0, y0, x1, y1 = cv.bbox()
        self.assertEqual((x1 - x0, y1 - y0), (r["size"][0] + 2, r["size"][1] + 2))   # 외곽선 1px씩
        body = [STYLE.color(f"{r['colors'][0]}.{i}") for i in range(2)]
        stitch = STYLE.color(f"{r['colors'][1]}.{r['stitch_step']}")
        self.assertEqual(cv.colors() - {STYLE.outline}, set(body) | {stitch})
        bad = dict(r, size=[2, 2])
        with self.assertRaises(RecipeError):
            render(bad, ctx())

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
        from pixelkit.makers.common import streaks
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


# ---------- 2단계: 팔레트 줄과 크기를 명세에서 ----------

def ramp_colors(prefix):
    return {STYLE.color(k) for k in STYLE._colors if k.rpartition(".")[0] == prefix}


def sized(rid, art_cells, **kw):
    r = with_values(rid, art_cells=art_cells, **kw)
    r.pop("states", None)
    return r


def assert_no_leftover_band(test, cv, n, material="wood.warm"):
    """그린 선반의 안쪽 가운데 열: 윗면 밑 외곽선부터 밑판까지 모든 줄이 뒤판·짙은 줄·쟁반·선반 판·외곽선 중 하나,
    선반 판 외곽선이 층 수만큼, 마지막 판 바로 밑이 밑판 윗줄(재질 단계 2)."""
    inside = {STYLE.color(k) for k in ("wood.back.0", "wood.back.1", "cream.2",
                                       f"{material}.3", f"{material}.4")}
    x = cv.w // 2
    col = [cv.get(x, y) for y in range(cv.h)]
    y = next(i for i, c in enumerate(col) if c[3] and c != STYLE.outline)
    y = col.index(STYLE.outline, y) + 1
    boards = 0
    while col[y] in inside or col[y] == STYLE.outline:
        boards += col[y] == STYLE.outline
        y += 1
    test.assertEqual(boards, n)
    test.assertEqual(col[y - 1], STYLE.outline)
    test.assertEqual(col[y], STYLE.color(f"{material}.2"))


class ShelfSizeTests(unittest.TestCase):
    def test_width_comes_from_art_cells(self):
        for cells, w in (([1, 3], 30), ([2, 3], 62), ([3, 3], 94)):
            with self.subTest(cells=cells):
                c = ctx()
                r = sized("bakery.shelf", cells, tiers=[["bread.loaf"], [], ["bread.bun"], []])
                cv, spec = render_with_spec(r, c)
                self.assertEqual(spec["_layout"]["size"][0], w)
                self.assertEqual((cv.w, cv.h), (cells[0] * 32, cells[1] * 32))
                self.assertEqual(lint_sprite(cv, r, c), [])
                self.assertEqual(c.problems, [])

    def test_every_size_and_tier_count_has_no_leftover_band(self):
        for cells, most in (([1, 3], 4), ([3, 3], 4), ([2, 2], 2), ([1, 2], 2), ([3, 2], 2)):
            for n in range(1, most + 1):
                with self.subTest(cells=cells, n=n):
                    c = ctx()
                    r = sized("bakery.shelf", cells, tiers=[[]] * n)
                    cv, spec = render_with_spec(r, c)
                    lay = spec["_layout"]
                    self.assertEqual(lay["tiers"][0][0], lay["inner"][0])
                    self.assertEqual(lay["tiers"][-1][1], lay["inner"][1])
                    assert_no_leftover_band(self, cv, n)
                    self.assertEqual(lint_sprite(cv, r, c), [])

    def test_low_shelf_is_shorter_and_holds_a_loaf_on_both_tiers(self):
        c = ctx()
        r = sized("bakery.shelf", [2, 2], tiers=[["bread.loaf"] * 3, ["bread.bun", "bread.roll"]])
        cv, spec = render_with_spec(r, c)
        self.assertEqual((cv.w, cv.h), (64, 64))
        self.assertLess(spec["_layout"]["size"][1], 62)
        self.assertEqual(c.problems, [])
        self.assertEqual(lint_sprite(cv, r, c), [])

    def test_low_shelf_refuses_more_tiers_than_fit(self):
        with self.assertRaises(RecipeError) as e:
            render(sized("bakery.shelf", [2, 2], tiers=[[]] * 3), ctx())
        self.assertTrue(str(e.exception).startswith("bakery.shelf:"))
        self.assertIn("1~2", str(e.exception))

    def test_art_cells_out_of_range_names_the_range(self):
        for cells in ([4, 3], [2, 1], [2, 4]):
            with self.subTest(cells=cells), self.assertRaises(RecipeError) as e:
                render(sized("bakery.shelf", cells), ctx())
            self.assertTrue(str(e.exception).startswith("bakery.shelf:"))
            self.assertIn("1~3", str(e.exception))
            self.assertIn("2~3", str(e.exception))

    def test_pixels_instead_of_art_cells_is_refused(self):
        r = with_values("bakery.shelf", pixels=[64, 96])
        del r["art_cells"]
        with self.assertRaises(RecipeError) as e:
            render(r, ctx())
        self.assertIn("art_cells", str(e.exception))


def counter_without_items(cells, **kw):
    r = sized("bakery.counter", cells, top=[], **kw)
    r.pop("tray", None)
    return r


class CounterSizeTests(unittest.TestCase):
    def test_width_and_panels_come_from_art_cells(self):
        for cells, w, n in (([2, 2], 62, 3), ([3, 2], 94, 4), ([4, 2], 126, 5)):
            with self.subTest(cells=cells):
                c = ctx()
                r = counter_without_items(cells)
                cv, spec = render_with_spec(r, c)
                lay = spec["_layout"]
                self.assertEqual(lay["size"][0], w)
                self.assertEqual(len(lay["panels"]), n)
                self.assertEqual(len({pw for _, pw in lay["panels"]}), 1)      # 판 폭이 모두 같다
                self.assertEqual(lint_sprite(cv, r, c), [])
                self.assertEqual(c.problems, [])

    def test_panel_dividers_are_evenly_spaced_in_the_painted_front(self):
        for cells in ([2, 2], [3, 2], [4, 2]):
            with self.subTest(cells=cells):
                cv, spec = render_with_spec(counter_without_items(cells), ctx())
                lay = spec["_layout"]
                ox = (cv.w - (lay["size"][0] + 2)) // 2 + 1
                oy = cv.h - (lay["size"][1] + 2) + 1
                y = oy + lay["front"][0] + 10                                 # 판 가운데 줄
                xs = [x - ox for x in range(ox + 1, ox + lay["size"][0] - 1) if cv.get(x, y) == STYLE.outline]
                self.assertEqual(len(xs), len(lay["panels"]) - 1)
                steps = {b - a for a, b in zip(xs, xs[1:])}
                self.assertLessEqual(len(steps), 1)
                left = xs[0] - 1 if xs else None
                right = lay["size"][0] - 2 - xs[-1] if xs else None
                self.assertLessEqual(abs(left - right), 1)                     # 양 끝 판도 같은 폭(가운데 맞춤)

    def test_todays_counter_keeps_four_panels_of_19px(self):
        _, spec = render_with_spec(RECIPES["bakery.counter"], ctx())
        self.assertEqual(spec["_layout"]["panels"], [[3, 19], [26, 19], [49, 19], [72, 19]])

    def test_art_cells_out_of_range_names_the_range(self):
        for cells in ([1, 2], [5, 2], [3, 3]):
            with self.subTest(cells=cells), self.assertRaises(RecipeError) as e:
                render(counter_without_items(cells), ctx())
            self.assertTrue(str(e.exception).startswith("bakery.counter:"))
            self.assertIn("2~4", str(e.exception))


class FurniturePaletteTests(unittest.TestCase):
    def test_shelf_in_another_material_has_no_warm_wood(self):
        c = ctx()
        r = with_values("bakery.shelf", material="roof.slate", tiers=[[], [], [], []])
        cv = render(r, c)
        self.assertEqual(cv.colors() & ramp_colors("wood.warm"), set())
        self.assertTrue(cv.colors() & ramp_colors("roof.slate"))
        self.assertEqual(lint_sprite(cv, r, c), [])

    def test_shelf_back_and_tray_come_from_the_recipe(self):
        cv = render(with_values("bakery.shelf", back="metal.dark", tray="wall.cream", tiers=[[], []]), ctx())
        old = {STYLE.color(k) for k in ("wood.back.0", "wood.back.1", "cream.2")}   # 지금 읽는 단계(wood.back.2는 wood.warm.0과 같은 색)
        self.assertEqual(cv.colors() & old, set())
        self.assertIn(STYLE.color("wall.cream.2"), cv.colors())

    def test_counter_in_another_material_has_no_warm_wood(self):
        c = ctx()
        r = counter_without_items([3, 2], material="roof.teal")
        cv = render(r, c)
        self.assertEqual(cv.colors() & ramp_colors("wood.warm"), set())
        self.assertEqual(lint_sprite(cv, r, c), [])

    def test_glass_case_and_trolley_frames_come_from_the_recipe(self):
        metal = ramp_colors("metal") | ramp_colors("metal.dark")
        for rid, kw in (("bakery.glass-case", {"frame": "stone"}),
                        ("bakery.trolley", {"frame": "stone", "inside": "brick"})):
            with self.subTest(rid):
                c = ctx()
                r = with_values(rid, **kw)
                cv = render(r, c, "empty")
                self.assertEqual(cv.colors() & metal, set())
                self.assertTrue(cv.colors() & ramp_colors("stone"))
                self.assertEqual(lint_sprite(cv, r, c), [])

    def test_defaults_written_out_give_the_same_bytes(self):
        written = {"bakery.shelf": {"material": "wood.warm", "back": "wood.back", "tray": "cream"},
                   "bakery.counter": {"material": "wood.warm"},
                   "bakery.glass-case": {"frame": "metal"},
                   "bakery.trolley": {"frame": "metal", "inside": "metal.dark"}}
        for rid, kw in written.items():
            with self.subTest(rid):
                a = render(RECIPES[rid], ctx()).to_image().tobytes()
                b = render(with_values(rid, **kw), ctx()).to_image().tobytes()
                self.assertEqual(a, b)

    def test_short_ramp_raises_with_id_ramp_and_steps(self):
        cases = (("bakery.shelf", "material", "metal", 5), ("bakery.shelf", "back", "white", 2),
                 ("bakery.shelf", "tray", "red", 3), ("bakery.counter", "material", "trim", 5),
                 ("bakery.glass-case", "frame", "red", 3), ("bakery.trolley", "frame", "red", 3),
                 ("bakery.trolley", "inside", "board.green", 3))
        for rid, key, ramp, need in cases:
            with self.subTest(rid=rid, key=key), self.assertRaises(RecipeError) as e:
                render(with_values(rid, **{key: ramp}), ctx())
            msg = str(e.exception)
            self.assertTrue(msg.startswith(f"{rid}:"), msg)
            self.assertIn(ramp, msg)
            self.assertIn(str(need), msg)

    def test_ramp_key_must_be_a_name(self):
        for bad in (5, ["wood.warm"], None):
            with self.subTest(bad=bad), self.assertRaises(RecipeError) as e:
                render(with_values("bakery.shelf", material=bad), ctx())
            self.assertTrue(str(e.exception).startswith("bakery.shelf:"))


if __name__ == "__main__":
    unittest.main()
