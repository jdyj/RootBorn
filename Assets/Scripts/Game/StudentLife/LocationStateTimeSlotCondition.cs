using System;
using Rootborn.Game.Dialogue;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public sealed class LocationStateTimeSlotCondition : LocationStateConditionBase
    {
        [SerializeField] private TimeSlotDefinition[] _timeSlots = Array.Empty<TimeSlotDefinition>();
        public override bool IsSatisfied(in LocationStateContext context)
        {
            if (_timeSlots == null || _timeSlots.Length == 0) return true;
            string currentId = context.TimeSlot != null ? context.TimeSlot.Id : NpcScheduleResolver.ResolveTimeSlotId(context.StudentProgress);
            if (string.IsNullOrEmpty(currentId)) return false;
            for (int i = 0; i < _timeSlots.Length; i++) if (_timeSlots[i] != null && _timeSlots[i].Id == currentId) return true;
            return false;
        }
        public void ConfigureForTests(TimeSlotDefinition[] timeSlots) { _timeSlots = timeSlots ?? Array.Empty<TimeSlotDefinition>(); }
    }
}
