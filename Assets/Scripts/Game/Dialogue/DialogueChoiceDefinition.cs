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

        public bool IsAvailable(in DialogueChoiceContext context)
        {
            if (_questAction == DialogueQuestAction.AcceptQuest)
            {
                return context.QuestLog != null
                    && _quest != null
                    && context.QuestLog.GetState(_quest) == QuestState.NotStarted;
            }

            if (_questAction == DialogueQuestAction.ClaimReward)
            {
                var rewardContext = context.RewardContext;
                return context.QuestLog != null
                    && _quest != null
                    && context.QuestLog.CanClaimReward(_quest, in rewardContext);
            }

            return true;
        }

        public bool TryExecute(in DialogueChoiceContext context)
        {
            if (!IsAvailable(in context))
            {
                return false;
            }

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
