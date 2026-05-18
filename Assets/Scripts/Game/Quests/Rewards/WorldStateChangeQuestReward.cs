using System;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.Quests.Rewards
{
    [CreateAssetMenu(fileName = "Reward_WorldStateChange", menuName = "Rootborn/Quests/Rewards/World State Change")]
    public sealed class WorldStateChangeQuestReward : QuestRewardBase
    {
        [SerializeField] private WorldStateFlagDefinition[] _flags = Array.Empty<WorldStateFlagDefinition>();
        [SerializeField] private string _sourceQuestChainId;
        [SerializeField] private string _sourceQuestStepId;
        [SerializeField] private string _sourceEventId;
        [SerializeField] private int _activationDay;

        public WorldStateFlagDefinition[] Flags => _flags;

        public override bool CanApply(in RewardRuntimeContext context)
        {
            if (context.WorldStateProgress == null || _flags == null || _flags.Length == 0) return false;
            for (int i = 0; i < _flags.Length; i++)
            {
                var flag = _flags[i];
                if (flag != null && !context.WorldStateProgress.IsActive(flag)) return true;
            }

            return false;
        }

        public override void Apply(in RewardRuntimeContext context)
        {
            if (context.WorldStateProgress == null || _flags == null) return;
            var source = new WorldStateActivationSource(_sourceQuestChainId, _sourceQuestStepId, _sourceEventId, _activationDay);
            for (int i = 0; i < _flags.Length; i++)
            {
                var flag = _flags[i];
                if (flag == null) continue;
                bool activated = context.WorldStateProgress.TryActivate(flag, source);
                if (activated) context.WorldStateProgress.MarkEffectVersionApplied(flag, flag.EffectVersion);
            }
        }

        public void ConfigureForTests(WorldStateFlagDefinition[] flags, string sourceQuestChainId, string sourceQuestStepId, string sourceEventId, int activationDay)
        {
            _flags = flags ?? Array.Empty<WorldStateFlagDefinition>();
            _sourceQuestChainId = sourceQuestChainId ?? string.Empty;
            _sourceQuestStepId = sourceQuestStepId ?? string.Empty;
            _sourceEventId = sourceEventId ?? string.Empty;
            _activationDay = activationDay;
        }
    }
}
