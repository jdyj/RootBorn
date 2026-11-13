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


if __name__ == "__main__":
    unittest.main()
