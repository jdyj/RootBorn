"""방 만들기 — 벽 모양·바닥 종류와 띠 색(top), 견본판 명령(room styles).

사용자 요청(2026-11-14): 벽 8가지·바닥 9가지 견본을 보고 "오 좋아 이거 말고도 다른것도 계속 만들어줘 유저가 자유롭게 만들 수 있도록".
그림의 특정 픽셀 색에는 기대지 않는다 — 검사 통과, 그림끼리 같은지 다른지, 어느 자리가 바뀌는지를 본다."""
import json
import subprocess
import sys
import tempfile
import unittest
from itertools import combinations
from pathlib import Path

from PIL import Image

from pixelkit.cli import load_kit
from pixelkit.lint import lint_tile
from pixelkit.makers import RecipeError
from pixelkit.recipes import Ctx, render
from pixelkit.room_tiles import (classify, draw_tile, lint_room_tile, plan_tiles, tile_layout, top_concept, top_label,
                                 wall_concept, wall_label, wall_styles, wall_top, wall_tops)
from tests.test_room_tiles import BOX, END, MID, SAMPLE, SHAPES, grid

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
KIT = load_kit(str(ROOT / "art" / "kit"))
STYLE = KIT.style
BAKERY_FLOOR = KIT.recipes["bakery.tile.floor"]

# 견본에서 정식으로 옮긴 것(W3~W8, 벽돌은 크림·붉은 둘)과 새로 더한 것. 이름은 README 표와 같다.
SAMPLE_WALLS = ("wallpaper-sprig", "panel-plank", "tile-white", "brick-cream", "brick-red", "paint-mint", "paint-green")
SAMPLE_FLOORS = ("floor.checker-soft", "floor.plank", "floor.herringbone", "floor.stone-slab", "floor.mosaic",
                 "floor.carpet", "floor.tatami", "floor.concrete")


def room_floors():
    """정식 바닥 명세(세트 floor)."""
    return sorted(rid for rid, r in KIT.recipes.items() if rid.split(".")[0] == "floor")


def all_keys():
    keys = set()
    for g in (SAMPLE, SHAPES, BOX, MID, END):
        p = plan_tiles(g)
        for rows in (p.wall, p.ground):
            keys |= {k for row in rows for k in row if k}
    keys |= {k for row in classify(grid("...", "###", "...")) for k in row if k}
    keys |= {k for row in classify(grid("##", "..")) for k in row if k}
    return sorted(keys)


def pixels(cv):
    return cv.to_image().tobytes()


class WallStyleTests(unittest.TestCase):
    def test_samples_are_registered_and_at_least_four_more(self):
        names = wall_styles()
        for n in ("paper", "plaster") + SAMPLE_WALLS:
            self.assertIn(n, names)
        self.assertGreaterEqual(len(names), 2 + len(SAMPLE_WALLS) + 4, names)

    def test_every_style_draws_every_key_and_passes_room_lint(self):
        keys = all_keys()
        for wall in wall_styles():
            for key in keys:
                with self.subTest(wall=wall, key=key):
                    cv = draw_tile(STYLE, key, wall, BAKERY_FLOOR)
                    self.assertEqual((cv.w, cv.h), (32, 32))
                    self.assertEqual(lint_room_tile(cv, key, STYLE), [])

    def test_styles_look_different_from_each_other(self):
        """이름만 다른 복사본이 없다: 정면 두 장(아래·위)이 모양마다 다르다."""
        looks = {w: (pixels(draw_tile(STYLE, "face:1", w, BAKERY_FLOOR)), pixels(draw_tile(STYLE, "face:2:c", w, BAKERY_FLOOR)))
                 for w in wall_styles()}
        for a, b in combinations(sorted(looks), 2):
            with self.subTest(a=a, b=b):
                self.assertNotEqual(looks[a][0], looks[b][0])
                self.assertNotEqual(looks[a][1], looks[b][1])

    def test_narrow_face_differs_between_styles_of_different_colour(self):
        key = plan_tiles(END).wall[6][4]                 # 세로 벽 끝 좁은 정면(k=1)
        self.assertIsNotNone(tile_layout(key).narrow)
        seen = {}
        for w in wall_styles():
            seen.setdefault(pixels(draw_tile(STYLE, key, w, BAKERY_FLOOR)), []).append(w)
        self.assertGreaterEqual(len(seen), len(wall_styles()) - 1, seen.values())

    def test_every_style_has_a_default_top_that_can_be_picked(self):
        for w in wall_styles():
            with self.subTest(wall=w):
                self.assertIn(wall_top(w), wall_tops())


