"""방 만들기 — 평면에서 LDtk 방 쓰기(room_ldtk). 임시 저장소에서 쓰고 다시 읽어 본다.

게임이 필수로 읽는 필드는 test_ldtk_test_level.check_loader_fields(LdtkFile.cs·WorldCatalog.cs를 옮겨 적은 검사)로 본다.
그림의 특정 픽셀 색에는 기대지 않는다."""
import copy
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from pixelkit.cli import load_kit
from pixelkit.makers import RecipeError
from pixelkit.room_ldtk import RoomBuildError, build_room, map_id
from pixelkit.room_plan import RoomError, cells, load_room
from pixelkit.room_tiles import FLOOR, OUT, WALL, classify
from tests.test_ldtk_test_level import MAPS_WITH_COMMENTS, check_loader_fields

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
REAL_KIT = ROOT / "art" / "kit"
REAL_PLAN = json.loads((REAL_KIT / "rooms" / "test.two-rooms.json").read_text("utf-8"))
KIT = load_kit(str(REAL_KIT))

SMALL = ["########", "########", "########", "#......#", "#......#", "#......#", "########"]
BASE = {"wall": "paper", "floor": "bakery.tile.floor"}


class RoomBase(unittest.TestCase):
    def setUp(self):
        t = tempfile.TemporaryDirectory()
        self.addCleanup(t.cleanup)
        self.root = Path(t.name)
        (self.root / "game/world").mkdir(parents=True)
        (self.root / "game/world/maps.json").write_text(MAPS_WITH_COMMENTS, encoding="utf-8")
        (self.root / "art/kit/rooms").mkdir(parents=True)

    def plan_file(self, doc):
        (self.root / "art/kit/rooms" / f"{doc['id']}.json").write_text(json.dumps(doc), encoding="utf-8")
        return doc

    def real_plan(self, **changes):
        doc = copy.deepcopy(REAL_PLAN)
        doc.update(changes)
        return self.plan_file(doc)

    def small_plan(self, rid, plan=SMALL, spawn=(2, 4), **extra):
        return self.plan_file({"id": rid, **BASE, "spawn": list(spawn), "plan": plan, **extra})

    def build(self, rid="test.two-rooms", **kw):
        return build_room(self.root, rid, kit=KIT, **kw)

    def out(self, rid="test.two-rooms"):
        return self.root / "art/world/rooms" / rid

    def read(self, rid="test.two-rooms"):
        out = self.out(rid)
        return (json.loads((out / f"{rid}.ldtk").read_text("utf-8")), json.loads((out / "maps.json").read_text("utf-8")),
                json.loads((out / "tiles.slots.json").read_text("utf-8")))

    @staticmethod
    def layers(ldtk):
        return {li["__identifier"]: li for li in ldtk["levels"][0]["layerInstances"]}

    def snapshot(self, rid="test.two-rooms"):
        return {p.name: p.read_bytes() for p in sorted(self.out(rid).iterdir())}

    def grid(self, rid="test.two-rooms"):
        return cells(load_room(self.root / "art/kit/rooms" / f"{rid}.json").grid)


class MapIdTests(unittest.TestCase):
    def test_pascal_case_without_dots_and_hyphens(self):
        self.assertEqual(map_id("test.two-rooms"), "TestTwoRooms")
        self.assertEqual(map_id("home"), "Home")
        self.assertEqual(map_id("a.b-c.d"), "ABCD")


