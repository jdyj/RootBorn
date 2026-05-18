using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class CareerCandidatePanel : MonoBehaviour
    {
        private const int MaxCandidateButtons = 6;

        private GameObject _root;
        private Text _title;
        private Text _list;
        private Text _details;
        private readonly Button[] _candidateButtons = new Button[MaxCandidateButtons];
        private readonly Text[] _candidateButtonLabels = new Text[MaxCandidateButtons];
        private CareerCandidateSummary[] _summaries = System.Array.Empty<CareerCandidateSummary>();

        public bool IsOpen => _root != null && _root.activeSelf;

        public static CareerCandidatePanel EnsureInScene(Canvas canvas)
        {
            var existing = Object.FindFirstObjectByType<CareerCandidatePanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("CareerCandidatePanel");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<CareerCandidatePanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Show(CareerCandidateDefinition[] candidates, CareerCandidateProgress progress, StudentLifeProgress studentProgress)
        {
            BuildIfNeeded();
            _summaries = CareerCandidateSummaryBuilder.BuildAll(candidates, progress, studentProgress);
            _title.text = "Career Candidates";
            _list.text = FormatList(_summaries);
            BindButtons();
            ShowDetails(_summaries.Length > 0 ? 0 : -1);
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("CareerCandidateRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(900f, 560f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();

            _title = MakeText(rt, "Title", "Career Candidates", new Vector2(0f, 238f), new Vector2(760f, 38f), 26, TextAnchor.MiddleCenter);
            _list = MakeText(rt, "SummaryList", string.Empty, new Vector2(-285f, 80f), new Vector2(250f, 275f), 14, TextAnchor.UpperLeft);
            _details = MakeText(rt, "Details", string.Empty, new Vector2(165f, 10f), new Vector2(470f, 420f), 15, TextAnchor.UpperLeft);

            for (int i = 0; i < _candidateButtons.Length; i++)
            {
                var button = MakeButton(rt, "CandidateButton_" + i, new Vector2(-285f, -102f - i * 48f), new Vector2(250f, 40f));
                _candidateButtons[i] = button;
                _candidateButtonLabels[i] = button.GetComponentInChildren<Text>(true);
            }

            var close = MakeButton(rt, "CloseButton", new Vector2(370f, -238f), new Vector2(120f, 38f));
            close.GetComponentInChildren<Text>(true).text = "Close";
            close.onClick.AddListener(Hide);
        }

        private void BindButtons()
        {
            for (int i = 0; i < _candidateButtons.Length; i++)
            {
                var button = _candidateButtons[i];
                var label = _candidateButtonLabels[i];
                button.onClick.RemoveAllListeners();
                if (i < _summaries.Length)
                {
                    int index = i;
                    label.text = _summaries[i].DisplayName + " " + _summaries[i].State;
                    button.gameObject.SetActive(true);
                    button.onClick.AddListener(() => ShowDetails(index));
                }
                else
                {
                    label.text = string.Empty;
                    button.gameObject.SetActive(false);
                }
            }
        }

        private void ShowDetails(int index)
        {
            if (index < 0 || index >= _summaries.Length)
            {
                _details.text = "No career candidates";
                return;
            }

            var summary = _summaries[index];
            string text = summary.DisplayName + "\nState: " + summary.State + "\nHints: " + summary.HintCount + " Score: " + summary.UnderstandingScore;
            if (!string.IsNullOrEmpty(summary.Description)) text += "\n" + summary.Description;
            text += FormatLines("\nHint IDs", summary.HintIds, "No hints yet");
            text += FormatLines("\nRelated Locations", summary.RelatedLocationKeys, "No locations yet");
            text += FormatLines("\nRelated Activities", summary.RelatedActivityKeys, "No activities yet");
            text += FormatLines("\nRequirements", summary.RequirementLines, "No requirements yet");
            text += FormatLines("\nRecommended", summary.RecommendedActionKeys, "Explore freely");
            _details.text = text;
        }

        private static string FormatList(CareerCandidateSummary[] summaries)
        {
            string text = "Candidates";
            if (summaries == null || summaries.Length == 0) return text + "\nNo candidates";
            for (int i = 0; i < summaries.Length; i++) text += "\n" + summaries[i].DisplayName + " " + summaries[i].State + " " + summaries[i].HintCount;
            return text;
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
