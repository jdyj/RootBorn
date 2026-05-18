using Rootborn.Game.Dialogue;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public sealed class LocationStateNpcScheduleCondition : LocationStateConditionBase
    {
        [SerializeField] private NpcScheduleDefinition _schedule;
        [SerializeField] private LocationDefinition _requiredLocation;
        public override bool IsSatisfied(in LocationStateContext context)
        {
            if (context.NpcScheduleResolver == null || _schedule == null) return false;
            string timeSlotId = context.TimeSlot != null ? context.TimeSlot.Id : NpcScheduleResolver.ResolveTimeSlotId(context.StudentProgress);
            int day = context.StudentProgress != null ? context.StudentProgress.CurrentDay : 1;
            var result = context.NpcScheduleResolver.Resolve(_schedule, new NpcScheduleContext(day, 0, timeSlotId, context.StudentProgress));
            var required = _requiredLocation != null ? _requiredLocation : context.Location;
            return required != null && result.Location != null && result.Location.Id == required.Id;
        }
        public void ConfigureForTests(NpcScheduleDefinition schedule, LocationDefinition requiredLocation) { _schedule = schedule; _requiredLocation = requiredLocation; }
    }
}
