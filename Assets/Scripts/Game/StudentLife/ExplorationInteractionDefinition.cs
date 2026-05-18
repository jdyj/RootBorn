using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationInteraction_New", menuName = "Rootborn/Student Life/Exploration Choices/Interaction")]
    public sealed class ExplorationInteractionDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _descriptionKey;
        [SerializeField] private Vector2 _worldPosition;
        [SerializeField] private float _interactionRadius = 1f;
        [SerializeField] private bool _repeatable;
        [SerializeField] private int _cooldownDays;
        [SerializeField] private ExplorationRiskPolicyDefinition _defaultRiskPolicy;
        [SerializeField] private ExplorationChoiceDefinition[] _choices = Array.Empty<ExplorationChoiceDefinition>();

        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public Vector2 WorldPosition => _worldPosition;
        public float InteractionRadius => Mathf.Max(0.1f, _interactionRadius);
        public bool Repeatable => _repeatable;
        public int CooldownDays => Mathf.Max(0, _cooldownDays);
        public ExplorationRiskPolicyDefinition DefaultRiskPolicy => _defaultRiskPolicy;
        public IReadOnlyList<ExplorationChoiceDefinition> Choices => _choices;

        public bool CanAttempt(ExplorationProgress progress, int currentDay, out string reason)
        {
            reason = string.Empty;
            if (progress == null) return true;
            var record = progress.GetRecord(Id);
            if (!_repeatable && record.Completed)
            {
                reason = "exploration.completed";
                return false;
            }

            int cooldown = Mathf.Max(0, _cooldownDays);
            if (cooldown > 0 && record.LastCompletedDay > 0 && Mathf.Max(1, currentDay) < record.LastCompletedDay + cooldown)
            {
                reason = "exploration.cooldown";
                return false;
            }

            return true;
        }

        public void ConfigureForTests(string id, string displayNameKey, string descriptionKey, Vector2 worldPosition, float interactionRadius, bool repeatable, int cooldownDays, ExplorationRiskPolicyDefinition defaultRiskPolicy, ExplorationChoiceDefinition[] choices)
        {
            ConfigureForTests(id, displayNameKey);
            _descriptionKey = descriptionKey;
            _worldPosition = worldPosition;
            _interactionRadius = interactionRadius;
            _repeatable = repeatable;
            _cooldownDays = cooldownDays;
            _defaultRiskPolicy = defaultRiskPolicy;
            _choices = choices ?? Array.Empty<ExplorationChoiceDefinition>();
        }
    }

    public abstract class ExplorationConditionBase : ScriptableObject
    {
        [SerializeField] private string _lockedReasonKey = "condition.missing";
        public string LockedReasonKey => string.IsNullOrEmpty(_lockedReasonKey) ? "condition.missing" : _lockedReasonKey;
        public abstract bool Evaluate(in ExplorationInteractionContext context, ExplorationChoiceDefinition choice);

        protected void SetLockedReasonForTests(string lockedReasonKey)
        {
            _lockedReasonKey = string.IsNullOrEmpty(lockedReasonKey) ? "condition.missing" : lockedReasonKey;
        }
    }

    public abstract class ExplorationOutcomeBase : ScriptableObject
    {
        public abstract string OutcomeId { get; }
        public abstract bool CanApply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice);
        public abstract bool Apply(in ExplorationInteractionContext context, ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition choice, ExplorationOutcomeCollector collector);
    }

    public readonly struct ExplorationInteractionContext
    {
        public readonly ExplorationProgress ExplorationProgress;
        public readonly StudentLifeProgress StudentProgress;
        public readonly Inventory Inventory;
        public readonly QuestLog QuestLog;
        public readonly DiscoveryClueProgress DiscoveryClueProgress;
        public readonly EncyclopediaProgress EncyclopediaProgress;
        public readonly int CurrentDay;
        public readonly int CurrentTimeMinutes;

        public ExplorationInteractionContext(ExplorationProgress explorationProgress, StudentLifeProgress studentProgress, Inventory inventory, QuestLog questLog, DiscoveryClueProgress discoveryClueProgress, EncyclopediaProgress encyclopediaProgress, int currentDay, int currentTimeMinutes)
        {
            ExplorationProgress = explorationProgress;
            StudentProgress = studentProgress;
            Inventory = inventory;
            QuestLog = questLog;
            DiscoveryClueProgress = discoveryClueProgress;
            EncyclopediaProgress = encyclopediaProgress;
            CurrentDay = Mathf.Max(1, currentDay);
            CurrentTimeMinutes = Mathf.Max(0, currentTimeMinutes);
        }
    }
}
