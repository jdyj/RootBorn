using System;
using System.Collections.Generic;

namespace Rootborn.Game.StudentLife
{
    public enum ExplorationChoiceResultKind
    {
        Applied,
        InvalidRequest,
        ConditionFailed,
        DuplicateRequest,
        CooldownActive,
        InsufficientResources
    }

    public sealed class ExplorationOutcomeCollector
    {
        private readonly List<string> _outcomeIds = new List<string>();
        private readonly List<string> _itemGrantIds = new List<string>();
        private readonly List<string> _itemSpendIds = new List<string>();
        private readonly List<string> _clueIds = new List<string>();
        private readonly List<string> _encyclopediaEntryIds = new List<string>();
        private readonly List<string> _careerHintIds = new List<string>();
        private readonly List<string> _followUpQuestIds = new List<string>();
        private readonly List<string> _fatigueStatusIds = new List<string>();

        public string[] OutcomeIds => _outcomeIds.ToArray();
        public string[] ItemGrantIds => _itemGrantIds.ToArray();
        public string[] ItemSpendIds => _itemSpendIds.ToArray();
        public string[] ClueIds => _clueIds.ToArray();
        public string[] EncyclopediaEntryIds => _encyclopediaEntryIds.ToArray();
        public string[] CareerHintIds => _careerHintIds.ToArray();
        public string[] FollowUpQuestIds => _followUpQuestIds.ToArray();
        public string[] FatigueStatusIds => _fatigueStatusIds.ToArray();

        public void AddOutcome(string id) => AddUnique(_outcomeIds, id);
        public void AddItemGrant(string id) => AddUnique(_itemGrantIds, id);
        public void AddItemSpend(string id) => AddUnique(_itemSpendIds, id);
        public void AddClue(string id) => AddUnique(_clueIds, id);
        public void AddEncyclopediaEntry(string id) => AddUnique(_encyclopediaEntryIds, id);
        public void AddCareerHint(string id) => AddUnique(_careerHintIds, id);
        public void AddFollowUpQuest(string id) => AddUnique(_followUpQuestIds, id);
        public void AddFatigueStatus(string id) => AddUnique(_fatigueStatusIds, id);

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value)) values.Add(value);
        }
    }

    public readonly struct ExplorationChoiceResult
    {
        public readonly ExplorationChoiceResultKind Kind;
        public readonly string InteractionId;
        public readonly string ChoiceId;
        public readonly string LockedReasonKey;
        public readonly string[] OutcomeIds;
        public readonly string[] ItemGrantIds;
        public readonly string[] ItemSpendIds;
        public readonly string[] ClueIds;
        public readonly string[] EncyclopediaEntryIds;
        public readonly string[] CareerHintIds;
        public readonly string[] FollowUpQuestIds;
        public readonly string[] FatigueStatusIds;

        public ExplorationChoiceResult(ExplorationChoiceResultKind kind, string interactionId, string choiceId, string lockedReasonKey, ExplorationOutcomeCollector collector)
        {
            Kind = kind;
            InteractionId = string.IsNullOrEmpty(interactionId) ? string.Empty : interactionId;
            ChoiceId = string.IsNullOrEmpty(choiceId) ? string.Empty : choiceId;
            LockedReasonKey = string.IsNullOrEmpty(lockedReasonKey) ? string.Empty : lockedReasonKey;
            OutcomeIds = collector != null ? collector.OutcomeIds : Array.Empty<string>();
            ItemGrantIds = collector != null ? collector.ItemGrantIds : Array.Empty<string>();
            ItemSpendIds = collector != null ? collector.ItemSpendIds : Array.Empty<string>();
            ClueIds = collector != null ? collector.ClueIds : Array.Empty<string>();
            EncyclopediaEntryIds = collector != null ? collector.EncyclopediaEntryIds : Array.Empty<string>();
            CareerHintIds = collector != null ? collector.CareerHintIds : Array.Empty<string>();
            FollowUpQuestIds = collector != null ? collector.FollowUpQuestIds : Array.Empty<string>();
            FatigueStatusIds = collector != null ? collector.FatigueStatusIds : Array.Empty<string>();
        }
    }

    public sealed class ExplorationInteractionRunner
    {
        public bool TryChoose(ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice, in ExplorationInteractionContext context, out ExplorationChoiceResult result)
        {
            string interactionId = interaction != null ? interaction.Id : string.Empty;
            string choiceId = choice != null ? choice.Id : string.Empty;
            result = new ExplorationChoiceResult(ExplorationChoiceResultKind.InvalidRequest, interactionId, choiceId, "exploration.invalid", null);
            if (interaction == null || choice == null || context.ExplorationProgress == null || context.StudentProgress == null)
            {
                return false;
            }

            if (!interaction.CanAttempt(context.ExplorationProgress, context.CurrentDay, out string interactionReason))
            {
                result = new ExplorationChoiceResult(interactionReason == "exploration.cooldown" ? ExplorationChoiceResultKind.CooldownActive : ExplorationChoiceResultKind.DuplicateRequest, interaction.Id, choice.Id, interactionReason, null);
                return false;
            }

            var record = context.ExplorationProgress.GetRecord(interaction.Id);
            if (!choice.Repeatable && record.IsRewardClaimed(choice.Id))
            {
                result = new ExplorationChoiceResult(ExplorationChoiceResultKind.DuplicateRequest, interaction.Id, choice.Id, "exploration.choice.completed", null);
                return false;
            }

            if (!choice.ConditionsSatisfied(context, out string reason))
            {
                result = new ExplorationChoiceResult(ExplorationChoiceResultKind.ConditionFailed, interaction.Id, choice.Id, reason, null);
                return false;
            }

            var risk = choice.RiskPolicy != null ? choice.RiskPolicy : interaction.DefaultRiskPolicy;
            if (risk != null && !risk.CanApply(context))
            {
                result = new ExplorationChoiceResult(ExplorationChoiceResultKind.InsufficientResources, interaction.Id, choice.Id, "exploration.resources", null);
                return false;
            }

            for (int i = 0; i < choice.Outcomes.Count; i++)
            {
                var outcome = choice.Outcomes[i];
                if (outcome != null && !outcome.CanApply(context, interaction, choice))
                {
                    result = new ExplorationChoiceResult(ExplorationChoiceResultKind.InsufficientResources, interaction.Id, choice.Id, "exploration.outcome.blocked", null);
                    return false;
                }
            }

            var collector = new ExplorationOutcomeCollector();
            risk?.Apply(context, collector);
            for (int i = 0; i < choice.Outcomes.Count; i++)
            {
                var outcome = choice.Outcomes[i];
                if (outcome != null && outcome.Apply(context, interaction, choice, collector)) collector.AddOutcome(outcome.OutcomeId);
            }

            context.ExplorationProgress.MarkCompleted(interaction.Id, choice.Id, context.CurrentDay, context.CurrentTimeMinutes, collector);
            context.StudentProgress.RecordActivityCompleted(interaction.Id, collector.OutcomeIds);
            result = new ExplorationChoiceResult(ExplorationChoiceResultKind.Applied, interaction.Id, choice.Id, string.Empty, collector);
            return true;
        }
    }
}
