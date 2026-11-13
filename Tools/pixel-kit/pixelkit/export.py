"""내보내기: 그림을 PNG + 좌표표로 내고, 가구 정의의 그림 참조(sprite·name 두 문자열)만 바꾼다.

계획(ExportPlan)을 먼저 만들고(쓰지 않는다), apply가 문제가 없을 때만 쓴다.
게임 쪽 모양은 data/README.md "그림 좌표표"와 data/interiors/FurnitureDefinition.json이다.
"""
import io
import json
import os
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple

from PIL import Image

from .cli import Kit, draw_and_check
from .lint import Problem

FURNITURE_DEFS = "data/interiors/FurnitureDefinition.json"   # 저장소 뿌리 기준
ATLAS_ROOT = "data/atlases"
PROPS_ROOT = "art/props"
PIXELS_PER_UNIT = 32
CELL = 32
SHEET_WIDTH_CELLS = 16                  # 묶음 시트 폭은 16칸(512px) 고정
TILE_PIVOT = {"x": 0.5, "y": 0.5}       # 타일은 칸 가운데
FOOT_PIVOT = {"x": 0.5, "y": 1.0}       # 건물·바깥 소품은 발 위치(아래 가운데)
# 종류 → (시트 폴더, 기준점). 새 종류는 여기 한 줄이다(if/switch로 닫지 않는다).
SHEET_KINDS = {
    "tile": ("art/tiles", TILE_PIVOT),
    "building": ("art/world", FOOT_PIVOT),
    "prop": ("art/world", FOOT_PIVOT),
}


class ExportWriteError(OSError):
    """쓰기가 도중에 실패했다. replaced는 그때까지 이미 제자리에 바뀐 파일(뿌리 기준 경로)."""

    def __init__(self, message: str, replaced: List[str]):
        done = ", ".join(replaced) if replaced else "(없음)"
        super().__init__(f"{message} — 이미 바뀐 파일: {done}")
        self.replaced = replaced


class ExportError(Exception):
    def __init__(self, code: str, message: str):
        super().__init__(message)
        self.code = code


@dataclass
class ExportPlan:
    files: Dict[str, bytes] = field(default_factory=dict)               # 뿌리 기준 경로 → 내용
    replace: List[Tuple[str, str, str]] = field(default_factory=list)   # (가구 id, 새 sprite, 새 name)
    problems: List[Problem] = field(default_factory=list)
    defs_before: Optional[bytes] = None    # 가구 정의 파일의 지금 내용(replace가 있을 때)
    defs_after: Optional[bytes] = None     # replace를 적용한 내용


def _problem(rid: str, code: str, message: str) -> Problem:
    return Problem(code, f"{rid}: {message}")


# --- 그림과 좌표표 ---------------------------------------------------------------

def _states_in_order(recipe: dict) -> List[Optional[str]]:
    return [None] + list((recipe.get("states") or {}).keys())


def _sheet_png(cells: list) -> bytes:
    """칸 그림들을 왼쪽부터 가로로 붙인 한 장."""
    w, h = cells[0].w, cells[0].h
    img = Image.new("RGBA", (w * len(cells), h), (0, 0, 0, 0))
    for i, cv in enumerate(cells):
        img.paste(cv.to_image(), (i * w, 0))
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=False)
    return buf.getvalue()


