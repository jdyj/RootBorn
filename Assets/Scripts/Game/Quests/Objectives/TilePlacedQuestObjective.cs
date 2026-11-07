using Rootborn.Core.Quests;
using Rootborn.Game.Tiles;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_TilePlaced", menuName = "Rootborn/Quests/Objectives/Tile Placed")]
    public sealed class TilePlacedQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private PlaceableTileDefinition _targetTile;

        public PlaceableTileDefinition TargetTile => _targetTile;

        public void ConfigureForRuntime(string displayKey, int requiredCount, PlaceableTileDefinition targetTile)
        {
            base.ConfigureForRuntime(displayKey, requiredCount);
            _targetTile = targetTile;
        }

        // No target accepts any placed tile.
        protected override QuestObjectiveSpec BuildSpec() => _targetTile == null
            ? QuestObjectiveSpec.AnyEvent((int)QuestEventKind.TilePlaced)
            : QuestObjectiveSpec.Listed((int)QuestEventKind.TilePlaced, QuestSubjectIds.Of(_targetTile));
    }
}
