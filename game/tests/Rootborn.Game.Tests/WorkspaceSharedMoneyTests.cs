using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.Workspaces;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 돈을 같이 쓰는 방에서 일터 템플릿 적용: 방의 허락을 기다리는 동안 일터를 떠나면 허락이 와도 아무것도 바꾸지 않고 잡은 돈을 푼다.
    /// 방 쪽은 Core RoomPieceSet을 테스트가 손으로 돌린다(GameSession 명령으로 진행하는 자동 테스트).
    /// </summary>
    public sealed class WorkspaceSharedMoneyTests
    {
        private const string CozyTemplate = "template.chef.cozy";

        [Test]
        public void WaitingTemplate_StayingInTheWorkspace_AppliesOnceAfterTheRoomAllows()
        {
            using var saves = new TempSaves();
            var (game, pieces, shares, wallet) = StartInMoneyRoom(saves);
            var chef = game.Definitions.Get<Workspace>("workspace.chef");
            game.EnterPlacementSurface(WorkspaceRoom.GridFor(game.Definitions, chef.Id), chef.SurfaceId);
            var template = game.Definitions.Get<WorkspaceTemplate>(CozyTemplate);
            int cost = template.Costs.Sum(c => c.Count);

            game.ApplyTemplate(CozyTemplate, "req:cozy");
            Assert.That(game.WaitingForRoom, Is.True);
            Deliver(pieces, shares);
            Assert.That(game.LastRoomCommandApplied, Is.True);
            Assert.That(game.House.Placed.Count, Is.EqualTo(template.Placements.Count));
            Deliver(pieces, shares);
            Assert.That(wallet.Balance, Is.EqualTo(100 - cost), "돈은 한 번만");
            Assert.That(game.ApplyTemplate(CozyTemplate, "req:cozy"), Is.EqualTo(WorkspaceResultKind.DuplicateRequest));
        }

        /// <summary>
        /// 확인(CanApplyTemplate)은 방 지갑으로 낼 수 있으면 된다고 말한다 — 화면이 적용 단추를 켤 수 있게. 방 지갑은 범위 밖의 돈 쓰기를 늘 거절하므로
        /// 확인도 확인용 범위 안에서 본다. 확인은 방에 요청하지 않고 아무것도 바꾸지 않는다. 모자라면 InsufficientResources.
        /// </summary>
        [Test]
        public void CanApplyTemplate_InAMoneySharingRoom_FollowsTheRoomWallet_WithoutAsking()
        {
            using var saves = new TempSaves();
            var (game, pieces, shares, wallet) = StartInMoneyRoom(saves);
            var chef = game.Definitions.Get<Workspace>("workspace.chef");
            game.EnterPlacementSurface(WorkspaceRoom.GridFor(game.Definitions, chef.Id), chef.SurfaceId);
            int outbox = shares.Outbox.Requests.Count;

            Assert.That(game.CanApplyTemplate(CozyTemplate), Is.EqualTo(WorkspaceResultKind.Applied), "방 지갑 100이면 낼 수 있다");
            Assert.That(shares.Outbox.Requests.Count, Is.EqualTo(outbox), "확인은 방에 요청하지 않는다");
            Assert.That(game.WaitingForRoom, Is.False);
            Assert.That(game.ApplyTemplate(CozyTemplate, "req:cozy"), Is.Not.EqualTo(WorkspaceResultKind.Applied), "확인 뒤에도 적용은 방의 허락을 기다린다");
            Assert.That(game.WaitingForRoom, Is.True);
            // 허락 뒤 다시 하는 적용이 알리는 동안(지갑 범위 안) 화면이 확인을 불러도 된다.
            game.Changed += _ => game.CanApplyTemplate(CozyTemplate);
            Assert.DoesNotThrow(() => Deliver(pieces, shares));
            Assert.That(game.LastRoomCommandApplied, Is.True);

            using var poorSaves = new TempSaves();
            var poor = StartInMoneyRoom(poorSaves, balance: 0);
            poor.Item1.EnterPlacementSurface(WorkspaceRoom.GridFor(poor.Item1.Definitions, chef.Id), chef.SurfaceId);
            Assert.That(poor.Item1.CanApplyTemplate(CozyTemplate), Is.EqualTo(WorkspaceResultKind.InsufficientResources), "방 지갑이 비면 모자라다");
        }

        // 집으로 나가거나, 나갔다가 같은 일터에 다시 들어와 새 배치가 열린 경우(데이터의 일터가 하나라 "다른 일터 면"은 이 길로 본다).
        [TestCase(false)]
        [TestCase(true)]
        public void WaitingTemplate_AfterLeavingTheWorkspace_IsRefusedWithoutChange_AndTheHoldIsReleased(bool reEnter)
        {
            using var saves = new TempSaves();
            var (game, pieces, shares, wallet) = StartInMoneyRoom(saves);

            var chef = game.Definitions.Get<Workspace>("workspace.chef");
            game.EnterPlacementSurface(WorkspaceRoom.GridFor(game.Definitions, chef.Id), chef.SurfaceId);
            var placedBefore = game.House.Placed.Select(p => p.Definition.StableId).ToArray();
            Assert.That(placedBefore, Is.Not.Empty, "기본 템플릿이 깔렸다");
            game.ApplyTemplate(CozyTemplate, "req:cozy");
            Assert.That(game.WaitingForRoom, Is.True, "방의 허락을 기다린다");

            game.EnterPlacementSurface(HouseRoom.Grid, HouseRoom.SurfaceId); // 기다리는 동안 집으로 걸어 나간다
            Assert.That(game.ActiveWorkspace, Is.Null);
            if (reEnter) game.EnterPlacementSurface(WorkspaceRoom.GridFor(game.Definitions, chef.Id), chef.SurfaceId);

            Assert.DoesNotThrow(() => Deliver(pieces, shares));
            Assert.That(game.WaitingForRoom, Is.False);
            Assert.That(game.LastRoomCommandApplied, Is.False, "기다리던 적용은 되지 않았다");
            Deliver(pieces, shares); // 풀기 요청이 방에 닿는다
            Assert.That(wallet.Balance, Is.EqualTo(100), "돈은 그대로");
            Assert.That(wallet.HeldTotal, Is.EqualTo(0), "잡은 돈이 풀렸다");

            Assert.That(saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.WorkspaceLayout(chef.SurfaceId, TestWorld.Player), out var layout), Is.True);
            Assert.That(layout.Items.Select(i => i.FurnitureId), Is.EquivalentTo(placedBefore), "일터 배치 그대로");
            var again = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, TestWorld.Slot, TestWorld.Player);
            again.EnterPlacementSurface(WorkspaceRoom.GridFor(again.Definitions, chef.Id), chef.SurfaceId);
            Assert.That(again.CanApplyTemplate(CozyTemplate), Is.Not.EqualTo(WorkspaceResultKind.DuplicateRequest));
            Assert.That(again.ApplyTemplate(CozyTemplate, "req:cozy"), Is.Not.EqualTo(WorkspaceResultKind.DuplicateRequest), "요청은 적용된 것으로 적히지 않았다");
        }

        // 요리사 성인이 돈을 같이 쓰는 방(방 지갑 100)에 들어온다.
        private static (GameSession, RoomPieceSet, RoomShares, RoomWallet) StartInMoneyRoom(TempSaves saves, int balance = 100)
        {
            ChefSetup.Adult(saves);
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
            return (game, pieces, shares, wallet);
        }

        // 방이 보낼 요청을 받아 반영하고(저장을 받은 것과 같다), 답과 바뀐 조각의 상태를 돌려준다.
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
