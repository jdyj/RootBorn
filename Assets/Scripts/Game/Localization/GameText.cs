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
        public static string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), args ?? Array.Empty<object>());
        }
    }
}
