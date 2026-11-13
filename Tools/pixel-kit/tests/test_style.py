import json
import re
import unittest
from pathlib import Path

from pixelkit.style import Style

# 저장소 루트: Tools/pixel-kit/tests/test_style.py 기준 세 단계 위
ROOT = Path(__file__).resolve().parents[3]
STYLE_PATH = ROOT / "art" / "kit" / "style.json"


class StyleTests(unittest.TestCase):
    def setUp(self):
        self.style = Style.load(str(STYLE_PATH))

    def test_cell_is_32(self):
        self.assertEqual(self.style.cell, 32)

    def test_color_returns_rgba_of_4(self):
        c = self.style.color("wood.warm.0")
        self.assertEqual(len(c), 4)
        self.assertEqual(c[3], 255)

    def test_unknown_key_raises_keyerror_with_key_name(self):
        with self.assertRaises(KeyError) as cm:
            self.style.color("nope.zero.9")
        self.assertIn("nope.zero.9", str(cm.exception))

    def test_brighter_step_is_next_step_of_same_ramp(self):
        c = self.style.color
        self.assertEqual(self.style.brighter(c("bread.1")), c("bread.2"))
        self.assertIsNone(self.style.brighter(c("bread.3")))           # 가장 밝은 단계
        self.assertIsNone(self.style.brighter(self.style.outline))     # 팔레트 단계가 아님

    def test_every_palette_value_is_hex(self):
        raw = json.loads(STYLE_PATH.read_text(encoding="utf-8"))
        for key, ramp in raw["palette"].items():
            self.assertTrue(ramp, key)
            for v in ramp:
                self.assertRegex(v, r"^#[0-9a-f]{6}$")

    def test_palette_contains_outline_and_all_colors(self):
        self.assertIn(self.style.outline, self.style.palette)
        self.assertIn(self.style.color("wood.warm.4"), self.style.palette)

    def test_shadow_and_max_colors(self):
        self.assertEqual(set(self.style.shadow), {"under", "front", "edge", "wall"})
        self.assertEqual(self.style.max_colors["furniture"], 24)

    def test_ramp_counts(self):
        raw = json.loads(STYLE_PATH.read_text(encoding="utf-8"))["palette"]
        self.assertEqual(len(raw["wood.warm"]), 5)
        self.assertEqual(len(raw["roof.slate"]), 5)
        self.assertEqual(len(raw["white"]), 1)
        self.assertEqual(len(raw["bread"]), 4)


if __name__ == "__main__":
    unittest.main()
