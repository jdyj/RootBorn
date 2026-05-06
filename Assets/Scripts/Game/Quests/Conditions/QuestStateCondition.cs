using UnityEngine;

namespace Rootborn.Game.Quests.Conditions
{
    [CreateAssetMenu(fileName = "Condition_QuestState", menuName = "Rootborn/Quests/Conditions/Quest State")]
    public sealed class QuestStateCondition : QuestConditionBase
    {
        [SerializeField] private QuestDefinition _quest;
        [SerializeField] private QuestState _requiredState = QuestState.RewardClaimed;

        public override bool IsSatisfied(in QuestRuntimeContext context)
        {
            return context.QuestLog != null
                && _quest != null
                && context.QuestLog.GetState(_quest) == _requiredState;
        }
    }
}
