using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.Game.VerticalSlice;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public readonly struct DailyPlanDaySummary
    {
        public DailyPlanDaySummary(string routeDisplayNameKey, string activityDisplayNameKey, bool completed, string[] resultLogs, string nextHintKey)
        {
            RouteDisplayNameKey = routeDisplayNameKey ?? string.Empty;
            ActivityDisplayNameKey = activityDisplayNameKey ?? string.Empty;
            Completed = completed;
            ResultLogs = resultLogs ?? System.Array.Empty<string>();
            NextHintKey = nextHintKey ?? string.Empty;
        }

        public string RouteDisplayNameKey { get; }
        public string ActivityDisplayNameKey { get; }
        public bool Completed { get; }
        public string[] ResultLogs { get; }
        public string NextHintKey { get; }
    }

    public sealed class StudentDayResultPanel : MonoBehaviour
    {
        private GameObject _root;
        private GameObject _backdrop;
        private RectTransform _content;
        private ScrollRect _scroll;
        private readonly Dictionary<string, string> _displayKeys = new Dictionary<string, string>(System.StringComparer.Ordinal);
        private readonly List<RectTransform> _summaryHosts = new List<RectTransform>();
        private Text _title;
        private Text _activities;
        private Text _results;
        private Text _quests;
        private Text _rewards;
        private Text _inventory;
        private Text _relationships;
        private Text _conditions;
        private Text _nextGuide;
        private Button _nextDayButton;
        private StudentLifeProgressComponent _progressComponent;

        public bool IsOpen => _root != null && _root.activeSelf;
        public string ResultsTextForTests => _results != null ? _results.text : string.Empty;
        public string NextGuideTextForTests => _nextGuide != null ? _nextGuide.text : string.Empty;

        public static StudentDayResultPanel EnsureInScene(Canvas canvas)
        {
            if (canvas == null) return null;
            foreach (var existing in canvas.GetComponentsInChildren<StudentDayResultPanel>(true))
                if (existing.GetComponentInParent<Canvas>() == canvas) return existing;
            var go = new GameObject("StudentDayResultPanel", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<StudentDayResultPanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Show(StudentLifeProgressComponent progressComponent, StudentDaySummary summary)
        {
            Show(progressComponent, summary, new StudentDayQuestInventorySummary(null, null, null), new StudentDayRelationshipConditionSummary(null, null, null));
        }

        public void Show(StudentLifeProgressComponent progressComponent, StudentDaySummary summary, StudentDayQuestInventorySummary questInventorySummary)
        {
            Show(progressComponent, summary, questInventorySummary, new StudentDayRelationshipConditionSummary(null, null, null));
        }

        public void Show(StudentLifeProgressComponent progressComponent, StudentDaySummary summary, StudentDayQuestInventorySummary questInventorySummary, StudentDayRelationshipConditionSummary relationshipConditionSummary)
        {
            Show(progressComponent, summary, questInventorySummary, relationshipConditionSummary, default);
        }

        public void Show(StudentLifeProgressComponent progressComponent, StudentDaySummary summary, StudentDayQuestInventorySummary questInventorySummary, StudentDayRelationshipConditionSummary relationshipConditionSummary, MilestoneDaySummary milestoneSummary)
        {
            _progressComponent = progressComponent;
            BuildIfNeeded();
            RefreshDisplayKeys();
            foreach (var host in _summaryHosts) { ClearSummaryHost(host); host.gameObject.SetActive(false); }
            _nextDayButton.GetComponentInChildren<Text>(true).text = Ui("next-day", "Next Day");
            _title.text = string.Format(Ui("title", "Day {0} Result"), summary.DayNumber);
            _activities.text = FormatList(Ui("activities", "Activities"), summary.CompletedActivityIds, Ui("empty-activities", "No activities recorded"));
            var careerCandidates = progressComponent != null ? progressComponent.GetComponent<CareerCandidateProgressComponent>() : null;
            var careerInterests = progressComponent != null ? progressComponent.GetComponent<CareerInterestProgressComponent>() : null;
            _results.text = FormatList(Ui("growth", "Growth"), GrowthLines(summary.ResultLogIds), Ui("empty-growth", "No growth recorded")) + "\n\n" + FormatOutsideSchoolList(summary.ResultLogIds) + "\n\n" + FormatCareerCandidateList(careerCandidates != null && careerCandidates.Progress != null ? careerCandidates.Progress.TodayHintResultLines : null) + "\n\n" + FormatCareerInterestList(careerInterests);
            _quests.text = FormatQuestList(questInventorySummary.QuestEntries);
            _rewards.text = FormatRewardList(questInventorySummary.RewardEntries);
            _inventory.text = FormatInventoryList(questInventorySummary.InventoryDeltas);
            _relationships.text = FormatRelationshipList(relationshipConditionSummary.RelationshipEntries);
            _conditions.text = FormatConditionList(relationshipConditionSummary.StatusEntries);
            _nextGuide.text = FormatNextGuide(summary, relationshipConditionSummary.NextDayImpacts) + FormatMilestoneGuide(milestoneSummary);
            _nextDayButton.interactable = progressComponent != null && progressComponent.Progress != null && progressComponent.Progress.DayState == StudentDayState.ResultReady;
            _root.SetActive(true);
            _backdrop.SetActive(true);
            transform.SetAsLastSibling();
            _scroll.verticalNormalizedPosition = 1f;
            UiGameplayInputLock.Acquire(this);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            if (_backdrop != null) _backdrop.SetActive(false);
            UiGameplayInputLock.Release(this);
        }

        private void OnEnable() { if (IsOpen) UiGameplayInputLock.Acquire(this); }
        private void OnDisable() => UiGameplayInputLock.Release(this);
        private void OnDestroy() => UiGameplayInputLock.Release(this);

        public void AppendVerticalSliceGuide(VerticalSliceSummary summary)
        {
            BuildIfNeeded();
            string prefix = _nextGuide != null && !string.IsNullOrEmpty(_nextGuide.text) ? _nextGuide.text + "\n" : string.Empty;
            _nextGuide.text = prefix + ResolveText(summary.FollowUpMotivationText);
        }

        public void AppendDailyPlanSummary(DailyPlanDaySummary summary)
        {
            BuildIfNeeded();
            if (string.IsNullOrEmpty(summary.RouteDisplayNameKey)) return;
            string resultPrefix = _results != null && !string.IsNullOrEmpty(_results.text) ? _results.text + "\n\n" : string.Empty;
            string text = Ui("daily-plan", "Daily Plan") + "\n" + ResolveText(summary.RouteDisplayNameKey);
            if (!string.IsNullOrEmpty(summary.ActivityDisplayNameKey)) text += "\n" + ResolveText(summary.ActivityDisplayNameKey);
            text += "\n" + (summary.Completed ? Ui("completed", "Completed") : Ui("selected", "Selected"));
            for (int i = 0; i < summary.ResultLogs.Length; i++) if (!string.IsNullOrEmpty(summary.ResultLogs[i])) text += "\n" + ResolveText(summary.ResultLogs[i]);
            _results.text = resultPrefix + text;
            if (!string.IsNullOrEmpty(summary.NextHintKey))
            {
                string guidePrefix = _nextGuide != null && !string.IsNullOrEmpty(_nextGuide.text) ? _nextGuide.text + "\n" : string.Empty;
                _nextGuide.text = guidePrefix + ResolveText(summary.NextHintKey);
            }
        }

        // Steps waiting for this day advance first; things done earlier are then offered to whichever step is now current.
        public static void PublishDayStartEvents(StudentLifeProgressComponent student)
        {
            if (student == null || student.Progress == null) return;
            var progress = student.Progress;
            QuestGameplayEventBridge.Record(new QuestEvent(QuestEventKind.DayStarted, QuestGameplayEventBridge.CreateRequestKey("day-started")), progress);
            var dreams = student.GetComponent<CareerInterestProgressComponent>();
            if (dreams != null && dreams.Progress != null && !string.IsNullOrEmpty(dreams.Progress.CurrentInterestId) && dreams.Interests != null)
                foreach (var interest in dreams.Interests)
                    if (interest != null && interest.Id == dreams.Progress.CurrentInterestId)
                    {
                        QuestGameplayEventBridge.Record(new QuestEvent(QuestEventKind.CareerInterestSelected, QuestGameplayEventBridge.CreateRequestKey("career-interest-kept"), careerInterest: interest), progress);
                        break;
                    }
            var room = Object.FindFirstObjectByType<Rootborn.UI.Interiors.AuthoredHousePlacementBinding>();
            if (room != null) room.AnnouncePlacedFurniture();
        }

        private void StartNextDay()
        {
            if (_progressComponent == null || _progressComponent.Progress == null) return;
            var owner = _progressComponent.GetComponent<Rootborn.UI.Careers.AdultCareerRuntimeComponent>();
            bool advanced;
            if (owner != null)
                advanced = owner.ExecuteSessionCommand((session, service) => new Rootborn.Game.Careers.AdultCareerResult(
                    session.Student.TryStartNextDay() ? Rootborn.Game.Careers.AdultCareerResultKind.Applied : Rootborn.Game.Careers.AdultCareerResultKind.InvalidRequest)).Applied;
            else
            {
                advanced = _progressComponent.Progress.TryStartNextDay();
                if (advanced) StudentLifeProgressPersistence.Save(_progressComponent);
            }
            if (!advanced) return;
            _progressComponent.GetComponent<DailyPlanProgressComponent>()?.EnsureProgress(_progressComponent.Progress);
            PublishDayStartEvents(_progressComponent);
            CaptureNextDayBaseline();
            Hide();
        }

        private void CaptureNextDayBaseline()
        {
            var tracker = _progressComponent.GetComponent<StudentDayQuestInventoryTracker>();
            var inventory = _progressComponent.GetComponent<PlayerInventory>();
            if (tracker != null && inventory != null && PlayerQuestRuntime.TryGetActive(_progressComponent.gameObject, out var quests))
            {
                tracker.CaptureBaseline(quests, inventory);
            }
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _backdrop = new GameObject("ResultBackdrop", typeof(RectTransform), typeof(Image));
            _backdrop.transform.SetParent(transform, false);
            var backdropRect = (RectTransform)_backdrop.transform;
            backdropRect.anchorMin = Vector2.zero; backdropRect.anchorMax = Vector2.one; backdropRect.offsetMin = Vector2.zero; backdropRect.offsetMax = Vector2.zero;
            _backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            if (transform is RectTransform ownerRect) { ownerRect.anchorMin = Vector2.zero; ownerRect.anchorMax = Vector2.one; ownerRect.offsetMin = Vector2.zero; ownerRect.offsetMax = Vector2.zero; }
            _root = new GameObject("StudentDayResultRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1160f, 900f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.SetTileSize(new Vector2(32f, 32f));
            tile.Rebuild();

            _title = MakeText(rt, "Title", Ui("heading", "Day Result"), new Vector2(0f, 390f), new Vector2(1040f, 56f), 36, TextAnchor.MiddleCenter);
            var viewport = new GameObject("ResultViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(rt, false);
            var view = (RectTransform)viewport.transform;
            view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
            view.offsetMin = new Vector2(42f, 112f); view.offsetMax = new Vector2(-66f, -112f);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            _scroll = viewport.GetComponent<ScrollRect>();
            _scroll.viewport = view; _scroll.horizontal = false; _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped; _scroll.scrollSensitivity = 48f;
            _content = new GameObject("ResultContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            _content.SetParent(view, false); _content.anchorMin = new Vector2(0f, 1f); _content.anchorMax = Vector2.one; _content.pivot = new Vector2(0.5f, 1f); _content.sizeDelta = Vector2.zero;
            var layout = _content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 20); layout.spacing = 22f;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            _content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.content = _content;
            _activities = MakeSection(_content, "Activities");
            _results = MakeSection(_content, "Results");
            _quests = MakeSection(_content, "Quests");
            _relationships = MakeSection(_content, "Relationships");
            _conditions = MakeSection(_content, "Conditions");
            _rewards = MakeSection(_content, "Rewards");
            _inventory = MakeSection(_content, "Inventory");
            _nextGuide = MakeSection(_content, "NextGuide");
            _nextDayButton = MakeButton(rt, "NextDayButton", Ui("next-day", "Next Day"), new Vector2(0f, -390f), new Vector2(300f, 58f));
            _nextDayButton.onClick.AddListener(StartNextDay);
            var track = new GameObject("ResultScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            track.transform.SetParent(rt, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(1f, 0f); trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(-52f, 112f); trackRect.offsetMax = new Vector2(-34f, -112f);
            track.GetComponent<Image>().color = new Color(0.25f, 0.18f, 0.12f, 0.15f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(trackRect, false);
            var handleRect = (RectTransform)handle.transform; handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one; handleRect.offsetMin = Vector2.zero; handleRect.offsetMax = Vector2.zero;
            handle.GetComponent<Image>().color = new Color(0.46f, 0.34f, 0.21f, 1f);
            var scrollbar = track.GetComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop; scrollbar.handleRect = handleRect; scrollbar.targetGraphic = handle.GetComponent<Image>();
            _scroll.verticalScrollbar = scrollbar;
        }

        private static Text MakeSection(RectTransform parent, string name)
        {
            var text = MakeText(parent, name, string.Empty, Vector2.zero, Vector2.zero, 20, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false; text.lineSpacing = 1.2f;
            return text;
        }

        public RectTransform EnsureSummaryHost(string name)
        {
            BuildIfNeeded();
            var existing = _content.Find(name) as RectTransform;
            if (existing != null) { existing.gameObject.SetActive(true); return existing; }
            var host = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            host.SetParent(_content, false);
            var layout = host.GetComponent<VerticalLayoutGroup>(); layout.spacing = 14f;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            _summaryHosts.Add(host);
            return host;
        }

        public RectTransform FindSummaryHost(string name) => _content != null ? _content.Find(name) as RectTransform : null;

        public static void ClearSummaryHost(RectTransform host)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var child = host.GetChild(i).gameObject; child.SetActive(false); child.transform.SetParent(null, false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
        }

        public void SetSummarySection(string name, string titleKey, string fallback, string body)
        {
            var host = EnsureSummaryHost(name); ClearSummaryHost(host);
            host.gameObject.SetActive(!string.IsNullOrEmpty(body));
            if (string.IsNullOrEmpty(body)) return;
            MakeSection(host, "Summary").text = Ui(titleKey, fallback) + "\n" + ResolveText(body);
        }

        public void ReflowSummaryCard(RectTransform card)
        {
            // The common card creates decorative child tiles; keep them out of text layout.
            // This surface already has its own Style2 background.
            var tile = card.GetComponent<ModernUiTileImage>(); if (tile != null) tile.enabled = false;
            for (int i = 0; i < card.childCount; i++)
            {
                var child = card.GetChild(i);
                if (child.GetComponent<Image>() != null && child.GetComponent<Text>() == null) child.gameObject.SetActive(false);
            }
            var layout = card.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16); layout.spacing = 8f;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            foreach (var label in card.GetComponentsInChildren<Text>(true))
            {
                label.fontSize = 20; label.verticalOverflow = VerticalWrapMode.Overflow; label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.supportRichText = false; label.lineSpacing = 1.2f;
            }
        }

        public Text AddSummaryRow(RectTransform host, string name, string body)
        {
            var label = MakeSection(host, name); label.text = ResolveText(body); return label;
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.12f, 0.18f, 0.25f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static Button MakeButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.72f, 0.58f, 0.32f, 1f);
            MakeText(rt, "Label", label, Vector2.zero, size, 22, TextAnchor.MiddleCenter);
            var tiles = go.AddComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.SetTileSize(new Vector2(16f, 16f));
            tiles.Rebuild();
            return go.GetComponent<Button>();
        }

        private string FormatList(string title, string[] values, string emptyText)
        {
            string text = title;
            if (values == null || values.Length == 0) return text + "\n" + emptyText;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) text += "\n" + ResolveText(values[i]);
            return text;
        }

        private string FormatCareerCandidateList(string[] resultLines)
        {
            string text = Ui("career-candidates", "Career Candidates");
            if (resultLines == null || resultLines.Length == 0) return text + "\n" + Ui("empty-candidates", "No career candidate changes");
            for (int i = 0; i < resultLines.Length; i++)
            {
                if (string.IsNullOrEmpty(resultLines[i])) continue;
                // Lines are stored as candidate:hint:state.
                var parts = resultLines[i].Split(':');
                for (int p = 0; p < parts.Length; p++) parts[p] = ResolveText(parts[p]);
                text += "\n" + string.Join(" · ", parts);
            }
            return text;
        }

        private string FormatCareerInterestList(CareerInterestProgressComponent component)
        {
            string text = Ui("career-interest", "Career Interest");
            if (component == null || component.Progress == null || string.IsNullOrEmpty(component.Progress.CurrentInterestId)) return text + "\n" + Ui("empty-interest", "No selected interest");
            text += "\n" + ResolveText(component.Progress.CurrentInterestId);
            var interests = component.Interests;
            if (interests != null)
            {
                for (int i = 0; i < interests.Length; i++)
                {
                    var interest = interests[i];
                    if (interest == null || interest.Id != component.Progress.CurrentInterestId) continue;
                    var recommendations = interest.BuildRecommendations(component.Progress, null, null, component.Progress.LastChangedDay);
                    for (int r = 0; r < recommendations.Length; r++) if (!string.IsNullOrEmpty(recommendations[r])) text += "\n" + ResolveText(recommendations[r]);
                    break;
                }
            }

            return text;
        }

        public static string[] GrowthLines(string[] resultLogIds) => PartTimeWorkLogCodec.AggregateDeltas(OutsideSchoolLogCodec.AggregateDeltas(resultLogIds));

        private string FormatOutsideSchoolList(string[] resultLogIds)
        {
            var summary = OutsideSchoolGrowthSummary.FromResultLogs(resultLogIds);
            // Store work and other place activities are listed under growth but are not outside-school entries; an "empty" line here would contradict them.
            if (summary.Entries.Length == 0) return string.Empty;
            string text = Ui("outside-school", "Outside School");
            for (int i = 0; i < summary.Entries.Length; i++)
            {
                var entry = summary.Entries[i];
                text += "\n" + ResolveText(entry.ActivityId) + " " + Signed(entry.Delta) + " " + ResolveText(entry.TargetId);
            }
            return text;
        }

        private string FormatNextGuide(StudentDaySummary summary, string[] nextDayImpacts)
        {
            string text = string.IsNullOrEmpty(summary.NextDayEntryPointId) ? Ui("next-day", "Next Day") : Ui("next-prefix", "Next Day: ") + ResolveText(summary.NextDayEntryPointId);
            if (!string.IsNullOrEmpty(summary.TutorialStageId)) text += "\n" + Ui("stage-prefix", "Stage: ") + ResolveText(summary.TutorialStageId);
            if (!string.IsNullOrEmpty(summary.NextObjectiveId)) text += "\n" + Ui("objective-prefix", "Next Objective: ") + ResolveText(summary.NextObjectiveId);
            if (!string.IsNullOrEmpty(summary.NextGuideText)) text += "\n" + ResolveText(summary.NextGuideText);
            if (nextDayImpacts != null)
            {
                for (int i = 0; i < nextDayImpacts.Length; i++) if (!string.IsNullOrEmpty(nextDayImpacts[i])) text += "\n" + ResolveText(nextDayImpacts[i]);
            }
            return text;
        }

        private string FormatMilestoneGuide(MilestoneDaySummary summary)
        {
            string text = string.Empty;
            if (summary.ProgressLines != null && summary.ProgressLines.Length > 0)
            {
                text += "\n" + Ui("milestones", "Milestones");
                for (int i = 0; i < summary.ProgressLines.Length; i++) if (!string.IsNullOrEmpty(summary.ProgressLines[i])) text += "\n" + ResolveText(summary.ProgressLines[i]);
            }

            if (summary.RecommendedActionIds != null && summary.RecommendedActionIds.Length > 0)
            {
                text += "\n" + Ui("recommended", "Recommended");
                for (int i = 0; i < summary.RecommendedActionIds.Length; i++) if (!string.IsNullOrEmpty(summary.RecommendedActionIds[i])) text += "\n" + ResolveText(summary.RecommendedActionIds[i]);
            }

            return text;
        }
        private string FormatQuestList(QuestSummaryEntry[] entries)
        {
            string text = Ui("quests", "Quests");
            if (entries == null || entries.Length == 0) return text + "\n" + Ui("empty-quests", "No quest changes");
            for (int i = 0; i < entries.Length; i++) text += "\n" + ResolveText(entries[i].DisplayName) + " " + Ui("quest-state." + entries[i].BeforeState, entries[i].BeforeState.ToString()) + " → " + Ui("quest-state." + entries[i].AfterState, entries[i].AfterState.ToString());
            return text;
        }

        private string FormatRewardList(RewardSummaryEntry[] entries)
        {
            string text = Ui("rewards", "Rewards");
            if (entries == null || entries.Length == 0) return text + "\n" + Ui("empty-rewards", "No rewards claimed");
            for (int i = 0; i < entries.Length; i++) text += "\n" + ResolveText(entries[i].QuestDisplayName) + ": " + ResolveText(entries[i].ItemDisplayName) + " +" + entries[i].Count;
            return text;
        }

        private string FormatInventoryList(InventoryDeltaEntry[] entries)
        {
            string text = Ui("inventory", "Inventory");
            if (entries == null || entries.Length == 0) return text + "\n" + Ui("empty-inventory", "No inventory changes");
            for (int i = 0; i < entries.Length; i++)
            {
                string sign = entries[i].Delta >= 0 ? "+" : string.Empty;
                string suffix = entries[i].FromReward ? Ui("reward-suffix", " reward") : string.Empty;
                text += "\n" + ResolveText(entries[i].DisplayName) + " " + sign + entries[i].Delta + suffix;
            }
            return text;
        }

        private string FormatRelationshipList(RelationshipDeltaEntry[] entries)
        {
            string text = Ui("relationships", "Relationships");
            if (entries == null || entries.Length == 0) return text + "\n" + Ui("empty-relationships", "No relationship changes");
            for (int i = 0; i < entries.Length; i++) text += "\n" + ResolveText(entries[i].DisplayName) + " " + entries[i].BeforeValue + " -> " + entries[i].AfterValue + " (" + Signed(entries[i].Delta) + ")";
            return text;
        }

        private string FormatConditionList(StatusDeltaEntry[] entries)
        {
            string text = Ui("conditions", "Conditions");
            if (entries == null || entries.Length == 0) return text + "\n" + Ui("empty-conditions", "No condition changes");
            for (int i = 0; i < entries.Length; i++) text += "\n" + ResolveText(entries[i].DisplayName) + " " + entries[i].BeforeValue + " -> " + entries[i].AfterValue + " (" + Signed(entries[i].Delta) + ") " + (entries[i].PersistsToNextDay ? Ui("persists", "persists") : Ui("recovers", "recovers"));
            return text;
        }

        public static string Ui(string key, string fallback)
        {
            string full = "day-result." + key; string translated = GameText.Get(full);
            return translated == full ? fallback : translated;
        }

        private void RefreshDisplayKeys()
        {
            _displayKeys.Clear();
            if (!GameDataRegistryProvider.TryGet(out var registry)) return;
            if (registry.LifeActivities != null) foreach (var definition in registry.LifeActivities)
                if (definition != null) _displayKeys[definition.Id] = definition.DisplayNameKey;
            if (registry.Npcs != null) foreach (var npc in registry.Npcs)
                if (npc != null) _displayKeys[npc.Id] = npc.DisplayNameKey;
            AddDefinitions(registry.LocationActivities); AddDefinitions(registry.OutsideSchoolActivities);
            AddDefinitions(registry.StudentLifeTraits); AddDefinitions(registry.StudentLifeSkills);
            AddDefinitions(registry.Relationships); AddDefinitions(registry.StudentConditionStatuses);
            AddDefinitions(registry.Careers); AddDefinitions(registry.CareerPractices); AddDefinitions(registry.CareerCandidates);
            AddDefinitions(registry.CareerInterests); AddDefinitions(registry.CareerHints); AddDefinitions(registry.PartTimeWorks);
            AddDefinitions(registry.Locations); AddDefinitions(registry.LocationStates); AddDefinitions(registry.TimeSlots);
            AddDefinitions(registry.TutorialStages); AddDefinitions(registry.Campaigns); AddDefinitions(registry.CampaignRoutes);
            if (registry.CampaignRoutes != null) foreach (var route in registry.CampaignRoutes) if (route != null) AddDefinitions(route.Objectives);
            if (registry.DailyPlanRouteSets != null) foreach (var set in registry.DailyPlanRouteSets) if (set != null) AddDefinitions(set.Routes);
        }

        private void AddDefinitions<T>(IEnumerable<T> definitions) where T : StudentLifeDefinitionBase
        {
            if (definitions == null) return;
            foreach (var definition in definitions) if (definition != null) _displayKeys[definition.Id] = definition.DisplayNameKey;
        }

        public string ResolveText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            const string visitPrefix = "location:", visitSuffix = ":visited";
            if (value.StartsWith(visitPrefix, System.StringComparison.Ordinal) && value.EndsWith(visitSuffix, System.StringComparison.Ordinal) && value.Length > visitPrefix.Length + visitSuffix.Length)
                return GameText.Format("day-result.location-visited", ResolveText(value.Substring(visitPrefix.Length, value.Length - visitPrefix.Length - visitSuffix.Length)));
            const string npcPrefix = "npc:", metSuffix = ":met";
            if (value.StartsWith(npcPrefix, System.StringComparison.Ordinal) && value.EndsWith(metSuffix, System.StringComparison.Ordinal) && value.Length > npcPrefix.Length + metSuffix.Length)
                return GameText.Format("day-result.npc-met", ResolveText(value.Substring(npcPrefix.Length, value.Length - npcPrefix.Length - metSuffix.Length)));
            // Schedule meetings are logged as npc@location@time.
            if (value.IndexOf('@') > 0)
            {
                var parts = value.Split('@');
                for (int i = 0; i < parts.Length; i++) parts[i] = ResolveText(parts[i]);
                return string.Join(" · ", parts);
            }
            if (_displayKeys.TryGetValue(value, out var key)) return GameText.Get(key);
            var translated = GameText.Get(value); if (translated != value) return translated;
            if (LocationActivityLogCodec.TryParse(value, out var location))
                return ResolveText(location.ActivityId) + " · " + ResolveText(location.TargetId) + " " + (location.IsUnlock ? Ui("unlocked", "Unlocked") : Signed(location.Delta));
            if (OutsideSchoolLogCodec.TryParseDelta(value, out var outside))
                return ResolveText(outside.ActivityId) + " · " + ResolveText(outside.TargetId) + " " + Signed(outside.Delta);
            if (PartTimeWorkLogCodec.TryParseDelta(value, out var work))
                return ResolveText(work.WorkId) + " · " + ResolveText(work.TargetId) + " " + Signed(work.Delta);
            // Translate each ID/key token in mixed summary sentences without branching on any entity ID.
            return System.Text.RegularExpressions.Regex.Replace(value, @"[A-Za-z][A-Za-z0-9_.-]*", match =>
            {
                string token = match.Value;
                return GameText.Get(_displayKeys.TryGetValue(token, out var displayKey) ? displayKey : token);
            });
        }

        private static string Signed(int value) => value >= 0 ? "+" + value : value.ToString();
    }
}

