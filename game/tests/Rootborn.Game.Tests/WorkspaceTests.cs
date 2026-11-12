using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Workspaces;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 일터를 GameSession 명령으로 확인한다: 들어가기(기본 템플릿 깔기), 한도, 템플릿 적용, 공간 점수와 일 이득, 꾸미며 배우기.
    /// 실제 data/와 임시 저장소. 수치는 시험용이라 "늘었다"만 본다(자동 테스트).
    /// </summary>
    public sealed class WorkspaceTests
    {
        private const string ChefWorkspace = "workspace.chef";
        private const string ChefFacility = "facility.chef";
        private const string ChefWork = "work.chef.level1";
        private const string CozyTemplate = "template.chef.cozy";
        private const string Interior = "skill.interior";
        private const string Cozy = "quality.cozy";
        // 해금이 필요 없고 집에 놓는 가구(FurnitureTests와 같은 것).
        private const string FreeDesk = "growth31.assembly.desk";
        // 한 칸짜리 가구(기본 템플릿에 든 것).
        private const string Plant = "school.potted.plant";

        private static Workspace Chef => TestWorld.Definitions.Get<Workspace>(ChefWorkspace);

        private static void EnterChef(GameSession game) => game.EnterPlacementSurface(WorkspaceRoom.GridFor(game.Definitions, ChefWorkspace), Chef.SurfaceId);

        private static WorkspaceTemplate BasicTemplate => TestWorld.Definitions.Get<WorkspaceTemplate>(Chef.DefaultTemplateId);

        private static bool HasLayoutFile(TempSaves saves)
            => saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.WorkspaceLayout(Chef.SurfaceId, TestWorld.Player), out _);

        // 아늑함을 주는 가구를 점수 높은 순으로, 아직 놓지 않은 것부터 놓는다. 일 이득이 생기거나 한도에 닿으면 멈춘다.
        private static void PlaceCozyUntilBonus(GameSession game)
        {
            var quality = game.Definitions.FurnitureQuality;
            var candidates = game.AvailableFurniture()
                .Select(f => (f.StableId, Score: quality.ScoresOf(f.StableId).Where(s => s.QualityId == Cozy).Select(s => s.Score).DefaultIfEmpty(0).Max()))
                .Where(c => c.Score > 0)
                .OrderByDescending(c => c.Score)
                .Select(c => c.StableId)
                .ToList();
            foreach (var id in candidates)
            {
                if (game.WorkExtrasFor(ChefWork).RewardPercentBonus > 0) return;
                var status = game.WorkspaceOf(ChefFacility);
                if (status.Placed >= status.Capacity) return;
                if (game.House.Placed.Any(p => p.Definition.StableId == id)) continue;
                if (!WorkspaceRoom.TryFreeAnchor(game, id, out var anchor)) continue;
                Assert.That(game.PlaceFurniture(id, anchor, FurniturePlacementDirection.North), Is.True, id);
            }
        }

        // 한도까지 아무 가구나 놓는다(기본 템플릿과 겹치지 않는 칸).
        private static void FillToCapacity(GameSession game)
        {
            while (game.WorkspaceOf(ChefFacility).Placed < game.WorkspaceOf(ChefFacility).Capacity)
                Assert.That(game.PlaceFurniture(FreeDesk, WorkspaceRoom.FreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
        }

        [Test]
        public void WORKSPACE_001_DecoratingRaisesWorkReward()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            EnterChef(game);
            Assert.That(game.ActiveWorkspace?.Id, Is.EqualTo(ChefWorkspace));
            Assert.That(game.OwnsActiveWorkspace, Is.True);
            var before = game.WorkExtrasFor(ChefWork);
            var seededScores = game.WorkspaceOf(ChefFacility).Scores;
            Assert.That(before.RewardPercentBonus, Is.EqualTo(game.Definitions.WorkLinks.ExtrasFor(ChefWork, seededScores).RewardPercentBonus), "놓기 전에는 기본 템플릿 값");

            PlaceCozyUntilBonus(game);

            var after = game.WorkExtrasFor(ChefWork);
            Assert.That(after.RewardPercentBonus, Is.GreaterThan(0));
            Assert.That(after.RewardPercentBonus, Is.GreaterThan(before.RewardPercentBonus));
            Assert.That(game.WorkspaceOf(ChefFacility).Scores.Get(Cozy), Is.GreaterThan(seededScores.Get(Cozy)));
            Assert.That(game.WorkExtrasFor("no.such.work").RewardPercentBonus, Is.EqualTo(0));
        }

        [Test]
        public void WorkExtras_AreDefault_ForAStudentOrAnotherFacilitysWork()
        {
            using var saves = new TempSaves();
            var student = TestWorld.Start(saves.Store);
            Assert.That(student.WorkExtrasFor(ChefWork).RewardPercentBonus, Is.EqualTo(0));
            Assert.That(student.WorkExtrasFor(ChefWork).SkillGains, Is.Empty, "자기 시설의 일이 아니면 이득이 없다");
        }

        [Test]
        public void WORKSPACE_002_TemplateOnce()
        {
            using var saves = new TempSaves();
            ChefSetup.Adult(saves);
            SaveEdits.EditSnapshot(saves.Store, s => SaveEdits.Fill(s, "item.allowance", 50));
            var game = TestWorld.Start(saves.Store);
            EnterChef(game);
            var allowance = game.Item("item.allowance");
            var template = game.Definitions.Get<WorkspaceTemplate>(CozyTemplate);
            int cost = template.Costs.Where(c => c.Item == "item.allowance").Sum(c => c.Count);
            Assert.That(game.TemplatesForActiveWorkspace().Select(t => t.Id), Does.Contain(CozyTemplate));
            Assert.That(game.CanApplyTemplate(CozyTemplate), Is.EqualTo(WorkspaceResultKind.Applied));

            Assert.That(game.ApplyTemplate(CozyTemplate, "req:cozy"), Is.EqualTo(WorkspaceResultKind.Applied));
            Assert.That(game.Inventory.CountOf(allowance), Is.EqualTo(50 - cost));
            Assert.That(game.House.Placed.Count, Is.EqualTo(template.Placements.Count));
            Assert.That(game.ApplyTemplate(CozyTemplate, "req:cozy"), Is.EqualTo(WorkspaceResultKind.DuplicateRequest));
            Assert.That(game.Inventory.CountOf(allowance), Is.EqualTo(50 - cost), "돈은 한 번만");

            var again = TestWorld.Start(saves.Store);
            EnterChef(again);
            Assert.That(again.Inventory.CountOf(allowance), Is.EqualTo(50 - cost));
            Assert.That(again.House.Placed.Count, Is.EqualTo(template.Placements.Count));
            Assert.That(again.ApplyTemplate(CozyTemplate, "req:cozy"), Is.EqualTo(WorkspaceResultKind.DuplicateRequest));
            Assert.That(again.House.Placed.Count, Is.EqualTo(template.Placements.Count));
            Assert.That(again.Inventory.CountOf(allowance), Is.EqualTo(50 - cost));
        }

        [Test]
        public void Template_WithoutMoney_ChangesNothing()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            EnterChef(game);
            var placedBefore = game.House.Placed.Select(p => p.Definition.StableId).ToArray();

            Assert.That(game.CanApplyTemplate(CozyTemplate), Is.EqualTo(WorkspaceResultKind.InsufficientResources));
            Assert.That(game.ApplyTemplate(CozyTemplate, "req:cozy"), Is.EqualTo(WorkspaceResultKind.InsufficientResources));
            Assert.That(game.House.Placed.Select(p => p.Definition.StableId), Is.EqualTo(placedBefore));
        }

        [Test]
        public void SKILL_001_ChefLearnsInteriorByDecorating()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            EnterChef(game);
            int skillBefore = game.Student.GetSkillValue(Interior);
            int capacityBefore = game.WorkspaceOf(ChefFacility).Capacity;

            Assert.That(game.PlaceFurniture(FreeDesk, WorkspaceRoom.FreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            Assert.That(game.Student.GetSkillValue(Interior), Is.GreaterThan(skillBefore), "일터에 가구를 놓아 인테리어가 오른다");
            Assert.That(TestWorld.Start(saves.Store).Student.GetSkillValue(Interior), Is.EqualTo(game.Student.GetSkillValue(Interior)), "저장된다");

            // 준비: 첫 단계에 닿게 기술 값을 직접 더한다.
            var tier = game.Definitions.Get<SkillTier>("tier.interior.1");
            SaveEdits.EditSnapshot(saves.Store, s => s.Student.Skills = s.Student.Skills
                .Where(e => e.Id != Interior)
                .Append(new StudentLifeProgressSaveData.StatEntry { Id = Interior, Value = tier.MinimumValue })
                .ToArray());
            var reached = TestWorld.Start(saves.Store);
            Assert.That(reached.WorkspaceOf(ChefFacility).Capacity, Is.GreaterThan(capacityBefore));
        }

        [Test]
        public void FirstEnter_SeedsDefaultTemplate_Once()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            EnterChef(game);
            Assert.That(game.House.Placed.Select(p => p.Definition.StableId), Is.EquivalentTo(BasicTemplate.Placements.Select(p => p.FurnitureId)));
            Assert.That(HasLayoutFile(saves), Is.True);

            EnterChef(game);
            Assert.That(game.House.Placed.Count, Is.EqualTo(BasicTemplate.Placements.Count), "다시 들어와도 다시 깔지 않는다");

            Assert.That(game.ClearFurniture(), Is.True);
            EnterChef(game);
            Assert.That(game.House.Placed, Is.Empty, "다 치운 뒤 다시 들어와도 다시 깔지 않는다");

            var again = TestWorld.Start(saves.Store);
            EnterChef(again);
            Assert.That(again.House.Placed, Is.Empty, "새로 읽어도");

            // 배치 파일이 사라져도 깔린 기록이 있으면 다시 깔지 않는다.
            saves.Store.Delete(TestWorld.Slot, SaveFileNames.WorkspaceLayout(Chef.SurfaceId, TestWorld.Player));
            var lost = TestWorld.Start(saves.Store);
            EnterChef(lost);
            Assert.That(lost.House.Placed, Is.Empty);
        }

        [Test]
        public void NotOwner_CannotPlaceOrApply_AndIsNotSeeded()
        {
            using var saves = new TempSaves();
            var student = TestWorld.Start(saves.Store);
            student.VisitLocation("location.town-square"); // 첫 저장
            SaveEdits.EditSnapshot(saves.Store, s => SaveEdits.Fill(s, "item.allowance", 50));
            student = TestWorld.Start(saves.Store);
            EnterChef(student);

            Assert.That(student.ActiveWorkspace?.Id, Is.EqualTo(ChefWorkspace));
            Assert.That(student.OwnsActiveWorkspace, Is.False);
            Assert.That(student.House.Placed, Is.Empty, "남의 일터에는 깔지 않는다");
            Assert.That(HasLayoutFile(saves), Is.False);
            Assert.That(WorkspaceRoom.TryFreeAnchor(student, FreeDesk, out var anchor), Is.True);
            Assert.That(student.PlaceFurniture(FreeDesk, anchor, FurniturePlacementDirection.North), Is.False);
            Assert.That(student.CanApplyTemplate(CozyTemplate), Is.EqualTo(WorkspaceResultKind.NotOwner));
            Assert.That(student.ApplyTemplate(CozyTemplate, "req:cozy"), Is.EqualTo(WorkspaceResultKind.NotOwner));
            Assert.That(student.Inventory.CountOf(student.Item("item.allowance")), Is.EqualTo(50));
            Assert.That(student.House.Placed, Is.Empty);
            Assert.That(student.WorkspaceOf(ChefFacility).Owned, Is.False);
            Assert.That(HasLayoutFile(saves), Is.False);
        }

        [Test]
        public void AtCapacity_PlaceRefused_MoveAndRemoveAllowed()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            EnterChef(game);
            FillToCapacity(game);
            int capacity = game.WorkspaceOf(ChefFacility).Capacity;
            Assert.That(game.House.Placed.Count, Is.EqualTo(capacity));

            Assert.That(game.PlaceFurniture(FreeDesk, WorkspaceRoom.FreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.False, "한도에서는 새로 놓지 못한다");
            var first = game.House.Placed[0];
            Assert.That(game.MoveFurniture(first.InstanceId, WorkspaceRoom.FreeAnchor(game, first.Definition.StableId), FurniturePlacementDirection.North), Is.True);
            Assert.That(game.RemoveFurniture(game.House.Placed[1].InstanceId), Is.True);
            Assert.That(game.House.Placed.Count, Is.EqualTo(capacity - 1));
            Assert.That(game.PlaceFurniture(FreeDesk, WorkspaceRoom.FreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True, "치우면 다시 놓을 수 있다");
        }

        [Test]
        public void OverCapacityOldLayout_IsKept()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            int capacity = game.WorkspaceOf(ChefFacility).Capacity;
            var grid = WorkspaceRoom.GridFor(game.Definitions, ChefWorkspace);
            // 출발 칸 줄 아래 한 줄에 한도보다 둘 많게 미리 써 둔다.
            var records = Enumerable.Range(0, capacity + 2).Select(i => new FurniturePlacementSaveData
            {
                FurnitureId = Plant, SurfaceId = Chef.SurfaceId, AnchorX = grid.Spawn.X + 1 + i, AnchorY = grid.Spawn.Y + 1, Direction = FurniturePlacementDirection.North,
            }).ToArray();
            WorkspaceSaveFiles.SaveLayout(saves.Store, TestWorld.Slot, Chef.SurfaceId, TestWorld.Player, records);

            EnterChef(game);
            Assert.That(game.House.Placed.Count, Is.EqualTo(capacity + 2), "옛 배치는 한도보다 많아도 그대로 놓인다");
            Assert.That(game.WorkspaceOf(ChefFacility).Placed, Is.EqualTo(capacity + 2));
            Assert.That(game.PlaceFurniture(FreeDesk, WorkspaceRoom.FreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.False);
            Assert.That(game.RemoveFurniture(game.House.Placed[0].InstanceId), Is.True);
            Assert.That(game.House.Placed.Count, Is.EqualTo(capacity + 1));
        }

        [Test]
        public void Layout_SurvivesNewSession()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            EnterChef(game);
            var anchor = WorkspaceRoom.FreeAnchor(game, FreeDesk);
            Assert.That(game.PlaceFurniture(FreeDesk, anchor, FurniturePlacementDirection.North), Is.True);
            var placed = game.House.Placed.Select(p => (p.Definition.StableId, p.Anchor)).ToArray();
            var scores = game.WorkspaceOf(ChefFacility).Scores.All.Select(s => (s.QualityId, s.Score)).ToArray();

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.WorkspaceOf(ChefFacility).Scores.All.Select(s => (s.QualityId, s.Score)), Is.EqualTo(scores), "열지 않아도 저장된 배치로 점수를 낸다");
            EnterChef(again);
            Assert.That(again.House.Placed.Select(p => (p.Definition.StableId, p.Anchor)), Is.EqualTo(placed));
        }

        [Test]
        public void HouseLayout_IsUntouchedByWorkspace()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            game.EnterPlacementSurface(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.ActiveWorkspace, Is.Null, "집에는 일터가 없다");
            var houseAnchor = HouseRoom.FirstFreeAnchor(game, FreeDesk);
            Assert.That(game.PlaceFurniture(FreeDesk, houseAnchor, FurniturePlacementDirection.North), Is.True);
            saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(game.HouseStage), out var houseBefore);

            EnterChef(game);
            Assert.That(game.PlaceFurniture(FreeDesk, WorkspaceRoom.FreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            Assert.That(game.ClearFurniture(), Is.True);

            saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(game.HouseStage), out var houseAfter);
            Assert.That(houseAfter.Items.Select(i => (i.FurnitureId, i.AnchorX, i.AnchorY)), Is.EqualTo(houseBefore.Items.Select(i => (i.FurnitureId, i.AnchorX, i.AnchorY))));
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.ActiveWorkspace, Is.Null);
            Assert.That(game.House.Placed.Select(p => (p.Definition.StableId, p.Anchor)), Is.EqualTo(new[] { (FreeDesk, houseAnchor) }));
        }

        [Test]
        public void HousePlacement_GrowsInteriorSkill_UpToDailyLimit()
        {
            // 학교 밖 길: 학생이 자기 집을 꾸며 인테리어를 배운다.
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var rule = game.Definitions.All<PlacementSkillGrowth>().First(g => g.SurfaceId == HouseRoom.SurfaceId);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            int before = game.Student.GetSkillValue(rule.SkillId);

            for (int i = 0; i < rule.DailyLimit + 1; i++)
                Assert.That(game.PlaceFurniture(FreeDesk, HouseRoom.FirstFreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            Assert.That(game.Student.GetSkillValue(rule.SkillId), Is.EqualTo(before + rule.Amount * rule.DailyLimit), "하루 한도까지만");

            var again = TestWorld.Start(saves.Store);
            again.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(again.PlaceFurniture(FreeDesk, HouseRoom.FirstFreeAnchor(again, FreeDesk), FurniturePlacementDirection.North), Is.True);
            Assert.That(again.Student.GetSkillValue(rule.SkillId), Is.EqualTo(before + rule.Amount * rule.DailyLimit), "다시 읽어도 오늘 센 횟수는 남는다");

            Assert.That(again.EndDay().Ended, Is.True);
            Assert.That(again.StartNextDay(), Is.True);
            Assert.That(again.PlaceFurniture(FreeDesk, HouseRoom.FirstFreeAnchor(again, FreeDesk), FurniturePlacementDirection.North), Is.True);
            Assert.That(again.Student.GetSkillValue(rule.SkillId), Is.EqualTo(before + rule.Amount * (rule.DailyLimit + 1)), "다음 날 다시 센다");
        }

        [Test]
        public void InAHouseSharingRoom_TheWorkspaceIsStillOwn()
        {
            using var saves = new TempSaves();
            ChefSetup.Adult(saves);
            var rules = new Rootborn.Core.Room.RoomHouseRules(TestWorld.Definitions.All<FurnitureDefinition>(), s => s == HouseRoom.SurfaceId ? HouseRoom.Grid : null);
            var pieces = new Rootborn.Core.Room.RoomPieceSet(new Rootborn.Core.Room.RoomShareSettings(new[] { Rootborn.Core.Room.RoomHouse.PieceName }),
                Rootborn.Core.Room.RoomPieceCatalog.CreateDefault(rules));
            int ids = 0;
            var shares = new Rootborn.Core.Room.RoomShares(pieces.Settings, Rootborn.Core.Room.RoomPieceCatalog.CreateDefault(), () => "r" + (++ids));
            foreach (var piece in pieces.Pieces) shares.Receive(piece.State());
            var game = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, TestWorld.Slot, TestWorld.Player, shares: shares);
            Assert.That(game.SharesHouse, Is.True);

            EnterChef(game);
            Assert.That(game.House.Placed.Count, Is.EqualTo(BasicTemplate.Placements.Count), "일터는 방의 배치가 아니라 자기 파일이다");
            Assert.That(game.PlaceFurniture(FreeDesk, WorkspaceRoom.FreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True, "방에 요청하지 않고 바로 놓인다");
            Assert.That(game.WaitingForRoom, Is.False);
            Assert.That(shares.Outbox.Requests.Any(r => r.Piece == Rootborn.Core.Room.RoomHouse.PieceName), Is.False);
            Assert.That(game.ClearFurniture(), Is.True);
            Assert.That(game.House.Placed, Is.Empty);
        }

        [Test]
        public void OldSave_WithoutWorkspaceFiles_Starts()
        {
            // Fixtures에 성인 슬롯이 없어, 일터 파일이 없는 저장소에서 성인 요리사를 명령으로 만든 뒤 다시 읽는다.
            using var saves = new TempSaves();
            ChefSetup.Adult(saves);
            saves.Store.Delete(TestWorld.Slot, SaveFileNames.WorkspaceProgress(TestWorld.Player));
            Assert.That(saves.Store.TryRead<WorkspaceProgressSaveData>(TestWorld.Slot, SaveFileNames.WorkspaceProgress(TestWorld.Player), out _), Is.False);
            Assert.That(HasLayoutFile(saves), Is.False);

            var game = TestWorld.Start(saves.Store);
            var status = game.WorkspaceOf(ChefFacility);
            Assert.That(status, Is.Not.Null);
            Assert.That(status.Owned, Is.True);
            Assert.That(status.Placed, Is.EqualTo(BasicTemplate.Placements.Count), "열기 전에는 기본 템플릿의 가구로 점수를 낸다");
            Assert.That(status.Scores.All, Is.Not.Empty);
            var expected = status.Scores.All.Select(s => (s.QualityId, s.Score)).ToArray();
            Assert.That(game.WorkspaceOf("no.such.facility"), Is.Null);

            EnterChef(game);
            Assert.That(game.WorkspaceOf(ChefFacility).Scores.All.Select(s => (s.QualityId, s.Score)), Is.EqualTo(expected), "깔고 난 뒤의 점수와 같다");
        }
    }
}
