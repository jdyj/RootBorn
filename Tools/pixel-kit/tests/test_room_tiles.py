"""방 만들기 — 둘레 칸으로 벽·바닥 타일을 고르고 그린다(room_tiles).

벽 윗면은 칸을 꽉 채우지 않고 두께 band(기본 16px)의 띠만 그린다(사용자 결정 2026-11-14 "A로 가보자").
띠·정면이 아닌 자리는 벽 타일에서 투명하고, 그 자리가 바닥 쪽이면 같은 칸의 Ground 층에 바닥 타일이 깔린다.

그림의 특정 픽셀 색에는 기대지 않고 구조(띠 범위, 투명 여부, 외곽선이 어느 변에 있는지, 열쇠가 같은지 다른지)를 본다."""
import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.lint import _period
from pixelkit.makers import RecipeError
from pixelkit.recipes import load_recipes
from pixelkit.room_tiles import (FLOOR, OUT, WALL, classify, draw_tile, lint_room_tile, plan_tiles, tile_layout,
                                 wall_band, wall_styles)
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
    return int(key.split(":")[1].split("@")[0])


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

# 견본 A를 볼 때 쓴 평면: 세로 벽 끝, 십자, T자, 문간, 두꺼운 덩어리(3x4), 위로 솟은 벽 끝.
SHAPES = grid(
    "####################",
    "####################",
    "####################",
    "#.....#......#.....#",
    "#.....#......#.....#",
    "#.....#......#.....#",
    "#............#.....#",
    "#.....#......#.....#",
    "###..##########..###",
    "###..##########..###",
    "###..##########..###",
    "#.....#...#...###..#",
    "#.....#...#...###..#",
    "#.........#...###..#",
    "#.....#...#...###..#",
    "#..#..#............#",
    "#..#..#............#",
    "####################",
)

# 바깥 벽만 있는 방. 왼쪽 벽 (0, 4), 오른쪽 벽 (6, 4), 아래 벽 (3, 6), 위 윗면 (3, 0) — 그 밑 두 칸은 정면.
BOX = grid(
    "#######",
    "#######",
    "#######",
    "#.....#",
    "#.....#",
    "#.....#",
    "#######",
)

# 방 둘 사이 세로 벽(4열). (4, 1)·(4, 2)는 양옆 가로 벽 정면 사이를 지나는 윗면, (4, 3..6)은 양옆이 바닥.
MID = grid(
    "#########",
    "#########",
    "#########",
    "#...#...#",
    "#...#...#",
    "#...#...#",
    "#...#...#",
    "#########",
)

# 세로 벽(4열)이 방 안에서 끝난다: (4, 3..4)는 윗면, (4, 5)·(4, 6)이 끝 정면(k=2, 1), (4, 7)이 그 밑 바닥.
END = grid(
    "#########",
    "#########",
    "#########",
    "#...#...#",
    "#...#...#",
    "#...#...#",
    "#...#...#",
    "#.......#",
    "#.......#",
    "#########",
)


def rect(x0, y0, x1, y1):
    return frozenset((x, y) for y in range(y0, y1) for x in range(x0, x1))


def wall_keys(*grids, band=16):
    keys = set()
    for g in grids:
        for row in plan_tiles(g, band=band).wall:
            keys |= {k for k in row if k is not None}
    return sorted(keys)


def opaque(cv):
    return frozenset((x, y) for y in range(cv.h) for x in range(cv.w) if cv.get(x, y)[3])


def drawn(layout):
    """띠·늘인 정면·좁은 정면이 차지하는 칸 안 픽셀."""
    pixels = set(layout.band)
    for fill in layout.fills.values():
        pixels |= fill
    if layout.narrow is not None:
        x0, x1 = layout.narrow
        pixels |= rect(x0, 0, x1, 32)
    return frozenset(pixels)


class ClassifyTests(unittest.TestCase):
    """classify는 칸의 종류만 고른다(바닥 / 정면 열쇠 / 윗면 "cap" / 밖). 그릴 타일 열쇠는 plan_tiles가 낸다."""

    def test_wall_within_face_height_above_floor_is_face(self):
        keys = classify(grid("###", "###", "###", "...", "..."))
        for x in range(3):
            self.assertEqual(level(keys[2][x]), 1)
            self.assertEqual(level(keys[1][x]), 2)
            self.assertEqual(keys[0][x], "cap")

    def test_face_height_is_a_parameter(self):
        keys = classify(grid("#", "#", "#", "#", "."), face_height=3)
        self.assertEqual([level(keys[y][0]) for y in range(4)], [None, 3, 2, 1])
        self.assertEqual(keys[0][0], "cap")

    def test_vertical_wall_is_cap_and_its_south_end_is_face(self):
        g = grid(".#.", ".#.", ".#.", ".#.", ".#.", "...")
        keys = classify(g)
        for y in range(3):
            self.assertEqual(keys[y][1], "cap", y)
        self.assertEqual(level(keys[3][1]), 2)
        self.assertEqual(level(keys[4][1]), 1)

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
        tiles = plan_tiles(grid(" # ", " . ", "   "))
        self.assertIsNone(tiles.wall[0][0])
        self.assertIsNone(tiles.ground[0][0])

    def test_thin_horizontal_wall_is_all_face_no_crash(self):
        keys = classify(grid("...", "###", "..."))
        self.assertEqual([level(k) for k in keys[1]], [1, 1, 1])
        for k in plan_tiles(grid("...", "###", "...")).wall[1]:
            draw_tile(STYLE, k, "paper", FLOOR_SPEC)

    def test_wall_at_map_edge_treats_outside_as_out(self):
        keys = classify(grid("#.", "#."))
        # 맵 밖(아래)은 바닥이 아니므로 정면이 아니다.
        self.assertEqual(keys[1][0], "cap")
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


