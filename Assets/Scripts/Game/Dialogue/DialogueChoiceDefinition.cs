using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    public enum DialogueQuestAction
    {
        None,
        AcceptQuest,
        ClaimReward,
        Close
    }

    public readonly struct DialogueChoiceContext
    {
        public readonly QuestLog QuestLog;
        public readonly RewardRuntimeContext RewardContext;

        public DialogueChoiceContext(QuestLog questLog, RewardRuntimeContext rewardContext)
        {
            QuestLog = questLog;
            RewardContext = rewardContext;
        }
    }

    [CreateAssetMenu(fileName = "DialogueChoice_New", menuName = "Rootborn/Dialogue/Dialogue Choice")]
    public sealed class DialogueChoiceDefinition : ScriptableObject
    {
        [SerializeField] private string _labelKey;
        [SerializeField] private DialogueQuestAction _questAction;
        [SerializeField] private QuestDefinition _quest;

        public string LabelKey => _labelKey;
        public DialogueQuestAction QuestAction => _questAction;
        public QuestDefinition Quest => _quest;

        public bool TryExecute(in DialogueChoiceContext context)
        {
            if (_questAction == DialogueQuestAction.AcceptQuest)
            {
                return context.QuestLog != null && context.QuestLog.Accept(_quest);
            }

            if (_questAction == DialogueQuestAction.ClaimReward)
            {
                var rewardContext = context.RewardContext;
                return context.QuestLog != null && context.QuestLog.ClaimReward(_quest, in rewardContext);
            }

            return _questAction == DialogueQuestAction.Close || _questAction == DialogueQuestAction.None;
        }
    }
}
