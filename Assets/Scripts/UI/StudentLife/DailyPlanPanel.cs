using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Localization;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class DailyPlanPanel : MonoBehaviour
    {
        private readonly Dictionary<Button, Vector2> _chromeSizes = new Dictionary<Button, Vector2>();
        private readonly List<Button> _routePool = new List<Button>();
        private readonly List<Button> _schedulePool = new List<Button>();
        private int _routeReuseIndex;
        private bool _scheduleMode;
        private DailyPlanRouteDefinition[] _routes = Array.Empty<DailyPlanRouteDefinition>();
        private DailyPlanProgressComponent _daily;
        private StudentLifeProgressComponent _student;
        private RectTransform _scheduleRoot;
        private RectTransform _navigation;
        private Button _previousDay;
        private Button _nextDay;
        private Button[] _slotButtons = Array.Empty<Button>();
        private int _selectedSlot;
        public int ScheduleSlotCountForTests { get; private set; }
        public int DisplayedDayForTests { get; private set; }
        public Button GetScheduleSlotButtonForTests(int index) => index >= 0 && index < _slotButtons.Length ? _slotButtons[index] : null;
        public Button GetPreviousDayButtonForTests() => _previousDay;
        private RectTransform _rowRoot;
        private Text _emptyText;
        private Text _timeBudget;
        private Button[] _routeButtons = System.Array.Empty<Button>();

        public string EmptyStateTextForTests { get; private set; } = string.Empty;
        public int RouteRowCountForTests { get; private set; }
        public bool UsesBoundedRouteListForTests { get; private set; }

        public void Bind(
            DailyPlanRouteDefinition[] routes,
            DailyPlanProgressComponent progressComponent,
            StudentLifeProgressComponent studentProgressComponent)
        {
            _scheduleMode = false;
            BindRoutes(routes, progressComponent, studentProgressComponent);
            SetScheduleVisibility();
            RefreshButtonChrome();
        }

        public void BindSchedule(DailyPlanRouteDefinition[] routes, DailyPlanProgressComponent progressComponent, StudentLifeProgressComponent studentProgressComponent)
        {
            _scheduleMode = true; _routes = routes ?? Array.Empty<DailyPlanRouteDefinition>();
            _daily = progressComponent; _student = studentProgressComponent;
            var student = _student != null ? _student.EnsureProgress() : null;
            var daily = _daily != null ? _daily.EnsureProgress(student) : student?.DailyPlan;
            DisplayedDayForTests = daily?.CurrentDay ?? 1; _selectedSlot = 0;
            if (daily != null) for (int i = 0; i < daily.Slots.Count; i++) if (!daily.Slots[i].Completed) { _selectedSlot = i; break; }
            BindRoutes(_routes, progressComponent, studentProgressComponent);
            EnsureScheduleLayout(); SetScheduleVisibility(); RenderSchedule();
        }

        private void BindRoutes(DailyPlanRouteDefinition[] routes, DailyPlanProgressComponent progressComponent, StudentLifeProgressComponent studentProgressComponent)
        {
            EnsureLayout();
            ClearRows();
            var student = studentProgressComponent != null ? studentProgressComponent.EnsureProgress() : null;
            _timeBudget.text = TimeBudgetText.Format(student);

            RouteRowCountForTests = routes == null ? 0 : routes.Length;
            EmptyStateTextForTests = RouteRowCountForTests == 0 ? "objective.daily.choose.empty" : string.Empty;
            UsesBoundedRouteListForTests = true;
            _emptyText.text = GameText.Get(EmptyStateTextForTests);
            _emptyText.gameObject.SetActive(RouteRowCountForTests == 0);

            if (routes == null || routes.Length == 0)
            {
                _routeButtons = System.Array.Empty<Button>();
                return;
            }

            _routeButtons = new Button[routes.Length];
            for (int i = 0; i < routes.Length; i++)
            {
                var route = routes[i];
                if (route == null)
                {
                    continue;
                }

                var button = CreateRouteButton(GameText.Get(route.DisplayNameKey), route);
                _routeButtons[i] = button;
                var selectedRoute = route;
                button.onClick.AddListener(() =>
                {
                    if (_scheduleMode)
                    {
                        if (_daily != null && _student != null && DisplayedDayForTests == _student.Progress.CurrentDay
                            && _daily.TryAssignScheduleSlot(_selectedSlot, selectedRoute, _student.Progress)) RenderSchedule();
                    }
                    else if (progressComponent != null && studentProgressComponent != null && progressComponent.TrySelect(selectedRoute, studentProgressComponent.Progress))
                    {
                        gameObject.SetActive(false);
                    }
                });
            }
        }

        private void EnsureScheduleLayout()
        {
            if (_scheduleRoot != null) return;
            var slots = new GameObject("ScheduleSlots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            slots.transform.SetParent(transform, false); _scheduleRoot = slots.GetComponent<RectTransform>();
            _scheduleRoot.anchorMin = new Vector2(0, 1); _scheduleRoot.anchorMax = Vector2.one;
            _scheduleRoot.pivot = new Vector2(0.5f, 1); _scheduleRoot.offsetMin = new Vector2(40, -316); _scheduleRoot.offsetMax = new Vector2(-40, -184);
            var layout = slots.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 8; layout.childControlWidth = true;
            layout.childForceExpandWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = true;
            var nav = new GameObject("DiaryNavigation", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            nav.transform.SetParent(transform, false); _navigation = nav.GetComponent<RectTransform>();
            _navigation.anchorMin = Vector2.zero; _navigation.anchorMax = new Vector2(1, 0);
            _navigation.pivot = new Vector2(0.5f, 0); _navigation.offsetMin = new Vector2(40, 36); _navigation.offsetMax = new Vector2(-40, 90);
            var navLayout = nav.GetComponent<HorizontalLayoutGroup>(); navLayout.spacing = 8;
            navLayout.childControlWidth = true; navLayout.childForceExpandWidth = true; navLayout.childControlHeight = true; navLayout.childForceExpandHeight = true;
            _previousDay = DiaryButton(_navigation, "PreviousDay", GameText.Get("daily-plan.previous")); _previousDay.onClick.AddListener(() => MoveDiary(-1));
            _nextDay = DiaryButton(_navigation, "NextDiaryDay", GameText.Get("daily-plan.next")); _nextDay.onClick.AddListener(() => MoveDiary(1));
            var close = DiaryButton(_navigation, "CloseDiary", GameText.Get("daily-plan.close")); close.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void SetScheduleVisibility()
        {
            if (_scheduleRoot != null) _scheduleRoot.gameObject.SetActive(_scheduleMode);
            if (_navigation != null) _navigation.gameObject.SetActive(_scheduleMode);
            if (_rowRoot != null && _rowRoot.parent is RectTransform viewport)
            { viewport.offsetMin = new Vector2(40, _scheduleMode ? 98 : 40); viewport.offsetMax = new Vector2(-40, _scheduleMode ? -332 : -184); }
        }

        private DailyPlanProgress CurrentPlan()
        {
            var student = _student != null ? _student.Progress : null;
            return _daily != null ? _daily.EnsureProgress(student) : student?.DailyPlan;
        }
        private void MoveDiary(int delta)
        {
            var daily = CurrentPlan(); if (daily == null) return;
            int index = daily.Diary.Count;
            for (int i = 0; i < daily.Diary.Count; i++) if (daily.Diary[i].DayNumber == DisplayedDayForTests) { index = i; break; }
            index = Mathf.Clamp(index + delta, 0, daily.Diary.Count);
            DisplayedDayForTests = index == daily.Diary.Count ? daily.CurrentDay : daily.Diary[index].DayNumber;
            RenderSchedule();
        }
        private void RenderSchedule()
        {
            var daily = CurrentPlan(); if (daily == null || _scheduleRoot == null) return;
            bool current = DisplayedDayForTests == daily.CurrentDay;
            IReadOnlyList<DailyPlanSlotRecord> slots = daily.Slots;
            int diaryIndex = daily.Diary.Count;
            if (!current)
                for (int i = 0; i < daily.Diary.Count; i++) if (daily.Diary[i].DayNumber == DisplayedDayForTests) { slots = daily.Diary[i].Slots; diaryIndex = i; break; }
            for(int i=0;i<_schedulePool.Count;i++){_schedulePool[i].gameObject.SetActive(false);_schedulePool[i].onClick.RemoveAllListeners();}
            _slotButtons = new Button[slots.Count]; ScheduleSlotCountForTests = slots.Count;
            _selectedSlot = Mathf.Clamp(_selectedSlot, 0, Mathf.Max(0, slots.Count - 1));
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i]; string routeName = GameText.Get("daily-plan.empty-slot");
                if (!string.IsNullOrEmpty(slot.RouteId))
                {
                    routeName = slot.RouteId;
                    foreach (var route in _routes) if (route != null && route.Id == slot.RouteId) { routeName = GameText.Get(route.DisplayNameKey); break; }
                }
                string status = slot.Completed ? GameText.Get("daily-plan.completed") : GameText.Get("daily-plan.planned");
                string selected = current && i == _selectedSlot ? "> " : string.Empty;
                string slotLabel=selected+GameText.Get(slot.PeriodKey)+"\n"+routeName+"\n"+status;
                if(i>=_schedulePool.Count)_schedulePool.Add(DiaryButton(_scheduleRoot,"ScheduleSlot"+i,slotLabel));
                _slotButtons[i]=_schedulePool[i];_slotButtons[i].gameObject.SetActive(true);_slotButtons[i].GetComponentInChildren<Text>(true).text=slotLabel;
                _slotButtons[i].interactable = current && !slot.Completed && _student != null && _student.Progress.DayState == StudentDayState.InProgress;
                int index = i; _slotButtons[i].onClick.AddListener(() => { _selectedSlot = index; RenderSchedule(); });
            }
            bool canAssign = current && slots.Count > 0 && !slots[_selectedSlot].Completed && _student != null && _student.Progress.DayState == StudentDayState.InProgress;
            foreach (var button in _routeButtons) if (button != null) button.interactable = canAssign;
            _previousDay.interactable = diaryIndex > 0;
            _nextDay.interactable = !current;
            _timeBudget.text = current ? TimeBudgetText.Format(_student != null ? _student.Progress : null)
                : string.Format(GameText.Get("daily-plan.diary-day"), DisplayedDayForTests);
            RefreshButtonChrome();
        }
        private void RefreshButtonChrome()
        {
            // Layout groups determine button sizes after construction. Rebuild only changed
            // chrome during user-driven binding/selection, never in an Update scan.
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
            if(_rowRoot!=null)LayoutRebuilder.ForceRebuildLayoutImmediate(_rowRoot);
            if(_scheduleRoot!=null)LayoutRebuilder.ForceRebuildLayoutImmediate(_scheduleRoot);
            if(_navigation!=null)LayoutRebuilder.ForceRebuildLayoutImmediate(_navigation);
            foreach(var button in GetComponentsInChildren<Button>())
            {
                var chrome=button.GetComponent<ModernUiTileImage>();if(chrome==null)continue;
                var size=((RectTransform)button.transform).rect.size;
                if(_chromeSizes.TryGetValue(button,out var previous)&&previous==size)continue;
                button.GetComponent<Image>().color=Color.clear;
                chrome.Rebuild();_chromeSizes[button]=size;
            }
        }
        private static Button DiaryButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage), typeof(LayoutElement));
            go.transform.SetParent(parent, false); go.GetComponent<ModernUiTileImage>().SetRecipe(ModernUiRecipes.CommonPanel);
            go.GetComponent<LayoutElement>().flexibleWidth = 1;
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text)); textGo.transform.SetParent(go.transform, false);
            var rect = textGo.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8, 3); rect.offsetMax = new Vector2(-8, -3);
            var text = textGo.GetComponent<Text>(); text.font = LifeUiFont.Shared; text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter; text.color = new Color32(48,71,83,255); text.raycastTarget = false;
            text.text = label; text.resizeTextForBestFit = false;
            return go.GetComponent<Button>();
        }

        private static string Ui(string key,string ko,string en)
        {
            var value=GameText.Get(key);return value==key?(GameText.Language=="ko"?ko:en):value;
        }

        public Button GetRouteButtonForTests(int index)
        {
            return index >= 0 && index < _routeButtons.Length ? _routeButtons[index] : null;
        }

        private void EnsureLayout()
        {
            var root = GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            root.sizeDelta = new Vector2(1120f, 800f);

            var chrome = GetComponent<ModernUiTileImage>();
            if (chrome == null) chrome = gameObject.AddComponent<ModernUiTileImage>();
            chrome.SetRecipe(ModernUiRecipes.CommonPanel);

            if (_rowRoot != null && _emptyText != null)
            {
                return;
            }

            var titleGo=new GameObject("PlanTitle",typeof(RectTransform),typeof(Text));titleGo.transform.SetParent(transform,false);
            var titleRect=(RectTransform)titleGo.transform;titleRect.anchorMin=new Vector2(0f,1f);titleRect.anchorMax=titleRect.anchorMin;titleRect.pivot=titleRect.anchorMin;titleRect.anchoredPosition=new Vector2(48f,-40f);titleRect.sizeDelta=new Vector2(800f,44f);
            var titleText=titleGo.GetComponent<Text>();titleText.font=LifeUiFont.Shared;titleText.fontSize=30;titleText.color=new Color32(48,71,83,255);titleText.raycastTarget=false;titleText.text=Ui("life.daily-plan.title","오늘의 계획","Plan Your Day");
            var iconGo=new GameObject("PlanIcon",typeof(RectTransform),typeof(Image),typeof(UrbanUiIcon));iconGo.transform.SetParent(transform,false);var iconRect=(RectTransform)iconGo.transform;iconRect.anchorMin=new Vector2(0f,1f);iconRect.anchorMax=iconRect.anchorMin;iconRect.pivot=iconRect.anchorMin;iconRect.anchoredPosition=new Vector2(48f,-92f);iconRect.sizeDelta=new Vector2(72f,72f);iconGo.GetComponent<Image>().raycastTarget=false;iconGo.GetComponent<UrbanUiIcon>().Configure("daily-planner");
            var close=DiaryButton(transform,"PlanCloseButton",Ui("daily-plan.close","닫기","Close"));var closeRect=(RectTransform)close.transform;closeRect.anchorMin=new Vector2(1f,1f);closeRect.anchorMax=closeRect.anchorMin;closeRect.pivot=closeRect.anchorMin;closeRect.anchoredPosition=new Vector2(-40f,-40f);closeRect.sizeDelta=new Vector2(160f,54f);close.onClick.AddListener(()=>gameObject.SetActive(false));
            var budgetGo = new GameObject("TimeBudget", typeof(RectTransform), typeof(Text));
            budgetGo.transform.SetParent(transform, false);
            var budgetRect = budgetGo.GetComponent<RectTransform>();
            budgetRect.anchorMin = new Vector2(0, 1); budgetRect.anchorMax = Vector2.one;
            budgetRect.pivot = new Vector2(0.5f, 1); budgetRect.offsetMin = new Vector2(136, -154); budgetRect.offsetMax = new Vector2(-40, -100);
            _timeBudget = budgetGo.GetComponent<Text>(); _timeBudget.font = LifeUiFont.Shared;
            _timeBudget.fontSize = 24; _timeBudget.alignment = TextAnchor.MiddleLeft;
            _timeBudget.color = new Color32(48,71,83,255); _timeBudget.raycastTarget = false;

            var viewportGo = new GameObject("RouteViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(transform, false);
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(40f, 40f);
            viewport.offsetMax = new Vector2(-40f, -184f);
            viewportGo.GetComponent<Image>().color = Color.white;
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var rowsGo = new GameObject("RouteRows", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            rowsGo.transform.SetParent(viewportGo.transform, false);
            _rowRoot = rowsGo.GetComponent<RectTransform>();
            _rowRoot.anchorMin = new Vector2(0f, 1f);
            _rowRoot.anchorMax = new Vector2(1f, 1f);
            _rowRoot.pivot = new Vector2(0.5f, 1f);
            _rowRoot.offsetMin = Vector2.zero;
            _rowRoot.offsetMax = Vector2.zero;
            var layout=rowsGo.GetComponent<GridLayoutGroup>();layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=3;layout.cellSize=new Vector2(336f,136f);layout.spacing=new Vector2(16f,16f);
            rowsGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = GetComponent<ScrollRect>();
            if (scroll == null) scroll = gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _rowRoot;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var emptyGo = new GameObject("EmptyState", typeof(RectTransform), typeof(Text));
            emptyGo.transform.SetParent(transform, false);
            var emptyRect = emptyGo.GetComponent<RectTransform>();
            emptyRect.anchorMin = Vector2.zero;
            emptyRect.anchorMax = Vector2.one;
            emptyRect.offsetMin = new Vector2(48f, 98f);
            emptyRect.offsetMax = new Vector2(-48f, -184f);
            _emptyText = emptyGo.GetComponent<Text>();
            _emptyText.font = LifeUiFont.Shared;
            _emptyText.fontSize = 20;
            _emptyText.alignment = TextAnchor.MiddleCenter;
            _emptyText.raycastTarget = false;
        }

        private Button CreateRouteButton(string label,DailyPlanRouteDefinition route)
        {
            Button button;
            if(_routeReuseIndex<_routePool.Count)button=_routePool[_routeReuseIndex];
            else
            {
                var go=new GameObject("DailyPlanRouteRow",typeof(RectTransform),typeof(Image),typeof(Button),typeof(ModernUiTileImage),typeof(LayoutElement));go.transform.SetParent(_rowRoot,false);
                ((RectTransform)go.transform).sizeDelta=new Vector2(336f,136f);go.GetComponent<LayoutElement>().preferredHeight=136f;
                var chrome=go.GetComponent<ModernUiTileImage>();chrome.SetRecipe(ModernUiRecipes.SlotPanel);chrome.SetTileSize(new Vector2(48f,48f));
                var iconGo=new GameObject("RouteIcon",typeof(RectTransform),typeof(Image),typeof(UrbanUiIcon));iconGo.transform.SetParent(go.transform,false);
                var iconRect=(RectTransform)iconGo.transform;iconRect.anchorMin=new Vector2(0f,.5f);iconRect.anchorMax=iconRect.anchorMin;iconRect.pivot=new Vector2(0f,.5f);iconRect.anchoredPosition=new Vector2(16f,0f);iconRect.sizeDelta=new Vector2(80f,80f);iconGo.GetComponent<Image>().raycastTarget=false;
                var textGo=new GameObject("Label",typeof(RectTransform),typeof(Text));textGo.transform.SetParent(go.transform,false);var textRect=(RectTransform)textGo.transform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.offsetMin=new Vector2(112f,16f);textRect.offsetMax=new Vector2(-16f,-16f);
                var text=textGo.GetComponent<Text>();text.font=LifeUiFont.Shared;text.fontSize=20;text.color=new Color32(48,71,83,255);text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
                button=go.GetComponent<Button>();_routePool.Add(button);
            }
            _routeReuseIndex++;button.gameObject.SetActive(true);button.interactable=true;button.onClick.RemoveAllListeners();
            button.GetComponentInChildren<Text>(true).text=string.IsNullOrEmpty(label)?GameText.Get("life.daily-plan.title"):label;
            button.transform.Find("RouteIcon").GetComponent<UrbanUiIcon>().ConfigureFor(route,"plan");return button;
        }

        private void ClearRows()
        {
            _routeReuseIndex=0;
            for(int i=0;i<_routePool.Count;i++){_routePool[i].gameObject.SetActive(false);_routePool[i].onClick.RemoveAllListeners();}
        }
    }
}
