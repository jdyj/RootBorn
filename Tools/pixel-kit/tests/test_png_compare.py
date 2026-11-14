"""커밋된 PNG는 바이트가 아니라 풀어낸 픽셀로 견준다 — zlib 판마다 같은 그림도 압축 결과가 달라서(2026-11-14 GitHub CI).
플랫폼 차이는 같은 픽셀을 다른 압축 수준으로 다시 저장한 사본으로 흉내 낸다."""
import io
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from pixelkit import export, room_export
from pixelkit.cli import load_kit

ROOT = Path(__file__).resolve().parents[3]
SHEET = "art/world/finishes/finishes.png"


def resave(src: Path, dst: Path, level: int, tweak=False) -> None:
    with Image.open(src) as im:
        img = im.convert("RGBA")
    if tweak:
        r, g, b, a = img.getpixel((0, 0))
        img.putpixel((0, 0), (255 - r, g, b, 255 if a == 0 else a))
    img.save(dst, format="PNG", compress_level=level)


class PngComparedByPixelsTests(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        self.addCleanup(lambda: __import__("shutil").rmtree(self.tmp, ignore_errors=True))
        self.committed = ROOT / SHEET
        self.data = self.committed.read_bytes()

    def test_same_pixels_with_other_compression_is_the_same_file(self):
        copy = self.tmp / "a.png"
        resave(self.committed, copy, 1)
        self.assertNotEqual(copy.read_bytes(), self.data)               # 바이트는 다르다(플랫폼 차이 흉내)
        self.assertFalse(export._differs(copy, self.data))

    def test_one_changed_pixel_is_different(self):
        copy = self.tmp / "b.png"
        resave(self.committed, copy, 9, tweak=True)
        self.assertTrue(export._differs(copy, self.data))

    def test_missing_or_broken_file_is_different(self):
        self.assertTrue(export._differs(self.tmp / "none.png", self.data))
        bad = self.tmp / "bad.png"
        bad.write_bytes(b"not a png")
        self.assertTrue(export._differs(bad, self.data))

    def test_text_files_are_still_compared_exactly(self):
        f = self.tmp / "x.json"
        f.write_bytes(b"{}\n")
        self.assertFalse(export._differs(f, b"{}\n"))
        self.assertTrue(export._differs(f, b"{}\r\n"))

    def test_export_finishes_dry_run_lists_nothing_for_a_recompressed_sheet(self):
        repo = self.tmp / "repo"
        for rel in (SHEET, "data/atlases/" + SHEET + ".atlas.json", "data/building/RoomFinishDefinition.json"):
            (repo / rel).parent.mkdir(parents=True, exist_ok=True)
            (repo / rel).write_bytes((ROOT / rel).read_bytes())
        resave(self.committed, repo / SHEET, 1)
        kit = load_kit(str(ROOT / "art" / "kit"))
        before = (repo / SHEET).read_bytes()
        self.assertEqual(room_export.export_finishes(repo, kit, dry_run=True), [])
        self.assertEqual(room_export.export_finishes(repo, kit), [])    # 쓸 것도 없다 — 파일을 다시 쓰지 않는다
        self.assertEqual((repo / SHEET).read_bytes(), before)


if __name__ == "__main__":
    unittest.main()
