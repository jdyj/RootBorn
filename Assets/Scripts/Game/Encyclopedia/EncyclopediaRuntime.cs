using System;
using Rootborn.Game.Common;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Encyclopedia
{
    [CreateAssetMenu(fileName = "EncyclopediaCondition_DiscoveryCompleted", menuName = "Rootborn/Encyclopedia/Conditions/Discovery Completed")]
    public sealed class EncyclopediaDiscoveryCompletedCondition : EncyclopediaUnlockConditionBase
    {
        [SerializeField] private DiscoveryDefinition _discovery;

        public override bool IsSatisfied(in EncyclopediaUnlockContext context)
        {
            return _discovery != null && context.StudentLifeProgress != null && EncyclopediaConditionHelpers.HasRecorded(context.StudentLifeProgress.GetActivityLogIds(), _discovery.Id);
        }
    }

    [DisallowMultipleComponent]
    public sealed class EncyclopediaProgressComponent : MonoBehaviour
    {
        [SerializeField] private EncyclopediaCategoryDefinition[] _categories = Array.Empty<EncyclopediaCategoryDefinition>();
        [SerializeField] private EncyclopediaEntryDefinition[] _entries = Array.Empty<EncyclopediaEntryDefinition>();

        private EncyclopediaIndex _index;
        private EncyclopediaProgress _progress;
        private StudentLifeProgressComponent _studentLife;
        private PlayerInventory _inventory;
        private GatherInteractor _gather;

        public event Action<EncyclopediaEntryDefinition> OnEntryUnlocked;
        public EncyclopediaIndex Index => _index;
        public EncyclopediaProgress Progress => _progress;

        public void Bind(EncyclopediaCategoryDefinition[] categories, EncyclopediaEntryDefinition[] entries, StudentLifeProgressComponent studentLife, PlayerInventory inventory, GatherInteractor gather)
        {
            _categories = categories ?? Array.Empty<EncyclopediaCategoryDefinition>();
            _entries = entries ?? Array.Empty<EncyclopediaEntryDefinition>();
            _studentLife = studentLife;
            _inventory = inventory;
            _gather = gather;
            _index = new EncyclopediaIndex(_categories, _entries);
            var progress = _studentLife != null ? _studentLife.EnsureProgress() : null;
            string saveSlot = progress != null ? progress.SaveSlot : "default";
            string playerId = progress != null ? progress.PlayerId : "player";
            _progress = EncyclopediaProgressPersistence.LoadOrCreate(saveSlot, playerId);
        }

        public int EvaluateUnlocks()
        {
            if (_progress == null || _entries == null) return 0;
            var student = _studentLife != null ? _studentLife.EnsureProgress() : null;
            KnowledgeProgress knowledge = _gather != null ? _gather.KnowledgeProgress : null;
            Inventory inventory = _inventory != null ? _inventory.Inventory : null;
            var context = new EncyclopediaUnlockContext(student, knowledge, inventory);
            int unlocked = 0;
            long now = DateTime.UtcNow.Ticks;
            for (int i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];
                if (entry == null || !entry.IsUnlocked(context)) continue;
                if (_progress.TryUnlock(entry.Id, entry.RevealStage, now))
                {
                    unlocked++;
                    OnEntryUnlocked?.Invoke(entry);
                }
            }

            if (unlocked > 0)
            {
                EncyclopediaProgressPersistence.Save(_progress);
            }
            return unlocked;
        }
    }
}
