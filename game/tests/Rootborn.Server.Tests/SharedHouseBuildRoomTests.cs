using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.World;
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
    /// 같이 쓰는 집의 꾸밈(집 꾸미기 1단계 (다))을 전송으로 이어 본다: 방 하나(RoomServer + 손님 세이브 폴더 + HouseGrids.Rules의 틀·꾸밈 정의, 실제 data/와 집 맵) +
    /// 게임 한 판 둘(GameSession). Host가 할 일(잇기, 집에 들어가기, 틀 붙이기)을 테스트가 한다. 꾸밈 요청은 GameSession 명령 직접 호출이다
    /// (화면·입력 근거 아님 — 자동 테스트). 같은 프로세스 안 전송.
    /// </summary>
    public sealed class SharedHouseBuildRoomTests
    {
        private const string HouseMap = "House";
        private const string Surface = "house";
        private const string Allowance = "item.allowance";
        private const string RoomId = "room-a";
        private static readonly CellPos TopLeftRoom = new CellPos(3, 4);

        private static GameDefinitions _definitions;
        private static GameStartSettings _settings;
        private static PlacementGrid _houseGrid;
        private static HouseBuildFrame _frame;
        private string _root;

        [OneTimeSetUp]
        public void LoadData()
        {
            string repo = ServerProcessTests.RepoRoot();
            var load = GameDataLoader.Load(Path.Combine(repo, "data"), RootbornBuilders.Register);
            load.ThrowIfFailed();
            _definitions = load.Definitions;
            _settings = GameStartSettings.FromDefinitions(_definitions);
            var loaded = WorldMapLoader.FromRepo(repo).LoadMap(HouseMap);
            _houseGrid = HouseGrids.FromMap(loaded.World);
            _frame = HouseBuildFrames.From(loaded) ?? throw new InvalidOperationException("집 맵은 짓는 집이어야 한다");
        }

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-shared-house-build", Guid.NewGuid().ToString("N"));
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
            public string Id;
        }

        private string Saves => Path.Combine(_root, "saves");

        /// <param name="saves">방의 세이브 폴더(없으면 <see cref="Saves"/>). 방 둘을 띄울 때 둘째 방에 다른 폴더를 준다.</param>
        private RoomRig NewRoom(string[] shared, int wallet = -1, RoomVersions? versions = null, string saves = null)
        {
            saves ??= Saves;
            var settings = new RoomServerSettings { MaxPlayers = 4, SharedPieces = shared };
            var guests = new GuestSaveFolder(GuestSaveFolder.FolderFor(saves, RoomId), GameSession.SaveCheck(_definitions, _settings));
            var pieces = RoomPieceCatalog.CreateDefault(HouseGrids.Rules(_definitions, ServerProcessTests.RepoRoot(), null));
            var rig = new RoomRig(new FileSaveStore(saves), RoomId, settings: settings, timeRules: Program.RoomTimeRules(_definitions), guests: guests, pieces: pieces,
                versions: versions);
            if (wallet >= 0)
            {
                var store = new FileSaveStore(Saves);
                store.Write(RoomId, SaveFileNames.RoomPiece(RoomWallet.PieceName), new RoomWalletSaveData { Version = RoomWalletSaveData.KnownVersion, Balance = wallet });
                WalletOf(rig).Load(store, RoomId);
            }

            return rig;
        }

        /// <param name="uploads">방에 닿지 못한 저장(Host RoomConnection이 방마다 들고 있는 것). 없으면 새 것.</param>
        private static Guest Join(RoomRig rig, string playerId, GuestUploads uploads = null)
        {
            var guest = new Guest { Id = playerId, Client = rig.Join(playerId) };
            guest.Link = new RoomGuestLink(guest.Client, new SessionClientSettings().MaxMessageBytes, uploads);
            rig.Run(0.5);
            Assert.That(guest.Link.Ready, Is.True, playerId);
            var store = new MemorySaveSlot(RoomGuestLink.Slot, guest.Link.Files.Writes);
            store.Committed += c => guest.Link.Upload(SaveFileSet.From(c.Writes, c.Deletes));
            guest.Game = GameSession.Start(_definitions, _settings, store, RoomGuestLink.Slot, playerId, shares: guest.Link.Shares);
            guest.Game.EnterHouse(_houseGrid, Surface); // Host: 집 맵에 들어가면
            guest.Game.EnterHouseBuild(_frame); // 짓는 집이면 틀을 붙인다
            rig.Run(0.3);
            return guest;
        }

        private static RoomHouseBuild BuildOf(RoomRig rig) => rig.Room.Pieces.TryGet(RoomHouseBuild.PieceName, out var p) ? (RoomHouseBuild)p : null;

        private static RoomWallet WalletOf(RoomRig rig) => rig.Room.Pieces.TryGet(RoomWallet.PieceName, out var p) ? (RoomWallet)p : null;

        private static IEnumerable<RoomFinishDefinition> PaidFloors()
            => _definitions.All<RoomFinishDefinition>()
                .Where(f => f.Slot == RoomFinishSlots.Floor && f.UnlockToken.Length == 0 && f.Id != _frame.DefaultFloor && f.Cost.Count > 0);

        private static int PerCell(RoomFinishDefinition finish) => finish.Cost.Where(c => c.Item == Allowance).Sum(c => c.Count);

        private static int Money(Guest g) => g.Game.Inventory.CountOf(g.Game.Item(Allowance));

        private static void Give(Guest g, int amount) => g.Game.Inventory.Add(g.Game.Item(Allowance), amount);

        private static int Changed(Guest g, RoomFinishDefinition f) => g.Game.PreviewHouseFinish(f.Id, HouseFinishTools.Room, TopLeftRoom).ChangedCells.Count;

        [Test]
        public void WhatOneDecorates_TheOtherSees_ThroughTheTransport()
        {
            using var rig = NewRoom(new[] { RoomHouse.PieceName });
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            Assert.That(a.Game.SharesHouseBuild && b.Game.SharesHouseBuild, Is.True, "집을 같이 쓰면 꾸밈도 같이 쓴다");
            var floor = PaidFloors().First();
            int cells = Changed(a, floor);
            Assert.That(cells, Is.GreaterThan(0));
            Give(a, PerCell(floor) * cells);
            var preview = a.Game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            Assert.That(b.Game.HouseFloorAt(TopLeftRoom), Is.EqualTo(_frame.DefaultFloor), "전제: 처음에는 기본 바닥");

            Assert.That(a.Game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False, "방에 요청만 했다");
            rig.Run(1.0);

            foreach (var cell in preview.Cells)
            {
                Assert.That(a.Game.HouseFloorAt(cell), Is.EqualTo(floor.Id));
                Assert.That(b.Game.HouseFloorAt(cell), Is.EqualTo(floor.Id), "b에게도 보인다");
            }

            Assert.That(BuildOf(rig).ToSaveData().Finishes.Length, Is.EqualTo(cells), "방의 기록");
            Assert.That(BuildOf(rig).HeldCount, Is.Zero);
            Assert.That(a.Game.WaitingForRoom || b.Game.WaitingForRoom, Is.False);
        }

        [Test]
        public void TheRoomsPlayerId_IsTheSessionsPlayerId_SoTheHoldIsTheDeciderOwn()
        {
            // 앞 작업의 걱정: 게임 한 판의 "떠도는 내 잡기" 판단은 방이 아는 플레이어 id = GameSession.PlayerId에 기댄다.
            // 서버를 거친 요청의 잡기가 그 세션의 CopyHoldsOf에 나와야 한다(안 나오면 떠도는 잡기가 풀리지 않거나 남의 것이 풀린다).
            using var rig = NewRoom(new[] { RoomHouse.PieceName });
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            var floor = PaidFloors().First();
            Give(a, PerCell(floor) * Changed(a, floor));
            Assert.That(a.Game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);

            var mine = new List<string>();
            var others = new List<string>();
            bool seen = false;
            for (int i = 0; i < 40 && !seen; i++)
            {
                rig.Run(rig.Settings.TickSeconds);
                a.Link.Shares.Find<RoomHouseBuildClient>().CopyHoldsOf(a.Game.PlayerId, mine);
                b.Link.Shares.Find<RoomHouseBuildClient>().CopyHoldsOf(a.Game.PlayerId, others);
                seen = mine.Count > 0;
            }

            Assert.That(seen, Is.True, "서버를 거친 잡기가 a 세션의 PlayerId로 나온다(\"" + a.Game.PlayerId + "\")");
            Assert.That(others, Is.EquivalentTo(mine), "다른 손님도 같은 주인으로 본다");
            b.Link.Shares.Find<RoomHouseBuildClient>().CopyHoldsOf(b.Game.PlayerId, others);
            Assert.That(others, Is.Empty, "b의 잡기는 없다");
        }

        [Test]
        public void MoneyNotShared_TheDecoratorPays_FromTheirOwnWallet()
        {
            using var rig = NewRoom(new[] { RoomHouse.PieceName });
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            var floor = PaidFloors().First();
            int cost = PerCell(floor) * Changed(a, floor);
            Give(a, cost + 5);
            int aMoney = Money(a), bMoney = Money(b);

            a.Game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            rig.Run(1.0);
            rig.Run(0.5);

            Assert.That(Money(a), Is.EqualTo(aMoney - cost), "한 번만");
            Assert.That(Money(b), Is.EqualTo(bMoney), "다른 사람의 돈은 그대로");
            Assert.That(b.Game.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(BuildOf(rig).HeldCount, Is.Zero);
        }

        [Test]
        public void MoneyShared_TheRoomWalletPaysOnce()
        {
            var floor = PaidFloors().First();
            int cost = 0;
            using var rig = NewRoom(new[] { RoomWallet.PieceName, RoomHouse.PieceName }, 100_000);
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            cost = PerCell(floor) * Changed(a, floor);
            Assert.That(a.Game.SharesWallet && a.Game.SharesHouseBuild, Is.True);

            a.Game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            rig.Run(1.5);
            rig.Run(0.5);

            Assert.That(WalletOf(rig).Balance, Is.EqualTo(100_000 - cost), "방 지갑에서 정확히 한 번");
            Assert.That(WalletOf(rig).HeldTotal, Is.Zero);
            Assert.That(Money(a), Is.EqualTo(WalletOf(rig).Balance), "같이 쓰는 돈이면 가방 대신 방 지갑이 보인다");
            Assert.That(b.Game.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(BuildOf(rig).HeldCount, Is.Zero);
        }

        // 서버가 칸을 잡았고(HeldCount 1) a의 쓰기는 아직 방에 닿지 않은 때까지 한 틱씩 돈다.
        private static void RunUntilHeld(RoomRig rig)
        {
            for (int i = 0; i < 60 && BuildOf(rig).HeldCount == 0; i++) rig.Run(rig.Settings.TickSeconds);
            Assert.That(BuildOf(rig).HeldCount, Is.EqualTo(1), "준비: 방이 칸을 잡았다");
        }

        [Test]
        public void TheRoomRestarts_FinishesAndHoldsContinue_ANewArrivalSeesThem()
        {
            var floors = PaidFloors().Take(2).ToArray();
            var first = floors[0];
            var second = floors[1];
            int secondCost;
            string[] shared = { RoomHouse.PieceName };
            Guest a;
            using (var rig = NewRoom(shared))
            {
                a = Join(rig, "a");
                int firstCost = PerCell(first) * Changed(a, first);
                Give(a, firstCost + 10_000);
                a.Game.ApplyHouseFinish(first.Id, HouseFinishTools.Room, TopLeftRoom);
                rig.Run(1.5);
                Assert.That(a.Game.HouseFloorAt(TopLeftRoom), Is.EqualTo(first.Id), "전제: 첫 꾸밈이 끝났다");
                Assert.That(BuildOf(rig).HeldCount, Is.Zero);

                // 둘째: 칸을 잡은 채로 방이 꺼진다(a의 쓰기가 닿기 전).
                secondCost = PerCell(second) * Changed(a, second);
                a.Game.ApplyHouseFinish(second.Id, HouseFinishTools.Room, TopLeftRoom);
                RunUntilHeld(rig);
                rig.Room.Shutdown();
            }

            using var again = NewRoom(shared);
            Assert.That(BuildOf(again).HeldCount, Is.EqualTo(1), "다시 켜도 잡기가 이어진다");
            var c = Join(again, "c");
            Assert.That(c.Game.HouseFloorAt(TopLeftRoom), Is.EqualTo(first.Id), "새 손님의 첫 상태가 끝난 꾸밈이다");

            Give(c, 10_000);
            int cMoney = Money(c);
            c.Game.ApplyHouseFinish(second.Id, HouseFinishTools.Room, TopLeftRoom);
            again.Run(1.0);
            Assert.That(c.Game.LastHouseFinishRefusal, Is.EqualTo(HouseFinishReasons.RoomRefused), "잡힌 칸의 요청은 거절");
            Assert.That(Money(c), Is.EqualTo(cMoney), "거절된 사람은 내지 않는다");
            Assert.That(c.Game.HouseFloorAt(TopLeftRoom), Is.EqualTo(first.Id));

            // 잡은 사람이 돌아와 끝낸다: 정확히 한 번.
            var a2 = Join(again, "a");
            again.Run(1.5);
            again.Run(0.5);
            Assert.That(BuildOf(again).HeldCount, Is.Zero, "잡기가 끝났다");
            Assert.That(a2.Game.HouseFloorAt(TopLeftRoom), Is.EqualTo(second.Id), "돌아와 끝낸 꾸밈이 적용됐다");
            Assert.That(c.Game.HouseFloorAt(TopLeftRoom), Is.EqualTo(second.Id));
            Assert.That(Money(a2), Is.EqualTo(10_000 - secondCost), "둘째 값은 정확히 한 번만 낸다(첫 값은 앞서 냈다)");
        }

        [Test]
        public void ThePaidRecord_TravelsWithTheGuestSave_IsKeptByTheRoom_AndComesBackOnRejoin()
        {
            // 손님 길: 돈을 낸 표시(room-house-build-paid-<id>.json)는 손님 저장과 함께 방으로 가고, 방은 손님 폴더에 그대로 두었다가 다시 들어오면 내려 준다.
            // 방(GuestSaveFolder)은 파일 이름 모양(.json, 경로 한 칸)만 보고 모양은 모른다 — 꾸밈 조각을 모르는 옛 방도 같은 코드로 이 파일을 받고 돌려준다.
            using var rig = NewRoom(new[] { RoomHouse.PieceName });
            var a = Join(rig, "a");
            var floor = PaidFloors().First();
            Give(a, PerCell(floor) * Changed(a, floor));
            string name = SaveFileNames.RoomHouseBuildPaid("a");
            var guests = new FileSaveStore(GuestSaveFolder.FolderFor(Saves, RoomId));
            a.Game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            rig.Run(1.5);
            rig.Run(0.5);
            Assert.That(a.Game.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "전제: 꾸밈이 끝났다");
            Assert.That(guests.TryRead<RoomHouseBuildPaidSaveData>("a", name, out var paid), Is.True, "돈을 낸 저장과 함께 방의 손님 폴더에 왔다");
            Assert.That(paid.Holds.Length, Is.EqualTo(1));

            a.Game.VisitLocation("location.town-square"); // 다음 저장 — 끝난 잡기의 표시를 지운다
            rig.Run(1.0);
            Assert.That(guests.TryRead<RoomHouseBuildPaidSaveData>("a", name, out var after), Is.True);
            Assert.That(after.Holds, Is.Empty);

            rig.Quit(a.Client);
            rig.Run(0.5);
            var a2 = Join(rig, "a");
            Assert.That(a2.Link.Files.Writes.Any(p => p.Key == name), Is.True, "다시 들어오면 방이 그 파일도 내려 준다");
        }

        [Test]
        public void AGuestDropsBetweenGrantAndPay_ComesBack_FinishesOnce()
        {
            using var rig = NewRoom(new[] { RoomHouse.PieceName });
            var a = Join(rig, "a");
            var floor = PaidFloors().First();
            int cost = PerCell(floor) * Changed(a, floor);
            Give(a, cost + 100);
            a.Game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            RunUntilHeld(rig); // 방이 칸을 잡았고 a의 쓰기는 아직 닿지 않았다
            rig.Quit(a.Client); // 끊김

            var a2 = Join(rig, "a");
            rig.Run(1.5);
            rig.Run(0.5);

            Assert.That(BuildOf(rig).HeldCount, Is.Zero, "잡기가 끝났다");
            Assert.That(a2.Game.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            rig.Run(1.0);
            Assert.That(BuildOf(rig).HeldCount, Is.Zero);
            Assert.That(a2.Game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange), "다시 돌려도 바뀔 것이 없다");
            Assert.That(Money(a2), Is.EqualTo(100), "값은 정확히 한 번만 낸다");
        }

        [Test]
        public void PaidThenTheLineBroke_ADetourToAnotherRoomLeavesItAlone_BackInTheFirstRoomTheFinishGoesOnceAndIsPaidOnce()
        {
            // Host RoomConnection이 하는 일: 못 보낸 저장은 방마다 따로(GuestUploadsByRoom). 돈을 낸 저장(돈 낸 표시 + 쓰기 요청)이 방 A에 닿기 전에 끊기고
            // 같은 사람이 방 B에 들렀다 나와도 B의 손님 세이브는 A의 것과 섞이지 않고, A로 돌아오면 표시가 살아 있어 쓰기가 다시 가고 값은 한 번만 낸다.
            var book = new GuestUploadsByRoom();
            using var roomA = NewRoom(new[] { RoomHouse.PieceName });
            using var roomB = NewRoom(new[] { RoomHouse.PieceName }, saves: Path.Combine(_root, "saves-b"));
            string paidName = SaveFileNames.RoomHouseBuildPaid("a");

            var a = Join(roomA, "a", book.For("A", "a"));
            var floor = PaidFloors().First();
            int cost = PerCell(floor) * Changed(a, floor);
            Give(a, cost + 100);
            a.Game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            for (int i = 0; i < 80 && Money(a) > 100; i++) roomA.Run(roomA.Settings.TickSeconds);
            Assert.That(Money(a), Is.EqualTo(100), "준비: 방이 칸을 잡았고 a가 값을 냈다");
            roomA.Sever(a.Client); // 값을 낸 저장은 A에 닿지 않는다
            roomA.Run(0.5);
            Assert.That(book.For("A", "a").Merged().Writes.Any(p => p.Key == paidName), Is.True, "전제: 돈 낸 표시가 못 보낸 저장에 있다");
            Assert.That(BuildOf(roomA).HeldCount, Is.EqualTo(1), "전제: A에서 그 잡기는 아직 쓰이지 않았다");
            roomA.Quit(a.Client);

            var inB = Join(roomB, "a", book.For("B", "a"));
            Assert.That(inB.Link.Files.Writes.Select(p => p.Key), Has.No.Member(paidName), "B가 내려 준 세이브에 A의 돈 낸 표시가 없다");
            Assert.That(book.For("B", "a").Count, Is.Zero, "B에 보낼 A의 저장이 없다");
            Assert.That(BuildOf(roomB).HeldCount, Is.Zero);
            roomB.Run(1.0);
            Assert.That(inB.Game.HouseFloorAt(TopLeftRoom), Is.Not.EqualTo(floor.Id), "B의 집은 A의 꾸밈과 무관하다");
            roomB.Quit(inB.Client);
            roomB.Run(0.5);
            Assert.That(book.For("A", "a").Count, Is.GreaterThan(0), "A의 못 보낸 저장은 B에 들르는 동안 그대로 남는다");

            var back = Join(roomA, "a", book.For("A", "a"));
            roomA.Run(1.5);
            roomA.Run(0.5);
            Assert.That(back.Game.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "돌아오면 표시가 살아 있어 쓰기가 다시 간다");
            Assert.That(BuildOf(roomA).HeldCount, Is.Zero, "잡기가 끝났다");
            Assert.That(Money(back), Is.EqualTo(100), "값은 정확히 한 번만 낸다");
            Assert.That(book.For("A", "a").Count, Is.Zero, "A가 받았다");
        }

        [Test]
        public void TheServersHouseFrame_IsTheSameAsThePlayersFrame()
        {
            string repo = ServerProcessTests.RepoRoot();
            var frames = HouseGrids.LoadFrames(repo, null);
            Assert.That(frames.Keys, Does.Contain(Surface), "짓는 집 맵마다 틀을 만든다");
            var server = frames[Surface];
            var player = HouseBuildFrames.From(WorldMapLoader.FromRepo(repo).LoadMap(HouseMap));

            Assert.That(server.FrameId, Is.EqualTo(player.FrameId));
            Assert.That(server.DefaultWall, Is.EqualTo(player.DefaultWall));
            Assert.That(server.DefaultFloor, Is.EqualTo(player.DefaultFloor));
            Assert.That(server.DefaultBand, Is.EqualTo(player.DefaultBand));
            Assert.That(server.Tiles.Cells.Count, Is.EqualTo(player.Tiles.Cells.Count), "출처표 칸 수");
            for (int i = 0; i < player.Tiles.Cells.Count; i++)
            {
                var s = server.Tiles.Cells[i];
                var p = player.Tiles.Cells[i];
                Assert.That(s.Cell, Is.EqualTo(p.Cell), "칸 " + i);
                Assert.That(s.Kind, Is.EqualTo(p.Kind), "칸 " + p.Cell);
                Assert.That(s.Parts.Select(x => x.Source + "|" + x.Shade + "|" + x.Piece + "|" + x.Pixels), Is.EqualTo(p.Parts.Select(x => x.Source + "|" + x.Shade + "|" + x.Piece + "|" + x.Pixels)), "부분 " + p.Cell);
            }

            Assert.That(RoomFill.Build(server.Tiles.Grid).RoomCount, Is.EqualTo(RoomFill.Build(player.Tiles.Grid).RoomCount), "방 채우기의 방 수");
            Assert.That(HouseGrids.LoadFrames(repo, null)[Surface], Is.Not.SameAs(server), "부를 때마다 새로 읽는다(기억은 규칙 쪽 몫)");
        }

        [Test]
        public void WithoutTheMaps_TheRoomRefusesToDecorate_AndSaysWhy()
        {
            var log = new List<string>();
            var rules = HouseGrids.Rules(_definitions, Path.Combine(_root, "no-repo"), log.Add);
            var room = new RoomPieceSet(new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault(rules));
            var floor = PaidFloors().First();
            var answers = new List<RoomAnswer>();
            room.Apply("a", new[]
            {
                new RoomRequestSaveData
                {
                    Piece = RoomHouseBuild.PieceName, RequestId = "r", Kind = RoomHouseBuild.Finish, Amount = 1,
                    Ids = new[] { Surface, floor.Id, HouseFinishTools.Brush, TopLeftRoom.X.ToString(), TopLeftRoom.Y.ToString() },
                },
            }, 1, answers, null);
            Assert.That(answers.Count, Is.EqualTo(1));
            Assert.That(answers[0].Accepted, Is.False);
            Assert.That(log.Any(l => l.Contains("집 맵을 읽지 못했다") && l.Contains("꾸밀 수 없다")), Is.True, string.Join("\n", log));
        }

        [Test]
        public void AGuestWithADifferentHouseMap_IsRefusedAtTheDoor()
        {
            string repo = ServerProcessTests.RepoRoot();
            string serverPrint = Rootborn.Data.DataFingerprint.Compute(Path.Combine(repo, "data"));
            string guestRepo = CopyRuleData(repo);
            string guestLdtk = Path.Combine(guestRepo, "art", "world", "rooms", "house", "house.ldtk");
            File.AppendAllText(guestLdtk, " "); // 한 바이트
            string guestPrint = Rootborn.Data.DataFingerprint.Compute(Path.Combine(guestRepo, "data"));
            Assert.That(guestPrint, Is.Not.EqualTo(serverPrint), "전제: 집 LDtk가 다르면 지문이 다르다");

            using var rig = new RoomRig(new FileSaveStore(Saves), RoomId, versions: new RoomVersions(RoomProtocol.Version, "game-1", serverPrint));
            var stranger = rig.Join("x", new RoomVersions(RoomProtocol.Version, "game-1", guestPrint));
            rig.Run(0.5);
            Assert.That(stranger.Status, Is.EqualTo(ClientStatus.Refused));
            Assert.That(stranger.FarewellReason, Is.EqualTo(RoomRefusal.DataVersionMismatch));
            Assert.That(rig.Log.Any(l => l.Contains(RoomRefusal.DataVersionMismatch)), Is.True, "거절 이유를 로그에 남긴다");

            var same = rig.Join("y", new RoomVersions(RoomProtocol.Version, "game-1", serverPrint));
            rig.Run(0.5);
            Assert.That(same.Status, Is.EqualTo(ClientStatus.Joined), "같은 판은 들어온다");
        }

        // 지문이 읽는 파일(data/, game/*.json, game/world/*.json, 집 LDtk)만 임시 폴더에 같은 모양으로 복사한다.
        private string CopyRuleData(string repo)
        {
            string target = Path.Combine(_root, "guest-repo");
            foreach (string path in Directory.GetFiles(Path.Combine(repo, "data"), "*.json", SearchOption.AllDirectories))
                CopyTo(repo, target, path);
            foreach (string name in new[] { "game/economy.json", "game/time.json", "game/world/maps.json", "game/world/interactions.json", "art/world/rooms/house/house.ldtk" })
                CopyTo(repo, target, Path.Combine(repo, name.Replace('/', Path.DirectorySeparatorChar)));
            return target;
        }

        private static void CopyTo(string repo, string target, string path)
        {
            if (!File.Exists(path)) return;
            string to = Path.Combine(target, Path.GetRelativePath(repo, path));
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(path, to, true);
        }
    }
}
