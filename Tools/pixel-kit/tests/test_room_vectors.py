"""방 만들기 — 픽셀 출처표(room_tiles.plan_sources)와 시험 표 room-vectors.json(room_vectors).

가장 중요한 것: 출처표 + 조각(정면 조각, 좁은 정면 조각, 띠 칠 네 색, 바닥 floor·floor:shade, 문지방·문짝)으로 칸을 다시
조합한 그림이 draw_tile(Wall 열쇠)를 draw_tile(Ground 열쇠) 위에 얹은 그림과 픽셀 단위로 같다 — 모든 사례 × 모든 벽 모양.
참조 조합기(compose_cell)는 게임(Host)이 C#으로 할 일과 같은 일을 한다. 특정 픽셀 색에는 기대지 않는다(두 경로가 같은지,
구조, 주인 칸)."""
import hashlib
import io
import json
import shutil
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

from PIL import Image

from pixelkit import cli, room_vectors
from pixelkit.recipes import load_recipes
from pixelkit.room_tiles import (CELL, FLOOR, SourcePart, _decode, _parse_floor, _shades, classify, draw_tile,
                                 plan_room, plan_sources, plan_tiles, tile_layout, wall_styles)
from pixelkit.room_vectors import all_cases, build_vectors, case_grid
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
STYLE = Style.load(str(ROOT / "art" / "kit" / "style.json"))
RECIPES = load_recipes(str(ROOT / "art" / "kit" / "recipes"))
FLOOR_SPEC = RECIPES["floor.plank"]
OTHER_FLOOR_SPEC = RECIPES["floor.carpet"]

# plan_tiles(격자, 정면 높이, 띠 두께, 문)의 [wall, ground]를 json.dumps(ensure_ascii=False)한 SHA-256. 처음은 커밋 8a2c4edc7 —
# 출처표를 내도록 plan_tiles를 다시 짜도 열쇠가 하나도 바뀌지 않았다는 증거였다. 규칙을 일부러 바꾸면 여기도 고친다.
# 2026-11-14 늘인 정면 아래 그림자: T자 자리의 세로 벽 띠 칸·좁은 정면 칸(늘인 정면 바로 아래) 13칸의 Ground 열쇠만 바뀌었다
# (cross·house·random-4·random-6·tee·test-two-rooms·two-rooms-wall·wall-doors-inner·wall-end). 새 사례 다섯은 처음 적은 값.
PLAN_TILES_BEFORE = {
    "band-24": "5dd2fccdf0148b5af69a5bed315208ff81e84b7f2f55f99d6fb480c8646602f2",
    "band-8": "252ced47e4b525f0ab5c424a6e1b2142584ab066b1664bd0f8eb69789dc202f8",
    "block-3x3": "7e16485de902581f11b90e7cab76a964529da62d202f0dd8b19695e02b03c87a",
    "cross": "13fd6d4cfba78203c70012ab2a053d351168d033a15a5fe07a54df03a72f99ba",
    "doorway": "9543c8d58dbd306464db57f5ec3fd647ec6d88687e61a2a70674844809235f34",
    "edge": "e45d66eb697fbe3335f4af61a7949b225b4f65ac5a3f09a5f00bf66a51c1416d",
    "face-height-1": "ce6d38195368cb94904761ed98b47b94775765d0a72ebf202feba389144e41b6",
    "face-height-3": "ff276304ddeec4a7acbc299f1440d687083b3b7d60e27f52e378e72d108b0775",
    "house": "e2651e09a9be5983dd686bdb8d43ac4ca4bb04cb34bb394b00f43a656a794440",
    "odd-1": "10163b479656c1bf1e97eb7a89e1e4f6f86ca5eee20f2a629f8128e5b88c16e3",
    "odd-2": "ece06a2de33f3feaeb2231adfac80ebe16c54a0367929184a671b7b272df96f2",
    "random-1": "1b028c0ae916e9f614fbf9ccc7e08155321a8d6ca8927eca8ac1b0e600515890",
    "random-2": "949e00618d4216ee5fedf08b1e7443db0d0c5fd386d96e55464eabff846e263a",
    "random-3": "a2bf95608f3573b322c8dd62dc4d231da0fd4c4f6227a7caff992bc8a59c8fa1",
    "random-4": "1ec044eb2904f783443b5198718a631c251e50cefff94bacd085bab687cad962",
    "random-5": "beb34124e37298ee37eb92617fe95f74a18f2ba55a8a74463ea6ed80fb247dd7",
    "random-6": "8a2fcea0a5c93a7af484ac8043d3932fce3090cf9cb4585c19bbfc09e48afe87",
    "random-7": "6c6d961ed933a57b861592977dec362266b937271cf15c047b1ba735b6d0f669",
    "random-8": "3df6b6583779e7104dc9d662c9ef541be59ba08d21dbc0c3df86cb4c2c133011",
    "sills-all": "6e638c850fea85f1b62e0bb6fdcf6a261012eb27e51900e4d40047c062c93b4c",
    "tee": "1aac5f09bc7cc474dfd4963621a07da15b6e487059305fbb4263ea0f5328b17c",
    "test-two-rooms": "f5c55eab8085b4670fc7ba86d345e7bc8f520bbaca5c4aa11223a91011fcc8b1",
    "thin-wall-1": "452102571140ca78009b55b35ef7f358e4d8137e429a4051cff8f9bf83d5ea3f",
    "thin-wall-2": "d67e52c385ec4f35103fc4b595cf521b251616dbe47e9e665d968d57d148b4b7",
    "two-rooms-wall": "432cc350b05e387fb1de70078518e2d220d9a5b8dde9e50f9ca6ab8c59f52ed6",
    "wall-doors": "4561df04590a2df33d9e2a50a2fabe7c17b005af15cdd41be724f432082ff11f",
    "wall-doors-inner": "b9c32eaa681239e3ec3089efbf7fdfe8e6faf11ff543af724b5ba6b95dc73e05",
    "wall-end": "efff672790c12164017ef125888caf1f41451359647743edb0c7f0cb96a53ac8",
}

