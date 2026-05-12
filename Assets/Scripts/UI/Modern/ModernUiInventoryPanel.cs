using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Modern
{
    [DisallowMultipleComponent]
    public sealed class ModernUiInventoryPanel : MonoBehaviour
    {
        private const int VisibleSlotCount = 16;
        private static readonly Vector2 DefaultSize = new Vector2(520f, 420f);

        private readonly List<GameObject> _generated = new List<GameObject>();
        private PlayerInventory _inventory;
        private RectTransform _slotGrid;
        private GameObject _popup;
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

            var rect = transform as RectTransform;
            if (rect != null && rect.sizeDelta == Vector2.zero)
            {
                rect.sizeDelta = DefaultSize;
            }

            MakeTilePanel("InventoryTitleTab", new Vector2(0f, 174f), new Vector2(200f, 42f));
            _slotGrid = (RectTransform)MakeTilePanel("InventorySlotGrid", new Vector2(-54f, 34f), new Vector2(344f, 288f)).transform;
            MakeTilePanel("InventoryScrollbar", new Vector2(158f, 34f), new Vector2(28f, 288f));
            MakeTilePanel("InventoryBottomControls", new Vector2(0f, -164f), new Vector2(420f, 54f));
            MakeLabel((RectTransform)transform, "InventoryTitle", "INVENTORY", new Vector2(0f, 174f), new Vector2(176f, 28f), 18, TextAnchor.MiddleCenter);
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
            float x = (visibleIndex % 4) * 84f;
            float y = -(visibleIndex / 4) * 68f;
            rect.anchoredPosition = new Vector2(6f + x, -8f + y);
            rect.sizeDelta = new Vector2(68f, 60f);

            var image = go.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();

            var icon = MakeImage(rect, "Icon", Vector2.zero, new Vector2(38f, 34f));
            icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            icon.sprite = hasItem ? slot.Item.Icon : null;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var count = MakeLabel(rect, "Count", hasItem && slot.Count > 1 ? slot.Count.ToString() : string.Empty,
                new Vector2(18f, -16f), new Vector2(44f, 20f), 12, TextAnchor.LowerRight);
            count.raycastTarget = false;

            var button = go.GetComponent<Button>();
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

            _popup = MakeTilePanel("ItemDetailPopup", new Vector2(168f, -28f), new Vector2(240f, 188f));
            var popupRect = (RectTransform)_popup.transform;
            MakeImage(popupRect, "ItemIcon", new Vector2(18f, -18f), new Vector2(52f, 52f)).sprite = slot.Item.Icon;
            MakeLabel(popupRect, "ItemName", slot.Item.DisplayKey, new Vector2(78f, -18f), new Vector2(140f, 28f), 16, TextAnchor.UpperLeft);
            MakeLabel(popupRect, "ItemCount", "Count: " + slot.Count, new Vector2(78f, -48f), new Vector2(140f, 24f), 13, TextAnchor.UpperLeft);
            MakeLabel(popupRect, "ItemDescription", slot.Item.Description, new Vector2(18f, -82f), new Vector2(204f, 58f), 12, TextAnchor.UpperLeft);
            MakeActionButton(popupRect);
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

        private void MakeActionButton(RectTransform parent)
        {
            var go = new GameObject("ActionButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(60f, -146f);
            rect.sizeDelta = new Vector2(120f, 30f);
            go.GetComponent<Image>().color = Color.clear;
            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            MakeLabel(rect, "Label", "USE", Vector2.zero, new Vector2(120f, 28f), 13, TextAnchor.MiddleCenter);
        }

        private GameObject MakeTilePanel(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            _generated.Add(go);
            return go;
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
            return go.GetComponent<Image>();
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
