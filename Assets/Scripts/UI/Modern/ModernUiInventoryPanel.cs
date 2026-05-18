using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using UnityEngine;
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
        private const int SlotRows = 4;
        // Visible rows = how many slot rows the panel shows at once. Anything beyond
        // VisibleRows still exists in the data — the scrollbar is reserved for it.
        private const int VisibleRows = 3;
        private const int VisibleSlotCount = SlotColumns * SlotRows;
        // Slot size is 1.5x of the surrounding UI (168 vs ~112 baseline). The panel's
        // DefaultSize is computed from VisibleRows so growing the slot does not balloon
        // the panel height (only the slot grid extends past the visible area).
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
            GridPadTop + VisibleRows * SlotSize + (VisibleRows - 1) * SlotSpacing + 28f);

        // Four category tabs, left to right.
        private static readonly string[] TabIconSubSpriteNames = new[]
        {
            "ModernUI_16_Style2_r0_c22", // item.bag        — All / Inventory
            "ModernUI_16_Style2_r7_c20", // item.swordGreen — Weapons / tools
            "ModernUI_16_Style2_r6_c20", // item.potionRed  — Consumables
            "ModernUI_16_Style2_r0_c24", // glyph.settings  — Misc
        };

        // Inner panel padding so its 9-slice border doesn't crowd the slot grid.
        private const float InnerPadding = 12f;

        private readonly List<GameObject> _generated = new List<GameObject>();
        private PlayerInventory _inventory;
        private RectTransform _innerContainer;
        private RectTransform _slotGrid;
        private GameObject _popup;
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
            if (_inventory != null)
            {
                _inventory.Inventory.OnChanged -= Refresh;
            }

            _inventory = inventory;
            if (_inventory != null)
            {
                _inventory.Inventory.OnChanged += Refresh;
            }

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
            gameObject.SetActive(false);
            _isVisible = false;
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.Inventory.OnChanged -= Refresh;
            }
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            var root = transform as RectTransform;
            if (root != null && root.sizeDelta == Vector2.zero)
            {
                root.sizeDelta = DefaultSize;
            }

            BuildRootPanel();
            BuildTabStrip();
            BuildSearchRow();
            BuildInnerPanel();
            BuildSlotGrid();
            BuildScrollbar();

            _built = true;
        }

        public void Refresh()
        {
            if (!_built || _slotGrid == null)
            {
                return;
            }

            for (int i = _slotGrid.childCount - 1; i >= 0; i--)
            {
                DestroyObject(_slotGrid.GetChild(i).gameObject);
            }

            int visibleIndex = 0;
            if (_inventory != null)
            {
                var slots = _inventory.Inventory.Slots;
                for (int i = 0; i < slots.Count && visibleIndex < VisibleSlotCount; i++)
                {
                    var slot = slots[i];
                    if (slot.Item == null || slot.Count <= 0)
                    {
                        continue;
                    }

                    MakeSlot(slot, visibleIndex++);
                }
            }

            while (visibleIndex < VisibleSlotCount)
            {
                MakeSlot(null, visibleIndex++);
            }
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
                icon.transform.SetAsLastSibling();

                int capturedIndex = i;
                var btn = go.GetComponent<Button>();
                btn.targetGraphic = hit;
                btn.onClick.AddListener(() => SelectTab(capturedIndex));

                _generated.Add(go);
            }
        }

        private static void BuildTabCells(RectTransform parent, bool selected)
        {
            // The 6 cells (r11_c0..r12_c2) each render at 1/3 of the tab width and
            // proportionally for height — so every sprite scales together with the tab,
            // including the corners. All cells use Image.Type.Simple stretch (Tiled was
            // hiding the corner sprites when the cell wasn't a multiple of 16).
            float w = parent.sizeDelta.x;
            float h = parent.sizeDelta.y;
            float colW = w / 3f;
            float topH = h / 3f;   // top row = top third of the tab
            float bodyH = h - topH; // body row = remaining two thirds

            Color tint = selected ? Color.white : new Color(0.78f, 0.74f, 0.66f, 1f);

            MakeTabCell(parent, "Cell_TL", ModernUiStyle2Sprites.TabPanel.TopLeftName,
                new Vector2(0f, 0f), new Vector2(colW, topH), tint);
            MakeTabCell(parent, "Cell_T", ModernUiStyle2Sprites.TabPanel.TopName,
                new Vector2(colW, 0f), new Vector2(colW, topH), tint);
            MakeTabCell(parent, "Cell_TR", ModernUiStyle2Sprites.TabPanel.TopRightName,
                new Vector2(colW * 2f, 0f), new Vector2(colW, topH), tint);
            MakeTabCell(parent, "Cell_L", ModernUiStyle2Sprites.TabPanel.BodyLeftName,
                new Vector2(0f, -topH), new Vector2(colW, bodyH), tint);
            MakeTabCell(parent, "Cell_M", ModernUiStyle2Sprites.TabPanel.BodyFillName,
                new Vector2(colW, -topH), new Vector2(colW, bodyH), tint);
            MakeTabCell(parent, "Cell_R", ModernUiStyle2Sprites.TabPanel.BodyRightName,
                new Vector2(colW * 2f, -topH), new Vector2(colW, bodyH), tint);
        }

        private static void MakeTabCell(RectTransform parent, string name, string subSpriteName, Vector2 topLeft, Vector2 size, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = ResolveSprite(subSpriteName);
            image.type = Image.Type.Simple;
            image.color = tint;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        private void SelectTab(int index)
        {
            _selectedTab = index;
            Color sel = Color.white;
            Color dim = new Color(0.78f, 0.74f, 0.66f, 1f);
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child.name.StartsWith("Tab_"))
                {
                    continue;
                }
                bool selected = child.name == "Tab_" + index;
                Color tint = selected ? sel : dim;
                for (int j = 0; j < child.childCount; j++)
                {
                    var cell = child.GetChild(j);
                    if (!cell.name.StartsWith("Cell_"))
                    {
                        continue;
                    }
                    var img = cell.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = tint;
                    }
                }
            }
            Refresh();
        }

        private void BuildSearchRow()
        {
            var rect = (RectTransform)transform;
            float rowY = rect.sizeDelta.y - GridPadTop + SlotSpacing;
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
            search.raycastTarget = false;

            var searchIcon = MakeImage((RectTransform)search.transform, "Icon",
                new Vector2(gap, -((SearchBarHeight - iconSize) * 0.5f)), new Vector2(iconSize, iconSize));
            searchIcon.sprite = ResolveSprite(ModernUiStyle2Sprites.IconButton.SearchName);
            searchIcon.enabled = searchIcon.sprite != null;
            searchIcon.preserveAspect = true;
            searchIcon.raycastTarget = false;

            // Delete (trash) button on the right.
            var deleteRoot = MakeImage((RectTransform)transform, "DeleteButton",
                new Vector2(rowLeft + searchWidth + gap, rowY), new Vector2(SearchBarHeight, SearchBarHeight));
            deleteRoot.sprite = ResolveSprite(ModernUiStyle2Sprites.Slot.BaseName);
            deleteRoot.type = Image.Type.Sliced;
            deleteRoot.color = Color.white;
            deleteRoot.raycastTarget = true;

            var deleteIcon = MakeImage((RectTransform)deleteRoot.transform, "Icon",
                new Vector2((SearchBarHeight - iconSize) * 0.5f, -((SearchBarHeight - iconSize) * 0.5f)),
                new Vector2(iconSize, iconSize));
            deleteIcon.sprite = ResolveSprite(ModernUiStyle2Sprites.IconButton.DeleteName);
            deleteIcon.enabled = deleteIcon.sprite != null;
            deleteIcon.preserveAspect = true;
            deleteIcon.raycastTarget = false;

            // Filter/settings button.
            var filterRoot = MakeImage((RectTransform)transform, "FilterButton",
                new Vector2(rowLeft + searchWidth + gap + SearchBarHeight + gap, rowY),
                new Vector2(SearchBarHeight, SearchBarHeight));
            filterRoot.sprite = ResolveSprite(ModernUiStyle2Sprites.Slot.BaseName);
            filterRoot.type = Image.Type.Sliced;
            filterRoot.color = Color.white;
            filterRoot.raycastTarget = true;

            var filterIcon = MakeImage((RectTransform)filterRoot.transform, "Icon",
                new Vector2((SearchBarHeight - iconSize) * 0.5f, -((SearchBarHeight - iconSize) * 0.5f)),
                new Vector2(iconSize, iconSize));
            filterIcon.sprite = ResolveSprite(ModernUiStyle2Sprites.IconButton.SortName);
            filterIcon.enabled = filterIcon.sprite != null;
            filterIcon.preserveAspect = true;
            filterIcon.raycastTarget = false;
        }

        private void BuildInnerPanel()
        {
            // Builds the InnerPanel 9-slice (r8..r10, c0..c2) that wraps the slot grid +
            // scrollbar. Sits inside the main beige panel below the search bar.
            var root = (RectTransform)transform;
            float gridWidth = SlotColumns * SlotSize + (SlotColumns - 1) * SlotSpacing;
            float gridHeight = SlotRows * SlotSize + (SlotRows - 1) * SlotSpacing;
            float innerWidth = gridWidth + ScrollbarWidth + InnerPadding * 3f;
            float innerHeight = gridHeight + InnerPadding * 2f;

            var go = new GameObject("InventoryInnerPanel", typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(GridPadX - InnerPadding, -(GridPadTop - InnerPadding));
            r.sizeDelta = new Vector2(innerWidth, innerHeight);

            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.InnerPanel);
            tiles.SetTileSize(new Vector2(32f, 32f)); // match root panel's 2x tile size.
            tiles.Rebuild();

            _innerContainer = r;
            _generated.Add(go);
        }

        private void BuildSlotGrid()
        {
            float gridWidth = SlotColumns * SlotSize + (SlotColumns - 1) * SlotSpacing;
            float gridHeight = SlotRows * SlotSize + (SlotRows - 1) * SlotSpacing;

            var go = new GameObject("SlotGrid", typeof(RectTransform));
            go.transform.SetParent(_innerContainer, false);
            var gridRect = (RectTransform)go.transform;
            gridRect.anchorMin = new Vector2(0f, 1f);
            gridRect.anchorMax = new Vector2(0f, 1f);
            gridRect.pivot = new Vector2(0f, 1f);
            gridRect.anchoredPosition = new Vector2(InnerPadding, -InnerPadding);
            gridRect.sizeDelta = new Vector2(gridWidth, gridHeight);

            _slotGrid = gridRect;
            _generated.Add(go);
        }

        private void BuildScrollbar()
        {
            float gridHeight = SlotRows * SlotSize + (SlotRows - 1) * SlotSpacing;
            float left = _innerContainer.sizeDelta.x - InnerPadding - ScrollbarWidth;

            // Track is built from 3 stacked sprites: top cap / middle (stretched) / bottom cap.
            const float capHeight = 32f;
            float midHeight = Mathf.Max(8f, gridHeight - capHeight * 2f);
            float top = -InnerPadding;

            var topCap = MakeImage(_innerContainer, "ScrollTrackTop",
                new Vector2(left, top), new Vector2(ScrollbarWidth, capHeight));
            topCap.sprite = ResolveSprite(ModernUiStyle2Sprites.Scrollbar.TrackTopName);
            topCap.preserveAspect = false;

            var midPart = MakeImage(_innerContainer, "ScrollTrackMid",
                new Vector2(left, top - capHeight), new Vector2(ScrollbarWidth, midHeight));
            midPart.sprite = ResolveSprite(ModernUiStyle2Sprites.Scrollbar.TrackMiddleName);
            midPart.type = Image.Type.Tiled;
            midPart.preserveAspect = false;

            var botCap = MakeImage(_innerContainer, "ScrollTrackBot",
                new Vector2(left, top - capHeight - midHeight), new Vector2(ScrollbarWidth, capHeight));
            botCap.sprite = ResolveSprite(ModernUiStyle2Sprites.Scrollbar.TrackBottomName);
            botCap.preserveAspect = false;
        }

        private GameObject MakeSlot(Inventory.Slot slot, int visibleIndex)
        {
            bool hasItem = slot != null && slot.Item != null && slot.Count > 0;
            var go = new GameObject(hasItem ? "Slot_" + slot.Item.Id : "Slot_Empty_" + visibleIndex,
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

            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.SlotPanel);
            tiles.SetTileSize(new Vector2(SlotSize / 3f, SlotSize / 3f));
            tiles.Rebuild();

            var icon = MakeImage(rect, "Icon", Vector2.zero, new Vector2(SlotSize - 32f, SlotSize - 32f));
            icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.sprite = hasItem ? slot.Item.Icon : null;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            if (hasItem && slot.Count > 1)
            {
                var count = MakeLabel(rect, "Count", slot.Count.ToString(),
                    new Vector2(SlotSize - 44f, -SlotSize + 8f), new Vector2(44f, 32f), 24, TextAnchor.LowerRight);
                count.raycastTarget = false;
                count.color = new Color(0.95f, 0.95f, 0.95f, 1f);
                var shadow = count.gameObject.AddComponent<UnityEngine.UI.Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
                shadow.effectDistance = new Vector2(1f, -1f);
            }

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            if (hasItem)
            {
                var captured = slot;
                button.onClick.AddListener(() => OpenPopup(captured));
            }
            else
            {
                button.onClick.AddListener(ClosePopup);
            }

            return go;
        }

        private void OpenPopup(Inventory.Slot slot)
        {
            ClosePopup();
            if (slot == null || slot.Item == null)
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

            MakeImage(rect, "ItemIcon", new Vector2(36f, -36f), new Vector2(104f, 104f)).sprite = slot.Item.Icon;
            MakeLabel(rect, "ItemName", slot.Item.DisplayKey, new Vector2(156f, -36f), new Vector2(280f, 56f), 36, TextAnchor.UpperLeft);
            MakeLabel(rect, "ItemCount", "Count: " + slot.Count, new Vector2(156f, -96f), new Vector2(280f, 48f), 28, TextAnchor.UpperLeft);
            MakeLabel(rect, "ItemDescription", slot.Item.Description, new Vector2(36f, -164f), new Vector2(408f, 140f), 28, TextAnchor.UpperLeft);
            ModernUiPanelBuilder.CreateCommonPanel48Button(rect, "ActionButton", new Vector2(0f, -168f), new Vector2(192f, 64f));

            _popup = go;
            _generated.Add(go);
        }

        private void ClosePopup()
        {
            if (_popup == null)
            {
                return;
            }

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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.28f, 0.19f, 0.12f, 1f);
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
