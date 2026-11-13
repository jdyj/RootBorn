"""LDtk 파일과 맵 목록을 쓰는 모듈들(ldtk_test_level, room_ldtk)이 같이 쓰는 도우미.

게임의 maps.json은 // 주석과 끝 쉼표가 있어 일반 JSON 읽기로는 못 읽는다. walker·npcWalker는 거기서 옮겨 온다.

아래 "편집기 꼴" 함수들은 LDtk 1.5.3 편집기가 저장하는 꼴의 완전한 객체(키 순서까지 편집기와 같다)를 만든다.
기본값은 저장소의 편집기 저장 파일(Assets/LDtk/House/house.ldtk 등)에서 가져왔고, 빠지면 안 되는 키는 공식 스키마
(Tools/pixel-kit/tests/fixtures/ldtk-1.5.3/JSON_SCHEMA.json)의 required로 테스트가 지킨다(tests/test_ldtk_editor_shape.py).
게임 읽기 코드(game/Rootborn.World/Ldtk/LdtkFile.cs)가 읽는 값은 부르는 쪽이 넘긴다.
"""
import json
import os
import re
from pathlib import Path
from typing import List, Optional, Tuple

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


# ---------------------------------------------------------------- 편집기 꼴(LDtk 1.5.3)

LDTK_VERSION = "1.5.3"
LDTK_BUILD_ID = 473703                    # 1.5.3 편집기가 저장한 파일의 appBuildId(배포일 번호, 사람마다 같다)
HEADER = {"fileType": "LDtk Project JSON", "app": "LDtk", "doc": "https://ldtk.io/json",
          "schema": "https://ldtk.io/files/JSON_SCHEMA.json", "appAuthor": "Sebastien 'deepnight' Benard",
          "appVersion": LDTK_VERSION, "url": "https://ldtk.io"}
PROJECT_KEYS = ("__header__", "iid", "jsonVersion", "appBuildId", "nextUid", "identifierStyle", "toc", "worldLayout",
                "worldGridWidth", "worldGridHeight", "defaultLevelWidth", "defaultLevelHeight", "defaultPivotX",
                "defaultPivotY", "defaultGridSize", "defaultEntityWidth", "defaultEntityHeight", "bgColor",
                "defaultLevelBgColor", "minifyJson", "externalLevels", "exportTiled", "simplifiedExport",
                "imageExportMode", "exportLevelBg", "pngFilePattern", "backupOnSave", "backupLimit", "backupRelPath",
                "levelNamePattern", "tutorialDesc", "customCommands", "flags", "defs", "levels", "worlds", "dummyWorldIid")


def smart_color(hex_color: str) -> str:
    """편집기가 레벨 __smartColor로 쓰는 색: 바탕색을 흰색 쪽으로 45% 옮긴 것(house.ldtk #696A79 → #ADADB5와 같다).
    편집기가 열 때 다시 계산하는 값이라 맞지 않아도 열기를 막지는 않는다."""
    rgb = [int(hex_color[i:i + 2], 16) for i in (1, 3, 5)]
    return "#" + "".join(f"{int(c + 0.45 * (255 - c) + 0.5):02X}" for c in rgb)


def missing_project_keys(doc) -> List[str]:
    """편집기가 저장한 프로젝트라면 늘 있는 최상위 키 중 빠진 것(예전 이 도구가 쓴 모자란 파일을 알아본다)."""
    return [k for k in PROJECT_KEYS if not isinstance(doc, dict) or k not in doc]


def project(iid: str, dummy_world_iid: str, *, next_uid: int, grid: int, level_size: Tuple[int, int],
            bg_color: str, level_bg_color: str, defs: dict, levels: list) -> dict:
    """프로젝트 최상위. 레벨은 파일 안에(externalLevels false), 세계는 하나(worlds 비어 있음, Free 배치)."""
    doc = {
        "__header__": dict(HEADER), "iid": iid, "jsonVersion": LDTK_VERSION, "appBuildId": LDTK_BUILD_ID,
        "nextUid": next_uid, "identifierStyle": "Capitalize", "toc": [], "worldLayout": "Free",
        "worldGridWidth": 256, "worldGridHeight": 256, "defaultLevelWidth": level_size[0], "defaultLevelHeight": level_size[1],
        "defaultPivotX": 0, "defaultPivotY": 0, "defaultGridSize": grid, "defaultEntityWidth": grid, "defaultEntityHeight": grid,
        "bgColor": bg_color, "defaultLevelBgColor": level_bg_color, "minifyJson": False, "externalLevels": False,
        "exportTiled": False, "simplifiedExport": False, "imageExportMode": "None", "exportLevelBg": True,
        "pngFilePattern": None, "backupOnSave": False, "backupLimit": 10, "backupRelPath": None,
        "levelNamePattern": "Level_%idx", "tutorialDesc": None, "customCommands": [], "flags": [],
        "defs": defs, "levels": levels, "worlds": [], "dummyWorldIid": dummy_world_iid,
    }
    assert tuple(doc) == PROJECT_KEYS
    return doc


