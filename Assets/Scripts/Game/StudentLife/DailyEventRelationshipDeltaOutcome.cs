using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyEventOutcome_RelationshipDelta", menuName = "Rootborn/Student Life/Daily Events/Outcomes/Relationship Delta")]
    public sealed class DailyEventRelationshipDeltaOutcome : DailyEventOutcomeBase
    {
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, DailyEventDefinition dailyEvent, DailyEventChoiceDefinition choice)
        {
            if (progress == null || _relationship == null || _delta == 0) return string.Empty;
            progress.AddRelationship(_relationship, _delta, dailyEvent != null ? dailyEvent.Id : string.Empty);
            return DailyEventLogCodec.EncodeDelta(dailyEvent, choice, _relationship.Id, _delta);
        }

        public void ConfigureForTests(RelationshipDefinition relationship, int delta)
        {
            _relationship = relationship;
            _delta = delta;
        }
    }
}
