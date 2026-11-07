using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Growing;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI.Spum
{
    // The new-game creator offers the growing character's options: name, hair, and a starting outfit per growth stage.
    public sealed class SpumCharacterCreatorPanelTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private GameObject _host;
        private SpumCharacterCreatorPanel _panel;
        private GrowingCharacterSheetDefinition _childBody, _schoolBody, _adultBody;
        private CharacterAppearanceDefinition[] _looks;

        [SetUp]
        public void SetUp()
        {
            _childBody = Sheet("body_child");
            _schoolBody = Sheet("body_school");
            _adultBody = Sheet("body_adult");
            // Deliberately out of growth order; the creator sorts by the rules on each asset.
            _looks = new CharacterAppearanceDefinition[]
            {
                Look("adult_casual", _adultBody, 1, true, null),
                Look("school_casual", _schoolBody, 2, false, null),
                Look("adult_chef", _adultBody, 1, true, "facility.chef"),
                Look("child_casual", _childBody, 1, false, null),
                Look("school_uniform", _schoolBody, 2, false, null),
                Look("adult_office", _adultBody, 1, true, null),
            };
            _host = Own(new GameObject("CreatorHost", typeof(RectTransform)));
            ((RectTransform)_host.transform).sizeDelta = new Vector2(1920f, 1080f);
            _panel = _host.AddComponent<SpumCharacterCreatorPanel>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        [Test]
        public void CREATOR_EDIT_001_GroupsOutfitsByBodyInGrowthOrderAndLeavesOutFacilityUniforms()
        {
            var stages = GrowingCharacterStageSelector.CreatorStages(_looks);
            Assert.That(stages.Select(s => s[0].Body), Is.EqualTo(new[] { _childBody, _schoolBody, _adultBody }));
            Assert.That(stages[1].Select(l => l.Id), Is.EqualTo(new[] { "school_casual", "school_uniform" }));
            Assert.That(stages[2].Select(l => l.Id), Is.EqualTo(new[] { "adult_casual", "adult_office" }), "Facility uniforms are earned at work, not picked here.");
        }

        [Test]
        public void CREATOR_EDIT_002_SingleOptionRowsHideTheirArrows()
        {
            _panel.Build(_looks, string.Empty, null, null);
            Assert.That(_panel.StageCount, Is.EqualTo(3));
            Assert.That(Find("Stage0_Next"), Is.Null, "A stage with one outfit must not show arrows that do nothing.");
            Assert.That(Find("Hair_Next"), Is.Null);
            Assert.That(Find("Stage1_Next").activeSelf, Is.True);
            Assert.That(Find("Stage2_Previous").activeSelf, Is.True);
        }

        [Test]
        public void CREATOR_EDIT_003_ArrowsChangeOnlyThatStageAndWrapAround()
        {
            _panel.Build(_looks, string.Empty, null, null);
            Click("Stage1_Next");
            Assert.That(_panel.ChoiceAt(1).Id, Is.EqualTo("school_uniform"));
            Assert.That(_panel.ChoiceAt(2).Id, Is.EqualTo("adult_casual"));
            Assert.That(_panel.PreviewStage, Is.EqualTo(1));
            Click("Stage1_Next");
            Assert.That(_panel.ChoiceAt(1).Id, Is.EqualTo("school_casual"), "Stepping past the last outfit wraps to the first.");
            Click("Stage2_Previous");
            Assert.That(_panel.ChoiceAt(2).Id, Is.EqualTo("adult_office"));
            Assert.That(Find("Stage2_Choice").GetComponent<Text>().text, Is.EqualTo("adult_office"), "The row shows the chosen outfit (id when no text exists).");
        }

        [Test]
        public void CREATOR_EDIT_004_ConfirmSendsOneGrowingSnapshotWithAnOutfitPerBodyAndTheTrimmedName()
        {
            CharacterAppearanceSnapshot confirmed = null; string name = null; int calls = 0;
            _panel.Build(_looks, string.Empty, (snapshot, playerName) => { confirmed = snapshot; name = playerName; calls++; }, () => Assert.Fail("Confirm must not cancel."));
            Click("Stage1_Next");
            Click("Stage2_Next");
            Find("NameField").GetComponent<InputField>().text = "   Haru   ";
            Click("ConfirmButton");

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(name, Is.EqualTo("Haru"));
            Assert.That(confirmed.VisualKind, Is.EqualTo(GrowingCharacterAppearanceDefinition.VisualKindName));
            Assert.That(GrowingCharacterStageSelector.ChoiceFromSnapshot(confirmed, "body_child"), Is.EqualTo("child_casual"));
            Assert.That(GrowingCharacterStageSelector.ChoiceFromSnapshot(confirmed, "body_school"), Is.EqualTo("school_uniform"));
            Assert.That(GrowingCharacterStageSelector.ChoiceFromSnapshot(confirmed, "body_adult"), Is.EqualTo("adult_office"));
        }

        [Test]
        public void CREATOR_EDIT_005_NameIsLimitedAndBlankNameStaysBlank()
        {
            _panel.Build(_looks, string.Empty, null, null);
            var field = Find("NameField").GetComponent<InputField>();
            Assert.That(field.characterLimit, Is.EqualTo(SpumCharacterCreatorPanel.MaxNameLength));
            field.text = "   ";
            Assert.That(_panel.PlayerName, Is.Empty, "A blank name keeps the save's default name.");
        }

        [Test]
        public void CREATOR_EDIT_006_SnapshotFromAnotherCharacterKindIsIgnored()
        {
            var spum = new CharacterAppearanceSnapshot(1, "spum", "x", "c", new[] { new CharacterAppearancePartSelection("outfit:body_school", "school_uniform") });
            Assert.That(GrowingCharacterStageSelector.ChoiceFromSnapshot(spum, "body_school"), Is.Empty);
            Assert.That(GrowingCharacterStageSelector.ChoiceFromSnapshot(null, "body_school"), Is.Empty);
        }

        [Test]
        public void CREATOR_EDIT_007_RandomPicksOnlyOfferedOutfits()
        {
            _panel.Build(_looks, string.Empty, null, null);
            var stages = GrowingCharacterStageSelector.CreatorStages(_looks);
            for (int seed = 0; seed < 20; seed++)
            {
                _panel.RandomizeSelection(seed);
                for (int i = 0; i < stages.Count; i++) Assert.That(stages[i], Does.Contain(_panel.ChoiceAt(i)));
            }
        }

        [Test]
        public void CREATOR_EDIT_008_RootUsesStyle2PanelAndRegionsDoNotOverlap()
        {
            _panel.Build(_looks, string.Empty, null, null);
            var root = _host.transform.Find("SpumCharacterCreatorRoot");
            var tiles = root.GetComponent<ModernUiTileImage>();
            Assert.That(tiles, Is.Not.Null);
            CollectionAssert.AreEqual(ModernUiRecipes.CommonPanel.Tiles, _panel.RootPanelRecipe.Tiles);
            var regions = new[] { "Header", "Preview", "Choices", "Footer" }.Select(n => Bounds((RectTransform)root.Find(n))).ToArray();
            var rootBounds = Bounds((RectTransform)root);
            for (int i = 0; i < regions.Length; i++)
            {
                Assert.That(rootBounds.Contains(regions[i].min) && rootBounds.Contains(regions[i].max), Is.True, "region " + i + " leaves the panel");
                for (int j = i + 1; j < regions.Length; j++) Assert.That(regions[i].Overlaps(regions[j]), Is.False, i + " overlaps " + j);
            }

            foreach (var text in root.GetComponentsInChildren<Text>(true)) Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(18), text.name);
        }

        private GameObject Find(string name)
        {
            return _host.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject).FirstOrDefault(g => g.name == name && g.activeSelf);
        }

        private void Click(string name)
        {
            var go = Find(name);
            Assert.That(go, Is.Not.Null, name);
            go.GetComponent<Button>().onClick.Invoke();
        }

        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private GrowingCharacterSheetDefinition Sheet(string id)
        {
            var sheet = Own(ScriptableObject.CreateInstance<GrowingCharacterSheetDefinition>());
            sheet.ConfigureForTests(id, new Sprite[GrowingCharacterSheetDefinition.FrameCount]);
            return sheet;
        }

        private GrowingCharacterAppearanceDefinition Look(string id, GrowingCharacterSheetDefinition body, int day, bool adult, string facility)
        {
            var look = Own(ScriptableObject.CreateInstance<GrowingCharacterAppearanceDefinition>());
            look.ConfigureForTests(id, body, Sheet("head_" + id), Sheet("outfit_" + id));
            look.ConfigureRulesForTests(day, adult, facility);
            return look;
        }

        private T Own<T>(T value) where T : Object
        {
            _owned.Add(value);
            return value;
        }
    }
}
