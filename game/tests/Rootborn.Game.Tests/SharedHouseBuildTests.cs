using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Careers;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// HOUSE-004: 같이 쓰는 집에서 한 사람이 벽지·바닥·띠 색을 꾸미면 모두에게 보이고, 돈은 요청한 사람(돈을 같이 쓰면 방 지갑)이 한 번만 낸다.
    /// 흐름: 방이 칸을 잡는다 → 요청한 쪽이 돈을 내는 바꾸기 안에서 쓰기를 적는다(못 내면 풀기) → 방이 적용하고 모두에게 알린다.
    /// 방 쪽은 Core RoomPieceSet을 테스트가 손으로 돌린다(서버가 할 일 — 답을 먼저, 상태를 뒤에, 서버와 같은 순서). 실제 data/와 실제 집 맵, 임시 폴더.
    /// GameSession 명령으로 진행하는 자동 테스트다(화면·전송 근거 아님).
    /// </summary>
    public sealed class SharedHouseBuildTests
    {
        private const string Allowance = "item.allowance";
        private const int TopLeftRoomFill = 87; // HouseFinishTests와 같은 방(방 바탕 84 + 맞닿은 문간 3)
        private static readonly CellPos TopLeftRoom = HouseRoom.TopLeftRoom;
        private const string RoomSlot = "room";

        private sealed class FakeRoom
        {
            private readonly List<(string Player, RoomShares Shares)> _players = new List<(string, RoomShares)>();
            private int _ids;

            public FakeRoom(TempSaves saves, int walletBalance, params string[] shared)
            {
                var d = TestWorld.Definitions;
                var rules = new RoomHouseRules(d.All<FurnitureDefinition>(), s => s == HouseRoom.SurfaceId ? HouseRoom.Grid : null,
                    buildFrameFor: s => s == HouseRoom.SurfaceId ? HouseRoom.Frame : null, finishes: d.All<RoomFinishDefinition>());
                Pieces = new RoomPieceSet(new RoomShareSettings(shared), RoomPieceCatalog.CreateDefault(rules));
                if (Wallet != null) SetWallet(saves, walletBalance);
            }

            public RoomPieceSet Pieces { get; }

            public RoomHouseBuild Build => Pieces.TryGet(RoomHouseBuild.PieceName, out var p) ? (RoomHouseBuild)p : null;

            public RoomWallet Wallet => Pieces.TryGet(RoomWallet.PieceName, out var p) ? (RoomWallet)p : null;

            /// <summary>방 지갑 잔액을 바꾼다(다른 사람이 그 사이 썼다 — 준비 단계).</summary>
            public void SetWallet(TempSaves saves, int balance)
            {
                saves.Store.Write(RoomSlot, SaveFileNames.RoomPiece(RoomWallet.PieceName), new RoomWalletSaveData { Version = RoomWalletSaveData.KnownVersion, Balance = balance });
                Wallet.Load(saves.Store, RoomSlot);
            }

            /// <summary>그 신원으로 들어온다. 방 접속(Net RoomGuestLink)처럼 조각 묶음에 방이 아는 신원을 적는다.</summary>
            public RoomShares Join(string playerId)
            {
                var shares = new RoomShares(Pieces.Settings, RoomPieceCatalog.CreateDefault(), () => playerId + "-r" + (++_ids));
                shares.NoteRoomPlayerId(playerId);
                foreach (var piece in Pieces.Pieces) shares.Receive(piece.State());
                _players.Add((playerId, shares));
                return shares;
            }

            /// <summary>그 접속이 끊겼다(그 게임 한 판은 더 아무것도 받지 않는다).</summary>
            public void Leave(RoomShares shares) => _players.RemoveAll(p => p.Shares == shares);

            /// <summary>그 사람의 보낼 요청을 방이 한 번 반영하고 답을 그 사람에게, 바뀐 상태를 모두에게 보낸다(서버처럼 답이 먼저).</summary>
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

            /// <summary>그 사람의 보낼 요청을 다른 방 조각 묶음(같은 방을 다른 규칙으로 다시 켠 것)이 반영하고 답·바뀐 상태를 그 사람에게 준다.</summary>
            public void DeliverVia(RoomPieceSet pieces, RoomShares from)
            {
                var (player, _) = _players.First(p => p.Shares == from);
                var answers = new List<RoomAnswer>();
                var changed = new List<IRoomPiece>();
                pieces.Apply(player, from.Outbox.Requests.Select(r => r.Copy()).ToList(), 1, answers, changed);
                foreach (var a in answers) from.Receive(a);
                foreach (var piece in changed) from.Receive(piece.State());
            }

            /// <summary>방은 요청을 반영했지만 그 사람에게 가는 답과 상태가 사라졌다(보낸 뒤 끊김). 다른 사람은 상태를 받는다.</summary>
            public void DeliverAndLoseTheAnswers(RoomShares from)
            {
                var (player, _) = _players.First(p => p.Shares == from);
                var answers = new List<RoomAnswer>();
                var changed = new List<IRoomPiece>();
                Pieces.Apply(player, from.Outbox.Requests.Select(r => r.Copy()).ToList(), 1, answers, changed);
                foreach (var piece in changed)
                    foreach (var p in _players)
                        if (p.Shares != from) p.Shares.Receive(piece.State());
            }
        }

        private static GameSession Start(TempSaves saves, RoomShares shares, string slot, string player) => Start(saves.Store, shares, slot, player);

        private static GameSession Start(ISaveStore store, RoomShares shares, string slot, string player)
        {
            var game = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, store, slot, player, shares: shares);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            game.EnterHouseBuild(HouseRoom.Frame);
            return game;
        }

        private static GameSession Solo(TempSaves saves, string slot, string player)
        {
            var game = TestWorld.Start(saves.Store, slot, player);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            game.EnterHouseBuild(HouseRoom.Frame);
            return game;
        }

        private static IEnumerable<RoomFinishDefinition> PaidFloors()
            => TestWorld.Definitions.All<RoomFinishDefinition>()
                .Where(f => f.Slot == RoomFinishSlots.Floor && f.UnlockToken.Length == 0 && f.Id != HouseRoom.Frame.DefaultFloor && f.Cost.Count > 0);

        private static RoomFinishDefinition PaidFloor() => PaidFloors().First();
        private static RoomFinishDefinition OtherPaidFloor() => PaidFloors().Skip(1).First();

        private static RoomFinishDefinition LockedFloor()
            => TestWorld.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == RoomFinishSlots.Floor && f.UnlockToken.Length > 0);

        private static RoomFinishDefinition OtherBand()
            => TestWorld.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == RoomFinishSlots.Band && f.UnlockToken.Length == 0 && f.Art != HouseRoom.Frame.DefaultBand);

        private static int PerCell(RoomFinishDefinition finish) => finish.Cost.Where(c => c.Item == Allowance).Sum(c => c.Count);

        private static int Money(GameSession game) => game.Inventory.CountOf(game.Item(Allowance));

        private static void Give(GameSession game, int amount) => game.Inventory.Add(game.Item(Allowance), amount);

        // 개인 지갑(스냅샷에 저장된 것). 돈을 같이 쓰는 방에서도 개인 지갑은 세이브에 그대로 남는다.
        private static int PersonalWallet(TempSaves saves, string slot, string player)
            => saves.Store.TryRead<AdultCareerSnapshotSaveData>(slot, SaveFileNames.AdultCareerSnapshot(player), out var s) ? s.Wallet.Balance : -1;

        private static int HouseBuildRequests(RoomShares shares, string kind)
            => shares.Outbox.Requests.Count(r => r.Piece == RoomHouseBuild.PieceName && r.Kind == kind);

        private static string BuildPath(TempSaves saves, string slot) => Path.Combine(saves.Path, slot, SaveFileNames.HouseBuild);

        private static IReadOnlyList<CellPos> RoomCells(GameSession game, RoomFinishDefinition finish)
        {
            var preview = game.PreviewHouseFinish(finish.Id, HouseFinishTools.Room, TopLeftRoom);
            Assert.That(preview.CanApply, Is.True, "준비: " + preview.ReasonKey);
            return preview.Cells;
        }

        [Test]
        public void HOUSE_004_OneDecorates_EveryoneSeesIt_TheDecoratorPaysOnce()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            Assert.That(a.SharesHouseBuild, Is.True);
            Assert.That(a.SharesWallet, Is.False, "전제: 돈은 각자");
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            Give(a, cost + 5);
            int aMoney = Money(a), bMoney = Money(b);
            int bHouseBuild = 0;
            b.Changed += t => { if (t == GameChangeTopics.HouseBuild) bHouseBuild++; };

            var preview = a.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            Assert.That(preview.CanApply, Is.True, preview.ReasonKey);
            Assert.That(preview.ChangedCells.Count, Is.EqualTo(TopLeftRoomFill));

            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False, "지금 바뀐 것은 없다 — 방에 요청만 했다");
            Assert.That(a.WaitingForRoom, Is.True);
            Assert.That(a.ApplyHouseFinish(OtherPaidFloor().Id, HouseFinishTools.Brush, new CellPos(TopLeftRoom.X + 1, TopLeftRoom.Y)), Is.False);
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Finish), Is.EqualTo(1), "기다리는 동안 다른 꾸밈 요청을 적지 않는다");
            Assert.That(Money(a), Is.EqualTo(aMoney), "칸을 잡기 전에는 돈이 나가지 않는다");
            Assert.That(saves.Store.TryRead<RoomOutboxSaveData>("slot-a", SaveFileNames.RoomOutbox("a"), out var saved)
                && saved.Requests.Any(r => r.Piece == RoomHouseBuild.PieceName), Is.True, "요청은 세이브와 함께 간다");

            room.Deliver(aShares); // 방이 칸을 잡는다 → a가 낸다(쓰기를 같은 바꾸기에)
            Assert.That(a.WaitingForRoom, Is.False);
            Assert.That(Money(a), Is.EqualTo(aMoney - cost), "87칸 × 칸당 값만큼 한 번");
            Assert.That(a.LastRoomCommandApplied, Is.True);
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Use), Is.EqualTo(1));
            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "방이 쓰기를 받기 전에는 그대로");

            room.Deliver(aShares); // 쓰기
            foreach (var cell in preview.Cells)
            {
                Assert.That(a.HouseFloorAt(cell), Is.EqualTo(floor.Id));
                Assert.That(b.HouseFloorAt(cell), Is.EqualTo(floor.Id), "다른 사람에게도 보인다");
            }

            Assert.That(Money(b), Is.EqualTo(bMoney), "다른 사람의 돈은 그대로");
            Assert.That(Money(a), Is.EqualTo(aMoney - cost), "쓰기 뒤에도 한 번");
            Assert.That(bHouseBuild, Is.GreaterThan(0), "다른 사람에게 HouseBuild를 알린다(Host가 다시 조합한다)");
            Assert.That(room.Build.ToSaveData().Finishes.Length, Is.EqualTo(TopLeftRoomFill), "방의 기록 87");
            Assert.That(room.Build.HeldCount, Is.Zero);
            Assert.That(a.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange), "같은 요청은 바뀌는 칸이 없다");

            room.Deliver(aShares);
            room.Deliver(bShares);
            Assert.That(Money(a), Is.EqualTo(aMoney - cost), "다시 돌려도 더 나가지 않는다");
        }

        [Test]
        public void MoneyShared_TheRoomWalletPaysOnce_NotTheBag()
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, cost + 5, RoomWallet.PieceName, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            Assert.That(a.SharesWallet && a.SharesHouseBuild, Is.True);
            a.VisitLocation("location.town-square"); // 개인 지갑이 저장된 상태에서 비교
            b.VisitLocation("location.town-square");
            int aPersonal = PersonalWallet(saves, "slot-a", "a"), bPersonal = PersonalWallet(saves, "slot-b", "b");
            Assert.That(aPersonal, Is.GreaterThanOrEqualTo(0));

            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(aShares); // 칸 잡기 → 방 지갑 잡기 요청
            Assert.That(a.WaitingForRoom, Is.True, "방 지갑의 허락을 기다린다");
            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor));
            room.Deliver(aShares); // 방 지갑 허락 → 쓰기(꾸밈·지갑)
            Assert.That(a.WaitingForRoom, Is.False);
            Assert.That(a.LastRoomCommandApplied, Is.True);
            room.Deliver(aShares); // 쓰기가 방에 닿는다
            room.Deliver(aShares);

            Assert.That(room.Wallet.Balance, Is.EqualTo(5), "방 지갑에서 정확히 한 번");
            Assert.That(room.Wallet.HeldTotal, Is.Zero);
            Assert.That(room.Build.HeldCount, Is.Zero);
            Assert.That(b.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(PersonalWallet(saves, "slot-a", "a"), Is.EqualTo(aPersonal), "a의 개인 지갑 그대로");
            Assert.That(PersonalWallet(saves, "slot-b", "b"), Is.EqualTo(bPersonal), "b의 개인 지갑 그대로");
        }

        [Test]
        public void TwoAtOnce_SameRoom_OnlyTheFirstIsHeld_TheSecondPaysNothing()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            var first = PaidFloor();
            var second = OtherPaidFloor();
            Give(a, 10_000);
            Give(b, 10_000);
            int aMoney = Money(a), bMoney = Money(b);

            Assert.That(a.ApplyHouseFinish(first.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            Assert.That(b.ApplyHouseFinish(second.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(aShares); // 먼저 온 a가 칸을 잡는다
            room.Deliver(bShares); // b는 잡힌 칸이라 거절

            Assert.That(b.WaitingForRoom, Is.False);
            Assert.That(b.LastHouseFinishRefusal, Is.EqualTo(HouseFinishReasons.RoomRefused));
            Assert.That(b.LastRoomCommandApplied, Is.False);
            Assert.That(Money(b), Is.EqualTo(bMoney), "거절된 사람은 내지 않는다");
            Assert.That(a.LastHouseFinishRefusal, Is.Null);

            room.Deliver(aShares); // a의 쓰기
            room.Deliver(bShares);
            Assert.That(b.HouseFloorAt(TopLeftRoom), Is.EqualTo(first.Id), "먼저 온 것이 된다");
            Assert.That(Money(a), Is.EqualTo(aMoney - PerCell(first) * TopLeftRoomFill));
            Assert.That(Money(b), Is.EqualTo(bMoney));
            Assert.That(room.Build.HeldCount, Is.Zero);

            // 다음 꾸밈 요청이 거절 표시를 지운다.
            Assert.That(b.ApplyHouseFinish(second.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            Assert.That(b.LastHouseFinishRefusal, Is.Null);
        }

        [Test]
        public void AfterOthersChange_MyStaleRequest_IsRefused_AndMyPreviewIsFresh()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            var floor = PaidFloor();
            Give(a, 10_000);
            Give(b, 10_000);

            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False, "a는 87칸을 본 채로 요청했다");
            int aMoney = Money(a);
            Assert.That(b.ApplyHouseFinish(floor.Id, HouseFinishTools.Brush, TopLeftRoom), Is.False);
            room.Deliver(bShares);
            room.Deliver(bShares); // b의 한 칸이 먼저 적용됐다(a도 상태를 받는다)

            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(a.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom).ChangedCells.Count, Is.EqualTo(TopLeftRoomFill - 1), "a의 미리 보기는 새 상태로");

            room.Deliver(aShares); // 상태를 받기 전에 보낸 a의 요청: 칸 수가 달라 거절
            Assert.That(a.LastHouseFinishRefusal, Is.EqualTo(HouseFinishReasons.RoomRefused));
            Assert.That(Money(a), Is.EqualTo(aMoney), "돈 그대로");
            Assert.That(a.HouseFloorAt(new CellPos(TopLeftRoom.X + 1, TopLeftRoom.Y)), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "꾸밈 그대로");
            Assert.That(room.Build.HeldCount, Is.Zero);

            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False, "새 미리 보기로 다시");
            room.Deliver(aShares);
            room.Deliver(aShares);
            Assert.That(Money(a), Is.EqualTo(aMoney - PerCell(floor) * (TopLeftRoomFill - 1)), "바뀌는 86칸만 낸다");
        }

        [Test]
        public void MoneyRunsOutBeforeTheGrant_HoldReleased_NothingChanges()
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");
            Give(a, cost - Money(a));
            Assert.That(Money(a), Is.EqualTo(cost));

            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            Assert.That(a.Inventory.Remove(a.Item(Allowance), 1), Is.True, "준비: 답이 오기 전에 돈을 다른 데 썼다");
            room.Deliver(aShares); // 잡힘 → 낼 수 없다 → 풀기

            Assert.That(a.WaitingForRoom, Is.False);
            Assert.That(a.LastRoomCommandApplied, Is.False);
            Assert.That(a.LastHouseFinishRefusal, Is.Null, "방은 거절하지 않았다");
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Release), Is.EqualTo(1));
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Use), Is.Zero);
            room.Deliver(aShares);

            Assert.That(room.Build.HeldCount, Is.Zero, "잡힌 칸이 풀렸다");
            Assert.That(Money(a), Is.EqualTo(cost - 1), "돈 그대로");
            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "꾸밈 그대로");
            Assert.That(room.Build.ToSaveData().Finishes, Is.Empty);
        }

        [Test]
        public void RoomWalletRefuses_HoldReleased()
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, cost, RoomWallet.PieceName, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");

            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(aShares); // 칸 잡힘 → 방 지갑 잡기 요청
            Assert.That(a.WaitingForRoom, Is.True);
            room.SetWallet(saves, cost - 1); // 그 사이 다른 사람이 방 지갑을 썼다
            room.Deliver(aShares); // 방 지갑 거절 → 꾸밈 풀기

            Assert.That(a.WaitingForRoom, Is.False);
            Assert.That(a.LastRoomRefusal, Is.True);
            Assert.That(a.LastRoomCommandApplied, Is.False);
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Release), Is.EqualTo(1));
            room.Deliver(aShares);
            Assert.That(room.Build.HeldCount, Is.Zero, "잡힌 칸이 풀렸다");
            Assert.That(room.Wallet.Balance, Is.EqualTo(cost - 1));
            Assert.That(room.Wallet.HeldTotal, Is.Zero);
            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor));
        }

        [Test]
        public void RestartAfterRequest_BeforeTheAnswer_AnswerReplays_PaysOnce()
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");
            Give(a, cost + 5);
            int money = Money(a);
            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.DeliverAndLoseTheAnswers(aShares); // 방은 잡았지만 답이 사라졌다
            Assert.That(room.Build.HeldCount, Is.EqualTo(1));
            room.Leave(aShares);

            var againShares = room.Join("a"); // 다시 들어온다: 방의 상태(내 잡기 포함)를 받는다
            var again = Start(saves, againShares, "slot-a", "a"); // 같은 저장소, 보낼 목록을 읽는다
            Assert.That(again.WaitingForRoom, Is.True, "보낼 목록의 꾸밈 요청을 기다린다");
            Assert.That(HouseBuildRequests(againShares, RoomHouseBuild.Release), Is.Zero, "답을 기다리는 잡기는 풀지 않는다");
            Assert.That(Money(again), Is.EqualTo(money));

            room.Deliver(againShares); // 장부가 같은 답(잡음)을 다시 준다 → 그때 낸다
            Assert.That(Money(again), Is.EqualTo(money - cost));
            room.Deliver(againShares); // 쓰기
            room.Deliver(againShares);
            Assert.That(Money(again), Is.EqualTo(money - cost), "한 번만");
            Assert.That(again.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(room.Build.HeldCount, Is.Zero);
            Assert.That(TestWorld.Start(saves.Store, "slot-a", "a").Inventory.CountOf(again.Item(Allowance)), Is.EqualTo(money - cost), "저장된 돈도 한 번");
        }

        [Test]
        public void RestartAfterGrant_BeforePaying_MyStrayHoldIsReleased_NoMoney()
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, cost + 5, RoomWallet.PieceName, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");
            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(aShares); // 칸 잡힘 → 방 지갑 잡기 요청까지 저장
            Assert.That(a.WaitingForRoom, Is.True);
            Assert.That(room.Build.HeldCount, Is.EqualTo(1));
            room.Leave(aShares); // 방 지갑의 답을 받기 전에 꺼졌다

            var againShares = room.Join("a");
            var again = Start(saves, againShares, "slot-a", "a");
            Assert.That(HouseBuildRequests(againShares, RoomHouseBuild.Release), Is.EqualTo(1), "다시 할 명령이 없는 내 잡기를 푼다");
            Assert.That(saves.Store.TryRead<RoomOutboxSaveData>("slot-a", SaveFileNames.RoomOutbox("a"), out var outbox)
                && outbox.Requests.Any(r => r.Piece == RoomHouseBuild.PieceName && r.Kind == RoomHouseBuild.Release), Is.True, "풀기는 저장된다");

            room.Deliver(againShares); // 방 지갑 허락(다시 할 명령 없음 → 지갑 풀기), 꾸밈 풀기
            room.Deliver(againShares);
            room.Deliver(againShares);

            Assert.That(room.Build.HeldCount, Is.Zero);
            Assert.That(room.Wallet.Balance, Is.EqualTo(cost + 5), "돈 그대로");
            Assert.That(room.Wallet.HeldTotal, Is.Zero);
            Assert.That(again.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "꾸밈 그대로");
            Assert.That(HouseBuildRequests(againShares, RoomHouseBuild.Release), Is.Zero, "한 번만 풀었다");
            Assert.That(again.WaitingForRoom, Is.False);
        }

        [Test]
        public void MyLockedFinish_IsNotAskedFor_SomeoneElsesLockedFinish_StaysVisibleToMe()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            var locked = LockedFloor();
            Give(a, 10_000);
            Give(b, 10_000);

            Assert.That(a.PreviewHouseFinish(locked.Id, HouseFinishTools.Brush, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.Locked), "해금은 각자");
            Assert.That(a.ApplyHouseFinish(locked.Id, HouseFinishTools.Brush, TopLeftRoom), Is.False);
            Assert.That(a.WaitingForRoom, Is.False);
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Finish), Is.Zero, "잠긴 꾸밈은 방에 묻지 않는다");

            Assert.That(b.FurnitureUnlocks.TryUnlock(locked.UnlockToken), Is.True, "준비: b가 해금했다");
            Assert.That(b.ApplyHouseFinish(locked.Id, HouseFinishTools.Brush, TopLeftRoom), Is.False);
            room.Deliver(bShares);
            room.Deliver(bShares);
            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(locked.Id), "b가 칠한 칸은 a에게도 그 꾸밈으로 보인다");
            Assert.That(a.IsFinishUnlocked(locked), Is.False, "a의 해금은 그대로");
        }

        [Test]
        public void Band_GoesThroughTheRoom_AndIsFree()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            var band = OtherBand();
            Assert.That(band.Cost, Is.Empty, "전제: 띠 색은 무료");
            int money = Money(a);
            Assert.That(a.PreviewHouseBand(band.Id).CanApply, Is.True);

            Assert.That(a.ApplyHouseBand(band.Id), Is.False, "방에 요청만");
            Assert.That(a.WaitingForRoom, Is.True);
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Band), Is.EqualTo(1));
            Assert.That(a.HouseBandColors(), Is.EqualTo(HouseRoom.Frame.DefaultBand));
            room.Deliver(aShares);
            room.Deliver(aShares);

            Assert.That(a.HouseBandColors(), Is.EqualTo(band.Art));
            Assert.That(b.HouseBandColors(), Is.EqualTo(band.Art), "다른 사람에게도");
            Assert.That(Money(a), Is.EqualTo(money), "무료");
            Assert.That(room.Build.HeldCount, Is.Zero);
            Assert.That(a.PreviewHouseBand(band.Id).ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
        }

        [Test]
        public void Growth_GoesToTheDecorator_WithTheirOwnDailyLimit()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            var rule = TestWorld.Definitions.All<Rootborn.Core.Workspaces.PlacementSkillGrowth>().First(g => g.SurfaceId == HouseRoom.SurfaceId);
            Assert.That(rule.DailyLimit, Is.EqualTo(3), "전제: 하루 3번");
            Give(a, 10_000);
            int aSkill = a.Student.GetSkillValue(rule.SkillId), bSkill = b.Student.GetSkillValue(rule.SkillId);
            var floor = PaidFloor();

            for (int i = 0; i < 4; i++)
            {
                Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Brush, new CellPos(TopLeftRoom.X + i, TopLeftRoom.Y)), Is.False);
                Assert.That(a.Student.GetSkillValue(rule.SkillId), Is.EqualTo(aSkill + rule.Amount * i), "요청만으로는 자라지 않는다");
                room.Deliver(aShares);
                room.Deliver(aShares);
            }

            Assert.That(a.ApplyHouseBand(OtherBand().Id), Is.False);
            room.Deliver(aShares);
            room.Deliver(aShares);

            Assert.That(a.Student.GetSkillValue(rule.SkillId), Is.EqualTo(aSkill + rule.Amount * rule.DailyLimit), "칸 꾸밈 넷 중 하루 한도 셋, 띠 색은 세지 않는다");
            Assert.That(b.Student.GetSkillValue(rule.SkillId), Is.EqualTo(bSkill), "b의 기술은 그대로");
            Assert.That(TestWorld.Start(saves.Store, "slot-a", "a").Student.GetSkillValue(rule.SkillId), Is.EqualTo(aSkill + rule.Amount * rule.DailyLimit), "저장됐다");
        }

        [Test]
        public void OwnHouseBuildFile_NeitherShownNorWritten_InASharedHouse()
        {
            using var saves = new TempSaves();
            var own = PaidFloor();
            var solo = Solo(saves, "slot-a", "a");
            Give(solo, 10_000);
            Assert.That(solo.ApplyHouseFinish(own.Id, HouseFinishTools.Room, TopLeftRoom), Is.True, "준비: 자기 house-build.json");
            byte[] written = File.ReadAllBytes(BuildPath(saves, "slot-a"));

            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");
            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "같이 쓰는 집에 자기 꾸밈을 보이지 않는다");
            var other = OtherPaidFloor();
            Assert.That(a.ApplyHouseFinish(other.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(aShares);
            room.Deliver(aShares);
            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(other.Id));
            a.VisitLocation("location.town-square"); // 저장하는 명령
            Assert.That(File.ReadAllBytes(BuildPath(saves, "slot-a")), Is.EqualTo(written), "자기 house-build.json은 바이트 그대로");
        }

        [Test]
        public void HostsSoloDecor_IsNotCarriedIntoANewRoom_AndIsBackWhenPlayingAlone()
        {
            using var saves = new TempSaves();
            var own = PaidFloor();
            var solo = Solo(saves, "slot-host", "host");
            Give(solo, 10_000);
            Assert.That(solo.ApplyHouseFinish(own.Id, HouseFinishTools.Room, TopLeftRoom), Is.True);
            byte[] written = File.ReadAllBytes(BuildPath(saves, "slot-host"));

            var room = new FakeRoom(saves, 0, RoomHouse.PieceName); // 새 방: 빈 조각
            var hostShares = room.Join("host");
            var host = Start(saves, hostShares, "slot-host", "host");
            Assert.That(host.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "방은 기본 꾸밈으로 시작한다(가구와 같다)");
            var other = OtherPaidFloor();
            Assert.That(host.ApplyHouseFinish(other.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(hostShares);
            room.Deliver(hostShares);
            Assert.That(host.HouseFloorAt(TopLeftRoom), Is.EqualTo(other.Id));
            Assert.That(File.ReadAllBytes(BuildPath(saves, "slot-host")), Is.EqualTo(written));

            var back = Solo(saves, "slot-host", "host");
            Assert.That(back.HouseFloorAt(TopLeftRoom), Is.EqualTo(own.Id), "혼자 하면 자기 꾸밈 그대로");
            Assert.That(back.SharesHouseBuild, Is.False);
        }

        [Test]
        public void ReloadInTheRoom_SameFinishesAsBefore()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");
            Give(a, 10_000);
            var floor = PaidFloor();
            var cells = RoomCells(a, floor);
            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(aShares);
            room.Deliver(aShares);
            Assert.That(a.ApplyHouseBand(OtherBand().Id), Is.False);
            room.Deliver(aShares);
            room.Deliver(aShares);
            room.Leave(aShares);

            var againShares = room.Join("a");
            var again = Start(saves, againShares, "slot-a", "a");
            foreach (var cell in cells)
            {
                Assert.That(again.HouseFloorAt(cell), Is.EqualTo(a.HouseFloorAt(cell)));
                Assert.That(again.HouseWallAt(cell), Is.EqualTo(a.HouseWallAt(cell)));
            }

            Assert.That(again.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(again.HouseBandColors(), Is.EqualTo(OtherBand().Art));
            Assert.That(again.WaitingForRoom, Is.False, "기다릴 것이 없다");
            Assert.That(HouseBuildRequests(againShares, RoomHouseBuild.Release), Is.Zero, "풀 것이 없다");
            // 마지막 쓰기의 답은 저장을 부르지 않아 디스크의 보낼 목록에 그 쓰기가 남아 있을 수 있다 — 다시 가도 장부가 같은 답을 준다(두 번 반영되지 않는다).
            int revision = room.Build.Revision;
            room.Deliver(againShares);
            Assert.That(againShares.Outbox.Requests.Where(r => r.Piece == RoomHouseBuild.PieceName), Is.Empty);
            Assert.That(room.Build.Revision, Is.EqualTo(revision), "방은 바뀌지 않는다");
            Assert.That(again.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
        }

        [Test]
        public void UseRefusedByTheRoom_HoldStaysMine_AUseIsSentAgain_NeverReleased()
        {
            // 돈을 낸 잡기의 쓰기를 방이 적용하지 못하면(Core: 잡은 채로 Refused) 낸 돈이 사라지지 않게 쓰기를 새 id로 다시 보낸다 — 풀지 않는다.
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");
            Give(a, cost);
            int money = Money(a);
            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(aShares); // 잡힘 → 냈다(쓰기 요청)
            Assert.That(Money(a), Is.EqualTo(money - cost));
            string firstUse = aShares.Outbox.Requests.Single(r => r.Piece == RoomHouseBuild.PieceName && r.Kind == RoomHouseBuild.Use).RequestId;

            // 방이 다시 켜지면서 규칙(틀) 없이 읽혔다 → 쓰기를 적용할 수 없어 잡은 채로 거절한다.
            room.Pieces.Save(saves.Store, RoomSlot);
            var broken = new RoomPieceSet(room.Pieces.Settings, RoomPieceCatalog.CreateDefault());
            broken.Load(saves.Store, RoomSlot);
            var answers = new List<RoomAnswer>();
            var changed = new List<IRoomPiece>();
            broken.Apply("a", aShares.Outbox.Requests.Select(r => r.Copy()).ToList(), 1, answers, changed);
            Assert.That(answers.Single(x => x.RequestId == firstUse).Outcome, Is.EqualTo(RoomOutcomes.Refused), "전제: 쓰기 거절");
            foreach (var x in answers) aShares.Receive(x);
            foreach (var piece in changed) aShares.Receive(piece.State());

            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Release), Is.Zero, "낸 잡기는 풀지 않는다");
            var resent = aShares.Outbox.Requests.Single(r => r.Piece == RoomHouseBuild.PieceName && r.Kind == RoomHouseBuild.Use);
            Assert.That(resent.RequestId, Is.Not.EqualTo(firstUse), "새 요청 id로 다시");
            a.VisitLocation("location.town-square"); // 다음 저장에 함께 간다
            Assert.That(saves.Store.TryRead<RoomOutboxSaveData>("slot-a", SaveFileNames.RoomOutbox("a"), out var outbox)
                && outbox.Requests.Any(r => r.RequestId == resent.RequestId), Is.True);

            // 규칙이 돌아온 방(원래 room)이 다시 보낸 쓰기를 받으면 적용된다. 돈은 처음 한 번만.
            room.Pieces.Load(saves.Store, RoomSlot);
            room.Deliver(aShares);
            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(room.Build.HeldCount, Is.Zero);
            Assert.That(Money(a), Is.EqualTo(money - cost));
        }

        // ---------- 돈을 낸 뒤의 틈(검토 경우 4·5·11): 디스크에서 "돈을 냈다" ⇔ "그 잡기의 쓰기가 보낼 목록에" ----------

        private static Rootborn.Core.Workspaces.PlacementSkillGrowth HouseGrowth()
            => TestWorld.Definitions.All<Rootborn.Core.Workspaces.PlacementSkillGrowth>().First(g => g.SurfaceId == HouseRoom.SurfaceId);

        [TestCase(false)]
        [TestCase(true)]
        public void PaidThenOff_RestartSendsTheUse_NoRelease_MoneyAndGrowthOnce(bool moneyShared)
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var room = moneyShared
                ? new FakeRoom(saves, cost + 5, RoomWallet.PieceName, RoomHouse.PieceName)
                : new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");
            if (!moneyShared) Give(a, cost + 5);
            var rule = HouseGrowth();
            int skill = a.Student.GetSkillValue(rule.SkillId);

            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(aShares);                  // 잡힘 → (자기 지갑) 냈다
            if (moneyShared) room.Deliver(aShares); // (방 지갑) 허락 → 냈다
            Assert.That(a.LastRoomCommandApplied, Is.True, "전제: 돈을 낸 Save()까지 갔다");
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Use), Is.EqualTo(1));
            room.Leave(aShares); // 쓰기가 방에 닿기 전에 꺼졌다

            var againShares = room.Join("a");
            var again = Start(saves, againShares, "slot-a", "a");
            Assert.That(HouseBuildRequests(againShares, RoomHouseBuild.Release), Is.Zero, "돈을 낸 잡기는 떠도는 잡기가 아니다");
            Assert.That(HouseBuildRequests(againShares, RoomHouseBuild.Use), Is.EqualTo(1), "쓰기가 다시 간다");
            Assert.That(HouseBuildRequests(againShares, RoomHouseBuild.Finish), Is.Zero, "잡기 요청은 다시 가지 않는다(다시 낼 길이 없다)");

            room.Deliver(againShares); // 쓰기의 답이 상태보다 먼저 온다(서버 순서)
            Assert.That(HouseBuildRequests(againShares, RoomHouseBuild.Release), Is.Zero, "낡은 상태에 잡기가 남아 보여도 풀지 않는다");
            room.Deliver(againShares);
            Assert.That(again.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(room.Build.HeldCount, Is.Zero);
            if (moneyShared)
            {
                Assert.That(room.Wallet.Balance, Is.EqualTo(5), "방 지갑에서 한 번");
                Assert.That(room.Wallet.HeldTotal, Is.Zero);
            }
            else
            {
                Assert.That(Money(again), Is.EqualTo(5), "자기 지갑에서 한 번");
                Assert.That(TestWorld.Start(saves.Store, "slot-a", "a").Inventory.CountOf(again.Item(Allowance)), Is.EqualTo(5), "저장된 돈도 한 번");
            }

            Assert.That(again.Student.GetSkillValue(rule.SkillId), Is.EqualTo(skill + rule.Amount), "기술도 한 번");
        }

        // ---------- 돈을 낸 표시(room-house-build-paid-<플레이어>.json): 보낼 목록과 따로, 돈 빠짐과 한 묶음 ----------

        private static string PaidFile(string player) => SaveFileNames.RoomHouseBuildPaid(player);

        [TestCase(false)]
        [TestCase(true)]
        public void PaidRecord_WrittenWithThePayment_ForgottenOnTheNextSaveAfterTheUseApplies(bool moneyShared)
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var room = moneyShared
                ? new FakeRoom(saves, cost + 5, RoomWallet.PieceName, RoomHouse.PieceName)
                : new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");
            if (!moneyShared) Give(a, cost + 5);

            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            string hold = aShares.Outbox.Requests.Single(r => r.Piece == RoomHouseBuild.PieceName && r.Kind == RoomHouseBuild.Finish).RequestId;
            Assert.That(SlotFiles(saves, "slot-a").ContainsKey(PaidFile("a")), Is.False, "잡기 요청만으로는 표시가 없다");
            room.Deliver(aShares);                  // 잡힘 → (자기 지갑) 냈다
            if (moneyShared)
            {
                Assert.That(SlotFiles(saves, "slot-a").ContainsKey(PaidFile("a")), Is.False, "방 지갑의 허락을 기다리는 동안에는 아직 내지 않았다");
                room.Deliver(aShares);              // (방 지갑) 허락 → 냈다
            }

            Assert.That(a.LastRoomCommandApplied, Is.True, "전제: 냈다");
            Assert.That(saves.Store.TryRead<RoomHouseBuildPaidSaveData>("slot-a", PaidFile("a"), out var paid), Is.True, "돈을 낸 저장 묶음에 표시가 든다");
            Assert.That(paid.Holds, Is.EqualTo(new[] { hold }));

            room.Deliver(aShares); // 쓰기의 답(적용) → 상태(잡기 없음)
            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(room.Build.HeldCount, Is.Zero);
            a.VisitLocation("location.town-square"); // 다음 저장
            Assert.That(saves.Store.TryRead<RoomHouseBuildPaidSaveData>("slot-a", PaidFile("a"), out var after), Is.True);
            Assert.That(after.Holds, Is.Empty, "적용된 잡기의 표시는 다음 저장에서 지운다(끝없이 자라지 않는다)");
        }

        [Test]
        public void AHoldThatCouldNotBePaid_IsReleased_AndNeverMarkedPaid()
        {
            var floor = PaidFloor();
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");
            Give(a, PerCell(floor) * TopLeftRoomFill);
            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            Assert.That(a.Inventory.Remove(a.Item(Allowance), Money(a)), Is.True, "준비: 잡히기 전에 돈을 다 썼다");
            room.Deliver(aShares); // 잡힘 → 낼 수 없어 푼다
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Release), Is.EqualTo(1));
            room.Deliver(aShares);
            Assert.That(room.Build.HeldCount, Is.Zero);
            a.VisitLocation("location.town-square");
            Assert.That(SlotFiles(saves, "slot-a").ContainsKey(PaidFile("a")), Is.False, "돈을 내지 못한 잡기는 표시하지 않는다(파일도 없다)");
        }

        [Test]
        public void SoloHouse_NeverWritesThePaidFile()
        {
            var floor = PaidFloor();
            using var saves = new TempSaves();
            var a = Solo(saves, "slot-a", TestWorld.Player);
            Give(a, PerCell(floor) * TopLeftRoomFill);
            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.True, "혼자 하는 집은 바로 꾸민다");
            a.VisitLocation("location.town-square");
            Assert.That(SlotFiles(saves, "slot-a").Keys.Any(k => k.StartsWith("room-house-build-paid-")), Is.False, "혼자 하는 판은 표시 파일을 쓰지 않는다");
        }

        [Test]
        public void SaveThrowsWhilePaying_DiskUnchanged_NextSessionPaysOnce()
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var store = new SwitchableFailStore(saves.Store);
            var a = Start(store, aShares, "slot-a", "a");
            Give(a, cost + 5);
            var rule = HouseGrowth();
            int skill = a.Student.GetSkillValue(rule.SkillId);
            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            var before = SlotFiles(saves, "slot-a");

            store.FailCommits = true;
            Assert.Throws<IOException>(() => room.Deliver(aShares), "돈을 낸 Save()가 던진다");
            Assert.That(store.Failed, Is.True);
            Assert.That(Money(a), Is.EqualTo(5), "던진 저장은 돈을 낸 그 저장이다(메모리에서는 냈다)");
            var after = SlotFiles(saves, "slot-a");
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            Assert.That(after.ContainsKey(PaidFile("a")), Is.False, "디스크에 돈 낸 표시도 없다(돈 빠짐과 한 묶음)");
            foreach (var pair in before) Assert.That(after[pair.Key], Is.EqualTo(pair.Value), "디스크 그대로(스냅샷·보낼 목록): " + pair.Key);
            room.Leave(aShares);

            var againShares = room.Join("a");
            var again = Start(saves, againShares, "slot-a", "a");
            Assert.That(again.WaitingForRoom, Is.True, "디스크의 잡기 요청을 기다린다");
            room.Deliver(againShares); // 장부가 같은 답(잡음) → 이번에 낸다
            room.Deliver(againShares);
            room.Deliver(againShares);
            Assert.That(Money(again), Is.EqualTo(5), "한 번만");
            Assert.That(TestWorld.Start(saves.Store, "slot-a", "a").Inventory.CountOf(again.Item(Allowance)), Is.EqualTo(5));
            Assert.That(again.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(again.Student.GetSkillValue(rule.SkillId), Is.EqualTo(skill + rule.Amount), "기술도 한 번");
            Assert.That(room.Build.HeldCount, Is.Zero);
        }

        [Test]
        public void UseRefusedForever_RepeatedSaves_DoNotGrowTheRoomLedger_AStateChangeSendsItAgainAndItApplies()
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, 0, RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            Give(a, cost);
            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(aShares); // 잡힘 → 냈다
            Assert.That(Money(a), Is.Zero);

            // 같은 방이 규칙(틀) 없이 다시 켜졌다 → 쓰기를 영영 적용할 수 없다.
            room.Pieces.Save(saves.Store, RoomSlot);
            var broken = new RoomPieceSet(room.Pieces.Settings, RoomPieceCatalog.CreateDefault());
            broken.Load(saves.Store, RoomSlot);
            room.DeliverVia(broken, aShares); // 거절 → 이 판의 첫 거절이라 새 id로 한 번
            room.DeliverVia(broken, aShares); // 그 새 id도 거절
            int ledger = broken.Ledger.Count;
            for (int i = 0; i < 10; i++)
            {
                a.VisitLocation("location.town-square"); // 저장(보낼 목록이 다시 간다)
                room.DeliverVia(broken, aShares);
            }

            Assert.That(broken.Ledger.Count, Is.EqualTo(ledger), "방 상태가 그대로면 저장을 되풀이해도 방 장부가 늘지 않는다");
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Use), Is.EqualTo(1), "쓰기는 보낼 목록에 남는다(돈을 낸 표시)");
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Release), Is.Zero, "풀지 않는다");
            Assert.That(saves.Store.TryRead<RoomOutboxSaveData>("slot-a", SaveFileNames.RoomOutbox("a"), out var outbox)
                && outbox.Requests.Any(r => r.Piece == RoomHouseBuild.PieceName && r.Kind == RoomHouseBuild.Use), Is.True, "디스크에도");
            Assert.That(broken.TryGet(RoomHouseBuild.PieceName, out var brokenPiece) && ((RoomHouseBuild)brokenPiece).HeldCount == 1, Is.True, "잡기는 남는다");

            // 규칙을 가진 방으로 다시 켰다(장부 그대로 — 같은 id는 같은 거절). 방의 상태가 바뀌면(b가 띠를 잡는다) 새 id로 다시 보내 적용된다.
            broken.Save(saves.Store, RoomSlot);
            room.Pieces.Load(saves.Store, RoomSlot);
            room.Deliver(aShares);
            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "같은 id는 장부가 같은 거절을 준다");
            Assert.That(b.ApplyHouseBand(OtherBand().Id), Is.False);
            room.Deliver(bShares); // 방 상태가 바뀐다(a도 받는다)
            room.Deliver(aShares);
            room.Deliver(aShares);
            Assert.That(a.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "다시 보낸 쓰기가 적용됐다");
            Assert.That(Money(a), Is.Zero, "돈은 처음 한 번만");
            Assert.That(HouseBuildRequests(aShares, RoomHouseBuild.Use), Is.Zero);
        }

        [Test]
        public void HostPath_SessionIdDiffersFromTheRoomIdentity_PaidRestartSendsTheUse_HeldOnlyRestartReleases()
        {
            // 내 PC에서 방을 연 사람(Host StartSession의 손님 저장을 받지 않는 길): 게임 한 판은 기본 플레이어 id로, 방은 접속의 신원으로 요청·잡기를 적는다.
            // "내 잡기"는 방이 아는 신원(RoomShares.RoomPlayerId)으로 가린다.
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var room = new FakeRoom(saves, cost * 2 + 5, RoomWallet.PieceName, RoomHouse.PieceName);
            string self = SaveFileNames.DefaultPlayerId;
            var hostShares = room.Join("host-room-id");
            Assert.That(hostShares.RoomPlayerId, Is.Not.EqualTo(self), "전제: 두 id가 다르다");
            var host = Start(saves, hostShares, "slot-host", self);

            // 잡기만 하고(방 지갑 허락을 기다리는 중) 끔 → 다시 켬: 떠도는 내 잡기로 푼다.
            Assert.That(host.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(hostShares);
            Assert.That(room.Build.HeldCount, Is.EqualTo(1));
            room.Leave(hostShares);
            var s2 = room.Join("host-room-id");
            var host2 = Start(saves, s2, "slot-host", self);
            Assert.That(HouseBuildRequests(s2, RoomHouseBuild.Release), Is.EqualTo(1), "방이 아는 신원으로 내 잡기를 찾아 푼다");
            room.Deliver(s2);
            room.Deliver(s2);
            Assert.That(room.Build.HeldCount, Is.Zero);
            Assert.That(room.Wallet.Balance, Is.EqualTo(cost * 2 + 5));

            // 잡기 → 돈 냄 → 끔 → 다시 켬: 쓰기가 다시 가고 풀지 않는다.
            Assert.That(host2.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(s2);
            room.Deliver(s2);
            Assert.That(host2.LastRoomCommandApplied, Is.True);
            room.Leave(s2);
            var s3 = room.Join("host-room-id");
            var host3 = Start(saves, s3, "slot-host", self);
            Assert.That(HouseBuildRequests(s3, RoomHouseBuild.Release), Is.Zero);
            Assert.That(HouseBuildRequests(s3, RoomHouseBuild.Use), Is.EqualTo(1));
            room.Deliver(s3);
            room.Deliver(s3);
            Assert.That(host3.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(room.Wallet.Balance, Is.EqualTo(cost + 5), "한 번");
            Assert.That(room.Build.HeldCount, Is.Zero);
        }

        private static Dictionary<string, byte[]> SlotFiles(TempSaves saves, string slot)
        {
            var dir = Path.Combine(saves.Path, slot);
            return Directory.Exists(dir) ? Directory.GetFiles(dir).ToDictionary(Path.GetFileName, File.ReadAllBytes) : new Dictionary<string, byte[]>();
        }

        /// <summary>FileSaveStore를 감싸고, 켜면 묶음 커밋에서 던지는 저장소(묶음을 버리므로 파일은 옛것 그대로).</summary>
        private sealed class SwitchableFailStore : IBatchSaveStore
        {
            private readonly FileSaveStore _inner;
            public SwitchableFailStore(FileSaveStore inner) => _inner = inner;
            public bool FailCommits { get; set; }
            public bool Failed { get; private set; }

            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new() => _inner.TryRead(saveSlot, fileName, out data);
            public void Write<T>(string saveSlot, string fileName, T data) where T : class => _inner.Write(saveSlot, fileName, data);
            public void Delete(string saveSlot, string fileName) => _inner.Delete(saveSlot, fileName);
            public ISaveBatch BeginBatch(string saveSlot) => new Batch(this, _inner.BeginBatch(saveSlot));

            private sealed class Batch : ISaveBatch
            {
                private readonly SwitchableFailStore _owner;
                private readonly ISaveBatch _inner;
                public Batch(SwitchableFailStore owner, ISaveBatch inner) { _owner = owner; _inner = inner; }
                public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new() => _inner.TryRead(saveSlot, fileName, out data);
                public void Write<T>(string saveSlot, string fileName, T data) where T : class => _inner.Write(saveSlot, fileName, data);
                public void Delete(string saveSlot, string fileName) => _inner.Delete(saveSlot, fileName);
                public void Commit()
                {
                    if (_owner.FailCommits)
                    {
                        _owner.Failed = true;
                        throw new IOException("시험: 묶음 커밋 실패");
                    }

                    _inner.Commit();
                }

                public void Dispose() => _inner.Dispose();
            }
        }
    }
}
