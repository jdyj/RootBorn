using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Characters.Spum;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Modern;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.Spum
{
    public sealed class SpumCharacterCreatorStyleTests
    {
        [Test]
        public void SPUM_CREATOR_STYLE_001_RootUsesModernCommonPanelTiles()
        {
            var host = new GameObject("CreatorHost", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<SpumCharacterCreatorPanel>();
                panel.Build(CreateCatalog(Part("spum.body.default", "body")));

                Transform root = host.transform.Find("SpumCharacterCreatorRoot");
                Assert.IsNotNull(root);
                var tiles = root.GetComponent<ModernUiTileImage>();
                Assert.IsNotNull(tiles);
                tiles.Rebuild();
                Assert.Greater(tiles.TileCount, 0);
                CollectionAssert.AreEqual(ModernUiRecipes.CommonPanel.Tiles, panel.RootPanelRecipe.Tiles);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SPUM_CREATOR_STYLE_002_DoesNotCreateNestedCardContainers()
        {
            var host = new GameObject("CreatorHost", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<SpumCharacterCreatorPanel>();
                panel.Build(CreateCatalog(Part("spum.body.default", "body"), Part("spum.hair.default", "hair")));

                string[] cardContainers = host.GetComponentsInChildren<Transform>(true)
                    .Select(transform => transform.name)
                    .Where(name => name.Contains("Card") || name.Contains("Nested"))
                    .ToArray();

                CollectionAssert.IsEmpty(cardContainers);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static SpumPartCatalogDefinition CreateCatalog(params SpumPartDefinition[] parts)
        {
            var catalog = ScriptableObject.CreateInstance<SpumPartCatalogDefinition>();
            catalog.ConfigureForTests("spum.catalog.creator.style.test", parts);
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
