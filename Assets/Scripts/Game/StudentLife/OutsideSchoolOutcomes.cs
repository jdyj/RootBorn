using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "OutsideSchoolOutcome_TraitDelta", menuName = "Rootborn/Student Life/Outside School/Outcomes/Trait Delta")]
    public sealed class OutsideSchoolTraitDeltaOutcome : OutsideSchoolOutcomeBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _delta;

        public TraitDefinition Trait => _trait;
        public int Delta => _delta;

        public override string Apply(StudentLifeProgress progress, string activityId)
        {
            if (progress == null || _trait == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddTrait(_trait, _delta);
            return OutsideSchoolLogCodec.EncodeDelta(activityId, _trait.Id, _delta);
        }

        public void ConfigureForTests(TraitDefinition trait, int delta)
        {
            _trait = trait;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "OutsideSchoolOutcome_RelationshipDelta", menuName = "Rootborn/Student Life/Outside School/Outcomes/Relationship Delta")]
    public sealed class OutsideSchoolRelationshipDeltaOutcome : OutsideSchoolOutcomeBase
    {
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, string activityId)
        {
            if (progress == null || _relationship == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddRelationship(_relationship, _delta, activityId);
            return OutsideSchoolLogCodec.EncodeDelta(activityId, _relationship.Id, _delta);
        }

        public void ConfigureForTests(RelationshipDefinition relationship, int delta)
        {
            _relationship = relationship;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "OutsideSchoolOutcome_StatusDelta", menuName = "Rootborn/Student Life/Outside School/Outcomes/Status Delta")]
    public sealed class OutsideSchoolStatusDeltaOutcome : OutsideSchoolOutcomeBase
    {
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, string activityId)
        {
            if (progress == null || _status == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddStatus(_status, _delta, activityId);
            return OutsideSchoolLogCodec.EncodeDelta(activityId, _status.Id, _delta);
        }

        public void ConfigureForTests(StatusDefinition status, int delta)
        {
            _status = status;
            _delta = delta;
        }
    }
}
