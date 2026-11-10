using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Localization;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    // The words shown when E at a time-and-energy activity is refused: each cause has its own message, and an allowed
    // activity explains nothing.
    public sealed class StudentActivityRefusalTextTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        [Test]
        public void ExplainsSpentTimeOnceDoneWrongDayAndEnergySeparately()
        {
            var progress = Student(energy: 10, focus: 10);
            var paid = Policy("policy.paid", cost: 1, mask: 0x7F, oncePerDay: false);
            Assert.AreEqual(string.Empty, StudentActivityRefusalText.Explain(progress, paid, 1, 1), "An affordable activity must not explain a refusal.");

            Assert.IsTrue(progress.TrySpendTimePoints(2));
            Assert.AreEqual(GameText.Localized("ui.prompt.no-time", "x"), StudentActivityRefusalText.Explain(progress, paid, 1, 1));

            var fresh = Student(energy: 10, focus: 10);
            var once = Policy("policy.once", cost: 0, mask: 0x7F, oncePerDay: true);
            Assert.IsTrue(fresh.TryUseTimePolicy(once));
            Assert.AreEqual(GameText.Localized("ui.prompt.done-today", "x"), StudentActivityRefusalText.Explain(fresh, once, 0, 0));

            var never = Policy("policy.never", cost: 0, mask: 0, oncePerDay: false);
            Assert.AreEqual(GameText.Localized("ui.prompt.not-today", "x"), StudentActivityRefusalText.Explain(fresh, never, 0, 0));

            var tired = Student(energy: 0, focus: 10);
            Assert.AreEqual(GameText.Localized("ui.prompt.no-energy", "x"), StudentActivityRefusalText.Explain(tired, paid, 1, 0));
        }

        private StudentLifeProgress Student(int energy, int focus)
        {
            var calendar = ScriptableObject.CreateInstance<LifeCalendarDefinition>();
            _objects.Add(calendar);
            calendar.ConfigureForTests(2, 3);
            var progress = new StudentLifeProgress("slot", "player", energy, focus);
            progress.ConfigureCalendar(calendar);
            return progress;
        }

        private ActivityTimePolicyDefinition Policy(string id, int cost, int mask, bool oncePerDay)
        {
            var policy = ScriptableObject.CreateInstance<ActivityTimePolicyDefinition>();
            _objects.Add(policy);
            policy.ConfigureForTests(id, cost, mask, oncePerDay);
            return policy;
        }
    }
}
