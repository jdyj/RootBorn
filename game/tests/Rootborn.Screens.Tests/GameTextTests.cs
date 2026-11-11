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
        public void ExtraFolder_AddsKeys_AndWinsOverTheFirstFolder()
        {
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rootborn-text-" + System.Guid.NewGuid().ToString("N"));
            string first = System.IO.Path.Combine(root, "first"), extra = System.IO.Path.Combine(root, "extra");
            System.IO.Directory.CreateDirectory(first);
            System.IO.Directory.CreateDirectory(extra);
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(first, "ko.json"), "{\"a\":\"처음\",\"b\":\"그대로\"}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(extra, "ko.json"), "{\"a\":\"고침\",\"c\":\"더함\"}");

                GameText text = GameText.LoadMerged("ko", first, extra, System.IO.Path.Combine(root, "missing"));

                Assert.That(text.Get("a"), Is.EqualTo("고침"));
                Assert.That(text.Get("b"), Is.EqualTo("그대로"));
                Assert.That(text.Get("c"), Is.EqualTo("더함"));
                Assert.That(() => GameText.LoadMerged("ko", System.IO.Path.Combine(root, "missing")), Throws.InstanceOf<System.IO.DirectoryNotFoundException>());
            }
            finally { System.IO.Directory.Delete(root, true); }
        }

        [Test]
        public void RealExtraFolder_HasTheSameKeysInBothLanguages()
        {
            var ko = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(System.IO.File.ReadAllText(System.IO.Path.Combine(TestData.ExtraLocalizationDirectory, "ko.json")));
            var en = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(System.IO.File.ReadAllText(System.IO.Path.Combine(TestData.ExtraLocalizationDirectory, "en.json")));
            Assert.That(ko.Keys.OrderBy(k => k), Is.EqualTo(en.Keys.OrderBy(k => k)));
            Assert.That(TestData.Text().Get("ui.hud.energy"), Is.EqualTo("체력"));
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
