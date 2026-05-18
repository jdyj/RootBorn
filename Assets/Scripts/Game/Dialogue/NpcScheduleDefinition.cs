using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [Serializable]
    public sealed class NpcScheduleEntry
    {
        [SerializeField] private LocationDefinition _location;
        [SerializeField] private DialogueDefinition _dialogue;
        [SerializeField] private string _dialogueKey;
        [SerializeField] private string _interactionHintKey;
        [SerializeField] private string _eventNoticeKey;
        [SerializeField] private int _priority;
        [SerializeField] private NpcScheduleRuleBase[] _rules = Array.Empty<NpcScheduleRuleBase>();

        public NpcScheduleEntry(LocationDefinition location, string dialogueKey, string interactionHintKey, string eventNoticeKey, int priority, NpcScheduleRuleBase[] rules)
        {
            _location = location;
            _dialogueKey = string.IsNullOrEmpty(dialogueKey) ? string.Empty : dialogueKey;
            _interactionHintKey = string.IsNullOrEmpty(interactionHintKey) ? string.Empty : interactionHintKey;
            _eventNoticeKey = string.IsNullOrEmpty(eventNoticeKey) ? string.Empty : eventNoticeKey;
            _priority = priority;
            _rules = rules ?? Array.Empty<NpcScheduleRuleBase>();
        }

        public LocationDefinition Location => _location;
        public DialogueDefinition Dialogue => _dialogue;
        public string DialogueKey => _dialogueKey;
        public string InteractionHintKey => _interactionHintKey;
        public string EventNoticeKey => _eventNoticeKey;
        public int Priority => _priority;
        public IReadOnlyList<NpcScheduleRuleBase> Rules => _rules;

        public bool IsMatch(NpcScheduleContext context)
        {
            for (int i = 0; i < _rules.Length; i++)
            {
                var rule = _rules[i];
                if (rule != null && !rule.IsSatisfied(context)) return false;
            }

            return true;
        }
    }

    [CreateAssetMenu(fileName = "NpcSchedule_New", menuName = "Rootborn/Dialogue/NPC Schedule")]
    public sealed class NpcScheduleDefinition : ScriptableObject
    {
        [SerializeField] private NpcDefinition _npc;
        [SerializeField] private LocationDefinition _fallbackLocation;
        [SerializeField] private NpcScheduleEntry[] _entries = Array.Empty<NpcScheduleEntry>();

        public NpcDefinition Npc => _npc;
        public LocationDefinition FallbackLocation => _fallbackLocation != null ? _fallbackLocation : (_npc != null ? _npc.HomeLocation : null);
        public IReadOnlyList<NpcScheduleEntry> Entries => _entries;

        public void ConfigureForTests(NpcDefinition npc, LocationDefinition fallbackLocation, NpcScheduleEntry[] entries)
        {
            _npc = npc;
            _fallbackLocation = fallbackLocation;
            _entries = entries ?? Array.Empty<NpcScheduleEntry>();
        }
    }

    public readonly struct NpcScheduleContext
    {
        public readonly int Day;
        public readonly int WeekdayIndex;
        public readonly string TimeSlotId;
        public readonly StudentLifeProgress Progress;

        public NpcScheduleContext(int day, int weekdayIndex, string timeSlotId, StudentLifeProgress progress)
        {
            Day = Mathf.Max(1, day);
            WeekdayIndex = Mathf.Clamp(weekdayIndex, 0, 6);
            TimeSlotId = string.IsNullOrEmpty(timeSlotId) ? string.Empty : timeSlotId;
            Progress = progress;
        }
    }

    public abstract class NpcScheduleRuleBase : ScriptableObject
    {
        public abstract bool IsSatisfied(NpcScheduleContext context);
    }
}
