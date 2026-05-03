using NUnit.Framework;
using Rootborn.Game.Time;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    // STATUS-002: GameClock의 Tick은 일자/일자 진행률을 결정론적으로 계산한다.
    public sealed class GameClockTests
    {
        [Test]
        public void Tick_AdvancesDay_When_RealSecondsPerDayElapses()
        {
            var go = new GameObject("clock");
            try
            {
                var clock = go.AddComponent<GameClock>();
                var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(GameClock).GetField("_realSecondsPerGameDay", bind).SetValue(clock, 60f);

                clock.Tick(30f);
                Assert.AreEqual(1, clock.Day);
                Assert.AreEqual(0.5f, clock.DayProgress01, 0.001f);

                clock.Tick(30f);
                Assert.AreEqual(2, clock.Day);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
