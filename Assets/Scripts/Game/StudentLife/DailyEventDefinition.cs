using System;
using System.Collections.Generic;
using Rootborn.Game.Dialogue;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public static class DailyEventStates
    {
        public const string Unseen = "unseen";
        public const string Available = "available";
        public const string Deferred = "deferred";
        public const string Completed = "completed";
        public const string Declined = "declined";
        public const string Expired = "expired";
        public const string Cooldown = "cooldown";
    }

    [CreateAssetMenu(fileName = "DailyEvent_New", menuName = "Rootborn/Student Life/Daily Events/Event")]
    public sealed class DailyEventDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _descriptionKey;
        [SerializeField] private LocationDefinition _location;
        [SerializeField] private NpcDefinition _npc;
        [SerializeField] private DailyEventKindDefinition _kind;
        [SerializeField] private DailyEventAvailabilityRuleBase[] _availabilityRules = Array.Empty<DailyEventAvailabilityRuleBase>();
        [SerializeField] private DailyEventChoiceDefinition[] _choices = Array.Empty<DailyEventChoiceDefinition>();

        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? Id : _descriptionKey;
        public LocationDefinition Location => _location;
        public string LocationId => _location != null ? _location.Id : string.Empty;
        public NpcDefinition Npc => _npc;
        public DailyEventKindDefinition Kind => _kind;
        public IReadOnlyList<DailyEventAvailabilityRuleBase> AvailabilityRules => _availabilityRules;
        public IReadOnlyList<DailyEventChoiceDefinition> Choices => _choices;

        public bool HasSatisfiedAvailability(DailyEventContext context)
        {
            for (int i = 0; i < _availabilityRules.Length; i++)
            {
                var rule = _availabilityRules[i];
                if (rule != null && !rule.IsSatisfied(context, this)) return false;
            }

            return true;
        }

        public void ConfigureForTests(string id, string displayNameKey, string descriptionKey, LocationDefinition location, NpcDefinition npc, DailyEventKindDefinition kind, DailyEventAvailabilityRuleBase[] availabilityRules, DailyEventChoiceDefinition[] choices)
        {
            ConfigureForTests(id, displayNameKey);
            _descriptionKey = string.IsNullOrEmpty(descriptionKey) ? id : descriptionKey;
            _location = location;
            _npc = npc;
            _kind = kind;
            _availabilityRules = availabilityRules ?? Array.Empty<DailyEventAvailabilityRuleBase>();
            _choices = choices ?? Array.Empty<DailyEventChoiceDefinition>();
        }
    }

    public readonly struct DailyEventContext
    {
        public readonly StudentLifeProgress StudentLifeProgress;
        public readonly DailyEventProgress EventProgress;
        public readonly LocationDefinition Location;
        public readonly int Day;
        public readonly int TimeMinutes;

        public DailyEventContext(StudentLifeProgress studentLifeProgress, DailyEventProgress eventProgress, LocationDefinition location, int day, int timeMinutes)
        {
            StudentLifeProgress = studentLifeProgress;
            EventProgress = eventProgress;
            Location = location;
            Day = Mathf.Max(1, day);
            TimeMinutes = Mathf.Max(0, timeMinutes);
        }
    }

    public abstract class DailyEventAvailabilityRuleBase : ScriptableObject
    {
        public abstract bool IsSatisfied(DailyEventContext context, DailyEventDefinition dailyEvent);
    }

    public abstract class DailyEventOutcomeBase : ScriptableObject
    {
        public abstract string Apply(StudentLifeProgress progress, DailyEventDefinition dailyEvent, DailyEventChoiceDefinition choice);
    }
}
