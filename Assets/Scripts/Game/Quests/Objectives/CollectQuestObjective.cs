using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Collect", menuName = "Rootborn/Quests/Objectives/Collect")]
    public sealed class CollectQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private ItemDefinition _targetItem;

        public ItemDefinition TargetItem => _targetItem;

        public override bool Matches(in QuestEvent questEvent)
        {
            return questEvent.Kind == QuestEventKind.Collect
                && SameEntry(questEvent.Item, _targetItem, questEvent.Item != null ? questEvent.Item.Id : null, _targetItem != null ? _targetItem.Id : null);
        }
    }
}
