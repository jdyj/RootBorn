using System;
using System.Collections.Generic;
using Rootborn.Game.Interiors;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Housing
{
    [Serializable]
    public struct HouseConstructionCellRequirement
    {
        [SerializeField] private Vector2Int _cell;
        [SerializeField] private HouseConstructionCellKind _kind;

        public HouseConstructionCellRequirement(Vector2Int cell, HouseConstructionCellKind kind)
        {
            _cell = cell;
            _kind = kind;
        }

        public Vector2Int Cell => _cell;
        public HouseConstructionCellKind Kind => _kind;
        public static HouseConstructionCellRequirement Floor(int x, int y) => new HouseConstructionCellRequirement(new Vector2Int(x, y), HouseConstructionCellKind.Floor);
        public static HouseConstructionCellRequirement Wall(int x, int y) => new HouseConstructionCellRequirement(new Vector2Int(x, y), HouseConstructionCellKind.Wall);
        public static HouseConstructionCellRequirement Door(int x, int y) => new HouseConstructionCellRequirement(new Vector2Int(x, y), HouseConstructionCellKind.Door);
    }

    public readonly struct HouseUpgradeContext
    {
        public HouseUpgradeContext(HouseStateSaveData state, HouseCurrencyWallet wallet, StudentLifeProgress progress)
        {
            State = state;
            Wallet = wallet;
            Progress = progress;
        }

        public HouseStateSaveData State { get; }
        public HouseCurrencyWallet Wallet { get; }
        public StudentLifeProgress Progress { get; }
    }

    public abstract class HouseUpgradeConditionBase : ScriptableObject
    {
        public abstract bool IsMet(in HouseUpgradeContext context);
    }

    public abstract class HouseUpgradeEffectBase : ScriptableObject
    {
        public abstract bool CanApply(in HouseUpgradeContext context);
        public abstract void Apply(in HouseUpgradeContext context);
    }

    [CreateAssetMenu(fileName = "HouseCondition_Always", menuName = "Rootborn/Housing/Conditions/Always")]
    public sealed class HouseAlwaysCondition : HouseUpgradeConditionBase
    {
        public override bool IsMet(in HouseUpgradeContext context) => true;
    }

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
    }

    [CreateAssetMenu(fileName = "HouseBlueprint_New", menuName = "Rootborn/Housing/Construction Blueprint")]
    public sealed class HouseConstructionBlueprintDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private RectInt _bounds;
        [SerializeField] private HouseConstructionCellRequirement[] _requiredCells = Array.Empty<HouseConstructionCellRequirement>();

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public RectInt Bounds => _bounds;
        public IReadOnlyList<HouseConstructionCellRequirement> RequiredCells => _requiredCells ?? Array.Empty<HouseConstructionCellRequirement>();

        public bool IsCellAllowed(Vector2Int cell, HouseConstructionCellKind kind)
        {
            if (!_bounds.Contains(cell)) return false;
            for (int i = 0; i < RequiredCells.Count; i++)
            {
                var required = RequiredCells[i];
                if (required.Cell == cell && required.Kind == kind) return true;
            }
            return false;
        }

        public static HouseConstructionBlueprintDefinition CreateForTests(string id, RectInt bounds, HouseConstructionCellRequirement[] requiredCells)
        {
            var definition = CreateInstance<HouseConstructionBlueprintDefinition>();
            definition._id = id;
            definition._bounds = bounds;
            definition._requiredCells = requiredCells ?? Array.Empty<HouseConstructionCellRequirement>();
            return definition;
        }
    }
}
