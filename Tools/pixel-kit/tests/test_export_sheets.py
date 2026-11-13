"""타일·건물·바깥 소품 묶음 시트 내보내기. 모두 임시 폴더의 가짜 저장소에서 한다."""
import copy
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from pixelkit import export
from pixelkit.cli import draw_and_check, load_kit

HERE = Path(__file__).resolve().parent
KIT_PY = HERE.parent / "kit.py"
REAL_KIT = HERE.parents[2] / "art" / "kit"


def slots_of(root: Path, sheet: str) -> dict:
    return json.loads((root / f"{sheet}.slots.json").read_text("utf-8"))


def pixels(root: Path, sheet: str, slot: dict):
    with Image.open(root / sheet) as im:
        return im.convert("RGBA").crop((slot["x"], slot["y"], slot["x"] + slot["w"], slot["y"] + slot["h"])).tobytes()


class Base(unittest.TestCase):
    def setUp(self):
        t = tempfile.TemporaryDirectory()
        self.addCleanup(t.cleanup)
        self.root = Path(t.name)
        self.kit = load_kit(str(REAL_KIT))
        self.floor = self.copy("bakery.tile.floor", "test.tile.floor")
        self.grass = self.copy("bakery.tile.grass", "test.tile.grass")
        self.wall = self.copy("bakery.tile.wall", "test.tile.wall")

    def copy(self, src, new_id):
        r = copy.deepcopy(self.kit.recipes[src])
        r["id"] = new_id
        return r

    def run_sheet(self, recipes, kind="tile", name="test"):
        plan = export.export_sheet(recipes, kind, name, self.kit, self.root)
        return plan, export.apply(plan, self.root)

    def snapshot(self):
        return {p.relative_to(self.root).as_posix(): (p.read_bytes(), p.stat().st_mtime_ns)
                for p in self.root.rglob("*") if p.is_file()}


class NewSheetTests(Base):
    def test_two_pictures_land_on_the_grid_without_overlap(self):
        plan, written = self.run_sheet([self.floor, self.grass])
        self.assertEqual(plan.problems, [])
        self.assertIn("art/tiles/test.png", written)
        table = slots_of(self.root, "art/tiles/test.png")
        self.assertEqual((table["cell"], table["width_cells"]), (32, 16))
        self.assertEqual(list(table["slots"]), ["test.tile.floor", "test.tile.grass"])
        rects = list(table["slots"].values())
        for s in rects:
            self.assertEqual((s["x"] % 32, s["y"] % 32, s["w"] % 32, s["h"] % 32), (0, 0, 0, 0))
        for i, a in enumerate(rects):
            for b in rects[i + 1:]:
                self.assertTrue(a["x"] + a["w"] <= b["x"] or b["x"] + b["w"] <= a["x"]
                                or a["y"] + a["h"] <= b["y"] or b["y"] + b["h"] <= a["y"])
        with Image.open(self.root / "art/tiles/test.png") as im:
            self.assertEqual(im.size[0], 512)
            self.assertEqual(im.size[1] % 32, 0)
            self.assertGreaterEqual(im.size[1], max(s["y"] + s["h"] for s in rects))

    def test_slot_pixels_are_the_drawn_picture(self):
        self.run_sheet([self.floor])
        slot = slots_of(self.root, "art/tiles/test.png")["slots"]["test.tile.floor"]
        cv, _ = draw_and_check(self.kit, self.floor, None)
        self.assertEqual(pixels(self.root, "art/tiles/test.png", slot), cv.to_image().convert("RGBA").tobytes())

    def test_tall_wall_is_one_slot_two_cells_high(self):
        self.run_sheet([self.wall])
        slot = slots_of(self.root, "art/tiles/test.png")["slots"]["test.tile.wall"]
        self.assertEqual((slot["w"], slot["h"]), (32, 64))

    def test_new_pictures_fill_a_row_left_to_right_then_wrap(self):
        recipes = [self.copy("bakery.tile.floor", f"test.tile.f{n:02d}") for n in range(17)]
        self.run_sheet(recipes)
        slots = slots_of(self.root, "art/tiles/test.png")["slots"]
        self.assertEqual([(s["x"], s["y"]) for s in list(slots.values())[:3]], [(0, 0), (32, 0), (64, 0)])
        self.assertEqual((slots["test.tile.f15"]["x"], slots["test.tile.f15"]["y"]), (480, 0))
        self.assertEqual((slots["test.tile.f16"]["x"], slots["test.tile.f16"]["y"]), (0, 32))

    def test_placement_follows_the_given_order(self):
        self.run_sheet([self.grass, self.floor])
        slots = slots_of(self.root, "art/tiles/test.png")["slots"]
        self.assertEqual(list(slots), ["test.tile.grass", "test.tile.floor"])
        self.assertEqual((slots["test.tile.grass"]["x"], slots["test.tile.floor"]["x"]), (0, 32))

    def test_too_wide_picture_is_a_problem_and_writes_nothing(self):
        wide = self.copy("bakery.tile.floor", "test.tile.wide")
        wide["art_cells"] = [17, 1]
        plan, written = self.run_sheet([wide])
        self.assertIn("export-too-wide", [p.code for p in plan.problems])
        self.assertEqual(written, [])
        self.assertEqual(self.snapshot(), {})

    def test_kind_that_does_not_belong_to_the_sheet_is_a_problem(self):
        plan, _ = self.run_sheet([self.floor], kind="building")
        self.assertIn("export-kind-mismatch", [p.code for p in plan.problems])

    def test_duplicate_id_in_one_run_is_placed_once(self):
        self.run_sheet([self.floor, copy.deepcopy(self.floor)])
        self.assertEqual(list(slots_of(self.root, "art/tiles/test.png")["slots"]), ["test.tile.floor"])

    def test_lint_problem_blocks_every_file(self):
        from pixelkit import export as ex
        from pixelkit.lint import Problem
        real = ex.draw_and_check

        def fake(kit, recipe, state):
            cv, found = real(kit, recipe, state)
            if recipe["id"] == "test.tile.grass":
                found = found + [Problem("orphan-pixel", "가짜")]
            return cv, found
        ex.draw_and_check = fake
        try:
            plan, written = self.run_sheet([self.floor, self.grass])
        finally:
            ex.draw_and_check = real
        self.assertIn("orphan-pixel", [p.code for p in plan.problems])
        self.assertTrue(any("test.tile.grass" in p.message for p in plan.problems))
        self.assertEqual(written, [])
        self.assertEqual(self.snapshot(), {})