class TopTests(unittest.TestCase):
    CAP = plan_tiles(MID).wall[4][4]                     # 양옆이 바닥인 세로 벽 띠
    STRETCH = plan_tiles(MID).wall[1][4]                 # 띠 + 늘인 정면

    def test_tops_include_the_wall_top_rows_and_every_default(self):
        tops = wall_tops()
        self.assertGreaterEqual(len([t for t in tops if t.startswith("wall.top.")]), 4, tops)
        for w in wall_styles():
            self.assertIn(wall_top(w), tops)

    def test_top_changes_only_the_band(self):
        layout = tile_layout(self.STRETCH)
        for wall in ("paper", "wallpaper-sprig", "brick-cream"):
            base = draw_tile(STYLE, self.STRETCH, wall, BAKERY_FLOOR)
            for top in wall_tops():
                if top == wall_top(wall):
                    continue
                cv = draw_tile(STYLE, self.STRETCH, wall, BAKERY_FLOOR, top=top)
                with self.subTest(wall=wall, top=top):
                    changed = {(x, y) for y in range(32) for x in range(32) if cv.get(x, y) != base.get(x, y)}
                    self.assertTrue(changed)
                    self.assertLessEqual(changed, set(layout.band))
                    self.assertEqual(lint_room_tile(cv, self.STRETCH, STYLE), [])

    def test_default_top_is_the_same_picture_as_no_top(self):
        for wall in wall_styles():
            self.assertEqual(pixels(draw_tile(STYLE, self.CAP, wall, BAKERY_FLOOR)),
                             pixels(draw_tile(STYLE, self.CAP, wall, BAKERY_FLOOR, top=wall_top(wall))), wall)

    def test_top_does_not_touch_faces_or_floors(self):
        for key in ("face:1", "face:2:c", "floor", "floor:shade"):
            self.assertEqual(pixels(draw_tile(STYLE, key, "paper", BAKERY_FLOOR)),
                             pixels(draw_tile(STYLE, key, "paper", BAKERY_FLOOR, top="wall.top.slate")), key)

    def test_unknown_top_is_recipe_error_listing_known(self):
        with self.assertRaises(RecipeError) as e:
            draw_tile(STYLE, self.CAP, "paper", BAKERY_FLOOR, top="wood.warm")
        for t in wall_tops():
            self.assertIn(t, str(e.exception))


class FloorTests(unittest.TestCase):
    def test_samples_are_specs_and_at_least_four_more(self):
        floors = room_floors()
        for f in SAMPLE_FLOORS:
            self.assertIn(f, floors)
        self.assertGreaterEqual(len(floors), len(SAMPLE_FLOORS) + 4, floors)

    def test_every_floor_passes_tile_lint_and_room_lint_with_shades(self):
        for fid in room_floors():
            spec = KIT.recipes[fid]
            with self.subTest(floor=fid):
                self.assertEqual(spec["kind"], "tile")
                ctx = KIT.ctx()
                cv = render(spec, ctx)
                self.assertEqual((cv.w, cv.h), (32, 32))
                self.assertEqual(lint_tile(cv, spec, ctx), [])
                for key in ("floor", "floor:shade", "floor:shade:8-24"):
                    t = draw_tile(STYLE, key, "paper", spec)
                    self.assertEqual(lint_room_tile(t, key, STYLE), [], key)
                shade = draw_tile(STYLE, "floor:shade", "paper", spec)
                changed = {y for y in range(32) for x in range(32) if shade.get(x, y) != cv.get(x, y)}
                self.assertTrue(changed)                   # 벽 밑 그림자가 보인다(가장 어두운 단계가 바탕과 다르다)
                self.assertLessEqual(max(changed), 1)

    def test_floors_look_different_from_each_other(self):
        looks = {f: pixels(render(KIT.recipes[f], Ctx(STYLE, {}))) for f in room_floors() + ["bakery.tile.floor"]}
        for a, b in combinations(sorted(looks), 2):
            self.assertNotEqual(looks[a], looks[b], (a, b))

    def test_plank_board_and_direction_are_checked(self):
        base = dict(KIT.recipes["floor.plank"])
        for bad in ({"board": 12}, {"board": "8"}, {"vertical": "yes"}, {"colors": []}):
            with self.subTest(bad=bad), self.assertRaises(RecipeError):
                render({**base, **bad}, KIT.ctx())
        flat = render(base, KIT.ctx())
        turned = render({**base, "vertical": True}, KIT.ctx())
        self.assertEqual([[turned.get(y, x) for y in range(32)] for x in range(32)],
                         [[flat.get(x, y) for y in range(32)] for x in range(32)])      # 세로 판은 가로 판을 돌린 것

    def test_floor_ids_follow_the_set_folder(self):
        for fid in room_floors():
            self.assertRegex(fid, r"^floor\.[a-z]+(-[a-z]+)*$")


