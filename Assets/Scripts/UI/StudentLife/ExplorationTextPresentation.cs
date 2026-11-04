using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.StudentLife;

namespace Rootborn.UI.StudentLife
{
    // Player-facing exploration text only; never changes definitions, progress or collector arrays.
    internal static class ExplorationTextPresentation
    {
        private const string Neutral = "—";

        public static string Text(string key, string fallbackKey)
        {
            if (TryText(key, out string value)) return value;
            return TryText(fallbackKey, out value) ? value : Neutral;
        }

        public static string Title(string key) => Text(key, "exploration.ui.title");

        public static string Prompt(string titleKey)
        {
            return TryText(titleKey, out string title)
                ? Format("exploration.ui.prompt", title)
                : Text("exploration.ui.prompt.generic", null);
        }

        public static string ChoiceLabel(ExplorationChoiceSummaryModel choice)
        {
            string name = Text(choice.DisplayNameKey, "exploration.ui.choice");
            string detail = choice.Available
                ? Text(choice.PreviewHintKey, "exploration.ui.hint")
                : Text(choice.LockedReasonKey, "exploration.ui.locked");
            return name + " — " + detail;
        }

        public static string Result(ExplorationChoiceResult result, bool compact = false)
        {
            string status = Text("exploration.result." + result.Kind.ToString().ToLowerInvariant(), "exploration.result.updated");
            // Failed/dismissed results must not advertise any malformed supplied rewards as applied effects.
            if (result.Kind != ExplorationChoiceResultKind.Applied)
                return result.Kind == ExplorationChoiceResultKind.ConditionFailed
                    ? status + "\n" + Text(result.LockedReasonKey, "exploration.ui.locked")
                    : status;

            var rewards = new List<string>();
            var vitals = new List<string>();
            int rewardFacts = 0, vitalFacts = 0, other = 0, growth = 0, unknownStatuses = 0;
            var grants = Values(result.ItemGrantIds).ToArray();
            var spends = Values(result.ItemSpendIds).ToArray();
            AddItems(grants, "exploration.effect.item-gain", rewards, ref rewardFacts, ref other);
            AddItems(spends, "exploration.effect.item-spent", rewards, ref rewardFacts, ref other);

            int clues = Count(result.ClueIds), entries = Count(result.EncyclopediaEntryIds);
            int careers = Count(result.CareerHintIds), quests = Count(result.FollowUpQuestIds);
            AddCount("exploration.effect.clues", clues, rewards, ref rewardFacts);
            AddCount("exploration.effect.entries", entries, rewards, ref rewardFacts);
            AddCount("exploration.effect.careers", careers, rewards, ref rewardFacts);
            AddCount("exploration.effect.quests", quests, rewards, ref rewardFacts);

            var represented = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in grants)
            {
                if (TryItem(value, out string grantId, out _)) represented.Add("item-grant:" + grantId);
            }
            foreach (string value in spends)
            {
                if (TryItem(value, out string spendId, out _)) represented.Add("item-spend:" + spendId);
            }
            AddRepresented(represented, "clue:", result.ClueIds);
            AddRepresented(represented, "encyclopedia:", result.EncyclopediaEntryIds);
            AddRepresented(represented, "career:", result.CareerHintIds);
            AddRepresented(represented, "quest:", result.FollowUpQuestIds);
            AddRepresented(represented, "status:", result.FatigueStatusIds);

            foreach (string outcome in Values(result.OutcomeIds))
            {
                int separator = outcome.IndexOf(':');
                string category = separator > 0 && separator < outcome.Length - 1 ? outcome.Substring(0, separator) : string.Empty;
                if (category == "trait" || category == "skill") { growth++; continue; }
                if (!represented.Contains(outcome)) other++;
            }
            AddCount("exploration.effect.growth", growth, rewards, ref rewardFacts);
            AddCount("exploration.effect.other", other, rewards, ref rewardFacts);

            foreach (string value in Values(result.FatigueStatusIds))
            {
                int separator = value.LastIndexOf(':');
                string category = separator > 0 ? value.Substring(0, separator) : string.Empty;
                if ((category == "energy" || category == "focus" || category == "stress")
                    && int.TryParse(value.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
                {
                    if (amount != 0)
                    {
                        vitals.Add(Format("exploration.effect." + category, amount.ToString("+0;-0;0", CultureInfo.InvariantCulture)));
                        vitalFacts++;
                    }
                }
                else unknownStatuses++;
            }
            AddCount("exploration.effect.status", unknownStatuses, vitals, ref vitalFacts);

            var lines = new List<string> { status };
            if (rewards.Count > 0) lines.Add(compact ? Format("exploration.effect.other", rewardFacts) : string.Join(" · ", rewards));
            if (vitals.Count > 0) lines.Add(compact ? Format("exploration.effect.status", vitalFacts) : string.Join(" · ", vitals));
            return string.Join("\n", lines);
        }

        private static bool TryText(string key, out string value)
        {
            value = string.IsNullOrWhiteSpace(key) ? null : GameText.Get(key);
            return !string.IsNullOrWhiteSpace(value) && !string.Equals(value, key, StringComparison.Ordinal);
        }

        private static string Format(string key, params object[] values)
        {
            if (!TryText(key, out string template)) return Neutral;
            try { return string.Format(CultureInfo.CurrentCulture, template, values); }
            catch (FormatException) { return Neutral; }
        }

        private static IEnumerable<string> Values(string[] values)
            => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal);

        private static int Count(string[] values) => Values(values).Count();

        private static void AddRepresented(HashSet<string> target, string prefix, string[] ids)
        {
            foreach (string id in Values(ids)) target.Add(prefix + id);
        }

        private static bool TryItem(string value, out string id, out int count)
        {
            int separator = value.LastIndexOf(':');
            id = separator > 0 ? value.Substring(0, separator) : null;
            count = 0;
            return id != null && int.TryParse(value.Substring(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out count) && count > 0;
        }

        private static void AddCount(string key, int count, List<string> target, ref int factCount)
        {
            if (count <= 0) return;
            target.Add(Format(key, count));
            factCount += count;
        }

        private static void AddItems(IEnumerable<string> values, string key, List<string> target, ref int facts, ref int malformed)
        {
            foreach (string value in values)
            {
                if (!TryItem(value, out string id, out int count))
                {
                    malformed++;
                    continue;
                }
                target.Add(Format(key, ItemName(id), count));
                facts++;
            }
        }

        private static string ItemName(string id)
        {
            if (GameDataRegistryProvider.TryGet(out var registry) && registry != null)
            {
                ItemDefinition found = null;
                int matches = 0;
                foreach (var item in registry.Items ?? Array.Empty<ItemDefinition>())
                {
                    if (item == null || !string.Equals(item.Id, id, StringComparison.Ordinal)) continue;
                    found = item;
                    matches++;
                }
                if (matches == 1) return Text(found.DisplayKey, "exploration.effect.item");
            }
            return Text("exploration.effect.item", null);
        }
    }
}
