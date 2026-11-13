"""방 만들기 — 둘레 칸으로 벽·바닥 타일을 고르고 그린다(room_tiles).

그림의 특정 픽셀 색에는 기대지 않고 구조(크기, 투명 여부, 외곽선이 어느 변에 있는지, 열쇠가 같은지 다른지)를 본다."""
import itertools
import unittest
from pathlib import Path

from pixelkit.lint import _period
from pixelkit.makers import RecipeError
from pixelkit.recipes import load_recipes
from pixelkit.room_tiles import (FLOOR, OUT, WALL, classify, draw_tile, lint_room_tile, wall_styles)
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE = Style.load(str(ROOT / "art" / "kit" / "style.json"))
FLOOR_SPEC = load_recipes(str(ROOT / "art" / "kit" / "recipes" / "bakery"))["bakery.tile.floor"]


def grid(*rows: str):
    """글자 평면 → 격자. '#' 벽, '.' 바닥, ' ' 밖."""
    m = {"#": WALL, ".": FLOOR, " ": OUT}
    return [[m[c] for c in r] for r in rows]


def level(key):
    """face 열쇠의 k(바닥에서 위로 몇 번째). face가 아니면 None."""
    if key is None or not key.startswith("face:"):
        return None
    return int(key.split(":")[1])


# 방 둘 + 세로 벽(문간 있음) + T자 + 가로 안벽(문간 있음) + 남쪽 벽 문간. 시험에 쓰는 대표 평면.
SAMPLE = grid(
    "###############",
    "###############",
    "###############",
    "#......#......#",
    "#......#......#",
    "#......#......#",
    "#.............#",
    "#......#......#",
    "#......########",
    "#......########",
    "#......##.#####",
    "#.............#",
    "#......#......#",
    "######.########",
)


