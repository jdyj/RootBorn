using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Screens.Tests
{
    public sealed class GameTextTests
    {
        [Test]
        public void RealFiles_LoadBothLanguages_WithTheSameKeys()
        {
            GameText text = TestData.Text();
            Assert.That(text.Languages.OrderBy(l => l), Is.EqualTo(new[] { "en", "ko" }));
            Assert.That(text.Get("ui.status.title"), Is.Not.EqualTo("ui.status.title"));
            Assert.That(text.MissingKeys, Is.Empty);
        }

        [Test]
        public void LanguageSwitch_ChangesTheText_AndRaisesOnce()
        {
            GameText text = TestData.Text();
            string ko = text.Get("career.chef");
            int raised = 0;
            text.LanguageChanged += () => raised++;
            text.Language = "en";
            Assert.That(ko, Is.EqualTo("요리사"));
            Assert.That(text.Get("career.chef"), Is.Not.EqualTo(ko));
            text.Language = "en";
            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void MissingKey_ReturnsTheKey_AndIsRecordedOnce()
        {
            GameText text = TestData.Text();
            Assert.That(text.Get("no.such.key"), Is.EqualTo("no.such.key"));
            text.Get("no.such.key");
            Assert.That(text.MissingKeys, Is.EqualTo(new[] { "ko:no.such.key" }));
        }

        [Test]
        public void Format_FillsArguments()
        {
            var text = new GameText(new Dictionary<string, Dictionary<string, string>>
            {
                ["ko"] = new Dictionary<string, string> { ["a"] = "{0}일 {1}시" },
            }, "ko");
            Assert.That(text.Format("a", 3, 8), Is.EqualTo("3일 8시"));
        }

        [Test]
        public void UnknownLanguage_Throws()
        {
            GameText text = TestData.Text();
            Assert.That(() => text.Language = "fr", Throws.ArgumentException);
        }
    }
}
