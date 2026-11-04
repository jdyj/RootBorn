using System;
using System.Collections.Generic;
using Rootborn.Game.Save;
using Rootborn.Game.Localization;
using Rootborn.UI.StudentLife;
using Rootborn.Game.VerticalSlice;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Objectives
{
    [DisallowMultipleComponent]
    public sealed class ObjectiveJournalPanel : MonoBehaviour
    {
        private static readonly Vector2 DefaultSize = new Vector2(1440f, 840f);
        private static readonly string[] DefaultCategoryIds = { "Goals", "Quests", "Campaign", "Hints" };
        private const float RowStep = 96f;
        private const float RowTop = 282f;

        private readonly Dictionary<string, ObjectiveJournalItem[]> _itemsByCategory = new Dictionary<string, ObjectiveJournalItem[]>(StringComparer.Ordinal);
        private readonly List<GameObject> _listRows = new List<GameObject>();
        private readonly List<GameObject> _rowPool = new List<GameObject>();
        private RectTransform _detailContent;
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
            RefreshRowColors();
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
            if (_root != null) return;
            var rect = transform as RectTransform;
            if (rect != null && (rect.sizeDelta.x < DefaultSize.x || rect.sizeDelta.y < DefaultSize.y)) rect.sizeDelta = DefaultSize;
            _root = MakeTilePanel(transform, "ObjectiveJournalRoot", Vector2.zero, DefaultSize);
            var shell = (RectTransform)_root.transform;
            MakeText(shell, "ObjectiveJournalTitle", Ui("ui.objective-journal.title", "목표 수첩", "Objective Journal"), new Vector2(48f,-38f), new Vector2(700f,48f), 32, TextAnchor.MiddleLeft);
            var close = ModernUiPanelBuilder.CreatePlainButton(shell, "ObjectiveCloseButton", new Vector2(568f,350f), new Vector2(184f,58f));
            MakeText((RectTransform)close.transform,"Label",Ui("ui.close", "닫기", "Close"),new Vector2(12f,-12f),new Vector2(160f,34f),22,TextAnchor.MiddleCenter);
            close.GetComponent<Button>().onClick.AddListener(Hide); SkinButton(close);
            _categoryRail=(RectTransform)MakePlainPanel(shell,"ObjectiveCategoryRail",new Vector2(-570f,-30f),new Vector2(200f,650f)).transform;
            _list=(RectTransform)MakePlainPanel(shell,"ObjectiveList",new Vector2(-230f,-30f),new Vector2(420f,650f)).transform;
            BuildScrollableList();
            _detail=(RectTransform)MakePlainPanel(shell,"ObjectiveDetail",new Vector2(350f,-30f),new Vector2(640f,650f)).transform;
            BuildCategoryRail(); BuildDetailTexts(); RefreshCategory(_selectedCategory);
        }

        private void BuildCategoryRail()
        {
            string[] iconKeys={"journal","plan","town_map","discovery"};
            string[] ko={"목표","심부름","이야기","발견"};
            for(int i=0;i<DefaultCategoryIds.Length;i++)
            {
                string category=DefaultCategoryIds[i];
                var button=ModernUiPanelBuilder.CreatePlainButton(_categoryRail,"ObjectiveCategory_"+category,new Vector2(0f,250f-i*130f),new Vector2(200f,108f));
                button.GetComponent<Button>().onClick.AddListener(()=>SelectCategory(category));
                MakeIcon(button.transform,"CategoryIcon",iconKeys[i],new Vector2(14f,-18f),new Vector2(72f,72f));
                MakeText((RectTransform)button.transform,"ObjectiveCategoryLabel_"+category,Ui("ui.objective.category."+category.ToLowerInvariant(),ko[i],category),new Vector2(94f,-36f),new Vector2(96f,36f),22,TextAnchor.MiddleLeft);
                SkinButton(button);
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
            for(int i=0;i<_rowPool.Count;i++)
            {
                var old=_rowPool[i]; old.SetActive(false); old.name="ObjectiveRowPool_"+i;
                old.GetComponent<Button>().onClick.RemoveAllListeners();
                var oldText=old.transform.Find("ObjectiveRowText").GetComponent<Text>();oldText.text=string.Empty;
            }
            _listRows.Clear(); var items=GetSelectedItems();
            if(items.Length==0)
            {
                _selectedItemIndex=0;_selectedStableKey=string.Empty;ResizeListContent(0);RefreshEmptyDetail(categoryId);return;
            }
            RestoreSelectionByStableKey(items);ResizeListContent(items.Length);
            var rowParent=_listContent!=null?_listContent:_list;
            for(int i=0;i<items.Length;i++)
            {
                GameObject row;
                if(i<_rowPool.Count) row=_rowPool[i];
                else
                {
                    row=ModernUiPanelBuilder.CreatePlainButton(rowParent,"ObjectiveRow_"+i,Vector2.zero,new Vector2(396f,88f));
                    var rowRect=(RectTransform)row.transform;rowRect.anchorMin=new Vector2(.5f,1f);rowRect.anchorMax=rowRect.anchorMin;rowRect.pivot=new Vector2(.5f,1f);
                    row.GetComponent<Image>().color=Color.white;
                    MakeIcon(row.transform,"RowIcon","journal",new Vector2(10f,-18f),new Vector2(56f,56f));
                    var text=MakeText(rowRect,"ObjectiveRowText",string.Empty,new Vector2(80f,-13f),new Vector2(302f,64f),18,TextAnchor.MiddleLeft);
                    text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
                    _rowPool.Add(row);
                }
                row.name="ObjectiveRow_"+i;row.SetActive(true);((RectTransform)row.transform).anchoredPosition=new Vector2(0f,-i*RowStep);
                row.transform.Find("RowIcon").GetComponent<UrbanUiIcon>().Configure(CategoryIconKey(categoryId));
                row.transform.Find("ObjectiveRowText").GetComponent<Text>().text=items[i].Title+"\n"+items[i].ProgressText;
                int rowIndex=i;row.GetComponent<Button>().onClick.AddListener(()=>SelectItem(rowIndex));_listRows.Add(row);
            }
            RefreshRowColors();RefreshDetail(items[_selectedItemIndex]);
        }

        private void BuildDetailTexts()
        {
            var scroll=_detail.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.inertia=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var viewportGo=new GameObject("ObjectiveDetailViewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D));viewportGo.transform.SetParent(_detail,false);
            var viewport=(RectTransform)viewportGo.transform;viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(8f,94f);viewport.offsetMax=new Vector2(-8f,-8f);
            viewportGo.GetComponent<Image>().color=Color.clear;
            var contentGo=new GameObject("ObjectiveDetailContent",typeof(RectTransform));contentGo.transform.SetParent(viewport,false);_detailContent=(RectTransform)contentGo.transform;
            _detailContent.anchorMin=new Vector2(0f,1f);_detailContent.anchorMax=Vector2.one;_detailContent.pivot=new Vector2(.5f,1f);_detailContent.sizeDelta=new Vector2(0f,540f);
            scroll.viewport=viewport;scroll.content=_detailContent;
            _detailTitle=MakeText(_detailContent,"ObjectiveDetailTitle",string.Empty,Vector2.zero,new Vector2(592f,42f),28,TextAnchor.UpperLeft);
            _detailBody=MakeText(_detailContent,"ObjectiveDetailBody",string.Empty,Vector2.zero,new Vector2(592f,120f),20,TextAnchor.UpperLeft);
            _detailProgress=MakeText(_detailContent,"ObjectiveDetailProgress",string.Empty,Vector2.zero,new Vector2(592f,36f),20,TextAnchor.UpperLeft);
            _detailReward=MakeText(_detailContent,"ObjectiveDetailReward",string.Empty,Vector2.zero,new Vector2(592f,36f),20,TextAnchor.UpperLeft);
            _detailAction=MakeText(_detailContent,"ObjectiveDetailAction",string.Empty,Vector2.zero,new Vector2(592f,54f),20,TextAnchor.UpperLeft);
            var track=ModernUiPanelBuilder.CreatePlainButton(_detail,"ObjectiveTrackButton",new Vector2(0f,-282f),new Vector2(300f,60f));
            track.GetComponent<Button>().onClick.AddListener(TrackSelectedItem);
            MakeText((RectTransform)track.transform,"ObjectiveTrackButtonLabel",Ui("ui.objective.track","목표 추적","Track"),new Vector2(14f,-13f),new Vector2(272f,34f),22,TextAnchor.MiddleCenter);SkinButton(track);
        }

        private void RefreshDetail(ObjectiveJournalItem item)
        {
            var detail = new ObjectiveJournalDetail(item.Title, item.Subtitle, item.ProgressText, item.State, item.NextHintText, "No objective selected");
            _detailTitle.text = detail.Title;
            _detailBody.text = detail.Body;
            _detailProgress.text = detail.ProgressText;
            _detailReward.text = detail.RewardText;
            _detailAction.text = detail.ActionText;
            LayoutDetail();
        }

        private void RefreshEmptyDetail(string categoryId)
        {
            _detailTitle.text = categoryId;
            _detailBody.text = Ui("ui.objective.empty", "이 분류에는 아직 기록이 없어요.", "No objectives in this category");
            _detailProgress.text = string.Empty;
            _detailReward.text = string.Empty;
            _detailAction.text = string.Empty;
            LayoutDetail();
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

            float height = Mathf.Max(_list.rect.height - 24f, itemCount <= 0 ? 0f : (itemCount - 1) * RowStep + 88f);
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
            text.font = LifeUiFont.Shared;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color32(48,71,83,255);
            text.raycastTarget = false;
            return text;
        }

        private void LayoutDetail()
        {
            if(_detailContent==null)return;
            float width=Mathf.Max(100f,((RectTransform)_detailContent.parent).rect.width-32f);float y=16f;
            var texts=new[]{_detailTitle,_detailBody,_detailProgress,_detailReward,_detailAction};
            for(int i=0;i<texts.Length;i++)
            {
                var text=texts[i];text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
                var rt=text.rectTransform;rt.anchoredPosition=new Vector2(16f,-y);rt.sizeDelta=new Vector2(width,Mathf.Max(text.fontSize+10f,text.preferredHeight+4f));
                y+=rt.sizeDelta.y+20f;
            }
            _detailContent.sizeDelta=new Vector2(0f,Mathf.Max(y,((RectTransform)_detailContent.parent).rect.height));
            _detailContent.anchoredPosition=Vector2.zero;_detail.GetComponent<ScrollRect>().verticalNormalizedPosition=1f;
        }

        private void RefreshRowColors()
        {
            for(int i=0;i<_listRows.Count;i++)
            {
                var button=_listRows[i].GetComponent<Button>();var colors=button.colors;
                colors.normalColor=i==_selectedItemIndex?new Color32(230,215,178,255):Color.clear;
                colors.highlightedColor=new Color32(236,225,196,255);colors.pressedColor=new Color32(218,199,154,255);colors.selectedColor=colors.normalColor;
                button.colors=colors;
            }
        }

        private static string CategoryIconKey(string category)
        {
            string[] keys={"journal","plan","town_map","discovery"};int index=Array.IndexOf(DefaultCategoryIds,category);return index>=0?keys[index]:"journal";
        }

        private static void SkinButton(GameObject go)
        {
            var skin=go.GetComponent<ModernUiTileImage>();if(skin==null)skin=go.AddComponent<ModernUiTileImage>();skin.SetRecipe(ModernUiRecipes.CommonPanel);skin.Rebuild();
        }

        private static UrbanUiIcon MakeIcon(Transform parent,string name,string key,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(UrbanUiIcon));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=new Vector2(0f,1f);rt.anchorMax=rt.anchorMin;rt.pivot=new Vector2(0f,1f);rt.anchoredPosition=position;rt.sizeDelta=size;
            go.GetComponent<Image>().raycastTarget=false;var icon=go.GetComponent<UrbanUiIcon>();icon.Configure(key);return icon;
        }

        private static string Ui(string key,string ko,string en)
        {
            var value=GameText.Get(key);return value==key?(GameText.Language=="ko"?ko:en):value;
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
