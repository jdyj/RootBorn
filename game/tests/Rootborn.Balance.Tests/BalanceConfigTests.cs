using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    public sealed class BalanceConfigTests
    {
        [Test]
        public void TheCommittedConfigIsValid()
        {
            var config = BalanceConfig.Read();
            Assert.That(() => config.Validate(), Throws.Nothing);
            Assert.That(config.Days, Is.EqualTo(30));
            Assert.That(config.Tiers.Select(t => t.Score), Is.EqualTo(new[] { 60, 80, 100 }));
        }

        [Test]
        public void UnknownKindNamesFailWithTheStrategyAndTheName()
        {
            AssertFails(Config(s => s.Each = "each-nope"), "each-nope", withStrategy: true);
            AssertFails(Config(s => s.Start = "start-nope"), "start-nope", withStrategy: true);
            AssertFails(Config(s => s.Do = new[] { "best-facility-work", "do-nope" }), "do-nope", withStrategy: true);
            AssertFails(Config(s => s.Do = new[] { "facility-work-level x" }), "facility-work-level x", withStrategy: true);
            var config = Config(_ => { });
            config.BaselineSetup.Sleep = "sleep-nope";
            AssertFails(config, "sleep-nope", withStrategy: false); // 잠은 전략이 아니라 기준 가정의 값이다
        }

        [Test]
        public void BandsAreNotCheckedYet_SoANonEmptyListFails()
        {
            var config = Config(_ => { });
            config.BandCount = 1;
            var error = Assert.Throws<InvalidOperationException>(() => config.Validate());
            Assert.That(error.Message, Does.Contain("bands"));
            Assert.That(BalanceConfig.Read().BandCount, Is.Zero, "올린 설정의 bands는 비어 있다");
        }

        [Test]
        public void StrategyNoteIsOptional()
        {
            var config = BalanceConfig.Read();
            Assert.That(config.Strategies.Any(s => !string.IsNullOrEmpty(s.Note)), Is.True, "설명 한 줄(note)을 가진 전략이 있다");
            Assert.That(config.Strategies.Any(s => s.Note == null), Is.True, "note가 없는 전략은 null이다");
        }

        [Test]
        public void AxesAreRead_AndContainTheBaseline()
        {
            var config = BalanceConfig.Read();
            var axes = config.Axes;
            var baseline = config.BaselineSetup;
            Assert.That(baseline.Sleep, Is.EqualTo("finish-by 22:00"));
            Assert.That(baseline.Score, Is.EqualTo("fixed 80"));
            Assert.That(baseline.PartTime, Is.EqualTo("always"));
            Assert.That(baseline.TravelMinutes, Is.EqualTo(DayAssumptions.Current.TravelMinutes), "걷기 기준은 day-balance.json");
            Assert.That(baseline.MinigameShare, Is.EqualTo(DayAssumptions.Current.MinigameShare), "몫 기준은 day-balance.json");
            Assert.That(axes.Sleep, Does.Contain(baseline.Sleep));
            Assert.That(axes.Score, Does.Contain(baseline.Score));
            Assert.That(axes.PartTime, Does.Contain(baseline.PartTime));
            Assert.That(axes.TravelMinutes, Does.Contain(baseline.TravelMinutes));
            Assert.That(axes.MinigameShare, Does.Contain(baseline.MinigameShare));
            Assert.That(axes.Sleep, Does.Contain("never"));
            Assert.That(axes.Days, Is.EqualTo(100));
            Assert.That(axes.Checkpoints, Is.EqualTo(new[] { 7, 30, 100 }));
            Assert.That(axes.FailScore, Is.EqualTo(50));
            Assert.That(axes.PassScore, Is.EqualTo(80));
        }

        [Test]
        public void AnAxisMissingItsBaselineFailsAndNamesTheAxis()
        {
            AssertAxisFails(c => c.Axes.Sleep = new[] { "never" }, "sleep");
            AssertAxisFails(c => c.Axes.Score = new[] { "fixed 50" }, "score");
            AssertAxisFails(c => c.Axes.PartTime = new[] { "fail-every 2" }, "partTime");
            AssertAxisFails(c => c.Axes.TravelMinutes = new[] { 0, 15 }, "travelMinutes");
            AssertAxisFails(c => c.Axes.MinigameShare = new[] { 0.25f }, "minigameShare");
        }

        [Test]
        public void UnknownSleepOrScoreTextInAxesFailsAndNamesIt()
        {
            AssertAxisFails(c => c.Axes.Sleep = c.Axes.Sleep.Concat(new[] { "doze 3" }).ToArray(), "doze 3");
            AssertAxisFails(c => c.Axes.Score = c.Axes.Score.Concat(new[] { "fixed 120" }).ToArray(), "fixed 120");
            AssertAxisFails(c => c.Axes.PartTime = c.Axes.PartTime.Concat(new[] { "sometimes" }).ToArray(), "sometimes");
        }

        private static void AssertAxisFails(Action<BalanceConfig> edit, string name)
        {
            var config = BalanceConfig.Read();
            edit(config);
            var error = Assert.Throws<InvalidOperationException>(() => config.Validate(), name);
            Assert.That(error.Message, Does.Contain(name));
        }

        private static void AssertFails(BalanceConfig config, string name, bool withStrategy)
        {
            var error = Assert.Throws<InvalidOperationException>(() => config.Validate());
            Assert.That(error.Message, Does.Contain(name));
            if (withStrategy) Assert.That(error.Message, Does.Contain("the-strategy"));
        }

        // 올린 설정의 축·기준 가정 위에 전략 하나만 손으로 둔다.
        private static BalanceConfig Config(Action<StrategySpec> edit)
        {
            var strategy = new StrategySpec { Id = "the-strategy", Each = "facility", Start = "adult", Scored = true, Do = new[] { "best-facility-work" } };
            edit(strategy);
            var read = BalanceConfig.Read();
            return new BalanceConfig
            {
                Days = 1,
                BaselineSetup = RunSetup.Baseline(read),
                Axes = read.Axes,
                Tiers = new[] { new ScoreTier { Id = "max", Score = 100 } },
                Strategies = new[] { strategy },
            };
        }
    }
}
