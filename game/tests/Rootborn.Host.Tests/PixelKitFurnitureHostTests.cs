using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Screens;
using Rootborn.Screens.Furniture;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 픽셀 키트가 내보낸 가구 그림을 게임 화면에서 본다: 새 슬롯(집) → P 키로 가구 배치 → 검색 칸에 가구 id → 팔레트 칸 →
    /// 놓을 칸 앞까지 걸어가 위를 보고 E → 배치 수가 하나 늘었다. 놓은 뒤 가구 뒤(맵에서 한 줄 위)로 걸어가 캐릭터와의 앞뒤를 찍는다.
    /// 그림 픽셀이나 그림 경로는 단언하지 않는다(그림은 바뀐다) — 무엇이 보이는지는 스크린샷을 직접 열어 본다.
    /// 견줄 옛 그림(48px) 가구 하나를 먼저 놓는다. 조작은 키·글자·포인터뿐(장치 이벤트 주입, OS 입력 계층 미확인).
    /// 놓을 칸을 고를 때만 Core 판정(House.CanPlace)과 충돌·화면 좌표를 읽는다.
    /// 스크린샷: output/pixel-kit/game/furniture/pixel-kit-furniture-NN-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class PixelKitFurnitureHostTests
    {
        /// <summary>놓을 가구(테스트 데이터): 견줄 옛 그림 하나, 그다음 픽셀 키트 그림으로 바꾼 셋.</summary>
        private static readonly (string Id, string Shot)[] Furniture =
        {
            ("growth31.assembly.desk", "reference-desk"),
            ("school.bookcase", "school-bookcase"),
            ("school.archive30.archive.bookshelf", "archive-bookshelf"),
            ("comfy31.kitchen.counter.front", "kitchen-counter-front"),
        };

        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-pixel-kit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start()
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "pixel-kit", "game", "furniture"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        [Test]
        public void PIXEL_KIT_001_PlaceKitFurnitureWithPSearchAndE_ScreenshotsPaletteRoomAndBehind()
        {
            using PlayerDriver player = Start();
            var session = player.Game.Session;
            FurnitureRoom room = player.Game.World.Room;
            var house = session.House;
            GridPoint spawn = room.Grid.Spawn;
            player.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement);
            FurniturePlacementScreen screen = FurniturePlacementHostTests.Placement(player);
            var anchors = new List<GridPoint>();
            int shot = 1;

            foreach ((string id, string name) in Furniture)
            {
                FurnitureDefinition definition = session.Definitions.Get<FurnitureDefinition>(id);

                // 검색 칸을 비우고 id를 친다 → 그 칸을 누른다.
                player.PressButton(FurniturePlacementScreen.SearchId);
                player.PressKey(TestKey.Backspace, screen.Palette.Query.Length);
                player.TypeText(id);
                Assert.That(screen.Palette.Query, Is.EqualTo(id));
                int slot = Enumerable.Range(0, 20).First(i => screen.Palette.At(i)?.StableId == id);
                player.Frames(2);
                player.Screenshot($"pixel-kit-furniture-{shot++:D2}-{name}-palette");
                player.PressButton(FurniturePlacementScreen.SlotId(slot));
                Assert.That(screen.Active, Is.SameAs(definition));

                // 놓을 칸: 화면에 보이고 UI가 그림(점유 칸과 그 위 두 줄)을 덮지 않으며, 점유 칸마다 맵에서 바로 아래(앞)·위 두 줄(뒤, 그리고 벽·HUD에서 떨어지게) 칸이 걸을 수 있고, 먼저 놓은 것과 떨어진 칸.
                IReadOnlyList<GridPoint> footprint = definition.GetFootprintCells(FurniturePlacementDirection.North);
                GridPoint target = FurniturePlacementHostTests.FindCell(player, c =>
                    house.CanPlace(definition, c, FurniturePlacementDirection.North)
                    && !FurniturePlacementHostTests.Near(c, spawn, 1)
                    && anchors.All(a => !FurniturePlacementHostTests.Near(a, c, 2))
                    && footprint.All(f => Open(player, room, new GridPoint(c.X + f.X, c.Y + f.Y), 1)
                                       && Open(player, room, new GridPoint(c.X + f.X, c.Y + f.Y), -1)
                                       && Open(player, room, new GridPoint(c.X + f.X, c.Y + f.Y), -2)
                                       && Uncovered(player, new GridPoint(c.X + f.X, c.Y + f.Y))),
                    id + " 놓을 칸");
                CellPos mapCell = room.ToMap(target);
                player.WalkTo(new CellPos(mapCell.X, mapCell.Y + 1));
                player.HoldKey(TestKey.Up, 1);
                Assert.That(FurniturePlacementHostTests.View(player).TryFacingCell(out GridPoint facing) && facing.Equals(target), Is.True, "준비: 놓을 칸을 바라본다");

                int before = house.Placed.Count;
                player.PressKey(TestKey.E);
                player.Frames(2);
                Assert.That(screen.LastAction, Is.EqualTo("placed"), id);
                Assert.That(house.Placed.Count, Is.EqualTo(before + 1), id + ": 배치 수가 하나 늘었다");
                Assert.That(house.Placed.Last().Anchor, Is.EqualTo(target), id + ": 바라보는 칸에 놓였다");
                anchors.Add(target);
                // 그림이 점유 칸 위에 서 있는지 스크린샷에서 재 보려고 칸의 화면 사각형을 적는다(단언하지 않는다).
                TestContext.Out.WriteLine($"{id}: 기준 칸 ({target.X}, {target.Y}), 화면 사각형 {FurniturePlacementHostTests.View(player).CellRect(target)}");
                player.Screenshot($"pixel-kit-furniture-{shot++:D2}-{name}-placed-front");

                // 가구 뒤(맵에서 한 줄 위)로 걸어가 아래를 본다: 캐릭터가 가구 뒤에 선다.
                player.WalkTo(new CellPos(mapCell.X, mapCell.Y - 1));
                player.HoldKey(TestKey.Down, 1);
                player.Frames(2);
                player.Screenshot($"pixel-kit-furniture-{shot++:D2}-{name}-behind");
            }

            // 넷을 다 놓은 방: 캐릭터를 놓은 것들에서 비켜 세운다.
            player.WalkTo(room.ToMap(spawn));
            player.Frames(2);
            Assert.That(house.Placed.Count, Is.EqualTo(Furniture.Length));
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty, "빠진 그림이 없다");
            player.Screenshot($"pixel-kit-furniture-{shot:D2}-all-placed");
        }

        /// <summary>
        /// 방 칸과 화면에서 그 위 두 줄(가구 그림이 솟는 자리)이 화면 안이고 보이는 UI(누를 수 없는 HUD 판 포함)가 덮지 않는가.
        /// 칸의 왼쪽·가운데·오른쪽을 본다. 화면 전체를 덮는 틀 노드는 세지 않는다.
        /// </summary>
        private static bool Uncovered(PlayerDriver p, GridPoint cell)
        {
            UiRect r = FurniturePlacementHostTests.View(p).CellRect(cell);
            for (int up = 0; up <= 2; up++)
            {
                float y = r.CenterY - up * r.H;
                if (y < 0) return false;
                foreach (float x in new[] { r.X + 1, r.CenterX, r.Right - 1 })
                    if (UiAt(p.Game.Ui.Root, x, y)) return false;
            }
            return true;
        }

        private static bool UiAt(UiNode node, float x, float y)
        {
            if (!node.Visible || node.Covered) return false;
            foreach (UiNode child in node.Children)
                if (UiAt(child, x, y)) return true;
            return node.Rect.W < 1600 && node.Rect.H < 900 && node.Rect.Contains(x, y);
        }

        /// <summary>방 칸 cell의 맵 칸에서 세로로 dy 칸 떨어진 칸이 걸을 수 있는가(아래 = +, 위 = -).</summary>
        private static bool Open(PlayerDriver p, FurnitureRoom room, GridPoint cell, int dy)
        {
            CellPos map = room.ToMap(cell);
            var next = new CellPos(map.X, map.Y + dy);
            if (next.Y < 0 || next.Y >= p.World.Map.Grid.Rows) return false;
            return !p.World.Map.Collision.IsBlocked(next);
        }
    }
}
