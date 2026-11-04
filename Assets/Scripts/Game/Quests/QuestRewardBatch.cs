using System;
using System.Collections.Generic;

namespace Rootborn.Game.Quests
{
    /// <summary>Preflights a reward group before committing its inventory and progress changes.</summary>
    public static class QuestRewardBatch
    {
        public static bool CanApply(QuestRewardBase[] rewards, in RewardRuntimeContext context)
        {
            return TryPrepare(rewards, in context, out _);
        }

        public static bool TryApply(QuestRewardBase[] rewards, in RewardRuntimeContext context, Action commitState)
        {
            if (!TryPrepare(rewards, in context, out var grants)) return false;
            // Capture the value context before the callback; in parameters cannot be captured.
            var capturedContext = context;
            Action commit = () =>
            {
                if (rewards != null)
                {
                    for (int i = 0; i < rewards.Length; i++)
                    {
                        var reward = rewards[i];
                        if (reward != null && !reward.TryGetInventoryGrant(out _)) reward.Apply(in capturedContext);
                    }
                }
                commitState?.Invoke();
            };

            if (grants.Count > 0) return context.Inventory.TryExchange(null, grants, commit);
            commit();
            return true;
        }

        private static bool TryPrepare(QuestRewardBase[] rewards, in RewardRuntimeContext context, out List<InventoryGrant> grants)
        {
            grants = new List<InventoryGrant>();
            if (rewards == null) return true;
            for (int i = 0; i < rewards.Length; i++)
            {
                var reward = rewards[i];
                if (reward == null) continue;
                if (!reward.CanApply(in context)) return false;
                if (reward.TryGetInventoryGrant(out var grant)) grants.Add(grant);
            }

            return grants.Count == 0 || (context.Inventory != null && context.Inventory.CanExchange(null, grants));
        }
    }
}
