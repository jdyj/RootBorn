using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Quests.Effects
{
    [CreateAssetMenu(fileName = "Effect_TraitDelta", menuName = "Rootborn/Quests/Effects/Trait Delta")]
    public sealed class TraitDeltaCompletionEffect : QuestCompletionEffectBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _delta = 1;
        [SerializeField] private CareerDefinition _careerHint;

        public TraitDefinition Trait => _trait;
        public int Delta => _delta;
        public CareerDefinition CareerHint => _careerHint;

        public override bool CanApply(in RewardRuntimeContext context)
        {
            return context.StudentLifeProgress != null && _trait != null && _delta != 0;
        }

        public override void Apply(in RewardRuntimeContext context)
        {
            if (!CanApply(in context))
            {
                return;
            }

            context.StudentLifeProgress.AddTrait(_trait, _delta);
            context.StudentLifeProgress.UnlockCareerHint(_careerHint);
        }

        public void ConfigureForTests(TraitDefinition trait, int delta)
        {
            ConfigureForTests(trait, delta, null);
        }

        public void ConfigureForTests(TraitDefinition trait, int delta, CareerDefinition careerHint)
        {
            _trait = trait;
            _delta = delta;
            _careerHint = careerHint;
        }
    }
}