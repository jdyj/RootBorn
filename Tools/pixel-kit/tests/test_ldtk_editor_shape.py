"""픽셀 키트가 쓰는 .ldtk가 LDtk 편집기(1.5.3)가 여는 꼴인지: 공식 스키마의 required·형(재귀), 편집기 저장 파일에 늘 있는 키,
uid·iid·참조의 짜임(tests/ldtk_schema.py).

2026-11-14 사용자가 편집기로 room build의 파일을 열자 "Cannot read properties of null (reading 'externalLevels')"가 났다
(게임이 읽는 필드만 쓰고 편집기가 기대하는 필드를 많이 빠뜨렸다). 이 테스트는 그 꼴을 지킨다.
편집기에서 실제로 열리는지는 이 테스트가 말하지 못한다(사람이 편집기로 열어 확인한다).
"""
import copy
import json
import shutil
import unittest
from pathlib import Path

from pixelkit import ldtk_common, ldtk_test_level
from pixelkit.room_ldtk import RoomBuildError, check_room, repaint_room
from tests import ldtk_schema
from tests.test_ldtk_test_level import LevelBase
from tests.test_room_ldtk import (DOOR_BUILD, DOOR_PLAN, EDITOR_SAVED, FIXTURE, KIT, RID, ROOT, RoomBase,
                                  add_build_and_wall_door)

EDITOR_HEADER = {"fileType": "LDtk Project JSON", "app": "LDtk", "doc": "https://ldtk.io/json",
                 "schema": "https://ldtk.io/files/JSON_SCHEMA.json", "appAuthor": "Sebastien 'deepnight' Benard",
                 "appVersion": "1.5.3", "url": "https://ldtk.io"}


class CheckerTests(unittest.TestCase):
    """검사기 자체가 빠진 키·틀린 형을 알아보는지(통과만 보면 검사기가 아무것도 안 보는지 모른다)."""

    def doc(self):
        return json.loads(FIXTURE.read_text("utf-8"))

    def test_imitation_of_an_editor_file_passes(self):
        self.assertEqual(ldtk_schema.all_problems(self.doc()), [])

    def test_files_really_saved_by_the_editor_pass_without_false_alarms(self):
        """흉내 낸 파일이 아니라 편집기가 저장한 파일(사용자가 칠하고 저장한 방, 저장소의 집 맵)에도 검사기가 오탐하지 않는다."""
        real = (Path(__file__).resolve().parent / "fixtures" / "room-editor-saved" / "test.two-rooms.ldtk",
                ROOT / "Assets" / "LDtk" / "House" / "house.ldtk")
        for path in real:
            self.assertTrue(path.is_file(), f"{path} 가 없다 — 이 검사를 조용히 건너뛰지 않는다")
            self.assertEqual(ldtk_schema.all_problems(json.loads(path.read_text("utf-8"))), [], str(path))

    def test_missing_required_keys_are_found_at_every_depth(self):
        for path in (("worlds",), ("defs", "layers", 0, "intGridValuesGroups"), ("levels", 0, "__neighbours"),
                     ("levels", 0, "layerInstances", 3, "gridTiles", 0, "d"), ("defs", "tilesets", 0, "__cWid"),
                     ("defs", "entities", 0, "fieldDefs", 0, "editorDisplayMode")):
            doc = self.doc()
            node = doc
            for k in path[:-1]:
                node = node[k]
            del node[path[-1]]
            self.assertTrue(ldtk_schema.schema_problems(doc), path)

    def test_wrong_types_and_enum_values_are_found(self):
        doc = self.doc()
        doc["levels"][0]["layerInstances"][0]["__tilesetDefUid"] = "1"
        self.assertTrue(ldtk_schema.schema_problems(doc))
        doc = self.doc()
        doc["identifierStyle"] = "Pascal"
        self.assertTrue(ldtk_schema.schema_problems(doc))
        doc = self.doc()
        doc["defs"]["layers"][0]["intGridValues"][0]["tile"] = {"x": 0}
        self.assertTrue(ldtk_schema.schema_problems(doc))

    def test_editor_keys_and_consistency_are_checked(self):
        doc = self.doc()
        del doc["levels"][0]["bgRelPath"]           # 스키마는 required가 아니지만 편집기 파일에는 늘 있다
        self.assertEqual(ldtk_schema.schema_problems(doc), [])
        self.assertTrue(ldtk_schema.editor_key_problems(doc))
        doc = self.doc()
        doc["nextUid"] = 1
        self.assertTrue(ldtk_schema.consistency_problems(doc))
        doc = self.doc()
        door_def = next(e for e in doc["defs"]["entities"] if e["identifier"] == "Door")
        door = ldtk_common.entity_instance(door_def, "00000000-0000-5000-8000-000000000001", (32, 96), 32,
                                           fields=[ldtk_common.field_instance_string(door_def["fieldDefs"][0], "town")])
        next(li for li in doc["levels"][0]["layerInstances"] if li["__identifier"] == "Door")["entityInstances"] = [door]
        self.assertEqual(ldtk_schema.all_problems(doc), [])
        door["fieldInstances"][0]["realEditorValues"] = []
        self.assertTrue(ldtk_schema.consistency_problems(doc))


