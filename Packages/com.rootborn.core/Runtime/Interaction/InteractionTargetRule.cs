namespace Rootborn.Core.Interaction
{
    /// <summary>
    /// Which thing E acts on when several are in reach: the one the player stands at. A clearly closer target wins;
    /// priority only settles near ties, such as a door beside a sign. Priority alone used to let anything ranked higher
    /// anywhere in reach (a passing townsperson, a facility entrance) take E from the desk the player was standing at.
    /// </summary>
    public static class InteractionTargetRule
    {
        /// <summary>Distances closer than this to each other count as equally close.</summary>
        public const float DistanceBand = 0.5f;

        public static bool IsBetter(int priority, float distance, int bestPriority, float bestDistance)
        {
            if (bestPriority == int.MinValue) return true;
            if (distance < bestDistance - DistanceBand) return true;
            if (bestDistance < distance - DistanceBand) return false;
            if (priority != bestPriority) return priority > bestPriority;
            return distance <= bestDistance;
        }
    }
}
