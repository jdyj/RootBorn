using System;
using Rootborn.Core.Time;

namespace Rootborn.Data.Builders
{
    internal static class TimeBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            // Unity TimeDefinition 게터처럼: 하루 길이 1초 이상, 시작 날 1 이상.
            // initialTimeScale(처음 시간 배속)은 DayClock이 받지 않는다. 시간 배속은 Host의 시계 진행 몫이다.
            b.Add("TimeDefinition", (n, c) => new DayClock(
                Math.Max(1f, n.Float("realSecondsPerGameDay")), Math.Max(1, n.Int("startDay"))));
        }
    }
}
