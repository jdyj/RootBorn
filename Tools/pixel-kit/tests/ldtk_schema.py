"""LDtk 1.5.3 공식 스키마(fixtures/ldtk-1.5.3/JSON_SCHEMA.json)의 required와 형을 재귀로 보는 작은 검사기(테스트 도우미).

jsonschema 패키지 없이 이 스키마가 쓰는 것만 본다: $ref, type(null 허용 포함), oneOf, enum, items, required, properties.
editor_key_problems는 편집기가 저장한 파일에 늘 있는 키(fixtures/ldtk-1.5.3/editor-keys.json)가 빠졌는지 본다.
"""
import json
from pathlib import Path
from typing import List

FIXTURES = Path(__file__).resolve().parent / "fixtures" / "ldtk-1.5.3"
SCHEMA = json.loads((FIXTURES / "JSON_SCHEMA.json").read_text("utf-8"))
EDITOR_KEYS = {k: set(v) for k, v in json.loads((FIXTURES / "editor-keys.json").read_text("utf-8"))["kinds"].items()}


def _resolve(ref: str) -> dict:
    node = SCHEMA
    for part in ref.lstrip("#/").split("/"):
        node = node[part]
    return node


def _is(kind: str, v) -> bool:
    if kind == "null":
        return v is None
    if kind == "boolean":
        return isinstance(v, bool)
    if kind == "integer":
        return isinstance(v, int) and not isinstance(v, bool)
    if kind == "number":
        return isinstance(v, (int, float)) and not isinstance(v, bool)
    if kind == "string":
        return isinstance(v, str)
    if kind == "array":
        return isinstance(v, list)
    if kind == "object":
        return isinstance(v, dict)
    raise ValueError(f"스키마에 모르는 형 {kind}")


def _check(node: dict, v, where: str, out: List[str]) -> None:
    if "$ref" in node:
        node = {**_resolve(node["$ref"]), **{k: x for k, x in node.items() if k != "$ref"}}
    if "oneOf" in node:
        if not any(not _errors(alt, v, where) for alt in node["oneOf"]):
            out.append(f"{where}: oneOf 어느 것에도 맞지 않는다({json.dumps(v, ensure_ascii=False)[:80]})")
        return
    if "enum" in node and v not in node["enum"]:
        out.append(f"{where}: {v!r}는 허용 값 {node['enum']}이 아니다")
        return
    types = node.get("type")
    if types is not None:
        types = types if isinstance(types, list) else [types]
        if not any(_is(t, v) for t in types):
            out.append(f"{where}: 형이 {types}가 아니다({json.dumps(v, ensure_ascii=False)[:80]})")
            return
    if isinstance(v, dict):
        props = node.get("properties", {})
        for k in node.get("required", []):
            if k not in v:
                out.append(f"{where}: {k} 없음")
        for k, x in v.items():
            if k in props:
                _check(props[k], x, f"{where}.{k}", out)
    elif isinstance(v, list) and isinstance(node.get("items"), dict):
        for i, x in enumerate(v):
            _check(node["items"], x, f"{where}[{i}]", out)


def _errors(node, v, where) -> List[str]:
    out: List[str] = []
    _check(node, v, where, out)
    return out


def schema_problems(doc) -> List[str]:
    """프로젝트 문서(.ldtk를 읽은 dict)가 스키마 required·형을 어긴 곳. 비면 통과."""
    return _errors(SCHEMA["LdtkJsonRoot"], doc, "root")


def objects(doc):
    """(종류, 자리, 객체) — editor-keys.json의 종류 이름으로."""
    yield "root", "root", doc
    yield "header", "__header__", doc.get("__header__") or {}
    defs = doc.get("defs") or {}
    yield "defs", "defs", defs
    for i, L in enumerate(defs.get("layers") or []):
        yield "layerDef", f"defs.layers[{i}]", L
        for j, v in enumerate(L.get("intGridValues") or []):
            yield "intGridValue", f"defs.layers[{i}].intGridValues[{j}]", v
    for i, E in enumerate(defs.get("entities") or []):
        yield "entityDef", f"defs.entities[{i}]", E
        for j, F in enumerate(E.get("fieldDefs") or []):
            yield "fieldDef", f"defs.entities[{i}].fieldDefs[{j}]", F
    for i, T in enumerate(defs.get("tilesets") or []):
        yield "tilesetDef", f"defs.tilesets[{i}]", T
    for i, lv in enumerate(doc.get("levels") or []):
        w = f"levels[{i}]"
        yield "level", w, lv
        for j, li in enumerate(lv.get("layerInstances") or []):
            lw = f"{w}.layerInstances[{j}]"
            yield "layerInstance", lw, li
            for key in ("gridTiles", "autoLayerTiles"):
                for k, t in enumerate(li.get(key) or []):
                    yield "tile", f"{lw}.{key}[{k}]", t
            for k, e in enumerate(li.get("entityInstances") or []):
                yield "entityInstance", f"{lw}.entityInstances[{k}]", e
                for m, f in enumerate(e.get("fieldInstances") or []):
                    yield "fieldInstance", f"{lw}.entityInstances[{k}].fieldInstances[{m}]", f


def editor_key_problems(doc) -> List[str]:
    """편집기 저장 파일에 늘 있는 키 중 빠진 것."""
    out = []
    for kind, where, obj in objects(doc):
        missing = sorted(EDITOR_KEYS.get(kind, set()) - set(obj))
        if missing:
            out.append(f"{where}({kind}): {', '.join(missing)} 없음")
    return out


