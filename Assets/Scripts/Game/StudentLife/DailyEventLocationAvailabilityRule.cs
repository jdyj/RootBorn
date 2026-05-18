using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyEventRule_Location", menuName = "Rootborn/Student Life/Daily Events/Rules/Location")]
    public sealed class DailyEventLocationAvailabilityRule : DailyEventAvailabilityRuleBase
    {
        [SerializeField] private LocationDefinition _location;

        public override bool IsSatisfied(DailyEventContext context, DailyEventDefinition dailyEvent)
        {
            var expected = _location != null ? _location : dailyEvent != null ? dailyEvent.Location : null;
            if (expected == null || context.Location == null) return false;
            return expected == context.Location || expected.Id == context.Location.Id;
        }

        public void ConfigureForTests(LocationDefinition location)
        {
            _location = location;
        }
    }
}
