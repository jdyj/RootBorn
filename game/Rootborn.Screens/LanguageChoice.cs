using System;
using System.Collections.Generic;

namespace Rootborn.Screens
{
    /// <summary>언어를 어디서 골랐는지.</summary>
    public enum LanguageChoiceSource { Argument, Saved, OperatingSystem, Base }

    public readonly struct LanguageChoiceResult
    {
        public LanguageChoiceResult(string language, LanguageChoiceSource source)
        {
            Language = language ?? string.Empty;
            Source = source;
        }

        public string Language { get; }
        public LanguageChoiceSource Source { get; }
    }

    /// <summary>
    /// 언어 고르기 순서(.claude/rules/localization.md): 실행 인자 --lang → 저장된 설정 → 운영체제 언어 → 기준 언어.
    /// 언어 파일이 있는 언어(available, 보통 GameText.Languages)만 고른다. 대소문자는 가리지 않고, 고른 값은 available의 철자 그대로 준다.
    /// 운영체제 언어는 "en-US"·"zh_Hant_TW"처럼 지역이 붙어도 된다: 전체가 같은 파일이 먼저, 없으면 첫 부분(언어)만.
    /// 기준 언어 파일도 없으면 available의 첫 언어(사전 순). 순수 함수다 — 운영체제 언어와 설정은 부르는 쪽(Host)이 읽어 넘긴다.
    /// </summary>
    public static class LanguageChoice
    {
        public static LanguageChoiceResult Pick(string argument, string saved, string operatingSystem, IEnumerable<string> available,
            string baseLanguage = GameText.DefaultBaseLanguage)
        {
            var languages = new List<string>();
            if (available != null) foreach (string code in available) if (!string.IsNullOrEmpty(code)) languages.Add(code);

            if (TryMatch(argument, languages, out string chosen)) return new LanguageChoiceResult(chosen, LanguageChoiceSource.Argument);
            if (TryMatch(saved, languages, out chosen)) return new LanguageChoiceResult(chosen, LanguageChoiceSource.Saved);
            if (TryMatch(operatingSystem, languages, out chosen) || TryMatch(LanguagePart(operatingSystem), languages, out chosen))
                return new LanguageChoiceResult(chosen, LanguageChoiceSource.OperatingSystem);
            if (TryMatch(baseLanguage, languages, out chosen)) return new LanguageChoiceResult(chosen, LanguageChoiceSource.Base);
            languages.Sort(StringComparer.OrdinalIgnoreCase);
            return new LanguageChoiceResult(languages.Count > 0 ? languages[0] : baseLanguage, LanguageChoiceSource.Base);
        }

        private static bool TryMatch(string wanted, List<string> languages, out string chosen)
        {
            chosen = null;
            if (string.IsNullOrEmpty(wanted)) return false;
            string normalized = wanted.Replace('_', '-');
            foreach (string code in languages)
                if (string.Equals(code.Replace('_', '-'), normalized, StringComparison.OrdinalIgnoreCase)) { chosen = code; return true; }
            return false;
        }

        private static string LanguagePart(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            int cut = tag.IndexOfAny(new[] { '-', '_' });
            return cut > 0 ? tag.Substring(0, cut) : null;
        }
    }
}
