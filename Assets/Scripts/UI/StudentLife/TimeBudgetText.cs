using Rootborn.Game.Localization;
using Rootborn.Game.StudentLife;
namespace Rootborn.UI.StudentLife
{
    internal static class TimeBudgetText
    {
        public static string Format(StudentLifeProgress progress)
        {
            if (progress == null || progress.Calendar == null) return GameText.Get("life.tp.loading");
            return GameText.Format("life.tp.summary", progress.CurrentDay,
                GameText.Get("life.weekday." + progress.Calendar.GetWeekdayIndex(progress.CurrentDay)),
                progress.TimePointsRemaining, progress.DailyTimePointBudget);
        }
        public static string Cost(ActivityTimePolicyDefinition policy)
        {
            return policy == null || policy.TimePointCost == 0 ? GameText.Get("life.tp.free") : GameText.Format("life.tp.cost", policy.TimePointCost);
        }
    }
}
