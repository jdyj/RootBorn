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
from pixelkit.room_ldtk import RoomBuildError, build_room, check_room, map_id, repaint_room
from pixelkit.room_plan import RoomError, cells, is_warning, load_room
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
        # 도착 지점 이름은 맵 사이에서 겹치지 않게 <맵 id 소문자>-entry(game/world/README.md 4절)
        self.assertEqual(m["spawns"], {"testtworooms-entry": [sx * 32 + 16, sy * 32 + 16]})

    def test_slots_table_remembers_wall_floor_and_face_height_for_repaint(self):
        self.real_plan(face_height=2)
        self.build()
        _, _, slots = self.read()
        self.assertEqual((slots["wall"], slots["floor"], slots["face_height"]), ("paper", "bakery.tile.floor", 2))

    def test_force_refuses_a_sheet_drawn_with_another_wall_and_writes_nothing(self):
        self.real_plan()
        self.build()
        before = self.snapshot()
        self.real_plan(wall="plaster")
        with self.assertRaises(RoomError):
            self.build(force=True)
        self.assertEqual(self.snapshot(), before)

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

    def test_force_refuses_an_out_dir_holding_the_game_map_list(self):
        self.real_plan()
        maps = self.root / "game/world/maps.json"
        before = maps.read_bytes()
        with self.assertRaises(RoomError) as e:
            self.build(out_dir=self.root / "game/world", force=True)
        self.assertIn("maps.json", str(e.exception))
        self.assertEqual(maps.read_bytes(), before)
        self.assertEqual(sorted(p.name for p in (self.root / "game/world").iterdir()), ["maps.json"])

    def test_force_refuses_another_rooms_folder(self):
        self.small_plan("t.other")
        self.build("t.other")
        self.real_plan()
        before = self.snapshot("t.other")
        with self.assertRaises(RoomError):
            self.build(out_dir=self.out("t.other"), force=True)
        self.assertEqual(self.snapshot("t.other"), before)

    def test_force_refuses_an_ldtk_of_the_same_name_that_is_not_a_room(self):
        game_map = ROOT / "Assets/LDtk/House/house.ldtk"
        folder = self.root / "Assets/LDtk/House"
        folder.mkdir(parents=True)
        shutil.copyfile(game_map, folder / "house.ldtk")
        self.small_plan("house")
        with self.assertRaises(RoomError) as e:
            self.build("house", out_dir=folder, force=True)
        self.assertIn("house.ldtk", str(e.exception))
        self.assertEqual((folder / "house.ldtk").read_bytes(), game_map.read_bytes())
        self.assertEqual(sorted(p.name for p in folder.iterdir()), ["house.ldtk"])

    def test_force_refuses_a_tile_sheet_without_its_slots_table(self):
        self.real_plan()
        self.out().mkdir(parents=True)
        Image.new("RGBA", (512, 32)).save(self.out() / "tiles.png")
        before = self.snapshot()
        with self.assertRaises(RoomError):
            self.build(force=True)
        self.assertEqual(self.snapshot(), before)

    def test_force_rebuilds_its_own_room_in_an_explicit_out_dir(self):
        self.real_plan()
        folder = self.root / "rooms-elsewhere"
        self.build(out_dir=folder)
        before = {p.name: p.read_bytes() for p in folder.iterdir()}
        self.assertEqual(self.build(out_dir=folder, force=True), [])
        self.assertEqual({p.name: p.read_bytes() for p in folder.iterdir()}, before)

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


class CliBase(RoomBase):
    """kit.py room build|check|repaint를 부른다. 키트는 임시 사본(방 평면을 바꿀 수 있게), 저장소는 임시 폴더."""

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