def definitions(layers: list, entities: list, tilesets: list) -> dict:
    return {"layers": layers, "entities": entities, "tilesets": tilesets, "enums": [], "externalEnums": [], "levelFields": []}


def int_grid_value(value: int, identifier: str, color: str) -> dict:
    return {"value": value, "identifier": identifier, "color": color, "tile": None, "groupUid": 0}


def layer_def(kind: str, identifier: str, uid: int, grid: int, *, doc: Optional[str] = None, tags: Optional[list] = None,
              tileset_uid: Optional[int] = None, int_grid_values: Optional[list] = None, opacity: float = 1) -> dict:
    """층 정의. kind는 IntGrid·Entities·Tiles."""
    return {
        "doc": doc, "uiColor": None, "guideGridWid": 0, "guideGridHei": 0, "displayOpacity": opacity,
        "inactiveOpacity": 1, "hideInList": False, "hideFieldsWhenInactive": False, "canSelectWhenInactive": True,
        "renderInWorldView": True, "pxOffsetX": 0, "pxOffsetY": 0, "parallaxFactorX": 0, "parallaxFactorY": 0,
        "parallaxScaling": True, "requiredTags": [], "excludedTags": [], "autoTilesKilledByOtherLayerUid": None,
        "useAsyncRender": False, "intGridValues": list(int_grid_values or []), "intGridValuesGroups": [],
        "autoRuleGroups": [], "autoSourceLayerDefUid": None, "tilesetDefUid": tileset_uid, "tilePivotX": 0,
        "tilePivotY": 0, "biomeFieldUid": None, "__type": kind, "identifier": identifier, "type": kind, "uid": uid,
        "gridSize": grid, "uiFilterTags": list(tags or []),
    }


def field_def_string(identifier: str, uid: int) -> dict:
    """문자열 필드 정의(값 없음 허용)."""
    return {
        "identifier": identifier, "doc": None, "__type": "String", "uid": uid, "type": "F_String", "isArray": False,
        "canBeNull": True, "arrayMinLength": None, "arrayMaxLength": None, "editorDisplayMode": "Hidden",
        "editorDisplayScale": 1, "editorDisplayPos": "Above", "editorLinkStyle": "StraightArrow",
        "editorDisplayColor": None, "editorAlwaysShow": False, "editorShowInWorld": True, "editorCutLongValues": True,
        "editorTextSuffix": None, "editorTextPrefix": None, "useForSmartColor": False, "exportToToc": False,
        "searchable": False, "min": None, "max": None, "regex": None, "acceptFileTypes": None, "defaultOverride": None,
        "textLanguageMode": None, "symmetricalRef": False, "autoChainRef": True, "allowOutOfLevelRef": True,
        "allowedRefs": "OnlySame", "allowedRefsEntityUid": None, "allowedRefTags": [], "tilesetUid": None,
    }


def tile_rect(tileset_uid: int, x: int, y: int, w: int, h: int) -> dict:
    return {"tilesetUid": tileset_uid, "x": x, "y": y, "w": w, "h": h}


def entity_def(identifier: str, uid: int, width: int, height: int, *, color: str, pivot: Tuple[float, float] = (0, 0),
               fields: Optional[list] = None, tile: Optional[dict] = None, resizable: bool = True,
               show_name: bool = True, doc: Optional[str] = None) -> dict:
    """엔티티 정의. tile(tile_rect)이 있으면 그 타일로 그린다(renderMode Tile) — 편집기는 엔티티 __tile을 여기서 다시 계산한다."""
    return {
        "identifier": identifier, "uid": uid, "tags": [], "exportToToc": False, "allowOutOfBounds": False, "doc": doc,
        "width": width, "height": height, "resizableX": resizable, "resizableY": resizable, "minWidth": None,
        "maxWidth": None, "minHeight": None, "maxHeight": None, "keepAspectRatio": False, "tileOpacity": 1,
        "fillOpacity": 0.08, "lineOpacity": 0, "hollow": False, "color": color,
        "renderMode": "Tile" if tile is not None else "Rectangle", "showName": show_name,
        "tilesetId": tile["tilesetUid"] if tile is not None else None,
        "tileRenderMode": "FitInside" if tile is not None else "Stretch",
        "tileRect": dict(tile) if tile is not None else None, "uiTileRect": None, "nineSliceBorders": [], "maxCount": 0,
        "limitScope": "PerLevel", "limitBehavior": "MoveLastOne", "pivotX": pivot[0], "pivotY": pivot[1],
        "fieldDefs": list(fields or []),
    }


