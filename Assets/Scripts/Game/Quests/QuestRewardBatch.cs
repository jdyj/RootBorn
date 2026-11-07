using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Rewards;
using Rootborn.Game.Common;

namespace Rootborn.Game.Quests
{
    /// <summary>
    /// Turns reward and completion-effect assets into a Rootborn.Core RewardBundle. The all-or-nothing rule
    /// (check everything, then give items, apply the rest and record state together) lives in RewardTransaction.
    /// </summary>
    public static class QuestRewardBatch
    {
        public static bool CanApply(QuestRewardBase[] rewards, in RewardRuntimeContext context)
            => RewardTransaction.CanApply(Bundle(rewards, null, in context), context.Inventory);

        public static bool TryApply(QuestRewardBase[] rewards, in RewardRuntimeContext context, Action commitState)
            => RewardTransaction.TryApply(Bundle(rewards, null, in context), context.Inventory, commitState);

        /// <summary>Item rewards become grants (checked, not applied); other rewards and then effects become steps.</summary>
        public static RewardBundle<ItemDefinition> Bundle(QuestRewardBase[] rewards, QuestCompletionEffectBase[] effects, in RewardRuntimeContext context)
        {
            var grants = new List<ItemAmount<ItemDefinition>>();
            var steps = new List<IRewardStep>();
            if (rewards != null)
            {
                for (int i = 0; i < rewards.Length; i++)
                {
                    var reward = rewards[i];
                    if (reward == null) continue;
                    bool isItem = reward.TryGetInventoryGrant(out var grant);
                    if (isItem) grants.Add(new ItemAmount<ItemDefinition>(grant.Item, grant.Count));
                    steps.Add(new RewardStep(reward, context, applies: !isItem));
                }
            }

            if (effects != null)
            {
                for (int i = 0; i < effects.Length; i++)
                    if (effects[i] != null) steps.Add(new EffectStep(effects[i], context));
            }

            return new RewardBundle<ItemDefinition>(grants, steps);
        }

        private sealed class RewardStep : IRewardStep
        {
            private readonly QuestRewardBase _reward;
            private readonly RewardRuntimeContext _context;
            private readonly bool _applies;

            public RewardStep(QuestRewardBase reward, RewardRuntimeContext context, bool applies)
            {
                _reward = reward;
                _context = context;
                _applies = applies;
            }

            public bool CanApply() => _reward.CanApply(in _context);

            public void Apply()
            {
                if (_applies) _reward.Apply(in _context);
            }
        }

        private sealed class EffectStep : IRewardStep
        {
            private readonly QuestCompletionEffectBase _effect;
            private readonly RewardRuntimeContext _context;

            public EffectStep(QuestCompletionEffectBase effect, RewardRuntimeContext context)
            {
                _effect = effect;
                _context = context;
            }

            public bool CanApply() => _effect.CanApply(in _context);
            public void Apply() => _effect.Apply(in _context);
        }
    }
}
