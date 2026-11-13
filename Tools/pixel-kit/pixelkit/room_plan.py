"""방 만들기 — 글자 평면(art/kit/rooms/<id>.json) 읽기와 검사.

평면 글자: `#` 벽, `.` 바닥, 빈칸 밖. `doors`에 적은 글자는 바닥 + 문(그 칸에 Door 엔티티가 놓인다).
`wallDoors`에 적은 글자는 벽 + 문(바깥 벽 칸에 다는 현관 문 — 그 칸은 벽으로 남고 Door 엔티티가 놓인다).
`build`는 평면과 같은 크기의 `0`·`1` 줄(`1` 지을 수 있음, `0` 고정) — 있으면 LDtk에 `Build` 층이 생긴다.
`frameId`는 `build`가 있는 평면에 필수인 테두리의 판 이름(맵을 다시 그리면 올리는 값) — 방 폴더 maps.json의 `build`에 쓴다.
검사(check_grid)는 쓰기 전에 문제를 알린다. 쓰기는 room_ldtk가 맡는다.
"""
import json
import re
from collections import deque
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple

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
    top: Optional[str] = None              # 띠 색 줄(없으면 벽 모양의 기본)
    wall_doors: Dict[Tuple[int, int], str] = field(default_factory=dict)    # 벽 칸 (x, y) → 목적지 값. 그 칸은 grid에서 벽('#')이다
    build: Optional[List[str]] = None      # 지을 수 있는 칸 줄('1' 지을 수 있음, '0' 고정). 없으면 Build 층을 만들지 않는다
    frame_id: Optional[str] = None         # 테두리의 판 이름(build가 있을 때 필수 — plan_problems). 게임 maps.json의 build.frameId


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
    top = need("top", lambda v: isinstance(v, str) and v, "띠 색 줄 이름") if "top" in doc else None
    raw_wall_doors = need("wallDoors", lambda v: isinstance(v, dict), "글자 → 목적지 값 표") if "wallDoors" in doc else {}
    build = need("build", lambda v: isinstance(v, list) and v and all(isinstance(r, str) for r in v), "0·1 글자 줄의 배열") if "build" in doc else None
    frame_id = need("frameId", lambda v: isinstance(v, str) and v.strip() != "", "비어 있지 않은 판 이름 글") if "frameId" in doc else None

    for letter, dest in raw_doors.items():
        if len(letter) != 1 or letter in CHARS:
            raise RoomError(f"{path}: 문 글자 {letter!r}는 '#', '.', 빈칸이 아닌 한 글자여야 한다")
        if not isinstance(dest, str) or not dest:
            raise RoomError(f"{path}: 문 {letter!r}의 목적지 값은 비어 있지 않은 글자여야 한다")
    for letter, dest in raw_wall_doors.items():
        if len(letter) != 1 or letter in CHARS or letter in raw_doors:
            raise RoomError(f"{path}: 벽 문 글자 {letter!r}는 '#', '.', 빈칸이나 doors의 글자가 아닌 한 글자여야 한다")
        if not isinstance(dest, str) or not dest:
            raise RoomError(f"{path}: 벽 문 {letter!r}의 목적지 값은 비어 있지 않은 글자여야 한다")
    doors: Dict[Tuple[int, int], str] = {}
    wall_doors: Dict[Tuple[int, int], str] = {}
    lines = []
    for y, row in enumerate(plan):
        chars = []
        for x, ch in enumerate(row):
            if ch in raw_doors:
                doors[(x, y)] = raw_doors[ch]
                ch = "."
            elif ch in raw_wall_doors:
                wall_doors[(x, y)] = raw_wall_doors[ch]
                ch = "#"
            chars.append(ch)
        lines.append("".join(chars))
    used = {ch for row in plan for ch in row}
    missing = sorted((set(raw_doors) | set(raw_wall_doors)) - used)
    if missing:
        raise RoomError(f"{path}: 평면에 없는 문 글자 {missing}")
    return Room(rid, wall, floor, lines, (spawn[0], spawn[1]), doors, face_height, top, wall_doors, build, frame_id)


def cells(grid: Sequence) -> List[List[int]]:
    """평면 줄(글자) 또는 값 격자를 값(OUT/WALL/FLOOR) 격자로. 모르는 글자는 KeyError."""
    return [[CHARS[c] if isinstance(c, str) else c for c in row] for row in grid]


def _is_thin(key) -> bool:
    parts = key.split(":") if key else []
    return len(parts) == 3 and parts[0] == "face" and "t" in parts[2]


