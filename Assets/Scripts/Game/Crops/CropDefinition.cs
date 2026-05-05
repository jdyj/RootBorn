using Rootborn.Game.Common;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Crops
{
    [CreateAssetMenu(fileName = "Crop_New", menuName = "Rootborn/Crops/Crop Definition")]
    public sealed class CropDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayKey;
        [SerializeField] private Sprite[] _growthStageSprites = System.Array.Empty<Sprite>();
        [SerializeField] private float[] _stageDurationsSec = System.Array.Empty<float>();
        [SerializeField] private ToolDefinition _requiredHarvestTool;
        [SerializeField] private GrowthBehaviorBase[] _behaviors = System.Array.Empty<GrowthBehaviorBase>();
        [SerializeField] private ItemDefinition _seedItem;
        [SerializeField] private ItemDefinition _harvestItem;
        [SerializeField] private int _harvestYieldMin = 1;
        [SerializeField] private int _harvestYieldMax = 2;

        public string Id => _id;
        public string DisplayKey => _displayKey;
        public Sprite[] GrowthStageSprites => _growthStageSprites;
        public float[] StageDurationsSec => _stageDurationsSec;
        public ToolDefinition RequiredHarvestTool => _requiredHarvestTool;
        public GrowthBehaviorBase[] Behaviors => _behaviors;
        public ItemDefinition SeedItem => _seedItem;
        public ItemDefinition HarvestItem => _harvestItem;
        public int HarvestYieldMin => _harvestYieldMin;
        public int HarvestYieldMax => _harvestYieldMax;

        public int StageCount => _growthStageSprites.Length;

        public int RollYield(System.Random rng)
        {
            int min = Mathf.Max(0, _harvestYieldMin);
            int max = Mathf.Max(min, _harvestYieldMax);
            if (rng == null) return min;
            return rng.Next(min, max + 1);
        }

        public float GetStageDuration(int stage)
        {
            if (stage < 0 || stage >= _stageDurationsSec.Length) return 0f;
            return _stageDurationsSec[stage];
        }

        public bool IsHarvestable(int stage) => stage >= StageCount - 1 && StageCount > 0;

        public bool CanHarvestWith(ToolDefinition tool)
        {
            if (_requiredHarvestTool == null) return true;
            return tool == _requiredHarvestTool;
        }

        public float ComputeGrowthRateMultiplier(in CropGrowthContext ctx)
        {
            float mul = 1f;
            for (int i = 0; i < _behaviors.Length; i++)
            {
                if (_behaviors[i] != null)
                    mul *= _behaviors[i].ModifyGrowthRate(in ctx);
            }
            return mul;
        }
    }
}
