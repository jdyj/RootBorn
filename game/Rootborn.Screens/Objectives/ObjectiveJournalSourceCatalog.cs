using System.Collections.Generic;
using Rootborn.Screens.Objectives.Sources;

namespace Rootborn.Screens.Objectives
{
    /// <summary>수첩의 모으는 쪽 목록(Unity ObjectiveJournalSourceCatalog와 같은 순서). 새 모으는 쪽 = 클래스 하나 + 여기 한 줄.</summary>
    public static class ObjectiveJournalSourceCatalog
    {
        public static void Register(List<IObjectiveJournalSource> sources)
        {
            sources.Add(new StudentDayGuideSource());
            sources.Add(new DailyPlanSource());
            sources.Add(new DailyPlanRouteHintSource());
            sources.Add(new ResourceRecipeSource());
            sources.Add(new StatusActivitySource());
            sources.Add(new QuestLogSource());
            sources.Add(new QuestChainSource());
            sources.Add(new CampaignSource());
            sources.Add(new MilestoneSource());
            sources.Add(new CareerInterestSource());
        }
    }
}
