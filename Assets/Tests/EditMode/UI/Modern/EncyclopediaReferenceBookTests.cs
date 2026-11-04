using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;
using Rootborn.UI.Encyclopedia;
using Rootborn.UI.Modern;

namespace Rootborn.Tests.EditMode.UI.Modern
{
    public sealed class EncyclopediaReferenceBookTests
    {
        private const string SpritePath = "Assets/Art/UI/UrbanMolding/encyclopedia_reference.png";
        private static readonly string[] ExistingThemeKeys =
        {
            "journal", "plan", "town_map", "discovery", "chef", "care", "interior", "training",
            "time", "energy", "focus", "money", "school", "inventory-all", "inventory-tools",
            "inventory-consumables", "inventory-misc", "inventory-search", "inventory-delete",
            "inventory-filter", "close", "stress", "daily-planner", "inventory-empty"
        };

        [Test]
        public void ReferenceBookSpriteHasExpectedImportAndAddressableSettings()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            Assert.That(sprite, Is.Not.Null, "Dedicated encyclopedia reference sprite is required");
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(64f, 64f)));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(32f, 32f)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f));

            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(SpritePath));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.address, Is.EqualTo("sprites/ui/urban-life/encyclopedia"));
        }

        [Test]
        public void ThemeAppendsEncyclopediaIconAndPreservesAllExistingKeys()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UrbanUiThemeDefinition>("Assets/Data/UI/UrbanMoldingTheme.asset");
            Assert.That(theme, Is.Not.Null);
            var icons = new SerializedObject(theme).FindProperty("_icons");
            Assert.That(icons, Is.Not.Null);
            Assert.That(icons.arraySize, Is.GreaterThanOrEqualTo(ExistingThemeKeys.Length + 1));

            var keys = Enumerable.Range(0, icons.arraySize)
                .Select(i => icons.GetArrayElementAtIndex(i).FindPropertyRelative("_key").stringValue)
                .ToArray();
            CollectionAssert.AreEqual(ExistingThemeKeys, keys.Take(ExistingThemeKeys.Length));
            Assert.That(keys[ExistingThemeKeys.Length], Is.EqualTo("encyclopedia"), "Later independent icons append after the original reference entry.");
            Assert.That(keys.Count(key => key == "encyclopedia"), Is.EqualTo(1));

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            Assert.That(theme.GetIcon("encyclopedia"), Is.SameAs(sprite));
        }

        [Test]
        public void EncyclopediaHeaderUsesDedicatedNonRaycastingReferenceIcon()
        {
            var root = new GameObject("EncyclopediaReferenceFixture", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<EncyclopediaPanel>();
                panel.Show();

                var iconTransform = root.transform.Find("HeaderIcon");
                Assert.That(iconTransform, Is.Not.Null);
                var rect = (RectTransform)iconTransform;
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(64f, 64f)));
                var image = iconTransform.GetComponent<Image>();
                Assert.That(image, Is.Not.Null);
                Assert.That(image.raycastTarget, Is.False);
                var icon = iconTransform.GetComponent<UrbanUiIcon>();
                Assert.That(icon, Is.Not.Null);
                Assert.That(new SerializedObject(icon).FindProperty("_key").stringValue, Is.EqualTo("encyclopedia"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
