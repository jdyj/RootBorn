"""방 만들기 — 시험 표 room-vectors.json: 방 격자마다 칸의 종류, 열쇠, 픽셀 출처표(room_tiles.plan_sources).

게임(C#, Rootborn.Core.Building)이 같은 고르기 규칙을 따로 갖고 이 표로 같은지 본다. 꼴은 README "방 만들기 — 시험 표".
사례는 여기 글로 둔다 — 방 평면 파일이 바뀌어도 시험 표가 흔들리지 않게. 표는 `python Tools/pixel-kit/kit.py room vectors`로
다시 쓰고 손으로 고치지 않는다(규칙을 고치면 다시 만들어 함께 커밋한다).
"""
import json
import random
from pathlib import Path
from typing import Dict, List, Sequence, Tuple

from .room_plan import cells
from .room_tiles import CELL, SOURCES, SourcePart, classify, part_order, plan_room  # noqa: F401 (part_order: 시험이 쓴다)

VECTORS_PATH = Path(__file__).resolve().parents[1] / "tests" / "fixtures" / "room-vectors.json"
FACE_HEIGHT = 2
BAND = 16
ABOUT = "방 격자 → 칸마다 열쇠와 픽셀 출처. 만든 곳: python Tools/pixel-kit/kit.py room vectors. 손으로 고치지 않는다."
VERSION = 1

# (id, 평면 줄, 벽 칸의 문 칸들, 정면 높이, 띠 두께). 평면: '#' 벽, '.' 바닥, ' ' 밖. 정면 높이·띠 두께를 적지 않은 사례는
# FACE_HEIGHT·BAND(_case가 채운다).
Case = Tuple[str, Sequence[str], Sequence[Tuple[int, int]], int, int]


def _case(cid: str, rows: Sequence[str], doors: Sequence[Tuple[int, int]],
          face_height: int = FACE_HEIGHT, band: int = BAND) -> Case:
    return (cid, rows, doors, face_height, band)


