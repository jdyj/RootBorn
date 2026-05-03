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

        public CropGrowthContext(CropDefinition crop, int currentStage, float stageProgress01, float waterLevel01, bool isRaining)
        {
            Crop = crop;
            CurrentStage = currentStage;
            StageProgress01 = stageProgress01;
            WaterLevel01 = waterLevel01;
            IsRaining = isRaining;
        }
    }
}
