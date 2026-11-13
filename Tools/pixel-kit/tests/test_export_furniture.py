"""가구 내보내기. 모두 임시 폴더의 가짜 저장소에서 한다(실제 data/·art/props에 쓰지 않는다)."""
import copy
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
from pathlib import Path

from PIL import Image

from pixelkit import export
from pixelkit.cli import load_kit

HERE = Path(__file__).resolve().parent
KIT_PY = HERE.parent / "kit.py"
REAL_KIT = HERE.parents[2] / "art" / "kit"


def entity(fid, cells, sprite, name, extra_parts=0):
    part = {"localCell": {"x": 0, "y": 0}, "tile": {"sprite": sprite, "name": name}}
    return {"type": "FurnitureDefinition", "id": fid, "asset": f"Assets/{fid}.asset", "stableId": fid,
            "displayName": f"한글 {fid} {{괄호}} \"따옴표\"", "category": "Storage",
            "allowedSurfaceIds": ["house"], "tileParts": [part] * (1 + extra_parts),
            "footprintCells": [{"x": x, "y": 0} for x in range(cells)],
            "blocksMovement": 1, "placementPreference": 0, "unlockToken": ""}


def defs_text(newline="\n"):
    data = [entity("test.before", 1, "Old/a.png", "a"),
            entity("test.shelf", 2, "Old/shelf.png", "shelf"),
            entity("test.after", 3, "Old/c.png", "c")]
    return json.dumps(data, ensure_ascii=False, indent=2).replace("\n", newline) + newline


def lines_changed(before: bytes, after: bytes) -> int:
    a, b = before.split(b"\n"), after.split(b"\n")
    return sum(1 for x, y in zip(a, b) if x != y) if len(a) == len(b) else -1


class Base(unittest.TestCase):
    newline = "\r\n"

    def setUp(self):
        t = tempfile.TemporaryDirectory()
        self.addCleanup(t.cleanup)
        self.root = Path(t.name)
        self.defs = self.root / export.FURNITURE_DEFS
        self.defs.parent.mkdir(parents=True)
        self.defs.write_bytes(defs_text(self.newline).encode("utf-8"))
        self.kit = load_kit(str(REAL_KIT))
        self.recipe = copy.deepcopy(self.kit.recipes["bakery.shelf"])
        self.recipe["id"] = "test.shelf"
        self.recipe["game"] = {"replaces": "test.shelf"}

    def run_export(self, recipe=None):
        plan = export.export_furniture(recipe or self.recipe, self.kit, self.root)
        return plan, export.apply(plan, self.root)

    def snapshot(self):
        return {p.relative_to(self.root).as_posix(): (p.read_bytes(), p.stat().st_mtime_ns)
                for p in self.root.rglob("*") if p.is_file()}