class CliTests(CliBase):
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

    def test_build_with_a_door_letter_warns_that_the_doors_table_is_empty_but_still_writes(self):
        plan = list(SMALL)
        plan[4] = "#..D...#"
        doc = {"id": "t.door", **BASE, "spawn": [2, 4], "plan": plan, "doors": {"D": "town"}}
        (self.kit_dir / "rooms" / "t.door.json").write_text(json.dumps(doc), encoding="utf-8")
        code, out, _ = self.run_kit("build", "t.door")
        self.assertEqual(code, 0, out)
        self.assertIn("알림 room-door-unregistered", out)
        self.assertIn("town", out)
        self.assertTrue((self.root / "art/world/rooms/t.door/t.door.ldtk").exists())
        # 평면에 문이 없으면 알림도 없다
        code, out, _ = self.run_kit("build", "test.two-rooms")
        self.assertNotIn("room-door-unregistered", out)

    def test_force_over_existing_files_warns_that_hand_edits_are_lost(self):
        code, out, _ = self.run_kit("build", "test.two-rooms")
        self.assertEqual(code, 0, out)
        self.assertNotIn("repaint", out)                      # 처음 만들 때는 알림이 없다
        code, out, _ = self.run_kit("build", "test.two-rooms", "--force")
        self.assertEqual(code, 0, out)
        warn = [line for line in out.splitlines() if line.startswith("알림") and "--force" in line]
        self.assertEqual(len(warn), 1, out)
        self.assertIn("room repaint", warn[0])
        # 쓰지 않으면(--dry-run) 알리지 않는다
        code, out, _ = self.run_kit("build", "test.two-rooms", "--force", "--dry-run")
        self.assertEqual(code, 0, out)
        self.assertNotIn("room repaint", out)

    def test_force_note_is_not_printed_when_the_build_is_refused(self):
        code, out, err = self.run_kit("build", "test.two-rooms", "--force", "--out", str(self.root / "game/world"))
        self.assertEqual(code, 2, out + err)
        self.assertNotIn("room repaint", out)


FIXTURE = HERE / "fixtures" / "room-saved-by-editor.ldtk"
RID = "test.two-rooms"


def _strip_art(doc):
    """Wall·Ground의 gridTiles와 타일셋 크기를 뺀 사본(repaint가 바꿔도 되는 것만 지운다)."""
    doc = copy.deepcopy(doc)
    for lv in doc["levels"]:
        for li in lv["layerInstances"]:
            if li["__identifier"] in ("Wall", "Ground"):
                li["gridTiles"] = None
    for ts in doc["defs"]["tilesets"]:
        for k in ("pxWid", "pxHei", "__cWid", "__cHei"):
            if k in ts:
                ts[k] = None
    return doc


def _level(doc):
    return doc["levels"][0]


def _layer(doc, name):
    return next(li for li in _level(doc)["layerInstances"] if li["__identifier"] == name)


