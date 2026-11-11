using NUnit.Framework;

namespace Rootborn.Screens.Tests
{
    /// <summary>언어 고르기 순서: 실행 인자 → 저장된 설정 → 운영체제 언어 → 기준 언어. 파일이 있는 언어만 고른다. 자동 테스트다.</summary>
    public sealed class LanguageChoiceTests
    {
        private static readonly string[] Available = { "ko", "en" };

        [Test]
        public void Argument_WinsOverEverything()
        {
            var choice = LanguageChoice.Pick("en", "ko", "ko-KR", Available);
            Assert.That(choice.Language, Is.EqualTo("en"));
            Assert.That(choice.Source, Is.EqualTo(LanguageChoiceSource.Argument));
        }

        [Test]
        public void SavedSetting_IsNext_WhenThereIsNoUsableArgument()
        {
            Assert.That(LanguageChoice.Pick(null, "en", "ko-KR", Available).Language, Is.EqualTo("en"));
            var unknownArgument = LanguageChoice.Pick("fr", "en", "ko-KR", Available);
            Assert.That(unknownArgument.Language, Is.EqualTo("en"), "파일이 없는 언어는 건너뛴다");
            Assert.That(unknownArgument.Source, Is.EqualTo(LanguageChoiceSource.Saved));
        }

        [Test]
        public void OperatingSystemLanguage_IsNext_MatchingTheRegionOrTheLanguage()
        {
            var os = LanguageChoice.Pick("", "", "en-US", Available);
            Assert.That(os.Language, Is.EqualTo("en"), "지역 붙은 이름은 언어 부분으로");
            Assert.That(os.Source, Is.EqualTo(LanguageChoiceSource.OperatingSystem));
            Assert.That(LanguageChoice.Pick(null, null, "pt-BR", new[] { "ko", "pt-br", "pt" }).Language, Is.EqualTo("pt-br"), "지역까지 같은 파일이 먼저");
            Assert.That(LanguageChoice.Pick(null, null, "zh_Hant_TW", new[] { "ko", "zh" }).Language, Is.EqualTo("zh"), "밑줄 구분도 안다");
        }

        [Test]
        public void BaseLanguage_IsTheLast()
        {
            var fallback = LanguageChoice.Pick("fr", "de", "ja-JP", Available);
            Assert.That(fallback.Language, Is.EqualTo(GameText.DefaultBaseLanguage));
            Assert.That(fallback.Source, Is.EqualTo(LanguageChoiceSource.Base));
            Assert.That(LanguageChoice.Pick(null, null, null, Available).Language, Is.EqualTo(GameText.DefaultBaseLanguage));
        }

        [Test]
        public void CaseIsIgnored_AndTheAvailableSpellingIsReturned()
        {
            Assert.That(LanguageChoice.Pick("EN", null, null, new[] { "ko", "En" }).Language, Is.EqualTo("En"));
        }

        [Test]
        public void TheRealFolders_OfferEveryLanguageTheyHave()
        {
            GameText text = TestData.Text();
            foreach (string language in text.Languages)
                Assert.That(LanguageChoice.Pick(language, null, null, text.Languages).Language, Is.EqualTo(language));
        }
    }
}