class BandTests(unittest.TestCase):
    """윗면 칸의 띠 자리(견본 A의 규칙)."""

    def test_outer_walls_put_the_band_on_the_room_side(self):
        tiles = plan_tiles(BOX)
        self.assertEqual(tile_layout(tiles.wall[4][0]).band, rect(16, 0, 32, 32))     # 왼쪽 벽: 오른쪽(방 쪽) 반
        self.assertEqual(tile_layout(tiles.wall[4][6]).band, rect(0, 0, 16, 32))      # 오른쪽 벽: 왼쪽 반
        self.assertEqual(tile_layout(tiles.wall[6][3]).band, rect(0, 0, 32, 16))      # 아래 벽: 위(방 쪽) 반

    def test_horizontal_cap_above_a_face_hugs_the_face(self):
        tiles = plan_tiles(BOX)
        self.assertEqual(tile_layout(tiles.wall[0][3]).band, rect(0, 16, 32, 32))

    def test_vertical_wall_with_rooms_on_both_sides_is_centred(self):
        tiles = plan_tiles(MID)
        for y in (3, 4, 5):
            self.assertEqual(tile_layout(tiles.wall[y][4]).band, rect(8, 0, 24, 32), y)

    def test_band_thickness_is_the_band_value(self):
        self.assertEqual(wall_band("paper"), 16)
        self.assertEqual(wall_band("plaster"), 16)
        thin = plan_tiles(MID, band=12)
        self.assertEqual(tile_layout(thin.wall[4][4]).band, rect(10, 0, 22, 32))
        self.assertEqual(tile_layout(plan_tiles(BOX, band=12).wall[4][0]).band, rect(20, 0, 32, 32))
        for bad in (0, 3, 32, 40, 16.0, "16", True):
            with self.subTest(band=bad), self.assertRaises(RecipeError):
                plan_tiles(MID, band=bad)

    def test_neighbouring_bands_meet_exactly_on_the_shared_edge(self):
        """T자·십자·끝·모서리·덩어리에서 두 윗면 칸이 나누는 변: 띠가 그 변을 건너고, 한쪽의 띠 범위가 다른 쪽을 품는다
        (T자에서 가로 띠가 세로 띠보다 넓은 것은 되지만, 두 범위가 엇갈려 띠가 칸 경계에서 옆으로 어긋나면 안 된다)."""
        for name, g in (("SAMPLE", SAMPLE), ("SHAPES", SHAPES), ("BOX", BOX), ("MID", MID), ("END", END)):
            kinds = classify(g)
            tiles = plan_tiles(g)
            for y, row in enumerate(kinds):
                for x, k in enumerate(row):
                    if k != "cap":
                        continue
                    a = tile_layout(tiles.wall[y][x]).band
                    if x + 1 < len(row) and row[x + 1] == "cap":
                        b = tile_layout(tiles.wall[y][x + 1]).band
                        self.assert_meet({py for px, py in a if px == 31}, {py for px, py in b if px == 0},
                                         f"{name} ({x},{y})|({x + 1},{y})")
                    if y + 1 < len(kinds) and kinds[y + 1][x] == "cap":
                        b = tile_layout(tiles.wall[y + 1][x]).band
                        self.assert_meet({px for px, py in a if py == 31}, {px for px, py in b if py == 0},
                                         f"{name} ({x},{y})/({x},{y + 1})")

    def assert_meet(self, a, b, where):
        self.assertTrue(a and b, where + ": 띠가 변을 건너지 않는다")
        self.assertTrue(a <= b or b <= a, f"{where}: 띠 범위가 엇갈린다 {sorted(a)} / {sorted(b)}")

    def test_a_tee_does_not_poke_out_of_the_centred_wall(self):
        """한쪽만 방인 칸이 양쪽이 방인 세로 벽에 이어지면 그 벽의 가로 자리를 따른다."""
        g = grid(
            "#########",
            "#########",
            "#########",
            "#...#...#",
            "#...#...#",
            "#...#####",       # (4, 5): 왼쪽만 바닥, 오른쪽은 두꺼운 벽 — 위의 (4, 4)는 양쪽이 바닥(가운데 띠)
            "#...#####",
            "#########",
        )
        tiles = plan_tiles(g)
        self.assertEqual(tile_layout(tiles.wall[4][4]).band, rect(8, 0, 24, 32))
        joint = tile_layout(tiles.wall[5][4]).band
        self.assertEqual(min(px for px, py in joint if py == 0), 8)          # 위 칸의 가운데 띠에서 그대로 내려온다
        self.assertFalse({px for px, py in joint if px < 8 and py < 8})     # 왼쪽(바닥 쪽) 위로 삐져나오지 않는다

    def test_leftover_is_transparent_and_band_and_face_pixels_are_opaque(self):
        for wall in wall_styles():
            for key in wall_keys(SAMPLE, SHAPES, BOX, MID, END):
                cv = draw_tile(STYLE, key, wall, FLOOR_SPEC)
                with self.subTest(wall=wall, key=key):
                    layout = tile_layout(key)
                    if layout is None:                         # 꽉 찬 정면
                        self.assertEqual(len(opaque(cv)), 32 * 32)
                    else:
                        self.assertEqual(opaque(cv), drawn(layout))
                        self.assertTrue(opaque(cv), "완전히 투명한 벽 타일")

    def test_end_of_a_vertical_wall_has_a_face_as_wide_as_the_band(self):
        tiles = plan_tiles(END)
        ol = STYLE.outline
        for y in (5, 6):
            key = tiles.wall[y][4]
            layout = tile_layout(key)
            self.assertEqual(layout.narrow, (8, 24), key)
            self.assertEqual(layout.band, frozenset(), key)
            cv = draw_tile(STYLE, key, "paper", FLOOR_SPEC)
            self.assertEqual(opaque(cv), rect(8, 0, 24, 32), key)
            self.assertTrue(all(cv.get(8, py) == ol and cv.get(23, py) == ol for py in range(32)), key)   # 양옆 외곽선
            self.assertEqual(tiles.ground[y][4], "floor", key)          # 좁은 정면 양옆으로 바닥이 비친다
        self.assertEqual(tiles.ground[7][4], "floor:shade:8-24")         # 그 밑 그림자도 좁은 폭으로만
        plain = draw_tile(STYLE, "floor", "paper", FLOOR_SPEC)
        shade = draw_tile(STYLE, "floor:shade:8-24", "paper", FLOOR_SPEC)
        changed = {(x, y) for y in range(32) for x in range(32) if plain.get(x, y) != shade.get(x, y)}
        self.assertTrue(changed)
        self.assertLessEqual({y for _, y in changed}, {0, 1})
        self.assertLessEqual({x for x, _ in changed}, set(range(8, 24)))

    def test_face_is_stretched_sideways_where_a_vertical_wall_crosses_it(self):
        tiles = plan_tiles(MID)
        kinds = classify(MID)
        for y in (1, 2):
            key = tiles.wall[y][4]
            layout = tile_layout(key)
            self.assertEqual(layout.band, rect(8, 0, 24, 32), key)
            self.assertEqual(set(layout.fills), {kinds[y][3]}, key)              # 옆 정면(같은 높이)을 늘인다
            self.assertEqual(layout.fills[kinds[y][3]], rect(0, 0, 8, 32) | rect(24, 0, 32, 32), key)
            self.assertEqual(len(opaque(draw_tile(STYLE, key, "paper", FLOOR_SPEC))), 32 * 32, key)
            self.assertIsNone(tiles.ground[y][4], key)

    def test_ground_under_wall_cells_only_where_floor_shows(self):
        mid, box = plan_tiles(MID), plan_tiles(BOX)
        self.assertEqual(mid.ground[4][4], "floor")             # 세로 벽 양옆 → 바닥
        self.assertIsNone(box.ground[4][0])                     # 바깥 벽 바깥쪽 → 비어 있다(맵 밖)
        self.assertIsNone(box.ground[6][3])
        self.assertIsNone(box.ground[0][3])
        self.assertIsNone(box.ground[1][3])                     # 꽉 찬 정면 밑에는 깔지 않는다
        self.assertEqual(box.ground[3][3], "floor:shade")
        self.assertEqual(box.ground[4][3], "floor")
        for y, row in enumerate(BOX):
            for x, v in enumerate(row):
                if v == FLOOR:
                    self.assertIsNone(box.wall[y][x])
                    self.assertIsNotNone(box.ground[y][x])

    def test_same_shape_same_key(self):
        mid = plan_tiles(MID)
        self.assertEqual(mid.wall[4][4], mid.wall[5][4])
        box = plan_tiles(BOX)
        self.assertEqual(box.wall[4][0], box.wall[3][0])
        moved = plan_tiles([[OUT, OUT] + row for row in BOX])            # 왼쪽에 밖 두 칸을 붙여 옮겨도
        for y, row in enumerate(BOX):
            for x in range(len(row)):
                self.assertEqual(moved.wall[y][x + 2], box.wall[y][x], (x, y))
                self.assertEqual(moved.ground[y][x + 2], box.ground[y][x], (x, y))

    def test_different_keys_draw_different_tiles(self):
        seen = {}
        for key in wall_keys(SAMPLE, SHAPES, BOX, MID, END):
            data = draw_tile(STYLE, key, "paper", FLOOR_SPEC).to_image().tobytes()
            self.assertFalse(data in seen, f"{key} 와 {seen.get(data)} 의 그림이 같다")
            seen[data] = key

    def test_corner_tee_end_and_run_differ(self):
        def centre(*rows):
            return plan_tiles(grid(*rows)).wall

        corner = centre("##", "# ")[0][0]
        tee = centre("###", " # ")[0][1]
        end = centre("##")[0][0]
        run = centre("###")[0][1]
        self.assertEqual(len({corner, tee, end, run}), 4, (corner, tee, end, run))
        self.assertEqual(centre("###", " # ")[0][1], centre("  ###", "   # ")[0][3])

    def test_a_far_diagonal_that_does_not_touch_the_tile_does_not_change_the_key(self):
        a = plan_tiles(grid("##", "  ")).wall[0][0]
        b = plan_tiles(grid("##", " #")).wall[0][0]     # 남동이 찼지만 이 칸의 띠 근처에 닿지 않는다
        self.assertEqual(a, b)
        full = plan_tiles(grid("##", "##")).wall[0][0]
        notch = plan_tiles(grid("##", "# ")).wall[0][0]
        self.assertNotEqual(full, notch)

    def test_band_outline_is_on_the_band_edges_not_across_it(self):
        ol = STYLE.outline
        for wall in wall_styles():
            cv = draw_tile(STYLE, plan_tiles(MID).wall[4][4], wall, FLOOR_SPEC)
            with self.subTest(wall=wall, cell="세로 벽"):
                self.assertTrue(all(cv.get(8, y) == ol and cv.get(23, y) == ol for y in range(32)))
                self.assertFalse(any(cv.get(x, y) == ol for y in range(32) for x in range(10, 22)))   # 위아래로 이어져 가로 선이 없다
            cv = draw_tile(STYLE, plan_tiles(BOX).wall[4][0], wall, FLOOR_SPEC)
            with self.subTest(wall=wall, cell="바깥 왼쪽 벽"):
                self.assertTrue(all(cv.get(16, y) == ol and cv.get(31, y) == ol for y in range(32)))
            cv = draw_tile(STYLE, plan_tiles(BOX).wall[0][3], wall, FLOOR_SPEC)
            with self.subTest(wall=wall, cell="정면 위 가로 윗면"):
                self.assertTrue(all(cv.get(x, 16) == ol and cv.get(x, 31) == ol for x in range(32)))


