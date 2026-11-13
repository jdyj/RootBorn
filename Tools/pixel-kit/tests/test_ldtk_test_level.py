"""시험용 LDtk 레벨 쓰기. 가짜 저장소(임시 폴더)에서 시트를 내보낸 뒤 레벨을 쓰고 다시 읽어 본다.

C#을 파이썬에서 부를 수 없으므로 `check_loader_fields`가 game/Rootborn.World/Ldtk/LdtkFile.cs와
WorldCatalog.cs가 *필수*로 읽는 필드(GetProperty)를 그대로 옮겨 적어 검사한다. 진짜 읽기는 Task 8에서 게임으로 본다.
"""
import copy
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from pixelkit import export, ldtk_test_level
from pixelkit.cli import load_kit

HERE = Path(__file__).resolve().parent
KIT_PY = HERE.parent / "kit.py"
REAL_KIT = HERE.parents[2] / "art" / "kit"
REAL_MAPS = HERE.parents[2] / "game" / "world" / "maps.json"


def is_int(v):
    return isinstance(v, int) and not isinstance(v, bool)


def is_num(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def pair(v, check):
    return isinstance(v, list) and len(v) == 2 and all(check(x) for x in v)


def need(d, key, check, where, problems):
    if not isinstance(d, dict) or key not in d:
        problems.append(f"{where}: {key} 없음")
    elif not check(d[key]):
        problems.append(f"{where}: {key} 형이 틀렸다({d[key]!r})")


def check_loader_fields(ldtk: dict, maps: dict) -> list:
    """LdtkFile.cs(Load·ReadLevel·ReadEntity)와 WorldCatalog.cs(Parse)가 GetProperty로 읽는 필드를 모두 확인한다."""
    p = []
    need(ldtk, "defs", lambda v: isinstance(v, dict), "root", p)
    need(ldtk, "levels", lambda v: isinstance(v, list), "root", p)
    if p:
        return p
    uids = {}
    need(ldtk["defs"], "tilesets", lambda v: isinstance(v, list), "defs", p)
    need(ldtk["defs"], "layers", lambda v: isinstance(v, list), "defs", p)
    if p:
        return p
    for ts in ldtk["defs"]["tilesets"]:
        w = f"tileset {ts.get('identifier')}"
        need(ts, "uid", is_int, w, p)
        need(ts, "identifier", lambda v: isinstance(v, str), w, p)
        need(ts, "pxWid", is_int, w, p)
        need(ts, "pxHei", is_int, w, p)
        need(ts, "tileGridSize", is_int, w, p)
        if "relPath" in ts:
            need(ts, "relPath", lambda v: isinstance(v, str), w, p)
        uids[ts.get("uid")] = ts
    for ld in ldtk["defs"]["layers"]:
        need(ld, "identifier", lambda v: isinstance(v, str), "layer def", p)
        if "uiFilterTags" in ld:
            need(ld, "uiFilterTags", lambda v: isinstance(v, list) and all(isinstance(t, str) for t in v), "layer def", p)
    for lv in ldtk["levels"]:
        w = f"level {lv.get('identifier')}"
        need(lv, "identifier", lambda v: isinstance(v, str), w, p)
        need(lv, "pxWid", is_int, w, p)
        need(lv, "pxHei", is_int, w, p)
        need(lv, "layerInstances", lambda v: isinstance(v, list), w, p)
        for li in lv.get("layerInstances") or []:
            lw = f"{w} layer {li.get('__identifier')}"
            need(li, "__identifier", lambda v: isinstance(v, str), lw, p)
            need(li, "__type", lambda v: isinstance(v, str), lw, p)
            for k in ("__gridSize", "__cWid", "__cHei", "__pxTotalOffsetX", "__pxTotalOffsetY"):
                need(li, k, is_int, lw, p)
            if "__opacity" in li:
                need(li, "__opacity", is_num, lw, p)
            if "visible" in li:
                need(li, "visible", lambda v: isinstance(v, bool), lw, p)
            if "__tilesetDefUid" in li and li["__tilesetDefUid"] not in uids:
                p.append(f"{lw}: __tilesetDefUid가 타일셋에 없다")
            for t in li.get("gridTiles") or []:
                need(t, "px", lambda v: pair(v, is_int), f"{lw} tile", p)
                need(t, "src", lambda v: pair(v, is_int), f"{lw} tile", p)
                need(t, "f", is_int, f"{lw} tile", p)
                if "a" in t:
                    need(t, "a", is_num, f"{lw} tile", p)
            for e in li.get("entityInstances") or []:
                ew = f"{lw} entity {e.get('__identifier')}"
                need(e, "__identifier", lambda v: isinstance(v, str), ew, p)
                need(e, "px", lambda v: pair(v, is_int), ew, p)
                need(e, "width", is_int, ew, p)
                need(e, "height", is_int, ew, p)
                if "__pivot" in e:
                    need(e, "__pivot", lambda v: pair(v, is_num), ew, p)
                if isinstance(e.get("__tile"), dict):
                    for k in ("tilesetUid", "x", "y", "w", "h"):
                        need(e["__tile"], k, is_int, ew + " __tile", p)
    if "walker" in maps:
        need(maps["walker"], "bodyCells", is_num, "walker", p)
        need(maps["walker"], "speedCellsPerSecond", is_num, "walker", p)
    need(maps, "npcWalker", lambda v: isinstance(v, dict) and is_num(v.get("speedCellsPerSecond")), "maps", p)
    need(maps, "maps", lambda v: isinstance(v, list), "maps", p)
    for m in maps.get("maps") or []:
        w = f"map {m.get('id')}"
        for k in ("id", "ldtk", "level"):
            need(m, k, lambda v: isinstance(v, str), w, p)
        need(m, "viewCells", is_num, w, p)
        need(m, "collision", lambda v: isinstance(v, dict) and isinstance(v.get("rule"), str), w, p)
        need(m, "doors", lambda v: isinstance(v, dict), w, p)
        need(m, "spawns", lambda v: isinstance(v, dict) and all(pair(x, is_num) for x in v.values()), w, p)
    return p


MAPS_WITH_COMMENTS = """{
  // 시험용 가짜 맵 목록(주석이 있다: 진짜 maps.json처럼)
  "walker": { "bodyCells": 1, "speedCellsPerSecond": 4 },
  // 느리게 둔다
  "npcWalker": { "speedCellsPerSecond": 2.5 },
  "maps": [ ]
}
"""


class LevelBase(unittest.TestCase):
    def setUp(self):
        t = tempfile.TemporaryDirectory()
        self.addCleanup(t.cleanup)
        self.root = Path(t.name)
        self.kit = load_kit(str(REAL_KIT))
        (self.root / "game/world").mkdir(parents=True)
        (self.root / "game/world/maps.json").write_text(MAPS_WITH_COMMENTS, encoding="utf-8")

    def recipe(self, src, new_id):
        r = copy.deepcopy(self.kit.recipes[src])
        r["id"] = new_id
        return r

    def export(self, recipes, kind, name):
        plan = export.export_sheet(recipes, kind, name, self.kit, self.root)
        self.assertEqual(plan.problems, [])
        export.apply(plan, self.root)

    def export_all(self):
        self.export([self.recipe("bakery.tile.floor", "test.tile.floor"), self.recipe("bakery.tile.grass", "test.tile.grass")],
                    "tile", "test")
        self.export([self.recipe("bakery.exterior.flat", "test.exterior.flat"), self.recipe("bakery.menu-board", "test.menu-board"),
                     self.recipe("bakery.rug", "test.rug")], "building", "test")

    def write(self, out=None):
        out = out or self.root / "art/world/test"
        written = ldtk_test_level.write_test_level(self.root, out)
        return Path(out), written

    def read(self, out):
        return (json.loads((out / "pixel-kit-test.ldtk").read_text("utf-8")),
                json.loads((out / "maps.json").read_text("utf-8")))


class TestLevelTests(LevelBase):
    def test_files_load_as_json_and_satisfy_every_required_loader_field(self):
        self.export_all()
        out, _ = self.write()
        ldtk, maps = self.read(out)
        self.assertEqual(check_loader_fields(ldtk, maps), [])

    def test_validator_itself_notices_a_missing_field(self):
        self.export_all()
        out, _ = self.write()
        ldtk, maps = self.read(out)
        del ldtk["levels"][0]["layerInstances"][0]["__cWid"]
        self.assertTrue(check_loader_fields(ldtk, maps))
        ldtk, maps = self.read(out)
        maps["npcWalker"] = {}
        self.assertTrue(check_loader_fields(ldtk, maps))

    def test_tilesets_point_at_existing_pngs_with_measured_size(self):
        self.export_all()
        out, _ = self.write()
        ldtk, _ = self.read(out)
        sets = ldtk["defs"]["tilesets"]
        self.assertEqual(len(sets), 2)
        for ts in sets:
            self.assertNotIn("\\", ts["relPath"])
            png = (out / ts["relPath"]).resolve()
            self.assertTrue(png.exists(), ts["relPath"])
            with Image.open(png) as im:
                self.assertEqual((ts["pxWid"], ts["pxHei"]), im.size)
            self.assertEqual(ts["tileGridSize"], 32)

    def test_ground_tiles_use_the_first_32px_tile_slot_and_fill_the_level(self):
        self.export_all()
        out, _ = self.write()
        ldtk, _ = self.read(out)
        level = ldtk["levels"][0]
        slots = json.loads((self.root / "art/tiles/test.png.slots.json").read_text("utf-8"))["slots"]
        first = slots["test.tile.floor"]
        ground = next(l for l in level["layerInstances"] if l["__identifier"] == "Ground")
        self.assertEqual(len(ground["gridTiles"]), ground["__cWid"] * ground["__cHei"])
        for t in ground["gridTiles"]:
            self.assertEqual(t["src"], [first["x"], first["y"]])
            self.assertEqual((t["px"][0] % 32, t["px"][1] % 32), (0, 0))
        self.assertEqual((level["pxWid"], level["pxHei"]), (ground["__cWid"] * 32, ground["__cHei"] * 32))
        self.assertEqual((level["pxWid"] % 32, level["pxHei"] % 32), (0, 0))

    def test_entities_are_the_world_slots_once_each_in_a_row_with_foot_pivot(self):
        self.export_all()
        out, _ = self.write()
        ldtk, _ = self.read(out)
        level = ldtk["levels"][0]
        slots = json.loads((self.root / "art/world/test.png.slots.json").read_text("utf-8"))["slots"]
        art = next(l for l in level["layerInstances"] if l["__identifier"] == "Art")
        ents = art["entityInstances"]
        self.assertEqual([e["__identifier"] for e in ents], list(slots))
        world_png = (self.root / "art/world/test.png").resolve()
        uid = next(t["uid"] for t in ldtk["defs"]["tilesets"] if (out / t["relPath"]).resolve() == world_png)
        base_y = {e["px"][1] for e in ents}
        self.assertEqual(len(base_y), 1)
        prev_right = None
        for e in ents:
            s = slots[e["__identifier"]]
            self.assertEqual(e["__tile"], {"tilesetUid": uid, "x": s["x"], "y": s["y"], "w": s["w"], "h": s["h"]})
            self.assertEqual((e["width"], e["height"], e["__pivot"]), (s["w"], s["h"], [0.5, 1]))
            left = e["px"][0] - s["w"] // 2
            if prev_right is not None:
                self.assertGreaterEqual(left - prev_right, 32)
            prev_right = e["px"][0] + s["w"] // 2
        self.assertLessEqual(prev_right, level["pxWid"])
        self.assertLessEqual(base_y.pop(), level["pxHei"])
        tallest = max(s["h"] for s in slots.values())
        self.assertGreaterEqual(ents[0]["px"][1] - tallest, 0)

    def test_art_layer_carries_the_art_tag_in_the_layer_definition(self):
        self.export_all()
        out, _ = self.write()
        ldtk, _ = self.read(out)
        defs = {d["identifier"]: d for d in ldtk["defs"]["layers"]}
        self.assertIn("art", defs["Art"]["uiFilterTags"])
        self.assertNotIn("art", defs["Ground"].get("uiFilterTags", []))
        names = [l["__identifier"] for l in ldtk["levels"][0]["layerInstances"]]
        self.assertEqual(names, ["Art", "Ground"])    # 파일은 위 층이 먼저

    def test_maps_json_copies_walkers_and_spawn_is_inside_the_level_below_the_first_building(self):
        self.export_all()
        out, _ = self.write()
        ldtk, maps = self.read(out)
        self.assertEqual(maps["walker"], {"bodyCells": 1, "speedCellsPerSecond": 4})
        self.assertEqual(maps["npcWalker"], {"speedCellsPerSecond": 2.5})
        self.assertEqual([m["id"] for m in maps["maps"]], ["PixelKitTest"])
        m = maps["maps"][0]
        self.assertEqual(m["level"], ldtk["levels"][0]["identifier"])
        self.assertEqual(m["collision"], {"rule": "collider-layers", "layers": []})
        self.assertEqual(m["doors"], {})
        self.assertNotIn("\\", m["ldtk"])
        self.assertEqual(m["ldtk"], "art/world/test/pixel-kit-test.ldtk")
        (sx, sy), = m["spawns"].values()
        level = ldtk["levels"][0]
        self.assertTrue(0 <= sx < level["pxWid"] and 0 <= sy < level["pxHei"])
        first = next(l for l in level["layerInstances"] if l["__identifier"] == "Art")["entityInstances"][0]
        self.assertGreater(sy, first["px"][1])
        self.assertLessEqual(abs(sx - first["px"][0]), 32)

    def test_rewrite_is_byte_identical_and_writes_nothing(self):
        self.export_all()
        out, written = self.write()
        self.assertEqual(sorted(Path(w).name for w in written), ["maps.json", "pixel-kit-test.ldtk"])
        snap = {p.name: (p.read_bytes(), p.stat().st_mtime_ns) for p in out.iterdir()}
        _, written2 = self.write()
        self.assertEqual(written2, [])
        self.assertEqual({p.name: (p.read_bytes(), p.stat().st_mtime_ns) for p in out.iterdir()}, snap)

    def test_result_is_stable_across_sheet_growth_only_by_appending(self):
        self.export_all()
        self.write()
        self.export([self.recipe("bakery.a-board", "test.a-board")], "prop", "test")
        out, _ = self.write()
        ldtk, maps = self.read(out)
        self.assertEqual(check_loader_fields(ldtk, maps), [])
        names = [e["__identifier"] for l in ldtk["levels"][0]["layerInstances"] for e in l.get("entityInstances", [])]
        self.assertEqual(names[-1], "test.a-board")

    def test_missing_tile_sheet_or_world_sheet_or_maps_is_an_error(self):
        with self.assertRaises(ldtk_test_level.TestLevelError):
            self.write()    # 시트가 하나도 없다
        self.export([self.recipe("bakery.tile.floor", "test.tile.floor")], "tile", "test")
        with self.assertRaises(ldtk_test_level.TestLevelError):
            self.write()    # 건물·소품 시트가 없다

    def test_out_dir_outside_the_repo_is_an_error(self):
        self.export_all()
        with tempfile.TemporaryDirectory() as other:
            with self.assertRaises(ldtk_test_level.TestLevelError):
                self.write(Path(other))

    def test_refuses_to_overwrite_a_real_maps_json(self):
        self.export_all()
        out = self.root / "game/world"
        real = (out / "maps.json").read_bytes()
        with self.assertRaises(ldtk_test_level.TestLevelError):
            self.write(out)
        self.assertEqual((out / "maps.json").read_bytes(), real)
        self.assertFalse((out / "pixel-kit-test.ldtk").exists())
        (out / "maps.json").write_text('{ "maps": [ {"id": "Other"} ], "npcWalker": {"speedCellsPerSecond": 1} }', "utf-8")
        with self.assertRaises(ldtk_test_level.TestLevelError):
            self.write(out)

    def test_refuses_to_overwrite_an_ldtk_this_tool_did_not_make(self):
        self.export_all()
        out = self.root / "art/world/test"
        out.mkdir(parents=True)
        (out / "pixel-kit-test.ldtk").write_text('{"defs": {}}', "utf-8")
        with self.assertRaises(ldtk_test_level.TestLevelError):
            self.write(out)
        self.assertEqual((out / "pixel-kit-test.ldtk").read_text("utf-8"), '{"defs": {}}')

    def test_rewriting_its_own_files_still_works(self):
        self.export_all()
        out, _ = self.write()
        _, written = self.write(out)
        self.assertEqual(written, [])
        self.export([self.recipe("bakery.menu-board", "test.z-board")], "prop", "test")
        self.assertTrue(self.write(out)[1])

    def test_walker_settings_are_required_in_the_source_maps(self):
        self.export_all()
        (self.root / "game/world/maps.json").write_text('{ "maps": [] }', encoding="utf-8")
        with self.assertRaises(ldtk_test_level.TestLevelError):
            self.write()

    def test_reads_the_real_maps_file_with_comments(self):
        self.export_all()
        (self.root / "game/world/maps.json").write_bytes(REAL_MAPS.read_bytes())
        out, _ = self.write()
        _, maps = self.read(out)
        self.assertIn("npcWalker", maps)
        self.assertEqual(len(maps["maps"]), 1)


class TestLevelCliTests(LevelBase):
    def test_test_level_command_writes_to_the_default_folder_under_the_repo(self):
        self.export_all()
        p = subprocess.run([sys.executable, str(KIT_PY), "test-level", "--repo", str(self.root)],
                           capture_output=True, text=True, encoding="utf-8")
        self.assertEqual(p.returncode, 0, p.stdout + p.stderr)
        self.assertTrue((self.root / "art/world/test/pixel-kit-test.ldtk").exists())
        self.assertTrue((self.root / "art/world/test/maps.json").exists())

    def test_error_exit_code_is_2(self):
        p = subprocess.run([sys.executable, str(KIT_PY), "test-level", "--repo", str(self.root)],
                           capture_output=True, text=True, encoding="utf-8")
        self.assertEqual(p.returncode, 2)


if __name__ == "__main__":
    unittest.main()
