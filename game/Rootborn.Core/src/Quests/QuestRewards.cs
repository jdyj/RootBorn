using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Rewards;

namespace Rootborn.Core.Quests
{
    /// <summary>Something a quest or quest step gives. Checked with <see cref="CanApply"/> before anything changes.</summary>
    public abstract class QuestReward
    {
        public abstract bool CanApply(in RewardRuntimeContext context);
        public abstract void Apply(in RewardRuntimeContext context);

        // Return true only when this reward's entire effect is the returned item grant.
        // Batch callers commit these grants together and do not call Apply a second time.
        public virtual bool TryGetInventoryGrant(out InventoryGrant grant)
        {
            grant = default;
            return false;
        }
    }

    /// <summary>Something that happens when a quest's reward is claimed (a flag, a trait, knowledge).</summary>
    public abstract class QuestCompletionEffect
    {
        public abstract bool CanApply(in RewardRuntimeContext context);
        public abstract void Apply(in RewardRuntimeContext context);
    }

    public sealed class ItemQuestReward : QuestReward
    {
        private readonly int _count;

        public ItemQuestReward(ItemDefinition item, int count = 1)
        {
            Item = item;
            _count = count;
        }

        public ItemDefinition Item { get; }
        public int Count => Math.Max(1, _count);

        public override bool TryGetInventoryGrant(out InventoryGrant grant)
        {
            grant = new InventoryGrant(Item, Count);
            return true;
        }

        public override bool CanApply(in RewardRuntimeContext context) => context.Inventory != null && Item != null && context.Inventory.CanAdd(Item, Count);

        public override void Apply(in RewardRuntimeContext context)
        {
            if (!CanApply(in context)) return;
            context.Inventory.Add(Item, Count);
        }
    }

    public sealed class FurnitureUnlockQuestReward : QuestReward
    {
        public FurnitureUnlockQuestReward(string unlockToken) { UnlockToken = unlockToken ?? string.Empty; }
        public string UnlockToken { get; }

        public override bool CanApply(in RewardRuntimeContext context)
            => context.FurnitureUnlockProgress != null && !string.IsNullOrEmpty(UnlockToken) && !context.FurnitureUnlockProgress.IsUnlocked(UnlockToken);

        public override void Apply(in RewardRuntimeContext context) => context.FurnitureUnlockProgress?.TryUnlock(UnlockToken);
    }

    /// <summary>A world state flag a reward turns on, with the effect version to record once it is on.</summary>
    public readonly struct QuestWorldStateFlag
    {
        public readonly string Id;
        public readonly int EffectVersion;

        public QuestWorldStateFlag(string id, int effectVersion)
        {
            Id = id;
            EffectVersion = effectVersion;
        }
    }

    /// <summary>Turns world state flags on. Applies while at least one listed flag is still off. A flag without an id is skipped.</summary>
    public sealed class WorldStateChangeQuestReward : QuestReward
    {
        private readonly QuestWorldStateFlag[] _flags;

        public WorldStateChangeQuestReward(IReadOnlyList<QuestWorldStateFlag> flags, string sourceQuestChainId, string sourceQuestStepId, string sourceEventId, int activationDay)
        {
            _flags = flags == null ? Array.Empty<QuestWorldStateFlag>() : new List<QuestWorldStateFlag>(flags).ToArray();
            Source = new QuestWorldStateSource(sourceQuestChainId, sourceQuestStepId, sourceEventId, activationDay);
        }

        public IReadOnlyList<QuestWorldStateFlag> Flags => _flags;
        public QuestWorldStateSource Source { get; }

        public override bool CanApply(in RewardRuntimeContext context)
        {
            if (context.WorldStateProgress == null || _flags.Length == 0) return false;
            for (int i = 0; i < _flags.Length; i++)
                if (!string.IsNullOrEmpty(_flags[i].Id) && !context.WorldStateProgress.IsActive(_flags[i].Id)) return true;
            return false;
        }

        public override void Apply(in RewardRuntimeContext context)
        {
            if (context.WorldStateProgress == null) return;
            for (int i = 0; i < _flags.Length; i++)
            {
                var flag = _flags[i];
                if (string.IsNullOrEmpty(flag.Id)) continue;
                if (context.WorldStateProgress.TryActivate(flag.Id, Source)) context.WorldStateProgress.MarkEffectVersionApplied(flag.Id, flag.EffectVersion);
            }
        }
    }

