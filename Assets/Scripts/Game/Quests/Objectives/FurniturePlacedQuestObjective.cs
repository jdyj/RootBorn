using Rootborn.Core.Quests;
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
        protected override QuestObjectiveSpec BuildSpec() => QuestObjectiveSpec.Listed((int)QuestEventKind.FurniturePlaced, QuestSubjectIds.Of(_acceptedFurniture)).WithRequiredSurface(_requiredSurfaceId);
    }
}
