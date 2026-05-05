using UnityEngine;

namespace Rootborn.Game.Crops
{
    public abstract class GrowthBehaviorBase : ScriptableObject
    {
        public abstract float ModifyGrowthRate(in CropGrowthContext ctx);
    }

    public readonly struct CropGrowthContext
    {
        public readonly CropDefinition Crop;
        public readonly int CurrentStage;
        public readonly float StageProgress01;
        public readonly float WaterLevel01;
        public readonly bool IsRaining;
        public readonly float FertilizerMultiplier;

        public CropGrowthContext(CropDefinition crop, int currentStage, float stageProgress01, float waterLevel01, bool isRaining)
            : this(crop, currentStage, stageProgress01, waterLevel01, isRaining, 1f) { }

        public CropGrowthContext(CropDefinition crop, int currentStage, float stageProgress01, float waterLevel01, bool isRaining, float fertilizerMultiplier)
        {
            Crop = crop;
            CurrentStage = currentStage;
            StageProgress01 = stageProgress01;
            WaterLevel01 = waterLevel01;
            IsRaining = isRaining;
            FertilizerMultiplier = fertilizerMultiplier <= 0f ? 1f : fertilizerMultiplier;
        }
    }
}
