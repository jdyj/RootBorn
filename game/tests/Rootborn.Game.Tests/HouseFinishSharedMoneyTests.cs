using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.World;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// HOUSE-001(돈만 같이 쓰는 방): 집은 각자, 지갑은 방 지갑. 미리 보기는 방 지갑 잔액으로 판단하고(요청을 적지 않는다), 적용은 방에 돈 잡기를
    /// 요청해 허락이 온 뒤에만 꾸밈과 돈이 함께 바뀐다. 방 쪽은 Core RoomPieceSet을 테스트가 손으로 돌린다(GameSession 명령으로 진행하는 자동 테스트).
    /// </summary>
    public sealed class HouseFinishSharedMoneyTests
    {
        private const string Allowance = "item.allowance";
        private const int TopLeftRoomFill = 87; // HouseFinishTests와 같은 방(방 바탕 84 + 맞닿은 문간 3)
        private static readonly CellPos TopLeftRoom = HouseRoom.TopLeftRoom;

        private static RoomFinishDefinition PaidFloor()
        {
            var frame = HouseRoom.Frame;
            return TestWorld.Definitions.All<RoomFinishDefinition>()
                .First(f => f.Slot == RoomFinishSlots.Floor && f.UnlockToken.Length == 0 && f.Id != frame.DefaultFloor && f.Cost.Count > 0);
        }

        private static int PerCell(RoomFinishDefinition finish) => finish.Cost.Where(c => c.Item == Allowance).Sum(c => c.Count);

        [Test]
        public void Preview_FollowsTheRoomWallet_WithoutAsking()
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            Assert.That(cost, Is.GreaterThan(0), "전제: 값이 있는 꾸밈");

            using var saves = new TempSaves();
            var (game, _, shares, _) = StartInMoneyRoom(saves, cost);
            int outbox = shares.Outbox.Requests.Count;
            var preview = game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            Assert.That(preview.CanApply, Is.True, preview.ReasonKey);
            Assert.That(preview.ReasonKey, Is.Null.Or.Empty, "이유 없음");
            Assert.That(preview.ChangedCells.Count, Is.EqualTo(TopLeftRoomFill));
            Assert.That(shares.Outbox.Requests.Count, Is.EqualTo(outbox), "미리 보기는 방에 요청하지 않는다");
            Assert.That(game.WaitingForRoom, Is.False);

            using var poorSaves = new TempSaves();
            var (poor, _, _, _) = StartInMoneyRoom(poorSaves, cost - 1);
            Assert.That(poor.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.NotEnoughMoney), "방 지갑이 1 모자라다");
        }

        [Test]
        public void Apply_AfterTheRoomAllows_ChangesOnce_PaysTheCostOnce_GrowsOnce()
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var (game, pieces, shares, wallet) = StartInMoneyRoom(saves, cost + 5);
            var rule = TestWorld.Definitions.All<Rootborn.Core.Workspaces.PlacementSkillGrowth>().First(g => g.SurfaceId == HouseRoom.SurfaceId);
            int skill = game.Student.GetSkillValue(rule.SkillId);
            // 허락 뒤 다시 하는 적용이 알리는 동안(지갑 범위 안) 화면이 미리 보기를 불러도 된다.
            game.Changed += _ => game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);

            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False, "처음에는 잡기를 요청한다");
            Assert.That(game.WaitingForRoom, Is.True);
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "허락 전에는 바뀌지 않는다");

            Assert.DoesNotThrow(() => Deliver(pieces, shares));
            Assert.That(game.WaitingForRoom, Is.False);
            Assert.That(game.LastRoomCommandApplied, Is.True);
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Deliver(pieces, shares); // 쓰기 요청이 방에 닿는다
            Assert.That(wallet.Balance, Is.EqualTo(5), "정확히 비용만큼 한 번");
            Assert.That(wallet.HeldTotal, Is.EqualTo(0));
            Assert.That(game.Student.GetSkillValue(rule.SkillId), Is.EqualTo(skill + rule.Amount), "기술은 한 번");

            int requests = shares.Outbox.Requests.Count;
            Assert.That(game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False, "같은 요청은 바뀌는 칸이 없다");
            Assert.That(game.WaitingForRoom, Is.False, "같은 요청은 방에 묻지 않는다");
            Assert.That(shares.Outbox.Requests.Count, Is.EqualTo(requests));
            Deliver(pieces, shares);
            Assert.That(wallet.Balance, Is.EqualTo(5), "추가 지출 없음");
            Assert.That(game.Student.GetSkillValue(rule.SkillId), Is.EqualTo(skill + rule.Amount));

            var again = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, TestWorld.Slot, TestWorld.Player);
            again.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            again.EnterHouseBuild(HouseRoom.Frame);
            Assert.That(again.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "저장됐다");
        }

        [Test]
        public void RoomRefuses_NothingChanges()
        {
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            using var saves = new TempSaves();
            var (game, pieces, shares, wallet) = StartInMoneyRoom(saves, cost);
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            Assert.That(game.WaitingForRoom, Is.True);

            // 기다리는 동안 다른 사람이 방 지갑을 썼다(방 잔액이 비용보다 적다).
            saves.Store.Write("room", SaveFileNames.RoomPiece(RoomWallet.PieceName), new RoomWalletSaveData { Version = RoomWalletSaveData.KnownVersion, Balance = cost - 1 });
            wallet.Load(saves.Store, "room");

            Deliver(pieces, shares);
            Assert.That(game.WaitingForRoom, Is.False);
            Assert.That(game.LastRoomCommandApplied, Is.False);
            Assert.That(game.LastRoomRefusal, Is.True);
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "꾸밈 그대로");
            Assert.That(wallet.Balance, Is.EqualTo(cost - 1), "지갑 그대로");
            Assert.That(wallet.HeldTotal, Is.EqualTo(0));
        }

        // 방 채우기를 요청한 뒤(허락 대기) 같은 방의 한 칸을 먼저 칠했다: 허락이 올 때 바뀔 칸이 하나 줄어 비용이 잡은 돈과 다르다 → 적용하지 않고 잡은 돈을 푼다.
        [Test]
        public void CostChangedWhileWaiting_ReleasesTheHold_NothingChanges()
        {
            var floor = PaidFloor();
            int perCell = PerCell(floor);
            int fill = perCell * TopLeftRoomFill;
            using var saves = new TempSaves();
            var (game, pieces, shares, wallet) = StartInMoneyRoom(saves, perCell + fill);

            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Brush, TopLeftRoom), Is.False, "한 칸: 잡기 요청");
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False, "방 전체: 잡기 요청");
            Deliver(pieces, shares); // 둘 다 허락 — 한 칸이 먼저 적용되고, 방 전체는 이제 86칸이라 잡은 87칸 값과 다르다
            Assert.That(game.WaitingForRoom, Is.False);
            Assert.That(game.LastRoomCommandApplied, Is.False, "방 전체는 되지 않았다");
            Deliver(pieces, shares); // 쓰기·풀기 요청이 방에 닿는다

            Assert.That(wallet.HeldTotal, Is.EqualTo(0), "잡은 돈이 풀렸다");
            Assert.That(wallet.Balance, Is.EqualTo(fill), "한 칸 값만 빠졌다");
            var preview = game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            Assert.That(preview.ChangedCells.Count, Is.EqualTo(TopLeftRoomFill - 1), "방의 나머지 칸은 그대로");
            Assert.That(preview.CanApply, Is.True, "남은 돈으로 나머지 칸을 낼 수 있다");
        }

        // 학생이 돈을 같이 쓰는 방(방 지갑 balance)에 들어와 자기 집(짓는 집)에 선다.
        private static (GameSession, RoomPieceSet, RoomShares, RoomWallet) StartInMoneyRoom(TempSaves saves, int balance)
        {
            var pieces = new RoomPieceSet(new RoomShareSettings(new[] { RoomWallet.PieceName }), RoomPieceCatalog.CreateDefault());
            saves.Store.Write("room", SaveFileNames.RoomPiece(RoomWallet.PieceName), new RoomWalletSaveData { Version = RoomWalletSaveData.KnownVersion, Balance = balance });
            pieces.TryGet(RoomWallet.PieceName, out var walletPiece);
            var wallet = (RoomWallet)walletPiece;
            wallet.Load(saves.Store, "room");
            int ids = 0;
            var shares = new RoomShares(pieces.Settings, RoomPieceCatalog.CreateDefault(), () => "r" + (++ids));
            foreach (var piece in pieces.Pieces) shares.Receive(piece.State());
            var game = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, TestWorld.Slot, TestWorld.Player, shares: shares);
            Assert.That(game.SharesWallet, Is.True);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            game.EnterHouseBuild(HouseRoom.Frame);
            Assert.That(game.HouseBuildOpen, Is.True, "돈만 같이 쓰는 방: 집은 자기 것");
            Assert.That(game.Inventory.CountOf(game.Item(Allowance)), Is.EqualTo(balance), "돈은 방 지갑");
            return (game, pieces, shares, wallet);
        }

        private static void Deliver(RoomPieceSet pieces, RoomShares shares)
        {
            var answers = new List<RoomAnswer>();
            var changed = new List<IRoomPiece>();
            pieces.Apply(TestWorld.Player, shares.Outbox.Requests.Select(r => r.Copy()).ToList(), 1, answers, changed);
            foreach (var a in answers) shares.Receive(a);
            foreach (var piece in changed) shares.Receive(piece.State());
        }
    }
}
