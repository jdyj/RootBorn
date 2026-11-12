using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.World;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(화면 근거 아님): 맵의 부지에 건물을 얹는다. 고정 파일 Fixtures/buildings(12x10칸 마당 + 2단계 건물).
    /// 설계 docs/superpowers/specs/2026-11-12-building-lots-doors-design.md 6·8절.
    /// </summary>
    public sealed class BuildingLotMapTests
    {
        private const string FixtureDir = "game/tests/Rootborn.World.Tests/Fixtures/buildings";
        private static string Fixture(string name) => Path.Combine(Repo.Root, FixtureDir, name);
        private static string MapsJson => File.ReadAllText(Fixture("maps.json"));
        private static string BuildingsJson => File.ReadAllText(Fixture("buildings.json"));

        /// <summary>시험용 단계 종류: 테스트가 값을 바꾼다.</summary>
        private sealed class SettableLevel : IBuildingLevelKind
        {
            public int Value = 1;
            public int Reads;
            public int LevelOf(LotLevel level) { Reads++; return Value; }
        }

        private static WorldMapLoader Loader(SettableLevel level, string maps = null, string buildings = null)
        {
            var kinds = BuildingLevelKinds.WithFixed().Add("test-level", level);
            string json = (maps ?? MapsJson).Replace("\"kind\": \"fixed\", \"value\": 1", "\"kind\": \"test-level\"");
            return new WorldMapLoader(Repo.Root, WorldCatalog.Parse(json), BuildingCatalog.Parse(buildings ?? BuildingsJson), kinds);
        }

        [Test]
        public void LOTMAP_001_LotsAreReadFromTheMapListAndBuildingsFromTheirOwnFile()
        {
            MapEntry yard = WorldCatalog.Parse(MapsJson).Find("Yard");
            LotEntry lot = yard.Lots.Single();
            Assert.That(lot.Name, Is.EqualTo("shop-lot"));
            Assert.That(lot.Building, Is.EqualTo("shop"));
            Assert.That(lot.DoorCell, Is.EqualTo(new CellPos(6, 6)));
            Assert.That(lot.Cells, Is.EqualTo(new CellRect(2, 1, 9, 7)));
            Assert.That((lot.Level.Kind, lot.Level.Value), Is.EqualTo(("fixed", 1)));
            Assert.That(lot.To, Is.EqualTo(new TravelTarget("Inside", "inside-entry")));
            Assert.That(lot.OpenSeconds, Is.Null);
            Assert.That(WorldCatalog.Parse(MapsJson).Find("Inside").Lots, Is.Empty);

            BuildingData shop = BuildingCatalog.Parse(BuildingsJson).Find("shop");
            Assert.That(shop.Levels.Count, Is.EqualTo(2));
            Assert.That(shop.Levels[1].Src, Is.EqualTo(new ImageRect(80, 0, 112, 80)));
            Assert.That(shop.Levels[1].Solid, Is.EqualTo(new CellRect(0, 1, 7, 4)));
            Assert.That(shop.Levels[1].DoorCell, Is.EqualTo(new CellPos(2, 4)));
            Assert.That(shop.Levels[0].DoorFrames.Count, Is.EqualTo(3));
            Assert.That((shop.Levels[0].DoorAtX, shop.Levels[0].DoorAtY), Is.EqualTo((16, 32)));
        }

        [Test]
        public void LOTMAP_002_BothLevelsKeepTheDoorWhereItIsAndOnlyTheBlockedCellsChange()
        {
            var level = new SettableLevel { Value = 1 };
            WorldMapLoader loader = Loader(level);
            LoadedMap one = loader.LoadMap("Yard");
            Assert.That(one.Problems, Is.Empty);
            Assert.That(one.LotProblems, Is.Empty, string.Join("\n", one.LotProblems));
            DoorArea door1 = one.World.Doors.Single();
            Assert.That(door1.Area, Is.EqualTo(new AreaRect(6 * 16, 6 * 16, 16, 16)), "문 상자는 문 칸 한 칸");
            Assert.That(door1.Destination, Is.EqualTo(new TravelTarget("Inside", "inside-entry")));
            Assert.That(one.World.Collision.IsBlocked(6, 6), Is.False, "문 칸은 막지 않는다");
            Assert.That(one.World.Collision.IsBlocked(5, 6), Is.True);
            Assert.That(one.World.Collision.IsBlocked(4, 6), Is.False);
            Assert.That(one.World.Collision.IsBlocked(5, 3), Is.False, "지붕 위 줄은 solid 밖");

            level.Value = 2;
            LoadedMap two = loader.LoadMap("Yard");
            Assert.That(two.LotProblems, Is.Empty, string.Join("\n", two.LotProblems));
            Assert.That(two.World.Doors.Single().Area, Is.EqualTo(door1.Area), "건물이 커져도 문 자리는 그대로");
            Assert.That(two.World.Collision.IsBlocked(4, 6), Is.True, "2단계는 더 넓게 막는다");
            Assert.That(two.World.Collision.IsBlocked(10, 3), Is.True);
            Assert.That(two.World.Collision.IsBlocked(6, 6), Is.False);
            Assert.That(two.World.Collision.IsBlocked(6, 7), Is.False, "문 앞");
        }

        [Test]
        public void LOTMAP_003_BuildingIsDrawnSortedByItsBottomEdgeWithItsTextureListed()
        {
            WorldMapLoader loader = Loader(new SettableLevel { Value = 1 });
            LoadedMap map = loader.LoadMap("Yard");
            string png = Path.GetFullPath(Fixture("shop.png"));
            Assert.That(map.Picture.Textures, Does.Contain(png));
            SpriteDraw building = map.Picture.Sorted.Single();
            Assert.That((building.SrcX, building.SrcY, building.SrcW, building.SrcH), Is.EqualTo((0, 0, 80, 64)));
            Assert.That((building.X, building.Y, building.W, building.H), Is.EqualTo((5f * 16, 3f * 16, 80f, 64f)), "왼쪽 위 = 문 칸 - 건물의 문 칸");
            Assert.That(building.SortY, Is.EqualTo(7f * 16), "발 위치 = 건물 아래 끝(캐릭터가 건물 뒤로 가면 가려진다)");

            BuildingDoorArt art = map.DoorArt.Single();
            Assert.That(art.Door, Is.SameAs(map.World.Doors.Single()));
            Assert.That(art.SortedIndex, Is.EqualTo(0));
            Assert.That(art.Frames.Count, Is.EqualTo(3));
            Assert.That((art.Frames[0].X, art.Frames[0].Y, art.Frames[0].W, art.Frames[0].H), Is.EqualTo((5f * 16 + 16, 3f * 16 + 32, 16f, 32f)));
            Assert.That(art.Frames.Select(f => f.SrcX), Is.EqualTo(new[] { 192, 208, 224 }));
            Assert.That(map.DoorArtFor(map.World.Doors.Single()), Is.SameAs(art));
            Assert.That(map.DoorArtFor(new DoorArea("x", default, null)), Is.Null);
        }

        [Test]
        public void LOTMAP_004_FrameAtGoesFromClosedToOpenAsTheDoorOpens()
        {
            BuildingDoorArt art = Loader(new SettableLevel()).LoadMap("Yard").DoorArt.Single();
            Assert.That(art.FrameAt(0f), Is.EqualTo(0));
            Assert.That(art.FrameAt(0.3f), Is.EqualTo(0));
            Assert.That(art.FrameAt(0.34f), Is.EqualTo(1));
            Assert.That(art.FrameAt(0.7f), Is.EqualTo(2));
            Assert.That(art.FrameAt(1f), Is.EqualTo(2));
            Assert.That(art.FrameAt(-1f), Is.EqualTo(0));
            // 0.35초 문을 60fps로: 보이는 마지막 진행도(20/21)에서 마지막 프레임.
            Assert.That(art.FrameAt(20f / 60f / DoorWatch.UnityOpeningSeconds), Is.EqualTo(2));
        }

        [Test]
        public void LOTMAP_005_WalkingIntoTheBuildingDoorArrivesInside()
        {
            WorldMapLoader loader = Loader(new SettableLevel { Value = 2 });
            var session = new WorldSession(loader, loader.Catalog.Walker);
            Assert.That(session.Enter("Yard", "front"), Is.True);
            bool travelled = false;
            var progress = new List<float>();
            for (int i = 0; i < 240 && !travelled; i++)
            {
                travelled = session.Update(0, -1, 1f / 60f);
                if (session.OpeningDoor != null) progress.Add(session.OpeningProgress);
            }
            Assert.That(travelled, Is.True);
            Assert.That(session.Map.Id, Is.EqualTo("Inside"));
            Assert.That((session.Walker.X, session.Walker.Y), Is.EqualTo((48f, 72f)));
            Assert.That(progress, Is.Ordered, "열림 진행도는 올라가기만 한다");
            Assert.That(progress.First(), Is.EqualTo(0f));
        }

        [Test]
        public void LOTMAP_006_MapIsRebuiltOnlyWhenALotsLevelChanges()
        {
            var level = new SettableLevel { Value = 1 };
            WorldMapLoader loader = Loader(level);
            LoadedMap first = loader.LoadMap("Yard");
            Assert.That(loader.LoadMap("Yard"), Is.SameAs(first), "단계가 같으면 기억한 맵");
            Assert.That(loader.IsStale(first), Is.False);
            LoadedMap inside = loader.LoadMap("Inside");
            Assert.That(loader.LoadMap("Inside"), Is.SameAs(inside), "부지가 없는 맵은 한 번만 만든다");

            level.Value = 2;
            Assert.That(loader.IsStale(first), Is.True);
            LoadedMap second = loader.LoadMap("Yard");
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Level, Is.SameAs(first.Level), "LDtk 레벨은 다시 읽지 않는다");
            Assert.That(loader.LoadMap("Yard"), Is.SameAs(second));
            level.Value = 7; // 단계 수보다 크다 → 가장 높은 단계
            LoadedMap clamped = loader.LoadMap("Yard");
            Assert.That(clamped.Picture.Sorted.Single().SrcX, Is.EqualTo(80));
            Assert.That(clamped.LotProblems, Is.Empty);
        }

        [Test]
        public void LOTMAP_007_ReloadAfterTheBuildingGrewLeavesTheBodyOnAnOpenCell()
        {
            var level = new SettableLevel { Value = 1 };
            WorldMapLoader loader = Loader(level);
            var session = new WorldSession(loader, loader.Catalog.Walker);
            session.Enter("Yard", "front");
            session.Walker.PlaceAt(CellPos.Center(new CellPos(4, 5), 16)); // 준비: 1단계 건물 옆, 2단계에서는 건물 안이 되는 칸
            Assert.That(session.Map.Collision.AnyBlocked(session.Walker.Box), Is.False);

            level.Value = 2;
            Assert.That(session.Reload(), Is.True);
            Assert.That(session.Map, Is.SameAs(loader.LoadMap("Yard").World));
            Assert.That(session.Map.Collision.AnyBlocked(session.Walker.Box), Is.False, "몸은 열린 칸에 있다");
            Assert.That(session.Map.Collision.IsBlocked(4, 5), Is.True);
        }

        [Test]
        public void LOTMAP_008_BrokenLotsAndBuildingsAreReportedAsProblems()
        {
            string maps = MapsJson.Replace(@"""lots"": [", @"""lots"": [
                { ""name"": ""ghost-lot"", ""building"": ""no-such-building"", ""doorCell"": [ 1, 9 ], ""cells"": [ 0, 8, 2, 2 ], ""level"": { ""kind"": ""fixed"", ""value"": 1 } },
                { ""name"": ""odd-lot"", ""building"": ""odd"", ""doorCell"": [ 11, 9 ], ""cells"": [ 10, 7, 4, 4 ], ""level"": { ""kind"": ""no-such-kind"" } },
                { ""name"": ""tight-lot"", ""building"": ""shop"", ""doorCell"": [ 6, 6 ], ""cells"": [ 5, 3, 5, 4 ], ""level"": { ""kind"": ""fixed"", ""value"": 2 } },");
            string buildings = BuildingsJson.Replace(@"""buildings"": [", @"""buildings"": [
                { ""id"": ""odd"", ""levels"": [ { ""texture"": ""no/such.png"", ""src"": [ 0, 0, 20, 32 ], ""solid"": [ 0, 0, 1, 2 ], ""door"": { ""cell"": [ 0, 1 ] } } ] },
                { ""id"": ""doorless"", ""levels"": [ { ""texture"": """ + FixtureDir + @"/shop.png"", ""src"": [ 0, 0, 32, 32 ], ""solid"": [ 0, 0, 2, 1 ], ""door"": { ""cell"": [ 1, 1 ] } } ] },
                { ""id"": ""empty"", ""levels"": [] },");
            var loader = new WorldMapLoader(Repo.Root, WorldCatalog.Parse(maps), BuildingCatalog.Parse(buildings), BuildingLevelKinds.WithFixed());
            List<string> problems = loader.LoadMap("Yard").LotProblems;
            string all = string.Join("\n", problems);
            Assert.That(problems, Has.Some.Contains("no-such-building").And.Contains("없다"), all);
            Assert.That(problems, Has.Some.Contains("no-such-kind"), all);
            Assert.That(problems, Has.Some.Contains("odd-lot").And.Contains("맵 밖"), all);
            Assert.That(problems, Has.Some.Contains("odd").And.Contains("칸 크기"), all);
            Assert.That(problems, Has.Some.Contains("tight-lot").And.Contains("밖으로"), all);
            Assert.That(problems, Has.Some.Contains("tight-lot").And.Contains("shop-lot").And.Contains("겹친다"), all);

            string more = MapsJson.Replace(@"""lots"": [", @"""lots"": [
                { ""name"": ""d-lot"", ""building"": ""doorless"", ""doorCell"": [ 1, 9 ], ""cells"": [ 0, 7, 2, 3 ], ""level"": { ""kind"": ""fixed"", ""value"": 1 } },
                { ""name"": ""e-lot"", ""building"": ""empty"", ""doorCell"": [ 11, 0 ], ""cells"": [ 11, 0, 1, 1 ], ""level"": { ""kind"": ""fixed"", ""value"": 1 } },");
            List<string> second = new WorldMapLoader(Repo.Root, WorldCatalog.Parse(more), BuildingCatalog.Parse(buildings), BuildingLevelKinds.WithFixed()).LoadMap("Yard").LotProblems;
            string all2 = string.Join("\n", second);
            Assert.That(second, Has.Some.Contains("doorless").And.Contains("문 칸"), all2);
            Assert.That(second, Has.Some.Contains("empty").And.Contains("단계"), all2);
        }

        [Test]
        public void LOTMAP_009_ABuildingWithoutADestinationIsBackgroundWithItsDoorCellBlocked()
        {
            string maps = System.Text.RegularExpressions.Regex.Replace(MapsJson, @",\s*""to"": \{[^}]*\}", "");
            Assert.That(maps, Does.Not.Contain("inside-entry\" }"));
            LoadedMap map = new WorldMapLoader(Repo.Root, WorldCatalog.Parse(maps), BuildingCatalog.Parse(BuildingsJson), BuildingLevelKinds.WithFixed()).LoadMap("Yard");
            Assert.That(map.World.Doors, Is.Empty, "열리지 않는 건물(배경)");
            Assert.That(map.World.Collision.IsBlocked(6, 6), Is.True);
            Assert.That(map.DoorArt, Is.Empty);
            Assert.That(map.LotProblems, Is.Empty);
        }

        [Test]
        public void LOTMAP_010_LotOpenSecondsAndMapDoorOpenSecondsAreUsedInThatOrder()
        {
            string withMap = MapsJson.Replace(@"""viewCells"": 10,", @"""viewCells"": 10, ""doorOpenSeconds"": 0.5,");
            LoadedMap mapTime = new WorldMapLoader(Repo.Root, WorldCatalog.Parse(withMap), BuildingCatalog.Parse(BuildingsJson), BuildingLevelKinds.WithFixed()).LoadMap("Yard");
            Assert.That(mapTime.World.Doors.Single().OpenSeconds, Is.EqualTo(0.5f));
            string withLot = withMap.Replace(@"""to"": {", @"""openSeconds"": 0.6, ""to"": {");
            LoadedMap lotTime = new WorldMapLoader(Repo.Root, WorldCatalog.Parse(withLot), BuildingCatalog.Parse(BuildingsJson), BuildingLevelKinds.WithFixed()).LoadMap("Yard");
            Assert.That(lotTime.World.Doors.Single().OpenSeconds, Is.EqualTo(0.6f));
        }

        [Test]
        public void LOTMAP_011_FurnitureRoomMapsWithLotsAreAMapProblem()
        {
            var loader = new WorldMapLoader(Repo.Root, WorldCatalog.Parse(MapsJson), BuildingCatalog.Parse(BuildingsJson), BuildingLevelKinds.WithFixed());
            const string things = @"{ ""radiusCells"": 1.75, ""npcPriority"": 30, ""doorPriority"": 110, ""maps"": { ""Yard"": { ""placementSurface"": ""yard-floor"", ""things"": [] } } }";
            List<string> found = MapChecks.Check(loader, InteractionData.Parse(things)).Select(p => p.ToString()).ToList();
            Assert.That(found, Has.Some.StartsWith("Yard: 부지: ").And.Contains("가구 방"), string.Join("\n", found));
            List<string> plain = MapChecks.Check(loader, InteractionData.Parse(things.Replace(@"""placementSurface"": ""yard-floor"", ", ""))).Select(p => p.ToString()).ToList();
            Assert.That(plain, Is.Empty, "고정 맵은 맵 약속을 지킨다: " + string.Join("\n", plain));
        }

        [Test]
        public void LOTMAP_012_MapsWithoutLotsDoNotAskForLevels()
        {
            var level = new SettableLevel();
            WorldMapLoader loader = Loader(level);
            loader.LoadMap("Inside");
            loader.LoadMap("Inside");
            Assert.That(level.Reads, Is.EqualTo(0));
        }
    }
}
