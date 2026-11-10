using NUnit.Framework;
using Rootborn.Core.Common;

namespace Rootborn.Core.Tests
{
    public sealed class CoreMathTests
    {
        [TestCase(25f, 24f, 1f)]
        [TestCase(-1f, 24f, 23f)]
        [TestCase(24f, 24f, 0f)]
        [TestCase(0f, 24f, 0f)]
        [TestCase(48.5f, 24f, 0.5f)]
        public void Repeat_WrapsIntoZeroToLength(float value, float length, float expected)
        {
            Assert.That(CoreMath.Repeat(value, length), Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(-0.5f, -1)]
        [TestCase(2.9f, 2)]
        [TestCase(3f, 3)]
        public void FloorToInt_RoundsTowardNegativeInfinity(float value, int expected)
        {
            Assert.That(CoreMath.FloorToInt(value), Is.EqualTo(expected));
        }

        [TestCase(-1f, 0f)]
        [TestCase(2f, 1f)]
        [TestCase(0.3f, 0.3f)]
        public void Clamp01_KeepsValuesBetweenZeroAndOne(float value, float expected)
        {
            Assert.That(CoreMath.Clamp01(value), Is.EqualTo(expected));
        }
    }
}
