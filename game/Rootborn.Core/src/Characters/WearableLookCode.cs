using System;
using System.Collections.Generic;

namespace Rootborn.Core.Characters
{
    /// <summary>
    /// A drawn look as a short string for the network: "stage|item,item". Decoding checks every id against the catalog,
    /// so another player can only show items the game ships, with art for that stage, each at most once.
    /// </summary>
    public static class WearableLookCode
    {
        /// <summary>Fits a 128-byte fixed network string with room to spare.</summary>
        public const int MaxBytes = 125;
        private const char StageSeparator = '|';
        private const char ItemSeparator = ',';

        public static string Encode(string stageId, IEnumerable<string> wearableIds)
        {
            var ids = new List<string>();
            if (wearableIds != null)
                foreach (string id in wearableIds)
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);
            return (stageId ?? string.Empty) + StageSeparator + string.Join(ItemSeparator.ToString(), ids);
        }

        public static bool TryDecode(WearableCatalog catalog, string code, out string stageId, out List<string> wearableIds)
        {
            stageId = string.Empty;
            wearableIds = new List<string>();
            if (catalog == null || string.IsNullOrEmpty(code)) return false;
            int split = code.IndexOf(StageSeparator);
            if (split <= 0) return false;
            string stage = code.Substring(0, split);
            if (!HasStage(catalog, stage)) return false;
            string items = code.Substring(split + 1);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (items.Length > 0)
            {
                foreach (string id in items.Split(ItemSeparator))
                {
                    var wearable = catalog.FindWearable(id);
                    if (wearable == null || !wearable.HasArtFor(stage) || !seen.Add(id)) return false;
                    wearableIds.Add(id);
                }
            }

            stageId = stage;
            return true;
        }

        private static bool HasStage(WearableCatalog catalog, string stageId)
        {
            foreach (var stage in catalog.Stages)
                if (string.Equals(stage.Id, stageId, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