class ClassifyTests(unittest.TestCase):
    def test_wall_within_face_height_above_floor_is_face(self):
        keys = classify(grid("###", "###", "###", "...", "..."))
        for x in range(3):
            self.assertEqual(level(keys[2][x]), 1)
            self.assertEqual(level(keys[1][x]), 2)
            self.assertTrue(keys[0][x].startswith("cap:"), keys[0][x])

    def test_face_height_is_a_parameter(self):
        keys = classify(grid("#", "#", "#", "#", "."), face_height=3)
        self.assertEqual([level(keys[y][0]) for y in range(4)], [None, 3, 2, 1])
        self.assertTrue(keys[0][0].startswith("cap:"))

    def test_vertical_wall_is_cap_and_its_south_end_is_face(self):
        g = grid(".#.", ".#.", ".#.", ".#.", ".#.", "...")
        keys = classify(g)
        for y in range(3):
            self.assertTrue(keys[y][1].startswith("cap:"), (y, keys[y][1]))
        self.assertEqual(level(keys[3][1]), 2)
        self.assertEqual(level(keys[4][1]), 1)

    def test_cap_mask_differs_at_corner_tee_end_and_run(self):
        # 바닥이 없으면 모든 벽이 윗면이다.
        corner = classify(grid("##", "# "))[0][0]
        tee = classify(grid("###", " # "))[0][1]
        end = classify(grid("##"))[0][0]
        run = classify(grid("###"))[0][1]
        self.assertEqual(len({corner, tee, end, run}), 4, (corner, tee, end, run))
        self.assertEqual(classify(grid("###", " # "))[0][1], classify(grid("  ###", "   # "))[0][3])

    def test_cap_keys_are_at_most_47(self):
        keys = set()
        for bits in itertools.product((OUT, WALL), repeat=8):
            g = [list(bits[0:3]), [bits[3], WALL, bits[4]], list(bits[5:8])]
            k = classify(g)[1][1]
            self.assertTrue(k.startswith("cap:"), k)
            keys.add(k)
        self.assertLessEqual(len(keys), 47)
        self.assertEqual(len(keys), 47)          # 정리한 뒤의 47가지가 모두 나온다

    def test_diagonal_bit_only_counts_between_two_cap_neighbours(self):
        a = classify(grid("##", "  "))[0][0]
        b = classify(grid("##", " #"))[0][0]     # 남동만 더 찼지만 남쪽이 비었다 → 대각 무시, 같은 열쇠
        self.assertEqual(a, b)
        c = classify(grid("# ", " #"))[0][0]
        d = classify(grid("# ", "  "))[0][0]
        self.assertEqual(c, d)                  # 이웃 둘이 다 비면 대각은 무시
        full = classify(grid("##", "##"))[0][0]
        notch = classify(grid("##", "# "))[0][0]
        self.assertNotEqual(full, notch)        # 동·남이 다 차면 남동이 안쪽 모서리를 가른다

    def test_floor_under_face_gets_shade(self):
        keys = classify(grid("###", "###", "###", "...", "..."))
        self.assertEqual(keys[3], ["floor:shade"] * 3)
        self.assertEqual(keys[4], ["floor"] * 3)

    def test_face_right_under_cap_marks_ceiling_shade(self):
        keys = classify(grid("###", "###", "###", "..."))
        self.assertEqual(level(keys[1][1]), 2)
        self.assertIn("c", keys[1][1].split(":")[2])          # 윗면 바로 아래 정면 → 천장 밑 그늘 표시
        self.assertEqual(level(keys[2][1]), 1)
        self.assertNotIn("c", "".join(keys[2][1].split(":")[2:]))   # 아래 정면 타일은 윗면 밑이 아니다

    def test_grid_values_other_than_out_wall_floor_are_errors(self):
        for bad in (3, -1, 9):
            with self.subTest(bad=bad), self.assertRaises(RecipeError):
                classify([[WALL, bad], [FLOOR, FLOOR]])

    def test_out_cells_have_no_tile(self):
        keys = classify(grid(" # ", " . ", "   "))
        self.assertIsNone(keys[0][0])
        self.assertIsNone(keys[2][1])
        self.assertIsNone(keys[1][0])

    def test_thin_horizontal_wall_is_all_face_no_crash(self):
        keys = classify(grid("...", "###", "..."))
        self.assertEqual([level(k) for k in keys[1]], [1, 1, 1])
        for k in keys[1]:
            draw_tile(STYLE, k, "paper", FLOOR_SPEC)

    def test_wall_at_map_edge_treats_outside_as_out(self):
        keys = classify(grid("#.", "#."))
        # 맵 밖(아래)은 바닥이 아니므로 정면이 아니다. 왼쪽 밖과 아래 밖은 윗면이 아니다.
        self.assertTrue(keys[1][0].startswith("cap:"))
        self.assertEqual(keys[1][0], classify(grid(" #. ", " #. ", "    "))[1][1])
        # 맨 윗줄 벽 바로 밑이 바닥이면 정면이고, 그 위는 맵 밖이라 위가 열린 정면이다.
        top = classify(grid("##", ".."))
        self.assertEqual([level(k) for k in top[0]], [1, 1])

    def test_doorway_in_horizontal_wall_ends_faces_on_both_sides(self):
        keys = classify(grid("##.##", "##.##", "##.##", "....."))
        left, right = keys[1][1], keys[1][3]
        self.assertEqual(level(left), 2)
        self.assertNotEqual(left, keys[1][0])           # 끝 표시가 붙어 가운데와 다르다
        self.assertNotEqual(left, right)                # 왼쪽 끝과 오른쪽 끝도 다르다
        self.assertEqual(keys[2][2], "floor")           # 문간은 바닥(위가 정면이 아니라 그늘 없음)
        self.assertEqual(keys[3][2], "floor")


