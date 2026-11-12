using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Workspaces;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 일터 꾸미기 화면이 읽는 조회: 지금 열린 일터의 상태, 가구를 더 놓을 수 있는지와 못 놓는 이유, 같이 쓰는 집이 열려 있는지.
    /// 판단은 Core(일터 한도·주인)가 하고 화면은 결과 종류만 받는다. 실제 data/와 임시 저장소(자동 테스트).
    /// </summary>
    public sealed class WorkspaceScreenQueryTests
    {
        private const string ChefWorkspace = "workspace.chef";
        private const string ChefFacility = "facility.chef";
        private const string FreeDesk = "growth31.assembly.desk";

        private static Workspace Chef => TestWorld.Definitions.Get<Workspace>(ChefWorkspace);

        private static void EnterChef(GameSession game) => game.EnterPlacementSurface(WorkspaceRoom.GridFor(game.Definitions, ChefWorkspace), Chef.SurfaceId);

        [Test]
        public void ActiveWorkspaceStatus_IsNullInTheHouse_AndIsTheOpenWorkspaceInside()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.ActiveWorkspaceStatus, Is.Null);

            EnterChef(game);
            var status = game.ActiveWorkspaceStatus;
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Workspace, Is.SameAs(game.ActiveWorkspace));
            Assert.That(status.Placed, Is.EqualTo(game.House.Placed.Count));
            Assert.That(status.Capacity, Is.EqualTo(game.WorkspaceOf(ChefFacility).Capacity));
            Assert.That(status.Owned, Is.True);
            Assert.That(status.Scores.All.Select(s => (s.QualityId, s.Score)), Is.EqualTo(game.WorkspaceOf(ChefFacility).Scores.All.Select(s => (s.QualityId, s.Score))));
        }

        [Test]
        public void CanAddToActiveWorkspace_SaysWhy()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.CanAddToActiveWorkspace(), Is.EqualTo(WorkspaceResultKind.InvalidRequest), "집은 일터가 아니다");

            EnterChef(game);
            Assert.That(game.CanAddToActiveWorkspace(), Is.EqualTo(WorkspaceResultKind.Applied));
            while (game.CanAddToActiveWorkspace() == WorkspaceResultKind.Applied)
                Assert.That(game.PlaceFurniture(FreeDesk, WorkspaceRoom.FreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            Assert.That(game.CanAddToActiveWorkspace(), Is.EqualTo(WorkspaceResultKind.OverCapacity));
            Assert.That(game.House.Placed.Count, Is.EqualTo(game.ActiveWorkspaceStatus.Capacity), "한도에 닿을 때까지 놓였다");
        }

        [Test]
        public void CanAddToActiveWorkspace_IsNotOwner_ForAStudent()
        {
            using var saves = new TempSaves();
            var student = TestWorld.Start(saves.Store);
            EnterChef(student);
            Assert.That(student.CanAddToActiveWorkspace(), Is.EqualTo(WorkspaceResultKind.NotOwner));
            Assert.That(student.ActiveWorkspaceStatus.Owned, Is.False);
        }
    }
}
