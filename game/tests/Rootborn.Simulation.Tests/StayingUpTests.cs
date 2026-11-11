using System.Linq;
using NUnit.Framework;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 밤샘(흐르는 시간 설계 3.4): 자러 가지 않는 플레이어를 며칠 돌려도 예외·막힘이 없고, 같은 씨앗으로 잔 플레이어(학교 밖 정책 — 같은 선호, 하루 마감 때 잔다)보다
    /// 얻는 성장이 많지 않다. 자동 테스트(창 없음). 실제 창은 Host HostPlayTests가 같은 비교를 한다.
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class StayingUpTests
    {
        private static PlaySettings ThreeDays() => new PlaySettings { Days = 3, StepsPerDay = 80, MaxSteps = 2000 };

        [Test]
        public void StayingUp_ForDays_HasNoFailure_AndGrowsNoMoreThanSleeping()
        {
            PlayResult up = FastPlayTests.Play(PlayPolicyCatalog.StayUp, 1, ThreeDays(), "stay-up-vs-sleep-up");
            PlayResult slept = FastPlayTests.Play(PlayPolicyCatalog.OffSchool, 1, ThreeDays(), "stay-up-vs-sleep-slept");
            Assert.That(up.Failure, Is.Null, up.Failure?.What);
            Assert.That(slept.Failure, Is.Null, slept.Failure?.What);
            Assert.That(up.DaysCompleted, Is.GreaterThanOrEqualTo(3), "자지 않아도 날은 넘어간다");
            Assert.That(up.Notes.Count(n => n.Contains("자지 않고 넘어왔다")), Is.GreaterThanOrEqualTo(2), "날마다 자지 않고 넘어왔다");

            int grownUp = Growth(up), grownSlept = Growth(slept);
            TestContext.WriteLine("성장 합(특성+기술): 밤샘 " + grownUp + ", 잠 " + grownSlept);
            Assert.That(grownUp, Is.LessThanOrEqualTo(grownSlept), "밤샘이 자는 것보다 이득이면 안 된다(사용자 결정 2026-11-12)");
        }

        /// <summary>끝 상태의 특성·기술 값 합(StateSnapshot 줄 "trait id=n", "skill id=n").</summary>
        public static int Growth(PlayResult result) => result.FinalState
            .Where(l => l.StartsWith("trait ") || l.StartsWith("skill "))
            .Sum(l => int.Parse(l.Substring(l.LastIndexOf('=') + 1)));
    }
}
