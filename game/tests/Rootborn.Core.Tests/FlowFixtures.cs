using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Core.Tests
{
    // 시간 포인트 한도가 없어진 뒤(흐르는 시간 2026-11-12) "오늘은 더 못 한다"를 만드는 법: 지치게 한다.
    internal static class FlowFixtures
    {
        /// <summary>
        /// 지금 시각을 취침 시각 1시간 뒤로 보는 규칙을 넣고 1시간을 보내 체력을 0으로 만든다. 시계가 60분 간다.
        /// 날이 넘어가지는 않는다(하루 시작은 지금보다 이르다).
        /// </summary>
        public static void Exhaust(StudentLifeProgress p)
        {
            int clock = TimeRules.ClockMinutes(p.TimeMinutes);
            int bedtime = System.Math.Max(0, clock - 60);
            p.ConfigureTimeRules(new TimeRules(600f, System.Math.Max(0, bedtime - 1), bedtime, 9999, 2));
            p.AdvanceClock(60);
            Assert.That(p.IsExhausted, Is.True, "준비: 지쳤다");
        }
    }
}