VECTORS = build_vectors()
# (id, 격자, 문 칸들, 정면 높이, 띠 두께)
ALL = [(cid, case_grid(rows), list(doors), fh, band) for cid, rows, doors, fh, band in all_cases()]
PLANS = {cid: plan_room(g, fh, band, doors) for cid, g, doors, fh, band in ALL}
TILES = {cid: tiles for cid, (tiles, _) in PLANS.items()}
SOURCES = {cid: sources for cid, (_, sources) in PLANS.items()}


def pixels(part: SourcePart):
    return _decode(part.px, 0, CELL, "px")


# ---------------------------------------------------------------- 참조 조합기

class Pieces:
    """조각을 draw_tile로 만든다(게임은 조각 시트에서 읽는다). 조각 이름은 그대로 그릴 수 있는 열쇠다 — 문짝만 정면 문 타일의 문 자리."""

    def __init__(self, wall: str, floor_at):
        self.wall, self.floor_at, self.cache = wall, floor_at, {}

    def tile(self, key: str, floor_spec=FLOOR_SPEC) -> Image.Image:
        k = (key, id(floor_spec))
        if k not in self.cache:
            self.cache[k] = draw_tile(STYLE, key, self.wall, floor_spec).to_image()
        return self.cache[k]

    def band(self, shade: str):
        dark, face, light = _top(self.wall)
        return {"o": STYLE.outline, "d": dark, "f": face, "l": light}[shade]


def _top(wall: str):
    from pixelkit.room_tiles import wall_top
    top = wall_top(wall)
    return tuple(STYLE.color(f"{top}.{i}") for i in range(3))


def rects(part: SourcePart):
    """px 글 → 사각형 (x0, y0, x1, y1)들(끝 제외)."""
    return [tuple(map(int, r.split(","))) for r in part.px.split("+")]


def _copy(out, src, part):
    for box in rects(part):
        out.paste(src.crop(box), box[:2])


def _band(out, pieces, part, cell):
    color = tuple(pieces.band(part.shade))
    for box in rects(part):
        out.paste(color, box)


def _wall_piece(out, pieces, part, cell):
    _copy(out, pieces.tile(part.piece), part)        # 벽지 주인은 cell + own(이 시험은 벽지 하나만 쓴다)


def _floor(out, pieces, part, cell):
    """바닥 주인(cell + own)의 floor 조각, 그림자 범위(shade 열쇠의 x 범위 × 위 두 줄)만 floor:shade 조각."""
    spec = pieces.floor_at((cell[0] + part.own[0], cell[1] + part.own[1]))
    _copy(out, pieces.tile("floor", spec), part)
    rng = _parse_floor(part.shade)
    if rng is None:
        return
    shaded = pieces.tile("floor:shade", spec)
    for x0, y0, x1, y1 in rects(part):
        box = (max(x0, rng[0]), y0, min(x1, rng[1]), min(y1, 2))
        if box[0] < box[2] and box[1] < box[3]:
            out.paste(shaded.crop(box), box[:2])


def _fixed(out, pieces, part, cell):
    _copy(out, pieces.tile("door:face:1" if part.piece == "door" else part.piece), part)