# 가로 벽(3줄, 1..3열)의 정면이 세로 벽(4열)과 만난다. 세로 벽은 정면 줄보다 한 칸 남쪽까지 내려와 그 끝에 좁은 정면
# (4, 7)·(4, 8)이 생기고, 가로 벽 아래 줄 정면 (3, 7)은 오른쪽 이웃 (4, 7)의 왼쪽 자리로 늘여 그려진다. 세로 벽이 정면 줄
# 위로도 이어지는 T자(새 집 평면 (12, 12)~(13, 13) 둘레와 같은 배치).
TJOIN = grid(
    "#########",
    "#########",
    "#########",
    "#...#...#",
    "#...#...#",
    "#####...#",
    "#####...#",
    "#####...#",
    "#...#...#",
    "#.......#",
    "#.......#",
    "#########",
)
# 세로 벽이 정면 줄 위로 이어지지 않는 ㄱ자, 가로 벽이 세로 벽 양쪽에 있는 십자.
LJOIN = grid(
    "#########",
    "#########",
    "#########",
    "#.......#",
    "#.......#",
    "#####...#",
    "#####...#",
    "#####...#",
    "#...#...#",
    "#.......#",
    "#.......#",
    "#########",
)
CROSS = grid(
    "#########",
    "#########",
    "#########",
    "#...#...#",
    "#...#...#",
    "#########",
    "#########",
    "#########",
    "#...#...#",
    "#.......#",
    "#.......#",
    "#########",
)