class PaletteTests(unittest.TestCase):
    # 1단계 줄 wall.cream은 cream·paper와 hex를 나눠 쓴다(그때 합친 것). 그 밖의 방 재료 줄은 남과 hex를 나누지 않는다 —
    # 같은 hex가 두 줄에 있으면 Style.darker가 이름순 첫 줄을 따라가 floor:shade가 어두워지는 줄이 달라질 수 있다.
    SHARED_ON_PURPOSE = {"wall.cream"}

    def test_room_material_rows_share_no_hex_with_any_row(self):
        raw = json.loads((ROOT / "art/kit/style.json").read_text("utf-8"))["palette"]
        owners = {}
        for name, ramp in raw.items():
            for h in ramp:
                owners.setdefault(h.lower(), set()).add(name)
        for name in raw:
            if not name.startswith(("wall.", "floor.", "brick.")) or name in self.SHARED_ON_PURPOSE:
                continue
            for h in raw[name]:
                with self.subTest(row=name, hex=h):
                    self.assertEqual(owners[h.lower()], {name})

    def test_new_rows_go_dark_to_light(self):
        raw = json.loads((ROOT / "art/kit/style.json").read_text("utf-8"))["palette"]

        def luma(h):
            r, g, b = int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16)
            return 0.299 * r + 0.587 * g + 0.114 * b
        for name in [n for n in raw if n.startswith(("wall.", "floor.", "brick."))]:
            ramp = raw[name]
            with self.subTest(row=name):
                self.assertEqual([luma(h) for h in ramp], sorted(luma(h) for h in ramp))


class StylesCommandTests(unittest.TestCase):
    def run_kit(self, *args):
        r = subprocess.run([sys.executable, str(HERE.parent / "kit.py"), "room", "styles", *args],
                           capture_output=True, text=True, encoding="utf-8")
        return r.returncode, r.stdout, r.stderr

    def test_list_prints_every_wall_top_and_floor(self):
        code, out, err = self.run_kit("--list")
        self.assertEqual(code, 0, err)
        for name in wall_styles() + wall_tops() + room_floors():
            self.assertIn(name, out)

    def test_draws_a_wall_board_and_a_floor_board(self):
        with tempfile.TemporaryDirectory() as d:
            code, out, err = self.run_kit("--out", d, "--scale", "2")
            self.assertEqual(code, 0, out + err)
            names = sorted(p.name for p in Path(d).iterdir())
            self.assertEqual(names, ["styles-floors.png", "styles-floors_x2.png", "styles-walls.png", "styles-walls_x2.png"])
            with Image.open(Path(d) / "styles-walls.png") as one, Image.open(Path(d) / "styles-walls_x2.png") as two:
                self.assertGreater(one.width * one.height, 0)
                self.assertGreater(two.width * two.height, 2 * one.width * one.height)     # 견본판은 줄바꿈하므로 넓이로 본다

    def test_styles_takes_no_room_id(self):
        code, _, err = self.run_kit("test.two-rooms")
        self.assertEqual(code, 2)


