using System;
using System.Collections.Generic;
using Rootborn.Game.Interiors;
using UnityEngine;

namespace Rootborn.Game.Housing
{
    [CreateAssetMenu(fileName = "HouseStage_New", menuName = "Rootborn/Housing/Upgrade Stage")]
    public sealed class HouseUpgradeStageDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayKey;
        [SerializeField] private int _stageIndex;
        [SerializeField] private int _hireCost;
        [SerializeField] private int _directCost;
        [SerializeField] private InteriorGenerationProfile _profile;
        [SerializeField] private InteriorTileSetDefinition _tileSet;
        [SerializeField] private HouseConstructionBlueprintDefinition _blueprint;
        [SerializeField] private HouseUpgradeConditionBase[] _generalConditions = Array.Empty<HouseUpgradeConditionBase>();
        [SerializeField] private HouseUpgradeConditionBase[] _directConditions = Array.Empty<HouseUpgradeConditionBase>();
        [SerializeField] private HouseUpgradeEffectBase[] _hireEffects = Array.Empty<HouseUpgradeEffectBase>();
        [SerializeField] private HouseUpgradeEffectBase[] _directEffects = Array.Empty<HouseUpgradeEffectBase>();

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayKey => string.IsNullOrEmpty(_displayKey) ? Id : _displayKey;
        public int StageIndex => Mathf.Max(0, _stageIndex);
        public int HireCost => Mathf.Max(0, _hireCost);
        public int DirectCost => Mathf.Max(0, _directCost);
        public InteriorGenerationProfile Profile => _profile;
        public InteriorTileSetDefinition TileSet => _tileSet;
        public HouseConstructionBlueprintDefinition Blueprint => _blueprint;
        public IReadOnlyList<HouseUpgradeConditionBase> GeneralConditions => _generalConditions ?? Array.Empty<HouseUpgradeConditionBase>();
        public IReadOnlyList<HouseUpgradeConditionBase> DirectConditions => _directConditions ?? Array.Empty<HouseUpgradeConditionBase>();
        public IReadOnlyList<HouseUpgradeEffectBase> HireEffects => _hireEffects ?? Array.Empty<HouseUpgradeEffectBase>();
        public IReadOnlyList<HouseUpgradeEffectBase> DirectEffects => _directEffects ?? Array.Empty<HouseUpgradeEffectBase>();

        public static HouseUpgradeStageDefinition CreateForTests(string id, int stageIndex, int hireCost, int directCost, InteriorGenerationProfile profile, InteriorTileSetDefinition tileSet, HouseConstructionBlueprintDefinition blueprint)
        {
            var definition = CreateInstance<HouseUpgradeStageDefinition>();
            definition._id = id;
            definition._stageIndex = stageIndex;
            definition._hireCost = hireCost;
            definition._directCost = directCost;
            definition._profile = profile;
            definition._tileSet = tileSet;
            definition._blueprint = blueprint;
            return definition;
        }

        public void ConfigureForTests(string id, int stageIndex, int hireCost, int directCost, InteriorGenerationProfile profile, InteriorTileSetDefinition tileSet, HouseConstructionBlueprintDefinition blueprint)
        {
            _id = id;
            _stageIndex = stageIndex;
            _hireCost = hireCost;
            _directCost = directCost;
            _profile = profile;
            _tileSet = tileSet;
            _blueprint = blueprint;
        }

        public void ConfigureConditionsForTests(HouseUpgradeConditionBase[] generalConditions, HouseUpgradeConditionBase[] directConditions)
        {
            _generalConditions = generalConditions ?? Array.Empty<HouseUpgradeConditionBase>();
            _directConditions = directConditions ?? Array.Empty<HouseUpgradeConditionBase>();
        }

        public void ConfigureEffectsForTests(HouseUpgradeEffectBase[] hireEffects, HouseUpgradeEffectBase[] directEffects)
        {
            _hireEffects = hireEffects ?? Array.Empty<HouseUpgradeEffectBase>();
            _directEffects = directEffects ?? Array.Empty<HouseUpgradeEffectBase>();
        }
    }
}