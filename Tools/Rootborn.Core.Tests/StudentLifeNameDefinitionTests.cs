// 옮겨 옴: Assets/Scripts/Game/StudentLife/StatusDefinition.cs·RelationshipDefinition.cs·TimeSlotDefinition.cs·StudentLifeCore.cs(StudentLifeDefinitionBase)의 기본값 규칙.
// 원본에는 이 정의만 따로 검사하는 EditMode 테스트가 없다(요약 테스트는 StudentDaySummaryAndRefusalTests).
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    public sealed class StudentLifeNameDefinitionTests
    {
        [Test]
        public void Status_DisplayNameFallsBackToTheId_AndPersistsByDefault()
        {
            var status = new StatusDefinition("status.fatigue", null);
            Assert.That(status.DisplayNameKey, Is.EqualTo("status.fatigue"));
            Assert.That(status.PersistsToNextDay, Is.True);
            Assert.That(new StatusDefinition(null, null).Id, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Summary_UsesTheStatusDefinition_AndSkipsNullOrEmptyOnes()
        {
            var before = new StudentLifeProgress("slot", "player", 10, 10);
            var after = StudentLifeProgress.FromSaveData(before.ToSaveData());
            after.AddStatus("status.stress", 2);
            var summary = StudentDayRelationshipConditionSummaryBuilder.Build(before, after, null,
                new[] { null, new StatusDefinition("", "Empty"), new StatusDefinition("status.stress", "Stress", false) });
            Assert.That(summary.StatusEntries.Length, Is.EqualTo(1));
            Assert.That(summary.StatusEntries[0].DisplayName, Is.EqualTo("Stress"));
            Assert.That(summary.NextDayImpacts, Is.EqualTo(new[] { "Stress recovers before tomorrow." }));
        }
    
        [Test]
        public void Relationship_TargetAndNameFallBackToTheId()
        {
            var trust = new RelationshipDefinition("relationship.first-guide.trust", "Guide Trust", "npc.first-guide");
            Assert.That((trust.DisplayNameKey, trust.TargetId), Is.EqualTo(("Guide Trust", "npc.first-guide")));
            var bare = new RelationshipDefinition("relationship.x", "", null);
            Assert.That((bare.DisplayNameKey, bare.TargetId), Is.EqualTo(("relationship.x", "relationship.x")));
        }

        [Test]
        public void Summary_UsesTheRelationshipDefinition_AndSkipsNullOnes()
        {
            var before = new StudentLifeProgress("slot", "player", 10, 10);
            var after = StudentLifeProgress.FromSaveData(before.ToSaveData());
            after.AddRelationship("relationship.x", 1);
            var summary = StudentDayRelationshipConditionSummaryBuilder.Build(before, after,
                new[] { null, new RelationshipDefinition("relationship.x", "X", null) }, null);
            Assert.That(summary.RelationshipEntries.Length, Is.EqualTo(1));
            Assert.That(summary.RelationshipEntries[0].DisplayName, Is.EqualTo("X"));
        }

        [Test]
        public void TimeSlot_KeepsItsOrderAndName()
        {
            var morning = new TimeSlotDefinition("time.morning", "time.morning.name", 0);
            var night = new TimeSlotDefinition("time.night", null, 3);
            Assert.That((morning.Order, morning.DisplayNameKey), Is.EqualTo((0, "time.morning.name")));
            Assert.That((night.Order, night.DisplayNameKey), Is.EqualTo((3, "time.night")));
        }
    }
}
