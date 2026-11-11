using System;
using System.Collections.Generic;
using System.Globalization;
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

            // Unity Gradient(색 키 m_NumColorKeys개, keyN과 ctimeN/65535)와 AnimationCurve(m_Curve) 값. 알파 키는 쓰지 않는다.
            b.Add("DayNightLightingProfile", (n, c) => new DayNightLightingProfile(
                n.Id, n.Float("lightingLerpSpeed"), ColorKeys(n), IntensityKeys(n)));
        }

        private static List<LightingColorKey> ColorKeys(NodeReader n)
        {
            var keys = new List<LightingColorKey>();
            if (!n.TryChild("globalColor", out var gradient)) return keys;
            int count = gradient.Int("m_NumColorKeys");
            for (int i = 0; i < count; i++)
            {
                string index = i.ToString(CultureInfo.InvariantCulture);
                if (!gradient.TryChild("key" + index, out var color)) continue;
                keys.Add(new LightingColorKey(gradient.Int("ctime" + index) / 65535f, color.Float("r"), color.Float("g"), color.Float("b")));
            }

            return keys;
        }

        private static List<LightingCurveKey> IntensityKeys(NodeReader n)
        {
            var keys = new List<LightingCurveKey>();
            if (!n.TryChild("globalIntensity", out var curve)) return keys;
            foreach (var key in curve.Items("m_Curve")) keys.Add(new LightingCurveKey(key.Float("time"), key.Float("value"), key.Float("inSlope"), key.Float("outSlope")));
            return keys;
        }
    }
}
