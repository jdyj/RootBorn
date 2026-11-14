using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Screens.Furniture;
using Rootborn.World;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 같이 쓰는 집(멀티플레이 설계 4.1절)을 전송으로 이어 본다: 방 하나(RoomServer + 손님 세이브 폴더 + 집 판정 칸 HouseGrids, 실제 data/와 집 맵) +
    /// 게임 한 판 둘(GameSession, 방이 내려 준 세이브). Host가 할 일(잇기, 집에 들어가기)을 테스트가 한다. 가구 놓기는 GameSession 명령 직접 호출이다
    /// (화면·입력 근거 아님 — 화면은 Host의 MP_HOUSE 테스트). 같은 프로세스 안 전송.
    /// </summary>
    public sealed class SharedHouseRoomTests
    {
        private static GameDefinitions _definitions;
        private static GameStartSettings _settings;
        private static PlacementGrid _houseGrid;
        private const string HouseMap = "House";
        private const string Surface = "house";
        private string _root;

        [OneTimeSetUp]
        public void LoadData()
        {
            var load = GameDataLoader.Load(Path.Combine(ServerProcessTests.RepoRoot(), "data"), RootbornBuilders.Register);
            load.ThrowIfFailed();
            _definitions = load.Definitions;
            _settings = GameStartSettings.FromDefinitions(_definitions);
            _houseGrid = HouseGrids.FromMap(WorldMapLoader.FromRepo(ServerProcessTests.RepoRoot()).LoadMap(HouseMap).World);
        }

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-shared-house", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private sealed class Guest
        {
            public SessionClient Client;
            public RoomGuestLink Link;
            public GameSession Game;
        }

        private string Saves => Path.Combine(_root, "saves");

        private RoomRig NewRoom(params string[] shared)
        {
            var settings = new RoomServerSettings { MaxPlayers = 4, SharedPieces = shared };
            var guests = new GuestSaveFolder(GuestSaveFolder.FolderFor(Saves, "room-a"), GameSession.SaveCheck(_definitions, _settings));
            var pieces = RoomPieceCatalog.CreateDefault(HouseGrids.Rules(_definitions, ServerProcessTests.RepoRoot(), null));
            return new RoomRig(new FileSaveStore(Saves), settings: settings, timeRules: Program.RoomTimeRules(_definitions), guests: guests, pieces: pieces);
        }

        private static Guest Join(RoomRig rig, string playerId)
        {
            var guest = new Guest { Client = rig.Join(playerId) };
            guest.Link = new RoomGuestLink(guest.Client, new SessionClientSettings().MaxMessageBytes);
            rig.Run(0.5);
            Assert.That(guest.Link.Ready, Is.True, playerId);
            var store = new MemorySaveSlot(RoomGuestLink.Slot, guest.Link.Files.Writes);
            store.Committed += c => guest.Link.Upload(SaveFileSet.From(c.Writes, c.Deletes));
            guest.Game = GameSession.Start(_definitions, _settings, store, RoomGuestLink.Slot, playerId, shares: guest.Link.Shares);
            guest.Game.EnterHouse(_houseGrid, Surface); // Host: 집 맵에 들어가면
            rig.Run(0.3);
            return guest;
        }

        private static string Chair(Guest g) => g.Game.AvailableFurniture()[0].StableId;

        private static GridPoint Free(Guest g, string furniture, int skip = 0)
        {
            var f = g.Game.Definitions.Get<FurnitureDefinition>(furniture);
            foreach (var cell in _houseGrid.FloorCellsInOrder())
                if (g.Game.House.CanPlace(f, cell, FurniturePlacementDirection.North) && skip-- <= 0) return cell;
            throw new InvalidOperationException("빈 칸이 없다");
        }

        private static string[] Lines(Guest g)
            => g.Game.House.Placed.Select(p => p.Definition.StableId + "@" + p.Anchor.X + "," + p.Anchor.Y + ":" + p.Direction).ToArray();

        private static RoomHouse RoomHouseOf(RoomRig rig) => rig.Room.Pieces.TryGet(RoomHouse.PieceName, out var h) ? (RoomHouse)h : null;

        [Test]
        public void HouseShareOn_WhatOnePlaces_TheOtherSees_AndTheSameCellAtOnce_OnlyTheFirst()
        {
            using var rig = NewRoom(RoomHouse.PieceName);
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            string chair = Chair(a);
            var cell = Free(a, chair);

            a.Game.PlaceFurniture(chair, cell, FurniturePlacementDirection.North);
            b.Game.PlaceFurniture(chair, cell, FurniturePlacementDirection.North);
            Assert.That(a.Game.WaitingForRoom && b.Game.WaitingForRoom, Is.True);
            rig.Run(1.0);

            Assert.That(RoomHouseOf(rig).Placed.Count, Is.EqualTo(1), "같은 칸은 먼저 온 요청만");
            Assert.That(RoomHouseOf(rig).Placed[0].PlacedBy, Is.EqualTo("a"));
            Assert.That(a.Game.LastRoomCommandApplied, Is.True);
            Assert.That(b.Game.LastRoomCommandApplied, Is.False);
            Assert.That(b.Game.House.Placed.Single().Anchor, Is.EqualTo(cell), "a가 놓은 가구가 b의 집에 보인다");
            Assert.That(a.Game.WaitingForRoom || b.Game.WaitingForRoom, Is.False);
        }

        [Test]
        public void HouseShareOn_RemovingReturnsNothingToLose_ThePlacerKeepsIt_AndTheRoomContinuesAfterRestart()
        {
            using (var rig = NewRoom(RoomHouse.PieceName))
            {
                var a = Join(rig, "a");
                var b = Join(rig, "b");
                string chair = Chair(a);
                int available = a.Game.AvailableFurniture().Count;
                a.Game.PlaceFurniture(chair, Free(a, chair), FurniturePlacementDirection.North);
                rig.Run(0.5);
                a.Game.PlaceFurniture(chair, Free(a, chair, 4), FurniturePlacementDirection.North);
                rig.Run(0.5);
                Assert.That(b.Game.House.Placed.Count, Is.EqualTo(2));

                b.Game.RemoveFurniture(b.Game.House.Placed[0].InstanceId);
                rig.Run(0.5);
                Assert.That(a.Game.House.Placed.Count, Is.EqualTo(1), "b가 치운 것이 a에게도");
                Assert.That(a.Game.AvailableFurniture().Count, Is.EqualTo(available), "치워도 놓은 사람은 그 가구를 그대로 가진다(해금은 줄지 않는다)");
                rig.Room.Shutdown();
            }

            using var again = NewRoom(RoomHouse.PieceName);
            var c = Join(again, "a");
            Assert.That(c.Game.House.Placed.Count, Is.EqualTo(1), "서버를 끄고 켜도 같이 쓰는 집이 이어진다");
            Assert.That(RoomHouseOf(again).Placed.Single().PlacedBy, Is.EqualTo("a"));
        }

        [Test]
        public void HouseShareOff_EachHasTheirOwnHouse_InTheirOwnGuestFolder()
        {
            using var rig = NewRoom(RoomWallet.PieceName);
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            string chair = Chair(a);
            // b는 자기 집이 있는 새 플레이어라 처음 배치 가구를 받는다(GameSession.HouseStarter). 보는 것은 a의 놓기가 b의 집을 바꾸지 않는가다.
            string[] bBefore = Lines(b);
            var anchor = Free(a, chair);
            Assert.That(a.Game.PlaceFurniture(chair, anchor, FurniturePlacementDirection.North), Is.True, "곧바로 놓인다(자기 집)");
            rig.Run(0.5);
            Assert.That(Lines(b), Is.EqualTo(bBefore), "a의 가구가 b의 집에 오지 않는다");
            Assert.That(b.Game.House.Placed.Any(p => p.Anchor.Equals(anchor) && p.Definition.StableId == chair), Is.False);
            Assert.That(RoomHouseOf(rig), Is.Null);
            Assert.That(File.Exists(Path.Combine(GuestSaveFolder.FolderFor(Saves, "room-a"), "a", SaveFileNames.HouseFurnitureLayout(a.Game.HouseStage))), Is.True,
                "a의 집은 a의 손님 세이브에");
        }

        [Test]
        public void AHouseFullOfFurniture_ReachesEveryone_EvenWhenItDoesNotFitInOneMessage()
        {
            using var rig = NewRoom(RoomHouse.PieceName);
            // 준비: 방에 직접 가구를 많이 놓는다(긴 id의 가구). 상태가 메시지 하나(4KB)를 넘는다.
            var longest = _definitions.All<FurnitureDefinition>().Where(f => f.AllowsSurface(Surface) && f.FootprintCells.Count == 1 && f.TileParts.Count == 1)
                .OrderBy(f => f.BlocksMovement).ThenByDescending(f => f.StableId.Length).First();
            var requests = new List<RoomRequestSaveData>();
            int n = 0;
            foreach (var cell in _houseGrid.FloorCellsInOrder())
            {
                requests.Add(new RoomRequestSaveData
                {
                    Piece = RoomHouse.PieceName, RequestId = "seed" + n++, Kind = RoomHouse.Place,
                    Ids = new[] { Surface, longest.StableId, cell.X.ToString(), cell.Y.ToString(), "0" },
                });
            }

            rig.Room.Pieces.Apply("setup", requests, 1, null, null);
            int placed = RoomHouseOf(rig).Placed.Count;
            var state = RoomHouseOf(rig).State();
            Assert.That(state.Ids.Sum(id => id.Length + 4), Is.GreaterThan(new SessionServerSettings().MaxMessageBytes), "이 시험의 전제: 상태가 메시지 하나보다 크다");

            var a = Join(rig, "a");
            Assert.That(a.Game.House.Placed.Count, Is.EqualTo(placed), "나눠 보낸 상태를 다 모았다");
        }

        [Test]
        public void TheServersHouseGrid_IsTheSameAsTheOneThePlayersScreenUses()
        {
            var map = WorldMapLoader.FromRepo(ServerProcessTests.RepoRoot()).LoadMap(HouseMap).World;
            var screen = new FurnitureRoom(map).Grid;
            var server = HouseGrids.FromMap(map);
            Assert.That(server.Spawn, Is.EqualTo(screen.Spawn));
            Assert.That(server.Approaches, Is.EqualTo(screen.Approaches));
            for (int x = -1; x <= map.Grid.Columns; x++)
                for (int y = -1; y <= map.Grid.Rows; y++)
                {
                    var cell = new GridPoint(x, y);
                    Assert.That(server.HasFloor(cell), Is.EqualTo(screen.HasFloor(cell)), cell.ToString());
                    Assert.That(server.IsFixed(cell), Is.EqualTo(screen.IsFixed(cell)), cell.ToString());
                }

            var rules = HouseGrids.Load(ServerProcessTests.RepoRoot(), null);
            Assert.That(rules.Keys, Does.Contain(Surface), "interactions.json의 가구 방마다 칸을 만든다");
        }

        [Test]
        public void WithoutTheMaps_TheRoomRefusesToPlace_AndSaysWhy()
        {
            var log = new List<string>();
            var rules = HouseGrids.Rules(_definitions, Path.Combine(_root, "no-repo"), log.Add);
            var room = new RoomPieceSet(new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault(rules));
            var answers = new List<RoomAnswer>();
            room.Apply("a", new[] { new RoomRequestSaveData { Piece = RoomHouse.PieceName, RequestId = "r", Kind = RoomHouse.Place, Ids = new[] { Surface, _definitions.All<FurnitureDefinition>().First().StableId, "1", "1", "0" } } },
                1, answers, null);
            Assert.That(answers[0].Accepted, Is.False);
            Assert.That(log.Any(l => l.Contains("집 맵을 읽지 못했다")), Is.True, string.Join("\n", log));
        }
    }

    internal static class PlacementGridCells
    {
        /// <summary>바닥 칸을 위 줄부터(테스트용 — 놓을 수 있는 첫 칸 찾기).</summary>
        public static IEnumerable<GridPoint> FloorCellsInOrder(this PlacementGrid grid)
        {
            for (int y = 40; y >= -1; y--)
                for (int x = -1; x <= 40; x++)
                {
                    var cell = new GridPoint(x, y);
                    if (grid.HasFloor(cell) && !grid.IsFixed(cell)) yield return cell;
                }
        }
    }
}
