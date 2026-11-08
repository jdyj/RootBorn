using Rootborn.Game.StudentLife;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class DailyEventPanel : MonoBehaviour
    {
        private const int MaxChoiceButtons = 3;

        private GameObject _root;
        private Text _title;
        private Text _description;
        private Text _deadline;
        private Text _result;
        private readonly Button[] _choiceButtons = new Button[MaxChoiceButtons];
        private readonly Text[] _choiceLabels = new Text[MaxChoiceButtons];
        private Button _deferButton;
        private Button _declineButton;
        private DailyEventDefinition _event;
        private string _lastChoiceSummary;
        private StudentLifeProgressComponent _studentProgress;
        private DailyEventProgressComponent _eventProgress;
        private readonly DailyEventRunner _runner = new DailyEventRunner();

        public bool IsOpen => _root != null && _root.activeInHierarchy;
        public LifeActivityResult LastResult { get; private set; }
        public DailyEventRecord LastRecord { get; private set; }
        public static event System.Action<StudentLifeProgress> OnAnyChoiceApplied;

        public static DailyEventPanel EnsureInScene(Canvas canvas)
        {
            var existing = Object.FindFirstObjectByType<DailyEventPanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("DailyEventPanel");
            go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<DailyEventPanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Show(DailyEventDefinition dailyEvent, GameObject player)
        {
            BuildIfNeeded();
            _event = dailyEvent;
            _studentProgress = player != null ? player.GetComponent<StudentLifeProgressComponent>() : null;
            _eventProgress = player != null ? player.GetComponent<DailyEventProgressComponent>() : null;
            if (player != null && _eventProgress == null) _eventProgress = player.AddComponent<DailyEventProgressComponent>();
            _title.text = GameText.Get(dailyEvent != null ? dailyEvent.DisplayNameKey : "life.event.ui.title");
            _description.text = GameText.Get(dailyEvent != null ? dailyEvent.DescriptionKey : "life.event.ui.none");
            _deadline.text = FormatDeadline(dailyEvent);
            _result.text = string.Empty;
            _lastChoiceSummary = string.Empty;
            BindButtons();
            _deferButton.GetComponentInChildren<Text>(true).text = GameText.Get("life.event.ui.later");
            _declineButton.GetComponentInChildren<Text>(true).text = GameText.Get("life.event.ui.decline");
            transform.SetAsLastSibling();
            _root.SetActive(true);
            UiGameplayInputLock.Acquire(this);
        }

        public void Hide()
        {
            UiGameplayInputLock.Release(this);
            if (_root != null) _root.SetActive(false);
        }

        private void OnDisable() => Hide();
        private void OnDestroy() => UiGameplayInputLock.Release(this);

        private void Choose(int index)
        {
            if (_event == null || index < 0 || index >= _event.Choices.Count) return;
            var choice = _event.Choices[index];
            var student = _studentProgress != null ? _studentProgress.EnsureProgress() : null;
            var events = _eventProgress != null ? _eventProgress.EnsureProgress() : null;
            string requestId = _event.Id + ":" + choice.Id;
            bool applied = _runner.TryChoose(_event, choice, student, events, requestId, out var result);
            LastResult = result;
            if (applied) _lastChoiceSummary = GameText.Get(choice.ResultSummaryKey);
            LastRecord = events != null ? events.GetRecord(_event.Id) : null;
            if (applied)
            {
                StudentLifeProgressPersistence.Save(_studentProgress);
                DailyEventProgressPersistence.Save(_eventProgress);
                OnAnyChoiceApplied?.Invoke(student);
                LocationNpcRuntimeInstaller.RefreshSchedulesForActiveScene();
            }
            _result.text = FormatResult(result, LastRecord);
        }

        private void Defer()
        {
            var events = _eventProgress != null ? _eventProgress.EnsureProgress() : null;
            var student = _studentProgress != null ? _studentProgress.EnsureProgress() : null;
            int day = student != null ? student.CurrentDay : 1;
            if (_runner.TryDefer(_event, events, day, out var record))
            {
                LastRecord = record;
                DailyEventProgressPersistence.Save(_eventProgress);
                _result.text = GameText.Format("life.event.ui.deferred", record.DueDay);
            }
        }

        private void Decline()
        {
            var events = _eventProgress != null ? _eventProgress.EnsureProgress() : null;
            var student = _studentProgress != null ? _studentProgress.EnsureProgress() : null;
            int day = student != null ? student.CurrentDay : 1;
            if (_runner.TryDecline(_event, events, day, out var record))
            {
                LastRecord = record;
                DailyEventProgressPersistence.Save(_eventProgress);
                _result.text = GameText.Get("life.event.ui.declined");
            }
        }

        private void BindButtons()
        {
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                var button = _choiceButtons[i];
                var label = _choiceLabels[i];
                button.onClick.RemoveAllListeners();
                if (_event != null && i < _event.Choices.Count && _event.Choices[i] != null)
                {
                    int index = i;
                    var choice = _event.Choices[i];
                    label.text = GameText.Get(choice.DisplayNameKey) + "\n" + GameText.Get(choice.ResultSummaryKey);
                    button.interactable = _studentProgress != null && _eventProgress != null;
                    button.gameObject.SetActive(true);
                    button.onClick.AddListener(() => Choose(index));
                }
                else
                {
                    label.text = string.Empty;
                    button.gameObject.SetActive(false);
                }
            }

            _deferButton.onClick.RemoveAllListeners();
            _deferButton.interactable = _event != null && _event.Kind != null && _event.Kind.IsDeferrable && _eventProgress != null;
            _deferButton.gameObject.SetActive(_event != null && _event.Kind != null && _event.Kind.IsDeferrable);
            _deferButton.onClick.AddListener(Defer);
            _declineButton.onClick.RemoveAllListeners();
            _declineButton.interactable = _event != null && _eventProgress != null;
            _declineButton.onClick.AddListener(Decline);
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("DailyEventRoot", typeof(RectTransform), typeof(ModernUiTileImage), typeof(Canvas), typeof(GraphicRaycaster));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1120f, 800f);
            var modal = _root.GetComponent<Canvas>(); modal.overrideSorting = true; modal.sortingOrder = 1300;
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();

            _title = MakeText(rt, "DailyEventTitle", string.Empty, new Vector2(0f, 320f), new Vector2(880f, 48f), 28, TextAnchor.MiddleCenter);
            _description = MakeText(rt, "DailyEventDescription", string.Empty, new Vector2(0f, 250f), new Vector2(944f, 72f), 20, TextAnchor.UpperCenter);
            _deadline = MakeText(rt, "DailyEventDeadline", string.Empty, new Vector2(0f, 195f), new Vector2(944f, 32f), 18, TextAnchor.MiddleCenter);
            _result = MakeText(rt, "DailyEventResult", string.Empty, new Vector2(0f, -222f), new Vector2(944f, 96f), 20, TextAnchor.UpperCenter);
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                var button = MakeButton(rt, "DailyEventChoiceButton_" + i, new Vector2(0f, 122f - i * 108f), new Vector2(944f, 92f));
                _choiceButtons[i] = button;
                _choiceLabels[i] = button.GetComponentInChildren<Text>(true);
            }
            var close = MakeButton(rt, "DailyEventCloseButton", new Vector2(504f, 342f), new Vector2(48f, 48f));
            var closeLabel = close.GetComponentInChildren<Text>(true);
            closeLabel.text = "×";
            // The 48px button leaves a 16px label box; a glyph wider than that (e.g. Noto Sans CJK) would wrap out of sight.
            closeLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            close.onClick.AddListener(Hide);
            _deferButton = MakeButton(rt, "DailyEventDeferButton", new Vector2(-130f, -326f), new Vector2(210f, 52f));
            _deferButton.GetComponentInChildren<Text>(true).text = GameText.Get("life.event.ui.later");
            _declineButton = MakeButton(rt, "DailyEventDeclineButton", new Vector2(130f, -326f), new Vector2(210f, 52f));
            _declineButton.GetComponentInChildren<Text>(true).text = GameText.Get("life.event.ui.decline");
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
            var skin = go.GetComponent<ModernUiTileImage>(); skin.SetRecipe(ModernUiRecipes.CommonPanel); skin.Rebuild();
            MakeText(rt, "Label", string.Empty, Vector2.zero, size - new Vector2(32f, 12f), 20, TextAnchor.MiddleCenter);
            return go.GetComponent<Button>();
        }

        private static string FormatDeadline(DailyEventDefinition dailyEvent)
        {
            if (dailyEvent == null || dailyEvent.Kind == null) return string.Empty;
            if (dailyEvent.Kind.IsTimed && dailyEvent.Kind.DueTimeMinutes > 0) return GameText.Format("life.event.ui.due", dailyEvent.Kind.DueTimeMinutes);
            if (dailyEvent.Kind.IsDeferrable) return GameText.Get("life.event.ui.deferrable");
            return GameText.Get("life.event.ui.optional");
        }

        private string FormatResult(LifeActivityResult result, DailyEventRecord record)
        {
            if (result.Kind == LifeActivityResultKind.Applied)
                return GameText.Get("life.event.ui.applied") + (string.IsNullOrEmpty(_lastChoiceSummary) ? string.Empty : "\n" + _lastChoiceSummary);
            return GameText.Get(result.Kind == LifeActivityResultKind.DuplicateRequest ? "life.event.ui.duplicate" : "life.event.ui.unavailable");
        }

        private static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Daily Event";
            return key.Replace("daily-event.", string.Empty).Replace("daily-event-choice.", string.Empty).Replace(".name", string.Empty).Replace('-', ' ').Replace('_', ' ');
        }
    }
}


