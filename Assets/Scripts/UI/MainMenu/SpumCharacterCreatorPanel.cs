using System;
using System.Collections.Generic;
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

        private SpumPartCatalogDefinition _catalog;
        private Transform _partGrid;
        private GameObject _nextPageButton;
        private GameObject _previousPageButton;
        private string[] _selectedCategoryIds = Array.Empty<string>();
        private int _currentPageIndex;

        public ModernUiTileRecipe RootPanelRecipe => ModernUiRecipes.CommonPanel;
        public int CurrentPageIndex => _currentPageIndex;

        public void Build(SpumPartCatalogDefinition catalog)
        {
            _catalog = catalog;
            _currentPageIndex = 0;
            ClearGeneratedChildren(transform);

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

        private void CreateCategoryTabs(Transform root)
        {
            Transform tabs = CreateContainer(root, "CategoryTabs", new Vector2(240f, 560f), new Vector2(-480f, -20f)).transform;
            for (int i = 0; i < CategoryGroups.Length; i++)
            {
                CategoryGroup group = CategoryGroups[i];
                if (!CatalogContainsAny(group.CategoryIds))
                    continue;

                GameObject tab = CreateButton(tabs, group.ObjectName, group.Label, new Vector2(220f, 56f));
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
            Transform controls = CreateContainer(root, "PageControls", new Vector2(360f, 64f), new Vector2(180f, -338f)).transform;
            _previousPageButton = CreateButton(controls, "PreviousPageButton", "<", new Vector2(96f, 48f));
            ((RectTransform)_previousPageButton.transform).anchoredPosition = new Vector2(-70f, 0f);
            _nextPageButton = CreateButton(controls, "NextPageButton", ">", new Vector2(96f, 48f));
            ((RectTransform)_nextPageButton.transform).anchoredPosition = new Vector2(70f, 0f);
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
