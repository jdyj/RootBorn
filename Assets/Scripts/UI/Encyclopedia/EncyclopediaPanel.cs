using System.Collections.Generic;
using Rootborn.Game.Encyclopedia;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Encyclopedia
{
    [DisallowMultipleComponent]
    public sealed class EncyclopediaPanel : MonoBehaviour
    {
        private const int PageSize = 24;
        private static readonly Vector2 DefaultSize = new Vector2(820f, 560f);

        private readonly List<Button> _rowPool = new List<Button>(PageSize);
        private readonly List<Button> _categoryButtons = new List<Button>();
        private EncyclopediaIndex _index;
        private EncyclopediaProgress _progress;
        private EncyclopediaCategoryDefinition _selectedCategory;
        private RectTransform _categoryRoot;
        private RectTransform _rowRoot;
        private Text _title;
        private Text _detailTitle;
        private Text _detailBody;
        private bool _built;
        private bool _isVisible;

        public bool IsVisible => _isVisible;

        public void Bind(EncyclopediaIndex index, EncyclopediaProgress progress)
        {
            _index = index;
            _progress = progress;
            if (_index != null && _index.Categories.Count > 0) _selectedCategory = _index.Categories[0];
            if (_built) RefreshAll();
        }

        public void Show()
        {
            EnsureBuilt();
            gameObject.SetActive(true);
            _isVisible = true;
            RefreshAll();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            _isVisible = false;
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            var rect = transform as RectTransform;
            if (rect != null && rect.sizeDelta == Vector2.zero) rect.sizeDelta = DefaultSize;
            var background = gameObject.GetComponent<ModernUiTileImage>();
            if (background == null) background = gameObject.AddComponent<ModernUiTileImage>();
            background.SetRecipe(ModernUiRecipes.CommonPanel);
            background.Rebuild();

            _title = MakeLabel((RectTransform)transform, "EncyclopediaTitle", "ENCYCLOPEDIA", new Vector2(0f, 250f), new Vector2(780f, 36f), 22, TextAnchor.MiddleCenter);
            _categoryRoot = (RectTransform)MakePanel("CategoryTabs", new Vector2(-278f, 176f), new Vector2(210f, 286f)).transform;
            _rowRoot = (RectTransform)MakePanel("EntryRows", new Vector2(-50f, -8f), new Vector2(330f, 404f)).transform;
            var detail = (RectTransform)MakePanel("EntryDetail", new Vector2(248f, -8f), new Vector2(236f, 404f)).transform;
            _detailTitle = MakeLabel(detail, "DetailTitle", "???", new Vector2(12f, -16f), new Vector2(212f, 34f), 18, TextAnchor.UpperLeft);
            _detailBody = MakeLabel(detail, "DetailBody", string.Empty, new Vector2(12f, -62f), new Vector2(212f, 300f), 14, TextAnchor.UpperLeft);
            _detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailBody.verticalOverflow = VerticalWrapMode.Truncate;
            for (int i = 0; i < PageSize; i++) _rowPool.Add(MakeRowButton(i));
            _built = true;
        }

        private void RefreshAll()
        {
            if (!_built) return;
            RefreshCategories();
            RefreshRows();
        }

        private void RefreshCategories()
        {
            for (int i = 0; i < _categoryButtons.Count; i++) DestroyObject(_categoryButtons[i].gameObject);
            _categoryButtons.Clear();
            if (_index == null) return;
            for (int i = 0; i < _index.Categories.Count; i++)
            {
                var category = _index.Categories[i];
                var button = MakeButton(_categoryRoot, "Category_" + category.Id, category.DisplayName, new Vector2(8f, -10f - i * 42f), new Vector2(194f, 34f), 13);
                var captured = category;
                button.onClick.AddListener(() => SelectCategory(captured));
                _categoryButtons.Add(button);
            }
        }

        private void SelectCategory(EncyclopediaCategoryDefinition category)
        {
            _selectedCategory = category;
            RefreshRows();
        }

        private void RefreshRows()
        {
            for (int i = 0; i < _rowPool.Count; i++) _rowPool[i].gameObject.SetActive(false);
            if (_index == null || _selectedCategory == null) return;
            var rows = _index.GetVisibleRows(_selectedCategory, _progress, 0, PageSize);
            for (int i = 0; i < rows.Count && i < _rowPool.Count; i++)
            {
                var row = rows[i];
                var button = _rowPool[i];
                button.name = "Entry_" + row.Entry.Id;
                button.gameObject.SetActive(true);
                SetButtonText(button, (row.IsNew ? "NEW " : string.Empty) + row.Title);
                button.onClick.RemoveAllListeners();
                var captured = row.Entry;
                button.onClick.AddListener(() => SelectEntry(captured));
            }
            if (rows.Count > 0) SelectEntry(rows[0].Entry);
        }

        private void SelectEntry(EncyclopediaEntryDefinition entry)
        {
            if (entry == null) return;
            bool unlocked = _progress != null && _progress.IsUnlocked(entry.Id);
            if (unlocked && _progress.MarkSeen(entry.Id))
            {
                EncyclopediaProgressPersistence.Save(_progress);
            }
            _detailTitle.text = unlocked ? entry.DisplayName : entry.LockedDisplayName;
            _detailBody.text = unlocked ? BuildDetailText(entry) : entry.LockedHint;
            RefreshRowLabelsOnly();
        }

        private void RefreshRowLabelsOnly()
        {
            if (_index == null || _selectedCategory == null) return;
            var rows = _index.GetVisibleRows(_selectedCategory, _progress, 0, PageSize);
            for (int i = 0; i < rows.Count && i < _rowPool.Count; i++) SetButtonText(_rowPool[i], (rows[i].IsNew ? "NEW " : string.Empty) + rows[i].Title);
        }

        private static string BuildDetailText(EncyclopediaEntryDefinition entry)
        {
            string text = entry.Description;
            for (int i = 0; i < entry.DetailLines.Count; i++) if (!string.IsNullOrEmpty(entry.DetailLines[i])) text += "\n" + entry.DetailLines[i];
            return text;
        }

        private GameObject MakePanel(string name, Vector2 position, Vector2 size)
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
            return go;
        }

        private Button MakeRowButton(int index) => MakeButton(_rowRoot, "EntryRow_" + index, string.Empty, new Vector2(8f, -10f - index * 16f), new Vector2(314f, 15f), 11);

        private static Button MakeButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = Color.clear;
            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            MakeLabel(rect, "Label", label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter);
            return go.GetComponent<Button>();
        }

        private static void SetButtonText(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text ?? string.Empty;
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

        private static void DestroyObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }
}
