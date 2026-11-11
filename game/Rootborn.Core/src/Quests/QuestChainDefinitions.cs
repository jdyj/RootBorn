using System;
using System.Collections.Generic;

namespace Rootborn.Core.Quests
{
    /// <summary>One step of a quest chain: objectives, conditions, rewards and where to go next.</summary>
    public sealed class QuestStepDefinition
    {
        private readonly string[] _legacyStepIds;
        private readonly QuestObjective[] _objectives;
        private readonly QuestCondition[] _completionConditions;
        private readonly QuestCondition[] _blockConditions;
        private readonly QuestReward[] _rewards;
        private readonly QuestChainTransition[] _transitions;

        /// <param name="legacyStepIds">Earlier ids of this step. Old saves that stopped on one of them resume on this step.</param>
        public QuestStepDefinition(string id, string displayNameKey, string descriptionKey, IReadOnlyList<QuestObjective> objectives,
            IReadOnlyList<QuestCondition> completionConditions = null, IReadOnlyList<QuestCondition> blockConditions = null,
            IReadOnlyList<QuestReward> rewards = null, IReadOnlyList<QuestChainTransition> transitions = null, IReadOnlyList<string> legacyStepIds = null)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            DescriptionKey = descriptionKey ?? string.Empty;
            _objectives = QuestDefinition.Copy(objectives);
            _completionConditions = QuestDefinition.Copy(completionConditions);
            _blockConditions = QuestDefinition.Copy(blockConditions);
            _rewards = QuestDefinition.Copy(rewards);
            _transitions = QuestDefinition.Copy(transitions);
            _legacyStepIds = QuestDefinition.Copy(legacyStepIds);
        }

        public string Id { get; }
        public IReadOnlyList<string> LegacyStepIds => _legacyStepIds;
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public IReadOnlyList<QuestObjective> Objectives => _objectives;
        public IReadOnlyList<QuestCondition> CompletionConditions => _completionConditions;
        public IReadOnlyList<QuestCondition> BlockConditions => _blockConditions;
        public IReadOnlyList<QuestReward> Rewards => _rewards;
        public IReadOnlyList<QuestChainTransition> Transitions => _transitions;
    }

    /// <summary>A chain of steps the player follows, with its start, block, failure and expiry conditions.</summary>
    public sealed class QuestChainDefinition
    {
        private readonly QuestStepDefinition[] _steps;
        private readonly QuestCondition[] _startConditions;
        private readonly QuestCondition[] _unblockConditions;
        private readonly QuestCondition[] _blockConditions;
        private readonly QuestCondition[] _failureConditions;
        private readonly QuestCondition[] _expirationConditions;
        private readonly QuestReward[] _completionRewards;

        /// <param name="developerOnly">Kept for automated tests. Never listed in the player's objective journal.</param>
        public QuestChainDefinition(string id, string displayNameKey, string descriptionKey, string relatedCareerInterestId, IReadOnlyList<QuestStepDefinition> steps,
            IReadOnlyList<QuestCondition> startConditions = null, IReadOnlyList<QuestCondition> unblockConditions = null, IReadOnlyList<QuestCondition> blockConditions = null,
            IReadOnlyList<QuestReward> completionRewards = null, int sortPriority = 0, IReadOnlyList<QuestCondition> failureConditions = null,
            IReadOnlyList<QuestCondition> expirationConditions = null, string startNpcId = null, bool developerOnly = false)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            DescriptionKey = descriptionKey ?? string.Empty;
            RelatedCareerInterestId = relatedCareerInterestId ?? string.Empty;
            _steps = QuestDefinition.Copy(steps);
            _startConditions = QuestDefinition.Copy(startConditions);
            _unblockConditions = QuestDefinition.Copy(unblockConditions);
            _blockConditions = QuestDefinition.Copy(blockConditions);
            _failureConditions = QuestDefinition.Copy(failureConditions);
            _expirationConditions = QuestDefinition.Copy(expirationConditions);
            _completionRewards = QuestDefinition.Copy(completionRewards);
            SortPriority = sortPriority;
            StartNpcId = startNpcId;
            DeveloperOnly = developerOnly;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string DescriptionKey { get; }
        public string RelatedCareerInterestId { get; }
        public string StartNpcId { get; }
        public IReadOnlyList<QuestStepDefinition> Steps => _steps;
        public IReadOnlyList<QuestCondition> StartConditions => _startConditions;
        public IReadOnlyList<QuestCondition> UnblockConditions => _unblockConditions;
        public IReadOnlyList<QuestCondition> BlockConditions => _blockConditions;
        public IReadOnlyList<QuestCondition> FailureConditions => _failureConditions;
        public IReadOnlyList<QuestCondition> ExpirationConditions => _expirationConditions;
        public IReadOnlyList<QuestReward> CompletionRewards => _completionRewards;
        public int SortPriority { get; }
        public bool DeveloperOnly { get; }

        /// <summary>True when talking to the NPC with this id starts the chain.</summary>
        public bool IsStartedByTalkingTo(string npcId)
            => !string.IsNullOrEmpty(StartNpcId) && !string.IsNullOrEmpty(npcId) && string.Equals(StartNpcId, npcId, StringComparison.Ordinal);
    }

    /// <summary>Chooses the step after the current one. An empty next id with true means the chain is complete.</summary>
    public abstract class QuestChainTransition
    {
        protected QuestChainTransition(string id)
        {
            Id = id ?? string.Empty;
        }

        public string Id { get; }

        public virtual string ResolveNextStepId(QuestChainDefinition chain, QuestChainProgress progress) => string.Empty;

        public virtual bool TryResolveNextStep(QuestChainDefinition chain, QuestChainProgress progress, in QuestRuntimeContext context, out string nextStepId)
        {
            nextStepId = ResolveNextStepId(chain, progress);
            return !string.IsNullOrEmpty(nextStepId);
        }
    }

    /// <summary>Goes to <see cref="TargetStepId"/> (or completes the chain) when every condition holds.</summary>
    public sealed class ConditionalQuestChainTransition : QuestChainTransition
    {
        private readonly QuestCondition[] _conditions;

        public ConditionalQuestChainTransition(string id, string targetStepId, bool completeChain, IReadOnlyList<QuestCondition> conditions = null) : base(id)
        {
            TargetStepId = targetStepId;
            CompleteChain = completeChain;
            _conditions = QuestDefinition.Copy(conditions);
        }

        public string TargetStepId { get; }
        public bool CompleteChain { get; }
        public IReadOnlyList<QuestCondition> Conditions => _conditions;

        public override bool TryResolveNextStep(QuestChainDefinition chain, QuestChainProgress progress, in QuestRuntimeContext context, out string nextStepId)
        {
            nextStepId = string.Empty;
            if (!CompleteChain && TargetStepId == null) return false;
            for (int i = 0; i < _conditions.Length; i++)
                if (_conditions[i] == null || !_conditions[i].IsSatisfied(in context)) return false;

            nextStepId = CompleteChain ? string.Empty : TargetStepId;
            return CompleteChain || !string.IsNullOrEmpty(nextStepId);
        }
    }
}
