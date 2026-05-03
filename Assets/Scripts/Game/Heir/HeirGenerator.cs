using System;
using System.Collections.Generic;

namespace Rootborn.Game.Heir
{
    public static class HeirGenerator
    {
        public static HeirData Generate(
            IReadOnlyList<HeirTrait> parentTraits,
            IReadOnlyList<HeirTrait> traitPool,
            int seed,
            float inheritProbability = 0.5f,
            int maxInherited = 2,
            int randomBonus = 1)
        {
            var rng = new Random(seed);
            var picked = new List<HeirTrait>();

            int inheritedSoFar = 0;
            if (parentTraits != null)
            {
                for (int i = 0; i < parentTraits.Count && inheritedSoFar < maxInherited; i++)
                {
                    var t = parentTraits[i];
                    if (t == null || !t.IsInheritable) continue;
                    if (rng.NextDouble() < inheritProbability)
                    {
                        picked.Add(t);
                        inheritedSoFar++;
                    }
                }
            }

            if (traitPool != null && randomBonus > 0)
            {
                var pool = new List<HeirTrait>();
                for (int i = 0; i < traitPool.Count; i++)
                {
                    var t = traitPool[i];
                    if (t == null) continue;
                    if (picked.Contains(t)) continue;
                    pool.Add(t);
                }

                int added = 0;
                while (added < randomBonus && pool.Count > 0)
                {
                    int idx = rng.Next(pool.Count);
                    picked.Add(pool[idx]);
                    pool.RemoveAt(idx);
                    added++;
                }
            }

            return new HeirData(picked);
        }
    }
}
