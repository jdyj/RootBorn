using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class CareerCandidatePanel : MonoBehaviour
    {
        private const int InitialCandidateButtons = 6;

        private GameObject _root;
        private Text _title;
        private Text _list;
        private Text _details;
        private ScrollRect _detailsScroll;
        private readonly List<Button> _candidateButtons = new List<Button>();
        private readonly List<Text> _candidateButtonLabels = new List<Text>();
        private RectTransform _candidateContent;
        private ScrollRect _candidateScroll;
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
            _title.text = Ui("ui.career-candidate.title", "진로 후보", "Career Candidates");
            _list.text = FormatList(_summaries);
            BindButtons();
            ShowDetails(_summaries.Length > 0 ? 0 : -1);
            _root.SetActive(true);
            transform.SetAsLastSibling();
            UiGameplayInputLock.Acquire(this);
        }

        public void Hide()
        {
            UiGameplayInputLock.Release(this);
            if (_root != null) _root.SetActive(false);
        }

        private void OnDisable() => UiGameplayInputLock.Release(this);
        private void OnDestroy() => UiGameplayInputLock.Release(this);

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

            _title = MakeText(rt, "Title", Ui("ui.career-candidate.title", "진로 후보", "Career Candidates"), new Vector2(0f, 238f), new Vector2(760f, 38f), 26, TextAnchor.MiddleCenter);
            _list = MakeText(rt, "SummaryList", string.Empty, new Vector2(-285f, 208f), new Vector2(250f, 30f), 18, TextAnchor.UpperLeft);
            var viewportObject = new GameObject("CandidateDetailsViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportObject.transform.SetParent(rt, false);
            var viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = viewport.anchorMax = new Vector2(.5f,.5f);
            viewport.anchoredPosition = new Vector2(165,10); viewport.sizeDelta = new Vector2(470,420);
            viewportObject.GetComponent<Image>().color = Color.clear;
            _detailsScroll = viewportObject.GetComponent<ScrollRect>();
            _detailsScroll.horizontal = false; _detailsScroll.vertical = true; _detailsScroll.inertia = false;
            _detailsScroll.movementType = ScrollRect.MovementType.Clamped; _detailsScroll.viewport = viewport;
            _details = MakeText(viewport, "Details", string.Empty, Vector2.zero, new Vector2(470,420), 18, TextAnchor.UpperLeft);
            var content = _details.rectTransform; content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f,1); content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0,420);
            _details.horizontalOverflow = HorizontalWrapMode.Wrap; _details.verticalOverflow = VerticalWrapMode.Overflow;
            _detailsScroll.content = content;

            var listObject = new GameObject("CandidateListViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            listObject.transform.SetParent(rt, false);
            var listViewport = (RectTransform)listObject.transform;
            listViewport.anchorMin = listViewport.anchorMax = new Vector2(.5f,.5f);
            listViewport.anchoredPosition = new Vector2(-285,-6); listViewport.sizeDelta = new Vector2(250,360);
            listObject.GetComponent<Image>().color = Color.clear;
            _candidateScroll = listObject.GetComponent<ScrollRect>(); _candidateScroll.horizontal = false; _candidateScroll.vertical = true;
            _candidateScroll.inertia = false; _candidateScroll.movementType = ScrollRect.MovementType.Clamped; _candidateScroll.viewport = listViewport;
            var contentObject = new GameObject("CandidateButtonsContent", typeof(RectTransform)); contentObject.transform.SetParent(listViewport, false);
            _candidateContent = (RectTransform)contentObject.transform; _candidateContent.anchorMin = new Vector2(0,1); _candidateContent.anchorMax = Vector2.one;
            _candidateContent.pivot = new Vector2(.5f,1); _candidateContent.sizeDelta = new Vector2(0,360); _candidateContent.anchoredPosition = Vector2.zero;
            _candidateScroll.content = _candidateContent;
            EnsureCandidateCapacity(InitialCandidateButtons);

            var close = MakeButton(rt, "CloseButton", new Vector2(370f, -238f), new Vector2(120f, 52f));
            close.GetComponentInChildren<Text>(true).text = Ui("ui.close", "닫기", "Close");
            close.onClick.AddListener(Hide);
        }

        private void EnsureCandidateCapacity(int count)
        {
            while (_candidateButtons.Count < count)
            {
                int index = _candidateButtons.Count;
                var button = MakeButton(_candidateContent, "CandidateButton_" + index, Vector2.zero, new Vector2(250,52));
                var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f,1);
                rect.anchoredPosition = new Vector2(0,-30-index*60);
                _candidateButtons.Add(button); _candidateButtonLabels.Add(button.GetComponentInChildren<Text>(true));
            }
        }

        private void BindButtons()
        {
            EnsureCandidateCapacity(_summaries.Length);
            _candidateContent.sizeDelta = new Vector2(0, Mathf.Max(360,_summaries.Length*60));
            _candidateContent.anchoredPosition = Vector2.zero; _candidateScroll.verticalNormalizedPosition = 1;
            for (int i = 0; i < _candidateButtons.Count; i++)
            {
                var button = _candidateButtons[i];
                var label = _candidateButtonLabels[i];
                button.onClick.RemoveAllListeners();
                if (i < _summaries.Length)
                {
                    int index = i;
                    label.text = GameText.Get(_summaries[i].DisplayName) + "\n" + GameText.Get(_summaries[i].State.ToString());
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
                _details.text = Ui("ui.career-candidate.empty", "아직 발견한 진로가 없어요.", "No career candidates");
                LayoutDetails();
                return;
            }

            var summary = _summaries[index];
            string text = GameText.Get(summary.DisplayName) + "\n" + Ui("ui.career-candidate.state", "상태", "State") + ": " + GameText.Get(summary.State.ToString());
            text += "\n" + string.Format(Ui("ui.career-candidate.stats", "단서 {0}개 · 이해도 {1}", "Hints: {0} · Understanding: {1}"), summary.HintCount, summary.UnderstandingScore);
            if(summary.State == CareerCandidateState.Locked) text += "\n" + Ui("ui.career-candidate.discover", "관련 활동을 경험하고 단서를 모아 이 진로를 알아가 보세요.", "Explore related activities and collect hints to discover this career.");
            else if (!string.IsNullOrEmpty(summary.Description)) text += "\n" + GameText.Get(summary.Description);
            text += FormatLines("\n" + Ui("ui.career-candidate.locations", "관련 장소", "Related Locations"), summary.RelatedLocationKeys, Ui("ui.career-candidate.no-locations", "아직 기록된 장소가 없어요.", "No locations yet"));
            text += FormatLines("\n" + Ui("ui.career-candidate.activities", "관련 활동", "Related Activities"), summary.RelatedActivityKeys, Ui("ui.career-candidate.no-activities", "아직 기록된 활동이 없어요.", "No activities yet"));
            text += FormatLines("\n" + Ui("ui.career-candidate.recommended", "다음에 해볼 일", "Recommended"), summary.RecommendedActionKeys, Ui("ui.career-candidate.explore", "자유롭게 마을을 둘러보세요.", "Explore freely"));
            _details.text = text; LayoutDetails();
        }
        private void LayoutDetails()
        {
            _details.rectTransform.sizeDelta = new Vector2(0, Mathf.Max(420, _details.preferredHeight + 16));
            _details.rectTransform.anchoredPosition = Vector2.zero; _detailsScroll.verticalNormalizedPosition = 1;
        }
        private static string FormatList(CareerCandidateSummary[] summaries)
        {
            int count = summaries != null ? summaries.Length : 0;
            return string.Format(Ui("ui.career-candidate.count", "진로 후보 {0}개", "{0} career candidates"), count);
        }
        private static string FormatLines(string title, string[] values, string empty)
        {
            string text = title;
            if (values == null || values.Length == 0) return text + "\n" + empty;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) text += "\n" + GameText.Get(values[i]);
            return text;
        }
        private static string Ui(string key, string ko, string en)
        {
            var text=GameText.Get(key);return text==key?(GameText.Language=="ko"?ko:en):text;
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
            go.GetComponent<Image>().color = Color.clear;
            go.GetComponent<ModernUiTileImage>().SetRecipe(ModernUiRecipes.CommonPanel);
            MakeText(rt, "Label", string.Empty, Vector2.zero, size, 16, TextAnchor.MiddleCenter);
            return go.GetComponent<Button>();
        }
    }
}
