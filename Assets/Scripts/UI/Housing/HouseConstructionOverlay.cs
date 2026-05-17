using System.Collections.Generic;
using Rootborn.Game.Housing;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.UI.Housing
{
    public sealed class HouseConstructionOverlay : MonoBehaviour
    {
        private const string DefaultStageAssetPath = "Assets/Data/Housing/UpgradeStages/HouseStage_ExpandedRoom_01.asset";

        private HouseConstructionBlueprintDefinition _blueprint;
        private HouseConstructionSession _session;
        private HouseUpgradeStageDefinition _stage;
        private string _saveSlot;
        private Text _progressText;
        private Text _feedbackText;
        private Button _completeButton;
        private Button _cancelButton;
        private Tilemap _placementTilemap;
        private Transform _markerRoot;
        private int _validMarkerCount;
        private int _placedMarkerCount;
        private bool _leftMouseWasPressed;

        public string ProgressTextForTests => _progressText != null ? _progressText.text : string.Empty;
        public bool CompleteButtonInteractableForTests => _completeButton != null && _completeButton.interactable;
        public int ValidMarkerCountForTests => _validMarkerCount;
        public int PlacedMarkerCountForTests => _placedMarkerCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoaded()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAfterInitialSceneLoad()
        {
            EnsureActiveConstructionForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureActiveConstructionForScene(scene);
        }

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
            _placementTilemap = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
            gameObject.SetActive(true);
            Refresh();
        }

        public void BindCompletionForTests(string saveSlot, HouseUpgradeStageDefinition stage)
        {
            BindActiveConstruction(string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot, stage);
        }

        public void CompleteForTests()
        {
            Complete();
        }

        public bool TryPlaceForTests(Vector2Int cell, HouseConstructionCellKind kind)
        {
            bool placed = _session != null && _session.TryPlace(cell, kind);
            if (placed)
            {
                SaveProgress();
            }

            Refresh();
            return placed;
        }

        private static void EnsureActiveConstructionForScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != "House")
            {
                return;
            }

            var saveSlot = !string.IsNullOrEmpty(ActiveSaveContext.SlotId) ? ActiveSaveContext.SlotId : "default";
            var state = HouseStatePersistence.Load(saveSlot);
            if (string.IsNullOrEmpty(state.ActiveConstructionStageId))
            {
                return;
            }

            var stage = LoadDefaultStage();
            if (stage == null || stage.Id != state.ActiveConstructionStageId)
            {
                return;
            }

            var overlay = FindFirstObjectByType<HouseConstructionOverlay>(FindObjectsInactive.Include) ?? Create();
            overlay.BindActiveConstruction(saveSlot, stage);
        }

        private static HouseUpgradeStageDefinition LoadDefaultStage()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<HouseUpgradeStageDefinition>(DefaultStageAssetPath);
#else
            return null;