def consistency_problems(doc) -> List[str]:
    """스키마가 말하지 않는 짜임: uid가 겹치지 않고 nextUid보다 작다, 참조가 정의를 가리킨다, iid가 겹치지 않는다,
    타일의 t·d가 자리와 맞다, 문자열 필드의 realEditorValues가 __value와 같다, 엔티티 __tile이 정의의 tileRect와 같다."""
    p = []
    defs = doc.get("defs") or {}
    uids = []
    for group in ("layers", "entities", "tilesets", "enums", "levelFields"):
        for d in defs.get(group) or []:
            uids.append(d.get("uid"))
            for f in d.get("fieldDefs") or []:
                uids.append(f.get("uid"))
    uids += [lv.get("uid") for lv in doc.get("levels") or []]
    if len(uids) != len(set(uids)):
        p.append(f"uid가 겹친다: {sorted(u for u in set(uids) if uids.count(u) > 1)}")
    if uids and not all(isinstance(u, int) and u < doc.get("nextUid", -1) for u in uids):
        p.append(f"nextUid {doc.get('nextUid')}가 쓰인 uid {max(u for u in uids if isinstance(u, int))}보다 크지 않다")
    layer_defs = {d.get("uid"): d for d in defs.get("layers") or []}
    tilesets = {t.get("uid"): t for t in defs.get("tilesets") or []}
    entity_defs = {e.get("uid"): e for e in defs.get("entities") or []}
    iids = [doc.get("iid"), doc.get("dummyWorldIid")]
    for lv in doc.get("levels") or []:
        iids.append(lv.get("iid"))
        for li in lv.get("layerInstances") or []:
            iids.append(li.get("iid"))
            w = f"{lv.get('identifier')}/{li.get('__identifier')}"
            ld = layer_defs.get(li.get("layerDefUid"))
            if ld is None:
                p.append(f"{w}: layerDefUid {li.get('layerDefUid')}인 층 정의가 없다")
                continue
            if (ld.get("identifier"), ld.get("__type"), ld.get("gridSize")) != (li.get("__identifier"), li.get("__type"), li.get("__gridSize")):
                p.append(f"{w}: 층 정의와 이름·종류·칸 크기가 다르다")
            if li.get("levelId") != lv.get("uid"):
                p.append(f"{w}: levelId {li.get('levelId')}가 레벨 uid {lv.get('uid')}와 다르다")
            ts = tilesets.get(li.get("__tilesetDefUid"))
            if li.get("__tilesetDefUid") is not None and (ts is None or ts.get("relPath") != li.get("__tilesetRelPath")):
                p.append(f"{w}: __tilesetDefUid·__tilesetRelPath가 타일셋 정의와 맞지 않는다")
            if li.get("__type") == "Tiles" and ld.get("tilesetDefUid") != li.get("__tilesetDefUid"):
                p.append(f"{w}: 층 정의의 tilesetDefUid와 층의 __tilesetDefUid가 다르다")
            for t in li.get("gridTiles") or []:
                if ts is None:
                    p.append(f"{w}: 타일셋 없이 타일이 있다")
                    break
                g = ts["tileGridSize"]
                want_t = (t["src"][1] // g) * ts.get("__cWid", -(-ts["pxWid"] // g)) + t["src"][0] // g
                want_d = [(t["px"][1] // li["__gridSize"]) * li["__cWid"] + t["px"][0] // li["__gridSize"]]
                if t.get("t") != want_t or t.get("d") != want_d:
                    p.append(f"{w}: 타일 {t['px']}의 t·d가 {want_t}·{want_d}가 아니다")
                    break
            for e in li.get("entityInstances") or []:
                iids.append(e.get("iid"))
                ed = entity_defs.get(e.get("defUid"))
                if ed is None or ed.get("identifier") != e.get("__identifier"):
                    p.append(f"{w}: 엔티티 {e.get('__identifier')}의 정의(defUid {e.get('defUid')})가 없다")
                    continue
                fds = {f["uid"]: f for f in ed.get("fieldDefs") or []}
                if sorted(f.get("defUid") for f in e.get("fieldInstances") or []) != sorted(fds):
                    p.append(f"{w}: 엔티티 {e['__identifier']}의 필드가 정의의 필드와 다르다")
                for f in e.get("fieldInstances") or []:
                    fd = fds.get(f.get("defUid"))
                    if fd is not None and fd.get("type") == "F_String":
                        want = [] if f.get("__value") is None else [{"id": "V_String", "params": [f.get("__value")]}]
                        if f.get("realEditorValues") != want:
                            p.append(f"{w}: 필드 {f['__identifier']}의 realEditorValues가 __value {f['__value']!r}와 다르다"
                                     f"(편집기는 realEditorValues를 읽는다)")
                if ed.get("tileRect") is not None and e.get("__tile") != ed["tileRect"]:
                    p.append(f"{w}: 엔티티 {e['__identifier']}의 __tile이 정의의 tileRect와 다르다(편집기가 다시 계산한다)")
                if e.get("__grid") != [e["px"][0] // li["__gridSize"], e["px"][1] // li["__gridSize"]]:
                    p.append(f"{w}: 엔티티 {e['__identifier']}의 __grid가 px와 맞지 않는다")
    if len(iids) != len(set(iids)):
        p.append("iid가 겹친다")
    for t in tilesets.values():
        g = t["tileGridSize"]
        if (t.get("__cWid"), t.get("__cHei")) != (-(-t["pxWid"] // g), -(-t["pxHei"] // g)):
            p.append(f"타일셋 {t['identifier']}의 __cWid·__cHei가 시트 크기와 맞지 않는다")
    return p


def all_problems(doc) -> List[str]:
    return schema_problems(doc) + editor_key_problems(doc) + consistency_problems(doc)
