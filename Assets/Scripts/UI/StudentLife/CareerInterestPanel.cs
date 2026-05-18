using System.Collections.Generic;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class CareerInterestPanel : MonoBehaviour
    {
        private const int MaxInterestButtons = 6;

        private GameObject _root;
        private Text _title;
        private Text _details;
        private readonly Button[] _interestButtons = new Button[MaxInterestButtons];
        private readonly Text[] _interestButtonLabels = new Text[MaxInterestButtons];
        private CareerInterestDefinition[] _interests = System.Array.Empty<CareerInterestDefinition>();
        private CareerInterestProgress _interestProgress;
        private CareerInterestProgressComponent _interestComponent;
        private CareerCandidateProgress _candidateProgress;
        private StudentLifeProgress _studentProgress;
        private int _day;

        public bool IsOpen => _root != null && _root.activeSelf;
        public event System.Action SelectionChanged;
        public string DetailsTextForTests => _details != null ? _details.text : string.Empty;

        public static CareerInterestPanel EnsureInScene(Canvas canvas)
        {
            var existing = Object.FindFirstObjectByType<CareerInterestPanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("CareerInterestPanel");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<CareerInterestPanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Show(CareerInterestDefinition[] interests, CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            _interestComponent = null;
            ShowInternal(interests, interestProgress, candidateProgress, studentProgress, day);
        }

        public void Show(CareerInterestDefinition[] interests, CareerInterestProgressComponent interestComponent, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            _interestComponent = interestComponent;
            ShowInternal(interests, interestComponent != null ? interestComponent.EnsureProgress() : null, candidateProgress, studentProgress, day);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public Button GetInterestButtonForTests(int index) => index >= 0 && index < _interestButtons.Length ? _interestButtons[index] : null;

        private void ShowInternal(CareerInterestDefinition[] interests, CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            BuildIfNeeded();
            _interests = interests ?? System.Array.Empty<CareerInterestDefinition>();
            _interestProgress = interestProgress;
            _candidateProgress = candidateProgress;
            _studentProgress = studentProgress;
            _day = Mathf.Max(0, day);
            _title.text = "Career Interest";
            BindButtons();
            EnsureCurrentInterestLinkedQuests();
            ShowDetails(_interests.Length > 0 ? 0 : -1);
            _root.SetActive(true);
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("CareerInterestRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(760f, 500f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();

            _title = MakeText(rt, "Title", "Career Interest", new Vector2(0f, 210f), new Vector2(640f, 38f), 24, TextAnchor.MiddleCenter);
            _details = MakeText(rt, "Details", string.Empty, new Vector2(120f, 20f), new Vector2(430f, 330f), 15, TextAnchor.UpperLeft);

            for (int i = 0; i < _interestButtons.Length; i++)
            {
                var button = MakeButton(rt, "InterestButton_" + i, new Vector2(-245f, 130f - i * 48f), new Vector2(220f, 38f));
                _interestButtons[i] = button;
                _interestButtonLabels[i] = button.GetComponentInChildren<Text>(true);
            }

            var close = MakeButton(rt, "CloseButton", new Vector2(300f, -212f), new Vector2(110f, 36f));
            close.GetComponentInChildren<Text>(true).text = "Close";
            close.onClick.AddListener(Hide);
        }

        private void BindButtons()
        {
            for (int i = 0; i < _interestButtons.Length; i++)
            {
                var button = _interestButtons[i];
                var label = _interestButtonLabels[i];
                button.onClick.RemoveAllListeners();
                if (i < _interests.Length && _interests[i] != null)
                {
                    int index = i;
                    var summary = CareerInterestSummaryBuilder.Build(_interests[i], _interestProgress, _candidateProgress, _studentProgress, _day);
                    label.text = summary.DisplayName + " " + summary.State;
                    button.gameObject.SetActive(true);
                    button.onClick.AddListener(() => SelectAndShow(index));
                }
                else
                {
                    label.text = string.Empty;
                    button.gameObject.SetActive(false);
                }
            }
        }

        private void SelectAndShow(int index)
        {
            if (index < 0 || index >= _interests.Length || _interestProgress == null) return;
            var result = _interestProgress.TrySelect(_interests[index], _candidateProgress, _studentProgress, _day, "career-interest-ui-" + _interests[index].Id + "-" + _day);
            if (result.Applied)
            {
                if (_interestComponent != null) CareerInterestProgressPersistence.Save(_interestComponent);
                ApplyLinkedQuests(_interests[index]);
                SelectionChanged?.Invoke();
            }
            BindButtons();
            ShowDetails(index);
        }

        private void EnsureCurrentInterestLinkedQuests()
        {
            if (_interestProgress == null || string.IsNullOrEmpty(_interestProgress.CurrentInterestId)) return;
            for (int i = 0; i < _interests.Length; i++)
            {
                var interest = _interests[i];
                if (interest != null && interest.Id == _interestProgress.CurrentInterestId)
                {
                    ApplyLinkedQuests(interest);
                    return;
                }
            }
        }

        private void ApplyLinkedQuests(CareerInterestDefinition interest)
        {
            if (interest == null || interest.LinkedQuests == null || interest.LinkedQuests.Count == 0) return;
            var questPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            if (questPanel == null || questPanel.QuestLog == null) return;
            var inventory = _interestComponent != null ? _interestComponent.GetComponent<PlayerInventory>() : null;
            string playerId = ResolvePlayerId(inventory);
            bool added = false;
            for (int i = 0; i < interest.LinkedQuests.Count; i++)
            {
                var quest = interest.LinkedQuests[i];
                if (quest == null || ContainsQuest(questPanel.Quests, quest.Id)) continue;
                questPanel.QuestLog.AddQuest(quest);
                added = true;
            }

            if (added) LoadQuestLog(questPanel.QuestLog, playerId);

            bool changed = false;
            for (int i = 0; i < interest.LinkedQuests.Count; i++)
            {
                var quest = interest.LinkedQuests[i];
                if (quest == null) continue;
                if (questPanel.QuestLog.GetState(quest) == QuestState.NotStarted && questPanel.QuestLog.Accept(quest)) changed = true;
            }

            var context = new RewardRuntimeContext(questPanel.QuestLog, inventory != null ? inventory.Inventory : null, null, new StoryFlagSet(), _studentProgress);
            questPanel.Bind(questPanel.QuestLog, AppendQuests(questPanel.Quests, interest.LinkedQuests), context);
            if (changed) SaveQuestLog(questPanel.QuestLog, playerId);
        }

        private static QuestDefinition[] AppendQuests(QuestDefinition[] current, IReadOnlyList<QuestDefinition> linked)
        {
            var result = new List<QuestDefinition>();
            if (current != null) for (int i = 0; i < current.Length; i++) if (current[i] != null && !ContainsQuest(result, current[i].Id)) result.Add(current[i]);
            if (linked != null) for (int i = 0; i < linked.Count; i++) if (linked[i] != null && !ContainsQuest(result, linked[i].Id)) result.Add(linked[i]);
            return result.ToArray();
        }

        private static bool ContainsQuest(QuestDefinition[] quests, string questId)
        {
            if (quests == null) return false;
            for (int i = 0; i < quests.Length; i++) if (quests[i] != null && quests[i].Id == questId) return true;
            return false;
        }

        private static bool ContainsQuest(List<QuestDefinition> quests, string questId)
        {
            for (int i = 0; i < quests.Count; i++) if (quests[i] != null && quests[i].Id == questId) return true;
            return false;
        }

        private static void SaveQuestLog(QuestLog questLog, string playerId)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || questLog == null) return;
            new SaveService(metadata.SlotId).WriteJson(QuestLogFileNameFor(playerId), JsonUtility.ToJson(questLog.ToSaveData(), true));
        }

        private static void LoadQuestLog(QuestLog questLog, string playerId)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || questLog == null) return;
            var json = new SaveService(metadata.SlotId).ReadJson(QuestLogFileNameFor(playerId));
            if (string.IsNullOrEmpty(json)) return;
            questLog.LoadFromSaveData(JsonUtility.FromJson<QuestLogSaveData>(json));
        }

        private static string ResolvePlayerId(PlayerInventory inventory)
        {
            var identity = inventory != null ? inventory.GetComponent<PlayerIdentity>() : null;
            return identity != null ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }

        private static string QuestLogFileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId) return "quest-log.json";
            return "quest-log-" + SanitizeFileName(playerId) + ".json";
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }

        private void ShowDetails(int index)
        {
            if (index < 0 || index >= _interests.Length || _interests[index] == null)
            {
                _details.text = "No career interests";
                return;
            }

            var summary = CareerInterestSummaryBuilder.Build(_interests[index], _interestProgress, _candidateProgress, _studentProgress, _day);
            string text = summary.DisplayName + "\nState: " + summary.State;
            if (!string.IsNullOrEmpty(summary.Reason)) text += "\n" + summary.Reason;
            if (!string.IsNullOrEmpty(summary.Description)) text += "\n" + summary.Description;
            text += FormatLines("\nRelated Locations", summary.RelatedLocationKeys, "No locations yet");
            text += FormatLines("\nRelated Activities", summary.RelatedActivityKeys, "No activities yet");
            text += FormatLines("\nRecommended", summary.RecommendedActionKeys, "Explore freely");
            _details.text = text;
        }

        private static string FormatLines(string title, string[] values, string empty)
        {
            string text = title;
            if (values == null || values.Length == 0) return text + "\n" + empty;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) text += "\n" + values[i];
            return text;
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

        private static Button MakeButton(RectTransform parent, string name, Vector2 position, Vector2 size)
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
            MakeText(rt, "Label", string.Empty, Vector2.zero, size, 13, TextAnchor.MiddleCenter);
            return go.GetComponent<Button>();
        }
    }
}