HANDLERS = {"band": _band, "face": _wall_piece, "narrow": _wall_piece, "floor": _floor, "sill": _fixed, "door": _fixed}


def compose_cell(parts, pieces, cell) -> Image.Image:
    out = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    for part in parts:
        HANDLERS[part.src](out, pieces, part, cell)
    return out


_DRAWN = {}


def drawn_cell(tiles, x, y, wall, floor_spec=FLOOR_SPEC) -> Image.Image:
    """키트가 지금 그리는 그림: Ground 타일 위에 Wall 타일."""
    out = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    for key in (tiles.ground[y][x], tiles.wall[y][x]):
        if key is not None:
            k = (key, wall, id(floor_spec))
            if k not in _DRAWN:
                _DRAWN[k] = draw_tile(STYLE, key, wall, floor_spec).to_image()
            out.alpha_composite(_DRAWN[k])
    return out


# ---------------------------------------------------------------- 시험

class SourcesRebuildTheTilesTests(unittest.TestCase):
    def test_reference_compositor_matches_draw_tile_on_every_case_and_wall_style(self):
        seen, compared = set(), 0
        for wall in wall_styles():
            pieces = Pieces(wall, lambda cell: FLOOR_SPEC)
            for cid, g, doors, fh, band in ALL:
                tiles = TILES[cid]
                src = SOURCES[cid]
                for y, row in enumerate(src):
                    for x, parts in enumerate(row):
                        if parts is None:
                            continue
                        # 바닥 하나·벽지 하나로 그리므로 주인은 그림을 바꾸지 않는다(주인은 아래 두 시험이 본다) — 같은 것은 한 번만
                        ck = (wall, tiles.wall[y][x], tiles.ground[y][x], tuple((p.src, p.shade, p.piece, p.px) for p in parts))
                        if ck in seen:
                            continue
                        seen.add(ck)
                        self.assertEqual(compose_cell(parts, pieces, (x, y)).tobytes(), drawn_cell(tiles, x, y, wall).tobytes(),
                                         f"{cid} {wall} 칸 ({x}, {y}) 열쇠 {tiles.wall[y][x]} / {tiles.ground[y][x]}")
                        compared += 1
        self.assertGreater(compared, len(wall_styles()) * 50)

    def test_two_rooms_with_different_floors_show_each_floor_beside_the_vertical_band(self):
        g = case_grid(dict((c[0], c[1]) for c in all_cases())["two-rooms-wall"])
        parts = plan_sources(g, 2, 16)[4][3]
        floors = lambda cell: FLOOR_SPEC if cell[0] < 3 else OTHER_FLOOR_SPEC      # 왼쪽 방과 오른쪽 방의 바닥이 다르다
        got = compose_cell(parts, Pieces("paper", floors), (3, 4))
        tiles = plan_tiles(g, 2, 16)
        left, right = drawn_cell(tiles, 3, 4, "paper", FLOOR_SPEC), drawn_cell(tiles, 3, 4, "paper", OTHER_FLOOR_SPEC)
        band = set().union(*(pixels(p) for p in parts if p.src == "band"))
        xs = sorted(x for x, _ in band)
        self.assertTrue(xs and 0 < xs[0] and xs[-1] < CELL - 1, "띠가 가운데에 있어야 양옆으로 바닥이 비친다")
        for y in range(CELL):
            for x in range(CELL):
                if (x, y) in band:
                    continue
                want = left if x < xs[0] else right
                self.assertEqual(got.getpixel((x, y)), want.getpixel((x, y)), f"({x}, {y})")
        self.assertNotEqual(left.tobytes(), right.tobytes(), "두 바닥이 같으면 이 시험은 아무것도 보지 않는다")


