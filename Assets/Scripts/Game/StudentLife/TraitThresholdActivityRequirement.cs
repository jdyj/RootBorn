using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ActivityReq_TraitThreshold", menuName = "Rootborn/Student Life/Activity Requirements/Trait Threshold")]
    public sealed class TraitThresholdActivityRequirement : LifeActivityRequirementBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && progress.GetTraitValue(_trait) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(TraitDefinition trait, int minimumValue)
        {
            _trait = trait;
            _minimumValue = minimumValue;
        }
    }
}
