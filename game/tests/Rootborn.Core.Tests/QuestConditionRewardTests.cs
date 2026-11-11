using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/Quests/QuestConditionTests.cs, Assets/Tests/EditMode/Quests/QuestRewardTests.cs (단독 보상),
    // Assets/Tests/EditMode/Interiors/QuestInteriorUnlockRewardTests.cs (가구 해금 보상), Assets/Tests/EditMode/WorldState/WorldStateCoreTests.cs (월드 상태 보상)
    public sealed class QuestConditionRewardTests
    {
        private static RewardRuntimeContext Rewards(ItemInventory inventory = null, IQuestStoryFlags flags = null, StudentLifeProgress student = null,
            IQuestWorldState world = null, IQuestFurnitureUnlocks furniture = null, IQuestKnowledge knowledge = null)
            => new RewardRuntimeContext(null, inventory, knowledge, flags, student, world, furniture);

        private static QuestRuntimeContext Context(IQuestStoryFlags flags = null, StudentLifeProgress student = null, IQuestKnowledge knowledge = null, QuestLog log = null)
            => new QuestRuntimeContext(log, null, knowledge, flags, student);

        [Test]
        public void QUEST_010_CompletionEffect_SetsStoryFlag()
        {
            var flags = new FakeStoryFlags();
            var effect = new SetStoryFlagCompletionEffect("flag.met-teacher");
            var context = Rewards(flags: flags);

            Assert.That(effect.CanApply(in context), Is.True);
            effect.Apply(in context);
            Assert.That(flags.IsSet("flag.met-teacher"), Is.True);
            Assert.That(new SetStoryFlagCompletionEffect(null).CanApply(in context), Is.False, "no flag");
            Assert.That(effect.CanApply(Rewards()), Is.False, "no flag set to write to");
        }

        [Test]
        public void QUEST_011_StoryFlagCondition_BlocksWhenMissing()
        {
            var flags = new FakeStoryFlags();
            var condition = new StoryFlagCondition("flag.library-open");
            Assert.That(condition.IsSatisfied(Context(flags)), Is.False);
            flags.Set("flag.library-open");
            Assert.That(condition.IsSatisfied(Context(flags)), Is.True);
            Assert.That(condition.IsSatisfied(Context()), Is.False, "no flag set");
        }

        [Test]
        public void QUEST_012_LocationActivityCompletedCondition_UsesStudentProgressActivityLog()
        {
            var condition = new LocationActivityCompletedCondition("activity.library.desk", "missing.library.access", "Visit the library desk to reopen this chain.");
            var progress = new StudentLifeProgress("slot", "player", 100, 100, 0, 8 * 60);

            Assert.That(condition.IsSatisfied(Context(student: progress)), Is.False, "condition should block until the activity is logged.");
            progress.RecordActivityCompleted("activity.library.desk");
            Assert.That(condition.IsSatisfied(Context(student: progress)), Is.True, "condition should pass after the activity is logged.");
            Assert.That(condition.BlockedReasonId, Is.EqualTo("missing.library.access"));
            Assert.That(condition.BlockedSummary, Does.Contain("library desk"));
        }

        [Test]
        public void LocationActivityCompletedCondition_DefaultsAndEmptyTexts()
        {
            var defaults = new LocationActivityCompletedCondition("activity.a");
            Assert.That(defaults.BlockedReasonId, Is.EqualTo("missing.location.activity"));
            Assert.That(defaults.BlockedSummary, Is.EqualTo("Complete the required location activity to continue."));
            var empty = new LocationActivityCompletedCondition("activity.a", "", "", "Condition_Library");
            Assert.That(empty.BlockedReasonId, Is.EqualTo("LocationActivityCompletedCondition"), "an empty reason falls back to the type name");
            Assert.That(empty.BlockedSummary, Is.EqualTo("Condition_Library"), "an empty summary falls back to the asset name");
            Assert.That(new LocationActivityCompletedCondition(null).IsSatisfied(Context(student: new StudentLifeProgress("s", "p", 1, 1))), Is.False);
            Assert.That(defaults.IsSatisfied(Context()), Is.False, "no student");
        }

        [Test]
        public void DayAtLeastCondition_ComparesTheCurrentDay()
        {
            var condition = new DayAtLeastCondition(2, "wait.summary");
            var progress = new StudentLifeProgress("slot", "player", 10, 10);
            Assert.That(condition.IsSatisfied(Context(student: progress)), Is.False);
            Assert.That(condition.BlockedReasonId, Is.EqualTo("wait.day.2"));
            Assert.That(condition.BlockedSummary, Is.EqualTo("wait.summary"));
            Assert.That(new DayAtLeastCondition(1).IsSatisfied(Context(student: progress)), Is.True);
            Assert.That(new DayAtLeastCondition(1).IsSatisfied(Context()), Is.False, "no student");
            Assert.That(new DayAtLeastCondition(1, null, "Condition_Day").BlockedSummary, Is.EqualTo("Condition_Day"));
        }

        [Test]
        public void KnowledgeUnlockedCondition_AndUnlockKnowledgeEffect_UseKnowledgeIds()
        {
            var knowledge = new FakeKnowledge();
            var condition = new KnowledgeUnlockedCondition("knowledge.fire");
            Assert.That(condition.IsSatisfied(Context(knowledge: knowledge)), Is.False);
            var effect = new UnlockKnowledgeCompletionEffect("knowledge.fire");
            var context = Rewards(knowledge: knowledge);
            Assert.That(effect.CanApply(in context), Is.True);
            effect.Apply(in context);
            Assert.That(condition.IsSatisfied(Context(knowledge: knowledge)), Is.True);
            Assert.That(condition.IsSatisfied(Context()), Is.False, "no knowledge progress");
            Assert.That(new UnlockKnowledgeCompletionEffect(null).CanApply(in context), Is.False);
            Assert.That(effect.CanApply(Rewards()), Is.False);
        }

        [Test]
        public void QuestStateCondition_ReadsTheQuestLogById()
        {
            var quest = new QuestDefinition("quest.first", null, null, new QuestObjective[] { new AnyEventObjective() });
            var log = new QuestLog(new[] { quest });
            var condition = new QuestStateCondition("quest.first", QuestStage.Active);
            Assert.That(condition.IsSatisfied(Context(log: log)), Is.False);
            log.Accept(quest);
            Assert.That(condition.IsSatisfied(Context(log: log)), Is.True);
            Assert.That(new QuestStateCondition("quest.first").IsSatisfied(Context(log: log)), Is.False, "default asks for the reward to be claimed");
            Assert.That(new QuestStateCondition(null, QuestStage.NotStarted).IsSatisfied(Context(log: log)), Is.False, "no quest");
            Assert.That(condition.IsSatisfied(Context()), Is.False, "no log");
        }

        [Test]
        public void Condition_DefaultReasonIsTypeNameAndSummaryIsName()
        {
            var condition = new StoryFlagCondition("flag.x", "Condition_StoryFlag_X");
            Assert.That(condition.BlockedReasonId, Is.EqualTo("StoryFlagCondition"));
            Assert.That(condition.BlockedSummary, Is.EqualTo("Condition_StoryFlag_X"));
            Assert.That(new StoryFlagCondition("flag.x").BlockedSummary, Is.EqualTo(string.Empty));
        }

        [Test]
        public void QUEST_005_ItemReward_CanApplyThenAddsExactlyOnce()
        {
            var item = new ItemDefinition("item.wood", 99);
            var reward = new ItemQuestReward(item, 3);
            var inventory = new ItemInventory();
            var context = Rewards(inventory);

            Assert.That(reward.CanApply(in context), Is.True);
            reward.Apply(in context);
            Assert.That(inventory.CountOf(item), Is.EqualTo(3));
            Assert.That(reward.TryGetInventoryGrant(out var grant), Is.True);
            Assert.That(grant.Item, Is.SameAs(item));
            Assert.That(grant.Count, Is.EqualTo(3));
            Assert.That(new ItemQuestReward(item, 0).Count, Is.EqualTo(1));
        }

        [Test]
        public void QUEST_006_FullInventory_PreflightFailsAndMutatesNothing()
        {
            var fullItem = new ItemDefinition("item.full", 1);
            var rewardItem = new ItemDefinition("item.reward", 1);
            var inventory = new ItemInventory();
            for (int i = 0; i < ItemInventory.MaxSlots; i++) inventory.Add(fullItem, 1);
            var reward = new ItemQuestReward(rewardItem, 1);
            var context = Rewards(inventory);

            Assert.That(reward.CanApply(in context), Is.False);
            reward.Apply(in context);
            Assert.That(inventory.CountOf(rewardItem), Is.Zero, "Apply re-checks and does nothing when it cannot apply");
            Assert.That(new ItemQuestReward(null, 1).CanApply(in context), Is.False);
            Assert.That(reward.CanApply(Rewards()), Is.False, "no inventory");
        }

        [Test]
        public void QUEST_STUDENT_001_TraitDeltaEffect_RaisesTraitAndUnlocksCareerHint()
        {
            var progress = new StudentLifeProgress("slot-a", "player-1", 10, 10);
            var context = Rewards(student: progress);
            var effect = new TraitDeltaCompletionEffect("trait.service-sense", 2, "career.nurse");
            Assert.That(effect.CanApply(in context), Is.True);
            effect.Apply(in context);
            Assert.That(progress.GetTraitValue("trait.service-sense"), Is.EqualTo(2));
            Assert.That(progress.IsCareerHintUnlocked("career.nurse"), Is.True);
            Assert.That(new TraitDeltaCompletionEffect("trait.service-sense", 0).CanApply(in context), Is.False, "a zero delta does nothing");
            Assert.That(new TraitDeltaCompletionEffect(null, 1).CanApply(in context), Is.False);
            Assert.That(effect.CanApply(Rewards()), Is.False, "no student");
            Assert.That(new TraitDeltaCompletionEffect("trait.a").Delta, Is.EqualTo(1), "default delta");
        }

        [Test]
        public void FurnitureUnlockReward_CanApplyRequiresProgressAndUnlocksExactlyOnce()
        {
            var reward = new FurnitureUnlockQuestReward("furniture.unlock.desk");
            Assert.That(reward.CanApply(Rewards()), Is.False, "must not apply without furniture unlock progress.");
            var progress = new FakeFurnitureUnlocks();
            var context = Rewards(furniture: progress);
            Assert.That(reward.CanApply(in context), Is.True);
            reward.Apply(in context);
            Assert.That(progress.IsUnlocked("furniture.unlock.desk"), Is.True);
            Assert.That(reward.CanApply(in context), Is.False, "idempotent once the token is unlocked.");
            reward.Apply(in context);
            Assert.That(progress.Count, Is.EqualTo(1));
            Assert.That(new FurnitureUnlockQuestReward(null).CanApply(in context), Is.False);
            Assert.That(new FurnitureUnlockQuestReward(null).UnlockToken, Is.EqualTo(string.Empty));
        }

        [Test]
        public void WorldStateChangeReward_ActivatesFlagsOnceWithSourceAndMarksEffectVersion()
        {
            var world = new FakeWorldState();
            var reward = new WorldStateChangeQuestReward(new[] { new QuestWorldStateFlag("world.library.archive-open", 2) }, "quest.library", "step.archive", "event.archive-open", 7);
            var context = Rewards(world: world);

            Assert.That(reward.CanApply(in context), Is.True);
            reward.Apply(in context);
            reward.Apply(in context);
            Assert.That(world.Sources.Count, Is.EqualTo(1));
            var source = world.Sources["world.library.archive-open"];
            Assert.That(source.QuestChainId, Is.EqualTo("quest.library"));
            Assert.That(source.QuestStepId, Is.EqualTo("step.archive"));
            Assert.That(source.EventId, Is.EqualTo("event.archive-open"));
            Assert.That(source.Day, Is.EqualTo(7));
            Assert.That(world.AppliedVersions["world.library.archive-open"], Is.EqualTo(2));
            Assert.That(reward.CanApply(in context), Is.False, "every flag already active");
        }

        [Test]
        public void WorldStateChangeReward_NeedsProgressAndAtLeastOneFlag()
        {
            var world = new FakeWorldState();
            Assert.That(new WorldStateChangeQuestReward(new[] { new QuestWorldStateFlag("world.a", 0) }, null, null, null, 0).CanApply(Rewards()), Is.False);
            Assert.That(new WorldStateChangeQuestReward(null, null, null, null, 0).CanApply(Rewards(world: world)), Is.False);
            Assert.That(new WorldStateChangeQuestReward(new[] { new QuestWorldStateFlag(null, 0) }, null, null, null, 0).CanApply(Rewards(world: world)), Is.False, "missing flags are skipped");

            var source = new QuestWorldStateSource(null, null, null, 3);
            Assert.That(source.QuestChainId, Is.EqualTo(string.Empty));
            Assert.That(source.EventId, Is.EqualTo(string.Empty));
        }
    }
}
