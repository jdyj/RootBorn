using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.UI.Modern;

namespace Rootborn.Tests.EditMode.UI.Modern
{
    public sealed class InventoryUiFilterTests
    {
        private string _language;
        private GameDataRegistry _registry;
        private GameTextCatalog _catalog;
        private ItemDefinition _tool;
        private ItemDefinition _resource;
        private ItemDefinition _seed;
        private ItemDefinition _misc;
        private object _previousProvider;
        private object _previousOverride;
        private object _previousCache;

        [SetUp]
        public void SetUp()
        {
            _language = GameText.Language;
            _previousProvider = ReadProviderField("s_provider");
            _previousOverride = ReadProviderField("s_overrideRegistry");
            _previousCache = ReadProviderField("s_cachedRegistry");
            GameText.Language = "en";
            _registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            _catalog = ScriptableObject.CreateInstance<GameTextCatalog>();
            typeof(GameDataRegistry).GetField("_textCatalog", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_registry, _catalog);
            _catalog.ConfigureJson("{\"entries\":[{\"key\":\"item.allowance.name\",\"ko\":\"용돈\",\"en\":\"Pocket Money\"},{\"key\":\"item.facility-materials.name\",\"ko\":\"시설 자재\",\"en\":\"Facility Materials\"},{\"key\":\"item.seed.wheat\",\"ko\":\"밀 씨앗\",\"en\":\"Wheat Seed\"},{\"key\":\"tool.bareHand\",\"ko\":\"맨손\",\"en\":\"Bare Hand\"}]} ");
            GameDataRegistryProvider.SetOverrideForTests(_registry);
            _tool = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Item_Tool_BareHand.asset");
            _resource = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Item_FacilityMaterials.asset");
            _seed = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Item_Seed_Wheat.asset");
            _misc = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Item_Allowance.asset");
            Assert.That(_tool, Is.Not.Null);
            Assert.That(_resource, Is.Not.Null);
            Assert.That(_seed, Is.Not.Null);
            Assert.That(_misc, Is.Not.Null);
            Assert.That(_tool.Category, Is.EqualTo(ItemCategory.Tool));
            Assert.That(_resource.Category, Is.EqualTo(ItemCategory.Resource));
            Assert.That(_seed.Category, Is.EqualTo(ItemCategory.Seed));
            Assert.That(_misc.Category, Is.EqualTo(ItemCategory.Misc));
        }

