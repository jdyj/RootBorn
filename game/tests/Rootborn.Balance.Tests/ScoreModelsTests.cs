using System;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Balance.Tests
{
    /// <summary>점수·알바 성공 모델(설계 13.1절): 판마다 새 인스턴스가 부른 차례를 센다.</summary>
    public sealed class ScoreModelsTests
    {
        [Test]
        public void Fixed80_AlwaysGives80()
        {
            var model = ScoreModels.Create("fixed 80", 50, 80);
            Assert.That(model.Id, Is.EqualTo("fixed 80"));
            Assert.That(Enumerable.Range(0, 10).Select(_ => model.Next()), Is.All.EqualTo(80));
        }

        [Test]
        public void FailEvery4_GivesFailScoreOnEvery4thCall_AndPassScoreOtherwise()
        {
            var model = ScoreModels.Create("fail-every 4", 50, 80);
            Assert.That(Enumerable.Range(0, 8).Select(_ => model.Next()), Is.EqualTo(new[] { 80, 80, 80, 50, 80, 80, 80, 50 }));
            var fresh = ScoreModels.Create("fail-every 4", 50, 80);
            Assert.That(fresh.Next(), Is.EqualTo(80), "새 인스턴스는 처음부터 센다");
        }

        [Test]
        public void SuccessFailEvery2_FailsEverySecondCall()
        {
            var model = SuccessModels.Create("fail-every 2");
            Assert.That(model.Id, Is.EqualTo("fail-every 2"));
            Assert.That(Enumerable.Range(0, 6).Select(_ => model.Next()), Is.EqualTo(new[] { true, false, true, false, true, false }));
            var always = SuccessModels.Create("always");
            Assert.That(Enumerable.Range(0, 5).Select(_ => always.Next()), Is.All.True);
        }

        [Test]
        public void UnknownModelSaysItsText()
        {
            foreach (var bad in new[] { "nope", "fixed", "fixed 101", "fixed abc", "fail-every 0" })
            {
                Assert.That(ScoreModels.Knows(bad), Is.False, bad);
                var e = Assert.Throws<ArgumentException>(() => ScoreModels.Create(bad, 50, 80), bad);
                Assert.That(e.Message, Does.Contain(bad));
            }
            foreach (var bad in new[] { "nope", "always 2", "fail-every", "fail-every -1" })
            {
                Assert.That(SuccessModels.Knows(bad), Is.False, bad);
                var e = Assert.Throws<ArgumentException>(() => SuccessModels.Create(bad), bad);
                Assert.That(e.Message, Does.Contain(bad));
            }
            Assert.That(ScoreModels.Knows("fixed 100") && ScoreModels.Knows("fail-every 2") && SuccessModels.Knows("always"), Is.True);
        }
    }
}
