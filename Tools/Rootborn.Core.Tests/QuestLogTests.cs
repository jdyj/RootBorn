using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/Quests/QuestProgressTests.cs, Assets/Tests/EditMode/Quests/QuestSaveTests.cs,
    // Assets/Tests/EditMode/Quests/QuestRewardTests.cs (퀘스트 로그 보상), Assets/Tests/EditMode/Quests/QuestObjectiveTests.cs (QUEST_004, QUEST_007 진행)
    public sealed class QuestLogTests
    {
        private static QuestDefinition Quest(string id, int requiredCount = 1, QuestReward[] rewards = null, QuestCompletionEffect[] effects = null)
            => new QuestDefinition(id, id + ".name", id + ".desc", new QuestObjective[] { new AnyEventObjective(requiredCount) }, rewards, effects);

        private static ItemDefinition Item(string id, int maxStack) => new ItemDefinition(id, maxStack);

        private static ItemInventory FullInventory(int freeSlots = 0)
        {
            var inventory = new ItemInventory();
            for (int i = 0; i < ItemInventory.MaxSlots - freeSlots; i++) inventory.Add(Item("filler-" + i, 1), 1);
            return inventory;
        }

        private static QuestLog CompletedLog(QuestDefinition quest)
        {
            var log = new QuestLog(new[] { quest });
            Assert.That(log.Accept(quest), Is.True);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));
            Assert.That(log.GetState(quest), Is.EqualTo(QuestStage.Completed));
            return log;
        }

        [Test]
        public void QUEST_002_AcceptedQuest_BecomesActive()
        {
            var progress = new QuestProgress(1);
            Assert.That(progress.State, Is.EqualTo(QuestStage.NotStarted));
            Assert.That(progress.TryAccept(), Is.True);
            Assert.That(progress.State, Is.EqualTo(QuestStage.Active));
        }

        [Test]
        public void QUEST_002_QuestLogAccept_ActivatesDefinition()
        {
            var quest = Quest("quest.a");
            var log = new QuestLog(new[] { quest });
            Assert.That(log.Accept(quest), Is.True);
            Assert.That(log.GetState(quest), Is.EqualTo(QuestStage.Active));
            Assert.That(log.Accept(quest), Is.False, "accepting twice changes nothing");
        }

        [Test]
        public void QUEST_004_QuestLogRecordEvent_CompletesWhenObjectiveSatisfied()
        {
            var quest = Quest("quest.a");
            var log = new QuestLog(new[] { quest });
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "event-1"));
            Assert.That(log.GetState(quest), Is.EqualTo(QuestStage.Completed));
        }

        [Test]
        public void QUEST_004_AllObjectivesRequiredBeforeCompletion()
        {
            var progress = new QuestProgress(2);
            progress.TryAccept();
            Assert.That(progress.TryAddObjectiveCount(0, 1, 1, "event-a"), Is.True);
            Assert.That(progress.State, Is.EqualTo(QuestStage.Active));
            Assert.That(progress.TryAddObjectiveCount(1, 1, 1, "event-b"), Is.True);
            Assert.That(progress.State, Is.EqualTo(QuestStage.Completed));
        }

        [Test]
        public void QUEST_007_DuplicateEventKey_DoesNotCountTwice()
        {
            var progress = new QuestProgress(1);
            progress.TryAccept();
            Assert.That(progress.TryAddObjectiveCount(0, 1, 2, "gather-1"), Is.True);
            Assert.That(progress.TryAddObjectiveCount(0, 1, 2, "gather-1"), Is.False);
            Assert.That(progress.GetObjectiveCount(0), Is.EqualTo(1));
            Assert.That(progress.State, Is.EqualTo(QuestStage.Active));
        }

        [Test]
        public void QUEST_SAVE_STATE_001_UndefinedSavedStateNameFallsBackToSavedNumber()
        {
            var progress = new QuestProgress(1);
            progress.LoadFromSaveData(new QuestProgressSaveData { State = QuestStage.Active, StateName = "7", ObjectiveCounts = new[] { 0 } });
            Assert.That(progress.State, Is.EqualTo(QuestStage.Active));
            progress.LoadFromSaveData(null);
            Assert.That(progress.State, Is.EqualTo(QuestStage.Active), "a null save changes nothing");
        }

        [Test]
        public void QUEST_012_ActiveProgress_RoundTripsThroughSaveData()
        {
            var quest = Quest("quest.active", 3);
            var log = new QuestLog(new[] { quest });
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));

            var loaded = new QuestLog(new[] { quest });
            loaded.LoadFromSaveData(log.ToSaveData());
            Assert.That(loaded.GetState(quest), Is.EqualTo(QuestStage.Active));
            Assert.That(loaded.GetObjectiveCount(quest, 0), Is.EqualTo(1));

            loaded.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));
            Assert.That(loaded.GetObjectiveCount(quest, 0), Is.EqualTo(1));
            loaded.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g2"));
            loaded.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g3"));
            Assert.That(loaded.GetState(quest), Is.EqualTo(QuestStage.Completed));
            Assert.That(loaded.GetObjectiveCount(quest, 0), Is.EqualTo(3));
        }

        [Test]
        public void SaveData_KeepsTheGameFieldNamesAndSkipsQuestsWithoutId()
        {
            var quest = Quest("quest.saved");
            var noId = Quest("");
            var log = new QuestLog(new[] { quest, noId });
            log.Accept(quest);
            var saved = log.ToSaveData();
            Assert.That(saved.Quests.Length, Is.EqualTo(1));
            var record = saved.Quests[0];
            Assert.That(record.QuestId, Is.EqualTo("quest.saved"));
            Assert.That(record.State, Is.EqualTo(QuestStage.Active));
            Assert.That(record.StateName, Is.EqualTo("Active"));
            CollectionAssert.AreEqual(new[] { 0 }, record.ObjectiveCounts);
            Assert.That(record.ProcessedEventKeys, Is.Empty);

            var loaded = new QuestLog(new[] { quest });
            loaded.LoadFromSaveData(new QuestLogSaveData { Quests = new[] { null, new QuestProgressSaveData { QuestId = "quest.unknown", State = QuestStage.Completed }, record } });
            Assert.That(loaded.GetState(quest), Is.EqualTo(QuestStage.Active), "unknown and empty records are skipped");
            loaded.LoadFromSaveData(null);
            Assert.That(loaded.GetState(quest), Is.EqualTo(QuestStage.Active));
        }

        [Test]
        public void QUEST_013_RewardClaimed_AfterLoadCannotPayAgain()
        {
            var item = Item("item.coin", 99);
            var quest = Quest("quest.claimed", 1, new QuestReward[] { new ItemQuestReward(item, 1) });
            var log = CompletedLog(quest);
            var inventory = new ItemInventory();
            Assert.That(log.ClaimReward(quest, new RewardRuntimeContext(log, inventory, null, null)), Is.True);

            var loaded = new QuestLog(new[] { quest });
            loaded.LoadFromSaveData(log.ToSaveData());
            Assert.That(loaded.GetState(quest), Is.EqualTo(QuestStage.RewardClaimed));
            Assert.That(loaded.ClaimReward(quest, new RewardRuntimeContext(loaded, inventory, null, null)), Is.False);
            Assert.That(inventory.CountOf(item), Is.EqualTo(1));
        }

        [Test]
        public void CompletedButUnclaimed_SaveLoadThenClaimPaysExactlyOnce()
        {
            var item = Item("item.coin", 99);
            var quest = Quest("quest.pending", 1, new QuestReward[] { new ItemQuestReward(item, 2) });
            var log = CompletedLog(quest);
            var loaded = new QuestLog(new[] { quest });
            loaded.LoadFromSaveData(log.ToSaveData());
            var inventory = new ItemInventory();
            var context = new RewardRuntimeContext(loaded, inventory, null, null);
            Assert.That(loaded.ClaimReward(quest, context), Is.True);
            Assert.That(loaded.ClaimReward(quest, context), Is.False);
            Assert.That(inventory.CountOf(item), Is.EqualTo(2));
        }

        [Test]
        public void QUEST_006_QuestLogClaimReward_RepeatedCallDoesNotDuplicate()
        {
            var item = Item("item.coin", 99);
            var quest = Quest("quest.a", 1, new QuestReward[] { new ItemQuestReward(item, 2) });
            var log = CompletedLog(quest);
            var inventory = new ItemInventory();
            var context = new RewardRuntimeContext(log, inventory, null, null);
            Assert.That(log.ClaimReward(quest, context), Is.True);
            Assert.That(log.ClaimReward(quest, context), Is.False);
            Assert.That(inventory.CountOf(item), Is.EqualTo(2));
            Assert.That(log.GetState(quest), Is.EqualTo(QuestStage.RewardClaimed));
        }

        [Test]
        public void QUEST_005_QuestLogClaimReward_FullInventoryMutatesNothing()
        {
            var rewardItem = Item("item.reward", 1);
            var quest = Quest("quest.a", 1, new QuestReward[] { new ItemQuestReward(rewardItem, 1) });
            var log = CompletedLog(quest);
            var inventory = FullInventory();
            Assert.That(log.ClaimReward(quest, new RewardRuntimeContext(log, inventory, null, null)), Is.False);
            Assert.That(inventory.CountOf(rewardItem), Is.Zero);
            Assert.That(log.GetState(quest), Is.EqualTo(QuestStage.Completed));
        }

        [Test]
        public void QUEST_005_QuestLogClaimReward_TwoRewardsOneFreeSlotMutatesNothing()
        {
            var first = Item("item.first", 1);
            var second = Item("item.second", 1);
            var quest = Quest("quest.a", 1, new QuestReward[] { new ItemQuestReward(first, 1), new ItemQuestReward(second, 1) });
            var log = CompletedLog(quest);
            var inventory = FullInventory(1);
            var context = new RewardRuntimeContext(log, inventory, null, null);
            Assert.That(log.CanClaimReward(quest, context), Is.False);
            Assert.That(log.ClaimReward(quest, context), Is.False);
            Assert.That(inventory.CountOf(first) + inventory.CountOf(second), Is.Zero);
            Assert.That(log.GetState(quest), Is.EqualTo(QuestStage.Completed));
        }

        [Test]
        public void QUEST_005_QuestLogClaimReward_TwoRewardsWithRoomGrantsBothOnce()
        {
            var first = Item("item.first", 1);
            var second = Item("item.second", 1);
            var quest = Quest("quest.a", 1, new QuestReward[] { new ItemQuestReward(first, 1), new ItemQuestReward(second, 1) });
            var log = CompletedLog(quest);
            var inventory = new ItemInventory();
            var context = new RewardRuntimeContext(log, inventory, null, null);
            var stateAtInventoryChange = default(QuestStage);
            inventory.OnChanged += () => stateAtInventoryChange = log.GetState(quest);

            Assert.That(log.ClaimReward(quest, context), Is.True);
            Assert.That(log.ClaimReward(quest, context), Is.False);
            Assert.That(inventory.CountOf(first), Is.EqualTo(1));
            Assert.That(inventory.CountOf(second), Is.EqualTo(1));
            Assert.That(log.GetState(quest), Is.EqualTo(QuestStage.RewardClaimed));
            Assert.That(stateAtInventoryChange, Is.EqualTo(QuestStage.RewardClaimed), "Inventory observers must see the claim already committed.");
        }

        [Test]
        public void QUEST_STUDENT_001_QuestCompletionEffectRaisesStudentLifeTraitAtomically()
        {
            var quest = Quest("quest.mentor", 1, null, new QuestCompletionEffect[] { new TraitDeltaCompletionEffect("trait.service-sense", 2) });
            var log = new QuestLog(new[] { quest });
            var progress = new StudentLifeProgress("slot-a", "player-1", 10, 10);
            var context = new RewardRuntimeContext(log, null, null, null, progress);
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Talk, "career-mentor"));

            Assert.That(log.ClaimReward(quest, context), Is.True);
            Assert.That(progress.GetTraitValue("trait.service-sense"), Is.EqualTo(2));
            Assert.That(log.ClaimReward(quest, context), Is.False);
            Assert.That(progress.GetTraitValue("trait.service-sense"), Is.EqualTo(2));
        }

        [Test]
        public void ItemsRewardsAndEffectsCommitTogether_OrNotAtAll()
        {
            var item = Item("item.coin", 1);
            var quest = Quest("quest.mixed", 1, new QuestReward[] { new ItemQuestReward(item, 1), new FurnitureUnlockQuestReward("furniture.desk") },
                new QuestCompletionEffect[] { new SetStoryFlagCompletionEffect("flag.done") });
            var log = CompletedLog(quest);
            var flags = new FakeStoryFlags();
            var furniture = new FakeFurnitureUnlocks();

            var full = FullInventory();
            Assert.That(log.ClaimReward(quest, new RewardRuntimeContext(log, full, null, flags, null, null, furniture)), Is.False);
            Assert.That(flags.IsSet("flag.done"), Is.False, "a refused item grant leaves the effect unapplied");
            Assert.That(furniture.Count, Is.Zero);

            var noFlags = new ItemInventory();
            Assert.That(log.ClaimReward(quest, new RewardRuntimeContext(log, noFlags, null, null, null, null, furniture)), Is.False);
            Assert.That(noFlags.CountOf(item), Is.Zero, "an effect that cannot apply leaves the items ungiven");

            var inventory = new ItemInventory();
            Assert.That(log.ClaimReward(quest, new RewardRuntimeContext(log, inventory, null, flags, null, null, furniture)), Is.True);
            Assert.That(inventory.CountOf(item), Is.EqualTo(1));
            Assert.That(furniture.IsUnlocked("furniture.desk"), Is.True);
            Assert.That(flags.IsSet("flag.done"), Is.True);
        }

        [Test]
        public void ClaimBeforeCompletion_OrForAnUnknownQuest_ChangesNothing()
        {
            var item = Item("item.coin", 99);
            var quest = Quest("quest.a", 2, new QuestReward[] { new ItemQuestReward(item, 1) });
            var log = new QuestLog(new[] { quest });
            var inventory = new ItemInventory();
            var context = new RewardRuntimeContext(log, inventory, null, null);
            Assert.That(log.ClaimReward(quest, context), Is.False, "not started");
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));
            Assert.That(log.CanClaimReward(quest, context), Is.False, "active");
            Assert.That(log.ClaimReward(quest, context), Is.False);
            Assert.That(log.ClaimReward(Quest("quest.other"), context), Is.False);
            Assert.That(log.ClaimReward(null, context), Is.False);
            Assert.That(inventory.CountOf(item), Is.Zero);
        }

        [Test]
        public void QUEST_005_QuestLogClaimReward_EffectCallingBackDoesNotApplyTwice()
        {
            var effect = new ReentrantCountingEffect(reenterIn: "apply");
            var quest = Quest("quest.a", 1, new QuestReward[0], new QuestCompletionEffect[] { effect });
            var log = CompletedLog(quest);
            effect.Log = log; effect.Quest = quest;
            Assert.That(log.ClaimReward(quest, new RewardRuntimeContext(log, new ItemInventory(), null, null)), Is.True);
            Assert.That(effect.AppliedCount, Is.EqualTo(1));
            Assert.That(log.GetState(quest), Is.EqualTo(QuestStage.RewardClaimed));
        }

        [Test]
        public void QUEST_REENTRY_002_QuestLogClaimReward_PreflightCallingBackDoesNotApplyTwice()
        {
            var effect = new ReentrantCountingEffect(reenterIn: "preflight");
            var quest = Quest("quest.a", 1, new QuestReward[0], new QuestCompletionEffect[] { effect });
            var log = CompletedLog(quest);
            effect.Log = log; effect.Quest = quest;
            Assert.That(log.ClaimReward(quest, new RewardRuntimeContext(log, new ItemInventory(), null, null)), Is.True);
            Assert.That(effect.AppliedCount, Is.EqualTo(1));
            Assert.That(log.GetState(quest), Is.EqualTo(QuestStage.RewardClaimed));
        }

        [Test]
        public void StateChanges_AreReportedInOrder()
        {
            var quest = Quest("quest.a", 2);
            var log = new QuestLog(new[] { quest });
            var changes = new List<QuestStateChange>();
            log.OnStateChanged += changes.Add;
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g2"));
            log.ClaimReward(quest, new RewardRuntimeContext(log, new ItemInventory(), null, null));

            Assert.That(changes.Count, Is.EqualTo(5));
            Assert.That(changes[0].Kind, Is.EqualTo(QuestStateChangeKind.Accepted));
            Assert.That(changes[1].Kind, Is.EqualTo(QuestStateChangeKind.ObjectiveProgressed));
            Assert.That(changes[1].ObjectiveCount, Is.EqualTo(1));
            Assert.That(changes[1].EventKey, Is.EqualTo("g1"));
            Assert.That(changes[2].Kind, Is.EqualTo(QuestStateChangeKind.ObjectiveProgressed));
            Assert.That(changes[3].Kind, Is.EqualTo(QuestStateChangeKind.Completed));
            Assert.That(changes[3].State, Is.EqualTo(QuestStage.Completed));
            Assert.That(changes[4].Kind, Is.EqualTo(QuestStateChangeKind.RewardClaimed));
            Assert.That(changes[4].Quest, Is.SameAs(quest));
        }

        [Test]
        public void AddQuest_ReplacesAnEarlierDefinitionWithTheSameId_AndLooksUpByIdFallback()
        {
            var original = Quest("quest.same");
            var copy = Quest("quest.same");
            var log = new QuestLog(new[] { original });
            log.Accept(original);
            Assert.That(log.GetState(copy), Is.EqualTo(QuestStage.Active), "a copy with the same id finds the same progress");
            Assert.That(log.GetState("quest.same"), Is.EqualTo(QuestStage.Active));
            Assert.That(log.AddQuest(copy), Is.True);
            Assert.That(log.GetState(original), Is.EqualTo(QuestStage.NotStarted), "adding by id starts over");
            Assert.That(log.AddQuest(null), Is.False);
            Assert.That(log.ReplaceQuestById(Quest("")), Is.False);
            Assert.That(log.ReplaceQuestById(Quest("quest.same")), Is.True);
            Assert.That(log.GetState(""), Is.EqualTo(QuestStage.NotStarted));
            Assert.That(log.GetObjectiveCount(Quest("quest.missing"), 0), Is.Zero);
        }

        // 옮겨 옴: Assets/Tests/EditMode/StudentLife/CareerInterestQuestLinkPersistenceTests.cs (장래희망에 묶인 퀘스트 목록은 그냥 퀘스트 목록으로 둔다)
        [Test]
        public void CAREER_INTEREST_EDIT_011_013_LinkedQuestProgressCompletionAndRewardClaimRestoreWithoutDuplication()
        {
            var reward = new CountingQuestReward();
            var quest = new QuestDefinition("quest.interest.learning", "quest.interest.learning", "desc.quest.interest.learning",
                new QuestObjective[] { new LocationActivityQuestObjective("objective.interest.location", 2, null) }, new QuestReward[] { reward });
            var linked = new[] { quest };
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var firstLog = new QuestLog(linked);

            Assert.That(firstLog.Accept(quest), Is.True, "linked quest was not accepted.");
            firstLog.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, "location-activity:library:0"));
            var restoredPartial = new QuestLog(linked);
            restoredPartial.LoadFromSaveData(firstLog.ToSaveData());

            Assert.That(restoredPartial.GetState(quest), Is.EqualTo(QuestStage.Active));
            Assert.That(restoredPartial.GetObjectiveCount(quest, 0), Is.EqualTo(1), "partial linked quest progress did not restore.");
            restoredPartial.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, "location-activity:library:0"));
            Assert.That(restoredPartial.GetObjectiveCount(quest, 0), Is.EqualTo(1), "duplicate saved event increased progress after load.");
            restoredPartial.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, "location-activity:library:1"));
            Assert.That(restoredPartial.GetState(quest), Is.EqualTo(QuestStage.Completed));
            Assert.That(restoredPartial.GetObjectiveCount(quest, 0), Is.EqualTo(2));

            Assert.That(restoredPartial.ClaimReward(quest, new RewardRuntimeContext(restoredPartial, null, null, null, student)), Is.True);
            Assert.That(reward.ApplyCount, Is.EqualTo(1));
            var restoredClaimed = new QuestLog(linked);
            restoredClaimed.LoadFromSaveData(restoredPartial.ToSaveData());
            Assert.That(restoredClaimed.GetState(quest), Is.EqualTo(QuestStage.RewardClaimed));
            Assert.That(restoredClaimed.ClaimReward(quest, new RewardRuntimeContext(restoredClaimed, null, null, null, student)), Is.False, "reward was claimable again after load.");
            Assert.That(reward.ApplyCount, Is.EqualTo(1));
        }

        private sealed class CountingQuestReward : QuestReward
        {
            public int ApplyCount { get; private set; }
            public override bool CanApply(in RewardRuntimeContext context) => true;
            public override void Apply(in RewardRuntimeContext context) => ApplyCount++;
        }

        private sealed class ReentrantCountingEffect : QuestCompletionEffect
        {
            private readonly string _reenterIn;
            private bool _reentered;
            public ReentrantCountingEffect(string reenterIn) { _reenterIn = reenterIn; }
            public QuestLog Log;
            public QuestDefinition Quest;
            public int AppliedCount;

            public override bool CanApply(in RewardRuntimeContext context)
            {
                if (_reenterIn == "preflight" && !_reentered) { _reentered = true; Log.ClaimReward(Quest, context); }
                return true;
            }

            public override void Apply(in RewardRuntimeContext context)
            {
                AppliedCount++;
                if (_reenterIn == "apply") Log.ClaimReward(Quest, context);
            }
        }
    }
}
