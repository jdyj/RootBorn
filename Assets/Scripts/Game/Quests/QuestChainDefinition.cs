using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Quests
{
    [CreateAssetMenu(fileName = "QuestChain_New", menuName = "Rootborn/Quests/Chains/Chain")]
    public sealed class QuestChainDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _descriptionKey;
        [SerializeField] private string _relatedCareerInterestId;
        [SerializeField] private QuestStepDefinition[] _steps = Array.Empty<QuestStepDefinition>();
        [SerializeField] private QuestConditionBase[] _startConditions = Array.Empty<QuestConditionBase>();
        [SerializeField] private QuestConditionBase[] _unblockConditions = Array.Empty<QuestConditionBase>();
        [SerializeField] private QuestConditionBase[] _blockConditions = Array.Empty<QuestConditionBase>();
        [SerializeField] private QuestConditionBase[] _failureConditions = Array.Empty<QuestConditionBase>();
        [SerializeField] private QuestConditionBase[] _expirationConditions = Array.Empty<QuestConditionBase>();
        [SerializeField] private QuestRewardBase[] _completionRewards = Array.Empty<QuestRewardBase>();
        [SerializeField] private int _sortPriority;

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public string DescriptionKey => _descriptionKey;
        public string RelatedCareerInterestId => _relatedCareerInterestId;
        public QuestStepDefinition[] Steps => _steps;
        public QuestConditionBase[] StartConditions => _startConditions;
        public QuestConditionBase[] UnblockConditions => _unblockConditions;
        public QuestConditionBase[] BlockConditions => _blockConditions;
        public QuestConditionBase[] FailureConditions => _failureConditions;
        public QuestConditionBase[] ExpirationConditions => _expirationConditions;
        public QuestRewardBase[] CompletionRewards => _completionRewards;
        public int SortPriority => _sortPriority;

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            string descriptionKey,
            string relatedCareerInterestId,
            ScriptableObject[] steps,
            QuestConditionBase[] startConditions,
            QuestConditionBase[] unblockConditions,
            QuestConditionBase[] blockConditions,
            QuestRewardBase[] completionRewards,
            int sortPriority,
            QuestConditionBase[] failureConditions = null,
            QuestConditionBase[] expirationConditions = null)
        {
            _id = id;
            _displayNameKey = displayNameKey;
            _descriptionKey = descriptionKey;
            _relatedCareerInterestId = relatedCareerInterestId;
            _steps = FilterSteps(steps);
            _startConditions = startConditions ?? Array.Empty<QuestConditionBase>();
            _unblockConditions = unblockConditions ?? Array.Empty<QuestConditionBase>();
            _blockConditions = blockConditions ?? Array.Empty<QuestConditionBase>();
            _failureConditions = failureConditions ?? Array.Empty<QuestConditionBase>();
            _expirationConditions = expirationConditions ?? Array.Empty<QuestConditionBase>();
            _completionRewards = completionRewards ?? Array.Empty<QuestRewardBase>();
            _sortPriority = sortPriority;
        }

        private static QuestStepDefinition[] FilterSteps(ScriptableObject[] steps)
        {
            if (steps == null || steps.Length == 0) return Array.Empty<QuestStepDefinition>();
            var results = new List<QuestStepDefinition>(steps.Length);
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] is QuestStepDefinition step) results.Add(step);
            }

            return results.ToArray();
        }
    }
}
