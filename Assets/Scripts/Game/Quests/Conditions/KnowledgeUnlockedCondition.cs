using Rootborn.Game.Knowledge;
using UnityEngine;

namespace Rootborn.Game.Quests.Conditions
{
    [CreateAssetMenu(fileName = "Condition_KnowledgeUnlocked", menuName = "Rootborn/Quests/Conditions/Knowledge Unlocked")]
    public sealed class KnowledgeUnlockedCondition : QuestConditionBase
    {
        [SerializeField] private KnowledgeNode _requiredKnowledge;

        public override bool IsSatisfied(in QuestRuntimeContext context)
        {
            return context.KnowledgeProgress != null && context.KnowledgeProgress.IsUnlocked(_requiredKnowledge);
        }
    }
}
