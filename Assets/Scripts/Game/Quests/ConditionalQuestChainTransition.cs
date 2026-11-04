using System;
using UnityEngine;

namespace Rootborn.Game.Quests
{
    [CreateAssetMenu(fileName = "QuestTransition_Conditional", menuName = "Rootborn/Quests/Chains/Conditional Transition")]
    public sealed class ConditionalQuestChainTransition : QuestChainTransitionBase
    {
        [SerializeField] private QuestStepDefinition _targetStep;
        [SerializeField] private bool _completeChain;
        [SerializeField] private QuestConditionBase[] _conditions = Array.Empty<QuestConditionBase>();

        public override bool TryResolveNextStep(QuestChainDefinition chain, QuestChainProgress progress, in QuestRuntimeContext context, out string nextStepId)
        {
            nextStepId = string.Empty;
            if (!_completeChain && _targetStep == null) return false;
            if (_conditions != null)
            {
                for (int i = 0; i < _conditions.Length; i++)
                {
                    if (_conditions[i] == null || !_conditions[i].IsSatisfied(in context)) return false;
                }
            }

            nextStepId = _completeChain ? string.Empty : _targetStep.Id;
            return _completeChain || !string.IsNullOrEmpty(nextStepId);
        }
    }
}
