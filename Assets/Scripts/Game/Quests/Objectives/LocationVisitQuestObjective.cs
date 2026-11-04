using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_LocationVisit", menuName = "Rootborn/Quests/Objectives/Location Visit")]
    public sealed class LocationVisitQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private LocationDefinition _targetLocation;
        public LocationDefinition TargetLocation => _targetLocation;
        public override bool Matches(in QuestEvent questEvent) => questEvent.Kind == QuestEventKind.LocationVisit
            && _targetLocation != null && questEvent.Location == _targetLocation;
    }
}
