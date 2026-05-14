using System;
using System.Collections.Generic;
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
        private GameObject _nextPageButton;
        private GameObject _previousPageButton;
        private string[] _selectedCategoryIds = Array.Empty<string>();
        private int _currentPageIndex;
        private Action<CharacterAppearanceSnapshot> _confirm;
        private Action _cancel;

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
            CreateCategoryTabs(root.transform);
            CreatePreview(root.transform);
            _partGrid = CreateContainer(root.transform, "PartGrid", new Vector2(840f, 420f), new Vector2(180f, -90f)).transform;
            CreatePageControls(root.transform);

            CategoryGroup? firstGroup = FindFirstAvailableGroup();
            if (firstGroup.HasValue)
            {
                SelectCategory(firstGroup.Value.CategoryIds[0]);
            }
        }

        public void SelectCategory(string categoryId)
        {
            _selectedCategoryIds = ResolveCategoryIds(categoryId);
            _currentPageIndex = 0;
            RebuildPartGrid();
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

        public void RandomizeSelection()
        {
            if (_catalog == null || _catalog.Parts == null)
                return;

            for (int i = 0; i < _catalog.Parts.Length; i++)
            {
                SpumPartDefinition part = _catalog.Parts[i];
                if (part != null && !string.IsNullOrWhiteSpace(part.CategoryId))
                {
                    _selectedPartIds[part.CategoryId] = part.StableId;
                }
            }

            RebuildPartGrid();
        }

        public CharacterAppearanceSnapshot CreateSnapshot()
        {
            var selections = new List<CharacterAppearancePartSelection>();
            foreach (KeyValuePair<string, string> selected in _selectedPartIds)
            {
                if (!string.IsNullOrWhiteSpace(selected.Key) && !string.IsNullOrWhiteSpace(selected.Value))
                {
                    selections.Add(new CharacterAppearancePartSelection(selected.Key, selected.Value));
                }
            }

            string catalogId = _catalog != null ? _catalog.Id : string.Empty;
            return new CharacterAppearanceSnapshot(1, "spum", "appearance.spum.generated", catalogId, selections.ToArray());
        }

        private void CreateCategoryTabs(Transform root)
        {
            Transform tabs = CreateContainer(root, "CategoryTabs", new Vector2(240f, 560f), new Vector2(-480f, -20f)).transform;
            for (int i = 0; i < CategoryGroups.Length; i++)
            {
                CategoryGroup group = CategoryGroups[i];
                if (!CatalogContainsAny(group.CategoryIds))
                    continue;

                string firstCategoryId = group.CategoryIds[0];
                GameObject tab = CreateButton(tabs, group.ObjectName, group.Label, new Vector2(220f, 56f));
                tab.GetComponent<Button>().onClick.AddListener(() => SelectCategory(firstCategoryId));
                var rect = (RectTransform)tab.transform;
                rect.anchoredPosition = new Vector2(0f, -i * 66f);
            }
        }

        private void CreatePreview(Transform root)
        {
            GameObject preview = CreatePanel(root, "Preview", new Vector2(300f, 420f));
            var rect = (RectTransform)preview.transform;
            rect.anchoredPosition = new Vector2(450f, -80f);
            var previewComponent = preview.AddComponent<SpumCharacterCreatorPreview>();
            previewComponent.Build(PartsForCategories(new[] { "weapon", "weapons" }));
        }

        private void CreatePageControls(Transform root)
        {
            Transform controls = CreateContainer(root, "PageControls", new Vector2(520f, 64f), new Vector2(180f, -338f)).transform;
            _previousPageButton = CreateButton(controls, "PreviousPageButton", "<", new Vector2(72f, 48f));
            _previousPageButton.GetComponent<Button>().onClick.AddListener(PreviousPage);
            ((RectTransform)_previousPageButton.transform).anchoredPosition = new Vector2(-210f, 0f);

            _nextPageButton = CreateButton(controls, "NextPageButton", ">", new Vector2(72f, 48f));
            _nextPageButton.GetComponent<Button>().onClick.AddListener(NextPage);
            ((RectTransform)_nextPageButton.transform).anchoredPosition = new Vector2(-126f, 0f);

            GameObject random = CreateButton(controls, "RandomButton", "Random", new Vector2(120f, 48f));
            random.GetComponent<Button>().onClick.AddListener(RandomizeSelection);
            ((RectTransform)random.transform).anchoredPosition = new Vector2(0f, 0f);

            GameObject confirm = CreateButton(controls, "ConfirmButton", "Confirm", new Vector2(132f, 48f));
            confirm.GetComponent<Button>().onClick.AddListener(() => _confirm?.Invoke(CreateSnapshot()));
            ((RectTransform)confirm.transform).anchoredPosition = new Vector2(142f, 0f);

            GameObject cancel = CreateButton(controls, "CancelButton", "Cancel", new Vector2(112f, 48f));
            cancel.GetComponent<Button>().onClick.AddListener(() => _cancel?.Invoke());
            ((RectTransform)cancel.transform).anchoredPosition = new Vector2(274f, 0f);
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
        }

        private void EnsureDefaultSelections()
        {
            if (_catalog == null || _catalog.Parts == null)
                return;

            for (int i = 0; i < _catalog.Parts.Length; i++)
            {
                SpumPartDefinition part = _catalog.Parts[i];
                if (part == null || string.IsNullOrWhiteSpace(part.CategoryId) || _selectedPartIds.ContainsKey(part.CategoryId))
                    continue;

                _selectedPartIds.Add(part.CategoryId, part.StableId);
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

            GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(button.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);
            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            return button;
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
            if (part == null)
                return string.Empty;

            string id = part.StableId;
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
