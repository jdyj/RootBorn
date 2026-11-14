using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.World;
using Rootborn.Host.World;
using Rootborn.World;
using Rootborn.World.Ldtk;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 짓는 집의 벽·바닥을 게임이 칸마다 조합해 그린다(작업 9 — 꾸밈은 맵의 기본값만). 실제 창과 게임 루프(장치 이벤트 주입, OS 입력 계층 미확인).
    /// 걷기·문 지나기는 PlayerDriver의 동사(걸어가기, 문 지나기)뿐 — 순간이동·내부 명령 없음. 칸 좌표를 적지 않는다: 찍을 자리는 게임이 읽은 출처표에서 고른다.
    /// 그림 색은 단언하지 않는다 — 두 경로(GPU에 올린 조합 텍스처 / 키트가 구워 LDtk에 칠한 타일)가 같은 그림을 내는지만 본다.
    /// 스크린샷은 직접 연다: output/house-building/stage1/house-tiles-NN-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class HouseTilesHostTests
    {
        private const int Size = RoomTilePlan.CellSize, Floor = 2;
        private string _root, _temp;

        [SetUp]
        public void SetUp()
        {
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            _temp = Path.Combine(Path.GetTempPath(), "rootborn-house-tiles-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temp);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_temp, true); } catch (IOException) { }
        }

        /// <summary>짓는 집(맵 목록의 build 성질)의 id. 맵 id를 적지 않는다.</summary>
        private string HouseId(string maps = null) =>
            WorldCatalog.Load(maps ?? Path.Combine(_root, WorldCatalog.DefaultRelativePath)).Maps.Single(m => m.Build != null).Id;

        private PlayerDriver Start(string maps = null, string size = "1920x1080")
        {
            var args = new List<string> { "--world", HouseId(maps), "--size", size, "--evidence", Path.Combine(_root, "output", "house-building", "stage1") };
            if (maps != null) { args.Add("--maps"); args.Add(maps); }
            HostOptions options = HostOptions.Resolve(args.ToArray());
            options.TestMode = true;
            return new PlayerDriver(options);
        }

        private static uint[] Pixels(Texture2D texture)
        {
            var pixels = new uint[texture.Width * texture.Height];
            texture.GetData(pixels);
            return pixels;
        }

        /// <summary>월드 한 장을 그리는 데 든 GPU 그리기 호출과 그림 수(장면 버퍼에, 한 번).</summary>
        private static (long Draws, long Sprites) DrawCost(PlayerDriver player)
        {
            HostGame game = player.Game;
            using var batch = new SpriteBatch(game.GraphicsDevice);
            game.GraphicsDevice.SetRenderTarget(game.Scene);
            game.World.Draw(batch, game.Scene.Width, game.Scene.Height); // 데우기(조합은 이미 했다)
            GraphicsMetrics before = game.GraphicsDevice.Metrics;
            game.World.Draw(batch, game.Scene.Width, game.Scene.Height);
            GraphicsMetrics after = game.GraphicsDevice.Metrics;
            game.GraphicsDevice.SetRenderTarget(null);
            return (after.DrawCount - before.DrawCount, after.SpriteCount - before.SpriteCount);
        }

        [Test]
        public void HOUSE_TILES_001_ComposedHouseMatchesTheKitTiles_AndComposesOnce()
        {
            using PlayerDriver player = Start();
            player.Frames(60);
            WorldScreen world = player.Game.World;
            LoadedMap map = world.Current;
            MapBuildSettings build = map.Entry.Build;
            Assert.That(build, Is.Not.Null);
            Assert.That(map.Problems, Is.Empty, string.Join("\n", map.Problems));
            Assert.That(world.ComposeCount, Is.EqualTo(1), "들어선 뒤 60프레임 동안 조합은 한 번");
            ComposedMapTiles composed = world.ComposedTiles;
            Assert.That(composed.Ok, Is.True, string.Join("\n", composed.Problems));
            Assert.That(world.ComposedTextureBytes, Is.EqualTo((long)composed.Room.Width * composed.Room.Height * 4).And.GreaterThan(0));
            int full = MapPicture.From(map.Level).Under.Count;
            (long draws, long sprites) = DrawCost(player);
            TestContext.Progress.WriteLine($"HOUSE_TILES_001 {map.Entry.Id}: 조합 {composed.Room.Cells.Count}칸, 서로 다른 그림 {composed.Room.UniqueTiles}, 텍스처 {composed.Room.Width}x{composed.Room.Height} "
                + $"{world.ComposedTextureBytes}바이트({world.ComposedTextureBytes / 1048576.0:0.00}MB), 조합+올리기 {world.LastComposeMilliseconds:0.0}ms; "
                + $"캐릭터 아래 그림 {world.UnderSpritesDrawn}장(LDtk 층으로 그리면 {full}장), 월드 한 장 그리기 호출 {draws}, 그림 {sprites}");
            Assert.That(world.UnderSpritesDrawn, Is.EqualTo(full - build.ReplacesLayers.Sum(n => map.Level.Layer(n).Tiles.Count) + composed.Room.Cells.Count));

            // 전제: 기본 꾸밈(art)·띠 색 = 키트가 이 방을 구울 때 쓴 벽 모양·바닥·띠 색(방 폴더 자리 표). 아니면 두 그림이 같을 까닭이 없다.
            RoomFinishDefinition wall = world.Content.Definitions.Get<RoomFinishDefinition>(build.DefaultWall);
            RoomFinishDefinition floor = world.Content.Definitions.Get<RoomFinishDefinition>(build.DefaultFloor);
            string folder = Path.GetDirectoryName(Path.Combine(_root, map.Entry.Ldtk));
            using (JsonDocument slots = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "tiles.slots.json"))))
            {
                JsonElement s = slots.RootElement;
                Assert.That((wall.Art, floor.Art, build.BandColors), Is.EqualTo((s.GetProperty("wall").GetString(), s.GetProperty("floor").GetString(), s.GetProperty("top").GetString())),
                    "전제: build.defaultWall/defaultFloor/bandColors의 그림이 방 평면(자리 표)의 wall/floor/top과 같다");
            }

            // GPU에 올린 조합 텍스처 / 키트가 구운 타일셋(LDtk Ground 위에 Wall)을 엔진으로 읽어 칸마다 견준다.
            uint[] atlas = Pixels(world.ComposedTexture);
            var baked = new Dictionary<CellPos, uint[]>();
            var sheets = new Dictionary<string, (int W, uint[] Px)>();
            foreach (LdtkLayer layer in map.Level.Layers)
            {
                if (!build.ReplacesLayers.Contains(layer.Name)) continue;
                if (!sheets.TryGetValue(layer.TilesetPath, out var sheet))
                {
                    using FileStream stream = File.OpenRead(layer.TilesetPath);
                    using Texture2D t = Texture2D.FromStream(player.Game.GraphicsDevice, stream);
                    sheets[layer.TilesetPath] = sheet = (t.Width, Pixels(t));
                }
                foreach (LdtkTile tile in layer.Tiles)
                {
                    var cell = new CellPos(tile.Px / Size, tile.Py / Size);
                    if (!baked.TryGetValue(cell, out uint[] px)) baked[cell] = px = new uint[Size * Size];
                    for (int y = 0; y < Size; y++)
                        for (int x = 0; x < Size; x++)
                        {
                            uint p = sheet.Px[(tile.SrcY + y) * sheet.W + tile.SrcX + x];
                            if ((p >> 24) == 255) px[y * Size + x] = p;
                            else Assert.That(p >> 24, Is.EqualTo(0u), "반투명 픽셀 " + cell);
                        }
                }
            }
            var different = new List<string>();
            foreach (var (cell, sx, sy) in composed.Room.Cells)
            {
                uint[] want = baked.TryGetValue(cell, out uint[] b) ? b : new uint[Size * Size];
                for (int p = 0; p < Size * Size; p++)
                {
                    uint got = atlas[(sy + p / Size) * world.ComposedTexture.Width + sx + p % Size];
                    if ((got >> 24) == 0) got = 0;
                    if (got != want[p]) { different.Add(cell + " (" + p % Size + ", " + p / Size + ") " + got.ToString("X8") + " / " + want[p].ToString("X8")); break; }
                }
            }
            Assert.That(composed.Room.Cells.Select(c => c.Cell), Is.SupersetOf(baked.Keys), "구운 칸마다 조합 칸이 있다");
            Assert.That(different, Is.Empty, "조합(GPU) = 구운 타일:\n" + string.Join("\n", different.Take(20)));

            // 스크린샷 넷 + 현관: 찍을 자리는 출처표에서 고른다(칸 좌표를 적지 않는다).
            RoomTilePlan plan = map.Tiles;
            LdtkLayer collision = map.Level.Layer("Collision");
            bool IsFloor(CellPos c) => c.X >= 0 && c.Y >= 0 && c.X < collision.Columns && c.Y < collision.Rows && collision.IntGrid[c.Y * collision.Columns + c.X] == Floor;
            CellPos Below(CellPos c) => new CellPos(c.X, c.Y + 1);
            int shot = 1;
            void Shot(string name) => player.Screenshot($"house-tiles-{shot++:D2}-{name}");
            Shot("spawn");
            CellTiles narrow = plan.Cells.First(c => c.Parts.Any(p => p.Source == TileSources.Narrow) && IsFloor(Below(c.Cell)));
            player.WalkTo(Below(narrow.Cell));
            Shot("narrow-face");
            // 두 방 사이 세로 벽: 띠 칸의 바닥 주인이 왼쪽·오른쪽 둘인 칸 중 맨 위(그 왼쪽 바닥에 선다).
            CellTiles between = plan.Cells.First(c => c.Parts.Where(p => p.Source == TileSources.Floor).Select(p => p.Owner.X).Distinct().Count() == 2
                && c.Parts.Any(p => p.Source == TileSources.Band) && IsFloor(new CellPos(c.Cell.X - 1, c.Cell.Y)));
            player.WalkTo(new CellPos(between.Cell.X - 1, between.Cell.Y));
            Shot("vertical-wall-between-rooms");
            // 문간: 좌우가 벽이고 위아래가 바닥인 바닥 칸(두 방을 잇는 좁은 틈, 양옆 벽 칸은 한쪽만 바닥).
            CellPos doorway = plan.Cells.Select(c => c.Cell).First(c => !IsFloor(c) && IsFloor(new CellPos(c.X + 1, c.Y)) && !IsFloor(new CellPos(c.X + 2, c.Y))
                && IsFloor(new CellPos(c.X + 1, c.Y - 1)) && IsFloor(new CellPos(c.X + 1, c.Y + 1)));
            player.WalkTo(new CellPos(doorway.X + 1, doorway.Y));
            Shot("doorway-between-rooms");
            // 가로 벽 정면 앞: 아래가 바닥인 꽉 찬 정면 칸 중 맨 위·왼쪽(맵 가장자리도 함께 찍힌다).
            CellTiles face = plan.Cells.First(c => c.Parts.Any(p => p.Source == TileSources.Face) && IsFloor(Below(c.Cell)) && !c.Parts.Any(p => p.Source == TileSources.Narrow));
            player.WalkTo(Below(face.Cell));
            Shot("wall-face");
            CellTiles sill = plan.Cells.First(c => c.Parts.Any(p => p.Source == TileSources.Sill));
            CellPos inside = new[] { new CellPos(sill.Cell.X, sill.Cell.Y - 1), Below(sill.Cell), new CellPos(sill.Cell.X - 1, sill.Cell.Y), new CellPos(sill.Cell.X + 1, sill.Cell.Y) }.First(IsFloor);
            player.WalkTo(new CellPos(inside.X + (inside.X - sill.Cell.X), inside.Y + (inside.Y - sill.Cell.Y)));
            Shot("front-door-sill");
            Assert.That(world.ComposeCount, Is.EqualTo(1), "걸어 다녀도 다시 조합하지 않는다");
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
            Assert.That(player.CollisionViolations, Is.EqualTo(0));
        }

        [Test]
        public void HOUSE_TILES_002_LeavingTheHouseReleasesTheComposedTexture()
        {
            using PlayerDriver player = Start();
            player.Frames(10);
            WorldScreen world = player.Game.World;
            string house = player.World.Map.Id;
            Assert.That((world.ComposeCount, world.ComposedTextureBytes > 0), Is.EqualTo((1, true)));
            DoorArea door = player.World.Map.Doors.Single(d => d.Destination.HasValue);
            string town = player.WalkThroughDoor(door.Target);
            Assert.That(town, Is.Not.EqualTo(house));
            player.Frames(3);
            Assert.That(world.ComposedTextureBytes, Is.EqualTo(0), "짓는 집을 떠나면 조합 텍스처를 버린다");
            Assert.That(world.ComposedTiles, Is.Null);
            Assert.That(world.ComposeCount, Is.EqualTo(1), "마을은 조합하지 않는다(그리는 길이 그대로)");
            Assert.That(world.UnderSpritesDrawn, Is.EqualTo(world.Current.Picture.Under.Count), "마을은 Under 그대로");
            player.Screenshot("house-tiles-20-town");
            string houseDoor = player.World.Map.Doors.First(d => d.Destination.HasValue && d.Destination.Value.MapId == house).Target;
            Assert.That(player.WalkThroughDoor(houseDoor), Is.EqualTo(house));
            player.Frames(3);
            Assert.That(world.ComposeCount, Is.EqualTo(2), "다시 들어오면 다시 만든다");
            Assert.That(world.ComposedTextureBytes, Is.GreaterThan(0));
            Texture2D held = world.ComposedTexture;
            long bytes = world.ComposedTextureBytes;
            world.RefreshComposedTiles(house);
            Assert.That(world.ComposedTextureBytes, Is.EqualTo(bytes), "다시 만들라고 해도 텍스처는 들고 있다(다음 그리기까지 그것을 그린다)");
            player.Frames(2);
            Assert.That(world.ComposeCount, Is.EqualTo(3), "다시 만들라고 하면 다음 그리기에서 만든다");
            Assert.That(world.ComposedTexture, Is.SameAs(held), "묶음 크기가 같으면 텍스처를 다시 만들지 않고 픽셀만 바꾼다");
            player.Screenshot("house-tiles-21-back-home");
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        [Test]
        public void HOUSE_TILES_003_WhenComposingFails_TheLdtkLayersAreDrawn_AndTheGameGoesOn()
        {
            // 맵 목록 사본의 띠 색 줄을 없는 이름으로 → 조합이 문제를 알리고 LDtk의 Wall·Ground 층을 그대로 그린다(지금까지의 그리는 길). 멈추지 않는다.
            string maps = Path.Combine(_temp, "maps.json");
            string text = File.ReadAllText(Path.Combine(_root, WorldCatalog.DefaultRelativePath));
            MapBuildSettings real = WorldCatalog.Load(Path.Combine(_root, WorldCatalog.DefaultRelativePath)).Maps.Single(m => m.Build != null).Build;
            string broken = text.Replace("\"bandColors\": \"" + real.BandColors + "\"", "\"bandColors\": \"wall.top.no-such\"");
            Assert.That(broken, Is.Not.EqualTo(text));
            File.WriteAllText(maps, broken);
            using PlayerDriver player = Start(maps);
            player.Frames(10);
            WorldScreen world = player.Game.World;
            LoadedMap map = world.Current;
            Assert.That(world.ComposeCount, Is.EqualTo(1));
            Assert.That(world.ComposedTiles.Ok, Is.False);
            Assert.That(world.ComposedTiles.Problems, Has.Some.Contains("wall.top.no-such"));
            Assert.That(world.ComposedTextureBytes, Is.EqualTo(0));
            int full = MapPicture.From(map.Level).Under.Count;
            Assert.That(world.UnderSpritesDrawn, Is.EqualTo(full), "LDtk 층을 건너뛰지 않은 그림 전부");
            (long draws, long sprites) = DrawCost(player);
            TestContext.Progress.WriteLine($"HOUSE_TILES_003 LDtk 층으로 그림: 캐릭터 아래 그림 {world.UnderSpritesDrawn}장, 월드 한 장 그리기 호출 {draws}, 그림 {sprites}");
            player.Screenshot("house-tiles-30-fallback-ldtk-layers");
            CellPos start = player.PlayerCell;
            player.WalkTo(new CellPos(start.X, start.Y - 1));
            player.Frames(30);
            Assert.That(world.ComposeCount, Is.EqualTo(1), "실패한 조합을 프레임마다 다시 하지 않는다");
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }

        [Test]
        public void HOUSE_TILES_004_EmptiedReplacedLayers_TheComposedWallsAndFloorsAreStillDrawn()
        {
            // 편집기에서 게임이 대신 그리는 두 층(Wall·Ground)을 비운 집(임시 폴더의 LDtk 사본, 맵 목록 사본이 가리킨다)으로 들어간다.
            // 조합은 되고, 조합한 칸이 모두 그려진다(그 층의 타일이 없어도 자리가 있다). 색은 보지 않는다 — 그린 그림 수와 스크린샷.
            string catalogPath = Path.Combine(_root, WorldCatalog.DefaultRelativePath);
            MapEntry entry = WorldCatalog.Load(catalogPath).Maps.Single(m => m.Build != null);
            string source = Path.Combine(_root, entry.Ldtk);
            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(source)))
                if (!file.EndsWith(".ldtk", StringComparison.OrdinalIgnoreCase)) File.Copy(file, Path.Combine(_temp, Path.GetFileName(file)));
            System.Text.Json.Nodes.JsonNode ldtk = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(source));
            int emptied = 0;
            foreach (System.Text.Json.Nodes.JsonNode level in ldtk["levels"].AsArray())
                foreach (System.Text.Json.Nodes.JsonNode layer in level["layerInstances"].AsArray())
                    if (entry.Build.ReplacesLayers.Contains((string)layer["__identifier"]))
                    {
                        layer["gridTiles"] = new System.Text.Json.Nodes.JsonArray();
                        layer["autoLayerTiles"] = new System.Text.Json.Nodes.JsonArray();
                        emptied++;
                    }
            Assert.That(emptied, Is.EqualTo(entry.Build.ReplacesLayers.Count), "전제: 바꿔 그릴 층 전부를 비웠다");
            string copy = Path.Combine(_temp, Path.GetFileName(source)).Replace('\\', '/');
            File.WriteAllText(copy, ldtk.ToJsonString());
            string text = File.ReadAllText(catalogPath);
            string maps = Path.Combine(_temp, "maps.json");
            string pointed = text.Replace("\"ldtk\": \"" + entry.Ldtk + "\"", "\"ldtk\": \"" + copy + "\"");
            Assert.That(pointed, Is.Not.EqualTo(text));
            File.WriteAllText(maps, pointed);

            using PlayerDriver player = Start(maps);
            player.Frames(10);
            WorldScreen world = player.Game.World;
            LoadedMap map = world.Current;
            Assert.That(map.Entry.Ldtk, Is.EqualTo(copy));
            Assert.That(map.Level.Layers.Where(l => entry.Build.ReplacesLayers.Contains(l.Name)).Sum(l => l.Tiles.Count), Is.EqualTo(0));
            ComposedMapTiles composed = world.ComposedTiles;
            Assert.That(composed.Ok, Is.True, string.Join("\n", composed.Problems));
            Assert.That(world.ComposedTextureBytes, Is.GreaterThan(0));
            Assert.That(world.UnderSpritesDrawn, Is.EqualTo(map.Picture.Under.Count + composed.Room.Cells.Count), "조합한 칸이 모두 그려진다");
            (long draws, long sprites) = DrawCost(player);
            TestContext.Progress.WriteLine($"HOUSE_TILES_004 비운 층: 캐릭터 아래 그림 {world.UnderSpritesDrawn}장(조합 칸 {composed.Room.Cells.Count}), 월드 한 장 그리기 호출 {draws}, 그림 {sprites}");
            Assert.That(sprites, Is.GreaterThanOrEqualTo(composed.Room.Cells.Count));
            player.Screenshot("house-tiles-40-replaced-layers-emptied");
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }
    }
}
