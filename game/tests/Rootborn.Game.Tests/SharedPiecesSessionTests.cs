using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Core.WorldState;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 게임 한 판이 공유 조각의 "주는 쪽"(RoomShares)을 받는다(멀티플레이 설계 4절). 방 쪽은 Core RoomPieceSet을 테스트가 손으로 돌린다
    /// (보낼 요청을 방에 넘기고 답과 상태를 돌려준다 — 서버가 할 일). GameSession 명령으로 진행하는 자동 테스트다(화면·전송 근거 아님).
    /// 전송을 거친 흐름은 Rootborn.Server.Tests의 SharedPiecesRoomTests.
    /// </summary>
    public sealed class SharedPiecesSessionTests
    {
        private const string MoneyOnlyWork = "work.clinic-helper"; // 보상: 용돈 5
        private const string Shop = "location.corner-store";
        private const string TownFlag = "world.library.archive-open"; // 범위 Shared
        private const string PersonalFlag = "world.chef.level2";       // 범위 Personal

        private static void Shift(GameSession game, string request)
        {
            Assert.That(game.BeginPartTimeWork(MoneyOnlyWork, request, 1).Applied, Is.True, request);
            Assert.That(game.SettleMinigame(request, true).Applied, Is.True, request);
        }

        /// <summary>방 하나(조각 + 장부)와 그 방에 들어간 플레이어들.</summary>
        private sealed class FakeRoom
        {
            private readonly List<(string Player, RoomShares Shares)> _players = new List<(string, RoomShares)>();
            private int _ids;

            public FakeRoom(params string[] shared) => Pieces = new RoomPieceSet(new RoomShareSettings(shared), RoomPieceCatalog.CreateDefault());

            public RoomPieceSet Pieces { get; }
            public int Day { get; set; } = 1;

            public RoomShares Join(string playerId)
            {
                var shares = new RoomShares(Pieces.Settings, RoomPieceCatalog.CreateDefault(), () => playerId + "-r" + (++_ids));
                foreach (var piece in Pieces.Pieces) shares.Receive(piece.State());
                _players.Add((playerId, shares));
                return shares;
            }

            public void Leave(RoomShares shares) => _players.RemoveAll(p => p.Shares == shares);

            /// <summary>그 플레이어의 보낼 요청을 방이 받아 반영하고(저장을 받은 것과 같다), 답을 그에게, 바뀐 조각의 상태를 모두에게 보낸다.</summary>
            public void Deliver(RoomShares from)
            {
                var (player, _) = _players.First(p => p.Shares == from);
                var answers = new List<RoomAnswer>();
                var changed = new List<IRoomPiece>();
                Pieces.Apply(player, from.Outbox.Requests.Select(r => r.Copy()).ToList(), Day, answers, changed);
                foreach (var a in answers) from.Receive(a);
                foreach (var piece in changed)
                    foreach (var p in _players) p.Shares.Receive(piece.State());
            }

            public int Balance => Pieces.TryGet(RoomWallet.PieceName, out var w) ? ((RoomWallet)w).Balance : -1;
        }

        private static GameSession Start(TempSaves saves, RoomShares shares, string slot = TestWorld.Slot, string player = TestWorld.Player, IRoomTime room = null)
            => GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, slot, player, room: room, shares: shares);

        // ---------- 돈 ----------

        [Test]
        public void MoneyShareOff_TheWalletIsPersonal_AsBefore()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomWorldFlags.PieceName);
            var game = Start(saves, room.Join("a"));
            Shift(game, "w1");
            Assert.That(game.Wallet, Is.Not.InstanceOf<RoomWalletClient>());
            Assert.That(game.Wallet.Balance, Is.EqualTo(5));
            Assert.That(game.Shares.Outbox.Requests.Where(r => r.Piece == RoomWallet.PieceName), Is.Empty);
        }

        [Test]
        public void MoneyShareOn_WhatOneEarns_ShowsInEveryonesBalance()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomWallet.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");

            Shift(a, "a:w1");
            Assert.That(a.Wallet.Balance, Is.EqualTo(5), "번 사람에게는 바로 보인다");
            Assert.That(saves.Store.TryRead<RoomOutboxSaveData>("slot-a", SaveFileNames.RoomOutbox("a"), out var outbox), Is.True);
            Assert.That(outbox.Requests.Any(r => r.Kind == RoomWallet.Deposit && r.Amount == 5), Is.True, "넣기 요청이 저장과 함께 간다");

            room.Deliver(aShares);
            Assert.That(room.Balance, Is.EqualTo(5));
            Assert.That(a.Wallet.Balance, Is.EqualTo(5), "답과 새 잔액이 와도 두 번 세지 않는다");
            Assert.That(b.Wallet.Balance, Is.EqualTo(5), "다른 사람의 잔액에도 보인다");
            Assert.That(b.Inventory.CountOf(b.Definitions.Currency), Is.EqualTo(5));
        }

        [Test]
        public void MoneyShareOn_BuyingWaitsForTheRoom_ThenCompletes()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomWallet.PieceName);
            var shares = room.Join("a");
            var game = Start(saves, shares, "slot-a", "a");
            var offer = game.FurnitureForSaleAt(Shop)[0];
            for (int i = 0; game.Wallet.Balance < offer.Price; i++) Shift(game, "w" + i);
            room.Deliver(shares);
            int before = room.Balance;

            game.PurchaseFurniture(offer.StableId);
            Assert.That(game.WaitingForRoom, Is.True, "방의 허락을 기다린다");
            Assert.That(game.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.False, "허락 전에는 아무것도 바뀌지 않는다");

            Assert.That(game.LastRoomCommandApplied, Is.Null, "기다리는 동안은 결과가 없다");
            room.Deliver(shares);
            Assert.That(game.WaitingForRoom, Is.False);
            Assert.That(game.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.True, "허락이 오면 같은 명령이 끝난다");
            Assert.That(game.LastRoomCommandApplied, Is.True, "화면은 기다리던 명령이 됐는지 안다");
            Assert.That(game.LastRoomRefusal, Is.False);
            Assert.That(room.Balance, Is.EqualTo(before - offer.Price));
            Assert.That(game.Wallet.Balance, Is.EqualTo(before - offer.Price));

            room.Deliver(shares);
            Assert.That(room.Balance, Is.EqualTo(before - offer.Price), "쓰기 요청을 다시 받아도 돈은 한 번만 나간다");
            Assert.That(game.Shares.Outbox.Requests, Is.Empty);
        }

        [Test]
        public void MoneyShareOn_TwoBuyAtOnce_OnlyTheFirstArrivingRequestBuys()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomWallet.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            var offer = a.FurnitureForSaleAt(Shop)[0];
            for (int i = 0; a.Wallet.Balance < offer.Price; i++) Shift(a, "w" + i);
            room.Deliver(aShares);
            Assume.That(room.Balance, Is.LessThan(offer.Price * 2), "둘 다 살 돈은 없다");

            a.PurchaseFurniture(offer.StableId);
            b.PurchaseFurniture(offer.StableId);
            Assert.That(a.WaitingForRoom && b.WaitingForRoom, Is.True);

            room.Deliver(bShares); // b의 요청이 먼저 닿았다
            room.Deliver(aShares);
            Assert.That(b.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.True);
            Assert.That(a.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.False, "늦게 온 요청은 거절되고 아무것도 바뀌지 않는다");
            Assert.That(a.WaitingForRoom, Is.False);
            Assert.That(a.LastRoomRefusal, Is.True, "화면은 거절을 구별해 보인다");
            Assert.That(a.LastRoomCommandApplied, Is.False);
            Assert.That(b.LastRoomCommandApplied, Is.True);
            Assert.That(room.Balance, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void MoneyShareOn_IfTheGameRestartsBeforeTheAnswer_TheHeldMoneyGoesBack()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomWallet.PieceName);
            var shares = room.Join("a");
            var game = Start(saves, shares, "slot-a", "a");
            var offer = game.FurnitureForSaleAt(Shop)[0];
            for (int i = 0; game.Wallet.Balance < offer.Price; i++) Shift(game, "w" + i);
            room.Deliver(shares);
            int before = room.Balance;
            game.PurchaseFurniture(offer.StableId);
            room.Leave(shares); // 답을 받기 전에 꺼졌다

            var again = room.Join("a");
            var restarted = Start(saves, again, "slot-a", "a");
            Assert.That(again.Outbox.Requests.Any(r => r.Kind == RoomWallet.Hold), Is.True, "보낸 잡기 요청은 세이브에 남아 있다");
            room.Deliver(again); // 방은 잡아 주지만 다시 할 명령이 없다
            room.Deliver(again);
            Assert.That(restarted.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.False);
            Assert.That(room.Balance, Is.EqualTo(before), "돈만 나가고 물건을 못 받는 일이 없다");
            Assert.That(restarted.Wallet.Balance, Is.EqualTo(before));
        }

        // ---------- 마을 상태 ----------

        [Test]
        public void TownShareOn_AFlagOneTurnsOn_IsOnForEveryone_PersonalFlagsStayPersonal()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomWorldFlags.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");

            a.World.TryActivate(a.Definitions.Get<WorldStateFlag>(TownFlag), new WorldStateActivationSource("", "", "test", 1)); // 준비: 깃발을 켜는 규칙 대신
            a.World.TryActivate(a.Definitions.Get<WorldStateFlag>(PersonalFlag), new WorldStateActivationSource("", "", "test", 1));
            Assert.That(a.VisitLocation("location.town-square"), Is.Not.Null); // 명령 하나로 저장한다
            room.Deliver(aShares);

            Assert.That(b.World.IsActive(TownFlag), Is.True, "마을 상태는 모두의 것");
            Assert.That(b.World.IsActive(PersonalFlag), Is.False, "개인 범위 깃발은 각자");
            Assert.That(Start(saves, room.Join("b"), "slot-b", "b").World.IsActive(TownFlag), Is.True, "켠 깃발은 b의 세이브에도 남는다");
        }

        [Test]
        public void TownShareOff_FlagsStayPersonal()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomWallet.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            a.World.TryActivate(a.Definitions.Get<WorldStateFlag>(TownFlag), new WorldStateActivationSource("", "", "test", 1));
            a.VisitLocation("location.town-square");
            room.Deliver(aShares);
            Assert.That(b.World.IsActive(TownFlag), Is.False);
        }

        // ---------- 방에 없던 날 ----------

        private static RoomClock RoomAt(int day, string player)
        {
            var clock = new RoomClock(TestWorld.Definitions.Get<LifeCalendar>(TestWorld.Settings.CalendarId), day: day);
            clock.Join(player);
            return clock;
        }

        [Test]
        public void ComingBackDaysLater_TheDaysAwayAreRestDays_AndTheGameIsOnTheRoomsDay()
        {
            using var saves = new TempSaves();
            var first = TestWorld.Start(saves.Store);
            Shift(first, "w1");
            int energyAfterWork = first.Student.Energy;

            var game = TestWorld.Start(saves.Store); // 방에 없던 사이 방은 4일째가 되었다
            var room = RoomAt(4, TestWorld.Player);
            game = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, TestWorld.Slot, TestWorld.Player, room: room);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(4));
            Assert.That(game.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
            Assert.That(game.Student.Energy, Is.GreaterThanOrEqualTo(energyAfterWork), "쉰 날의 하루 끝 회복을 받는다");
            Assert.That(game.Wallet.Balance, Is.EqualTo(5), "쉰 날에 잃는 것은 없다");
            Assert.That(TestWorld.Start(saves.Store).Student.CurrentDay, Is.EqualTo(4), "맞춘 날을 저장했다");
        }

        [Test]
        public void StartNextDay_GoesStraightToTheRoomsDay_WhenSeveralDaysPassed()
        {
            using var saves = new TempSaves();
            var room = RoomAt(1, TestWorld.Player);
            var game = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, TestWorld.Slot, TestWorld.Player, room: room);
            Assert.That(game.EndDay().Ended, Is.True);
            // 방은 이 사람 없이 3일째까지 갔다(다른 사람들이 마감).
            var later = RoomAt(3, TestWorld.Player);
            var again = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, TestWorld.Slot, TestWorld.Player, room: later);
            Assert.That(again.CanStartNextDay, Is.True);
            Assert.That(again.StartNextDay(), Is.True);
            Assert.That(again.Student.CurrentDay, Is.EqualTo(3), "하루씩이 아니라 방의 날로 간다");
        }

        // ---------- 끊긴 동안 보낸 요청 ----------

        [Test]
        public void ARequestLostBeforeTheRoomCountedIt_IsSentAgain_AndCountedOnce()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Shift(game, "w1"); // 개인 진행은 시간 포인트를 썼다
            int spent = game.Student.TimePointsSpentToday;
            Assume.That(spent, Is.GreaterThan(0));

            var room = RoomAt(1, TestWorld.Player); // 방은 그 요청을 받지 못했다
            var again = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, TestWorld.Slot, TestWorld.Player, room: room);
            Assert.That(room.CompletedParts(TestWorld.Player), Is.EqualTo(spent), "다시 보내 방이 센다");
            again.SyncWithRoom();
            Assert.That(room.CompletedParts(TestWorld.Player), Is.EqualTo(spent), "한 번만 센다");
        }

        [Test]
        public void ADayEndLostBeforeTheRoomCountedIt_IsSentAgain()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.EndDay().Ended, Is.True);
            var room = RoomAt(1, TestWorld.Player);
            room.Join("other");
            GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, TestWorld.Slot, TestWorld.Player, room: room);
            Assert.That(room.IsDayEnded(TestWorld.Player), Is.True);
            Assert.That(room.Day, Is.EqualTo(1), "다른 사람은 아직 마감하지 않았다");
        }
    }
}
