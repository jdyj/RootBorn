using Rootborn.Core.Quests;
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Collect", menuName = "Rootborn/Quests/Objectives/Collect")]
    public sealed class CollectQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private ItemDefinition _targetItem;

        public ItemDefinition TargetItem => _targetItem;

        protected override QuestObjectiveSpec BuildSpec() => QuestObjectiveSpec.Listed((int)QuestEventKind.Collect, _targetItem != null ? QuestSubjectIds.Of(_targetItem) : null);
    }
}