class AppendOnlyTests(Base):
    def test_same_run_again_changes_nothing(self):
        self.run_sheet([self.floor, self.grass])
        before = self.snapshot()
        plan, written = self.run_sheet([self.floor, self.grass])
        self.assertEqual((plan.problems, written), ([], []))
        self.assertEqual(export.pending_writes(plan, self.root), [])
        self.assertEqual(self.snapshot(), before)

    def test_third_picture_keeps_the_first_two_slots_and_pixels(self):
        self.run_sheet([self.floor, self.grass])
        sheet = "art/tiles/test.png"
        first = slots_of(self.root, sheet)["slots"]
        shots = {k: pixels(self.root, sheet, s) for k, s in first.items()}
        self.run_sheet([self.wall])
        after = slots_of(self.root, sheet)["slots"]
        for k, s in first.items():
            self.assertEqual(after[k], s)
            self.assertEqual(pixels(self.root, sheet, after[k]), shots[k])
        w = after["test.tile.wall"]
        self.assertGreaterEqual(w["y"], max(s["y"] + s["h"] for s in first.values()))

    def test_appended_after_last_used_row(self):
        self.run_sheet([self.wall, self.floor])    # 줄 높이 64
        self.run_sheet([self.grass])
        slots = slots_of(self.root, "art/tiles/test.png")["slots"]
        self.assertEqual((slots["test.tile.grass"]["x"], slots["test.tile.grass"]["y"]), (0, 64))

    def test_redrawing_a_known_id_keeps_its_slot_and_leaves_other_pixels_alone(self):
        self.run_sheet([self.floor, self.grass])
        sheet = "art/tiles/test.png"
        before = slots_of(self.root, sheet)["slots"]
        other = pixels(self.root, sheet, before["test.tile.grass"])
        old = pixels(self.root, sheet, before["test.tile.floor"])
        changed = copy.deepcopy(self.floor)
        changed["seed"] = 3
        plan, written = self.run_sheet([changed])
        self.assertEqual(plan.problems, [])
        self.assertIn(sheet, written)
        self.assertNotIn(sheet + ".slots.json", written)
        after = slots_of(self.root, sheet)["slots"]
        self.assertEqual(after, before)
        self.assertEqual(pixels(self.root, sheet, after["test.tile.grass"]), other)
        self.assertNotEqual(pixels(self.root, sheet, after["test.tile.floor"]), old)

    def test_size_change_is_refused_and_nothing_is_written(self):
        self.run_sheet([self.floor, self.grass])
        before = self.snapshot()
        bigger = copy.deepcopy(self.floor)
        bigger["art_cells"] = [2, 2]
        plan, written = self.run_sheet([bigger, self.wall])
        self.assertEqual([p.code for p in plan.problems], ["export-slot-size-changed"])
        self.assertIn("test.tile.floor", plan.problems[0].message)
        self.assertEqual(written, [])
        self.assertEqual(self.snapshot(), before)

    def test_slot_table_without_png_is_a_problem(self):
        self.run_sheet([self.floor])
        (self.root / "art/tiles/test.png").unlink()
        plan, written = self.run_sheet([self.grass])
        self.assertIn("export-sheet-inconsistent", [p.code for p in plan.problems])
        self.assertEqual(written, [])

    def test_unreadable_slot_table_is_a_problem(self):
        self.run_sheet([self.floor])
        (self.root / "art/tiles/test.png.slots.json").write_text("{깨짐", encoding="utf-8")
        plan, _ = self.run_sheet([self.grass])
        self.assertIn("export-slots-unreadable", [p.code for p in plan.problems])