def _atlas_json(image_path: str, png: bytes, names: List[str], art_cells: list) -> bytes:
    with Image.open(io.BytesIO(png)) as im:
        width, height = im.size
    cw = width // len(names)
    aw, ah = art_cells
    pivot = {"x": 0.5 / aw, "y": (ah - 0.5) / ah}   # 점유 칸 줄의 왼쪽 아래 칸 가운데
    sprites = {n: {"x": i * cw, "y": 0, "w": cw, "h": height, "pivot": dict(pivot)} for i, n in enumerate(names)}
    doc = {"image": image_path, "width": width, "height": height, "pixelsPerUnit": PIXELS_PER_UNIT,
           "origin": "top-left", "sprites": sprites}
    return (json.dumps(doc, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


# --- 가구 정의 한 항목의 두 문자열 바꾸기 ------------------------------------------------

def _strip_bom(text: str) -> str:
    return text[1:] if text.startswith("﻿") else text


def _top_level_spans(text: str):
    """최상위 배열의 원소 객체마다 (시작, 끝) 글자 위치. 문자열 안의 괄호는 세지 않는다."""
    depth, in_str, esc, start = 0, False, False, -1
    for i, ch in enumerate(text):
        if in_str:
            if esc:
                esc = False
            elif ch == "\\":
                esc = True
            elif ch == '"':
                in_str = False
            continue
        if ch == '"':
            in_str = True
        elif ch in "{[":
            depth += 1
            if ch == "{" and depth == 2:
                start = i
        elif ch in "}]":
            if ch == "}" and depth == 2:
                yield start, i + 1
            depth -= 1


def _find_entity(text: str, furniture_id: str) -> Tuple[int, int]:
    needle = json.dumps(furniture_id, ensure_ascii=False)
    for s, e in _top_level_spans(text):
        chunk = text[s:e]
        if needle not in chunk:
            continue
        try:
            obj = json.loads(chunk)
        except json.JSONDecodeError:
            continue
        if obj.get("type") == "FurnitureDefinition" and obj.get("id") == furniture_id:
            return s, e
    raise ExportError("export-unknown-furniture", f"가구 정의에 {furniture_id}가 없다")


_STR = r'"((?:[^"\\]|\\.)*)"'


def _substitute(raw: bytes, replacements: List[Tuple[str, str, str]]) -> bytes:
    """항목마다 tileParts[0].tile의 sprite·name 두 값만 바꾼다. 바뀐 결과는 다시 읽어 검증한다."""
    text = raw.decode("utf-8")
    try:
        expected = json.loads(_strip_bom(text))
    except json.JSONDecodeError as e:
        raise ExportError("export-unreadable-definitions", f"{FURNITURE_DEFS}를 JSON으로 못 읽었다: {e}") from None
    out = text
    for fid, sprite, name in replacements:
        s, e = _find_entity(out, fid)
        chunk = out[s:e]
        tp = chunk.find('"tileParts"')
        if tp < 0:
            raise ExportError("export-unsupported-furniture", f"{fid}: tileParts가 없다")
        ms = re.compile(r'"sprite"\s*:\s*' + _STR).search(chunk, tp)
        mn = re.compile(r'"name"\s*:\s*' + _STR).search(chunk, tp)
        if not ms or not mn:
            raise ExportError("export-unsupported-furniture", f"{fid}: tile의 sprite·name을 찾지 못했다")
        # 뒤쪽 값부터 바꿔 앞쪽 위치가 밀리지 않게 한다.
        for m, new in sorted(((ms, sprite), (mn, name)), key=lambda t: -t[0].start()):
            chunk = chunk[:m.start(1)] + json.dumps(new, ensure_ascii=False)[1:-1] + chunk[m.end(1):]
        out = out[:s] + chunk + out[e:]
        for ent in expected:
            if ent.get("type") == "FurnitureDefinition" and ent.get("id") == fid:
                ent["tileParts"][0]["tile"]["sprite"] = sprite
                ent["tileParts"][0]["tile"]["name"] = name
    try:
        got = json.loads(_strip_bom(out))
    except json.JSONDecodeError as e:
        raise ExportError("export-replace-failed", f"치환 뒤 JSON이 깨졌다: {e}") from None
    if got != expected:
        raise ExportError("export-replace-failed", "치환 결과가 기대와 다르다(두 문자열 밖이 바뀌었다)")
    return out.encode("utf-8")


# --- 계획 ----------------------------------------------------------------------------

def _check_furniture(recipe: dict, defs_text: str, fid: str) -> None:
    s, e = _find_entity(defs_text, fid)
    ent = json.loads(defs_text[s:e])
    parts = ent.get("tileParts") or []
    if len(parts) != 1 or parts[0].get("localCell") != {"x": 0, "y": 0}:
        raise ExportError("export-unsupported-furniture", f"{fid}: tileParts가 하나(localCell 0,0)가 아니다")
    cells = recipe.get("cells")
    if not (isinstance(cells, list) and len(cells) == 2 and cells[1] == 1):
        raise ExportError("export-unsupported-furniture", "한 줄 점유(cells [w, 1])만 바꿀 수 있다")
    art = recipe.get("art_cells")
    if art[0] != cells[0]:
        raise ExportError("export-unsupported-furniture",
                          f"art_cells {art}가 점유 칸 {cells}보다 넓어 pivot이 어긋난다(가로 칸 수가 같아야 한다)")
    want = {(i, 0) for i in range(cells[0])}
    have = {(c["x"], c["y"]) for c in ent.get("footprintCells") or []}
    if want != have:
        raise ExportError("export-footprint-mismatch",
                          f"명세 cells {cells}와 {fid}의 footprintCells {sorted(have)}가 다르다")


def export_furniture(recipe: dict, kit: Kit, repo_root) -> ExportPlan:
    root = Path(repo_root)
    rid = recipe["id"]
    plan = ExportPlan()
    cells, names = [], []
    for st in _states_in_order(recipe):
        cv, found = draw_and_check(kit, recipe, st)
        label = rid if st is None else f"{rid}@{st}"
        plan.problems += [Problem(p.code, f"{label}: {p.message}", p.x, p.y) for p in found]
        cells.append(cv)
        names.append(label)
    art = recipe.get("art_cells")
    if not (isinstance(art, list) and len(art) == 2):
        plan.problems.append(_problem(rid, "export-unsupported-furniture", "art_cells [w, h]가 있어야 한다"))
        return plan
    png_path = f"{PROPS_ROOT}/{rid.split('.')[0]}/{rid}.png"
    target = (recipe.get("game") or {}).get("replaces")
    if target is not None:
        try:
            raw = (root / FURNITURE_DEFS).read_bytes()
            _check_furniture(recipe, raw.decode("utf-8"), target)
            plan.replace.append((target, png_path, rid))
            plan.defs_before = raw
            plan.defs_after = _substitute(raw, plan.replace)
        except OSError:
            plan.problems.append(_problem(rid, "export-unreadable-definitions", f"{FURNITURE_DEFS}를 못 읽었다"))
        except UnicodeDecodeError:
            plan.problems.append(_problem(rid, "export-unreadable-definitions",
                                          f"{FURNITURE_DEFS}가 UTF-8이 아니라 못 읽었다"))
        except ExportError as e:
            plan.problems.append(_problem(rid, e.code, str(e)))
    if plan.problems:
        return plan
    png = _sheet_png(cells)
    plan.files[png_path] = png
    plan.files[f"{ATLAS_ROOT}/{png_path}.atlas.json"] = _atlas_json(png_path, png, names, art)
    return plan


# --- 묶음 시트(타일, 건물·바깥 소품): 덧붙이기만 한다 ----------------------------------------

def _json_bytes(doc) -> bytes:
    return (json.dumps(doc, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def sheet_path(kind: str, set_name: str) -> str:
    """종류와 세트 이름으로 시트 PNG 경로(뿌리 기준). 건물·소품은 같은 시트를 쓴다."""
    return f"{SHEET_KINDS[kind][0]}/{set_name}.png"


def _read_existing_sheet(root: Path, png_path: str, plan: ExportPlan):
    """(자리 표, 그림). 둘 다 없으면 ({}, None). 하나만 있거나 어긋나면 문제를 적고 (None, None)."""
    table_file, png_file = root / (png_path + ".slots.json"), root / png_path
    has_table, has_png = table_file.exists(), png_file.exists()
    if not has_table and not has_png:
        return {}, None
    if has_table != has_png:
        plan.problems.append(_problem(png_path, "export-sheet-inconsistent",
                                      "자리 표와 PNG 중 하나만 있다(덧붙이기만 하는 시트라 어느 쪽도 믿고 고칠 수 없다)"))
        return None, None
    try:
        doc = json.loads(table_file.read_text(encoding="utf-8"))
        slots = doc["slots"]
        for k, v in slots.items():
            if not all(type(v[f]) is int for f in "xywh"):
                raise ValueError(f"{k}의 x·y·w·h가 정수가 아니다")
        if (doc["cell"], doc["width_cells"]) != (CELL, SHEET_WIDTH_CELLS):
            raise ValueError("cell·width_cells가 지금 약속(32, 16)과 다르다")
        slots = {k: {f: v[f] for f in "xywh"} for k, v in slots.items()}
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as e:
        plan.problems.append(_problem(png_path, "export-slots-unreadable", f"{png_path}.slots.json를 못 읽었다({e})"))
        return None, None
    try:
        with Image.open(png_file) as im:
            img = im.convert("RGBA")
    except (OSError, ValueError) as e:
        plan.problems.append(_problem(png_path, "export-sheet-inconsistent", f"{png_path}를 못 읽었다({e})"))
        return None, None
    bad = [k for k, v in slots.items()
           if v["x"] < 0 or v["y"] < 0 or v["x"] + v["w"] > img.width or v["y"] + v["h"] > img.height]
    if img.width != CELL * SHEET_WIDTH_CELLS or bad:
        plan.problems.append(_problem(png_path, "export-sheet-inconsistent",
                                      f"PNG 크기 {img.size}가 자리 표와 맞지 않는다" + (f"(밖: {', '.join(bad)})" if bad else "")))
        return None, None
    return slots, img


def _sheet_atlas_json(png_path: str, size, slots: dict, pivot: dict) -> bytes:
    sprites = {n: {"x": s["x"], "y": s["y"], "w": s["w"], "h": s["h"], "pivot": dict(pivot)} for n, s in slots.items()}
    return _json_bytes({"image": png_path, "width": size[0], "height": size[1], "pixelsPerUnit": PIXELS_PER_UNIT,
                        "origin": "top-left", "sprites": sprites})


def export_sheet(recipes: List[dict], kind: str, set_name: str, kit: Kit, repo_root) -> ExportPlan:
    """같은 시트로 가는 명세들(각자 기본 그림만)을 시트에 넣는다.

    이미 자리가 있는 id는 그 자리에 다시 그리고(크기가 달라졌으면 export-slot-size-changed), 시트의 나머지 픽셀은
    그대로 둔다. 새 id는 마지막으로 쓴 줄 뒤에 주어진 순서대로 왼쪽부터 놓고, 폭이 모자라면 다음 줄로 간다."""
    root = Path(repo_root)
    plan = ExportPlan()
    if kind not in SHEET_KINDS:
        plan.problems.append(Problem("export-kind-mismatch", f"{kind}는 묶음 시트로 내는 종류가 아니다"))
        return plan
    png_path = sheet_path(kind, set_name)
    folder, pivot = SHEET_KINDS[kind]
    drawn: Dict[str, object] = {}
    for recipe in recipes:
        rid = recipe["id"]
        if rid in drawn:
            continue
        if SHEET_KINDS.get(recipe.get("kind"), (None,))[0] != folder:
            plan.problems.append(_problem(rid, "export-kind-mismatch",
                                          f"{recipe.get('kind')}는 {folder} 시트에 들어가지 않는다"))
            continue
        cv, found = draw_and_check(kit, recipe, None)
        plan.problems += [Problem(p.code, f"{rid}: {p.message}", p.x, p.y) for p in found]
        if cv.w > CELL * SHEET_WIDTH_CELLS:
            plan.problems.append(_problem(rid, "export-too-wide",
                                          f"그림 폭 {cv.w}px가 시트 폭 {CELL * SHEET_WIDTH_CELLS}px보다 넓다"))
            continue
        drawn[rid] = cv
    old_slots, old_img = _read_existing_sheet(root, png_path, plan)
    if old_slots is None:
        return plan
    for rid, cv in drawn.items():
        s = old_slots.get(rid)
        if s is not None and (s["w"], s["h"]) != (cv.w, cv.h):
            plan.problems.append(_problem(rid, "export-slot-size-changed",
                                          f"자리 {s['w']}x{s['h']}인데 그림이 {cv.w}x{cv.h}가 됐다"
                                          "(맵에 놓인 그림이 깨지므로 덮어쓰지 않는다)"))
    if plan.problems:
        return plan
    slots = {k: dict(v) for k, v in old_slots.items()}
    x, y, row_h = 0, max((s["y"] + s["h"] for s in slots.values()), default=0), 0
    for rid, cv in drawn.items():
        if rid in slots:
            continue
        if x + cv.w > CELL * SHEET_WIDTH_CELLS:
            x, y, row_h = 0, y + row_h, 0
        slots[rid] = {"x": x, "y": y, "w": cv.w, "h": cv.h}
        x += cv.w
        row_h = max(row_h, cv.h)
    height = max([old_img.height if old_img else 0] + [s["y"] + s["h"] for s in slots.values()])
    img = Image.new("RGBA", (CELL * SHEET_WIDTH_CELLS, height), (0, 0, 0, 0))
    if old_img is not None:
        img.paste(old_img, (0, 0))
    for rid, cv in drawn.items():
        s = slots[rid]
        img.paste(cv.to_image().convert("RGBA"), (s["x"], s["y"]))   # 마스크 없이: 투명 픽셀도 그대로 덮는다
    if old_img is not None and old_img.size == img.size and old_img.tobytes() == img.tobytes():
        png = (root / png_path).read_bytes()          # 그림이 같으면 있는 바이트를 그대로 둔다
    else:
        buf = io.BytesIO()
        img.save(buf, format="PNG", optimize=False)
        png = buf.getvalue()
    plan.files[png_path] = png
    plan.files[png_path + ".slots.json"] = _json_bytes({"cell": CELL, "width_cells": SHEET_WIDTH_CELLS, "slots": slots})
    plan.files[f"{ATLAS_ROOT}/{png_path}.atlas.json"] = _sheet_atlas_json(png_path, img.size, slots, pivot)
    return plan


def merge_plans(plans: List[ExportPlan]) -> ExportPlan:
    """여러 계획을 하나로. 문제 하나가 전체를 막는다. 가구 정의는 원본에서 한 번에 다시 치환한다."""
    out = ExportPlan()
    for p in plans:
        out.files.update(p.files)
        out.replace += p.replace
        out.problems += p.problems
        if p.defs_before is not None:
            out.defs_before = p.defs_before
    seen: Dict[str, str] = {}
    for fid, _sprite, name in out.replace:
        if fid in seen:
            out.problems.append(Problem("export-duplicate-target",
                                        f"{seen[fid]}와 {name}가 같은 가구 {fid}를 바꾸려 한다"))
        seen.setdefault(fid, name)
    if out.replace and not out.problems:
        try:
            out.defs_after = _substitute(out.defs_before, out.replace)
        except ExportError as e:
            out.problems.append(Problem(e.code, str(e)))
    if out.problems:
        out.files.clear()
        out.defs_after = None
    return out


def _differs(path: Path, content: bytes) -> bool:
    try:
        return path.read_bytes() != content
    except OSError:
        return True


def pending_writes(plan: ExportPlan, repo_root) -> List[str]:
    """실제로 바뀔 파일(뿌리 기준 경로). 같은 내용이면 뺀다. 가구 정의가 있으면 맨 끝."""
    root = Path(repo_root)
    out = [rel for rel, data in plan.files.items() if _differs(root / rel, data)]
    if plan.defs_after is not None and plan.defs_after != plan.defs_before:
        out.append(FURNITURE_DEFS)
    return out


def _write_staged(path: Path, data: bytes) -> None:
    path.write_bytes(data)


def _replace(src: Path, dst: Path) -> None:
    os.replace(src, dst)


def _remove_quietly(paths) -> None:
    for p in paths:
        try:
            p.unlink()
        except OSError:
            pass


def apply(plan: ExportPlan, repo_root) -> List[str]:
    """문제가 있으면 아무것도 쓰지 않는다. 쓴 파일 목록을 돌려준다.

    모든 파일을 먼저 같은 폴더의 `<이름>.tmp`에 쓰고, 다 됐을 때만 PNG, 좌표표, 가구 정의 순서로 바꿔친다.
    스테이징이 실패하면 임시 파일을 지우고 대상은 그대로 둔다. 바꿔치기가 도중에 실패하면
    남은 임시 파일을 지우고 이미 바뀐 파일을 ExportWriteError에 담아 알린다."""
    if plan.problems:
        return []
    root = Path(repo_root)
    todo = pending_writes(plan, repo_root)
    staged: List[Tuple[str, Path, Path]] = []
    try:
        for rel in todo:
            data = plan.defs_after if rel == FURNITURE_DEFS else plan.files[rel]
            dst = root / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            tmp = dst.with_name(dst.name + ".tmp")
            staged.append((rel, tmp, dst))    # 쓰다 실패해도 지워지도록 먼저 올린다
            _write_staged(tmp, data)
    except OSError:
        _remove_quietly(t for _, t, _ in staged)
        raise
    done: List[str] = []
    for rel, tmp, dst in staged:
        try:
            _replace(tmp, dst)
        except OSError as e:
            _remove_quietly(t for _, t, _ in staged if t.exists())
            raise ExportWriteError(f"{rel}를 못 바꿨다({e})", done) from None
        done.append(rel)
    return done
