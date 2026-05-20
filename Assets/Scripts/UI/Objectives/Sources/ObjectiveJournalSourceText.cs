using System.Collections.Generic;

namespace Rootborn.UI.Objectives
{
    internal static class ObjectiveJournalSourceText
    {
        public static string SafeText(string primary, string fallback)
        {
            return string.IsNullOrEmpty(primary) ? fallback : primary;
        }

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