class SourceShapeTests(unittest.TestCase):
    def test_parts_cover_disjoint_and_owners_are_floor(self):
        for cid, g, doors, fh, band in ALL:
            tiles = TILES[cid]
            for y, row in enumerate(SOURCES[cid]):
                for x, parts in enumerate(row):
                    if parts is None:
                        self.assertIsNone(classify(g, fh)[y][x], f"{cid} ({x}, {y})")
                        continue
                    seen = set()
                    for p in parts:
                        px = pixels(p)
                        self.assertFalse(px & seen, f"{cid} ({x}, {y}) {p} 겹침")
                        seen |= px
                        self.assertIn(p.src, HANDLERS)
                        if p.src in ("face", "narrow", "floor"):
                            ox, oy = x + p.own[0], y + p.own[1]
                            self.assertEqual(g[oy][ox], FLOOR, f"{cid} ({x}, {y}) {p}: 주인 ({ox}, {oy})이 바닥이 아니다")
                        else:
                            self.assertIsNone(p.own, f"{cid} ({x}, {y}) {p}")
                    # 덮은 픽셀 = 그린 그림의 불투명 픽셀(비어 있음은 적지 않는다)
                    opaque = {(i % CELL, i // CELL) for i, a in enumerate(drawn_cell(tiles, x, y, "paper").getchannel("A").tobytes()) if a}
                    self.assertEqual(seen, opaque, f"{cid} ({x}, {y})")
                    self.assertEqual(parts, sorted(parts, key=room_vectors.part_order), f"{cid} ({x}, {y}) 순서")

    def test_vertical_wall_band_has_two_floor_owners(self):
        g = case_grid(dict((c[0], c[1]) for c in all_cases())["two-rooms-wall"])
        for y in (3, 4, 5):                        # 두 방 사이 세로 벽의 윗면 칸들
            owners = {tuple(p.own) for p in plan_sources(g, 2, 16)[y][3] if p.src == "floor"}
            self.assertEqual(owners, {(-1, 0), (1, 0)}, f"(3, {y})")
        house = SOURCES["house"]
        owners = {tuple(p.own) for p in house[4][13] if p.src == "floor"}      # 새 집 위 두 방 사이 세로 벽
        self.assertEqual(owners, {(-1, 0), (1, 0)})

    def test_face_owner_is_k_cells_below(self):
        n = 0
        for cid, g, doors, fh, band in ALL:
            for y, row in enumerate(SOURCES[cid]):
                for x, parts in enumerate(row or ()):
                    for p in parts or ():
                        if p.src in ("face", "narrow"):
                            k = int(p.piece.split(":")[1].split("@")[0])
                            fx, fy = x + p.own[0], y + p.own[1] - k          # 그 정면 픽셀이 온 정면 칸
                            self.assertTrue(classify(g, fh)[fy][fx].startswith(f"face:{k}"), f"{cid} ({x}, {y}) {p}")
                            if (fx, fy) == (x, y):
                                n += 1
        self.assertGreater(n, 0)

    def test_band_shades_match_tile_layout(self):
        for cid, g, doors, fh, band in ALL:
            tiles = TILES[cid]
            for y, row in enumerate(SOURCES[cid]):
                for x, parts in enumerate(row):
                    key = tiles.wall[y][x]
                    layout = tile_layout(key) if key else None
                    want = {}
                    if layout is not None and layout.band:
                        want = _shades(layout.band, layout.padded)
                    got = {xy: p.shade for p in parts or () if p.src == "band" for xy in pixels(p)}
                    self.assertEqual(got, want, f"{cid} ({x}, {y}) {key}")

    def test_kind_and_keys_in_vectors_match_classify_and_plan_tiles(self):
        doc = json.loads(VECTORS.decode("utf-8"))
        self.assertEqual([c["id"] for c in doc["cases"]], [c[0] for c in ALL])
        for case, (cid, g, doors, fh, band) in zip(doc["cases"], ALL):
            kinds, tiles = classify(g, fh), TILES[cid]
            want = [(x, y) for y in range(len(g)) for x in range(len(g[y])) if kinds[y][x] is not None]
            self.assertEqual([(c["x"], c["y"]) for c in case["cells"]], want, cid)
            for c in case["cells"]:
                x, y = c["x"], c["y"]
                self.assertEqual((c["kind"], c["wall"], c["ground"]), (kinds[y][x], tiles.wall[y][x], tiles.ground[y][x]))
                self.assertEqual([SourcePart(p["src"], p.get("shade"), p.get("piece"),
                                             tuple(p["own"]) if "own" in p else None, p["px"]) for p in c["parts"]],
                                 SOURCES[cid][y][x])

    def test_floor_without_a_floor_neighbour_takes_the_fallback_owner_and_the_vectors_have_each_case(self):
        from pixelkit.room_tiles import _plan
        seen = set()
        for cid, g, doors, fh, band in ALL:
            p = _plan(g, fh, band, doors)
            for (x, y), cls in p.cells.items():
                if p.ground[y][x] is None:
                    continue
                sill = (x, y) in p.sills
                for i, c in enumerate(cls):
                    what = ("sill-band" if c == "F" and p.origin[x, y][i] is None else "narrow-empty" if c == "V"
                            else "sill-face" if sill and c.startswith("face:") else None)
                    if what is None:
                        continue
                    seen.add(what)
                    want = p.towards[x, y] if sill else next(o for c2, o in zip(cls, p.origin[x, y]) if c2 == "F")
                    part = next(q for q in SOURCES[cid][y][x] if (i % CELL, i // CELL) in pixels(q))
                    self.assertEqual((part.src, part.own), ("floor", want), f"{cid} ({x}, {y}) {what}")
        self.assertEqual(seen, {"sill-band", "narrow-empty", "sill-face"})

    def test_every_wall_door_direction_is_in_the_vectors(self):
        srcs = {(p.src, p.piece.split(":")[1] if p.src == "sill" else None)
                for row in SOURCES["wall-doors"] for parts in row if parts for p in parts}
        for want in (("sill", "n"), ("sill", "e"), ("sill", "w"), ("door", None)):
            self.assertIn(want, srcs)

    def test_every_sill_piece_is_in_the_table(self):
        """조각 시트가 굽는 문지방(common/sill:*) 9가지가 모두 시험 표 사례 머리의 문지방 열쇠 목록에 있고, 그 목록의 열쇠는 띠가 같은
        사례들의 출처표에 나온다 — 띠 자리 규칙상 나올 수 없는 셋(room_vectors.UNREACHABLE_SILLS)만 빼고. 그 셋은 어디에도 나오지 않는다."""
        from pixelkit import room_export
        doc = json.loads(room_vectors.VECTORS_PATH.read_text(encoding="utf-8"))      # 게임 테스트도 읽는 파일 그대로
        listed = [c for c in doc["cases"] if "sills" in c]
        self.assertTrue(listed, "문지방 열쇠 목록을 적은 사례가 없다")
        for case in listed:
            band = case["band"]
            used = {p["piece"] for c in doc["cases"] if c["band"] == band for cell in c["cells"]
                    for p in cell["parts"] if p["src"] == "sill"}
            with self.subTest(case=case["id"]):
                if band == room_export.BAND:
                    self.assertEqual(sorted(case["sills"]), sorted(room_export.sill_pieces()))
                for key in case["sills"]:
                    if key in room_vectors.UNREACHABLE_SILLS:
                        self.assertNotIn(key, used, f"{key}가 나온다 — UNREACHABLE_SILLS에서 뺀다")
                    else:
                        self.assertIn(key, used, f"문지방 {key}가 시험 표 출처표에 없다")
                self.assertEqual(used - set(case["sills"]), set(), "목록에 없는 문지방")
        self.assertEqual(len(set(room_vectors.UNREACHABLE_SILLS) & set(room_export.sill_pieces())), 3)


class VectorsFileTests(unittest.TestCase):
    def test_vectors_file_is_up_to_date(self):
        self.assertTrue(room_vectors.VECTORS_PATH.is_file(), "python Tools/pixel-kit/kit.py room vectors로 만든다")
        self.assertEqual(VECTORS, room_vectors.VECTORS_PATH.read_bytes(),
                         "규칙이 바뀌었으면 python Tools/pixel-kit/kit.py room vectors로 시험 표를 다시 만든다")

    def test_vectors_bytes_are_deterministic_lf_and_end_with_newline(self):
        a = VECTORS
        self.assertEqual(a, build_vectors())
        self.assertNotIn(b"\r", a)
        self.assertTrue(a.endswith(b"\n"))
        doc = json.loads(a.decode("utf-8"))
        self.assertEqual((doc["version"], doc["cell"]), (1, CELL))

    def test_plan_tiles_unchanged_on_every_case(self):
        self.assertEqual(set(PLAN_TILES_BEFORE), {c[0] for c in ALL})
        for cid, g, doors, fh, band in ALL:
            t = plan_tiles(g, fh, band, doors)
            got = hashlib.sha256(json.dumps([t.wall, t.ground], ensure_ascii=False).encode()).hexdigest()
            self.assertEqual(got, PLAN_TILES_BEFORE[cid], cid)

    def test_cli_check_fails_on_stale_file(self):
        tmp = Path(tempfile.mkdtemp())
        try:
            path = tmp / "room-vectors.json"
            shutil.copy(room_vectors.VECTORS_PATH, path)
            path.write_bytes(path.read_bytes().replace(b'"band"', b'"BAND"', 1))       # 규칙만 고치고 표를 다시 만들지 않은 것과 같다
            with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
                self.assertEqual(cli.vectors_command(check=True, path=path), 1)
                self.assertEqual(cli.vectors_command(check=False, path=path), 0)
                self.assertEqual(path.read_bytes(), room_vectors.VECTORS_PATH.read_bytes())
                self.assertEqual(cli.vectors_command(check=True, path=path), 0)
        finally:
            shutil.rmtree(tmp)


if __name__ == "__main__":
    unittest.main()
