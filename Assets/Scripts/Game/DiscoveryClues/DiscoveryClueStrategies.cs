using System.Collections.Generic;

namespace Rootborn.Game.DiscoveryClues
{
    internal static class DiscoveryClueOutcomeIds
    {
        public const string Encyclopedia = "encyclopedia";
        public const string CareerHint = "career-hint";
        public const string Quest = "quest";
        public const string WorldState = "world-state";

        public static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && values != null && !values.Contains(value)) values.Add(value);
        }
    }
}
