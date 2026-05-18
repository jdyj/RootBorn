using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Conditions;
using Rootborn.Game.Quests.Effects;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestConditionTests
    {
        [Test]
        public void QUEST_010_CompletionEffect_SetsStoryFlag()
        {
            var flag = ScriptableObject.CreateInstance<StoryFlagDefinition>();
            var flags = new StoryFlagSet();
            var effect = ScriptableObject.CreateInstance<SetStoryFlagCompletionEffect>();
            SetField(effect, "_flag", flag);
            var context = new RewardRuntimeContext(null, null, null, flags);

            Assert.IsTrue(effect.CanApply(in context));
            effect.Apply(in context);

            Assert.IsTrue(flags.IsSet(flag));
        }

        [Test]
        public void QUEST_011_StoryFlagCondition_BlocksWhenMissing()
        {
            var flag = ScriptableObject.CreateInstance<StoryFlagDefinition>();
            var flags = new StoryFlagSet();
            var condition = ScriptableObject.CreateInstance<StoryFlagCondition>();
            SetField(condition, "_requiredFlag", flag);

            Assert.IsFalse(condition.IsSatisfied(new QuestRuntimeContext(null, null, null, flags)));
            flags.Set(flag);
            Assert.IsTrue(condition.IsSatisfied(new QuestRuntimeContext(null, null, null, flags)));
        }

        [Test]
        public void QUEST_012_LocationActivityCompletedCondition_UsesStudentProgressActivityLog()
        {
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            var condition = ScriptableObject.CreateInstance<LocationActivityCompletedCondition>();
            var progress = new StudentLifeProgress("slot", "player", 100, 100, 0, 8 * 60);
            try
            {
                activity.ConfigureForTests("activity.library.desk", "activity.library.desk", null, LocationGrowthRoute.SelfStudy, 0, 0, 0, 0, null, null);
                condition.ConfigureForTests(activity, "missing.library.access", "Visit the library desk to reopen this chain.");

                Assert.IsFalse(condition.IsSatisfied(new QuestRuntimeContext(null, null, null, null, progress)), "QUEST_012 failed: condition should block until the activity is logged.");
                progress.RecordActivityCompleted(activity.Id);
                Assert.IsTrue(condition.IsSatisfied(new QuestRuntimeContext(null, null, null, null, progress)), "QUEST_012 failed: condition should pass after the activity is logged.");
                Assert.AreEqual("missing.library.access", condition.BlockedReasonId);
                StringAssert.Contains("library desk", condition.BlockedSummary);
            }
            finally
            {
                Object.DestroyImmediate(condition);
                Object.DestroyImmediate(activity);
            }
        }

        private static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(name);
        }
    }
}