def mirror(g):
    return [list(reversed(row)) for row in g]


def compose(g, wall="paper", face_height=2):
    """방 전체 그림(Ground 먼저, Wall 위 — 게임 순서). 칸 32px."""
    tiles = plan_tiles(g, face_height, wall_band(wall))
    h, w = len(g), len(g[0])
    out = Canvas(w * 32, h * 32)
    for layer in (tiles.ground, tiles.wall):
        for y, row in enumerate(layer):
            for x, key in enumerate(row):
                if key is None:
                    continue
                cv = draw_tile(STYLE, key, wall, FLOOR_SPEC)
                for py in range(32):
                    for px in range(32):
                        c = cv.get(px, py)
                        if c[3]:
                            out.px(x * 32 + px, y * 32 + py, c)
    return tiles, out


class FaceMeetsVerticalWallTests(unittest.TestCase):
    """가로 벽 정면이 세로 벽(좁은 정면)과 만나는 끝: 정면은 세로 벽 칸 안으로 늘여 그려지므로 끝 외곽선과 밑 그림자도
    늘인 자리의 끝(좁은 정면의 옆변)까지 가야 한다. 정면 칸 자기 변(칸 경계)에서 일찍 끝나면 안 된다."""

    def outline_columns(self, cv, x0, x1, y0, y1):
        ol = STYLE.outline
        return {x for x in range(x0, x1) if all(cv.get(x, y) == ol for y in range(y0, y1))}

    def check(self, g, face_cell, narrow_cell):
        (fx, fy), (nx, ny) = face_cell, narrow_cell
        for wall in wall_styles():
            tiles, cv = compose(g, wall)
            layout = tile_layout(tiles.wall[ny][nx])
            with self.subTest(wall=wall):
                self.assertIsNotNone(layout.narrow, tiles.wall[ny][nx])
                a, b = layout.narrow
                lo, hi = min(fx, nx) * 32, (max(fx, nx) + 1) * 32
                # 정면 줄(그 칸 높이 전체)에서 위아래로 꽉 찬 외곽선 열은 좁은 정면의 두 옆변뿐이다
                self.assertEqual(self.outline_columns(cv, lo, hi, fy * 32, fy * 32 + 32),
                                 {nx * 32 + a, nx * 32 + b - 1}, tiles.wall[fy][fx])

    def test_face_end_outline_is_at_the_narrow_face_not_at_the_cell_edge(self):
        self.check(TJOIN, (3, 7), (4, 7))

    def test_mirrored(self):
        self.check(mirror(TJOIN), (5, 7), (4, 7))

    def test_l_join_and_cross(self):
        self.check(LJOIN, (3, 7), (4, 7))
        self.check(mirror(LJOIN), (5, 7), (4, 7))
        self.check(CROSS, (3, 7), (4, 7))
        self.check(CROSS, (5, 7), (4, 7))

    def test_face_neighbour_of_a_narrow_face_has_no_end_mark_toward_it(self):
        self.assertNotIn("r", plan_tiles(TJOIN).wall[7][3].split(":")[-1])
        self.assertNotIn("l", plan_tiles(mirror(TJOIN)).wall[7][5].split(":")[-1])

    def test_floor_shade_runs_under_every_face_pixel(self):
        """바닥이 보이는 맨 위 두 줄은 바로 위 픽셀이 정면이면(그 칸의 맨 아랫줄이 정면 그림이면) 그늘, 아니면 그늘이 없다."""
        plain = draw_tile(STYLE, "floor", "paper", FLOOR_SPEC)
        full = draw_tile(STYLE, "floor:shade", "paper", FLOOR_SPEC)
        # 바닥 그림에서 이미 가장 어두운 색인 열(나무 이음 등)은 그늘이 있어도 같아 보인다 — 그 열은 보지 않는다
        visible = {px for px in range(32) if any(plain.get(px, py) != full.get(px, py) for py in (0, 1))}
        self.assertGreater(len(visible), 16)
        for name, g in (("T", TJOIN), ("T mirror", mirror(TJOIN)), ("L", LJOIN), ("cross", CROSS),
                        ("sample", SAMPLE), ("shapes", SHAPES), ("end", END)):
            tiles = plan_tiles(g)
            for y, row in enumerate(tiles.ground):
                for x, key in enumerate(row):
                    if key is None or y == 0:
                        continue
                    above = tiles.wall[y - 1][x]
                    face_cols = set()
                    if above is not None and above.startswith("face:"):
                        lay = tile_layout(above)
                        face_cols = set(range(32)) if lay is None else {px for px, py in drawn(lay) if py == 31}
                    wall_here = tiles.wall[y][x]
                    lay_here = tile_layout(wall_here) if wall_here else None
                    covered = drawn(lay_here) if lay_here else (rect(0, 0, 32, 32) if wall_here else frozenset())
                    floor_cv = draw_tile(STYLE, key, "paper", FLOOR_SPEC)
                    for px in range(32):
                        if (px, 0) in covered or px not in visible:
                            continue
                        shaded = any(floor_cv.get(px, py) != plain.get(px, py) for py in (0, 1))
                        with self.subTest(grid=name, cell=(x, y), col=px, key=key, above=above):
                            self.assertEqual(shaded, px in face_cols)