    public sealed class SetStoryFlagCompletionEffect : QuestCompletionEffect
    {
        public SetStoryFlagCompletionEffect(string flagId) { FlagId = flagId; }
        public string FlagId { get; }

        public override bool CanApply(in RewardRuntimeContext context) => context.StoryFlags != null && !string.IsNullOrEmpty(FlagId);

        public override void Apply(in RewardRuntimeContext context)
        {
            if (!CanApply(in context)) return;
            context.StoryFlags.Set(FlagId);
        }
    }

    /// <summary>Adds to a trait and, when given, unlocks a career hint.</summary>
    public sealed class TraitDeltaCompletionEffect : QuestCompletionEffect
    {
        public TraitDeltaCompletionEffect(string traitId, int delta = 1, string careerHintId = null)
        {
            TraitId = traitId;
            Delta = delta;
            CareerHintId = careerHintId;
        }

        public string TraitId { get; }
        public int Delta { get; }
        public string CareerHintId { get; }

        public override bool CanApply(in RewardRuntimeContext context) => context.StudentLifeProgress != null && !string.IsNullOrEmpty(TraitId) && Delta != 0;

        public override void Apply(in RewardRuntimeContext context)
        {
            if (!CanApply(in context)) return;
            context.StudentLifeProgress.AddTrait(TraitId, Delta);
            context.StudentLifeProgress.UnlockCareerHint(CareerHintId);
        }
    }

    public sealed class UnlockKnowledgeCompletionEffect : QuestCompletionEffect
    {
        public UnlockKnowledgeCompletionEffect(string knowledgeId) { KnowledgeId = knowledgeId; }
        public string KnowledgeId { get; }

        public override bool CanApply(in RewardRuntimeContext context) => context.KnowledgeProgress != null && !string.IsNullOrEmpty(KnowledgeId);

        public override void Apply(in RewardRuntimeContext context)
        {
            if (!CanApply(in context)) return;
            context.KnowledgeProgress.Seed(KnowledgeId);
        }
    }

    /// <summary>
    /// Turns rewards and completion effects into a <see cref="RewardBundle{TItem}"/>. The all-or-nothing rule
    /// (check everything, then give items, apply the rest and record state together) lives in <see cref="RewardTransaction"/>.
    /// </summary>
    public static class QuestRewardBatch
    {
        public static bool CanApply(IReadOnlyList<QuestReward> rewards, in RewardRuntimeContext context)
            => RewardTransaction.CanApply(Bundle(rewards, null, in context), context.Inventory);

        public static bool TryApply(IReadOnlyList<QuestReward> rewards, in RewardRuntimeContext context, Action commitState)
            => RewardTransaction.TryApply(Bundle(rewards, null, in context), context.Inventory, commitState);

        /// <summary>Item rewards become grants (checked, not applied); other rewards and then effects become steps.</summary>
        public static RewardBundle<ItemDefinition> Bundle(IReadOnlyList<QuestReward> rewards, IReadOnlyList<QuestCompletionEffect> effects, in RewardRuntimeContext context)
        {
            var grants = new List<ItemAmount<ItemDefinition>>();
            var steps = new List<IRewardStep>();
            if (rewards != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    var reward = rewards[i];
                    if (reward == null) continue;
                    bool isItem = reward.TryGetInventoryGrant(out var grant);
                    if (isItem) grants.Add(new ItemAmount<ItemDefinition>(grant.Item, grant.Count));
                    steps.Add(new RewardStep(reward, context, !isItem));
                }
            }

            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                    if (effects[i] != null) steps.Add(new EffectStep(effects[i], context));
            }

            return new RewardBundle<ItemDefinition>(grants, steps);
        }

        private sealed class RewardStep : IRewardStep
        {
            private readonly QuestReward _reward;
            private readonly RewardRuntimeContext _context;
            private readonly bool _applies;

            public RewardStep(QuestReward reward, RewardRuntimeContext context, bool applies)
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
            private readonly QuestCompletionEffect _effect;
            private readonly RewardRuntimeContext _context;

            public EffectStep(QuestCompletionEffect effect, RewardRuntimeContext context)
            {
                _effect = effect;
                _context = context;
            }

            public bool CanApply() => _effect.CanApply(in _context);
            public void Apply() => _effect.Apply(in _context);
        }
    }
}
