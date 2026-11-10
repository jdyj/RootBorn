using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Time;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    // STATUS-002/TIME-001~003: GameClock의 Tick은 일자/일자 진행률을 결정론적으로 계산한다.
    public sealed class GameClockTests
    {
        private const System.Reflection.BindingFlags Bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        [Test]
        public void TIME_001_DefaultDayLengthIs600SecondsAndRollsToDay2()
        {
            var go = new GameObject("clock");
            try
            {
                var clock = go.AddComponent<GameClock>();

                Assert.AreEqual(600f, clock.RealSecondsPerGameDay, 0.001f);

                clock.Tick(599f);
                Assert.AreEqual(1, clock.Day);
                Assert.AreEqual(599f / 600f, clock.DayProgress01, 0.001f);

                clock.Tick(1f);
                Assert.AreEqual(2, clock.Day);
                Assert.AreEqual(0f, clock.DayProgress01, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TIME_002_TimeDefinitionControlsDayProgressDeterministically()
        {
            var definition = ScriptableObject.CreateInstance<TimeDefinition>();
            var go = new GameObject("clock");
            try
            {
                typeof(TimeDefinition).GetField("_realSecondsPerGameDay", Bind).SetValue(definition, 120f);
                typeof(TimeDefinition).GetField("_startDay", Bind).SetValue(definition, 3);
                typeof(TimeDefinition).GetField("_initialTimeScale", Bind).SetValue(definition, 2f);

                var clock = go.AddComponent<GameClock>();
                clock.Configure(definition);

                Assert.AreEqual(120f, clock.RealSecondsPerGameDay, 0.001f);
                Assert.AreEqual(3, clock.Day);
                Assert.AreEqual(2f, clock.TimeScale, 0.001f);

                clock.Tick(60f);
                Assert.AreEqual(3, clock.Day);
                Assert.AreEqual(0.5f, clock.DayProgress01, 0.001f);

                clock.Tick(60f);
                Assert.AreEqual(4, clock.Day);
                Assert.AreEqual(0f, clock.DayProgress01, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TIME_003_OnDayRolledFiresOnceForEachRolledDay()
        {
            var go = new GameObject("clock");
            try
            {
                var clock = go.AddComponent<GameClock>();
                var rolledDays = new List<int>();
                clock.OnDayRolled += rolledDays.Add;

                clock.Tick(600f * 3f);

                CollectionAssert.AreEqual(new[] { 2, 3, 4 }, rolledDays);
                Assert.AreEqual(4, clock.Day);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Tick_AdvancesDay_When_RealSecondsPerDayElapses()
        {
            var go = new GameObject("clock");
            try
            {
                var clock = go.AddComponent<GameClock>();
                typeof(GameClock).GetField("_realSecondsPerGameDay", Bind).SetValue(clock, 60f);

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
