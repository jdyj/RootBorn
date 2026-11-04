using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Save;
using Rootborn.UI.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.Characters
{
    public sealed class GrowingWardrobeTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-wardrobe-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _objects) Object.DestroyImmediate(item);
            _objects.Clear();
            Directory.Delete(_root, true);
        }

        private GrowingCharacterSheetDefinition Sheet(string id)
        {
            var sheet = ScriptableObject.CreateInstance<GrowingCharacterSheetDefinition>();
            _objects.Add(sheet);
            sheet.ConfigureForTests(id, new Sprite[GrowingCharacterSheetDefinition.FrameCount]);
            return sheet;
        }

        private GrowingCharacterAppearanceDefinition Look(string id, GrowingCharacterSheetDefinition body, GrowingCharacterSheetDefinition head, int day = 1, bool adult = false, string facility = "")
        {
            var look = ScriptableObject.CreateInstance<GrowingCharacterAppearanceDefinition>();
            _objects.Add(look);
            look.ConfigureForTests(id, body, head, Sheet(id + "-outfit"));
            look.ConfigureRulesForTests(day, adult, facility);
            return look;
        }

        [Test]
        public void GROW_WARDROBE_001_ChoiceSurvivesSaveAndLoad()
        {
            var storage = new SaveService("slot-a", _root);
            var store = new GrowingWardrobeStore();
            store.SetChoice("body_school", "school_casual");
            Assert.That(store.Save(storage, "player-1"), Is.True);
            var loaded = GrowingWardrobeStore.Load(storage, "player-1");
            Assert.That(loaded.GetChoice("body_school"), Is.EqualTo("school_casual"));
        }

        [Test]
        public void GROW_WARDROBE_002_PlayersKeepSeparateChoices()
        {
            var storage = new SaveService("slot-a", _root);
            var store = new GrowingWardrobeStore();
            store.SetChoice("body_school", "a");
            store.Save(storage, "player-1");
            Assert.That(GrowingWardrobeStore.Load(storage, "player-2").GetChoice("body_school"), Is.Null);
        }

        [Test]
        public void GROW_WARDROBE_003_CorruptFileLoadsEmptyWithoutThrowing()
        {
            var storage = new SaveService("slot-a", _root);
            storage.WriteJson(GrowingWardrobeStore.FileNameFor("player-1"), "{not json");
            Assert.That(GrowingWardrobeStore.Load(storage, "player-1").GetChoice("body_school"), Is.Null);
        }

        [Test]
        public void GROW_WARDROBE_004_ChoosingAgainReplacesThePreviousChoice()
        {
            var store = new GrowingWardrobeStore();
            store.SetChoice("body", "a");
            store.SetChoice("body", "b");
            Assert.That(store.GetChoice("body"), Is.EqualTo("b"));
        }

        [Test]
        public void GROW_WARDROBE_005_OptionsAreLooksThatShareTheSameBodyAndHead()
        {
            var body = Sheet("body");
            var head = Sheet("head");
            var a = Look("a", body, head);
            var b = Look("b", body, head, 1, true, "facility.chef");
            var other = Look("other", Sheet("taller"), head);
            var options = GrowingCharacterStageSelector.Options(new CharacterAppearanceDefinition[] { a, other, null, b }, a);
            Assert.That(options, Is.EqualTo(new[] { a, b }));
        }

        [Test]
        public void GROW_WARDROBE_006_ChoiceOverridesDefaultOnlyWhenItFitsThisBody()
        {
            var body = Sheet("body");
            var head = Sheet("head");
            var school = Look("school", body, head);
            var casual = Look("casual", body, head);
            var other = Look("other", Sheet("taller"), head);
            var all = new CharacterAppearanceDefinition[] { school, casual, other };
            Assert.That(GrowingCharacterStageSelector.ApplyChoice(all, school, "casual"), Is.EqualTo(casual));
            Assert.That(GrowingCharacterStageSelector.ApplyChoice(all, school, "other"), Is.EqualTo(school), "another stage's outfit must not fit");
            Assert.That(GrowingCharacterStageSelector.ApplyChoice(all, school, "missing"), Is.EqualTo(school));
            Assert.That(GrowingCharacterStageSelector.ApplyChoice(all, school, null), Is.EqualTo(school));
        }

        [Test]
        public void GROW_WARDROBE_009_LockedFileLoadsEmptyInsteadOfThrowing()
        {
            var storage = new SaveService("slot-a", _root);
            string path = Path.Combine(storage.DirectoryPath, GrowingWardrobeStore.FileNameFor("player-1"));
            File.WriteAllText(path, "{}");
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.That(GrowingWardrobeStore.Load(storage, "player-1").GetChoice("body"), Is.Null);
            }
        }

        [Test]
        public void GROW_WARDROBE_010_RemoteDriverNeverChangesItsLook()
        {
            var body = Sheet("body");
            var head = Sheet("head");
            var a = Look("a", body, head);
            var b = Look("b", body, head);
            var registry = ScriptableObject.CreateInstance<Rootborn.Game.Common.GameDataRegistry>();
            _objects.Add(registry);
            typeof(Rootborn.Game.Common.GameDataRegistry).GetField("_characterAppearances", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(registry, new CharacterAppearanceDefinition[] { a, b });
            var player = new GameObject("player");
            _objects.Add(player);
            var view = player.AddComponent<GrowingCharacterVisualView>();
            view.Apply(b);
            view.IsRemote = true;
            var driver = player.AddComponent<GrowingCharacterStageDriver>();
            driver.Configure(view, registry);
            Assert.That(driver.IsLocalPlayer, Is.False);
            driver.Apply(1, false, null);
            Assert.That(view.Appearance, Is.EqualTo(b));
            driver.ChooseOutfit("a");
            Assert.That(view.Appearance, Is.EqualTo(b));
        }

        [Test]
        public void GROW_INSTALL_005_ReinstallKeepsTheCurrentLook()
        {
            var body = Sheet("body");
            var head = Sheet("head");
            var first = Look("first", body, head);
            var second = Look("second", body, head);
            var registry = ScriptableObject.CreateInstance<Rootborn.Game.Common.GameDataRegistry>();
            _objects.Add(registry);
            typeof(Rootborn.Game.Common.GameDataRegistry).GetField("_characterAppearances", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(registry, new CharacterAppearanceDefinition[] { first, second });
            var player = new GameObject("player");
            _objects.Add(player);
            GrowingCharacterRuntimeInstaller.TryInstall(player, registry);
            var view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
            view.Apply(second);
            GrowingCharacterRuntimeInstaller.TryInstall(player, registry);
            Assert.That(view.Appearance, Is.EqualTo(second));
        }

        [Test]
        public void GROW_WARDROBE_007_PanelShowsOneCardPerOptionAndReportsClicks()
        {
            var body = Sheet("body");
            var head = Sheet("head");
            var a = Look("a", body, head);
            var b = Look("b", body, head);
            var host = new GameObject("panel", typeof(RectTransform));
            _objects.Add(host);
            var panel = host.AddComponent<GrowingWardrobePanel>();
            string chosen = null;
            panel.Show(new[] { a, b }, a, id => chosen = id);
            var buttons = host.GetComponentsInChildren<Button>(true);
            var card = System.Array.Find(buttons, candidate => candidate.name == "Outfit_b");
            Assert.That(card, Is.Not.Null);
            card.onClick.Invoke();
            Assert.That(chosen, Is.EqualTo("b"));
            Assert.That(panel.IsVisible, Is.True);
            panel.Hide();
            Assert.That(panel.IsVisible, Is.False);
        }

        [Test]
        public void GROW_WARDROBE_008_ReopeningWithFewerOptionsHidesOldCards()
        {
            var body = Sheet("body");
            var head = Sheet("head");
            var a = Look("a", body, head);
            var b = Look("b", body, head);
            var host = new GameObject("panel", typeof(RectTransform));
            _objects.Add(host);
            var panel = host.AddComponent<GrowingWardrobePanel>();
            panel.Show(new[] { a, b }, a, null);
            panel.Show(new[] { a }, a, null);
            Assert.That(panel.VisibleCardCount, Is.EqualTo(1));
        }
    }
}