class BuildTests(RoomBase):
    def test_writes_four_files_that_satisfy_every_required_loader_field(self):
        self.real_plan()
        written = self.build()
        out = "art/world/rooms/test.two-rooms/"
        self.assertEqual(sorted(written), sorted(out + n for n in ("test.two-rooms.ldtk", "tiles.png", "tiles.slots.json", "maps.json")))
        ldtk, maps, _ = self.read()
        self.assertEqual(check_loader_fields(ldtk, maps), [])

    def test_collision_csv_equals_the_plan_grid(self):
        self.real_plan()
        self.build()
        ldtk, _, _ = self.read()
        col = self.layers(ldtk)["Collision"]
        grid = self.grid()
        self.assertEqual(col["__type"], "IntGrid")
        self.assertEqual((col["__cWid"], col["__cHei"], col["__gridSize"]), (len(grid[0]), len(grid), 32))
        self.assertEqual(col["intGridCsv"], [v for row in grid for v in row])
        self.assertEqual(len(col["intGridCsv"]), col["__cWid"] * col["__cHei"])
        self.assertEqual((ldtk["levels"][0]["pxWid"], ldtk["levels"][0]["pxHei"]), (col["__cWid"] * 32, col["__cHei"] * 32))
        layer_def = next(d for d in ldtk["defs"]["layers"] if d["identifier"] == "Collision")
        self.assertEqual({d["value"]: d["identifier"] for d in layer_def["intGridValues"]}, {1: "wall", 2: "floor"})

    def test_layer_order_tags_and_door_entity_definition(self):
        self.real_plan()
        self.build()
        ldtk, _, _ = self.read()
        self.assertEqual([li["__identifier"] for li in ldtk["levels"][0]["layerInstances"]],
                         ["Collision", "Door", "Furniture", "Wall", "Ground"])
        defs = {d["identifier"]: d for d in ldtk["defs"]["layers"]}
        self.assertEqual(defs["Furniture"]["uiFilterTags"], ["ysort"])
        self.assertEqual({k: v["type"] for k, v in defs.items()},
                         {"Collision": "IntGrid", "Door": "Entities", "Furniture": "Entities", "Wall": "Tiles", "Ground": "Tiles"})
        door_def = next(e for e in ldtk["defs"]["entities"] if e["identifier"] == "Door")
        self.assertIn("Where", [f["identifier"] for f in door_def["fieldDefs"]])
        self.assertEqual(self.layers(ldtk)["Furniture"]["entityInstances"], [])
        self.assertEqual(len(ldtk["defs"]["tilesets"]), 1)
        self.assertEqual(ldtk["defs"]["tilesets"][0]["relPath"], "tiles.png")
        self.assertEqual(ldtk["defs"]["tilesets"][0]["tileGridSize"], 32)

    def test_tile_layers_have_one_tile_per_wall_and_floor_cell(self):
        self.real_plan()
        self.build()
        ldtk, _, slots = self.read()
        grid = self.grid()
        keys = classify(grid)
        layers = self.layers(ldtk)
        for name, want in (("Wall", WALL), ("Ground", FLOOR)):
            tiles = layers[name]["gridTiles"]
            cell_set = {(x, y) for y, row in enumerate(grid) for x, v in enumerate(row) if v == want}
            self.assertEqual(len(tiles), len(cell_set), name)
            self.assertEqual({(t["px"][0] // 32, t["px"][1] // 32) for t in tiles}, cell_set, name)
            for t in tiles:
                cx, cy = t["px"][0] // 32, t["px"][1] // 32
                slot = slots["slots"][keys[cy][cx]]
                self.assertEqual(t["src"], [slot["x"], slot["y"]])
                self.assertEqual(t["t"], (slot["y"] // 32) * 16 + slot["x"] // 32)
                self.assertEqual(t["d"], [cy * len(grid[0]) + cx])
                self.assertEqual((t["f"], t["a"]), (0, 1))
            self.assertEqual(layers[name]["__tilesetDefUid"], ldtk["defs"]["tilesets"][0]["uid"])

    def test_outside_cells_get_no_tile_and_collision_zero(self):
        plan = ["    " + row for row in SMALL]
        self.small_plan("t.out", plan, spawn=(6, 4))
        self.build("t.out")
        ldtk, _, _ = self.read("t.out")
        layers = self.layers(ldtk)
        self.assertEqual(layers["Collision"]["intGridCsv"][:4], [0, 0, 0, 0])
        grid = self.grid("t.out")
        self.assertGreater(sum(row.count(OUT) for row in grid), 0)
        for name in ("Wall", "Ground"):
            self.assertTrue(all(t["px"][0] >= 4 * 32 for t in layers[name]["gridTiles"]))
            self.assertEqual(len(layers[name]["gridTiles"]),
                             sum(row.count(WALL if name == "Wall" else FLOOR) for row in grid))

    def test_slots_are_in_the_png_and_nothing_is_transparent(self):
        self.real_plan()
        self.build()
        _, _, slots = self.read()
        self.assertEqual((slots["cell"], slots["width_cells"]), (32, 16))
        with Image.open(self.out() / "tiles.png") as im:
            self.assertEqual(im.width, 512)
            self.assertEqual(im.height % 32, 0)
            for key, s in slots["slots"].items():
                self.assertEqual((s["w"], s["h"]), (32, 32))
                tile = im.crop((s["x"], s["y"], s["x"] + 32, s["y"] + 32)).convert("RGBA")
                self.assertTrue(all(p[3] == 255 for p in tile.getdata()), key)
        self.assertEqual(len({(s["x"], s["y"]) for s in slots["slots"].values()}), len(slots["slots"]))

    def test_maps_json_registers_the_room(self):
        self.real_plan()
        self.build()
        _, maps, _ = self.read()
        self.assertEqual(maps["walker"], {"bodyCells": 1, "speedCellsPerSecond": 4})
        self.assertEqual(maps["npcWalker"], {"speedCellsPerSecond": 2.5})
        (m,) = maps["maps"]
        self.assertEqual(m["id"], "TestTwoRooms")
        self.assertEqual(m["level"], "TestTwoRooms")
        self.assertEqual(m["ldtk"], "art/world/rooms/test.two-rooms/test.two-rooms.ldtk")
        self.assertEqual(m["collision"], {"rule": "interior-auto"})
        self.assertEqual(m["viewCells"], 13)               # 레벨 높이 14칸과 13 중 작은 것
        self.assertEqual(m["doors"], {})
        sx, sy = REAL_PLAN["spawn"]
        self.assertEqual(m["spawns"], {"spawn": [sx * 32 + 16, sy * 32 + 16]})

    def test_view_cells_is_level_height_when_lower_than_13(self):
        self.small_plan("t.small")
        self.build("t.small")
        _, maps, _ = self.read("t.small")
        self.assertEqual(maps["maps"][0]["viewCells"], 7)

    def test_door_cell_is_floor_with_a_door_entity_and_where_value(self):
        plan = list(SMALL)
        plan[4] = "#..D...#"
        self.small_plan("t.door", plan, doors={"D": "town"})
        self.build("t.door")
        ldtk, maps, _ = self.read("t.door")
        layers = self.layers(ldtk)
        self.assertEqual(layers["Collision"]["intGridCsv"][4 * 8 + 3], FLOOR)
        (door,) = layers["Door"]["entityInstances"]
        self.assertEqual(door["__identifier"], "Door")
        self.assertEqual(door["px"], [3 * 32, 4 * 32])
        self.assertEqual((door["width"], door["height"]), (32, 32))
        self.assertEqual([(f["__identifier"], f["__value"]) for f in door["fieldInstances"]], [("Where", "town")])
        self.assertEqual(check_loader_fields(ldtk, maps), [])

    def test_thin_wall_only_warns_and_still_writes(self):
        self.small_plan("t.thin", ["#####", "#...#", "#.###", "#...#", "#####"], spawn=(1, 1))
        self.assertEqual(len(self.build("t.thin")), 4)

    def test_same_input_same_bytes_and_lf_endings(self):
        self.real_plan()
        self.build()
        first = self.snapshot()
        for name in ("test.two-rooms.ldtk", "maps.json", "tiles.slots.json"):
            self.assertNotIn(b"\r", first[name], name)
            self.assertTrue(first[name].endswith(b"\n"), name)
        self.build(force=True)
        self.assertEqual(self.snapshot(), first)


class RefusalTests(RoomBase):
    def test_second_run_without_force_is_refused_and_changes_nothing(self):
        self.real_plan()
        self.build()
        before = self.snapshot()
        with self.assertRaises(RoomError):
            self.build()
        self.assertEqual(self.snapshot(), before)

    def test_a_single_existing_file_also_refuses(self):
        self.real_plan()
        self.out().mkdir(parents=True)
        (self.out() / "maps.json").write_text("{}", encoding="utf-8")
        with self.assertRaises(RoomError):
            self.build()
        self.assertEqual(sorted(p.name for p in self.out().iterdir()), ["maps.json"])

    def test_problem_plan_writes_nothing(self):
        plan = list(SMALL)
        for y in (3, 4, 5):
            plan[y] = "#.#..#.#"             # 세로 벽이 방을 셋으로 갈라 가운데·오른쪽 바닥에 닿지 못한다
        self.small_plan("t.bad", plan, spawn=(1, 4))
        with self.assertRaises(RoomBuildError) as e:
            self.build("t.bad")
        self.assertIn("room-unreachable-floor", {p.code for p in e.exception.problems})
        self.assertFalse((self.root / "art/world").exists())

    def test_dry_run_writes_nothing_and_lists_the_files(self):
        self.real_plan()
        self.assertEqual(len(self.build(dry_run=True)), 4)
        self.assertFalse((self.root / "art/world").exists())

    def test_unknown_wall_style_and_floor_are_errors_and_write_nothing(self):
        self.real_plan(wall="no-such-wall")
        with self.assertRaises(RecipeError):
            self.build()
        self.real_plan(floor="no.such.floor")
        with self.assertRaises(RoomError):
            self.build()
        self.real_plan(floor="bakery.exterior.flat")        # 타일이 아니다
        with self.assertRaises(RoomError):
            self.build()
        self.assertFalse((self.root / "art/world").exists())

    def test_out_dir_outside_the_repo_is_refused(self):
        self.real_plan()
        with tempfile.TemporaryDirectory() as other:
            with self.assertRaises(RoomError):
                self.build(out_dir=other)

    def test_missing_walker_source_is_an_error_and_writes_nothing(self):
        self.real_plan()
        (self.root / "game/world/maps.json").write_text('{"maps": []}', encoding="utf-8")
        with self.assertRaises(RoomError):
            self.build()
        self.assertFalse((self.root / "art/world").exists())


class AppendOnlySheetTests(RoomBase):
    def test_tiles_already_in_the_sheet_keep_their_slot_when_more_are_added(self):
        self.small_plan("t.grow", spawn=(3, 4))
        self.build("t.grow")
        _, _, slots1 = self.read("t.grow")
        with Image.open(self.out("t.grow") / "tiles.png") as im:
            old_img = im.convert("RGBA").copy()
        # 같은 방 id의 평면을 크게 바꿔(타일 종류가 늘어난다) --force로 다시 쓴다
        self.real_plan(id="t.grow", spawn=[3, 4])
        self.build("t.grow", force=True)
        ldtk2, _, slots2 = self.read("t.grow")
        self.assertGreater(len(slots2["slots"]), 16)                      # 한 줄(16칸)을 넘는다
        for key, s in slots1["slots"].items():
            self.assertEqual(slots2["slots"][key], s, key)
        self.assertEqual(list(slots2["slots"])[:len(slots1["slots"])], list(slots1["slots"]))     # 덧붙이기만
        with Image.open(self.out("t.grow") / "tiles.png") as im:
            new_img = im.convert("RGBA")
            self.assertEqual(new_img.width, 512)
            self.assertGreater(new_img.height, old_img.height)
            for key, s in slots1["slots"].items():
                box = (s["x"], s["y"], s["x"] + 32, s["y"] + 32)
                self.assertEqual(new_img.crop(box).tobytes(), old_img.crop(box).tobytes(), key)
        idx = sorted((s["y"] // 32) * 16 + s["x"] // 32 for s in slots2["slots"].values())
        self.assertEqual(idx, list(range(len(idx))))                      # 빈 칸 없이 이어 붙인다
        for name in ("Wall", "Ground"):
            for t in self.layers(ldtk2)[name]["gridTiles"]:
                self.assertEqual(t["t"], (t["src"][1] // 32) * 16 + t["src"][0] // 32)

    def test_unreadable_sheet_table_is_refused_even_with_force(self):
        self.real_plan()
        self.build()
        (self.out() / "tiles.slots.json").write_text("{ nope", encoding="utf-8")
        with self.assertRaises(RoomError):
            self.build(force=True)


class CliTests(RoomBase):
    """kit.py room build|check를 부른다. 키트는 임시 사본(방 평면을 바꿀 수 있게), 저장소는 임시 폴더."""

    def setUp(self):
        super().setUp()
        self.kit_dir = self.root / "kitcopy"
        shutil.copytree(REAL_KIT, self.kit_dir)

    def run_kit(self, *args):
        r = subprocess.run([sys.executable, str(HERE.parent / "kit.py"), "room", *args, "--kit", str(self.kit_dir),
                            "--repo", str(self.root)], capture_output=True, text=True, encoding="utf-8")
        return r.returncode, r.stdout, r.stderr

    def put(self, rid, plan, spawn=(2, 4)):
        doc = {"id": rid, **BASE, "spawn": list(spawn), "plan": plan}
        (self.kit_dir / "rooms" / f"{rid}.json").write_text(json.dumps(doc), encoding="utf-8")

    def test_check_real_plan_is_clean(self):
        code, out, _ = self.run_kit("check", "test.two-rooms")
        self.assertEqual(code, 0, out)
        self.assertIn("문제 없음", out)

    def test_check_exit_codes_problem_warning_and_usage(self):
        self.put("t.bad", ["#####", "#.#.#", "#####"], spawn=(1, 1))
        code, out, _ = self.run_kit("check", "t.bad")
        self.assertEqual(code, 1)
        self.assertIn("room-unreachable-floor", out)
        self.put("t.thin", ["#####", "#...#", "#.###", "#...#", "#####"], spawn=(1, 1))
        code, out, _ = self.run_kit("check", "t.thin")
        self.assertEqual(code, 0, out)                       # 알림만 있으면 0
        self.assertIn("알림 room-wall-thin", out)
        self.assertEqual(self.run_kit("check")[0], 2)
        self.assertEqual(self.run_kit("check", "no.such")[0], 2)
        self.assertEqual(self.run_kit("paint", "test.two-rooms")[0], 2)

    def test_build_refuses_a_second_run_and_force_and_dry_run_work(self):
        code, out, _ = self.run_kit("build", "test.two-rooms")
        self.assertEqual(code, 0, out)
        self.assertIn("저장: art/world/rooms/test.two-rooms/test.two-rooms.ldtk", out)
        before = self.snapshot()
        code, _, err = self.run_kit("build", "test.two-rooms")
        self.assertEqual(code, 2)
        self.assertIn("--force", err)
        self.assertEqual(self.run_kit("build", "test.two-rooms", "--force")[0], 0)
        self.assertEqual(self.snapshot(), before)
        code, out, _ = self.run_kit("build", "test.two-rooms", "--force", "--dry-run")
        self.assertEqual(code, 0)
        self.assertIn("--dry-run", out)
        self.assertEqual(self.snapshot(), before)

    def test_build_with_a_problem_writes_nothing_and_exits_1(self):
        self.put("t.bad", ["#####", "#.#.#", "#####"], spawn=(1, 1))
        code, out, _ = self.run_kit("build", "t.bad")
        self.assertEqual(code, 1)
        self.assertIn("room-unreachable-floor", out)
        self.assertFalse((self.root / "art/world").exists())


if __name__ == "__main__":
    unittest.main()
