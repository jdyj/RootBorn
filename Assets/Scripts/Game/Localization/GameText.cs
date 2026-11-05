using System;
using System.Globalization;
using UnityEngine;

namespace Rootborn.Game.Localization
{
    public static class GameText
    {
        private static string _language;
        public static string Language
        {
            get => _language ?? (Application.systemLanguage == SystemLanguage.Korean ? "ko" : "en");
            set => _language = value == "ko" ? "ko" : "en";
        }
        public static string Get(string key)
        {
            Rootborn.Game.Common.GameDataRegistryProvider.TryGet(out var registry);
            return registry != null && registry.TextCatalog != null ? registry.TextCatalog.Get(key, Language) : key ?? string.Empty;
        }
        // Text with an English fallback, for callers that may run before a catalog is available.
        public static string Localized(string key, string fallback)
        {
            string value = Get(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }
        public static string FormatLocalized(string key, string fallbackFormat, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Localized(key, fallbackFormat), args ?? Array.Empty<object>());
        }
        public static string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), args ?? Array.Empty<object>());
        }
    }
}