def sill_keys(band: int = BAND) -> List[str]:
    """띠 두께 band의 문지방 열쇠 9가지: 바닥이 있는 쪽(n·e·w) × 띠가 칸 안에 놓이는 범위(위·왼 끝, 가운데, 아래·오른 끝).
    띠 16에서는 조각 시트의 common/sill:*(room_export.sill_pieces)와 같은 목록이어야 한다 — 시험이 견준다. 그중 셋은 어떤 격자로도
    나오지 않는다(UNREACHABLE_SILLS)."""
    return [f"sill:{s}:{a}-{a + band}" for s in "new" for a in (0, (CELL - band) // 2, CELL - band)]

_PLANS = [
    # 새 집(art/kit/rooms/house.json의 plan, 문 글자 D는 벽 + 문 칸) — 2026-11-14 커밋된 것의 사본
    ("house", [
        "############################",
        "############################",
        "############################",
        "#............#.............#",
        "#............#.............#",
        "#............#.............#",
        "#............#.............#",
        "#............#######.#######",
        "#............#######.#######",
        "#............#######.#######",
        "######.#######.............#",
        "######.#######.............#",
        "######.#######.............#",
        "#............#.............#",
        "#..........................#",
        "#..........................#",
        "#............#.............#",
        "############################",
    ], [(14, 17)]),
    # 시험 방(art/kit/rooms/test.two-rooms.json의 plan) — 2026-11-14 커밋된 것의 사본
    ("test-two-rooms", [
        "####################",
        "####################",
        "####################",
        "#........#.........#",
        "#........#.........#",
        "#........#.........#",
        "#..................#",
        "#........#.........#",
        "#........#.........#",
        "######...######....#",
        "######...######....#",
        "######...######....#",
        "#........#.........#",
        "####################",
    ], []),
    # 두 방 사이 세로 벽: 띠 칸의 왼쪽·오른쪽 비침은 서로 다른 방의 바닥(Review Focus 3)
    ("two-rooms-wall", [
        "#######",
        "#######",
        "#######",
        "#..#..#",
        "#..#..#",
        "#..#..#",
        "#######",
    ], []),
    ("tee", [
        "#########",
        "#########",
        "#########",
        "#...#...#",
        "#...#...#",
        "#########",
        "#.......#",
        "#.......#",
        "#########",
    ], []),
    ("cross", [
        "#########",
        "#########",
        "#########",
        "#...#...#",
        "#...#...#",
        "#########",
        "#...#...#",
        "#...#...#",
        "#########",
    ], []),
    # 방 안에서 끝나는 세로 벽(남쪽 끝은 좁은 정면) + 위로 솟은 벽 끝
    ("wall-end", [
        "#########",
        "#########",
        "#########",
        "#...#...#",
        "#...#...#",
        "#.......#",
        "#.......#",
        "#..#....#",
        "#..#....#",
        "#########",
    ], []),
    ("block-3x3", [
        "#########",
        "#########",
        "#########",
        "#.......#",
        "#..###..#",
        "#..###..#",
        "#..###..#",
        "#.......#",
        "#.......#",
        "#########",
    ], []),
    # 가로 벽과 세로 벽의 문간(벽이 끊긴 자리)
    ("doorway", [
        "##########",
        "##########",
        "##########",
        "#...#....#",
        "#........#",
        "#...#....#",
        "##.#####.#",
        "##.#####.#",
        "#........#",
        "#........#",
        "##########",
    ], []),
    # 얇은 가로 벽: 한 줄(정면 1칸뿐, 위가 열림)과 두 줄(정면 2칸, 윗면 없음)
    ("thin-wall-1", [
        "#######",
        "#######",
        "#######",
        "#.....#",
        "#.###.#",
        "#.....#",
        "#######",
    ], []),
    ("thin-wall-2", [
        "#######",
        "#######",
        "#######",
        "#.....#",
        "#.###.#",
        "#.###.#",
        "#.....#",
        "#######",
    ], []),
    # 맵 밖(빈칸)과 격자 가장자리에 닿은 벽, ㄱ자 방
    ("edge", [
        "   #####",
        "   #####",
        "   #####",
        "####...#",
        "####...#",
        "####...#",
        "#......#",
        "#......#",
        "########",
    ], []),
    # 벽 칸의 문 네 방향: 아래 벽(바닥이 위 n), 왼쪽 벽(바닥이 오른쪽 e), 오른쪽 벽(바닥이 왼쪽 w), 위 벽의 정면(문짝)
    ("wall-doors", [
        "#########",
        "#########",
        "#########",
        "#.......#",
        "#.......#",
        "#.......#",
        "#.......#",
        "#########",
    ], [(4, 7), (0, 4), (8, 5), (3, 2)]),
    # 세로 벽 칸의 문과 T자 옆의 문(이웃 띠의 끝이 닫힌다)
    ("wall-doors-inner", [
        "#########",
        "#########",
        "#########",
        "#...#...#",
        "#...#...#",
        "#...#...#",
        "#########",
    ], [(4, 4), (2, 6), (6, 2)]),
    # 드문 모양(무작위로 찾은 작은 격자 — 평면 검사는 통과하지 않는다). 벽 그림이 덮지 않는데 이웃 바닥에서 오지 않은 비침
    # 픽셀이 생긴다: 문지방 칸의 남은 띠 자리·늘인 정면 자리, 좁은 정면 옆의 빈 자리. 그 주인 규칙(README)을 시험 표에 남긴다.
    ("odd-1", [
        "#.##.",
        ".#. .",
        "## # ",
        "# .# ",
        " #.#.",
        "## . ",
    ], [(1, 1), (0, 3)]),
    ("odd-2", [
        "###.",
        "#. #",
        "###.",
        "...#",
        "# .#",
        " ###",
    ], [(2, 0), (0, 2)]),
]
_ROWS = {cid: rows for cid, rows, _ in _PLANS}

# 문지방 열쇠 전부가 나오게 짠 격자(띠 16): 왼쪽 바깥 벽(e 오른끝), 두 방 사이 세로 벽(e 가운데), 아래 바깥 벽(n 위끝), 안쪽 가로 벽의
# 윗면(n 아래끝 — 아래가 정면), 가로 벽 정면이 붙은 세로 벽 칸(w 가운데 — 오른쪽이 정면이라 위·오른쪽에 바닥이 없다), 오른쪽 바깥 벽(w 위끝).
SILLS_ALL = (["#########",
              "#########",
              "#########",
              "#...#...#",
              "#...#...#",
              "#...#####",
              "#...#####",
              "#...#####",
              "#...#...#",
              "#...#...#",
              "#########"],
             [(4, 3), (0, 4), (6, 5), (4, 6), (8, 8), (2, 10)])

# 사례 머리에 "sills"(그 띠 두께에서 있을 수 있는 문지방 열쇠 목록, 조각 시트의 common/sill:*와 같다)를 적는 사례.
SILL_CASES = ("sills-all",)

# 띠 16의 문지방 9가지 중 어떤 격자로도 나오지 않는 셋. 바닥이 있는 쪽 축의 띠 자리는 그 쪽 끝이나 가운데뿐이다(_band_layout:
# 그 쪽이 방이면 axis가 그 쪽 끝·가운데를 주고, 이웃을 따를 때도 양쪽이 방인 가운데만 받는다. 위가 바닥인 윗면은 위끝, 아래가 정면이면
# 아래끝). 그래서 위가 바닥인데 가운데(n 8-24), 오른쪽이 바닥인데 왼끝(e 0-16), 왼쪽이 바닥인데 오른끝(w 16-32)은 없다.
# 조각 시트는 9가지를 다 굽는다(room_export.sill_pieces). 셋 중 하나라도 시험 표에 나오면 시험이 알린다(이 목록을 고친다).
UNREACHABLE_SILLS = ("sill:n:8-24", "sill:e:0-16", "sill:w:16-32")

FIXED: List[Case] = [_case(*c) for c in _PLANS]

# 정면 높이·띠 두께가 다른 사례와 문지방 전부(무작위 사례 뒤에 둔다 — 앞 사례의 순서가 그대로이게)
VARIANTS: List[Case] = [
    # 정면 높이가 다른 집(새 집 평면의 사본): 정면 1칸뿐, 정면 3칸(face:3 조각 — 지금 조각 시트에는 없다)
    _case("face-height-1", _ROWS["house"], [(14, 17)], face_height=1),
    _case("face-height-3", _ROWS["house"], [(14, 17)], face_height=3),
    # 띠 두께가 다른 시험 방(시험 방 평면의 사본) — 조각 시트는 띠 16만 받는다
    _case("band-8", _ROWS["test-two-rooms"], [], band=8),
    _case("band-24", _ROWS["test-two-rooms"], [], band=24),
    _case("sills-all", *SILLS_ALL),
]

RANDOM_SEED = 20261114
RANDOM_COUNT = 8


def random_cases(seed: int = RANDOM_SEED, count: int = RANDOM_COUNT) -> List[Case]:
    """14x10 바깥 벽 안에 길이 2~6의 가로·세로 벽 1~5개. 씨앗을 고정해 늘 같은 격자다."""
    rng = random.Random(seed)
    out: List[Case] = []
    for n in range(count):
        w, h = 14, 10
        g = [["#" if x in (0, w - 1) or y in (0, h - 1) else "." for x in range(w)] for y in range(h)]
        for _ in range(rng.randint(1, 5)):
            length = rng.randint(2, 6)
            if rng.random() < 0.5:
                x0, y0 = rng.randint(1, w - 1 - length), rng.randint(1, h - 2)
                for x in range(x0, x0 + length):
                    g[y0][x] = "#"
            else:
                x0, y0 = rng.randint(1, w - 2), rng.randint(1, h - 1 - length)
                for y in range(y0, y0 + length):
                    g[y][x0] = "#"
        out.append(_case(f"random-{n + 1}", ["".join(r) for r in g], []))
    return out


def all_cases() -> List[Case]:
    return FIXED + random_cases() + VARIANTS


def case_grid(rows: Sequence[str]) -> List[List[int]]:
    return cells(rows)


def _part_json(part: SourcePart) -> dict:
    out = {"src": part.src}
    if part.shade is not None:
        out["shade"] = part.shade
    if part.piece is not None:
        out["piece"] = part.piece
    if part.own is not None:
        out["own"] = list(part.own)
    out["px"] = part.px
    return out


def _case_json(cid: str, rows: Sequence[str], doors: Sequence[Tuple[int, int]], face_height: int, band: int) -> dict:
    g = case_grid(rows)
    kinds = classify(g, face_height)
    tiles, sources = plan_room(g, face_height, band, doors)
    cells_out = []
    for y, row in enumerate(sources):
        for x, parts in enumerate(row):
            if parts is None:
                continue
            cells_out.append({"x": x, "y": y, "kind": kinds[y][x], "wall": tiles.wall[y][x], "ground": tiles.ground[y][x],
                              "parts": [_part_json(p) for p in parts]})
    out = {"id": cid, "faceHeight": face_height, "band": band, "grid": list(rows), "doors": [list(d) for d in sorted(doors)]}
    if cid in SILL_CASES:
        out["sills"] = sill_keys(band)
    out["cells"] = cells_out
    return out


def _line(value) -> str:
    return json.dumps(value, ensure_ascii=False)


def build_vectors() -> bytes:
    """시험 표의 바이트. 같은 코드면 같은 바이트(키 순서 고정, LF, 끝 줄바꿈, ensure_ascii=False).

    줄 나눔: 머리 값은 한 줄에 하나, 사례는 머리 한 줄 + 칸 하나가 한 줄. 규칙을 고쳤을 때 바뀐 칸이 diff에서 한 줄로 보이고,
    파일이 indent=1 통째보다 4할쯤 작다. 읽는 쪽에는 그냥 JSON이다."""
    head = {"about": ABOUT, "version": VERSION, "cell": CELL,
            "sources": {name: {"field": field, "own": owned} for name, (field, owned) in SOURCES.items()}}
    out = ["{"] + [f" {_line(k)}: {_line(v)}," for k, v in head.items()] + [' "cases": [']
    cases = [_case_json(*c) for c in all_cases()]
    for n, case in enumerate(cases):
        out.append("  {" + ", ".join(f"{_line(k)}: {_line(v)}" for k, v in case.items() if k != "cells") + ', "cells": [')
        out += ["   " + _line(c) + ("," if i + 1 < len(case["cells"]) else "") for i, c in enumerate(case["cells"])]
        out.append("  ]}" + ("," if n + 1 < len(cases) else ""))
    out += [" ]", "}"]
    return ("\n".join(out) + "\n").encode("utf-8")


def check_vectors(path: Path = VECTORS_PATH) -> bool:
    """path의 시험 표가 지금 규칙으로 다시 만든 것과 바이트까지 같은가."""
    return path.is_file() and path.read_bytes() == build_vectors()


def write_vectors(path: Path = VECTORS_PATH) -> bool:
    """시험 표를 다시 쓴다. 바뀐 것이 있으면 True."""
    data = build_vectors()
    if path.is_file() and path.read_bytes() == data:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    return True
