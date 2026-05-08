using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.ModernSociety;
using Rootborn.Game.Story;
using UnityEngine;

namespace Rootborn.Tests.EditMode.ModernSociety
{
    public sealed class ModernSocietyLoopTests
    {
        [Test]
        public void MOD_SCH_002_StudyAndErrandInSameDay_SetsBalancedRoutineFlag()
        {
            var balancedRoutine = ScriptableObject.CreateInstance<StoryFlagDefinition>();
            var study = MakeActivity(ModernActivityKind.Study, energyCost: 1, knowledgeDelta: 1);
            var errand = MakeActivity(ModernActivityKind.Errand, energyCost: 1, moneyDelta: 1);
            var rule = ScriptableObject.CreateInstance<ModernRoutineFlagRule>();
            SetField(rule, "_requiredActivities", new[] { study, errand });
            SetField(rule, "_flag", balancedRoutine);

            var flags = new StoryFlagSet();
            var stats = new ModernSocietyStats(energy: 3, money: 0, anxiety: 0, knowledge: 0, relationships: 0);
            var day = new ModernSocietyDay(stats, flags, new[] { rule });

            Assert.IsTrue(day.TryPerform(study));
            Assert.IsTrue(day.TryPerform(errand));
            day.EndDay();

            Assert.IsTrue(flags.IsSet(balancedRoutine));
            Assert.AreEqual(1, day.Stats.Knowledge);
            Assert.AreEqual(1, day.Stats.Money);
            Assert.AreEqual(1, day.Stats.Energy);
        }

        private static ModernActivityDefinition MakeActivity(
            ModernActivityKind kind,
            int energyCost = 0,
            int moneyDelta = 0,
            int anxietyDelta = 0,
            int knowledgeDelta = 0,
            int relationshipsDelta = 0)
        {
            var activity = ScriptableObject.CreateInstance<ModernActivityDefinition>();
            SetField(activity, "_kind", kind);
            SetField(activity, "_energyCost", energyCost);
            SetField(activity, "_moneyDelta", moneyDelta);
            SetField(activity, "_anxietyDelta", anxietyDelta);
            SetField(activity, "_knowledgeDelta", knowledgeDelta);
            SetField(activity, "_relationshipsDelta", relationshipsDelta);
            return activity;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(fieldName);
        }
    }
}