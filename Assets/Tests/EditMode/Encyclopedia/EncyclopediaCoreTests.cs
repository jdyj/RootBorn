using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Encyclopedia
{
    public sealed class EncyclopediaCoreTests
    {
        [Test]
        public void ENCYCLOPEDIA_EDIT_001_GameDataRegistryExposesCategoriesAndEntries()
        {
            var category = ScriptableObject.CreateInstance<EncyclopediaCategoryDefinition>();
            category.ConfigureForTests("ency.category.locations", "Locations", 0);
            var entry = ScriptableObject.CreateInstance<EncyclopediaEntryDefinition>();
            entry.ConfigureForTests("ency.location.library", category, "Library", "???", "Read books", "Find it", Array.Empty<string>(), Array.Empty<EncyclopediaUnlockConditionBase>());

            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            SetPrivateField(registry, "_encyclopediaCategories", new[] { category });
            SetPrivateField(registry, "_encyclopediaEntries", new[] { entry });

            Assert.That(registry.EncyclopediaCategories, Is.EquivalentTo(new[] { category }));
            Assert.That(registry.EncyclopediaEntries, Is.EquivalentTo(new[] { entry }));
        }

        [Test]
        public void ENCYCLOPEDIA_EDIT_002_UnlockConditionsEvaluateStateWithoutEntityBranching()
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests("location.library", "Library", Vector2.zero, Array.Empty<DiscoveryDefinition>());
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests("activity.study", "Study", LifeActivityCategory.SelfStudy, 10, 1, 1, 0, Array.Empty<LifeActivityRequirementBase>(), Array.Empty<LifeActivityEffectBase>());
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            career.ConfigureForTests("career.writer", "Writer", Array.Empty<CareerUnlockRequirementBase>());
            var knowledge = ScriptableObject.CreateInstance<KnowledgeNode>();
            SetPrivateField(knowledge, "_id", "knowledge.soil");
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetPrivateField(item, "_id", "item.book");
            SetPrivateField(item, "_maxStack", 8);
            SetPrivateField(item, "_category", ItemCategory.Misc);

            var progress = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            progress.RecordActivityCompleted(location.Id);
            progress.RecordActivityCompleted(activity.Id);
            progress.UnlockCareerHint(career);
            var knowledgeProgress = new KnowledgeProgress(new[] { knowledge });
            knowledgeProgress.Seed(knowledge);
            var inventory = new Inventory();
            inventory.Add(item, 1);
            var context = new EncyclopediaUnlockContext(progress, knowledgeProgress, inventory);

            Assert.IsTrue(MakeCondition<EncyclopediaLocationUnlockedCondition>("_location", location).IsSatisfied(context));
            Assert.IsTrue(MakeCondition<EncyclopediaActivityCompletedCondition>("_activity", activity).IsSatisfied(context));
            Assert.IsTrue(MakeCondition<EncyclopediaCareerHintUnlockedCondition>("_career", career).IsSatisfied(context));
            Assert.IsTrue(MakeCondition<EncyclopediaKnowledgeUnlockedCondition>("_knowledge", knowledge).IsSatisfied(context));
            Assert.IsTrue(MakeCondition<EncyclopediaItemPossessedCondition>("_item", item).IsSatisfied(context));
        }

        [Test]
        public void ENCYCLOPEDIA_EDIT_003_ProgressPersistsUnlockStagesAndNewFlagsBySaveSlotAndPlayer()
        {
            var progress = new EncyclopediaProgress("slot-a", "player-a");
            Assert.IsTrue(progress.TryUnlock("entry-a", 2, 123L));
            Assert.IsTrue(progress.IsNew("entry-a"));
            progress.MarkSeen("entry-a");

            SaveService.SetRootDirectoryForTests(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-ency-" + Guid.NewGuid().ToString("N")));
            try
            {
                EncyclopediaProgressPersistence.Save(progress);
                var loaded = EncyclopediaProgressPersistence.LoadOrCreate("slot-a", "player-a");

                Assert.IsTrue(loaded.IsUnlocked("entry-a"));
                Assert.AreEqual(2, loaded.GetRevealStage("entry-a"));
                Assert.IsFalse(loaded.IsNew("entry-a"));
                Assert.IsFalse(EncyclopediaProgressPersistence.LoadOrCreate("slot-a", "player-b").IsUnlocked("entry-a"));
            }
            finally
            {
                SaveService.SetRootDirectoryForTests(null);
            }
        }

        [Test]
        public void ENCYCLOPEDIA_EDIT_004_ReapplyingUnlockIsIdempotent()
        {
            var progress = new EncyclopediaProgress("slot-a", "player-a");

            Assert.IsTrue(progress.TryUnlock("entry-a", 1, 1L));
            Assert.IsFalse(progress.TryUnlock("entry-a", 1, 2L));
            Assert.AreEqual(1, progress.UnlockedCount);
            Assert.AreEqual(1, progress.NewCount);
        }

        [Test]
        public void ENCYCLOPEDIA_EDIT_005_IndexCachesLookupWithoutRegistryRescanOnRefresh()
        {
            var category = ScriptableObject.CreateInstance<EncyclopediaCategoryDefinition>();
            category.ConfigureForTests("cat", "Cat", 0);
            var entries = new[] { CreateEntry("entry-a", category), CreateEntry("entry-b", category) };
            var index = new EncyclopediaIndex(new[] { category }, entries);
            var progress = new EncyclopediaProgress("slot-a", "player-a");

            Assert.AreEqual(1, index.BuildVersion);
            Assert.AreEqual(2, index.GetEntries(category).Count);
            Assert.AreEqual(2, index.GetVisibleRows(category, progress, 0, 25).Count);
            Assert.AreEqual(1, index.BuildVersion, "UI row refreshes must use the cached index rather than rebuilding registry lookup tables.");
        }

        [Test]
        public void ENCYCLOPEDIA_EDIT_006_ListModelPagesMoreThanOneHundredEntries()
        {
            var category = ScriptableObject.CreateInstance<EncyclopediaCategoryDefinition>();
            category.ConfigureForTests("cat", "Cat", 0);
            var entries = new EncyclopediaEntryDefinition[125];
            for (int i = 0; i < entries.Length; i++) entries[i] = CreateEntry("entry-" + i.ToString("000"), category);

            var index = new EncyclopediaIndex(new[] { category }, entries);
            var page = index.GetVisibleRows(category, new EncyclopediaProgress("slot-a", "player-a"), 2, 50);

            Assert.AreEqual(25, page.Count);
            Assert.AreEqual("entry-100", page[0].Entry.Id);
            Assert.AreEqual("entry-124", page[24].Entry.Id);
        }

        private static EncyclopediaEntryDefinition CreateEntry(string id, EncyclopediaCategoryDefinition category)
        {
            var entry = ScriptableObject.CreateInstance<EncyclopediaEntryDefinition>();
            entry.ConfigureForTests(id, category, id, "???", "Description " + id, "Find a clue", Array.Empty<string>(), Array.Empty<EncyclopediaUnlockConditionBase>());
            return entry;
        }

        private static T MakeCondition<T>(string fieldName, UnityEngine.Object value) where T : EncyclopediaUnlockConditionBase
        {
            var condition = ScriptableObject.CreateInstance<T>();
            SetPrivateField(condition, fieldName, value);
            return condition;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, target.GetType().Name + " missing field " + fieldName);
            field.SetValue(target, value);
        }
    }
}
