using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 방 설정과 공유 조각 틀(멀티플레이 설계 3.2절·4절): 방 설정은 만들 때만 정하고, 조각은 방이 주인이며 요청 id로 한 번만 반영한다.
    /// 방 지갑: 넣기는 거절하지 않고, 쓰기는 방이 잡아 준 뒤에만 개인 쪽이 바뀐다(먼저 온 요청만, 음수 없음).
    /// </summary>
    public sealed class RoomPiecesTests
    {
        private static RoomRequestSaveData Req(string piece, string id, string kind, int amount = 0, params string[] ids)
            => new RoomRequestSaveData { Piece = piece, RequestId = id, Kind = kind, Amount = amount, Ids = ids };

        private static RoomPieceSet Room(params string[] shared) => new RoomPieceSet(new RoomShareSettings(shared), RoomPieceCatalog.CreateDefault());

        private static RoomAnswer ApplyOne(RoomPieceSet room, string player, RoomRequestSaveData request, List<IRoomPiece> changed = null)
        {
            var answers = new List<RoomAnswer>();
            room.Apply(player, new[] { request }, 1, answers, changed);
            return answers[0];
        }

        // ---------- 방 설정 ----------

        [Test]
        public void ROOMSET_001_Settings_AreSortedAndUnique_AndRoundTrip()
        {
            var settings = new RoomShareSettings(new[] { "town", "wallet", " wallet ", "" });
            Assert.That(settings.SharedPieces, Is.EqualTo(new[] { "town", "wallet" }));
            Assert.That(settings.Shares("wallet"), Is.True);
            Assert.That(settings.Shares("house"), Is.False);
            Assert.That(RoomShareSettings.FromSaveData(settings.ToSaveData()).SameAs(settings), Is.True);
        }

        [Test]
        public void ROOMSET_002_NewRoom_WritesTheRequestedSettings_LaterOpensKeepTheFirstOnes()
        {
            var store = new InMemorySaveStore();
            var first = RoomSettingsSaveFiles.LoadOrCreate(store, "room", true, new RoomShareSettings(new[] { "wallet" }), out bool created);
            Assert.That(created, Is.True);
            Assert.That(first.Shares("wallet"), Is.True);

            var again = RoomSettingsSaveFiles.LoadOrCreate(store, "room", false, new RoomShareSettings(new[] { "town" }), out created);
            Assert.That(created, Is.False);
            Assert.That(again.SharedPieces, Is.EqualTo(new[] { "wallet" }), "방을 만든 뒤에는 바꾸지 않는다");
        }

        [Test]
        public void ROOMSET_003_ARoomMadeBeforeSettingsExisted_SharesNothing()
        {
            var store = new InMemorySaveStore();
            var settings = RoomSettingsSaveFiles.LoadOrCreate(store, "room", false, new RoomShareSettings(new[] { "wallet" }), out bool created);
            Assert.That(created, Is.False);
            Assert.That(settings.SharedPieces, Is.Empty, "그 방은 지금까지 각자 쓰고 있었다");
            Assert.That(store.Contains("room", SaveFileNames.RoomSettings), Is.False);
        }

        [Test]
        public void ROOMSET_004_UnknownPiece_IsReported()
        {
            var unknown = new List<string>();
            new RoomShareSettings(new[] { "wallet", "house" }).CopyUnknown(RoomPieceCatalog.CreateDefault(), unknown);
            Assert.That(unknown, Is.EqualTo(new[] { "house" }));
            Assert.Throws<ArgumentException>(() => Room("house"));
        }

        [Test]
        public void ROOMSET_005_Catalog_AddsAPieceWithOneLine_AndRefusesBadNames()
        {
            var catalog = RoomPieceCatalog.CreateDefault();
            catalog.Add("chest", () => new RoomWorldFlags(), outbox => new RoomWorldFlagsClient(outbox));
            Assert.That(catalog.Knows("chest"), Is.True);
            Assert.Throws<ArgumentException>(() => catalog.Add("chest", () => new RoomWallet(), o => new RoomWalletClient(o)));
            Assert.Throws<ArgumentException>(() => catalog.Add("bad name", () => new RoomWallet(), o => new RoomWalletClient(o)));
        }

        // ---------- 요청 장부 ----------

        [Test]
        public void ROOMREQ_001_SameRequestId_IsAppliedOnce_AndGetsTheSameAnswer()
        {
            var room = Room("wallet");
            var first = ApplyOne(room, "a", Req("wallet", "r1", RoomWallet.Deposit, 10));
            var again = ApplyOne(room, "a", Req("wallet", "r1", RoomWallet.Deposit, 10));
            Assert.That(first.Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That((again.Outcome, again.Amount), Is.EqualTo((first.Outcome, first.Amount)));
            room.TryGet("wallet", out var piece);
            Assert.That(((RoomWallet)piece).Balance, Is.EqualTo(10), "한 번만 넣었다");
        }

        [Test]
        public void ROOMREQ_002_AnotherPlayerReusingAnId_IsInvalid_AndChangesNothing()
        {
            var room = Room("wallet");
            ApplyOne(room, "a", Req("wallet", "r1", RoomWallet.Deposit, 10));
            var stolen = ApplyOne(room, "b", Req("wallet", "r1", RoomWallet.Deposit, 10));
            Assert.That(stolen.Outcome, Is.EqualTo(RoomOutcomes.Invalid));
        }

        [Test]
        public void ROOMREQ_003_RequestForAPieceTheRoomDoesNotShare_IsInvalid()
        {
            var room = Room("town");
            Assert.That(ApplyOne(room, "a", Req("wallet", "r1", RoomWallet.Deposit, 10)).Outcome, Is.EqualTo(RoomOutcomes.Invalid));
        }

        [Test]
        public void ROOMREQ_004_Ledger_SurvivesSaveAndLoad_AndKeepsOnlyTheNewest()
        {
            var store = new InMemorySaveStore();
            var room = Room("wallet");
            ApplyOne(room, "a", Req("wallet", "r1", RoomWallet.Deposit, 10));
            room.Save(store, "room");

            var again = Room("wallet");
            again.Load(store, "room");
            ApplyOne(again, "a", Req("wallet", "r1", RoomWallet.Deposit, 10));
            again.TryGet("wallet", out var piece);
            Assert.That(((RoomWallet)piece).Balance, Is.EqualTo(10), "다시 켠 방도 같은 요청을 다시 반영하지 않는다");

            var ledger = new RoomRequestLedger();
            for (int i = 0; i < RoomRequestLedger.Capacity + 5; i++) ledger.Record("a", new RoomAnswer("wallet", "id" + i, RoomOutcomes.Applied, i));
            Assert.That(ledger.Count, Is.EqualTo(RoomRequestLedger.Capacity));
            Assert.That(ledger.TryGet("a", "id0", out _), Is.False);
            Assert.That(ledger.TryGet("a", "id" + (RoomRequestLedger.Capacity + 4), out _), Is.True);
        }

        // ---------- 방 지갑(방 쪽) ----------

        [Test]
        public void ROOMWALLET_001_TwoHoldsAtOnce_OnlyTheFirstWins_AndNeverNegative()
        {
            var room = Room("wallet");
            ApplyOne(room, "a", Req("wallet", "d", RoomWallet.Deposit, 100));
            var answers = new List<RoomAnswer>();
            room.Apply("a", new[] { Req("wallet", "ha", RoomWallet.Hold, 80) }, 1, answers, null);
            room.Apply("b", new[] { Req("wallet", "hb", RoomWallet.Hold, 80) }, 1, answers, null);
            Assert.That(answers[0].Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(answers[1].Outcome, Is.EqualTo(RoomOutcomes.Refused));
            room.TryGet("wallet", out var piece);
            var wallet = (RoomWallet)piece;
            Assert.That(wallet.Balance, Is.EqualTo(20));
            Assert.That(wallet.HeldTotal, Is.EqualTo(80));
        }

        [Test]
        public void ROOMWALLET_002_UseKeepsTheMoneySpent_ReleaseGivesItBack_OnlyTheHolderMay()
        {
            var room = Room("wallet");
            ApplyOne(room, "a", Req("wallet", "d", RoomWallet.Deposit, 100));
            ApplyOne(room, "a", Req("wallet", "h1", RoomWallet.Hold, 30));
            ApplyOne(room, "a", Req("wallet", "h2", RoomWallet.Hold, 20));
            Assert.That(ApplyOne(room, "b", Req("wallet", "x", RoomWallet.Release, 30, "h1")).Outcome, Is.EqualTo(RoomOutcomes.Invalid), "남이 잡은 돈은 못 푼다");
            Assert.That(ApplyOne(room, "a", Req("wallet", "u1", RoomWallet.Use, 30, "h1")).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(ApplyOne(room, "a", Req("wallet", "r2", RoomWallet.Release, 20, "h2")).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(ApplyOne(room, "a", Req("wallet", "r1", RoomWallet.Release, 30, "h1")).Outcome, Is.EqualTo(RoomOutcomes.Invalid), "쓴 잡기는 풀 수 없다");
            room.TryGet("wallet", out var piece);
            Assert.That(((RoomWallet)piece).Balance, Is.EqualTo(70));
            Assert.That(((RoomWallet)piece).Recent.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void ROOMWALLET_003_Holds_SurviveSaveAndLoad()
        {
            var store = new InMemorySaveStore();
            var room = Room("wallet");
            ApplyOne(room, "a", Req("wallet", "d", RoomWallet.Deposit, 50));
            ApplyOne(room, "a", Req("wallet", "h", RoomWallet.Hold, 40));
            room.Save(store, "room");
            var again = Room("wallet");
            again.Load(store, "room");
            Assert.That(ApplyOne(again, "a", Req("wallet", "r", RoomWallet.Release, 40, "h")).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            again.TryGet("wallet", out var piece);
            Assert.That(((RoomWallet)piece).Balance, Is.EqualTo(50));
        }

        [Test]
        public void ROOMWALLET_004_BadRequests_AreInvalid()
        {
            var room = Room("wallet");
            Assert.That(ApplyOne(room, "a", Req("wallet", "1", RoomWallet.Deposit, -5)).Outcome, Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(ApplyOne(room, "a", Req("wallet", "2", RoomWallet.Hold, 0)).Outcome, Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(ApplyOne(room, "a", Req("wallet", "3", "steal", 5)).Outcome, Is.EqualTo(RoomOutcomes.Invalid));
        }

        // ---------- 방 지갑(플레이어 쪽) ----------

        private static (RoomShares Shares, RoomWalletClient Wallet) PlayerWallet(int roomBalance)
        {
            int n = 0;
            var shares = new RoomShares(new RoomShareSettings(new[] { "wallet" }), RoomPieceCatalog.CreateDefault(), () => "id" + (++n));
            var wallet = shares.Find<RoomWalletClient>();
            shares.Receive(new RoomPieceState("wallet", roomBalance, null));
            return (shares, wallet);
        }

        [Test]
        public void ROOMWALLETC_001_Deposit_ShowsAtOnce_AndIsWrittenToSend()
        {
            var (shares, wallet) = PlayerWallet(10);
            Assert.That(wallet.TryExchange(0, 5, null), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(15));
            Assert.That(shares.Outbox.Requests.Count, Is.EqualTo(1));
            Assert.That(shares.Outbox.Requests[0].Kind, Is.EqualTo(RoomWallet.Deposit));

            shares.Receive(new RoomAnswer("wallet", shares.Outbox.Requests[0].RequestId, RoomOutcomes.Applied, 15));
            shares.Receive(new RoomPieceState("wallet", 15, null));
            Assert.That(wallet.Balance, Is.EqualTo(15), "답과 새 잔액을 받아도 두 번 세지 않는다");
            Assert.That(shares.Outbox.Requests, Is.Empty);
        }

        [Test]
        public void ROOMWALLETC_002_SpendingOutsideACommand_IsRefused()
        {
            var (shares, wallet) = PlayerWallet(100);
            Assert.That(wallet.CanExchange(10, 0), Is.False);
            Assert.That(wallet.TryExchange(10, 0, null), Is.False);
            Assert.That(shares.Outbox.Requests, Is.Empty);
        }

        [Test]
        public void ROOMWALLETC_003_SpendingInACommand_AsksFirst_ThenWorksWithTheGrant()
        {
            var (shares, wallet) = PlayerWallet(100);
            bool committed = false;
            wallet.BeginScope("buy-1");
            Assert.That(wallet.CanExchange(30, 0), Is.True, "다른 확인이 다 지나게 될 수 있다고 답한다");
            Assert.That(wallet.TryExchange(30, 0, () => committed = true), Is.False, "허락 전에는 바뀌지 않는다");
            wallet.EndScope();
            Assert.That(committed, Is.False);
            Assert.That(wallet.WasAsked("buy-1"), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(100));

            string granted = null;
            wallet.Granted += s => granted = s;
            shares.Receive(new RoomAnswer("wallet", "buy-1", RoomOutcomes.Applied, 70));
            shares.Receive(new RoomPieceState("wallet", 70, null));
            Assert.That(granted, Is.EqualTo("buy-1"));

            wallet.BeginScope("buy-1");
            Assert.That(wallet.TryExchange(30, 0, () => committed = true), Is.True);
            wallet.EndScope();
            Assert.That(committed, Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(70), "잡은 돈은 이미 방 잔액에서 빠졌다");
            Assert.That(shares.Outbox.Requests[shares.Outbox.Requests.Count - 1].Kind, Is.EqualTo(RoomWallet.Use));
        }

        [Test]
        public void ROOMWALLETC_004_Refused_ChangesNothing_AndLetsTheCommandAskAgain()
        {
            var (shares, wallet) = PlayerWallet(100);
            string refused = null;
            wallet.Refused += s => refused = s;
            wallet.BeginScope("buy-1");
            wallet.TryExchange(80, 0, null);
            wallet.EndScope();
            shares.Receive(new RoomAnswer("wallet", "buy-1", RoomOutcomes.Refused, 10));
            Assert.That(refused, Is.EqualTo("buy-1"));
            Assert.That(wallet.HasGrant("buy-1"), Is.False);
            Assert.That(shares.Outbox.Requests, Is.Empty);
        }

        [Test]
        public void ROOMWALLETC_005_MoreThanTheBalance_IsNotEvenAsked()
        {
            var (shares, wallet) = PlayerWallet(10);
            wallet.BeginScope("buy-1");
            Assert.That(wallet.CanExchange(30, 0), Is.False);
            wallet.EndScope();
            Assert.That(shares.Outbox.Requests, Is.Empty);
        }

        [Test]
        public void ROOMWALLETC_006_ReleasingAGrant_GivesTheMoneyBackInTheShownBalance()
        {
            var (shares, wallet) = PlayerWallet(100);
            wallet.BeginScope("buy-1");
            wallet.TryExchange(30, 0, null);
            wallet.EndScope();
            shares.Receive(new RoomAnswer("wallet", "buy-1", RoomOutcomes.Applied, 70));
            shares.Receive(new RoomPieceState("wallet", 70, null));
            Assert.That(wallet.ReleaseGrant("buy-1"), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(100));
            Assert.That(shares.Outbox.Requests[0].Kind, Is.EqualTo(RoomWallet.Release));
        }

        [Test]
        public void ROOMWALLETC_007_InTheBag_AFullBagNeverAsks_AndTheGrantBuysAllOrNothing()
        {
            var (shares, wallet) = PlayerWallet(100);
            var money = new ItemDefinition("money", 9999, ItemCategory.Misc);
            var chair = new ItemDefinition("chair", 1, ItemCategory.Resource);
            var bag = new ItemInventory(wallet, money);
            var cost = new[] { new InventoryGrant(money, 40) };
            var reward = new[] { new InventoryGrant(chair, 1) };

            wallet.BeginScope("buy");
            Assert.That(bag.TryExchange(cost, reward), Is.False);
            wallet.EndScope();
            Assert.That(bag.CountOf(chair), Is.EqualTo(0));
            Assert.That(wallet.WasAsked("buy"), Is.True);

            shares.Receive(new RoomAnswer("wallet", "buy", RoomOutcomes.Applied, 60));
            shares.Receive(new RoomPieceState("wallet", 60, null));
            wallet.BeginScope("buy");
            Assert.That(bag.TryExchange(cost, reward), Is.True);
            wallet.EndScope();
            Assert.That(bag.CountOf(chair), Is.EqualTo(1));
            Assert.That(bag.CountOf(money), Is.EqualTo(60));

            for (int i = 0; i < ItemInventory.MaxSlots; i++) bag.Add(new ItemDefinition("rock" + i, 1, ItemCategory.Resource));
            wallet.BeginScope("buy-full");
            Assert.That(bag.TryExchange(cost, reward), Is.False);
            wallet.EndScope();
            Assert.That(wallet.WasAsked("buy-full"), Is.False, "가방이 차서 안 되는 명령은 방에 묻지 않는다");
        }

        [Test]
        public void ROOMSHARES_001_AnswersThatCameBeforeTheOutboxWasRead_AreMatchedLater()
        {
            var shares = new RoomShares(new RoomShareSettings(new[] { "wallet" }), RoomPieceCatalog.CreateDefault());
            var wallet = shares.Find<RoomWalletClient>();
            shares.Receive(new RoomPieceState("wallet", 100, null));
            shares.Receive(new RoomAnswer("wallet", "hold-1", RoomOutcomes.Applied, 70));
            shares.LoadOutbox(new RoomOutboxSaveData
            {
                Requests = new[] { new RoomRequestSaveData { Piece = "wallet", RequestId = "hold-1", Kind = RoomWallet.Hold, Amount = 30 } },
            });
            Assert.That(wallet.HasGrant("hold-1"), Is.True);
            Assert.That(shares.Outbox.Requests, Is.Empty);
        }

        // ---------- 마을 상태 ----------

        [Test]
        public void ROOMTOWN_001_Activating_IsIdempotent_AndPublishSendsOnlyNewFlags()
        {
            var room = Room("town");
            var changed = new List<IRoomPiece>();
            ApplyOne(room, "a", Req("town", "1", RoomWorldFlags.Activate, 0, "shop-open"), changed);
            Assert.That(changed.Count, Is.EqualTo(1));
            changed.Clear();
            ApplyOne(room, "b", Req("town", "2", RoomWorldFlags.Activate, 0, "shop-open"), changed);
            Assert.That(changed, Is.Empty, "이미 켜진 깃발은 바뀐 것이 없다");

            var shares = new RoomShares(new RoomShareSettings(new[] { "town" }), RoomPieceCatalog.CreateDefault());
            var town = shares.Find<RoomWorldFlagsClient>();
            room.TryGet("town", out var piece);
            shares.Receive(piece.State());
            Assert.That(town.RoomFlags, Is.EquivalentTo(new[] { "shop-open" }));
            Assert.That(town.Publish(new[] { "shop-open", "park-open" }), Is.True);
            Assert.That(shares.Outbox.Requests[0].Ids, Is.EqualTo(new[] { "park-open" }));
            Assert.That(town.Publish(new[] { "park-open" }), Is.False, "보낸 것은 다시 적지 않는다");
        }
    }
}
