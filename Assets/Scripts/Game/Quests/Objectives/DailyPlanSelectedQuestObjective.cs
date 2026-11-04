using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_DailyPlanSelected", menuName = "Rootborn/Quests/Objectives/Daily Plan Selected")]
    public sealed class DailyPlanSelectedQuestObjective : QuestObjectiveBase
    {
        public override bool Matches(in QuestEvent questEvent) => questEvent.Kind == QuestEventKind.DailyPlanSelected
            && questEvent.DailyPlanRoute != null;
    }
}
