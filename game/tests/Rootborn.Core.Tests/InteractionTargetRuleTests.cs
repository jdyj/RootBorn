using NUnit.Framework;
using Rootborn.Core.Interaction;

namespace Rootborn.Core.Tests
{
    public sealed class InteractionTargetRuleTests
    {
        [Test]
        public void ClearlyCloserTargetWinsOverHigherPriority()
        {
            // Standing at the self-study desk (0.3 away) with a townsperson (priority 30) passing 1.4 away: E studies.
            Assert.That(InteractionTargetRule.IsBetter(0, 0.3f, 30, 1.4f), Is.True);
            Assert.That(InteractionTargetRule.IsBetter(30, 1.4f, 0, 0.3f), Is.False);
        }

        [Test]
        public void NearlyEqualDistancesFallBackToPriority()
        {
            // A door and a sign side by side: the door (higher priority) wins.
            Assert.That(InteractionTargetRule.IsBetter(110, 0.9f, 45, 0.6f), Is.True);
            Assert.That(InteractionTargetRule.IsBetter(45, 0.6f, 110, 0.9f), Is.False);
        }

        [Test]
        public void SamePriorityPrefersTheCloserOne()
        {
            Assert.That(InteractionTargetRule.IsBetter(45, 0.5f, 45, 0.7f), Is.True);
            Assert.That(InteractionTargetRule.IsBetter(45, 0.7f, 45, 0.5f), Is.False);
        }

        [Test]
        public void TheBandEdgeIsStillATie()
        {
            float band = InteractionTargetRule.DistanceBand;
            Assert.That(InteractionTargetRule.IsBetter(10, 1f, 50, 1f + band), Is.False, "exactly one band closer is still a tie, so priority decides");
            Assert.That(InteractionTargetRule.IsBetter(10, 1f, 50, 1f + band + 0.01f), Is.True);
        }

        [Test]
        public void AnyCandidateBeatsNoCandidate()
        {
            Assert.That(InteractionTargetRule.IsBetter(0, 1.7f, int.MinValue, float.MaxValue), Is.True);
        }
    }
}
