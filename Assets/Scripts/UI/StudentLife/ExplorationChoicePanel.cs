using System;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class ExplorationChoicePanel : MonoBehaviour
    {
        private ExplorationSummaryModel _model;
        private Action<string> _onChoiceSelected;
        private Text _titleText;
        private Text _resultText;
        private Transform _buttonRoot;

        public bool IsOpen => gameObject.activeSelf;
        public ExplorationSummaryModel Model => _model;
        public ExplorationProgress Progress { get; private set; }
        public string ResultText => _resultText != null ? _resultText.text : string.Empty;

        public static ExplorationChoicePanel EnsureInScene(Canvas canvas)
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<ExplorationChoicePanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var parent = canvas != null ? canvas.transform : CreateCanvas().transform;
            var go = new GameObject("ExplorationChoicePanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(560f, 360f);
            var image = go.AddComponent<ModernUiTileImage>();
            image.SetRecipe(ModernUiRecipes.CommonPanel);
            var panel = go.AddComponent<ExplorationChoicePanel>();
            panel.BuildStaticUi();
            go.SetActive(false);
            return panel;
        }

        public void Open(ExplorationSummaryModel model, ExplorationProgress progress, Action<string> onChoiceSelected)
        {
            _model = model;
            Progress = progress;
            _onChoiceSelected = onChoiceSelected;
            gameObject.SetActive(true);
            Render();
        }

        public void Open(ExplorationSummaryModel model, Action<string> onChoiceSelected)
        {
            Open(model, Progress, onChoiceSelected);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public bool SelectChoice(string choiceId)
        {
            if (string.IsNullOrEmpty(choiceId) || _onChoiceSelected == null) return false;
            _onChoiceSelected(choiceId);
            return true;
        }

        public void ShowResult(ExplorationChoiceResult result)
        {
            if (_resultText == null) BuildStaticUi();
            _resultText.text = BuildResultText(result);
        }

        private void BuildStaticUi()
        {
            if (_titleText != null) return;
            _titleText = CreateText(transform, "ExplorationTitle", new Vector2(0f, 140f), new Vector2(500f, 42f), 24, TextAnchor.MiddleCenter);
            _resultText = CreateText(transform, "ExplorationResultText", new Vector2(0f, -135f), new Vector2(500f, 72f), 16, TextAnchor.UpperLeft);
            var root = new GameObject("ExplorationChoiceButtonRoot", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, 20f);
            rootRect.sizeDelta = new Vector2(500f, 190f);
            _buttonRoot = root.transform;
        }

        private void Render()
        {
            BuildStaticUi();
            _titleText.text = _model.DisplayNameKey;
            _resultText.text = string.Empty;
            ClearChildren(_buttonRoot);
            for (int i = 0; i < _model.Choices.Length; i++)
            {
                var choice = _model.Choices[i];
                CreateChoiceButton(choice, i);
            }
        }

        private void CreateChoiceButton(ExplorationChoiceSummaryModel choice, int index)
        {
            var go = new GameObject("ChoiceButton_" + choice.ChoiceId, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_buttonRoot, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * 46f);
            rect.sizeDelta = new Vector2(480f, 38f);
            var image = go.GetComponent<Image>();
            image.color = choice.Available ? new Color(0.15f, 0.19f, 0.16f, 0.95f) : new Color(0.18f, 0.18f, 0.18f, 0.65f);
            var button = go.GetComponent<Button>();
            button.interactable = choice.Available;
            string id = choice.ChoiceId;
            button.onClick.AddListener(() => SelectChoice(id));
            string label = choice.Available ? choice.DisplayNameKey + " - " + choice.PreviewHintKey : choice.DisplayNameKey + " - " + choice.LockedReasonKey;
            CreateText(go.transform, "Label", Vector2.zero, new Vector2(456f, 32f), 15, TextAnchor.MiddleLeft).text = label;
        }

        private static string BuildResultText(ExplorationChoiceResult result)
        {
            string text = result.Kind.ToString();
            Append(ref text, result.ItemGrantIds);
            Append(ref text, result.ItemSpendIds);
            Append(ref text, result.ClueIds);
            Append(ref text, result.EncyclopediaEntryIds);
            Append(ref text, result.CareerHintIds);
            Append(ref text, result.FollowUpQuestIds);
            Append(ref text, result.FatigueStatusIds);
            Append(ref text, result.OutcomeIds);
            return text;
        }

        private static void Append(ref string text, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) text += "\n" + values[i];
        }

        private static Text CreateText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("ExplorationChoiceCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
        }
    }
}
