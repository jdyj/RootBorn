using Rootborn.Game.Resources;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Gather", menuName = "Rootborn/Quests/Objectives/Gather")]
    public sealed class GatherQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private ResourceNodeDefinition _targetResource;

        public ResourceNodeDefinition TargetResource => _targetResource;

        public override bool Matches(in QuestEvent questEvent)
        {
            return questEvent.Kind == QuestEventKind.Gather
                && _targetResource != null
                && questEvent.Resource == _targetResource;
        }
    }
}
