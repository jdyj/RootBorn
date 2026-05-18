using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationOutcome_FollowUpQuest", menuName = "Rootborn/Student Life/Exploration Choices/Outcomes/Follow-up Quest")]
    public sealed class ExplorationFollowUpQuestOutcome : ExplorationOutcomeBase
    {
        [SerializeField] private QuestDefinition _quest;

        public override string OutcomeId => _quest != null ? "quest:" + _quest.Id : string.Empty;

        public override bool CanApply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice)
        {
            return _quest != null;
        }

        public override bool Apply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice, ExplorationOutcomeCollector collector)
        {
            if (_quest == null || string.IsNullOrEmpty(_quest.Id)) return false;
            bool accepted = false;
            if (context.QuestLog != null)
            {
                context.QuestLog.AddQuest(_quest);
                accepted = context.QuestLog.Accept(_quest);
            }

            collector?.AddFollowUpQuest(_quest.Id);
            return context.QuestLog == null || accepted;
        }

        public void ConfigureForTests(QuestDefinition quest)
        {
            _quest = quest;
        }
    }
}
