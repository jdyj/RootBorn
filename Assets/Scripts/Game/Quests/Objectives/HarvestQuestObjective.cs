using Rootborn.Game.Common;
using Rootborn.Game.Crops;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_Harvest", menuName = "Rootborn/Quests/Objectives/Harvest")]
    public sealed class HarvestQuestObjective : QuestObjectiveBase
    {
        [SerializeField] private CropDefinition _targetCrop;
        [SerializeField] private ItemDefinition _targetHarvestItem;

        public CropDefinition TargetCrop => _targetCrop;
        public ItemDefinition TargetHarvestItem => _targetHarvestItem;

        public override bool Matches(in QuestEvent questEvent)
        {
            if (questEvent.Kind != QuestEventKind.Harvest)
            {
                return false;
            }

            bool cropMatches = _targetCrop != null && questEvent.Crop == _targetCrop;
            bool itemMatches = _targetHarvestItem != null && questEvent.Item == _targetHarvestItem;
            return cropMatches || itemMatches;
        }
    }
}
