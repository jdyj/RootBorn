using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.Spum
{
    public sealed class SpumCharacterCreatorLayoutTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private GameObject _host;
        private SpumCharacterCreatorPanel _panel;
        private SpumPartCatalogDefinition _catalog;
        private string _language;
        [SetUp] public void SetUp()
        {
            _language = GameText.Language;
            _host = Own(new GameObject("CreatorLayoutHost", typeof(RectTransform)));
            ((RectTransform)_host.transform).sizeDelta = new Vector2(1920, 1080);
            _panel = _host.AddComponent<SpumCharacterCreatorPanel>();
            var parts = new List<SpumPartDefinition>();
            foreach (string category in new[] { "body", "skin", "eye", "outfit", "accessory", "weapon" }) parts.Add(Part("spum." + category + ".default", category));
            for (int i = 0; i < 14; i++) parts.Add(Part("spum.hair." + i, "hair"));
            _catalog = Own(ScriptableObject.CreateInstance<SpumPartCatalogDefinition>());
            _catalog.ConfigureForTests("spum.layout.test", parts.ToArray());
        }
        [TearDown] public void TearDown()
        {
            GameDataRegistryProvider.ClearOverrideForTests(); GameDataRegistryProvider.ResetCacheForTests(); GameText.Language = _language;
            // Dispose UI first: preview children may refer to catalog parts.
            if (_host != null) Object.DestroyImmediate(_host);
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        [Test] public void ModalHasOpaqueRaycastSurfaceCoveringWholeHost()
        {
            _panel.Build(_catalog);
            Rect host = Bounds((RectTransform)_host.transform);
            Assert.That(_host.GetComponentsInChildren<Image>(true).Any(image => image.gameObject.activeInHierarchy && image.raycastTarget
                && image.color.a >= 0.99f && Contains(Bounds(image.rectTransform), host)), Is.True,
                "Creator needs an opaque full-host input blocker so save-slot controls cannot show or receive pointer input beneath it.");
        }

        [Test] public void RootStyle2TilesCoverTheFinalRootSizeAfterBuild()
        {
            _panel.Build(_catalog);
            var root = Root(); var chrome = root.GetComponent<ModernUiTileImage>();
            Assert.That(chrome, Is.Not.Null); CollectionAssert.AreEqual(ModernUiRecipes.CommonPanel.Tiles, chrome.Recipe.Tiles);
            var tiles = root.Cast<Transform>().Where(x => x.name.StartsWith("Tile_", StringComparison.Ordinal)).Select(x => Bounds((RectTransform)x)).ToArray();
            Assert.That(tiles, Is.Not.Empty);
            Rect covered = Rect.MinMaxRect(tiles.Min(x => x.xMin), tiles.Min(x => x.yMin), tiles.Max(x => x.xMax), tiles.Max(x => x.yMax));
            Assert.That(Contains(covered, Bounds(root)), Is.True,
                "Adding ModernUiTileImage before sizing currently leaves a tiny initial tile patch. Build must render final bounds without test-side Rebuild.");
        }

        [Test] public void PartGridAndPreviewHaveSeparateVisibleBounds()
        {
            _panel.Build(_catalog);
            Assert.That(Overlaps(At("PartGrid"), At("Preview")), Is.False, "Part choices may not cover the character preview.");
            Assert.That(Overlaps(At("PartGrid"), At("CategoryTabs")), Is.False);
            Assert.That(Overlaps(At("Preview"), At("CategoryTabs")), Is.False);
        }

        [Test] public void MajorRegionsStayInsideCreatorRoot()
        {
            _panel.Build(_catalog);
            foreach (string name in new[] { "Header", "CategoryTabs", "PartGrid", "Preview", "PageControls", "Footer" })
                Assert.That(Contains(Bounds(Root()), Bounds(At(name))), Is.True, name + " extends beyond the creator panel.");
        }

        [Test] public void PaginationAndFooterHaveSeparateBounds()
        {
            _panel.Build(_catalog);
            Assert.That(Overlaps(At("PageControls"), At("Footer")), Is.False, "Pagination, cancellation and selected-part summary need separate rows.");
        }

        [Test] public void HeaderButtonsRemainInsideTheirHeaderRegion()
        {
            _panel.Build(_catalog);
            Rect header = Bounds(At("Header"));
            foreach (var button in At("Header").GetComponentsInChildren<Button>(true))
                Assert.That(Contains(header, Bounds((RectTransform)button.transform)), Is.True, button.name);
        }

        [Test] public void SelectedPartChipsDoNotCoverStatusText()
        {
            _panel.Build(_catalog);
            var status = At("Footer/StatusLine"); var footer = At("Footer");
            var chips = At("Footer/SelectedPartChips").GetComponentsInChildren<Button>(true);
            Assert.That(chips.Length, Is.GreaterThanOrEqualTo(6), "Exercise the full existing six-chip budget.");
            foreach (var chip in chips)
            {
                var rect = (RectTransform)chip.transform;
                Assert.That(Overlaps(rect, status), Is.False, chip.name + " covers status text.");
                Assert.That(Contains(Bounds(footer), Bounds(rect)), Is.True, chip.name + " exceeds its footer.");
            }
        }

        [Test] public void PartCellsRemainBoundedAcrossBothPages()
        {
            _panel.Build(_catalog); _panel.SelectCategory("hair");
            for (int page = 0; page < 2; page++)
            {
                var grid = At("PartGrid");
                var cells = grid.Cast<Transform>().Where(x => x.name.StartsWith("PartCell_", StringComparison.Ordinal)).ToArray();
                Assert.That(cells.Length, Is.GreaterThan(0).And.LessThanOrEqualTo(SpumCharacterCreatorPanel.VisiblePartCellBudget));
                foreach (var cell in cells) Assert.That(Contains(Bounds(grid), Bounds((RectTransform)cell)), Is.True, cell.name);
                for (int i = 0; i < cells.Length; i++) for (int j = i + 1; j < cells.Length; j++)
                    Assert.That(Overlaps((RectTransform)cells[i], (RectTransform)cells[j]), Is.False);
                _panel.NextPage();
            }
        }

        [Test] public void SelectingPartThenConfirmPreservesStableSnapshotAndCallsOnce()
        {
            CharacterAppearanceSnapshot confirmed = null; int calls = 0;
            _panel.Build(_catalog, snapshot => { confirmed = snapshot; calls++; }, () => Assert.Fail("Confirm must not cancel."));
            _panel.SelectCategory("hair");
            At("PartGrid/PartCell_spum_hair_1").GetComponent<Button>().onClick.Invoke();
            At("Header/ConfirmButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1)); Assert.That(confirmed, Is.Not.Null);
            Assert.That(confirmed.GetSelectedPartId("hair"), Is.EqualTo("spum.hair.1"));
            Assert.That(confirmed.VisualKind, Is.EqualTo("spum")); Assert.That(confirmed.CatalogId, Is.EqualTo(_catalog.Id));
        }

        [TestCase("ko", "내 캐릭터", "결정", "머리", "7개 선택")]
        [TestCase("en", "My character", "Accept", "Hair choices", "7 selected")]
        public void CreatorLabelsAndSelectionStatusUseCurrentLanguage(string language, string title, string confirm, string hair, string status)
        {
            var registry = Own(ScriptableObject.CreateInstance<GameDataRegistry>());
            var text = Own(ScriptableObject.CreateInstance<GameTextCatalog>());
            text.ConfigureJson("{\"entries\":[{\"key\":\"spum.creator.title\",\"ko\":\"내 캐릭터\",\"en\":\"My character\"},{\"key\":\"spum.creator.confirm\",\"ko\":\"결정\",\"en\":\"Accept\"},{\"key\":\"spum.creator.category.hair\",\"ko\":\"머리\",\"en\":\"Hair choices\"},{\"key\":\"spum.creator.selected-count\",\"ko\":\"{0}개 선택\",\"en\":\"{0} selected\"}]}");
            typeof(GameDataRegistry).GetField("_textCatalog", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(registry, text);
            GameDataRegistryProvider.SetOverrideForTests(registry); GameText.Language = language;
            _panel.Build(_catalog);
            Assert.That(At("Header/Title").GetComponent<Text>().text, Is.EqualTo(title));
            Assert.That(At("Header/ConfirmButton/Label").GetComponent<Text>().text, Is.EqualTo(confirm));
            Assert.That(At("CategoryTabs/Tab_Hair/Label").GetComponent<Text>().text, Is.EqualTo(hair));
            Assert.That(At("Footer/StatusLine").GetComponent<Text>().text, Is.EqualTo(status));
        }

        private RectTransform Root() => (RectTransform)_host.transform.Find("SpumCharacterCreatorRoot");
        private RectTransform At(string path) { var value = Root().Find(path) as RectTransform; Assert.That(value, Is.Not.Null, path); return value; }
        private SpumPartDefinition Part(string id, string category) { var part = Own(ScriptableObject.CreateInstance<SpumPartDefinition>()); part.ConfigureForTests(id, category, false); return part; }
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(x => x.x), corners.Min(x => x.y), corners.Max(x => x.x), corners.Max(x => x.y));
        }
        private static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 0.1f && inner.xMax <= outer.xMax + 0.1f
            && inner.yMin >= outer.yMin - 0.1f && inner.yMax <= outer.yMax + 0.1f;
        private static bool Overlaps(RectTransform a, RectTransform b)
        {
            Rect left = Bounds(a), right = Bounds(b);
            return Mathf.Min(left.xMax, right.xMax) - Mathf.Max(left.xMin, right.xMin) > 0.1f
                && Mathf.Min(left.yMax, right.yMax) - Mathf.Max(left.yMin, right.yMin) > 0.1f;
        }
    }
}