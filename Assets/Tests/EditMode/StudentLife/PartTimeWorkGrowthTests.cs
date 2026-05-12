using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class PartTimeWorkGrowthTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void WORK_EDIT_001_WorkAndWorkplaceDefinitionsLoadFromRegistryDataPath()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.IsNotNull(registry.PartTimeWorks, "Part-time work definitions must be exposed by GameDataRegistry.");
            Assert.IsNotNull(registry.Workplaces, "Workplaces must be exposed by GameDataRegistry.");
            Assert.GreaterOrEqual(registry.PartTimeWorks.Length, 1, "At least one part-time work definition must be registered.");
            Assert.GreaterOrEqual(registry.Workplaces.Length, 1, "At least one workplace definition must be registered.");
            StringAssert.StartsWith("Assets/Data/StudentLife/PartTimeWork/", AssetDatabase.GetAssetPath(registry.PartTimeWorks[0]));
            StringAssert.StartsWith("Assets/Data/StudentLife/PartTimeWork/", AssetDatabase.GetAssetPath(registry.Workplaces[0]));
        }

        [Test]
        public void WORK_EDIT_002_RequirementsAndOutcomeStrategiesRunWithoutEntityIdBranching()
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var requirement = ScriptableObject.CreateInstance<WorkTraitThresholdRequirement>();
            var progress = new StudentLifeProgress("slot-work", "player-work", 8, 8);
            try
            {
                trait.ConfigureForTests("trait.responsibility", "trait.responsibility");
                requirement.ConfigureForTests(trait, 2);

                Assert.IsFalse(requirement.IsSatisfied(progress));
                progress.AddTrait(trait, 2);
                Assert.IsTrue(requirement.IsSatisfied(progress));
            }
            finally
            {
                Object.DestroyImmediate(requirement);
                Object.DestroyImmediate(trait);
            }
        }

        [Test]
        public void WORK_EDIT_003_RewardPreflightBlocksFullInventoryWithoutMutatingProgressOrInventory()
        {
            var wage = MakeItem("item.wage-token", 99);
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var outcome = ScriptableObject.CreateInstance<WorkTraitDeltaOutcome>();
            var work = ScriptableObject.CreateInstance<PartTimeWorkDefinition>();
            var workplace = ScriptableObject.CreateInstance<WorkplaceDefinition>();
            var progress = new StudentLifeProgress("slot-work", "player-work", 4, 4);
            var inventory = MakeFullInventoryWithDifferentItems();
            var runner = new PartTimeWorkRunner();
            try
            {
                trait.ConfigureForTests("trait.responsibility", "trait.responsibility");
                outcome.ConfigureForTests(trait, 3);
                workplace.ConfigureForTests("workplace.corner-store", "workplace.corner-store", "location.store", "npc.owner", null);
                work.ConfigureForTests("work.shift.store", "work.shift.store", workplace, 45, 1, 1, 1, new[] { new InventoryGrant(wage, 1) }, null, new WorkOutcomeBase[] { outcome });

                Assert.IsFalse(runner.TryPerform(work, progress, inventory, "work-request-1", out var result));

                Assert.AreEqual(LifeActivityResultKind.InsufficientResources, result.Kind);
                Assert.AreEqual(4, progress.Energy, "Energy must not be spent when reward cannot fit.");
                Assert.AreEqual(4, progress.Focus, "Focus must not be spent when reward cannot fit.");
                Assert.AreEqual(0, progress.GetTraitValue(trait), "Outcome must not apply when reward preflight fails.");
                Assert.AreEqual(0, inventory.CountOf(wage), "Reward item must not be partially granted.");
                Assert.IsFalse(progress.HasAppliedRequest("work-request-1"), "Request must remain unapplied on reward preflight failure.");
            }
            finally
            {
                Object.DestroyImmediate(work);
                Object.DestroyImmediate(workplace);
                Object.DestroyImmediate(outcome);
                Object.DestroyImmediate(trait);
                Object.DestroyImmediate(wage);
            }
        }

        [Test]
        public void WORK_EDIT_004_WorkAppliesItemRewardCostAndGenericGrowthOutcome()
        {
            var wage = MakeItem("item.wage-token", 99);
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var outcome = ScriptableObject.CreateInstance<WorkTraitDeltaOutcome>();
            var work = ScriptableObject.CreateInstance<PartTimeWorkDefinition>();
            var workplace = ScriptableObject.CreateInstance<WorkplaceDefinition>();
            var progress = new StudentLifeProgress("slot-work", "player-work", 5, 5);
            var inventory = new Inventory();
            var runner = new PartTimeWorkRunner();
            try
            {
                trait.ConfigureForTests("trait.responsibility", "trait.responsibility");
                outcome.ConfigureForTests(trait, 2);
                workplace.ConfigureForTests("workplace.corner-store", "workplace.corner-store", "location.store", "npc.owner", null);
                work.ConfigureForTests("work.shift.store", "work.shift.store", workplace, 30, 2, 1, 1, new[] { new InventoryGrant(wage, 3) }, null, new WorkOutcomeBase[] { outcome });

                Assert.IsTrue(runner.TryPerform(work, progress, inventory, "work-request-2", out var result));

                string expectedGrowth = "part-time-work:work.shift.store:+trait.responsibility=2";
                string expectedReward = "part-time-work:work.shift.store:+item.wage-token=3";
                Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
                Assert.AreEqual("work.shift.store", result.WorkId);
                Assert.AreEqual("workplace.corner-store", result.WorkplaceId);
                Assert.AreEqual(3, progress.Energy);
                Assert.AreEqual(4, progress.Focus);
                Assert.AreEqual(1, progress.Stress);
                Assert.AreEqual(3, inventory.CountOf(wage));
                Assert.AreEqual(2, progress.GetTraitValue(trait));
                CollectionAssert.Contains(result.OutcomeLogIds, expectedGrowth);
                CollectionAssert.Contains(result.OutcomeLogIds, expectedReward);
                CollectionAssert.Contains(progress.GetTodayActivityIds(), "work.shift.store");
                CollectionAssert.Contains(progress.GetTodayResultLogIds(), expectedGrowth);
                CollectionAssert.Contains(progress.GetTodayResultLogIds(), expectedReward);
            }
            finally
            {
                Object.DestroyImmediate(work);
                Object.DestroyImmediate(workplace);
                Object.DestroyImmediate(outcome);
                Object.DestroyImmediate(trait);
                Object.DestroyImmediate(wage);
            }
        }

        [Test]
        public void WORK_EDIT_005_SaveLoadKeepsRequestAppliedAndPreventsDuplicateReward()
        {
            var wage = MakeItem("item.wage-token", 99);
            var work = ScriptableObject.CreateInstance<PartTimeWorkDefinition>();
            var workplace = ScriptableObject.CreateInstance<WorkplaceDefinition>();
            var progress = new StudentLifeProgress("slot-work", "player-work", 5, 5);
            var inventory = new Inventory();
            var runner = new PartTimeWorkRunner();
            try
            {
                workplace.ConfigureForTests("workplace.corner-store", "workplace.corner-store", "location.store", "npc.owner", null);
                work.ConfigureForTests("work.shift.store", "work.shift.store", workplace, 30, 0, 0, 0, new[] { new InventoryGrant(wage, 2) }, null, null);

                Assert.IsTrue(runner.TryPerform(work, progress, inventory, "same-work-request", out var first));
                var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData(), null, null, null);
                Assert.IsFalse(runner.TryPerform(work, restored, inventory, "same-work-request", out var duplicate));

                Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
                Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
                Assert.AreEqual(2, inventory.CountOf(wage), "Reloaded duplicate request must not pay again.");
                Assert.AreEqual(1, PartTimeWorkDayLog.FromResultLogs(restored.GetTodayResultLogIds()).WorkIds.Length);
            }
            finally
            {
                Object.DestroyImmediate(work);
                Object.DestroyImmediate(workplace);
                Object.DestroyImmediate(wage);
            }
        }

        [Test]
        public void WORK_EDIT_006_SourceAvoidsEntityIdBranchingPatterns()
        {
            string[] files =
            {
                "Assets/Scripts/Game/StudentLife/PartTimeWorkDefinition.cs",
                "Assets/Scripts/Game/StudentLife/PartTimeWorkRunner.cs",
                "Assets/Scripts/Game/StudentLife/PartTimeWorkOutcomes.cs",
                "Assets/Scripts/Game/StudentLife/PartTimeWorkRequirements.cs",
                "Assets/Scripts/Game/StudentLife/PartTimeWorkInteractor.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsTrue(File.Exists(files[i]), files[i] + " must exist.");
                string source = File.ReadAllText(files[i]);
                StringAssert.DoesNotContain("workId ==", source);
                StringAssert.DoesNotContain("workplaceId ==", source);
                StringAssert.DoesNotContain("itemId ==", source);
                StringAssert.DoesNotContain("switch (workId", source);
                StringAssert.DoesNotContain("switch (workplaceId", source);
                StringAssert.DoesNotContain("switch (itemId", source);
            }
        }

        private static Inventory MakeFullInventoryWithDifferentItems()
        {
            var inventory = new Inventory();
            for (int i = 0; i < Inventory.MaxSlots; i++)
            {
                inventory.Add(MakeItem("item.filler-" + i, 1), 1);
            }

            return inventory;
        }

        private static ItemDefinition MakeItem(string id, int maxStack)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_id", id);
            SetField(item, "_displayKey", id);
            SetField(item, "_maxStack", maxStack);
            return item;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, target.GetType().Name + " missing field " + fieldName);
            field.SetValue(target, value);
        }
    }
}
