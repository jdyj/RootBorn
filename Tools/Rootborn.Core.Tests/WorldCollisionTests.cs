// 옮겨 옴: Assets/Scripts/Editor/LDtk/LdtkImportValidator.cs (InteriorDoorPortalBaker.BakeCollision, ApplyTownPaintedCollision, LdtkPaintedCollision)
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// WORLD-COL: 실내 자동 판정, 마을 충돌 층, 칠한 Collision 층. 칸 좌표는 LDtk와 같이 왼쪽 위가 (0,0)이고 아래로 갈수록 y가 커진다.
    /// </summary>
    public sealed class WorldCollisionTests
    {
        private const int Cell = 32;

        /// <summary>Unity 판 실내 맵들의 가구 층 이름 앞부분. 이제 Core가 아니라 맵 목록(maps.json)의 값이다.</summary>
        private static readonly string[] UnityInteriorProps = { "Police", "Kitchen", "Shop", "Shooting", "School", "House1", "House2" };

        // 문자 그림으로 층을 만든다. 각 문자열이 한 행, 공백이 아닌 칸에 타일이 있다.
        private static MapLayer Layer(string name, params string[] rows)
        {
            int cols = rows[0].Length;
            var cells = new bool[cols * rows.Length];
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < cols; x++)
                    cells[y * cols + x] = rows[y][x] != ' ';
            return new MapLayer(name, cells);
        }

        private static string Draw(CollisionGrid grid)
        {
            var lines = new List<string>();
            for (int y = 0; y < grid.Rows; y++)
            {
                var chars = new char[grid.Columns];
                for (int x = 0; x < grid.Columns; x++) chars[x] = grid.IsBlocked(x, y) ? '#' : '.';
                lines.Add(new string(chars));
            }
            return string.Join("\n", lines);
        }

        private static CollisionGrid Bake(ICollisionRule rule, IReadOnlyList<MapLayer> layers, IReadOnlyList<AreaRect> exits = null, int[] painted = null, int cols = 7, int rows = 6)
        {
            var input = new CollisionInput(new MapGrid(cols, rows, Cell), layers, exits ?? new AreaRect[0], painted);
            return CollisionBaker.Bake(input, rule);
        }

        private static AreaRect CellArea(int x, int y, int w = 1, int h = 1) => new AreaRect(x * Cell, y * Cell, w * Cell, h * Cell);

        [Test]
        public void WORLD_COL_001_InteriorAuto_FloorWithoutSolidIsOpen_EverythingElseInBoundsPlusOneRingBlocks()
        {
            // 바닥 3x2, 위에 벽 한 줄. 벽 칸과 바닥 밖 칸(테두리 한 칸 포함)은 막힌다.
            var ground = Layer("Ground",
                "       ",
                "       ",
                "  ...  ",
                "  ...  ",
                "       ",
                "       ");
            var wall = Layer("Wall",
                "       ",
                "  ...  ",
                "       ",
                "       ",
                "       ",
                "       ");
            CollisionGrid grid = Bake(new InteriorAutoCollision(), new[] { ground, wall });
            Assert.That(Draw(grid), Is.EqualTo(
                ".#####.\n" +
                ".#####.\n" +
                ".#...#.\n" +
                ".#...#.\n" +
                ".#####.\n" +
                "......."));
        }

        [Test]
        public void WORLD_COL_002_InteriorAuto_PropLayersBlockButWallsAreJudgedByNamePart()
        {
            // Shop으로 시작하는 층은 가구라 막는다. Border·Wall_3d는 이름에 Border·Wall이 들어 있어 막는다. ForeGround는 이름에 Ground가 있어 바닥으로 친다.
            var ground = Layer("Ground",
                "       ",
                " ..... ",
                " ..... ",
                " ..... ",
                " ..... ",
                "       ");
            var shop = Layer("Shop", "       ", "       ", "  .    ", "       ", "       ", "       ");
            var wall3d = Layer("Wall_3d", "       ", "       ", "       ", "    .  ", "       ", "       ");
            var fore = Layer("ForeGround", "       ", "       ", "       ", "       ", "       ", "     . ");
            var other = Layer("Animation", "       ", "       ", "       ", "       ", " .     ", "       ");
            CollisionGrid grid = Bake(new InteriorAutoCollision(propPrefixes: UnityInteriorProps), new[] { ground, shop, wall3d, fore, other });
            Assert.That(grid.IsBlocked(2, 2), Is.True, "Shop 가구");
            Assert.That(grid.IsBlocked(4, 3), Is.True, "Wall_3d");
            Assert.That(grid.IsBlocked(5, 5), Is.False, "ForeGround는 바닥으로 친다(Unity 판 이름 규칙 그대로)");
            Assert.That(grid.IsBlocked(1, 4), Is.False, "가구도 벽도 아닌 층은 바닥 위에서 막지 않는다");
            Assert.That(grid.IsBlocked(3, 3), Is.False);
        }

        [Test]
        public void WORLD_COL_003_InteriorAuto_ExitCellsStayOpenAndFloorNextToExitIgnoresProps()
        {
            // 문 칸은 벽 위라도 열리고, 문 둘레 한 칸의 바닥은 가구가 있어도 열린다. 벽은 문 옆이라도 막힌 채다.
            var ground = Layer("Ground",
                "       ",
                " ..... ",
                " ..... ",
                " ..... ",
                " ..... ",
                "       ");
            var wall = Layer("Wall", "       ", " ..... ", "       ", "       ", "       ", "       ");
            var counter = Layer("Kitchen", "       ", "       ", "  .... ", "       ", "       ", "       ");
            CollisionGrid grid = Bake(new InteriorAutoCollision(propPrefixes: UnityInteriorProps), new[] { ground, wall, counter }, new[] { CellArea(3, 1) });
            Assert.That(grid.IsBlocked(3, 1), Is.False, "문 칸");
            Assert.That(grid.IsBlocked(2, 2), Is.False, "문 옆 가구 칸");
            Assert.That(grid.IsBlocked(4, 2), Is.False, "문 옆 가구 칸");
            Assert.That(grid.IsBlocked(2, 1), Is.True, "문 옆 벽");
            Assert.That(grid.IsBlocked(5, 2), Is.True, "문에서 두 칸 떨어진 가구는 막는다");
        }

        [Test]
        public void WORLD_COL_004_ExitAreaCoversCellsItOverlapsMoreThanAHundredthOfACell()
        {
            // 문 상자 경계에서 0.01칸 안쪽까지만 칸으로 친다. 칸 경계에 딱 맞춘 상자는 옆 칸을 열지 않는다.
            var area = new AreaRect(1 * Cell, 1 * Cell, 2 * Cell, 1 * Cell);
            CollectionAssert.AreEquivalent(new[] { new CellPos(1, 1), new CellPos(2, 1) }, CollisionInput.CellsUnder(area, Cell));
            var off = new AreaRect(1.5f * Cell, 1 * Cell, 1 * Cell, 1 * Cell);
            CollectionAssert.AreEquivalent(new[] { new CellPos(1, 1), new CellPos(2, 1) }, CollisionInput.CellsUnder(off, Cell));
        }

        [Test]
        public void WORLD_COL_005_PaintedBlockAlwaysBlocksAndPaintedOpenAlwaysOpens()
        {
            var ground = Layer("Ground",
                "       ",
                " ..... ",
                " ..... ",
                " ..... ",
                " ..... ",
                "       ");
            var wall = Layer("Wall", "       ", " ..... ", "       ", "       ", "       ", "       ");
            var painted = new int[7 * 6];
            painted[3 * 7 + 3] = PaintedCollision.Block;
            painted[1 * 7 + 2] = PaintedCollision.Open;
            painted[2 * 7 + 3] = 7; // 모르는 값은 무시한다
            CollisionGrid grid = Bake(new InteriorAutoCollision(), new[] { ground, wall }, painted: painted);
            Assert.That(grid.IsBlocked(3, 3), Is.True);
            Assert.That(grid.IsBlocked(2, 1), Is.False);
            Assert.That(grid.IsBlocked(3, 2), Is.False);
        }

        [Test]
        public void WORLD_COL_006_ColliderLayers_TilesOnListedLayersBlockWholeCells()
        {
            // 마을: 건물·울타리처럼 충돌체를 단 층의 타일만 칸 전체를 막는다. 소품·바닥은 막지 않는다. 칠한 Open이 그 칸을 연다.
            var ground = Layer("Ground", ".......", ".......", ".......", ".......", ".......", ".......");
            var building = Layer("Building", "       ", " ...   ", " ...   ", "       ", "       ", "       ");
            var props = Layer("Props", "       ", "       ", "       ", "    .  ", "       ", "       ");
            var fence = Layer("Fense", "       ", "       ", "       ", "       ", ".......", "       ");
            var painted = new int[7 * 6];
            painted[4 * 7 + 3] = PaintedCollision.Open;
            painted[5 * 7 + 6] = PaintedCollision.Block;
            var rule = new ColliderLayersCollision(new[] { "Building", "Fense" });
            CollisionGrid grid = Bake(rule, new[] { ground, building, props, fence }, painted: painted);
            Assert.That(Draw(grid), Is.EqualTo(
                ".......\n" +
                ".###...\n" +
                ".###...\n" +
                ".......\n" +
                "###.###\n" +
                "......#"));
        }

        [Test]
        public void WORLD_COL_007_OutsideTheMapIsAlwaysBlocked()
        {
            var ground = Layer("Ground", ".......", ".......", ".......", ".......", ".......", ".......");
            CollisionGrid grid = Bake(new ColliderLayersCollision(new string[0]), new[] { ground });
            Assert.That(grid.IsBlocked(-1, 0), Is.True);
            Assert.That(grid.IsBlocked(0, -1), Is.True);
            Assert.That(grid.IsBlocked(7, 0), Is.True);
            Assert.That(grid.IsBlocked(0, 6), Is.True);
            Assert.That(grid.IsBlocked(6, 5), Is.False);
        }

        [Test]
        public void WORLD_COL_010_ExitReachingPastTheMapEdgeOpensOnlyThoseOutsideCells()
        {
            // Unity 판은 타일 범위 + 한 칸 테두리 안을 굽고 출구 칸은 열어 두어, 맵 바로 밖의 출구 칸까지 걸어 나갈 수 있었다.
            var ground = Layer("Ground", ".......", ".......", ".......", ".......", ".......", ".......");
            CollisionGrid grid = Bake(new InteriorAutoCollision(), new[] { ground }, new[] { CellArea(3, 6) });
            Assert.That(grid.IsBlocked(3, 6), Is.False, "맵 밖이지만 출구 칸");
            Assert.That(grid.IsBlocked(2, 6), Is.True);
            Assert.That(grid.IsBlocked(3, 7), Is.True);
            CollectionAssert.AreEquivalent(new[] { new CellPos(3, 6) }, grid.OpenOutside);
        }

        [Test]
        public void WORLD_COL_008_RulesAreFoundByNameAndUnknownNamesAreRefused()
        {
            Assert.That(CollisionRules.Create("interior-auto", new string[0]), Is.InstanceOf<InteriorAutoCollision>());
            Assert.That(CollisionRules.Create("collider-layers", new[] { "Building" }), Is.InstanceOf<ColliderLayersCollision>());
            Assert.Throws<System.ArgumentException>(() => CollisionRules.Create("sprite-outline", new string[0]));
        }

        [Test]
        public void WORLD_COL_010_InteriorAuto_LayerNamesComeFromTheMapNotFromCore()
        {
            // 새 맵은 값 없이 기본 이름 규칙(바닥 Ground, 벽 Wall·Border, 가구 Prop으로 시작)으로 읽힌다.
            // 지금 맵의 가구 층 이름(Shop·Kitchen…)은 맵 목록이 넘기는 값이고 Core 기본값에 없다.
            var ground = Layer("Ground", "       ", " ..... ", " ..... ", " ..... ", " ..... ", "       ");
            var shop = Layer("Shop", "       ", "  .    ", "       ", "       ", "       ", "       ");
            var prop = Layer("PropTable", "       ", "       ", "    .  ", "       ", "       ", "       ");

            CollisionGrid byDefault = Bake(CollisionRules.Create("interior-auto", new CollisionRuleOptions()), new[] { ground, shop, prop });
            Assert.That(byDefault.IsBlocked(2, 1), Is.False, "Shop은 기본 가구 이름이 아니다");
            Assert.That(byDefault.IsBlocked(4, 2), Is.True, "Prop으로 시작하는 층은 가구");

            var options = new CollisionRuleOptions(propPrefixes: new[] { "Shop" });
            CollisionGrid fromMap = Bake(CollisionRules.Create("interior-auto", options), new[] { ground, shop, prop });
            Assert.That(fromMap.IsBlocked(2, 1), Is.True, "맵이 Shop을 가구 이름으로 넘겼다");
            Assert.That(fromMap.IsBlocked(4, 2), Is.False, "맵이 이름 목록을 주면 그것만 쓴다");

            var keys = new CollisionRuleOptions(groundKeys: new[] { "Floor" }, solidKeys: new[] { "Block" });
            var floor = Layer("Floor", "       ", " ..... ", " ..... ", " ..... ", " ..... ", "       ");
            var block = Layer("Block", "       ", " .     ", "       ", "       ", "       ", "       ");
            CollisionGrid custom = Bake(CollisionRules.Create("interior-auto", keys), new[] { floor, block });
            Assert.That(custom.IsBlocked(2, 2), Is.False, "Floor가 바닥");
            Assert.That(custom.IsBlocked(1, 1), Is.True, "Block이 벽");
        }

        [Test]
        public void WORLD_COL_009_LayerSizeMustMatchTheMap()
        {
            var tooSmall = new MapLayer("Ground", new bool[3]);
            Assert.Throws<System.ArgumentException>(() => new CollisionInput(new MapGrid(7, 6, Cell), new[] { tooSmall }, new AreaRect[0], null));
        }
    }
}
