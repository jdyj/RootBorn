using Rootborn.Core.Quests;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_LocationVisit", menuName = "Rootborn/Quests/Objectives/Location Visit")]
    public sealed class LocationVisitQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private LocationDefinition _targetLocation;
        public LocationDefinition TargetLocation => _targetLocation;
        protected override QuestObjectiveSpec BuildSpec() => QuestObjectiveSpec.Listed((int)QuestEventKind.LocationVisit, _targetLocation != null ? QuestSubjectIds.Of(_targetLocation) : null);
    }
}
