using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.UI.MainMenu;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.Spum
{
    public sealed class SpumCharacterCreatorPanelTests
    {
        [Test]
        public void SPUM_CREATOR_PANEL_001_BuildsExpectedCategoryTabsFromCatalogParts()
        {
            var host = new GameObject("CreatorHost", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<SpumCharacterCreatorPanel>();
                SpumPartCatalogDefinition catalog = CreateCatalog(
                    Part("spum.body.default", "body"),
                    Part("spum.skin.default", "skin"),
                    Part("spum.eye.default", "eye"),
                    Part("spum.hair.default", "hair"),
                    Part("spum.outfit.default", "outfit"),
                    Part("spum.cloth.default", "cloth"),
                    Part("spum.accessory.default", "accessory"),
                    Part("spum.back.default", "back"),
                    Part("spum.helmet.default", "helmet"),
                    Part("spum.weapon.default", "weapon"));

                panel.Build(catalog);

                AssertTab(host, "Tab_BodySkin");
                AssertTab(host, "Tab_Eye");
                AssertTab(host, "Tab_Hair");
                AssertTab(host, "Tab_OutfitCloth");
                AssertTab(host, "Tab_AccessoryBackHelmet");
                Assert.IsNotNull(host.transform.Find("SpumCharacterCreatorRoot/Preview/WeaponPreview"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SPUM_CREATOR_PANEL_002_PagesPartGridWhenVisibleCellBudgetIsExceeded()
        {
            var host = new GameObject("CreatorHost", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<SpumCharacterCreatorPanel>();
                SpumPartDefinition[] parts = Enumerable.Range(0, 14)
                    .Select(index => Part("spum.hair." + index, "hair"))
                    .ToArray();

                panel.Build(CreateCatalog(parts));
                panel.SelectCategory("hair");

                Transform grid = host.transform.Find("SpumCharacterCreatorRoot/PartGrid");
                Assert.IsNotNull(grid);
                Assert.LessOrEqual(grid.childCount, SpumCharacterCreatorPanel.VisiblePartCellBudget);
                Assert.IsTrue(host.transform.Find("SpumCharacterCreatorRoot/PageControls/NextPageButton").gameObject.activeSelf);
                Assert.AreEqual(0, panel.CurrentPageIndex);

                panel.NextPage();

                Assert.AreEqual(1, panel.CurrentPageIndex);
                Assert.Greater(grid.childCount, 0);
                Assert.LessOrEqual(grid.childCount, SpumCharacterCreatorPanel.VisiblePartCellBudget);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SPUM_CREATOR_PANEL_003_BuildsHeaderFooterAndPartStepControls()
        {
            var host = new GameObject("CreatorHost", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<SpumCharacterCreatorPanel>();
                panel.Build(CreateCatalog(
                    Part("spum.hair.short", "hair"),
                    Part("spum.hair.long", "hair"),
                    Part("spum.body.default", "body")));

                Assert.IsNotNull(host.transform.Find("SpumCharacterCreatorRoot/Header/Title"));
                Assert.IsNotNull(host.transform.Find("SpumCharacterCreatorRoot/Header/BackButton"));
                Assert.IsNotNull(host.transform.Find("SpumCharacterCreatorRoot/Header/RandomButton"));
                Assert.IsNotNull(host.transform.Find("SpumCharacterCreatorRoot/Header/ConfirmButton"));
                Assert.IsNotNull(host.transform.Find("SpumCharacterCreatorRoot/PageControls/PreviousPartButton"));
                Assert.IsNotNull(host.transform.Find("SpumCharacterCreatorRoot/PageControls/NextPartButton"));
                Assert.IsNotNull(host.transform.Find("SpumCharacterCreatorRoot/Footer/SelectedPartChips"));
                Assert.IsNotNull(host.transform.Find("SpumCharacterCreatorRoot/Footer/StatusLine"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SPUM_CREATOR_PANEL_004_NextPartSelectsStableNextPartAndUpdatesPreview()
        {
            var host = new GameObject("CreatorHost", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<SpumCharacterCreatorPanel>();
                panel.Build(CreateCatalog(
                    Part("spum.hair.short", "hair"),
                    Part("spum.hair.long", "hair")));
                panel.SelectCategory("hair");

                panel.NextPart();

                Assert.AreEqual("spum.hair.long", panel.CreateSnapshot().GetSelectedPartId("hair"));
                Assert.IsNotNull(host.transform.Find("SpumCharacterCreatorRoot/Preview/PreviewSelected_hair"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SPUM_CREATOR_PANEL_005_PreviewRootUsesCharacterVisualViewBoundary()
        {
            var host = new GameObject("CreatorHost", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<SpumCharacterCreatorPanel>();
                panel.Build(CreateCatalog(Part("spum.body.default", "body")));

                Transform preview = host.transform.Find("SpumCharacterCreatorRoot/Preview");
                Assert.IsNotNull(preview);
                Assert.IsNotNull(preview.GetComponent<ICharacterVisualView>(), "Creator preview must use the same visual view boundary as runtime character visuals.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void AssertTab(GameObject host, string name)
        {
            Assert.IsNotNull(host.transform.Find("SpumCharacterCreatorRoot/CategoryTabs/" + name), name + " must be built.");
        }

        private static SpumPartCatalogDefinition CreateCatalog(params SpumPartDefinition[] parts)
        {
            var catalog = ScriptableObject.CreateInstance<SpumPartCatalogDefinition>();
            catalog.ConfigureForTests("spum.catalog.creator.test", parts);
            return catalog;
        }

        private static SpumPartDefinition Part(string stableId, string categoryId)
        {
            var part = ScriptableObject.CreateInstance<SpumPartDefinition>();
            part.ConfigureForTests(stableId, categoryId, false);
            return part;
        }
    }
}
