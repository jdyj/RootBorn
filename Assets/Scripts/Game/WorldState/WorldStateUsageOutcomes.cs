using System.Collections.Generic;

namespace Rootborn.Game.WorldState
{
    internal static class WorldStateUsageOutcomeIds
    {
        public const string Activity = "activity";
        public const string Quest = "quest";
        public const string Encyclopedia = "encyclopedia";
        public const string CareerHint = "career-hint";

        public static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && values != null && !values.Contains(value)) values.Add(value);
        }
    }
}