class DrawTests(unittest.TestCase):
    def all_keys(self):
        keys = {k for row in classify(SAMPLE) for k in row if k is not None}
        for bits in itertools.product((OUT, WALL), repeat=8):
            g = [list(bits[0:3]), [bits[3], WALL, bits[4]], list(bits[5:8])]
            keys.add(classify(g)[1][1])
        keys |= {k for row in classify(grid("...", "###", "...")) for k in row}
        keys |= {k for row in classify(grid("##", "..")) for k in row}
        return sorted(keys)

    def test_sample_has_every_kind(self):
        keys = {k for row in classify(SAMPLE) for k in row if k is not None}
        self.assertIn("floor:shade", keys)
        self.assertIn("floor", keys)
        self.assertTrue(any(level(k) == 1 for k in keys))
        self.assertTrue(any(level(k) == 2 for k in keys))
        self.assertGreater(sum(1 for k in keys if k.startswith("cap:")), 8)

    def test_every_key_draws_32x32_opaque_and_passes_lint(self):
        self.assertGreaterEqual(len(wall_styles()), 2)
        for wall in wall_styles():
            for key in self.all_keys():
                with self.subTest(wall=wall, key=key):
                    cv = draw_tile(STYLE, key, wall, FLOOR_SPEC)
                    self.assertEqual((cv.w, cv.h), (32, 32))
                    self.assertTrue(all(cv.get(x, y)[3] == 255 for y in range(32) for x in range(32)))
                    self.assertEqual(lint_room_tile(cv, key, STYLE), [])

    def test_deterministic(self):
        for key in ("cap:0", "face:1", "floor:shade"):
            a = draw_tile(STYLE, key, "paper", FLOOR_SPEC).to_image().tobytes()
            b = draw_tile(STYLE, key, "paper", FLOOR_SPEC).to_image().tobytes()
            self.assertEqual(a, b)

    def test_cap_outline_is_on_the_sides_without_cap_neighbour(self):
        ol = STYLE.outline
        middle = range(4, 28)
        sides = {   # 변 이름 → (변의 픽셀들, 그 변의 이웃이 비고 나머지가 찬 평면, 그 변의 이웃만 찬 평면)
            "west": ([(0, i) for i in middle], grid(" ##", " ##", " ##"), grid("## ", "## ", "## ")),
            "east": ([(31, i) for i in middle], grid("## ", "## ", "## "), grid(" ##", " ##", " ##")),
            "north": ([(i, 0) for i in middle], grid("   ", "###", "###"), grid("###", "###", "   ")),
            "south": ([(i, 31) for i in middle], grid("###", "###", "   "), grid("   ", "###", "###")),
        }
        for wall in wall_styles():
            for name, (pixels, open_side, closed_side) in sides.items():
                with self.subTest(wall=wall, side=name):
                    cv = draw_tile(STYLE, classify(open_side)[1][1], wall, FLOOR_SPEC)
                    self.assertTrue(all(cv.get(x, y) == ol for x, y in pixels))
                    cv = draw_tile(STYLE, classify(closed_side)[1][1], wall, FLOOR_SPEC)
                    self.assertFalse(any(cv.get(x, y) == ol for x, y in pixels))

    def test_face_ends_have_an_outline_column_and_runs_do_not(self):
        keys = classify(grid("##.##", "##.##", "##.##", "....."))
        ol = STYLE.outline
        for wall in wall_styles():
            run = draw_tile(STYLE, keys[1][0], wall, FLOOR_SPEC)
            left_of_gap = draw_tile(STYLE, keys[1][1], wall, FLOOR_SPEC)   # 오른쪽이 문간
            right_of_gap = draw_tile(STYLE, keys[1][3], wall, FLOOR_SPEC)  # 왼쪽이 문간
            with self.subTest(wall=wall):
                self.assertFalse(all(run.get(31, y) == ol for y in range(32)))
                self.assertTrue(all(left_of_gap.get(31, y) == ol for y in range(32)))
                self.assertFalse(all(left_of_gap.get(0, y) == ol for y in range(32)))
                self.assertTrue(all(right_of_gap.get(0, y) == ol for y in range(32)))

    def test_face_next_to_cap_has_no_outline_column(self):
        """정면 옆이 윗면이면 그 변의 외곽선은 윗면이 친다(두 겹이 되지 않게)."""
        keys = classify(grid("####", "####", "#..#", "#..#"))
        ol = STYLE.outline
        self.assertTrue(keys[1][0].startswith("cap:"))
        cv = draw_tile(STYLE, keys[1][1], "paper", FLOOR_SPEC)
        self.assertFalse(all(cv.get(0, y) == ol for y in range(32)))
        cap = draw_tile(STYLE, keys[1][0], "paper", FLOOR_SPEC)
        self.assertTrue(all(cap.get(31, y) == ol for y in range(4, 28)))

    def test_thin_wall_face_has_outline_on_top(self):
        keys = classify(grid("...", "###", "..."))
        cv = draw_tile(STYLE, keys[1][1], "paper", FLOOR_SPEC)
        self.assertTrue(all(cv.get(x, 0) == STYLE.outline for x in range(32)))

    def test_face_tiles_tile_horizontally(self):
        keys = classify(grid("###", "###", "###", "..."))
        for wall in wall_styles():
            for y in (1, 2):
                cv = draw_tile(STYLE, keys[y][1], wall, FLOOR_SPEC)
                with self.subTest(wall=wall, key=keys[y][1]):
                    self.assertEqual([p for p in lint_room_tile(cv, keys[y][1], STYLE) if p.code == "tile-seam"], [])
                    p = _period(cv, True)
                    self.assertIsNotNone(p)
                    self.assertEqual(32 % p, 0)

    def test_floor_shade_differs_only_at_the_top(self):
        plain = draw_tile(STYLE, "floor", "paper", FLOOR_SPEC)
        shade = draw_tile(STYLE, "floor:shade", "paper", FLOOR_SPEC)
        changed = {y for y in range(32) for x in range(32) if plain.get(x, y) != shade.get(x, y)}
        self.assertTrue(changed)
        self.assertLessEqual(max(changed), 1)

    def test_ceiling_shade_differs_only_at_the_top_rows(self):
        plain = draw_tile(STYLE, "face:2", "paper", FLOOR_SPEC)
        shade = draw_tile(STYLE, "face:2:c", "paper", FLOOR_SPEC)
        changed = {y for y in range(32) for x in range(32) if plain.get(x, y) != shade.get(x, y)}
        self.assertTrue(changed)
        self.assertLessEqual(max(changed), 3)        # 맨 위 몇 줄만 다르고 아래는 같다
        for wall in wall_styles():
            a = draw_tile(STYLE, "face:2", wall, FLOOR_SPEC)
            b = draw_tile(STYLE, "face:2:c", wall, FLOOR_SPEC)
            self.assertTrue(all(a.get(x, y) == b.get(x, y) for y in range(4, 32) for x in range(32)), wall)

    def test_lint_catches_transparent_and_off_palette(self):
        from pixelkit.canvas import Canvas
        cv = Canvas(32, 32)
        codes = {p.code for p in lint_room_tile(cv, "floor", STYLE)}
        self.assertIn("tile-transparent", codes)
        cv.rect(0, 0, 32, 32, (1, 2, 3, 255))
        codes = {p.code for p in lint_room_tile(cv, "floor", STYLE)}
        self.assertIn("off-palette", codes)

    def test_unknown_wall_style_is_recipe_error_listing_known(self):
        with self.assertRaises(RecipeError) as e:
            draw_tile(STYLE, "cap:0", "no-such-wall", FLOOR_SPEC)
        for name in wall_styles():
            self.assertIn(name, str(e.exception))

    def test_unknown_key_is_recipe_error(self):
        for key in ("roof", "face:x", "cap:999", "face:0", "face:²", "face:٢", "cap:²"):
            with self.subTest(key=key), self.assertRaises(RecipeError):
                draw_tile(STYLE, key, "paper", FLOOR_SPEC)


if __name__ == "__main__":
    unittest.main()