class KeyTests(unittest.TestCase):
    def test_every_planned_key_reads_back(self):
        for key in wall_keys(SAMPLE, SHAPES, BOX, MID, END) + wall_keys(MID, END, band=12):
            with self.subTest(key=key):
                layout = tile_layout(key)
                if key.startswith("cap:") or "@" in key:
                    self.assertIsNotNone(layout)
                draw_tile(STYLE, key, "paper", FLOOR_SPEC)

    def test_bad_keys_are_refused(self):
        good_cap = plan_tiles(MID).wall[1][4]
        good_narrow = plan_tiles(END).wall[6][4]
        self.assertIn(";", good_cap)
        self.assertIn("@", good_narrow)
        bad = [
            "roof", "face:x", "cap:999", "face:0", "face:²", "face:٢", "cap:²", "cap", "cap:",
            "cap:24,0,8,32",                         # 거꾸로 된 범위
            "cap:0,0,40,32",                          # 칸 밖 3px 넘음
            "cap:0,0,16,32+8,0,24,32",                # 겹치는 사각형(정규형이 아니다)
            "cap:-2,-2,0,0",                          # 칸 안에 띠가 없다
            "cap:8,-2,24,34;face:2:c@0,0,9,32",       # 늘인 정면이 띠와 겹친다
            "cap:8,-2,24,34;face:2:lc@0,0,8,32",      # 늘인 정면에 끝 표시
            "cap:8,-2,24,34;face:2:c@0,0,8,32;",      # 끝에 빈 조각
            "cap:8,-2,24,34;face:2:c@24,0,32,32;face:1@0,0,8,32",   # 늘인 정면 순서가 정규형이 아니다
            "face:1:l@8-24", "face:1:lrt@8-24", "face:1:lr@8-8", "face:1:lr@-1-24", "face:1:lr@8-33",
            "floor:shade:0-32", "floor:shade:5-3", "floor:shade:5", "floor:x",
        ]
        for key in bad:
            with self.subTest(key=key), self.assertRaises(RecipeError):
                draw_tile(STYLE, key, "paper", FLOOR_SPEC)
        # 정규형이 아닌 여백(띠에서 2px 넘게 떨어진 이웃 픽셀을 적음)도 틀린 열쇠다 — 같은 그림이 열쇠 둘이 되지 않게
        self.assertTrue(good_cap.startswith("cap:"))
        with self.assertRaises(RecipeError):
            draw_tile(STYLE, good_cap.replace("cap:", "cap:-2,-2,-1,-1+", 1), "paper", FLOOR_SPEC)


