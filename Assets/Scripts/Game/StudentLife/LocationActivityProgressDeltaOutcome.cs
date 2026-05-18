using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public enum LocationActivityProgressTarget
    {
        Trait,
        Skill,
        Relationship,
        Status,
        CareerHint
    }

    [CreateAssetMenu(fileName = "LocationActivityOutcome_ProgressDelta", menuName = "Rootborn/Student Life/Location Identity/Outcomes/Progress Delta")]
    public sealed class LocationActivityProgressDeltaOutcome : LocationActivityOutcomeBase
    {
        [SerializeField] private LocationActivityProgressTarget _target;
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private CareerDefinition _career;
        [SerializeField] private int _delta = 1;

        public LocationActivityProgressTarget Target => _target;
        public int Delta => _delta;

        public override string Apply(StudentLifeProgress progress, LocationActivityDefinition activity)
        {
            if (progress == null || activity == null) return string.Empty;
            if (_target == LocationActivityProgressTarget.Trait && _trait != null && _delta != 0)
            {
                progress.AddTrait(_trait, _delta);
                return LocationActivityLogCodec.EncodeDelta(activity.Id, _trait.Id, _delta);
            }
            if (_target == LocationActivityProgressTarget.Skill && _skill != null && _delta != 0)
            {
                progress.AddSkill(_skill, _delta);
                return LocationActivityLogCodec.EncodeDelta(activity.Id, _skill.Id, _delta);
            }
            if (_target == LocationActivityProgressTarget.Relationship && _relationship != null && _delta != 0)
            {
                progress.AddRelationship(_relationship, _delta, activity.Id);
                return LocationActivityLogCodec.EncodeDelta(activity.Id, _relationship.Id, _delta);
            }
            if (_target == LocationActivityProgressTarget.Status && _status != null && _delta != 0)
            {
                progress.AddStatus(_status, _delta, activity.Id);
                return LocationActivityLogCodec.EncodeDelta(activity.Id, _status.Id, _delta);
            }
            if (_target == LocationActivityProgressTarget.CareerHint && _career != null)
            {
                bool wasUnlocked = progress.IsCareerHintUnlocked(_career);
                progress.UnlockCareerHint(_career);
                return wasUnlocked ? string.Empty : LocationActivityLogCodec.EncodeUnlock(activity.Id, _career.Id);
            }

            return string.Empty;
        }

        public void ConfigureTraitForTests(TraitDefinition trait, int delta)
        {
            _target = LocationActivityProgressTarget.Trait;
            _trait = trait;
            _delta = delta;
        }

        public void ConfigureSkillForTests(SkillDefinition skill, int delta)
        {
            _target = LocationActivityProgressTarget.Skill;
            _skill = skill;
            _delta = delta;
        }

        public void ConfigureRelationshipForTests(RelationshipDefinition relationship, int delta)
        {
            _target = LocationActivityProgressTarget.Relationship;
            _relationship = relationship;
            _delta = delta;
        }

        public void ConfigureStatusForTests(StatusDefinition status, int delta)
        {
            _target = LocationActivityProgressTarget.Status;
            _status = status;
            _delta = delta;
        }

        public void ConfigureCareerHintForTests(CareerDefinition career)
        {
            _target = LocationActivityProgressTarget.CareerHint;
            _career = career;
            _delta = 1;
        }
    }
}
