using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Room;
using Rootborn.Simulation.Rooms;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트: 같이 하는 방의 불변식 도우미가 어긋남을 실제로 잡는지 — 손으로 만든 어긋난 기록을 넣으면 실패한다.
    /// 같이 쓰는 지갑의 돈 장부(SharedWalletBook): 이중 청구·덜 청구·까닭 없는 감소.
    /// </summary>
    [TestFixture]
    public sealed class RoomMoneyAndRejoinChecksTests
    {
        private const string P = "p1";

        private sealed class FakeLedger
        {
            private readonly Dictionary<string, RoomAnswer> _answers = new Dictionary<string, RoomAnswer>();
            public void Answer(RoomRequestSaveData r, string outcome) => _answers[P + "/" + r.RequestId] = new RoomAnswer(r.Piece, r.RequestId, outcome, r.Amount);
            public bool TryGet(string player, string id, out RoomAnswer answer) => _answers.TryGetValue(player + "/" + id, out answer);
        }

        private static RoomRequestSaveData Wallet(string id, string kind, int amount, string scope = null)
            => new RoomRequestSaveData { Piece = RoomWallet.PieceName, RequestId = id, Kind = kind, Amount = amount, Ids = scope == null ? new string[0] : new[] { scope } };

        // 기준 100을 잡고, 꾸밈 하나(잡기 id hold-1)를 방 지갑 잡기 scope-1(held)로 낸다. 방이 반영했다고 답한다.
        private static (SharedWalletBook Book, FakeLedger Ledger) PaidDecoration(int held, string outcome = RoomOutcomes.Applied)
        {
            var book = new SharedWalletBook();
            var ledger = new FakeLedger();
            Assert.That(book.Check(100, ledger.TryGet, false), Is.Null, "기준");
            var hold = Wallet("scope-1", RoomWallet.Hold, held);
            var use = Wallet("use-1", RoomWallet.Use, held, "scope-1");
            book.See(P, hold);
            Assert.That(book.LinkDecoration(P, "hold-1", "scope-1"), Is.Null);
            book.See(P, use);
            ledger.Answer(hold, outcome);
            ledger.Answer(use, outcome == RoomOutcomes.Applied ? RoomOutcomes.Applied : RoomOutcomes.Invalid);
            return (book, ledger);
        }

        [Test]
        public void SharedWallet_DecorationPaidAtTheRecomputedCost_Passes()
        {
            var (book, ledger) = PaidDecoration(30);
            var deposit = Wallet("dep-1", RoomWallet.Deposit, 50);
            book.See(P, deposit);
            ledger.Answer(deposit, RoomOutcomes.Applied);
            Assert.That(book.DecorationLanded(P, "hold-1", 30), Is.Null);
            Assert.That(book.Check(120, ledger.TryGet, false), Is.Null, "100 + 넣기 50 − 꾸밈 30 = 120");
        }

        [Test]
        public void SharedWallet_DoubleCharge_HeldMoreThanTheRoomRecordCosts_Fails()
        {
            var (book, ledger) = PaidDecoration(60);
            Assert.That(book.DecorationLanded(P, "hold-1", 30), Does.Contain("잡은 돈과 방이 바꾼 것의 비용이 다르다"));
            Assert.That(book.Check(40, ledger.TryGet, false), Is.Not.Null, "실제로 60이 빠졌는데 방의 기록으로는 30이다");
        }

        [Test]
        public void SharedWallet_TwoWalletHoldsForOneDecoration_Fails()
        {
            var (book, _) = PaidDecoration(30);
            Assert.That(book.LinkDecoration(P, "hold-1", "scope-2"), Does.Contain("이중 청구"));
        }

        [Test]
        public void SharedWallet_DecorationWithoutAWalletHold_Fails()
        {
            var book = new SharedWalletBook();
            Assert.That(book.DecorationLanded(P, "hold-9", 30), Does.Contain("덜 청구"));
        }

        [Test]
        public void SharedWallet_UnderCharge_HoldRefusedButTheDecorationLanded_Fails()
        {
            var (book, ledger) = PaidDecoration(30, RoomOutcomes.Refused);
            book.DecorationLanded(P, "hold-1", 30);
            Assert.That(book.Check(100, ledger.TryGet, false), Does.Contain("덜 청구"));
        }

        [Test]
        public void SharedWallet_BalanceDropsWithoutAKnownSpending_Fails()
        {
            var book = new SharedWalletBook();
            var ledger = new FakeLedger();
            Assert.That(book.Check(100, ledger.TryGet, false), Is.Null);
            Assert.That(book.Check(70, ledger.TryGet, false), Does.Contain("알려진 지출과 맞지 않는다"));
        }

        [Test]
        public void SharedWallet_RequestNeverReachedTheRoom_IsNotCounted()
        {
            var book = new SharedWalletBook();
            var ledger = new FakeLedger();
            Assert.That(book.Check(100, ledger.TryGet, false), Is.Null);
            book.See(P, Wallet("dep-lost", RoomWallet.Deposit, 50)); // 보내기 전에 끊겨 사라짐(방의 답 없음)
            Assert.That(book.Check(100, ledger.TryGet, false), Is.Null);
            Assert.That(book.Unanswered, Is.EqualTo(1));
        }
    }
}
