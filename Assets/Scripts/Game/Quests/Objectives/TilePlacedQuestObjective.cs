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

        public override bool Matches(in QuestEvent questEvent)
        {
            if (questEvent.Kind != QuestEventKind.TilePlaced)
            {
                return false;
            }

            return _targetTile == null || questEvent.Tile == _targetTile;
        }
    }
}
