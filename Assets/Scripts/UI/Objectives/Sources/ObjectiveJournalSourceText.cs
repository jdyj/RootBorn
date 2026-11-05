using System.Collections.Generic;
using Rootborn.Game.Localization;

namespace Rootborn.UI.Objectives
{
    internal static class ObjectiveJournalSourceText
    {
        public static string SafeText(string primary, string fallback)
        {
            return GameText.Get(string.IsNullOrEmpty(primary) ? fallback : primary);
        }

        // Falls back to the supplied text when no catalog is loaded or the key is absent.
        public static string Localized(string key, string fallback)
        {
            string value = GameText.Get(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        public static string LocalizedFormat(string key, string fallbackFormat, object argument)
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, Localized(key, fallbackFormat), argument);
        }

        // Display layers translate once more so a key handed over by any source still reads as text. Plain text passes unchanged.
        public static string Shown(string value) => string.IsNullOrEmpty(value) ? string.Empty : GameText.Get(value);

        public static string ShownState(string value) => string.IsNullOrEmpty(value) ? string.Empty : Localized("ui.journal.state." + value, Shown(value));

        public static string State(string stateName) => Localized("ui.journal.state." + stateName, stateName);

        public static string ObjectiveLabel(string displayKey) => SafeText(displayKey, "ui.journal.objective.generic");

        public static string JoinNonEmpty(string[] values, string separator)
        {
            if (values == null || values.Length == 0)
            {
                return string.Empty;
            }

            var result = new List<string>();
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (!string.IsNullOrEmpty(value) && !result.Contains(value))
                {
                    result.Add(value);
                }
            }

            return string.Join(separator, result.ToArray());
        }
    }
}
