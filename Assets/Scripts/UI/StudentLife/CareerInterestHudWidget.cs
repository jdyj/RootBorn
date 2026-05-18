using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class CareerInterestHudWidget : MonoBehaviour
    {
        private GameObject _root;
        private Text _title;
        private Text _interest;
        private Text _recommendations;

        public string VisibleText => ((_title != null ? _title.text : string.Empty) + "\n" + (_interest != null ? _interest.text : string.Empty) + "\n" + (_recommendations != null ? _recommendations.text : string.Empty)).Trim();

        public static CareerInterestHudWidget EnsureInScene(Canvas canvas)
        {
            var existing = Object.FindFirstObjectByType<CareerInterestHudWidget>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("CareerInterestHudWidget");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<CareerInterestHudWidget>();
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        public void Refresh(CareerInterestDefinition[] interests, CareerInterestProgress interestProgress, CareerCandidateProgress candidateProgress, StudentLifeProgress studentProgress, int day)
        {
            BuildIfNeeded();
            _title.text = "Career Interest";
            var current = FindCurrent(interests, interestProgress);
            if (current == null)
            {
                _interest.text = "No selected interest";
                _recommendations.text = "Next\nOpen career candidates";
                _root.SetActive(true);
                return;
            }

            _interest.text = current.DisplayNameKey + "\n" + current.Id;
            var recommendations = current.BuildRecommendations(interestProgress, candidateProgress, studentProgress, day);
            _recommendations.text = FormatRecommendations(recommendations);
            _root.SetActive(true);
        }

        public void Hide()
        {
            BuildIfNeeded();
            _root.SetActive(false);
        }

        private static CareerInterestDefinition FindCurrent(CareerInterestDefinition[] interests, CareerInterestProgress progress)
        {
            if (interests == null || progress == null || string.IsNullOrEmpty(progress.CurrentInterestId)) return null;
            for (int i = 0; i < interests.Length; i++)
            {
                var interest = interests[i];
                if (interest != null && interest.Id == progress.CurrentInterestId) return interest;
            }
            return null;
        }

        private static string FormatRecommendations(string[] recommendations)
        {
            string text = "Next";
            int added = 0;
            if (recommendations != null)
            {
                for (int i = 0; i < recommendations.Length && added < 3; i++)
                {
                    if (string.IsNullOrEmpty(recommendations[i])) continue;
                    text += "\n" + recommendations[i];
                    added++;
                }
            }
            return added > 0 ? text : text + "\nChoose a town activity";
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("CareerInterestHudRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(470f, -24f);
            rt.sizeDelta = new Vector2(390f, 150f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();
            _title = MakeText(rt, "Title", "Career Interest", new Vector2(18f, -18f), new Vector2(350f, 24f), 18, TextAnchor.UpperLeft);
            _interest = MakeText(rt, "Interest", string.Empty, new Vector2(18f, -48f), new Vector2(350f, 42f), 14, TextAnchor.UpperLeft);
            _recommendations = MakeText(rt, "Recommendations", string.Empty, new Vector2(18f, -92f), new Vector2(350f, 46f), 14, TextAnchor.UpperLeft);
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
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
    }
}
