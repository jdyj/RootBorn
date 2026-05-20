using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class DailyPlanPanel : MonoBehaviour
    {
        private RectTransform _rowRoot;
        private Text _emptyText;
        private Button[] _routeButtons = System.Array.Empty<Button>();

        public string EmptyStateTextForTests { get; private set; } = string.Empty;
        public int RouteRowCountForTests { get; private set; }
        public bool UsesBoundedRouteListForTests { get; private set; }

        public void Bind(
            DailyPlanRouteDefinition[] routes,
            DailyPlanProgressComponent progressComponent,
            StudentLifeProgressComponent studentProgressComponent)
        {
            EnsureLayout();
            ClearRows();

            RouteRowCountForTests = routes == null ? 0 : routes.Length;
            EmptyStateTextForTests = RouteRowCountForTests == 0 ? "objective.daily.choose.empty" : string.Empty;
            UsesBoundedRouteListForTests = true;
            _emptyText.text = EmptyStateTextForTests;
            _emptyText.gameObject.SetActive(RouteRowCountForTests == 0);

            if (routes == null || routes.Length == 0)
            {
                _routeButtons = System.Array.Empty<Button>();
                return;
            }

            _routeButtons = new Button[routes.Length];
            for (int i = 0; i < routes.Length; i++)
            {
                var route = routes[i];
                if (route == null)
                {
                    continue;
                }

                var button = CreateRouteButton(route.DisplayNameKey);
                _routeButtons[i] = button;
                var selectedRoute = route;
                button.onClick.AddListener(() =>
                {
                    if (progressComponent != null && studentProgressComponent != null && progressComponent.TrySelect(selectedRoute, studentProgressComponent.Progress))
                    {
                        gameObject.SetActive(false);
                    }
                });
            }
        }

        public Button GetRouteButtonForTests(int index)
        {
            return index >= 0 && index < _routeButtons.Length ? _routeButtons[index] : null;
        }

        private void EnsureLayout()
        {
            var root = GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            root.sizeDelta = new Vector2(640f, 420f);

            var chrome = GetComponent<ModernUiTileImage>();
            if (chrome == null) chrome = gameObject.AddComponent<ModernUiTileImage>();
            chrome.SetRecipe(ModernUiRecipes.CommonPanel);

            if (_rowRoot != null && _emptyText != null)
            {
                return;
            }

            var viewportGo = new GameObject("RouteViewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(transform, false);
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(24f, 24f);
            viewport.offsetMax = new Vector2(-24f, -24f);
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var rowsGo = new GameObject("RouteRows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rowsGo.transform.SetParent(viewportGo.transform, false);
            _rowRoot = rowsGo.GetComponent<RectTransform>();
            _rowRoot.anchorMin = new Vector2(0f, 1f);
            _rowRoot.anchorMax = new Vector2(1f, 1f);
            _rowRoot.pivot = new Vector2(0.5f, 1f);
            _rowRoot.offsetMin = Vector2.zero;
            _rowRoot.offsetMax = Vector2.zero;
            var layout = rowsGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            rowsGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = GetComponent<ScrollRect>();
            if (scroll == null) scroll = gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = _rowRoot;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var emptyGo = new GameObject("EmptyState", typeof(RectTransform), typeof(Text));
            emptyGo.transform.SetParent(transform, false);
            var emptyRect = emptyGo.GetComponent<RectTransform>();
            emptyRect.anchorMin = Vector2.zero;
            emptyRect.anchorMax = Vector2.one;
            emptyRect.offsetMin = new Vector2(32f, 32f);
            emptyRect.offsetMax = new Vector2(-32f, -32f);
            _emptyText = emptyGo.GetComponent<Text>();
            _emptyText.alignment = TextAnchor.MiddleCenter;
            _emptyText.raycastTarget = false;
        }

        private Button CreateRouteButton(string label)
        {
            var buttonGo = new GameObject("DailyPlanRouteRow", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage), typeof(LayoutElement));
            buttonGo.transform.SetParent(_rowRoot, false);
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(560f, 52f);
            buttonGo.GetComponent<LayoutElement>().preferredHeight = 52f;
            var tile = buttonGo.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(buttonGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 4f);
            textRect.offsetMax = new Vector2(-16f, -4f);
            var text = textGo.GetComponent<Text>();
            text.text = string.IsNullOrEmpty(label) ? "daily.route" : label;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;

            return buttonGo.GetComponent<Button>();
        }

        private void ClearRows()
        {
            if (_rowRoot == null)
            {
                return;
            }

            for (int i = _rowRoot.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_rowRoot.GetChild(i).gameObject);
            }
        }
    }
}
