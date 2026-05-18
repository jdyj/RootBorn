using UnityEngine;

namespace Rootborn.Game.WorldState
{
    [CreateAssetMenu(fileName = "WorldStateUsageRepeatPolicy_New", menuName = "Rootborn/World State/Usage/Repeat Policy")]
    public sealed class WorldStateUsageRepeatPolicyDefinition : ScriptableObject
    {
        private static WorldStateUsageRepeatPolicyDefinition _oncePolicy;

        [SerializeField] private WorldStateUsageRepeatMode _mode = WorldStateUsageRepeatMode.Once;
        [SerializeField] private int _cooldownDays;
        [SerializeField] private int _maxUses = 1;
        [SerializeField] private bool _allowDuplicateRewards;

        public static WorldStateUsageRepeatPolicyDefinition OncePolicy
        {
            get
            {
                if (_oncePolicy == null)
                {
                    _oncePolicy = CreateInstance<WorldStateUsageRepeatPolicyDefinition>();
                    _oncePolicy.ConfigureForTests(WorldStateUsageRepeatMode.Once, 0, 1, false);
                }

                return _oncePolicy;
            }
        }

        public WorldStateUsageRepeatMode Mode => _mode;
        public int CooldownDays => Mathf.Max(0, _cooldownDays);
        public int MaxUses => Mathf.Max(0, _maxUses);
        public bool AllowDuplicateRewards => _allowDuplicateRewards;

        public bool CanUse(WorldStateUsageDefinition usage, WorldStateUsageProgress progress, int currentDay, out string reason)
        {
            reason = string.Empty;
            if (usage == null || progress == null)
            {
                reason = "missing usage progress";
                return false;
            }

            var record = progress.GetRecord(usage.Id);
            int day = Mathf.Max(1, currentDay);
            if (record.CooldownUntilDay > day)
            {
                reason = "cooldown until day " + record.CooldownUntilDay;
                return false;
            }

            if (_mode == WorldStateUsageRepeatMode.Once && record.UsedCount > 0)
            {
                reason = "already used";
                return false;
            }

            if (_mode == WorldStateUsageRepeatMode.OncePerDay && record.LastUsedDay == day)
            {
                reason = "already used today";
                return false;
            }

            if (_mode == WorldStateUsageRepeatMode.MaxUses && MaxUses > 0 && record.UsedCount >= MaxUses)
            {
                reason = "max uses reached";
                return false;
            }

            return true;
        }

        public int NextCooldownUntilDay(int currentDay)
        {
            if (_mode != WorldStateUsageRepeatMode.DailyCooldown || CooldownDays <= 0) return 0;
            return Mathf.Max(1, currentDay) + CooldownDays;
        }

        public void ConfigureForTests(WorldStateUsageRepeatMode mode, int cooldownDays, int maxUses, bool allowDuplicateRewards)
        {
            _mode = mode;
            _cooldownDays = Mathf.Max(0, cooldownDays);
            _maxUses = Mathf.Max(0, maxUses);
            _allowDuplicateRewards = allowDuplicateRewards;
        }
    }
}