class DrawTests(unittest.TestCase):
    def test_every_key_draws_32x32_and_passes_lint(self):
        self.assertGreaterEqual(len(wall_styles()), 2)
        keys = wall_keys(SAMPLE, SHAPES, BOX, MID, END)
        keys += sorted({k for g in (SAMPLE, SHAPES, END) for row in plan_tiles(g).ground for k in row if k})
        keys += [k for row in classify(grid("...", "###", "...")) for k in row]
        keys += [k for row in classify(grid("##", "..")) for k in row]
        for wall in wall_styles():
            for key in keys:
                with self.subTest(wall=wall, key=key):
                    cv = draw_tile(STYLE, key, wall, FLOOR_SPEC)
                    self.assertEqual((cv.w, cv.h), (32, 32))
                    if key.startswith("floor") or (key.startswith("face:") and "@" not in key):
                        self.assertEqual(len(opaque(cv)), 32 * 32)
                    self.assertEqual(lint_room_tile(cv, key, STYLE), [])

    def test_deterministic(self):
        for key in (plan_tiles(MID).wall[4][4], "face:1", "floor:shade", plan_tiles(END).wall[6][4]):
            a = draw_tile(STYLE, key, "paper", FLOOR_SPEC).to_image().tobytes()
            b = draw_tile(STYLE, key, "paper", FLOOR_SPEC).to_image().tobytes()
            self.assertEqual(a, b)

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
        """정면 옆이 윗면이면 그 변의 외곽선은 윗면 쪽(띠 가장자리)이 친다(두 겹이 되지 않게)."""
        g = grid("####", "####", "#..#", "#..#")
        keys = classify(g)
        ol = STYLE.outline
        self.assertEqual(keys[1][0], "cap")
        cv = draw_tile(STYLE, keys[1][1], "paper", FLOOR_SPEC)
        self.assertFalse(all(cv.get(0, y) == ol for y in range(32)))

    def test_thin_wall_face_has_outline_on_top(self):
        keys = classify(grid("...", "###", "..."))
        cv = draw_tile(STYLE, keys[1][1], "paper", FLOOR_SPEC)
        self.assertTrue(all(cv.get(x, 0) == STYLE.outline for x in range(32)))

    def test_face_tiles_tile_horizontally(self):
        keys = classify(grid("###", "###", "###", "..."))
        for wall in wall_styles():
            periods = {}
            for y in (1, 2):
                cv = draw_tile(STYLE, keys[y][1], wall, FLOOR_SPEC)
                with self.subTest(wall=wall, key=keys[y][1]):
                    self.assertEqual([p for p in lint_room_tile(cv, keys[y][1], STYLE) if p.code == "tile-seam"], [])
                periods[y] = _period(cv, True)
            with self.subTest(wall=wall):
                # 주기가 타일 폭 전체(32)인 모양(벽돌마다 다른 밝기, 나뭇결, 꽃)은 두 줄 모두 자기와 겹치는 밀기가 없다(None) —
                # 그림에서 계산한다. 그 밖의 모양은 주기가 보여야 하고(한 줄이라도) 보이는 주기는 32의 약수여야 한다.
                if all(p is None for p in periods.values()):
                    continue
                self.assertTrue(any(p is not None for p in periods.values()))
                for p in periods.values():
                    if p is not None:
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
        cv = Canvas(32, 32)
        codes = {p.code for p in lint_room_tile(cv, "floor", STYLE)}
        self.assertIn("tile-transparent", codes)
        cv.rect(0, 0, 32, 32, (1, 2, 3, 255))
        codes = {p.code for p in lint_room_tile(cv, "floor", STYLE)}
        self.assertIn("off-palette", codes)

    def test_lint_wall_tile_transparency_follows_the_key(self):
        key = plan_tiles(MID).wall[4][4]
        good = draw_tile(STYLE, key, "paper", FLOOR_SPEC)
        self.assertEqual(lint_room_tile(good, key, STYLE), [])
        holed = draw_tile(STYLE, key, "paper", FLOOR_SPEC)
        holed.px(15, 15, (0, 0, 0, 0))                         # 띠에 구멍
        self.assertIn("tile-transparent", {p.code for p in lint_room_tile(holed, key, STYLE)})
        baked = draw_tile(STYLE, key, "paper", FLOOR_SPEC)
        floor = draw_tile(STYLE, "floor", "paper", FLOOR_SPEC)
        for y in range(32):
            for x in range(8):
                baked.px(x, y, floor.get(x, y))                # 남는 자리에 바닥을 구웠다
        self.assertIn("room-tile-leftover", {p.code for p in lint_room_tile(baked, key, STYLE)})
        self.assertIn("room-tile-empty", {p.code for p in lint_room_tile(Canvas(32, 32), key, STYLE)})

    def test_lint_band_seam_catches_an_outline_across_a_continuing_band(self):
        key = plan_tiles(MID).wall[4][4]                      # 띠가 위아래 변으로 이어진다
        cv = draw_tile(STYLE, key, "paper", FLOOR_SPEC)
        for x in range(8, 24):
            cv.px(x, 0, STYLE.outline)                         # 이어지는 변을 선으로 끊었다
        self.assertIn("tile-seam", {p.code for p in lint_room_tile(cv, key, STYLE)})

    def test_unknown_wall_style_is_recipe_error_listing_known(self):
        with self.assertRaises(RecipeError) as e:
            draw_tile(STYLE, "face:1", "no-such-wall", FLOOR_SPEC)
        for name in wall_styles():
            self.assertIn(name, str(e.exception))
        with self.assertRaises(RecipeError):
            wall_band("no-such-wall")


