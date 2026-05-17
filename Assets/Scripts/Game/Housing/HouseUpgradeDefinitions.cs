using System;
using UnityEngine;
using Rootborn.Game.StudentLife;

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
}