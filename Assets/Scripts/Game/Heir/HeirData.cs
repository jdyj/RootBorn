using System.Collections.Generic;

namespace Rootborn.Game.Heir
{
    public sealed class HeirData
    {
        public IReadOnlyList<HeirTrait> Traits { get; }

        public HeirData(IReadOnlyList<HeirTrait> traits)
        {
            Traits = traits;
        }

        public float CombinedHungerDecayMul()
        {
            float mul = 1f;
            for (int i = 0; i < Traits.Count; i++) mul *= Traits[i].HungerDecayMul;
            return mul;
        }

        public float CombinedFatigueDecayMul()
        {
            float mul = 1f;
            for (int i = 0; i < Traits.Count; i++) mul *= Traits[i].FatigueDecayMul;
            return mul;
        }

        public float CombinedGatherSpeedMul()
        {
            float mul = 1f;
            for (int i = 0; i < Traits.Count; i++) mul *= Traits[i].GatherSpeedMul;
            return mul;
        }

        public float CombinedLearnSpeedMul()
        {
            float mul = 1f;
            for (int i = 0; i < Traits.Count; i++) mul *= Traits[i].LearnSpeedMul;
            return mul;
        }
    }
}
