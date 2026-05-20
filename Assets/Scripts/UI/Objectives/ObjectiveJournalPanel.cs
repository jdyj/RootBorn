using System;
using System.Collections.Generic;
using Rootborn.Game.Save;
using Rootborn.Game.VerticalSlice;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Objectives
{
    [DisallowMultipleComponent]
    public sealed class ObjectiveJournalPanel : MonoBehaviour
    {
        private static readonly Vector2 DefaultSize = new Vector2(1180f, 720f);
        private static readonly string[] DefaultCategoryIds = { "Goals", "Quests", "Campaign", "Hints" };
        private const float RowStep = 58f;
        private const float RowTop = 282f;

        private readonly Dictionary<string, ObjectiveJournalItem[]> _itemsByCategory = new Dictionary<string, ObjectiveJournalItem[]>(StringComparer.Ordinal);
        private readonly List<GameObject> _listRows = new List<GameObject>();
        private GameObject _root;
        private RectTransform _categoryRail;
        private RectTransform _list;
        private RectTransform _listContent;
        private RectTransform _detail;
        private Text _detailTitle;
        private Text _detailBody;
        private Text _detailProgress;
        private Text _detailReward;
        private Text _detailAction;
        private TrackedObjectiveHud _trackedHud;
        private string _selectedCategory = "Goals";
        private string _selectedStableKey = string.Empty;
        private int _selectedItemIndex;
        private bool _isVisible;

        public bool IsVisible => _isVisible;
        public string VisibleText => BuildVisibleText();
        public string SelectedCategoryIdForTests => _selectedCategory;
        public string SelectedStableKeyForTests => _selectedStableKey;
        public int SelectedItemIndexForTests => _selectedItemIndex;
        public int VisibleRowCountForTests => _listRows.Count;

        private void Awake()
        {
            SeedDefaultItems();
        }

        public void BindTrackedHud(TrackedObjectiveHud trackedHud)
        {
            _trackedHud = trackedHud;
        }

        public void SetItems(string categoryId, ObjectiveJournalItem[] items)
        {
            SeedDefaultItems();
            if (string.IsNullOrEmpty(categoryId)) return;
            _itemsByCategory[categoryId] = items ?? Array.Empty<ObjectiveJournalItem>();
            if (_selectedCategory == categoryId && _root != null) RefreshCategory(categoryId);
        }

        public void HideTrackedObjective()
        {
            if (_trackedHud != null)
            {
                _trackedHud.Hide();
            }
        }

        public void SetVerticalSliceSummary(VerticalSliceSummary summary)
        {
            var item = new ObjectiveJournalItem(
                summary.StableKey,
                "Goals",
                string.IsNullOrEmpty(summary.NextObjectiveText) ? "Next objective" : summary.NextObjectiveText,
                summary.DayResultGuideText,
                summary.ChangedDomainIds.Length >= 2 ? "Linked" : "Started",
                summary.ChangedDomainIds.Length + " domains changed",
                string.IsNullOrEmpty(summary.NextActionText) ? summary.FollowUpMotivationText : summary.NextActionText,
                false,
                0);

            SetItems("Goals", new[] { item });
        }

        public void Show()
        {
            gameObject.SetActive(true);
            EnsureBuilt();
            if (_root != null)
            {
                _root.SetActive(true);
            }
            _isVisible = true;
            RefreshCategory(_selectedCategory);
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
            _isVisible = false;
            gameObject.SetActive(false);
        }

        public void SelectCategory(string categoryId)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(categoryId) || !_itemsByCategory.ContainsKey(categoryId))
            {
                return;
            }

            _selectedCategory = categoryId;
            _selectedItemIndex = 0;
            _selectedStableKey = string.Empty;
            RefreshCategory(_selectedCategory);
        }

        public void SelectItem(int index)
        {
            EnsureBuilt();
            var items = GetSelectedItems();
            if (index < 0 || index >= items.Length)
            {
                return;
            }

            _selectedItemIndex = index;
            _selectedStableKey = items[index].StableKey ?? string.Empty;
            RefreshDetail(items[_selectedItemIndex]);
        }

        public void TrackSelectedItem()
        {
            EnsureBuilt();
            var items = GetSelectedItems();
            if (_trackedHud == null || items.Length == 0 || _selectedItemIndex < 0 || _selectedItemIndex >= items.Length)
            {
                return;
            }

            var selected = items[_selectedItemIndex];
            _selectedStableKey = selected.StableKey ?? string.Empty;
            _trackedHud.Show(selected);
            ObjectiveJournalTrackingStore.Save(ActiveSaveContext.Metadata, selected.CategoryId, selected.StableKey);
        }

        private void SeedDefaultItems()
        {
            if (_itemsByCategory.Count > 0)
            {
                return;
            }

            _itemsByCategory["Goals"] = new[]
            {
                new ObjectiveJournalItem("goal.daily-growth", "Goals", "Goals", "Pick a growth route for today", "Active", "0 / 1 selected", "Choose a town activity", false, 0)
            };
            _itemsByCategory["Quests"] = new[]
            {
                new ObjectiveJournalItem("quest.active-summary", "Quests", "Quest Tracker", "Review active quest objectives", "Available", "Open quest details", "Talk to NPCs or inspect objective rows", false, 0)
            };
            _itemsByCategory["Campaign"] = new[]
            {
                new ObjectiveJournalItem("campaign.first-days", "Campaign", "Campaign Route", "First-days town route", "Active", "Day route in progress", "Visit locations and complete daily choices", false, 0)
            };
            _itemsByCategory["Hints"] = new[]
            {
                new ObjectiveJournalItem("hint.career", "Hints", "Career Hints", "Discovered growth hints", "Unlocked", "Hints available", "Use activities outside one fixed school path", false, 0)
            };
        }

        private void EnsureBuilt()
        {
            SeedDefaultItems();
            if (_root != null)
            {
                return;
            }

            var rect = transform as RectTransform;
            if (rect != null && rect.sizeDelta == Vector2.zero)
            {
                rect.sizeDelta = DefaultSize;
            }

            _root = MakeTilePanel(transform, "ObjectiveJournalRoot", Vector2.zero, rect != null && rect.sizeDelta != Vector2.zero ? rect.sizeDelta : DefaultSize);
            _categoryRail = (RectTransform)MakePlainPanel(_root.transform, "ObjectiveCategoryRail", new Vector2(-500f, 0f), new Vector2(180f, 640f)).transform;
            _list = (RectTransform)MakePlainPanel(_root.transform, "ObjectiveList", new Vector2(-220f, 0f), new Vector2(340f, 640f)).transform;
            BuildScrollableList();
            _detail = (RectTransform)MakePlainPanel(_root.transform, "ObjectiveDetail", new Vector2(230f, 0f), new Vector2(520f, 640f)).transform;

            BuildCategoryRail();
            BuildDetailTexts();
            RefreshCategory(_selectedCategory);
        }

        private void BuildCategoryRail()
        {
            for (int i = 0; i < DefaultCategoryIds.Length; i++)
            {
                string category = DefaultCategoryIds[i];
                var categoryButton = ModernUiPanelBuilder.CreatePlainButton(_categoryRail, "ObjectiveCategory_" + category, new Vector2(90f, 292f - i * 48f), new Vector2(144f, 34f));
                categoryButton.GetComponent<Button>().onClick.AddListener(() => SelectCategory(category));
                MakeText((RectTransform)categoryButton.transform, "ObjectiveCategoryLabel_" + category, category, new Vector2(8f, -5f), new Vector2(128f, 24f), 16, TextAnchor.MiddleLeft);
            }
        }

        private void BuildScrollableList()
        {
            var scrollRect = _list.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = false;

            var viewportGo = new GameObject("ObjectiveListViewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(_list, false);
            var viewport = (RectTransform)viewportGo.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = new Vector2(12f, 12f);
            viewport.offsetMax = new Vector2(-12f, -12f);

            var contentGo = new GameObject("ObjectiveListContent", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            _listContent = (RectTransform)contentGo.transform;
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.offsetMin = new Vector2(0f, _listContent.offsetMin.y);
            _listContent.offsetMax = new Vector2(0f, _listContent.offsetMax.y);

            scrollRect.viewport = viewport;
            scrollRect.content = _listContent;
        }

        private void RefreshCategory(string categoryId)
        {
            for (int i = 0; i < _listRows.Count; i++)
            {
                DestroyObject(_listRows[i]);
            }
            _listRows.Clear();

            var items = GetSelectedItems();
            if (items.Length == 0)
            {
                _selectedItemIndex = 0;
                _selectedStableKey = string.Empty;
                ResizeListContent(0);
                RefreshEmptyDetail(categoryId);
                return;
            }

            RestoreSelectionByStableKey(items);
            ResizeListContent(items.Length);

            var rowParent = _listContent != null ? _listContent : _list;
            for (int i = 0; i < items.Length; i++)
            {
                int rowIndex = i;
                var row = ModernUiPanelBuilder.CreatePlainButton(rowParent, "ObjectiveRow_" + i, new Vector2(0f, -i * RowStep), new Vector2(292f, 46f));
                var rowRect = (RectTransform)row.transform;
                rowRect.anchorMin = new Vector2(0.5f, 1f);
                rowRect.anchorMax = new Vector2(0.5f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                row.GetComponent<Button>().onClick.AddListener(() => SelectItem(rowIndex));
                _listRows.Add(row);
                var rowText = MakeText((RectTransform)row.transform, "ObjectiveRowText", items[i].Title + "\n" + items[i].ProgressText, new Vector2(12f, -7f), new Vector2(268f, 34f), 13, TextAnchor.UpperLeft);
                rowText.horizontalOverflow = HorizontalWrapMode.Wrap;
                rowText.verticalOverflow = VerticalWrapMode.Truncate;
            }

            RefreshDetail(items[_selectedItemIndex]);
        }

        private void BuildDetailTexts()
        {
            _detailTitle = MakeText(_detail, "ObjectiveDetailTitle", string.Empty, new Vector2(24f, -24f), new Vector2(472f, 34f), 20, TextAnchor.UpperLeft);
            _detailBody = MakeText(_detail, "ObjectiveDetailBody", string.Empty, new Vector2(24f, -72f), new Vector2(472f, 120f), 15, TextAnchor.UpperLeft);
            _detailProgress = MakeText(_detail, "ObjectiveDetailProgress", string.Empty, new Vector2(24f, -214f), new Vector2(472f, 36f), 15, TextAnchor.UpperLeft);
            _detailReward = MakeText(_detail, "ObjectiveDetailReward", string.Empty, new Vector2(24f, -266f), new Vector2(472f, 36f), 15, TextAnchor.UpperLeft);
            _detailAction = MakeText(_detail, "ObjectiveDetailAction", string.Empty, new Vector2(24f, -318f), new Vector2(472f, 54f), 15, TextAnchor.UpperLeft);
            var trackButton = ModernUiPanelBuilder.CreatePlainButton(_detail, "ObjectiveTrackButton", new Vector2(-176f, -258f), new Vector2(120f, 32f));
            trackButton.GetComponent<Button>().onClick.AddListener(TrackSelectedItem);
            MakeText((RectTransform)trackButton.transform, "ObjectiveTrackButtonLabel", "Track", new Vector2(8f, -4f), new Vector2(104f, 24f), 16, TextAnchor.MiddleCenter);
        }

        private void RefreshDetail(ObjectiveJournalItem item)
        {
            var detail = new ObjectiveJournalDetail(item.Title, item.Subtitle, item.ProgressText, item.State, item.NextHintText, "No objective selected");
            _detailTitle.text = detail.Title;
            _detailBody.text = detail.Body;
            _detailProgress.text = detail.ProgressText;
            _detailReward.text = detail.RewardText;
            _detailAction.text = detail.ActionText;
        }

        private void RefreshEmptyDetail(string categoryId)
        {
            _detailTitle.text = categoryId;
            _detailBody.text = "No objectives in this category";
            _detailProgress.text = string.Empty;
            _detailReward.text = string.Empty;
            _detailAction.text = string.Empty;
        }

        private void RestoreSelectionByStableKey(ObjectiveJournalItem[] items)
        {
            if (!string.IsNullOrEmpty(_selectedStableKey))
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (string.Equals(items[i].StableKey, _selectedStableKey, StringComparison.Ordinal))
                    {
                        _selectedItemIndex = i;
                        return;
                    }
                }

                _selectedItemIndex = 0;
            }
            else if (_selectedItemIndex < 0 || _selectedItemIndex >= items.Length)
            {
                _selectedItemIndex = 0;
            }

            _selectedStableKey = items[_selectedItemIndex].StableKey ?? string.Empty;
        }

        private void ResizeListContent(int itemCount)
        {
            if (_listContent == null)
            {
                return;
            }

            float height = Mathf.Max(616f, itemCount <= 0 ? 0f : (itemCount - 1) * RowStep + 46f);
            _listContent.sizeDelta = new Vector2(0f, height);
            _listContent.anchoredPosition = Vector2.zero;
        }

        private ObjectiveJournalItem[] GetSelectedItems()
        {
            return _itemsByCategory.TryGetValue(_selectedCategory, out var items) && items != null ? items : Array.Empty<ObjectiveJournalItem>();
        }

        private string BuildVisibleText()
        {
            string text = string.Empty;
            for (int i = 0; i < DefaultCategoryIds.Length; i++)
            {
                text += (text.Length == 0 ? string.Empty : "\n") + DefaultCategoryIds[i];
            }

            AppendVisibleText(ref text, _detailTitle);
            AppendVisibleText(ref text, _detailBody);
            AppendVisibleText(ref text, _detailProgress);
            AppendVisibleText(ref text, _detailReward);
            AppendVisibleText(ref text, _detailAction);

            return text;
        }

        private static GameObject MakeTilePanel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var tile = go.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel48);
            tile.Rebuild();
            return go;
        }

        private static GameObject MakePlainPanel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            return ModernUiPanelBuilder.CreatePlainContainer(parent, name, position, size);
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
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
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static void AppendVisibleText(ref string text, Text value)
        {
            if (value == null || string.IsNullOrEmpty(value.text))
            {
                return;
            }

            text += "\n" + value.text;
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
