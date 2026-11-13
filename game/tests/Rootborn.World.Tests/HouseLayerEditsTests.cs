using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Rootborn.Core.Building;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음): 짓는 집의 LDtk를 편집기에서 손봤을 때 — 게임이 대신 그리는 층(build.replacesLayers)을 비우거나 숨기거나 지우거나,
    /// 그 두 층 사이에 다른 층을 끼우거나, 조각 시트 좌표표가 깨졌을 때. 저장소의 집 LDtk·좌표표는 고치지 않는다 — 임시 폴더의 사본을 고친다.
    /// 화면 근거가 아니다(화면은 Host HouseTilesHostTests).
    /// </summary>
    public sealed class HouseLayerEditsTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rootborn-house-layers-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch (IOException) { }
        }

        /// <summary>
        /// 집 LDtk를 임시 폴더에 옮겨(같은 폴더의 그림도) 그 레벨의 layerInstances를 고치고, 맵 목록 사본의 집 항목이 그 사본을 가리키게 한다.
        /// </summary>
        private (WorldMapLoader Loader, MapEntry Entry) HouseWith(Action<JsonArray> edit)
        {
            WorldCatalog catalog = WorldCatalog.Parse(File.ReadAllText(Path.Combine(Repo.Root, WorldCatalog.DefaultRelativePath)));
            MapEntry entry = catalog.Maps.Single(m => m.Build != null);
            string source = Path.Combine(Repo.Root, entry.Ldtk);
            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(source)))
                if (!file.EndsWith(".ldtk", StringComparison.OrdinalIgnoreCase)) File.Copy(file, Path.Combine(_dir, Path.GetFileName(file)));
            JsonNode root = JsonNode.Parse(File.ReadAllText(source));
            JsonArray levels = root["levels"].AsArray();
            JsonNode level = levels.FirstOrDefault(l => (string)l["identifier"] == entry.Level) ?? levels[0];
            edit(level["layerInstances"].AsArray());
            string copy = Path.Combine(_dir, Path.GetFileName(source));
            File.WriteAllText(copy, root.ToJsonString());
            entry.Ldtk = copy;
            return (new WorldMapLoader(Repo.Root, catalog), entry);
        }

        private static JsonNode Layer(JsonArray layers, string name) => layers.Single(l => (string)l["__identifier"] == name);

        private static void Empty(JsonArray layers, string name) { Layer(layers, name)["gridTiles"] = new JsonArray(); Layer(layers, name)["autoLayerTiles"] = new JsonArray(); }

        private static void Hide(JsonArray layers, string name) => Layer(layers, name)["visible"] = false;

        private static ComposedMapTiles Compose(LoadedMap map) =>
            ComposedMapTiles.Compose(map, new RoomTileCompositor(HouseMapTests.Sheets(), new RoomFinishArt(Path.Combine(Repo.Root, "data"))),
                id => Repo.Definitions.TryGet(id, out RoomFinishDefinition d) ? d : null);

        private static List<string> Checked(WorldMapLoader loader, MapEntry entry, RoomFinishArt art = null) =>
            MapChecks.Check(loader, InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath)), Repo.Definitions,
                    art ?? new RoomFinishArt(Path.Combine(Repo.Root, "data")))
                .Where(p => p.MapId == entry.Id).Select(p => p.Problem).ToList();

        private static IEnumerable<TestCaseData> EmptiedOrHidden()
        {
            yield return new TestCaseData((Action<JsonArray>)(l => Empty(l, "Wall"))).SetName("Wall_Emptied");
            yield return new TestCaseData((Action<JsonArray>)(l => Hide(l, "Ground"))).SetName("Ground_Hidden");
            yield return new TestCaseData((Action<JsonArray>)(l => { Empty(l, "Wall"); Empty(l, "Ground"); })).SetName("Both_Emptied");
            yield return new TestCaseData((Action<JsonArray>)(l => { Hide(l, "Wall"); Hide(l, "Ground"); })).SetName("Both_Hidden");
            yield return new TestCaseData((Action<JsonArray>)(l => { Empty(l, "Wall"); Hide(l, "Ground"); })).SetName("Wall_Emptied_Ground_Hidden");
        }

        [TestCaseSource(nameof(EmptiedOrHidden))]
        public void AReplacedLayerThatIsEmptyOrHidden_StillHasAPlace_AndTheComposedCellsAreDrawnThere(Action<JsonArray> edit)
        {
            var (loader, entry) = HouseWith(edit);
            Assert.That(entry.Build.ReplacesLayers, Is.EquivalentTo(new[] { "Wall", "Ground" }), "전제: 이 시험이 고치는 두 층이 바꿔 그릴 층이다");
            LoadedMap map = loader.LoadMap(entry.Id);
            Assert.That(map.Tiles, Is.Not.Null);
            Assert.That(map.Picture.ReplacedAt, Is.InRange(0, map.Picture.Under.Count), "바꿔 그릴 층을 만난 자리(타일이 없거나 숨겨져도)");
            ComposedMapTiles tiles = Compose(map);
            Assert.That(tiles.Ok, Is.True, string.Join("\n", tiles.Problems));
            Assert.That(tiles.InsertAt, Is.EqualTo(map.Picture.ReplacedAt), "조합 칸은 그 자리에 끼운다");
            Assert.That(tiles.Draws.Count, Is.EqualTo(map.Tiles.Cells.Count));
            Assert.That(Checked(loader, entry), Is.Empty, "층을 비우거나 숨기는 것은 문제가 아니다(게임이 대신 그린다)");
        }

        [Test]
        public void BothReplacedLayersMissing_IsAMapProblem_AndTheComposedCellsAreDrawnAtTheBottom()
        {
            var (loader, entry) = HouseWith(l =>
            {
                l.Remove(Layer(l, "Wall"));
                l.Remove(Layer(l, "Ground"));
            });
            LoadedMap map = loader.LoadMap(entry.Id);
            Assert.That(map.Tiles, Is.Not.Null, "출처표는 Collision에서 만든다");
            Assert.That(map.Picture.ReplacedAt, Is.EqualTo(-1), "건너뛸 층이 레벨에 없다");
            Assert.That(Checked(loader, entry), Has.Some.StartsWith("짓는 집: 바꿔 그릴 층 Wall").And.Some.StartsWith("짓는 집: 바꿔 그릴 층 Ground"));
            ComposedMapTiles tiles = Compose(map);
            Assert.That(tiles.Ok, Is.True, string.Join("\n", tiles.Problems));
            Assert.That(tiles.InsertAt, Is.EqualTo(0), "그릴 자리가 없으면 캐릭터 아래 층의 맨 아래에 그린다(빈 화면이 되지 않는다)");
            Assert.That(tiles.Draws.Count, Is.EqualTo(map.Tiles.Cells.Count));
        }

        [Test]
        public void AnotherTileLayerBetweenTheReplacedLayers_IsAMapProblem()
        {
            // Wall 층을 본뜬 타일 층 하나를 Wall과 Ground 사이에 끼운다(타일 하나). 조합 칸은 한 번에 그려지므로 그 층은 벽 아래로 들어가지 못한다.
            var (loader, entry) = HouseWith(l =>
            {
                JsonNode wall = Layer(l, "Wall");
                JsonNode between = wall.DeepClone();
                between["__identifier"] = "Decor";
                JsonArray one = new JsonArray(wall["gridTiles"].AsArray()[0].DeepClone());
                between["gridTiles"] = one;
                l.Insert(l.IndexOf(wall) + 1, between); // layerInstances는 위 층 먼저 — Wall 다음 = Wall 바로 아래
            });
            LoadedMap map = loader.LoadMap(entry.Id);
            int ground = map.Level.Layers.FindIndex(x => x.Name == "Ground"), decor = map.Level.Layers.FindIndex(x => x.Name == "Decor"),
                wallAt = map.Level.Layers.FindIndex(x => x.Name == "Wall");
            Assert.That(ground < decor && decor < wallAt, Is.True, "전제: 그리기 순서 Ground → Decor → Wall");
            Assert.That(Checked(loader, entry), Has.Some.Contains("Decor").And.Some.StartsWith("짓는 집: 바꿔 그릴 층 사이"));
        }

        [Test]
        public void AHiddenOrEmptyLayerBetweenTheReplacedLayers_IsNotAProblem()
        {
            var (loader, entry) = HouseWith(l =>
            {
                JsonNode wall = Layer(l, "Wall");
                JsonNode hidden = wall.DeepClone();
                hidden["__identifier"] = "Hidden";
                hidden["visible"] = false;
                JsonNode empty = wall.DeepClone();
                empty["__identifier"] = "Empty";
                empty["gridTiles"] = new JsonArray();
                l.Insert(l.IndexOf(wall) + 1, hidden);
                l.Insert(l.IndexOf(wall) + 1, empty);
            });
            Assert.That(Checked(loader, entry), Is.Empty, "그려지지 않는 층은 앞뒤를 바꾸지 않는다");
        }

        [Test]
        public void ABrokenAtlas_IsAMapProblemLine_NotAnException()
        {
            // 기본 벽 꾸밈 시트의 좌표표 사본을 JSON이 아닌 글로 → 맵 검사는 던지지 않고 문제 한 줄을 낸다.
            var (loader, entry) = HouseWith(_ => { });
            RoomFinishDefinition wall = Repo.Definitions.Get<RoomFinishDefinition>(entry.Build.DefaultWall);
            string data = Path.Combine(_dir, "data");
            string atlas = Path.Combine(data, "atlases", wall.Sheet + ".atlas.json");
            Directory.CreateDirectory(Path.GetDirectoryName(atlas));
            File.WriteAllText(atlas, "{ \"image\": ");
            List<string> found = null;
            Assert.DoesNotThrow(() => found = Checked(loader, entry, new RoomFinishArt(data)));
            Assert.That(found, Has.Some.StartsWith("짓는 집: 시트 " + wall.Sheet + "의 좌표표를 읽지 못했다"));
        }
    }
}
