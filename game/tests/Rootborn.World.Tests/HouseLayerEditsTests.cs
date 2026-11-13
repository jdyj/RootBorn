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
    }
}
