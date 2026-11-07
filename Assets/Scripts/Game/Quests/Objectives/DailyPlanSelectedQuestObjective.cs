using Rootborn.Core.Quests;
using UnityEngine;

namespace Rootborn.Game.Quests.Objectives
{
    [CreateAssetMenu(fileName = "Objective_DailyPlanSelected", menuName = "Rootborn/Quests/Objectives/Daily Plan Selected")]
    public sealed class DailyPlanSelectedQuestObjective : QuestObjectiveBase
    {
        protected override QuestObjectiveSpec BuildSpec() => QuestObjectiveSpec.AnySubject((int)QuestEventKind.DailyPlanSelected);
    }
}
