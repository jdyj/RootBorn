using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;

namespace Rootborn.Game.WorldState
{
    public static class WorldStateSummaryBuilder
    {
        public static WorldStateSummaryModel[] BuildForSurface(IReadOnlyList<WorldStateFlagDefinition> flags, WorldStateProgress progress, WorldStateSummarySurface surface)
        {
            return Build(flags, progress, surface, null);
        }

        public static WorldStateSummaryModel[] BuildForLocation(IReadOnlyList<WorldStateFlagDefinition> flags, WorldStateProgress progress, LocationDefinition location, WorldStateSummarySurface surface)
        {
            return Build(flags, progress, surface, location);
        }

        private static WorldStateSummaryModel[] Build(IReadOnlyList<WorldStateFlagDefinition> flags, WorldStateProgress progress, WorldStateSummarySurface surface, LocationDefinition location)
        {
            if (flags == null || progress == null) return Array.Empty<WorldStateSummaryModel>();
            var result = new List<WorldStateSummaryModel>();
            for (int i = 0; i < flags.Count; i++)
            {
                var flag = flags[i];
                if (flag == null || !progress.IsActive(flag) || !flag.IsVisibleOn(surface)) continue;
                if (location != null && !flag.IsRelatedTo(location)) continue;
                var record = progress.GetRecord(flag);
                result.Add(new WorldStateSummaryModel(
                    flag.Id,
                    flag.DisplayNameKey,
                    flag.DescriptionKey,
                    flag.RelatedLocation != null ? flag.RelatedLocation.DisplayNameKey : string.Empty,
                    flag.FirstNpcDisplayName(),
                    flag.NextActionKey,
                    flag.ChangeKind,
                    record.Scope,
                    flag.Badges,
                    flag.SortPriority,
                    record.SeenNotification));
            }

            result.Sort((left, right) => right.SortPriority.CompareTo(left.SortPriority));
            return result.ToArray();
        }
    }

    public sealed class WorldStateFlagLookupCache
    {
        private readonly IReadOnlyList<WorldStateFlagDefinition> _flags;
        private Dictionary<string, WorldStateFlagDefinition> _byId;

        public WorldStateFlagLookupCache(IReadOnlyList<WorldStateFlagDefinition> flags)
        {
            _flags = flags ?? Array.Empty<WorldStateFlagDefinition>();
        }

        public int BuildCount { get; private set; }

        public bool TryGetFlag(string id, out WorldStateFlagDefinition flag)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(id))
            {
                flag = null;
                return false;
            }

            return _byId.TryGetValue(id, out flag);
        }

        private void EnsureBuilt()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, WorldStateFlagDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < _flags.Count; i++)
            {
                var flag = _flags[i];
                if (flag != null && !string.IsNullOrEmpty(flag.Id)) _byId[flag.Id] = flag;
            }

            BuildCount++;
        }
    }
}
