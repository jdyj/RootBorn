using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Localization;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rootborn.UI.Modern
{
    [DisallowMultipleComponent]
    public sealed class ModernUiInventoryPanel : MonoBehaviour
    {
        // Layout follows the reference mock (Style2 Modern UI): a beige 9-slice panel
        // with a row of tab ribbons poking out the top, a search-bar row, a slot grid,
        // and a vertical scrollbar on the right.
        private const int SlotColumns = 5;
        private const int SlotRows = (Inventory.MaxSlots + SlotColumns - 1) / SlotColumns;
        // The viewport shows three rows; the pooled content covers the full model capacity.
        private const int VisibleRows = 3;
        // Keep the approved large slots while clipping overflow inside the frame.
        private const float SlotSize = 168f;
        private const float SlotSpacing = 16f;
        private const float GridPadX = 40f;
        private const float GridPadTop = 184f;
        private const float SearchBarHeight = 56f;
        // Tabs are slightly wider than tall (4:3) so the 6-cell sprite block reads as a
        // proper bookmark — square cells would crush the 3-column wide top edge.
        private const float TabWidth = 128f;
        private const float TabHeight = 96f;
        private const float TabSpacing = 8f;
        private const float TabOverlap = 36f;
        private const float ScrollbarWidth = 48f;
        private static readonly Vector2 DefaultSize = new Vector2(
            GridPadX * 2f + SlotColumns * SlotSize + (SlotColumns - 1) * SlotSpacing + ScrollbarWidth + 8f,
            GridPadTop + VisibleRows * SlotSize + (VisibleRows - 1) * SlotSpacing + 40f);

        // Four category tabs, left to right.
        private static readonly string[] TabIconSubSpriteNames = new[]
        {
            "ModernUI_16_Style2_r0_c22", // item.bag        — All / Inventory
            "ModernUI_16_Style2_r7_c20", // item.swordGreen — Weapons / tools
            "ModernUI_16_Style2_r6_c20", // Legacy icon alias; this tab groups resources and seeds.
            "ModernUI_16_Style2_r0_c24", // glyph.settings  — Misc
        };

        private static readonly string[] TabThemeIconKeys = { "inventory-all", "inventory-tools", "inventory-consumables", "inventory-misc" };

        private sealed class SlotView
        {
            public GameObject Root;
            public Image Icon;
            public Text Count;
            public Button Button;
            public Inventory.Slot Slot;
        }

        private readonly List<SlotView> _slots = new List<SlotView>(Inventory.MaxSlots);
        private PlayerInventory _inventory;
        private Inventory _boundInventory;
        private RectTransform _innerContainer;
        private RectTransform _slotGrid;
        private RectTransform _viewport;
        private ScrollRect _scroll;
        private GameObject _popup;
        private InputField _search;
        private Text _title;
        private Text _categoryLabel;
        private Text _searchPlaceholder;
        private Text _emptyHint;
        private GameObject _emptyState;
        private string _query = string.Empty;
        private int _selectedTab;
        private bool _built;
        private bool _isVisible;

        public bool IsVisible => _isVisible;
        public int TileCount
        {
            get
            {
                int count = 0;
                var tiles = GetComponentsInChildren<ModernUiTileImage>(true);
                for (int i = 0; i < tiles.Length; i++)
                {
                    count += tiles[i].TileCount;
                }

                return count;
            }
        }

        public void Bind(PlayerInventory inventory)
        {
            var nextStorage = inventory != null ? inventory.Inventory : null;
            if (object.ReferenceEquals(_inventory, inventory) && object.ReferenceEquals(_boundInventory, nextStorage))
                return;

            if (_boundInventory != null)
            {
                _boundInventory.OnChanged -= Refresh;
            }

            _inventory = inventory;
            _boundInventory = nextStorage;
            if (_boundInventory != null)
            {
                _boundInventory.OnChanged += Refresh;
            }

            ClosePopup();
            ResetQuery();

            if (_built)
            {
                Refresh();
            }
        }

        public void Show()
        {
            EnsureBuilt();
            gameObject.SetActive(true);
            _isVisible = true;
            Refresh();
        }

        public void Hide()
        {
            ClosePopup();
            ReleaseSelection();
            ResetQuery();
            gameObject.SetActive(false);
            _isVisible = false;
        }

        private void OnDestroy()
        {
            if (_boundInventory != null)
            {
                _boundInventory.OnChanged -= Refresh;
            }
        }

        private void ReleaseSelection()
        {
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            if (selected != null && selected.transform.IsChildOf(transform))
                events.SetSelectedGameObject(null);
        }

        private void ResetQuery()
        {
            _query = string.Empty;
            if (_search != null) _search.SetTextWithoutNotify(string.Empty);
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            var root = transform as RectTransform;
            if (root != null && (root.sizeDelta.x < DefaultSize.x || root.sizeDelta.y < DefaultSize.y))
            {
                root.sizeDelta = DefaultSize;
            }

            BuildRootPanel();
            BuildTabStrip();
            string title = GameText.Get("ui.inventory.title");
            if (title == "ui.inventory.title") title = GameText.Language == "ko" ? "\uAC00\uBC29" : "Inventory";
            _title = MakeLabel((RectTransform)transform, "Title", title, new Vector2(GridPadX, -40f), new Vector2(240f, 44f), 32, TextAnchor.MiddleLeft);
            BuildHeaderControls();
            BuildSearchRow();
            BuildInnerPanel();
            BuildSlotGrid();
            BuildScrollbar();
            BuildEmptyState();

            _built = true;
        }

        public void Refresh()
        {
            if (!_built || _slotGrid == null)
            {
                return;
            }

            ClosePopup();
            RefreshLabels();
            int visibleIndex = 0;
            if (_boundInventory != null)
            {
                var slots = _boundInventory.Slots;
                for (int i = 0; i < slots.Count && visibleIndex < _slots.Count; i++)
                {
                    var slot = slots[i];
                    if (slot.Item == null || slot.Count <= 0 || !InventoryUiFilter.Matches(slot.Item, _selectedTab, _query))
                    {
                        continue;
                    }

                    BindSlot(_slots[visibleIndex], slot, visibleIndex);
                    visibleIndex++;
                }
            }

            bool hasMatches = visibleIndex > 0;
            while (visibleIndex < _slots.Count)
            {
                BindSlot(_slots[visibleIndex], null, visibleIndex);
                visibleIndex++;
            }
            _slotGrid.gameObject.SetActive(hasMatches);
            // Permanent scrollbar visibility is reapplied by ScrollRect.LateUpdate. An empty
            // view has no scroll interaction; disabling the controller keeps that frame owner
            // from resurrecting the track while retaining its geometry and all pooled slots.
            _scroll.enabled = hasMatches;
            _scroll.verticalScrollbar.gameObject.SetActive(hasMatches);
            _emptyState.SetActive(!hasMatches);
        }

        private void RefreshLabels()
        {
            string title = GameText.Get("ui.inventory.title");
            if (title == "ui.inventory.title") title = GameText.Language == "ko" ? "가방" : "Inventory";
            _title.text = title;
            _categoryLabel.text = GameText.Get(InventoryUiFilter.CategoryKey(_selectedTab));
            _searchPlaceholder.text = GameText.Get("ui.inventory.search");
            _emptyHint.text = GameText.Get("ui.inventory.empty");
        }

        private void BuildHeaderControls()
        {
            var root = (RectTransform)transform;
            _categoryLabel = MakeLabel(root, "CategoryLabel", string.Empty, new Vector2(304f, -40f),
                new Vector2(root.sizeDelta.x - 420f, 44f), 24, TextAnchor.MiddleRight);
            var close = MakeImage(root, "InventoryCloseButton", new Vector2(root.sizeDelta.x - 88f, -38f), new Vector2(48f, 48f));
            AddSlotBackplate(close);
            close.raycastTarget = true;
            var button = close.gameObject.AddComponent<Button>();
            button.targetGraphic = close;
            button.onClick.AddListener(Hide);
            var icon = MakeImage(close.rectTransform, "Icon", new Vector2(8f, -8f), new Vector2(32f, 32f));
            icon.gameObject.AddComponent<UrbanUiIcon>().Configure("close");
        }

        private void BuildEmptyState()
        {
            float width = _viewport.sizeDelta.x - 144f;
            var background = MakeImage(_viewport, "EmptyState", new Vector2(72f, -104f), new Vector2(width, 288f));
            AddSlotBackplate(background);
            _emptyState = background.gameObject;
            var icon = MakeImage(background.rectTransform, "Icon", new Vector2((width - 96f) * .5f, -40f), new Vector2(96f, 96f));
            icon.gameObject.AddComponent<UrbanUiIcon>().Configure("inventory-empty");
            _emptyHint = MakeLabel(background.rectTransform, "Hint", string.Empty, new Vector2(24f, -168f),
                new Vector2(width - 48f, 56f), 28, TextAnchor.MiddleCenter);
        }

        private void BuildRootPanel()
        {
            // The root GameObject itself draws the beige 9-slice CommonPanel.
            var image = GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
            }
            image.color = Color.clear;
            image.raycastTarget = true;

            var tiles = GetComponent<ModernUiTileImage>();
            if (tiles == null)
            {
                tiles = gameObject.AddComponent<ModernUiTileImage>();
            }
            tiles.SetRecipe(ModernUiRecipes.CommonPanel48);
            tiles.SetTileSize(new Vector2(32f, 32f)); // 2x the default 16 — chunkier corners + edges.
            tiles.Rebuild();
        }

        private void BuildTabStrip()
        {
            // Each tab is a 2x3 mini panel built from the thin Style2 panel block at
            // (r11_c0..r12_c2). Tabs are TabWidth x TabHeight (4:3) so the 3-column top
            // edge reads as a proper bookmark instead of a thin vertical bar.
            var rect = (RectTransform)transform;
            int tabCount = TabIconSubSpriteNames.Length;
            float totalTabsWidth = TabWidth * tabCount + TabSpacing * (tabCount - 1);
            float startX = (rect.sizeDelta.x - totalTabsWidth) * 0.5f;
            float yOffset = rect.sizeDelta.y - TabOverlap;

            for (int i = 0; i < tabCount; i++)
            {
                var go = new GameObject("Tab_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(transform, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = new Vector2(0f, 0f);
                r.anchorMax = new Vector2(0f, 0f);
                r.pivot = new Vector2(0f, 0f);
                r.anchoredPosition = new Vector2(startX + i * (TabWidth + TabSpacing), yOffset);
                r.sizeDelta = new Vector2(TabWidth, TabHeight);

                // Transparent hit target on the tab root — the visual is drawn by the
                // 6 child cells below so we can keep them as crisp 16x16 corners.
                var hit = go.GetComponent<Image>();
                hit.sprite = null;
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;

                BuildTabCells(r, i == _selectedTab);

                var icon = MakeImage(r, "Icon", Vector2.zero, new Vector2(56f, 56f));
                icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(0f, 8f);
                icon.sprite = ResolveSprite(TabIconSubSpriteNames[i]);
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.gameObject.AddComponent<UrbanUiIcon>().ConfigureWithFallback(TabThemeIconKeys[i], icon.sprite);
                icon.transform.SetAsLastSibling();

                int capturedIndex = i;
                var btn = go.GetComponent<Button>();
                btn.onClick.AddListener(() => SelectTab(capturedIndex));

            }
        }

        private static void BuildTabCells(RectTransform parent, bool selected)
        {
            var button = parent.GetComponent<Button>();
            button.targetGraphic = parent.GetComponent<Image>();
            SetTabColors(button, selected);
            var tiles = parent.gameObject.AddComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.SetTileSize(new Vector2(16f, 16f));
            tiles.Rebuild();
        }

        private static void SetTabColors(Button button, bool selected)
        {
            var colors = button.colors;
            colors.normalColor = selected ? Color.white : new Color(0.78f, 0.74f, 0.66f, 1f);
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
        }

        private void SelectTab(int index)
        {
            ClosePopup();
            _selectedTab = index;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Tab_") && child.TryGetComponent<Button>(out var button))
                    SetTabColors(button, child.name == "Tab_" + index);
            }
            Refresh();
            _scroll.verticalNormalizedPosition = 1f;
        }

        private static void AddSlotBackplate(Image image)
        {
            image.sprite = null;
            image.color = Color.clear;
            var tiles = image.gameObject.AddComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.SlotPanel);
            tiles.SetTileSize(new Vector2(16f, 16f));
            tiles.Rebuild();
        }

        private void BuildSearchRow()
        {
            var rect = (RectTransform)transform;
            float rowY = -GridPadTop + SlotSpacing + SearchBarHeight;
            float rowLeft = GridPadX;
            float rowRight = rect.sizeDelta.x - GridPadX - ScrollbarWidth - 8f;
            float rowWidth = rowRight - rowLeft;

            // Search field background — single slot sprite stretched wide.
            const float iconSize = 32f;
            const float gap = 8f;
            float searchWidth = rowWidth - (SearchBarHeight + gap) * 2f - gap;
            var search = MakeImage((RectTransform)transform, "SearchField",
                new Vector2(rowLeft, rowY), new Vector2(searchWidth, SearchBarHeight));
            search.sprite = ResolveSprite(ModernUiStyle2Sprites.Slot.BaseName);
            search.type = Image.Type.Sliced;
            search.color = new Color(1f, 1f, 1f, 1f);
            search.raycastTarget = true;
            AddSlotBackplate(search);

            var searchIcon = MakeImage((RectTransform)search.transform, "Icon",
                new Vector2(gap, -((SearchBarHeight - iconSize) * 0.5f)), new Vector2(iconSize, iconSize));
            searchIcon.sprite = ResolveSprite(ModernUiStyle2Sprites.IconButton.SearchName);
            searchIcon.enabled = searchIcon.sprite != null;
            searchIcon.preserveAspect = true;
            searchIcon.raycastTarget = false;
            searchIcon.gameObject.AddComponent<UrbanUiIcon>().ConfigureWithFallback("inventory-search", searchIcon.sprite);

            _search = search.gameObject.AddComponent<InputField>();
            _search.targetGraphic = search;
            _search.lineType = InputField.LineType.SingleLine;
            _search.characterLimit = 64;
            var text = MakeLabel(search.rectTransform, "Text", string.Empty, new Vector2(48f, -8f),
                new Vector2(searchWidth - 64f, SearchBarHeight - 16f), 24, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            _searchPlaceholder = MakeLabel(search.rectTransform, "Placeholder", string.Empty, new Vector2(48f, -8f),
                new Vector2(searchWidth - 64f, SearchBarHeight - 16f), 24, TextAnchor.MiddleLeft);
            _searchPlaceholder.color = new Color(.33f, .36f, .38f, 1f);
            _search.textComponent = text;
            _search.placeholder = _searchPlaceholder;
            _search.onValueChanged.AddListener(OnQueryChanged);

            // Delete (trash) button on the right.
            var deleteRoot = MakeImage((RectTransform)transform, "DeleteButton",
                new Vector2(rowLeft + searchWidth + gap, rowY), new Vector2(SearchBarHeight, SearchBarHeight));
            deleteRoot.sprite = ResolveSprite(ModernUiStyle2Sprites.Slot.BaseName);
            deleteRoot.type = Image.Type.Sliced;
            deleteRoot.color = Color.white;
            deleteRoot.raycastTarget = true;
            AddSlotBackplate(deleteRoot);

            var deleteIcon = MakeImage((RectTransform)deleteRoot.transform, "Icon",
                new Vector2((SearchBarHeight - iconSize) * 0.5f, -((SearchBarHeight - iconSize) * 0.5f)),
                new Vector2(iconSize, iconSize));
            deleteIcon.sprite = ResolveSprite(ModernUiStyle2Sprites.IconButton.DeleteName);
            deleteIcon.enabled = deleteIcon.sprite != null;
            deleteIcon.preserveAspect = true;
            deleteIcon.raycastTarget = false;
            deleteIcon.gameObject.AddComponent<UrbanUiIcon>().ConfigureWithFallback("inventory-delete", deleteIcon.sprite);

            // Filter/settings button.
            var filterRoot = MakeImage((RectTransform)transform, "FilterButton",
                new Vector2(rowLeft + searchWidth + gap + SearchBarHeight + gap, rowY),
                new Vector2(SearchBarHeight, SearchBarHeight));
            filterRoot.sprite = ResolveSprite(ModernUiStyle2Sprites.Slot.BaseName);
            filterRoot.type = Image.Type.Sliced;
            filterRoot.color = Color.white;
            filterRoot.raycastTarget = true;
            AddSlotBackplate(filterRoot);

            var filterIcon = MakeImage((RectTransform)filterRoot.transform, "Icon",
                new Vector2((SearchBarHeight - iconSize) * 0.5f, -((SearchBarHeight - iconSize) * 0.5f)),
                new Vector2(iconSize, iconSize));
            filterIcon.sprite = ResolveSprite(ModernUiStyle2Sprites.IconButton.SortName);
            filterIcon.enabled = filterIcon.sprite != null;
            filterIcon.preserveAspect = true;
            filterIcon.raycastTarget = false;
            filterIcon.gameObject.AddComponent<UrbanUiIcon>().ConfigureWithFallback("inventory-filter", filterIcon.sprite);
        }

        private void BuildInnerPanel()
        {
            float gridWidth = SlotColumns * SlotSize + (SlotColumns - 1) * SlotSpacing;
            float visibleHeight = VisibleRows * SlotSize + (VisibleRows - 1) * SlotSpacing;
            var go = new GameObject("InventoryInnerPanel", typeof(RectTransform), typeof(ScrollRect));
            go.transform.SetParent(transform, false);
            _innerContainer = (RectTransform)go.transform;
            _innerContainer.anchorMin = _innerContainer.anchorMax = new Vector2(0f, 1f);
            _innerContainer.pivot = new Vector2(0f, 1f);
            _innerContainer.anchoredPosition = new Vector2(GridPadX, -GridPadTop);
            _innerContainer.sizeDelta = new Vector2(gridWidth + ScrollbarWidth + 8f, visibleHeight);
            _scroll = go.GetComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 48f;
            var viewport = new GameObject("InventoryViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(_innerContainer, false);
            _viewport = (RectTransform)viewport.transform;
            _viewport.anchorMin = _viewport.anchorMax = new Vector2(0f, 1f);
            _viewport.pivot = new Vector2(0f, 1f);
            _viewport.sizeDelta = new Vector2(gridWidth, visibleHeight);
            viewport.GetComponent<Image>().color = Color.clear;
            viewport.GetComponent<Image>().raycastTarget = true;
            _scroll.viewport = _viewport;
        }

        private void BuildSlotGrid()
        {
            float gridWidth = SlotColumns * SlotSize + (SlotColumns - 1) * SlotSpacing;
            float gridHeight = SlotRows * SlotSize + (SlotRows - 1) * SlotSpacing;
            var go = new GameObject("SlotGrid", typeof(RectTransform));
            go.transform.SetParent(_viewport, false);
            _slotGrid = (RectTransform)go.transform;
            _slotGrid.anchorMin = _slotGrid.anchorMax = new Vector2(0f, 1f);
            _slotGrid.pivot = new Vector2(0f, 1f);
            _slotGrid.sizeDelta = new Vector2(gridWidth, gridHeight);
            _scroll.content = _slotGrid;
            for (int i = 0; i < Inventory.MaxSlots; i++) MakeSlot(i);
        }

        private void BuildScrollbar()
        {
            float left = _viewport.sizeDelta.x + 8f;
            var track = MakeImage(_innerContainer, "ScrollTrackTop", new Vector2(left, 0f),
                new Vector2(ScrollbarWidth, _viewport.sizeDelta.y));
            track.color = new Color(0.83f, 0.81f, 0.74f, 1f);
            track.raycastTarget = true;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            var handle = MakeImage(track.rectTransform, "ScrollHandle", Vector2.zero, Vector2.zero);
            handle.rectTransform.anchorMin = Vector2.zero;
            handle.rectTransform.anchorMax = Vector2.one;
            handle.rectTransform.offsetMin = new Vector2(8f, 0f);
            handle.rectTransform.offsetMax = new Vector2(-8f, 0f);
            handle.color = new Color(0.12f, 0.18f, 0.25f, 1f);
            handle.raycastTarget = true;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalNormalizedPosition = 1f;
        }

        private void OnQueryChanged(string value)
        {
            _query = value ?? string.Empty;
            Refresh();
            _scroll.verticalNormalizedPosition = 1f;
        }

        private void MakeSlot(int visibleIndex)
        {
            var go = new GameObject("Slot_Empty_" + visibleIndex,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            go.transform.SetParent(_slotGrid, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            int col = visibleIndex % SlotColumns;
            int row = visibleIndex / SlotColumns;
            float x = col * (SlotSize + SlotSpacing);
            float y = -row * (SlotSize + SlotSpacing);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(SlotSize, SlotSize);

            // Slot frame is a SlotPanel 9-slice (r8_c3..r10_c5). The root Image is a
            // transparent button hit target; the slice tiles are drawn by ModernUiTileImage.
            var image = go.GetComponent<Image>();
            image.sprite = null;
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.SlotPanel);
            tiles.SetTileSize(new Vector2(SlotSize / 3f, SlotSize / 3f));
            tiles.Rebuild();

            var icon = MakeImage(rect, "Icon", Vector2.zero, new Vector2(SlotSize - 32f, SlotSize - 32f));
            icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.enabled = false;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var count = MakeLabel(rect, "Count", string.Empty,
                new Vector2(SlotSize - 84f, -SlotSize + 52f), new Vector2(64f, 32f), 24, TextAnchor.LowerRight);
            count.color = new Color(0.12f, 0.18f, 0.25f, 1f);
            var shadow = count.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0.97f, 0.94f, 0.85f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);
            var view = new SlotView { Root = go, Icon = icon, Count = count, Button = button };
            count.gameObject.SetActive(false);
            // One listener per pooled view reads its current binding instead of capturing an old model slot.
            button.onClick.AddListener(() => OpenPopup(view.Slot));
            _slots.Add(view);
        }

        private static void BindSlot(SlotView view, Inventory.Slot slot, int visibleIndex)
        {
            bool hasItem = slot != null && slot.Item != null && slot.Count > 0;
            view.Slot = hasItem ? slot : null;
            view.Root.name = hasItem ? "Slot_" + slot.Item.Id : "Slot_Empty_" + visibleIndex;
            view.Icon.sprite = hasItem ? slot.Item.Icon : null;
            view.Icon.enabled = view.Icon.sprite != null;
            bool showCount = hasItem && slot.Count > 1;
            view.Count.text = showCount ? slot.Count.ToString() : string.Empty;
            view.Count.gameObject.SetActive(showCount);
            view.Button.interactable = hasItem;
        }

        private void OpenPopup(Inventory.Slot slot)
        {
            ClosePopup();
            if (slot == null || slot.Item == null || slot.Count <= 0)
            {
                return;
            }

            var rootRect = (RectTransform)transform;
            var go = new GameObject("ItemDetailPopup", typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(rootRect.sizeDelta.x * 0.5f + 32f, 0f);
            rect.sizeDelta = new Vector2(480f, 400f);

            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel48);
            tiles.Rebuild();

            var itemIcon = MakeImage(rect, "ItemIcon", new Vector2(36f, -36f), new Vector2(104f, 104f));
            itemIcon.sprite = slot.Item.Icon;
            itemIcon.enabled = itemIcon.sprite != null;
            var itemName = MakeLabel(rect, "ItemName", GameText.Get(slot.Item.DisplayKey), new Vector2(156f, -36f), new Vector2(280f, 56f), 36, TextAnchor.UpperLeft);
            itemName.resizeTextForBestFit = true;
            itemName.resizeTextMinSize = 24;
            itemName.resizeTextMaxSize = 36;
            string count = GameText.Get("ui.inventory.item-count");
            if (count == "ui.inventory.item-count") count = "Count: {0}";
            MakeLabel(rect, "ItemCount", string.Format(count, slot.Count), new Vector2(156f, -96f), new Vector2(280f, 48f), 28, TextAnchor.UpperLeft);
            MakeLabel(rect, "ItemDescription", slot.Item.Description, new Vector2(36f, -164f), new Vector2(408f, 140f), 28, TextAnchor.UpperLeft);
            var closeRoot = ModernUiPanelBuilder.CreateCommonPanel48Button(rect, "ActionButton", new Vector2(0f, -168f), new Vector2(192f, 64f));
            string closeText = GameText.Get("ui.inventory.close-detail");
            if (closeText == "ui.inventory.close-detail") closeText = GameText.Language == "ko" ? "닫기" : "Close";
            var closeLabel = MakeLabel((RectTransform)closeRoot.transform, "Label", closeText, new Vector2(16f, -12f), new Vector2(112f, 40f), 26, TextAnchor.MiddleCenter);
            closeLabel.raycastTarget = false;
            var closeIcon = MakeImage((RectTransform)closeRoot.transform, "Icon", new Vector2(144f, -16f), new Vector2(32f, 32f));
            closeIcon.gameObject.AddComponent<UrbanUiIcon>().Configure("close");
            closeRoot.GetComponent<Button>().onClick.AddListener(ClosePopup);
            closeRoot.GetComponent<ModernUiTileImage>().Rebuild();

            _popup = go;
        }

        private void ClosePopup()
        {
            if (_popup == null)
            {
                return;
            }

            // Destroy is deferred in PlayMode: remove its hit targets and hierarchy entry immediately.
            _popup.SetActive(false);
            _popup.transform.SetParent(null, false);
            DestroyObject(_popup);
            _popup = null;
        }

        private static Sprite ResolveSprite(string subSpriteName)
        {
            var resourceManager = Managers.Resource;
            if (resourceManager == null)
            {
                return null;
            }

            var cached = resourceManager.GetCachedSubSprite(ModernUiStyle2Sprites.SheetAddress, subSpriteName);
            if (cached != null)
            {
                return cached;
            }

            var load = resourceManager.LoadSubSpriteAsync(ModernUiStyle2Sprites.SheetAddress, subSpriteName);
            return load.IsCompleted && !load.IsFaulted && !load.IsCanceled ? load.Result : null;
        }

        private static Text MakeLabel(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value ?? string.Empty;
            text.font = Rootborn.UI.StudentLife.LifeUiFont.Shared;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.color = new Color(0.12f, 0.18f, 0.25f, 1f);
            return text;
        }

        private static Image MakeImage(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void DestroyObject(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }
    }
}