class ExportFurnitureTests(Base):
    def test_export_writes_png_atlas_and_replaces_two_strings_only(self):
        before = self.defs.read_bytes()
        plan, written = self.run_export()
        self.assertEqual(plan.problems, [])
        after = self.defs.read_bytes()
        self.assertEqual(lines_changed(before, after), 2)
        self.assertIn(b"\r\n", after)
        self.assertEqual(after.count(b"\n"), before.count(b"\n"))
        self.assertEqual(written[-1], export.FURNITURE_DEFS)
        png_path = self.root / "art/props/test/test.shelf.png"
        atlas = json.loads((self.root / "data/atlases/art/props/test/test.shelf.png.atlas.json").read_text("utf-8"))
        self.assertEqual((atlas["pixelsPerUnit"], atlas["origin"]), (32, "top-left"))
        with Image.open(png_path) as im:
            self.assertEqual((atlas["width"], atlas["height"]), im.size)
        self.assertEqual(atlas["image"], "art/props/test/test.shelf.png")
        self.assertEqual(atlas["sprites"]["test.shelf"]["pivot"], {"x": 0.25, "y": 5 / 6})
        parsed = json.loads(after.decode("utf-8"))
        self.assertEqual(parsed[1]["tileParts"][0]["tile"],
                         {"sprite": "art/props/test/test.shelf.png", "name": "test.shelf"})
        self.assertEqual(parsed[0], json.loads(before.decode("utf-8"))[0])
        self.assertEqual(parsed[2], json.loads(before.decode("utf-8"))[2])

    def test_states_are_extra_cells_in_the_same_png(self):
        self.run_export()
        atlas = json.loads((self.root / "data/atlases/art/props/test/test.shelf.png.atlas.json").read_text("utf-8"))
        self.assertEqual(list(atlas["sprites"]), ["test.shelf", "test.shelf@empty", "test.shelf@sweets"])
        self.assertEqual(atlas["sprites"]["test.shelf@empty"]["x"], 64)
        self.assertEqual(atlas["sprites"]["test.shelf@sweets"]["x"], 128)
        self.assertEqual((atlas["width"], atlas["height"]), (64 * 3, 96))
        self.assertEqual(atlas["sprites"]["test.shelf@sweets"]["w"], 64)

    def test_second_export_changes_nothing(self):
        self.run_export()
        snap = self.snapshot()
        plan, written = self.run_export()
        self.assertEqual(written, [])
        self.assertEqual(self.snapshot(), snap)

    def test_unknown_furniture_or_footprint_mismatch_writes_nothing(self):
        snap = self.snapshot()
        r1 = copy.deepcopy(self.recipe)
        r1["game"] = {"replaces": "test.nope"}
        plan, written = self.run_export(r1)
        self.assertEqual([p.code for p in plan.problems], ["export-unknown-furniture"])
        r2 = copy.deepcopy(self.recipe)
        r2["game"] = {"replaces": "test.after"}      # 3칸이고 명세는 2칸
        plan, written = self.run_export(r2)
        self.assertEqual([p.code for p in plan.problems], ["export-footprint-mismatch"])
        self.assertEqual((written, self.snapshot()), ([], snap))

    def test_unsupported_furniture_codes(self):
        data = json.loads(self.defs.read_text("utf-8"))
        data[1]["tileParts"].append(copy.deepcopy(data[1]["tileParts"][0]))
        self.defs.write_text(json.dumps(data, indent=2), encoding="utf-8")
        plan, _ = self.run_export()
        self.assertEqual([p.code for p in plan.problems], ["export-unsupported-furniture"])
        r = copy.deepcopy(self.recipe)
        r["cells"] = [2, 2]
        plan, _ = self.run_export(r)
        self.assertEqual([p.code for p in plan.problems], ["export-unsupported-furniture"])

    def test_state_with_different_canvas_size_is_a_problem_and_writes_nothing(self):
        r = copy.deepcopy(self.recipe)
        r["states"]["empty"] = dict(r["states"]["empty"], art_cells=[3, 3])
        snap = self.snapshot()
        plan, written = self.run_export(r)
        self.assertIn("export-state-size-mismatch", [p.code for p in plan.problems])
        self.assertEqual((written, self.snapshot()), ([], snap))

    def test_definition_without_tile_or_footprint_xy_is_a_problem_not_a_traceback(self):
        snap = self.snapshot()
        for mutate in (lambda d: d["tileParts"][0].pop("tile"),
                       lambda d: d["footprintCells"][0].pop("x"),
                       lambda d: d["footprintCells"][1].pop("y")):
            data = json.loads(self.defs.read_text("utf-8"))
            mutate(data[1])
            self.defs.write_text(json.dumps(data, indent=2), encoding="utf-8")
            plan, _ = self.run_export()
            self.assertEqual([p.code for p in plan.problems], ["export-unsupported-furniture"])
        self.assertEqual(sorted(k for k in self.snapshot() if k != export.FURNITURE_DEFS),
                         sorted(k for k in snap if k != export.FURNITURE_DEFS))

    def test_lint_problem_blocks_every_file_of_the_run(self):
        good = copy.deepcopy(self.recipe)
        bad = copy.deepcopy(self.recipe)
        bad["id"] = "test.shelf2"
        bad["game"] = {"replaces": "test.shelf"}
        strict = load_kit(str(REAL_KIT))
        strict.style.max_colors["furniture"] = 2        # 색 수 한도를 넘겨 lint 문제를 낸다
        snap = self.snapshot()
        plans = [export.export_furniture(good, self.kit, self.root),
                 export.export_furniture(bad, strict, self.root)]
        self.assertEqual(plans[0].problems, [])
        self.assertTrue(plans[0].files)
        self.assertIn("too-many-colors", [p.code for p in plans[1].problems])
        plan = export.merge_plans(plans)
        self.assertTrue(plan.problems)
        self.assertEqual(plan.files, {})
        self.assertEqual((export.apply(plan, self.root), self.snapshot()), ([], snap))

    def test_without_replaces_definitions_are_untouched(self):
        del self.recipe["game"]
        before = self.snapshot()[export.FURNITURE_DEFS]
        plan, written = self.run_export()
        self.assertEqual(plan.replace, [])
        self.assertNotIn(export.FURNITURE_DEFS, written)
        self.assertEqual(self.snapshot()[export.FURNITURE_DEFS], before)
        self.assertEqual(len(written), 2)

    def test_failed_verification_writes_nothing(self):
        # tileParts[0]에 tile 밖의 "name"이 먼저 있으면 잘못된 값이 바뀌므로 검증이 막아야 한다.
        data = json.loads(self.defs.read_text("utf-8"))
        part = data[1]["tileParts"][0]
        data[1]["tileParts"][0] = {"name": "label", **part}
        self.defs.write_text(json.dumps(data, indent=2), encoding="utf-8")
        snap = self.snapshot()
        plan, written = self.run_export()
        self.assertEqual([p.code for p in plan.problems], ["export-replace-failed"])
        self.assertEqual((written, self.snapshot()), ([], snap))

    def test_definitions_not_utf8_is_a_clear_problem(self):
        self.defs.write_bytes(bytes([0x5b, 0xff, 0xfe, 0x5d]))
        snap = self.snapshot()
        plan, written = self.run_export()
        self.assertEqual([p.code for p in plan.problems], ["export-unreadable-definitions"])
        self.assertIn(export.FURNITURE_DEFS, plan.problems[0].message)
        self.assertEqual((written, self.snapshot()), ([], snap))

    # --- 고침 1차 ---
    def tmp_files(self):
        return sorted(p.relative_to(self.root).as_posix() for p in self.root.rglob("*.tmp"))

    def test_staging_failure_of_definitions_leaves_every_target_untouched(self):
        before = self.defs.read_bytes()
        plan = export.export_furniture(self.recipe, self.kit, self.root)
        real = export._write_staged

        def failing(path, data):
            if Path(path).name.startswith("FurnitureDefinition"):
                raise OSError("디스크가 가득 찼다")
            real(path, data)
        with mock.patch.object(export, "_write_staged", failing):
            with self.assertRaises(OSError):
                export.apply(plan, self.root)
        self.assertEqual(self.defs.read_bytes(), before)
        for sub in ("art", "data/atlases"):      # 빈 폴더는 남아도 파일은 없어야 한다
            self.assertEqual([p for p in (self.root / sub).rglob("*") if p.is_file()], [])
        self.assertEqual(self.tmp_files(), [])

    def test_replace_failure_of_definitions_names_what_was_replaced(self):
        before = self.defs.read_bytes()
        plan = export.export_furniture(self.recipe, self.kit, self.root)
        real = os.replace

        def failing(src, dst):
            if Path(dst).name.startswith("FurnitureDefinition"):
                raise OSError("잠겨 있다")
            real(src, dst)
        with mock.patch.object(export, "_replace", failing):
            with self.assertRaises(export.ExportWriteError) as cm:
                export.apply(plan, self.root)
        self.assertEqual(self.defs.read_bytes(), before)
        self.assertEqual(cm.exception.replaced,
                         ["art/props/test/test.shelf.png", "data/atlases/art/props/test/test.shelf.png.atlas.json"])
        self.assertIn("art/props/test/test.shelf.png", str(cm.exception))
        self.assertEqual(self.tmp_files(), [])

    def test_two_recipes_with_the_same_target_are_refused(self):
        other = copy.deepcopy(self.recipe)
        other["id"] = "test.shelf2"
        plans = [export.export_furniture(self.recipe, self.kit, self.root),
                 export.export_furniture(other, self.kit, self.root)]
        snap = self.snapshot()
        plan = export.merge_plans(plans)
        self.assertEqual([p.code for p in plan.problems], ["export-duplicate-target"])
        self.assertIn("test.shelf", plan.problems[0].message)
        self.assertIn("test.shelf2", plan.problems[0].message)
        self.assertEqual((export.apply(plan, self.root), self.snapshot()), ([], snap))

    def test_wider_art_than_footprint_is_unsupported_with_replaces(self):
        self.recipe["art_cells"] = [3, 3]
        plan, written = self.run_export()
        self.assertEqual([p.code for p in plan.problems], ["export-unsupported-furniture"])
        self.assertIn("art_cells", plan.problems[0].message)
        self.assertEqual(written, [])

    def test_unreadable_definitions_file_is_reported_as_such(self):
        self.defs.unlink()
        self.defs.mkdir()
        plan, written = self.run_export()
        self.assertEqual([p.code for p in plan.problems], ["export-unreadable-definitions"])
        self.assertEqual(written, [])

    def test_malformed_definitions_elsewhere_is_a_problem_not_a_traceback(self):
        text = self.defs.read_text("utf-8").rstrip()
        self.defs.write_text(text[:-1] + ", oops ]", encoding="utf-8")
        snap = self.snapshot()
        plan, written = self.run_export()
        self.assertEqual([p.code for p in plan.problems], ["export-unreadable-definitions"])
        self.assertIn(export.FURNITURE_DEFS, plan.problems[0].message)
        self.assertEqual((written, self.snapshot()), ([], snap))

    def test_lf_file_and_bom_are_preserved(self):
        self.defs.write_bytes(b"\xef\xbb\xbf" + defs_text("\n").encode("utf-8").rstrip(b"\n"))
        self.run_export()
        after = self.defs.read_bytes()
        self.assertTrue(after.startswith(b"\xef\xbb\xbf"))
        self.assertFalse(after.endswith(b"\n"))
        self.assertNotIn(b"\r", after)


