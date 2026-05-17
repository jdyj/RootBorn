using Rootborn.Game.Housing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Housing
{
    public sealed class HouseConstructionOverlay : MonoBehaviour
    {
        private HouseConstructionBlueprintDefinition _blueprint;
        private HouseConstructionSession _session;
        private HouseUpgradeStageDefinition _stage;
        private string _saveSlot;
        private Text _progressText;
        private Button _completeButton;
        private Button _cancelButton;

        public string ProgressTextForTests => _progressText != null ? _progressText.text : string.Empty;
        public bool CompleteButtonInteractableForTests => _completeButton != null && _completeButton.interactable;

        public static HouseConstructionOverlay EnsureForTests(HouseConstructionBlueprintDefinition blueprint)
        {
            var overlay = FindFirstObjectByType<HouseConstructionOverlay>(FindObjectsInactive.Include) ?? Create();
            overlay.Bind(blueprint);
            return overlay;
        }

        public void Bind(HouseConstructionBlueprintDefinition blueprint)
        {
            if (_progressText == null)
            {
                Build();
            }

            _blueprint = blueprint;
            _session = new HouseConstructionSession(blueprint);
            gameObject.SetActive(true);
            Refresh();
        }

        public void BindCompletionForTests(string saveSlot, HouseUpgradeStageDefinition stage)
        {
            _saveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            _stage = stage;
            if (_stage != null && _stage.Blueprint != null)
            {
                Bind(_stage.Blueprint);
            }
        }

        public void CompleteForTests()
        {
            Complete();
        }

        public bool TryPlaceForTests(Vector2Int cell, HouseConstructionCellKind kind)
        {
            bool placed = _session != null && _session.TryPlace(cell, kind);
            Refresh();
            return placed;
        }

        private static HouseConstructionOverlay Create()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("HouseConstructionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasGo, SceneManager.GetActiveScene());
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            var go = new GameObject("HouseConstructionOverlay", typeof(RectTransform), typeof(Image), typeof(HouseConstructionOverlay));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -24f);
            rect.sizeDelta = new Vector2(420f, 96f);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            return go.GetComponent<HouseConstructionOverlay>();
        }

        private void Build()
        {
            _progressText = MakeText("ConstructionProgressText", new Vector2(0f, 18f), new Vector2(380f, 36f));
            _completeButton = MakeButton("CompleteConstructionButton", "Complete", new Vector2(-90f, -26f));
            _cancelButton = MakeButton("CancelConstructionButton", "Cancel", new Vector2(90f, -26f));
            _completeButton.onClick.AddListener(Complete);
            _cancelButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        private void Complete()
        {
            if (_stage == null || _session == null)
            {
                return;
            }

            var state = HouseStatePersistence.Load(_saveSlot);
            var wallet = new HouseCurrencyWallet(state.Currency != null ? state.Currency.Balance : 0);
            var service = new HouseUpgradeService(new[] { _stage });
            var result = service.TryCompleteDirect(_stage, state, wallet, _session);
            if (result.Kind != HouseUpgradeResultKind.Applied)
            {
                Refresh();
                return;
            }

            state.Currency.Balance = wallet.Balance;
            HouseStatePersistence.Save(_saveSlot, state);
            gameObject.SetActive(false);
        }

        private void Refresh()
        {
            int placed = _session != null ? _session.ToSaveData().Length : 0;
            int required = _blueprint != null ? _blueprint.RequiredCells.Count : 0;
            _progressText.text = placed + "/" + required;
            _completeButton.interactable = _session != null && _session.IsComplete;
        }

        private Text MakeText(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private Button MakeButton(string name, string label, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(150f, 34f);
            go.GetComponent<Image>().color = Color.white;
            var text = MakeText("Text", Vector2.zero, rect.sizeDelta);
            text.transform.SetParent(go.transform, false);
            text.text = label;
            text.color = Color.black;
            return go.GetComponent<Button>();
        }
    }
}