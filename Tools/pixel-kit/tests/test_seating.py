"""의자(furniture.chair)와 책상(furniture.desk): 방향, 앉은 사람 앞뒤 층, 캐릭터에서 얻은 치수."""
import copy
import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.cli import Kit, draw_and_check
from pixelkit.lint import lint_sprite
from pixelkit.parts import load_parts
from pixelkit.recipes import Ctx, RecipeError, ghost_ctx, load_recipes, render_with_spec
from pixelkit.scene import build_scene
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
KIT = ROOT / "art" / "kit"
STYLE = Style.load(str(KIT / "style.json"))
PARTS = load_parts(str(KIT / "parts"), STYLE)
FACINGS = ("south", "north", "west", "east")
LAYERS = ("all", "back", "front")
CHAIR = {"id": "t.chair", "kind": "furniture", "part": "furniture.chair", "cells": [1, 1], "art_cells": [1, 2],
         "material": "wood.warm", "cushion": "book.blue"}      # 방석 줄은 단계 셋인 아무 줄(실제 명세는 cushion.*)
DESK = {"id": "t.desk", "kind": "furniture", "part": "furniture.desk", "cells": [2, 1], "art_cells": [2, 2],
        "material": "wood.warm"}


def ctx():
    return Ctx(STYLE, PARTS, [])


def spec(base, **kw):
    r = copy.deepcopy(base)
    r.update(kw)
    return r


def draw(recipe):
    """그린 캔버스, 만드는 부품이 적은 layout, 그 그림의 문제(만드는 부품 + 검사)."""
    c = ctx()
    cv, s = render_with_spec(recipe, c)
    body, _ = render_with_spec(recipe, ghost_ctx(c))
    return cv, s["_layout"], list(c.problems) + lint_sprite(cv, s, c, layout=s["_layout"], body=body)


def origin(cv, layout):
    """그린 그림(외곽선 전) 좌표 → 렌더된 캔버스 좌표의 밀림(사방 1px 외곽선 여유, 아래 가운데)."""
    w, h = layout["size"]
    return (cv.w - (w + 2)) // 2 + 1, cv.h - (h + 2) + 1


def opaque(cv):
    return {(x, y) for y in range(cv.h) for x in range(cv.w) if cv.get(x, y)[3]}


def stacked(back, front):
    out = Canvas(back.w, back.h)
    out.paste(back, 0, 0)
    out.paste(front, 0, 0)
    return out


def mirrored(cv):
    out = Canvas(cv.w, cv.h)
    for y in range(cv.h):
        for x in range(cv.w):
            out.px(cv.w - 1 - x, y, cv.get(x, y))
    return out


