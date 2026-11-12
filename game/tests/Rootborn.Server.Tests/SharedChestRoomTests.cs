using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.World;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 같이 쓰는 상자를 전송으로 이어 본다: 방 하나(RoomServer + 손님 세이브 폴더 + HouseGrids의 집·상자 규칙, 실제 data/와 집 맵) + 게임 한 판 둘.
    /// 넣고 꺼내기는 GameSession 명령 직접 호출이다(화면·입력 근거 아님). 같은 프로세스 안 전송. 손님 진행은 저장과 함께 방에 간다.
    /// </summary>
    public sealed class SharedChestRoomTests
    {
        private static GameDefinitions _definitions;
        private static GameStartSettings _settings;
        private static PlacementGrid _houseGrid;
        private const string Surface = "house";
        private string _root;

        [OneTimeSetUp]
        public void LoadData()
        {
            var load = GameDataLoader.Load(Path.Combine(ServerProcessTests.RepoRoot(), "data"), RootbornBuilders.Register);
            load.ThrowIfFailed();
            _definitions = load.Definitions;
            _settings = GameStartSettings.FromDefinitions(_definitions);
            _houseGrid = HouseGrids.FromMap(WorldMapLoader.FromRepo(ServerProcessTests.RepoRoot()).LoadMap("House").World);
        }

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-shared-chest", Guid.NewGuid().ToString("N"));
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

        private RoomRig NewRoom()
        {
            var settings = new RoomServerSettings { MaxPlayers = 4, SharedPieces = new[] { RoomHouse.PieceName } };
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
            guest.Game.EnterHouse(_houseGrid, Surface);
            rig.Run(0.3);
            return guest;
        }

        private static string ChestFurniture => _definitions.All<FurnitureStorageDefinition>().First().Furniture.StableId;

        private static string PlaceChest(RoomRig rig, Guest g)
        {
            var f = _definitions.Get<FurnitureDefinition>(ChestFurniture);
            var cell = _houseGrid.FloorCellsInOrder().First(c => g.Game.House.CanPlace(f, c, FurniturePlacementDirection.North));
            g.Game.PlaceFurniture(ChestFurniture, cell, FurniturePlacementDirection.North);
            rig.Run(0.5);
            return g.Game.House.Placed.Single(p => p.Definition.StableId == ChestFurniture).InstanceId;
        }

        private static RoomChests ChestsOf(RoomRig rig) => rig.Room.Pieces.TryGet(RoomChests.PieceName, out var c) ? (RoomChests)c : null;

        [Test]
        public void WhatOnePuts_TheOtherTakes_AndTheItemsStayInTheRoomAfterTheyLeave()
        {
            using var rig = NewRoom();
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            string chest = PlaceChest(rig, a);
            var wood = _definitions.Get<Rootborn.Core.Inventory.ItemDefinition>("Wood");
            a.Game.Inventory.Add(wood, 10);

            Assert.That(a.Game.PutIntoStorage(chest, "Wood", 10), Is.EqualTo(StorageTransferResult.Requested));
            rig.Run(1.0);
            Assert.That(a.Game.Inventory.CountOf(wood), Is.EqualTo(0));
            Assert.That(ChestsOf(rig).CountOf(chest, "Wood"), Is.EqualTo(10));
            Assert.That(b.Game.StorageAt(chest).CountOf(wood), Is.EqualTo(10), "b의 화면이 읽을 상자");

            a.Client.Dispose();
            rig.Run(0.5);
            Assert.That(ChestsOf(rig).CountOf(chest, "Wood"), Is.EqualTo(10), "a가 나가도 상자의 물건은 방에 남는다");

            Assert.That(b.Game.TakeFromStorage(chest, "Wood", 7), Is.EqualTo(StorageTransferResult.Requested));
            rig.Run(1.0);
            Assert.That(b.Game.Inventory.CountOf(wood), Is.EqualTo(7));
            Assert.That(ChestsOf(rig).CountOf(chest, "Wood"), Is.EqualTo(3));
            Assert.That(ChestsOf(rig).HeldCount, Is.EqualTo(0), "잡은 것은 모두 쓰였다");
        }

        [Test]
        public void BothTakeTheSameItemsAtOnce_OnlyTheFirstGetsThem_NothingIsCopied()
        {
            using var rig = NewRoom();
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            string chest = PlaceChest(rig, a);
            var stone = _definitions.Get<Rootborn.Core.Inventory.ItemDefinition>("Stone");
            a.Game.Inventory.Add(stone, 5);
            a.Game.PutIntoStorage(chest, "Stone", 5);
            rig.Run(1.0);

            a.Game.TakeFromStorage(chest, "Stone", 5);
            b.Game.TakeFromStorage(chest, "Stone", 5);
            rig.Run(1.5);
            int got = a.Game.Inventory.CountOf(stone) + b.Game.Inventory.CountOf(stone);
            Assert.That(got, Is.EqualTo(5), "한 사람만 받았다");
            Assert.That(a.Game.Inventory.CountOf(stone) == 5 ^ b.Game.Inventory.CountOf(stone) == 5, Is.True);
            Assert.That(ChestsOf(rig).CountOf(chest, "Stone"), Is.EqualTo(0));
            Assert.That(a.Game.WaitingForStorage || b.Game.WaitingForStorage, Is.False);
        }

        [Test]
        public void TheRoomRestarts_TheChestAndTheGuestsBagContinue()
        {
            string chest;
            var wood = _definitions.Get<Rootborn.Core.Inventory.ItemDefinition>("Wood");
            using (var rig = NewRoom())
            {
                var a = Join(rig, "a");
                chest = PlaceChest(rig, a);
                a.Game.Inventory.Add(wood, 6);
                a.Game.PutIntoStorage(chest, "Wood", 4);
                rig.Run(1.0);
                Assert.That(a.Game.Inventory.CountOf(wood), Is.EqualTo(2));
                rig.Room.Shutdown();
            }

            using var again = NewRoom();
            var c = Join(again, "a");
            Assert.That(ChestsOf(again).CountOf(chest, "Wood"), Is.EqualTo(4), "서버를 끄고 켜도 상자가 이어진다");
            Assert.That(c.Game.Inventory.CountOf(wood), Is.EqualTo(2), "손님의 가방은 방이 가진 손님 세이브에서");
            Assert.That(c.Game.StorageAt(chest).CountOf(wood), Is.EqualTo(4));
            Assert.That(c.Game.RemoveFurniture(chest), Is.False);
            again.Run(0.5);
            Assert.That(c.Game.House.Placed.Any(p => p.InstanceId == chest), Is.True, "물건이 든 상자는 방이 치우지 않는다");
        }
    }
}
