using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DiscoveryRequirement_PreviousDiscovery", menuName = "Rootborn/Student Life/Exploration/Requirements/Previous Discovery")]
    public sealed class PreviousDiscoveryRequirement : DiscoveryRequirementBase
    {
        [SerializeField] private DiscoveryDefinition _requiredDiscovery;

        public DiscoveryDefinition RequiredDiscovery => _requiredDiscovery;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && _requiredDiscovery != null && progress.HasAppliedRequest(DiscoveryRunner.RequestIdFor(_requiredDiscovery));
        }

        public void ConfigureForTests(DiscoveryDefinition requiredDiscovery)
        {
            _requiredDiscovery = requiredDiscovery;
        }
    }

    [CreateAssetMenu(fileName = "DiscoveryRequirement_TraitThreshold", menuName = "Rootborn/Student Life/Exploration/Requirements/Trait Threshold")]
    public sealed class DiscoveryTraitThresholdRequirement : DiscoveryRequirementBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && _trait != null && progress.GetTraitValue(_trait) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(TraitDefinition trait, int minimumValue)
        {
            _trait = trait;
            _minimumValue = minimumValue;
        }
    }

    [CreateAssetMenu(fileName = "DiscoveryRequirement_CareerHint", menuName = "Rootborn/Student Life/Exploration/Requirements/Career Hint")]
    public sealed class DiscoveryCareerHintRequirement : DiscoveryRequirementBase
    {
        [SerializeField] private CareerDefinition _career;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && _career != null && progress.IsCareerHintUnlocked(_career);
        }

        public void ConfigureForTests(CareerDefinition career)
        {
            _career = career;
        }
    }
}
