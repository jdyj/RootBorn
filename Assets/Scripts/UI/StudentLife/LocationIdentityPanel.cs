using System;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class LocationIdentityPanel : MonoBehaviour
    {
        private const int MaxActivityButtons = 3;

        private GameObject _root;
        private Text _title;
        private Text _description;
        private Text _hints;
        private Text _result;
        private readonly Button[] _activityButtons = new Button[MaxActivityButtons];
        private readonly Text[] _activityLabels = new Text[MaxActivityButtons];
        private LocationIdentityDefinition _identity;
        private GameObject _player;
        private StudentLifeProgressComponent _progressComponent;
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

        public void Show(LocationIdentityDefinition identity, GameObject player)
        {
            BuildIfNeeded();
            _identity = identity;
            _player = player;
            _progressComponent = player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
            _title.text = Humanize(identity != null && identity.Location != null ? identity.Location.DisplayNameKey : string.Empty);
            _description.text = identity != null ? identity.DescriptionKey : "No location identity";
            _hints.text = FormatHints(identity);
            _result.text = string.Empty;
            BindButtons(identity);
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
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
                RecordQuestActivity(activity, requestId);
                OnAnyActivityApplied?.Invoke(activity, requestId);
            }
            _result.text = FormatResult(result);
        }

        private void RecordQuestActivity(LocationActivityDefinition activity, string requestId)
        {
            var questPanel = UnityEngine.Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            if (questPanel == null || questPanel.QuestLog == null || activity == null) return;
            questPanel.QuestLog.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, requestId, activity: activity));
            SaveQuestLog(questPanel.QuestLog, ResolvePlayerId(_player));
        }

        private static void SaveQuestLog(QuestLog questLog, string playerId)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || questLog == null) return;
            new SaveService(metadata.SlotId).WriteJson(QuestLogFileNameFor(playerId), JsonUtility.ToJson(questLog.ToSaveData(), true));
        }

        private static string ResolvePlayerId(GameObject player)
        {
            var identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
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

        private void BindButtons(LocationIdentityDefinition identity)
        {
            for (int i = 0; i < _activityButtons.Length; i++)
            {
                var button = _activityButtons[i];
                var label = _activityLabels[i];
                button.onClick.RemoveAllListeners();
                if (identity != null && i < identity.AvailableActivities.Count && identity.AvailableActivities[i] != null)
                {
                    int index = i;
                    var activity = identity.AvailableActivities[i];
                    label.text = Humanize(activity.DisplayNameKey) + "\n" + activity.GrowthRoute;
                    button.interactable = _progressComponent != null;
                    button.gameObject.SetActive(true);
                    button.onClick.AddListener(() => PerformActivity(index));
                }
                else
                {
                    label.text = string.Empty;
                    button.gameObject.SetActive(false);
                }
            }
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("LocationIdentityRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 190f);
            rt.sizeDelta = new Vector2(620f, 360f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();

            _title = MakeText(rt, "LocationName", "Location", new Vector2(0f, 136f), new Vector2(540f, 34f), 22, TextAnchor.MiddleCenter);
            _description = MakeText(rt, "LocationDescription", string.Empty, new Vector2(0f, 92f), new Vector2(540f, 52f), 15, TextAnchor.UpperCenter);
            _hints = MakeText(rt, "LocationHints", string.Empty, new Vector2(-170f, 0f), new Vector2(210f, 128f), 14, TextAnchor.UpperLeft);
            _result = MakeText(rt, "LocationActivityResult", string.Empty, new Vector2(155f, -78f), new Vector2(270f, 68f), 14, TextAnchor.UpperLeft);

            for (int i = 0; i < _activityButtons.Length; i++)
            {
                var button = MakeButton(rt, "LocationActivityButton_" + i, new Vector2(155f, 54f - i * 54f), new Vector2(270f, 46f));
                _activityButtons[i] = button;
                _activityLabels[i] = button.GetComponentInChildren<Text>(true);
            }
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

        private static string FormatHints(LocationIdentityDefinition identity)
        {
            string text = "Hints";
            if (identity == null || identity.Hints.Count == 0) return text + "\nNo hints yet";
            for (int i = 0; i < identity.Hints.Count; i++)
            {
                var hint = identity.Hints[i];
                text += "\n" + (string.IsNullOrEmpty(hint.DisplayKey) ? hint.GrowthRoute.ToString() : hint.DisplayKey);
            }

            return text;
        }

        private static string FormatResult(LocationActivityResult result)
        {
            string text = result.Kind.ToString();
            var logs = result.OutcomeLogIds;
            if (logs != null) for (int i = 0; i < logs.Length; i++) if (!string.IsNullOrEmpty(logs[i])) text += "\n" + logs[i];
            return text;
        }

        private static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            int dot = key.LastIndexOf('.');
            return dot >= 0 && dot + 1 < key.Length ? key.Substring(dot + 1) : key;
        }
    }
}
