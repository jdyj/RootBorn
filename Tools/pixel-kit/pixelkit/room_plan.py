"""방 만들기 — 글자 평면(art/kit/rooms/<id>.json) 읽기와 검사.

평면 글자: `#` 벽, `.` 바닥, 빈칸 밖. `doors`에 적은 글자는 바닥 + 문(그 칸에 Door 엔티티가 놓인다).
검사(check_grid)는 쓰기 전에 문제를 알린다. 쓰기는 room_ldtk가 맡는다.
"""
import json
import re
from collections import deque
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Sequence, Tuple

from .lint import Problem
from .room_tiles import FLOOR, OUT, WALL, classify

CHARS = {"#": WALL, ".": FLOOR, " ": OUT}
WARNING_CODES = frozenset({"room-wall-thin", "room-door-unregistered"})     # 알림이다: 찍되 쓰기를 막지 않고 끝 코드도 0
_ID = re.compile(r"[A-Za-z0-9._-]+")


class RoomError(Exception):
    """평면 파일을 읽을 수 없다(없음, JSON 아님, 모양이 틀림). 끝 코드 2."""


@dataclass
class Room:
    id: str
    wall: str
    floor: str
    grid: List[str]                        # 평면 줄. 문 글자는 바닥('.')으로 바뀌어 있다
    spawn: Tuple[int, int]                 # 칸 (x, y)
    doors: Dict[Tuple[int, int], str]      # 칸 (x, y) → 목적지 값(Door 엔티티의 Where)
    face_height: int = 2


def load_room(path) -> Room:
    path = Path(path)
    try:
        doc = json.loads(path.read_text(encoding="utf-8"))
    except OSError as e:
        raise RoomError(f"{path}를 못 읽었다({e})") from None
    except ValueError as e:
        raise RoomError(f"{path}가 JSON이 아니다({e})") from None
    if not isinstance(doc, dict):
        raise RoomError(f"{path}: 맨 바깥이 객체여야 한다")

    def need(key, check, what):
        if key not in doc or not check(doc[key]):
            raise RoomError(f"{path}: {key}는 {what}여야 한다(지금 {doc.get(key)!r})")
        return doc[key]

    def is_int(v):
        return type(v) is int

    rid = need("id", lambda v: isinstance(v, str) and _ID.fullmatch(v) is not None, "글자·숫자·점·하이픈·밑줄로 된 이름")
    if rid != path.stem:
        raise RoomError(f"{path}: id {rid!r}가 파일 이름 {path.stem!r}과 다르다")
    wall = need("wall", lambda v: isinstance(v, str) and v, "벽 모양 이름")
    floor = need("floor", lambda v: isinstance(v, str) and v, "바닥 타일 명세 id")
    plan = need("plan", lambda v: isinstance(v, list) and v and all(isinstance(r, str) for r in v), "글자 줄의 배열")
    spawn = need("spawn", lambda v: isinstance(v, list) and len(v) == 2 and all(is_int(c) for c in v), "[x, y] 칸")
    raw_doors = need("doors", lambda v: isinstance(v, dict), "글자 → 목적지 값 표") if "doors" in doc else {}
    face_height = need("face_height", lambda v: is_int(v) and v >= 1, "1 이상의 정수") if "face_height" in doc else 2

    for letter, dest in raw_doors.items():
        if len(letter) != 1 or letter in CHARS:
            raise RoomError(f"{path}: 문 글자 {letter!r}는 '#', '.', 빈칸이 아닌 한 글자여야 한다")
        if not isinstance(dest, str) or not dest:
            raise RoomError(f"{path}: 문 {letter!r}의 목적지 값은 비어 있지 않은 글자여야 한다")
    doors: Dict[Tuple[int, int], str] = {}
    lines = []
    for y, row in enumerate(plan):
        chars = []
        for x, ch in enumerate(row):
            if ch in raw_doors:
                doors[(x, y)] = raw_doors[ch]
                ch = "."
            chars.append(ch)
        lines.append("".join(chars))
    missing = sorted(set(raw_doors) - {ch for row in plan for ch in row})
    if missing:
        raise RoomError(f"{path}: 평면에 없는 문 글자 {missing}")
    return Room(rid, wall, floor, lines, (spawn[0], spawn[1]), doors, face_height)


