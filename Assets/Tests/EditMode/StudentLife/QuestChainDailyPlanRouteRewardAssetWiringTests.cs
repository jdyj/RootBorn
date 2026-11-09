using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.Objectives;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class QuestChainDailyPlanRouteRewardAssetWiringTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string QuestChainRuntimeInstallerPath = "Assets/Scripts/UI/Quests/QuestChainRuntimeInstaller.cs";
        private const string ChainId = "questchain.test.career-interest.library";
        private const string WorldStateId = "world.questchain.library-route-reward";
        private const string RouteId = "daily.route.questchain.library-reward";

        [Test]
        public void QuestChainRuntimeInstaller_RewardContextProvidesAndPersistsWorldStateProgress()
        {
            // World state moved into the career owner's atomic save session: the owner loads it once, the quest-chain
            // reward context reads it from the owner/session, and the session commit saves it with the inventory.
            string service = File.ReadAllText("Assets/Scripts/UI/Quests/QuestChainRuntimeService.cs");
            string owner = File.ReadAllText("Assets/Scripts/UI/Careers/AdultCareerRuntimeComponent.cs");

            StringAssert.Contains("WorldStateProgressPersistence.LoadOrCreate", owner, "The career owner must load WorldStateProgress for world-state route rewards.");
            StringAssert.Contains("owner.World", service, "Quest-chain reward context must use the owner's world state.");
            StringAssert.Contains("session.World", service, "Quest-chain reward claims must apply world-state rewards inside the saved session.");
        }

        [Test]
        public void RegisteredQuestChainCompletionRewardReferencesSameFlagUsedByDailyPlanRoute()
        {
            var registry = LoadRegistry();
            var chain = FindChain(registry);
            var flag = FindWorldState(registry);
            var route = FindRoute(registry);

            Assert.IsNotNull(chain, "Quest-chain route reward chain must be registered.");
            Assert.IsNotNull(flag, "Quest-chain route reward world-state flag must be registered.");
            Assert.IsNotNull(route, "Quest-chain reward Daily Plan route must be registered in a route set.");
            Assert.IsTrue(chain.CompletionRewards.OfType<WorldStateChangeQuestReward>().Any(reward => RewardReferencesFlag(reward, flag)), "Quest-chain completion reward must activate the same world-state flag used by the Daily Plan route.");
            Assert.IsTrue(route.Requirements.OfType<DailyPlanWorldStateRequirement>().Any(requirement => RequirementReferencesFlag(requirement, flag)), "Quest-chain reward route must be gated by the quest reward world-state flag.");
        }

        [Test]
        public void QuestChainRewardRoute_IsUnavailableBeforeRewardFlagAndAvailableAfterRewardClaim()
        {
            var registry = LoadRegistry();
            var chain = FindChain(registry);
            var flag = FindWorldState(registry);
            var route = FindRoute(registry);
            var reward = chain != null ? chain.CompletionRewards.OfType<WorldStateChangeQuestReward>().FirstOrDefault(candidate => RewardReferencesFlag(candidate, flag)) : null;
            var student = new StudentLifeProgress("slot-quest-route", "player-quest-route", 100, 100);
            var world = new WorldStateProgress("slot-quest-route", "player-quest-route");
            var locked = new DailyPlanRouteContext(student, world, null, null);

            Assert.IsNotNull(chain);
            Assert.IsNotNull(flag);
            Assert.IsNotNull(route);
            Assert.IsNotNull(reward, "Quest-chain completion reward must be a world-state reward for the route flag.");
            Assert.IsFalse(route.IsAvailable(in locked), "Quest-chain reward route must start locked before reward claim.");

            var context = new RewardRuntimeContext(null, null, null, null, student, world);
            Assert.IsTrue(reward.CanApply(in context), "World-state reward should be claimable before the flag is active.");
            reward.Apply(in context);
            Assert.IsTrue(world.IsActive(flag), "World-state reward must activate the route flag.");
            Assert.IsFalse(reward.CanApply(in context), "World-state reward must not be claimable again after activating its flag.");

            var unlocked = new DailyPlanRouteContext(student, world, null, null);
            Assert.IsTrue(route.IsAvailable(in unlocked), "Quest-chain reward route must become available after reward flag activation.");
        }

        [Test]
        public void ObjectiveJournal_ExposesUnlockedUnselectedQuestRewardRouteAsHintThenMovesSelectedRouteToGoals()
        {
            var registry = LoadRegistry();
            var flag = FindWorldState(registry);
            var route = FindRoute(registry);
            var player = new GameObject("Player");
            string rootDirectory = Path.Combine(Path.GetTempPath(), "rootborn-quest-route-objective-" + System.Guid.NewGuid().ToString("N"));
            Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(rootDirectory);

            try
            {
                Assert.IsNotNull(flag);
                Assert.IsNotNull(route);
                var student = player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("slot-quest-route-objective", "player-quest-route-objective", 100, 100, 0, 480);
                player.AddComponent<Rootborn.Game.Player.PlayerIdentity>().Configure("player-quest-route-objective");
                var daily = player.AddComponent<DailyPlanProgressComponent>();
                daily.RestoreFromSaveData(new DailyPlanProgressSaveData { SaveSlot = "slot-quest-route-objective", PlayerId = "player-quest-route-objective", CurrentDay = 1 }, "slot-quest-route-objective", "player-quest-route-objective", 1);
                var world = new WorldStateProgress("slot-quest-route-objective", "player-quest-route-objective");
                Assert.IsTrue(world.TryActivate(flag, new WorldStateActivationSource(ChainId, "completion", "test", 1)));
                WorldStateProgressPersistence.Save(world);
                var context = new ObjectiveJournalContext(registry, player, new Rootborn.Game.Save.SaveSlotMetadata { SlotId = "slot-quest-route-objective", DisplayName = "Quest Route" });
                var aggregator = new ObjectiveJournalAggregator(new IObjectiveJournalSource[] { new DailyPlanObjectiveSource(), new DailyPlanRouteHintObjectiveSource() });

                var unselected = aggregator.Build(context);
                Assert.IsTrue(unselected["Hints"].Any(item => item.StableKey == "daily-plan-route-hint:" + route.Id));

                // The day plan belongs to the student now; a restore on the component is ignored once the student owns it. Select the way the planner does.
                Assert.IsTrue(daily.TrySelect(route, student.EnsureProgress()), "Planner selection of the unlocked route must succeed.");
                var selected = aggregator.Build(context);

                Assert.IsTrue(selected["Goals"].Any(item => item.StableKey == "daily-plan:" + route.Id));
                Assert.IsFalse(selected["Hints"].Any(item => item.StableKey == "daily-plan-route-hint:" + route.Id));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, true);
            }
        }

        private static GameDataRegistry LoadRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            return registry;
        }

        private static QuestChainDefinition FindChain(GameDataRegistry registry)
        {
            return registry.QuestChains.FirstOrDefault(chain => chain != null && chain.Id == ChainId);
        }

        private static WorldStateFlagDefinition FindWorldState(GameDataRegistry registry)
        {
            return registry.WorldStateFlags.FirstOrDefault(flag => flag != null && flag.Id == WorldStateId);
        }

        private static DailyPlanRouteDefinition FindRoute(GameDataRegistry registry)
        {
            return registry.DailyPlanRouteSets
                .Where(set => set != null)
                .SelectMany(set => set.Routes)
                .FirstOrDefault(route => route != null && route.Id == RouteId);
        }

        private static bool RewardReferencesFlag(WorldStateChangeQuestReward reward, WorldStateFlagDefinition flag)
        {
            return reward != null && flag != null && reward.Flags != null && reward.Flags.Contains(flag);
        }

        private static bool RequirementReferencesFlag(DailyPlanWorldStateRequirement requirement, WorldStateFlagDefinition flag)
        {
            if (requirement == null || flag == null) return false;
            var field = typeof(DailyPlanWorldStateRequirement).GetField("_flag", BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null && ReferenceEquals(field.GetValue(requirement), flag);
        }
    }
}
