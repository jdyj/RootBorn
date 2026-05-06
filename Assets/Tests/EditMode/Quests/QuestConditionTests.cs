using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Conditions;
using Rootborn.Game.Quests.Effects;
using Rootborn.Game.Story;
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
