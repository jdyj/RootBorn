using NUnit.Framework;
using Rootborn.Game.Status;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    // STATUS-001: 상태 값은 시간 경과에 따라 누적되고, 클램프 + Restore + Penalty 곡선이 동작한다.
    public sealed class StatusValueTests
    {
        private static StatusEffectDefinition MakeDef(float decay, float max)
        {
            var s = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            var bind = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(StatusEffectDefinition).GetField("_decayPerSecond", bind).SetValue(s, decay);
            typeof(StatusEffectDefinition).GetField("_maxValue", bind).SetValue(s, max);
            typeof(StatusEffectDefinition).GetField("_gameplayPenalty", bind)
                .SetValue(s, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return s;
        }

        [Test]
        public void Tick_AccumulatesAndClamps()
        {
            var def = MakeDef(decay: 2f, max: 10f);
            var v = new StatusValue(def);
            v.Tick(1f);
            Assert.AreEqual(2f, v.Current, 0.0001f);
            v.Tick(100f);
            Assert.AreEqual(10f, v.Current, 0.0001f);
        }

        [Test]
        public void Restore_DecreasesAndFloorsAtZero()
        {
            var def = MakeDef(decay: 1f, max: 100f);
            var v = new StatusValue(def, initial: 50f);
            v.Restore(10f);
            Assert.AreEqual(40f, v.Current, 0.0001f);
            v.Restore(999f);
            Assert.AreEqual(0f, v.Current, 0.0001f);
        }

        [Test]
        public void Penalty_LinearAcrossRange()
        {
            var def = MakeDef(decay: 1f, max: 100f);
            var v = new StatusValue(def, initial: 50f);
            Assert.AreEqual(0.5f, v.Penalty01(), 0.001f);
        }
    }
}
