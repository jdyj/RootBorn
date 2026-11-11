using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.World.Ldtk;

namespace Rootborn.World.Tests
{
    internal static class Repo
    {
        private static readonly Lazy<string> _root = new Lazy<string>(() =>
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, WorldCatalog.DefaultRelativePath))) return d.FullName;
            throw new DirectoryNotFoundException(WorldCatalog.DefaultRelativePath + "를 찾지 못했다");
        });

        public static string Root => _root.Value;
        public static WorldMapLoader Loader() => WorldMapLoader.FromRepo(Root);
    }

    /// <summary>자동 테스트: 실제 LDtk 파일과 맵 목록으로 맵을 만든다. 화면 근거가 아니다.</summary>
    public sealed class WorldMapTests
    {
        private static readonly string[] AllMaps = { "Town", "House", "ShopInterior", "HospitalInterior", "PoliceInterior", "PoliceShootingInterior", "KitchenInterior", "Kitchen2Interior" };

        [Test]
        public void WorldAssembly_DoesNotReferenceAGameEngine()
        {
            string[] references = typeof(WorldMapLoader).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(references.Where(n => n.Contains("MonoGame") || n.Contains("Xna") || n.Contains("FNA") || n.Contains("Unity")), Is.Empty);
        }

        [Test]
        public void Shop_IsReadStraightFromTheLdtkLevelFile()
        {
            LoadedMap shop = Repo.Loader().LoadMap("Shop");
            Assert.That(shop.Entry.Id, Is.EqualTo("ShopInterior"), "레벨 이름으로도 찾는다");
            Assert.That(shop.World.Grid.Columns, Is.EqualTo(23));
            Assert.That(shop.World.Grid.Rows, Is.EqualTo(13));
            Assert.That(shop.World.Grid.CellSize, Is.EqualTo(32));
            Assert.That(shop.Level.Layers[0].Name, Is.EqualTo("Ground"), "아래 층부터");
            Assert.That(shop.Level.Layer("Ground").Tiles.Count, Is.EqualTo(149));
            Assert.That(shop.Level.Layer("Shop").Tiles.Count, Is.EqualTo(95));
            DoorArea door = shop.World.Doors.Single();
            Assert.That(door.Target, Is.EqualTo("town"));
            Assert.That(door.Destination, Is.EqualTo(new TravelTarget("Town", "town-shop-entry")));
            Assert.That(door.Area, Is.EqualTo(new AreaRect(288, 64, 64, 64)), "Unity 씬의 InteriorLdtkDoor_town_18_2: 가운데 (10,-2), 2x2 — 그림 기준으로는 LDtk 자리보다 한 줄 아래");
            Assert.That(shop.Problems, Is.Empty);
        }

        [Test]
        public void Town_SnowTilesetOutsideTheRepoIsSkippedAndReported()
        {
            var project = LdtkProject.Load(Path.Combine(Repo.Root, "Assets/LDtk/Town/town.ldtk"));
            LdtkTileset snow = project.Tilesets.Values.Single(t => t.Identifier.StartsWith("Snow"));
            Assert.That(snow.Missing, Is.True);
            Assert.That(project.Tilesets.Values.Count(t => t.Missing), Is.EqualTo(1));
            LoadedMap town = Repo.Loader().LoadMap("Town");
            Assert.That(town.World.Grid.Columns, Is.EqualTo(90));
            Assert.That(town.World.Grid.Rows, Is.EqualTo(63));
            Assert.That(town.Picture.Textures.Any(t => t.Contains("town_snow")), Is.False);
        }

        [Test]
        public void Town_DoorsMatchTheUnityScenePortals()
        {
            LoadedMap town = Repo.Loader().LoadMap("Town");
            var active = town.World.Doors.Where(d => d.Destination.HasValue).ToDictionary(d => d.Target);
            // Unity 씬 Town.unity의 TownLdtkDoor_* 위치(가운데 x, y 유닛)와 크기를 그림 기준 픽셀 (x*32, (1-y)*32)로 옮긴 값
            Assert.That(active["house"].Area, Is.EqualTo(AreaRect.Centered(18.5f * 32, 14f * 32, 32, 64)));
            Assert.That(active["shopping center"].Area, Is.EqualTo(AreaRect.Centered(62f * 32, 12f * 32, 64, 64)));
            Assert.That(active["hospital"].Area, Is.EqualTo(AreaRect.Centered(50f * 32, 43f * 32, 64, 64)));
            Assert.That(active["police"].Area, Is.EqualTo(AreaRect.Centered(24.5f * 32, 40f * 32, 32, 64)));
            Assert.That(active["kitchen"].Area, Is.EqualTo(AreaRect.Centered(24.5f * 32, 56f * 32, 32, 64)));
            Assert.That(active.Count, Is.EqualTo(5), "값이 비었거나 표에 없는 문(north 등)은 열리지 않는다");
        }

        private static HashSet<(int, int)> UnityBaked(string mapId)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "unity-baked-collision.json");
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            var set = new HashSet<(int, int)>();
            foreach (JsonElement c in doc.RootElement.GetProperty("maps").GetProperty(mapId).EnumerateArray()) set.Add((c[0].GetInt32(), c[1].GetInt32()));
            return set;
        }

        [TestCase("ShopInterior")]
        [TestCase("HospitalInterior")]
        [TestCase("PoliceInterior")]
        [TestCase("PoliceShootingInterior")]
        [TestCase("KitchenInterior")]
        [TestCase("Kitchen2Interior")]
        [TestCase("Town")]
        public void Collision_MatchesWhatUnityBakedIntoTheScene(string mapId)
        {
            // Unity 판은 그림 타일을 LDtk 행 r → Unity 칸 -r에, 문·칠한 칸은 LDtk 픽셀 좌표 그대로(행 r → 칸 -r-1) 놓아
            // 문 상자와 칠한 칸이 그림보다 한 줄 아래에 있었다. 맵 목록의 entityRowShift 1이 그것을 재현한다. 굽기 결과가 칸 하나까지 같아야 한다.
            CollisionGrid grid = Repo.Loader().LoadMap(mapId).World.Collision;
            HashSet<(int, int)> expected = UnityBaked(mapId);
            var actual = new HashSet<(int, int)>(grid.BlockedCells().Select(c => (c.X, c.Y)));
            var missing = expected.Except(actual).OrderBy(c => c).ToList();
            var extra = actual.Except(expected).OrderBy(c => c).ToList();
            Assert.That(missing.Count + extra.Count, Is.EqualTo(0),
                mapId + " Unity 판에만 막힘: " + string.Join(" ", missing) + " / 여기서만 막힘: " + string.Join(" ", extra));
        }

        [Test]
        public void EveryIndoorSpawn_IsOnOpenFloorAwayFromExitsWithEveryExitReachable()
        {
            // Unity 판 굽기가 실내 씬마다 저장 전에 하던 검사. 마을은 Unity 판도 검사하지 않았다(아래 Town_ 테스트).
            WorldMapLoader loader = Repo.Loader();
            var problems = new List<string>();
            foreach (string id in AllMaps.Where(m => m != "Town"))
            {
                WorldMap map = loader.LoadMap(id).World;
                foreach (string spawn in map.SpawnIds)
                {
                    map.TryGetSpawn(spawn, out var point);
                    foreach (string p in ArrivalRules.Check(map.Collision, map.Doors, point)) problems.Add(id + "/" + spawn + ": " + p);
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Town_SpawnsAreOnOpenFloorAndEveryDoorButTheHospitalCanBeWalkedTo()
        {
            // 걸어서 닿는다 = 문 칸에 들어가거나 문 상자에 맞닿은 열린 칸까지 갈 수 있다(Unity 판 접촉 여유).
            WorldMap town = Repo.Loader().LoadMap("Town").World;
            int size = town.Grid.CellSize;
            town.TryGetSpawn("town-entry", out var start);
            var reached = new HashSet<CellPos>();
            CellPos from = CellPos.Containing(start.X, start.Y, size);
            reached.Add(from);
            GridPath.Find(town.Collision, from, c => { reached.Add(c); return false; });
            foreach (string spawn in town.SpawnIds)
            {
                town.TryGetSpawn(spawn, out var p);
                Assert.That(reached, Does.Contain(CellPos.Containing(p.X, p.Y, size)), spawn + "는 마을 길과 이어진 열린 칸이어야 한다");
            }
            var walkable = new List<string>();
            foreach (DoorArea door in town.Doors.Where(d => d.Destination.HasValue))
            {
                var grown = new AreaRect(door.Area.X - 1, door.Area.Y - 1, door.Area.W + 2, door.Area.H + 2);
                if (reached.Any(c => new AreaRect(c.X * size, c.Y * size, size, size).Overlaps(grown))) walkable.Add(door.Target);
            }
            CollectionAssert.AreEquivalent(new[] { "house", "shopping center", "police", "kitchen" }, walkable,
                "병원 문은 Unity 판에서도 건물 계단 타일(칸 전체 충돌) 세 줄에 막혀 걸어서 닿지 않는다 — LDtk를 고칠지는 사용자 결정");
        }

        [Test]
        public void EveryDoorDestination_IsAMapWithThatSpawn()
        {
            WorldMapLoader loader = Repo.Loader();
            var problems = new List<string>();
            foreach (string id in AllMaps)
                foreach (DoorArea door in loader.LoadMap(id).World.Doors)
                {
                    if (!door.Destination.HasValue) continue;
                    TravelTarget t = door.Destination.Value;
                    WorldMap target = loader.LoadMap(t.MapId).World;
                    if (!target.TryGetSpawn(t.SpawnId, out _)) problems.Add(id + " 문 " + door.Target + " → " + t + " 도착 지점 없음");
                }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void EveryCatalogSpawnId_IsOneUnityKnewForThatScene()
        {
            // Unity 판 WorldDestinationDefinition(data/locations/WorldDestinationDefinition.json)의 sceneName·spawnIds와 맞춘다.
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo.Root, "data/locations/WorldDestinationDefinition.json")));
            var known = new Dictionary<string, HashSet<string>>();
            foreach (JsonElement d in doc.RootElement.EnumerateArray())
                known[d.GetProperty("sceneName").GetString()] = new HashSet<string>(d.GetProperty("spawnIds").EnumerateArray().Select(s => s.GetString()));
            foreach (MapEntry entry in Repo.Loader().Catalog.Maps)
            {
                Assert.That(known.ContainsKey(entry.Id), entry.Id + "는 Unity 판 씬 이름이어야 한다");
                foreach (string spawn in entry.Spawns.Keys) Assert.That(known[entry.Id], Does.Contain(spawn), entry.Id + "/" + spawn);
            }
        }

        [Test]
        public void ShopPicture_DrawsEveryTileLayerUnderTheCharacterInLdtkOrder()
        {
            LoadedMap shop = Repo.Loader().LoadMap("ShopInterior");
            Assert.That(shop.Picture.Sorted, Is.Empty, "실내 타일 층은 모두 캐릭터 아래(Unity 판 정렬값)");
            int total = shop.Level.Layers.Where(l => l.Type == "Tiles").Sum(l => l.Tiles.Count);
            Assert.That(shop.Picture.Under.Count, Is.EqualTo(total));
            Assert.That(shop.Picture.Under[0].Texture, Does.EndWith("Room_Builder_Floors_32x32.png"));
        }
    }
}
