using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.Player;
using Rootborn.UI.Modern;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.Modern
{
    // Presenter contract tests. Actual pointer/typing/cold-save evidence remains a separate native gate.
    public sealed class UrbanInventoryBrowseTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _owned = new List<Object>();
        private readonly Dictionary<FieldInfo, object> _providerState = new Dictionary<FieldInfo, object>();
        private GameObject _root;
        private ModernUiInventoryPanel _panel;
        private PlayerInventory _owner;
        private string _language;
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _language = GameText.Language;
            foreach (var name in new[] { "s_provider", "s_overrideRegistry", "s_cachedRegistry" })
            {
                var field = typeof(GameDataRegistryProvider).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "Provider isolation seam changed: " + name);
                _providerState.Add(field, field.GetValue(null));
            }
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(registry, Is.Not.Null);
            GameDataRegistryProvider.SetOverrideForTests(registry);
            GameText.Language = "en";
            _texture = Own(new Texture2D(1, 1));
            _root = Own(new GameObject("InventoryBrowseFixture", typeof(RectTransform)));
            _panel = _root.AddComponent<ModernUiInventoryPanel>();
            _owner = MakeOwner("OwnerA");
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy panel before its owner/items so subscriptions and captured item references are released.
            if (_root != null) Object.DestroyImmediate(_root);
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            GameText.Language = _language;
            foreach (var state in _providerState) state.Key.SetValue(null, state.Value);
            _providerState.Clear();
        }

        [Test]
        public void FullInventoryShowsEveryModelSlotInSevenScrollableRows()
        {
            var items = Enumerable.Range(0, Inventory.MaxSlots)
                .Select(i => Add("Entry " + i, ItemCategory.Misc)).ToArray();
            Open();
            var scroll = Scroll;
            Assert.That(scroll.content.childCount, Is.EqualTo(Inventory.MaxSlots));
            Assert.That(VisibleIcons(), Is.EqualTo(items.Select(x => x.Icon).ToArray()));
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            var first = (RectTransform)scroll.content.GetChild(0);
            var secondRow = (RectTransform)scroll.content.GetChild(5);
            var last = (RectTransform)scroll.content.GetChild(Inventory.MaxSlots - 1);
            Assert.That(first.sizeDelta, Is.EqualTo(new Vector2(168f, 168f)));
            Assert.That(secondRow.anchoredPosition.y, Is.EqualTo(-184f).Within(.1f));
            Assert.That(last.anchoredPosition, Is.EqualTo(new Vector2(184f, -1104f)));
            Assert.That(scroll.content.rect.height, Is.EqualTo(7 * 168f + 6 * 16f).Within(.1f));
            Assert.That(scroll.viewport.rect.height, Is.EqualTo(3 * 168f + 2 * 16f).Within(.1f));
            scroll.verticalNormalizedPosition = 0f;
            AssertInside(last, scroll.viewport, 0f);
        }

        [Test]
        public void SearchDiscoversTheLastModelSlotAndClearRestoresOriginalOrderWithoutMutation()
        {
            var items = Enumerable.Range(0, Inventory.MaxSlots)
                .Select(i => Add(i == Inventory.MaxSlots - 1 ? "Last needle" : "Other " + i, ItemCategory.Resource)).ToArray();
            string before = Save(_owner);
            var itemBytes = items.Select(x => EditorJsonUtility.ToJson(x)).ToArray();
            Open();
            Search(" needle ");
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { items.Last().Icon }));
            Search("");
            Assert.That(VisibleIcons(), Is.EqualTo(items.Select(x => x.Icon).ToArray()));
            Assert.That(Save(_owner), Is.EqualTo(before));
            Assert.That(items.Select(x => EditorJsonUtility.ToJson(x)), Is.EqualTo(itemBytes));
        }

        [Test]
        public void SearchUsesTheOwnedItemsLocalizedNamesAndDoesNotChangeTheirSavedDefinitions()
        {
            var allowance = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Item_Allowance.asset");
            var materials = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/Item_FacilityMaterials.asset");
            Assert.That(allowance, Is.Not.Null);
            Assert.That(materials, Is.Not.Null);
            var beforeAllowance = EditorJsonUtility.ToJson(allowance);
            var beforeMaterials = EditorJsonUtility.ToJson(materials);
            _owner.Inventory.Add(allowance, 490);
            _owner.Inventory.Add(materials, 75);
            var beforeInventory = Save(_owner);
            GameText.Language = "ko";
            Open();
            Search("자재");
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { materials.Icon }));
            GameText.Language = "en";
            Search("RENOVATION");
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { materials.Icon }));
            Search("");
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { allowance.Icon, materials.Icon }));
            Assert.That(Save(_owner), Is.EqualTo(beforeInventory));
            Assert.That(EditorJsonUtility.ToJson(allowance), Is.EqualTo(beforeAllowance));
            Assert.That(EditorJsonUtility.ToJson(materials), Is.EqualTo(beforeMaterials));
        }

        [Test]
        public void CategoryButtonsCombineWithNameQueryAcrossResourcesAndSeeds()
        {
            var resource = Add("Shared material", ItemCategory.Resource);
            var seed = Add("Shared seed", ItemCategory.Seed);
            var tool = Add("Shared tool", ItemCategory.Tool);
            Add("Other material", ItemCategory.Resource);
            var misc = Add("Shared keepsake", ItemCategory.Misc);
            Open();
            Search("shared");
            Tab(2);
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { resource.Icon, seed.Icon }));
            Tab(1);
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { tool.Icon }));
            Tab(3);
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { misc.Icon }));
            Tab(0);
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { resource.Icon, seed.Icon, tool.Icon, misc.Icon }));
        }

        [Test]
        public void FilteringAndInventoryEventsReuseTheSameSlotObjects()
        {
            var item = Add("Hammer", ItemCategory.Tool, 2);
            Add("Stone", ItemCategory.Resource, 3);
            Open();
            var original = SlotObjects();
            Assert.That(original.Length, Is.EqualTo(Inventory.MaxSlots));
            Search("hammer");
            Assert.That(SlotObjects(), Is.EqualTo(original));
            Search("missing");
            Assert.That(SlotObjects(), Is.EqualTo(original));
            Search("");
            Tab(2);
            Assert.That(SlotObjects(), Is.EqualTo(original));
            Tab(0);
            _owner.Inventory.Add(item, 5);
            Assert.That(SlotObjects(), Is.EqualTo(original));
            Assert.That(SlotWithIcon(item.Icon).Find("Count").GetComponent<Text>().text, Is.EqualTo("7"));
        }

        [Test]
        public void ZeroMatchesShowDecorativeEmptyStateAndRemoveStalePopup()
        {
            var item = Add("Hammer", ItemCategory.Tool, 12);
            Open();
            SlotWithIcon(item.Icon).GetComponent<Button>().onClick.Invoke();
            Assert.That(_root.transform.Find("ItemDetailPopup"), Is.Not.Null);
            Search("missing");
            Assert.That(_root.transform.Find("ItemDetailPopup"), Is.Null);
            var empty = Find("EmptyState");
            Assert.That(empty.gameObject.activeInHierarchy, Is.True);
            Assert.That(empty.IsChildOf(Scroll.viewport), Is.True);
            Assert.That(Scroll.content.gameObject.activeInHierarchy, Is.False);
            Assert.That(Scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.False);
            Assert.That(Scroll.content.childCount, Is.EqualTo(Inventory.MaxSlots));
            Assert.That(empty.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), Is.True);
            var icon = empty.GetComponentsInChildren<UrbanUiIcon>(true)
                .Single(x => new SerializedObject(x).FindProperty("_key").stringValue == "inventory-empty");
            Assert.That(((RectTransform)icon.transform).sizeDelta, Is.EqualTo(new Vector2(96f, 96f)));
            Assert.That(empty.GetComponentsInChildren<Text>(true).Any(t => t.text == "No items to show"), Is.True);
            Search("");
            Assert.That(empty.gameObject.activeInHierarchy, Is.False);
            Assert.That(Scroll.content.gameObject.activeInHierarchy, Is.True);
            Assert.That(Scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.True);
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { item.Icon }));
        }

        [Test]
        public void ReusedButtonOpensCurrentFilteredItemWithItsCurrentCount()
        {
            var hammer = Add("Hammer", ItemCategory.Tool, 2);
            var stone = Add("Stone", ItemCategory.Resource, 37);
            Open();
            var first = Scroll.content.GetChild(0).GetComponent<Button>();
            first.onClick.Invoke();
            AssertPopup("Hammer", "2");
            Tab(2);
            Assert.That(_root.transform.Find("ItemDetailPopup"), Is.Null);
            Assert.That(Scroll.content.GetChild(0).GetComponent<Button>(), Is.SameAs(first));
            Assert.That(SlotWithIcon(stone.Icon).Find("Count").GetComponent<Text>().text, Is.EqualTo("37"));
            first.onClick.Invoke();
            AssertPopup("Stone", "37");
            _owner.Inventory.Add(stone, 4);
            first.onClick.Invoke();
            AssertPopup("Stone", "41");
            Tab(1);
            first.onClick.Invoke();
            AssertPopup("Hammer", "2");
            Assert.That(_owner.Inventory.CountOf(hammer), Is.EqualTo(2));
        }

        [Test]
        public void RebindingSingleAndEmptySlotsClearsOldCountIconAndItemCallback()
        {
            Add("Stack", ItemCategory.Resource, 37);
            var single = Add("One keepsake", ItemCategory.Misc);
            Open();
            var first = Scroll.content.GetChild(0);
            Tab(3);
            Assert.That(Scroll.content.GetChild(0), Is.SameAs(first));
            Assert.That(first.Find("Icon").GetComponent<Image>().sprite, Is.SameAs(single.Icon));
            var count = first.Find("Count");
            Assert.That(count == null || !count.gameObject.activeInHierarchy || !count.GetComponent<Text>().enabled || count.GetComponent<Text>().text == "", Is.True,
                "A single item must not retain the previous stack count.");
            _owner.Inventory.Clear();
            var icon = first.Find("Icon").GetComponent<Image>();
            Assert.That(icon.sprite, Is.Null);
            Assert.That(icon.enabled, Is.False);
            first.GetComponent<Button>().onClick.Invoke();
            Assert.That(_root.transform.Find("ItemDetailPopup"), Is.Null, "An empty pooled slot must not retain a previous item listener.");
        }

        [Test]
        public void OwnerRebindAndHideClearPreviousQueryAndPopup()
        {
            var oldItem = Add("Old item", ItemCategory.Resource);
            Open();
            Search("old");
            SlotWithIcon(oldItem.Icon).GetComponent<Button>().onClick.Invoke();
            var next = MakeOwner("OwnerB");
            var nextItem = MakeItem("New item", ItemCategory.Resource);
            next.Inventory.Add(nextItem, 9);
            _panel.Bind(next);
            Assert.That(SearchField.text, Is.Empty);
            Assert.That(_root.transform.Find("ItemDetailPopup"), Is.Null);
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { nextItem.Icon }));
            _owner.Inventory.Add(oldItem, 5);
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { nextItem.Icon }));
            Search("new");
            _panel.Hide();
            _panel.Show();
            Assert.That(SearchField.text, Is.Empty);
            Assert.That(VisibleIcons(), Is.EqualTo(new[] { nextItem.Icon }));
        }

        [TestCase("ko", "이름으로 검색", "표시할 아이템이 없어요", "전체", "도구", "자원과 씨앗", "기타")]
        [TestCase("en", "Search by name", "No items to show", "All", "Tools", "Resources & seeds", "Miscellaneous")]
        public void SearchCategoryAndEmptyLabelsAreLocalizedReadableAndInsideTheirFrames(
            string language, string placeholder, string emptyText, params string[] categories)
        {
            GameText.Language = language;
            Open();
            Assert.That(SearchField.lineType, Is.EqualTo(InputField.LineType.SingleLine));
            Assert.That(SearchField.characterLimit, Is.EqualTo(64));
            Assert.That(((Text)SearchField.placeholder).text, Is.EqualTo(placeholder));
            Assert.That(SearchField.textComponent.fontSize, Is.EqualTo(24));
            var fontType = typeof(ModernUiInventoryPanel).Assembly.GetType("Rootborn.UI.StudentLife.LifeUiFont");
            var sharedFont = (Font)fontType.GetProperty("Shared").GetValue(null);
            foreach (var text in new[] { SearchField.textComponent, (Text)SearchField.placeholder })
            {
                Assert.That(text.font, Is.SameAs(sharedFont));
                Assert.That(text.raycastTarget, Is.False);
                AssertInside(text.rectTransform, (RectTransform)SearchField.transform, 4f);
                var glyph = (RectTransform)SearchField.transform.Find("Icon");
                var textCorners = new Vector3[4];
                var glyphCorners = new Vector3[4];
                text.rectTransform.GetWorldCorners(textCorners);
                glyph.GetWorldCorners(glyphCorners);
                Assert.That(textCorners.Min(c => SearchField.transform.InverseTransformPoint(c).x),
                    Is.GreaterThanOrEqualTo(glyphCorners.Max(c => SearchField.transform.InverseTransformPoint(c).x) + 4f),
                    "Text must leave the search glyph clear.");
            }
            for (int i = 0; i < categories.Length; i++)
            {
                Tab(i);
                var category = Find("CategoryLabel").GetComponent<Text>();
                Assert.That(category.text, Is.EqualTo(categories[i]));
                Assert.That(category.font, Is.SameAs(sharedFont));
                Assert.That(category.preferredWidth, Is.LessThanOrEqualTo(category.rectTransform.rect.width + 2f));
                AssertInside(category.rectTransform, (RectTransform)_root.transform, 36f);
            }
            var empty = Find("EmptyState").GetComponentsInChildren<Text>(true).Single(t => t.text == emptyText);
            Assert.That(empty.font, Is.SameAs(sharedFont));
            Assert.That(empty.preferredWidth, Is.LessThanOrEqualTo(empty.rectTransform.rect.width + 2f));
            AssertInside(empty.rectTransform, Scroll.viewport, 16f);
        }

        [Test]
        public void RefreshUpdatesLanguageOnAlreadyBuiltControls()
        {
            Open();
            Tab(2);
            GameText.Language = "ko";
            _panel.Refresh();
            Assert.That(((Text)SearchField.placeholder).text, Is.EqualTo("이름으로 검색"));
            Assert.That(Find("CategoryLabel").GetComponent<Text>().text, Is.EqualTo("자원과 씨앗"));
            Assert.That(Find("EmptyState").GetComponentsInChildren<Text>(true).Any(t => t.text == "표시할 아이템이 없어요"), Is.True);
        }

        [Test]
        public void HeaderCloseHidesPanelReleasesSelectionAndPreservesInventorySnapshot()
        {
            Add("Keep me", ItemCategory.Misc, 123);
            string before = Save(_owner);
            Open();
            using (var eventScope = new InventoryBrowseEventSystemScope())
            {
                var events = eventScope.Events;
                events.SetSelectedGameObject(SearchField.gameObject);
                Assert.That(UiTextInputFocus.HasFocus, Is.True, eventScope.Describe(SearchField));
                var close = Find("InventoryCloseButton");
                Assert.That(((RectTransform)close).sizeDelta, Is.EqualTo(new Vector2(48f, 48f)));
                AssertInside((RectTransform)close, (RectTransform)_root.transform, 36f);
                var glyph = close.GetComponentsInChildren<UrbanUiIcon>(true).Single();
                Assert.That(new SerializedObject(glyph).FindProperty("_key").stringValue, Is.EqualTo("close"));
                close.GetComponent<Button>().onClick.Invoke();
                Assert.That(_panel.IsVisible, Is.False);
                Assert.That(_root.activeSelf, Is.False);
                Assert.That(UiTextInputFocus.HasFocus, Is.False);
                Assert.That(events.currentSelectedGameObject, Is.Null);
                Assert.That(Save(_owner), Is.EqualTo(before));
                _panel.Show();
                Assert.That(SearchField.text, Is.Empty);
            }
        }

        private ScrollRect Scroll => _root.GetComponentsInChildren<ScrollRect>(true).Single();
        private InputField SearchField
        {
            get
            {
                var field = Find("SearchField").GetComponent<InputField>();
                Assert.That(field, Is.Not.Null, "The existing decorative SearchField needs an actual legacy InputField.");
                return field;
            }
        }
        private Transform Find(string name)
        {
            var result = _root.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == name);
            Assert.That(result, Is.Not.Null, "Missing inventory UI: " + name);
            return result;
        }
        private void Open() { _panel.Bind(_owner); _panel.Show(); }
        private void Search(string value) { SearchField.text = value; }
        private void Tab(int index) { Find("Tab_" + index).GetComponent<Button>().onClick.Invoke(); }
        private GameObject[] SlotObjects() => Scroll.content.Cast<Transform>().Select(t => t.gameObject).ToArray();
        private Sprite[] VisibleIcons() => Scroll.content.Cast<Transform>()
            .Where(t => t.gameObject.activeInHierarchy)
            .Select(t => t.Find("Icon").GetComponent<Image>())
            .Where(i => i.enabled && i.sprite != null).Select(i => i.sprite).ToArray();
        private Transform SlotWithIcon(Sprite sprite) => Scroll.content.Cast<Transform>()
            .Single(t => t.gameObject.activeInHierarchy && t.Find("Icon").GetComponent<Image>().sprite == sprite);
        private void AssertPopup(string name, string count)
        {
            var popup = Find("ItemDetailPopup");
            Assert.That(popup.Find("ItemName").GetComponent<Text>().text, Is.EqualTo(name));
            Assert.That(popup.Find("ItemCount").GetComponent<Text>().text, Is.EqualTo("Count: " + count));
        }
        private PlayerInventory MakeOwner(string name)
        {
            var go = Own(new GameObject(name, typeof(PlayerInventory)));
            var owner = go.GetComponent<PlayerInventory>();
            // Fixture isolation only: never clear/mutate PlayerGlobalState's active save inventory.
            SetField(owner, "_inventory", new Inventory());
            return owner;
        }
        private ItemDefinition Add(string name, ItemCategory category, int quantity = 1)
        {
            var item = MakeItem(name, category);
            _owner.Inventory.Add(item, quantity);
            return item;
        }
        private ItemDefinition MakeItem(string name, ItemCategory category)
        {
            var item = Own(ScriptableObject.CreateInstance<ItemDefinition>());
            SetField(item, "_id", "fixture.browse." + _owned.Count);
            SetField(item, "_displayKey", name);
            SetField(item, "_category", category);
            SetField(item, "_maxStack", 9999);
            var sprite = Own(Sprite.Create(_texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f)));
            SetField(item, "_icon", sprite);
            return item;
        }
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
        private static string Save(PlayerInventory owner) => JsonUtility.ToJson(owner.ToSaveData());
        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, PrivateInstance);
            Assert.That(field, Is.Not.Null, "Fixture seam changed: " + name);
            field.SetValue(target, value);
        }
        private static void AssertInside(RectTransform child, RectTransform frame, float inset)
        {
            var rect = frame.rect;
            rect.xMin += inset; rect.xMax -= inset;
            rect.yMin += inset; rect.yMax -= inset;
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var local = frame.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(rect.xMin - .1f, rect.xMax + .1f), child.name + " crosses horizontal molding.");
                Assert.That(local.y, Is.InRange(rect.yMin - .1f, rect.yMax + .1f), child.name + " crosses vertical molding.");
            }
        }
    }
}
