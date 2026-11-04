using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Lighting;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Time;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode
{
    public sealed class LifeTimeLightingTests
    {
        [Test]
        public void SceneLifeTimeDrivesLightingWithoutChangingLegacyClock()
        {
            var actor = new GameObject("LifeTimeLightingActor");
            var lightObject = new GameObject("LifeTimeLightingGlobal");
            var clockObject = new GameObject("LifeTimeLightingLegacyClock");
            var profile = ScriptableObject.CreateInstance<DayNightLightingProfile>();
            try
            {
                var clock = clockObject.AddComponent<GameClock>();
                clock.SetCurrentHour(22f);
                var owner = actor.AddComponent<StudentLifeProgressComponent>();
                owner.ConfigureForTests("light-slot", "light-player", 12, 10, 0, 480);
                var light = lightObject.AddComponent<Light2D>();
                var lighting = lightObject.AddComponent<LightingManager>();
                lighting.Configure(light, profile, clock);
                var sourceType = typeof(GameClock).Assembly.GetType("Rootborn.Game.Time.LifeTimePresentation");
                Assert.That(sourceType, Is.Not.Null, "Town lighting needs the same life time presentation source as the HUD.");
                var source = actor.AddComponent(sourceType);
                sourceType.GetMethod("Bind").Invoke(source, new object[] { owner });
                var bind = typeof(LightingManager).GetMethod("BindLifeTimePresentation", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(bind, Is.Not.Null);
                bind.Invoke(lighting, new[] { source });
                Assert.That(light.intensity, Is.EqualTo(profile.EvaluateIntensity(8f)).Within(0.001f));
                owner.Progress.Spend(600, 0, 0, 0);
                Assert.That(light.intensity, Is.EqualTo(profile.EvaluateIntensity(18f)).Within(0.001f));
                Assert.That(clock.CurrentHour, Is.EqualTo(22f).Within(0.001f), "Presentation must not mutate the Farm clock.");
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(clockObject);
                Object.DestroyImmediate(profile);
            }
        }
    }
}
