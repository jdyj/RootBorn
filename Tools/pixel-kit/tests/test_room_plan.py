"""방 만들기 — 평면 읽기와 검사(room_plan). 쓰지 않는 쪽이라 임시 폴더에 평면 파일만 만든다."""
import json
import tempfile
import unittest
from pathlib import Path

from pixelkit.room_plan import RoomError, check_grid, is_warning, load_room

ROOT = Path(__file__).resolve().parents[3]
REAL_PLAN = ROOT / "art" / "kit" / "rooms" / "test.two-rooms.json"

GOOD = [
    "########",
    "########",
    "########",
    "#......#",
    "#......#",
    "#......#",
    "########",
]


def codes(problems):
    return sorted(p.code for p in problems)


class LoadRoomTests(unittest.TestCase):
    def setUp(self):
        t = tempfile.TemporaryDirectory()
        self.addCleanup(t.cleanup)
        self.dir = Path(t.name)

    def write(self, doc, name="r.x"):
        path = self.dir / f"{name}.json"
        path.write_text(json.dumps(doc), encoding="utf-8")
        return path

    def doc(self, **extra):
        d = {"id": "r.x", "wall": "paper", "floor": "bakery.tile.floor", "spawn": [2, 3], "plan": GOOD}
        d.update(extra)
        return d

    def test_reads_fields_and_defaults(self):
        room = load_room(self.write(self.doc()))
        self.assertEqual((room.id, room.wall, room.floor, room.spawn), ("r.x", "paper", "bakery.tile.floor", (2, 3)))
        self.assertEqual(room.grid, GOOD)
        self.assertEqual((room.doors, room.face_height), ({}, 2))

    def test_door_letter_becomes_floor_plus_door(self):
        plan = list(GOOD)
        plan[4] = "#..D...#"
        room = load_room(self.write(self.doc(plan=plan, doors={"D": "town"})))
        self.assertEqual(room.doors, {(3, 4): "town"})
        self.assertEqual(room.grid[4], "#......."[:3] + "." + "...#")
        self.assertEqual(check_grid(room.grid, room.spawn, room.doors, room.face_height), [])

    def test_bad_files_are_room_errors(self):
        bad = [
            self.doc(id="other"),                     # 파일 이름과 다르다
            self.doc(id="r x"),
            self.doc(plan=[]),
            self.doc(plan="####"),
            self.doc(spawn=[1]),
            self.doc(spawn=[1.5, 2]),
            self.doc(face_height=0),
            self.doc(doors={"#": "town"}),
            self.doc(doors={"DD": "town"}),
            self.doc(doors={"D": ""}),
            self.doc(doors={"D": "town"}),            # 평면에 D가 없다
        ]
        for doc in bad:
            with self.subTest(doc=doc), self.assertRaises(RoomError):
                load_room(self.write(doc))
        with self.assertRaises(RoomError):
            load_room(self.dir / "nope.json")
        path = self.dir / "r.x.json"
        path.write_text("{ not json", encoding="utf-8")
        with self.assertRaises(RoomError):
            load_room(path)

    def test_real_test_plan_loads_and_has_no_problems_at_all(self):
        room = load_room(REAL_PLAN)
        self.assertEqual(room.id, "test.two-rooms")
        self.assertEqual(check_grid(room.grid, room.spawn, room.doors, room.face_height), [])


