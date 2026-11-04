using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Localization;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.MainMenu
{
    public sealed class SpumCharacterCreatorPanel : MonoBehaviour
    {
        public const int VisiblePartCellBudget = 12;

        private static readonly CategoryGroup[] CategoryGroups =
        {
            new CategoryGroup("spum.creator.category.body-skin", "Body/Skin", "Tab_BodySkin", new[] { "body", "skin" }),
            new CategoryGroup("spum.creator.category.eye", "Eye", "Tab_Eye", new[] { "eye", "eyes" }),
            new CategoryGroup("spum.creator.category.hair", "Hair", "Tab_Hair", new[] { "hair" }),
            new CategoryGroup("spum.creator.category.outfit-cloth", "Outfit/Cloth", "Tab_OutfitCloth", new[] { "outfit", "cloth" }),
            new CategoryGroup("spum.creator.category.accessory", "Accessory", "Tab_AccessoryBackHelmet", new[] { "accessory", "back", "helmet" }),
        };

        private readonly Dictionary<string, string> _selectedPartIds = new Dictionary<string, string>();
        private SpumPartCatalogDefinition _catalog;
        private Transform _partGrid;
        private Transform _selectedPartChips;
        private Text _statusLine;
        private GameObject _nextPageButton;
        private GameObject _previousPageButton;
        private string[] _selectedCategoryIds = Array.Empty<string>();
        private int _currentPageIndex;
        private Action<CharacterAppearanceSnapshot> _confirm;
        private Action _cancel;
        private SpumCharacterCreatorPreview _preview;

        public ModernUiTileRecipe RootPanelRecipe => ModernUiRecipes.CommonPanel;
        public int CurrentPageIndex => _currentPageIndex;

        public void Build(SpumPartCatalogDefinition catalog)
        {
            Build(catalog, null, null);
        }

        public void Build(SpumPartCatalogDefinition catalog, Action<CharacterAppearanceSnapshot> onConfirm, Action onCancel)
        {
            _catalog = catalog;
            _confirm = onConfirm;
            _cancel = onCancel;
            _currentPageIndex = 0;
            _selectedPartIds.Clear();
            ClearGeneratedChildren(transform);
            EnsureDefaultSelections();
            EnsureModalBackdrop();

            GameObject root = CreatePanel(transform, "SpumCharacterCreatorRoot", new Vector2(1296f, 864f));
            CreateHeader(root.transform);
            CreateCategoryTabs(root.transform);
            CreatePreview(root.transform);
            _partGrid = CreateContainer(root.transform, "PartGrid", new Vector2(600f, 500f), new Vector2(-60f, 40f)).transform;
            CreatePageControls(root.transform);
            CreateFooter(root.transform);

            CategoryGroup? firstGroup = FindFirstAvailableGroup();
            if (firstGroup.HasValue)
            {
                SelectCategory(firstGroup.Value.CategoryIds[0]);
            }
            else
            {
                RefreshSelectionSurfaces();
            }
        }

        public void SelectCategory(string categoryId)
        {
            _selectedCategoryIds = ResolveCategoryIds(categoryId);
            _currentPageIndex = 0;
            RebuildPartGrid();
            RefreshSelectionSurfaces();
        }

        public void NextPage()
        {
            int maxPage = MaxPageIndex(CurrentSelectedParts().Count);
            _currentPageIndex = Mathf.Min(_currentPageIndex + 1, maxPage);
            RebuildPartGrid();
        }

        public void PreviousPage()
        {
            _currentPageIndex = Mathf.Max(0, _currentPageIndex - 1);
            RebuildPartGrid();
        }

        public void NextPart()
        {
            StepPart(1);
        }

        public void PreviousPart()
        {
            StepPart(-1);
        }

        public void RandomizeSelection()
        {
            RandomizeSelection(Environment.TickCount);
        }

        public void RandomizeSelection(int seed)
        {
            if (_catalog == null || _catalog.Parts == null)
                return;

            var rng = new System.Random(seed);
            string[] categories = _catalog.Parts
                .Where(part => part != null && !string.IsNullOrWhiteSpace(part.CategoryId))
                .Select(part => part.CategoryId)
                .Distinct()
                .OrderBy(categoryId => categoryId, StringComparer.Ordinal)
                .ToArray();

            for (int i = 0; i < categories.Length; i++)
            {
                List<SpumPartDefinition> parts = PartsForCategories(new[] { categories[i] });
                if (parts.Count == 0)
                    continue;

                SpumPartDefinition selected = parts[rng.Next(parts.Count)];
                _selectedPartIds[categories[i]] = selected.StableId;
            }

            RebuildPartGrid();
            RefreshSelectionSurfaces();
        }

        public CharacterAppearanceSnapshot CreateSnapshot()
        {
            var selections = new List<CharacterAppearancePartSelection>();
            foreach (KeyValuePair<string, string> selected in _selectedPartIds.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(selected.Key) && !string.IsNullOrWhiteSpace(selected.Value))
                {
                    selections.Add(new CharacterAppearancePartSelection(selected.Key, selected.Value));
                }
            }

            string catalogId = _catalog != null ? _catalog.Id : string.Empty;
            return new CharacterAppearanceSnapshot(1, "spum", "appearance.spum.generated", catalogId, selections.ToArray());
        }

        private void CreateHeader(Transform root)
        {
            Transform header = CreateContainer(root, "Header", new Vector2(1180f, 64f), new Vector2(0f, 360f)).transform;
            MakeText(header, "Title", L("spum.creator.title", "Character"), new Vector2(-430f, 0f), new Vector2(220f, 48f), 30, TextAnchor.MiddleLeft);

            GameObject back = CreateButton(header, "BackButton", L("spum.creator.back", "Back"), new Vector2(104f, 48f));
            back.GetComponent<Button>().onClick.AddListener(() => _cancel?.Invoke());
            ((RectTransform)back.transform).anchoredPosition = new Vector2(180f, 0f);

            GameObject random = CreateButton(header, "RandomButton", L("spum.creator.random", "Random"), new Vector2(120f, 48f));
            random.GetComponent<Button>().onClick.AddListener(RandomizeSelection);
            ((RectTransform)random.transform).anchoredPosition = new Vector2(316f, 0f);

            GameObject confirm = CreateButton(header, "ConfirmButton", L("spum.creator.confirm", "Confirm"), new Vector2(132f, 48f));
            confirm.GetComponent<Button>().onClick.AddListener(() => _confirm?.Invoke(CreateSnapshot()));
            ((RectTransform)confirm.transform).anchoredPosition = new Vector2(462f, 0f);
        }

        private void CreateCategoryTabs(Transform root)
        {
            Transform tabs = CreateContainer(root, "CategoryTabs", new Vector2(220f, 500f), new Vector2(-490f, 40f)).transform;
            for (int i = 0; i < CategoryGroups.Length; i++)
            {
                CategoryGroup group = CategoryGroups[i];
                if (!CatalogContainsAny(group.CategoryIds))
                    continue;

                string firstCategoryId = group.CategoryIds[0];
                GameObject tab = CreateButton(tabs, group.ObjectName, L(group.LabelKey, group.Label), new Vector2(204f, 56f));
                tab.GetComponent<Button>().onClick.AddListener(() => SelectCategory(firstCategoryId));
                var rect = (RectTransform)tab.transform;
                rect.anchoredPosition = new Vector2(0f, 204f - i * 76f);
            }
        }

        private void CreatePreview(Transform root)
        {
            GameObject preview = CreateContainer(root, "Preview", new Vector2(300f, 500f), new Vector2(430f, 40f));
            // Keep the preview's own labels readable; its renderer owns only its generated children.
            var background = preview.AddComponent<Image>();
            background.color = new Color(0.13f, 0.16f, 0.22f, 1f);
            background.raycastTarget = false;
            _preview = preview.AddComponent<SpumCharacterCreatorPreview>();
            _preview.Build(PartsForCategories(new[] { "weapon", "weapons", "weapon-preview" }));
        }

        private void CreatePageControls(Transform root)
        {
            Transform controls = CreateContainer(root, "PageControls", new Vector2(680f, 56f), new Vector2(-60f, -250f)).transform;
            _previousPageButton = CreateButton(controls, "PreviousPageButton", L("spum.creator.page.previous", "<"), new Vector2(72f, 48f));
            _previousPageButton.GetComponent<Button>().onClick.AddListener(PreviousPage);
            ((RectTransform)_previousPageButton.transform).anchoredPosition = new Vector2(-300f, 0f);

            _nextPageButton = CreateButton(controls, "NextPageButton", L("spum.creator.page.next", ">"), new Vector2(72f, 48f));
            _nextPageButton.GetComponent<Button>().onClick.AddListener(NextPage);
            ((RectTransform)_nextPageButton.transform).anchoredPosition = new Vector2(-216f, 0f);

            GameObject previousPart = CreateButton(controls, "PreviousPartButton", L("spum.creator.part.previous", "Prev"), new Vector2(96f, 48f));
            previousPart.GetComponent<Button>().onClick.AddListener(PreviousPart);
            ((RectTransform)previousPart.transform).anchoredPosition = new Vector2(-88f, 0f);

            GameObject nextPart = CreateButton(controls, "NextPartButton", L("spum.creator.part.next", "Next"), new Vector2(96f, 48f));
            nextPart.GetComponent<Button>().onClick.AddListener(NextPart);
            ((RectTransform)nextPart.transform).anchoredPosition = new Vector2(22f, 0f);

            GameObject cancel = CreateButton(controls, "CancelButton", L("spum.creator.cancel", "Cancel"), new Vector2(112f, 48f));
            cancel.GetComponent<Button>().onClick.AddListener(() => _cancel?.Invoke());
            ((RectTransform)cancel.transform).anchoredPosition = new Vector2(154f, 0f);
        }

        private void CreateFooter(Transform root)
        {
            Transform footer = CreateContainer(root, "Footer", new Vector2(1180f, 84f), new Vector2(0f, -342f)).transform;
            _selectedPartChips = CreateContainer(footer, "SelectedPartChips", new Vector2(1120f, 36f), new Vector2(0f, 20f)).transform;
            _statusLine = MakeText(footer, "StatusLine", string.Empty, new Vector2(0f, -24f), new Vector2(1120f, 28f), 18, TextAnchor.MiddleCenter);
        }

        private void RebuildPartGrid()
        {
            if (_partGrid == null)
                return;

            ClearGeneratedChildren(_partGrid);
            List<SpumPartDefinition> parts = CurrentSelectedParts();
            int start = _currentPageIndex * VisiblePartCellBudget;
            int count = Mathf.Min(VisiblePartCellBudget, Mathf.Max(0, parts.Count - start));
            for (int i = 0; i < count; i++)
            {
                SpumPartDefinition part = parts[start + i];
                string cellName = "PartCell_" + SanitizeName(part != null ? part.StableId : "empty");
                GameObject cell = CreateButton(_partGrid, cellName, ShortPartLabel(part), new Vector2(180f, 74f));
                SpumPartDefinition selectedPart = part;
                cell.GetComponent<Button>().onClick.AddListener(() => SelectPart(selectedPart));
                var rect = (RectTransform)cell.transform;
                int column = i % 3;
                int row = i / 3;
                rect.anchoredPosition = new Vector2(column * 196f - 196f, -row * 104f + 174f);
            }

            int maxPage = MaxPageIndex(parts.Count);
            if (_previousPageButton != null)
                _previousPageButton.SetActive(_currentPageIndex > 0);
            if (_nextPageButton != null)
                _nextPageButton.SetActive(_currentPageIndex < maxPage);
        }

        private void SelectPart(SpumPartDefinition part)
        {
            if (part == null || string.IsNullOrWhiteSpace(part.CategoryId))
                return;

            _selectedPartIds[part.CategoryId] = part.StableId;
            RefreshSelectionSurfaces();
        }

        private void StepPart(int offset)
        {
            List<SpumPartDefinition> parts = CurrentSelectedParts();
            if (parts.Count == 0)
                return;

            string categoryId = !string.IsNullOrWhiteSpace(parts[0].CategoryId) ? parts[0].CategoryId : string.Empty;
            string currentPartId = _selectedPartIds.TryGetValue(categoryId, out string selected) ? selected : string.Empty;
            int currentIndex = Mathf.Max(0, parts.FindIndex(part => part != null && part.StableId == currentPartId));
            int nextIndex = (currentIndex + offset + parts.Count) % parts.Count;
            SelectPart(parts[nextIndex]);
        }

        private void EnsureDefaultSelections()
        {
            if (_catalog == null || _catalog.Parts == null)
                return;

            for (int i = 0; i < _catalog.Parts.Length; i++)
            {
                SpumPartDefinition part = _catalog.Parts[i];
                if (part == null || string.IsNullOrWhiteSpace(part.CategoryId) || !part.IsDefault || _selectedPartIds.ContainsKey(part.CategoryId))
                    continue;

                _selectedPartIds.Add(part.CategoryId, part.StableId);
            }

            for (int i = 0; i < _catalog.Parts.Length; i++)
            {
                SpumPartDefinition part = _catalog.Parts[i];
                if (part == null || string.IsNullOrWhiteSpace(part.CategoryId) || _selectedPartIds.ContainsKey(part.CategoryId))
                    continue;

                _selectedPartIds.Add(part.CategoryId, part.StableId);
            }
        }

        private void RefreshSelectionSurfaces()
        {
            _preview?.ApplySelectedParts(_selectedPartIds);
            RebuildSelectedPartChips();
            if (_statusLine != null)
                _statusLine.text = _selectedPartIds.Count == 0
                    ? L("spum.creator.none-selected", "No parts selected")
                    : string.Format(System.Globalization.CultureInfo.CurrentCulture, L("spum.creator.selected-count", "{0} parts selected"), _selectedPartIds.Count);
        }

        private void RebuildSelectedPartChips()
        {
            if (_selectedPartChips == null)
                return;

            ClearGeneratedChildren(_selectedPartChips);
            int index = 0;
            foreach (KeyValuePair<string, string> selected in _selectedPartIds.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                GameObject chip = CreateButton(_selectedPartChips, "Chip_" + SanitizeName(selected.Key), ShortPartLabel(selected.Value), new Vector2(164f, 32f));
                chip.GetComponent<Button>().interactable = false;
                ((RectTransform)chip.transform).anchoredPosition = new Vector2(-440f + index * 176f, 0f);
                index++;
                if (index >= 6)
                    break;
            }
        }

        private CategoryGroup? FindFirstAvailableGroup()
        {
            for (int i = 0; i < CategoryGroups.Length; i++)
            {
                if (CatalogContainsAny(CategoryGroups[i].CategoryIds))
                    return CategoryGroups[i];
            }

            return null;
        }

        private string[] ResolveCategoryIds(string categoryId)
        {
            for (int i = 0; i < CategoryGroups.Length; i++)
            {
                CategoryGroup group = CategoryGroups[i];
                for (int j = 0; j < group.CategoryIds.Length; j++)
                {
                    if (group.CategoryIds[j] == categoryId)
                        return group.CategoryIds;
                }
            }

            return string.IsNullOrWhiteSpace(categoryId) ? Array.Empty<string>() : new[] { categoryId };
        }

        private bool CatalogContainsAny(IReadOnlyList<string> categoryIds)
        {
            return PartsForCategories(categoryIds).Count > 0;
        }

        private List<SpumPartDefinition> CurrentSelectedParts()
        {
            return PartsForCategories(_selectedCategoryIds);
        }

        private List<SpumPartDefinition> PartsForCategories(IReadOnlyList<string> categoryIds)
        {
            var parts = new List<SpumPartDefinition>();
            if (_catalog == null || _catalog.Parts == null || categoryIds == null)
                return parts;

            for (int i = 0; i < _catalog.Parts.Length; i++)
            {
                SpumPartDefinition part = _catalog.Parts[i];
                if (part == null)
                    continue;

                for (int j = 0; j < categoryIds.Count; j++)
                {
                    if (part.CategoryId == categoryIds[j])
                    {
                        parts.Add(part);
                        break;
                    }
                }
            }

            return parts;
        }

        private static int MaxPageIndex(int partCount)
        {
            return Mathf.Max(0, Mathf.CeilToInt(partCount / (float)VisiblePartCellBudget) - 1);
        }

        private void EnsureModalBackdrop()
        {
            var backdrop = GetComponent<Image>();
            if (backdrop == null) backdrop = gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.06f, 0.08f, 0.12f, 1f);
            backdrop.raycastTarget = true;
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            // Configure geometry before OnEnable builds the tiled chrome. One root surface, no per-cell tile grids.
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var backplate = go.GetComponent<Image>();
            backplate.color = new Color(0.90f, 0.89f, 0.94f, 1f);
            backplate.raycastTarget = true;
            var tile = go.AddComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.SetTileSize(new Vector2(48f, 48f));
            go.SetActive(true);
            return go;
        }

        private GameObject CreateContainer(Transform parent, string name, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return go;
        }

        private GameObject CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            GameObject button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(parent, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = button.GetComponent<Image>();
            image.color = new Color(0.19f, 0.23f, 0.31f, 1f);
            image.raycastTarget = true;
            button.GetComponent<Button>().targetGraphic = image;
            var text = MakeText(button.transform, "Label", label, Vector2.zero, new Vector2(size.x - 16f, size.y - 8f), 18, TextAnchor.MiddleCenter);
            text.color = Color.white;
            return button;
        }

        private static Text MakeText(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            GameObject textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(parent, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = position;
            textRect.sizeDelta = size;
            var label = textGo.GetComponent<Text>();
            label.text = text;
            label.alignment = alignment;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.color = new Color(0.10f, 0.13f, 0.20f, 1f);
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static void ClearGeneratedChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static string ShortPartLabel(SpumPartDefinition part)
        {
            return part == null ? string.Empty : ShortPartLabel(part.StableId);
        }

        private static string ShortPartLabel(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            int separator = id.LastIndexOf('.', id.Length - 1);
            return separator >= 0 && separator < id.Length - 1 ? id.Substring(separator + 1) : id;
        }

        private static string SanitizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "empty" : value.Replace('.', '_').Replace('/', '_').Replace('\\', '_');
        }

        private static string L(string key, string fallback)
        {
            string value = GameText.Get(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        private readonly struct CategoryGroup
        {
            public CategoryGroup(string labelKey, string label, string objectName, string[] categoryIds)
            {
                LabelKey = labelKey;
                Label = label;
                ObjectName = objectName;
                CategoryIds = categoryIds;
            }

            public string LabelKey { get; }
            public string Label { get; }
            public string ObjectName { get; }
            public string[] CategoryIds { get; }
        }
    }
}
