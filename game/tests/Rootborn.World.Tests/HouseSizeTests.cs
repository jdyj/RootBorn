using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.World.Ldtk;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 집은 커질 수 있다(사용자 결정 2026-11-11). 맵 크기·카메라 범위·충돌·가구 범위는 모두 읽어 온 맵에서 얻는다.
    /// 실제 집 맵(맵 목록의 House가 가리키는 LDtk, 28x18)과 오른쪽·아래로 늘린 사본으로 같은 코드를 돌린다. 자동 테스트.
    /// </summary>
    public sealed class HouseSizeTests
    {
        private const int ExtraColumns = 6, ExtraRows = 4;
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rootborn-house-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch (IOException) { }
        }

        /// <summary>맵 목록의 집 LDtk(저장소 뿌리 기준 경로를 맵 목록에서 얻는다).</summary>
        private static string HouseLdtk() => Path.Combine(Repo.Root, Repo.Loader().Catalog.Find("House").Ldtk);

        /// <summary>
        /// 레벨을 오른쪽·아래로 늘린 집 LDtk 사본. 늘린 칸은 칠한 Collision에서 바닥(2)이고 맨 끝 줄·열은 벽(1), 다른 IntGrid 층(Build)은 0(고정).
        /// 원래 바깥벽은 그대로 남는다.
        /// </summary>
        private string BiggerHouse()
        {
            string source = HouseLdtk();
            JsonNode root = JsonNode.Parse(File.ReadAllText(source));
            // 타일셋 경로는 원래 폴더 기준이라 사본 옆에서도 풀리도록 절대 경로로 바꾼다.
            foreach (JsonNode ts in root["defs"]["tilesets"].AsArray())
                ts["relPath"] = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source), (string)ts["relPath"])).Replace('\\', '/');
            JsonNode level = root["levels"][0];
            int oldW = (int)level["pxWid"], oldH = (int)level["pxHei"];
            level["pxWid"] = oldW + ExtraColumns * 32;
            level["pxHei"] = oldH + ExtraRows * 32;
            foreach (JsonNode li in level["layerInstances"].AsArray())
            {
                int grid = (int)li["__gridSize"];
                int oldCols = (int)li["__cWid"], oldRows = (int)li["__cHei"];
                int cols = (int)level["pxWid"] / grid, rows = (int)level["pxHei"] / grid;
                li["__cWid"] = cols;
                li["__cHei"] = rows;
                JsonArray csv = li["intGridCsv"].AsArray();
                if (csv.Count == 0) continue;
                var grown = new JsonArray();
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                        grown.Add(x < oldCols && y < oldRows ? (int)csv[y * oldCols + x]
                            : (string)li["__identifier"] != "Collision" ? 0 : x == cols - 1 || y == rows - 1 ? 1 : 2);
                li["intGridCsv"] = grown;
            }
            string path = Path.Combine(_dir, "house.ldtk");
            File.WriteAllText(path, root.ToJsonString());
            return path;
        }

        private static LoadedMap Build(string ldtkPath)
        {
            MapEntry entry = Repo.Loader().Catalog.Find("House");
            return WorldMapLoader.Build(entry, LdtkProject.Load(ldtkPath).LoadLevel(entry.Level));
        }

        [Test]
        public void TwoHouseSizes_RunThroughTheSameMapCodeWithSizesTakenFromTheFile()
        {
            LoadedMap normal = Build(HouseLdtk());
            LoadedMap bigger = Build(BiggerHouse());

            Assert.That((normal.World.Grid.Columns, normal.World.Grid.Rows), Is.EqualTo((28, 18)), "게임의 집 크기(사용자 결정)");
            Assert.That((bigger.World.Grid.Columns, bigger.World.Grid.Rows), Is.EqualTo((28 + ExtraColumns, 18 + ExtraRows)));

            foreach (LoadedMap house in new[] { normal, bigger })
            {
                WorldMap map = house.World;
                Assert.That(house.Problems, Is.Empty);
                Assert.That(map.TryGetSpawn("house-entry", out var spawn), Is.True);
                Assert.That(ArrivalRules.Check(map.Collision, map.Doors, spawn), Is.Empty, "도착 지점에서 출구까지 걸어갈 수 있다");
                Assert.That(map.Collision.IsBlocked(map.Grid.Columns, 0), Is.True, "맵 오른쪽 밖은 막힘 — 맵 크기에서 정해진다");
                Assert.That(map.Collision.IsBlocked(map.Grid.Columns - 2, map.Grid.Rows - 2), Is.False, "맵 오른쪽 아래 안쪽 칸은 맵 안의 열린 칸");

                // 카메라는 맵 크기로 범위를 정한다: 오른쪽 끝을 따라가도 맵 오른쪽 끝에서 멈춘다.
                const int view = 640;
                float right = WorldCamera.Offset(map.Grid.PixelWidth, map.Grid.PixelWidth, view);
                Assert.That(right, Is.EqualTo(map.Grid.PixelWidth - view));

                // 가구는 레벨 안에 있다.
                foreach (SpriteDraw furniture in house.Picture.Sorted)
                {
                    Assert.That(furniture.X, Is.GreaterThanOrEqualTo(0));
                    Assert.That(furniture.X + furniture.W, Is.LessThanOrEqualTo(map.Grid.PixelWidth));
                    Assert.That(furniture.Y + furniture.H, Is.LessThanOrEqualTo(map.Grid.PixelHeight));
                }
            }
            Assert.That(bigger.Picture.Sorted.Count, Is.EqualTo(normal.Picture.Sorted.Count));
        }

        [Test]
        public void ActorIndex_PutsTheCharacterInFrontOfFurnitureAboveItAndBehindFurnitureBelow()
        {
            // 키트로 만든 집에는 앞뒤를 가리는 그림이 없다(가구는 가구 배치가 얹는다). 정렬값 다섯을 넣은 그림으로 같은 규칙을 본다.
            var picture = new MapPicture();
            foreach (float sortY in new[] { 400f, 48f, 240f, 112f, 496f })
                picture.AddSorted(new SpriteDraw(null, 0, 0, 32, 32, 0, sortY - 16, 32, 32, 0, 1f, sortY));
            Assert.That(picture.Sorted.Select(s => s.SortY), Is.Ordered);
            float middle = picture.Sorted[picture.Sorted.Count / 2].SortY;
            int index = picture.ActorIndex(middle);
            for (int i = 0; i < index; i++) Assert.That(picture.Sorted[i].SortY, Is.LessThanOrEqualTo(middle), "캐릭터보다 먼저 그림(뒤)");
            for (int i = index; i < picture.Sorted.Count; i++) Assert.That(picture.Sorted[i].SortY, Is.GreaterThan(middle), "캐릭터 다음에 그림(앞)");
        }
    }
}
