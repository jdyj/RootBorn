using System;
using System.Collections.Generic;

namespace Rootborn.Core.Time
{
    /// <summary>낮밤 빛 색 키 하나. DayFraction은 하루 중 위치(0~1, 0시~24시), 색은 0~1.</summary>
    public readonly struct LightingColorKey
    {
        public LightingColorKey(float dayFraction, float r, float g, float b)
        {
            DayFraction = dayFraction;
            R = r;
            G = g;
            B = b;
        }

        public float DayFraction { get; }
        public float R { get; }
        public float G { get; }
        public float B { get; }
    }

    /// <summary>낮밤 빛 세기 곡선 키 하나(Unity AnimationCurve 키). Hour는 0~24.</summary>
    public readonly struct LightingCurveKey
    {
        public LightingCurveKey(float hour, float value, float inSlope, float outSlope)
        {
            Hour = hour;
            Value = value;
            InSlope = inSlope;
            OutSlope = outSlope;
        }

        public float Hour { get; }
        public float Value { get; }
        public float InSlope { get; }
        public float OutSlope { get; }
    }

    /// <summary>
    /// 낮밤 조명 프로필의 값(원본 Assets/Scripts/Game/Lighting/DayNightLightingProfile.cs). 색 키와 세기 키는 시간 순서로 정렬해 둔다.
    /// 시각에서 빛을 계산하는 일은 화면(월드) 몫이다. 따라가는 속도는 0.01 이상이다(Unity 판 LightingLerpSpeed와 같다).
    /// </summary>
    public sealed class DayNightLightingProfile
    {
        private readonly LightingColorKey[] _colors;
        private readonly LightingCurveKey[] _intensity;

        public DayNightLightingProfile(string id, float lerpSpeed, IReadOnlyList<LightingColorKey> colorKeys, IReadOnlyList<LightingCurveKey> intensityKeys)
        {
            Id = id ?? string.Empty;
            LerpSpeed = Math.Max(0.01f, lerpSpeed);
            _colors = Sorted(colorKeys, (a, b) => a.DayFraction.CompareTo(b.DayFraction));
            _intensity = Sorted(intensityKeys, (a, b) => a.Hour.CompareTo(b.Hour));
        }

        public string Id { get; }
        public float LerpSpeed { get; }
        public IReadOnlyList<LightingColorKey> ColorKeys => _colors;
        public IReadOnlyList<LightingCurveKey> IntensityKeys => _intensity;

        // 같은 시간의 키는 받은 순서를 지킨다(안정 정렬).
        private static T[] Sorted<T>(IReadOnlyList<T> keys, Comparison<T> compare)
        {
            var list = new List<T>(keys ?? Array.Empty<T>());
            var indexed = new List<KeyValuePair<int, T>>(list.Count);
            for (int i = 0; i < list.Count; i++) indexed.Add(new KeyValuePair<int, T>(i, list[i]));
            indexed.Sort((a, b) =>
            {
                int byTime = compare(a.Value, b.Value);
                return byTime != 0 ? byTime : a.Key.CompareTo(b.Key);
            });
            var result = new T[indexed.Count];
            for (int i = 0; i < result.Length; i++) result[i] = indexed[i].Value;
            return result;
        }
    }
}
