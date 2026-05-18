using System;
using System.Collections.Generic;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    public enum ClueInterpretationSourceKind { NpcDialogue, LocationInvestigation, ObjectInteraction, ActivityReview, EncyclopediaReview, BoardRecord, MapCheck }
    public enum ClueInterpretationSummarySurface { ClueDetail, NpcDialogue, LocationPanel, ObjectPanel, Encyclopedia, Journal }
    public enum ClueInterpretationPolicyKind { Exclusive, NonExclusive, Sequential, Repeatable }
    public enum ClueInterpretationResultKind { Completed, InvalidRequest, ConditionFailed, AlreadyCompleted, LockedByPolicy, CooldownActive }

    public abstract class ClueInterpretationConditionBase : ScriptableObject
    {
        [SerializeField] private string _lockedReasonKey = "condition.missing";
        public string LockedReasonKey => string.IsNullOrEmpty(_lockedReasonKey) ? "condition.missing" : _lockedReasonKey;
        public abstract bool Evaluate(in ClueInterpretationContext context, ClueInterpretationDefinition interpretation);
    }

    public abstract class ClueInterpretationOutcomeBase : ScriptableObject
    {
        public abstract string OutcomeId { get; }
        public abstract bool Apply(in ClueInterpretationContext context, ClueInterpretationDefinition interpretation, List<string> encyclopediaEntryIds, List<string> careerHintIds, List<string> followUpQuestIds, List<string> worldStateIds, List<string> relationshipIds, List<string> statusIds);
    }

    public readonly struct ClueInterpretationContext
    {
        public readonly WorldStateProgress WorldStateProgress;
        public readonly DiscoveryClueProgress ClueProgress;
        public readonly ClueInterpretationProgress InterpretationProgress;
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly QuestLog QuestLog;
        public readonly EncyclopediaProgress EncyclopediaProgress;
        public readonly int CurrentDay;

        public ClueInterpretationContext(WorldStateProgress worldStateProgress, DiscoveryClueProgress clueProgress, ClueInterpretationProgress interpretationProgress, StudentLifeProgress studentLifeProgress, QuestLog questLog, EncyclopediaProgress encyclopediaProgress, int currentDay)
        {
            WorldStateProgress = worldStateProgress;
            ClueProgress = clueProgress;
            InterpretationProgress = interpretationProgress;
            StudentLifeProgress = studentLifeProgress;
            QuestLog = questLog;
            EncyclopediaProgress = encyclopediaProgress;
            CurrentDay = Mathf.Max(1, currentDay);
        }
    }

    public readonly struct ClueInterpretationResult
    {
        public readonly ClueInterpretationResultKind Kind;
        public readonly string InterpretationId;
        public readonly string Reason;
        public readonly string[] OutcomeIds;

        public ClueInterpretationResult(ClueInterpretationResultKind kind, string interpretationId, string reason, string[] outcomeIds)
        {
            Kind = kind;
            InterpretationId = string.IsNullOrEmpty(interpretationId) ? string.Empty : interpretationId;
            Reason = string.IsNullOrEmpty(reason) ? string.Empty : reason;
            OutcomeIds = outcomeIds ?? Array.Empty<string>();
        }
    }

    public sealed class ClueInterpretationRunner
    {
        public bool TryComplete(ClueInterpretationDefinition interpretation, in ClueInterpretationContext context, out ClueInterpretationResult result)
        {
            string id = interpretation != null ? interpretation.Id : string.Empty;
            result = new ClueInterpretationResult(ClueInterpretationResultKind.InvalidRequest, id, "missing interpretation", Array.Empty<string>());
            if (interpretation == null || context.InterpretationProgress == null) return false;
            if (!interpretation.ConditionsSatisfied(context, out string conditionReason))
            {
                result = new ClueInterpretationResult(ClueInterpretationResultKind.ConditionFailed, interpretation.Id, conditionReason, Array.Empty<string>());
                return false;
            }

            if (!context.InterpretationProgress.CanComplete(interpretation, context.CurrentDay, out string policyReason, out var policyResult))
            {
                result = new ClueInterpretationResult(policyResult, interpretation.Id, policyReason, Array.Empty<string>());
                return false;
            }

            var entries = new List<string>();
            var careers = new List<string>();
            var quests = new List<string>();
            var worldStates = new List<string>();
            var relationships = new List<string>();
            var statuses = new List<string>();
            var outcomes = new List<string>();
            for (int i = 0; i < interpretation.Outcomes.Count; i++)
            {
                var outcome = interpretation.Outcomes[i];
                if (outcome != null && outcome.Apply(context, interpretation, entries, careers, quests, worldStates, relationships, statuses)) AddUnique(outcomes, outcome.OutcomeId);
            }

            context.InterpretationProgress.MarkCompleted(interpretation, context.CurrentDay, outcomes.ToArray(), entries.ToArray(), careers.ToArray(), quests.ToArray(), worldStates.ToArray(), relationships.ToArray(), statuses.ToArray());
            result = new ClueInterpretationResult(ClueInterpretationResultKind.Completed, interpretation.Id, string.Empty, outcomes.ToArray());
            return true;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && values != null && !values.Contains(value)) values.Add(value);
        }
    }
}