class RoomShapeTests(RoomBase):
    def built(self):
        self.real_plan()
        self.build()
        return json.loads((self.out() / f"{RID}.ldtk").read_text("utf-8"))

    def test_room_build_writes_every_required_and_editor_key(self):
        self.assertEqual(ldtk_schema.all_problems(self.built()), [])

    def test_room_with_a_door_keeps_the_where_value_where_the_editor_reads_it(self):
        plan = ["########", "########", "########", "#......#", "#..D...#", "#......#", "########"]
        self.small_plan("t.door", plan, doors={"D": "town"})
        self.build("t.door")
        doc = json.loads((self.out("t.door") / "t.door.ldtk").read_text("utf-8"))
        self.assertEqual(ldtk_schema.all_problems(doc), [])
        door = next(li for li in doc["levels"][0]["layerInstances"] if li["__identifier"] == "Door")["entityInstances"][0]
        self.assertEqual(door["fieldInstances"][0]["realEditorValues"], [{"id": "V_String", "params": ["town"]}])

    def test_header_is_the_editors_and_the_tool_mark_is_in_a_layer_doc(self):
        doc = self.built()
        self.assertEqual(doc["__header__"], EDITOR_HEADER)
        self.assertEqual((doc["jsonVersion"], doc["appBuildId"]), ("1.5.3", 473703))
        self.assertTrue(any("room build" in (d.get("doc") or "") for d in doc["defs"]["layers"]))

    def test_level_keeps_its_name_when_the_editor_opens_it(self):
        doc = self.built()
        level = doc["levels"][0]
        self.assertIs(level["useAutoIdentifier"], False)     # true면 편집기가 Level_0으로 바꿔 maps.json과 어긋난다
        self.assertEqual(level["__bgColor"], doc["defaultLevelBgColor"])    # 편집기가 다시 계산해도 같은 바탕

    def test_repaint_of_a_built_room_stays_complete(self):
        doc = self.built()
        path = self.out() / f"{RID}.ldtk"
        col = next(li for li in doc["levels"][0]["layerInstances"] if li["__identifier"] == "Collision")
        w = col["__cWid"]
        col["intGridCsv"][4 * w + 5] = 1                      # 바닥 한 칸을 벽으로 칠한다
        path.write_text(json.dumps(doc), "utf-8")
        self.assertIn(f"{RID}.ldtk", repaint_room(path, kit=KIT))
        self.assertEqual(ldtk_schema.all_problems(json.loads(path.read_text("utf-8"))), [])

    def test_repaint_of_the_editor_fixture_stays_complete(self):
        self.real_plan()
        self.build()
        path = self.out() / f"{RID}.ldtk"
        shutil.copyfile(FIXTURE, path)
        repaint_room(path, kit=KIT)
        self.assertEqual(ldtk_schema.all_problems(json.loads(path.read_text("utf-8"))), [])

    def test_rooms_committed_in_the_repo_have_every_required_and_editor_key(self):
        """저장소의 키트 방(art/world/rooms/*/<id>.ldtk) — 새 집 house는 반드시 든다."""
        found = sorted((ROOT / "art" / "world" / "rooms").glob("*/*.ldtk"))
        self.assertIn(ROOT / "art" / "world" / "rooms" / "house" / "house.ldtk", found)
        for path in found:
            with self.subTest(path.parent.name):
                self.assertEqual(ldtk_schema.all_problems(json.loads(path.read_text("utf-8"))), [], str(path))

    def test_room_with_a_build_layer_and_a_wall_door_has_every_required_and_editor_key(self):
        self.small_plan("t.house", DOOR_PLAN, wallDoors={"W": "town"}, build=DOOR_BUILD)
        self.build("t.house")
        doc = json.loads((self.out("t.house") / "t.house.ldtk").read_text("utf-8"))
        self.assertEqual(ldtk_schema.all_problems(doc), [])
        build_def = next(d for d in doc["defs"]["layers"] if d["identifier"] == "Build")
        self.assertEqual([(v["tile"], v["groupUid"]) for v in build_def["intGridValues"]], [(None, 0)])

    def test_repaint_of_an_editor_saved_room_with_a_build_layer_and_a_wall_door_stays_complete(self):
        shutil.copytree(EDITOR_SAVED, self.out())
        path = self.out() / f"{RID}.ldtk"
        doc = json.loads(path.read_text("utf-8"))
        add_build_and_wall_door(doc, (3, 4))
        self.assertEqual(ldtk_schema.all_problems(doc), [])
        path.write_text(json.dumps(doc), "utf-8")
        repaint_room(path, kit=KIT)
        self.assertEqual(ldtk_schema.all_problems(json.loads(path.read_text("utf-8"))), [])

    def test_an_old_incomplete_room_file_is_refused_by_repaint_and_flagged_by_check(self):
        doc = self.built()
        path = self.out() / f"{RID}.ldtk"
        old = copy.deepcopy(doc)
        for k in ("worlds", "toc", "dummyWorldIid", "flags", "identifierStyle"):
            del old[k]
        path.write_text(json.dumps(old), "utf-8")
        before = {p.name: p.read_bytes() for p in self.out().iterdir()}
        with self.assertRaises(RoomBuildError) as caught:
            repaint_room(path, kit=KIT)
        self.assertEqual([p.code for p in caught.exception.problems], ["room-ldtk-incomplete"])
        self.assertIn("room build", caught.exception.problems[0].message)
        self.assertEqual({p.name: p.read_bytes() for p in self.out().iterdir()}, before)
        self.assertIn("room-ldtk-incomplete", [p.code for p in check_room(path)])