class CheckGridTests(unittest.TestCase):
    def test_good_plan_has_no_problems(self):
        self.assertEqual(check_grid(GOOD, (2, 4), {}, 2), [])

    def test_ragged_plan(self):
        got = check_grid(GOOD[:3] + ["#......"] + GOOD[4:], (2, 4), {}, 2)
        self.assertEqual(codes(got), ["room-ragged-plan"])      # 조용히 채우지 않는다

    def test_unknown_char(self):
        plan = list(GOOD)
        plan[4] = "#.x....#"
        got = check_grid(plan, (3, 4), {}, 2)
        self.assertEqual(codes(got), ["room-unknown-char"])
        self.assertEqual((got[0].x, got[0].y), (2, 4))

    def test_open_edge_floor_touching_map_edge_or_outside(self):
        edge = ["...", "###"]
        self.assertIn("room-open-edge", codes(check_grid(edge, (0, 0), {}, 2)))
        plan = list(GOOD)
        plan[4] = "#.... .#"                       # 바닥 옆이 밖 칸
        self.assertIn("room-open-edge", codes(check_grid(plan, (1, 4), {}, 2)))

    def test_unreachable_floor_is_reported_once_per_pocket(self):
        plan = [
            "###########",
            "###########",
            "###########",
            "#...#.#...#",
            "#...#.#...#",
            "#...#.#...#",
            "###########",
        ]
        got = [p for p in check_grid(plan, (1, 3), {}, 2) if p.code == "room-unreachable-floor"]
        self.assertEqual(len(got), 2)      # 가운데 한 줄 바닥과 오른쪽 방 — 덩어리마다 하나

    def test_spawn_in_wall_outside_or_on_nothing(self):
        for spawn in ((0, 0), (99, 99), (-1, 3)):
            with self.subTest(spawn=spawn):
                self.assertEqual(codes(check_grid(GOOD, spawn, {}, 2)), ["room-spawn-in-wall"])

    def test_door_in_wall(self):
        self.assertEqual(codes(check_grid(GOOD, (2, 4), {(0, 0): "town"}, 2)), ["room-door-in-wall"])
        self.assertEqual(codes(check_grid(GOOD, (2, 4), {(2, 4): "town"}, 2)), [])

    def test_thin_wall_is_a_warning_not_an_error(self):
        got = check_grid(["#...#", "#####", "#...#", "#####"], (1, 0), {}, 2)
        self.assertIn("room-wall-thin", codes(got))
        thin = [p for p in got if p.code == "room-wall-thin"]
        self.assertTrue(all(is_warning(p) for p in thin))
        self.assertFalse(any(is_warning(p) for p in check_grid(GOOD[:3] + ["#.x...#"], (3, 3), {}, 2)))

    def test_value_grids_work_too(self):
        grid = [[1] * 5, [1] * 5, [1] * 5, [1, 2, 2, 2, 1], [1, 2, 2, 2, 1], [1] * 5]
        self.assertEqual(check_grid(grid, (2, 4), {}, 2), [])
        grid[4][2] = 7
        self.assertEqual(codes(check_grid(grid, (1, 4), {}, 2)), ["room-unknown-char"])


BUILD_OK = ["00000000", "00000000", "00000000", "01111110", "01001110", "01111110", "00000000"]


