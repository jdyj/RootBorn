"""방 만들기 — 꾸밈 조각 시트 내보내기(room export-finishes).

가장 중요한 것: 내보낸 PNG와 좌표표에서 조각을 다시 잘라 참조 조합기(test_room_vectors.compose_cell)에 넣어도 시험 표의 모든
사례·칸의 그림이 draw_tile(Wall 열쇠)를 draw_tile(Ground 열쇠) 위에 얹은 것과 픽셀 단위로 같다 — 시트와 좌표표가 정말 쓸 수 있다는 증명.
임시 폴더에만 쓰고, 특정 픽셀 색에는 기대지 않는다."""
import io
import json
import shutil
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest import mock

from PIL import Image

from pixelkit import cli, room_export
from pixelkit.cli import load_kit
from pixelkit.room_tiles import CELL, DOOR_RECT, wall_tops
from tests import test_room_vectors as V

ROOT = Path(__file__).resolve().parents[3]
DEFS = "data/building/RoomFinishDefinition.json"
SHEET = "art/world/finishes/finishes.png"
ATLAS = "data/atlases/art/world/finishes/finishes.png.atlas.json"
KIT = load_kit(str(ROOT / "art" / "kit"))


def make_repo(defs=None) -> Path:
    """임시 저장소: 정의 파일만 들어 있다(없는 것은 export가 만든다). 기본은 시험용 꾸밈 열 개(고정 값 — 실제 data/와 같은 모양)."""
    root = Path(tempfile.mkdtemp(prefix="room-export-"))
    (root / "data" / "building").mkdir(parents=True)
    text = json.dumps(TEN if defs is None else defs, ensure_ascii=False)
    (root / DEFS).write_text(text, encoding="utf-8", newline="\n")
    return root


def finish(fid, slot, art, sheet=SHEET):
    return {"type": "RoomFinishDefinition", "id": fid, "slot": slot, "nameKey": fid + ".name", "sheet": sheet, "art": art,
            "costItems": [], "costCounts": [], "unlockToken": ""}


def atlas_of(root: Path) -> dict:
    return json.loads((root / ATLAS).read_text(encoding="utf-8"))


TEN = [finish(f"finish.wall.{a}", "wall", a) for a in ("paper", "wallpaper-sprig", "paint-two-tone", "panel-plank", "paint-mint")] + [finish("finish.floor." + a.replace("floor.", "", 1), "floor", a)
       for a in ("floor.plank", "floor.checker-soft", "floor.carpet", "floor.tatami", "floor.carpet-diamond")]


class SheetPieces:
    """내보낸 시트에서 조각을 자른다 — test_room_vectors.Pieces와 같은 꼴(tile, band, floor_at). 게임이 할 일과 같다."""

    def __init__(self, root: Path, wall: str, floor_at):
        self.atlas = atlas_of(root)
        self.sheet = Image.open(root / SHEET).convert("RGBA")
        self.wall, self.floor_at = wall, floor_at

    def _crop(self, name: str) -> Image.Image:
        s = self.atlas["sprites"][name]
        return self.sheet.crop((s["x"], s["y"], s["x"] + s["w"], s["y"] + s["h"]))

    def tile(self, key: str, floor_art=None) -> Image.Image:
        if key.startswith("floor"):
            return self._crop(f"{floor_art}/{key}")
        if key.startswith("sill:"):
            return self._crop(f"common/{key}")
        if key.startswith("door:"):
            return self._crop("common/door")
        return self._crop(f"{self.wall}/{key}")

    def band(self, shade: str):
        top = room_export.wall_top(self.wall)
        c = self.atlas["bands"][top][shade]
        return tuple(int(c[i:i + 2], 16) for i in (1, 3, 5)) + (255,)


class ExportShapeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = make_repo()
        cls.written = room_export.export_finishes(cls.root, KIT)
        cls.atlas = atlas_of(cls.root)
        cls.defs = json.loads((cls.root / DEFS).read_text(encoding="utf-8"))

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.root, ignore_errors=True)

    def test_writes_the_sheet_and_the_atlas(self):
        self.assertEqual(sorted(self.written), sorted([SHEET, ATLAS]))
        self.assertTrue((self.root / SHEET).exists() and (self.root / ATLAS).exists())

    def test_piece_counts_per_finish(self):
        names = list(self.atlas["sprites"])
        for d in self.defs:
            mine = [n for n in names if n.startswith(d["art"] + "/")]
            self.assertEqual(len(mine), 36 if d["slot"] == "wall" else 2, d["art"])
        self.assertEqual(len([n for n in names if n.startswith("common/")]), 10)
        walls = sum(1 for d in self.defs if d["slot"] == "wall")
        floors = sum(1 for d in self.defs if d["slot"] == "floor")
        self.assertEqual(len(names), walls * 36 + floors * 2 + 10)

    def test_wall_piece_names(self):
        names = set(self.atlas["sprites"])
        want = set()
        for k in (1, 2):
            for e in ("", "l", "r", "lr"):
                for t in ("", "c", "t"):
                    flags = e + t
                    want.add(f"paper/face:{k}" + (":" + flags if flags else ""))
        self.assertEqual(len(want), 24)
        self.assertLessEqual(want, names)
        narrow = {f"paper/face:{k}:lr{c}@{a}-{b}" for k in (1, 2) for c in ("", "c") for a, b in ((0, 16), (8, 24), (16, 32))}
        self.assertEqual(len(narrow), 12)
        self.assertLessEqual(narrow, names)

    def test_floor_and_common_piece_names(self):
        names = set(self.atlas["sprites"])
        self.assertLessEqual({"floor.plank/floor", "floor.plank/floor:shade"}, names)
        sills = {f"common/sill:{s}:{a}-{b}" for s in "new" for a, b in ((0, 16), (8, 24), (16, 32))}
        self.assertEqual(len(sills), 9)
        self.assertLessEqual(sills | {"common/door"}, names)

    def test_every_piece_is_a_32_square_on_the_grid_inside_the_sheet(self):
        a = self.atlas
        self.assertEqual((a["origin"], a["pixelsPerUnit"], a["band"], a["image"]), ("top-left", 32, 16, SHEET))
        with Image.open(self.root / SHEET) as im:
            self.assertEqual(im.size, (a["width"], a["height"]))
            self.assertEqual(im.width, 16 * CELL)
        seen = set()
        for name, s in a["sprites"].items():
            self.assertEqual((s["w"], s["h"]), (CELL, CELL), name)
            self.assertEqual((s["x"] % CELL, s["y"] % CELL), (0, 0), name)
            self.assertLessEqual(s["x"] + CELL, a["width"])
            self.assertLessEqual(s["y"] + CELL, a["height"])
            self.assertNotIn((s["x"], s["y"]), seen, name)
            seen.add((s["x"], s["y"]))

    def test_slots_follow_name_order_row_by_row(self):
        names = sorted(self.atlas["sprites"])
        for i, n in enumerate(names):
            s = self.atlas["sprites"][n]
            self.assertEqual((s["x"], s["y"]), (i % 16 * CELL, i // 16 * CELL), n)

    def test_band_colors_for_every_top(self):
        bands = self.atlas["bands"]
        self.assertEqual(sorted(bands), sorted(wall_tops()))
        for top, c in bands.items():
            self.assertEqual(sorted(c), ["d", "f", "l", "o"], top)
            for v in c.values():
                self.assertRegex(v, r"\A#[0-9A-F]{6}\Z")
            self.assertEqual(c["o"], "#%02X%02X%02X" % tuple(V.STYLE.outline[:3]))
            self.assertEqual(c["f"], "#%02X%02X%02X" % tuple(V.STYLE.color(top + ".1")[:3]))

    def test_door_piece_is_transparent_outside_the_door_rect(self):
        s = self.atlas["sprites"]["common/door"]
        im = Image.open(self.root / SHEET).convert("RGBA").crop((s["x"], s["y"], s["x"] + CELL, s["y"] + CELL))
        x0, y0, x1, y1 = DOOR_RECT
        for y in range(CELL):
            for x in range(CELL):
                inside = x0 <= x < x1 and y0 <= y < y1
                self.assertEqual(im.getpixel((x, y))[3] == 255, inside, (x, y))

    def test_text_files_use_lf(self):
        raw = (self.root / ATLAS).read_bytes()
        self.assertNotIn(b"\r", raw)
        self.assertTrue(raw.endswith(b"\n"))


class ExportRulesTests(unittest.TestCase):
    def tearDown(self):
        for r in getattr(self, "roots", []):
            shutil.rmtree(r, ignore_errors=True)

    def repo(self, defs=None):
        root = make_repo(defs)
        self.roots = getattr(self, "roots", []) + [root]
        return root

    def test_same_input_gives_same_bytes(self):
        a, b = self.repo(), self.repo()
        room_export.export_finishes(a, KIT)
        room_export.export_finishes(b, KIT)
        self.assertEqual((a / SHEET).read_bytes(), (b / SHEET).read_bytes())
        self.assertEqual((a / ATLAS).read_bytes(), (b / ATLAS).read_bytes())
        self.assertEqual(room_export.export_finishes(a, KIT), [], "같은 내용이면 다시 쓰지 않는다")

    def test_dry_run_writes_nothing_and_lists_the_files(self):
        root = self.repo()
        self.assertEqual(sorted(room_export.export_finishes(root, KIT, dry_run=True)), sorted([SHEET, ATLAS]))
        self.assertFalse((root / SHEET).exists() or (root / ATLAS).exists())

    def test_only_the_listed_finishes_are_exported(self):
        root = self.repo([finish("finish.wall.paper", "wall", "paper"), finish("finish.floor.plank", "floor", "floor.plank")])
        room_export.export_finishes(root, KIT)
        arts = {n.split("/")[0] for n in atlas_of(root)["sprites"]}
        self.assertEqual(arts, {"paper", "floor.plank", "common"})

    def test_a_wall_with_another_band_is_rejected_and_nothing_is_written(self):
        root = self.repo([finish("finish.wall.paper", "wall", "paper")])
        with mock.patch.object(room_export, "wall_band", return_value=12):
            with self.assertRaises(room_export.RoomExportError) as cm:
                room_export.export_finishes(root, KIT)
        self.assertEqual(cm.exception.code, "room-finish-band")
        self.assertFalse((root / SHEET).exists() or (root / ATLAS).exists())

    def test_unknown_art_slot_and_missing_fields_are_rejected(self):
        cases = {
            "room-finish-art": [finish("finish.wall.x", "wall", "no-such-wall")],
            "room-finish-art-floor": [finish("finish.floor.x", "floor", "no-such-floor")],
            "room-finish-slot": [finish("finish.x.x", "ceiling", "paper")],
            "room-finish-duplicate": [finish("finish.wall.paper", "wall", "paper"), finish("finish.wall.paper", "wall", "plaster")],
            "room-finish-sheet": [finish("finish.wall.paper", "wall", "paper", sheet="")],
        }
        for code, defs in cases.items():
            root = self.repo(defs)
            with self.assertRaises(room_export.RoomExportError, msg=code) as cm:
                room_export.export_finishes(root, KIT)
            self.assertEqual(cm.exception.code.replace("-floor", ""), code.replace("-floor", ""), code)
            self.assertFalse((root / SHEET).exists(), code)

    def test_a_wall_art_used_as_floor_is_rejected(self):
        root = self.repo([finish("finish.floor.paper", "floor", "paper")])
        with self.assertRaises(room_export.RoomExportError):
            room_export.export_finishes(root, KIT)

    def test_missing_definitions_file_is_an_error(self):
        root = self.repo()
        (root / DEFS).unlink()
        with self.assertRaises(room_export.RoomExportError) as cm:
            room_export.export_finishes(root, KIT)
        self.assertEqual(cm.exception.code, "room-finish-defs")

    def test_other_entity_types_in_the_file_are_ignored(self):
        root = self.repo([{"type": "SomethingElse", "id": "x"}, finish("finish.wall.paper", "wall", "paper")])
        room_export.export_finishes(root, KIT)
        self.assertIn("paper/face:1", atlas_of(root)["sprites"])

    def test_two_sheets_get_their_own_files(self):
        other = "art/world/finishes/second.png"
        root = self.repo([finish("finish.wall.paper", "wall", "paper"), finish("finish.wall.plaster", "wall", "plaster", sheet=other)])
        room_export.export_finishes(root, KIT)
        self.assertTrue((root / other).exists() and (root / ("data/atlases/" + other + ".atlas.json")).exists())
        self.assertNotIn("plaster/face:1", atlas_of(root)["sprites"])

    def test_cli_dry_run_and_write(self):
        root = self.repo()
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = cli.run(_args(root, dry_run=True))
        self.assertEqual(code, 0, err.getvalue())
        self.assertFalse((root / SHEET).exists())
        self.assertIn("--dry-run", out.getvalue())
        with redirect_stdout(out), redirect_stderr(err):
            self.assertEqual(cli.run(_args(root)), 0, err.getvalue())
        self.assertTrue((root / SHEET).exists())

    def test_cli_reports_problems_with_exit_code_1_and_writes_nothing(self):
        root = self.repo([finish("finish.wall.x", "wall", "no-such-wall")])
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            self.assertEqual(cli.run(_args(root)), 1)
        self.assertIn("room-finish-art", out.getvalue() + err.getvalue())
        self.assertFalse((root / SHEET).exists())


def _args(root: Path, dry_run=False):
    import argparse
    return argparse.Namespace(command="room", ids=["export-finishes"], kit=str(ROOT / "art" / "kit"), out=None, scale=None,
                              repo=str(root), check=False, dry_run=dry_run, force=False, list=False)


class CommittedSheetTests(unittest.TestCase):
    def test_the_committed_sheet_is_what_the_real_definitions_export_now(self):
        """저장소의 실제 data/building 정의로 내면 저장소의 시트·좌표표와 바이트까지 같다(--dry-run이 쓸 것 없음). 손으로 고치지 않았다는 증거."""
        plan = room_export.plan_finishes(ROOT, KIT)
        self.assertGreaterEqual(len(plan.files), 2)
        for rel, data in plan.files.items():
            self.assertEqual((ROOT / rel).read_bytes(), data, f"{rel}가 지금 정의·규칙과 다르다 — python Tools/pixel-kit/kit.py room export-finishes")
        defs = room_export.read_definitions(ROOT)
        self.assertEqual(len(defs), 10)
        self.assertEqual(sum(1 for d in defs if d["slot"] == "wall"), 5)


class SheetRecomposesTheKitTilesTests(unittest.TestCase):
    """내보낸 시트의 조각 + 출처표 = 키트가 지금 그리는 그림(픽셀까지)."""

    @classmethod
    def setUpClass(cls):
        cls.root = make_repo()
        room_export.export_finishes(cls.root, KIT)
        cls.defs = json.loads((cls.root / DEFS).read_text(encoding="utf-8"))

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.root, ignore_errors=True)

    def test_every_case_and_wall_finish_matches_draw_tile(self):
        walls = [d["art"] for d in self.defs if d["slot"] == "wall"]
        self.assertGreaterEqual(len(walls), 5)
        seen, compared = set(), 0
        for wall in walls:
            pieces = SheetPieces(self.root, wall, lambda cell: "floor.plank")
            for cid, g, doors, fh, band in V.ALL:
                if (fh, band) != (2, 16):
                    continue                    # 조각 시트는 정면 1·2칸, 띠 16만 굽는다(맵 검사가 그 밖을 알린다)
                tiles, src = V.TILES[cid], V.SOURCES[cid]
                for y, row in enumerate(src):
                    for x, parts in enumerate(row):
                        if parts is None:
                            continue
                        ck = (wall, tiles.wall[y][x], tiles.ground[y][x], tuple((p.src, p.shade, p.piece, p.px) for p in parts))
                        if ck in seen:
                            continue
                        seen.add(ck)
                        got = V.compose_cell(parts, pieces, (x, y))
                        want = V.drawn_cell(tiles, x, y, wall)
                        self.assertEqual(got.tobytes(), want.tobytes(), f"{cid} {wall} 칸 ({x}, {y}) {tiles.wall[y][x]} / {tiles.ground[y][x]}")
                        compared += 1
        self.assertGreater(compared, len(walls) * 50)

    def test_each_floor_finish_matches_draw_tile_on_a_floor_only_room(self):
        floors = [d["art"] for d in self.defs if d["slot"] == "floor"]
        self.assertGreaterEqual(len(floors), 5)
        cid, g, doors, *_ = next(c for c in V.ALL if c[0] == "house")
        tiles, src = V.TILES[cid], V.SOURCES[cid]
        for art in floors:
            spec = KIT.recipes[art]
            pieces = SheetPieces(self.root, "paper", lambda cell, a=art: a)
            checked = 0
            for y, row in enumerate(src):
                for x, parts in enumerate(row):
                    if parts is None or tiles.ground[y][x] is None:
                        continue
                    parts = [p for p in parts if p.src == "floor"]
                    if not parts or any(p.own != (0, 0) for p in parts):
                        continue
                    got = V.compose_cell(parts, pieces, (x, y))
                    want = V.Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
                    want.alpha_composite(V.draw_tile(V.STYLE, tiles.ground[y][x], "paper", spec).to_image())
                    self.assertEqual(got.tobytes(), want.tobytes(), f"{art} 칸 ({x}, {y})")
                    checked += 1
            self.assertGreater(checked, 20, art)

    def test_two_floors_beside_a_vertical_band_come_from_the_sheet(self):
        g = V.case_grid(dict((c[0], c[1]) for c in V.all_cases())["two-rooms-wall"])
        parts = V.plan_sources(g, 2, 16)[4][3]
        pieces = SheetPieces(self.root, "paper", lambda cell: "floor.plank" if cell[0] < 3 else "floor.carpet")
        got = V.compose_cell(parts, pieces, (3, 4))
        tiles = V.plan_tiles(g, 2, 16)
        left = V.drawn_cell(tiles, 3, 4, "paper", KIT.recipes["floor.plank"])
        right = V.drawn_cell(tiles, 3, 4, "paper", KIT.recipes["floor.carpet"])
        band = set().union(*(V.pixels(p) for p in parts if p.src == "band"))
        xs = sorted(x for x, _ in band)
        for y in range(CELL):
            for x in range(CELL):
                if (x, y) not in band:
                    self.assertEqual(got.getpixel((x, y)), (left if x < xs[0] else right).getpixel((x, y)), f"({x}, {y})")

    def test_every_piece_the_vectors_name_is_in_the_atlas(self):
        names = set(atlas_of(self.root)["sprites"])
        walls = [d["art"] for d in self.defs if d["slot"] == "wall"]
        floors = [d["art"] for d in self.defs if d["slot"] == "floor"]
        vectors = json.loads((ROOT / "Tools/pixel-kit/tests/fixtures/room-vectors.json").read_text(encoding="utf-8"))
        want = set()
        for case in vectors["cases"]:
            if (case["faceHeight"], case["band"]) != (2, 16):
                continue                        # 조각 시트가 받는 사례만
            for cell in case["cells"]:
                for p in cell["parts"]:
                    if p["src"] in ("face", "narrow"):
                        want |= {f"{w}/{p['piece']}" for w in walls}
                    elif p["src"] in ("sill",):
                        want.add("common/" + p["piece"])
                    elif p["src"] == "door":
                        want.add("common/door")
                    elif p["src"] == "floor":
                        key = "floor:shade" if p["shade"].startswith("floor:shade") else "floor"
                        want |= {f"{f}/{key}" for f in floors}
        self.assertGreater(len(want), 50)
        self.assertEqual(sorted(want - names), [])