        [TearDown]
        public void TearDown()
        {
            GameText.Language = _language;
            WriteProviderField("s_provider", _previousProvider);
            WriteProviderField("s_overrideRegistry", _previousOverride);
            WriteProviderField("s_cachedRegistry", _previousCache);
            if (_registry != null) UnityEngine.Object.DestroyImmediate(_registry);
            if (_catalog != null) UnityEngine.Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void AllTabMatchesEveryCategoryAndNullNeverMatches()
        {
            Assert.That(Matches(_tool, 0, null), Is.True);
            Assert.That(Matches(_resource, 0, null), Is.True);
            Assert.That(Matches(_seed, 0, null), Is.True);
            Assert.That(Matches(_misc, 0, null), Is.True);
            Assert.That(Matches(null, 0, null), Is.False);
        }

        [Test]
        public void ToolTabIncludesToolsAndExcludesOtherCategories()
        {
            Assert.That(Matches(_tool, 1, null), Is.True);
            Assert.That(Matches(_resource, 1, null), Is.False);
            Assert.That(Matches(_seed, 1, null), Is.False);
            Assert.That(Matches(_misc, 1, null), Is.False);
        }

        [Test]
        public void ResourcesTabIncludesResourcesAndSeedsOnly()
        {
            Assert.That(Matches(_resource, 2, null), Is.True);
            Assert.That(Matches(_seed, 2, null), Is.True);
            Assert.That(Matches(_tool, 2, null), Is.False);
            Assert.That(Matches(_misc, 2, null), Is.False);
        }

        [Test]
        public void MiscTabIncludesMiscOnly()
        {
            Assert.That(Matches(_misc, 3, null), Is.True);
            Assert.That(Matches(_tool, 3, null), Is.False);
            Assert.That(Matches(_resource, 3, null), Is.False);
            Assert.That(Matches(_seed, 3, null), Is.False);
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(100)]
        public void UnknownTabFallsBackToAll(int tab)
        {
            Assert.That(Matches(_resource, tab, null), Is.True);
            Assert.That(Matches(_tool, tab, null), Is.True);
        }

        [Test]
        public void QueryUsesLocalizedDisplayNameAndExcludesIdAndDescription()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            try
            {
                var serialized = new SerializedObject(item);
                serialized.FindProperty("_id").stringValue = "unique-id-token-741";
                serialized.FindProperty("_displayKey").stringValue = "item.facility-materials.name";
                serialized.FindProperty("_description").stringValue = "unique-description-token-852";
                serialized.FindProperty("_category").enumValueIndex = (int)ItemCategory.Resource;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(Matches(item, 0, "  fAcIlItY mAtErIaLs  "), Is.True);
                Assert.That(Matches(item, 0, "unique-id-token-741"), Is.False);
                Assert.That(Matches(item, 0, "unique-description-token-852"), Is.False);
                GameText.Language = "ko";
                serialized.FindProperty("_displayKey").stringValue = "item.allowance.name";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(Matches(item, 0, "  용돈  "), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void CategoryKeysMapToLocalizedUiKeysAndUnknownTabUsesAll()
        {
            Assert.That(CategoryKey(0), Is.EqualTo("ui.inventory.category.all"));
            Assert.That(CategoryKey(1), Is.EqualTo("ui.inventory.category.tools"));
            Assert.That(CategoryKey(2), Is.EqualTo("ui.inventory.category.resources"));
            Assert.That(CategoryKey(3), Is.EqualTo("ui.inventory.category.misc"));
            Assert.That(CategoryKey(99), Is.EqualTo("ui.inventory.category.all"));
        }

        [Test]
        public void FilteringReadsItemFieldsWithoutMutatingTheDefinition()
        {
            var before = EditorJsonUtility.ToJson(_resource);
            var id = _resource.Id;
            var key = _resource.DisplayKey;
            var category = _resource.Category;
            var maxStack = _resource.MaxStack;
            Assert.That(Matches(_resource, 2, "Facility"), Is.True);
            Assert.That(EditorJsonUtility.ToJson(_resource), Is.EqualTo(before));
            Assert.That(_resource.Id, Is.EqualTo(id));
            Assert.That(_resource.DisplayKey, Is.EqualTo(key));
            Assert.That(_resource.Category, Is.EqualTo(category));
            Assert.That(_resource.MaxStack, Is.EqualTo(maxStack));
        }

        private static Type FilterType()
        {
            var type = typeof(ModernUiInventoryPanel).Assembly.GetType("Rootborn.UI.Modern.InventoryUiFilter");
            Assert.That(type, Is.Not.Null, "InventoryUiFilter helper is not present yet");
            return type;
        }

        private static bool Matches(ItemDefinition item, int tab, string query)
        {
            var method = FilterType().GetMethod("Matches", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "InventoryUiFilter.Matches(ItemDefinition, int, string) is missing");
            return (bool)method.Invoke(null, new object[] { item, tab, query });
        }

        private static string CategoryKey(int tab)
        {
            var method = FilterType().GetMethod("CategoryKey", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "InventoryUiFilter.CategoryKey(int) is missing");
            return (string)method.Invoke(null, new object[] { tab });
        }

        private static object ReadProviderField(string name) => typeof(GameDataRegistryProvider).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        private static void WriteProviderField(string name, object value) => typeof(GameDataRegistryProvider).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);
    }
}

