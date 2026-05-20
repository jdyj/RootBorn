namespace Rootborn.UI.Objectives
{
    public static class ObjectiveJournalSourceCatalog
    {
        public static IObjectiveJournalSource[] CreateDefaultSources()
        {
            return new IObjectiveJournalSource[]
            {
                new StudentDayGuideObjectiveSource(),
                new DailyPlanObjectiveSource(),
                new QuestLogObjectiveSource(),
                new QuestChainObjectiveSource(),
                new CampaignObjectiveSource(),
                new MilestoneObjectiveSource(),
                new CareerInterestObjectiveSource()
            };
        }
    }
}
