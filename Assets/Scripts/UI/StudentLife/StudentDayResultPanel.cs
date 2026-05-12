using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class StudentDayResultPanel : MonoBehaviour
    {
        private GameObject _root;
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

        public static StudentDayResultPanel EnsureInScene(Canvas canvas)
        {
            var existing = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("StudentDayResultPanel");
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
            _title.text = "Day " + summary.DayNumber + " Result";
            _activities.text = FormatList("Activities", summary.CompletedActivityIds, "No activities recorded");
            _results.text = FormatList("Growth", summary.ResultLogIds, "No growth recorded") + "\n\n" + FormatOutsideSchoolList(summary.ResultLogIds);
            _quests.text = FormatQuestList(questInventorySummary.QuestEntries);
            _rewards.text = FormatRewardList(questInventorySummary.RewardEntries);
            _inventory.text = FormatInventoryList(questInventorySummary.InventoryDeltas);
            _relationships.text = FormatRelationshipList(relationshipConditionSummary.RelationshipEntries);
            _conditions.text = FormatConditionList(relationshipConditionSummary.StatusEntries);
            _nextGuide.text = FormatNextGuide(summary, relationshipConditionSummary.NextDayImpacts) + FormatMilestoneGuide(milestoneSummary);
            _nextDayButton.interactable = progressComponent != null && progressComponent.Progress != null && progressComponent.Progress.DayState == StudentDayState.ResultReady;
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void StartNextDay()
        {
            if (_progressComponent == null || _progressComponent.Progress == null) return;
            if (_progressComponent.Progress.TryStartNextDay())
            {
                StudentLifeProgressPersistence.Save(_progressComponent);
                CaptureNextDayBaseline();
                Hide();
            }
        }

        private void CaptureNextDayBaseline()
        {
            var tracker = _progressComponent.GetComponent<StudentDayQuestInventoryTracker>();
            var inventory = _progressComponent.GetComponent<PlayerInventory>();
            var questPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            if (tracker != null && inventory != null && questPanel != null && questPanel.QuestLog != null)
            {
                tracker.CaptureBaseline(questPanel, inventory);
            }
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("StudentDayResultRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(860f, 640f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();

            _title = MakeText(rt, "Title", "Day Result", new Vector2(0f, 270f), new Vector2(740f, 42f), 28, TextAnchor.MiddleCenter);
            _activities = MakeText(rt, "Activities", string.Empty, new Vector2(-295f, 164f), new Vector2(230f, 132f), 15, TextAnchor.UpperLeft);
            _results = MakeText(rt, "Results", string.Empty, new Vector2(-35f, 164f), new Vector2(250f, 132f), 15, TextAnchor.UpperLeft);
            _quests = MakeText(rt, "Quests", string.Empty, new Vector2(260f, 164f), new Vector2(230f, 132f), 15, TextAnchor.UpperLeft);
            _relationships = MakeText(rt, "Relationships", string.Empty, new Vector2(-220f, 10f), new Vector2(360f, 130f), 15, TextAnchor.UpperLeft);
            _conditions = MakeText(rt, "Conditions", string.Empty, new Vector2(220f, 10f), new Vector2(360f, 130f), 15, TextAnchor.UpperLeft);
            _rewards = MakeText(rt, "Rewards", string.Empty, new Vector2(-220f, -140f), new Vector2(360f, 118f), 15, TextAnchor.UpperLeft);
            _inventory = MakeText(rt, "Inventory", string.Empty, new Vector2(220f, -140f), new Vector2(360f, 118f), 15, TextAnchor.UpperLeft);
            _nextGuide = MakeText(rt, "NextGuide", "Next Day", new Vector2(0f, -252f), new Vector2(760f, 78f), 15, TextAnchor.MiddleCenter);
            _nextDayButton = MakeButton(rt, "NextDayButton", "Next Day", new Vector2(0f, -305f), new Vector2(220f, 44f));
            _nextDayButton.onClick.AddListener(StartNextDay);
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
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
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
            return go.GetComponent<Button>();
        }

        private static string FormatList(string title, string[] values, string emptyText)
        {
            string text = title;
            if (values == null || values.Length == 0) return text + "\n" + emptyText;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) text += "\n" + values[i];
            return text;
        }

        private static string FormatOutsideSchoolList(string[] resultLogIds)
        {
            string text = "Outside School";
            var summary = OutsideSchoolGrowthSummary.FromResultLogs(resultLogIds);
            if (summary.Entries.Length == 0) return text + "\nNo outside-school growth";
            for (int i = 0; i < summary.Entries.Length; i++)
            {
                var entry = summary.Entries[i];
                text += "\n" + entry.ActivityId + " " + Signed(entry.Delta) + " " + entry.TargetId;
            }
            return text;
        }

        private static string FormatNextGuide(StudentDaySummary summary, string[] nextDayImpacts)
        {
            string text = string.IsNullOrEmpty(summary.NextDayEntryPointId) ? "Next Day" : "Next Day: " + summary.NextDayEntryPointId;
            if (!string.IsNullOrEmpty(summary.TutorialStageId)) text += "\nStage: " + summary.TutorialStageId;
            if (!string.IsNullOrEmpty(summary.NextObjectiveId)) text += "\nNext Objective: " + summary.NextObjectiveId;
            if (!string.IsNullOrEmpty(summary.NextGuideText)) text += "\n" + summary.NextGuideText;
            if (nextDayImpacts != null)
            {
                for (int i = 0; i < nextDayImpacts.Length; i++) if (!string.IsNullOrEmpty(nextDayImpacts[i])) text += "\n" + nextDayImpacts[i];
            }
            return text;
        }

        private static string FormatMilestoneGuide(MilestoneDaySummary summary)
        {
            string text = string.Empty;
            if (summary.ProgressLines != null && summary.ProgressLines.Length > 0)
            {
                text += "\nMilestones";
                for (int i = 0; i < summary.ProgressLines.Length; i++) if (!string.IsNullOrEmpty(summary.ProgressLines[i])) text += "\n" + summary.ProgressLines[i];
            }

            if (summary.RecommendedActionIds != null && summary.RecommendedActionIds.Length > 0)
            {
                text += "\nRecommended";
                for (int i = 0; i < summary.RecommendedActionIds.Length; i++) if (!string.IsNullOrEmpty(summary.RecommendedActionIds[i])) text += "\n" + summary.RecommendedActionIds[i];
            }

            return text;
        }
        private static string FormatQuestList(QuestSummaryEntry[] entries)
        {
            string text = "Quests";
            if (entries == null || entries.Length == 0) return text + "\nNo quest changes";
            for (int i = 0; i < entries.Length; i++) text += "\n" + entries[i].DisplayName + " " + entries[i].BeforeState + " -> " + entries[i].AfterState;
            return text;
        }

        private static string FormatRewardList(RewardSummaryEntry[] entries)
        {
            string text = "Rewards";
            if (entries == null || entries.Length == 0) return text + "\nNo rewards claimed";
            for (int i = 0; i < entries.Length; i++) text += "\n" + entries[i].QuestDisplayName + ": " + entries[i].ItemDisplayName + " +" + entries[i].Count;
            return text;
        }

        private static string FormatInventoryList(InventoryDeltaEntry[] entries)
        {
            string text = "Inventory";
            if (entries == null || entries.Length == 0) return text + "\nNo inventory changes";
            for (int i = 0; i < entries.Length; i++)
            {
                string sign = entries[i].Delta >= 0 ? "+" : string.Empty;
                string suffix = entries[i].FromReward ? " reward" : string.Empty;
                text += "\n" + entries[i].DisplayName + " " + sign + entries[i].Delta + suffix;
            }
            return text;
        }

        private static string FormatRelationshipList(RelationshipDeltaEntry[] entries)
        {
            string text = "Relationships";
            if (entries == null || entries.Length == 0) return text + "\nNo relationship changes";
            for (int i = 0; i < entries.Length; i++) text += "\n" + entries[i].DisplayName + " " + entries[i].BeforeValue + " -> " + entries[i].AfterValue + " (" + Signed(entries[i].Delta) + ")";
            return text;
        }

        private static string FormatConditionList(StatusDeltaEntry[] entries)
        {
            string text = "Conditions";
            if (entries == null || entries.Length == 0) return text + "\nNo condition changes";
            for (int i = 0; i < entries.Length; i++) text += "\n" + entries[i].DisplayName + " " + entries[i].BeforeValue + " -> " + entries[i].AfterValue + " (" + Signed(entries[i].Delta) + ") " + (entries[i].PersistsToNextDay ? "persists" : "recovers");
            return text;
        }

        private static string Signed(int value) => value >= 0 ? "+" + value : value.ToString();
    }
}