class AtlasTests(Base):
    def atlas(self, sheet):
        return json.loads((self.root / f"data/atlases/{sheet}.atlas.json").read_text("utf-8"))

    def test_tile_atlas_matches_png_and_slots_with_centre_pivot(self):
        self.run_sheet([self.floor, self.wall])
        sheet = "art/tiles/test.png"
        a = self.atlas(sheet)
        with Image.open(self.root / sheet) as im:
            self.assertEqual((a["width"], a["height"]), im.size)
        self.assertEqual((a["image"], a["pixelsPerUnit"], a["origin"]), (sheet, 32, "top-left"))
        slots = slots_of(self.root, sheet)["slots"]
        self.assertEqual(list(a["sprites"]), list(slots))
        for name, c in a["sprites"].items():
            s = slots[name]
            self.assertEqual({k: c[k] for k in "xywh"}, s)
            self.assertLessEqual(c["x"] + c["w"], a["width"])
            self.assertLessEqual(c["y"] + c["h"], a["height"])
            self.assertEqual(c["pivot"], {"x": 0.5, "y": 0.5})

    def test_world_sheet_holds_building_and_prop_with_foot_pivot(self):
        b = self.copy("bakery.exterior.flat", "test.exterior.flat")
        p = self.copy("bakery.menu-board", "test.menu-board")
        plan, _ = self.run_sheet([b, p], kind="building")
        self.assertEqual(plan.problems, [])
        sheet = "art/world/test.png"
        a = self.atlas(sheet)
        self.assertEqual(list(a["sprites"]), ["test.exterior.flat", "test.menu-board"])
        for c in a["sprites"].values():
            self.assertEqual(c["pivot"], {"x": 0.5, "y": 1.0})
        self.assertEqual(slots_of(self.root, sheet)["slots"]["test.exterior.flat"]["h"], 192)

    def test_atlas_lists_slots_from_earlier_runs_too(self):
        self.run_sheet([self.floor])
        self.run_sheet([self.grass])
        self.assertEqual(list(self.atlas("art/tiles/test.png")["sprites"]), ["test.tile.floor", "test.tile.grass"])


def cli_run(root, kit_dir, args, command="export"):
    p = subprocess.run([sys.executable, str(KIT_PY), command, *args, "--kit", str(kit_dir), "--repo", str(root)],
                       capture_output=True, text=True, encoding="utf-8")
    return p.returncode, p.stdout, p.stderr


class SheetCliTests(unittest.TestCase):
    def setUp(self):
        t = tempfile.TemporaryDirectory()
        self.addCleanup(t.cleanup)
        self.root = Path(t.name) / "repo"
        self.root.mkdir()
        self.kit = REAL_KIT

    def files(self):
        return sorted(p.relative_to(self.root).as_posix() for p in self.root.rglob("*") if p.is_file())

    def test_export_by_id_groups_by_set_and_sheet_and_is_idempotent(self):
        ids = ["bakery.tile.floor", "bakery.tile.grass", "bakery.exterior.flat", "bakery.menu-board"]
        code, out, err = cli_run(self.root, self.kit, ids)
        self.assertEqual(code, 0, out + err)
        self.assertEqual(self.files(), [
            "art/tiles/bakery.png", "art/tiles/bakery.png.slots.json",
            "art/world/bakery.png", "art/world/bakery.png.slots.json",
            "data/atlases/art/tiles/bakery.png.atlas.json", "data/atlases/art/world/bakery.png.atlas.json"])
        snap = {f: (self.root / f).stat().st_mtime_ns for f in self.files()}
        code, out, _ = cli_run(self.root, self.kit, ids)
        self.assertEqual(code, 0)
        self.assertIn("바꿀 것 없음", out)
        self.assertEqual({f: (self.root / f).stat().st_mtime_ns for f in self.files()}, snap)

    def test_ids_are_placed_in_argument_order(self):
        cli_run(self.root, self.kit, ["bakery.tile.sidewalk", "bakery.tile.floor"])
        slots = json.loads((self.root / "art/tiles/bakery.png.slots.json").read_text("utf-8"))["slots"]
        self.assertEqual(list(slots), ["bakery.tile.sidewalk", "bakery.tile.floor"])

    def test_dry_run_writes_nothing(self):
        code, out, _ = cli_run(self.root, self.kit, ["bakery.tile.floor", "--dry-run"])
        self.assertEqual(code, 0)
        self.assertIn("art/tiles/bakery.png", out)
        self.assertEqual(self.files(), [])

    def test_scene_is_not_exportable(self):
        code, _, err = cli_run(self.root, self.kit, ["bakery.scene.interior"])
        self.assertEqual(code, 2)
        self.assertIn("아직 없다", err)


if __name__ == "__main__":
    unittest.main()
