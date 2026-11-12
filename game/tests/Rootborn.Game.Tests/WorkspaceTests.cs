using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
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
        private const string Hygiene = "quality.hygiene";
        // 해금이 필요 없고 집에 놓는 가구(FurnitureTests와 같은 것).
        private const string FreeDesk = "growth31.assembly.desk";
        // 한 칸짜리 가구(기본 템플릿에 든 것).
        private const string Plant = "school.potted.plant";

        private static Workspace Chef => TestWorld.Definitions.Get<Workspace>(ChefWorkspace);

        private static void EnterChef(GameSession game) => game.EnterPlacementSurface(WorkspaceRoom.GridFor(game.Definitions, ChefWorkspace), Chef.SurfaceId);

        private static WorkspaceTemplate BasicTemplate => TestWorld.Definitions.Get<WorkspaceTemplate>(Chef.DefaultTemplateId);

        private static bool HasLayoutFile(TempSaves saves)
            => saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.WorkspaceLayout(Chef.SurfaceId, TestWorld.Player), out _);

        // 아늑함을 주는 가구를 놓아 보상 비율 이득을 만든다.
        private static void PlaceCozyUntilBonus(GameSession game)
            => PlaceQualityUntil(game, Cozy, extras => extras.RewardPercentBonus > 0);

        // 그 성질을 주는 가구를 점수 높은 순으로, 아직 놓지 않은 것부터 놓는다. 조건이 맞거나 한도에 닿으면 멈춘다.
        private static void PlaceQualityUntil(GameSession game, string qualityId, System.Func<WorkExtras, bool> done)
        {
            var quality = game.Definitions.FurnitureQuality;
            var candidates = game.AvailableFurniture()
                .Select(f => (f.StableId, Score: quality.ScoresOf(f.StableId).Where(s => s.QualityId == qualityId).Select(s => s.Score).DefaultIfEmpty(0).Max()))
                .Where(c => c.Score > 0)
                .OrderByDescending(c => c.Score)
                .Select(c => c.StableId)
                .ToList();
            foreach (var id in candidates)
            {
                if (done(game.WorkExtrasFor(ChefWork))) return;
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
        public void LeavingAWorkspaceWithUnrestoredRecords_ScoresFromTheSavedRecords()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            Assert.That(game.Definitions.FurnitureQuality.ScoresOf(Plant), Is.Not.Empty, "준비: 화분은 점수를 준다");
            var grid = WorkspaceRoom.GridFor(game.Definitions, ChefWorkspace);
            // 하나는 놓이는 칸, 하나는 방 밖이라 되살리지 못하는 칸. 되살리지 못한 기록도 파일에 남는다.
            var records = new[]
            {
                new FurniturePlacementSaveData { FurnitureId = Plant, SurfaceId = Chef.SurfaceId, AnchorX = grid.Spawn.X + 1, AnchorY = grid.Spawn.Y + 1, Direction = FurniturePlacementDirection.North },
                new FurniturePlacementSaveData { FurnitureId = Plant, SurfaceId = Chef.SurfaceId, AnchorX = -1000, AnchorY = -1000, Direction = FurniturePlacementDirection.North },
            };
            WorkspaceSaveFiles.SaveLayout(saves.Store, TestWorld.Slot, Chef.SurfaceId, TestWorld.Player, records);

            EnterChef(game);
            Assert.That(game.House.Placed.Count, Is.EqualTo(1), "준비: 하나만 되살아났다");
            var open = game.WorkspaceOf(ChefFacility).Scores.All.Select(s => (s.QualityId, s.Score)).ToArray();
            var closed = TestWorld.Start(saves.Store).WorkspaceOf(ChefFacility).Scores.All.Select(s => (s.QualityId, s.Score)).ToArray();
            Assert.That(closed, Is.Not.EqualTo(open), "준비: 열려 있을 때와 파일 기록의 점수가 다르다");

            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.WorkspaceOf(ChefFacility).Scores.All.Select(s => (s.QualityId, s.Score)), Is.EqualTo(closed), "나간 뒤에는 파일 기록으로 낸다");
        }

        [Test]
        public void HousePlacement_ThatGrowsASkill_NotifiesStudent()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var rule = game.Definitions.All<PlacementSkillGrowth>().First(g => g.SurfaceId == HouseRoom.SurfaceId);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            int before = game.Student.GetSkillValue(rule.SkillId);
            var topics = new System.Collections.Generic.List<string>();
            game.Changed += topics.Add;

            Assert.That(game.PlaceFurniture(FreeDesk, HouseRoom.FirstFreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            Assert.That(game.Student.GetSkillValue(rule.SkillId), Is.GreaterThan(before), "준비: 기술이 올랐다");
            Assert.That(topics, Does.Contain(GameChangeTopics.Student));
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
            Assert.That(game.SharesOpenLayout, Is.False, "일터가 열려 있으면 같이 쓰는 배치가 아니다(화면이 공유 중으로 보이지 않는다)");
            Assert.That(game.House.Placed.Count, Is.EqualTo(BasicTemplate.Placements.Count), "일터는 방의 배치가 아니라 자기 파일이다");
            Assert.That(game.PlaceFurniture(FreeDesk, WorkspaceRoom.FreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True, "방에 요청하지 않고 바로 놓인다");
            Assert.That(game.WaitingForRoom, Is.False);
            Assert.That(shares.Outbox.Requests.Any(r => r.Piece == Rootborn.Core.Room.RoomHouse.PieceName), Is.False);
            Assert.That(game.ClearFurniture(), Is.True);
            Assert.That(game.House.Placed, Is.Empty);

            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.SharesOpenLayout, Is.True, "집으로 돌아오면 같이 쓰는 배치다");
        }

        // ---------- 일 명령이 공간 이득을 받는다 ----------

        private const string Cooking = "skill.cooking";
        private const string Allowance = "item.allowance";
        private const string Materials = "item.facility-materials";

        private static FacilityWork ChefWorkDefinition => TestWorld.Definitions.Get<FacilityWork>(ChefWork);

        private static int LevelRewardPercent(GameSession game) => game.Definitions.Get<Facility>(ChefFacility).GetLevel(game.Adult.FacilityLevel).RewardPercent;

        private static (int Allowance, int Materials) Counts(GameSession game)
            => (game.Inventory.CountOf(game.Item(Allowance)), game.Inventory.CountOf(game.Item(Materials)));

        // 그 비율로 만점 보상을 만들었을 때 받을 수(아이템별).
        private static (int Allowance, int Materials) RewardsAt(int percent)
        {
            Assert.That(ChefWorkDefinition.TryBuildRewards(100, percent, out var rewards), Is.True);
            return (rewards.Where(r => r.Item == Allowance).Sum(r => r.Count), rewards.Where(r => r.Item == Materials).Sum(r => r.Count));
        }

        // 만점으로 바로 하고 받은 수를 돌려준다.
        private static (int Allowance, int Materials) CompleteAndCount(GameSession game, string requestId)
        {
            var before = Counts(game);
            Assert.That(game.CompleteFacilityWork(ChefWork, requestId, 100, autoSkip: false).Kind, Is.EqualTo(AdultCareerResultKind.Applied), requestId);
            var after = Counts(game);
            return (after.Allowance - before.Allowance, after.Materials - before.Materials);
        }

        [Test]
        public void CompleteWork_PaysMoreInDecoratedWorkspace()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            EnterChef(game);
            var plain = CompleteAndCount(game, "work:plain");

            PlaceCozyUntilBonus(game);
            Assert.That(game.WorkExtrasFor(ChefWork).RewardPercentBonus, Is.GreaterThan(0), "준비: 꾸며서 이득이 생겼다");
            var decorated = CompleteAndCount(game, "work:decorated");

            Assert.That(decorated.Allowance + decorated.Materials, Is.GreaterThan(plain.Allowance + plain.Materials), "같은 점수라도 꾸민 뒤가 많다");
            Assert.That(decorated.Allowance, Is.GreaterThanOrEqualTo(plain.Allowance));
            Assert.That(decorated.Materials, Is.GreaterThanOrEqualTo(plain.Materials));
            Assert.That(decorated, Is.EqualTo(RewardsAt(LevelRewardPercent(game) + game.WorkExtrasFor(ChefWork).RewardPercentBonus)));
        }

        [Test]
        public void CompleteWork_GrowsCookingSkill()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            var gains = game.WorkExtrasFor(ChefWork).SkillGains.Where(g => g.SkillId == Cooking).Sum(g => g.Amount);
            Assert.That(gains, Is.GreaterThan(0), "준비: 요리 일은 요리 기술을 키운다");
            int before = game.Student.GetSkillValue(Cooking);

            Assert.That(game.CompleteFacilityWork(ChefWork, "work:fail", 0, autoSkip: false).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(game.Student.GetSkillValue(Cooking), Is.EqualTo(before), "실패한 일은 기술을 키우지 않는다");

            CompleteAndCount(game, "work:pass");
            Assert.That(game.Student.GetSkillValue(Cooking), Is.EqualTo(before + gains));
            Assert.That(TestWorld.Start(saves.Store).Student.GetSkillValue(Cooking), Is.EqualTo(before + gains), "저장된다");
        }

        [Test]
        public void CompleteWork_GrowsSkill_EvenWhenTheFacilityHasNoWorkspace()
        {
            // 일을 하면 기술이 자라는 것은 일터와 상관없다. data/를 임시 폴더로 복사해 일터·템플릿 정의만 빼면
            // 요리사 시설은 일터가 없고 WorkSkillGrowth(요리 일 → 요리 기술)만 남는다.
            var data = CopyDataWithoutWorkspaces();
            try
            {
                var loaded = Rootborn.Data.GameDataLoader.Load(data, Rootborn.Data.Builders.RootbornBuilders.Register);
                loaded.ThrowIfFailed();
                var definitions = loaded.Definitions;
                Assert.That(definitions.All<Workspace>(), Is.Empty, "준비: 일터가 없다");
                var settings = GameStartSettings.FromDefinitions(definitions);
                using var saves = new TempSaves();
                GameSession Start() => GameSession.Start(definitions, settings, saves.Store, TestWorld.Slot, TestWorld.Player);
                Start().TalkTo("npc.first-guide"); // 첫 저장
                SaveEdits.EditSnapshot(saves.Store, SaveEdits.MakeChefCandidateReady);
                var game = Start();
                Assert.That(game.ChooseFacility(ChefFacility, "facility:chef").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
                Assert.That(game.WorkspaceOf(ChefFacility), Is.Null);

                var extras = game.WorkExtrasFor(ChefWork);
                int gains = extras.SkillGains.Where(g => g.SkillId == Cooking).Sum(g => g.Amount);
                Assert.That(gains, Is.GreaterThan(0), "일터가 없어도 일의 기술 성장은 남는다");
                Assert.That(extras.RewardPercentBonus, Is.EqualTo(0), "공간 이득은 없다");
                Assert.That(extras.ReputationBonus, Is.EqualTo(0));
                int before = game.Student.GetSkillValue(Cooking);

                Assert.That(game.CompleteFacilityWork(ChefWork, "work:no-workspace", 100, autoSkip: false).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
                Assert.That(game.Student.GetSkillValue(Cooking), Is.EqualTo(before + gains));
                Assert.That(Start().Student.GetSkillValue(Cooking), Is.EqualTo(before + gains), "저장된다");
            }
            finally
            {
                System.IO.Directory.Delete(System.IO.Path.GetDirectoryName(data), true);
            }
        }

        // data/와 그 옆 game/ 설정 파일을 임시 폴더로 복사하고 일터·템플릿 정의 파일만 뺀다(CareerInterestChoiceTests와 같은 방식).
        private static string CopyDataWithoutWorkspaces()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-game-tests", System.Guid.NewGuid().ToString("N"));
            var target = System.IO.Path.Combine(root, "data");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "game"));
            foreach (var name in new[] { "time.json", "economy.json" })
                System.IO.File.Copy(System.IO.Path.Combine(TestWorld.RepoRoot, "game", name), System.IO.Path.Combine(root, "game", name));
            foreach (var file in System.IO.Directory.GetFiles(TestWorld.DataDirectory, "*", System.IO.SearchOption.AllDirectories))
            {
                var relative = System.IO.Path.GetRelativePath(TestWorld.DataDirectory, file).Replace('\\', '/');
                if (relative == "workspaces/WorkspaceDefinition.json" || relative == "workspaces/WorkspaceTemplate.json") continue;
                var copy = System.IO.Path.Combine(target, relative);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(copy));
                System.IO.File.Copy(file, copy);
            }
            return target;
        }

        [Test]
        public void ReserveThenDecorateThenSettle_RewardUsesReservation()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            EnterChef(game);
            int reservedBonus = game.WorkExtrasFor(ChefWork).RewardPercentBonus;
            Assert.That(game.ReserveFacilityWork(ChefWork, "work:reserved", 5).Kind, Is.EqualTo(AdultCareerResultKind.Applied));

            PlaceCozyUntilBonus(game);
            var settleExtras = game.WorkExtrasFor(ChefWork);
            Assert.That(settleExtras.RewardPercentBonus, Is.GreaterThan(reservedBonus), "준비: 예약 뒤에 꾸며 이득이 늘었다");
            var before = Counts(game);
            int reputation = game.Adult.Reputation;

            Assert.That(game.SettleFacilityWork("work:reserved", 100).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            var after = Counts(game);
            Assert.That((after.Allowance - before.Allowance, after.Materials - before.Materials), Is.EqualTo(RewardsAt(LevelRewardPercent(game) + reservedBonus)),
                "보상은 예약할 때의 비율로 낸다");
            Assert.That(game.Adult.Reputation - reputation, Is.EqualTo(ChefWorkDefinition.ReputationPerSuccess + settleExtras.ReputationBonus),
                "아늑함만 놓아 명성 이득은 0이다 — 0이 아닌 정산 때 명성 이득은 ReserveThenAddHygieneThenSettle이 본다");

            // 거꾸로: 꾸민 채 예약하고 다 치운 뒤 정산해도 예약할 때의 이득으로 낸다.
            int decoratedBonus = game.WorkExtrasFor(ChefWork).RewardPercentBonus;
            Assert.That(game.ReserveFacilityWork(ChefWork, "work:reserved-decorated", 6).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(game.ClearFurniture(), Is.True);
            Assert.That(game.WorkExtrasFor(ChefWork).RewardPercentBonus, Is.LessThan(decoratedBonus), "준비: 치워서 이득이 줄었다");
            before = Counts(game);
            Assert.That(game.SettleFacilityWork("work:reserved-decorated", 100).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            after = Counts(game);
            Assert.That((after.Allowance - before.Allowance, after.Materials - before.Materials), Is.EqualTo(RewardsAt(LevelRewardPercent(game) + decoratedBonus)));
        }

        [Test]
        public void ReserveThenAddHygieneThenSettle_ReputationAndSkillUseSettleExtras()
        {
            using var saves = new TempSaves();
            ChefSetup.Adult(saves);
            // 준비: 위생 명성 이득에 닿을 만큼 가구를 놓을 수 있게 인테리어 기술을 가장 높은 단계까지 직접 더한다(한도·점수 배율이 는다).
            var topTier = TestWorld.Definitions.All<SkillTier>().Where(t => t.SkillId == Interior).OrderByDescending(t => t.MinimumValue).First();
            SaveEdits.EditSnapshot(saves.Store, s => s.Student.Skills = s.Student.Skills
                .Where(e => e.Id != Interior)
                .Append(new StudentLifeProgressSaveData.StatEntry { Id = Interior, Value = topTier.MinimumValue })
                .ToArray());
            var game = TestWorld.Start(saves.Store);
            EnterChef(game);
            Assert.That(game.ClearFurniture(), Is.True, "준비: 빈 일터에서 시작한다");
            var reserveExtras = game.WorkExtrasFor(ChefWork);
            Assert.That(reserveExtras.ReputationBonus, Is.EqualTo(0), "준비: 예약 때는 위생 명성 이득이 없다");
            Assert.That(game.ReserveFacilityWork(ChefWork, "work:hygiene", 9).Kind, Is.EqualTo(AdultCareerResultKind.Applied));

            PlaceQualityUntil(game, Hygiene, extras => extras.ReputationBonus > 0);
            var settleExtras = game.WorkExtrasFor(ChefWork);
            Assert.That(settleExtras.ReputationBonus, Is.GreaterThan(reserveExtras.ReputationBonus), "준비: 예약 뒤 위생 가구로 명성 이득이 생겼다");
            int gains = settleExtras.SkillGains.Where(g => g.SkillId == Cooking).Sum(g => g.Amount);
            Assert.That(gains, Is.GreaterThan(0));
            int reputation = game.Adult.Reputation;
            int cooking = game.Student.GetSkillValue(Cooking);

            Assert.That(game.SettleFacilityWork("work:hygiene", 100).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(game.Adult.Reputation - reputation, Is.EqualTo(ChefWorkDefinition.ReputationPerSuccess + settleExtras.ReputationBonus), "명성 이득은 정산 때 것");
            Assert.That(game.Student.GetSkillValue(Cooking), Is.EqualTo(cooking + gains), "정산한 일도 요리 기술을 키운다");
            var again = TestWorld.Start(saves.Store);
            Assert.That(again.Adult.Reputation, Is.EqualTo(game.Adult.Reputation), "저장된다");
            Assert.That(again.Student.GetSkillValue(Cooking), Is.EqualTo(cooking + gains));
        }

        [Test]
        public void UndecoratedWorkspace_NeverPaysLessThanBefore()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            EnterChef(game);
            Assert.That(game.ClearFurniture(), Is.True);
            Assert.That(game.House.Placed, Is.Empty);

            Assert.That(CompleteAndCount(game, "work:empty"), Is.EqualTo(RewardsAt(LevelRewardPercent(game))), "다 치워도 레벨 비율만의 보상은 그대로");
        }

        [Test]
        public void RewardOnce_StillHolds()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            EnterChef(game);
            PlaceCozyUntilBonus(game);
            CompleteAndCount(game, "work:once");
            var paid = Counts(game);
            int skill = game.Student.GetSkillValue(Cooking);
            int reputation = game.Adult.Reputation;

            Assert.That(game.CompleteFacilityWork(ChefWork, "work:once", 100, autoSkip: false).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            var again = TestWorld.Start(saves.Store);
            Assert.That(again.CompleteFacilityWork(ChefWork, "work:once", 100, autoSkip: false).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            foreach (var session in new[] { game, again })
            {
                Assert.That(Counts(session), Is.EqualTo(paid), "보상은 한 번만");
                Assert.That(session.Student.GetSkillValue(Cooking), Is.EqualTo(skill), "기술은 한 번만");
                Assert.That(session.Adult.Reputation, Is.EqualTo(reputation), "명성은 한 번만");
            }
        }

        [Test]
        public void BonusOverflowsInventory_CommandRefusesAndChangesNothing()
        {
            using var saves = new TempSaves();
            var decorating = ChefSetup.Adult(saves);
            EnterChef(decorating);
            PlaceCozyUntilBonus(decorating);
            int levelPercent = LevelRewardPercent(decorating);
            var plain = RewardsAt(levelPercent);
            var bonused = RewardsAt(levelPercent + decorating.WorkExtrasFor(ChefWork).RewardPercentBonus);
            Assert.That(bonused.Allowance + bonused.Materials, Is.GreaterThan(plain.Allowance + plain.Materials), "준비: 이득이 보상 수를 늘린다");

            // 준비: 이득 없는 보상이 꼭 들어갈 만큼만 남긴다. 돈(allowance)은 지갑으로 가므로 지갑을 상한 바로 아래로 채우고,
            // 재료는 한 칸만 남기고 돌로 채운 뒤 그 칸을 이득 없는 재료 수만큼만 비운다.
            var stone = TestWorld.Definitions.Get<Rootborn.Core.Inventory.ItemDefinition>("Stone");
            var materials = TestWorld.Definitions.Get<Rootborn.Core.Inventory.ItemDefinition>(Materials);
            int walletBalance = Rootborn.Core.Inventory.PersonalWallet.MaxBalance - plain.Allowance;
            SaveEdits.EditSnapshot(saves.Store, s =>
            {
                s.Wallet = new Rootborn.Core.Inventory.WalletSaveData { Version = Rootborn.Core.Inventory.WalletSaveData.KnownVersion, Balance = walletBalance };
                s.Inventory ??= new Rootborn.Core.Inventory.PlayerInventorySaveData();
                s.Inventory.Items = new[]
                {
                    new Rootborn.Core.Inventory.PlayerInventoryItemSaveData { ItemId = "Stone", Count = stone.MaxStack * (Rootborn.Core.Inventory.ItemInventory.MaxSlots - 1) },
                    new Rootborn.Core.Inventory.PlayerInventoryItemSaveData { ItemId = Materials, Count = materials.MaxStack - plain.Materials },
                };
            });

            var game = TestWorld.Start(saves.Store);
            Assert.That(game.WorkExtrasFor(ChefWork).RewardPercentBonus, Is.GreaterThan(0), "준비: 저장된 배치의 이득");
            var before = Counts(game);
            Assert.That(before, Is.EqualTo((walletBalance, materials.MaxStack - plain.Materials)), "준비: 지갑과 가방을 고친 대로 읽었다");
            var student = game.Student.ToSaveData();
            int reputation = game.Adult.Reputation;
            int cooking = game.Student.GetSkillValue(Cooking);

            Assert.That(game.CompleteFacilityWork(ChefWork, "work:overflow", 100, autoSkip: false).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));
            Assert.That(Counts(game), Is.EqualTo(before));
            Assert.That(game.Student.GetSkillValue(Cooking), Is.EqualTo(cooking));
            Assert.That(game.Student.TimeMinutes, Is.EqualTo(student.TimeMinutes));
            Assert.That(game.Student.TimePointsSpentToday, Is.EqualTo(student.TimePointsSpentToday));
            Assert.That(game.Adult.Reputation, Is.EqualTo(reputation));
            Assert.That(game.Adult.GetSuccessfulCompletions(ChefWorkDefinition), Is.EqualTo(0));

            // 다 치우면 이득 없는 보상은 꼭 들어간다: 거절은 이득 때문이었고, 거절된 요청은 쓴 것으로 남지 않았다.
            var cleared = TestWorld.Start(saves.Store);
            EnterChef(cleared);
            Assert.That(cleared.ClearFurniture(), Is.True);
            Assert.That(cleared.CompleteFacilityWork(ChefWork, "work:overflow", 100, autoSkip: false).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(Counts(cleared), Is.EqualTo((before.Allowance + plain.Allowance, before.Materials + plain.Materials)));
        }

        // ---------- 시설 조회도 공간 이득을 본다(설계 2026-11-13 4절) ----------

        // 요리사 2단계 성인, 첫 일 숙련·성공을 채워 자동 넘기기 조건을 맞춘다(AdultAutoCompleteTests와 같은 준비). 일터에 아늑한 가구를 놓아 이득을 만든다.
        private static (int Allowance, int Materials) PrepareBonusOverflow(TempSaves saves, out int bonus)
        {
            TestWorld.Start(saves.Store).TalkTo("npc.first-guide"); // 첫 저장
            SaveEdits.EditSnapshot(saves.Store, s =>
            {
                SaveEdits.MakeChefCandidateReady(s);
                s.Adult.FacilityId = ChefFacility;
                s.Adult.FacilityLevel = 2;
                s.Adult.Works = new[] { new FacilityWorkProgressSaveData { WorkId = ChefWork, Mastery = 100, SuccessfulCompletions = 5 } };
            });
            var decorating = TestWorld.Start(saves.Store);
            EnterChef(decorating);
            PlaceCozyUntilBonus(decorating);
            bonus = decorating.WorkExtrasFor(ChefWork).RewardPercentBonus;
            int levelPercent = LevelRewardPercent(decorating);
            var plain = RewardsAt(levelPercent);
            var bonused = RewardsAt(levelPercent + bonus);
            Assert.That(bonus, Is.GreaterThan(0), "준비: 꾸며서 이득이 생겼다");
            Assert.That(bonused.Allowance + bonused.Materials, Is.GreaterThan(plain.Allowance + plain.Materials), "준비: 이득이 보상 수를 늘린다");
            FillSoOnlyThePlainRewardFits(saves, plain);
            return plain;
        }

        // 이득 없는 보상이 꼭 들어갈 만큼만 남긴다(BonusOverflowsInventory_CommandRefusesAndChangesNothing와 같은 방법).
        private static void FillSoOnlyThePlainRewardFits(TempSaves saves, (int Allowance, int Materials) plain)
        {
            var stone = TestWorld.Definitions.Get<Rootborn.Core.Inventory.ItemDefinition>("Stone");
            var materials = TestWorld.Definitions.Get<Rootborn.Core.Inventory.ItemDefinition>(Materials);
            SaveEdits.EditSnapshot(saves.Store, s =>
            {
                s.Wallet = new Rootborn.Core.Inventory.WalletSaveData { Version = Rootborn.Core.Inventory.WalletSaveData.KnownVersion, Balance = Rootborn.Core.Inventory.PersonalWallet.MaxBalance - plain.Allowance };
                s.Inventory ??= new Rootborn.Core.Inventory.PlayerInventorySaveData();
                s.Inventory.Items = new[]
                {
                    new Rootborn.Core.Inventory.PlayerInventoryItemSaveData { ItemId = "Stone", Count = stone.MaxStack * (Rootborn.Core.Inventory.ItemInventory.MaxSlots - 1) },
                    new Rootborn.Core.Inventory.PlayerInventoryItemSaveData { ItemId = Materials, Count = materials.MaxStack - plain.Materials },
                };
            });
        }

        [Test]
        public void FacilityStatus_AutoSkip_CountsTheSpaceBonus_LikeTheCommand()
        {
            using var saves = new TempSaves();
            PrepareBonusOverflow(saves, out _);
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.WorkExtrasFor(ChefWork).RewardPercentBonus, Is.GreaterThan(0), "준비: 저장된 배치의 이득");
            var before = Counts(game);
            var adult = game.Adult.ToSaveData();
            int timePoints = game.Student.TimePointsRemaining;

            var status = game.FacilityStatus(ChefFacility).Works.Single(w => w.Work.Id == ChefWork);
            Assert.That(status.AutoSkip, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity), "이득을 얹은 보상이 안 들어가니 단추를 끈다");
            Assert.That(game.CompleteFacilityWork(ChefWork, "auto:overflow", 100, autoSkip: true).Kind, Is.EqualTo(status.AutoSkip), "명령도 같은 이유");
            Assert.That(Counts(game), Is.EqualTo(before), "가방 그대로");
            Assert.That(game.Student.TimePointsRemaining, Is.EqualTo(timePoints));
            Assert.That(game.Adult.GetSuccessfulCompletions(ChefWorkDefinition), Is.EqualTo(adult.Works.Single(w => w.WorkId == ChefWork).SuccessfulCompletions), "진행 그대로");
            Assert.That(game.Adult.Reputation, Is.EqualTo(adult.Reputation));

            // 다 치우면 이득이 없어 보상이 들어간다: 조회와 명령이 함께 된다.
            var cleared = TestWorld.Start(saves.Store);
            EnterChef(cleared);
            Assert.That(cleared.ClearFurniture(), Is.True);
            Assert.That(cleared.FacilityStatus(ChefFacility).Works.Single(w => w.Work.Id == ChefWork).AutoSkip, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(cleared.CompleteFacilityWork(ChefWork, "auto:overflow", 100, autoSkip: true).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
        }

        [Test]
        public void MinigameWork_RewardNoLongerFitsAtSettle_IsRefused_AndTheReservationStays()
        {
            // 지금 동작을 고정한다(규칙은 바꾸지 않는다): 미니게임 일은 예약 때 이득을 얹은 비율을 적어 두고 정산 때 가방을 본다.
            // 판을 끝냈는데 보상이 안 들어가면 정산이 거절되고 아무것도 바뀌지 않으며 예약은 남는다 — 가방을 비우면 같은 요청으로 다시 정산된다.
            using var saves = new TempSaves();
            var plain = PrepareBonusOverflow(saves, out int bonus);
            var game = TestWorld.Start(saves.Store);
            var direct = game.FacilityStatus(ChefFacility).Works.Single(w => w.Work.Id == ChefWork).Direct;
            Assert.That(direct, Is.EqualTo(AdultCareerResultKind.Applied), "미니게임 일의 예약은 가방을 보지 않는다(지금 동작)");
            Assert.That(game.ReserveFacilityWork(ChefWork, "work:overflow", 4).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            var before = Counts(game);
            int reputation = game.Adult.Reputation;
            int successes = game.Adult.GetSuccessfulCompletions(ChefWorkDefinition);

            Assert.That(game.SettleFacilityWork("work:overflow", 100).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));
            Assert.That(Counts(game), Is.EqualTo(before), "가방 그대로");
            Assert.That(game.Adult.Reputation, Is.EqualTo(reputation));
            Assert.That(game.Adult.GetSuccessfulCompletions(ChefWorkDefinition), Is.EqualTo(successes));
            Assert.That(game.Adult.PendingWork?.RequestId, Is.EqualTo("work:overflow"), "예약은 남는다");
            var reloaded = TestWorld.Start(saves.Store);
            Assert.That(reloaded.Adult.PendingWork?.RequestId, Is.EqualTo("work:overflow"), "다시 읽어도 예약이 남는다");

            // 가방을 비우면 같은 요청으로 정산되고, 보상은 예약 때의 비율(레벨 + 그때의 이득)이다.
            SaveEdits.EditSnapshot(saves.Store, s =>
            {
                s.Inventory.Items = System.Array.Empty<Rootborn.Core.Inventory.PlayerInventoryItemSaveData>();
                s.Wallet = new Rootborn.Core.Inventory.WalletSaveData { Version = Rootborn.Core.Inventory.WalletSaveData.KnownVersion, Balance = 0 };
            });
            var freed = TestWorld.Start(saves.Store);
            var freedBefore = Counts(freed);
            Assert.That(freed.SettleFacilityWork("work:overflow", 100).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            var paid = (Counts(freed).Allowance - freedBefore.Allowance, Counts(freed).Materials - freedBefore.Materials);
            Assert.That(paid.Item2, Is.EqualTo(RewardsAt(LevelRewardPercent(freed) + bonus).Materials));
            Assert.That(freed.SettleFacilityWork("work:overflow", 100).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest), "한 번만");
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
