using NUnit.Framework;
using Rootborn.Game.Lighting;
using Rootborn.Game.Time;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rootborn.Tests.EditMode
{
    public sealed class DayNightLightingTests
    {
        private const System.Reflection.BindingFlags Bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        [Test]
        public void TIME_DAYNIGHT_001_GameClockExposesWrappedGameHourAndMinute()
        {
            var go = new GameObject("clock");
            try
            {
                var clock = go.AddComponent<GameClock>();
                clock.Tick(600f * 0.25f);

                Assert.AreEqual(6f, clock.CurrentHour, 0.001f);
                Assert.AreEqual(6, clock.Hour);
                Assert.AreEqual(0, clock.Minute);

                clock.Tick(600f * 0.999f);

                Assert.GreaterOrEqual(clock.CurrentHour, 0f);
                Assert.Less(clock.CurrentHour, 24f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TIME_DAYNIGHT_002_GameClockTimeScaleControlsRuntimeAdvance()
        {
            var go = new GameObject("clock");
            try
            {
                var clock = go.AddComponent<GameClock>();
                clock.TimeScale = 3f;

                clock.Tick(10f * clock.TimeScale);

                Assert.AreEqual(30f / 600f * 24f, clock.CurrentHour, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TIME_DAYNIGHT_003_GameClockClassifiesDayPartsAtBoundaries()
        {
            Assert.IsTrue(GameClock.IsMorningHour(5f));
            Assert.IsFalse(GameClock.IsMorningHour(11f));
            Assert.IsTrue(GameClock.IsAfternoonHour(11f));
            Assert.IsTrue(GameClock.IsEveningHour(17f));
            Assert.IsFalse(GameClock.IsEveningHour(20f));
            Assert.IsTrue(GameClock.IsNightHour(20f));
            Assert.IsTrue(GameClock.IsNightHour(4.99f));
            Assert.IsFalse(GameClock.IsNightHour(5f));
        }

        [Test]
        public void LIGHT_DAYNIGHT_001_ProfileEvaluatesColorAndIntensityByHour()
        {
            var profile = ScriptableObject.CreateInstance<DayNightLightingProfile>();
            try
            {
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Color.black, 0f),
                        new GradientColorKey(Color.white, 0.5f),
                        new GradientColorKey(Color.blue, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    });

                typeof(DayNightLightingProfile).GetField("_globalColor", Bind).SetValue(profile, gradient);
                typeof(DayNightLightingProfile).GetField("_globalIntensity", Bind).SetValue(profile, AnimationCurve.Linear(0f, 0.25f, 24f, 1f));

                Assert.AreEqual(Color.white.r, profile.EvaluateColor(12f).r, 0.01f);
                Assert.AreEqual(0.625f, profile.EvaluateIntensity(12f), 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void LIGHT_DAYNIGHT_002_LightingManagerMovesGlobalLightTowardProfileTarget()
        {
            var profile = ScriptableObject.CreateInstance<DayNightLightingProfile>();
            var go = new GameObject("lighting-manager");
            var lightObject = new GameObject("global-light");
            try
            {
                typeof(DayNightLightingProfile).GetField("_globalIntensity", Bind).SetValue(profile, AnimationCurve.Constant(0f, 24f, 0.3f));
                typeof(DayNightLightingProfile).GetField("_lightingLerpSpeed", Bind).SetValue(profile, 100f);

                var light = lightObject.AddComponent<Light2D>();
                light.intensity = 1f;
                var manager = go.AddComponent<LightingManager>();
                manager.Configure(light, profile);
                manager.ApplyTimeOfDay(18f, 0.1f);

                Assert.Less(light.intensity, 1f);
                Assert.AreEqual(0.3f, light.intensity, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(lightObject);
            }
        }

        [Test]
        public void LIGHT_DAYNIGHT_003_LocalLightControllerUsesProfileForDayAndNightState()
        {
            var go = new GameObject("local-light");
            try
            {
                var light = go.AddComponent<Light2D>();
                var controller = go.AddComponent<LocalLightController>();
                controller.Configure(light, 0.1f, 0.85f);

                controller.ApplyTimeOfDay(12f);
                Assert.AreEqual(0.1f, light.intensity, 0.001f);

                controller.ApplyTimeOfDay(22f);
                Assert.AreEqual(0.85f, light.intensity, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