def check_grid(grid: Sequence, spawn: Tuple[int, int], doors: Dict[Tuple[int, int], str], face_height: int,
               wall_doors: Optional[Dict[Tuple[int, int], str]] = None, build: Optional[Sequence[str]] = None) -> List[Problem]:
    """평면(글자 줄 또는 값 격자)을 검사한다. 쓰지 않는다. room-wall-thin만 알림(WARNING_CODES)이다.
    wall_doors는 벽 칸에 다는 문(칸 → 목적지), build는 평면과 같은 크기의 '0'·'1' 줄(없으면 그 검사는 하지 않는다)."""
    wall_doors = wall_doors or {}
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
    for (x, y), dest in sorted(wall_doors.items()):
        if at(x, y) != WALL:
            problems.append(Problem("room-door-in-wall", f"벽 문 ({x}, {y}){dest!r}이 벽 칸이 아닌 곳에 있다", x, y))
    sx, sy = spawn
    seen = set()
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
    if at(sx, sy) == FLOOR:
        for (x, y), dest in sorted(wall_doors.items()):
            if at(x, y) == WALL and not any((x + dx, y + dy) in seen for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                problems.append(Problem("room-door-no-floor",
                                        f"벽 문 ({x}, {y}){dest!r} 둘레에 도착 지점 ({sx}, {sy})에서 걸어 닿는 바닥 칸이 없다", x, y))
    if build is not None:
        problems += _check_build(build, g, spawn, doors, wall_doors, at)
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


def _check_build(build: Sequence[str], g, spawn, doors, wall_doors, at) -> List[Problem]:
    """Build 줄(평면과 같은 크기의 '0'·'1')을 검사한다: 크기, 글자, 고정이어야 할 칸(밖, 도착 지점, 문, 벽 문과 그 둘레 바닥)."""
    h, w = len(g), (len(g[0]) if g else 0)
    if len(build) != h or any(not isinstance(r, str) or len(r) != w for r in build):
        return [Problem("room-build-ragged", f"build 줄이 평면({w}칸 x {h}줄)과 크기가 다르다: " + ", ".join(
            f"{y}번째 {len(r)}칸" for y, r in enumerate(build)) + f" (모두 {len(build)}줄)")]
    problems: List[Problem] = []
    for y, row in enumerate(build):
        for x, ch in enumerate(row):
            if ch not in "01":
                problems.append(Problem("room-build-char", f"build의 모르는 글자 {ch!r} (({x}, {y})) — 0과 1만 쓴다", x, y))
    if problems:
        return problems
    fixed = {}
    for y in range(h):
        for x in range(w):
            if g[y][x] == OUT:
                fixed[(x, y)] = "밖 칸"
    fixed[tuple(spawn)] = "도착 지점"
    for cell in doors:
        fixed[cell] = "문 칸"
    for (x, y) in wall_doors:
        fixed[(x, y)] = "벽 문 칸"
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if at(x + dx, y + dy) == FLOOR:
                fixed.setdefault((x + dx, y + dy), "벽 문에 닿은 바닥 칸")
    for (x, y), what in sorted(fixed.items(), key=lambda kv: (kv[0][1], kv[0][0])):
        if 0 <= y < h and 0 <= x < w and build[y][x] == "1":
            problems.append(Problem("room-build-fixed", f"{what} ({x}, {y})이 지을 수 있음(1)이다 — 고정(0)이어야 한다", x, y))
    return problems


def plan_problems(room: Room) -> List[Problem]:
    """평면 하나의 검사: check_grid + frameId 약속(build가 있으면 frameId가 있어야 하고, build가 없으면 frameId를 쓰지 않는다).
    게임은 맵 항목의 build에 frameId를 요구하고 기본값으로 메우지 않는다(game/Rootborn.World/WorldCatalog.cs ParseBuild)."""
    problems = check_grid(room.grid, room.spawn, room.doors, room.face_height, room.wall_doors, room.build)
    if room.build is not None and room.frame_id is None:
        problems.append(Problem("room-build-no-frame-id",
                                "build가 있는 평면에 frameId(테두리의 판 이름, 예: \"<id>.v1\")가 없다 — 게임이 그 방의 maps.json을 거절한다"))
    if room.build is None and room.frame_id is not None:
        problems.append(Problem("room-frame-id-without-build",
                                f"frameId {room.frame_id!r}는 build가 있는 평면에만 쓴다(지을 수 있는 칸이 없는 방에는 판 이름이 없다)"))
    return problems


def is_warning(problem: Problem) -> bool:
    return problem.code in WARNING_CODES
