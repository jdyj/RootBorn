using System;
using System.Collections.Generic;
using System.Text;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Careers
{
    public sealed class FacilityManagementPanel : MonoBehaviour
    {
        private sealed class WorkRow
        {
            public FacilityWorkDefinition Work;
            public Text Info;
            public Button Play;
            public Button Skip;
        }
        private readonly List<WorkRow> _rows = new List<WorkRow>();
        private AdultCareerRuntimeComponent _runtime;
        private RectTransform _content;
        private Text _title, _summary, _costs, _status;
        private Image _achievement;
        private Button _transition, _upgrade, _practice;
        private CanvasGroup _upgradeIconState;
        private FacilityDefinition _renderedFacility;
        public static event Action<FacilityWorkDefinition, AdultCareerRuntimeComponent> WorkRequested;
        public FacilityDefinition Facility { get; private set; }
        public bool IsOpen => gameObject.activeSelf;
        public int WorkRowCountForTests => _rows.Count;
        public Button TransitionButtonForTests => _transition;
        public Button UpgradeButtonForTests => _upgrade;
        public Button PracticeButtonForTests => _practice;
        public Button GetWorkButtonForTests(int index) => index >= 0 && index < _rows.Count ? _rows[index].Play : null;
        public Button GetAutoSkipButtonForTests(int index) => index >= 0 && index < _rows.Count ? _rows[index].Skip : null;

        public static FacilityManagementPanel EnsureInScene(Canvas canvas)
        {
            if (canvas == null) return null;
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
            var existing = canvas.GetComponentInChildren<FacilityManagementPanel>(true);
            if (existing != null) return existing;
            var go = new GameObject("FacilityManagementPanel", typeof(RectTransform), typeof(Image), typeof(ModernUiTileImage));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(880f, 680f);
            go.GetComponent<Image>().color = Color.clear;
            go.GetComponent<ModernUiTileImage>().SetRecipe(ModernUiRecipes.CommonPanel);
            var panel = go.AddComponent<FacilityManagementPanel>();
            panel.BuildLayout(); go.SetActive(false); return panel;
        }

        public void Show(FacilityDefinition facility, AdultCareerRuntimeComponent runtime)
        {
            if (facility == null || runtime == null) return;
            Unsubscribe(); Facility = facility; _runtime = runtime;
            BuildLayout(); BuildRowsIfNeeded(); _status.text = string.Empty;
            gameObject.SetActive(true); transform.SetAsLastSibling();
            UiGameplayInputLock.Acquire(this);
            _runtime.Committed += Refresh;
            Refresh();
        }
        public void Close()
        {
            Unsubscribe(); UiGameplayInputLock.Release(this); gameObject.SetActive(false);
        }
        private void OnDisable() { Unsubscribe(); UiGameplayInputLock.Release(this); }
        private void OnDestroy() { Unsubscribe(); UiGameplayInputLock.Release(this); }
        private void Unsubscribe() { if (_runtime != null) _runtime.Committed -= Refresh; }

        private void Refresh()
        {
            if (_runtime == null || Facility == null || _runtime.Progress == null) return;
            var progress = _runtime.Progress;
            bool selected = progress.IsAdult && progress.FacilityId == Facility.Id;
            var currentLevel = selected ? Facility.GetLevel(progress.FacilityLevel) : null;
            _title.text = GameText.Get(currentLevel != null ? currentLevel.DisplayNameKey : Facility.DisplayNameKey);
            var trophy = selected && progress.HasEnding(Facility) && currentLevel != null && currentLevel.CompletesRoute ? currentLevel.AchievementSprite : null;
            _achievement.sprite = trophy;
            _achievement.gameObject.SetActive(trophy != null);
            _summary.rectTransform.sizeDelta = new Vector2(trophy != null ? 700 : 816, 90);
            _summary.text = TimeBudgetText.Format(_runtime.StudentComponent.Progress) + "\n" +
                (selected ? GameText.Format("facility.level-reputation", progress.FacilityLevel, progress.Reputation) : GameText.Get("facility.choose-route"));
            if (selected && progress.HasEnding(Facility)) _summary.text += "\n" + GameText.Get("facility.route-complete");
            _transition.interactable = _runtime.CanTransition(Facility);
            _upgrade.interactable = _runtime.CanUpgrade(Facility);
            if (_upgradeIconState != null) _upgradeIconState.alpha = _upgrade.interactable ? 1f : .35f;
            _practice.interactable = CanRequestPractice();
            var next = selected && progress.FacilityLevel < 3 ? Facility.GetLevel(progress.FacilityLevel + 1) : null;
            _costs.text = next != null ? GameText.Format("facility.upgrade-cost", next.Level, FormatCosts(next.BuildCosts())) : GameText.Get(selected ? "facility.maximum-level" : "facility.transition-requirements");
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                row.Info.text = GameText.Get(row.Work.DisplayNameKey) + "\n" + GameText.Format("facility.work-progress", row.Work.TimePointCost, progress.GetMastery(row.Work), row.Work.MaximumMastery, progress.GetSuccessfulCompletions(row.Work));
                row.Play.interactable = _runtime.CanWork(row.Work);
                row.Skip.interactable = _runtime.CanAutoSkip(row.Work);
            }
            if (_runtime.RecoveryRequired) _status.text = GameText.Get("facility.result.RecoveryRequired");
            else if (!_runtime.CanMutate) _status.text = GameText.Get("facility.result.AuthorityDenied");
        }
        private void SelectRoute()
        {
            if (!IsOpen || _runtime == null || !_runtime.CanTransition(Facility)) return;
            ShowResult(_runtime.TryTransition(Facility, Guid.NewGuid().ToString("N")));
        }
        private void Upgrade()
        {
            if (!IsOpen || _runtime == null || !_runtime.CanUpgrade(Facility)) return;
            ShowResult(_runtime.TryUpgrade(Facility, Guid.NewGuid().ToString("N")));
        }
        private void RequestWork(FacilityWorkDefinition work)
        {
            if (!IsOpen || _runtime == null || !_runtime.CanWork(work) || WorkRequested == null) return;
            var owner = _runtime; Close(); WorkRequested?.Invoke(work, owner);
        }
        private bool CanRequestPractice()
        {
            var practice = Facility != null ? Facility.Practice : null;
            if (_runtime == null || !_runtime.CanMutate || _runtime.Progress.IsAdult || _runtime.Progress.HasPendingWork
                || practice == null || practice.Activity == null || practice.Minigame == null || !practice.Minigame.IsValid) return false;
            var student = _runtime.StudentComponent.Progress;
            var activity = practice.Activity;
            if (student.HasPendingMinigame || student.DayState != StudentDayState.InProgress || activity.TimePolicy == null
                || activity.TimePolicy.TimePointCost <= 0 || !student.CanUseTimePolicy(activity.TimePolicy)
                || !student.CanSpend(activity.EnergyCost, activity.FocusCost) || !activity.HasSatisfiedRequirements(student)) return false;
            var registered = _runtime.Registry.CareerPractices;
            for (int i = 0; i < registered.Length; i++) if (ReferenceEquals(registered[i], practice)) return true;
            return false;
        }
        private void RequestPractice()
        {
            if (!IsOpen || !CanRequestPractice()) return;
            var owner = _runtime; var facility = Facility; Close();
            if (!StudentMinigameRequestBridge.TryRequestPractice(owner.gameObject, facility.Practice, QuestGameplayEventBridge.CreateRequestKey("career-practice")))
            {
                Show(facility, owner);
                _status.text = GameText.Get("facility.result.InvalidRequest");
            }
        }
        private void SkipWork(FacilityWorkDefinition work)
        {
            if (!IsOpen || _runtime == null || !_runtime.CanAutoSkip(work)) return;
            ShowResult(_runtime.TryCompleteWork(work, Guid.NewGuid().ToString("N"), 100, true));
        }
        private void ShowResult(AdultCareerResult result)
        {
            Refresh(); _status.text = GameText.Get("facility.result." + result.Kind);
        }
        private static string FormatCosts(InventoryGrant[] costs)
        {
            if (costs.Length == 0) return GameText.Get("facility.no-cost");
            var result = new StringBuilder();
            for (int i = 0; i < costs.Length; i++)
            {
                if (i > 0) result.Append(" · ");
                string key = string.IsNullOrEmpty(costs[i].Item.DisplayKey) ? costs[i].Item.Id : costs[i].Item.DisplayKey;
                result.Append(GameText.Get(key)).Append(" × ").Append(costs[i].Count);
            }
            return result.ToString();
        }
        private void BuildRowsIfNeeded()
        {
            if (_renderedFacility == Facility) return;
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            _rows.Clear(); _renderedFacility = Facility;
            for (int i = 0; i < Facility.Works.Count; i++)
            {
                var work = Facility.Works[i]; if (work == null) continue;
                var go = new GameObject("WorkRow", typeof(RectTransform), typeof(LayoutElement)); go.transform.SetParent(_content, false);
                go.GetComponent<LayoutElement>().preferredHeight = 114;
                var row = new WorkRow { Work = work };
                row.Info = TextAt(go.transform, "WorkInfo", new Vector2(18, -8), new Vector2(450, 94), 21, TextAnchor.UpperLeft);
                row.Play = ButtonAt(go.transform, "PlayWork", GameText.Get("facility.play-work"), new Vector2(480, -10), new Vector2(142, 42), () => RequestWork(work));
                row.Skip = ButtonAt(go.transform, "AutoSkipWork", GameText.Get("facility.auto-skip"), new Vector2(636, -10), new Vector2(142, 42), () => SkipWork(work));
                TextAt(go.transform, "AutoSkipHint", new Vector2(480, -58), new Vector2(298, 50), 16, TextAnchor.UpperLeft).text = GameText.Get("facility.auto-skip-requirements");
                _rows.Add(row);
            }
        }
        private void BuildLayout()
        {
            if (_title != null) return;
            _title = TextAt(transform, "Title", new Vector2(32, -20), new Vector2(700, 44), 30, TextAnchor.MiddleLeft);
            ButtonAt(transform, "Close", GameText.Get("facility.close"), new Vector2(754, -24), new Vector2(94, 40), Close);
            _summary = TextAt(transform, "Summary", new Vector2(32, -78), new Vector2(816, 90), 22, TextAnchor.UpperLeft);
            var achievementObject = new GameObject("CareerAchievement", typeof(RectTransform), typeof(Image));
            achievementObject.transform.SetParent(transform, false);
            TopLeft((RectTransform)achievementObject.transform, new Vector2(756, -80), new Vector2(80, 80));
            _achievement = achievementObject.GetComponent<Image>();
            _achievement.preserveAspect = true;
            _achievement.raycastTarget = false;
            achievementObject.SetActive(false);
            var viewport = new GameObject("WorkViewport", typeof(RectTransform), typeof(RectMask2D)); viewport.transform.SetParent(transform, false);
            var viewRect = (RectTransform)viewport.transform; TopLeft(viewRect, new Vector2(32, -178), new Vector2(816, 286));
            var content = new GameObject("WorkRows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); content.transform.SetParent(viewport.transform, false);
            _content = (RectTransform)content.transform; _content.anchorMin = new Vector2(0, 1); _content.anchorMax = Vector2.one; _content.pivot = new Vector2(0.5f, 1); _content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>(); layout.spacing = 12; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewRect; scroll.content = _content; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            _costs = TextAt(transform, "UpgradeCost", new Vector2(32, -482), new Vector2(816, 54), 20, TextAnchor.UpperLeft);
            _transition = ButtonAt(transform, "ChooseRoute", GameText.Get("facility.transition"), new Vector2(32, -544), new Vector2(256, 48), SelectRoute);
            _practice = ButtonAt(transform, "Practice", GameText.Get("facility.practice"), new Vector2(312, -544), new Vector2(256, 48), RequestPractice);
            _upgrade = ButtonAt(transform, "UpgradeFacility", GameText.Get("facility.upgrade"), new Vector2(592, -544), new Vector2(256, 48), Upgrade);
            var upgradeIcon = new GameObject("UpgradeIcon", typeof(RectTransform), typeof(Image), typeof(UrbanUiIcon), typeof(CanvasGroup));
            upgradeIcon.transform.SetParent(_upgrade.transform, false);
            TopLeft((RectTransform)upgradeIcon.transform, new Vector2(10, -4), new Vector2(40, 40));
            upgradeIcon.GetComponent<UrbanUiIcon>().Configure("facility-upgrade");
            _upgradeIconState = upgradeIcon.GetComponent<CanvasGroup>();
            _upgradeIconState.blocksRaycasts = false;
            var upgradeLabel = _upgrade.transform.Find("Label").GetComponent<Text>();
            TopLeft(upgradeLabel.rectTransform, new Vector2(58, -4), new Vector2(186, 40));
            _status = TextAt(transform, "Result", new Vector2(32, -608), new Vector2(816, 48), 19, TextAnchor.UpperLeft);
        }
        private static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static Text TextAt(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); TopLeft((RectTransform)go.transform, position, size);
            var text = go.GetComponent<Text>(); text.font = LifeUiFont.Shared; text.fontSize = fontSize; text.alignment = alignment; text.color = new Color(0.22f, 0.19f, 0.18f); text.raycastTarget = false; return text;
        }
        private static Button ButtonAt(Transform parent, string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage)); go.transform.SetParent(parent, false); TopLeft((RectTransform)go.transform, position, size);
            go.GetComponent<ModernUiTileImage>().SetRecipe(ModernUiRecipes.CommonPanel);
            var text = TextAt(go.transform, "Label", new Vector2(8, -4), size - new Vector2(16, 8), 20, TextAnchor.MiddleCenter); text.text = label;
            var button = go.GetComponent<Button>(); button.onClick.AddListener(action); return button;
        }
    }
}
