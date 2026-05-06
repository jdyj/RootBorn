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
                && _targetItem != null
                && questEvent.Item == _targetItem;
        }
    }
}
