using Rootborn.Game.Localization;
using Rootborn.UI.StudentLife;
using Rootborn.Game.Common;
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
        private static readonly Vector2 DefaultSize = new Vector2(1280f, 800f);

        private readonly List<Button> _rowPool = new List<Button>(PageSize);
        private readonly List<Button> _categoryButtons = new List<Button>();
        private EncyclopediaIndex _index;
        private EncyclopediaProgress _progress;
        private EncyclopediaCategoryDefinition _selectedCategory;
        private RectTransform _categoryRoot;
        private RectTransform _rowRoot;
        private RectTransform _rowViewport;
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
            transform.SetAsLastSibling();
            UiGameplayInputLock.Acquire(this);
            _title.text = Ui("ui.encyclopedia.open", "도감", "Encyclopedia");
            RefreshAll();
        }

        public void Hide()
        {
            UiGameplayInputLock.Release(this);
            gameObject.SetActive(false);
            _isVisible = false;
        }

        private void OnDisable() => UiGameplayInputLock.Release(this);
        private void OnDestroy() => UiGameplayInputLock.Release(this);

        private static string Ui(string key, string ko, string en)
        {
            string value = GameText.Get(key);
            return value == key ? (GameText.Language == "ko" ? ko : en) : value;
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            var rect = transform as RectTransform;
            if (rect != null) rect.sizeDelta = new Vector2(Mathf.Max(DefaultSize.x, rect.sizeDelta.x), Mathf.Max(DefaultSize.y, rect.sizeDelta.y));
            var background = gameObject.GetComponent<ModernUiTileImage>();
            if (background == null) background = gameObject.AddComponent<ModernUiTileImage>();
            background.SetRecipe(ModernUiRecipes.CommonPanel);
            background.Rebuild();

            var icon = new GameObject("HeaderIcon", typeof(RectTransform), typeof(Image), typeof(UrbanUiIcon));
            icon.transform.SetParent(transform, false);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 1f); iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(48f, -20f); iconRect.sizeDelta = new Vector2(64f, 64f);
            icon.GetComponent<Image>().raycastTarget = false; icon.GetComponent<Image>().preserveAspect = true;
            icon.GetComponent<UrbanUiIcon>().Configure("encyclopedia");
            _title = MakeLabel((RectTransform)transform, "EncyclopediaTitle", Ui("ui.encyclopedia.open", "도감", "Encyclopedia"), new Vector2(136f, -28f), new Vector2(1000f, 48f), 32, TextAnchor.MiddleCenter);
            var close = MakeButton((RectTransform)transform, "CloseButton", "×", new Vector2(1200f, -28f), new Vector2(48f, 48f), 28);
            close.onClick.AddListener(Hide);
            _categoryRoot = (RectTransform)MakePanel("CategoryTabs", new Vector2(-442f, -48f), new Vector2(300f, 632f)).transform;
            var rowsFrame = (RectTransform)MakePanel("EntryRows", new Vector2(-52f, -48f), new Vector2(432f, 632f)).transform;
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect));
            viewportObject.transform.SetParent(rowsFrame, false);
            _rowViewport = (RectTransform)viewportObject.transform;
            _rowViewport.anchorMin = Vector2.zero;
            _rowViewport.anchorMax = Vector2.one;
            _rowViewport.offsetMin = new Vector2(32f, 32f);
            _rowViewport.offsetMax = new Vector2(-32f, -32f);
            viewportObject.GetComponent<Image>().color = Color.clear;
            var scroll = viewportObject.GetComponent<ScrollRect>();
            var content = new GameObject("RowsContent", typeof(RectTransform)); content.transform.SetParent(_rowViewport, false);
            _rowRoot = (RectTransform)content.transform;
            _rowRoot.anchorMin = _rowRoot.anchorMax = new Vector2(.5f, 1f);
            _rowRoot.pivot = new Vector2(.5f, 1f);
            _rowRoot.sizeDelta = new Vector2(368f, 568f);
            scroll.viewport = _rowViewport; scroll.content = _rowRoot; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32f;
            var detail = (RectTransform)MakePanel("EntryDetail", new Vector2(388f, -48f), new Vector2(400f, 632f)).transform;
            _detailTitle = MakeLabel(detail, "DetailTitle", "???", new Vector2(32f, -32f), new Vector2(336f, 44f), 26, TextAnchor.UpperLeft);
            _detailBody = MakeLabel(detail, "DetailBody", string.Empty, new Vector2(32f, -96f), new Vector2(336f, 488f), 20, TextAnchor.UpperLeft);
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
                var button = MakeButton(_categoryRoot, "Category_" + category.Id, ResolveText(category.DisplayName), new Vector2(32f, -32f - i * 64f), new Vector2(236f, 48f), 22);
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
            if (_index == null || _selectedCategory == null) { ResizeRowContent(0); ShowEmptyDetail(); return; }
            var rows = _index.GetVisibleRows(_selectedCategory, _progress, 0, PageSize);
            for (int i = 0; i < rows.Count && i < _rowPool.Count; i++)
            {
                var row = rows[i];
                var button = _rowPool[i];
                button.name = "Entry_" + row.Entry.Id;
                button.gameObject.SetActive(true);
                SetButtonText(button, RowLabel(row.IsNew, row.Title));
                button.onClick.RemoveAllListeners();
                var captured = row.Entry;
                button.onClick.AddListener(() => SelectEntry(captured));
            }
            ResizeRowContent(rows.Count);
            if (rows.Count > 0) SelectEntry(rows[0].Entry);
            else ShowEmptyDetail();
        }

        private void SelectEntry(EncyclopediaEntryDefinition entry)
        {
            if (entry == null) return;
            bool unlocked = _progress != null && _progress.IsUnlocked(entry.Id);
            if (unlocked && _progress.MarkSeen(entry.Id))
            {
                EncyclopediaProgressPersistence.Save(_progress);
            }
            _detailTitle.text = ResolveText(unlocked ? entry.DisplayName : entry.LockedDisplayName);
            _detailBody.text = unlocked ? BuildDetailText(entry) : ResolveText(entry.LockedHint);
            RefreshRowLabelsOnly();
        }

        private void RefreshRowLabelsOnly()
        {
            if (_index == null || _selectedCategory == null) return;
            var rows = _index.GetVisibleRows(_selectedCategory, _progress, 0, PageSize);
            for (int i = 0; i < rows.Count && i < _rowPool.Count; i++) SetButtonText(_rowPool[i], RowLabel(rows[i].IsNew, rows[i].Title));
        }

        private void ShowEmptyDetail()
        {
            _detailTitle.text = Ui("ui.encyclopedia.empty.title", "아직 기록이 없어요", "No entries yet");
            _detailBody.text = Ui("ui.encyclopedia.empty.hint", "마을을 탐험하며 이 분류의 기록을 모아 보세요.", "Explore the town to collect entries in this category.");
        }

        private void ResizeRowContent(int count)
        {
            if (_rowRoot == null || _rowViewport == null) return;
            _rowRoot.sizeDelta = new Vector2(_rowViewport.rect.width, Mathf.Max(_rowViewport.rect.height, count * 56f + 16f));
            _rowRoot.anchoredPosition = Vector2.zero;
        }

        private static string ResolveText(string key) => string.IsNullOrEmpty(key) ? string.Empty : GameText.Get(key);
        private static string RowLabel(bool isNew, string title) => (isNew ? Ui("ui.encyclopedia.new", "새 항목", "NEW") + " " : string.Empty) + ResolveText(title);

        private static string BuildDetailText(EncyclopediaEntryDefinition entry)
        {
            string text = ResolveText(entry.Description);
            for (int i = 0; i < entry.DetailLines.Count; i++) if (!string.IsNullOrEmpty(entry.DetailLines[i])) text += "\n" + ResolveText(entry.DetailLines[i]);
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

        private Button MakeRowButton(int index) => MakeButton(_rowRoot, "EntryRow_" + index, string.Empty, new Vector2(0f, -8f - index * 56f), new Vector2(368f, 48f), 22);

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
            text.font = LifeUiFont.Shared;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.color = new Color32(48, 71, 83, 255);
            return text;
        }

        private static void DestroyObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }
}
