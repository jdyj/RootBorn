using System;
using System.Collections.Generic;
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

        private readonly Dictionary<string, ObjectiveJournalItem[]> _itemsByCategory = new Dictionary<string, ObjectiveJournalItem[]>(StringComparer.Ordinal);
        private readonly List<GameObject> _listRows = new List<GameObject>();
        private GameObject _root;
        private RectTransform _categoryRail;
        private RectTransform _list;
        private RectTransform _detail;
        private Text _detailTitle;
        private Text _detailBody;
        private Text _detailProgress;
        private Text _detailReward;
        private Text _detailAction;
        private TrackedObjectiveHud _trackedHud;
        private string _selectedCategory = "Goals";
        private int _selectedItemIndex;
        private bool _isVisible;

        public bool IsVisible => _isVisible;
        public string VisibleText => BuildVisibleText();

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

            _trackedHud.Show(items[_selectedItemIndex]);
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
                MakeText(_categoryRail, "ObjectiveCategory_" + category, category, new Vector2(18f, -24f - i * 48f), new Vector2(144f, 34f), 16, TextAnchor.MiddleLeft);
            }
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
                RefreshEmptyDetail(categoryId);
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                var row = MakePlainPanel(_list, "ObjectiveRow_" + i, new Vector2(0f, 282f - i * 58f), new Vector2(292f, 46f));
                _listRows.Add(row);
                MakeText((RectTransform)row.transform, "ObjectiveRowText", items[i].Title + "\n" + items[i].ProgressText, new Vector2(12f, -7f), new Vector2(268f, 34f), 13, TextAnchor.UpperLeft);
            }

            if (_selectedItemIndex < 0 || _selectedItemIndex >= items.Length)
            {
                _selectedItemIndex = 0;
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
            MakeText(_detail, "ObjectiveTrackButtonLabel", "Track", new Vector2(24f, -560f), new Vector2(120f, 32f), 16, TextAnchor.MiddleCenter);
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
