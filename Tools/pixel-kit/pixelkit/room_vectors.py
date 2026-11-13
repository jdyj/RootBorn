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

# (id, 평면 줄, 벽 칸의 문 칸들). 평면: '#' 벽, '.' 바닥, ' ' 밖.
Case = Tuple[str, Sequence[str], Sequence[Tuple[int, int]]]

FIXED: List[Case] = [
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
        out.append((f"random-{n + 1}", ["".join(r) for r in g], []))
    return out


def all_cases() -> List[Case]:
    return FIXED + random_cases()


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


def _case_json(cid: str, rows: Sequence[str], doors: Sequence[Tuple[int, int]]) -> dict:
    g = case_grid(rows)
    kinds = classify(g, FACE_HEIGHT)
    tiles, sources = plan_room(g, FACE_HEIGHT, BAND, doors)
    cells_out = []
    for y, row in enumerate(sources):
        for x, parts in enumerate(row):
            if parts is None:
                continue
            cells_out.append({"x": x, "y": y, "kind": kinds[y][x], "wall": tiles.wall[y][x], "ground": tiles.ground[y][x],
                              "parts": [_part_json(p) for p in parts]})
    return {"id": cid, "faceHeight": FACE_HEIGHT, "band": BAND, "grid": list(rows),
            "doors": [list(d) for d in sorted(doors)], "cells": cells_out}


def _line(value) -> str:
    return json.dumps(value, ensure_ascii=False)


def build_vectors() -> bytes:
    """시험 표의 바이트. 같은 코드면 같은 바이트(키 순서 고정, LF, 끝 줄바꿈, ensure_ascii=False).

    줄 나눔: 머리 값은 한 줄에 하나, 사례는 머리 한 줄 + 칸 하나가 한 줄. 규칙을 고쳤을 때 바뀐 칸이 diff에서 한 줄로 보이고,
    파일이 indent=1 통째보다 4할쯤 작다. 읽는 쪽에는 그냥 JSON이다."""
    head = {"about": ABOUT, "version": VERSION, "cell": CELL,
            "sources": {name: {"field": field, "own": owned} for name, (field, owned) in SOURCES.items()}}
    out = ["{"] + [f" {_line(k)}: {_line(v)}," for k, v in head.items()] + [' "cases": [']
    cases = [_case_json(cid, rows, doors) for cid, rows, doors in all_cases()]
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
