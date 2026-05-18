using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Quests
{
    [CreateAssetMenu(fileName = "QuestStep_New", menuName = "Rootborn/Quests/Chains/Step")]
    public sealed class QuestStepDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _descriptionKey;
        [SerializeField] private QuestObjectiveBase[] _objectives = Array.Empty<QuestObjectiveBase>();
        [SerializeField] private QuestConditionBase[] _completionConditions = Array.Empty<QuestConditionBase>();
        [SerializeField] private QuestConditionBase[] _blockConditions = Array.Empty<QuestConditionBase>();
        [SerializeField] private QuestRewardBase[] _rewards = Array.Empty<QuestRewardBase>();
        [SerializeField] private QuestChainTransitionBase[] _transitions = Array.Empty<QuestChainTransitionBase>();

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public string DescriptionKey => _descriptionKey;
        public QuestObjectiveBase[] Objectives => _objectives;
        public QuestConditionBase[] CompletionConditions => _completionConditions;
        public QuestConditionBase[] BlockConditions => _blockConditions;
        public QuestRewardBase[] Rewards => _rewards;
        public QuestChainTransitionBase[] Transitions => _transitions;

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            string descriptionKey,
            QuestObjectiveBase[] objectives,
            QuestConditionBase[] completionConditions,
            QuestConditionBase[] blockConditions,
            QuestRewardBase[] rewards,
            ScriptableObject[] transitions)
        {
            _id = id;
            _displayNameKey = displayNameKey;
            _descriptionKey = descriptionKey;
            _objectives = objectives ?? Array.Empty<QuestObjectiveBase>();
            _completionConditions = completionConditions ?? Array.Empty<QuestConditionBase>();
            _blockConditions = blockConditions ?? Array.Empty<QuestConditionBase>();
            _rewards = rewards ?? Array.Empty<QuestRewardBase>();
            _transitions = FilterTransitions(transitions);
        }

        private static QuestChainTransitionBase[] FilterTransitions(ScriptableObject[] transitions)
        {
            if (transitions == null || transitions.Length == 0) return Array.Empty<QuestChainTransitionBase>();
            var results = new List<QuestChainTransitionBase>(transitions.Length);
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i] is QuestChainTransitionBase transition) results.Add(transition);
            }

            return results.ToArray();
        }
    }
}
