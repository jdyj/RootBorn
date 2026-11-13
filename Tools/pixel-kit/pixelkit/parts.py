"""점 부품 읽기: 픽셀 행(글자 하나 = 팔레트 키 하나)을 외곽선까지 입힌 Canvas로.

부품 파일은 폴더 안의 *.json, 각 파일은 부품 배열이다.
  { "id": "...", "anchor": "bottom-center", "rows": ["..AB", ...], "keys": {"A": "bread.1"} }
'.'은 투명. 줄 길이가 다르면 짧은 줄의 끝은 투명으로 채운다.
"""
import json
from pathlib import Path
from typing import Dict, FrozenSet

from .canvas import Canvas
from .style import Style

CLEAR_CHAR = "."


class PartError(Exception):
    """부품 파일이 잘못됐다. 메시지에 파일과 부품 id가 든다."""


def _build(part: dict, style: Style, where: str) -> Canvas:
    pid = part.get("id")
    if not isinstance(pid, str) or not pid:
        raise PartError(f"{where}: id가 없는 부품")
    rows = part.get("rows")
    keys = part.get("keys")
    if not isinstance(rows, list) or not rows or not all(isinstance(r, str) for r in rows):
        raise PartError(f"{where}: {pid}: rows는 글자열 배열이어야 한다")
    if not isinstance(keys, dict):
        raise PartError(f"{where}: {pid}: keys가 없다")
    colors = {}
    for ch, key in keys.items():
        try:
            colors[ch] = style.color(key)
        except KeyError:
            raise PartError(f"{where}: {pid}: 팔레트에 없는 키 {key} (글자 {ch})") from None
    w = max(len(r) for r in rows)
    h = len(rows)
    # 외곽선이 들어갈 1px 여유를 사방에 둔다.
    cv = Canvas(w + 2, h + 2)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == CLEAR_CHAR:
                continue
            if ch not in colors:
                raise PartError(f"{where}: {pid}: keys에 없는 글자 {ch} ({y}줄 {x}칸)")
            cv.px(x + 1, y + 1, colors[ch])
    if cv.bbox() is None:
        raise PartError(f"{where}: {pid}: 빈 부품")
    return cv.outline(style.outline)


def load_parts(directory: str, style: Style) -> Dict[str, Canvas]:
    """폴더의 부품 파일을 모두 읽어 id → Canvas. 파일 순서는 이름순(결정적)."""
    out: Dict[str, Canvas] = {}
    origin: Dict[str, str] = {}
    for path in sorted(Path(directory).glob("*.json")):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            raise PartError(f"{path.name}: JSON 읽기 실패: {e}") from None
        if not isinstance(data, list):
            raise PartError(f"{path.name}: 파일은 부품 배열이어야 한다")
        for part in data:
            if not isinstance(part, dict):
                raise PartError(f"{path.name}: 부품은 객체여야 한다")
            cv = _build(part, style, path.name)
            pid = part["id"]
            if pid in out:
                raise PartError(f"{path.name}: {pid}: id가 겹친다({origin[pid]}에도 있다)")
            out[pid] = cv
            origin[pid] = path.name
    return out


def load_part_allowances(directory: str) -> Dict[str, FrozenSet[str]]:
    """부품 id → 검사에서 풀어 줄 코드 집합(부품 JSON의 "allow"). 없으면 빈 집합.
    의도한 외톨이 점(손잡이, 씨앗)만 이렇게 푼다."""
    out: Dict[str, FrozenSet[str]] = {}
    for path in sorted(Path(directory).glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        for part in data:
            out[part["id"]] = frozenset(part.get("allow", []))
    return out
