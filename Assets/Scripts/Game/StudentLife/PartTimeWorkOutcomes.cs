using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "WorkOutcome_TraitDelta", menuName = "Rootborn/Student Life/Part-Time Work/Outcomes/Trait Delta")]
    public sealed class WorkTraitDeltaOutcome : WorkOutcomeBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _delta;

        public TraitDefinition Trait => _trait;
        public int Delta => _delta;

        public override string Apply(StudentLifeProgress progress, string workId)
        {
            if (progress == null || _trait == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddTrait(_trait, _delta);
            return PartTimeWorkLogCodec.EncodeDelta(workId, _trait.Id, _delta);
        }

        public void ConfigureForTests(TraitDefinition trait, int delta)
        {
            _trait = trait;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "WorkOutcome_SkillDelta", menuName = "Rootborn/Student Life/Part-Time Work/Outcomes/Skill Delta")]
    public sealed class WorkSkillDeltaOutcome : WorkOutcomeBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _delta;

        public SkillDefinition Skill => _skill;
        public int Delta => _delta;

        public override string Apply(StudentLifeProgress progress, string workId)
        {
            if (progress == null || _skill == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddSkill(_skill, _delta);
            return PartTimeWorkLogCodec.EncodeDelta(workId, _skill.Id, _delta);
        }

        public void ConfigureForTests(SkillDefinition skill, int delta)
        {
            _skill = skill;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "WorkOutcome_RelationshipDelta", menuName = "Rootborn/Student Life/Part-Time Work/Outcomes/Relationship Delta")]
    public sealed class WorkRelationshipDeltaOutcome : WorkOutcomeBase
    {
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, string workId)
        {
            if (progress == null || _relationship == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddRelationship(_relationship, _delta, workId);
            return PartTimeWorkLogCodec.EncodeDelta(workId, _relationship.Id, _delta);
        }

        public void ConfigureForTests(RelationshipDefinition relationship, int delta)
        {
            _relationship = relationship;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "WorkOutcome_StatusDelta", menuName = "Rootborn/Student Life/Part-Time Work/Outcomes/Status Delta")]
    public sealed class WorkStatusDeltaOutcome : WorkOutcomeBase
    {
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, string workId)
        {
            if (progress == null || _status == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddStatus(_status, _delta, workId);
            return PartTimeWorkLogCodec.EncodeDelta(workId, _status.Id, _delta);
        }

        public void ConfigureForTests(StatusDefinition status, int delta)
        {
            _status = status;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "WorkOutcome_CareerHint", menuName = "Rootborn/Student Life/Part-Time Work/Outcomes/Career Hint")]
    public sealed class WorkCareerHintOutcome : WorkOutcomeBase
    {
        [SerializeField] private CareerDefinition _career;

        public override string Apply(StudentLifeProgress progress, string workId)
        {
            if (progress == null || _career == null)
            {
                return string.Empty;
            }

            bool wasUnlocked = progress.IsCareerHintUnlocked(_career);
            progress.UnlockCareerHint(_career);
            return wasUnlocked ? string.Empty : PartTimeWorkLogCodec.EncodeDelta(workId, _career.Id, 1);
        }

        public void ConfigureForTests(CareerDefinition career)
        {
            _career = career;
        }
    }
}
