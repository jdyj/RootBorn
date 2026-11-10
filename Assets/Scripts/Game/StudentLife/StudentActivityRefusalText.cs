using Rootborn.Game.Localization;

namespace Rootborn.Game.StudentLife
{
    /// <summary>
    /// Why a time-and-energy activity refused a press, in words the player can act on. Empty when the refusal is not about
    /// today's time, the day, or energy and focus (for example an unmet requirement).
    /// </summary>
    public static class StudentActivityRefusalText
    {
        public static string Explain(StudentLifeProgress progress, ActivityTimePolicyDefinition timePolicy, int energyCost, int focusCost)
        {
            if (progress == null) return string.Empty;
            if (progress.DayState != StudentDayState.InProgress
                || (timePolicy != null && !progress.CanSpendTimePoints(timePolicy.TimePointCost)))
                return GameText.Localized("ui.prompt.no-time", "No time left today. Try again tomorrow.");
            if (timePolicy != null && !timePolicy.IsAllowedDay(progress.CurrentDay))
                return GameText.Localized("ui.prompt.not-today", "You can't do this today.");
            if (!progress.CanUseTimePolicy(timePolicy))
                return GameText.Localized("ui.prompt.done-today", "You already did this today. Try again tomorrow.");
            if (!progress.CanSpend(energyCost, focusCost))
                return GameText.Localized("ui.prompt.no-energy", "Not enough energy or focus. Rest first.");
            return string.Empty;
        }
    }
}
