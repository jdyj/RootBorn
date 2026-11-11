// 옮겨 옴: Assets/Scripts/Game/Lighting/DayNightLightingProfile.cs(값과 LightingLerpSpeed 하한)
using NUnit.Framework;
using Rootborn.Core.Time;

namespace Rootborn.Core.Tests
{
    public sealed class DayNightLightingProfileTests
    {
        [Test]
        public void KeysAreKeptInTimeOrder_AndEqualTimesKeepTheirOrder()
        {
            var profile = new DayNightLightingProfile("lighting", 2f,
                new[] { new LightingColorKey(0.5f, 1, 1, 1), new LightingColorKey(0f, 0.1f, 0.2f, 0.3f), new LightingColorKey(0.5f, 0.5f, 0.5f, 0.5f) },
                new[] { new LightingCurveKey(12f, 1f, 0, 0), new LightingCurveKey(0f, 0.25f, 0, 0) });

            Assert.That(profile.ColorKeys[0].R, Is.EqualTo(0.1f));
            Assert.That(profile.ColorKeys[1].R, Is.EqualTo(1f), "같은 시간은 받은 순서");
            Assert.That(profile.ColorKeys[2].R, Is.EqualTo(0.5f));
            Assert.That(profile.IntensityKeys[0].Hour, Is.EqualTo(0f));
            Assert.That(profile.LerpSpeed, Is.EqualTo(2f));
        }

        [Test]
        public void LerpSpeedIsAtLeastAHundredth_AndMissingKeysAreEmpty()
        {
            var profile = new DayNightLightingProfile(null, 0f, null, null);
            Assert.That(profile.LerpSpeed, Is.EqualTo(0.01f));
            Assert.That(profile.ColorKeys, Is.Empty);
            Assert.That(profile.IntensityKeys, Is.Empty);
            Assert.That(profile.Id, Is.Empty);
        }
    }
}