class WallDoorAndBuildTests(unittest.TestCase):
    def setUp(self):
        t = tempfile.TemporaryDirectory()
        self.addCleanup(t.cleanup)
        self.dir = Path(t.name)

    def load(self, plan, **extra):
        doc = {"id": "r.x", "wall": "paper", "floor": "bakery.tile.floor", "spawn": [2, 3], "plan": plan}
        doc.update(extra)
        path = self.dir / "r.x.json"
        path.write_text(json.dumps(doc), encoding="utf-8")
        return load_room(path)

    def test_wall_door_cell_stays_wall_and_is_listed(self):
        plan = list(GOOD)
        plan[6] = "###W####"
        room = self.load(plan, wallDoors={"W": "town"})
        self.assertEqual(room.wall_doors, {(3, 6): "town"})
        self.assertEqual(room.grid[6], "########")
        self.assertEqual(room.doors, {})
        self.assertEqual(check_grid(room.grid, room.spawn, room.doors, room.face_height, room.wall_doors, room.build), [])

    def test_wall_door_on_bottom_edge_is_not_open_edge(self):
        got = check_grid(GOOD, (2, 4), {}, 2, {(3, 6): "town"})
        self.assertEqual(codes(got), [])

    def test_wall_door_without_reachable_floor_is_problem(self):
        got = check_grid(GOOD, (2, 4), {}, 2, {(3, 1): "town"})          # 둘레가 벽뿐인 칸
        self.assertEqual(codes(got), ["room-door-no-floor"])
        pocket = ["#########", "#########", "#########", "#...#.#.#", "#########"]
        got = check_grid(pocket, (1, 3), {}, 2, {(5, 4): "town"})        # 안쪽 바닥은 있지만 도착 지점에서 닿지 못한다
        self.assertIn("room-door-no-floor", codes(got))

    def test_bad_wall_door_letters_and_values_are_room_errors(self):
        plan = list(GOOD)
        plan[6] = "###W####"
        for extra in ({"wallDoors": {"#": "town"}}, {"wallDoors": {"WW": "town"}}, {"wallDoors": {"W": ""}},
                      {"wallDoors": {"X": "town"}}, {"wallDoors": {"W": "town"}, "doors": {"W": "town"}}):
            with self.subTest(extra=extra), self.assertRaises(RoomError):
                self.load(plan, **extra)

    def test_build_rows_are_read_and_must_match_plan(self):
        self.assertEqual(self.load(GOOD, build=BUILD_OK).build, BUILD_OK)
        self.assertEqual(check_grid(GOOD, (2, 4), {}, 2, None, BUILD_OK), [])
        for build in (BUILD_OK[:-1], BUILD_OK + ["00000000"], BUILD_OK[:3] + ["0111111"] + BUILD_OK[4:]):
            with self.subTest(rows=len(build)):
                self.assertEqual(codes(check_grid(GOOD, (2, 4), {}, 2, None, build)), ["room-build-ragged"])

    def test_build_unknown_char(self):
        build = list(BUILD_OK)
        build[4] = "01x11110"
        got = check_grid(GOOD, (2, 4), {}, 2, None, build)
        self.assertEqual(codes(got), ["room-build-char"])
        self.assertEqual((got[0].x, got[0].y), (2, 4))

    def test_build_marks_spawn_or_door_buildable_is_problem(self):
        def with_one(x, y, base=BUILD_OK):
            build = [list(r) for r in base]
            build[y][x] = "1"
            return ["".join(r) for r in build]
        floor_door = list(GOOD)
        floor_door[4] = "#..D...#"
        floor_door = [r.replace("D", ".") for r in floor_door]
        doors = {(3, 4): "town"}
        self.assertEqual(check_grid(floor_door, (2, 4), doors, 2, None, BUILD_OK), [])
        for cell, what in (((2, 4), "도착 지점"), ((3, 4), "문 칸")):
            with self.subTest(what=what):
                got = check_grid(floor_door, (2, 4), doors, 2, None, with_one(*cell))
                self.assertEqual(codes(got), ["room-build-fixed"])
                self.assertEqual((got[0].x, got[0].y), cell)
        wall_doors = {(3, 6): "town"}
        door_base = list(BUILD_OK)
        door_base[5] = "01101110"                                                                 # 벽 문 안쪽 바닥 (3,5)은 고정
        self.assertEqual(check_grid(GOOD, (2, 4), {}, 2, wall_doors, door_base), [])
        for cell, what in (((3, 5), "벽 문 안쪽 바닥"), ((3, 6), "벽 문 칸")):
            with self.subTest(what=what):
                got = check_grid(GOOD, (2, 4), {}, 2, wall_doors, with_one(*cell, base=door_base))
                self.assertEqual(codes(got), ["room-build-fixed"])
                self.assertEqual((got[0].x, got[0].y), cell)
        self.assertEqual(check_grid(GOOD, (2, 4), {}, 2, wall_doors, with_one(4, 4, base=door_base)), [])   # 다른 바닥 칸은 이미 1
        self.assertEqual(check_grid(GOOD, (2, 4), {}, 2, None, with_one(0, 0)), [])              # 벽 칸은 1이어도 된다

    def test_build_marks_outside_cell_buildable_is_problem(self):
        plan = ["########", "########", "########", "#......#", "#......#", "#......#", "########", "        "]
        build = ["00000000"] * 7 + ["01000000"]
        self.assertEqual(codes(check_grid(plan, (2, 4), {}, 2, None, build)), ["room-build-fixed"])
        self.assertEqual(check_grid(plan, (2, 4), {}, 2, None, ["00000000"] * 8), [])

    def test_old_plan_without_new_keys_reads_as_before(self):
        room = load_room(REAL_PLAN)
        self.assertEqual((room.wall_doors, room.build), ({}, None))
        self.assertEqual(check_grid(room.grid, room.spawn, room.doors, room.face_height), [])


if __name__ == "__main__":
    unittest.main()
