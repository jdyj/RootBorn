using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Core.Time;
using Rootborn.Data;

namespace Rootborn.World
{
    /// <summary>빛 색(0~1).</summary>
    public readonly struct LightColor
    {
        public readonly float R, G, B;
        public LightColor(float r, float g, float b) { R = r; G = g; B = b; }
        public static readonly LightColor White = new LightColor(1f, 1f, 1f);
        public LightColor Times(float k) => new LightColor(R * k, G * k, B * k);
        public static LightColor Lerp(LightColor a, LightColor b, float t) => new LightColor(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        public override string ToString() => "(" + R.ToString("0.###") + ", " + G.ToString("0.###") + ", " + B.ToString("0.###") + ")";
    }

    /// <summary>
    /// 낮밤 조명(Unity DayNightLightingProfile). 정의 로더가 data/time에서 만든 Core DayNightLightingProfile의 키로
    /// 시각(0~24)의 빛 색(그라디언트, 키 사이 직선)과 세기(곡선, Unity AnimationCurve의 에르미트 보간)를 낸다.
    /// </summary>
    public sealed class DayNightLighting
    {
        private readonly List<(float time, LightColor color)> _colors = new List<(float, LightColor)>();
        private readonly List<(float time, float value, float inSlope, float outSlope)> _intensity = new List<(float, float, float, float)>();

        public float LerpSpeed { get; }

        /// <summary>Core 프로필의 키를 그대로 쓴다(프로필이 이미 시간 순으로 정렬해 두었다).</summary>
        public DayNightLighting(DayNightLightingProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            LerpSpeed = profile.LerpSpeed;
            foreach (LightingColorKey k in profile.ColorKeys) _colors.Add((k.DayFraction, new LightColor(k.R, k.G, k.B)));
            foreach (LightingCurveKey k in profile.IntensityKeys) _intensity.Add((k.Hour, k.Value, k.InSlope, k.OutSlope));
        }

        /// <summary>정의에서 낮밤 조명 프로필 하나를 꺼낸다. 없으면 오류.</summary>
        public static DayNightLighting From(GameDefinitions definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            IReadOnlyList<DayNightLightingProfile> all = definitions.All<DayNightLightingProfile>();
            if (all.Count == 0) throw new InvalidDataException("낮밤 조명 프로필(DayNightLightingProfile)이 없다");
            return new DayNightLighting(all[0]);
        }

        private static float Wrap(float hour)
        {
            float h = hour % 24f;
            return h < 0f ? h + 24f : h;
        }

        public LightColor EvaluateColor(float hour)
        {
            if (_colors.Count == 0) return LightColor.White;
            float t = Wrap(hour) / 24f;
            if (t <= _colors[0].time) return _colors[0].color;
            for (int i = 1; i < _colors.Count; i++)
            {
                if (t > _colors[i].time) continue;
                var (t0, c0) = _colors[i - 1];
                var (t1, c1) = _colors[i];
                return LightColor.Lerp(c0, c1, t1 > t0 ? (t - t0) / (t1 - t0) : 1f);
            }
            return _colors[_colors.Count - 1].color;
        }

        /// <summary>곡선 세기(음수는 0). 키 밖은 끝 키 값(Unity 곡선의 Clamp가 아니어도 0~24 안에서는 같다).</summary>
        public float EvaluateIntensity(float hour)
        {
            if (_intensity.Count == 0) return 1f;
            float x = Wrap(hour);
            if (x <= _intensity[0].time) return Math.Max(0f, _intensity[0].value);
            for (int i = 1; i < _intensity.Count; i++)
            {
                var k1 = _intensity[i];
                if (x > k1.time) continue;
                var k0 = _intensity[i - 1];
                float dx = k1.time - k0.time;
                if (dx <= 0f) return Math.Max(0f, k1.value);
                float t = (x - k0.time) / dx, t2 = t * t, t3 = t2 * t;
                float v = (2 * t3 - 3 * t2 + 1) * k0.value + (t3 - 2 * t2 + t) * dx * k0.outSlope + (-2 * t3 + 3 * t2) * k1.value + (t3 - t2) * dx * k1.inSlope;
                return Math.Max(0f, v);
            }
            return Math.Max(0f, _intensity[_intensity.Count - 1].value);
        }
    }

    /// <summary>
    /// 지금 화면에 입힐 빛(Unity LightingManager): 시각이 정한 목표 색·세기를 t = 1 - exp(-속도 × dt)씩 따라간다.
    /// 화면 색은 빛 색 × 세기(Unity 2D 조명에서 빛을 받는 그림의 색). 시간은 넘겨받은 값으로만 흐른다.
    /// </summary>
    public sealed class DayNightLight
    {
        private readonly DayNightLighting _profile;
        private LightColor _color = LightColor.White;
        private float _intensity = 1f;

        public DayNightLight(DayNightLighting profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public LightColor Tint => _color.Times(_intensity);

        /// <summary>목표로 바로 맞춘다(Unity: 처음 붙을 때 무한대 dt).</summary>
        public void Snap(float hour)
        {
            _color = _profile.EvaluateColor(hour);
            _intensity = _profile.EvaluateIntensity(hour);
        }

        public void Update(float hour, float dt)
        {
            float t = 1f - (float)Math.Exp(-_profile.LerpSpeed * Math.Max(0f, dt));
            _color = LightColor.Lerp(_color, _profile.EvaluateColor(hour), t);
            float target = _profile.EvaluateIntensity(hour);
            _intensity += (target - _intensity) * t;
        }
    }
}
