using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/StudentRelationshipConditionTests.cs의 RELCOND_EDIT_004, StudentActivityRefusalTextTests.cs
    // (등록부·대화 조건·결과 화면 검사와 생활 활동 중복 검사 005는 이 영역이 아니라 옮기지 않음)
    public sealed class StudentDaySummaryAndRefusalTests
    {
        private static readonly RelationshipSummaryInfo Trust = new RelationshipSummaryInfo("relationship.first-guide.trust", "Guide Trust");
        private static readonly StatusSummaryInfo Fatigue = new StatusSummaryInfo("status.fatigue", "Fatigue", true);

        [Test]
        public void RELCOND_EDIT_004_ResultSummaryBuildsBeforeAfterDeltasForRelationshipAndCondition()
        {
            var before = new StudentLifeProgress("slot-rel", "player-rel", 10, 10);
            before.AddRelationship(Trust.Id, 1);
            before.AddStatus(Fatigue.Id, 4);
            var after = StudentLifeProgress.FromSaveData(before.ToSaveData());
            after.AddRelationship(Trust.Id, 2);
            after.AddStatus(Fatigue.Id, 3);

            var summary = StudentDayRelationshipConditionSummaryBuilder.Build(before, after, new[] { Trust }, new[] { Fatigue });

            Assert.AreEqual(1, summary.RelationshipEntries.Length);
            Assert.AreEqual("Guide Trust", summary.RelationshipEntries[0].DisplayName);
            Assert.AreEqual(1, summary.RelationshipEntries[0].BeforeValue);
            Assert.AreEqual(3, summary.RelationshipEntries[0].AfterValue);
            Assert.AreEqual(2, summary.RelationshipEntries[0].Delta);
            Assert.AreEqual(1, summary.StatusEntries.Length);
            Assert.AreEqual("Fatigue", summary.StatusEntries[0].DisplayName);
            Assert.AreEqual(4, summary.StatusEntries[0].BeforeValue);
            Assert.AreEqual(7, summary.StatusEntries[0].AfterValue);
            Assert.AreEqual(3, summary.StatusEntries[0].Delta);
            Assert.IsNotEmpty(summary.NextDayImpacts);
        }

        [Test]
        public void ExplainsSpentTimeOnceDoneWrongDayAndEnergySeparately()
        {
            StudentLifeProgress Student(int energy, int focus)
            {
                var progress = new StudentLifeProgress("slot", "player", energy, focus);
                progress.ConfigureCalendar(new LifeCalendar(2, 3));
                return progress;
            }

            var progress = Student(10, 10);
            var paid = new ActivityTimePolicy("policy.paid", 1, 0x7F, false);
            Assert.AreEqual(StudentActivityRefusalReason.None, StudentActivityRefusal.Explain(progress, paid, 1, 1));
            Assert.IsTrue(progress.TrySpendTimePoints(2));
            var noTime = StudentActivityRefusal.Explain(progress, paid, 1, 1);
            var fresh = Student(10, 10);
            var once = new ActivityTimePolicy("policy.once", 0, 0x7F, true);
            Assert.IsTrue(fresh.TryUseTimePolicy(once));
            var doneToday = StudentActivityRefusal.Explain(fresh, once, 0, 0);
            var notToday = StudentActivityRefusal.Explain(fresh, new ActivityTimePolicy("policy.never", 0, 0, false), 0, 0);
            var noEnergy = StudentActivityRefusal.Explain(Student(0, 10), paid, 1, 0);

            Assert.AreEqual(StudentActivityRefusalReason.NoTime, noTime);
            Assert.AreEqual(StudentActivityRefusalReason.DoneToday, doneToday);
            Assert.AreEqual(StudentActivityRefusalReason.NotToday, notToday);
            Assert.AreEqual(StudentActivityRefusalReason.NoEnergy, noEnergy);
            var texts = new HashSet<string>();
            var keys = new HashSet<string>();
            foreach (var reason in new[] { noTime, doneToday, notToday, noEnergy })
            {
                Assert.IsNotEmpty(StudentActivityRefusal.FallbackText(reason));
                texts.Add(StudentActivityRefusal.FallbackText(reason));
                keys.Add(StudentActivityRefusal.LocalizationKey(reason));
            }
            Assert.AreEqual(4, texts.Count);
            Assert.AreEqual(4, keys.Count);
        }

        // 더한 경계

        [Test]
        public void RefusalWithoutProgressOrPolicyAndSettledDay()
        {
            Assert.AreEqual(StudentActivityRefusalReason.None, StudentActivityRefusal.Explain(null, null, 5, 5));
            Assert.AreEqual(string.Empty, StudentActivityRefusal.LocalizationKey(StudentActivityRefusalReason.None));
            var progress = new StudentLifeProgress("slot", "player", 1, 1);
            Assert.AreEqual(StudentActivityRefusalReason.NoEnergy, StudentActivityRefusal.Explain(progress, null, 2, 0));
            Assert.IsTrue(progress.TryEndDay(null, out _));
            Assert.AreEqual(StudentActivityRefusalReason.NoTime, StudentActivityRefusal.Explain(progress, null, 0, 0));
        }

        [Test]
        public void ResultLogSummaryReadsPlusLogsUsesKnownNamesAndSkipsOthers()
        {
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            progress.AddRelationship(Trust.Id, 3);
            progress.AddStatus("status.unknown", 2);
            var logs = new[]
            {
                "activity.help:+relationship.first-guide.trust=2", "activity.rest:+status.unknown=2", "activity.rest:status.fatigue=-1", "noise", null,
            };

            var summary = StudentDayRelationshipConditionSummaryBuilder.BuildFromResultLogs(progress, logs, new[] { Trust }, new[] { new StatusSummaryInfo("status.other", "Other", false) });

            Assert.AreEqual(1, summary.RelationshipEntries.Length);
            Assert.AreEqual("Guide Trust", summary.RelationshipEntries[0].DisplayName);
            Assert.AreEqual(1, summary.RelationshipEntries[0].BeforeValue);
            Assert.AreEqual(1, summary.StatusEntries.Length, "음수 변화 기록은 '+'가 없어 읽지 않는다(원본과 같음).");
            Assert.AreEqual("status.unknown", summary.StatusEntries[0].DisplayName);
            Assert.IsTrue(summary.StatusEntries[0].PersistsToNextDay);
            CollectionAssert.AreEqual(new[] { "Guide Trust affects tomorrow's NPC response.", "status.unknown carries into tomorrow." }, summary.NextDayImpacts);
            Assert.IsEmpty(StudentDayRelationshipConditionSummaryBuilder.BuildFromResultLogs(null, logs, null, null).StatusEntries);
            Assert.IsEmpty(StudentDayRelationshipConditionSummaryBuilder.Build(progress, progress, new[] { Trust }, null).RelationshipEntries);
        }
    }
}