#endif
        }

        private void BindActiveConstruction(string saveSlot, HouseUpgradeStageDefinition stage)
        {
            _saveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            _stage = stage;
            if (_stage == null || _stage.Blueprint == null)
            {
                return;
            }

            Bind(_stage.Blueprint);
            var state = HouseStatePersistence.Load(_saveSlot);
            RestorePlacedCells(state.PlacedConstructionCells);
            Refresh();
        }

        private void RestorePlacedCells(HouseConstructionCellSaveData[] placedCells)
        {
            if (_session == null || placedCells == null)
            {
                return;
            }

            for (int i = 0; i < placedCells.Length; i++)
            {
                var placed = placedCells[i];
                _session.TryPlace(new Vector2Int(placed.X, placed.Y), placed.Kind);
            }
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                _leftMouseWasPressed = false;
                return;
            }

            bool pressed = mouse.leftButton.isPressed;
            if (pressed && !_leftMouseWasPressed)
            {
                TryPlaceFromScreen(mouse.position.ReadValue());
            }

            _leftMouseWasPressed = pressed;
        }

        private bool TryPlaceFromScreen(Vector2 screenPosition)
        {
            if (_blueprint == null || _session == null)
            {
                return false;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            if (_placementTilemap == null)
            {
                _placementTilemap = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
            }

            var world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(camera.transform.position.z)));
            var cell3 = _placementTilemap != null ? _placementTilemap.WorldToCell(world) : Vector3Int.RoundToInt(world);
            var cell = new Vector2Int(cell3.x, cell3.y);
            var required = _blueprint.RequiredCells;
            for (int i = 0; i < required.Count; i++)
            {
                if (required[i].Cell != cell)
                {
                    continue;
                }

                bool placed = _session.TryPlace(cell, required[i].Kind);
                if (placed)
                {
                    SaveProgress();
                    if (_feedbackText != null) _feedbackText.text = "Placed";
                }

                Refresh();
                return placed;
            }

            if (_feedbackText != null) _feedbackText.text = "Invalid";
            Refresh();
            return false;
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
            rect.sizeDelta = new Vector2(480f, 126f);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            return go.GetComponent<HouseConstructionOverlay>();
        }

        private void Build()
        {
            _progressText = MakeText("ConstructionProgressText", new Vector2(0f, 36f), new Vector2(380f, 28f));
            _feedbackText = MakeText("ConstructionFeedbackText", new Vector2(0f, 10f), new Vector2(380f, 24f));
            _completeButton = MakeButton("CompleteConstructionButton", "Complete", new Vector2(-90f, -38f));
            _cancelButton = MakeButton("CancelConstructionButton", "Cancel", new Vector2(90f, -38f));
            _markerRoot = new GameObject("ConstructionMarkers", typeof(RectTransform)).transform;
            _markerRoot.SetParent(transform, false);
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

        private void SaveProgress()
        {
            if (string.IsNullOrEmpty(_saveSlot) || _stage == null || _session == null)
            {
                return;
            }

            var state = HouseStatePersistence.Load(_saveSlot);
            state.ActiveConstructionStageId = _stage.Id;
            state.PlacedConstructionCells = _session.ToSaveData();
            HouseStatePersistence.Save(_saveSlot, state);
        }

        private void Refresh()
        {
            int placed = _session != null ? _session.ToSaveData().Length : 0;
            int required = _blueprint != null ? _blueprint.RequiredCells.Count : 0;
            _progressText.text = placed + "/" + required;
            _completeButton.interactable = _session != null && _session.IsComplete;
            RefreshMarkers();
        }

        private void RefreshMarkers()
        {
            _validMarkerCount = 0;
            _placedMarkerCount = 0;
            if (_markerRoot == null || _blueprint == null)
            {
                return;
            }

            for (int i = _markerRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_markerRoot.GetChild(i).gameObject);
            }

            var placedCells = new HashSet<Vector2Int>();
            if (_session != null)
            {
                var saved = _session.ToSaveData();
                for (int i = 0; i < saved.Length; i++)
                {
                    placedCells.Add(new Vector2Int(saved[i].X, saved[i].Y));
                }
            }

            var required = _blueprint.RequiredCells;
            for (int i = 0; i < required.Count; i++)
            {
                bool isPlaced = placedCells.Contains(required[i].Cell);
                MakeMarker(required[i].Cell, isPlaced);
                _validMarkerCount++;
                if (isPlaced) _placedMarkerCount++;
            }
        }

        private void MakeMarker(Vector2Int cell, bool placed)
        {
            var go = new GameObject((placed ? "PlacedConstructionMarker_" : "ValidConstructionMarker_") + cell.x + "_" + cell.y, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_markerRoot, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(14f, 14f);
            rect.anchoredPosition = new Vector2(-42f + cell.x * 24f, -2f + cell.y * 8f);
            go.GetComponent<Image>().color = placed ? new Color(0.3f, 0.95f, 0.45f, 0.9f) : new Color(1f, 0.85f, 0.2f, 0.8f);
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