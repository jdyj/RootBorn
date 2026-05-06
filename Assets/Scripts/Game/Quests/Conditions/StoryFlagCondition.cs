using Rootborn.Game.Story;
using UnityEngine;

namespace Rootborn.Game.Quests.Conditions
{
    [CreateAssetMenu(fileName = "Condition_StoryFlag", menuName = "Rootborn/Quests/Conditions/Story Flag")]
    public sealed class StoryFlagCondition : QuestConditionBase
    {
        [SerializeField] private StoryFlagDefinition _requiredFlag;

        public override bool IsSatisfied(in QuestRuntimeContext context)
        {
            return context.StoryFlags != null && context.StoryFlags.IsSet(_requiredFlag);
        }
    }
}
