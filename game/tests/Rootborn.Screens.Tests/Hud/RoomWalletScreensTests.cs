using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Room;
using Rootborn.Game;
using Rootborn.Screens.Day;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Inventory;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Hud
{
    /// <summary>
    /// 돈을 같이 쓰는 방에서 화면이 방 지갑을 보인다(멀티플레이 설계 4.1절, 3.2절): HUD 잔액·가방의 용돈 카드가 방 지갑이고, 다른 사람이 벌면
    /// 알림으로 숫자가 바뀐다(매 프레임 읽지 않는다). 가게에서 사면 방의 허락을 기다리는 동안과 거절됐을 때를 구별해 보인다.
    /// 방 쪽은 Core RoomPieceSet을 테스트가 손으로 돌린다(서버가 할 일 — 전송을 거친 흐름은 Host·Server 테스트). 창 없이 화면을 눌러 보는 자동 테스트다.
    /// </summary>
    public sealed class RoomWalletScreensTests
    {
        private const string MoneyOnlyWork = "work.clinic-helper"; // 보상: 용돈 5
        private const string Store = "location.corner-store";

        /// <summary>방 하나(조각 + 장부)와 그 방에 들어간 사람들(Game 테스트의 FakeRoom과 같은 일).</summary>
        private sealed class FakeRoom
        {
            private readonly List<(string Player, RoomShares Shares)> _players = new List<(string, RoomShares)>();
            private int _ids;

            public FakeRoom(params string[] shared) => Pieces = new RoomPieceSet(new RoomShareSettings(shared), RoomPieceCatalog.CreateDefault());

            public RoomPieceSet Pieces { get; }

            public RoomShares Join(string playerId)
            {
                var shares = new RoomShares(Pieces.Settings, RoomPieceCatalog.CreateDefault(), () => playerId + "-r" + (++_ids));
                foreach (var piece in Pieces.Pieces) shares.Receive(piece.State());
                _players.Add((playerId, shares));
                return shares;
            }

            public void Deliver(RoomShares from)
            {
                var (player, _) = _players.First(p => p.Shares == from);
                var answers = new List<RoomAnswer>();
                var changed = new List<IRoomPiece>();
                Pieces.Apply(player, from.Outbox.Requests.Select(r => r.Copy()).ToList(), 1, answers, changed);
                foreach (var a in answers) from.Receive(a);
                foreach (var piece in changed)
                    foreach (var p in _players) p.Shares.Receive(piece.State());
            }

            /// <summary>다른 사람이 돈을 넣었다(그 사람의 게임 한 판 없이 요청만).</summary>
            public void Deposit(RoomShares other, int amount)
            {
                other.Outbox.Add(new RoomRequestSaveData { Piece = RoomWallet.PieceName, RequestId = other.Outbox.NewId(), Kind = RoomWallet.Deposit, Amount = amount });
                Deliver(other);
            }

            public int Balance => ((RoomWallet)Pieces.Pieces.First(p => p.Name == RoomWallet.PieceName)).Balance;
        }

        private static string ClockText(DayScreensFixture f) => ((UiLabel)f.Node(ClockHudScreen.LabelId)).Text;

        [Test]
        public void HudBalance_IsTheRoomWallet_AndChangesWhenSomeoneElseEarns()
        {
            var room = new FakeRoom(RoomWallet.PieceName);
            RoomShares mine = room.Join("me"), theirs = room.Join("friend");
            using var f = new DayScreensFixture(shares: mine, playerId: "me");
            f.Stack.OpenHud();
            Assert.That(f.Session.SharesWallet, Is.True);
            Assert.That(ClockText(f), Does.EndWith(f.Text.Format("ui.hud.room-balance", 0)), "돈을 같이 쓰는 방이면 방 지갑이라고 보인다");

            room.Deposit(theirs, 7);
            Assert.That(ClockText(f), Does.EndWith(f.Text.Format("ui.hud.room-balance", 7)), "다른 사람이 번 돈이 알림으로 내 HUD에 보인다");
            KnownMissingKeys.AssertOnlyKnownMissing(f.Text, f.Stack.Find(ScreenCatalog.HudClock).TextKeys);
        }

        [Test]
        public void HudBalance_WithoutMoneyShare_IsThePersonalAllowanceAsBefore()
        {
            var room = new FakeRoom(RoomWorldFlags.PieceName);
            using var f = new DayScreensFixture(shares: room.Join("me"), playerId: "me");
            f.Stack.OpenHud();
            Assert.That(f.Session.SharesWallet, Is.False);
            Assert.That(ClockText(f), Does.EndWith(f.Text.Format("dream.shop.balance", f.Session.Wallet.Balance)));
        }

        [Test]
        public void InventoryMoneyCard_ShowsTheRoomWallet_AndFollowsIt()
        {
            var room = new FakeRoom(RoomWallet.PieceName);
            RoomShares mine = room.Join("me"), theirs = room.Join("friend");
            using var f = new DayScreensFixture(shares: mine, playerId: "me");
            room.Deposit(theirs, 12);
            f.Stack.Open(ScreenCatalog.Inventory);
            var screen = (InventoryScreen)f.Stack.Find(ScreenCatalog.Inventory);
            var currency = f.Session.Inventory.Currency;
            Assert.That(screen.Model.Rows.First(r => r.Item == currency).Count, Is.EqualTo(12), "가방의 용돈 카드는 방 지갑의 잔액");
            room.Deposit(theirs, 3);
            Assert.That(screen.Model.Rows.First(r => r.Item == currency).Count, Is.EqualTo(15), "열린 가방도 알림으로 바뀐다");
            Assert.That(screen.RoomWalletShown, Is.True, "방 지갑이라는 표시");
        }

        private static LocationScreen OpenStore(DayScreensFixture f)
        {
            Assert.That(f.Interact("Town", LocationScreen.InteractionKind, Store), Is.True);
            return (LocationScreen)f.Stack.Find(ScreenCatalog.Location);
        }

        private static int FirstPurchase(LocationScreen screen)
        {
            for (int i = 0; i < screen.Model.Rows.Count; i++)
                if (screen.Model.Rows[i].Kind != LocationRowKind.Activity && screen.Model.Rows[i].Enabled) return i;
            return -1;
        }

        private static void Press(DayScreensFixture f, LocationScreen screen, int index)
        {
            UiRect r = screen.RowRect(index);
            f.ClickAt(r.CenterX, r.CenterY, LocationScreen.RowsId);
        }

        private static string Result(DayScreensFixture f) => ((UiTextBlock)f.Node(LocationScreen.ResultId)).Text;

        [Test]
        public void BuyingFurniture_ShowsWaiting_ThenBought_AndCannotBePressedAgainWhileWaiting()
        {
            var room = new FakeRoom(RoomWallet.PieceName);
            RoomShares mine = room.Join("me"), theirs = room.Join("friend");
            using var f = new DayScreensFixture(shares: mine, playerId: "me");
            room.Deposit(theirs, 500);
            LocationScreen screen = OpenStore(f);
            int row = FirstPurchase(screen);
            Assert.That(row, Is.GreaterThanOrEqualTo(0), "방 지갑의 돈으로 살 수 있는 가구");
            string id = screen.Model.Rows[row].Id;

            Press(f, screen, row);
            Assert.That(f.Session.WaitingForRoom, Is.True);
            Assert.That(Result(f), Is.EqualTo(f.Text.Get("ui.room.wallet-waiting")), "기다리는 중 — '돈이 모자라요'가 아니다");
            Assert.That(((UiLabel)f.Node(LocationScreen.TimeBudgetId)).Text, Does.EndWith(string.Format(f.Text.Get("life.location.time-with-room-balance"), "", 500).Substring(2)),
                "가게의 잔액 줄도 방 지갑");
            int asked = mine.Outbox.Requests.Count;
            Press(f, screen, row);
            Assert.That(mine.Outbox.Requests.Count, Is.EqualTo(asked), "기다리는 동안 다시 눌러도 다시 묻지 않는다");

            room.Deliver(mine);
            Assert.That(f.Session.WaitingForRoom, Is.False);
            string name = f.Text.Get(screen.Model.Rows.First(r => r.Id == id).NameKey);
            Assert.That(Result(f), Is.EqualTo(string.Format(f.Text.Get("dream.shop.bought"), name)), "허락이 오면 산 것을 보인다");
            KnownMissingKeys.AssertOnlyKnownMissing(f.Text, screen.TextKeys);
        }

        [Test]
        public void BuyingFurniture_RefusedByTheRoom_SaysWhy_AndChangesNothing()
        {
            var room = new FakeRoom(RoomWallet.PieceName);
            RoomShares mine = room.Join("me"), theirs = room.Join("friend");
            using var f = new DayScreensFixture(shares: mine, playerId: "me");
            room.Deposit(theirs, 500);
            LocationScreen screen = OpenStore(f);
            int row = FirstPurchase(screen);
            Press(f, screen, row);
            Assert.That(f.Session.WaitingForRoom, Is.True);

            // 친구가 먼저 다 써 버렸다: 잡기가 닿기 전에 방 지갑이 빈다.
            theirs.Outbox.Add(new RoomRequestSaveData { Piece = RoomWallet.PieceName, RequestId = "friend-hold", Kind = RoomWallet.Hold, Amount = room.Balance });
            room.Deliver(theirs);
            room.Deliver(mine);
            Assert.That(f.Session.WaitingForRoom, Is.False);
            Assert.That(f.Session.LastRoomRefusal, Is.True);
            Assert.That(Result(f), Is.EqualTo(f.Text.Get("ui.room.wallet-refused")), "거절 이유 — 기다림·구매와 구별된다");
            Assert.That(screen.Model.Rows[row].Owned, Is.False);
            KnownMissingKeys.AssertOnlyKnownMissing(f.Text, screen.TextKeys);
        }

        [Test]
        public void WalletTexts_AreInEveryLanguage()
        {
            GameText text = TestData.Text();
            KnownMissingKeys.AssertOnlyKnownMissing(text, new[] { "ui.hud.room-balance", "ui.room.wallet-waiting", "ui.room.wallet-refused", "ui.inventory.room-wallet" });
        }
    }
}
