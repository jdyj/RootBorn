using System;
using System.Collections.Generic;
using Rootborn.Game.Localization;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Placement;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Careers;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class LocationIdentityPanel : MonoBehaviour
    {

        private GameObject _root;
        private Text _title;
        private Text _description;
        private Text _hints;
        private Text _result;
        private readonly List<Button> _activityButtons = new List<Button>();
        private readonly List<Text> _activityLabels = new List<Text>();
        private RectTransform _activityContent;
        private Text _timeBudget;
        private Text _locationState;
        private LocationStateSummaryModel _locationStateSummary;
        private bool _hasLocationStateSummary;
        private LocationIdentityDefinition _identity;
        private GameObject _player;
        private StudentLifeProgressComponent _progressComponent;
        private DailyPlanRouteSetDefinition[] _dailyPlanRouteSets = Array.Empty<DailyPlanRouteSetDefinition>();
        private readonly List<FurnitureDefinition> _offers = new List<FurnitureDefinition>();
        private int _requestSequence;
        private readonly LocationActivityRunner _runner = new LocationActivityRunner();

        public static event Action<LocationActivityDefinition, string> OnAnyActivityApplied;

        public bool IsOpen => _root != null && _root.activeSelf;
        public LocationActivityResult LastResult { get; private set; }

        public static LocationIdentityPanel EnsureInScene(Canvas canvas)
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("LocationIdentityPanel");
            go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<LocationIdentityPanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void BindDailyPlanRouteSetsForTests(DailyPlanRouteSetDefinition[] routeSets)
        {
            _dailyPlanRouteSets = routeSets ?? Array.Empty<DailyPlanRouteSetDefinition>();
        }

        public void ClickActivityForTests(int index)
        {
            BuildIfNeeded();
            if (index >= 0 && index < _activityButtons.Count)
            {
                _activityButtons[index].onClick.Invoke();
            }
        }

        public void Show(LocationIdentityDefinition identity, GameObject player)
        {
            BuildIfNeeded();
            _identity = identity;
            _player = player;
            _progressComponent = player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
            _title.text = GameText.Get(identity != null && identity.Location != null ? identity.Location.DisplayNameKey : string.Empty);
            _description.text = GameText.Get(identity != null ? identity.DescriptionKey : "life.location.unavailable");
            _hints.text = FormatHints(identity);
            _result.text = string.Empty;
            RefreshOffers(identity);
            RefreshTimeBudget();
            BindButtons(identity);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _root.transform.SetAsLastSibling();
            _root.SetActive(true);
            UiGameplayInputLock.Acquire(this);
            RefreshLocationState();
        }

        public void Hide()
        {
            UiGameplayInputLock.Release(this);
            if (_root != null) _root.SetActive(false);
        }

        private void OnDisable()
        {
            UiGameplayInputLock.Release(this);
            if (_root != null) _root.SetActive(false);
        }

        private void OnDestroy() => UiGameplayInputLock.Release(this);

        public void SetLocationStateSummary(LocationStateSummaryModel summary)
        {
            BuildIfNeeded();
            _locationStateSummary = summary;
            _hasLocationStateSummary = true;
            RefreshLocationState();
        }

        private void RefreshLocationState()
        {
            if (_locationState == null) return;
            bool visible = _hasLocationStateSummary && _identity != null && _identity.Location != null
                && _locationStateSummary.LocationId == _identity.Location.Id
                && _locationStateSummary.PrimaryState != null && !_locationStateSummary.IsHidden
                && !string.IsNullOrEmpty(_locationStateSummary.DisplayName);
            _locationState.gameObject.SetActive(visible);
            _locationState.text = visible ? GameText.Get(_locationStateSummary.DisplayName)
                + (string.IsNullOrEmpty(_locationStateSummary.Description) ? string.Empty : "\n" + GameText.Get(_locationStateSummary.Description)) : string.Empty;
        }

        private void PerformActivity(int index)
        {
            if (_identity == null || index < 0 || index >= _identity.AvailableActivities.Count) return;
            var activity = _identity.AvailableActivities[index];
            if (activity == null || _progressComponent == null) return;
            var progress = _progressComponent.EnsureProgress();
            int activityLogCount = progress.GetActivityLogIds().Length;
            string requestId = "location-activity:" + activity.Id + ":" + activityLogCount + ":" + _requestSequence++;
            bool applied = _runner.TryPerform(activity, progress, requestId, out var result);
            LastResult = result;
            if (applied)
            {
                StudentLifeProgressPersistence.Save(_progressComponent);
                var careerCandidates = _player != null ? _player.GetComponent<CareerCandidateProgressComponent>() : null;
                if (careerCandidates != null) careerCandidates.ApplyHintsFromResult(progress, result.RequestId, result.OutcomeLogIds);
                RecordDailyPlanActivity(activity, requestId, progress);
                progress = _progressComponent.Progress;
                RecordQuestActivity(activity, requestId);
                QuestGameplayEventBridge.Record(new QuestEvent(QuestEventKind.LocationActivity, requestId, activity: activity), progress);
                progress = _progressComponent.Progress;
                OnAnyActivityApplied?.Invoke(activity, requestId);
            }
            _result.text = applied
                ? GameText.Format("life.tp.applied", progress.TimePointsRemaining, progress.DailyTimePointBudget)
                : GameText.Get("life.tp.unavailable");
            if (applied && !string.IsNullOrEmpty(activity.ResultTextKey))
                _result.text += "\n" + GameText.Get(activity.ResultTextKey);
            RefreshTimeBudget();
            BindButtons(_identity);
        }

        private void RecordDailyPlanActivity(LocationActivityDefinition activity, string requestId, StudentLifeProgress progress)
        {
            var dailyPlan = _player != null ? _player.GetComponent<DailyPlanProgressComponent>() : null;
            if (dailyPlan == null || activity == null || progress == null) return;
            var routeSets = ResolveDailyPlanRouteSets();
            if (routeSets.Length == 0) return;
            dailyPlan.TryRecordScheduledActivity(routeSets, activity, progress, progress.CurrentDay, requestId, out _);
        }

        private DailyPlanRouteSetDefinition[] ResolveDailyPlanRouteSets()
        {
            if (_dailyPlanRouteSets != null && _dailyPlanRouteSets.Length > 0) return _dailyPlanRouteSets;
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            return registry != null ? registry.DailyPlanRouteSets : Array.Empty<DailyPlanRouteSetDefinition>();
        }

        private void RecordQuestActivity(LocationActivityDefinition activity, string requestId)
        {
            // The quest session saves itself when the event advances an objective.
            if (activity == null || !Rootborn.Game.Player.PlayerQuestRuntime.TryGetActive(_player, out var quests)) return;
            quests.QuestLog.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, requestId, activity: activity));
        }

        public static string OfferBalanceText(IReadOnlyList<FurnitureDefinition> offers, Rootborn.Game.Player.PlayerInventory inventory)
        {
            if (offers == null || offers.Count == 0 || offers[0] == null || offers[0].PriceItem == null || inventory == null || inventory.Inventory == null) return string.Empty;
            return GameText.FormatLocalized("dream.shop.balance", " · Allowance {0}", inventory.Inventory.CountOf(offers[0].PriceItem));
        }

        private void RefreshTimeBudget()
        {
            _timeBudget.text = TimeBudgetText.Format(_progressComponent != null ? _progressComponent.EnsureProgress() : null)
                + OfferBalanceText(_offers, _player != null ? _player.GetComponent<Rootborn.Game.Player.PlayerInventory>() : null);
        }

        private void BindButtons(LocationIdentityDefinition identity)
        {
            int activityCount = identity != null ? identity.AvailableActivities.Count : 0;
            int count = activityCount + _offers.Count;
            while (_activityButtons.Count < count)
            {
                int i = _activityButtons.Count;
                var button = MakeButton(_activityContent, "LocationActivityButton_" + i, new Vector2(0, -46 - i * 92), new Vector2(504, 76));
                var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
                _activityButtons.Add(button); _activityLabels.Add(button.GetComponentInChildren<Text>(true));
            }
            _activityContent.sizeDelta = new Vector2(0, count * 92);
            var progress = _progressComponent != null ? _progressComponent.EnsureProgress() : null;
            for (int i = 0; i < _activityButtons.Count; i++)
            {
                var button = _activityButtons[i];
                var label = _activityLabels[i];
                button.onClick.RemoveAllListeners();
                if (identity != null && i < identity.AvailableActivities.Count && identity.AvailableActivities[i] != null)
                {
                    int index = i;
                    var activity = identity.AvailableActivities[i];
                    label.text = GameText.Get(activity.DisplayNameKey) + "\n" + TimeBudgetText.Cost(activity.TimePolicy);
                    button.interactable = progress != null && progress.CanUseTimePolicy(activity.TimePolicy)
                        && progress.CanSpend(activity.EnergyCost, activity.FocusCost) && activity.HasSatisfiedRequirements(progress);
                    button.gameObject.SetActive(true);
                    button.onClick.AddListener(() => PerformActivity(index));
                }
                else if (i >= activityCount && i < count)
                {
                    BindOffer(button, label, _offers[i - activityCount]);
                }
                else
                {
                    label.text = string.Empty;
                    button.gameObject.SetActive(false);
                }
            }
        }

        // Found once per visit: the furniture list is large and does not change while the panel is open.
        private void RefreshOffers(LocationIdentityDefinition identity)
        {
            _offers.Clear();
            string locationId = identity != null && identity.Location != null ? identity.Location.Id : string.Empty;
            if (string.IsNullOrEmpty(locationId) || !GameDataRegistryProvider.TryGet(out var registry) || registry == null || registry.FurnitureDefinitions == null) return;
            var furniture = registry.FurnitureDefinitions;
            for (int i = 0; i < furniture.Length; i++)
                if (furniture[i] != null && furniture[i].IsForSale && furniture[i].PurchaseLocationId == locationId) _offers.Add(furniture[i]);
        }

        private static string OfferName(FurnitureDefinition furniture) => string.IsNullOrEmpty(furniture.DisplayNameKey) ? furniture.DisplayName : GameText.Get(furniture.DisplayNameKey);

        private void BindOffer(Button button, Text label, FurnitureDefinition furniture)
        {
            var owner = _player != null ? _player.GetComponent<AdultCareerRuntimeComponent>() : null;
            bool owned = owner != null && owner.FurnitureUnlocks != null && owner.FurnitureUnlocks.IsUnlocked(furniture.UnlockToken);
            var inventory = _player != null ? _player.GetComponent<PlayerInventory>() : null;
            bool affordable = inventory != null && inventory.Inventory != null && inventory.Inventory.CountOf(furniture.PriceItem) >= furniture.Price;
            label.text = owned
                ? GameText.Format("dream.shop.owned", OfferName(furniture))
                : GameText.Format("dream.shop.buy", OfferName(furniture), furniture.Price);
            button.interactable = owner != null && owner.CanMutate && !owned && affordable;
            button.gameObject.SetActive(true);
            button.onClick.AddListener(() => PurchaseOffer(owner, furniture));
        }

        private void PurchaseOffer(AdultCareerRuntimeComponent owner, FurnitureDefinition furniture)
        {
            if (owner == null) return;
            var result = owner.TryPurchaseFurniture(furniture);
            _result.text = result == FurniturePurchaseResult.Purchased ? GameText.Format("dream.shop.bought", OfferName(furniture))
                : result == FurniturePurchaseResult.NotEnoughMoney ? GameText.Get("dream.shop.not-enough")
                : result == FurniturePurchaseResult.AlreadyOwned ? GameText.Format("dream.shop.owned", OfferName(furniture))
                : GameText.Get("dream.shop.failed");
            BindButtons(_identity);
            RefreshTimeBudget();
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("LocationIdentityRoot", typeof(RectTransform), typeof(ModernUiTileImage), typeof(Canvas), typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f,.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1120,800);
            var modal = _root.GetComponent<Canvas>(); modal.overrideSorting = true; modal.sortingOrder = 1200;
            var tile = _root.GetComponent<ModernUiTileImage>(); tile.SetRecipe(ModernUiRecipes.CommonPanel); tile.Rebuild();
            _title = MakeText(rt,"LocationName",string.Empty,new Vector2(0,340),new Vector2(880,48),32,TextAnchor.MiddleCenter);
            _description = MakeText(rt,"LocationDescription",string.Empty,new Vector2(0,272),new Vector2(944,64),22,TextAnchor.UpperCenter);
            _timeBudget = MakeText(rt,"TimeBudget",string.Empty,new Vector2(0,216),new Vector2(944,36),22,TextAnchor.MiddleCenter);
            _locationState = MakeText(rt,"LocationStateSummary",string.Empty,new Vector2(0,154),new Vector2(944,64),20,TextAnchor.UpperCenter);
            _locationState.gameObject.SetActive(false);
            var hintFrame = MakeFrame(rt,"HintFrame",new Vector2(-318,-56),new Vector2(340,336));
            _hints = MakeText(hintFrame,"LocationHints",string.Empty,Vector2.zero,new Vector2(268,272),20,TextAnchor.UpperLeft);
            var activities = MakeFrame(rt,"ActivitiesFrame",new Vector2(176,-56),new Vector2(568,336));
            _result = MakeText(rt,"LocationActivityResult",string.Empty,new Vector2(0,-296),new Vector2(976,112),20,TextAnchor.UpperLeft);
            var viewportGo = new GameObject("ActivityViewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));
            viewportGo.transform.SetParent(activities,false);var viewport=(RectTransform)viewportGo.transform;
            viewport.anchorMin=viewport.anchorMax=viewport.pivot=new Vector2(.5f,.5f);viewport.anchoredPosition=Vector2.zero;viewport.sizeDelta=new Vector2(504,272);
            viewportGo.GetComponent<Image>().color=new Color(1,1,1,.001f);
            var contentGo=new GameObject("ActivityContent",typeof(RectTransform));contentGo.transform.SetParent(viewport,false);_activityContent=(RectTransform)contentGo.transform;
            _activityContent.anchorMin=new Vector2(0,1);_activityContent.anchorMax=Vector2.one;_activityContent.pivot=new Vector2(.5f,1);_activityContent.anchoredPosition=Vector2.zero;
            var scroll=viewportGo.GetComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=_activityContent;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=32;
            var close=MakeButton(rt,"CloseLocation",new Vector2(488,344),new Vector2(48,48));close.GetComponentInChildren<Text>().text=GameText.Get("ui.close-symbol");close.onClick.AddListener(Hide);
        }

        private static RectTransform MakeFrame(RectTransform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(ModernUiTileImage));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;
            rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size;var tile=go.GetComponent<ModernUiTileImage>();tile.SetRecipe(ModernUiRecipes.CommonPanel);tile.Rebuild();return rt;
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = LifeUiFont.Shared;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static Button MakeButton(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = Color.clear;
            var molding = go.GetComponent<ModernUiTileImage>();
            molding.SetRecipe(ModernUiRecipes.CommonPanel);
            molding.Rebuild();
            MakeText(rt, "Label", string.Empty, Vector2.zero, size, 20, TextAnchor.MiddleCenter);
            return go.GetComponent<Button>();
        }

        private static string FormatHints(LocationIdentityDefinition identity)
        {
            string text = GameText.Get("life.location.hints");
            if (identity == null || identity.Hints.Count == 0) return text + "\n" + GameText.Get("life.location.no-hints");
            for (int i = 0; i < identity.Hints.Count; i++)
            {
                var hint = identity.Hints[i];
                text += "\n" + GameText.Get(string.IsNullOrEmpty(hint.DisplayKey) ? "life.route." + hint.GrowthRoute : hint.DisplayKey);
            }

            return text;
        }

    }
}
