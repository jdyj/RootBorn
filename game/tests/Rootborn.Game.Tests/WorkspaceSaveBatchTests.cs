using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Core.Workspaces;
using Rootborn.Data.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 일터 배치는 GameSession.Save()의 저장 묶음 안에서 가방·일터 진행과 함께 쓴다(설계 2026-11-13 3절).
    /// 템플릿 적용의 저장이 어느 단계에서 끊겨도 다시 시작하면 배치·돈·적용 기록이 모두 옛것이거나 모두 새것이다.
    /// 일터의 놓기·옮기기·치우기도 같은 묶음으로 쓰고, 새 GameSession으로 다시 읽으면 남아 있다.
    /// 끊김은 FileSaveStore의 시험용 단계 고리에서 던져 흉내 낸다. 자동 테스트다.
    /// </summary>
    public sealed class WorkspaceSaveBatchTests
    {
        private const string ChefWorkspace = "workspace.chef";
        private const string CozyTemplate = "template.chef.cozy";
        private const string Allowance = "item.allowance";
        private const string FreeDesk = "growth31.assembly.desk";
        private const string Request = "req:cozy";
        private const int StartMoney = 50;

        private sealed class SimulatedCrash : Exception
        {
        }

        private static Workspace Chef => TestWorld.Definitions.Get<Workspace>(ChefWorkspace);
        private static WorkspaceTemplate Cozy => TestWorld.Definitions.Get<WorkspaceTemplate>(CozyTemplate);
        private static WorkspaceTemplate Basic => TestWorld.Definitions.Get<WorkspaceTemplate>(Chef.DefaultTemplateId);
        private static int Cost => Cozy.Costs.Where(c => c.Item == Allowance).Sum(c => c.Count);

        private static void EnterChef(GameSession game) => game.EnterPlacementSurface(WorkspaceRoom.GridFor(game.Definitions, ChefWorkspace), Chef.SurfaceId);

        private static string[] LayoutIds(GameSession game) => game.House.Placed.Select(p => p.Definition.StableId).OrderBy(s => s, StringComparer.Ordinal).ToArray();

        private static string[] Sorted(IEnumerable<string> ids) => ids.OrderBy(s => s, StringComparer.Ordinal).ToArray();

        private static (int X, int Y)[] SavedAnchors(TempSaves saves)
        {
            saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.WorkspaceLayout(Chef.SurfaceId, TestWorld.Player), out var layout);
            return layout?.Items?.Select(i => (i.AnchorX, i.AnchorY)).ToArray() ?? Array.Empty<(int, int)>();
        }

        // 요리사 성인이 돈 50을 들고 일터에 한 번 들어가(기본 템플릿이 깔리고 저장된다) 템플릿을 적용하기 직전의 슬롯.
        private static SortedDictionary<string, byte[]> PrepareBeforeApply(TempSaves saves)
        {
            ChefSetup.Adult(saves);
            SaveEdits.EditSnapshot(saves.Store, s => SaveEdits.Fill(s, Allowance, StartMoney));
            EnterChef(TestWorld.Start(saves.Store));
            return SlotCheckpoints.Read(saves, TestWorld.Slot);
        }

        private static readonly Lazy<(SortedDictionary<string, byte[]> Before, SortedDictionary<string, byte[]> After, List<string> Steps)> Reference =
            new Lazy<(SortedDictionary<string, byte[]>, SortedDictionary<string, byte[]>, List<string>)>(() =>
            {
                using var saves = new TempSaves();
                var before = PrepareBeforeApply(saves);
                var store = new FileSaveStore(saves.Path);
                var game = TestWorld.Start(store);
                EnterChef(game);
                var steps = new List<string>();
                store.BatchStepForTests = steps.Add;
                Assert.That(game.ApplyTemplate(CozyTemplate, Request), Is.EqualTo(WorkspaceResultKind.Applied));
                return (before, SlotCheckpoints.Read(saves, TestWorld.Slot), steps);
            });

        public static IEnumerable<TestCaseData> StopPoints()
        {
            var steps = Reference.Value.Steps;
            for (int k = 0; k < steps.Count; k++) yield return new TestCaseData(k).SetName($"ApplyTemplate_StoppedAfterStep{k:D2}_{steps[k].Replace(':', '_')}");
        }

        [Test]
        public void ApplyingATemplate_WritesLayoutMoneyAndRecord_InOneBatch()
        {
            var (before, after, steps) = Reference.Value;
            var changed = after.Keys.Where(k => !before.ContainsKey(k) || !before[k].AsSpan().SequenceEqual(after[k])).ToArray();
            Assert.That(changed, Does.Contain(SaveFileNames.WorkspaceLayout(Chef.SurfaceId, TestWorld.Player)), "시험이 성립하려면 배치 파일이 바뀌어야 한다");
            Assert.That(changed, Does.Contain(SaveFileNames.WorkspaceProgress(TestWorld.Player)));
            Assert.That(changed, Does.Contain(SaveFileNames.AdultCareerSnapshot(TestWorld.Player)));
            Assert.That(steps.Count(s => s == "mark"), Is.EqualTo(1), "저장 한 번이 묶음 하나다: " + string.Join(", ", steps));
            Assert.That(steps, Does.Contain("stage:" + SaveFileNames.WorkspaceLayout(Chef.SurfaceId, TestWorld.Player)), "배치 파일이 묶음 안에서 쓰인다: " + string.Join(", ", steps));
        }

        [TestCaseSource(nameof(StopPoints))]
        public void ApplyTemplate_StoppedAtAnyStep_RestartsAllBeforeOrAllAfter(int k)
        {
            var (before, after, steps) = Reference.Value;
            using var saves = new TempSaves();
            SlotCheckpoints.WriteAll(saves, before, TestWorld.Slot);
            var store = new FileSaveStore(saves.Path);
            var game = TestWorld.Start(store);
            EnterChef(game);
            int seen = 0;
            store.BatchStepForTests = _ => { if (seen++ == k) throw new SimulatedCrash(); };
            Assert.Throws<SimulatedCrash>(() => game.ApplyTemplate(CozyTemplate, Request));

            var restarted = TestWorld.Start(new FileSaveStore(saves.Path));
            var files = SlotCheckpoints.Read(saves, TestWorld.Slot);
            bool isBefore = SlotCheckpoints.Same(files, before);
            bool isAfter = SlotCheckpoints.Same(files, after);
            TestContext.Out.WriteLine($"단계 {k}({steps[k]}) 뒤에 멈춤 → {(isBefore ? "전부 옛것" : isAfter ? "전부 새것" : "섞임")}");
            Assert.That(isBefore || isAfter, Is.True, "슬롯이 전부 옛것이거나 전부 새것이다");

            int money = restarted.Inventory.CountOf(restarted.Item(Allowance));
            EnterChef(restarted);
            if (isAfter)
            {
                Assert.That(money, Is.EqualTo(StartMoney - Cost));
                Assert.That(LayoutIds(restarted), Is.EqualTo(Sorted(Cozy.Placements.Select(p => p.FurnitureId))));
                Assert.That(restarted.ApplyTemplate(CozyTemplate, Request), Is.EqualTo(WorkspaceResultKind.DuplicateRequest));
            }
            else
            {
                Assert.That(money, Is.EqualTo(StartMoney), "돈은 그대로");
                Assert.That(LayoutIds(restarted), Is.EqualTo(Sorted(Basic.Placements.Select(p => p.FurnitureId))), "배치는 기본 템플릿 그대로");
                Assert.That(restarted.CanApplyTemplate(CozyTemplate), Is.EqualTo(WorkspaceResultKind.Applied), "적용 기록도 없다");
                Assert.That(restarted.ApplyTemplate(CozyTemplate, Request), Is.EqualTo(WorkspaceResultKind.Applied), "옛것이면 같은 요청으로 다시 적용할 수 있다");
                Assert.That(restarted.Inventory.CountOf(restarted.Item(Allowance)), Is.EqualTo(StartMoney - Cost));
            }

            var last = TestWorld.Start(new FileSaveStore(saves.Path));
            Assert.That(last.ApplyTemplate(CozyTemplate, Request), Is.Not.EqualTo(WorkspaceResultKind.Applied));
            Assert.That(last.Inventory.CountOf(last.Item(Allowance)), Is.EqualTo(StartMoney - Cost), "돈은 한 번만");
        }

        [Test]
        public void ApplyTemplate_Succeeds_AllThreeChange_AndTheSameRequestPaysOnce()
        {
            using var saves = new TempSaves();
            PrepareBeforeApply(saves);
            var game = TestWorld.Start(saves.Store);
            EnterChef(game);
            Assert.That(game.ApplyTemplate(CozyTemplate, Request), Is.EqualTo(WorkspaceResultKind.Applied));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.Inventory.CountOf(again.Item(Allowance)), Is.EqualTo(StartMoney - Cost));
            EnterChef(again);
            Assert.That(LayoutIds(again), Is.EqualTo(Sorted(Cozy.Placements.Select(p => p.FurnitureId))));
            Assert.That(again.CanApplyTemplate(CozyTemplate), Is.EqualTo(WorkspaceResultKind.Applied), "다른 요청이면 다시 살 수 있다(확인용 요청은 적용 기록이 아니다)");
            Assert.That(again.ApplyTemplate(CozyTemplate, Request), Is.EqualTo(WorkspaceResultKind.DuplicateRequest), "적용 기록이 저장됐다");
            Assert.That(again.Inventory.CountOf(again.Item(Allowance)), Is.EqualTo(StartMoney - Cost), "돈은 한 번만");
        }

        // ---------- 놓기·옮기기·치우기 ----------

        [Test]
        public void PlaceMoveRemoveClear_InAWorkspace_SurviveANewSession()
        {
            using var saves = new TempSaves();
            var game = ChefSetup.Adult(saves);
            EnterChef(game);

            Assert.That(game.PlaceFurniture(FreeDesk, WorkspaceRoom.FreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            AssertReadBack(saves, game, "놓기");

            var first = game.House.Placed[0];
            Assert.That(game.MoveFurniture(first.InstanceId, WorkspaceRoom.FreeAnchor(game, first.Definition.StableId), FurniturePlacementDirection.North), Is.True);
            AssertReadBack(saves, game, "옮기기");

            Assert.That(game.RemoveFurniture(game.House.Placed[1].InstanceId), Is.True);
            AssertReadBack(saves, game, "치우기");

            Assert.That(game.ClearFurniture(), Is.True);
            AssertReadBack(saves, game, "다 치우기");
        }

        private static void AssertReadBack(TempSaves saves, GameSession game, string what)
        {
            var expected = game.House.Placed.Select(p => (p.Definition.StableId, p.Anchor)).ToArray();
            var again = TestWorld.Start(saves.Store);
            EnterChef(again);
            Assert.That(again.House.Placed.Select(p => (p.Definition.StableId, p.Anchor)), Is.EqualTo(expected), what + " 뒤 새 GameSession으로 읽으면 배치가 같다");
        }

        [TestCase("move")]
        [TestCase("remove")]
        [TestCase("clear")]
        public void MoveRemoveClear_InAWorkspace_WriteTheLayoutInsideTheBatch(string command)
        {
            using var saves = new TempSaves();
            ChefSetup.Adult(saves);
            EnterChef(TestWorld.Start(saves.Store));
            var anchorsBefore = SavedAnchors(saves);
            Assert.That(anchorsBefore, Is.Not.Empty, "준비: 기본 템플릿이 저장돼 있다");

            var store = new FileSaveStore(saves.Path);
            var game = TestWorld.Start(store);
            EnterChef(game);
            var first = game.House.Placed[0];
            var target = WorkspaceRoom.FreeAnchor(game, first.Definition.StableId);
            store.BatchStepForTests = _ => throw new SimulatedCrash();
            Assert.Throws<SimulatedCrash>(() =>
            {
                if (command == "move") game.MoveFurniture(first.InstanceId, target, FurniturePlacementDirection.North);
                else if (command == "remove") game.RemoveFurniture(first.InstanceId);
                else game.ClearFurniture();
            }, "배치 쓰기는 저장 묶음을 거친다");

            Assert.That(SavedAnchors(saves), Is.EqualTo(anchorsBefore), "묶음이 끊기면 배치 파일은 옛것 그대로");
        }

        [Test]
        public void HousePlacement_StillWritesTheHouseLayout()
        {
            // 집(일터 아닌 면)의 배치 쓰기는 바꾸지 않았다: 놓기·옮기기·치우기 뒤 새 GameSession으로 읽으면 남아 있다.
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.PlaceFurniture(FreeDesk, HouseRoom.FirstFreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            Assert.That(game.PlaceFurniture(FreeDesk, HouseRoom.FirstFreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            var moved = game.House.Placed[0];
            Assert.That(game.MoveFurniture(moved.InstanceId, HouseRoom.FirstFreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            Assert.That(game.RemoveFurniture(game.House.Placed[1].InstanceId), Is.True);
            var expected = game.House.Placed.Select(p => (p.Definition.StableId, p.Anchor)).ToArray();

            var again = TestWorld.Start(saves.Store);
            again.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(again.House.Placed.Select(p => (p.Definition.StableId, p.Anchor)), Is.EqualTo(expected));
        }
    }
}
