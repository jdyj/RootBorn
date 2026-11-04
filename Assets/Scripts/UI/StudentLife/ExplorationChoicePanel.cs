using System;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using UnityEngine.InputSystem;
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
        private Text _closeText;
        private Transform _buttonRoot;

        public bool IsOpen => gameObject.activeInHierarchy;
        public ExplorationSummaryModel Model => _model;
        public ExplorationProgress Progress { get; private set; }
        public string ResultText => _resultText != null ? _resultText.text : string.Empty;

        public static ExplorationChoicePanel EnsureInScene(Canvas canvas)
        {
            if (canvas == null) canvas = CreateCanvas();
            if (!canvas.isActiveAndEnabled || !canvas.gameObject.activeInHierarchy) return null;
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
            // Only a direct child belongs to this root surface; nested hidden panels have another owner.
            foreach (Transform child in canvas.transform)
            {
                var existing = child.GetComponent<ExplorationChoicePanel>();
                if (existing != null) return existing;
            }
            var parent = canvas.transform;
            var go = new GameObject("ExplorationChoicePanel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(560f, 360f);
            go.GetComponent<Image>().color = Color.clear;
            go.GetComponent<Image>().raycastTarget = true;
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
            transform.SetAsLastSibling();
            UiGameplayInputLock.Acquire(this);
            Render();
        }

        public void Open(ExplorationSummaryModel model, Action<string> onChoiceSelected)
        {
            Open(model, Progress, onChoiceSelected);
        }

        public void Close()
        {
            _onChoiceSelected = null;
            gameObject.SetActive(false);
            UiGameplayInputLock.Release(this);
        }

        private void OnDisable() => UiGameplayInputLock.Release(this);
        private void OnDestroy() => UiGameplayInputLock.Release(this);
        // Consume dismissal after world shortcut routers observe the modal lease in Update.
        private void LateUpdate()
        {
            if (!IsOpen) return;
            foreach (var device in InputSystem.devices)
                if (device is Keyboard keyboard && keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
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
            _resultText.text = ExplorationTextPresentation.Result(result);
            if (_resultText.preferredHeight > _resultText.rectTransform.rect.height + .5f)
                _resultText.text = ExplorationTextPresentation.Result(result, compact: true);
        }

        private void BuildStaticUi()
        {
            if (_titleText != null) return;
            _titleText = CreateText(transform, "ExplorationTitle", new Vector2(-20f, 140f), new Vector2(420f, 42f), 24, TextAnchor.MiddleCenter);
            var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            close.transform.SetParent(transform, false);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(.5f, .5f);
            closeRect.anchoredPosition = new Vector2(226f, 142f);
            closeRect.sizeDelta = new Vector2(64f, 32f);
            close.GetComponent<ModernUiTileImage>().SetRecipe(ModernUiRecipes.CommonPanel);
            close.GetComponent<Button>().onClick.AddListener(Close);
            _closeText = CreateText(close.transform, "Label", Vector2.zero, new Vector2(60f, 28f), 16, TextAnchor.MiddleCenter);
            _closeText.text = ExplorationTextPresentation.Text("facility.close", null);
            _resultText = CreateText(transform, "ExplorationResultText", new Vector2(0f, -116f), new Vector2(500f, 72f), 16, TextAnchor.UpperLeft);
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
            _titleText.text = ExplorationTextPresentation.Title(_model.DisplayNameKey);
            if (_closeText != null) _closeText.text = ExplorationTextPresentation.Text("facility.close", null);
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
            var go = new GameObject("ChoiceButton_" + choice.ChoiceId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            go.transform.SetParent(_buttonRoot, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * 46f);
            rect.sizeDelta = new Vector2(480f, 38f);
            var image = go.GetComponent<Image>();
            image.color = Color.white;
            go.GetComponent<ModernUiTileImage>().SetRecipe(ModernUiRecipes.CommonPanel);
            var button = go.GetComponent<Button>();
            button.interactable = choice.Available;
            string id = choice.ChoiceId;
            button.onClick.AddListener(() => SelectChoice(id));
            string label = ExplorationTextPresentation.ChoiceLabel(choice);
            CreateText(go.transform, "Label", Vector2.zero, new Vector2(456f, 32f), 15, TextAnchor.MiddleLeft).text = label;
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
            text.font = LifeUiFont.Shared;
            text.raycastTarget = false;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = parent.GetComponentInParent<Button>() != null ? Color.white : new Color(.22f, .19f, .18f);
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
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }
}