class ChairDrawingTests(unittest.TestCase):
    def test_every_facing_and_layer_is_32x64_and_lint_clean(self):
        for cushion in ("book.blue", None):
            for facing in FACINGS:
                for layer in LAYERS:
                    with self.subTest(cushion=cushion, facing=facing, layer=layer):
                        r = spec(CHAIR, facing=facing, layer=layer)
                        if cushion is None:
                            del r["cushion"]
                        cv, _, problems = draw(r)
                        self.assertEqual((cv.w, cv.h), (32, 64))
                        self.assertEqual([(p.code, p.message) for p in problems], [])

    def test_back_then_front_in_the_same_place_is_byte_equal_to_all(self):
        for cushion in ("book.blue", None):
            for facing in FACINGS:
                with self.subTest(cushion=cushion, facing=facing):
                    r = spec(CHAIR, facing=facing)
                    if cushion is None:
                        del r["cushion"]
                    whole = draw(spec(r, layer="all"))[0]
                    back = draw(spec(r, layer="back"))[0]
                    front = draw(spec(r, layer="front"))[0]
                    self.assertEqual(stacked(back, front).to_image().tobytes(), whole.to_image().tobytes())
                    self.assertTrue(opaque(front))
                    self.assertTrue(opaque(back))

    def test_back_layer_above_the_legs_is_what_sits_behind_the_sitter(self):
        # 다리 줄(좌판 밑)은 두 층이 함께 갖는다. 그 위에서 back은 앞 조각이 아닌 것만이다:
        # 남쪽·옆은 등받이와 좌판 윗면, 북쪽은 아무것도 없다(좌판이 등받이에 가려진다).
        for facing in FACINGS:
            with self.subTest(facing=facing):
                back, lay, _ = draw(spec(CHAIR, facing=facing, layer="back"))
                front = draw(spec(CHAIR, facing=facing, layer="front"))[0]
                _, oy = origin(back, lay)
                legs = oy + lay["shared_from"]
                above = {p for p in opaque(back) if p[1] < legs - 1}      # 다리 줄 위 외곽선 한 줄은 뺀다
                self.assertEqual(bool(above), facing != "north")
                both = [p for p in above if back.get(*p) != STYLE.outline
                        and front.get(*p)[3] and front.get(*p) != STYLE.outline]
                self.assertEqual(both, [])                                 # 다리 줄 위에서 겹쳐 그린 몸통은 없다

    def test_south_front_has_nothing_above_the_seat_top_row(self):
        cv, lay, _ = draw(spec(CHAIR, facing="south", layer="front"))
        ox, oy = origin(cv, lay)
        seat_top = oy + lay["seat"][1]
        self.assertTrue(opaque(cv))
        self.assertGreaterEqual(min(y for _, y in opaque(cv)), seat_top)

    def test_north_front_reaches_the_back_top_row(self):
        cv, lay, _ = draw(spec(CHAIR, facing="north", layer="front"))
        _, oy = origin(cv, lay)
        self.assertIn(oy + lay["back_top"], {y for _, y in opaque(cv)})

    def test_front_layer_covers_the_backrest_only_when_facing_north(self):
        # 앉은 사람 앞에 오는 것: 북쪽은 등받이, 그 밖은 좌판 윗면 줄 아래뿐
        for facing in FACINGS:
            with self.subTest(facing=facing):
                cv, lay, _ = draw(spec(CHAIR, facing=facing, layer="front"))
                _, oy = origin(cv, lay)
                above = [p for p in opaque(cv) if p[1] < oy + lay["seat"][1]]
                self.assertEqual(bool(above), facing == "north")

    def test_sizes_come_from_the_character(self):
        # 캐릭터 프레임 32x59, 발이 칸 아랫변: 좌판 윗면 13~15px(다리 24px의 무릎), 폭 20~22px(몸통).
        # 등받이 꼭대기 25~28px: 브리프는 34~38이었으나 캐릭터와 맞춰 본 판(task-11-report.md)에서 36px은 서 있는
        # 캐릭터의 턱까지 오고, 엉덩이를 좌판에 둔 캐릭터의 머리 아래 절반을 뒤(북쪽)에서 가렸다. 다리로 잰 척도에서
        # 실제 의자처럼 등받이가 좌판 위로 좌판 높이만큼 솟게 했다: 남쪽·옆 14 + 14 = 28, 북쪽 11 + 14 = 25.
        for facing in FACINGS:
            with self.subTest(facing=facing):
                cv, lay, _ = draw(spec(CHAIR, facing=facing))
                ox, oy = origin(cv, lay)
                self.assertEqual(oy + lay["floor"], cv.h)          # 바닥 = 캔버스 아랫변
                floor = lay["floor"]
                sx, sy, sw, sh = lay["seat"]
                self.assertTrue(13 <= floor - sy <= 15, floor - sy)
                self.assertTrue(25 <= floor - lay["back_top"] <= 28, floor - lay["back_top"])
                self.assertTrue(20 <= lay["size"][0] <= 22, lay["size"])
                self.assertGreater(sh, 0)
                px, py = lay["sit_point"]
                self.assertTrue(sx <= px < sx + sw and sy <= py < sy + sh)
                self.assertLessEqual(abs(px - (sx + (sw - 1) / 2)), 1)
                # 앉은 사람의 엉덩이 자리에는 의자가 그려져 있다
                self.assertTrue(cv.get(ox + px, oy + py)[3])
                if facing in ("south", "north"):
                    self.assertEqual(sw, lay["size"][0])

    def test_back_top_is_the_highest_painted_row(self):
        for facing in FACINGS:
            with self.subTest(facing=facing):
                cv, lay, _ = draw(spec(CHAIR, facing=facing))
                _, oy = origin(cv, lay)
                self.assertEqual(min(y for _, y in opaque(cv)), oy + lay["back_top"] - 1)   # 그 위 한 줄은 외곽선

    def test_east_is_the_west_silhouette_mirrored_with_light_from_the_left_again(self):
        for layer in LAYERS:
            with self.subTest(layer=layer):
                west = draw(spec(CHAIR, facing="west", layer=layer))[0]
                east = draw(spec(CHAIR, facing="east", layer=layer))[0]
                self.assertEqual(opaque(east), opaque(mirrored(west)))
                self.assertNotEqual(east.to_image().tobytes(), mirrored(west).to_image().tobytes())

    def test_cushion_colors_only_with_a_cushion(self):
        navy = {STYLE.color(f"book.blue.{i}") for i in range(3)}
        with_c = draw(spec(CHAIR))[0].colors()
        r = spec(CHAIR)
        del r["cushion"]
        without = draw(r)[0].colors()
        self.assertTrue(navy & with_c)
        self.assertFalse(navy & without)

    def test_material_ramp_comes_from_the_recipe(self):
        warm = {STYLE.color(f"wood.warm.{i}") for i in range(5)}
        cv = draw(spec(CHAIR, material="wood.school"))[0]
        self.assertFalse(warm & cv.colors())

    def test_unknown_values_raise_with_the_known_list(self):
        for key, bad, known in (("facing", "up", ("south", "north", "west", "east")),
                                ("layer", "middle", ("all", "back", "front"))):
            with self.subTest(key=key):
                with self.assertRaises(RecipeError) as e:
                    draw(spec(CHAIR, **{key: bad}))
                msg = str(e.exception)
                self.assertTrue(msg.startswith("t.chair"))
                self.assertIn(bad, msg)
                for k in known:
                    self.assertIn(k, msg)

    def test_art_cells_must_be_1_by_2(self):
        with self.assertRaises(RecipeError):
            draw(spec(CHAIR, art_cells=[2, 2]))

    def test_short_cushion_ramp_raises(self):
        with self.assertRaises(RecipeError) as e:
            draw(spec(CHAIR, cushion="glass.streak"))
        self.assertIn("cushion", str(e.exception))


