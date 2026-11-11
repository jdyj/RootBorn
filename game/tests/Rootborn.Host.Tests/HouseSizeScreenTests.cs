using System;
using System.IO;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Rootborn.Core.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 집은 커질 수 있다. 실제 house.ldtk(28x18)와 오른쪽·아래로 늘린 사본(맵 목록 --maps로 바꿔 끼움)을 같은 Host 코드로 띄워,
    /// 오른쪽 방 끝까지 걸어간 뒤 카메라가 그 맵의 크기에서 정한 범위 끝에 멈추는지 본다. 장치 이벤트 주입, OS 입력 계층 미확인.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class HouseSizeScreenTests
    {
        private const int Width = 1280, Height = 720, ExtraColumns = 6, ExtraRows = 4;
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rootborn-house-screen-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch (IOException) { }
        }

        /// <summary>집 맵만 바꾼 맵 목록. bigger면 레벨을 늘린 house.ldtk 사본을 가리킨다.</summary>
        private string Catalog(string repo, bool bigger)
        {
            string house = Path.Combine(repo, "Assets/LDtk/House/house.ldtk");
            if (bigger)
            {
                JsonNode root = JsonNode.Parse(File.ReadAllText(house));
                foreach (JsonNode ts in root["defs"]["tilesets"].AsArray())
                    ts["relPath"] = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(house), (string)ts["relPath"])).Replace('\\', '/');
                JsonNode level = root["levels"][0];
                level["pxWid"] = (int)level["pxWid"] + ExtraColumns * 32;
                level["pxHei"] = (int)level["pxHei"] + ExtraRows * 32;
                foreach (JsonNode li in level["layerInstances"].AsArray())
                {
                    int grid = (int)li["__gridSize"], oldCols = (int)li["__cWid"], oldRows = (int)li["__cHei"];
                    int cols = (int)level["pxWid"] / grid, rows = (int)level["pxHei"] / grid;
                    li["__cWid"] = cols;
                    li["__cHei"] = rows;
                    JsonArray csv = li["intGridCsv"].AsArray();
                    if (csv.Count == 0) continue;
                    var grown = new JsonArray();
                    for (int y = 0; y < rows; y++)
                        for (int x = 0; x < cols; x++) grown.Add(x < oldCols && y < oldRows ? (int)csv[y * oldCols + x] : 0);
                    li["intGridCsv"] = grown;
                }
                house = Path.Combine(_dir, "house.ldtk");
                File.WriteAllText(house, root.ToJsonString());
            }
            JsonNode catalog = JsonNode.Parse(File.ReadAllText(Path.Combine(repo, "game/world/maps.json")),
                documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip });
            foreach (JsonNode map in catalog["maps"].AsArray())
                if ((string)map["id"] == "House") map["ldtk"] = house.Replace('\\', '/');
            string path = Path.Combine(_dir, "maps.json");
            File.WriteAllText(path, catalog.ToJsonString());
            return path;
        }

        [TestCase(false, 28, 18)]
        [TestCase(true, 28 + ExtraColumns, 18 + ExtraRows)]
        public void WORLD_PLAY_006_SameHostCodeRunsHousesOfDifferentSizes(bool bigger, int columns, int rows)
        {
            HostOptions probe = HostOptions.Resolve(new[] { "--world", "House", "--size", Width + "x" + Height });
            HostOptions options = HostOptions.Resolve(new[] { "--world", "House", "--size", Width + "x" + Height, "--maps", Catalog(probe.RepoRoot, bigger) });
            options.TestMode = true;
            using var player = new PlayerDriver(options);
            player.Frames(3);
            WorldSession world = player.World;
            MapGrid grid = world.Map.Grid;
            Assert.That((grid.Columns, grid.Rows), Is.EqualTo((columns, rows)), "맵 크기는 파일에서 읽는다");

            player.WalkTo(new CellPos(26, 9)); // 오른쪽 방의 오른쪽 끝(원래 바깥벽 바로 안)
            player.Frames(2);
            int zoom = player.Game.World.Zoom;
            float viewW = Width / (float)zoom, viewH = Height / (float)zoom;
            float expectedX = WorldCamera.Offset(world.Walker.X, grid.PixelWidth, viewW);
            Assert.That(player.Game.World.CameraOffset.X, Is.EqualTo(expectedX));
            Assert.That(player.Game.World.CameraOffset.X, Is.EqualTo(Math.Min(grid.PixelWidth - viewW, world.Walker.X - viewW / 2f)).Within(1f),
                "카메라 오른쪽 끝은 그 맵의 너비에서 정해진다");
            Assert.That(player.Game.World.CameraOffset.Y + viewH, Is.LessThanOrEqualTo(grid.PixelHeight));
            Assert.That(player.CollisionViolations, Is.EqualTo(0));
            player.Screenshot(bigger ? "08-house-bigger-east" : "08-house-normal-east");
        }
    }
}
