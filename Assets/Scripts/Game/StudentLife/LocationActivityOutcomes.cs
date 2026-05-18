using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "LocationActivityOutcome_TraitDelta", menuName = "Rootborn/Student Life/Location Identity/Outcomes/Trait Delta")]
    public sealed class LocationActivityTraitDeltaOutcome : LocationActivityOutcomeBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, LocationActivityDefinition activity)
        {
            if (progress == null || activity == null || _trait == null || _delta == 0) return string.Empty;
            progress.AddTrait(_trait, _delta);
            return LocationActivityLogCodec.EncodeDelta(activity.Id, _trait.Id, _delta);
        }

        public void ConfigureForTests(TraitDefinition trait, int delta)
        {
            _trait = trait;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "LocationActivityOutcome_SkillDelta", menuName = "Rootborn/Student Life/Location Identity/Outcomes/Skill Delta")]
    public sealed class LocationActivitySkillDeltaOutcome : LocationActivityOutcomeBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, LocationActivityDefinition activity)
        {
            if (progress == null || activity == null || _skill == null || _delta == 0) return string.Empty;
            progress.AddSkill(_skill, _delta);
            return LocationActivityLogCodec.EncodeDelta(activity.Id, _skill.Id, _delta);
        }

        public void ConfigureForTests(SkillDefinition skill, int delta)
        {
            _skill = skill;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "LocationActivityOutcome_RelationshipDelta", menuName = "Rootborn/Student Life/Location Identity/Outcomes/Relationship Delta")]
    public sealed class LocationActivityRelationshipDeltaOutcome : LocationActivityOutcomeBase
    {
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, LocationActivityDefinition activity)
        {
            if (progress == null || activity == null || _relationship == null || _delta == 0) return string.Empty;
            progress.AddRelationship(_relationship, _delta, activity.Id);
            return LocationActivityLogCodec.EncodeDelta(activity.Id, _relationship.Id, _delta);
        }

        public void ConfigureForTests(RelationshipDefinition relationship, int delta)
        {
            _relationship = relationship;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "LocationActivityOutcome_StatusDelta", menuName = "Rootborn/Student Life/Location Identity/Outcomes/Status Delta")]
    public sealed class LocationActivityStatusDeltaOutcome : LocationActivityOutcomeBase
    {
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, LocationActivityDefinition activity)
        {
            if (progress == null || activity == null || _status == null || _delta == 0) return string.Empty;
            progress.AddStatus(_status, _delta, activity.Id);
            return LocationActivityLogCodec.EncodeDelta(activity.Id, _status.Id, _delta);
        }

        public void ConfigureForTests(StatusDefinition status, int delta)
        {
            _status = status;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "LocationActivityOutcome_CareerHint", menuName = "Rootborn/Student Life/Location Identity/Outcomes/Career Hint")]
    public sealed class LocationActivityCareerHintOutcome : LocationActivityOutcomeBase
    {
        [SerializeField] private CareerDefinition _career;

        public override string Apply(StudentLifeProgress progress, LocationActivityDefinition activity)
        {
            if (progress == null || activity == null || _career == null) return string.Empty;
            bool wasUnlocked = progress.IsCareerHintUnlocked(_career);
            progress.UnlockCareerHint(_career);
            return wasUnlocked ? string.Empty : LocationActivityLogCodec.EncodeUnlock(activity.Id, _career.Id);
        }

        public void ConfigureForTests(CareerDefinition career)
        {
            _career = career;
        }
    }
}
