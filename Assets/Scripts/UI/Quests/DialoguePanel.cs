using System;
using Rootborn.Game.Dialogue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rootborn.UI.Quests
{
    [DisallowMultipleComponent]
    public sealed class DialoguePanel : MonoBehaviour
    {
        private DialogueDefinition _dialogue;
        private DialogueChoiceContext _context;
        private RectTransform _choiceRoot;
        private Text _lineText;
        private bool _wasKeyboardChoicePressed;

        public bool IsOpen { get; private set; }
        public event Action<bool> OnChoiceExecuted;

        public void Open(DialogueDefinition dialogue, DialogueChoiceContext context)
        {
            _dialogue = dialogue;
            _context = context;
            IsOpen = true;
            _wasKeyboardChoicePressed = IsKeyboardChoicePressed();
            gameObject.SetActive(true);
            EnsureLineText();
            EnsureCloseButton();
            RebuildLineText();
            RebuildChoiceButtons();
        }

        public void Close()
        {
            IsOpen = false;
            _dialogue = null;
            _wasKeyboardChoicePressed = false;
            ClearChoiceButtons();
            gameObject.SetActive(false);
        }

        public bool Choose(int choiceIndex)
        {
            if (_dialogue == null || _dialogue.Choices == null)
            {
                OnChoiceExecuted?.Invoke(false);
                return false;
            }

            if (choiceIndex < 0 || choiceIndex >= _dialogue.Choices.Length)
            {
                OnChoiceExecuted?.Invoke(false);
                return false;
            }

            var choice = _dialogue.Choices[choiceIndex];
            bool result = choice != null && choice.TryExecute(in _context);
            OnChoiceExecuted?.Invoke(result);
            if (result && choice != null && choice.QuestAction != DialogueQuestAction.None)
            {
                Close();
                return true;
            }

            RebuildChoiceButtons();
            return result;
        }

        private void Update()
        {
            bool pressed = IsKeyboardChoicePressed();
            if (IsOpen && pressed && !_wasKeyboardChoicePressed)
            {
                ChooseFirstAvailableChoiceFromKeyboard();
            }

            _wasKeyboardChoicePressed = pressed;
        }

        private static bool IsKeyboardChoicePressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null
                && (keyboard.eKey.isPressed
                    || keyboard.spaceKey.isPressed
                    || keyboard.enterKey.isPressed
                    || keyboard.numpadEnterKey.isPressed);
        }

        private void ChooseFirstAvailableChoiceFromKeyboard()
        {
            if (_dialogue == null || _dialogue.Choices == null)
            {
                return;
            }

            for (int choiceIndex = 0; choiceIndex < _dialogue.Choices.Length; choiceIndex++)
            {
                var choice = _dialogue.Choices[choiceIndex];
                if (choice != null && choice.IsAvailable(in _context))
                {
                    Choose(choiceIndex);
                    return;
                }
            }
        }

        private void RebuildLineText()
        {
            if (_lineText == null)
            {
                return;
            }

            var lines = _dialogue != null ? _dialogue.LineKeys : null;
            if (lines == null || lines.Length == 0)
            {
                _lineText.text = string.Empty;
                return;
            }

            string text = string.Empty;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                {
                    text = string.IsNullOrEmpty(text) ? lines[i] : text + "\n" + lines[i];
                }
            }

            _lineText.text = text;
        }

        private void RebuildChoiceButtons()
        {
            ClearChoiceButtons();
            var choices = _dialogue != null ? _dialogue.Choices : null;
            if (choices == null || choices.Length == 0)
            {
                return;
            }

            EnsureChoiceRoot();
            int visibleIndex = 0;
            for (int i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                if (choice == null || !choice.IsAvailable(in _context))
                {
                    continue;
                }

                CreateChoiceButton(i, visibleIndex, choice);
                visibleIndex++;
            }
        }

        private void EnsureLineText()
        {
            if (_lineText != null)
            {
                return;
            }

            var existing = transform.Find("Lines");
            if (existing != null && existing.TryGetComponent(out _lineText))
            {
                return;
            }

            var lineGo = new GameObject("Lines", typeof(RectTransform), typeof(Text));
            lineGo.transform.SetParent(transform, false);
            var rect = (RectTransform)lineGo.transform;
            rect.anchorMin = new Vector2(0f, 0.45f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(24f, 0f);
            rect.offsetMax = new Vector2(-56f, -42f);

            _lineText = lineGo.GetComponent<Text>();
            _lineText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _lineText.fontSize = 20;
            _lineText.alignment = TextAnchor.UpperLeft;
            _lineText.color = new Color(0.96f, 0.93f, 0.84f, 1f);
            _lineText.raycastTarget = false;
        }

        private void EnsureChoiceRoot()
        {
            if (_choiceRoot != null)
            {
                return;
            }

            var existing = transform.Find("ChoiceButtons");
            if (existing != null)
            {
                _choiceRoot = (RectTransform)existing;
                return;
            }

            var rootGo = new GameObject("ChoiceButtons", typeof(RectTransform));
            rootGo.transform.SetParent(transform, false);
            _choiceRoot = (RectTransform)rootGo.transform;
            _choiceRoot.anchorMin = new Vector2(0f, 0f);
            _choiceRoot.anchorMax = new Vector2(1f, 0f);
            _choiceRoot.pivot = new Vector2(0.5f, 0f);
            _choiceRoot.anchoredPosition = new Vector2(0f, 20f);
            _choiceRoot.sizeDelta = new Vector2(-40f, 92f);
        }

        private void EnsureCloseButton()
        {
            var existing = transform.Find("CloseButton");
            if (existing != null)
            {
                var existingButton = existing.GetComponent<Button>();
                if (existingButton != null)
                {
                    existingButton.onClick.RemoveListener(Close);
                    existingButton.onClick.AddListener(Close);
                }
                return;
            }

            var buttonGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(transform, false);
            var rect = (RectTransform)buttonGo.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-14f, -14f);
            rect.sizeDelta = new Vector2(34f, 34f);

            var image = buttonGo.GetComponent<Image>();
            image.color = new Color(0.16f, 0.20f, 0.24f, 0.95f);
            image.raycastTarget = true;

            var button = buttonGo.GetComponent<Button>();
            button.onClick.AddListener(Close);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGo.GetComponent<Text>();
            label.text = "X";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.96f, 0.93f, 0.84f, 1f);
        }

        private void CreateChoiceButton(int choiceIndex, int visibleIndex, DialogueChoiceDefinition choice)
        {
            var buttonGo = new GameObject("ChoiceButton_" + choiceIndex, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(_choiceRoot, false);
            var rect = (RectTransform)buttonGo.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -visibleIndex * 42f);
            rect.sizeDelta = new Vector2(0f, 36f);

            var image = buttonGo.GetComponent<Image>();
            image.color = new Color(0.18f, 0.24f, 0.30f, 0.92f);

            int capturedIndex = choiceIndex;
            var button = buttonGo.GetComponent<Button>();
            button.onClick.AddListener(() => Choose(capturedIndex));

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);

            var label = labelGo.GetComponent<Text>();
            label.text = string.IsNullOrEmpty(choice.LabelKey) ? choice.QuestAction.ToString() : choice.LabelKey;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.96f, 0.93f, 0.84f, 1f);
        }

        private void ClearChoiceButtons()
        {
            if (_choiceRoot == null)
            {
                var existing = transform.Find("ChoiceButtons");
                if (existing == null)
                {
                    return;
                }

                _choiceRoot = (RectTransform)existing;
            }

            for (int i = _choiceRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_choiceRoot.GetChild(i).gameObject);
            }
        }
    }
}
