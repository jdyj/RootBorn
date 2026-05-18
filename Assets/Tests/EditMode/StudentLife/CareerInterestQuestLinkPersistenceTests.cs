using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class CareerInterestQuestLinkPersistenceTests
    {
        [Test]
        public void CAREER_INTEREST_EDIT_011_013_LinkedQuestProgressCompletionAndRewardClaimRestoreWithoutDuplication()
        {
            var objective = ScriptableObject.CreateInstance<LocationActivityQuestObjective>();
            objective.ConfigureForRuntime("objective.interest.location", 2, null);
            var reward = ScriptableObject.CreateInstance<CountingQuestReward>();
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureForRuntime("quest.interest.learning", "quest.interest.learning", "desc.quest.interest.learning", new QuestObjectiveBase[] { objective }, new QuestRewardBase[] { reward }, null);
            var interest = ScriptableObject.CreateInstance<CareerInterestDefinition>();
            interest.ConfigureForTests("interest.learning", "interest.learning", "desc.interest.learning", null, null, null, null, null, null, null, new[] { quest });
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var firstLog = new QuestLog(interest.LinkedQuests);

            Assert.IsTrue(firstLog.Accept(quest), "CAREER-INTEREST-EDIT-011 failed: linked quest was not accepted.");
            firstLog.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, "location-activity:library:0"));
            var partialSave = firstLog.ToSaveData();
            var restoredPartial = new QuestLog(interest.LinkedQuests);
            restoredPartial.LoadFromSaveData(partialSave);

            Assert.AreEqual(QuestState.Active, restoredPartial.GetState(quest), "CAREER-INTEREST-EDIT-011 failed: linked quest accepted state did not restore.");
            Assert.AreEqual(1, restoredPartial.GetObjectiveCount(quest, 0), "CAREER-INTEREST-EDIT-012 failed: partial linked quest progress did not restore.");
            restoredPartial.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, "location-activity:library:0"));
            Assert.AreEqual(1, restoredPartial.GetObjectiveCount(quest, 0), "CAREER-INTEREST-EDIT-012 failed: duplicate saved event increased progress after load.");
            restoredPartial.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, "location-activity:library:1"));
            Assert.AreEqual(QuestState.Completed, restoredPartial.GetState(quest), "CAREER-INTEREST-EDIT-011 failed: linked quest did not become completion-ready after restored progress continued.");
            Assert.AreEqual(2, restoredPartial.GetObjectiveCount(quest, 0), "CAREER-INTEREST-EDIT-011 failed: linked quest completion count was incorrect.");

            var context = new RewardRuntimeContext(restoredPartial, null, null, null, student);
            Assert.IsTrue(restoredPartial.ClaimReward(quest, in context), "CAREER-INTEREST-EDIT-013 failed: completed linked quest reward could not be claimed.");
            Assert.AreEqual(1, reward.ApplyCount, "CAREER-INTEREST-EDIT-013 failed: linked quest reward was not applied exactly once.");
            var claimedSave = restoredPartial.ToSaveData();
            var restoredClaimed = new QuestLog(interest.LinkedQuests);
            restoredClaimed.LoadFromSaveData(claimedSave);
            var restoredContext = new RewardRuntimeContext(restoredClaimed, null, null, null, student);

            Assert.AreEqual(QuestState.RewardClaimed, restoredClaimed.GetState(quest), "CAREER-INTEREST-EDIT-013 failed: reward claimed state did not restore.");
            Assert.IsFalse(restoredClaimed.ClaimReward(quest, in restoredContext), "CAREER-INTEREST-EDIT-013 failed: linked quest reward was claimable again after load.");
            Assert.AreEqual(1, reward.ApplyCount, "CAREER-INTEREST-EDIT-013 failed: linked quest reward was applied more than once.");
        }

        private sealed class CountingQuestReward : QuestRewardBase
        {
            public int ApplyCount { get; private set; }
            public override bool CanApply(in RewardRuntimeContext context) => true;
            public override void Apply(in RewardRuntimeContext context) => ApplyCount++;
        }
    }
}
