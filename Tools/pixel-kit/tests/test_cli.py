import hashlib
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
KIT_PY = HERE.parent / "kit.py"
REPO = HERE.parents[2]
OK = str(HERE / "fixtures" / "kit_ok")
BAD = str(HERE / "fixtures" / "kit_bad")


def run(*args, cwd=None):
    p = subprocess.run([sys.executable, str(KIT_PY), *args], capture_output=True, text=True,
                       encoding="utf-8", cwd=cwd)
    return p.returncode, p.stdout, p.stderr


def tree(root):
    return sorted(str(p.relative_to(root)).replace("\\", "/") for p in Path(root).rglob("*") if p.is_file())


class CliTests(unittest.TestCase):
    def setUp(self):
        self._t = tempfile.TemporaryDirectory()
        self.out = self._t.name
        self.addCleanup(self._t.cleanup)

    def test_render_writes_base_and_states_with_exact_enlargement(self):
        code, out, err = run("render", "demo.tile.a", "--kit", OK, "--out", self.out, "--scale", "3")
        self.assertEqual(code, 0, out + err)
        self.assertEqual(tree(self.out), ["render/demo.tile.a.png", "render/demo.tile.a@warm.png",
                                          "render/demo.tile.a@warm_x3.png", "render/demo.tile.a_x3.png"])
        with Image.open(Path(self.out) / "render/demo.tile.a.png") as one:
            self.assertEqual(one.size, (32, 32))
        with Image.open(Path(self.out) / "render/demo.tile.a_x3.png") as big:
            self.assertEqual(big.size, (96, 96))

    def test_render_one_state_only(self):
        code, _, err = run("render", "demo.tile.a@warm", "--kit", OK, "--out", self.out)
        self.assertEqual(code, 0, err)
        self.assertEqual(tree(self.out), ["render/demo.tile.a@warm.png", "render/demo.tile.a@warm_x6.png"])

    def test_render_reports_lint_problems_with_exit_1(self):
        code, out, _ = run("render", "demo.tile.a", "--kit", BAD, "--out", self.out)
        self.assertEqual(code, 1)
        self.assertIn("demo.tile.a: too-many-colors", out)

    def test_render_scene_id_is_an_error_pointing_to_scene_command(self):
        code, _, err = run("render", "demo.scene.room", "--kit", OK, "--out", self.out)
        self.assertEqual(code, 2)
        self.assertIn("scene", err)

    def test_lint_clean_kit_is_exit_0(self):
        code, out, err = run("lint", "--kit", OK, "--out", self.out)
        self.assertEqual(code, 0, out + err)
        self.assertEqual(tree(self.out), [])

    def test_lint_bad_kit_prints_id_and_code_with_exit_1(self):
        code, out, _ = run("lint", "--kit", BAD, "--out", self.out)
        self.assertEqual(code, 1)
        self.assertRegex(out, r"demo\.tile\.a: too-many-colors ")
        self.assertRegex(out, r"demo\.tile\.a@warm: too-many-colors ")

    def test_lint_says_why_a_scene_or_a_part_with_state_is_skipped(self):
        code, out, err = run("lint", "demo.scene.room", "demo.dot@warm", "--kit", OK, "--out", self.out)
        self.assertEqual(code, 0, out + err)
        self.assertIn("demo.scene.room", err)
        self.assertIn("장면은 검사 대상이 아니다 — scene 명령으로 본다", err)
        self.assertIn("demo.dot@warm", err)
        self.assertIn("점 부품에는 상태가 없다", err)

    def test_unknown_id_is_exit_2_and_lists_known_ids(self):
        for cmd in ("lint", "render", "scene"):
            code, _, err = run(cmd, "nope.id", "--kit", OK, "--out", self.out)
            self.assertEqual(code, 2, cmd)
            self.assertIn("nope.id", err)
            self.assertIn("demo.tile.a", err)

    def test_unreadable_kit_is_exit_2(self):
        code, _, err = run("lint", "--kit", str(HERE / "fixtures" / "no_such_kit"), "--out", self.out)
        self.assertEqual(code, 2)
        self.assertTrue(err.strip())

    def test_only_writes_under_out_and_from_another_cwd(self):
        before = tree(REPO / "output") if (REPO / "output").exists() else None
        with tempfile.TemporaryDirectory() as cwd:
            for args in (("render", "demo.tile.a"), ("sheet", "demo"), ("scene", "demo.scene.room"), ("parts",)):
                code, out, err = run(*args, "--kit", OK, "--out", self.out, cwd=cwd)
                self.assertEqual(code, 0, out + err)
            self.assertEqual(os.listdir(cwd), [])
        after = tree(REPO / "output") if (REPO / "output").exists() else None
        self.assertEqual(before, after)
        files = tree(self.out)
        for expected in ("sheet/demo.png", "scene/demo.scene.room.png", "scene/demo.scene.room_x4.png", "parts.png"):
            self.assertIn(expected, files)

    def test_default_kit_resolves_from_script_location(self):
        # --kit 없이 다른 폴더에서 돌려도 저장소의 art/kit을 찾는다(없는 id라 2로 끝나며 아는 id가 나온다).
        with tempfile.TemporaryDirectory() as cwd:
            code, _, err = run("lint", "nope.id", "--out", self.out, cwd=cwd)
        self.assertEqual(code, 2)
        self.assertIn("bakery.", err)

    def test_refused_save_exits_2_but_keeps_saving_the_others(self):
        # 같은 이름의 폴더를 만들어 저장을 거절시킨다(다른 프로그램이 열어 둔 파일의 대용).
        os.makedirs(Path(self.out) / "render" / "demo.tile.a.png")
        code, _, err = run("render", "demo.tile.a", "--kit", OK, "--out", self.out)
        self.assertEqual(code, 2)
        self.assertIn("못 썼다:", err)
        self.assertIn("다른 프로그램에서 열려 있는지 확인", err)
        files = tree(self.out)
        self.assertIn("render/demo.tile.a@warm.png", files)
        self.assertIn("render/demo.tile.a_x6.png", files)
        self.assertNotIn("Traceback", err)

    def test_sheet_twice_is_byte_identical(self):
        digests = []
        for i in range(2):
            out = os.path.join(self.out, f"run{i}")
            code, o, err = run("sheet", "demo", "--kit", BAD, "--out", out)
            self.assertEqual(code, 0, o + err)
            digests.append(hashlib.sha256((Path(out) / "sheet/demo.png").read_bytes()).hexdigest())
        self.assertEqual(digests[0], digests[1])

    def test_other_commands_are_deterministic(self):
        sums = []
        for i in range(2):
            out = os.path.join(self.out, f"r{i}")
            for args in (("render", "demo.tile.a"), ("scene", "demo.scene.room"), ("parts",)):
                self.assertEqual(run(*args, "--kit", OK, "--out", out)[0], 0)
            sums.append({f: hashlib.sha256((Path(out) / f).read_bytes()).hexdigest() for f in tree(out)})
        self.assertEqual(sums[0], sums[1])

    def test_sheet_unknown_set_is_exit_2(self):
        code, _, err = run("sheet", "zzz", "--kit", OK, "--out", self.out)
        self.assertEqual(code, 2)
        self.assertIn("demo", err)

    def test_real_kit_lint_is_clean(self):
        code, out, err = run("lint", "--out", self.out)
        self.assertEqual(code, 0, out + err)


if __name__ == "__main__":
    unittest.main()