class RepaintTests(RoomBase):
    """편집기가 저장한 모양을 흉내 낸 고정 파일(LDtk 1.5.3 house.ldtk의 키·키 순서·머리말·탭 들여쓰기, Furniture 엔티티와
    그 정의, 작은 옛 시트, Collision 두 칸을 다시 칠함). 진짜 편집기가 저장한 것은 아니다 — 편집기에서 열고 저장한 파일로 바꿀 수 있으면 바꾼다."""

    def setUp(self):
        super().setUp()
        self.real_plan()
        self.build()
        self.ldtk_path = self.out() / f"{RID}.ldtk"
        shutil.copyfile(FIXTURE, self.ldtk_path)

    def doc(self):
        return json.loads(self.ldtk_path.read_text("utf-8"))

    def codes(self):
        return [p.code for p in check_room(self.ldtk_path)]

    def test_fixture_looks_saved_by_the_editor_and_is_out_of_sync(self):
        doc = self.doc()
        self.assertEqual(doc["__header__"]["app"], "LDtk")
        self.assertIn(b"\n\t\"", FIXTURE.read_bytes())          # 편집기처럼 탭 들여쓰기(이 도구는 공백 1칸)
        self.assertEqual(len(_layer(doc, "Furniture")["entityInstances"]), 1)
        self.assertIn("room-art-mismatch", self.codes())

    def test_repaint_makes_wall_and_ground_tiles_follow_collision(self):
        written = repaint_room(self.ldtk_path)
        self.assertIn(f"{RID}.ldtk", written)
        doc = self.doc()
        self.assertNotIn("room-art-mismatch", self.codes())
        col = _layer(doc, "Collision")
        w, h = col["__cWid"], col["__cHei"]
        grid = [col["intGridCsv"][y * w:(y + 1) * w] for y in range(h)]
        keys = classify(grid)
        slots = json.loads((self.out() / "tiles.slots.json").read_text("utf-8"))["slots"]
        for name, want in (("Wall", WALL), ("Ground", FLOOR)):
            tiles = _layer(doc, name)["gridTiles"]
            self.assertEqual({(t["px"][0] // 32, t["px"][1] // 32) for t in tiles},
                             {(x, y) for y in range(h) for x in range(w) if grid[y][x] == want}, name)
            for t in tiles:
                x, y = t["px"][0] // 32, t["px"][1] // 32
                s = slots[keys[y][x]]
                self.assertEqual(t["src"], [s["x"], s["y"]])
                self.assertEqual(t["t"], (s["y"] // 32) * 16 + s["x"] // 32)
                self.assertEqual(t["d"], [y * w + x])

    def test_everything_but_the_two_tile_layers_and_tileset_size_is_kept_in_value_and_key_order(self):
        before = self.doc()
        repaint_room(self.ldtk_path)
        after = self.doc()
        # 키 순서까지: 정렬하지 않고 덤프한 글이 같다
        self.assertEqual(json.dumps(_strip_art(after), ensure_ascii=False), json.dumps(_strip_art(before), ensure_ascii=False))
        self.assertEqual(after["__header__"], before["__header__"])
        self.assertEqual(_layer(after, "Furniture"), _layer(before, "Furniture"))
        self.assertEqual(_layer(after, "Collision"), _layer(before, "Collision"))
        for name in ("Wall", "Ground"):
            self.assertEqual(list(_layer(after, name)), list(_layer(before, name)), name + " 층의 키 순서")

    def test_new_tiles_are_appended_and_old_slots_keep_their_picture(self):
        old_slots = json.loads((self.out() / "tiles.slots.json").read_text("utf-8"))["slots"]
        with Image.open(self.out() / "tiles.png") as im:
            old = im.convert("RGBA").copy()
        repaint_room(self.ldtk_path)
        table = json.loads((self.out() / "tiles.slots.json").read_text("utf-8"))
        self.assertGreater(len(table["slots"]), len(old_slots))           # 기둥·넓힌 문간으로 새 열쇠가 생긴다
        self.assertEqual(list(table["slots"])[:len(old_slots)], list(old_slots))
        for k, s in old_slots.items():
            self.assertEqual(table["slots"][k], s, k)
        with Image.open(self.out() / "tiles.png") as im:
            new = im.convert("RGBA")
            for k, s in old_slots.items():
                box = (s["x"], s["y"], s["x"] + 32, s["y"] + 32)
                self.assertEqual(new.crop(box).tobytes(), old.crop(box).tobytes(), k)
            ts = self.doc()["defs"]["tilesets"][0]
            self.assertEqual((ts["pxWid"], ts["pxHei"]), new.size)
            self.assertEqual((ts["__cWid"], ts["__cHei"]), (new.width // 32, new.height // 32))
        self.assertEqual((table["wall"], table["floor"], table["face_height"]), ("paper", "bakery.tile.floor", 2))

    def test_tileset_cell_count_follows_a_sheet_that_grows(self):
        # 타일 종류가 적은 평면으로 시트를 한 줄(32px)만 만든 뒤, 편집기에서 칠한 큰 방을 repaint해 시트를 키운다
        for p in self.out().iterdir():
            p.unlink()
        self.small_plan(RID)
        self.build()
        with Image.open(self.out() / "tiles.png") as im:
            old_h = im.height
        doc = json.loads(FIXTURE.read_text("utf-8"))
        ts = doc["defs"]["tilesets"][0]
        ts["pxHei"], ts["__cHei"] = old_h, old_h // 32                 # 편집기가 그 작은 시트로 저장한 모양
        self.ldtk_path.write_text(json.dumps(doc, indent="	"), encoding="utf-8")
        repaint_room(self.ldtk_path)
        with Image.open(self.out() / "tiles.png") as im:
            new_w, new_h = im.size
        self.assertGreater(new_h, old_h)
        ts = self.doc()["defs"]["tilesets"][0]
        self.assertEqual((ts["pxWid"], ts["pxHei"], ts["__cWid"], ts["__cHei"]), (new_w, new_h, new_w // 32, new_h // 32))
        self.assertNotIn("room-art-mismatch", self.codes())

    def test_second_repaint_has_nothing_to_change(self):
        repaint_room(self.ldtk_path)
        first = self.snapshot()
        self.assertEqual(repaint_room(self.ldtk_path), [])
        self.assertEqual(self.snapshot(), first)
        self.assertLessEqual(set(self.codes()), {"room-wall-thin"})          # 기둥은 얇은 벽 알림만

    def test_tiles_in_another_order_count_as_unchanged(self):
        repaint_room(self.ldtk_path)
        doc = self.doc()
        for name in ("Wall", "Ground"):
            _layer(doc, name)["gridTiles"].reverse()
        self.ldtk_path.write_text(json.dumps(doc, indent="\t"), encoding="utf-8")
        before = self.snapshot()
        self.assertEqual(repaint_room(self.ldtk_path), [])
        self.assertEqual(self.snapshot(), before)

    def test_dry_run_lists_files_and_writes_nothing(self):
        before = self.snapshot()
        listed = repaint_room(self.ldtk_path, dry_run=True)
        self.assertIn(f"{RID}.ldtk", listed)
        self.assertIn("tiles.png", listed)
        self.assertEqual(self.snapshot(), before)

    def test_level_grown_in_the_editor_follows_the_collision_size(self):
        doc = self.doc()
        col = _layer(doc, "Collision")
        w, h = col["__cWid"], col["__cHei"]
        rows = [col["intGridCsv"][y * w:(y + 1) * w] for y in range(h)]
        col["intGridCsv"] = [v for row in rows for v in row + [WALL]]       # 오른쪽에 벽 한 줄
        for li in _level(doc)["layerInstances"]:
            li["__cWid"] = w + 1
        _level(doc)["pxWid"] = (w + 1) * 32
        self.ldtk_path.write_text(json.dumps(doc), encoding="utf-8")
        repaint_room(self.ldtk_path)
        wall = _layer(self.doc(), "Wall")["gridTiles"]
        self.assertIn([w * 32, 0], [t["px"] for t in wall])
        for t in wall:
            x, y = t["px"][0] // 32, t["px"][1] // 32
            self.assertEqual(t["d"], [y * (w + 1) + x])
        self.assertNotIn("room-art-mismatch", self.codes())

    def test_a_document_without_the_room_layers_is_refused_and_nothing_is_written(self):
        for drop in ("Wall", "Ground", "Collision"):
            doc = self.doc()
            _level(doc)["layerInstances"] = [li for li in _level(doc)["layerInstances"] if li["__identifier"] != drop]
            self.ldtk_path.write_text(json.dumps(doc), encoding="utf-8")
            before = self.snapshot()
            self.assertEqual([p.code for p in check_room(self.ldtk_path)], ["room-not-a-room"], drop)
            with self.assertRaises(RoomBuildError) as e:
                repaint_room(self.ldtk_path)
            self.assertEqual([p.code for p in e.exception.problems], ["room-not-a-room"])
            self.assertEqual(self.snapshot(), before, drop)
        doc = json.loads(FIXTURE.read_text("utf-8"))
        _layer(doc, "Wall")["__type"] = "IntGrid"                        # 이름은 있어도 종류가 다르다
        self.ldtk_path.write_text(json.dumps(doc), encoding="utf-8")
        self.assertEqual([p.code for p in check_room(self.ldtk_path)], ["room-not-a-room"])

    def test_layers_whose_size_differs_from_collision_are_refused(self):
        def wall_wider(doc):
            _layer(doc, "Wall")["__cWid"] += 1

        def ground_taller(doc):
            _layer(doc, "Ground")["__cHei"] += 1

        def level_wider(doc):
            _level(doc)["pxWid"] += 32

        def level_taller(doc):
            _level(doc)["pxHei"] -= 32

        for change in (wall_wider, ground_taller, level_wider, level_taller):
            doc = json.loads(FIXTURE.read_text("utf-8"))
            change(doc)
            self.ldtk_path.write_text(json.dumps(doc), encoding="utf-8")
            before = self.snapshot()
            self.assertEqual([p.code for p in check_room(self.ldtk_path)], ["room-not-a-room"], change.__name__)
            with self.assertRaises(RoomBuildError) as e:
                repaint_room(self.ldtk_path, dry_run=True)
            self.assertEqual([p.code for p in e.exception.problems], ["room-not-a-room"], change.__name__)
            self.assertEqual(self.snapshot(), before, change.__name__)

    def test_a_painted_problem_refuses_and_writes_nothing(self):
        doc = self.doc()
        col = _layer(doc, "Collision")
        w = col["__cWid"]
        for y in range(3, 9):                                          # 문간을 막아 오른쪽 방에 닿지 못한다
            col["intGridCsv"][y * w + 9] = WALL
        self.ldtk_path.write_text(json.dumps(doc), encoding="utf-8")
        before = self.snapshot()
        with self.assertRaises(RoomBuildError) as e:
            repaint_room(self.ldtk_path)
        self.assertIn("room-unreachable-floor", [p.code for p in e.exception.problems])
        self.assertEqual(self.snapshot(), before)

    def test_old_slots_table_without_room_values_is_refused(self):
        table = json.loads((self.out() / "tiles.slots.json").read_text("utf-8"))
        for k in ("wall", "floor", "face_height"):
            del table[k]
        (self.out() / "tiles.slots.json").write_text(json.dumps(table), encoding="utf-8")
        before = self.snapshot()
        with self.assertRaises(RoomError):
            repaint_room(self.ldtk_path)
        self.assertEqual(self.snapshot(), before)


class CheckRoomTests(RoomBase):
    def test_a_fresh_build_is_clean(self):
        self.real_plan()
        self.build()
        self.assertEqual(check_room(self.out() / f"{RID}.ldtk"), [])

    def test_door_whose_where_is_not_in_the_doors_table_is_a_warning(self):
        plan = list(SMALL)
        plan[4] = "#..D...#"
        self.small_plan("t.door", plan, doors={"D": "Shopping-Center"})
        self.build("t.door")
        path = self.out("t.door") / "t.door.ldtk"
        problems = check_room(path)
        self.assertEqual([p.code for p in problems], ["room-door-unregistered"])
        self.assertTrue(is_warning(problems[0]))
        maps = json.loads((self.out("t.door") / "maps.json").read_text("utf-8"))
        maps["maps"][0]["doors"] = {"shopping center": {"map": "Town", "spawn": "town-entry"}}   # 게임처럼 대소문자·하이픈을 고르게 본다
        (self.out("t.door") / "maps.json").write_text(json.dumps(maps), encoding="utf-8")
        self.assertEqual(check_room(path), [])


class RepaintCliTests(CliBase):
    def test_repaint_and_check_on_an_ldtk_path(self):
        self.assertEqual(self.run_kit("build", "test.two-rooms")[0], 0)
        path = self.root / "art/world/rooms" / RID / f"{RID}.ldtk"
        shutil.copyfile(FIXTURE, path)
        code, out, _ = self.run_kit("check", str(path))
        self.assertEqual(code, 1, out)
        self.assertIn("room-art-mismatch", out)
        code, out, _ = self.run_kit("repaint", str(path), "--dry-run")
        self.assertEqual(code, 0, out)
        self.assertIn("--dry-run", out)
        code, out, _ = self.run_kit("repaint", str(path))
        self.assertEqual(code, 0, out)
        self.assertIn("저장:", out)
        code, out, _ = self.run_kit("repaint", str(path))
        self.assertEqual(code, 0, out)
        self.assertIn("바꿀 것 없음", out)
        code, out, _ = self.run_kit("check", str(path))
        self.assertEqual(code, 0, out)
        self.assertNotIn("room-art-mismatch", out)

    def test_repaint_refuses_a_non_room_and_a_missing_file(self):
        self.assertEqual(self.run_kit("build", "test.two-rooms")[0], 0)
        path = self.root / "art/world/rooms" / RID / f"{RID}.ldtk"
        doc = json.loads(path.read_text("utf-8"))
        doc["levels"][0]["layerInstances"] = [li for li in doc["levels"][0]["layerInstances"] if li["__identifier"] != "Wall"]
        path.write_text(json.dumps(doc), encoding="utf-8")
        code, out, _ = self.run_kit("repaint", str(path))
        self.assertEqual(code, 1)
        self.assertIn("room-not-a-room", out)
        self.assertEqual(self.run_kit("repaint", str(path.with_name("nope.ldtk")))[0], 2)
        self.assertEqual(self.run_kit("repaint")[0], 2)


class RealGameMapsAreNotRoomsTests(unittest.TestCase):
    """저장소의 실제 게임 맵·시험 맵을 repaint가 방으로 잘못 알고 고치지 않는다(읽기만, dry-run)."""

    def test_repaint_refuses_every_real_game_map(self):
        groups = {"Assets/LDtk/**/*.ldtk": sorted(ROOT.glob("Assets/LDtk/**/*.ldtk")),
                  "art/world/test/*.ldtk": sorted(ROOT.glob("art/world/test/*.ldtk"))}
        for pattern, paths in groups.items():
            self.assertTrue(paths, f"{pattern}에 맞는 파일이 없다(저장소를 못 찾았거나 맵이 옮겨졌다)")
        for path in (p for paths in groups.values() for p in paths):
            before = path.read_bytes()
            with self.assertRaises(RoomBuildError, msg=str(path)) as e:
                repaint_room(path, dry_run=True, kit=KIT)
            self.assertEqual([p.code for p in e.exception.problems], ["room-not-a-room"], str(path))
            self.assertEqual(path.read_bytes(), before, str(path))


if __name__ == "__main__":
    unittest.main()
