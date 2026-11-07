using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.Rewards
{
    /// <summary>A non-item part of a reward (money, a flag, a completion effect): checked first, applied on commit.</summary>
    public interface IRewardStep
    {
        bool CanApply();
        void Apply();
    }

    /// <summary>
    /// The inventory side of a reward. TryReceive must add every grant or none, run <c>commit</c> after adding,
    /// and undo the grants if <c>commit</c> throws.
    /// </summary>
    public interface IRewardInventory<TItem> where TItem : class
    {
        bool CanReceive(IReadOnlyList<ItemAmount<TItem>> grants);
        bool TryReceive(IReadOnlyList<ItemAmount<TItem>> grants, Action commit);
    }

    /// <summary>Everything one reward gives: item grants plus other steps, applied together or not at all.</summary>
    public sealed class RewardBundle<TItem> where TItem : class
    {
        private static readonly ItemAmount<TItem>[] NoGrants = new ItemAmount<TItem>[0];
        private static readonly IRewardStep[] NoSteps = new IRewardStep[0];

        public RewardBundle(IReadOnlyList<ItemAmount<TItem>> grants, IReadOnlyList<IRewardStep> steps)
        {
            Grants = grants ?? NoGrants;
            Steps = steps ?? NoSteps;
        }

        public IReadOnlyList<ItemAmount<TItem>> Grants { get; }
        public IReadOnlyList<IRewardStep> Steps { get; }
    }

    /// <summary>Checks a whole reward before changing anything, then gives items, applies steps and records state together.</summary>
    public static class RewardTransaction
    {
        public static bool CanApply<TItem>(RewardBundle<TItem> bundle, IRewardInventory<TItem> inventory) where TItem : class
        {
            if (bundle == null) return false;
            for (int i = 0; i < bundle.Steps.Count; i++)
                if (bundle.Steps[i] != null && !bundle.Steps[i].CanApply()) return false;
            return bundle.Grants.Count == 0 || (inventory != null && inventory.CanReceive(bundle.Grants));
        }

        /// <summary>
        /// Gives the items, then applies the steps in order, then runs <paramref name="commitState"/> (for example marking
        /// the reward claimed). Returns false and changes nothing when any part cannot apply.
        /// </summary>
        public static bool TryApply<TItem>(RewardBundle<TItem> bundle, IRewardInventory<TItem> inventory, Action commitState) where TItem : class
        {
            if (!CanApply(bundle, inventory)) return false;
            Action commit = () =>
            {
                for (int i = 0; i < bundle.Steps.Count; i++) bundle.Steps[i]?.Apply();
                commitState?.Invoke();
            };
            if (bundle.Grants.Count > 0) return inventory.TryReceive(bundle.Grants, commit);
            commit();
            return true;
        }
    }

    /// <summary>
    /// Lets one claim run at a time: a reward or effect that starts another claim while one is running gets false,
    /// so nothing is paid twice. Whether the claim is still open is the caller's state (e.g. a quest stage).
    /// </summary>
    public sealed class RewardClaimGate
    {
        private bool _claiming;

        public bool IsClaiming => _claiming;

        public bool TryClaim(Func<bool> isClaimable, Func<bool> claim)
        {
            if (_claiming || isClaimable == null || claim == null) return false;
            _claiming = true;
            try { return isClaimable() && claim(); }
            finally { _claiming = false; }
        }
    }
}
