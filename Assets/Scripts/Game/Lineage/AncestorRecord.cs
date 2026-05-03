using System.Collections.Generic;
using Rootborn.Game.Heir;

namespace Rootborn.Game.Lineage
{
    public sealed class AncestorRecord
    {
        public int GenerationIndex { get; }
        public IReadOnlyList<HeirTrait> Traits { get; }
        public float SurvivedSeconds { get; }

        public AncestorRecord(int generationIndex, IReadOnlyList<HeirTrait> traits, float survivedSeconds)
        {
            GenerationIndex = generationIndex;
            Traits = traits;
            SurvivedSeconds = survivedSeconds;
        }
    }
}
