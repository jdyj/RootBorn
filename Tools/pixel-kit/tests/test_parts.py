import json
import shutil
import tempfile
import unittest
from pathlib import Path

from pixelkit.parts import PartError, load_part_allowances, load_parts
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE_PATH = ROOT / "art" / "kit" / "style.json"
PARTS_DIR = ROOT / "art" / "kit" / "parts"
FIXTURES = Path(__file__).resolve().parent / "fixtures"

BREAD = [
    "bread.loaf", "bread.baguette", "bread.croissant", "bread.donut",
    "bread.muffin", "bread.cake", "bread.sandwich", "bread.pretzel",
    "bread.bun", "bread.toast", "bread.roll", "bread.pie",
]
PROPS = ["prop.basket", "prop.plant", "prop.wall-lamp", "prop.register", "prop.clock"]


def _mask(cv):
    x0, y0, x1, y1 = cv.bbox()
    return tuple(tuple(bool(cv.get(x, y)[3]) for x in range(x0, x1)) for y in range(y0, y1))


class _TempDir:
    """고정 파일을 임시 폴더에 옮겨 load_parts에 폴더로 넘긴다."""

    def __enter__(self):
        self.path = tempfile.mkdtemp()
        return Path(self.path)

    def __exit__(self, *exc):
        shutil.rmtree(self.path, ignore_errors=True)


class RealPartsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.style = Style.load(str(STYLE_PATH))
        cls.parts = load_parts(str(PARTS_DIR), cls.style)

    def test_all_17_ids_exist(self):
        for pid in BREAD + PROPS:
            self.assertIn(pid, self.parts)

    def test_all_colors_are_in_palette(self):
        for pid, cv in self.parts.items():
            extra = cv.colors() - self.style.palette
            self.assertFalse(extra, f"{pid}: 팔레트 밖 색 {extra}")

    def test_outline_applied_with_1px_margin(self):
        for pid, cv in self.parts.items():
            self.assertEqual(cv.bbox(), (0, 0, cv.w, cv.h), pid)
            self.assertIn(self.style.outline, cv.colors(), pid)
            # 사방 테두리 줄에는 외곽선 색이나 투명만 있다.
            for x in range(cv.w):
                for y in (0, cv.h - 1):
                    self.assertIn(cv.get(x, y), (self.style.outline, (0, 0, 0, 0)), pid)
            for y in range(cv.h):
                for x in (0, cv.w - 1):
                    self.assertIn(cv.get(x, y), (self.style.outline, (0, 0, 0, 0)), pid)

    def test_bread_silhouettes_differ(self):
        seen = {}
        for pid in BREAD:
            m = _mask(self.parts[pid])
            self.assertNotIn(m, seen, f"{pid} 실루엣이 {seen.get(m)}와 같다")
            seen[m] = pid

    def test_bread_size_limits(self):
        for pid in BREAD:
            cv = self.parts[pid]
            # 외곽선 1px을 뺀 내용 크기
            self.assertLessEqual(cv.w - 2, 16, pid)
            self.assertLessEqual(cv.h - 2, 12, pid)

    def test_new_bread_width_9_to_12(self):
        for pid in ("bread.bun", "bread.toast", "bread.roll", "bread.pie"):
            w = self.parts[pid].w - 2
            self.assertTrue(9 <= w <= 12, f"{pid} 폭 {w}")

    def test_load_is_deterministic(self):
        again = load_parts(str(PARTS_DIR), self.style)
        for pid, cv in self.parts.items():
            self.assertEqual(cv.to_image().tobytes(), again[pid].to_image().tobytes(), pid)


class FixtureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.style = Style.load(str(STYLE_PATH))

    def test_unknown_palette_key_names_part_and_key(self):
        with _TempDir() as d:
            shutil.copy(FIXTURES / "parts_bad_key.json", d / "bad.json")
            with self.assertRaises(PartError) as cm:
                load_parts(str(d), self.style)
        msg = str(cm.exception)
        self.assertIn("test.bad", msg)
        self.assertIn("nope.zero.9", msg)
        self.assertIn("bad.json", msg)

    def test_ragged_rows_pad_short_rows_with_clear(self):
        with _TempDir() as d:
            shutil.copy(FIXTURES / "parts_ragged.json", d / "ragged.json")
            parts = load_parts(str(d), self.style)
        cv = parts["test.ragged"]
        self.assertEqual((cv.w, cv.h), (3 + 2, 3 + 2))
        a = self.style.color("bread.1")
        # 안쪽(1px 여유 뒤) 내용: "A..", "AAA", "AA." — 채운 빈 칸은 색이 아니라 외곽선이 된다.
        self.assertEqual(cv.get(1, 1), a)
        self.assertEqual(cv.get(3, 1), self.style.outline)
        self.assertEqual(cv.get(3, 2), a)
        self.assertEqual(cv.get(3, 3), self.style.outline)  # 짧은 줄 끝 옆은 외곽선

    def test_duplicate_id_raises(self):
        part = {"id": "test.same", "rows": ["A"], "keys": {"A": "bread.1"}}
        with _TempDir() as d:
            (d / "a.json").write_text(json.dumps([part]), encoding="utf-8")
            (d / "b.json").write_text(json.dumps([part]), encoding="utf-8")
            with self.assertRaises(PartError) as cm:
                load_parts(str(d), self.style)
        self.assertIn("test.same", str(cm.exception))

    def test_char_without_key_raises(self):
        part = {"id": "test.nokey", "rows": ["AZ"], "keys": {"A": "bread.1"}}
        with _TempDir() as d:
            (d / "a.json").write_text(json.dumps([part]), encoding="utf-8")
            with self.assertRaises(PartError) as cm:
                load_parts(str(d), self.style)
        self.assertIn("test.nokey", str(cm.exception))
        self.assertIn("Z", str(cm.exception))


class AllowanceTests(unittest.TestCase):
    def test_maps_allow_and_defaults_to_empty(self):
        a = {"id": "t.a", "rows": ["A"], "keys": {"A": "bread.1"}, "allow": ["orphan-pixel"]}
        b = {"id": "t.b", "rows": ["A"], "keys": {"A": "bread.1"}}
        with _TempDir() as d:
            (d / "x.json").write_text(json.dumps([a, b]), encoding="utf-8")
            got = load_part_allowances(str(d))
        self.assertEqual(got["t.a"], frozenset({"orphan-pixel"}))
        self.assertEqual(got["t.b"], frozenset())

    def test_malformed_entries_raise_part_error_naming_file(self):
        for bad in (["oops"], [{"rows": ["A"]}], {"id": "x"}):
            with _TempDir() as d:
                (d / "bad.json").write_text(json.dumps(bad), encoding="utf-8")
                with self.assertRaises(PartError) as cm:
                    load_part_allowances(str(d))
            self.assertIn("bad.json", str(cm.exception))


if __name__ == "__main__":
    unittest.main()