def cli_run(root, kit_dir, args):
    p = subprocess.run([sys.executable, str(KIT_PY), "export", *args, "--kit", kit_dir, "--repo", str(root)],
                       capture_output=True, text=True, encoding="utf-8")
    return p.returncode, p.stdout, p.stderr


class ExportCliTests(unittest.TestCase):
    def setUp(self):
        t = tempfile.TemporaryDirectory()
        self.addCleanup(t.cleanup)
        self.root = Path(t.name) / "repo"
        self.kit = Path(t.name) / "kit"
        shutil.copytree(REAL_KIT, self.kit)
        (self.kit / "recipes" / "test").mkdir()
        shelf = json.loads((REAL_KIT / "recipes/bakery/furniture.json").read_text("utf-8"))[0]
        shelf["id"] = "test.shelf"
        shelf["game"] = {"replaces": "test.shelf"}
        (self.kit / "recipes/test/furniture.json").write_text(json.dumps([shelf]), encoding="utf-8")
        defs = self.root / export.FURNITURE_DEFS
        defs.parent.mkdir(parents=True)
        defs.write_bytes(defs_text("\n").encode("utf-8"))

    def files(self):
        return sorted(p.relative_to(self.root).as_posix() for p in self.root.rglob("*") if p.is_file())

    def test_dry_run_writes_nothing_and_lists_the_plan(self):
        before = self.files()
        code, out, err = cli_run(self.root, self.kit, ["test.shelf", "--dry-run"])
        self.assertEqual(code, 0, out + err)
        self.assertEqual(self.files(), before)
        self.assertIn("art/props/test/test.shelf.png", out)
        self.assertIn("test.shelf", out)
        self.assertIn("art/props/test/test.shelf.png.atlas.json", out)

    def test_real_run_then_exit_codes(self):
        code, out, err = cli_run(self.root, self.kit, ["test.shelf"])
        self.assertEqual(code, 0, out + err)
        self.assertIn("art/props/test/test.shelf.png", self.files())
        snap = {f: (self.root / f).stat().st_mtime_ns for f in self.files()}
        code, out, err = cli_run(self.root, self.kit, ["test.shelf"])
        self.assertEqual(code, 0, out + err)
        self.assertIn("바꿀 것 없음", out)
        self.assertEqual({f: (self.root / f).stat().st_mtime_ns for f in self.files()}, snap)

    def test_options_may_come_before_the_ids(self):
        code, out, err = cli_run(self.root, self.kit, ["--dry-run", "test.shelf"])
        self.assertEqual(code, 0, out + err)
        self.assertIn("art/props/test/test.shelf.png", out)

    def test_other_kinds_are_not_yet_supported(self):
        code, _, err = cli_run(self.root, self.kit, ["bakery.scene.interior"])
        self.assertEqual(code, 2)
        self.assertIn("아직 없다", err)

    def test_output_prints_replacement_only_when_it_will_change_and_no_change_once(self):
        code, out, _ = cli_run(self.root, self.kit, ["test.shelf", "--dry-run"])
        self.assertIn("가구 정의 test.shelf", out)
        self.assertEqual(out.count("바꿀 것 없음"), 0)
        code, out, _ = cli_run(self.root, self.kit, ["test.shelf"])
        self.assertIn("가구 정의 test.shelf", out)
        for extra in ([], ["--dry-run"]):
            code, out, _ = cli_run(self.root, self.kit, ["test.shelf", *extra])
            self.assertEqual(code, 0)
            self.assertNotIn("가구 정의 test.shelf", out)
            self.assertEqual(out.count("바꿀 것 없음"), 1, out)

    def test_write_failure_midway_exits_2_and_names_replaced_files(self):
        import argparse
        import contextlib
        import io
        from pixelkit import cli
        args = argparse.Namespace(command="export", ids=["test.shelf"], kit=str(self.kit), out=str(self.root),
                                  scale=None, repo=str(self.root), dry_run=False)
        real = os.replace

        def failing(src, dst):
            if Path(dst).name.startswith("FurnitureDefinition"):
                raise OSError("잠겨 있다")
            real(src, dst)
        err = io.StringIO()
        with mock.patch.object(export, "_replace", failing), contextlib.redirect_stderr(err),                 contextlib.redirect_stdout(io.StringIO()):
            code = cli.run(args)
        self.assertEqual(code, 2)
        self.assertIn("art/props/test/test.shelf.png", err.getvalue())
        self.assertEqual(sorted(self.root.rglob("*.tmp")), [])

    def test_problem_exits_1_and_writes_nothing(self):
        data = json.loads((self.kit / "recipes/test/furniture.json").read_text("utf-8"))
        data[0]["game"] = {"replaces": "test.nope"}
        (self.kit / "recipes/test/furniture.json").write_text(json.dumps(data), encoding="utf-8")
        before = self.files()
        code, out, _ = cli_run(self.root, self.kit, ["test.shelf"])
        self.assertEqual(code, 1)
        self.assertIn("export-unknown-furniture", out)
        self.assertEqual(self.files(), before)


if __name__ == "__main__":
    unittest.main()
