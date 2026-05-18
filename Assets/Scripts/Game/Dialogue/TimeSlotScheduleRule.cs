using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [CreateAssetMenu(fileName = "TimeSlotScheduleRule_New", menuName = "Rootborn/Dialogue/Schedule Rules/Time Slot")]
    public sealed class TimeSlotScheduleRule : NpcScheduleRuleBase
    {
        [SerializeField] private TimeSlotDefinition[] _timeSlots = Array.Empty<TimeSlotDefinition>();

        public IReadOnlyList<TimeSlotDefinition> TimeSlots => _timeSlots;

        public override bool IsSatisfied(NpcScheduleContext context)
        {
            if (_timeSlots.Length == 0) return true;
            for (int i = 0; i < _timeSlots.Length; i++)
            {
                var slot = _timeSlots[i];
                if (slot != null && string.Equals(slot.Id, context.TimeSlotId, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        public void ConfigureForTests(TimeSlotDefinition[] timeSlots)
        {
            _timeSlots = timeSlots ?? Array.Empty<TimeSlotDefinition>();
        }
    }
}
