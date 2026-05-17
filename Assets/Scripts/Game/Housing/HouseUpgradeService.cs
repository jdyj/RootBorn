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

            bool hasCondition = false;
            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null) return false;

                hasCondition = true;
                if (!condition.IsMet(in context)) return false;
            }

            return hasCondition;
        }

        public HouseUpgradeResult TryHire(HouseUpgradeStageDefinition stage, HouseStateSaveData state, HouseCurrencyWallet wallet)
        {
            if (stage == null || state == null || wallet == null) return new HouseUpgradeResult(HouseUpgradeResultKind.InvalidStage, "Invalid stage");
            if (state.CurrentStageIndex >= stage.StageIndex) return new HouseUpgradeResult(HouseUpgradeResultKind.AlreadyApplied, "Already applied");
            if (!IsRequestedNextStage(stage, state)) return new HouseUpgradeResult(HouseUpgradeResultKind.InvalidStage, "Stage is not available");

            var context = new HouseUpgradeContext(state, wallet, null);
            if (!AreConditionsMet(stage.GeneralConditions, in context)) return new HouseUpgradeResult(HouseUpgradeResultKind.RequirementFailed, "Requirement failed");
            if (!CanApplyEffects(stage.HireEffects, in context)) return new HouseUpgradeResult(HouseUpgradeResultKind.RequirementFailed, "Effect requirement failed");
            if (!wallet.CanSpend(stage.HireCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");
            if (!wallet.TrySpend(stage.HireCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");

            state.CurrentStageIndex = stage.StageIndex;
            state.ActiveConstructionStageId = string.Empty;
            state.PlacedConstructionCells = Array.Empty<HouseConstructionCellSaveData>();
            state.LatestRoute = HouseUpgradeRouteKind.HireConstruction;
            ApplyEffects(stage.HireEffects, in context);
            return new HouseUpgradeResult(HouseUpgradeResultKind.Applied, "Applied");
        }

        public HouseUpgradeResult TryCompleteDirect(HouseUpgradeStageDefinition stage, HouseStateSaveData state, HouseCurrencyWallet wallet, HouseConstructionSession session)
        {
            if (stage == null || state == null || wallet == null || session == null) return new HouseUpgradeResult(HouseUpgradeResultKind.InvalidStage, "Invalid direct construction");
            if (state.CurrentStageIndex >= stage.StageIndex) return new HouseUpgradeResult(HouseUpgradeResultKind.AlreadyApplied, "Already applied");
            if (!IsRequestedNextStage(stage, state)) return new HouseUpgradeResult(HouseUpgradeResultKind.InvalidStage, "Stage is not available");
            if (session.Blueprint != stage.Blueprint) return new HouseUpgradeResult(HouseUpgradeResultKind.InvalidStage, "Construction blueprint mismatch");

            var context = new HouseUpgradeContext(state, wallet, null);
            if (!AreConditionsMet(stage.GeneralConditions, in context)) return new HouseUpgradeResult(HouseUpgradeResultKind.RequirementFailed, "Requirement failed");
            if (!CanStartDirect(stage, in context)) return new HouseUpgradeResult(HouseUpgradeResultKind.RequirementFailed, "Direct construction unavailable");
            if (!session.IsComplete) return new HouseUpgradeResult(HouseUpgradeResultKind.ConstructionIncomplete, "Construction incomplete");
            if (!CanApplyEffects(stage.DirectEffects, in context)) return new HouseUpgradeResult(HouseUpgradeResultKind.RequirementFailed, "Effect requirement failed");
            if (!wallet.CanSpend(stage.DirectCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");
            if (!wallet.TrySpend(stage.DirectCost)) return new HouseUpgradeResult(HouseUpgradeResultKind.InsufficientCurrency, "Insufficient currency");

            state.CurrentStageIndex = stage.StageIndex;
            state.ActiveConstructionStageId = string.Empty;
            state.PlacedConstructionCells = Array.Empty<HouseConstructionCellSaveData>();
            state.LatestRoute = HouseUpgradeRouteKind.DirectConstruction;
            ApplyEffects(stage.DirectEffects, in context);
            return new HouseUpgradeResult(HouseUpgradeResultKind.Applied, "Applied");
        }

        private bool IsRequestedNextStage(HouseUpgradeStageDefinition stage, HouseStateSaveData state)
        {
            if (stage == null || state == null) return false;
            if (stage.StageIndex != state.CurrentStageIndex + 1) return false;
            return ReferenceEquals(ResolveNextStage(state), stage);
        }

        private static bool AreConditionsMet(IReadOnlyList<HouseUpgradeConditionBase> conditions, in HouseUpgradeContext context)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null || !condition.IsMet(in context)) return false;
            }

            return true;
        }

        private static bool CanApplyEffects(IReadOnlyList<HouseUpgradeEffectBase> effects, in HouseUpgradeContext context)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null || !effect.CanApply(in context)) return false;
            }

            return true;
        }

        private static void ApplyEffects(IReadOnlyList<HouseUpgradeEffectBase> effects, in HouseUpgradeContext context)
        {
            for (int i = 0; i < effects.Count; i++) effects[i].Apply(in context);
        }
    }
}