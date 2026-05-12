using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DiscoveryOutcome_TraitDelta", menuName = "Rootborn/Student Life/Exploration/Outcomes/Trait Delta")]
    public sealed class DiscoveryTraitDeltaOutcome : DiscoveryOutcomeBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _delta;

        public TraitDefinition Trait => _trait;
        public int Delta => _delta;

        public override string Apply(StudentLifeProgress progress, string discoveryId)
        {
            if (progress == null || _trait == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddTrait(_trait, _delta);
            return DiscoveryLogCodec.EncodeDelta(discoveryId, _trait.Id, _delta);
        }

        public void ConfigureForTests(TraitDefinition trait, int delta)
        {
            _trait = trait;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "DiscoveryOutcome_SkillDelta", menuName = "Rootborn/Student Life/Exploration/Outcomes/Skill Delta")]
    public sealed class DiscoverySkillDeltaOutcome : DiscoveryOutcomeBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _delta;

        public SkillDefinition Skill => _skill;
        public int Delta => _delta;

        public override string Apply(StudentLifeProgress progress, string discoveryId)
        {
            if (progress == null || _skill == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddSkill(_skill, _delta);
            return DiscoveryLogCodec.EncodeDelta(discoveryId, _skill.Id, _delta);
        }

        public void ConfigureForTests(SkillDefinition skill, int delta)
        {
            _skill = skill;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "DiscoveryOutcome_StatusDelta", menuName = "Rootborn/Student Life/Exploration/Outcomes/Status Delta")]
    public sealed class DiscoveryStatusDeltaOutcome : DiscoveryOutcomeBase
    {
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, string discoveryId)
        {
            if (progress == null || _status == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddStatus(_status, _delta, discoveryId);
            return DiscoveryLogCodec.EncodeDelta(discoveryId, _status.Id, _delta);
        }

        public void ConfigureForTests(StatusDefinition status, int delta)
        {
            _status = status;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "DiscoveryOutcome_CareerHint", menuName = "Rootborn/Student Life/Exploration/Outcomes/Career Hint")]
    public sealed class DiscoveryCareerHintOutcome : DiscoveryOutcomeBase
    {
        [SerializeField] private CareerDefinition _career;

        public CareerDefinition Career => _career;

        public override string Apply(StudentLifeProgress progress, string discoveryId)
        {
            if (progress == null || _career == null)
            {
                return string.Empty;
            }

            bool wasUnlocked = progress.IsCareerHintUnlocked(_career);
            progress.UnlockCareerHint(_career);
            return wasUnlocked ? string.Empty : DiscoveryLogCodec.EncodeDelta(discoveryId, _career.Id, 1);
        }

        public void ConfigureForTests(CareerDefinition career)
        {
            _career = career;
        }
    }
}
