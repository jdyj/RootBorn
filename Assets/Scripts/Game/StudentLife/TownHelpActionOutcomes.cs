using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "HelpActionOutcome_TraitDelta", menuName = "Rootborn/Student Life/Town Help/Outcomes/Trait Delta")]
    public sealed class HelpActionTraitDeltaOutcome : HelpActionOutcomeBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _delta;

        public TraitDefinition Trait => _trait;
        public int Delta => _delta;

        public override string Apply(StudentLifeProgress progress, string actionId)
        {
            if (progress == null || _trait == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddTrait(_trait, _delta);
            return TownHelpActionLogCodec.EncodeDelta(actionId, _trait.Id, _delta);
        }

        public void ConfigureForTests(TraitDefinition trait, int delta)
        {
            _trait = trait;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "HelpActionOutcome_SkillDelta", menuName = "Rootborn/Student Life/Town Help/Outcomes/Skill Delta")]
    public sealed class HelpActionSkillDeltaOutcome : HelpActionOutcomeBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, string actionId)
        {
            if (progress == null || _skill == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddSkill(_skill, _delta);
            return TownHelpActionLogCodec.EncodeDelta(actionId, _skill.Id, _delta);
        }

        public void ConfigureForTests(SkillDefinition skill, int delta)
        {
            _skill = skill;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "HelpActionOutcome_StatusDelta", menuName = "Rootborn/Student Life/Town Help/Outcomes/Status Delta")]
    public sealed class HelpActionStatusDeltaOutcome : HelpActionOutcomeBase
    {
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, string actionId)
        {
            if (progress == null || _status == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddStatus(_status, _delta, actionId);
            return TownHelpActionLogCodec.EncodeDelta(actionId, _status.Id, _delta);
        }

        public void ConfigureForTests(StatusDefinition status, int delta)
        {
            _status = status;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "HelpActionOutcome_CareerHint", menuName = "Rootborn/Student Life/Town Help/Outcomes/Career Hint")]
    public sealed class HelpActionCareerHintOutcome : HelpActionOutcomeBase
    {
        [SerializeField] private CareerDefinition _career;

        public override string Apply(StudentLifeProgress progress, string actionId)
        {
            if (progress == null || _career == null)
            {
                return string.Empty;
            }

            bool wasUnlocked = progress.IsCareerHintUnlocked(_career);
            progress.UnlockCareerHint(_career);
            return wasUnlocked ? string.Empty : TownHelpActionLogCodec.EncodeDelta(actionId, _career.Id, 1);
        }

        public void ConfigureForTests(CareerDefinition career)
        {
            _career = career;
        }
    }
}
