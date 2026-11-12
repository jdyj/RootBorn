using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Workspaces;

namespace Rootborn.Core.Tests
{
    public sealed class WorkLinksTests
    {
        private sealed class FixedSource : ISpaceQualitySource
        {
            private readonly (string Id, long Score)[] _scores;
            public FixedSource(params (string, long)[] scores) { _scores = scores; }
            public void AddTo(Dictionary<string, long> totals)
            {
                foreach (var (id, score) in _scores) totals[id] = score;
            }
        }

        private static SpaceScores Scores(params (string, long)[] scores)
            => SpaceScoreCalculator.Calculate(new ISpaceQualitySource[] { new FixedSource(scores) }, 100);

        [Test]
        public void RewardBonus_StepsAndCaps()
        {
            var bonus = new QualityRewardPercentBonus("tidy", 10, 5, 8);

            Assert.That(bonus.RewardPercent(Scores(("tidy", 25))), Is.EqualTo(8));
            Assert.That(bonus.RewardPercent(Scores(("tidy", 9))), Is.EqualTo(0));
            Assert.That(bonus.RewardPercent(Scores(("tidy", 10))), Is.EqualTo(5));
            Assert.That(bonus.Reputation(Scores(("tidy", 25))), Is.EqualTo(0));
            Assert.That(new QualityRewardPercentBonus("tidy", 0, 5, 8).RewardPercent(Scores(("tidy", 25))), Is.EqualTo(0));
            Assert.That(new QualityRewardPercentBonus("tidy", 10, -5, 8).RewardPercent(Scores(("tidy", 25))), Is.EqualTo(0));
        }

        [Test]
        public void ReputationBonus_StepsAndCaps()
        {
            var bonus = new QualityReputationBonus("cozy", 5, 2, 7);

            Assert.That(bonus.Reputation(Scores(("cozy", 12))), Is.EqualTo(4));
            Assert.That(bonus.Reputation(Scores(("cozy", 100))), Is.EqualTo(7));
            Assert.That(bonus.Reputation(Scores(("cozy", 4))), Is.EqualTo(0));
            Assert.That(bonus.RewardPercent(Scores(("cozy", 100))), Is.EqualTo(0));
            Assert.That(bonus.Reputation(SpaceScores.Empty), Is.EqualTo(0));
        }

        [Test]
        public void ExtrasFor_SumsAllBindingsOfThatWork()
        {
            var links = new WorkLinks(
                new[]
                {
                    new WorkBonusBinding("b1", "w", new IWorkspaceBonus[] { new QualityRewardPercentBonus("tidy", 10, 5, 8), new QualityReputationBonus("cozy", 5, 2, 7) }),
                    new WorkBonusBinding("b2", "w", new IWorkspaceBonus[] { new QualityRewardPercentBonus("tidy", 10, 1, 3) }),
                    new WorkBonusBinding("b3", "other", new IWorkspaceBonus[] { new QualityRewardPercentBonus("tidy", 10, 50, 50) }),
                },
                null);

            var extras = links.ExtrasFor("w", Scores(("tidy", 25), ("cozy", 12)));

            Assert.That(extras.RewardPercentBonus, Is.EqualTo(8 + 2));
            Assert.That(extras.ReputationBonus, Is.EqualTo(4));
        }

        [Test]
        public void ExtrasFor_UnlinkedWork_IsDefault()
        {
            var links = new WorkLinks(
                new[] { new WorkBonusBinding("b", "w", new IWorkspaceBonus[] { new QualityRewardPercentBonus("tidy", 10, 5, 8) }) },
                null);

            var extras = links.ExtrasFor("nope", Scores(("tidy", 99)));

            Assert.That(extras.RewardPercentBonus, Is.EqualTo(0));
            Assert.That(extras.ReputationBonus, Is.EqualTo(0));
            Assert.That(extras.SkillGains, Is.Empty);
            Assert.That(default(WorkExtras).SkillGains, Is.Empty);
        }

        [Test]
        public void ExtrasFor_IncludesSkillGains()
        {
            var links = new WorkLinks(
                null,
                new[]
                {
                    new WorkSkillGrowth("g1", "w", "cooking", 3),
                    new WorkSkillGrowth("g2", "w", "tidying", 1),
                    new WorkSkillGrowth("g3", "other", "x", 9),
                });

            var extras = links.ExtrasFor("w", SpaceScores.Empty);

            Assert.That(extras.SkillGains.Count, Is.EqualTo(2));
            Assert.That(extras.SkillGains[0].SkillId, Is.EqualTo("cooking"));
            Assert.That(extras.SkillGains[0].Amount, Is.EqualTo(3));
            Assert.That(extras.SkillGains[1].SkillId, Is.EqualTo("tidying"));
        }
    }
}
