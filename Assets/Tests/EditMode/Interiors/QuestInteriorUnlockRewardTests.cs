using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Placement;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Save;
using Rootborn.UI.Interiors;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class QuestInteriorUnlockRewardTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string ChainId = "questchain.test.career-interest.library";
        private const string UnlockToken = "unlock.furniture.quest.library-chair";
        private const string FurnitureId = "chair.quest-reward";
        private const string QuestInteriorRewardE2EPath = "Assets/Tests/PlayMode/EndToEnd/QuestInteriorRewardE2EScenarioTests.cs";

        [Test]
        public void FurnitureUnlockReward_CanApplyRequiresProgressAndUnlocksExactlyOnce()
        {
            var reward = ScriptableObject.CreateInstance<FurnitureUnlockQuestReward>();
            SetField(reward, "_unlockToken", UnlockToken);

            var missingContext = new RewardRuntimeContext(null, null, null, null, null, null, null);
            Assert.IsFalse(reward.CanApply(in missingContext), "Furniture unlock reward must not apply without FurnitureUnlockProgress.");

            var progress = new FurnitureUnlockProgress("slot-a", "player-a");
            var context = new RewardRuntimeContext(null, null, null, null, null, null, progress);
            Assert.IsTrue(reward.CanApply(in context));
            reward.Apply(in context);
            Assert.IsTrue(progress.IsUnlocked(UnlockToken));
            Assert.IsFalse(reward.CanApply(in context), "Furniture unlock reward must be idempotent after the token is already unlocked.");
            reward.Apply(in context);
            Assert.AreEqual(1, progress.UnlockedCount, "Repeated furniture unlock reward application must not duplicate tokens.");
        }

        [Test]
        public void FurnitureUnlockProgress_PersistsPerSaveSlotAndPlayer()
        {
            string root = Path.Combine(Application.temporaryCachePath, "rootborn-furniture-unlock-tests", System.Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(root);
            try
            {
                var progress = new FurnitureUnlockProgress("slot-furniture", "player-a");
                progress.TryUnlock(UnlockToken);
                FurnitureUnlockProgressPersistence.Save(progress);

                var restored = FurnitureUnlockProgressPersistence.LoadOrCreate("slot-furniture", "player-a");
                Assert.IsTrue(restored.IsUnlocked(UnlockToken));
                Assert.IsFalse(FurnitureUnlockProgressPersistence.LoadOrCreate("slot-furniture", "player-b").IsUnlocked(UnlockToken));
                Assert.IsFalse(FurnitureUnlockProgressPersistence.LoadOrCreate("other-slot", "player-a").IsUnlocked(UnlockToken));
            }
            finally
            {
                SaveService.SetRootDirectoryForTests(null);
            }
        }

        [Test]
        public void FurnitureUnlockProgress_FromSaveDataDeduplicatesUnlockTokens()
        {
            var saveData = new FurnitureUnlockProgressSaveData
            {
                SaveSlot = "slot-furniture",
                PlayerId = "player-a",
                UnlockTokens = new[] { UnlockToken, UnlockToken, string.Empty, null }
            };

            var restored = FurnitureUnlockProgress.FromSaveData(saveData, "fallback-slot", "fallback-player");

            Assert.IsTrue(restored.IsUnlocked(UnlockToken));
            Assert.AreEqual(1, restored.UnlockedCount, "Loading saved unlock data must collapse duplicate or empty unlock tokens before the next save.");
            CollectionAssert.AreEqual(new[] { UnlockToken }, restored.ToSaveData().UnlockTokens);
        }

        [Test]
        public void QuestInteriorRewardE2E_ContainsExactOnceSavedStateAssertions()
        {
            string source = File.ReadAllText(QuestInteriorRewardE2EPath);

            StringAssert.Contains(
                "AssertFileContainsExactlyOnce",
                source,
                "Quest/interior reward E2E must assert the unlock save file contains the reward token exactly once, not merely that it contains the token.");
            StringAssert.Contains(
                "AssertLayoutContainsExactlyOnce",
                source,
                "Quest/interior reward E2E must assert the serialized layout contains the reward furniture fields exactly once.");
            StringAssert.Contains(
                "CountOccurrences",
                source,
                "Quest/interior reward E2E exact-once assertions must use an explicit occurrence counter so duplicate serialized records fail visibly.");
        }

        [Test]
        public void RegisteredQuestChainCompletionReward_UnlocksFurnitureToken()
        {
            var registry = LoadRegistry();
            var chain = registry.QuestChains.SingleOrDefault(definition => definition != null && definition.Id == ChainId);
            Assert.IsNotNull(chain, "Expected test quest chain to remain registered.");

            var reward = chain.CompletionRewards.OfType<FurnitureUnlockQuestReward>().SingleOrDefault();
            Assert.IsNotNull(reward, "Quest chain completion rewards must include one furniture unlock reward.");
            Assert.AreEqual(UnlockToken, reward.UnlockToken);
        }

        [Test]
        public void LockedFurniture_IsHiddenBeforeRewardAndVisibleAfterUnlock()
        {
            var registry = LoadRegistry();
            var furniture = registry.FurnitureDefinitions.SingleOrDefault(definition => definition != null && definition.StableId == FurnitureId);
            Assert.IsNotNull(furniture, "Expected the quest reward furniture asset to remain registered.");
            Assert.AreEqual(UnlockToken, furniture.UnlockToken, "The selected furniture asset must be gated by the quest unlock token.");

            string root = Path.Combine(Application.temporaryCachePath, "rootborn-furniture-catalog-tests", System.Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(root);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-furniture", DisplayName = "slot-furniture" });
            try
            {
                CollectionAssert.DoesNotContain(
                    InteriorFurnitureCatalog.LoadFurniture(registry).Select(item => item.Id).ToArray(),
                    FurnitureId,
                    "Locked furniture must be hidden before the quest reward is claimed.");

                var unlocks = FurnitureUnlockProgressPersistence.LoadOrCreate("slot-furniture", PlayerIdentity.DefaultPlayerId);
                unlocks.TryUnlock(UnlockToken);
                FurnitureUnlockProgressPersistence.Save(unlocks);

                CollectionAssert.Contains(
                    InteriorFurnitureCatalog.LoadFurniture(registry).Select(item => item.Id).ToArray(),
                    FurnitureId,
                    "Unlocked furniture must appear in the placement palette catalog.");
            }
            finally
            {
                ActiveSaveContext.Clear();
                SaveService.SetRootDirectoryForTests(null);
            }
        }

        private static GameDataRegistry LoadRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, "GameDataRegistry missing at " + RegistryPath);
            return registry;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(fieldName);
        }
    }
}
