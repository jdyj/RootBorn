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
    /// 놓을 칸 앞까지 걸어가 위를 보고 E → 배치 수가 하나 늘었다. 놓은 뒤 캐릭터를 가구 앞·뒤·(막지 않는 의자면) 그 칸에 세워 찍는다.
    /// 그림 픽셀이나 그림 경로는 단언하지 않는다(그림은 바뀐다) — 무엇이 보이는지는 스크린샷을 직접 열어 본다.
    /// 조작은 키·글자·포인터뿐(장치 이벤트 주입, OS 입력 계층 미확인). 놓을 칸을 고를 때만 Core 판정(House.CanPlace)과 충돌·화면 좌표를 읽는다.
    /// 스크린샷: output/pixel-kit/game/furniture/pixel-kit-furniture-NN-*.png, output/pixel-kit/game/chairs/pixel-kit-chairs-NN-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class PixelKitFurnitureHostTests
    {
        /// <summary>놓을 가구(테스트 데이터): 견줄 옛 그림(48px) 하나, 그다음 픽셀 키트 그림으로 바꾼 셋.</summary>
        private static readonly (string Id, string Shot)[] Furniture =
        {
            ("comfy31.kitchen.sink.front", "reference-sink"),
            ("school.bookcase", "school-bookcase"),
            ("school.archive30.archive.bookshelf", "archive-bookshelf"),
            ("comfy31.kitchen.counter.front", "kitchen-counter-front"),
        };

        /// <summary>
        /// 책상과 그 앞(맵에서 한 줄 아래) 칸의 의자(테스트 데이터). ChairDx = 의자가 책상 왼쪽 칸에서 몇 칸 오른쪽인가 —
        /// 책상 그림의 무릎 자리 쪽(서랍 반대쪽)에 맞춘 고르기일 뿐 단언하지 않는다.
        /// 새 슬롯의 팔레트에 있는 것만 쓴다: story.study-desk·chair.quest-reward는 잠긴 가구(unlockToken)라 새 슬롯에서 못 놓는다.
        /// </summary>
        private static readonly (string Desk, string Chair, int ChairDx, string Shot)[] DeskAndChair =
        {
            ("growth31.assembly.desk", "chair.black", 0, "home-desk"),
            ("school.desk", "school.chair.north", 0, "school-desk"),
        };

        /// <summary>혼자 놓는 것(테스트 데이터): 앞·뒤에 서 본다.</summary>
        private static readonly (string Id, string Shot)[] Alone =
        {
            ("school.teacher.desk", "teacher-desk"),
            ("school.desk.with.open.book", "desk-open-book"),
            ("school.chair.south", "school-chair-south"),
        };

        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-pixel-kit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            // 준비: 이 시험들은 빈 집에 키트 가구를 놓아 세고 찍는다 — 새 게임의 처음 배치 가구(작업 10)를 받지 않게 빈 배치 파일을 둔다
            // (처음 가구를 모두 치운 집과 같다). 임시 폴더의 슬롯에만 쓴다.
            FurnitureSaveFiles.SaveHouseLayout(new Rootborn.Data.Save.FileSaveStore(_saves), "slot-1", 0, Array.Empty<FurniturePlacementSaveData>());
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start(string evidence)
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "pixel-kit", "game", evidence),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        [Test]
        public void PIXEL_KIT_001_PlaceKitFurnitureWithPSearchAndE_ScreenshotsPaletteRoomAndBehind()
        {
            using PlayerDriver player = Start("furniture");
            var session = player.Game.Session;
            FurnitureRoom room = player.Game.World.Room;
            var house = session.House;
            GridPoint spawn = room.Grid.Spawn;
            player.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement);
            FurniturePlacementScreen screen = FurniturePlacementHostTests.Placement(player);
            var used = new List<GridPoint>();
            int shot = 1;

            foreach ((string id, string name) in Furniture)
            {
                FurnitureDefinition definition = session.Definitions.Get<FurnitureDefinition>(id);
                Pick(player, screen, definition);
                player.Screenshot($"pixel-kit-furniture-{shot++:D2}-{name}-palette");
                // 놓을 칸: 점유 칸마다 맵에서 바로 아래(앞)·위 두 줄(뒤, 그리고 벽·HUD에서 떨어지게) 칸이 걸을 수 있다.
                GridPoint target = FindCell(player, definition, used, Cells(definition, default), new[] { 1, -1, -2 }, id + " 놓을 칸");
                PlaceFacingUp(player, screen, definition, target);
                used.AddRange(Cells(definition, target));
                player.Screenshot($"pixel-kit-furniture-{shot++:D2}-{name}-placed-front");
                StandFacing(player, room, target, -1, TestKey.Down);
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
        /// 의자와 책상: 책상을 놓고 그 앞 칸에 의자를 놓는다(책상을 보고 앉을 자리). 막지 않는 의자면 그 칸에 걸어 들어가 서서
        /// 위(책상)·아래를 본다 — 캐릭터 칸이 의자 칸인지 단언한다. 혼자 놓은 책상·의자는 앞 칸과 뒤 칸에 서서 앞뒤 가림을 찍는다.
        /// </summary>
        [Test]
        public void PIXEL_KIT_002_DeskWithChairInFront_WalkIntoOpenChair_StandBehindAndInFront()
        {
            using PlayerDriver player = Start("chairs");
            var session = player.Game.Session;
            FurnitureRoom room = player.Game.World.Room;
            var house = session.House;
            GridPoint spawn = room.Grid.Spawn;
            player.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement);
            FurniturePlacementScreen screen = FurniturePlacementHostTests.Placement(player);
            var used = new List<GridPoint>();
            int shot = 1;

            foreach ((string deskId, string chairId, int dx, string name) in DeskAndChair)
            {
                FurnitureDefinition desk = session.Definitions.Get<FurnitureDefinition>(deskId);
                FurnitureDefinition chair = session.Definitions.Get<FurnitureDefinition>(chairId);
                var chairOffset = new GridPoint(dx, -1); // 방 칸은 위가 +Y다: 맵(화면)에서 한 줄 아래 = 방 Y - 1
                // 책상 칸: 책상 뒤 두 줄과 왼쪽 칸 바로 아래(책상을 놓으러 설 칸)가 걸을 수 있고, 의자 칸에 의자를 놓을 수 있고, 의자 앞 두 줄(놓으러 설 칸, 앞에 설 칸)이 걸을 수 있다.
                Pick(player, screen, desk);
                GridPoint target = FindCell(player, desk, used, Cells(desk, default).Append(chairOffset).ToList(), new[] { -1, -2 }, deskId + " 놓을 칸",
                    c => Open(player, room, c, 1)
                         && house.CanPlace(chair, Add(c, chairOffset), FurniturePlacementDirection.North)
                         && Open(player, room, Add(c, chairOffset), 1) && Open(player, room, Add(c, chairOffset), 2));
                PlaceFacingUp(player, screen, desk, target);
                used.AddRange(Cells(desk, target));
                player.Screenshot($"pixel-kit-chairs-{shot++:D2}-{name}-placed");

                GridPoint seat = Add(target, chairOffset);
                Pick(player, screen, chair);
                PlaceFacingUp(player, screen, chair, seat);
                used.Add(seat);
                player.Screenshot($"pixel-kit-chairs-{shot++:D2}-{name}-with-{chairId}-front");

                if (!chair.BlocksMovement)
                {
                    // 막지 않는 의자: 그 칸에 걸어 들어가 선다(앉는 동작은 아직 없다). 위 = 책상을 본다.
                    CellPos seatMap = room.ToMap(seat);
                    player.WalkTo(seatMap);
                    Assert.That(player.PlayerCell, Is.EqualTo(seatMap), chairId + ": 의자 칸에 들어가 섰다");
                    player.HoldKey(TestKey.Up, 1);
                    player.Frames(2);
                    player.Screenshot($"pixel-kit-chairs-{shot++:D2}-{name}-in-chair-facing-desk");
                    player.HoldKey(TestKey.Down, 1);
                    player.Frames(2);
                    player.Screenshot($"pixel-kit-chairs-{shot++:D2}-{name}-in-chair-facing-down");
                }

                StandFacing(player, room, target, -1, TestKey.Down);
                player.Screenshot($"pixel-kit-chairs-{shot++:D2}-{name}-behind-desk");
            }

            foreach ((string id, string name) in Alone)
            {
                FurnitureDefinition definition = session.Definitions.Get<FurnitureDefinition>(id);
                Pick(player, screen, definition);
                GridPoint target = FindCell(player, definition, used, Cells(definition, default), new[] { 1, -1, -2 }, id + " 놓을 칸");
                PlaceFacingUp(player, screen, definition, target);
                used.AddRange(Cells(definition, target));
                player.Screenshot($"pixel-kit-chairs-{shot++:D2}-{name}-front");
                StandFacing(player, room, target, -1, TestKey.Down);
                player.Screenshot($"pixel-kit-chairs-{shot++:D2}-{name}-behind");
            }

            player.WalkTo(room.ToMap(spawn));
            player.Frames(2);
            Assert.That(house.Placed.Count, Is.EqualTo(DeskAndChair.Length * 2 + Alone.Length));
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty, "빠진 그림이 없다");
            player.Screenshot($"pixel-kit-chairs-{shot:D2}-all-placed");
        }

        /// <summary>2026-11-15 키트 그림으로 바꾼 가구(테스트 데이터) 첫 묶음: 책상·서랍장·강의대·사물함·타이어.
        /// desk.basic·desk.large는 전에 그림이 없어 게임에서 빈칸이었다.</summary>
        private static readonly (string Id, string Shot)[] RedrawnFirst =
        {
            ("desk.basic", "desk-basic"),
            ("desk.large", "desk-large"),
            ("school.drawer.cabinet", "drawer-cabinet"),
            ("school.lectern", "lectern"),
            ("comfy31.school.lockers", "locker-steel"),
            ("school.lockers", "cubby-lockers"),
            ("activity31.training.tire", "training-tire"),
        };

        /// <summary>둘째 묶음: 부엌·집 소품. story.photo-board는 잠긴 가구라 새 슬롯에서 못 놓아 뺐다.</summary>
        private static readonly (string Id, string Shot)[] RedrawnSecond =
        {
            ("comfy31.kitchen.sink.front", "kitchen-sink"),
            ("studentlife.toy.kitchen", "toy-kitchen"),
            ("studentlife.snack.delivery.crate", "snack-crate"),
            ("school.potted.plant", "potted-plant"),
            ("school.waste.basket", "waste-basket"),
            ("growth31.first.aid.play.set", "first-aid-kit"),
        };

        [Test]
        public void PIXEL_KIT_003_RedrawnDesksCabinetsLockers_PlacedWithPSearchAndE() => PlaceAll("redrawn-1", RedrawnFirst);

        [Test]
        public void PIXEL_KIT_004_RedrawnKitchenAndHomeProps_PlacedWithPSearchAndE() => PlaceAll("redrawn-2", RedrawnSecond);

        /// <summary>목록의 가구를 하나씩 P 화면 검색 → 팔레트 칸 → 걸어가 E로 놓고 찍는다. 빠진 그림이 없어야 한다.</summary>
        private void PlaceAll(string evidence, (string Id, string Shot)[] list)
        {
            using PlayerDriver player = Start(evidence);
            var session = player.Game.Session;
            FurnitureRoom room = player.Game.World.Room;
            var house = session.House;
            player.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement);
            FurniturePlacementScreen screen = FurniturePlacementHostTests.Placement(player);
            var used = new List<GridPoint>();
            int shot = 1;
            foreach ((string id, string name) in list)
            {
                FurnitureDefinition definition = session.Definitions.Get<FurnitureDefinition>(id);
                Pick(player, screen, definition);
                GridPoint target = FindCell(player, definition, used, Cells(definition, default), new[] { 1, -1 }, id + " 놓을 칸");
                PlaceFacingUp(player, screen, definition, target);
                used.AddRange(Cells(definition, target));
                player.Screenshot($"pixel-kit-{evidence}-{shot++:D2}-{name}");
            }
            player.WalkTo(room.ToMap(room.Grid.Spawn));
            player.Frames(2);
            Assert.That(house.Placed.Count, Is.EqualTo(list.Length));
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty, "빠진 그림이 없다");
            player.Screenshot($"pixel-kit-{evidence}-{shot:D2}-all-placed");
        }

        /// <summary>검색 칸을 비우고 id를 친다 → 검색 결과의 그 칸을 누른다.</summary>
        private static void Pick(PlayerDriver player, FurniturePlacementScreen screen, FurnitureDefinition definition)
        {
            string id = definition.StableId;
            player.PressButton(FurniturePlacementScreen.SearchId);
            player.PressKey(TestKey.Backspace, screen.Palette.Query.Length);
            player.TypeText(id);
            Assert.That(screen.Palette.Query, Is.EqualTo(id));
            int slot = Enumerable.Range(0, 20).Where(i => screen.Palette.At(i)?.StableId == id).DefaultIfEmpty(-1).First();
            Assert.That(slot, Is.Not.EqualTo(-1), id + ": 검색 결과 첫 쪽(20칸)에 없다");
            player.Frames(2);
            player.PressButton(FurniturePlacementScreen.SlotId(slot));
            Assert.That(screen.Active, Is.SameAs(definition));
        }

        /// <summary>놓을 칸 아래(맵) 칸까지 걸어가 위 키 한 프레임으로 그 칸을 보고 E. 배치 수가 하나 늘고 그 칸에 놓였다.</summary>
        private static void PlaceFacingUp(PlayerDriver player, FurniturePlacementScreen screen, FurnitureDefinition definition, GridPoint target)
        {
            var house = player.Game.Session.House;
            CellPos mapCell = player.Game.World.Room.ToMap(target);
            player.WalkTo(new CellPos(mapCell.X, mapCell.Y + 1));
            player.HoldKey(TestKey.Up, 1);
            Assert.That(FurniturePlacementHostTests.View(player).TryFacingCell(out GridPoint facing) && facing.Equals(target), Is.True, "준비: 놓을 칸을 바라본다");
            int before = house.Placed.Count;
            player.PressKey(TestKey.E);
            player.Frames(2);
            string id = definition.StableId;
            Assert.That(screen.LastAction, Is.EqualTo("placed"), id);
            Assert.That(house.Placed.Count, Is.EqualTo(before + 1), id + ": 배치 수가 하나 늘었다");
            Assert.That(house.Placed.Last().Anchor, Is.EqualTo(target), id + ": 바라보는 칸에 놓였다");
            // 그림이 점유 칸 위에 서 있는지 스크린샷에서 재 보려고 칸의 화면 사각형을 적는다(단언하지 않는다).
            TestContext.Out.WriteLine($"{id}: 기준 칸 ({target.X}, {target.Y}), 화면 사각형 {FurniturePlacementHostTests.View(player).CellRect(target)}");
        }

        /// <summary>방 칸 cell의 맵 칸에서 세로로 dy 칸 떨어진 칸까지 걸어가 key 쪽을 본다.</summary>
        private static void StandFacing(PlayerDriver player, FurnitureRoom room, GridPoint cell, int dy, TestKey key)
        {
            CellPos map = room.ToMap(cell);
            player.WalkTo(new CellPos(map.X, map.Y + dy));
            player.HoldKey(key, 1);
            player.Frames(2);
        }

        /// <summary>
        /// 놓을 칸: 놓을 수 있고, 출발 자리·먼저 놓은 것에서 떨어지고, area(기준 칸에서의 상대 칸)마다 화면에 보이고 UI에 덮이지 않으며
        /// 맵에서 rows(+ = 아래, - = 위) 칸이 걸을 수 있다. more는 덧붙는 조건.
        /// </summary>
        private static GridPoint FindCell(PlayerDriver player, FurnitureDefinition definition, List<GridPoint> used, IReadOnlyList<GridPoint> area,
            int[] rows, string what, Func<GridPoint, bool> more = null)
        {
            FurnitureRoom room = player.Game.World.Room;
            var house = player.Game.Session.House;
            GridPoint spawn = room.Grid.Spawn;
            return FurniturePlacementHostTests.FindCell(player, c =>
                house.CanPlace(definition, c, FurniturePlacementDirection.North)
                && !FurniturePlacementHostTests.Near(c, spawn, 1)
                && area.All(a => used.All(u => !FurniturePlacementHostTests.Near(u, Add(c, a), 2)))
                && area.All(a => rows.All(dy => Open(player, room, Add(c, a), dy, area.Select(b => room.ToMap(Add(c, b))).ToHashSet()))
                                 && Uncovered(player, Add(c, a)))
                && (more == null || more(c)),
                what);
        }

        /// <summary>북쪽 방향으로 놓은 가구의 점유 칸(기준 칸 at에서).</summary>
        private static List<GridPoint> Cells(FurnitureDefinition definition, GridPoint at) =>
            definition.GetFootprintCells(FurniturePlacementDirection.North).Select(f => Add(at, f)).ToList();

        private static GridPoint Add(GridPoint a, GridPoint b) => new GridPoint(a.X + b.X, a.Y + b.Y);

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

        /// <summary>
        /// 방 칸 cell의 맵 칸에서 세로로 dy 칸 떨어진 칸이 걸을 수 있고, 캐릭터가 지금 자리에서 placed(곧 놓을 가구의 맵 칸)를 밟지 않고
        /// 걸어 닿는가(아래 = +, 위 = -). 닿는지도 보는 것은 방 안에 벽으로 갇힌 칸과, 가구를 놓으면 막다른 곳이 되는 칸이 있어서다.
        /// </summary>
        private static bool Open(PlayerDriver p, FurnitureRoom room, GridPoint cell, int dy, ISet<CellPos> placed = null)
        {
            CollisionGrid collision = p.World.Map.Collision;
            CellPos map = room.ToMap(cell);
            var next = new CellPos(map.X, map.Y + dy);
            if (next.Y < 0 || next.Y >= p.World.Map.Grid.Rows || collision.IsBlocked(next)) return false;
            if (placed != null && placed.Contains(next)) return true; // 가구 칸 자체(앞뒤 줄이 겹칠 때)는 따로 판정하지 않는다
            // 너비 우선으로 칸을 퍼뜨린다(GridPath와 같은 걸음, 놓을 칸을 막힌 것으로 본다).
            var seen = new HashSet<CellPos> { p.PlayerCell };
            var queue = new Queue<CellPos>(seen);
            while (queue.Count > 0)
            {
                CellPos at = queue.Dequeue();
                if (at.Equals(next)) return true;
                foreach ((int sx, int sy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var step = new CellPos(at.X + sx, at.Y + sy);
                    if (collision.IsBlocked(step) || (placed != null && placed.Contains(step)) || !seen.Add(step)) continue;
                    queue.Enqueue(step);
                }
            }
            return false;
        }
    }
}
