using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Workspaces;
using Rootborn.Game;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Tests.AdultFacility;
using Rootborn.Screens.Tests.Furniture;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Workspaces
{
    /// <summary>
    /// 일터 꾸미기 화면 시험 준비: 성인 요리사(또는 학생) 한 판(FacilityFixture와 같은 저장 고치기)에서 요리사 일터 면에 들어가
    /// HUD 단추로 가구 배치 화면을 연다. 방은 시험용 맵(벽으로 둘러싼, 데이터의 템플릿 칸이 다 들어가는 크기)이고 월드 좌표는 칸 48픽셀 시험용 보기다(Host가 카메라로 하는 일).
    /// 실제 일터 맵·카메라·그림은 Host 시나리오가 본다. 창 없이 화면을 눌러 보는 자동 테스트다.
    /// </summary>
    internal sealed class WorkspaceFixture : IDisposable
    {
        public const string ChefWorkspace = "workspace.chef";
        public const string ChefWork = "work.chef.level1";
        public const string CozyTemplate = "template.chef.cozy";
        public const string BasicTemplate = "template.chef.basic";
        public const string Cozy = "quality.cozy";
        public const string Allowance = "item.allowance";
        public const string FreeDesk = "growth31.assembly.desk";

        public readonly FacilityFixture F;
        public readonly FurnitureRoom Room = new FurnitureRoom(RoomCoveringTemplates());

        /// <summary>
        /// 시험용 방: 데이터의 일터 템플릿 칸(가구 칸 좌표, 실제 일터 맵에 맞춘 값)이 모두 들어가고 둘레에 여유가 있는 크기. 최소 10x8.
        /// 템플릿 칸이 바뀌어도(맵을 새로 그리면) 이 시험이 따라간다.
        /// </summary>
        private static Rootborn.Core.World.WorldMap RoomCoveringTemplates()
        {
            int maxX = 0, maxY = 0;
            foreach (WorkspaceTemplate t in TestSession.Definitions.All<WorkspaceTemplate>())
                foreach (TemplatePlacement p in t.Placements) { maxX = System.Math.Max(maxX, p.Anchor.X); maxY = System.Math.Max(maxY, p.Anchor.Y); }
            return FurniturePlacementScreenTests.TestRoomMap(System.Math.Max(10, maxX + 4), System.Math.Max(8, maxY + 4));
        }
        public readonly FurniturePlacementScreenTests.TestView View;

        /// <param name="adult">요리사 시설 1단계 성인(자기 일터). 거짓이면 학생(남의 일터).</param>
        /// <param name="allowance">용돈(템플릿 값). null이면 새 게임 그대로.</param>
        public WorkspaceFixture(bool adult = true, int? allowance = null, bool longText = false, string language = "ko",
            Rootborn.Core.Room.RoomShares shares = null, string playerId = null, bool open = true)
        {
            F = new FacilityFixture(s =>
            {
                if (adult) FacilityFixture.AlreadyAdultAtLevelOne(s);
                if (allowance.HasValue) FacilityFixture.SetCount(s, Allowance, allowance.Value);
            }, language: language, candidateReady: adult, longText: longText, shares: shares, playerId: playerId);
            View = new FurniturePlacementScreenTests.TestView(Room);
            if (open) EnterWorkspace();
        }

        public GameSession Session => F.Session;
        public ScreenStack Stack => F.Stack;
        public GameText Text => F.Text;
        public FurniturePlacementScreen Placement => (FurniturePlacementScreen)Stack.Find(ScreenCatalog.FurniturePlacement);

        /// <summary>Host가 하는 일: 일터 면에 들어가 맵 성질을 넘기고, 집 HUD의 가구 배치 단추를 누른다.</summary>
        public void EnterWorkspace()
        {
            Session.EnterPlacementSurface(Room.Grid, Definitions.Get<Workspace>(ChefWorkspace).SurfaceId);
            Stack.EnterMap("Workspace", new[] { MapFeatures.FurniturePlacement });
            F.Click("hud.button.placement");
            Placement.Attach(new FurnitureArt(TestData.DataDirectory), View);
            F.Emit();
        }

        public Rootborn.Data.GameDefinitions Definitions => Session.Definitions;

        public UiNode Node(string id) => F.Node(id);
        public void Click(string id) => F.Click(id);
        public DrawList Emit() => F.Emit();

        public string Label(string id) => Node(id) is UiLabel l ? l.Text : Node(id) is UiTextBlock b ? b.Text : ((UiButton)Node(id)).Text;

        /// <summary>Host가 하는 일: 포인터가 UI 위인지 UiTree로 보고, 아니면 월드 포인터로 넘긴다.</summary>
        public void ClickCell(GridPoint cell)
        {
            (float x, float y) = View.Center(cell);
            foreach (bool down in new[] { false, true, false })
            {
                Stack.Pointer(x, y, down, 0);
                Placement.WorldPointer(x, y, down, Stack.Tree.HitTest(x, y) != null);
            }
        }

        /// <summary>검색 칸을 눌러 글자를 치고 팔레트에서 그 가구 칸을 누른다.</summary>
        public void PickFromPalette(string furnitureId)
        {
            Click(FurniturePlacementScreen.SearchId);
            var field = (UiTextField)Node(FurniturePlacementScreen.SearchId);
            while (field.Text.Length > 0) Stack.TextBackspace();
            Stack.TextInput(furnitureId);
            Emit();
            for (int i = 0; i < FurniturePlacementScreen.Metrics.PageSize; i++)
                if (Placement.Palette.At(i)?.StableId == furnitureId) { Click(FurniturePlacementScreen.SlotId(i)); return; }
            Assert.Fail("팔레트에 없다: " + furnitureId);
        }

        /// <summary>지금 열린 배치에서 그 가구를 놓을 수 있는 첫 칸(방 바닥 칸 순서).</summary>
        public GridPoint FreeCell(string furnitureId)
        {
            FurnitureDefinition furniture = Definitions.Get<FurnitureDefinition>(furnitureId);
            foreach (GridPoint cell in Room.FloorCells)
                if (Session.House.CanPlace(furniture, cell, FurniturePlacementDirection.North)) return cell;
            throw new InvalidOperationException(furnitureId + "를 놓을 칸이 없다");
        }

        /// <summary>준비: 한도까지 아무 가구나 놓는다(GameSession 명령 — 확인 대상이 아니다).</summary>
        public void FillToCapacity()
        {
            while (Session.CanAddToActiveWorkspace() == WorkspaceResultKind.Applied)
                Assert.That(Session.PlaceFurniture(FreeDesk, FreeCell(FreeDesk), FurniturePlacementDirection.North), Is.True);
        }

        /// <summary>그 성질을 주는 가구(점수 높은 순, 아직 놓지 않은 것). 해금이 필요 없는 집 가구 중에서.</summary>
        public IEnumerable<string> FurnitureGiving(string qualityId)
        {
            var table = Definitions.FurnitureQuality;
            return Session.AvailableFurniture()
                .Select(f => (f.StableId, Score: table.ScoresOf(f.StableId).Where(s => s.QualityId == qualityId).Select(s => s.Score).DefaultIfEmpty(0).Max()))
                .Where(c => c.Score > 0)
                .OrderByDescending(c => c.Score)
                .Select(c => c.StableId);
        }

        /// <summary>준비: 아늑함 가구를 놓아 요리 일에 보상 이득이 생기게 한다(GameSession 명령).</summary>
        public void DecorateUntilBonus()
        {
            foreach (string id in FurnitureGiving(Cozy))
            {
                if (Session.WorkExtrasFor(ChefWork).RewardPercentBonus > 0) return;
                if (Session.CanAddToActiveWorkspace() != WorkspaceResultKind.Applied) break;
                if (Session.House.Placed.Any(p => p.Definition.StableId == id)) continue;
                Assert.That(Session.PlaceFurniture(id, FreeCell(id), FurniturePlacementDirection.North), Is.True, id);
            }
            Assert.That(Session.WorkExtrasFor(ChefWork).RewardPercentBonus, Is.GreaterThan(0), "준비: 이득이 생겼다");
        }

        public void Dispose() => F.Dispose();
    }
}