class ConceptTests(unittest.TestCase):
    """종류마다 한글 이름(label)과 컨셉 한 줄(concept)이 있다 — 새 종류를 더하면서 빼먹으면 실패한다.
    문구에는 기대지 않는다(비어 있지 않음, 종류끼리 서로 다름)."""

    def assert_filled_and_distinct(self, pairs):
        for name, (label, concept) in pairs.items():
            with self.subTest(name=name):
                for v in (label, concept):
                    self.assertIsInstance(v, str)
                    self.assertTrue(v.strip(), name)
        labels = [l for l, _ in pairs.values()]
        concepts = [c for _, c in pairs.values()]
        self.assertEqual(len(set(labels)), len(labels), "한글 이름이 겹친다")
        self.assertEqual(len(set(concepts)), len(concepts), "컨셉이 겹친다")

    def test_every_wall_style_has_label_and_concept(self):
        self.assert_filled_and_distinct({w: (wall_label(w), wall_concept(w)) for w in wall_styles()})

    def test_every_top_has_label_and_concept(self):
        self.assert_filled_and_distinct({t: (top_label(t), top_concept(t)) for t in wall_tops()})

    def test_every_floor_spec_has_label_and_concept(self):
        self.assert_filled_and_distinct({f: (KIT.recipes[f].get("label"), KIT.recipes[f].get("concept"))
                                         for f in room_floors()})

    def test_unknown_name_is_recipe_error(self):
        for fn in (wall_label, wall_concept, top_label, top_concept):
            with self.subTest(fn=fn.__name__), self.assertRaises(RecipeError):
                fn("no.such.name")

    def test_recipe_label_and_concept_must_be_strings(self):
        from pixelkit.recipes import load_recipes
        for bad in ({"label": 3}, {"concept": ["a"]}, {"label": ""}):
            with self.subTest(bad=bad), tempfile.TemporaryDirectory() as d:
                (Path(d) / "a.json").write_text(json.dumps([{"id": "x.y", "kind": "tile", **bad}]), encoding="utf-8")
                with self.assertRaises(RecipeError):
                    load_recipes(d)

    def test_list_prints_label_and_concept_of_every_kind(self):
        code, out, err = StylesCommandTests.run_kit(None, "--list")
        self.assertEqual(code, 0, err)
        for w in wall_styles():
            self.assertIn(wall_label(w), out)
            self.assertIn(wall_concept(w), out)
        for t in wall_tops():
            self.assertIn(top_label(t), out)
            self.assertIn(top_concept(t), out)
        for f in room_floors():
            self.assertIn(KIT.recipes[f]["label"], out)
            self.assertIn(KIT.recipes[f]["concept"], out)

    def test_readme_tables_list_exactly_the_registered_ids(self):
        import re
        text = (ROOT / "Tools" / "pixel-kit" / "README.md").read_text("utf-8")

        def ids(heading, pattern):
            body = text.split(heading, 1)[1].split(chr(10) + "###", 1)[0].split(chr(10) + "## ", 1)[0]
            return {m.group(1) for line in body.splitlines() if line.startswith("| `")
                    for m in [re.match(pattern, line)] if m}

        self.assertEqual(ids("### 벽 모양", r"\| `([a-z-]+)` \|"), set(wall_styles()))
        self.assertEqual(ids("### 띠 색", r"\| `(wall\.[a-z.]+)` \|"), set(wall_tops()))
        self.assertEqual({f for f in ids("### 바닥", r"\| `(floor\.[a-z-]+)` \|")}, set(room_floors()))

    def test_styles_boards_draw_without_a_korean_font(self):
        """글꼴이 없는 길은 프로세스 안에서 돈다(다른 프로세스에는 가로채기가 닿지 않는다): 끝 코드 0, 그림이 생기고, 알림 한 줄."""
        import contextlib
        import io
        from unittest import mock
        from PIL import ImageFont
        import kit as kit_main
        from pixelkit import boards

        def run(font_result):
            out, err = (io.TextIOWrapper(io.BytesIO(), encoding="utf-8") for _ in range(2))
            with tempfile.TemporaryDirectory() as d, contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
                with mock.patch.object(boards, "find_korean_font", return_value=font_result):
                    code = kit_main.main(["room", "styles", "--out", d, "--scale", "1"])
                made = sorted(p.name for p in Path(d).iterdir())
            err.flush()
            return code, made, err.buffer.getvalue().decode("utf-8")

        code, made, err = run(None)
        self.assertEqual(code, 0, err)
        self.assertIn("styles-walls.png", made)
        self.assertIn("styles-floors.png", made)
        self.assertEqual(len([l for l in err.splitlines() if "한글 글꼴을 못 찾아" in l]), 1, err)
        code, _, err = run(ImageFont.load_default(size=14))
        self.assertEqual(code, 0, err)
        self.assertNotIn("한글 글꼴을 못 찾아", err)                # 가로채기를 바꾸면 결과가 달라진다 — 테스트가 이 길을 실제로 돈다


if __name__ == "__main__":
    unittest.main()
