"""LDtk 파일과 맵 목록을 쓰는 모듈들(ldtk_test_level, room_ldtk)이 같이 쓰는 도우미.

게임의 maps.json은 // 주석과 끝 쉼표가 있어 일반 JSON 읽기로는 못 읽는다. walker·npcWalker는 거기서 옮겨 온다.
"""
import json
import os
import re
from pathlib import Path
from typing import List, Tuple

SOURCE_MAPS = "game/world/maps.json"      # walker·npcWalker를 옮겨 올 곳(저장소 뿌리 기준)

_JSON_TOKENS = re.compile(r'"(?:[^"\\]|\\.)*"|//[^\n]*|/\*.*?\*/', re.S)
_TRAILING_COMMA = re.compile(r'"(?:[^"\\]|\\.)*"|,(?=\s*[}\]])', re.S)


def loads_with_comments(text: str):
    """게임의 maps.json처럼 // 주석과 끝 쉼표가 있는 JSON을 읽는다(문자열 안은 건드리지 않는다)."""
    text = text.lstrip("﻿")
    text = _JSON_TOKENS.sub(lambda m: m.group(0) if m.group(0).startswith('"') else "", text)
    text = _TRAILING_COMMA.sub(lambda m: m.group(0) if m.group(0).startswith('"') else "", text)
    return json.loads(text)


def walkers(root: Path, error=Exception) -> dict:
    """게임 맵 목록의 walker·npcWalker. 못 읽으면 error(메시지)를 낸다."""
    try:
        src = loads_with_comments((root / SOURCE_MAPS).read_text(encoding="utf-8"))
    except (OSError, ValueError) as e:
        raise error(f"{SOURCE_MAPS}를 못 읽었다({e})") from None
    npc = src.get("npcWalker")
    if not isinstance(npc, dict) or "speedCellsPerSecond" not in npc:
        raise error(f"{SOURCE_MAPS}에 npcWalker.speedCellsPerSecond가 없다(게임이 이 값 없이는 맵 목록을 거절한다)")
    out = {}
    if "walker" in src:
        out["walker"] = src["walker"]
    out["npcWalker"] = npc
    return out


def dump(doc) -> bytes:
    """결정적인 JSON(같은 입력 → 같은 바이트). 들여쓰기 1칸, LF."""
    return (json.dumps(doc, ensure_ascii=False, indent=1) + "\n").encode("utf-8")


def write_all(root: Path, files: List[Tuple[Path, bytes]]) -> List[str]:
    """(경로, 내용)을 모두 임시 파일에 쓴 뒤에야 바꿔치기한다. 같은 내용이면 건너뛴다. 바뀐 파일(뿌리 기준 경로)을 돌려준다."""
    todo = []
    for path, data in files:
        try:
            if path.read_bytes() == data:
                continue
        except OSError:
            pass
        todo.append((path, data))
    staged = []
    try:
        for path, data in todo:
            path.parent.mkdir(parents=True, exist_ok=True)
            tmp = path.with_name(path.name + ".tmp")
            staged.append((tmp, path))
            tmp.write_bytes(data)
    except OSError:
        for tmp, _ in staged:
            try:
                tmp.unlink()
            except OSError:
                pass
        raise
    done = []
    for tmp, path in staged:
        os.replace(tmp, path)
        done.append(path.relative_to(root).as_posix())
    return done
