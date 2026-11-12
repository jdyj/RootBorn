using System;
using System.Linq;
using NUnit.Framework;

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
            var config = Config(_ => { });
            config.Sleep = "sleep-nope";
            AssertFails(config, "sleep-nope", withStrategy: false); // 잠은 전략이 아니라 설정 전체의 값이다
        }

        private static void AssertFails(BalanceConfig config, string name, bool withStrategy)
        {
            var error = Assert.Throws<InvalidOperationException>(() => config.Validate());
            Assert.That(error.Message, Does.Contain(name));
            if (withStrategy) Assert.That(error.Message, Does.Contain("the-strategy"));
        }

        private static BalanceConfig Config(Action<StrategySpec> edit)
        {
            var strategy = new StrategySpec { Id = "the-strategy", Each = "facility", Start = "adult", Scored = true, Do = new[] { "best-facility-work" } };
            edit(strategy);
            return new BalanceConfig
            {
                Days = 1,
                Sleep = "finish-by-bedtime",
                Tiers = new[] { new ScoreTier { Id = "max", Score = 100 } },
                Strategies = new[] { strategy },
            };
        }
    }
}