def cells(grid: Sequence) -> List[List[int]]:
    """평면 줄(글자) 또는 값 격자를 값(OUT/WALL/FLOOR) 격자로. 모르는 글자는 KeyError."""
    return [[CHARS[c] if isinstance(c, str) else c for c in row] for row in grid]


def _is_thin(key) -> bool:
    parts = key.split(":") if key else []
    return len(parts) == 3 and parts[0] == "face" and "t" in parts[2]


def check_grid(grid: Sequence, spawn: Tuple[int, int], doors: Dict[Tuple[int, int], str], face_height: int) -> List[Problem]:
    """평면(글자 줄 또는 값 격자)을 검사한다. 쓰지 않는다. room-wall-thin만 알림(WARNING_CODES)이다."""
    problems: List[Problem] = []
    widths = {len(row) for row in grid}
    if len(widths) > 1:
        problems.append(Problem("room-ragged-plan", "평면 줄 길이가 다르다: " + ", ".join(
            f"{y}번째 {len(row)}칸" for y, row in enumerate(grid))))
        return problems          # 칸 위치가 믿을 수 없으니 더 보지 않는다(조용히 채우지 않는다)
    for y, row in enumerate(grid):
        for x, ch in enumerate(row):
            if (ch not in CHARS) if isinstance(ch, str) else (ch not in (OUT, WALL, FLOOR) or isinstance(ch, bool)):
                problems.append(Problem("room-unknown-char", f"모르는 칸 값 {ch!r} (({x}, {y}))", x, y))
    if problems:
        return problems
    g = cells(grid)
    h, w = len(g), (len(g[0]) if g else 0)

    def at(x, y):
        return g[y][x] if 0 <= y < h and 0 <= x < w else OUT

    for y in range(h):
        for x in range(w):
            if g[y][x] == FLOOR and any(at(x + dx, y + dy) == OUT for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                problems.append(Problem("room-open-edge", f"바닥 ({x}, {y})이 맵 가장자리나 밖 칸에 닿아 열려 있다", x, y))
    for (x, y), dest in sorted(doors.items()):
        if at(x, y) != FLOOR:
            problems.append(Problem("room-door-in-wall", f"문 ({x}, {y}){dest!r}이 바닥이 아닌 칸에 있다", x, y))
    sx, sy = spawn
    if at(sx, sy) != FLOOR:
        problems.append(Problem("room-spawn-in-wall", f"도착 지점 ({sx}, {sy})이 바닥이 아닌 칸에 있다", sx, sy))
    else:
        seen = {(sx, sy)}
        queue = deque([(sx, sy)])
        while queue:
            x, y = queue.popleft()
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                n = (x + dx, y + dy)
                if n not in seen and at(*n) == FLOOR:
                    seen.add(n)
                    queue.append(n)
        # 닿지 못하는 바닥은 덩어리마다 하나로 알린다(첫 칸 위치).
        left = {(x, y) for y in range(h) for x in range(w) if g[y][x] == FLOOR} - seen
        while left:
            start = min(left, key=lambda c: (c[1], c[0]))
            part, queue = {start}, deque([start])
            while queue:
                x, y = queue.popleft()
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    n = (x + dx, y + dy)
                    if n in left and n not in part:
                        part.add(n)
                        queue.append(n)
            left -= part
            problems.append(Problem("room-unreachable-floor",
                                    f"도착 지점 ({sx}, {sy})에서 걸어 닿지 못하는 바닥 {len(part)}칸(첫 칸 ({start[0]}, {start[1]}))",
                                    start[0], start[1]))
    # 얇은 벽: 위가 윗면이 아닌 정면(열쇠에 t 표시)이 가로로 이어진 줄
    keys = classify(g, face_height)
    for y, row in enumerate(keys):
        x = 0
        while x < len(row):
            if _is_thin(row[x]):
                end = x
                while end + 1 < len(row) and _is_thin(row[end + 1]):
                    end += 1
                problems.append(Problem("room-wall-thin",
                                        f"벽 ({x}..{end}, {y})이 얇아 윗면 없이 정면만 남는다(정면 {face_height}칸 + 윗면 1칸이 필요)", x, y))
                x = end
            x += 1
    return problems


def is_warning(problem: Problem) -> bool:
    return problem.code in WARNING_CODES
