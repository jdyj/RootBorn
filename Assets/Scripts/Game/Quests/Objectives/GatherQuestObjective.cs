using Rootborn.Core.Quests;
using Rootborn.Game.Resources;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Gather", menuName = "Rootborn/Quests/Objectives/Gather")]
    public sealed class GatherQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private ResourceNodeDefinition _targetResource;

        public ResourceNodeDefinition TargetResource => _targetResource;

        protected override QuestObjectiveSpec BuildSpec() => QuestObjectiveSpec.Listed((int)QuestEventKind.Gather, _targetResource != null ? QuestSubjectIds.Of(_targetResource) : null);
    }
}
