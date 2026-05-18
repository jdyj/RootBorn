using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
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
            new CategoryGroup("Body/Skin", "Tab_BodySkin", new[] { "body", "skin" }),
            new CategoryGroup("Eye", "Tab_Eye", new[] { "eye", "eyes" }),
            new CategoryGroup("Hair", "Tab_Hair", new[] { "hair" }),
            new CategoryGroup("Outfit/Cloth", "Tab_OutfitCloth", new[] { "outfit", "cloth" }),
            new CategoryGroup("Accessory", "Tab_AccessoryBackHelmet", new[] { "accessory", "back", "helmet" }),
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

            GameObject root = CreatePanel(transform, "SpumCharacterCreatorRoot", new Vector2(1280f, 760f));
            CreateHeader(root.transform);
            CreateCategoryTabs(root.transform);
            CreatePreview(root.transform);
            _partGrid = CreatePanel(root.transform, "PartGrid", new Vector2(840f, 420f)).transform;
            ((RectTransform)_partGrid).anchoredPosition = new Vector2(180f, -90f);
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
            Transform header = CreateContainer(root, "Header", new Vector2(1180f, 72f), new Vector2(0f, 320f)).transform;
            MakeText(header, "Title", "Character", new Vector2(-430f, 0f), new Vector2(220f, 48f), 30, TextAnchor.MiddleLeft);

            GameObject back = CreateButton(header, "BackButton", "Back", new Vector2(104f, 48f));
            back.GetComponent<Button>().onClick.AddListener(() => _cancel?.Invoke());
            ((RectTransform)back.transform).anchoredPosition = new Vector2(270f, 0f);

            GameObject random = CreateButton(header, "RandomButton", "Random", new Vector2(120f, 48f));
            random.GetComponent<Button>().onClick.AddListener(RandomizeSelection);
            ((RectTransform)random.transform).anchoredPosition = new Vector2(402f, 0f);

            GameObject confirm = CreateButton(header, "ConfirmButton", "Confirm", new Vector2(132f, 48f));
            confirm.GetComponent<Button>().onClick.AddListener(() => _confirm?.Invoke(CreateSnapshot()));
            ((RectTransform)confirm.transform).anchoredPosition = new Vector2(546f, 0f);
        }

        private void CreateCategoryTabs(Transform root)
        {
            Transform tabs = CreatePanel(root, "CategoryTabs", new Vector2(240f, 560f)).transform;
            ((RectTransform)tabs).anchoredPosition = new Vector2(-480f, -20f);
            for (int i = 0; i < CategoryGroups.Length; i++)
            {
                CategoryGroup group = CategoryGroups[i];
                if (!CatalogContainsAny(group.CategoryIds))
                    continue;

                string firstCategoryId = group.CategoryIds[0];
                GameObject tab = CreateButton(tabs, group.ObjectName, group.Label, new Vector2(220f, 56f));
                tab.GetComponent<Button>().onClick.AddListener(() => SelectCategory(firstCategoryId));
                var rect = (RectTransform)tab.transform;
                rect.anchoredPosition = new Vector2(0f, 210f - i * 66f);
            }
        }

        private void CreatePreview(Transform root)
        {
            GameObject preview = CreatePanel(root, "Preview", new Vector2(300f, 420f));
            var rect = (RectTransform)preview.transform;
            rect.anchoredPosition = new Vector2(450f, -80f);
            _preview = preview.AddComponent<SpumCharacterCreatorPreview>();
            _preview.Build(PartsForCategories(new[] { "weapon", "weapons", "weapon-preview" }));
        }

        private void CreatePageControls(Transform root)
        {
            Transform controls = CreateContainer(root, "PageControls", new Vector2(700f, 64f), new Vector2(180f, -338f)).transform;
            _previousPageButton = CreateButton(controls, "PreviousPageButton", "<", new Vector2(72f, 48f));
            _previousPageButton.GetComponent<Button>().onClick.AddListener(PreviousPage);
            ((RectTransform)_previousPageButton.transform).anchoredPosition = new Vector2(-300f, 0f);

            _nextPageButton = CreateButton(controls, "NextPageButton", ">", new Vector2(72f, 48f));
            _nextPageButton.GetComponent<Button>().onClick.AddListener(NextPage);
            ((RectTransform)_nextPageButton.transform).anchoredPosition = new Vector2(-216f, 0f);

            GameObject previousPart = CreateButton(controls, "PreviousPartButton", "Prev", new Vector2(96f, 48f));
            previousPart.GetComponent<Button>().onClick.AddListener(PreviousPart);
            ((RectTransform)previousPart.transform).anchoredPosition = new Vector2(-88f, 0f);

            GameObject nextPart = CreateButton(controls, "NextPartButton", "Next", new Vector2(96f, 48f));
            nextPart.GetComponent<Button>().onClick.AddListener(NextPart);
            ((RectTransform)nextPart.transform).anchoredPosition = new Vector2(22f, 0f);

            GameObject cancel = CreateButton(controls, "CancelButton", "Cancel", new Vector2(112f, 48f));
            cancel.GetComponent<Button>().onClick.AddListener(() => _cancel?.Invoke());
            ((RectTransform)cancel.transform).anchoredPosition = new Vector2(154f, 0f);
        }

        private void CreateFooter(Transform root)
        {
            Transform footer = CreateContainer(root, "Footer", new Vector2(1180f, 86f), new Vector2(0f, -360f)).transform;
            _selectedPartChips = CreateContainer(footer, "SelectedPartChips", new Vector2(900f, 44f), new Vector2(-100f, 12f)).transform;
            _statusLine = MakeText(footer, "StatusLine", "Select appearance parts", new Vector2(390f, -18f), new Vector2(360f, 32f), 16, TextAnchor.MiddleRight);
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
                int column = i % 4;
                int row = i / 4;
                rect.anchoredPosition = new Vector2(column * 196f - 294f, -row * 88f + 144f);
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
                _statusLine.text = _selectedPartIds.Count == 0 ? "No parts selected" : _selectedPartIds.Count + " parts selected";
        }

        private void RebuildSelectedPartChips()
        {
            if (_selectedPartChips == null)
                return;

            ClearGeneratedChildren(_selectedPartChips);
            int index = 0;
            foreach (KeyValuePair<string, string> selected in _selectedPartIds.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                GameObject chip = CreateButton(_selectedPartChips, "Chip_" + SanitizeName(selected.Key), ShortPartLabel(selected.Value), new Vector2(132f, 32f));
                chip.GetComponent<Button>().interactable = false;
                ((RectTransform)chip.transform).anchoredPosition = new Vector2(-380f + index * 144f, 0f);
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

        private GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var tile = go.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
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
            GameObject button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            button.transform.SetParent(parent, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = button.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.01f);
            var tile = button.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);

            MakeText(button.transform, "Label", label, Vector2.zero, new Vector2(size.x - 16f, size.y - 8f), 18, TextAnchor.MiddleCenter);
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
            label.color = Color.white;
            label.raycastTarget = false;
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

        private readonly struct CategoryGroup
        {
            public CategoryGroup(string label, string objectName, string[] categoryIds)
            {
                Label = label;
                ObjectName = objectName;
                CategoryIds = categoryIds;
            }

            public string Label { get; }
            public string ObjectName { get; }
            public string[] CategoryIds { get; }
        }
    }
}
