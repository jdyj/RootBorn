using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 방 하나(RoomServer + 손님 세이브 폴더 GuestSaveFolder, 실제 data/) + 게임 한 판 둘(GameSession, 실제 data/)을 같은 프로세스 안 전송으로 잇는다.
    /// 각 판은 방이 내려 준 세이브(MemorySaveSlot)로 돌고, 저장할 때마다 방으로 보낸다(RoomGuestLink). 방의 시계는 RemoteRoomTime,
    /// 공유 조각은 방이 보낸 RoomShares를 따른다. Host가 할 일(잇기, SyncWithRoom)을 테스트가 한다.
    /// GameSession 명령과 전송만 쓰는 자동 테스트다(화면·입력 근거 아님). 마지막 테스트 하나는 같은 기계의 UDP 루프백이다.
    /// </summary>
    public sealed class SharedPiecesRoomTests
    {
        private const string MoneyOnlyWork = "work.clinic-helper"; // 보상: 용돈 5
        private const string LibraryStudy = "outside.self-study.library";
        private const string Shop = "location.corner-store";
        private const string TownFlag = "world.library.archive-open";
        private static GameDefinitions _definitions;
        private static GameStartSettings _settings;
        private string _root;

        [OneTimeSetUp]
        public void LoadData()
        {
            var load = GameDataLoader.Load(Path.Combine(ServerProcessTests.RepoRoot(), "data"), RootbornBuilders.Register);
            load.ThrowIfFailed();
            _definitions = load.Definitions;
            _settings = GameStartSettings.FromDefinitions(_definitions);
        }

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-shared-pieces", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        // ---------- 틀 ----------

        private sealed class Guest
        {
            public string PlayerId;
            public SessionClient Client;
            public RoomGuestLink Link;
            public MemorySaveSlot Store;
            public GameSession Game;
        }

        private FileSaveStore RoomStore => new FileSaveStore(Path.Combine(_root, "saves"));

        private RoomRig NewRoom(params string[] shared)
        {
            var settings = new RoomServerSettings { MaxPlayers = 4, SharedPieces = shared };
            var guests = new GuestSaveFolder(GuestSaveFolder.FolderFor(Path.Combine(_root, "saves"), "room-a"), _definitions, _settings);
            return new RoomRig(RoomStore, settings: settings, calendar: Program.RoomCalendar(_definitions), guests: guests);
        }

        private static Guest Join(RoomRig rig, string playerId, GuestUploads uploads = null)
        {
            var guest = new Guest { PlayerId = playerId, Client = rig.Join(playerId) };
            guest.Link = new RoomGuestLink(guest.Client, new SessionClientSettings().MaxMessageBytes, uploads);
            rig.Run(0.5);
            Assert.That(guest.Link.Ready, Is.True, playerId + ": 방 설정과 세이브를 받았다");
            guest.Store = new MemorySaveSlot(RoomGuestLink.Slot, guest.Link.Files.Writes);
            guest.Store.Committed += c => guest.Link.Upload(SaveFileSet.From(c.Writes, c.Deletes));
            var time = new RemoteRoomTime(guest.Client, playerId, _definitions.Get<LifeCalendar>(_settings.CalendarId));
            guest.Game = GameSession.Start(_definitions, _settings, guest.Store, RoomGuestLink.Slot, playerId, room: time, shares: guest.Link.Shares);
            time.Changed += () => guest.Game.SyncWithRoom();
            rig.Run(0.3);
            return guest;
        }

        private static void Shift(Guest guest, string request)
        {
            Assert.That(guest.Game.BeginPartTimeWork(MoneyOnlyWork, request, 1).Applied, Is.True, request);
            Assert.That(guest.Game.SettleMinigame(request, true).Applied, Is.True, request);
        }

        private string GuestFile(string playerId, string file) => Path.Combine(_root, "saves", "room-a", GuestSaveFolder.FolderName, playerId, file);

        private static int RoomBalance(RoomRig rig) => rig.Room.Pieces.TryGet(RoomWallet.PieceName, out var w) ? ((RoomWallet)w).Balance : -1;

        /// <summary>준비: 사람들이 들어오기 전에 방 지갑에 돈을 넣는다(일을 여러 번 하는 대신).</summary>
        private static void Seed(RoomRig rig, int amount)
            => rig.Room.Pieces.Apply("setup", new[] { new RoomRequestSaveData { Piece = RoomWallet.PieceName, RequestId = "seed", Kind = RoomWallet.Deposit, Amount = amount } },
                1, null, null);

        // ---------- 방 설정 ----------

        [Test]
        public void TheRoomSettings_AreWrittenWhenTheRoomIsMade_SentToJoiners_AndNotChangedLater()
        {
            using (var rig = NewRoom(RoomWallet.PieceName))
            {
                Assert.That(rig.Room.IsNewRoom, Is.True);
                var a = Join(rig, "a");
                Assert.That(a.Link.Settings.SharedPieces, Is.EqualTo(new[] { RoomWallet.PieceName }));
                Assert.That(a.Game.Wallet, Is.InstanceOf<RoomWalletClient>());
                rig.Room.Shutdown();
            }

            using var again = NewRoom(RoomWorldFlags.PieceName);
            Assert.That(again.Room.ShareSettings.SharedPieces, Is.EqualTo(new[] { RoomWallet.PieceName }), "방을 만든 뒤에는 못 바꾼다");
            Assert.That(again.Log.Any(l => l.Contains("방 설정은 방을 만들 때만")), Is.True);
        }

        // ---------- 돈 ----------

        [Test]
        public void MoneyShareOn_WhatOneEarns_IsInEveryonesBalance()
        {
            using var rig = NewRoom(RoomWallet.PieceName);
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            Shift(a, "a:w1");
            rig.Run(0.5);
            Assert.That(RoomBalance(rig), Is.EqualTo(5));
            Assert.That(a.Game.Wallet.Balance, Is.EqualTo(5));
            Assert.That(b.Game.Wallet.Balance, Is.EqualTo(5), "b의 잔액에도 보인다");
        }

        [Test]
        public void MoneyShareOff_EachKeepsTheirOwn()
        {
            using var rig = NewRoom();
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            Shift(a, "a:w1");
            rig.Run(0.5);
            Assert.That(a.Game.Wallet.Balance, Is.EqualTo(5));
            Assert.That(b.Game.Wallet.Balance, Is.Zero);
            Assert.That(rig.Room.Pieces.Pieces, Is.Empty);
        }

        [Test]
        public void MoneyShareOn_TwoSpendAtOnce_OnlyTheFirstRequestBuys_AndTheBalanceNeverGoesNegative()
        {
            using var rig = NewRoom(RoomWallet.PieceName);
            var offer = GameSession.Start(_definitions, _settings, new MemorySaveSlot("x"), "x", "x").FurnitureForSaleAt(Shop)[0];
            Seed(rig, offer.Price + offer.Price / 2);
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            Assert.That(a.Game.Wallet.Balance, Is.EqualTo(offer.Price + offer.Price / 2));

            a.Game.PurchaseFurniture(offer.StableId);
            b.Game.PurchaseFurniture(offer.StableId);
            Assert.That(a.Game.WaitingForRoom && b.Game.WaitingForRoom, Is.True, "둘 다 방의 허락을 기다린다");
            rig.Run(1.0);

            bool aBought = a.Game.FurnitureUnlocks.IsUnlocked(offer.UnlockToken);
            bool bBought = b.Game.FurnitureUnlocks.IsUnlocked(offer.UnlockToken);
            Assert.That(aBought ^ bBought, Is.True, "한 사람만 산다");
            Assert.That(aBought, Is.True, "먼저 도착한 요청(a)이 이긴다");
            Assert.That(b.Game.LastRoomRefusal, Is.True);
            Assert.That(RoomBalance(rig), Is.EqualTo(offer.Price / 2));
            Assert.That(a.Game.Wallet.Balance, Is.EqualTo(offer.Price / 2));
            Assert.That(b.Game.Wallet.Balance, Is.EqualTo(offer.Price / 2));
            Assert.That(a.Game.WaitingForRoom || b.Game.WaitingForRoom, Is.False);
        }

        [Test]
        public void ItemsAreNeverShared_InAnySetting()
        {
            foreach (var shared in new[] { new string[0], new[] { RoomWallet.PieceName }, new[] { RoomWallet.PieceName, RoomWorldFlags.PieceName } })
            {
                using var rig = NewRoom(shared);
                var a = Join(rig, "a");
                var b = Join(rig, "b");
                Assert.That(a.Game.BeginPartTimeWork("work.shift.corner-store", "a:wood", 1).Applied, Is.True); // 나무 + 용돈
                Assert.That(a.Game.SettleMinigame("a:wood", true).Applied, Is.True);
                rig.Run(0.5);
                Assert.That(a.Game.Inventory.CountOf(a.Game.Item("Wood")), Is.EqualTo(3));
                Assert.That(b.Game.Inventory.CountOf(b.Game.Item("Wood")), Is.Zero, "가방은 어떤 설정에서도 각자: " + string.Join(",", shared));
                rig.Room.Shutdown();
                Directory.Delete(Path.Combine(_root, "saves"), true);
            }
        }

        // ---------- 마을 상태 ----------

        [Test]
        public void TownShareOn_AFlagOneTurnsOn_IsOnForTheOther()
        {
            using var rig = NewRoom(RoomWorldFlags.PieceName);
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            a.Game.World.TryActivate(_definitions.Get<WorldStateFlag>(TownFlag), new WorldStateActivationSource("", "", "test", 1)); // 준비: 깃발을 켜는 규칙 대신
            a.Game.VisitLocation("location.town-square");
            rig.Run(0.5);
            Assert.That(b.Game.World.IsActive(TownFlag), Is.True);
        }

        // ---------- 손님 저장 ----------

        [Test]
        public void TheGuestsProgress_IsSavedOnTheServer_AndComesBackOnRejoin()
        {
            using var rig = NewRoom();
            var a = Join(rig, "a");
            Shift(a, "a:w1");
            rig.Run(0.5);
            Assert.That(File.Exists(GuestFile("a", SaveFileNames.AdultCareerSnapshot("a"))), Is.True, "진행은 서버의 세이브에 남는다");

            rig.Quit(a.Client);
            rig.Run(0.5);
            var again = Join(rig, "a");
            Assert.That(again.Game.Wallet.Balance, Is.EqualTo(5), "서버가 내려 준 세이브로 이어진다");
            Assert.That(again.Game.Student.TimePointsSpentToday, Is.EqualTo(a.Game.Student.TimePointsSpentToday));
        }

        [Test]
        public void ANewcomer_StartsANewGame()
        {
            using var rig = NewRoom();
            var a = Join(rig, "a");
            Assert.That(a.Link.Files.IsEmpty, Is.True, "처음 온 사람의 세이브는 비어 있다");
            Assert.That(a.Game.Student.CurrentDay, Is.EqualTo(1));
            Assert.That(a.Game.Wallet.Balance, Is.Zero);
        }

        [Test]
        public void ADamagedSave_IsRefused_AndTheOldSaveIsKept()
        {
            using var rig = NewRoom();
            var a = Join(rig, "a");
            Shift(a, "a:w1");
            rig.Run(0.5);
            var snapshot = GuestFile("a", SaveFileNames.AdultCareerSnapshot("a"));
            string before = File.ReadAllText(snapshot);

            var broken = SaveFileSet.From(new[] { new KeyValuePair<string, string>(SaveFileNames.AdultCareerSnapshot("a"), "{ broken") }, null);
            Assert.That(a.Link.Upload(broken), Is.True);
            rig.Run(0.5);
            Assert.That(a.Link.LastRefusal, Is.EqualTo(GuestSaveRefusal.Damaged));
            Assert.That(File.ReadAllText(snapshot), Is.EqualTo(before), "옛 세이브를 지킨다");

            var tooNew = SaveFileSet.From(new[] { new KeyValuePair<string, string>(SaveFileNames.AdultCareerSnapshot("a"), "{ \"Version\": 999 }") }, null);
            a.Link.Upload(tooNew);
            rig.Run(0.5);
            Assert.That(a.Link.LastRefusal, Is.EqualTo(GuestSaveRefusal.Damaged), "더 새 형식 버전");

            var badName = SaveFileSet.From(new[] { new KeyValuePair<string, string>("../escape.json", "{}") }, null);
            a.Link.Upload(badName);
            rig.Run(0.5);
            Assert.That(a.Link.LastRefusal, Is.EqualTo(GuestSaveRefusal.BadFileName));
            Assert.That(File.ReadAllText(snapshot), Is.EqualTo(before));
        }

        [Test]
        public void AnotherPlayersProgress_IsRefused()
        {
            using var rig = NewRoom();
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            Shift(b, "b:w1");
            rig.Run(0.5);
            string bSnapshot = File.ReadAllText(GuestFile("b", SaveFileNames.AdultCareerSnapshot("b")));

            a.Link.Upload(SaveFileSet.From(new[] { new KeyValuePair<string, string>(SaveFileNames.AdultCareerSnapshot("a"), bSnapshot) }, null));
            rig.Run(0.5);
            Assert.That(a.Link.LastRefusal, Is.EqualTo(GuestSaveRefusal.Damaged), "b의 진행을 a의 것으로 올릴 수 없다");
        }

        [Test]
        public void WhenTheDayChanges_TheServerKeepsACopyOfTheSlot()
        {
            using var rig = NewRoom();
            var a = Join(rig, "a");
            Assert.That(a.Game.EndDay().Ended, Is.True);
            rig.Run(0.5);
            Assert.That(rig.Room.Clock.Day, Is.EqualTo(2));
            Assert.That(File.Exists(Path.Combine(_root, "saves", "room-a", FileSaveStore.DayCopiesFolder, "2", SaveFileNames.RoomClock)), Is.True, "방의 파일 사본");
            Assert.That(File.Exists(GuestFile("a", Path.Combine(FileSaveStore.DayCopiesFolder, "2", SaveFileNames.AdultCareerSnapshot("a")))), Is.True, "손님 세이브 사본");
        }

        // ---------- 방에 없던 날 ----------

        [Test]
        public void ComingBackDaysLater_StartsOnTheRoomsDay_WithTheDaysAwayAsRestDays()
        {
            using var rig = NewRoom();
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            Shift(a, "a:w1");
            rig.Run(0.5);
            rig.Quit(a.Client);
            rig.Run(0.5);

            for (int day = 1; day <= 2; day++)
            {
                Assert.That(b.Game.EndDay().Ended, Is.True);
                rig.Run(0.5);
                Assert.That(b.Game.StartNextDay(), Is.True, "a가 없으니 b만으로 넘어간다");
                rig.Run(0.5);
            }

            Assert.That(rig.Room.Clock.Day, Is.EqualTo(3));
            var back = Join(rig, "a");
            Assert.That(back.Game.Student.CurrentDay, Is.EqualTo(3), "방의 날로 맞춘다");
            Assert.That(back.Game.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
            Assert.That(back.Game.Wallet.Balance, Is.EqualTo(5), "쉰 날에 잃는 것이 없다");
            Assert.That(back.Game.PerformOutsideSchoolActivity(LibraryStudy, "a:day3").Kind, Is.EqualTo(LifeActivityResultKind.Applied), "그날 바로 할 수 있다");
        }

        // ---------- 끊긴 동안 보낸 요청 ----------

        [Test]
        public void ARequestSentJustBeforeTheLineBroke_IsSentAgain_AndCountedOnce()
        {
            using var rig = NewRoom(RoomWallet.PieceName);
            var a = Join(rig, "a");
            var b = Join(rig, "b");
            var uploads = a.Link.Uploads;
            Shift(a, "a:w1"); // 시간 포인트를 쓰고 돈을 벌었다 — 저장과 방의 시계 요청이 나간다
            rig.Sever(a.Client);
            rig.Run(11.0); // 방은 시간 초과로 a를 내보낸다
            Assert.That(rig.Room.Clock.CompletedParts("a"), Is.Zero, "방은 a의 요청을 받지 못했다");
            Assert.That(RoomBalance(rig), Is.Zero);
            Assert.That(uploads.Count, Is.GreaterThan(0), "답을 받지 못한 저장은 들고 있다");

            rig.Quit(a.Client);
            var back = Join(rig, "a", uploads);
            rig.Run(0.5);
            Assert.That(back.Game.Wallet.Balance, Is.EqualTo(5), "들고 있던 저장을 겹쳐 시작한다");
            Assert.That(rig.Room.Clock.CompletedParts("a"), Is.EqualTo(back.Game.Student.TimePointsSpentToday), "다시 보내 방이 센다");
            Assert.That(RoomBalance(rig), Is.EqualTo(5), "넣기도 다시 가서 한 번만 반영된다");
            Assert.That(uploads.Count, Is.Zero);

            back.Game.SyncWithRoom();
            back.Game.VisitLocation("location.town-square"); // 다시 저장해도
            rig.Run(0.5);
            Assert.That(RoomBalance(rig), Is.EqualTo(5), "두 번 넣지 않는다");
            Assert.That(b.Game.Wallet.Balance, Is.EqualTo(5));
        }

        // ---------- 서버를 껐다 켬 ----------

        [Test]
        public void AfterRestartingTheServer_BothProgressesAndTheRoomsWalletContinue()
        {
            using (var rig = NewRoom(RoomWallet.PieceName))
            {
                var a = Join(rig, "a");
                var b = Join(rig, "b");
                Shift(a, "a:w1");
                Shift(b, "b:w1");
                rig.Run(0.5);
                Assert.That(RoomBalance(rig), Is.EqualTo(10));
                rig.Room.Shutdown();
            }

            using var again = NewRoom(RoomWallet.PieceName);
            Assert.That(again.Room.IsNewRoom, Is.False);
            Assert.That(RoomBalance(again), Is.EqualTo(10), "방 지갑이 이어진다");
            var a2 = Join(again, "a");
            var b2 = Join(again, "b");
            Assert.That(a2.Game.Wallet.Balance, Is.EqualTo(10));
            Assert.That(a2.Game.Student.TimePointsSpentToday, Is.EqualTo(1), "a의 진행이 이어진다");
            Assert.That(b2.Game.Student.TimePointsSpentToday, Is.EqualTo(1), "b의 진행이 이어진다");
            Assert.That(again.Room.Clock.IsDayOver || again.Room.Clock.PartIndex == 1, Is.True, "방의 시계도 이어진다");
        }

        // ---------- UDP ----------

        [Test, NonParallelizable]
        public void OverUdpLoopback_AGuestSavesOnTheServer_AndSharesMoney()
        {
            var saves = Path.Combine(_root, "udp");
            using var transport = UdpTransport.Listen(IPAddress.Loopback, 0, RoomProtocol.ConnectionKey);
            var guests = new GuestSaveFolder(GuestSaveFolder.FolderFor(saves, "room-u"), _definitions, _settings);
            using var room = new RoomServer(transport, new RoomServerSettings { SharedPieces = new[] { RoomWallet.PieceName } }, RoomRig.Versions,
                new FileSaveStore(saves), "room-u", null, Program.RoomCalendar(_definitions), guests);
            var client = RoomClient.Create(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), transport.LocalPort, RoomProtocol.ConnectionKey),
                RoomRig.Versions, "u", "child|", new SessionClientSettings());
            var link = new RoomGuestLink(client, new SessionClientSettings().MaxMessageBytes);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            bool Pump(Func<bool> until)
            {
                double end = watch.Elapsed.TotalSeconds + 5;
                while (watch.Elapsed.TotalSeconds < end)
                {
                    room.Tick(watch.Elapsed.TotalSeconds);
                    client.Update(watch.Elapsed.TotalSeconds);
                    if (until()) return true;
                    Thread.Sleep(5);
                }

                return false;
            }

            try
            {
                Assert.That(Pump(() => link.Ready), Is.True);
                var store = new MemorySaveSlot(RoomGuestLink.Slot, link.Files.Writes);
                store.Committed += c => link.Upload(SaveFileSet.From(c.Writes, c.Deletes));
                var time = new RemoteRoomTime(client, "u", _definitions.Get<LifeCalendar>(_settings.CalendarId));
                var game = GameSession.Start(_definitions, _settings, store, RoomGuestLink.Slot, "u", room: time, shares: link.Shares);
                Assert.That(Pump(() => time.Known), Is.True);
                Assert.That(game.BeginPartTimeWork(MoneyOnlyWork, "u:w1", 1).Applied, Is.True);
                Assert.That(game.SettleMinigame("u:w1", true).Applied, Is.True);
                Assert.That(Pump(() => link.Uploads.Count == 0 && RoomBalance(room) == 5), Is.True, "UDP로 저장이 가고 방 지갑에 들어간다");
                Assert.That(File.Exists(Path.Combine(GuestSaveFolder.FolderFor(saves, "room-u"), "u", SaveFileNames.AdultCareerSnapshot("u"))), Is.True);
            }
            finally
            {
                client.Dispose();
            }
        }

        private static int RoomBalance(RoomServer room) => room.Pieces.TryGet(RoomWallet.PieceName, out var w) ? ((RoomWallet)w).Balance : -1;
    }
}
