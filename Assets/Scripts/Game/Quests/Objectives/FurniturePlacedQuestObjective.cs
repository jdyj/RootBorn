using System;
using System.Collections.Generic;
using Rootborn.Game.Placement;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_FurniturePlaced", menuName = "Rootborn/Quests/Objectives/Furniture Placed")]
    public sealed class FurniturePlacedQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private FurnitureDefinition[] _acceptedFurniture = Array.Empty<FurnitureDefinition>();
        [SerializeField] private string _requiredSurfaceId;
        public IReadOnlyList<FurnitureDefinition> AcceptedFurniture => _acceptedFurniture;
        public string RequiredSurfaceId => _requiredSurfaceId;
        public override bool Matches(in QuestEvent questEvent)
        {
            if (questEvent.Kind != QuestEventKind.FurniturePlaced || questEvent.Furniture == null || _acceptedFurniture == null) return false;
            if (!string.IsNullOrEmpty(_requiredSurfaceId) && !string.Equals(_requiredSurfaceId, questEvent.SurfaceId, StringComparison.Ordinal)) return false;
            for (int i = 0; i < _acceptedFurniture.Length; i++) if (_acceptedFurniture[i] == questEvent.Furniture) return true;
            return false;
        }
    }
}
