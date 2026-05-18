using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public sealed class LocationStateQuestStateCondition : LocationStateConditionBase
    {
        [SerializeField] private QuestDefinition _quest;
        [SerializeField] private QuestState _requiredState = QuestState.Completed;
        public override bool IsSatisfied(in LocationStateContext context) => context.QuestLog != null && _quest != null && context.QuestLog.GetState(_quest) == _requiredState;
        public void ConfigureForTests(QuestDefinition quest, QuestState requiredState) { _quest = quest; _requiredState = requiredState; }
    }
}