# ---------------------------------------------------------------- 벽 칸의 문(wallDoors)

# 네 방향 벽에 문이 하나씩 든 방: 북쪽 정면 (4,2), 서쪽 (0,4), 동쪽 (8,4), 남쪽 (4,6).
DOORS4 = grid(
    "#########",
    "#########",
    "#########",
    "#.......#",
    "#.......#",
    "#.......#",
    "#########",
)
DOOR_CELLS = {"north": (4, 2), "west": (0, 4), "east": (8, 4), "south": (4, 6)}


def band_colors(wall="paper", top=None):
    """띠를 칠하는 팔레트 줄의 색(외곽선 제외) — 그 줄 이름은 벽 모양에서 읽는다(색값을 적지 않는다)."""
    from pixelkit.room_tiles import wall_top
    name = top or wall_top(wall)
    return {STYLE.color(f"{name}.{i}") for i in range(3)}


class WallDoorTests(unittest.TestCase):
    """벽 칸의 문은 그 칸을 문으로 알아볼 수 있게 그린다. 칸은 여전히 벽(충돌)이고 바뀌는 것은 그 칸과 이웃의 그림뿐."""

    def setUp(self):
        self.plain = plan_tiles(DOORS4)
        self.doors = plan_tiles(DOORS4, wall_doors=set(DOOR_CELLS.values()))

    def tile(self, plan, cell, wall="paper"):
        x, y = cell
        return draw_tile(STYLE, plan.wall[y][x], wall, FLOOR_SPEC)

    def test_door_cell_tile_differs_from_the_same_cell_without_a_door(self):
        for name, (x, y) in DOOR_CELLS.items():
            with self.subTest(name):
                self.assertNotEqual(self.doors.wall[y][x], self.plain.wall[y][x])
                self.assertNotEqual(self.tile(self.doors, (x, y)).to_image().tobytes(),
                                    self.tile(self.plain, (x, y)).to_image().tobytes())

    def test_no_wall_doors_changes_nothing(self):
        for g in (SAMPLE, SHAPES, BOX, MID, END, DOORS4):
            with self.subTest(g=len(g)):
                self.assertEqual(plan_tiles(g, wall_doors=set()), plan_tiles(g))
                self.assertEqual(plan_tiles(g, wall_doors=None), plan_tiles(g))

    def test_only_the_door_cell_and_its_neighbours_change(self):
        near = {(x + dx, y + dy) for x, y in DOOR_CELLS.values() for dy in (-1, 0, 1) for dx in (-1, 0, 1)}
        for layer in ("wall", "ground"):
            a, b = getattr(self.plain, layer), getattr(self.doors, layer)
            changed = {(x, y) for y in range(len(a)) for x in range(len(a[y])) if a[y][x] != b[y][x]}
            with self.subTest(layer):
                self.assertLessEqual(changed, near)

    def test_south_door_breaks_the_band_across_the_whole_cell(self):
        for wall in wall_styles():
            cv = self.tile(self.doors, DOOR_CELLS["south"], wall)
            colors = band_colors(wall)
            with self.subTest(wall=wall):
                for x in range(32):
                    self.assertFalse(any(cv.get(x, y) in colors for y in range(32)), f"x={x}")
                # 띠가 있던 자리에 문지방이 깔린다(투명하지 않다). 그 밖은 띠 없는 같은 칸처럼 투명
                plain = self.tile(self.plain, DOOR_CELLS["south"], wall)
                self.assertEqual(opaque(cv), opaque(plain))

    def test_band_ends_beside_a_south_door_close_with_an_outline(self):
        ol = STYLE.outline
        x, y = DOOR_CELLS["south"]
        for wall in ("paper", "plaster"):
            for dx, col in ((-1, 31), (1, 0)):
                with self.subTest(wall=wall, side=dx):
                    door = self.tile(self.doors, (x + dx, y), wall)
                    plain = self.tile(self.plain, (x + dx, y), wall)
                    rows = [py for py in range(32) if door.get(col, py)[3]]
                    self.assertTrue(rows)
                    self.assertTrue(all(door.get(col, py) == ol for py in rows))        # 띠 끝이 외곽선으로 닫힌다
                    self.assertFalse(all(plain.get(col, py) == ol for py in rows))      # 문이 없으면 띠가 이어진다

    def test_side_doors_break_the_vertical_band_and_close_its_ends(self):
        ol = STYLE.outline
        for name in ("west", "east"):
            x, y = DOOR_CELLS[name]
            cv = self.tile(self.doors, (x, y))
            colors = band_colors()
            with self.subTest(name):
                self.assertFalse(any(cv.get(px, py) in colors for py in range(32) for px in range(32)))
                for dy, row in ((-1, 31), (1, 0)):
                    n = self.tile(self.doors, (x, y + dy))
                    cols = [px for px in range(32) if n.get(px, row)[3]]
                    self.assertTrue(cols)
                    self.assertTrue(all(n.get(px, row) == ol for px in cols))

    def test_north_door_is_a_full_face_with_a_door(self):
        x, y = DOOR_CELLS["north"]
        key = self.doors.wall[y][x]
        self.assertIsNone(tile_layout(key))                    # 꽉 찬 정면처럼 칸 전체가 불투명
        cv = self.tile(self.doors, (x, y))
        self.assertEqual(len(opaque(cv)), 32 * 32)

    def test_every_direction_draws_and_passes_lint_for_every_wall_style(self):
        keys = sorted({self.doors.wall[y + dy][x + dx] for x, y in DOOR_CELLS.values()
                       for dy in (-1, 0, 1) for dx in (-1, 0, 1)
                       if 0 <= y + dy < len(DOORS4) and 0 <= x + dx < 9 and self.doors.wall[y + dy][x + dx]})
        for wall in wall_styles():
            for key in keys:
                with self.subTest(wall=wall, key=key):
                    cv = draw_tile(STYLE, key, wall, FLOOR_SPEC)
                    self.assertEqual(lint_room_tile(cv, key, STYLE), [])

    def test_door_keys_read_back_and_same_shape_same_key(self):
        self.assertEqual(plan_tiles(DOORS4, wall_doors=set(DOOR_CELLS.values())), self.doors)
        for x, y in DOOR_CELLS.values():
            key = self.doors.wall[y][x]
            with self.subTest(key=key):
                tile_layout(key)
        # 남쪽 벽에 떨어져 있는 문 둘은 같은 모양이라 같은 열쇠
        wide = grid("###########", "###########", "###########", "#.........#", "#.........#", "###########")
        p = plan_tiles(wide, wall_doors={(3, 5), (7, 5)})
        self.assertEqual(p.wall[5][3], p.wall[5][7])

    def test_bad_door_keys_are_refused(self):
        sill = self.doors.wall[6][4]
        door = self.doors.wall[2][4]
        for key in (sill + "x", sill.replace(":n:", ":q:"), sill.replace(":0-", ":00-"), sill.replace("-16", "-33"),
                    sill.replace("0-16", "16-0"), sill.replace("0-16", "0-2"), door.replace("face:1", "face:2"),
                    door + "@0-8", "door:", "door:cap"):
            with self.subTest(key=key), self.assertRaises(RecipeError):
                draw_tile(STYLE, key, "paper", FLOOR_SPEC)

    def test_a_door_cell_without_floor_beside_it_is_drawn_as_a_plain_wall(self):
        p = plan_tiles(DOORS4, wall_doors={(4, 0)})            # 둘레가 벽뿐인 윗면(평면 검사가 room-door-no-floor로 막는다)
        self.assertEqual(p, self.plain)

if __name__ == "__main__":
    unittest.main()
