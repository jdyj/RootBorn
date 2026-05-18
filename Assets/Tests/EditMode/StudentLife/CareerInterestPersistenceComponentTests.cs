using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class CareerInterestPersistenceComponentTests
    {
        [Test]
        public void CAREER_INTEREST_EDIT_007_ComponentPersistsBySaveSlotAndPlayerIdentity()
        {
            var go = new GameObject("career-interest-component-test");
            try
            {
                var component = go.AddComponent<CareerInterestProgressComponent>();
                component.ConfigureForTests("slot-a", "player-a");
                var progress = component.EnsureProgress();
                Assert.AreEqual("slot-a", progress.SaveSlot);
                Assert.AreEqual("player-a", progress.PlayerId);

                var saveData = new CareerInterestProgressSaveData
                {
                    SaveSlot = "slot-a",
                    PlayerId = "player-a",
                    CurrentInterestId = "interest.learning",
                    PreviousInterestId = "interest.technical",
                    LastChangedDay = 2,
                    ChangeCountForCurrentDay = 1,
                    SelectionHistoryIds = new[] { "interest.learning", "interest.technical" },
                    ClaimedRewardIds = new[] { "interest.learning:reward.learning.focus" },
                    LastRecommendationSourceVersion = "interest.learning:2",
                    TodayInterestActionSummary = new[] { "interest.learning:selected" },
                };

                component.RestoreFromSaveData(saveData);

                Assert.AreEqual("interest.learning", component.Progress.CurrentInterestId);
                Assert.AreEqual("interest.technical", component.Progress.PreviousInterestId);
                Assert.IsTrue(component.Progress.HasClaimedReward("interest.learning", "reward.learning.focus"));
                CollectionAssert.Contains(component.Progress.SelectionHistoryIds, "interest.technical");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
