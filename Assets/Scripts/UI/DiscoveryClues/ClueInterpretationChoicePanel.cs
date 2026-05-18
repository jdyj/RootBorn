using System;
using Rootborn.Game.DiscoveryClues;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.DiscoveryClues
{
    public sealed class ClueInterpretationChoicePanel : MonoBehaviour
    {
        private GameObject _root;
        private RectTransform _buttonsRoot;
        private Text _title;
        private Action<ClueInterpretationSummaryModel> _onSelected;
        private ClueInterpretationSummaryModel[] _models = Array.Empty<ClueInterpretationSummaryModel>();

        public int ButtonCountForTests => _buttonsRoot != null ? _buttonsRoot.childCount : 0;
        public string TextForTests
        {
            get
            {
                if (_buttonsRoot == null) return string.Empty;
                string text = _title != null ? _title.text + "\n" : string.Empty;
                for (int i = 0; i < _buttonsRoot.childCount; i++)
                {
                    var label = _buttonsRoot.GetChild(i).GetComponentInChildren<Text>(true);
                    if (label != null) text += label.text + "\n";
                }

                return text;
            }
        }

        public static ClueInterpretationChoicePanel EnsureInScene(Canvas canvas)
        {
            var root = canvas != null ? canvas.transform.Find("ClueInterpretationChoicePanel") : null;
            if (root != null)
            {
                var panel = root.GetComponent<ClueInterpretationChoicePanel>();
                if (panel != null) return panel;
            }

            var go = new GameObject("ClueInterpretationChoicePanel");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<ClueInterpretationChoicePanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Show(ClueInterpretationSummaryModel[] summaries, Action<ClueInterpretationSummaryModel> onSelected)
        {
            BuildIfNeeded();
            ClearButtons();
            _models = summaries ?? Array.Empty<ClueInterpretationSummaryModel>();
            _onSelected = onSelected;
            for (int i = 0; i < _models.Length; i++) MakeButton(i, _models[i]);
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public Button GetButtonForTests(int index)
        {
            if (_buttonsRoot == null || index < 0 || index >= _buttonsRoot.childCount) return null;
            return _buttonsRoot.GetChild(index).GetComponent<Button>();
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("ClueInterpretationChoiceRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(460f, 420f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();
            _title = MakeText(rt, "Title", "Interpret Clue", new Vector2(0f, 178f), new Vector2(360f, 30f), 20, TextAnchor.MiddleCenter);
            var buttons = new GameObject("Buttons", typeof(RectTransform));
            buttons.transform.SetParent(rt, false);
            _buttonsRoot = (RectTransform)buttons.transform;
            _buttonsRoot.anchorMin = new Vector2(0.5f, 1f);
            _buttonsRoot.anchorMax = new Vector2(0.5f, 1f);
            _buttonsRoot.pivot = new Vector2(0.5f, 1f);
            _buttonsRoot.anchoredPosition = new Vector2(0f, -58f);
            _buttonsRoot.sizeDelta = new Vector2(380f, 320f);
        }

        private void MakeButton(int index, ClueInterpretationSummaryModel model)
        {
            var go = new GameObject("ClueInterpretationButton_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_buttonsRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -index * 72f);
            rt.sizeDelta = new Vector2(368f, 62f);
            var image = go.GetComponent<Image>();
            image.color = model.Available ? new Color(0.84f, 0.76f, 0.58f, 1f) : new Color(0.55f, 0.50f, 0.43f, 1f);
            var button = go.GetComponent<Button>();
            button.interactable = model.Available;
            int captured = index;
            button.onClick.AddListener(() => Select(captured));
            string status = model.Completed ? "Completed" : model.Available ? "Available" : model.LockedReason;
            MakeText(rt, "Label", Humanize(model.DisplayName) + "\n" + model.SourceKind + " " + Humanize(model.MethodText) + " " + status, Vector2.zero, new Vector2(330f, 48f), 13, TextAnchor.MiddleLeft);
        }

        private void Select(int index)
        {
            if (index < 0 || index >= _models.Length || !_models[index].Available) return;
            _onSelected?.Invoke(_models[index]);
        }

        private void ClearButtons()
        {
            if (_buttonsRoot == null) return;
            for (int i = _buttonsRoot.childCount - 1; i >= 0; i--) DestroyImmediate(_buttonsRoot.GetChild(i).gameObject);
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

        private static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            int dot = key.LastIndexOf('.');
            return dot >= 0 && dot + 1 < key.Length ? key.Substring(dot + 1) : key;
        }
    }
}