class TestLevelShapeTests(LevelBase):
    def test_test_level_writes_every_required_and_editor_key(self):
        self.export_all()
        out, _ = self.write()
        ldtk, _ = self.read(out)
        self.assertEqual(ldtk_schema.all_problems(ldtk), [])
        self.assertEqual(ldtk["__header__"], EDITOR_HEADER)

    def test_art_entities_have_definitions_whose_tile_is_the_slot(self):
        self.export_all()
        out, _ = self.write()
        ldtk, _ = self.read(out)
        defs = {d["uid"]: d for d in ldtk["defs"]["entities"]}
        art = next(l for l in ldtk["levels"][0]["layerInstances"] if l["__identifier"] == "Art")
        for e in art["entityInstances"]:
            d = defs[e["defUid"]]
            self.assertEqual((d["identifier"], d["tileRect"], d["renderMode"]), (e["__identifier"], e["__tile"], "Tile"))

    def test_the_same_picture_name_in_two_sheets_is_an_error(self):
        self.export_all()
        self.export([self.recipe("bakery.menu-board", "test.menu-board")], "prop", "other")     # 같은 이름, 다른 시트
        with self.assertRaises(ldtk_test_level.TestLevelError):
            self.write()
        self.assertFalse((self.root / "art/world/test" / ldtk_test_level.LDTK_NAME).exists())

    def test_an_old_file_written_with_the_pixel_kit_header_is_still_overwritable(self):
        self.export_all()
        out, _ = self.write()
        ldtk, _ = self.read(out)
        ldtk["__header__"] = {"fileType": "LDtk Project JSON", "app": "pixel-kit"}
        (out / ldtk_test_level.LDTK_NAME).write_text(json.dumps(ldtk), "utf-8")
        self.assertIn("art/world/test/pixel-kit-test.ldtk", self.write(out)[1])

    def test_a_file_saved_by_the_editor_is_still_overwritable(self):
        self.export_all()
        out, _ = self.write()
        path = out / ldtk_test_level.LDTK_NAME
        doc = json.loads(path.read_text("utf-8"))
        path.write_bytes(json.dumps(doc, indent="\t").replace("\n", "\r\n").encode("utf-8"))    # 편집기처럼 탭·CRLF
        self.assertIn("art/world/test/pixel-kit-test.ldtk", self.write(out)[1])


if __name__ == "__main__":
    unittest.main()
