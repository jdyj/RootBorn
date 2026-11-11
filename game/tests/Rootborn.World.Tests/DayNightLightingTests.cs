using System.IO;
using NUnit.Framework;

namespace Rootborn.World.Tests
{
    /// <summary>자동 테스트: 정의 로더가 data/time에서 만든 낮밤 조명 프로필. 화면 근거가 아니다.</summary>
    public sealed class DayNightLightingTests
    {
        private static DayNightLighting Profile => DayNightLighting.From(Repo.Definitions);

        [Test]
        public void ColorKeysAndIntensityKeysAreTheUnityProfileValues()
        {
            DayNightLighting p = Profile;
            Assert.That(p.LerpSpeed, Is.EqualTo(2f));
            LightColor midnight = p.EvaluateColor(0f);
            Assert.That(midnight.R, Is.EqualTo(0.09f).Within(0.0001f));
            Assert.That(midnight.G, Is.EqualTo(0.16f).Within(0.0001f));
            Assert.That(midnight.B, Is.EqualTo(0.29f).Within(0.0001f));
            LightColor noon = p.EvaluateColor(12f);
            Assert.That(noon.R, Is.EqualTo(1f).Within(0.001f));
            Assert.That(noon.B, Is.EqualTo(1f).Within(0.001f));
            Assert.That(p.EvaluateIntensity(12f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(p.EvaluateIntensity(0f), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(p.EvaluateIntensity(20f), Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(p.EvaluateIntensity(24f + 12f), Is.EqualTo(1f).Within(0.0001f), "시각은 24로 감는다");
        }

        [Test]
        public void BetweenKeysColorIsLinearAndIntensityFollowsFlatTangents()
        {
            DayNightLighting p = Profile;
            // 12시(흰색)와 17.5시(1, 0.69, 0.4) 사이의 가운데.
            LightColor c = p.EvaluateColor(14.75f);
            Assert.That(c.G, Is.EqualTo((1f + 0.69f) / 2f).Within(0.002f));
            // 기울기 0인 키 사이는 3t²-2t³: 가운데에서 정확히 중간값.
            Assert.That(p.EvaluateIntensity(14.75f), Is.EqualTo((1f + 0.85f) / 2f).Within(0.0001f));
            Assert.That(p.EvaluateIntensity(13f), Is.GreaterThan(1f - 0.15f * 0.5f), "앞쪽 4분의 1 지점은 직선보다 앞 키에 가깝다");
        }

        [Test]
        public void LightApproachesTheTargetExponentiallyAndSnapsWhenAsked()
        {
            DayNightLighting p = Profile;
            var light = new DayNightLight(p);
            light.Snap(12f);
            Assert.That(light.Tint.R, Is.EqualTo(1f).Within(0.001f));
            light.Update(0f, 0.5f);
            float t = 1f - (float)System.Math.Exp(-2f * 0.5f);
            float expected = (1f + (0.09f - 1f) * t) * (1f + (0.25f - 1f) * t);
            Assert.That(light.Tint.R, Is.EqualTo(expected).Within(0.002f), "Unity LightingManager: 색과 세기를 따로 t = 1 - exp(-속도 × dt)만큼 따라간다");
            light.Snap(0f);
            Assert.That(light.Tint.B, Is.EqualTo(0.29f * 0.25f).Within(0.001f), "화면 색 = 빛 색 × 세기");
        }
    }
}