class DeskDrawingTests(unittest.TestCase):
    def test_south_desk_is_64x64_and_lint_clean_for_every_drawer_side(self):
        for drawers in ("right", "left", "none", "both"):
            with self.subTest(drawers=drawers):
                cv, lay, problems = draw(spec(DESK, drawers=drawers))
                self.assertEqual((cv.w, cv.h), (64, 64))
                self.assertEqual([(p.code, p.message) for p in problems], [])

    def test_side_desk_is_32x64_and_lint_clean(self):
        for facing in ("west", "east"):
            for drawers in ("right", "left", "none", "both"):
                with self.subTest(facing=facing, drawers=drawers):
                    cv, _, problems = draw(spec(DESK, facing=facing, drawers=drawers, art_cells=[1, 2]))
                    self.assertEqual((cv.w, cv.h), (32, 64))
                    self.assertEqual([(p.code, p.message) for p in problems], [])

    def test_kneehole_is_open_down_to_the_floor(self):
        for drawers in ("right", "left", "none", "both"):
            with self.subTest(drawers=drawers):
                cv, lay, _ = draw(spec(DESK, drawers=drawers))
                ox, oy = origin(cv, lay)
                kx, ky, kw, kh = lay["kneehole"]
                self.assertGreaterEqual(kw, 14)
                self.assertGreaterEqual(kh, 12)
                self.assertEqual(ky + kh, lay["size"][1])             # 바닥까지 뚫렸다
                for y in range(oy + ky, oy + ky + kh):
                    for x in range(ox + kx, ox + kx + kw):
                        self.assertEqual(cv.get(x, y)[3], 0, (x, y))

    def test_work_height_is_10_to_13_above_the_chair_seat(self):
        _, chair, _ = draw(spec(CHAIR))
        seat = chair["floor"] - chair["seat"][1]
        for facing, cells in (("south", [2, 2]), ("west", [1, 2]), ("east", [1, 2])):
            with self.subTest(facing=facing):
                cv, lay, _ = draw(spec(DESK, facing=facing, art_cells=cells))
                self.assertTrue(10 <= lay["work_height"] - seat <= 13, lay["work_height"] - seat)
                _, oy = origin(cv, lay)
                self.assertEqual(oy + lay["floor"], cv.h)
                tx, ty, tw, th = lay["top"]
                self.assertEqual(lay["floor"] - (ty + th - 1), lay["work_height"])   # 윗면 앞 모서리 줄

    def test_kneehole_side_follows_the_drawers(self):
        centre = {}
        for drawers in ("right", "left", "none", "both"):
            cv, lay, _ = draw(spec(DESK, drawers=drawers))
            kx, _, kw, _ = lay["kneehole"]
            centre[drawers] = (kx + (kw - 1) / 2, kw, lay["size"][0])
        mid = (centre["both"][2] - 1) / 2
        self.assertLessEqual(abs(centre["both"][0] - mid), 1)
        self.assertLessEqual(abs(centre["none"][0] - mid), 1)
        self.assertLess(centre["right"][0], mid)
        self.assertGreater(centre["left"][0], mid)
        self.assertGreater(centre["none"][1], centre["right"][1])     # 다리만이면 가장 넓다
        self.assertLess(centre["both"][1], centre["right"][1])

    def test_top_ramp_and_items(self):
        cv, _, problems = draw(spec(DESK, top="wood.top", top_items=[{"part": "book.stack", "x": 10}]))
        self.assertEqual(problems, [])
        self.assertIn(STYLE.color("wood.top.1"), cv.colors())

    def test_item_off_the_top_reports_slot_clipped(self):
        c = ctx()
        render_with_spec(spec(DESK, top_items=[{"part": "book.stack", "x": 60}]), c)
        self.assertIn("slot-clipped", [p.code for p in c.problems])

    def test_unknown_values_raise_with_the_known_list(self):
        for key, bad, known in (("facing", "north", ("south", "west", "east")),
                                ("drawers", "top", ("right", "left", "none", "both"))):
            with self.subTest(key=key):
                with self.assertRaises(RecipeError) as e:
                    draw(spec(DESK, **{key: bad}))
                msg = str(e.exception)
                self.assertTrue(msg.startswith("t.desk"))
                for k in known:
                    self.assertIn(k, msg)

    def test_art_cells_follow_the_facing(self):
        for facing, cells in (("south", [1, 2]), ("west", [2, 2])):
            with self.subTest(facing=facing):
                with self.assertRaises(RecipeError):
                    draw(spec(DESK, facing=facing, art_cells=cells))

    def test_bad_top_items_raise(self):
        with self.assertRaises(RecipeError):
            draw(spec(DESK, top_items=[{"part": "book.stack"}]))

    def test_one_cell_south_desk_has_legs_only_and_an_open_kneehole(self):
        cv, lay, problems = draw(spec(DESK, art_cells=[1, 2], cells=[1, 1], drawers="none"))
        self.assertEqual((cv.w, cv.h), (32, 64))
        self.assertEqual([(p.code, p.message) for p in problems], [])
        self.assertEqual(lay["size"][0], 30)
        ox, oy = origin(cv, lay)
        kx, ky, kw, kh = lay["kneehole"]
        self.assertGreaterEqual(kw, 14)
        self.assertEqual(ky + kh, lay["size"][1])
        for y in range(oy + ky, oy + ky + kh):
            for x in range(ox + kx, ox + kx + kw):
                self.assertEqual(cv.get(x, y)[3], 0, (x, y))

    def test_one_cell_south_desk_with_drawers_raises(self):
        # 서랍장(22px)을 넣으면 무릎 자리가 남지 않는다.
        for drawers in ("right", "left", "both"):
            with self.subTest(drawers=drawers):
                with self.assertRaises(RecipeError) as e:
                    draw(spec(DESK, art_cells=[1, 2], drawers=drawers))
                self.assertIn("none", str(e.exception))


class RealSeatingRecipesTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.recipes = load_recipes(str(KIT / "recipes"))
        cls.kit = Kit(STYLE, PARTS, {}, cls.recipes)

    def test_table(self):
        layer_states = {f"{f}-{l}" for f in ("north", "west", "east") for l in ("back", "front")}
        chair_states = {"north", "west", "east", "back", "front"} | layer_states
        expect = {
            "home.chair": ("furniture.chair", [1, 1], [1, 2], chair_states, "chair.black"),
            "home.chair-reward": ("furniture.chair", [1, 1], [1, 2], chair_states, "chair.quest-reward"),
            "school.chair-south": ("furniture.chair", [1, 1], [1, 2], {"back", "front"}, "school.chair.south"),
            "school.chair-north": ("furniture.chair", [1, 1], [1, 2], {"back", "front"}, "school.chair.north"),
            "home.desk": ("furniture.desk", [2, 1], [2, 2], set(), "growth31.assembly.desk"),
            "home.study-desk": ("furniture.desk", [2, 1], [2, 2], set(), "story.study-desk"),
            "school.desk": ("furniture.desk", [2, 1], [2, 2], set(), "school.desk"),
            "school.desk-open-book": ("furniture.desk", [2, 1], [2, 2], set(), "school.desk.with.open.book"),
            "school.teacher-desk": ("furniture.desk", [2, 1], [2, 2], set(), "school.teacher.desk"),
        }
        for rid, (part, cells, art, states, replaces) in expect.items():
            with self.subTest(rid):
                r = self.recipes[rid]
                self.assertEqual((r["part"], r["kind"], r["cells"], r["art_cells"]), (part, "furniture", cells, art))
                self.assertEqual(set(r.get("states") or {}), states)
                self.assertEqual(r["game"]["replaces"], replaces)

    def test_layer_states_are_the_facing_with_a_layer(self):
        for rid in ("home.chair", "home.chair-reward"):
            st = self.recipes[rid]["states"]
            for f in ("north", "west", "east"):
                self.assertEqual(st[f], {"facing": f})
                for l in ("back", "front"):
                    self.assertEqual(st[f"{f}-{l}"], {"facing": f, "layer": l})
            for l in ("back", "front"):
                self.assertEqual(st[l], {"layer": l})
        self.assertEqual(self.recipes["school.chair-north"]["facing"], "north")
        self.assertEqual(self.recipes["school.chair-south"].get("facing", "south"), "south")
        self.assertNotIn("cushion", self.recipes["school.chair-south"])
        self.assertNotIn("cushion", self.recipes["school.chair-north"])

    def test_reward_chair_differs_only_in_cushion(self):
        a, b = dict(self.recipes["home.chair"]), dict(self.recipes["home.chair-reward"])
        for k in ("id", "cushion", "game"):
            a.pop(k), b.pop(k)
        self.assertEqual(a, b)
        self.assertNotEqual(self.recipes["home.chair"]["cushion"], self.recipes["home.chair-reward"]["cushion"])

    def test_every_seating_picture_is_lint_clean(self):
        for rid, r in sorted(self.recipes.items()):
            if r.get("part") not in ("furniture.chair", "furniture.desk"):
                continue
            for st in [None] + sorted(r.get("states") or {}):
                with self.subTest(rid=rid, state=st):
                    _, problems = draw_and_check(self.kit, r, st)
                    self.assertEqual([(p.code, p.message) for p in problems], [])

    def test_the_two_home_desks_are_different_pictures(self):
        a = render_with_spec(self.recipes["home.desk"], ctx())[0]
        b = render_with_spec(self.recipes["home.study-desk"], ctx())[0]
        self.assertNotEqual(a.to_image().tobytes(), b.to_image().tobytes())

    def test_preview_scenes_build_and_hold_the_listed_things(self):
        for sid, need in (("home.scene.study", ("home.study-desk", "home.chair")),
                          ("school.scene.classroom", ("school.desk", "school.chair-north", "school.teacher-desk",
                                                      "school.bookcase", "school.archive-bookshelf"))):
            with self.subTest(sid):
                s = self.recipes[sid]
                cv = build_scene(s, self.recipes, ctx())
                self.assertEqual((cv.w, cv.h), (s["size"][0] * 32, s["size"][1] * 32))
                names = [p.get("id") for p in s["place"]]
                for n in need:
                    self.assertIn(n, names)
        study = [(p.get("id"), p.get("state")) for p in self.recipes["home.scene.study"]["place"]]
        for state in ("north", None):
            self.assertIn(("home.chair", state), study)
        self.assertTrue(any(i == "home.chair" and s in ("west", "east") for i, s in study))


if __name__ == "__main__":
    unittest.main()