def tileset_def(identifier: str, uid: int, rel_path: str, px_wid: int, px_hei: int, grid: int) -> dict:
    """타일셋 정의. __cWid·__cHei는 시트 크기에서, cachedPixelData는 null(스키마가 허용하고 편집기 저장 파일도 null — 편집기가 다시 만든다)."""
    return {
        "__cWid": -(-px_wid // grid), "__cHei": -(-px_hei // grid), "identifier": identifier, "uid": uid,
        "relPath": rel_path, "embedAtlas": None, "pxWid": px_wid, "pxHei": px_hei, "tileGridSize": grid, "spacing": 0,
        "padding": 0, "tags": [], "tagsSourceEnumUid": None, "enumTags": [], "customData": [], "savedSelections": [],
        "cachedPixelData": None,
    }


def level(identifier: str, iid: str, uid: int, px_wid: int, px_hei: int, bg_color: str, layer_instances: list) -> dict:
    """레벨 하나(세계 (0, 0)). bgColor는 null이라 바탕은 프로젝트의 defaultLevelBgColor(그 값을 bg_color로 넘긴다).
    useAutoIdentifier는 false — true면 편집기가 레벨 이름을 Level_0으로 바꿔 맵 목록과 어긋난다."""
    return {
        "identifier": identifier, "iid": iid, "uid": uid, "worldX": 0, "worldY": 0, "worldDepth": 0,
        "pxWid": px_wid, "pxHei": px_hei, "__bgColor": bg_color, "bgColor": None, "useAutoIdentifier": False,
        "bgRelPath": None, "bgPos": None, "bgPivotX": 0.5, "bgPivotY": 0.5, "__smartColor": smart_color(bg_color),
        "__bgPos": None, "externalRelPath": None, "fieldInstances": [], "layerInstances": layer_instances, "__neighbours": [],
    }


def layer_instance(defn: dict, iid: str, level_uid: int, size: Tuple[int, int], *, tileset: Optional[dict] = None,
                   int_grid: Optional[list] = None, grid_tiles: Optional[list] = None, entities: Optional[list] = None) -> dict:
    """층 인스턴스(defn은 layer_def). size는 (칸 폭, 칸 높이). tileset은 타일 층의 타일셋 정의(아니면 None)."""
    return {
        "__identifier": defn["identifier"], "__type": defn["__type"], "__cWid": size[0], "__cHei": size[1],
        "__gridSize": defn["gridSize"], "__opacity": 1, "__pxTotalOffsetX": 0, "__pxTotalOffsetY": 0,
        "__tilesetDefUid": tileset["uid"] if tileset is not None else None,
        "__tilesetRelPath": tileset["relPath"] if tileset is not None else None,
        "iid": iid, "levelId": level_uid, "layerDefUid": defn["uid"], "pxOffsetX": 0, "pxOffsetY": 0, "visible": True,
        "optionalRules": [], "intGridCsv": list(int_grid or []), "autoLayerTiles": [], "seed": 0,
        "overrideTilesetUid": None, "gridTiles": list(grid_tiles or []), "entityInstances": list(entities or []),
    }


def grid_tile(px: Tuple[int, int], src: Tuple[int, int], tileset_cwid: int, tileset_grid: int,
              layer_cwid: int, layer_grid: int) -> dict:
    """타일 층의 타일 하나. t는 타일셋 안 칸 번호(행 우선), d는 [층 안 칸 번호]."""
    return {"px": [px[0], px[1]], "src": [src[0], src[1]], "f": 0,
            "t": (src[1] // tileset_grid) * tileset_cwid + src[0] // tileset_grid,
            "d": [(px[1] // layer_grid) * layer_cwid + px[0] // layer_grid], "a": 1}


def field_instance_string(fdef: dict, value: Optional[str]) -> dict:
    """문자열 필드 값. 편집기는 realEditorValues를 읽어 값을 정하므로 __value와 같은 값을 거기에도 둔다."""
    return {"__identifier": fdef["identifier"], "__type": "String", "__value": value, "__tile": None, "defUid": fdef["uid"],
            "realEditorValues": [] if value is None else [{"id": "V_String", "params": [value]}]}


def entity_instance(edef: dict, iid: str, px: Tuple[int, int], layer_grid: int, *, size: Optional[Tuple[int, int]] = None,
                    fields: Optional[list] = None) -> dict:
    """엔티티 하나(세계 (0, 0)의 레벨 안). __tile은 정의의 tileRect(편집기도 그렇게 다시 계산한다)."""
    w, h = size if size is not None else (edef["width"], edef["height"])
    return {
        "__identifier": edef["identifier"], "__grid": [px[0] // layer_grid, px[1] // layer_grid],
        "__pivot": [edef["pivotX"], edef["pivotY"]], "__tags": list(edef["tags"]),
        "__tile": dict(edef["tileRect"]) if edef["tileRect"] is not None else None, "__smartColor": edef["color"],
        "iid": iid, "width": w, "height": h, "defUid": edef["uid"], "px": [px[0], px[1]],
        "fieldInstances": list(fields or []), "__worldX": px[0], "__worldY": px[1],
    }
