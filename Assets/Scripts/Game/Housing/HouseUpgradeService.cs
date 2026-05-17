using System;
using System.Collections.Generic;

namespace Rootborn.Game.Housing
{
    public enum HouseUpgradeResultKind
    {
        Applied,
        InvalidStage,
        AlreadyApplied,
        InsufficientCurrency,
        RequirementFailed,
        ConstructionIncomplete
    }

    public readonly struct HouseUpgradeResult
    {
        public HouseUpgradeResult(HouseUpgradeResultKind kind, string message)
        {
            Kind = kind;
            Message = message ?? string.Empty;
        }

        public HouseUpgradeResultKind Kind { get; }
        public string Message { get; }
    }

    public sealed class HouseUpgradeService
    {
        private readonly HouseUpgradeStageDefinition[] _stages;

        public HouseUpgradeService(IReadOnlyList<HouseUpgradeStageDefinition> stages)
        {
            if (stages == null)
            {
                _stages = Array.Empty<HouseUpgradeStageDefinition>();
                return;
            }

            _stages = new HouseUpgradeStageDefinition[stages.Count];
            for (int i = 0; i < stages.Count; i++) _stages[i] = stages[i];
        }

        public HouseUpgradeStageDefinition ResolveNextStage(HouseStateSaveData state)
        {
            int current = state == null ? 0 : state.CurrentStageIndex;
            HouseUpgradeStageDefinition best = null;
            for (int i = 0; i < _stages.Length; i++)
            {
                var stage = _stages[i];
                if (stage == null || stage.StageIndex <= current) continue;
                if (best == null || stage.StageIndex < best.StageIndex) best = stage;
            }

            return best;
        }

        public bool CanStartDirect(HouseUpgradeStageDefinition stage, in HouseUpgradeContext context)
        {
            if (stage == null || stage.Blueprint == null) return false;
            var conditions = stage.DirectConditions;
            if (conditions.Count == 0) return false;
            for (int i = 0; i < conditions.Count; i++)
            {
                if (conditions[i] != null && !conditions[i].IsMet(in context)) return false;
            }

            return true;
        }

        public HouseUpgradeResult TryHire(HouseUpgradeStageDefinition stage, HouseStateSaveData state, HouseCurrencyWallet wallet)
        {
            if (stage == null || state == null || wallet == null) return new HouseUpgradeResult(HouseUpgradeResultKind.InvalidStage, "Invalid stage");
            if (state.CurrentStageIndex >= stage.StageIndex) return new HouseUpgradeResult(HouseUpgradeResultKind.AlreadyApplied, "Already applied");
            if (!wallet.CanSpend(stage.HireCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");
            if (!wallet.TrySpend(stage.HireCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");

            state.CurrentStageIndex = stage.StageIndex;
            state.ActiveConstructionStageId = string.Empty;
            state.PlacedConstructionCells = Array.Empty<HouseConstructionCellSaveData>();
            state.LatestRoute = HouseUpgradeRouteKind.HireConstruction;
            return new HouseUpgradeResult(HouseUpgradeResultKind.Applied, "Applied");
        }

        public HouseUpgradeResult TryCompleteDirect(HouseUpgradeStageDefinition stage, HouseStateSaveData state, HouseCurrencyWallet wallet, HouseConstructionSession session)
        {
            if (stage == null || state == null || wallet == null || session == null) return new HouseUpgradeResult(HouseUpgradeResultKind.InvalidStage, "Invalid direct construction");
            if (state.CurrentStageIndex >= stage.StageIndex) return new HouseUpgradeResult(HouseUpgradeResultKind.AlreadyApplied, "Already applied");
            if (!session.IsComplete) return new HouseUpgradeResult(HouseUpgradeResultKind.ConstructionIncomplete, "Construction incomplete");
            if (!wallet.CanSpend(stage.DirectCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");
            if (!wallet.TrySpend(stage.DirectCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");

            state.CurrentStageIndex = stage.StageIndex;
            state.ActiveConstructionStageId = string.Empty;
            state.PlacedConstructionCells = Array.Empty<HouseConstructionCellSaveData>();
            state.LatestRoute = HouseUpgradeRouteKind.DirectConstruction;
            return new HouseUpgradeResult(HouseUpgradeResultKind.Applied, "Applied");
        }
    }
}
